using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PixelPatchStudio
{
    internal sealed class MainForm : Form
    {
        private readonly SettingsStore store = new SettingsStore();
        private readonly PhotoshopBridge photoshop = new PhotoshopBridge();
        private readonly RealEsrganService esrgan = new RealEsrganService();
        private readonly string tempDirectory;
        private AppSettings settings;
        private PhotoshopImage photoshopSource;
        private Bitmap generated;
        private Bitmap composite;
        private Bitmap referenceImage;
        private string referenceImageName;
        private CancellationTokenSource cancellation;

        private ImageCanvas canvas;
        private Panel sidebar;
        private Panel[] toolPages;
        private NavIconButton[] navButtons;
        private ComboBox provider;
        private TextBox prompt;
        private PictureBox referencePreview;
        private Label referenceState;
        private Button selectReferenceButton;
        private Button clearReferenceButton;
        private ModernTrackBar brush;
        private Label brushValue;
        private Label status;
        private Label imageInfo;
        private Control zoomInfo;
        private AccentProgressBar progress;
        private Button generateButton;
        private Button cancelButton;
        private Button addButton;
        private Button eraseButton;
        private Button previewButton;
        private Button pushButton;
        private Button saveButton;
        private CheckBox useEsrgan;
        private Button installEsrganButton;
        private Label esrganState;

        public MainForm()
        {
            settings = store.Load();
            tempDirectory = Path.Combine(store.DataDirectory, "Temp");
            Directory.CreateDirectory(tempDirectory);
            BuildUi();
            UiScale.Apply(this, settings.UiScalePercent, false);
            canvas.BrushSize = settings.BrushSize;
            UpdateBrushUi();
            UpdateProviderUi();
            UpdateRealEsrganUi();
            Shown += delegate { canvas.Focus(); };
            FormClosed += OnClosed;
        }

        private void BuildUi()
        {
            Text = "PhotoSense 1.0.16 · Photoshop AI 局部重绘";
            Icon = AppIcon.Create();
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1440, 900);
            WindowState = FormWindowState.Maximized;
            MinimumSize = new Size(1160, 720);
            BackColor = UiTheme.Window;
            ForeColor = UiTheme.Text;
            Font = new Font("Microsoft YaHei UI", 9f);
            KeyPreview = true;
            AutoScaleMode = AutoScaleMode.Dpi;

            BrandGradientLine accentLine = new BrandGradientLine { Dock = DockStyle.Top };
            Controls.Add(accentLine);
            Panel top = new Panel { Dock = DockStyle.Top, Height = 48, BackColor = UiTheme.Header };
            Label brand = new Label { Text = "PHOTOSENSE", Font = new Font("Microsoft YaHei UI", 12f, FontStyle.Bold), ForeColor = UiTheme.Text, AutoSize = true, Location = new Point(18, 12), BackColor = Color.Transparent };
            Label subtitle = new Label { Text = "Get Good Get PhotoSense.", Font = new Font("Segoe UI", 7.5f, FontStyle.Regular), ForeColor = UiTheme.Subtle, AutoSize = true, Location = new Point(142, 17), BackColor = Color.Transparent };
            RoundedLabel version = new RoundedLabel { Text = "V1.0.16", Size = new Size(68, 22), Location = new Point(390, 13), FillColor = Color.FromArgb(28, 33, 27), TextColor = UiTheme.AccentBright, Radius = 5f };
            Label keyHint = new Label { Text = "B  画笔    E  橡皮    [ ]  笔刷    空格  平移", Dock = DockStyle.Right, Width = 340, TextAlign = ContentAlignment.MiddleRight, Padding = new Padding(0, 0, 18, 0), ForeColor = UiTheme.Subtle, Font = new Font("Microsoft YaHei UI", 8f), BackColor = Color.Transparent };
            top.Controls.Add(keyHint); top.Controls.Add(version); top.Controls.Add(subtitle); top.Controls.Add(brand);
            Controls.Add(top);

            Panel bottom = new Panel { Dock = DockStyle.Bottom, Height = 38, BackColor = UiTheme.Header, Padding = new Padding(14, 7, 12, 7) };
            Label statusDot = new Label { Text = "●", Dock = DockStyle.Left, Width = 18, TextAlign = ContentAlignment.MiddleCenter, ForeColor = UiTheme.Success };
            status = new Label { Text = "就绪", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = UiTheme.Muted, AutoEllipsis = true };
            zoomInfo = new RoundedLabel { Text = "—", Dock = DockStyle.Right, Width = 76, FillColor = UiTheme.Field, TextColor = UiTheme.Text, Radius = 5f };
            bottom.Controls.Add(status); bottom.Controls.Add(statusDot); bottom.Controls.Add(zoomInfo);
            Controls.Add(bottom);

            Panel toolsHost = new Panel { Dock = DockStyle.Left, Width = 382, BackColor = UiTheme.Sidebar };
            Panel navRail = new Panel { Dock = DockStyle.Left, Width = 58, BackColor = Color.FromArgb(8, 10, 10) };
            Panel railBorder = new Panel { Dock = DockStyle.Right, Width = 1, BackColor = UiTheme.CardBorder };
            navRail.Controls.Add(railBorder);
            BrandMark mark = new BrandMark { Location = new Point(9, 10), Size = new Size(40, 40) };
            navRail.Controls.Add(mark);
            ToolTip tips = new ToolTip { InitialDelay = 250, ReshowDelay = 100, AutoPopDelay = 5000 };
            navButtons = new[]
            {
                new NavIconButton(NavIconKind.Source),
                new NavIconButton(NavIconKind.Mask),
                new NavIconButton(NavIconKind.Generate),
                new NavIconButton(NavIconKind.Result)
            };
            string[] navNames = { "图像来源", "重绘蒙版", "AI 生成", "使用结果" };
            for (int i = 0; i < navButtons.Length; i++)
            {
                int pageIndex = i;
                navButtons[i].Location = new Point(2, 70 + i * 58);
                navButtons[i].Click += delegate { ShowToolPage(pageIndex); };
                tips.SetToolTip(navButtons[i], navNames[i]);
                navRail.Controls.Add(navButtons[i]);
            }
            NavIconButton settingsButton = new NavIconButton(NavIconKind.Settings) { Anchor = AnchorStyles.Left | AnchorStyles.Bottom, Location = new Point(2, toolsHost.Height - 64) };
            settingsButton.Click += OpenSettings;
            tips.SetToolTip(settingsButton, "设置");
            navRail.Controls.Add(settingsButton);

            sidebar = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.Sidebar };
            Panel pageBorder = new Panel { Dock = DockStyle.Right, Width = 1, BackColor = UiTheme.CardBorder };
            sidebar.Controls.Add(pageBorder);
            toolsHost.Controls.Add(sidebar);
            toolsHost.Controls.Add(navRail);
            Controls.Add(toolsHost);

            Panel workspace = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.Workspace, Padding = new Padding(14) };
            CardPanel canvasFrame = new CardPanel { Dock = DockStyle.Fill, BackColor = UiTheme.CardBorder, Padding = new Padding(1) };
            canvas = new ImageCanvas { Dock = DockStyle.Fill, BackColor = UiTheme.Canvas };
            canvas.ViewChanged += delegate { zoomInfo.Text = Math.Round(canvas.Zoom * 100) + "%"; };
            canvas.MaskChanged += delegate { ClearGeneratedResult(); status.Text = "蒙版已更新，可开始生成"; };
            canvas.BrushChanged += delegate { UpdateBrushUi(); };
            canvasFrame.Controls.Add(canvas);
            workspace.Controls.Add(canvasFrame);
            Controls.Add(workspace);
            workspace.BringToFront();

            toolPages = new[]
            {
                BuildSourcePage(),
                BuildMaskPage(),
                BuildGeneratePage(),
                BuildResultPage()
            };
            foreach (Panel page in toolPages) sidebar.Controls.Add(page);
            ShowToolPage(0);
        }

        private Panel BuildSourcePage()
        {
            Panel page = ToolPage("SOURCE", "图像来源", "从 Photoshop 当前文档或本地文件开始");
            Button fromPs = ButtonOf("获取 Photoshop 当前图片", true);
            fromPs.SetBounds(16, 104, 292, 42);
            fromPs.Click += LoadFromPhotoshop;
            Button fromFile = ButtonOf("打开本地图片", false);
            fromFile.SetBounds(16, 154, 292, 38);
            fromFile.Click += LoadFromFile;

            Label documentLabel = SectionCaption("当前文档");
            documentLabel.Location = new Point(16, 218);
            imageInfo = new Label
            {
                Text = "尚未载入图片",
                Location = new Point(16, 244),
                Size = new Size(292, 76),
                BackColor = UiTheme.Field,
                ForeColor = UiTheme.Muted,
                Padding = new Padding(12, 10, 10, 8),
                Font = new Font("Microsoft YaHei UI", 9f),
                AutoEllipsis = true
            };
            Label help = PageHint("读取 Photoshop 时会保留文档尺寸、DPI 与文档 ID。原文档和原图层不会被覆盖。", 16, 340, 292, 62);
            page.Controls.Add(help); page.Controls.Add(imageInfo); page.Controls.Add(documentLabel); page.Controls.Add(fromFile); page.Controls.Add(fromPs);
            return page;
        }

        private Panel BuildMaskPage()
        {
            Panel page = ToolPage("MASK", "重绘蒙版", "自动识别后，可继续用画笔精确微调");
            Label automatic = SectionCaption("自动识别范围"); automatic.Location = new Point(16, 94);
            Button person = ButtonOf("人物", false); person.SetBounds(16, 120, 92, 36); person.Click += delegate { AutomaticMask(false, "人物"); };
            Button subject = ButtonOf("主体", false); subject.SetBounds(116, 120, 92, 36); subject.Click += delegate { AutomaticMask(false, "主体"); };
            Button background = ButtonOf("背景", false); background.SetBounds(216, 120, 92, 36); background.Click += delegate { AutomaticMask(true, "背景"); };

            Label manual = SectionCaption("手动微调"); manual.Location = new Point(16, 180);
            addButton = ButtonOf("画笔   B", false); addButton.SetBounds(16, 206, 142, 38); addButton.Click += delegate { SetPaintMode(PaintMode.Add); };
            eraseButton = ButtonOf("橡皮   E", false); eraseButton.SetBounds(166, 206, 142, 38); eraseButton.Click += delegate { SetPaintMode(PaintMode.Erase); };
            StyleButton(addButton, true);

            Label brushLabel = SectionCaption("笔刷大小"); brushLabel.Location = new Point(16, 266);
            brushValue = new Label { Location = new Point(238, 262), Size = new Size(70, 24), TextAlign = ContentAlignment.MiddleRight, ForeColor = UiTheme.AccentBright, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
            brush = new ModernTrackBar { Minimum = 2, Maximum = 500, Location = new Point(16, 290), Width = 292 };
            brush.ValueChanged += delegate { canvas.BrushSize = brush.Value; };

            Button invert = ButtonOf("反选", false); invert.SetBounds(16, 340, 92, 34); invert.Click += delegate { canvas.InvertMask(); };
            Button clear = ButtonOf("清空", false); clear.SetBounds(116, 340, 92, 34); clear.Click += delegate { canvas.ClearMask(); };
            Button fit = ButtonOf("适应窗口", false); fit.SetBounds(216, 340, 92, 34); fit.Click += delegate { canvas.FitToWindow(); };
            Label help = PageHint("滚轮缩放 · 空格拖动 · [ ] 调整笔刷\n右键临时擦除 · Ctrl+Z/Y 撤销/重做", 16, 398, 292, 58);

            page.Controls.Add(help); page.Controls.Add(fit); page.Controls.Add(clear); page.Controls.Add(invert); page.Controls.Add(brush); page.Controls.Add(brushValue); page.Controls.Add(brushLabel); page.Controls.Add(eraseButton); page.Controls.Add(addButton); page.Controls.Add(manual); page.Controls.Add(background); page.Controls.Add(subject); page.Controls.Add(person); page.Controls.Add(automatic);
            return page;
        }

        private Panel BuildGeneratePage()
        {
            Panel page = ToolPage("GENERATE", "AI 局部重绘", "描述效果并选择图像生成服务");
            Label promptLabel = SectionCaption("效果描述"); promptLabel.Location = new Point(16, 94);
            prompt = new TextBox
            {
                Location = new Point(16, 120),
                Size = new Size(292, 96),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = UiTheme.Field,
                ForeColor = UiTheme.Text,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Microsoft YaHei UI", 9.5f),
                Text = "重绘选中区域，保持原图的光线、透视和质感自然一致"
            };

            Label referenceLabel = SectionCaption("参考图（可选）"); referenceLabel.Location = new Point(16, 226);
            referencePreview = new PictureBox
            {
                Location = new Point(16, 252),
                Size = new Size(56, 56),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = UiTheme.Field,
                BorderStyle = BorderStyle.FixedSingle
            };
            selectReferenceButton = ButtonOf("选择参考图", false); selectReferenceButton.SetBounds(82, 252, 146, 32); selectReferenceButton.Click += SelectReferenceImage;
            clearReferenceButton = ButtonOf("清除", false); clearReferenceButton.SetBounds(236, 252, 72, 32); clearReferenceButton.Enabled = false; clearReferenceButton.Click += ClearReferenceImageClicked;
            referenceState = new Label
            {
                Text = "未使用参考图；现有请求保持不变",
                Location = new Point(82, 289),
                Size = new Size(226, 34),
                ForeColor = UiTheme.Subtle,
                Font = new Font("Microsoft YaHei UI", 8f),
                AutoEllipsis = true
            };

            Label providerLabel = SectionCaption("生成服务"); providerLabel.Location = new Point(16, 332);
            provider = new ComboBox { Location = new Point(16, 358), Size = new Size(292, 34), DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = UiTheme.Field, ForeColor = UiTheme.Text };
            provider.Items.AddRange(new object[] { "GPT Image 2", "Nano Banana" });
            StyleComboBox(provider);
            provider.SelectedIndexChanged += ProviderChanged;

            useEsrgan = new ModernCheckBox { Text = "生成后使用 Real-ESRGAN 超分", Location = new Point(16, 406), Size = new Size(292, 28) };
            installEsrganButton = ButtonOf("自动部署 Real-ESRGAN", false); installEsrganButton.SetBounds(16, 440, 292, 36); installEsrganButton.Click += InstallRealEsrgan;
            esrganState = new Label { Location = new Point(16, 482), Size = new Size(292, 42), ForeColor = UiTheme.Subtle, Font = new Font("Microsoft YaHei UI", 8f), AutoEllipsis = true };

            generateButton = ButtonOf("开始 AI 局部重绘", true); generateButton.SetBounds(16, 536, 292, 44); generateButton.Font = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold); generateButton.Click += Generate;
            progress = new AccentProgressBar { Location = new Point(16, 588), Width = 292, Visible = false };
            cancelButton = ButtonOf("取消当前任务", false); cancelButton.SetBounds(16, 606, 292, 36); cancelButton.Visible = false; cancelButton.Click += delegate { if (cancellation != null) cancellation.Cancel(); };

            page.Controls.Add(cancelButton); page.Controls.Add(progress); page.Controls.Add(generateButton); page.Controls.Add(esrganState); page.Controls.Add(installEsrganButton); page.Controls.Add(useEsrgan); page.Controls.Add(provider); page.Controls.Add(providerLabel); page.Controls.Add(referenceState); page.Controls.Add(clearReferenceButton); page.Controls.Add(selectReferenceButton); page.Controls.Add(referencePreview); page.Controls.Add(referenceLabel); page.Controls.Add(prompt); page.Controls.Add(promptLabel);
            return page;
        }

        private Panel BuildResultPage()
        {
            Panel page = ToolPage("OUTPUT", "使用结果", "预览、保存或以透明补丁回写 Photoshop");
            previewButton = ButtonOf("切换原图 / 结果预览", false); previewButton.SetBounds(16, 108, 292, 38); previewButton.Enabled = false; previewButton.Click += TogglePreview;
            pushButton = ButtonOf("作为透明补丁送回 Photoshop", true); pushButton.SetBounds(16, 160, 292, 44); pushButton.Enabled = false; pushButton.Click += PushToPhotoshop;
            saveButton = ButtonOf("保存完整合成 PNG", false); saveButton.SetBounds(16, 216, 292, 38); saveButton.Enabled = false; saveButton.Click += SaveComposite;
            Label protection = SectionCaption("无损保护"); protection.Location = new Point(16, 292);
            Label help = PageHint("未选中的区域会在本地强制使用原图像素。送回 Photoshop 的新图层在蒙版外完全透明，不会覆盖原图层。", 16, 320, 292, 88);
            page.Controls.Add(help); page.Controls.Add(protection); page.Controls.Add(saveButton); page.Controls.Add(pushButton); page.Controls.Add(previewButton);
            return page;
        }

        private static Panel ToolPage(string code, string title, string subtitle)
        {
            Panel page = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.Sidebar, Visible = false, AutoScroll = true };
            Label codeLabel = new Label { Text = code, Location = new Point(16, 14), AutoSize = true, ForeColor = UiTheme.Accent, Font = new Font("Segoe UI", 7.5f, FontStyle.Bold) };
            Label titleLabel = new Label { Text = title, Location = new Point(16, 32), Size = new Size(292, 27), ForeColor = UiTheme.Text, Font = new Font("Microsoft YaHei UI", 13f, FontStyle.Bold) };
            Label subtitleLabel = new Label { Text = subtitle, Location = new Point(16, 61), Size = new Size(292, 22), ForeColor = UiTheme.Subtle, Font = new Font("Microsoft YaHei UI", 8f), AutoEllipsis = true };
            Panel line = new Panel { Location = new Point(16, 86), Size = new Size(292, 1), BackColor = UiTheme.CardBorder };
            page.Controls.Add(line); page.Controls.Add(subtitleLabel); page.Controls.Add(titleLabel); page.Controls.Add(codeLabel);
            return page;
        }

        private static Label SectionCaption(string text)
        {
            return new Label { Text = text, Size = new Size(190, 22), ForeColor = UiTheme.Muted, Font = new Font("Microsoft YaHei UI", 8.5f, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft };
        }

        private static Label PageHint(string text, int x, int y, int width, int height)
        {
            return new Label { Text = text, Location = new Point(x, y), Size = new Size(width, height), ForeColor = UiTheme.Subtle, BackColor = UiTheme.Field, Padding = new Padding(10, 8, 8, 6), Font = new Font("Microsoft YaHei UI", 8f) };
        }

        private static void StyleComboBox(ComboBox combo)
        {
            combo.DrawMode = DrawMode.OwnerDrawFixed;
            combo.ItemHeight = 24;
            combo.DrawItem += delegate(object sender, DrawItemEventArgs e)
            {
                if (e.Index < 0) return;
                bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
                using (Brush fill = new SolidBrush(selected ? Color.FromArgb(47, 54, 35) : UiTheme.Field)) e.Graphics.FillRectangle(fill, e.Bounds);
                Color color = selected ? UiTheme.AccentBright : UiTheme.Text;
                TextRenderer.DrawText(e.Graphics, Convert.ToString(combo.Items[e.Index]), combo.Font, Rectangle.Inflate(e.Bounds, -7, 0), color, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                e.DrawFocusRectangle();
            };
        }

        private void ShowToolPage(int index)
        {
            if (toolPages == null || index < 0 || index >= toolPages.Length) return;
            for (int i = 0; i < toolPages.Length; i++)
            {
                toolPages[i].Visible = i == index;
                if (navButtons != null && i < navButtons.Length) navButtons[i].SelectedState = i == index;
            }
            toolPages[index].BringToFront();
            if (index == 1 && canvas != null) canvas.Focus();
        }

        internal void DebugShowToolPage(int index)
        {
            ShowToolPage(index);
        }

        internal void DebugSetRealEsrganChecked(bool value)
        {
            if (useEsrgan != null) useEsrgan.Checked = value;
        }

        private void LoadFromPhotoshop(object sender, EventArgs e)
        {
            try
            {
                SetStatus("正在从 Photoshop 获取当前文档…");
                PhotoshopImage info = photoshop.ExportActiveDocument(tempDirectory);
                using (Image image = Image.FromFile(info.ImagePath)) LoadBitmap(new Bitmap(image), info);
                status.Text = "已连接 Photoshop，可自动选择人物、主体或背景";
            }
            catch (Exception ex) { HandlePhotoshopError(ex); }
        }

        private void LoadFromFile(object sender, EventArgs e)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "打开图片";
                dialog.Filter = "图片文件|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff;*.webp|所有文件|*.*";
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    using (Image image = Image.FromFile(dialog.FileName))
                    {
                        PhotoshopImage info = new PhotoshopImage { DocumentName = Path.GetFileName(dialog.FileName), Width = image.Width, Height = image.Height, DocumentId = 0, ImagePath = dialog.FileName, Resolution = image.HorizontalResolution };
                        LoadBitmap(new Bitmap(image), info);
                    }
                    status.Text = "已打开本地图片；可手动画蒙版（自动选区需从 Photoshop 获取）";
                }
                catch (Exception ex) { ShowError(ex); }
            }
        }

        private void SelectReferenceImage(object sender, EventArgs e)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "选择风格参考图";
                dialog.Filter = "图片文件|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff|所有文件|*.*";
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    Bitmap prepared;
                    using (Image image = Image.FromFile(dialog.FileName))
                    using (Bitmap loaded = new Bitmap(image))
                    {
                        prepared = ImageComposer.PrepareStyleReference(loaded, 1280);
                    }
                    ApplyPreparedReference(prepared, Path.GetFileName(dialog.FileName), true);
                }
                catch (Exception ex)
                {
                    ShowError(new InvalidOperationException("无法读取参考图。请使用有效的 PNG、JPG、BMP 或 TIFF 图片。\n\n" + ex.Message, ex));
                }
            }
        }

        private void ApplyPreparedReference(Bitmap prepared, string displayName, bool notify)
        {
            if (prepared == null) throw new ArgumentNullException("prepared");
            ReleaseReferenceImage(false);
            referenceImage = prepared;
            referenceImageName = string.IsNullOrWhiteSpace(displayName) ? "参考图" : displayName;
            referencePreview.Image = referenceImage;
            referenceState.Text = referenceImageName + " · " + referenceImage.Width + " × " + referenceImage.Height;
            clearReferenceButton.Enabled = true;
            ClearGeneratedResult();
            if (notify) status.Text = "参考图已载入；不会改写效果描述，人物和主体不会从参考图复制";
        }

        internal void DebugSetReferenceImage(Bitmap image, string displayName)
        {
            ApplyPreparedReference(ImageComposer.PrepareStyleReference(image, 1280), displayName, false);
        }

        private void ClearReferenceImageClicked(object sender, EventArgs e)
        {
            ReleaseReferenceImage(true);
        }

        private void ReleaseReferenceImage(bool notify)
        {
            if (referencePreview != null) referencePreview.Image = null;
            if (referenceImage != null) { referenceImage.Dispose(); referenceImage = null; }
            referenceImageName = null;
            if (referenceState != null) referenceState.Text = "未使用参考图；现有请求保持不变";
            if (clearReferenceButton != null) clearReferenceButton.Enabled = false;
            if (notify)
            {
                ClearGeneratedResult();
                status.Text = "参考图已清除；已恢复原有请求方式";
            }
        }

        private void LoadBitmap(Bitmap bitmap, PhotoshopImage info)
        {
            using (bitmap)
            {
                photoshopSource = info;
                ClearGeneratedResult();
                canvas.SetImage(bitmap);
            }
            imageInfo.Text = info.DocumentName + "\n" + info.Width + " × " + info.Height + (info.DocumentId > 0 ? " · Photoshop 已连接" : " · 本地图片");
            canvas.ShowMaskOverlay = true;
            ShowToolPage(1);
        }

        private void AutomaticMask(bool background, string label)
        {
            if (!RequireImage()) return;
            if (photoshopSource == null || photoshopSource.DocumentId == 0)
            {
                MessageBox.Show(this, "自动选区会调用 Photoshop 的“选择主体”。请先使用“从 Photoshop 获取”。", "自动选区", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            try
            {
                SetStatus("Photoshop 正在识别" + label + "…");
                string path = photoshop.ExportAutomaticMask(tempDirectory, photoshopSource.DocumentId, background);
                using (Image image = Image.FromFile(path))
                using (Bitmap mask = new Bitmap(image))
                {
                    if (mask.Width != photoshopSource.Width || mask.Height != photoshopSource.Height) throw new InvalidDataException("Photoshop 蒙版尺寸与原图不一致，请重新获取文档。");
                    double coverage = ImageComposer.SelectionCoverage(mask);
                    if (coverage <= 0.002 || coverage >= 0.998)
                    {
                        MessageBox.Show(this, "Photoshop 这次没有识别出有效的" + label + "范围（覆盖率 " + Math.Round(coverage * 100, 1) + "%）。已保留原蒙版，请尝试“主体”或手动画笔。", "自动选区", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        status.Text = "自动选区无效，原蒙版未改变";
                        return;
                    }
                    canvas.SetMask(mask);
                    status.Text = label + "已自动选中（覆盖 " + Math.Round(coverage * 100, 1) + "%），可用画笔继续微调";
                }
            }
            catch (Exception ex) { HandlePhotoshopError(ex); }
        }

        private async void Generate(object sender, EventArgs e)
        {
            if (!RequireImage()) return;
            if (string.IsNullOrWhiteSpace(prompt.Text))
            {
                MessageBox.Show(this, "请先描述想重绘成什么效果。", "局部重绘", MessageBoxButtons.OK, MessageBoxIcon.Information);
                prompt.Focus();
                return;
            }
            using (Bitmap check = canvas.GetMaskCopy())
            {
                if (!ImageComposer.HasSelection(check))
                {
                    MessageBox.Show(this, "还没有选中重绘范围。请自动选择，或用画笔涂出区域。", "局部重绘", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
            }

            if (useEsrgan.Checked && esrgan.ResolveExecutable(settings) == null)
            {
                DialogResult install = MessageBox.Show(this, "已勾选 Real-ESRGAN，但当前用户尚未部署。是否现在自动下载并安装？", "Real-ESRGAN", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                if (install != DialogResult.Yes || !await DeployRealEsrganAsync(false)) return;
            }

            string key = store.GetApiKey(settings.SelectedProvider);
            if (string.IsNullOrEmpty(key))
            {
                using (SettingsDialog dialog = new SettingsDialog(store, settings))
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK) return;
                }
                UpdateProviderUi();
                key = store.GetApiKey(settings.SelectedProvider);
                if (string.IsNullOrEmpty(key))
                {
                    MessageBox.Show(this, "需要为当前服务填写 API Key。", "API Key", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            cancellation = new CancellationTokenSource();
            SetBusy(true);
            ClearGeneratedResult();
            try
            {
                using (Bitmap source = canvas.GetSourceCopy())
                using (Bitmap mask = canvas.GetMaskCopy())
                using (ImageApiClient client = new ImageApiClient(settings.ApiTimeoutSeconds))
                {
                    status.Text = "正在优化接口图片并调用 " + ProviderDescription() + "…";
                    Bitmap apiResult = await client.GenerateAsync(source, mask, referenceImage, prompt.Text, settings, key, cancellation.Token);
                    string resolutionWarning = ImageApiClient.GeminiResolutionWarning(settings, apiResult.Width, apiResult.Height);
                    if (!string.IsNullOrEmpty(resolutionWarning))
                    {
                        status.Text = "中转返回的图片低于所选 Nano Banana 分辨率";
                        MessageBox.Show(this, resolutionWarning, "Nano Banana 分辨率未生效", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                    if (useEsrgan.Checked)
                    {
                        status.Text = "正在使用 Real-ESRGAN 超分辨率…";
                        Bitmap upscaled = await esrgan.UpscaleAsync(apiResult, settings, tempDirectory, cancellation.Token);
                        apiResult.Dispose();
                        apiResult = upscaled;
                    }
                    generated = apiResult;
                    composite = ImageComposer.Composite(source, generated, mask);
                    canvas.ShowMaskOverlay = false;
                    canvas.SetPreview(composite);
                }
                previewButton.Text = "查看原图";
                previewButton.Enabled = true;
                pushButton.Enabled = photoshopSource != null && photoshopSource.DocumentId > 0;
                saveButton.Enabled = true;
                status.Text = "局部重绘完成；蒙版外使用原图，送回 Photoshop 时蒙版外完全透明";
                ShowToolPage(3);
            }
            catch (OperationCanceledException)
            {
                status.Text = "任务已取消";
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
            finally
            {
                SetBusy(false);
                cancellation.Dispose();
                cancellation = null;
            }
        }

        private void TogglePreview(object sender, EventArgs e)
        {
            if (composite == null) return;
            if (canvas.ShowingPreview)
            {
                canvas.SetPreview(null);
                canvas.ShowMaskOverlay = false;
                previewButton.Text = "查看结果";
            }
            else
            {
                canvas.SetPreview(composite);
                canvas.ShowMaskOverlay = false;
                previewButton.Text = "查看原图";
            }
        }

        private void PushToPhotoshop(object sender, EventArgs e)
        {
            if (generated == null || photoshopSource == null || photoshopSource.DocumentId == 0) return;
            try
            {
                string patchDirectory = Path.Combine(store.DataDirectory, "Patches", DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(patchDirectory);
                string patchPath = Path.Combine(patchDirectory, "full-patch.png");
                using (Bitmap mask = canvas.GetMaskCopy())
                using (Bitmap patch = ImageComposer.CreatePatch(generated, mask))
                {
                    float resolution = photoshopSource.Resolution > 0 && photoshopSource.Resolution <= 9600 ? (float)photoshopSource.Resolution : 96f;
                    patch.SetResolution(resolution, resolution);
                    patch.Save(patchPath, ImageFormat.Png);
                    try
                    {
                        photoshop.PlacePatchAsLayer(patchPath, "PhotoSense · AI 局部重绘", photoshopSource.DocumentId);
                        status.Text = "已作为透明补丁智能对象送回 “" + photoshopSource.DocumentName + "”";
                        return;
                    }
                    catch (PhotoshopConnectionException) { throw; }
                    catch (Exception fullImageError)
                    {
                        SetStatus("Photoshop 无法创建整幅补丁图层，正在自动切换为低内存分块回写…");
                        try
                        {
                            string tileDirectory = Path.Combine(patchDirectory, "tiles");
                            List<PatchTile> tiles = ImageComposer.SavePatchTiles(patch, tileDirectory, 2048, resolution);
                            photoshop.PlacePatchTilesAsGroup(tiles, "PhotoSense · AI 局部重绘", photoshopSource.DocumentId);
                            status.Text = "已通过 " + tiles.Count + " 个低内存分块送回 “" + photoshopSource.DocumentName + "”；图层已整理为同一组";
                            return;
                        }
                        catch (Exception tileError)
                        {
                            throw new InvalidOperationException("Photoshop 整图回写失败：" + fullImageError.Message + "\n\n自动分块回写也失败：" + tileError.Message, tileError);
                        }
                    }
                }
            }
            catch (Exception ex) { HandlePhotoshopError(ex); }
        }

        private void SaveComposite(object sender, EventArgs e)
        {
            if (composite == null) return;
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Title = "保存完整合成图";
                dialog.Filter = "PNG 图片|*.png";
                dialog.DefaultExt = "png";
                dialog.FileName = "PhotoSense-Result.png";
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                composite.Save(dialog.FileName, ImageFormat.Png);
                status.Text = "已保存：" + dialog.FileName;
            }
        }

        private void OpenSettings(object sender, EventArgs e)
        {
            int previousScale = settings.UiScalePercent;
            using (SettingsDialog dialog = new SettingsDialog(store, settings))
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    settings = store.Load();
                    UpdateProviderUi();
                    UpdateRealEsrganUi();
                    if (settings.UiScalePercent != previousScale)
                    {
                        status.Text = "UI Scale 已保存，重新启动 PhotoSense 后生效";
                        MessageBox.Show(this, "UI Scale 已保存。为避免当前画布和蒙版发生布局跳动，新比例会在下次启动 PhotoSense 时生效。", "UI Scale", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else status.Text = "设置已保存到当前 Windows 用户";
                }
            }
        }

        private void ProviderChanged(object sender, EventArgs e)
        {
            if (provider.SelectedIndex < 0) return;
            settings.Provider = provider.SelectedIndex == 1 ? "Nano Banana" : "GPT Image 2";
            store.Save(settings);
            status.Text = "当前服务：" + ProviderDescription();
        }

        private string ProviderDescription()
        {
            if (settings.SelectedProvider != ApiProvider.NanoBanana)
                return settings.Provider + " · " + settings.Model;
            string imageSize = GeminiResolution.Normalize(settings.GeminiImageSize);
            return settings.Provider + " · " + settings.Model + " · " + (imageSize == "Auto" ? "自动分辨率" : imageSize);
        }

        private void UpdateProviderUi()
        {
            int index = settings.SelectedProvider == ApiProvider.NanoBanana ? 1 : 0;
            if (provider.SelectedIndex != index) provider.SelectedIndex = index;
        }

        private void SetPaintMode(PaintMode mode)
        {
            canvas.Mode = mode;
            StyleButton(addButton, mode == PaintMode.Add);
            StyleButton(eraseButton, mode == PaintMode.Erase);
            status.Text = mode == PaintMode.Add ? "画笔：添加重绘范围（B）" : "橡皮：移除重绘范围（E）";
            canvas.Focus();
        }

        private void UpdateBrushUi()
        {
            if (brush == null || canvas == null) return;
            int shown = Math.Min(brush.Maximum, Math.Max(brush.Minimum, canvas.BrushSize));
            if (brush.Value != shown) brush.Value = shown;
            brushValue.Text = canvas.BrushSize + " px";
            settings.BrushSize = canvas.BrushSize;
        }

        private void ClearGeneratedResult()
        {
            if (generated != null) { generated.Dispose(); generated = null; }
            if (composite != null) { composite.Dispose(); composite = null; }
            if (canvas != null)
            {
                canvas.SetPreview(null);
                canvas.ShowMaskOverlay = true;
            }
            if (previewButton != null) { previewButton.Enabled = false; previewButton.Text = "查看原图"; }
            if (pushButton != null) pushButton.Enabled = false;
            if (saveButton != null) saveButton.Enabled = false;
        }

        private bool RequireImage()
        {
            if (canvas.HasImage) return true;
            MessageBox.Show(this, "请先从 Photoshop 获取当前图片，或打开本地图片。", "PhotoSense", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return false;
        }

        private async void InstallRealEsrgan(object sender, EventArgs e)
        {
            await DeployRealEsrganAsync(true);
        }

        private async Task<bool> DeployRealEsrganAsync(bool showSuccess)
        {
            if (cancellation != null) return false;
            cancellation = new CancellationTokenSource();
            SetBusy(true);
            try
            {
                Progress<InstallProgress> installProgress = new Progress<InstallProgress>(delegate(InstallProgress value)
                {
                    status.Text = value.Message + (value.Percent > 0 && value.Percent < 100 ? " · " + value.Percent + "%" : "");
                    if (esrganState != null) esrganState.Text = value.Message;
                });
                string executable = await esrgan.InstallAsync(store.DataDirectory, settings.RealEsrganDownloadUrl, installProgress, cancellation.Token);
                settings.RealEsrganPath = executable;
                store.Save(settings);
                useEsrgan.Checked = true;
                UpdateRealEsrganUi();
                status.Text = "Real-ESRGAN 已部署到当前 Windows 用户目录";
                if (showSuccess)
                {
                    MessageBox.Show(this, "Real-ESRGAN 已自动下载、校验并安装完成。\n\n安装位置：" + Path.GetDirectoryName(executable), "部署完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                return true;
            }
            catch (OperationCanceledException)
            {
                status.Text = "Real-ESRGAN 部署已取消";
                return false;
            }
            catch (Exception ex)
            {
                ShowError(new InvalidOperationException("Real-ESRGAN 自动部署失败。可在“设置”中更换下载 URL 后重试。\n\n" + ex.Message, ex));
                return false;
            }
            finally
            {
                SetBusy(false);
                cancellation.Dispose();
                cancellation = null;
                UpdateRealEsrganUi();
            }
        }

        private void UpdateRealEsrganUi()
        {
            if (installEsrganButton == null || esrganState == null) return;
            string executable = esrgan.ResolveExecutable(settings);
            if (executable == null)
            {
                installEsrganButton.Text = "自动部署 Real-ESRGAN";
                esrganState.Text = "尚未部署；启用超分时也会自动提示安装";
            }
            else
            {
                installEsrganButton.Text = "重新校验 / 修复 Real-ESRGAN";
                esrganState.Text = "已就绪 · " + RealEsrganService.ReleaseVersion + " · 当前用户安装";
            }
        }

        private void SetBusy(bool busy)
        {
            generateButton.Enabled = !busy;
            if (installEsrganButton != null) installEsrganButton.Enabled = !busy;
            if (selectReferenceButton != null) selectReferenceButton.Enabled = !busy;
            if (clearReferenceButton != null) clearReferenceButton.Enabled = !busy && referenceImage != null;
            progress.Visible = busy;
            cancelButton.Visible = busy;
            provider.Enabled = !busy;
        }

        private void SetStatus(string text)
        {
            status.Text = text;
            status.Refresh();
        }

        private void ShowError(Exception ex)
        {
            status.Text = "操作失败";
            string logPath = "";
            try
            {
                string logDirectory = Path.Combine(store.DataDirectory, "Logs");
                Directory.CreateDirectory(logDirectory);
                logPath = Path.Combine(logDirectory, "errors.log");
                File.AppendAllText(logPath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " · PhotoSense 1.0.16\r\n" + ex + "\r\n\r\n");
            }
            catch { }
            string details = ex.Message + (IsPhotoshopStorageError(ex) ? LowDiskWarning() : "") + (string.IsNullOrEmpty(logPath) ? "" : "\n\n诊断记录：" + logPath);
            MessageBox.Show(this, details, "PhotoSense 1.0.16", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private static bool IsPhotoshopStorageError(Exception ex)
        {
            string text = ex == null ? "" : ex.ToString();
            return text.IndexOf("暂存盘", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("scratch disk", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("整图回写", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("分块回写", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("补丁图层", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string LowDiskWarning()
        {
            try
            {
                StringBuilder warning = new StringBuilder();
                foreach (DriveInfo drive in DriveInfo.GetDrives())
                {
                    if (!drive.IsReady || drive.DriveType != DriveType.Fixed || drive.AvailableFreeSpace >= 5L * 1024 * 1024 * 1024) continue;
                    if (warning.Length == 0) warning.Append("\n\n检测到磁盘空间不足：");
                    warning.Append(" ").Append(drive.Name).Append(" 仅剩 ").Append((drive.AvailableFreeSpace / 1073741824d).ToString("0.0")).Append(" GB；");
                }
                if (warning.Length > 0) warning.Append("若该盘是 PSD 所在盘或 Photoshop 暂存盘，请释放至少 20 GB 或在 Photoshop 首选项中更换暂存盘。");
                return warning.ToString();
            }
            catch { return ""; }
        }

        private void HandlePhotoshopError(Exception ex)
        {
            PhotoshopConnectionException connection = ex as PhotoshopConnectionException;
            if (connection != null && connection.ElevationSuggested && Program.TryRestartAsAdministrator(this))
            {
                status.Text = "正在以与 Photoshop 相同的权限重启…";
                return;
            }
            ShowError(ex);
        }

        private void OnClosed(object sender, FormClosedEventArgs e)
        {
            settings.BrushSize = canvas.BrushSize;
            try { store.Save(settings); } catch { }
            if (cancellation != null) cancellation.Cancel();
            ClearGeneratedResult();
            ReleaseReferenceImage(false);
            photoshop.Dispose();
        }

        private static Button ButtonOf(string text, bool accent)
        {
            ModernButton button = new ModernButton { Text = text, Height = 36, Accent = accent, Margin = new Padding(0) };
            return button;
        }

        private static void StyleButton(Button button, bool accent)
        {
            ModernButton modern = button as ModernButton;
            if (modern != null) modern.SelectedState = accent;
        }
    }
}
