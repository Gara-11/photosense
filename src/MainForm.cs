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
    internal sealed partial class MainForm : Form
    {
        private sealed class GenerationVersion : IDisposable
        {
            public string GeneratedPath;
            public Bitmap Thumbnail;
            public string ProviderLabel;
            public string Prompt;
            public DateTime CreatedAt;
            public bool Favorite;
            public GenerationOperation Operation;
            public int DetailProtection = 92;

            public void Dispose()
            {
                if (Thumbnail != null) { Thumbnail.Dispose(); Thumbnail = null; }
                if (!string.IsNullOrEmpty(GeneratedPath))
                {
                    try { File.Delete(GeneratedPath); } catch { }
                    GeneratedPath = null;
                }
            }
        }

        private sealed class GenerationBuild
        {
            public GenerationVersion Version;
            public string ResolutionWarning;
        }

        private readonly SettingsStore store = new SettingsStore();
        private readonly PhotoshopBridge photoshop = new PhotoshopBridge();
        private readonly RealEsrganService esrgan = new RealEsrganService();
        private readonly string tempDirectory;
        private readonly string resultSessionDirectory;
        private AppSettings settings;
        private PhotoshopImage photoshopSource;
        private Bitmap activeGenerated;
        private Bitmap activeComposite;
        private readonly List<GenerationVersion> generationVersions = new List<GenerationVersion>();
        private int activeGenerationIndex = -1;
        private Bitmap referenceImage;
        private string referenceImageName;
        private Bitmap chainedMask;
        private int chainedStepCount;
        private CancellationTokenSource cancellation;

        private ImageCanvas canvas;
        private Panel sidebar;
        private Panel[] toolPages;
        private NavIconButton[] navButtons;
        private ComboBox provider;
        private ComboBox candidateCount;
        private TextBox prompt;
        private PictureBox referencePreview;
        private Label referenceState;
        private Button selectReferenceButton;
        private Button clearReferenceButton;
        private ModernTrackBar brush;
        private Label brushValue;
        private ModernTrackBar refineRadius;
        private Label refineRadiusValue;
        private Label maskCoverage;
        private Button overlayViewButton;
        private Button blackWhiteViewButton;
        private Button outlineViewButton;
        private Label status;
        private Label imageInfo;
        private Control zoomInfo;
        private AccentProgressBar progress;
        private Button generateButton;
        private Button cancelButton;
        private Button addButton;
        private Button eraseButton;
        private Button smartSelectButton;
        private Button freehandLassoButton;
        private Button magneticLassoButton;
        private Button previewButton;
        private FlowLayoutPanel resultStrip;
        private Label resultState;
        private Button favoriteResultButton;
        private Button deleteResultButton;
        private Button regenerateResultButton;
        private Button pushButton;
        private Button saveButton;
        private Button continueResultButton;
        private CheckBox useEsrgan;
        private Button installEsrganButton;
        private Label esrganState;

        public MainForm()
        {
            settings = store.Load();
            tempDirectory = Path.Combine(store.DataDirectory, "Temp");
            Directory.CreateDirectory(tempDirectory);
            resultSessionDirectory = Path.Combine(tempDirectory, "Results", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(resultSessionDirectory);
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
            Text = "PhotoSense 1.0.17 · Photoshop AI 局部重绘";
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
            RoundedLabel version = new RoundedLabel { Text = "V1.0.17", Size = new Size(68, 22), Location = new Point(390, 13), FillColor = Color.FromArgb(28, 33, 27), TextColor = UiTheme.AccentBright, Radius = 5f };
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
                new NavIconButton(NavIconKind.Relight),
                new NavIconButton(NavIconKind.Result)
            };
            string[] navNames = { "图像来源", "重绘蒙版", "AI 局部重绘", "光影重绘", "使用结果" };
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
            canvas.MaskChanged += CanvasMaskChanged;
            canvas.BrushChanged += delegate { UpdateBrushUi(); };
            canvas.MaskViewChanged += delegate { UpdateMaskViewUi(); };
            canvas.ToolChanged += delegate { UpdateSelectionToolUi(); };
            canvas.PaintModeChanged += delegate { UpdateSelectionToolUi(); };
            canvasFrame.Controls.Add(canvas);
            workspace.Controls.Add(canvasFrame);
            Controls.Add(workspace);
            workspace.BringToFront();

            toolPages = new[]
            {
                BuildSourcePage(),
                BuildMaskPage(),
                BuildGeneratePage(),
                BuildRelightPage(),
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

            Label manual = SectionCaption("选区工具"); manual.Location = new Point(16, 180);
            addButton = ButtonOf("画笔   B", false); addButton.SetBounds(16, 206, 142, 38); addButton.Click += delegate { SetPaintMode(PaintMode.Add); };
            eraseButton = ButtonOf("橡皮   E", false); eraseButton.SetBounds(166, 206, 142, 38); eraseButton.Click += delegate { SetPaintMode(PaintMode.Erase); };
            smartSelectButton = ButtonOf("智能点选 S", false); smartSelectButton.SetBounds(16, 252, 92, 38); smartSelectButton.Click += delegate { SetSelectionTool(SelectionTool.SmartSelect); };
            freehandLassoButton = ButtonOf("自由套索 P", false); freehandLassoButton.SetBounds(116, 252, 92, 38); freehandLassoButton.Click += delegate { SetSelectionTool(SelectionTool.FreehandLasso); };
            magneticLassoButton = ButtonOf("磁性套索 L", false); magneticLassoButton.SetBounds(216, 252, 92, 38); magneticLassoButton.Click += delegate { SetSelectionTool(SelectionTool.MagneticLasso); };
            StyleButton(addButton, true);

            Label brushLabel = SectionCaption("笔刷大小"); brushLabel.Location = new Point(16, 312);
            brushValue = new Label { Location = new Point(238, 308), Size = new Size(70, 24), TextAlign = ContentAlignment.MiddleRight, ForeColor = UiTheme.AccentBright, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
            brush = new ModernTrackBar { Minimum = 2, Maximum = 500, Location = new Point(16, 336), Width = 292 };
            brush.ValueChanged += delegate { canvas.BrushSize = brush.Value; };

            Label refine = SectionCaption("选区精修"); refine.Location = new Point(16, 378);
            refineRadiusValue = new Label { Location = new Point(238, 374), Size = new Size(70, 24), TextAlign = ContentAlignment.MiddleRight, ForeColor = UiTheme.AccentBright, Font = new Font("Segoe UI", 9f, FontStyle.Bold), Text = "4 px" };
            refineRadius = new ModernTrackBar { Minimum = 1, Maximum = 64, Value = 4, Location = new Point(16, 402), Width = 292 };
            refineRadius.ValueChanged += delegate { refineRadiusValue.Text = refineRadius.Value + " px"; };
            Button contract = ButtonOf("收缩选区", false); contract.SetBounds(16, 448, 142, 34); contract.Click += delegate { RefineMask("收缩", ImageComposer.ContractMask); };
            Button expand = ButtonOf("扩展选区", false); expand.SetBounds(166, 448, 142, 34); expand.Click += delegate { RefineMask("扩展", ImageComposer.ExpandMask); };
            Button smooth = ButtonOf("平滑边缘", false); smooth.SetBounds(16, 490, 142, 34); smooth.Click += delegate { RefineMask("平滑", ImageComposer.SmoothMask); };
            Button feather = ButtonOf("羽化边缘", false); feather.SetBounds(166, 490, 142, 34); feather.Click += delegate { RefineMask("羽化", ImageComposer.FeatherMask); };

            Label viewLabel = SectionCaption("蒙版预览"); viewLabel.Location = new Point(16, 542);
            overlayViewButton = ButtonOf("叠加", false); overlayViewButton.SetBounds(16, 568, 92, 34); overlayViewButton.Click += delegate { canvas.MaskViewMode = MaskViewMode.Overlay; };
            blackWhiteViewButton = ButtonOf("黑白", false); blackWhiteViewButton.SetBounds(116, 568, 92, 34); blackWhiteViewButton.Click += delegate { canvas.MaskViewMode = MaskViewMode.BlackWhite; };
            outlineViewButton = ButtonOf("边缘", false); outlineViewButton.SetBounds(216, 568, 92, 34); outlineViewButton.Click += delegate { canvas.MaskViewMode = MaskViewMode.Outline; };
            StyleButton(overlayViewButton, true);

            Button invert = ButtonOf("反选", false); invert.SetBounds(16, 620, 92, 34); invert.Click += delegate { canvas.InvertMask(); };
            Button clear = ButtonOf("清空", false); clear.SetBounds(116, 620, 92, 34); clear.Click += delegate { canvas.ClearMask(); };
            Button fit = ButtonOf("适应窗口", false); fit.SetBounds(216, 620, 92, 34); fit.Click += delegate { canvas.FitToWindow(); };
            maskCoverage = new Label { Text = "覆盖率 0%", Location = new Point(16, 666), Size = new Size(292, 24), ForeColor = UiTheme.Subtle, Font = new Font("Microsoft YaHei UI", 8.5f, FontStyle.Bold) };
            Label help = PageHint("智能点选：左键添加，右键排除\n自由套索：按住拖动，松开闭合（右键排除）\n磁性套索：双击/Enter 闭合，右键起点为排除\nBackspace 退点 · Esc 取消 · Ctrl+Z/Y 撤销/重做", 16, 696, 292, 98);

            page.Controls.Add(help); page.Controls.Add(maskCoverage); page.Controls.Add(fit); page.Controls.Add(clear); page.Controls.Add(invert); page.Controls.Add(outlineViewButton); page.Controls.Add(blackWhiteViewButton); page.Controls.Add(overlayViewButton); page.Controls.Add(viewLabel); page.Controls.Add(feather); page.Controls.Add(smooth); page.Controls.Add(expand); page.Controls.Add(contract); page.Controls.Add(refineRadius); page.Controls.Add(refineRadiusValue); page.Controls.Add(refine); page.Controls.Add(brush); page.Controls.Add(brushValue); page.Controls.Add(brushLabel); page.Controls.Add(magneticLassoButton); page.Controls.Add(freehandLassoButton); page.Controls.Add(smartSelectButton); page.Controls.Add(eraseButton); page.Controls.Add(addButton); page.Controls.Add(manual); page.Controls.Add(background); page.Controls.Add(subject); page.Controls.Add(person); page.Controls.Add(automatic);
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

            Label providerLabel = SectionCaption("生成服务"); providerLabel.Location = new Point(16, 332); providerLabel.Size = new Size(190, 22);
            Label candidateLabel = SectionCaption("生成张数"); candidateLabel.Location = new Point(220, 332); candidateLabel.Size = new Size(88, 22);
            provider = new ComboBox { Location = new Point(16, 358), Size = new Size(196, 34), DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = UiTheme.Field, ForeColor = UiTheme.Text };
            provider.Items.AddRange(new object[] { "GPT Image 2", "Nano Banana" });
            StyleComboBox(provider);
            provider.SelectedIndexChanged += ProviderChanged;
            candidateCount = new ComboBox { Location = new Point(220, 358), Size = new Size(88, 34), DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = UiTheme.Field, ForeColor = UiTheme.Text };
            candidateCount.Items.AddRange(new object[] { "1 张", "2 张", "3 张", "4 张" });
            candidateCount.SelectedIndex = 0;
            StyleComboBox(candidateCount);
            Label candidateCostHint = new Label
            {
                Text = "费用提示：每生成 1 张会调用 1 次 API；生成多张时，调用次数与费用会相应增加。",
                Location = new Point(16, 400),
                Size = new Size(292, 38),
                ForeColor = UiTheme.AccentBright,
                Font = new Font("Microsoft YaHei UI", 8f, FontStyle.Bold)
            };

            useEsrgan = new ModernCheckBox { Text = "生成后使用 Real-ESRGAN 超分", Location = new Point(16, 446), Size = new Size(292, 28) };
            installEsrganButton = ButtonOf("自动部署 Real-ESRGAN", false); installEsrganButton.SetBounds(16, 480, 292, 36); installEsrganButton.Click += InstallRealEsrgan;
            esrganState = new Label { Location = new Point(16, 522), Size = new Size(292, 42), ForeColor = UiTheme.Subtle, Font = new Font("Microsoft YaHei UI", 8f), AutoEllipsis = true };

            generateButton = ButtonOf("开始 AI 局部重绘", true); generateButton.SetBounds(16, 576, 292, 44); generateButton.Font = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold); generateButton.Click += Generate;
            progress = new AccentProgressBar { Location = new Point(16, 628), Width = 292, Visible = false };
            cancelButton = ButtonOf("取消当前任务", false); cancelButton.SetBounds(16, 646, 292, 36); cancelButton.Visible = false; cancelButton.Click += delegate { if (cancellation != null) cancellation.Cancel(); };

            page.Controls.Add(cancelButton); page.Controls.Add(progress); page.Controls.Add(generateButton); page.Controls.Add(esrganState); page.Controls.Add(installEsrganButton); page.Controls.Add(useEsrgan); page.Controls.Add(candidateCostHint); page.Controls.Add(candidateCount); page.Controls.Add(candidateLabel); page.Controls.Add(provider); page.Controls.Add(providerLabel); page.Controls.Add(referenceState); page.Controls.Add(clearReferenceButton); page.Controls.Add(selectReferenceButton); page.Controls.Add(referencePreview); page.Controls.Add(referenceLabel); page.Controls.Add(prompt); page.Controls.Add(promptLabel);
            return page;
        }

        private Panel BuildResultPage()
        {
            Panel page = ToolPage("OUTPUT", "使用结果", "预览、保存或以透明补丁回写 Photoshop");
            Label versionsLabel = SectionCaption("候选结果"); versionsLabel.Location = new Point(16, 94);
            resultStrip = new FlowLayoutPanel
            {
                Location = new Point(16, 120),
                Size = new Size(292, 106),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoScroll = true,
                BackColor = UiTheme.Field,
                Padding = new Padding(6)
            };
            resultState = new Label { Text = "尚未生成结果", Location = new Point(16, 234), Size = new Size(292, 24), ForeColor = UiTheme.Subtle, Font = new Font("Microsoft YaHei UI", 8.2f), AutoEllipsis = true };
            favoriteResultButton = ButtonOf("收藏结果", false); favoriteResultButton.SetBounds(16, 268, 142, 34); favoriteResultButton.Enabled = false; favoriteResultButton.Click += ToggleFavoriteResult;
            deleteResultButton = ButtonOf("删除结果", false); deleteResultButton.SetBounds(166, 268, 142, 34); deleteResultButton.Enabled = false; deleteResultButton.Click += DeleteActiveResult;
            regenerateResultButton = ButtonOf("重新生成当前候选", false); regenerateResultButton.SetBounds(16, 310, 292, 36); regenerateResultButton.Enabled = false; regenerateResultButton.Click += RegenerateActiveResult;
            previewButton = ButtonOf("切换原图 / 结果预览", false); previewButton.SetBounds(16, 360, 292, 38); previewButton.Enabled = false; previewButton.Click += TogglePreview;
            continueResultButton = ButtonOf("将结果作为下一步输入", false); continueResultButton.SetBounds(16, 408, 292, 38); continueResultButton.Enabled = false; continueResultButton.Click += ContinueWithResult;
            pushButton = ButtonOf("作为透明补丁送回 Photoshop", true); pushButton.SetBounds(16, 456, 292, 44); pushButton.Enabled = false; pushButton.Click += PushToPhotoshop;
            saveButton = ButtonOf("保存完整合成 PNG", false); saveButton.SetBounds(16, 510, 292, 38); saveButton.Enabled = false; saveButton.Click += SaveComposite;
            Label protection = SectionCaption("无损保护"); protection.Location = new Point(16, 566);
            Label help = PageHint("可将当前结果继续交给其他功能。所有步骤累计为一个透明补丁；未选区域始终从最初原图取像素。光影模式只应用低频明暗/色温变化。", 16, 594, 292, 100);
            page.Controls.Add(help); page.Controls.Add(protection); page.Controls.Add(saveButton); page.Controls.Add(pushButton); page.Controls.Add(continueResultButton); page.Controls.Add(previewButton); page.Controls.Add(regenerateResultButton); page.Controls.Add(deleteResultButton); page.Controls.Add(favoriteResultButton); page.Controls.Add(resultState); page.Controls.Add(resultStrip); page.Controls.Add(versionsLabel);
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
                if (chainedMask != null) { chainedMask.Dispose(); chainedMask = null; }
                chainedStepCount = 0;
                ClearGeneratedResult();
                canvas.SetImage(bitmap);
            }
            imageInfo.Text = info.DocumentName + "\n" + info.Width + " × " + info.Height + (info.DocumentId > 0 ? " · Photoshop 已连接" : " · 本地图片");
            canvas.ShowMaskOverlay = true;
            canvas.MaskViewMode = MaskViewMode.Overlay;
            UpdateMaskCoverage();
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
            if (!ValidateGenerationInput()) return;
            string key = await EnsureGenerationResourcesAsync();
            if (string.IsNullOrEmpty(key)) return;
            GenerationPlan plan = GenerationPlan.Create(settings.SelectedProvider, candidateCount == null ? 1 : candidateCount.SelectedIndex + 1);
            cancellation = new CancellationTokenSource();
            SetBusy(true);
            ClearGeneratedResult();
            try
            {
                using (Bitmap source = canvas.GetSourceCopy())
                using (Bitmap mask = canvas.GetMaskCopy())
                using (ImageApiClient client = new ImageApiClient(settings.ApiTimeoutSeconds))
                {
                    string resolutionWarning = null;
                    Exception partialFailure = null;
                    for (int index = 0; index < plan.CandidateCount; index++)
                    {
                        try
                        {
                            GenerationBuild build = await GenerateVersionAsync(client, source, mask, key, index + 1, plan.CandidateCount, cancellation.Token);
                            generationVersions.Add(build.Version);
                            if (string.IsNullOrEmpty(resolutionWarning)) resolutionWarning = build.ResolutionWarning;
                            RefreshResultStrip();
                        }
                        catch (OperationCanceledException) { throw; }
                        catch (Exception ex)
                        {
                            partialFailure = ex;
                            break;
                        }
                    }
                    if (generationVersions.Count == 0 && partialFailure != null) throw partialFailure;
                    if (generationVersions.Count == 0) throw new InvalidOperationException("接口没有生成可用结果。");
                    ActivateGeneration(0, true);
                    if (!string.IsNullOrEmpty(resolutionWarning))
                    {
                        MessageBox.Show(this, resolutionWarning, "Nano Banana 分辨率未生效", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                    if (partialFailure != null)
                    {
                        MessageBox.Show(this, "计划生成 " + plan.CandidateCount + " 个候选，已完成 " + generationVersions.Count + " 个，其余请求失败。已完成的结果会保留。\n\n" + partialFailure.Message, "部分候选生成失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
                status.Text = "已生成 " + generationVersions.Count + " 个候选；蒙版外继续使用原图像素";
                ShowToolPage(4);
            }
            catch (OperationCanceledException)
            {
                if (generationVersions.Count > 0)
                {
                    ActivateGeneration(0, true);
                    status.Text = "任务已取消；已保留 " + generationVersions.Count + " 个完成的候选";
                    ShowToolPage(4);
                }
                else status.Text = "任务已取消";
            }
            catch (Exception ex)
            {
                ClearGeneratedResult();
                ShowError(ex);
            }
            finally
            {
                cancellation.Dispose();
                cancellation = null;
                SetBusy(false);
                UpdateResultControls();
            }
        }

        private bool ValidateGenerationInput()
        {
            if (!RequireImage()) return false;
            if (string.IsNullOrWhiteSpace(prompt.Text))
            {
                MessageBox.Show(this, "请先描述想重绘成什么效果。", "局部重绘", MessageBoxButtons.OK, MessageBoxIcon.Information);
                prompt.Focus();
                return false;
            }
            using (Bitmap check = canvas.GetMaskCopy())
            {
                if (!ImageComposer.HasSelection(check))
                {
                    MessageBox.Show(this, "还没有选中重绘范围。请自动选择，或用画笔涂出区域。", "局部重绘", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return false;
                }
                double coverage = ImageComposer.SelectionCoverage(check);
                if (coverage >= 0.985)
                {
                    DialogResult continueGeneration = MessageBox.Show(this,
                        "当前选区覆盖画面 " + (coverage * 100d).ToString("0.0") + "% ，可能误选了整张图片。\n\n继续生成将允许模型重绘几乎整个画面。是否仍要继续？",
                        "选区接近整张画面", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
                    if (continueGeneration != DialogResult.Yes) return false;
                }
            }
            return true;
        }

        private async Task<string> EnsureGenerationResourcesAsync()
        {
            if (useEsrgan.Checked && esrgan.ResolveExecutable(settings) == null)
            {
                DialogResult install = MessageBox.Show(this, "已勾选 Real-ESRGAN，但当前用户尚未部署。是否现在自动下载并安装？", "Real-ESRGAN", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                if (install != DialogResult.Yes || !await DeployRealEsrganAsync(false)) return null;
            }
            string key = store.GetApiKey(settings.SelectedProvider);
            if (!string.IsNullOrEmpty(key)) return key;
            using (SettingsDialog dialog = new SettingsDialog(store, settings))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return null;
            }
            settings = store.Load();
            UpdateProviderUi();
            key = store.GetApiKey(settings.SelectedProvider);
            if (!string.IsNullOrEmpty(key)) return key;
            MessageBox.Show(this, "需要为当前服务填写 API Key。", "API Key", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return null;
        }

        private async Task<GenerationBuild> GenerateVersionAsync(ImageApiClient client, Bitmap source, Bitmap mask, string key, int ordinal, int total, CancellationToken token)
        {
            SetStatus("正在生成候选 " + ordinal + " / " + total + " · " + ProviderDescription() + "…");
            Bitmap apiResult = await client.GenerateAsync(source, mask, referenceImage, prompt.Text, settings, key, token);
            try
            {
                string resolutionWarning = ImageApiClient.GeminiResolutionWarning(settings, apiResult.Width, apiResult.Height);
                if (useEsrgan.Checked)
                {
                    SetStatus("正在超分候选 " + ordinal + " / " + total + "…");
                    Bitmap upscaled = await esrgan.UpscaleAsync(apiResult, settings, tempDirectory, token);
                    apiResult.Dispose();
                    apiResult = upscaled;
                }
                token.ThrowIfCancellationRequested();
                string path = Path.Combine(resultSessionDirectory, DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N") + ".png");
                apiResult.Save(path, ImageFormat.Png);
                try
                {
                    using (Bitmap localComposite = ImageComposer.Composite(source, apiResult, mask))
                    {
                        return new GenerationBuild
                        {
                            Version = new GenerationVersion
                            {
                                GeneratedPath = path,
                                Thumbnail = BuildResultThumbnail(localComposite, 160, 112),
                                ProviderLabel = ProviderDescription(),
                                Prompt = prompt.Text,
                                CreatedAt = DateTime.Now,
                                Operation = GenerationOperation.Repaint,
                                DetailProtection = 92
                            },
                            ResolutionWarning = resolutionWarning
                        };
                    }
                }
                catch
                {
                    try { File.Delete(path); } catch { }
                    throw;
                }
            }
            finally
            {
                apiResult.Dispose();
            }
        }

        private static Bitmap BuildResultThumbnail(Bitmap image, int width, int height)
        {
            Bitmap thumbnail = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using (Graphics graphics = Graphics.FromImage(thumbnail))
            {
                graphics.Clear(UiTheme.Canvas);
                float scale = Math.Min(width / (float)image.Width, height / (float)image.Height);
                int targetWidth = Math.Max(1, (int)Math.Round(image.Width * scale));
                int targetHeight = Math.Max(1, (int)Math.Round(image.Height * scale));
                Rectangle target = new Rectangle((width - targetWidth) / 2, (height - targetHeight) / 2, targetWidth, targetHeight);
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                graphics.DrawImage(image, target);
            }
            return thumbnail;
        }

        private static Bitmap LoadBitmapCopy(string path)
        {
            using (Image image = Image.FromFile(path)) return new Bitmap(image);
        }

        private void RefreshResultStrip()
        {
            if (resultStrip == null) return;
            resultStrip.SuspendLayout();
            try
            {
                while (resultStrip.Controls.Count > 0)
                {
                    Control oldControl = resultStrip.Controls[0];
                    resultStrip.Controls.RemoveAt(0);
                    oldControl.Dispose();
                }
                for (int index = 0; index < generationVersions.Count; index++)
                {
                    int selectedIndex = index;
                    GenerationVersion version = generationVersions[index];
                    ResultThumbnailControl thumbnail = new ResultThumbnailControl
                    {
                        Thumbnail = version.Thumbnail,
                        Caption = "结果 " + (index + 1),
                        Favorite = version.Favorite,
                        SelectedState = index == activeGenerationIndex
                    };
                    thumbnail.Click += delegate { ActivateGeneration(selectedIndex, true); };
                    resultStrip.Controls.Add(thumbnail);
                }
            }
            finally
            {
                resultStrip.ResumeLayout();
            }
            UpdateResultControls();
        }

        private void ActivateGeneration(int index, bool showResult)
        {
            if (index < 0 || index >= generationVersions.Count) return;
            ReleaseActiveResultBitmaps();
            activeGenerationIndex = index;
            GenerationVersion version = generationVersions[index];
            activeGenerated = LoadBitmapCopy(version.GeneratedPath);
            using (Bitmap source = canvas.GetSourceCopy())
            using (Bitmap mask = canvas.GetMaskCopy())
                activeComposite = ComposeOperation(source, activeGenerated, mask, version.Operation, version.DetailProtection);
            canvas.ShowMaskOverlay = false;
            canvas.SetPreview(showResult ? activeComposite : null);
            previewButton.Text = showResult ? "查看原图" : "查看结果";
            RefreshResultStrip();
            status.Text = "正在查看候选 " + (index + 1) + " / " + generationVersions.Count;
        }

        private void UpdateResultControls()
        {
            bool hasResult = activeGenerationIndex >= 0 && activeGenerationIndex < generationVersions.Count && activeComposite != null;
            if (previewButton != null) previewButton.Enabled = hasResult;
            if (favoriteResultButton != null)
            {
                favoriteResultButton.Enabled = hasResult && cancellation == null;
                favoriteResultButton.Text = hasResult && generationVersions[activeGenerationIndex].Favorite ? "取消收藏" : "收藏结果";
            }
            if (deleteResultButton != null) deleteResultButton.Enabled = hasResult && cancellation == null;
            if (regenerateResultButton != null) regenerateResultButton.Enabled = hasResult && cancellation == null;
            if (pushButton != null) pushButton.Enabled = hasResult && photoshopSource != null && photoshopSource.DocumentId > 0;
            if (saveButton != null) saveButton.Enabled = hasResult;
            if (continueResultButton != null) continueResultButton.Enabled = hasResult && cancellation == null;
            if (resultState != null)
            {
                resultState.Text = hasResult
                    ? OperationName(generationVersions[activeGenerationIndex].Operation) + " " + (activeGenerationIndex + 1) + " / " + generationVersions.Count + " · " + generationVersions[activeGenerationIndex].ProviderLabel + " · " + generationVersions[activeGenerationIndex].CreatedAt.ToString("HH:mm:ss")
                    : "尚未生成结果";
            }
        }

        private void ToggleFavoriteResult(object sender, EventArgs e)
        {
            if (activeGenerationIndex < 0 || activeGenerationIndex >= generationVersions.Count) return;
            GenerationVersion version = generationVersions[activeGenerationIndex];
            version.Favorite = !version.Favorite;
            RefreshResultStrip();
            status.Text = version.Favorite ? "当前候选已收藏" : "已取消收藏";
        }

        private void DeleteActiveResult(object sender, EventArgs e)
        {
            if (activeGenerationIndex < 0 || activeGenerationIndex >= generationVersions.Count) return;
            int removedIndex = activeGenerationIndex;
            ReleaseActiveResultBitmaps();
            GenerationVersion removed = generationVersions[removedIndex];
            generationVersions.RemoveAt(removedIndex);
            removed.Dispose();
            activeGenerationIndex = -1;
            if (generationVersions.Count == 0)
            {
                RefreshResultStrip();
                canvas.SetPreview(null);
                canvas.ShowMaskOverlay = false;
                previewButton.Text = "查看原图";
                status.Text = "所有候选结果已删除";
                return;
            }
            ActivateGeneration(Math.Min(removedIndex, generationVersions.Count - 1), true);
            status.Text = "候选结果已删除";
        }

        private async void RegenerateActiveResult(object sender, EventArgs e)
        {
            if (activeGenerationIndex < 0 || activeGenerationIndex >= generationVersions.Count) return;
            GenerationVersion selectedVersion = generationVersions[activeGenerationIndex];
            if (selectedVersion.Operation != GenerationOperation.Repaint)
            {
                await RegenerateOperationResultAsync(activeGenerationIndex, selectedVersion);
                return;
            }
            if (!ValidateGenerationInput()) return;
            string key = await EnsureGenerationResourcesAsync();
            if (string.IsNullOrEmpty(key)) return;
            int replaceIndex = activeGenerationIndex;
            cancellation = new CancellationTokenSource();
            SetBusy(true);
            try
            {
                using (Bitmap source = canvas.GetSourceCopy())
                using (Bitmap mask = canvas.GetMaskCopy())
                using (ImageApiClient client = new ImageApiClient(settings.ApiTimeoutSeconds))
                {
                    GenerationBuild build = await GenerateVersionAsync(client, source, mask, key, 1, 1, cancellation.Token);
                    ReleaseActiveResultBitmaps();
                    GenerationVersion previous = generationVersions[replaceIndex];
                    generationVersions[replaceIndex] = build.Version;
                    previous.Dispose();
                    activeGenerationIndex = -1;
                    ActivateGeneration(replaceIndex, true);
                    if (!string.IsNullOrEmpty(build.ResolutionWarning))
                        MessageBox.Show(this, build.ResolutionWarning, "Nano Banana 分辨率未生效", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                status.Text = "当前候选已重新生成，其他候选保持不变";
            }
            catch (OperationCanceledException) { status.Text = "重新生成已取消，原候选保持不变"; }
            catch (Exception ex) { ShowError(ex); }
            finally
            {
                cancellation.Dispose();
                cancellation = null;
                SetBusy(false);
                UpdateResultControls();
            }
        }

        private void ReleaseActiveResultBitmaps()
        {
            if (activeGenerated != null) { activeGenerated.Dispose(); activeGenerated = null; }
            if (activeComposite != null) { activeComposite.Dispose(); activeComposite = null; }
        }

        private void TogglePreview(object sender, EventArgs e)
        {
            if (activeComposite == null) return;
            if (canvas.ShowingPreview)
            {
                canvas.SetPreview(null);
                canvas.ShowMaskOverlay = false;
                previewButton.Text = "查看结果";
            }
            else
            {
                canvas.SetPreview(activeComposite);
                canvas.ShowMaskOverlay = false;
                previewButton.Text = "查看原图";
            }
        }

        private void PushToPhotoshop(object sender, EventArgs e)
        {
            if (activeGenerated == null || photoshopSource == null || photoshopSource.DocumentId == 0) return;
            try
            {
                string patchDirectory = Path.Combine(store.DataDirectory, "Patches", DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(patchDirectory);
                string patchPath = Path.Combine(patchDirectory, "full-patch.png");
                using (Bitmap currentMask = canvas.GetMaskCopy())
                using (Bitmap mask = chainedStepCount > 0 ? ImageComposer.UnionMasks(chainedMask, currentMask) : new Bitmap(currentMask))
                using (Bitmap patch = ImageComposer.CreatePatch(
                    chainedStepCount > 0 || generationVersions[activeGenerationIndex].Operation != GenerationOperation.Repaint ? activeComposite : activeGenerated,
                    mask))
                {
                    float resolution = photoshopSource.Resolution > 0 && photoshopSource.Resolution <= 9600 ? (float)photoshopSource.Resolution : 96f;
                    patch.SetResolution(resolution, resolution);
                    patch.Save(patchPath, ImageFormat.Png);
                    try
                    {
                        photoshop.PlacePatchAsLayer(patchPath, "PhotoSense · " + OperationName(generationVersions[activeGenerationIndex].Operation), photoshopSource.DocumentId);
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
                            photoshop.PlacePatchTilesAsGroup(tiles, "PhotoSense · " + OperationName(generationVersions[activeGenerationIndex].Operation), photoshopSource.DocumentId);
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
            if (activeComposite == null) return;
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Title = "保存完整合成图";
                dialog.Filter = "PNG 图片|*.png";
                dialog.DefaultExt = "png";
                dialog.FileName = "PhotoSense-Result.png";
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                activeComposite.Save(dialog.FileName, ImageFormat.Png);
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

        private void CanvasMaskChanged(object sender, EventArgs e)
        {
            ClearGeneratedResult();
            UpdateMaskCoverage();
            string selectionMessage = canvas.ConsumeLastSelectionMessage();
            status.Text = string.IsNullOrEmpty(selectionMessage) ? "蒙版已更新，可开始生成" : selectionMessage;
        }

        private void UpdateMaskCoverage()
        {
            if (maskCoverage == null || canvas == null || !canvas.HasImage)
            {
                if (maskCoverage != null) maskCoverage.Text = "覆盖率 0%";
                return;
            }
            using (Bitmap currentMask = canvas.GetMaskCopy())
            {
                double coverage = ImageComposer.SelectionCoverage(currentMask);
                maskCoverage.Text = "覆盖率 " + (coverage * 100d).ToString(coverage < 0.01 ? "0.00" : "0.0") + "%" +
                    (coverage >= 0.985 ? " · 可能选中了整张图片" : "");
                maskCoverage.ForeColor = coverage >= 0.985 ? Color.FromArgb(255, 174, 90) : UiTheme.Subtle;
            }
        }

        private void UpdateMaskViewUi()
        {
            if (canvas == null) return;
            StyleButton(overlayViewButton, canvas.MaskViewMode == MaskViewMode.Overlay);
            StyleButton(blackWhiteViewButton, canvas.MaskViewMode == MaskViewMode.BlackWhite);
            StyleButton(outlineViewButton, canvas.MaskViewMode == MaskViewMode.Outline);
        }

        private async void RefineMask(string operationName, Func<Bitmap, int, Bitmap> operation)
        {
            if (!RequireImage() || operation == null) return;
            int radius = refineRadius == null ? 4 : refineRadius.Value;
            Bitmap input = canvas.GetMaskCopy();
            Bitmap refined = null;
            toolPages[1].Enabled = false;
            canvas.Enabled = false;
            UseWaitCursor = true;
            SetStatus("正在" + operationName + "选区边缘…");
            try
            {
                refined = await Task.Run(delegate { return operation(input, radius); });
                canvas.SetMask(refined);
                status.Text = operationName + "完成 · 半径 " + radius + " px · 可用 Ctrl+Z 撤销";
            }
            catch (Exception ex)
            {
                ShowError(new InvalidOperationException(operationName + "选区失败：" + ex.Message, ex));
            }
            finally
            {
                if (refined != null) refined.Dispose();
                input.Dispose();
                UseWaitCursor = false;
                canvas.Enabled = true;
                toolPages[1].Enabled = true;
                canvas.Focus();
            }
        }

        private void SetPaintMode(PaintMode mode)
        {
            canvas.Tool = SelectionTool.Brush;
            canvas.Mode = mode;
            UpdateSelectionToolUi();
            status.Text = mode == PaintMode.Add ? "画笔：添加重绘范围（B）" : "橡皮：移除重绘范围（E）";
            canvas.Focus();
        }

        private void SetSelectionTool(SelectionTool tool)
        {
            canvas.Tool = tool;
            UpdateSelectionToolUi();
            status.Text = tool == SelectionTool.SmartSelect
                ? "智能点选：左键添加同一物体区域，右键排除误选区域（S）"
                : tool == SelectionTool.FreehandLasso
                    ? "自由套索：按住鼠标自由绘制，松开后自动闭合（P）"
                    : "磁性套索：沿物体边缘单击锚点，双击或 Enter 闭合（L）";
            canvas.Focus();
        }

        private void UpdateSelectionToolUi()
        {
            if (canvas == null) return;
            bool brushTool = canvas.Tool == SelectionTool.Brush;
            StyleButton(addButton, brushTool && canvas.Mode == PaintMode.Add);
            StyleButton(eraseButton, brushTool && canvas.Mode == PaintMode.Erase);
            StyleButton(smartSelectButton, canvas.Tool == SelectionTool.SmartSelect);
            StyleButton(freehandLassoButton, canvas.Tool == SelectionTool.FreehandLasso);
            StyleButton(magneticLassoButton, canvas.Tool == SelectionTool.MagneticLasso);
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
            ReleaseActiveResultBitmaps();
            activeGenerationIndex = -1;
            if (resultStrip != null)
            {
                while (resultStrip.Controls.Count > 0)
                {
                    Control control = resultStrip.Controls[0];
                    resultStrip.Controls.RemoveAt(0);
                    control.Dispose();
                }
            }
            foreach (GenerationVersion version in generationVersions) version.Dispose();
            generationVersions.Clear();
            if (canvas != null)
            {
                canvas.SetPreview(null);
                canvas.ShowMaskOverlay = true;
            }
            if (previewButton != null) { previewButton.Enabled = false; previewButton.Text = "查看原图"; }
            if (favoriteResultButton != null) favoriteResultButton.Enabled = false;
            if (deleteResultButton != null) deleteResultButton.Enabled = false;
            if (regenerateResultButton != null) regenerateResultButton.Enabled = false;
            if (pushButton != null) pushButton.Enabled = false;
            if (saveButton != null) saveButton.Enabled = false;
            if (continueResultButton != null) continueResultButton.Enabled = false;
            if (resultState != null) resultState.Text = "尚未生成结果";
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
            if (relightGenerateButton != null) relightGenerateButton.Enabled = !busy;
            if (relightCancelButton != null) relightCancelButton.Visible = busy;
            if (installEsrganButton != null) installEsrganButton.Enabled = !busy;
            if (selectReferenceButton != null) selectReferenceButton.Enabled = !busy;
            if (clearReferenceButton != null) clearReferenceButton.Enabled = !busy && referenceImage != null;
            progress.Visible = busy;
            cancelButton.Visible = busy;
            provider.Enabled = !busy;
            if (candidateCount != null) candidateCount.Enabled = !busy;
            if (resultStrip != null) resultStrip.Enabled = !busy;
            if (favoriteResultButton != null) favoriteResultButton.Enabled = !busy && activeGenerationIndex >= 0;
            if (deleteResultButton != null) deleteResultButton.Enabled = !busy && activeGenerationIndex >= 0;
            if (regenerateResultButton != null) regenerateResultButton.Enabled = !busy && activeGenerationIndex >= 0;
            if (continueResultButton != null) continueResultButton.Enabled = !busy && activeGenerationIndex >= 0;
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
                File.AppendAllText(logPath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " · PhotoSense 1.0.17\r\n" + ex + "\r\n\r\n");
            }
            catch { }
            string details = ex.Message + (IsPhotoshopStorageError(ex) ? LowDiskWarning() : "") + (string.IsNullOrEmpty(logPath) ? "" : "\n\n诊断记录：" + logPath);
            MessageBox.Show(this, details, "PhotoSense 1.0.17", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            if (chainedMask != null) { chainedMask.Dispose(); chainedMask = null; }
            try { if (Directory.Exists(resultSessionDirectory)) Directory.Delete(resultSessionDirectory, true); } catch { }
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
