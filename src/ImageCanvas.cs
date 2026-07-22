using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PixelPatchStudio
{
    internal sealed class ImageCanvas : Control
    {
        private sealed class MaskTile
        {
            public Rectangle Bounds;
            public Bitmap Pixels;

            public void Dispose()
            {
                if (Pixels != null) { Pixels.Dispose(); Pixels = null; }
            }
        }

        private sealed class MaskHistoryEntry
        {
            public Bitmap FullMask;
            public List<MaskTile> Tiles;

            public void Dispose()
            {
                if (FullMask != null) { FullMask.Dispose(); FullMask = null; }
                if (Tiles == null) return;
                foreach (MaskTile tile in Tiles) tile.Dispose();
                Tiles.Clear();
            }
        }

        private const int HistoryTileSize = 256;
        private const int InteractivePreviewMaxEdge = 2048;
        private Bitmap source;
        private Bitmap preview;
        private Bitmap mask;
        private Bitmap overlay;
        private Bitmap viewCache;
        private Bitmap overlayViewCache;
        private Bitmap interactiveSource;
        private Bitmap interactivePreview;
        private Bitmap interactiveOverlay;
        private readonly Stack<MaskHistoryEntry> undo = new Stack<MaskHistoryEntry>();
        private readonly Stack<MaskHistoryEntry> redo = new Stack<MaskHistoryEntry>();
        private readonly Timer interactionSettleTimer;
        private Dictionary<long, MaskTile> activeStrokeTiles;
        private PaintMode activeStrokeMode;
        private float zoom = 1f;
        private PointF pan;
        private PointF lastImagePoint;
        private Point lastMouse;
        private bool painting;
        private bool panning;
        private bool spaceDown;
        private bool maskHasContent;
        private PaintMode mode = PaintMode.Add;
        private int brushSize = 80;
        private bool showMaskOverlay = true;
        private bool fastInteraction;

        public event EventHandler ViewChanged;
        public event EventHandler MaskChanged;
        public event EventHandler BrushChanged;

        public ImageCanvas()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            BackColor = Color.FromArgb(18, 21, 28);
            TabStop = true;
            interactionSettleTimer = new Timer { Interval = 90 };
            interactionSettleTimer.Tick += delegate
            {
                interactionSettleTimer.Stop();
                if (!fastInteraction) return;
                fastInteraction = false;
                InvalidateViewCache();
            };
        }

        public bool HasImage { get { return source != null; } }
        public bool ShowingPreview { get { return preview != null; } }
        public bool ShowMaskOverlay
        {
            get { return showMaskOverlay; }
            set
            {
                if (showMaskOverlay == value) return;
                showMaskOverlay = value;
                Invalidate();
            }
        }
        public float Zoom { get { return zoom; } }
        public PaintMode Mode
        {
            get { return mode; }
            set
            {
                if (mode == value) return;
                Rectangle oldCursor = BrushPreviewBounds(lastMouse);
                mode = value;
                if (!oldCursor.IsEmpty) Invalidate(oldCursor);
            }
        }
        public int BrushSize
        {
            get { return brushSize; }
            set
            {
                int next = Math.Max(2, Math.Min(1000, value));
                if (next == brushSize) return;
                Rectangle dirty = BrushPreviewBounds(lastMouse);
                brushSize = next;
                if (BrushChanged != null) BrushChanged(this, EventArgs.Empty);
                dirty = Rectangle.Union(dirty, BrushPreviewBounds(lastMouse));
                if (!dirty.IsEmpty) Invalidate(dirty);
            }
        }

        public void SetImage(Bitmap image)
        {
            DisposeImages();
            source = new Bitmap(image);
            interactiveSource = BuildInteractiveBitmap(source, InteractivePreviewMaxEdge, InterpolationMode.HighQualityBicubic);
            mask = NewMask(source.Width, source.Height);
            overlay = NewOverlay(source.Width, source.Height);
            interactiveOverlay = NewOverlay(interactiveSource.Width, interactiveSource.Height);
            maskHasContent = false;
            ClearHistory(undo);
            ClearHistory(redo);
            InvalidateViewCache(false);
            if (IsHandleCreated) BeginInvoke(new Action(FitToWindow));
            else FitToWindow();
            Invalidate();
        }

        public Bitmap GetSourceCopy() { return source == null ? null : new Bitmap(source); }
        public Bitmap GetMaskCopy() { return mask == null ? null : new Bitmap(mask); }

        public void SetPreview(Bitmap value)
        {
            if (preview == null && value == null) return;
            if (preview != null) { preview.Dispose(); preview = null; }
            if (interactivePreview != null) { interactivePreview.Dispose(); interactivePreview = null; }
            if (value != null) preview = new Bitmap(value);
            if (preview != null && interactiveSource != null) interactivePreview = ResizeBitmap(preview, interactiveSource.Width, interactiveSource.Height, InterpolationMode.HighQualityBicubic);
            InvalidateBaseViewCache();
        }

        internal void DebugPaintStroke(PointF from, PointF to, int segments, PaintMode strokeMode)
        {
            if (mask == null) throw new InvalidOperationException("测试笔刷前需要载入图片。");
            EnsureViewCache();
            BeginStrokeHistory();
            ClearHistory(redo);
            activeStrokeMode = strokeMode;
            PointF previous = from;
            int count = Math.Max(1, segments);
            for (int i = 1; i <= count; i++)
            {
                float amount = i / (float)count;
                PointF current = new PointF(from.X + (to.X - from.X) * amount, from.Y + (to.Y - from.Y) * amount);
                DrawStroke(previous, current, strokeMode);
                previous = current;
            }
            CommitStrokeHistory();
        }

        internal Color DebugMaskPixel(int x, int y)
        {
            return mask.GetPixel(x, y);
        }

        internal int DebugUndoTileCount
        {
            get
            {
                if (undo.Count == 0 || undo.Peek().Tiles == null) return 0;
                return undo.Peek().Tiles.Count;
            }
        }

        internal void DebugZoomAt(Point location, int delta)
        {
            ApplyZoom(location, delta);
        }

        internal void DebugFinishInteraction()
        {
            CancelFastInteraction();
            InvalidateViewCache();
        }

        public void SetMask(Bitmap value)
        {
            if (source == null) return;
            if (maskHasContent) SaveFullUndo();
            if (mask != null) mask.Dispose();
            if (overlay != null) overlay.Dispose();
            mask = ResizeMask(value, source.Width, source.Height);
            overlay = BuildOverlay(mask);
            RebuildInteractiveOverlay();
            maskHasContent = true;
            ClearHistory(redo);
            RaiseMaskChanged();
            InvalidateViewCache();
        }

        public void ClearMask()
        {
            if (mask == null) return;
            if (maskHasContent) SaveFullUndo();
            using (Graphics graphics = Graphics.FromImage(mask)) graphics.Clear(Color.Black);
            using (Graphics graphics = Graphics.FromImage(overlay))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.Clear(Color.Transparent);
            }
            if (interactiveOverlay != null)
            {
                using (Graphics graphics = Graphics.FromImage(interactiveOverlay))
                {
                    graphics.CompositingMode = CompositingMode.SourceCopy;
                    graphics.Clear(Color.Transparent);
                }
            }
            maskHasContent = false;
            ClearHistory(redo);
            RaiseMaskChanged();
            InvalidateViewCache();
        }

        public void InvertMask()
        {
            if (mask == null) return;
            if (maskHasContent) SaveFullUndo();
            for (int y = 0; y < mask.Height; y++)
            {
                for (int x = 0; x < mask.Width; x++)
                {
                    int value = 255 - mask.GetPixel(x, y).R;
                    mask.SetPixel(x, y, Color.FromArgb(255, value, value, value));
                }
            }
            if (overlay != null) overlay.Dispose();
            overlay = BuildOverlay(mask);
            RebuildInteractiveOverlay();
            maskHasContent = true;
            ClearHistory(redo);
            RaiseMaskChanged();
            InvalidateViewCache();
        }

        public void UndoMask()
        {
            if (mask == null || undo.Count == 0) return;
            MaskHistoryEntry entry = undo.Pop();
            PushHistory(redo, CaptureCurrent(entry));
            ApplyHistory(entry);
            entry.Dispose();
        }

        public void RedoMask()
        {
            if (mask == null || redo.Count == 0) return;
            MaskHistoryEntry entry = redo.Pop();
            PushHistory(undo, CaptureCurrent(entry));
            ApplyHistory(entry);
            entry.Dispose();
        }

        public void FitToWindow()
        {
            if (source == null || ClientSize.Width < 1 || ClientSize.Height < 1) return;
            float zx = (ClientSize.Width - 48f) / source.Width;
            float zy = (ClientSize.Height - 48f) / source.Height;
            zoom = Math.Max(0.02f, Math.Min(32f, Math.Min(zx, zy)));
            pan = new PointF((ClientSize.Width - source.Width * zoom) / 2f, (ClientSize.Height - source.Height * zoom) / 2f);
            RaiseViewChanged();
            InvalidateViewCache();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (source == null)
            {
                e.Graphics.Clear(BackColor);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                float cardWidth = Math.Min(430f, Math.Max(300f, Width - 100f));
                RectangleF card = new RectangleF((Width - cardWidth) / 2f, Height / 2f - 105f, cardWidth, 210f);
                using (GraphicsPath cardPath = UiTheme.RoundedRectangle(card, 12f))
                using (Brush cardFill = new SolidBrush(Color.FromArgb(14, 17, 17)))
                using (Pen cardBorder = new Pen(UiTheme.CardBorder, 1f))
                using (Pen icon = new Pen(UiTheme.Accent, 2f))
                using (Font title = new Font("Microsoft YaHei UI", 15f, FontStyle.Bold))
                using (Font caption = new Font("Microsoft YaHei UI", 9f))
                using (Font code = new Font("Segoe UI", 7.5f, FontStyle.Bold))
                using (Brush primary = new SolidBrush(UiTheme.Text))
                using (Brush secondary = new SolidBrush(UiTheme.Muted))
                using (Brush accent = new SolidBrush(UiTheme.AccentBright))
                {
                    e.Graphics.FillPath(cardFill, cardPath);
                    e.Graphics.DrawPath(cardBorder, cardPath);
                    RectangleF imageIcon = new RectangleF(card.X + card.Width / 2f - 25f, card.Y + 27f, 50f, 40f);
                    e.Graphics.DrawRectangle(icon, imageIcon.X, imageIcon.Y, imageIcon.Width, imageIcon.Height);
                    e.Graphics.DrawEllipse(icon, imageIcon.X + 9f, imageIcon.Y + 8f, 7f, 7f);
                    e.Graphics.DrawLines(icon, new[] { new PointF(imageIcon.X + 6f, imageIcon.Bottom - 7f), new PointF(imageIcon.X + 19f, imageIcon.Y + 23f), new PointF(imageIcon.X + 27f, imageIcon.Y + 30f), new PointF(imageIcon.X + 36f, imageIcon.Y + 18f), new PointF(imageIcon.Right - 5f, imageIcon.Bottom - 7f) });
                    string a = "从 Photoshop 获取当前图片";
                    string b = "或在“图像来源”中打开本地 PNG / JPG";
                    SizeF sa = e.Graphics.MeasureString(a, title);
                    SizeF sb = e.Graphics.MeasureString(b, caption);
                    e.Graphics.DrawString(a, title, primary, card.X + (card.Width - sa.Width) / 2f, card.Y + 86f);
                    e.Graphics.DrawString(b, caption, secondary, card.X + (card.Width - sb.Width) / 2f, card.Y + 124f);
                    string formats = "PHOTOSHOP   ·   PNG   ·   JPG   ·   TIFF";
                    SizeF sf = e.Graphics.MeasureString(formats, code);
                    e.Graphics.DrawString(formats, code, accent, card.X + (card.Width - sf.Width) / 2f, card.Y + 164f);
                }
                return;
            }

            if (fastInteraction && interactiveSource != null)
            {
                RenderInteractiveView(e.Graphics, e.ClipRectangle);
            }
            else
            {
                EnsureViewCache();
                Rectangle paintArea = Rectangle.Intersect(e.ClipRectangle, ClientRectangle);
                e.Graphics.CompositingMode = CompositingMode.SourceCopy;
                e.Graphics.DrawImage(viewCache, paintArea, paintArea, GraphicsUnit.Pixel);
                if (ShowMaskOverlay && overlayViewCache != null)
                {
                    e.Graphics.CompositingMode = CompositingMode.SourceOver;
                    e.Graphics.DrawImage(overlayViewCache, paintArea, paintArea, GraphicsUnit.Pixel);
                }
            }

            if (!panning && ClientRectangle.Contains(lastMouse))
            {
                float diameter = Math.Max(2f, brushSize * zoom);
                RectangleF circle = new RectangleF(lastMouse.X - diameter / 2f, lastMouse.Y - diameter / 2f, diameter, diameter);
                using (Pen shadow = new Pen(Color.FromArgb(190, 0, 0, 0), 3f)) e.Graphics.DrawEllipse(shadow, circle);
                using (Pen previewPen = new Pen(mode == PaintMode.Add ? Color.White : Color.FromArgb(255, 255, 120, 120), 1f)) e.Graphics.DrawEllipse(previewPen, circle);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            lastMouse = e.Location;
            if (source == null) return;
            if (e.Button == MouseButtons.Middle || (e.Button == MouseButtons.Left && spaceDown))
            {
                panning = true;
                Cursor = Cursors.Hand;
                return;
            }
            if (e.Button == MouseButtons.Left || e.Button == MouseButtons.Right)
            {
                ShowMaskOverlay = true;
                BeginStrokeHistory();
                ClearHistory(redo);
                painting = true;
                activeStrokeMode = e.Button == MouseButtons.Right ? PaintMode.Erase : mode;
                lastImagePoint = ScreenToImage(e.Location);
                DrawStroke(lastImagePoint, lastImagePoint, activeStrokeMode);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Point oldMouse = lastMouse;
            lastMouse = e.Location;
            if (panning)
            {
                pan.X += e.X - oldMouse.X;
                pan.Y += e.Y - oldMouse.Y;
                RaiseViewChanged();
                ScheduleFastInteraction();
                return;
            }
            if (painting)
            {
                PointF current = ScreenToImage(e.Location);
                DrawStroke(lastImagePoint, current, activeStrokeMode);
                lastImagePoint = current;
            }
            else InvalidateCursorMove(oldMouse, lastMouse);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (painting)
            {
                CommitStrokeHistory();
                RaiseMaskChanged();
            }
            painting = false;
            panning = false;
            Cursor = Cursors.Default;
            Invalidate(BrushPreviewBounds(lastMouse));
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            Point oldMouse = lastMouse;
            lastMouse = new Point(-10000, -10000);
            Invalidate(BrushPreviewBounds(oldMouse));
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (source == null) return;
            if ((ModifierKeys & Keys.Alt) == Keys.Alt)
            {
                BrushSize += e.Delta > 0 ? 8 : -8;
                return;
            }
            ApplyZoom(e.Location, e.Delta);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode;
            if (key == Keys.Space || key == Keys.OemOpenBrackets || key == Keys.OemCloseBrackets) return true;
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Space) { spaceDown = true; Cursor = Cursors.Hand; e.Handled = true; }
            else if (e.KeyCode == Keys.B) { Mode = PaintMode.Add; e.Handled = true; }
            else if (e.KeyCode == Keys.E) { Mode = PaintMode.Erase; e.Handled = true; }
            else if (e.KeyCode == Keys.OemOpenBrackets) { BrushSize -= Math.Max(2, BrushSize / 10); e.Handled = true; }
            else if (e.KeyCode == Keys.OemCloseBrackets) { BrushSize += Math.Max(2, BrushSize / 10); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.D0) { FitToWindow(); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.Z) { UndoMask(); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.Y) { RedoMask(); e.Handled = true; }
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            base.OnKeyUp(e);
            if (e.KeyCode == Keys.Space) { spaceDown = false; if (!panning) Cursor = Cursors.Default; }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                DisposeImages();
                interactionSettleTimer.Dispose();
            }
            base.Dispose(disposing);
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            InvalidateViewCache();
        }

        private void DrawStroke(PointF from, PointF to, PaintMode strokeMode)
        {
            if (mask == null) return;
            CaptureStrokeTiles(from, to);
            Color maskColor = strokeMode == PaintMode.Add ? Color.White : Color.Black;
            Color overlayColor = strokeMode == PaintMode.Add ? Color.FromArgb(105, 52, 184, 255) : Color.Transparent;
            using (Graphics maskGraphics = Graphics.FromImage(mask))
            using (Graphics overlayGraphics = Graphics.FromImage(overlay))
            using (Pen maskPen = new Pen(maskColor, brushSize))
            using (Pen overlayPen = new Pen(overlayColor, brushSize))
            {
                maskGraphics.SmoothingMode = SmoothingMode.AntiAlias;
                overlayGraphics.SmoothingMode = SmoothingMode.AntiAlias;
                maskGraphics.CompositingMode = CompositingMode.SourceCopy;
                overlayGraphics.CompositingMode = CompositingMode.SourceCopy;
                maskPen.StartCap = maskPen.EndCap = LineCap.Round;
                overlayPen.StartCap = overlayPen.EndCap = LineCap.Round;
                maskGraphics.DrawLine(maskPen, from, to);
                overlayGraphics.DrawLine(overlayPen, from, to);
            }
            DrawInteractiveStroke(from, to, strokeMode);
            if (strokeMode == PaintMode.Add) maskHasContent = true;
            float inflate = brushSize * zoom / 2f + 6f;
            RectangleF dirty = RectangleF.FromLTRB(Math.Min(from.X, to.X) * zoom + pan.X - inflate, Math.Min(from.Y, to.Y) * zoom + pan.Y - inflate, Math.Max(from.X, to.X) * zoom + pan.X + inflate, Math.Max(from.Y, to.Y) * zoom + pan.Y + inflate);
            Rectangle dirtyPixels = Rectangle.Ceiling(dirty);
            UpdateOverlayViewCache(dirtyPixels);
            Invalidate(dirtyPixels);
        }

        private void SaveFullUndo()
        {
            if (mask == null) return;
            PushHistory(undo, new MaskHistoryEntry { FullMask = new Bitmap(mask) });
        }

        private void BeginStrokeHistory()
        {
            ClearStrokeHistory();
            activeStrokeTiles = new Dictionary<long, MaskTile>();
        }

        private void CaptureStrokeTiles(PointF from, PointF to)
        {
            if (activeStrokeTiles == null || mask == null) return;
            float dx = to.X - from.X;
            float dy = to.Y - from.Y;
            float distance = (float)Math.Sqrt(dx * dx + dy * dy);
            int steps = Math.Max(1, (int)Math.Ceiling(distance / (HistoryTileSize / 2f)));
            float radius = brushSize / 2f + 3f;
            for (int step = 0; step <= steps; step++)
            {
                float amount = step / (float)steps;
                float x = from.X + dx * amount;
                float y = from.Y + dy * amount;
                CaptureTilesInRectangle(Rectangle.Ceiling(RectangleF.FromLTRB(x - radius, y - radius, x + radius, y + radius)));
            }
        }

        private void CaptureTilesInRectangle(Rectangle rectangle)
        {
            rectangle.Intersect(new Rectangle(0, 0, mask.Width, mask.Height));
            if (rectangle.IsEmpty) return;
            int firstX = rectangle.Left / HistoryTileSize;
            int lastX = (rectangle.Right - 1) / HistoryTileSize;
            int firstY = rectangle.Top / HistoryTileSize;
            int lastY = (rectangle.Bottom - 1) / HistoryTileSize;
            for (int tileY = firstY; tileY <= lastY; tileY++)
            {
                for (int tileX = firstX; tileX <= lastX; tileX++)
                {
                    long key = ((long)tileY << 32) | (uint)tileX;
                    if (activeStrokeTiles.ContainsKey(key)) continue;
                    Rectangle bounds = new Rectangle(tileX * HistoryTileSize, tileY * HistoryTileSize,
                        Math.Min(HistoryTileSize, mask.Width - tileX * HistoryTileSize),
                        Math.Min(HistoryTileSize, mask.Height - tileY * HistoryTileSize));
                    activeStrokeTiles.Add(key, new MaskTile { Bounds = bounds, Pixels = mask.Clone(bounds, PixelFormat.Format32bppArgb) });
                }
            }
        }

        private void CommitStrokeHistory()
        {
            if (activeStrokeTiles == null) return;
            if (activeStrokeTiles.Count > 0)
            {
                PushHistory(undo, new MaskHistoryEntry { Tiles = new List<MaskTile>(activeStrokeTiles.Values) });
                activeStrokeTiles.Clear();
            }
            activeStrokeTiles = null;
        }

        private void DrawInteractiveStroke(PointF from, PointF to, PaintMode strokeMode)
        {
            if (interactiveOverlay == null || source == null) return;
            float scaleX = interactiveOverlay.Width / (float)source.Width;
            float scaleY = interactiveOverlay.Height / (float)source.Height;
            float width = Math.Max(1f, brushSize * Math.Min(scaleX, scaleY));
            Color color = strokeMode == PaintMode.Add ? Color.FromArgb(105, 52, 184, 255) : Color.Transparent;
            using (Graphics graphics = Graphics.FromImage(interactiveOverlay))
            using (Pen pen = new Pen(color, width))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.CompositingMode = CompositingMode.SourceCopy;
                pen.StartCap = pen.EndCap = LineCap.Round;
                graphics.DrawLine(pen, from.X * scaleX, from.Y * scaleY, to.X * scaleX, to.Y * scaleY);
            }
        }

        private void ClearStrokeHistory()
        {
            if (activeStrokeTiles == null) return;
            foreach (MaskTile tile in activeStrokeTiles.Values) tile.Dispose();
            activeStrokeTiles.Clear();
            activeStrokeTiles = null;
        }

        private MaskHistoryEntry CaptureCurrent(MaskHistoryEntry template)
        {
            if (template.FullMask != null) return new MaskHistoryEntry { FullMask = new Bitmap(mask) };
            List<MaskTile> tiles = new List<MaskTile>();
            if (template.Tiles != null)
            {
                foreach (MaskTile tile in template.Tiles)
                {
                    tiles.Add(new MaskTile { Bounds = tile.Bounds, Pixels = mask.Clone(tile.Bounds, PixelFormat.Format32bppArgb) });
                }
            }
            return new MaskHistoryEntry { Tiles = tiles };
        }

        private void ApplyHistory(MaskHistoryEntry entry)
        {
            if (entry.FullMask != null)
            {
                if (mask != null) mask.Dispose();
                mask = new Bitmap(entry.FullMask);
                if (overlay != null) overlay.Dispose();
                overlay = BuildOverlay(mask);
            }
            else if (entry.Tiles != null)
            {
                using (Graphics maskGraphics = Graphics.FromImage(mask))
                using (Graphics overlayGraphics = Graphics.FromImage(overlay))
                {
                    maskGraphics.CompositingMode = CompositingMode.SourceCopy;
                    overlayGraphics.CompositingMode = CompositingMode.SourceCopy;
                    foreach (MaskTile tile in entry.Tiles)
                    {
                        maskGraphics.DrawImageUnscaled(tile.Pixels, tile.Bounds.Location);
                        using (Bitmap overlayTile = BuildOverlay(tile.Pixels)) overlayGraphics.DrawImageUnscaled(overlayTile, tile.Bounds.Location);
                    }
                }
            }
            maskHasContent = true;
            RebuildInteractiveOverlay();
            RaiseMaskChanged();
            InvalidateOverlayViewCache();
        }

        private static void PushHistory(Stack<MaskHistoryEntry> history, MaskHistoryEntry entry)
        {
            history.Push(entry);
            if (history.Count <= 20) return;
            MaskHistoryEntry[] entries = history.ToArray();
            history.Clear();
            for (int i = 19; i >= 0; i--) history.Push(entries[i]);
            for (int i = 20; i < entries.Length; i++) entries[i].Dispose();
        }

        private static void ClearHistory(Stack<MaskHistoryEntry> history)
        {
            foreach (MaskHistoryEntry entry in history) entry.Dispose();
            history.Clear();
        }

        private static Bitmap NewMask(int width, int height)
        {
            Bitmap result = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using (Graphics graphics = Graphics.FromImage(result)) graphics.Clear(Color.Black);
            return result;
        }

        private static Bitmap NewOverlay(int width, int height)
        {
            Bitmap result = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using (Graphics graphics = Graphics.FromImage(result))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.Clear(Color.Transparent);
            }
            return result;
        }

        private static Bitmap ResizeMask(Bitmap input, int width, int height)
        {
            Bitmap result = NewMask(width, height);
            using (Graphics graphics = Graphics.FromImage(result))
            {
                graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
                graphics.PixelOffsetMode = PixelOffsetMode.Half;
                graphics.DrawImage(input, new Rectangle(0, 0, width, height), 0, 0, input.Width, input.Height, GraphicsUnit.Pixel);
            }
            return result;
        }

        private static Bitmap BuildInteractiveBitmap(Bitmap input, int maxEdge, InterpolationMode interpolationMode)
        {
            float scale = Math.Min(1f, maxEdge / (float)Math.Max(input.Width, input.Height));
            int width = Math.Max(1, (int)Math.Round(input.Width * scale));
            int height = Math.Max(1, (int)Math.Round(input.Height * scale));
            return ResizeBitmap(input, width, height, interpolationMode);
        }

        private static Bitmap ResizeBitmap(Bitmap input, int width, int height, InterpolationMode interpolationMode)
        {
            Bitmap result = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using (Graphics graphics = Graphics.FromImage(result))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.CompositingQuality = CompositingQuality.HighSpeed;
                graphics.InterpolationMode = interpolationMode;
                graphics.PixelOffsetMode = interpolationMode == InterpolationMode.NearestNeighbor ? PixelOffsetMode.Half : PixelOffsetMode.HighQuality;
                graphics.DrawImage(input, new Rectangle(0, 0, width, height), 0, 0, input.Width, input.Height, GraphicsUnit.Pixel);
            }
            return result;
        }

        internal static Bitmap BuildOverlay(Bitmap selectionMask)
        {
            Bitmap result = NewOverlay(selectionMask.Width, selectionMask.Height);
            Rectangle rect = new Rectangle(0, 0, selectionMask.Width, selectionMask.Height);
            Bitmap normalized = selectionMask.PixelFormat == PixelFormat.Format32bppArgb
                ? selectionMask
                : ResizeMask(selectionMask, selectionMask.Width, selectionMask.Height);
            BitmapData inputData = normalized.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            BitmapData outputData = result.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                int inputStride = Math.Abs(inputData.Stride);
                int outputStride = Math.Abs(outputData.Stride);
                byte[] input = new byte[selectionMask.Width * 4];
                byte[] output = new byte[selectionMask.Width * 4];
                for (int y = 0; y < selectionMask.Height; y++)
                {
                    IntPtr inputRow = IntPtr.Add(inputData.Scan0, y * inputStride);
                    IntPtr outputRow = IntPtr.Add(outputData.Scan0, y * outputStride);
                    Marshal.Copy(inputRow, input, 0, input.Length);
                    for (int x = 0; x < selectionMask.Width; x++)
                    {
                        int sourceIndex = x * 4;
                        int targetIndex = sourceIndex;
                        int selected = Math.Max(input[sourceIndex], Math.Max(input[sourceIndex + 1], input[sourceIndex + 2]));
                        output[targetIndex] = 255;
                        output[targetIndex + 1] = 184;
                        output[targetIndex + 2] = 52;
                        output[targetIndex + 3] = (byte)((selected * 105 + 127) / 255);
                    }
                    Marshal.Copy(output, 0, outputRow, output.Length);
                }
            }
            finally
            {
                normalized.UnlockBits(inputData);
                result.UnlockBits(outputData);
                if (!object.ReferenceEquals(normalized, selectionMask)) normalized.Dispose();
            }
            return result;
        }

        private Rectangle BrushPreviewBounds(Point point)
        {
            if (source == null || point.X < -1000 || point.Y < -1000) return Rectangle.Empty;
            int diameter = (int)Math.Ceiling(Math.Max(2f, brushSize * zoom));
            int radius = diameter / 2 + 6;
            return Rectangle.FromLTRB(point.X - radius, point.Y - radius, point.X + radius + 1, point.Y + radius + 1);
        }

        private void InvalidateCursorMove(Point oldPoint, Point newPoint)
        {
            Rectangle dirty = Rectangle.Union(BrushPreviewBounds(oldPoint), BrushPreviewBounds(newPoint));
            if (!dirty.IsEmpty) Invalidate(dirty);
        }

        private void EnsureViewCache()
        {
            if (ClientSize.Width < 1 || ClientSize.Height < 1) return;
            if (viewCache == null || viewCache.Width != ClientSize.Width || viewCache.Height != ClientSize.Height)
            {
                if (viewCache != null) viewCache.Dispose();
                viewCache = new Bitmap(ClientSize.Width, ClientSize.Height, PixelFormat.Format32bppArgb);
                using (Graphics graphics = Graphics.FromImage(viewCache)) RenderBaseView(graphics, ClientRectangle);
            }
            if (overlayViewCache == null || overlayViewCache.Width != ClientSize.Width || overlayViewCache.Height != ClientSize.Height)
            {
                if (overlayViewCache != null) overlayViewCache.Dispose();
                overlayViewCache = new Bitmap(ClientSize.Width, ClientSize.Height, PixelFormat.Format32bppArgb);
                using (Graphics graphics = Graphics.FromImage(overlayViewCache)) RenderOverlayView(graphics);
            }
        }

        private void RenderInteractiveView(Graphics graphics, Rectangle clip)
        {
            GraphicsState state = graphics.Save();
            try
            {
                graphics.SetClip(clip);
                graphics.CompositingMode = CompositingMode.SourceCopy;
                using (Brush background = new SolidBrush(BackColor)) graphics.FillRectangle(background, clip);
                Bitmap baseImage = interactivePreview ?? interactiveSource;
                if (baseImage == null) return;
                RectangleF target = ImageRectangle();
                graphics.CompositingQuality = CompositingQuality.HighSpeed;
                graphics.SmoothingMode = SmoothingMode.None;
                graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
                graphics.PixelOffsetMode = PixelOffsetMode.Half;
                graphics.DrawImage(baseImage, target, new RectangleF(0f, 0f, baseImage.Width, baseImage.Height), GraphicsUnit.Pixel);
                if (ShowMaskOverlay && maskHasContent && interactiveOverlay != null)
                {
                    graphics.CompositingMode = CompositingMode.SourceOver;
                    graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
                    graphics.DrawImage(interactiveOverlay, target, new RectangleF(0f, 0f, interactiveOverlay.Width, interactiveOverlay.Height), GraphicsUnit.Pixel);
                }
                graphics.CompositingMode = CompositingMode.SourceOver;
                using (Pen border = new Pen(Color.FromArgb(90, 255, 255, 255), 1f)) graphics.DrawRectangle(border, target.X, target.Y, target.Width, target.Height);
            }
            finally
            {
                graphics.Restore(state);
            }
        }

        private void ScheduleFastInteraction()
        {
            if (interactiveSource == null)
            {
                InvalidateViewCache();
                return;
            }
            fastInteraction = true;
            interactionSettleTimer.Stop();
            interactionSettleTimer.Start();
            Invalidate();
        }

        private void ApplyZoom(Point location, int delta)
        {
            if (source == null || delta == 0) return;
            PointF before = ScreenToImage(location);
            float factor = (float)Math.Pow(1.15d, delta / 120d);
            zoom = Math.Max(0.02f, Math.Min(32f, zoom * factor));
            pan = new PointF(location.X - before.X * zoom, location.Y - before.Y * zoom);
            RaiseViewChanged();
            ScheduleFastInteraction();
        }

        private void CancelFastInteraction()
        {
            interactionSettleTimer.Stop();
            fastInteraction = false;
        }

        private void RebuildInteractiveOverlay()
        {
            if (interactiveOverlay != null) { interactiveOverlay.Dispose(); interactiveOverlay = null; }
            if (overlay == null || interactiveSource == null) return;
            interactiveOverlay = ResizeBitmap(overlay, interactiveSource.Width, interactiveSource.Height, InterpolationMode.NearestNeighbor);
        }

        private void UpdateOverlayViewCache(Rectangle dirty)
        {
            if (overlayViewCache == null || overlayViewCache.Width != ClientSize.Width || overlayViewCache.Height != ClientSize.Height) return;
            dirty.Intersect(ClientRectangle);
            if (dirty.IsEmpty) return;
            using (Graphics graphics = Graphics.FromImage(overlayViewCache))
            using (Brush clear = new SolidBrush(Color.Transparent))
            {
                graphics.SetClip(dirty);
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.FillRectangle(clear, dirty);
                graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
                graphics.PixelOffsetMode = PixelOffsetMode.Half;
                graphics.DrawImage(overlay, ImageRectangle());
            }
        }

        private void RenderBaseView(Graphics graphics, Rectangle clip)
        {
            GraphicsState state = graphics.Save();
            try
            {
                graphics.SetClip(clip);
                using (Brush background = new SolidBrush(BackColor)) graphics.FillRectangle(background, clip);
                if (source == null) return;
                RectangleF target = ImageRectangle();
                Bitmap baseImage = preview ?? source;
                Bitmap screenProxy = interactivePreview ?? interactiveSource;
                if (screenProxy != null && target.Width <= screenProxy.Width * 1.1f && target.Height <= screenProxy.Height * 1.1f) baseImage = screenProxy;
                graphics.CompositingMode = CompositingMode.SourceOver;
                graphics.InterpolationMode = zoom >= 1f ? InterpolationMode.NearestNeighbor : InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.Half;
                graphics.DrawImage(baseImage, target);
                using (Pen border = new Pen(Color.FromArgb(90, 255, 255, 255), 1f)) graphics.DrawRectangle(border, target.X, target.Y, target.Width, target.Height);
            }
            finally
            {
                graphics.Restore(state);
            }
        }

        private void RenderOverlayView(Graphics graphics)
        {
            graphics.CompositingMode = CompositingMode.SourceCopy;
            graphics.Clear(Color.Transparent);
            if (overlay == null) return;
            Bitmap displayOverlay = overlay;
            RectangleF target = ImageRectangle();
            if (interactiveOverlay != null && target.Width <= interactiveOverlay.Width * 1.1f && target.Height <= interactiveOverlay.Height * 1.1f) displayOverlay = interactiveOverlay;
            graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            graphics.PixelOffsetMode = PixelOffsetMode.Half;
            graphics.DrawImage(displayOverlay, target);
        }

        private void InvalidateViewCache()
        {
            InvalidateViewCache(true);
        }

        private void InvalidateViewCache(bool repaint)
        {
            CancelFastInteraction();
            if (viewCache != null)
            {
                viewCache.Dispose();
                viewCache = null;
            }
            if (overlayViewCache != null)
            {
                overlayViewCache.Dispose();
                overlayViewCache = null;
            }
            if (repaint) Invalidate();
        }

        private void InvalidateOverlayViewCache()
        {
            CancelFastInteraction();
            if (overlayViewCache != null)
            {
                overlayViewCache.Dispose();
                overlayViewCache = null;
            }
            Invalidate();
        }

        private void InvalidateBaseViewCache()
        {
            CancelFastInteraction();
            if (viewCache != null)
            {
                viewCache.Dispose();
                viewCache = null;
            }
            Invalidate();
        }

        private RectangleF ImageRectangle()
        {
            return new RectangleF(pan.X, pan.Y, source.Width * zoom, source.Height * zoom);
        }

        private PointF ScreenToImage(Point point)
        {
            return new PointF(Math.Max(0, Math.Min(source.Width - 1, (point.X - pan.X) / zoom)), Math.Max(0, Math.Min(source.Height - 1, (point.Y - pan.Y) / zoom)));
        }

        private void DisposeImages()
        {
            CancelFastInteraction();
            if (source != null) { source.Dispose(); source = null; }
            if (preview != null) { preview.Dispose(); preview = null; }
            if (mask != null) { mask.Dispose(); mask = null; }
            if (overlay != null) { overlay.Dispose(); overlay = null; }
            if (viewCache != null) { viewCache.Dispose(); viewCache = null; }
            if (overlayViewCache != null) { overlayViewCache.Dispose(); overlayViewCache = null; }
            if (interactiveSource != null) { interactiveSource.Dispose(); interactiveSource = null; }
            if (interactivePreview != null) { interactivePreview.Dispose(); interactivePreview = null; }
            if (interactiveOverlay != null) { interactiveOverlay.Dispose(); interactiveOverlay = null; }
            ClearStrokeHistory();
            ClearHistory(undo);
            ClearHistory(redo);
        }

        private void RaiseViewChanged() { if (ViewChanged != null) ViewChanged(this, EventArgs.Empty); }
        private void RaiseMaskChanged() { if (MaskChanged != null) MaskChanged(this, EventArgs.Empty); }
    }
}
