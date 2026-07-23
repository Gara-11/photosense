using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
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
                        if (args.Length > 6 && string.Equals(args[6], "reference-loaded", StringComparison.OrdinalIgnoreCase))
                        {
                            using (Bitmap reference = new Bitmap(320, 180))
                            using (Graphics referenceGraphics = Graphics.FromImage(reference))
                            {
                                referenceGraphics.Clear(Color.FromArgb(224, 116, 164));
                                referenceGraphics.FillEllipse(Brushes.PeachPuff, 44, 22, 104, 136);
                                referenceGraphics.FillRectangle(Brushes.MediumPurple, 164, 30, 118, 112);
                                form.DebugSetReferenceImage(reference, "reference-preview.jpg");
                            }
                        }
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
                    if (args.Length > 3 && string.Equals(args[3], "nano-4k", StringComparison.OrdinalIgnoreCase))
                    {
                        settings.Provider = "Nano Banana";
                        settings.GeminiImageSize = "4K";
                    }
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
            if (args.Length > 4 && string.Equals(args[0], "--smart-selection-probe", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    int pointX;
                    int pointY;
                    if (!int.TryParse(args[2], out pointX) || !int.TryParse(args[3], out pointY))
                        throw new ArgumentException("Smart selection probe coordinates must be integers.");
                    using (Image loaded = Image.FromFile(args[1]))
                    using (Bitmap source = new Bitmap(loaded))
                    using (Bitmap mask = new Bitmap(source.Width, source.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                    {
                        using (Graphics graphics = Graphics.FromImage(mask)) graphics.Clear(Color.Black);
                        SmartSelectionEngine engine = new SmartSelectionEngine(source);
                        SmartRegion region = engine.Select(new PointF(pointX, pointY));
                        engine.ApplyRegion(mask, region, true);
                        mask.Save(args[4], System.Drawing.Imaging.ImageFormat.Png);
                        Console.WriteLine("SMART_SELECTION_PROBE_OK " + region.PixelCount);
                    }
                    return 0;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("SMART_SELECTION_PROBE_FAILED " + ex);
                    return 9;
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
                        using (Bitmap blackWhiteOverlay = ImageCanvas.BuildOverlay(renderedMask, MaskViewMode.BlackWhite))
                        {
                            Assert(blackWhiteOverlay.GetPixel(0, 0).A == 255 &&
                                blackWhiteOverlay.GetPixel(0, 0).R == 0 &&
                                blackWhiteOverlay.GetPixel(1, 0).A == 255 &&
                                blackWhiteOverlay.GetPixel(1, 0).R == 255,
                                "黑白蒙版预览没有忠实显示选区");
                        }
                        Assert(Math.Abs(ImageComposer.SelectionCoverage(renderedMask) - 0.5) < 0.001, "蒙版覆盖率计算失败");
                    }
                }

                using (Bitmap refineMask = new Bitmap(9, 9))
                {
                    using (Graphics graphics = Graphics.FromImage(refineMask))
                    {
                        graphics.Clear(Color.Black);
                        graphics.FillRectangle(Brushes.White, 3, 3, 3, 3);
                    }
                    using (Bitmap expanded = ImageComposer.ExpandMask(refineMask, 1))
                    using (Bitmap contracted = ImageComposer.ContractMask(refineMask, 1))
                    using (Bitmap feathered = ImageComposer.FeatherMask(refineMask, 2))
                    using (Bitmap smoothed = ImageComposer.SmoothMask(refineMask, 1))
                    using (Bitmap outline = ImageCanvas.BuildOverlay(refineMask, MaskViewMode.Outline))
                    {
                        Assert(expanded.GetPixel(2, 4).R > 250 && expanded.GetPixel(1, 4).R < 5,
                            "扩展选区半径计算失败");
                        Assert(contracted.GetPixel(4, 4).R > 250 && contracted.GetPixel(3, 4).R < 5,
                            "收缩选区半径计算失败");
                        Assert(feathered.GetPixel(2, 4).R > 0 && feathered.GetPixel(2, 4).R < 255,
                            "羽化选区没有产生柔和边缘");
                        Assert((smoothed.GetPixel(4, 4).R == 0 || smoothed.GetPixel(4, 4).R == 255) &&
                            (smoothed.GetPixel(2, 4).R == 0 || smoothed.GetPixel(2, 4).R == 255),
                            "平滑选区输出不是二值蒙版");
                        Assert(outline.GetPixel(3, 3).A > 0 &&
                            outline.GetPixel(4, 4).A == 0 &&
                            outline.GetPixel(0, 0).A == 0,
                            "边缘预览没有只显示选区轮廓");
                    }
                }

                GenerationPlan gptDefaultPlan = GenerationPlan.Create(ApiProvider.GptImage2, 1);
                GenerationPlan nanoDefaultPlan = GenerationPlan.Create(ApiProvider.NanoBanana, 1);
                GenerationPlan gptMultiPlan = GenerationPlan.Create(ApiProvider.GptImage2, 4);
                GenerationPlan nanoMultiPlan = GenerationPlan.Create(ApiProvider.NanoBanana, 3);
                Assert(gptDefaultPlan.CandidateCount == 1 && nanoDefaultPlan.CandidateCount == 1,
                    "单候选模式必须为两个模型保留原有单次请求路径");
                Assert(gptMultiPlan.CandidateCount == 4 && nanoMultiPlan.CandidateCount == 3,
                    "多候选模式没有同时支持 GPT Image 2 与 Nano Banana");

                string originalPrompt = "original repaint prompt remains untouched";
                string gptRelightPrompt = ImageApiClient.BuildOperationPrompt(ApiProvider.GptImage2, GenerationOperation.Relight, "soft window light");
                string nanoRelightPrompt = ImageApiClient.BuildOperationPrompt(ApiProvider.NanoBanana, GenerationOperation.Relight, "soft window light");
                Assert(originalPrompt == "original repaint prompt remains untouched" &&
                    gptRelightPrompt.IndexOf("facial features", StringComparison.Ordinal) >= 0 &&
                    nanoRelightPrompt.IndexOf("facial features", StringComparison.Ordinal) >= 0 &&
                    gptRelightPrompt.IndexOf("ghost remnants", StringComparison.Ordinal) >= 0 &&
                    nanoRelightPrompt.IndexOf("ghost remnants", StringComparison.Ordinal) >= 0,
                    "光影提示词没有同时覆盖双模型、人物保护或防重影约束");
                Assert(ImageApiClient.BuildOperationPrompt(ApiProvider.GptImage2, GenerationOperation.Repaint, originalPrompt) == originalPrompt,
                    "关闭独立工作流时不应改变原局部重绘提示词");

                using (Bitmap relightSource = new Bitmap(24, 12, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                using (Bitmap relightGuide = new Bitmap(24, 12, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                using (Bitmap relightMask = new Bitmap(24, 12, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                {
                    for (int y = 0; y < relightSource.Height; y++)
                    {
                        for (int x = 0; x < relightSource.Width; x++)
                        {
                            int detail = (x + y) % 2 == 0 ? 60 : 105;
                            relightSource.SetPixel(x, y, Color.FromArgb(255, detail, detail + 10, detail + 20));
                            relightGuide.SetPixel(x, y, Color.FromArgb(255, 180, 155, 130));
                            relightMask.SetPixel(x, y, x >= 8 ? Color.White : Color.Black);
                        }
                    }
                    using (Bitmap relit = ImageComposer.RelightComposite(relightSource, relightGuide, relightMask, 92))
                    {
                        Assert(relit.GetPixel(3, 5).ToArgb() == relightSource.GetPixel(3, 5).ToArgb(),
                            "光影重绘修改了蒙版外像素");
                        int sourceContrast = relightSource.GetPixel(10, 4).R - relightSource.GetPixel(11, 4).R;
                        int resultContrast = relit.GetPixel(10, 4).R - relit.GetPixel(11, 4).R;
                        Assert(Math.Abs(sourceContrast - resultContrast) <= 3 &&
                            relit.GetPixel(10, 4).ToArgb() != relightGuide.GetPixel(10, 4).ToArgb(),
                            "光影合成没有保留原图高频细节，或直接采用了 AI 像素");
                    }
                    using (Bitmap secondMask = new Bitmap(24, 12, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                    {
                        secondMask.SetPixel(2, 2, Color.White);
                        using (Bitmap union = ImageComposer.UnionMasks(relightMask, secondMask))
                            Assert(union.GetPixel(2, 2).R == 255 && union.GetPixel(12, 2).R == 255 && union.GetPixel(5, 2).R == 0,
                                "串联工作流没有正确累计各步骤蒙版");
                    }
                }

                using (Bitmap reference = new Bitmap(80, 80))
                {
                    using (Graphics referenceGraphics = Graphics.FromImage(reference))
                    {
                        for (int row = 0; row < 8; row++)
                            for (int column = 0; column < 8; column++)
                                using (Brush cell = new SolidBrush(Color.FromArgb((column * 31 + row * 7) % 256, (row * 29 + column * 5) % 256, (column * 13 + row * 17) % 256)))
                                    referenceGraphics.FillRectangle(cell, column * 10, row * 10, 10, 10);
                    }
                    using (Bitmap safeReference = ImageComposer.PrepareStyleReference(reference, 1280))
                    {
                        Assert(safeReference.Width == 80 && safeReference.Height == 80 &&
                            safeReference.GetPixel(5, 5).ToArgb() == reference.GetPixel(35, 15).ToArgb() &&
                            safeReference.GetPixel(5, 5).ToArgb() != reference.GetPixel(5, 5).ToArgb(),
                            "参考图防残影拼贴没有打散原始人物/构图位置");
                    }
                }

                Assert(ApiUrl.Combine("https://api.openai.com/", "/v1/images/edits") == "https://api.openai.com/v1/images/edits", "URL 拼接失败");
                Assert(ApiUrl.Combine("https://api.intenext.ai/v1", "/v1/images/edits") == "https://api.intenext.ai/v1/images/edits", "中转 URL 重复路径未去除");
                Assert(UiScale.NormalizePercent(125) == 125 && UiScale.NormalizePercent(113) == 0 &&
                    UiScale.PercentValues[UiScale.IndexOfPercent(175)] == 175,
                    "UI Scale 选项归一化失败");
                Assert(GeminiResolution.Normalize("4k") == "4K" && GeminiResolution.Normalize("unsupported") == "Auto" &&
                    GeminiResolution.Values[GeminiResolution.IndexOf("2K")] == "2K",
                    "Nano Banana 分辨率选项归一化失败");
                Assert(GeminiResolutionProtocol.Normalize("image config") == "ImageConfig" &&
                    GeminiResolutionProtocol.Values[GeminiResolutionProtocol.IndexOf("response format")] == "ResponseFormat",
                    "Nano Banana 分辨率协议归一化失败");
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
                    GeminiModel = "gemini-3.1-flash-image:generateContent",
                    GeminiImageSize = "4K"
                };
                Assert(ImageApiClient.UsesGeminiGenerateContent(relaySettings), "没有识别 Nano Banana generateContent 中转协议");
                Assert(ImageApiClient.GeminiRequestUrl(relaySettings) == "https://api.vectorengine.ai/v1beta/models/gemini-3.1-flash-image:generateContent", "Nano Banana 中转请求地址拼接失败");
                var relayPayload = ImageApiClient.BuildGeminiPayload(relaySettings, "prompt", "source64", "mask64");
                var testJson = new System.Web.Script.Serialization.JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                string serializedRelay = testJson.Serialize(relayPayload);
                Assert(serializedRelay.IndexOf("\"contents\"", StringComparison.Ordinal) >= 0 &&
                    serializedRelay.IndexOf("\"inline_data\"", StringComparison.Ordinal) >= 0 &&
                    serializedRelay.IndexOf("\"responseModalities\"", StringComparison.Ordinal) >= 0 &&
                    serializedRelay.IndexOf("\"imageConfig\"", StringComparison.Ordinal) >= 0 &&
                    serializedRelay.IndexOf("\"imageSize\":\"4K\"", StringComparison.Ordinal) >= 0 &&
                    serializedRelay.IndexOf("\"responseFormat\"", StringComparison.Ordinal) < 0 &&
                    serializedRelay.IndexOf("STYLE_REFERENCE_ONLY", StringComparison.Ordinal) < 0 &&
                    serializedRelay.IndexOf("\"input\"", StringComparison.Ordinal) < 0,
                    "VectorEngine 自动兼容请求体格式错误");
                string referencePrompt = ImageApiClient.BuildGeminiPrompt("user prompt remains", true);
                string serializedRelayReference = testJson.Serialize(ImageApiClient.BuildGeminiPayload(relaySettings, referencePrompt, "source64", "mask64", "reference64"));
                Assert(referencePrompt.StartsWith("user prompt remains", StringComparison.Ordinal) &&
                    referencePrompt.IndexOf("ghost remnants", StringComparison.Ordinal) >= 0 &&
                    serializedRelayReference.IndexOf("STYLE_REFERENCE_ONLY", StringComparison.Ordinal) >= 0 &&
                    serializedRelayReference.IndexOf("reference64", StringComparison.Ordinal) >= 0,
                    "Nano Banana 参考图请求没有独立标记或防残影约束");
                string openAiWithoutReference = ImageApiClient.BuildOpenAiPrompt("unchanged prompt", false);
                string openAiWithReference = ImageApiClient.BuildOpenAiPrompt("unchanged prompt", true);
                Assert(openAiWithoutReference.StartsWith("unchanged prompt", StringComparison.Ordinal) &&
                    openAiWithoutReference.IndexOf("style reference", StringComparison.OrdinalIgnoreCase) < 0 &&
                    openAiWithReference.StartsWith("unchanged prompt", StringComparison.Ordinal) &&
                    openAiWithReference.IndexOf("ghost remnants", StringComparison.Ordinal) >= 0,
                    "GPT Image 2 参考图分支影响了原有提示词保护逻辑");
                using (MultipartFormDataContent openAiReferenceForm = ImageApiClient.BuildOpenAiForm(
                    new AppSettings(), "prompt", new byte[] { 1 }, new byte[] { 2 }, new byte[] { 3 }))
                {
                    Assert(CountMultipartParts(openAiReferenceForm, "image[]", null) == 2 &&
                        CountMultipartParts(openAiReferenceForm, "image[]", "source.png") == 1 &&
                        CountMultipartParts(openAiReferenceForm, "image[]", "style-reference.png") == 1 &&
                        CountMultipartParts(openAiReferenceForm, "mask", "mask.png") == 1,
                        "GPT Image 2 reference image was not uploaded as the second image[] part");
                }
                using (MultipartFormDataContent openAiPlainForm = ImageApiClient.BuildOpenAiForm(
                    new AppSettings(), "prompt", new byte[] { 1 }, new byte[] { 2 }, null))
                {
                    Assert(CountMultipartParts(openAiPlainForm, "image[]", null) == 1 &&
                        CountMultipartParts(openAiPlainForm, "image[]", "style-reference.png") == 0,
                        "GPT Image 2 request changed when no reference image was selected");
                }
                relaySettings.GeminiResolutionProtocol = "ResponseFormat";
                string serializedResponseFormat = testJson.Serialize(ImageApiClient.BuildGeminiPayload(relaySettings, "prompt", "source64", "mask64"));
                Assert(serializedResponseFormat.IndexOf("\"responseFormat\"", StringComparison.Ordinal) >= 0 &&
                    serializedResponseFormat.IndexOf("\"imageSize\":\"4K\"", StringComparison.Ordinal) >= 0 &&
                    serializedResponseFormat.IndexOf("\"imageConfig\"", StringComparison.Ordinal) < 0,
                    "Nano Banana Response Format 请求体格式错误");
                AppSettings genericRelaySettings = new AppSettings
                {
                    Provider = "Nano Banana",
                    GeminiBaseUrl = "https://relay.example",
                    GeminiEndpoint = "/v1beta/models/{model}:generateContent",
                    GeminiModel = "gemini-image",
                    GeminiImageSize = "4K",
                    GeminiResolutionProtocol = "Auto"
                };
                string serializedGenericRelay = testJson.Serialize(ImageApiClient.BuildGeminiPayload(genericRelaySettings, "prompt", "source64", "mask64"));
                Assert(serializedGenericRelay.IndexOf("\"imageConfig\"", StringComparison.Ordinal) >= 0 &&
                    serializedGenericRelay.IndexOf("\"imageSize\":\"4K\"", StringComparison.Ordinal) >= 0 &&
                    serializedGenericRelay.IndexOf("\"responseFormat\"", StringComparison.Ordinal) < 0,
                    "generateContent 中转的自动分辨率协议没有使用标准 Image Config");
                AppSettings interactionsSettings = new AppSettings
                {
                    GeminiBaseUrl = "https://generativelanguage.googleapis.com",
                    GeminiEndpoint = "/v1beta/interactions",
                    GeminiModel = "gemini-3.1-flash-image",
                    GeminiImageSize = "2K"
                };
                Assert(!ImageApiClient.UsesGeminiGenerateContent(interactionsSettings), "官方 Interactions 协议被错误识别为 generateContent");
                string serializedInteractions = testJson.Serialize(ImageApiClient.BuildGeminiPayload(interactionsSettings, "prompt", "source64", "mask64"));
                Assert(serializedInteractions.IndexOf("\"input\"", StringComparison.Ordinal) >= 0 &&
                    serializedInteractions.IndexOf("\"model\"", StringComparison.Ordinal) >= 0 &&
                    serializedInteractions.IndexOf("\"image_size\":\"2K\"", StringComparison.Ordinal) >= 0,
                    "Nano Banana Interactions 请求体格式错误");
                object sampleGeminiResponse = testJson.DeserializeObject("{\"candidates\":[{\"content\":{\"parts\":[{\"inlineData\":{\"mimeType\":\"image/png\",\"data\":\"aW1hZ2U=\"}}]}}]}");
                Assert(ImageApiClient.FindImageData(sampleGeminiResponse) == "aW1hZ2U=", "generateContent 图片响应解析失败");
                object thinkingGeminiResponse = testJson.DeserializeObject("{\"candidates\":[{\"content\":{\"parts\":[{\"inlineData\":{\"mimeType\":\"image/png\",\"data\":\"dGhvdWdodA==\"}},{\"inlineData\":{\"mimeType\":\"image/png\",\"data\":\"ZmluYWw=\"}}]}}]}");
                Assert(ImageApiClient.FindImageData(thinkingGeminiResponse) == "ZmluYWw=", "没有优先使用 Gemini 最后一张最终图片");
                object openAiStyleImageResponse = testJson.DeserializeObject("{\"data\":[{\"b64_json\":\"iVBORw0KGgoAAAANSUhEUg==\"}]}");
                Assert(ImageApiClient.FindImageData(openAiStyleImageResponse) == "iVBORw0KGgoAAAANSUhEUg==",
                    "Nano Banana 中转的 OpenAI 风格 b64_json 图片没有被识别");
                object urlStyleImageResponse = testJson.DeserializeObject("{\"images\":[{\"image_url\":{\"url\":\"https://relay.example/result.png\"}}]}");
                Assert(ImageApiClient.FindImageUrl(urlStyleImageResponse) == "https://relay.example/result.png",
                    "Nano Banana 中转返回的图片 URL 没有被识别");
                relaySettings.Provider = "Nano Banana";
                relaySettings.GeminiResolutionProtocol = "Auto";
                Assert(!string.IsNullOrEmpty(ImageApiClient.GeminiResolutionWarning(relaySettings, 842, 1264)) &&
                    string.IsNullOrEmpty(ImageApiClient.GeminiResolutionWarning(relaySettings, 3392, 5056)),
                    "Nano Banana 分辨率不足检测失败");
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
                using (Bitmap smartSource = new Bitmap(240, 160, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                using (ImageCanvas smartCanvas = new ImageCanvas())
                {
                    using (Graphics graphics = Graphics.FromImage(smartSource))
                    using (Brush smartFill = new SolidBrush(Color.FromArgb(218, 228, 236)))
                    using (Pen smartTail = new Pen(Color.FromArgb(218, 228, 236), 1f))
                    {
                        graphics.Clear(Color.FromArgb(24, 32, 42));
                        graphics.FillPolygon(smartFill, new[]
                        {
                            new Point(42, 78), new Point(82, 34), new Point(158, 42),
                            new Point(210, 86), new Point(154, 130), new Point(76, 118)
                        });
                        graphics.DrawLine(smartTail, 205, 86, 238, 86);
                    }
                    smartCanvas.Size = new Size(640, 480);
                    smartCanvas.CreateControl();
                    smartCanvas.SetImage(smartSource);
                    smartCanvas.Tool = SelectionTool.SmartSelect;
                    smartCanvas.BrushSize = 2;
                    Rectangle smartCursorBounds = smartCanvas.DebugToolCursorBounds(new Point(100, 80));
                    Assert(smartCursorBounds.Width >= 35 && smartCursorBounds.Height >= 35,
                        "智能点选准星擦除范围仍依赖笔刷大小，可能产生绿色拖尾");
                    smartCanvas.DebugSmartSelect(new PointF(120, 80), true);
                    Assert(smartCanvas.DebugMaskPixel(120, 80).R > 240 &&
                        smartCanvas.DebugMaskPixel(10, 10).R < 15,
                        "智能点选没有把目标与背景分离");
                    Assert(smartCanvas.DebugMaskPixel(235, 86).R < 15,
                        "智能点选没有清理与主体相连的细长拖尾");
                    smartCanvas.DebugSmartSelect(new PointF(120, 80), false);
                    Assert(smartCanvas.DebugMaskPixel(120, 80).R < 15, "智能点选右键排除失败");
                    smartCanvas.UndoMask();
                    Assert(smartCanvas.DebugMaskPixel(120, 80).R > 240, "智能点选没有接入撤销历史");
                }
                using (Bitmap edgeSource = new Bitmap(240, 160, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                using (ImageCanvas lassoCanvas = new ImageCanvas())
                {
                    using (Graphics graphics = Graphics.FromImage(edgeSource))
                    {
                        graphics.Clear(Color.FromArgb(22, 28, 34));
                        graphics.FillRectangle(Brushes.White, 120, 0, 120, 160);
                    }
                    lassoCanvas.Size = new Size(640, 480);
                    lassoCanvas.CreateControl();
                    lassoCanvas.SetImage(edgeSource);
                    List<PointF> snapped = lassoCanvas.DebugSnapSegment(new PointF(112, 20), new PointF(112, 140));
                    Assert(snapped.Count > 4 && snapped[snapped.Count / 2].X >= 116f && snapped[snapped.Count / 2].X <= 122f,
                        "磁性套索没有吸附到高对比度边缘");
                    List<PointF> polygon = new List<PointF>
                    {
                        new PointF(30, 30), new PointF(90, 30), new PointF(90, 100), new PointF(30, 100)
                    };
                    lassoCanvas.DebugApplyMagneticLasso(polygon, true);
                    Assert(lassoCanvas.DebugMaskPixel(60, 60).R > 240 && lassoCanvas.DebugMaskPixel(10, 10).R < 15,
                        "磁性套索没有创建封闭蒙版");
                    lassoCanvas.DebugApplyMagneticLasso(polygon, false);
                    Assert(lassoCanvas.DebugMaskPixel(60, 60).R < 15, "磁性套索排除模式失败");
                    lassoCanvas.UndoMask();
                    Assert(lassoCanvas.DebugMaskPixel(60, 60).R > 240, "磁性套索没有接入撤销历史");
                    List<PointF> freehandPolygon = new List<PointF>
                    {
                        new PointF(140, 35), new PointF(210, 60), new PointF(175, 125), new PointF(132, 82)
                    };
                    lassoCanvas.DebugApplyFreehandLasso(freehandPolygon, true);
                    Assert(lassoCanvas.DebugMaskPixel(170, 75).R > 240 && lassoCanvas.DebugMaskPixel(225, 140).R < 15,
                        "自由套索没有创建非吸附封闭蒙版");
                    lassoCanvas.UndoMask();
                    Assert(lassoCanvas.DebugMaskPixel(170, 75).R < 15, "自由套索没有接入撤销历史");
                }
                Environment.SetEnvironmentVariable("PIXELPATCH_DATA_DIR", root);
                SettingsStore store = new SettingsStore();
                AppSettings saved = new AppSettings();
                saved.Provider = "Nano Banana";
                saved.GeminiBaseUrl = "https://api.vectorengine.ai/v1";
                saved.GeminiEndpoint = "/v1beta/models";
                saved.GeminiModel = "test-image-model:generateContent";
                saved.GeminiImageSize = "4k";
                saved.GeminiResolutionProtocol = "image config";
                saved.UiScalePercent = 125;
                store.Save(saved);
                store.SetApiKey(ApiProvider.NanoBanana, "self-test-secret");
                AppSettings loaded = store.Load();
                Assert(loaded.Provider == "Nano Banana" && loaded.GeminiBaseUrl == "https://api.vectorengine.ai" &&
                    loaded.GeminiEndpoint == "/v1beta/models/{model}:generateContent" && loaded.GeminiModel == "test-image-model",
                    "Nano Banana 中转旧配置迁移失败");
                Assert(loaded.UiScalePercent == 125, "UI Scale 设置持久化失败");
                Assert(loaded.GeminiImageSize == "4K", "Nano Banana 分辨率设置持久化失败");
                Assert(loaded.GeminiResolutionProtocol == "ImageConfig", "Nano Banana 分辨率协议持久化失败");
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

        private static int CountMultipartParts(MultipartFormDataContent form, string name, string fileName)
        {
            int count = 0;
            foreach (HttpContent part in form)
            {
                if (part.Headers.ContentDisposition == null) continue;
                string partName = (part.Headers.ContentDisposition.Name ?? string.Empty).Trim('"');
                string partFileName = (part.Headers.ContentDisposition.FileName ?? string.Empty).Trim('"');
                if (string.Equals(partName, name, StringComparison.Ordinal) &&
                    (fileName == null || string.Equals(partFileName, fileName, StringComparison.Ordinal))) count++;
            }
            return count;
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
