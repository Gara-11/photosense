using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PixelPatchStudio
{
    internal sealed partial class MainForm
    {
        private TextBox relightPrompt;
        private ModernTrackBar relightProtection;
        private Label relightProtectionValue;
        private ComboBox relightCandidateCount;
        private Button relightGenerateButton;
        private Button relightCancelButton;

        private Panel BuildRelightPage()
        {
            Panel page = ToolPage("RELIGHT", "光影重绘", "AI 只提供低频光影引导，原图细节本地保留");
            Label promptLabel = SectionCaption("光影描述"); promptLabel.Location = new Point(16, 94);
            relightPrompt = new TextBox
            {
                Location = new Point(16, 120),
                Size = new Size(292, 104),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = UiTheme.Field,
                ForeColor = UiTheme.Text,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Microsoft YaHei UI", 9.5f),
                Text = "自然柔和的侧光，保持场景原有氛围，光影方向与环境一致"
            };
            Label protectionLabel = SectionCaption("人物 / 细节保护"); protectionLabel.Location = new Point(16, 242);
            relightProtectionValue = new Label
            {
                Text = "92%",
                Location = new Point(238, 238),
                Size = new Size(70, 24),
                TextAlign = ContentAlignment.MiddleRight,
                ForeColor = UiTheme.AccentBright,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            };
            relightProtection = new ModernTrackBar { Minimum = 70, Maximum = 100, Value = 92, Location = new Point(16, 268), Width = 292 };
            relightProtection.ValueChanged += delegate { relightProtectionValue.Text = relightProtection.Value + "%"; };
            Label protection = PageHint("最终结果不会使用 AI 重画的人脸或人物纹理，只从 AI 图中提取平滑的明暗、阴影与色温差，再叠加到原图像素。", 16, 310, 292, 76);
            Button editMask = ButtonOf("前往蒙版页调整光影范围", false);
            editMask.SetBounds(16, 400, 292, 36);
            editMask.Click += delegate { ShowToolPage(1); };

            Label countLabel = SectionCaption("生成张数"); countLabel.Location = new Point(16, 456);
            relightCandidateCount = CandidateCombo(16, 482);
            Label cost = new Label
            {
                Text = "每张候选分别调用 1 次当前 API，费用相应增加。",
                Location = new Point(116, 484),
                Size = new Size(192, 38),
                ForeColor = UiTheme.AccentBright,
                Font = new Font("Microsoft YaHei UI", 8f, FontStyle.Bold)
            };
            relightGenerateButton = ButtonOf("开始光影重绘", true);
            relightGenerateButton.SetBounds(16, 548, 292, 44);
            relightGenerateButton.Font = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold);
            relightGenerateButton.Click += GenerateRelight;
            relightCancelButton = ButtonOf("取消当前任务", false);
            relightCancelButton.SetBounds(16, 604, 292, 36);
            relightCancelButton.Visible = false;
            relightCancelButton.Click += delegate { if (cancellation != null) cancellation.Cancel(); };

            page.Controls.Add(relightCancelButton);
            page.Controls.Add(relightGenerateButton);
            page.Controls.Add(cost);
            page.Controls.Add(relightCandidateCount);
            page.Controls.Add(countLabel);
            page.Controls.Add(editMask);
            page.Controls.Add(protection);
            page.Controls.Add(relightProtection);
            page.Controls.Add(relightProtectionValue);
            page.Controls.Add(protectionLabel);
            page.Controls.Add(relightPrompt);
            page.Controls.Add(promptLabel);
            return page;
        }

        private static ComboBox CandidateCombo(int x, int y)
        {
            ComboBox combo = new ComboBox
            {
                Location = new Point(x, y),
                Size = new Size(92, 34),
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                BackColor = UiTheme.Field,
                ForeColor = UiTheme.Text
            };
            combo.Items.AddRange(new object[] { "1 张", "2 张", "3 张", "4 张" });
            combo.SelectedIndex = 0;
            StyleComboBox(combo);
            return combo;
        }

        private async void GenerateRelight(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(relightPrompt.Text))
            {
                MessageBox.Show(this, "请描述希望得到的光线、阴影或色温效果。", "光影重绘", MessageBoxButtons.OK, MessageBoxIcon.Information);
                relightPrompt.Focus();
                return;
            }
            if (!ValidateOperationSelection("光影重绘")) return;
            string requestPrompt = ImageApiClient.BuildOperationPrompt(settings.SelectedProvider, GenerationOperation.Relight, relightPrompt.Text);
            await RunOperationAsync(GenerationOperation.Relight, requestPrompt, relightCandidateCount.SelectedIndex + 1, relightProtection.Value);
        }

        private bool ValidateOperationSelection(string title)
        {
            if (!RequireImage()) return false;
            using (Bitmap mask = canvas.GetMaskCopy())
            {
                if (!ImageComposer.HasSelection(mask))
                {
                    MessageBox.Show(this, "请先在蒙版页选中允许处理的范围。", title, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return false;
                }
            }
            return true;
        }

        private Task<string> EnsureOperationApiKeyAsync()
        {
            string key = store.GetApiKey(settings.SelectedProvider);
            if (!string.IsNullOrEmpty(key)) return Task.FromResult(key);
            using (SettingsDialog dialog = new SettingsDialog(store, settings))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return Task.FromResult<string>(null);
            }
            settings = store.Load();
            UpdateProviderUi();
            key = store.GetApiKey(settings.SelectedProvider);
            if (!string.IsNullOrEmpty(key)) return Task.FromResult(key);
            MessageBox.Show(this, "需要为当前服务填写 API Key。", "API Key", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return Task.FromResult<string>(null);
        }

        private async Task RunOperationAsync(GenerationOperation operation, string requestPrompt, int count, int detailProtection)
        {
            string key = await EnsureOperationApiKeyAsync();
            if (string.IsNullOrEmpty(key)) return;
            GenerationPlan plan = GenerationPlan.Create(settings.SelectedProvider, count);
            cancellation = new CancellationTokenSource();
            SetBusy(true);
            ClearGeneratedResult();
            try
            {
                using (Bitmap source = canvas.GetSourceCopy())
                using (Bitmap mask = canvas.GetMaskCopy())
                using (ImageApiClient client = new ImageApiClient(settings.ApiTimeoutSeconds))
                {
                    string warning = null;
                    Exception partialFailure = null;
                    for (int index = 0; index < plan.CandidateCount; index++)
                    {
                        try
                        {
                            GenerationBuild build = await GenerateOperationVersionAsync(client, source, mask, key, requestPrompt, operation, detailProtection, index + 1, plan.CandidateCount, cancellation.Token);
                            generationVersions.Add(build.Version);
                            if (string.IsNullOrEmpty(warning)) warning = build.ResolutionWarning;
                            RefreshResultStrip();
                        }
                        catch (OperationCanceledException) { throw; }
                        catch (Exception ex) { partialFailure = ex; break; }
                    }
                    if (generationVersions.Count == 0 && partialFailure != null) throw partialFailure;
                    if (generationVersions.Count == 0) throw new InvalidOperationException("接口没有生成可用结果。");
                    ActivateGeneration(0, true);
                    if (!string.IsNullOrEmpty(warning))
                        MessageBox.Show(this, warning, "Nano Banana 分辨率未生效", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    if (partialFailure != null)
                        MessageBox.Show(this, "已完成 " + generationVersions.Count + " / " + plan.CandidateCount + " 张，其余请求失败。\n\n" + partialFailure.Message, "部分候选生成失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                status.Text = OperationName(operation) + "完成；蒙版外仍为当前输入图的原始像素";
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

        private async Task<GenerationBuild> GenerateOperationVersionAsync(ImageApiClient client, Bitmap source, Bitmap mask, string key, string requestPrompt, GenerationOperation operation, int detailProtection, int ordinal, int total, CancellationToken token)
        {
            SetStatus("正在" + OperationName(operation) + " " + ordinal + " / " + total + " · " + ProviderDescription() + "…");
            Bitmap apiResult = await client.GenerateAsync(source, mask, null, requestPrompt, settings, key, token);
            try
            {
                string warning = ImageApiClient.GeminiResolutionWarning(settings, apiResult.Width, apiResult.Height);
                token.ThrowIfCancellationRequested();
                string path = Path.Combine(resultSessionDirectory, DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N") + ".png");
                apiResult.Save(path, ImageFormat.Png);
                try
                {
                    using (Bitmap localComposite = ComposeOperation(source, apiResult, mask, operation, detailProtection))
                    {
                        return new GenerationBuild
                        {
                            Version = new GenerationVersion
                            {
                                GeneratedPath = path,
                                Thumbnail = BuildResultThumbnail(localComposite, 160, 112),
                                ProviderLabel = ProviderDescription(),
                                Prompt = requestPrompt,
                                CreatedAt = DateTime.Now,
                                Operation = operation,
                                DetailProtection = detailProtection
                            },
                            ResolutionWarning = warning
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

        private static Bitmap ComposeOperation(Bitmap source, Bitmap generated, Bitmap mask, GenerationOperation operation, int detailProtection)
        {
            return operation == GenerationOperation.Relight
                ? ImageComposer.RelightComposite(source, generated, mask, detailProtection)
                : ImageComposer.Composite(source, generated, mask);
        }

        private static string OperationName(GenerationOperation operation)
        {
            if (operation == GenerationOperation.Relight) return "光影重绘";
            return "局部重绘";
        }

        private async Task RegenerateOperationResultAsync(int replaceIndex, GenerationVersion version)
        {
            if (!ValidateOperationSelection(OperationName(version.Operation))) return;
            string key = await EnsureOperationApiKeyAsync();
            if (string.IsNullOrEmpty(key)) return;
            cancellation = new CancellationTokenSource();
            SetBusy(true);
            try
            {
                using (Bitmap source = canvas.GetSourceCopy())
                using (Bitmap mask = canvas.GetMaskCopy())
                using (ImageApiClient client = new ImageApiClient(settings.ApiTimeoutSeconds))
                {
                    GenerationBuild build = await GenerateOperationVersionAsync(client, source, mask, key, version.Prompt, version.Operation, version.DetailProtection, 1, 1, cancellation.Token);
                    ReleaseActiveResultBitmaps();
                    GenerationVersion previous = generationVersions[replaceIndex];
                    generationVersions[replaceIndex] = build.Version;
                    previous.Dispose();
                    activeGenerationIndex = -1;
                    ActivateGeneration(replaceIndex, true);
                }
                status.Text = "当前" + OperationName(version.Operation) + "候选已重新生成";
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

        private void ContinueWithResult(object sender, EventArgs e)
        {
            if (activeComposite == null) return;
            Bitmap next = new Bitmap(activeComposite);
            Bitmap merged = null;
            using (Bitmap currentMask = canvas.GetMaskCopy())
                merged = ImageComposer.UnionMasks(chainedMask, currentMask);
            if (chainedMask != null) chainedMask.Dispose();
            chainedMask = merged;
            chainedStepCount++;
            ClearGeneratedResult();
            using (next) canvas.SetImage(next);
            canvas.ShowMaskOverlay = true;
            canvas.MaskViewMode = MaskViewMode.Overlay;
            UpdateMaskCoverage();
            imageInfo.Text = photoshopSource.DocumentName + "\n" + photoshopSource.Width + " × " + photoshopSource.Height + " · 已串联 " + chainedStepCount + " 步";
            status.Text = "当前结果已作为下一步输入；可重新画蒙版后选择局部重绘、移除/替换或光影重绘";
            ShowToolPage(1);
        }
    }
}
