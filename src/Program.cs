using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Security.Principal;
using System.Windows.Forms;

namespace PixelPatchStudio
{
    internal static class Program
    {
        public static bool TryRestartAsAdministrator(IWin32Window owner)
        {
            if (IsAdministrator()) return false;
            DialogResult choice = MessageBox.Show(
                owner,
                "检测到 Photoshop 正在以管理员权限运行。PhotoSense 需要使用相同权限才能读取和回写当前文档。\n\n是否现在自动重启 PhotoSense？Photoshop 不会被关闭。",
                "重新连接 Photoshop",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information);
            if (choice != DialogResult.Yes) return false;

            try
            {
                ProcessStartInfo start = new ProcessStartInfo
                {
                    FileName = Application.ExecutablePath,
                    WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory,
                    UseShellExecute = true,
                    Verb = "runas"
                };
                Process.Start(start);
                Application.Exit();
                return true;
            }
            catch (Win32Exception ex)
            {
                if (ex.NativeErrorCode != 1223)
                {
                    MessageBox.Show(owner, "无法以管理员权限重启：" + ex.Message, "PhotoSense", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                return false;
            }
        }

        private static bool IsAdministrator()
        {
            try
            {
                WindowsIdentity identity = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch { return false; }
        }

        [STAThread]
        private static int Main(string[] args)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            ServicePointManager.Expect100Continue = false;
            ServicePointManager.DefaultConnectionLimit = Math.Max(ServicePointManager.DefaultConnectionLimit, 8);

            if (args.Length > 0 && string.Equals(args[0], "--self-test", StringComparison.OrdinalIgnoreCase))
            {
                return SelfTests.Run();
            }
            if (args.Length > 1 && string.Equals(args[0], "--render-ui", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    Environment.SetEnvironmentVariable("PIXELPATCH_DATA_DIR", Path.Combine(Path.GetTempPath(), "PixelPatchStudio-UiTest"));
                    if (args.Length > 3) Environment.SetEnvironmentVariable("PHOTOSENSE_UI_SCALE", args[3]);
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    using (MainForm form = new MainForm())
                    {
                        form.WindowState = FormWindowState.Normal;
                        int renderWidth;
                        int renderHeight;
                        form.Size = args.Length > 5 && int.TryParse(args[4], out renderWidth) && int.TryParse(args[5], out renderHeight)
                            ? new Size(Math.Max(900, renderWidth), Math.Max(600, renderHeight))
                            : new Size(1440, 900);
                        form.Show();
                        Application.DoEvents();
                        int page;
                        if (args.Length > 2 && int.TryParse(args[2], out page)) form.DebugShowToolPage(page);
                        if (args.Length > 6 && string.Equals(args[6], "esrgan-checked", StringComparison.OrdinalIgnoreCase))
                            form.DebugSetRealEsrganChecked(true);
                        Application.DoEvents();
                        using (Bitmap screenshot = new Bitmap(form.Width, form.Height))
                        {
                            form.DrawToBitmap(screenshot, new Rectangle(Point.Empty, form.Size));
                            screenshot.Save(args[1]);
                        }
                        form.Close();
                    }
                    return 0;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("RENDER_FAILED " + ex.GetType().FullName + ": " + ex.Message);
                    if (ex.StackTrace != null) Console.Error.WriteLine(ex.StackTrace);
                    return 2;
                }
            }
            if (args.Length > 1 && string.Equals(args[0], "--render-settings", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    Environment.SetEnvironmentVariable("PIXELPATCH_DATA_DIR", Path.Combine(Path.GetTempPath(), "PixelPatchStudio-SettingsTest"));
                    if (args.Length > 2) Environment.SetEnvironmentVariable("PHOTOSENSE_UI_SCALE", args[2]);
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    SettingsStore store = new SettingsStore();
                    AppSettings settings = store.Load();
                    using (SettingsDialog dialog = new SettingsDialog(store, settings))
                    {
                        dialog.Show();
                        Application.DoEvents();
                        using (Bitmap screenshot = new Bitmap(dialog.Width, dialog.Height))
                        {
                            dialog.DrawToBitmap(screenshot, new Rectangle(Point.Empty, dialog.Size));
                            screenshot.Save(args[1]);
                        }
                        dialog.Close();
                    }
                    return 0;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("SETTINGS_RENDER_FAILED " + ex);
                    return 8;
                }
            }
            if (args.Length > 0 && string.Equals(args[0], "--photoshop-probe", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    using (PhotoshopBridge bridge = new PhotoshopBridge())
                    {
                        PhotoshopImage info = bridge.ExportActiveDocument(Path.Combine(Path.GetTempPath(), "PixelPatchStudio-PhotoshopProbe"));
                        Console.WriteLine("PHOTOSHOP_OK " + info.DocumentName + " " + info.Width + "x" + info.Height);
                    }
                    return 0;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("PHOTOSHOP_PROBE " + ex.GetType().FullName + ": " + ex.Message);
                    return 3;
                }
            }
            if (args.Length > 3 && string.Equals(args[0], "--patch-probe", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    using (Image sourceImage = Image.FromFile(args[1]))
                    using (Image maskImage = Image.FromFile(args[2]))
                    using (Bitmap source = new Bitmap(sourceImage))
                    using (Bitmap mask = new Bitmap(maskImage))
                    using (Bitmap patch = ImageComposer.CreatePatch(source, mask))
                    {
                        patch.SetResolution(source.HorizontalResolution, source.VerticalResolution);
                        patch.Save(args[3], System.Drawing.Imaging.ImageFormat.Png);
                    }
                    Console.WriteLine("PATCH_PROBE_OK " + new FileInfo(args[3]).Length);
                    return 0;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("PATCH_PROBE_FAILED " + ex);
                    return 4;
                }
            }
            if (args.Length > 2 && string.Equals(args[0], "--api-payload-probe", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    using (Image sourceImage = Image.FromFile(args[1]))
                    using (Image maskImage = Image.FromFile(args[2]))
                    using (Bitmap source = new Bitmap(sourceImage))
                    using (Bitmap mask = new Bitmap(maskImage))
                    using (Bitmap requestSource = ImageApiClient.ResizeForApi(source, 2048, System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic))
                    using (Bitmap requestSelection = ImageApiClient.ResizeForApi(mask, 2048, System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor))
                    using (Bitmap requestMask = ImageComposer.PrepareOpenAiMask(requestSelection))
                    using (MemoryStream sourceBytes = new MemoryStream())
                    using (MemoryStream maskBytes = new MemoryStream())
                    {
                        requestSource.Save(sourceBytes, System.Drawing.Imaging.ImageFormat.Png);
                        requestMask.Save(maskBytes, System.Drawing.Imaging.ImageFormat.Png);
                        Console.WriteLine("API_PAYLOAD_OK " + requestSource.Width + "x" + requestSource.Height + " " + (sourceBytes.Length + maskBytes.Length));
                    }
                    return 0;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("API_PAYLOAD_PROBE_FAILED " + ex);
                    return 5;
                }
            }
            if (args.Length > 1 && string.Equals(args[0], "--brush-benchmark", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    Application.EnableVisualStyles();
                    using (Image image = Image.FromFile(args[1]))
                    using (Bitmap bitmap = new Bitmap(image))
                    using (ImageCanvas benchmark = new ImageCanvas())
                    {
                        benchmark.Size = new Size(1600, 900);
                        benchmark.CreateControl();
                        benchmark.SetImage(bitmap);
                        benchmark.FitToWindow();
                        benchmark.BrushSize = 96;
                        PointF from = new PointF(bitmap.Width * 0.18f, bitmap.Height * 0.2f);
                        PointF to = new PointF(bitmap.Width * 0.82f, bitmap.Height * 0.8f);
                        Stopwatch timer = Stopwatch.StartNew();
                        benchmark.DebugPaintStroke(from, to, 400, PaintMode.Add);
                        timer.Stop();
                        int tiles = benchmark.DebugUndoTileCount;
                        benchmark.UndoMask();
                        benchmark.RedoMask();
                        Console.WriteLine("BRUSH_BENCHMARK_OK " + bitmap.Width + "x" + bitmap.Height + " " + timer.ElapsedMilliseconds + "ms " + tiles + "tiles");
                    }
                    return 0;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("BRUSH_BENCHMARK_FAILED " + ex);
                    return 6;
                }
            }
            if (args.Length > 1 && string.Equals(args[0], "--zoom-benchmark", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    Application.EnableVisualStyles();
                    using (Image image = Image.FromFile(args[1]))
                    using (Bitmap bitmap = new Bitmap(image))
                    using (ImageCanvas benchmark = new ImageCanvas())
                    using (Bitmap frame = new Bitmap(1400, 820, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                    {
                        benchmark.Size = frame.Size;
                        benchmark.CreateControl();
                        benchmark.SetImage(bitmap);
                        benchmark.FitToWindow();
                        benchmark.BrushSize = 140;
                        benchmark.DebugPaintStroke(new PointF(bitmap.Width * 0.2f, bitmap.Height * 0.25f), new PointF(bitmap.Width * 0.8f, bitmap.Height * 0.75f), 40, PaintMode.Add);
                        benchmark.DrawToBitmap(frame, new Rectangle(Point.Empty, frame.Size));
                        Point center = new Point(frame.Width / 2, frame.Height / 2);
                        const int frames = 80;
                        Stopwatch timer = Stopwatch.StartNew();
                        for (int i = 0; i < frames; i++)
                        {
                            benchmark.DebugZoomAt(center, i < frames / 2 ? 120 : -120);
                            benchmark.DrawToBitmap(frame, new Rectangle(Point.Empty, frame.Size));
                        }
                        timer.Stop();
                        Stopwatch settle = Stopwatch.StartNew();
                        benchmark.DebugFinishInteraction();
                        benchmark.DrawToBitmap(frame, new Rectangle(Point.Empty, frame.Size));
                        settle.Stop();
                        Console.WriteLine("ZOOM_BENCHMARK_OK " + bitmap.Width + "x" + bitmap.Height + " " + frames + "frames " + timer.ElapsedMilliseconds + "ms " + Math.Round(timer.Elapsed.TotalMilliseconds / frames, 2) + "ms/frame settle=" + settle.ElapsedMilliseconds + "ms");
                    }
                    return 0;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("ZOOM_BENCHMARK_FAILED " + ex);
                    return 9;
                }
            }
            if (args.Length > 2 && string.Equals(args[0], "--brush-render", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    Application.EnableVisualStyles();
                    using (Image image = Image.FromFile(args[1]))
                    using (Bitmap bitmap = new Bitmap(image))
                    using (ImageCanvas canvas = new ImageCanvas())
                    {
                        canvas.Size = new Size(1000, 700);
                        canvas.CreateControl();
                        canvas.SetImage(bitmap);
                        canvas.FitToWindow();
                        canvas.BrushSize = 240;
                        canvas.DebugPaintStroke(new PointF(bitmap.Width * 0.12f, bitmap.Height * 0.35f), new PointF(bitmap.Width * 0.88f, bitmap.Height * 0.68f), 400, PaintMode.Add);
                        using (Bitmap rendered = new Bitmap(canvas.Width, canvas.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                        {
                            canvas.DrawToBitmap(rendered, new Rectangle(Point.Empty, canvas.Size));
                            rendered.Save(args[2], System.Drawing.Imaging.ImageFormat.Png);
                        }
                    }
                    Console.WriteLine("BRUSH_RENDER_OK " + args[2]);
                    return 0;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("BRUSH_RENDER_FAILED " + ex);
                    return 7;
                }
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException += delegate(object sender, System.Threading.ThreadExceptionEventArgs e)
            {
                MessageBox.Show(e.Exception.Message, "PhotoSense", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };

            Application.Run(new MainForm());
            return 0;
        }
    }

    internal static class SelfTests
    {
        public static int Run()
        {
            string root = Path.Combine(Path.GetTempPath(), "PixelPatchStudio-SelfTest-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                using (Bitmap source = new Bitmap(8, 8))
                using (Bitmap generated = new Bitmap(8, 8))
                using (Bitmap mask = new Bitmap(8, 8))
                {
                    using (Graphics gs = Graphics.FromImage(source)) gs.Clear(Color.FromArgb(255, 10, 20, 30));
                    using (Graphics gg = Graphics.FromImage(generated)) gg.Clear(Color.FromArgb(255, 200, 100, 50));
                    using (Graphics gm = Graphics.FromImage(mask))
                    {
                        gm.Clear(Color.Black);
                        gm.FillRectangle(Brushes.White, 2, 2, 3, 3);
                    }

                    using (Bitmap composite = ImageComposer.Composite(source, generated, mask))
                    using (Bitmap patch = ImageComposer.CreatePatch(generated, mask))
                    {
                        Assert(composite.GetPixel(0, 0).ToArgb() == source.GetPixel(0, 0).ToArgb(), "蒙版外像素被修改");
                        Assert(composite.GetPixel(3, 3).ToArgb() == generated.GetPixel(3, 3).ToArgb(), "蒙版内像素未替换");
                        Assert(patch.GetPixel(0, 0).A == 0, "补丁蒙版外必须透明");
                        Assert(patch.GetPixel(3, 3).A == 255, "补丁蒙版内必须不透明");
                    }
                    using (Bitmap apiMask = ImageComposer.PrepareOpenAiMask(mask))
                    {
                        Assert(apiMask.GetPixel(0, 0).A == 255, "OpenAI 蒙版外必须不透明");
                        Assert(apiMask.GetPixel(3, 3).A == 0, "OpenAI 重绘区必须透明");
                    }
                    using (Bitmap renderedMask = new Bitmap(2, 1))
                    {
                        renderedMask.SetPixel(0, 0, Color.Black);
                        renderedMask.SetPixel(1, 0, Color.White);
                        using (Bitmap renderedOverlay = ImageCanvas.BuildOverlay(renderedMask))
                        {
                            Assert(renderedOverlay.GetPixel(0, 0).A == 0, "未选区预览必须完全透明");
                            Assert(renderedOverlay.GetPixel(1, 0).A == 105, "选区预览透明度错误");
                        }
                        Assert(Math.Abs(ImageComposer.SelectionCoverage(renderedMask) - 0.5) < 0.001, "蒙版覆盖率计算失败");
                    }
                }

                Assert(ApiUrl.Combine("https://api.openai.com/", "/v1/images/edits") == "https://api.openai.com/v1/images/edits", "URL 拼接失败");
                Assert(ApiUrl.Combine("https://api.intenext.ai/v1", "/v1/images/edits") == "https://api.intenext.ai/v1/images/edits", "中转 URL 重复路径未去除");
                Assert(UiScale.NormalizePercent(125) == 125 && UiScale.NormalizePercent(113) == 0 &&
                    UiScale.PercentValues[UiScale.IndexOfPercent(175)] == 175,
                    "UI Scale 选项归一化失败");
                using (ModernCheckBox checkBox = new ModernCheckBox())
                {
                    bool changed = false;
                    checkBox.CheckedChanged += delegate { changed = true; };
                    checkBox.DebugPerformClick();
                    Assert(checkBox.Checked && changed, "Real-ESRGAN 复选框点击未切换选中状态");
                }
                AppSettings relaySettings = new AppSettings
                {
                    GeminiBaseUrl = "https://api.vectorengine.ai/v1",
                    GeminiEndpoint = "/v1beta/models/{model}:generateContent",
                    GeminiModel = "gemini-3.1-flash-image:generateContent"
                };
                Assert(ImageApiClient.UsesGeminiGenerateContent(relaySettings), "没有识别 Nano Banana generateContent 中转协议");
                Assert(ImageApiClient.GeminiRequestUrl(relaySettings) == "https://api.vectorengine.ai/v1beta/models/gemini-3.1-flash-image:generateContent", "Nano Banana 中转请求地址拼接失败");
                var relayPayload = ImageApiClient.BuildGeminiPayload(relaySettings, "prompt", "source64", "mask64");
                var testJson = new System.Web.Script.Serialization.JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                string serializedRelay = testJson.Serialize(relayPayload);
                Assert(serializedRelay.IndexOf("\"contents\"", StringComparison.Ordinal) >= 0 &&
                    serializedRelay.IndexOf("\"inline_data\"", StringComparison.Ordinal) >= 0 &&
                    serializedRelay.IndexOf("\"responseModalities\"", StringComparison.Ordinal) >= 0 &&
                    serializedRelay.IndexOf("\"input\"", StringComparison.Ordinal) < 0,
                    "Nano Banana generateContent 请求体格式错误");
                AppSettings interactionsSettings = new AppSettings
                {
                    GeminiBaseUrl = "https://generativelanguage.googleapis.com",
                    GeminiEndpoint = "/v1beta/interactions",
                    GeminiModel = "gemini-3.1-flash-image"
                };
                Assert(!ImageApiClient.UsesGeminiGenerateContent(interactionsSettings), "官方 Interactions 协议被错误识别为 generateContent");
                string serializedInteractions = testJson.Serialize(ImageApiClient.BuildGeminiPayload(interactionsSettings, "prompt", "source64", "mask64"));
                Assert(serializedInteractions.IndexOf("\"input\"", StringComparison.Ordinal) >= 0 && serializedInteractions.IndexOf("\"model\"", StringComparison.Ordinal) >= 0,
                    "Nano Banana Interactions 请求体格式错误");
                object sampleGeminiResponse = testJson.DeserializeObject("{\"candidates\":[{\"content\":{\"parts\":[{\"inlineData\":{\"mimeType\":\"image/png\",\"data\":\"aW1hZ2U=\"}}]}}]}");
                Assert(ImageApiClient.FindImageData(sampleGeminiResponse) == "aW1hZ2U=", "generateContent 图片响应解析失败");
                using (Bitmap large = new Bitmap(300, 450))
                using (Bitmap resized = ImageApiClient.ResizeForApi(large, 200, System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic))
                {
                    Assert(resized.Width == 133 && resized.Height == 200, "接口图片等比例缩放失败");
                }
                string dpiPng = Path.Combine(root, "dpi-test.png");
                using (Bitmap dpiBitmap = new Bitmap(2, 2))
                {
                    dpiBitmap.SetResolution(300f, 300f);
                    dpiBitmap.Save(dpiPng, System.Drawing.Imaging.ImageFormat.Png);
                }
                using (Image dpiReloaded = Image.FromFile(dpiPng)) Assert(Math.Abs(dpiReloaded.HorizontalResolution - 300f) < 1f, "PNG 没有保留 Photoshop 文档 DPI");
                using (Bitmap tileSource = new Bitmap(600, 600, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                {
                    using (Graphics tileGraphics = Graphics.FromImage(tileSource)) tileGraphics.FillRectangle(Brushes.White, 520, 520, 20, 20);
                    var tiles = ImageComposer.SavePatchTiles(tileSource, Path.Combine(root, "tile-test"), 256, 300f);
                    Assert(tiles.Count == 1 && tiles[0].X == 512 && tiles[0].Y == 512, "透明补丁分块没有跳过空白区域");
                    string tileScript = PhotoshopBridge.BuildPlaceTilesScript(tiles, "PixelPatch", 123);
                    Assert(tileScript.IndexOf("layerSets.add", StringComparison.Ordinal) >= 0 && tileScript.IndexOf("stringIDToTypeID('linked')", StringComparison.Ordinal) >= 0, "Photoshop 分块回写脚本无效");
                }
                using (Bitmap paintSource = new Bitmap(1600, 1200, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                using (ImageCanvas paintCanvas = new ImageCanvas())
                {
                    paintCanvas.Size = new Size(1000, 700);
                    paintCanvas.CreateControl();
                    paintCanvas.SetImage(paintSource);
                    paintCanvas.FitToWindow();
                    paintCanvas.BrushSize = 80;
                    paintCanvas.DebugPaintStroke(new PointF(200, 200), new PointF(1400, 1000), 120, PaintMode.Add);
                    Assert(paintCanvas.DebugMaskPixel(800, 600).R > 240, "增量笔刷没有写入蒙版");
                    Assert(paintCanvas.DebugUndoTileCount > 0 && paintCanvas.DebugUndoTileCount < 24, "笔刷撤销没有使用稀疏分块");
                    paintCanvas.UndoMask();
                    Assert(paintCanvas.DebugMaskPixel(800, 600).R < 15, "分块撤销失败");
                    paintCanvas.RedoMask();
                    Assert(paintCanvas.DebugMaskPixel(800, 600).R > 240, "分块重做失败");
                }
                Environment.SetEnvironmentVariable("PIXELPATCH_DATA_DIR", root);
                SettingsStore store = new SettingsStore();
                AppSettings saved = new AppSettings();
                saved.Provider = "Nano Banana";
                saved.GeminiBaseUrl = "https://api.vectorengine.ai/v1";
                saved.GeminiEndpoint = "/v1beta/models";
                saved.GeminiModel = "test-image-model:generateContent";
                saved.UiScalePercent = 125;
                store.Save(saved);
                store.SetApiKey(ApiProvider.NanoBanana, "self-test-secret");
                AppSettings loaded = store.Load();
                Assert(loaded.Provider == "Nano Banana" && loaded.GeminiBaseUrl == "https://api.vectorengine.ai" &&
                    loaded.GeminiEndpoint == "/v1beta/models/{model}:generateContent" && loaded.GeminiModel == "test-image-model",
                    "Nano Banana 中转旧配置迁移失败");
                Assert(loaded.UiScalePercent == 125, "UI Scale 设置持久化失败");
                Assert(store.GetApiKey(ApiProvider.NanoBanana) == "self-test-secret", "DPAPI Key 持久化失败");

                string placeScript = PhotoshopBridge.BuildPlacePatchScript("C:\\tmp\\patch.png", "PixelPatch", 123);
                Assert(placeScript.IndexOf("charIDToTypeID('Plc ')", StringComparison.Ordinal) >= 0, "Photoshop 回写没有使用置入通道");
                Assert(placeScript.IndexOf("stringIDToTypeID('linked')", StringComparison.Ordinal) >= 0, "Photoshop 回写没有使用链接置入");
                Assert(placeScript.IndexOf(".duplicate(target", StringComparison.Ordinal) < 0, "Photoshop 回写仍在使用跨文档复制");
                Assert(placeScript.IndexOf("selection.copy", StringComparison.Ordinal) < 0, "Photoshop 回写仍在使用剪贴板");

                string packageRoot = Path.Combine(root, "fake-esrgan-package");
                string modelsRoot = Path.Combine(packageRoot, "models");
                Directory.CreateDirectory(modelsRoot);
                File.WriteAllBytes(Path.Combine(packageRoot, "realesrgan-ncnn-vulkan.exe"), new byte[] { (byte)'M', (byte)'Z', 0, 0 });
                File.WriteAllText(Path.Combine(modelsRoot, "realesrgan-x4plus.param"), "test");
                File.WriteAllBytes(Path.Combine(modelsRoot, "realesrgan-x4plus.bin"), new byte[] { 1, 2, 3 });
                string packageZip = Path.Combine(root, "fake-esrgan.zip");
                ZipFile.CreateFromDirectory(packageRoot, packageZip);
                string installed = new RealEsrganService().InstallFromArchive(packageZip, Path.Combine(root, "installed-esrgan"));
                Assert(File.Exists(installed), "Real-ESRGAN 自动部署未复制主程序");
                Assert(File.Exists(Path.Combine(Path.GetDirectoryName(installed), "models", "realesrgan-x4plus.param")), "Real-ESRGAN 自动部署未复制模型");

                Environment.SetEnvironmentVariable("PIXELPATCH_DATA_DIR", null);
                Console.WriteLine("SELF_TEST_OK");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("SELF_TEST_FAILED: " + ex);
                return 1;
            }
            finally
            {
                try { Directory.Delete(root, true); } catch { }
            }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
