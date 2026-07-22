using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace PixelPatchStudio
{
    internal sealed class SettingsDialog : Form
    {
        private readonly SettingsStore store;
        private readonly AppSettings settings;
        private ComboBox provider;
        private TextBox baseUrl;
        private TextBox endpoint;
        private TextBox model;
        private ComboBox geminiImageSize;
        private TextBox apiKey;
        private Label keyState;
        private TextBox esrganPath;
        private TextBox esrganDownloadUrl;
        private TextBox esrganModel;
        private NumericUpDown esrganScale;
        private ComboBox uiScale;
        private ApiProvider editingProvider;

        public SettingsDialog(SettingsStore store, AppSettings settings)
        {
            this.store = store;
            this.settings = settings;
            editingProvider = settings.SelectedProvider;
            BuildUi();
            UiScale.Apply(this, settings.UiScalePercent, true);
            LoadProviderFields();
        }

        private void BuildUi()
        {
            Text = "设置 · PhotoSense";
            Icon = AppIcon.Create();
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(820, 610);
            MinimumSize = new Size(760, 560);
            BackColor = UiTheme.Window;
            ForeColor = UiTheme.Text;
            Font = new Font("Microsoft YaHei UI", 9f);
            FormBorderStyle = FormBorderStyle.Sizable;
            AutoScaleMode = AutoScaleMode.Dpi;

            BrandGradientLine accentLine = new BrandGradientLine { Dock = DockStyle.Top };
            Controls.Add(accentLine);
            Panel header = new Panel { Dock = DockStyle.Top, Height = 68, BackColor = UiTheme.Header };
            BrandMark mark = new BrandMark { Location = new Point(18, 13), Size = new Size(40, 40) };
            Label title = new Label { Text = "工作台设置", Location = new Point(72, 12), AutoSize = true, ForeColor = UiTheme.Text, Font = new Font("Microsoft YaHei UI", 13f, FontStyle.Bold) };
            Label subtitle = new Label { Text = "API CONNECTION  /  UPSCALE ENGINE", Location = new Point(74, 39), AutoSize = true, ForeColor = UiTheme.Subtle, Font = new Font("Segoe UI", 7.5f, FontStyle.Bold) };
            RoundedLabel secure = new RoundedLabel { Text = "CURRENT USER · DPAPI", Anchor = AnchorStyles.Top | AnchorStyles.Right, Location = new Point(ClientSize.Width - 184, 22), Size = new Size(164, 24), FillColor = Color.FromArgb(28, 33, 27), TextColor = UiTheme.AccentBright, Radius = 5f };
            header.Resize += delegate { secure.Left = header.ClientSize.Width - secure.Width - 20; };
            header.Controls.Add(secure); header.Controls.Add(subtitle); header.Controls.Add(title); header.Controls.Add(mark);
            Controls.Add(header);

            Panel content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(18, 16, 18, 14), BackColor = UiTheme.Workspace };
            Controls.Add(content);
            TableLayoutPanel columns = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = UiTheme.Workspace };
            columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            content.Controls.Add(columns);
            CardPanel apiCard = new CardPanel { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 7, 0), Padding = new Padding(16, 12, 16, 12), AutoScroll = true };
            CardPanel upscaleCard = new CardPanel { Dock = DockStyle.Fill, Margin = new Padding(7, 0, 0, 0), Padding = new Padding(16, 12, 16, 12), AutoScroll = true };
            columns.Controls.Add(apiCard, 0, 0); columns.Controls.Add(upscaleCard, 1, 0);

            TableLayoutPanel apiTable = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, RowCount = 9, BackColor = UiTheme.Card };
            apiTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 86));
            apiTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            apiCard.Controls.Add(apiTable);
            Label apiTitle = Title("API CONNECTION", "生成接口");
            apiTable.Controls.Add(apiTitle, 0, 0);
            apiTable.SetColumnSpan(apiTitle, 2);
            provider = Combo();
            provider.Items.AddRange(new object[] { "GPT Image 2", "Nano Banana" });
            provider.SelectedItem = settings.Provider;
            if (provider.SelectedIndex < 0) provider.SelectedIndex = 0;
            provider.SelectedIndexChanged += ProviderChanged;
            AddRow(apiTable, 1, "服务", provider);
            baseUrl = Field(); AddRow(apiTable, 2, "中转 URL", baseUrl);
            endpoint = Field(); AddRow(apiTable, 3, "接口", endpoint);
            model = Field(); AddRow(apiTable, 4, "模型", model);
            geminiImageSize = Combo();
            geminiImageSize.Items.AddRange(GeminiResolution.DisplayNames);
            geminiImageSize.SelectedIndex = GeminiResolution.IndexOf(settings.GeminiImageSize);
            AddRow(apiTable, 5, "生成分辨率", geminiImageSize);

            Panel keyPanel = new Panel { Dock = DockStyle.Fill, Height = 64 };
            apiKey = Field(); apiKey.UseSystemPasswordChar = true; apiKey.Dock = DockStyle.Top;
            keyState = new Label { Dock = DockStyle.Bottom, Height = 24, ForeColor = UiTheme.Muted, TextAlign = ContentAlignment.MiddleLeft };
            Button clearKey = SmallButton("清除已保存 Key"); clearKey.Dock = DockStyle.Right; clearKey.Width = 130; clearKey.Click += ClearKey;
            keyPanel.Controls.Add(apiKey); keyPanel.Controls.Add(clearKey); keyPanel.Controls.Add(keyState);
            AddRow(apiTable, 6, "API Key", keyPanel);

            Label note = new Label
            {
                AutoSize = true,
                MaximumSize = new Size(270, 0),
                ForeColor = UiTheme.Subtle,
                Font = new Font("Microsoft YaHei UI", 8f),
                Text = "Key 使用当前用户 DPAPI 加密并跨版本沿用。Nano Banana 中转可使用 {model} 占位符；4K 需要 Gemini 3 图像模型及中转支持。"
            };
            apiTable.Controls.Add(note, 1, 7);
            Panel openSourcePanel = new Panel
            {
                Dock = DockStyle.Fill,
                Height = 40,
                BackColor = Color.Transparent,
                Margin = new Padding(0)
            };
            Label openSourceDeclaration = new Label
            {
                Text = "本软件免费开源 · 第三方 API 费用由服务商决定",
                Dock = DockStyle.Top,
                Height = 20,
                AutoEllipsis = true,
                ForeColor = UiTheme.Text,
                Font = new Font("Microsoft YaHei UI", 8.2f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };
            LinkLabel projectLink = new LinkLabel
            {
                Text = "GitHub：Gara-11/photosense（点击访问）",
                Dock = DockStyle.Bottom,
                Height = 20,
                LinkColor = UiTheme.AccentBright,
                ActiveLinkColor = UiTheme.Accent,
                VisitedLinkColor = UiTheme.AccentBright,
                Font = new Font("Microsoft YaHei UI", 8.2f, FontStyle.Bold),
                TabStop = true,
                TextAlign = ContentAlignment.MiddleLeft
            };
            projectLink.Links.Clear();
            projectLink.Links.Add(0, projectLink.Text.Length, "https://github.com/Gara-11/photosense/");
            projectLink.LinkClicked += OpenProjectLink;
            openSourcePanel.Controls.Add(projectLink);
            openSourcePanel.Controls.Add(openSourceDeclaration);
            apiTable.Controls.Add(openSourcePanel, 1, 8);

            TableLayoutPanel upscaleTable = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, RowCount = 7, BackColor = UiTheme.Card };
            upscaleTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 86));
            upscaleTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            upscaleCard.Controls.Add(upscaleTable);
            Label esrganTitle = Title("UPSCALE ENGINE", "Real-ESRGAN");
            upscaleTable.Controls.Add(esrganTitle, 0, 0);
            upscaleTable.SetColumnSpan(esrganTitle, 2);
            Panel pathPanel = new Panel { Dock = DockStyle.Fill, Height = 34 };
            esrganPath = Field(); esrganPath.Dock = DockStyle.Fill;
            Button browse = SmallButton("选择…"); browse.Dock = DockStyle.Right; browse.Width = 80; browse.Click += BrowseEsrgan;
            pathPanel.Controls.Add(esrganPath); pathPanel.Controls.Add(browse);
            AddRow(upscaleTable, 1, "程序路径", pathPanel);
            esrganDownloadUrl = Field(); esrganDownloadUrl.Text = settings.RealEsrganDownloadUrl; AddRow(upscaleTable, 2, "下载 URL", esrganDownloadUrl);
            esrganModel = Field(); esrganModel.Text = settings.RealEsrganModel; AddRow(upscaleTable, 3, "模型", esrganModel);
            esrganScale = new NumericUpDown { Minimum = 2, Maximum = 4, Value = settings.RealEsrganScale, Dock = DockStyle.Left, Width = 100, BackColor = UiTheme.Field, ForeColor = UiTheme.Text, BorderStyle = BorderStyle.FixedSingle };
            AddRow(upscaleTable, 4, "放大倍率", esrganScale);
            uiScale = Combo();
            uiScale.Items.AddRange(UiScale.DisplayNames);
            uiScale.SelectedIndex = UiScale.IndexOfPercent(settings.UiScalePercent);
            AddRow(upscaleTable, 5, "UI Scale", uiScale);
            esrganPath.Text = settings.RealEsrganPath;
            Label upscaleNote = new Label { Text = "自动模式当前约为 " + UiScale.EffectivePercent(settings.UiScalePercent) + "%；会结合 Windows DPI 与屏幕有效分辨率。修改后重新启动生效。", AutoSize = true, MaximumSize = new Size(270, 0), ForeColor = UiTheme.Subtle, Font = new Font("Microsoft YaHei UI", 8f) };
            upscaleTable.Controls.Add(upscaleNote, 1, 6);

            FlowLayoutPanel footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 62, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 12, 18, 0), BackColor = UiTheme.Header };
            Button save = AccentButton("保存"); save.Click += SaveClicked;
            Button cancel = SmallButton("取消"); cancel.DialogResult = DialogResult.Cancel;
            footer.Controls.Add(save); footer.Controls.Add(cancel);
            Controls.Add(footer);
            Controls.Clear();
            TableLayoutPanel root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = new Padding(0), Padding = new Padding(0) };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
            header.Dock = DockStyle.Fill;
            accentLine.Dock = DockStyle.Bottom;
            header.Controls.Add(accentLine);
            content.Dock = DockStyle.Fill;
            footer.Dock = DockStyle.Fill;
            root.Controls.Add(header, 0, 0);
            root.Controls.Add(content, 0, 1);
            root.Controls.Add(footer, 0, 2);
            Controls.Add(root);
            AcceptButton = save;
            CancelButton = cancel;
        }

        private void ProviderChanged(object sender, EventArgs e)
        {
            CommitProviderFields();
            editingProvider = provider.SelectedIndex == 1 ? ApiProvider.NanoBanana : ApiProvider.GptImage2;
            LoadProviderFields();
        }

        private void LoadProviderFields()
        {
            if (editingProvider == ApiProvider.GptImage2)
            {
                baseUrl.Text = settings.OpenAiBaseUrl;
                endpoint.Text = settings.OpenAiEndpoint;
                model.Text = settings.OpenAiModel;
            }
            else
            {
                baseUrl.Text = settings.GeminiBaseUrl;
                endpoint.Text = settings.GeminiEndpoint;
                model.Text = settings.GeminiModel;
            }
            apiKey.Text = "";
            keyState.Text = string.IsNullOrEmpty(store.GetApiKey(editingProvider)) ? "尚未保存 Key" : "✓ 已为此服务保存 Key";
            geminiImageSize.Enabled = editingProvider == ApiProvider.NanoBanana;
        }

        private void CommitProviderFields()
        {
            if (baseUrl == null) return;
            if (editingProvider == ApiProvider.GptImage2)
            {
                settings.OpenAiBaseUrl = baseUrl.Text.Trim();
                settings.OpenAiEndpoint = endpoint.Text.Trim();
                settings.OpenAiModel = model.Text.Trim();
            }
            else
            {
                settings.GeminiBaseUrl = baseUrl.Text.Trim();
                settings.GeminiEndpoint = endpoint.Text.Trim();
                settings.GeminiModel = model.Text.Trim();
            }
            if (!string.IsNullOrWhiteSpace(apiKey.Text)) store.SetApiKey(editingProvider, apiKey.Text);
        }

        private void SaveClicked(object sender, EventArgs e)
        {
            CommitProviderFields();
            if (string.IsNullOrWhiteSpace(baseUrl.Text) || string.IsNullOrWhiteSpace(endpoint.Text) || string.IsNullOrWhiteSpace(model.Text))
            {
                MessageBox.Show(this, "中转 URL、接口和模型不能为空。", "设置", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            settings.Provider = provider.SelectedIndex == 1 ? "Nano Banana" : "GPT Image 2";
            settings.RealEsrganPath = esrganPath.Text.Trim();
            settings.RealEsrganDownloadUrl = esrganDownloadUrl.Text.Trim();
            settings.RealEsrganModel = esrganModel.Text.Trim();
            settings.RealEsrganScale = (int)esrganScale.Value;
            settings.GeminiImageSize = GeminiResolution.Values[Math.Max(0, geminiImageSize.SelectedIndex)];
            settings.UiScalePercent = UiScale.PercentValues[Math.Max(0, uiScale.SelectedIndex)];
            store.Save(settings);
            DialogResult = DialogResult.OK;
            Close();
        }

        private void ClearKey(object sender, EventArgs e)
        {
            store.SetApiKey(editingProvider, "");
            apiKey.Text = "";
            keyState.Text = "尚未保存 Key";
        }

        private void OpenProjectLink(object sender, LinkLabelLinkClickedEventArgs e)
        {
            string url = e.Link.LinkData as string ?? "https://github.com/Gara-11/photosense/";
            try
            {
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "无法打开浏览器。请手动访问：\n" + url + "\n\n" + ex.Message,
                    "PhotoSense 免费开源项目", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void BrowseEsrgan(object sender, EventArgs e)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "选择 realesrgan-ncnn-vulkan.exe";
                dialog.Filter = "Real-ESRGAN|realesrgan-ncnn-vulkan.exe|EXE 文件|*.exe";
                if (dialog.ShowDialog(this) == DialogResult.OK) esrganPath.Text = dialog.FileName;
            }
        }

        private static void AddRow(TableLayoutPanel table, int row, string label, Control control)
        {
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Label title = new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.TopLeft, Padding = new Padding(0, 10, 8, 0), ForeColor = UiTheme.Muted, AutoSize = true };
            control.Margin = new Padding(0, 6, 0, 6);
            table.Controls.Add(title, 0, row);
            table.Controls.Add(control, 1, row);
        }

        private static Label Title(string code, string text)
        {
            Label label = new Label { Text = code + "\n" + text, Font = new Font("Microsoft YaHei UI", 11f, FontStyle.Bold), ForeColor = UiTheme.Text, AutoSize = true, Padding = new Padding(0, 6, 0, 12) };
            return label;
        }

        private static TextBox Field()
        {
            return new TextBox { Dock = DockStyle.Fill, BackColor = UiTheme.Field, ForeColor = UiTheme.Text, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Microsoft YaHei UI", 9.5f) };
        }

        private static ComboBox Combo()
        {
            ComboBox combo = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = UiTheme.Field, ForeColor = UiTheme.Text, FlatStyle = FlatStyle.Flat, DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 24 };
            combo.DrawItem += delegate(object sender, DrawItemEventArgs e)
            {
                if (e.Index < 0) return;
                bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
                using (Brush fill = new SolidBrush(selected ? Color.FromArgb(47, 54, 35) : UiTheme.Field)) e.Graphics.FillRectangle(fill, e.Bounds);
                TextRenderer.DrawText(e.Graphics, Convert.ToString(combo.Items[e.Index]), combo.Font, Rectangle.Inflate(e.Bounds, -6, 0), selected ? UiTheme.AccentBright : UiTheme.Text, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            };
            return combo;
        }

        private static Button SmallButton(string text)
        {
            return new ModernButton { Text = text, Height = 34, Width = 100, Accent = false };
        }

        private static Button AccentButton(string text)
        {
            ModernButton button = (ModernButton)SmallButton(text);
            button.Accent = true;
            return button;
        }
    }
}
