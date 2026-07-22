using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace PixelPatchStudio
{
    internal static class UiTheme
    {
        public static readonly Color Window = Color.FromArgb(8, 10, 10);
        public static readonly Color Header = Color.FromArgb(13, 15, 15);
        public static readonly Color Sidebar = Color.FromArgb(11, 13, 13);
        public static readonly Color Workspace = Color.FromArgb(6, 8, 8);
        public static readonly Color Canvas = Color.FromArgb(9, 11, 12);
        public static readonly Color Card = Color.FromArgb(18, 20, 20);
        public static readonly Color CardBorder = Color.FromArgb(39, 43, 41);
        public static readonly Color Field = Color.FromArgb(23, 26, 25);
        public static readonly Color FieldHover = Color.FromArgb(31, 35, 33);
        public static readonly Color Accent = Color.FromArgb(151, 196, 36);
        public static readonly Color AccentBright = Color.FromArgb(194, 229, 71);
        public static readonly Color AccentDark = Color.FromArgb(111, 150, 24);
        public static readonly Color Text = Color.FromArgb(234, 237, 232);
        public static readonly Color Muted = Color.FromArgb(151, 158, 151);
        public static readonly Color Subtle = Color.FromArgb(102, 109, 103);
        public static readonly Color Success = Color.FromArgb(158, 207, 44);
        public static readonly Color Danger = Color.FromArgb(245, 111, 126);

        public static GraphicsPath RoundedRectangle(RectangleF bounds, float radius)
        {
            float diameter = Math.Max(1f, radius * 2f);
            GraphicsPath path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal sealed class ModernButton : Button
    {
        private bool hovered;
        private bool accent;
        private bool selected;

        public bool Accent
        {
            get { return accent; }
            set { accent = value; Invalidate(); }
        }

        public bool SelectedState
        {
            get { return selected; }
            set { selected = value; Invalidate(); }
        }

        public ModernButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            UseVisualStyleBackColor = false;
            Cursor = Cursors.Hand;
            Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Regular);
            ForeColor = UiTheme.Text;
            Height = 38;
            TabStop = true;
        }

        protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hovered = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            RectangleF bounds = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            bool active = accent || selected;
            Color start;
            Color end;
            Color border;
            if (!Enabled)
            {
                start = end = Color.FromArgb(23, 26, 25);
                border = Color.FromArgb(36, 40, 38);
            }
            else if (active)
            {
                start = hovered ? UiTheme.AccentBright : UiTheme.Accent;
                end = hovered ? Color.FromArgb(214, 239, 111) : UiTheme.AccentBright;
                border = Color.Transparent;
            }
            else
            {
                start = end = hovered ? UiTheme.FieldHover : UiTheme.Field;
                border = hovered ? Color.FromArgb(78, 86, 77) : UiTheme.CardBorder;
            }

            using (GraphicsPath path = UiTheme.RoundedRectangle(bounds, 8f))
            using (LinearGradientBrush fill = new LinearGradientBrush(bounds, start, end, LinearGradientMode.Horizontal))
            using (Pen outline = new Pen(border, 1f))
            {
                e.Graphics.FillPath(fill, path);
                if (border.A > 0) e.Graphics.DrawPath(outline, path);
            }

            Color textColor = !Enabled ? Color.FromArgb(90, 96, 91) : active ? Color.FromArgb(15, 18, 14) : UiTheme.Text;
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, textColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            if (Focused && ShowFocusCues)
            {
                Rectangle focus = Rectangle.Inflate(ClientRectangle, -4, -4);
                ControlPaint.DrawFocusRectangle(e.Graphics, focus, Color.White, Color.Transparent);
            }
        }
    }

    internal sealed class ModernCheckBox : CheckBox
    {
        private bool hovered;

        public ModernCheckBox()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                ControlStyles.Selectable | ControlStyles.SupportsTransparentBackColor, true);
            AutoCheck = true;
            ThreeState = false;
            Cursor = Cursors.Hand;
            BackColor = Color.Transparent;
            ForeColor = UiTheme.Text;
            Font = new Font("Microsoft YaHei UI", 9f);
            TabStop = true;
        }

        protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hovered = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnCheckedChanged(EventArgs e) { base.OnCheckedChanged(e); Invalidate(); }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

        internal void DebugPerformClick()
        {
            OnClick(EventArgs.Empty);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaintBackground(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            int boxSize = Math.Max(14, Math.Min(Math.Max(14, Height - 4), (int)Math.Round(Font.Height * 0.92f)));
            Rectangle box = new Rectangle(1, Math.Max(1, (Height - boxSize) / 2), boxSize, boxSize);
            Color fill = !Enabled ? Color.FromArgb(24, 27, 26) : Checked ? UiTheme.AccentBright : UiTheme.Field;
            Color border = !Enabled ? Color.FromArgb(55, 60, 56) : Checked ? UiTheme.AccentBright : hovered ? UiTheme.Muted : Color.FromArgb(94, 101, 95);
            float radius = Math.Max(2f, boxSize * 0.18f);
            using (GraphicsPath path = UiTheme.RoundedRectangle(box, radius))
            using (Brush background = new SolidBrush(fill))
            using (Pen outline = new Pen(border, Math.Max(1f, boxSize / 14f)))
            {
                e.Graphics.FillPath(background, path);
                e.Graphics.DrawPath(outline, path);
            }

            if (Checked)
            {
                PointF first = new PointF(box.Left + box.Width * 0.24f, box.Top + box.Height * 0.52f);
                PointF middle = new PointF(box.Left + box.Width * 0.43f, box.Top + box.Height * 0.70f);
                PointF last = new PointF(box.Left + box.Width * 0.77f, box.Top + box.Height * 0.31f);
                using (Pen check = new Pen(Color.FromArgb(24, 29, 19), Math.Max(1.8f, boxSize / 7f)))
                {
                    check.StartCap = check.EndCap = LineCap.Round;
                    check.LineJoin = LineJoin.Round;
                    e.Graphics.DrawLines(check, new[] { first, middle, last });
                }
            }

            Rectangle textBounds = new Rectangle(box.Right + Math.Max(7, Font.Height / 2), 0,
                Math.Max(0, Width - box.Right - Math.Max(7, Font.Height / 2)), Height);
            Color textColor = Enabled ? ForeColor : UiTheme.Subtle;
            TextRenderer.DrawText(e.Graphics, Text, Font, textBounds, textColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);

            if (Focused && ShowFocusCues)
            {
                Rectangle focus = Rectangle.Inflate(ClientRectangle, -1, -2);
                ControlPaint.DrawFocusRectangle(e.Graphics, focus, UiTheme.AccentBright, Color.Transparent);
            }
        }
    }

    internal sealed class CardPanel : Panel
    {
        public CardPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = UiTheme.Card;
        }

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            if (Width < 2 || Height < 2) return;
            using (GraphicsPath path = UiTheme.RoundedRectangle(new RectangleF(0, 0, Width, Height), 12f))
            {
                Region old = Region;
                Region = new Region(path);
                if (old != null) old.Dispose();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = UiTheme.RoundedRectangle(new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f), 11.5f))
            using (Pen border = new Pen(UiTheme.CardBorder, 1f)) e.Graphics.DrawPath(border, path);
        }
    }

    internal sealed class RoundedLabel : Control
    {
        public Color FillColor = UiTheme.Field;
        public Color TextColor = UiTheme.Muted;
        public float Radius = 8f;

        public RoundedLabel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Font = new Font("Microsoft YaHei UI", 8.5f, FontStyle.Bold);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = UiTheme.RoundedRectangle(new RectangleF(0, 0, Width - 1f, Height - 1f), Radius))
            using (Brush fill = new SolidBrush(FillColor)) e.Graphics.FillPath(fill, path);
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, TextColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
        }
    }

    internal sealed class BrandMark : Control
    {
        public BrandMark()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Size = new Size(42, 42);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            float inset = Math.Max(0.5f, Math.Min(Width, Height) * 0.025f);
            BrandAssets.DrawLogo(e.Graphics, new RectangleF(inset, inset, Width - inset * 2f, Height - inset * 2f));
        }
    }

    internal sealed class BrandGradientLine : Control
    {
        public BrandGradientLine()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = UiTheme.Header;
            Height = 3;
            TabStop = false;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (Width < 4 || Height < 1) return;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            RectangleF gradientBounds = new RectangleF(1f, 0f, Width - 2f, Math.Max(1f, Height));
            using (LinearGradientBrush gradient = new LinearGradientBrush(gradientBounds, Color.Black, Color.Black, LinearGradientMode.Horizontal))
            {
                gradient.InterpolationColors = new ColorBlend
                {
                    Positions = new[] { 0f, 0.07f, 0.14f, 0.34f, 0.52f, 0.70f, 0.88f, 1f },
                    Colors = new[]
                    {
                        Color.FromArgb(35, 105, 118),
                        Color.FromArgb(105, 78, 31),
                        Color.FromArgb(47, 43, 63),
                        Color.FromArgb(82, 42, 126),
                        Color.FromArgb(136, 45, 105),
                        Color.FromArgb(70, 55, 137),
                        Color.FromArgb(58, 104, 47),
                        Color.FromArgb(123, 126, 45)
                    }
                };
                using (Pen pen = new Pen(gradient, 1.6f))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    float y = Height / 2f;
                    e.Graphics.DrawLine(pen, 2f, y, Width - 3f, y);
                }
            }
        }
    }

    internal enum NavIconKind
    {
        Source,
        Mask,
        Generate,
        Result,
        Settings
    }

    internal sealed class NavIconButton : Control
    {
        private bool hovered;
        private bool selected;
        public readonly NavIconKind Kind;

        public bool SelectedState
        {
            get { return selected; }
            set { selected = value; Invalidate(); }
        }

        public NavIconButton(NavIconKind kind)
        {
            Kind = kind;
            Size = new Size(54, 54);
            Cursor = Cursors.Hand;
            TabStop = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hovered = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            if (selected || hovered)
            {
                using (Brush fill = new SolidBrush(selected ? Color.FromArgb(27, 31, 25) : Color.FromArgb(20, 23, 22))) e.Graphics.FillRectangle(fill, ClientRectangle);
            }
            if (selected)
            {
                using (Brush accent = new SolidBrush(UiTheme.AccentBright)) e.Graphics.FillRectangle(accent, 0, 8, 3, Height - 16);
            }

            Color color = selected ? UiTheme.AccentBright : hovered ? UiTheme.Text : UiTheme.Muted;
            using (Pen pen = new Pen(color, 1.8f))
            {
                pen.StartCap = pen.EndCap = LineCap.Round;
                DrawIcon(e.Graphics, pen);
            }
        }

        private void DrawIcon(Graphics graphics, Pen pen)
        {
            float cx = Width / 2f;
            float cy = Height / 2f;
            if (Kind == NavIconKind.Source)
            {
                graphics.DrawRectangle(pen, cx - 11, cy - 8, 22, 16);
                graphics.DrawLine(pen, cx - 8, cy - 4, cx - 2, cy - 4);
                graphics.DrawLine(pen, cx - 2, cy - 4, cx + 1, cy - 1);
                graphics.DrawLine(pen, cx + 1, cy - 1, cx + 8, cy - 1);
            }
            else if (Kind == NavIconKind.Mask)
            {
                graphics.DrawEllipse(pen, cx - 10, cy - 10, 20, 20);
                graphics.DrawArc(pen, cx - 6, cy - 6, 12, 12, 35, 245);
                graphics.DrawLine(pen, cx + 5, cy + 7, cx + 11, cy + 13);
            }
            else if (Kind == NavIconKind.Generate)
            {
                PointF[] star = new[] { new PointF(cx, cy - 12), new PointF(cx + 3, cy - 3), new PointF(cx + 12, cy), new PointF(cx + 3, cy + 3), new PointF(cx, cy + 12), new PointF(cx - 3, cy + 3), new PointF(cx - 12, cy), new PointF(cx - 3, cy - 3) };
                graphics.DrawPolygon(pen, star);
                graphics.DrawLine(pen, cx + 9, cy - 10, cx + 9, cy - 5);
                graphics.DrawLine(pen, cx + 6.5f, cy - 7.5f, cx + 11.5f, cy - 7.5f);
            }
            else if (Kind == NavIconKind.Result)
            {
                graphics.DrawRectangle(pen, cx - 10, cy - 8, 17, 17);
                graphics.DrawRectangle(pen, cx - 5, cy - 12, 17, 17);
                graphics.DrawLine(pen, cx - 6, cy + 5, cx + 2, cy - 3);
            }
            else
            {
                graphics.DrawEllipse(pen, cx - 5, cy - 5, 10, 10);
                graphics.DrawEllipse(pen, cx - 11, cy - 11, 22, 22);
                for (int i = 0; i < 8; i++)
                {
                    double angle = i * Math.PI / 4d;
                    graphics.DrawLine(pen, cx + (float)Math.Cos(angle) * 11f, cy + (float)Math.Sin(angle) * 11f, cx + (float)Math.Cos(angle) * 14f, cy + (float)Math.Sin(angle) * 14f);
                }
            }
        }
    }

    internal sealed class AccentProgressBar : Control
    {
        private readonly Timer timer;
        private int offset;

        public AccentProgressBar()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Height = 4;
            timer = new Timer();
            timer.Interval = 30;
            timer.Tick += delegate { offset = (offset + 12) % Math.Max(1, Width + 90); Invalidate(); };
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (Visible) timer.Start(); else timer.Stop();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (Brush track = new SolidBrush(UiTheme.Field)) e.Graphics.FillRectangle(track, ClientRectangle);
            Rectangle bar = new Rectangle(offset - 90, 0, 90, Height);
            using (LinearGradientBrush fill = new LinearGradientBrush(bar, UiTheme.Accent, UiTheme.AccentBright, LinearGradientMode.Horizontal)) e.Graphics.FillRectangle(fill, bar);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) timer.Dispose();
            base.Dispose(disposing);
        }
    }

    internal sealed class ModernTrackBar : Control
    {
        private int minimum;
        private int maximum = 100;
        private int current;
        private bool dragging;

        public event EventHandler ValueChanged;

        public int Minimum
        {
            get { return minimum; }
            set { minimum = value; if (maximum <= minimum) maximum = minimum + 1; Value = current; Invalidate(); }
        }

        public int Maximum
        {
            get { return maximum; }
            set { maximum = Math.Max(minimum + 1, value); Value = current; Invalidate(); }
        }

        public int Value
        {
            get { return current; }
            set
            {
                int next = Math.Max(minimum, Math.Min(maximum, value));
                if (next == current) return;
                current = next;
                if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
                Invalidate();
            }
        }

        public ModernTrackBar()
        {
            Height = 26;
            TabStop = true;
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        }

        protected override void OnMouseDown(MouseEventArgs e) { dragging = true; Focus(); SetFromMouse(e.X); base.OnMouseDown(e); }
        protected override void OnMouseMove(MouseEventArgs e) { if (dragging) SetFromMouse(e.X); base.OnMouseMove(e); }
        protected override void OnMouseUp(MouseEventArgs e) { dragging = false; base.OnMouseUp(e); }
        protected override bool IsInputKey(Keys keyData) { Keys key = keyData & Keys.KeyCode; return key == Keys.Left || key == Keys.Right || base.IsInputKey(keyData); }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Left) { Value--; e.Handled = true; }
            else if (e.KeyCode == Keys.Right) { Value++; e.Handled = true; }
            base.OnKeyDown(e);
        }

        private void SetFromMouse(int x)
        {
            float amount = Math.Max(0f, Math.Min(1f, (x - 8f) / Math.Max(1f, Width - 16f)));
            Value = minimum + (int)Math.Round(amount * (maximum - minimum));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            float amount = (current - minimum) / (float)Math.Max(1, maximum - minimum);
            float left = 8f;
            float right = Width - 8f;
            float y = Height / 2f;
            float x = left + (right - left) * amount;
            using (Pen track = new Pen(Color.FromArgb(57, 63, 58), 3f))
            using (Pen fill = new Pen(UiTheme.Accent, 3f))
            using (Brush thumb = new SolidBrush(UiTheme.AccentBright))
            {
                track.StartCap = track.EndCap = LineCap.Round;
                fill.StartCap = fill.EndCap = LineCap.Round;
                e.Graphics.DrawLine(track, left, y, right, y);
                e.Graphics.DrawLine(fill, left, y, x, y);
                e.Graphics.FillEllipse(thumb, x - 5f, y - 5f, 10f, 10f);
            }
        }
    }
}
