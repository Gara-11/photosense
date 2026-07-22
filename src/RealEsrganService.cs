using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PixelPatchStudio
{
    internal sealed class InstallProgress
    {
        public readonly string Message;
        public readonly int Percent;

        public InstallProgress(string message, int percent)
        {
            Message = message;
            Percent = percent;
        }
    }

    internal sealed class RealEsrganService
    {
        public const string ReleaseVersion = "0.2.5.0";
        public const string OfficialDownloadUrl = "https://github.com/xinntao/Real-ESRGAN/releases/download/v0.2.5.0/realesrgan-ncnn-vulkan-20220424-windows.zip";

        public string ResolveExecutable(AppSettings settings)
        {
            if (!string.IsNullOrWhiteSpace(settings.RealEsrganPath) && IsValidInstallation(settings.RealEsrganPath)) return settings.RealEsrganPath;

            string persisted = Path.Combine(GetDefaultDataDirectory(), "RealESRGAN", ReleaseVersion, "realesrgan-ncnn-vulkan.exe");
            if (IsValidInstallation(persisted)) return persisted;

            string bundled = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "realesrgan-ncnn-vulkan.exe");
            return IsValidInstallation(bundled) ? bundled : null;
        }

        public async Task<string> InstallAsync(string dataDirectory, string downloadUrl, IProgress<InstallProgress> progress, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(dataDirectory)) throw new ArgumentException("Real-ESRGAN 安装目录不能为空。", "dataDirectory");
            if (string.IsNullOrWhiteSpace(downloadUrl)) downloadUrl = OfficialDownloadUrl;

            string installDirectory = Path.Combine(dataDirectory, "RealESRGAN", ReleaseVersion);
            string existing = Path.Combine(installDirectory, "realesrgan-ncnn-vulkan.exe");
            if (IsValidInstallation(existing))
            {
                Report(progress, "Real-ESRGAN 已安装并通过校验", 100);
                return existing;
            }

            string operationDirectory = Path.Combine(dataDirectory, "Temp", "RealESRGAN-Install-" + Guid.NewGuid().ToString("N"));
            string archivePath = Path.Combine(operationDirectory, "realesrgan-windows.zip");
            Directory.CreateDirectory(operationDirectory);

            try
            {
                Report(progress, "正在连接 Real-ESRGAN 官方下载地址…", 0);
                await DownloadAsync(downloadUrl, archivePath, progress, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                Report(progress, "正在安全解压并校验程序与模型…", 92);

                string installed = await Task.Run(delegate
                {
                    return InstallFromArchive(archivePath, installDirectory);
                }, cancellationToken).ConfigureAwait(false);

                Report(progress, "Real-ESRGAN 自动部署完成", 100);
                return installed;
            }
            finally
            {
                TryDeleteDirectory(operationDirectory);
            }
        }

        internal string InstallFromArchive(string archivePath, string installDirectory)
        {
            if (!File.Exists(archivePath)) throw new FileNotFoundException("Real-ESRGAN 安装包不存在。", archivePath);
            string extractionDirectory = Path.Combine(Path.GetDirectoryName(archivePath), "Extract-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(extractionDirectory);
            try
            {
                ExtractZipSafely(archivePath, extractionDirectory);
                string sourceExecutable = FindFile(extractionDirectory, "realesrgan-ncnn-vulkan.exe");
                if (sourceExecutable == null) throw new InvalidDataException("下载包中没有 realesrgan-ncnn-vulkan.exe。请检查下载 URL 是否指向官方 Windows ZIP。 ");
                ValidateInstallation(sourceExecutable);

                string sourceDirectory = Path.GetDirectoryName(sourceExecutable);
                Directory.CreateDirectory(installDirectory);
                CopyDirectory(sourceDirectory, installDirectory);

                string installedExecutable = Path.Combine(installDirectory, "realesrgan-ncnn-vulkan.exe");
                ValidateInstallation(installedExecutable);
                return installedExecutable;
            }
            finally
            {
                TryDeleteDirectory(extractionDirectory);
            }
        }

        public async Task<Bitmap> UpscaleAsync(Bitmap input, AppSettings settings, string workingDirectory, CancellationToken cancellationToken)
        {
            string executable = ResolveExecutable(settings);
            if (executable == null) throw new FileNotFoundException("尚未部署 Real-ESRGAN。请点击软件中的“自动部署 Real-ESRGAN”。");
            Directory.CreateDirectory(workingDirectory);
            string inputPath = Path.Combine(workingDirectory, "esrgan-in-" + Guid.NewGuid().ToString("N") + ".png");
            string outputPath = Path.Combine(workingDirectory, "esrgan-out-" + Guid.NewGuid().ToString("N") + ".png");
            input.Save(inputPath, ImageFormat.Png);

            ProcessStartInfo start = new ProcessStartInfo();
            start.FileName = executable;
            start.WorkingDirectory = Path.GetDirectoryName(executable);
            start.Arguments = "-i \"" + inputPath + "\" -o \"" + outputPath + "\" -n \"" + settings.RealEsrganModel + "\" -s " + settings.RealEsrganScale + " -f png";
            start.UseShellExecute = false;
            start.CreateNoWindow = true;
            start.RedirectStandardOutput = true;
            start.RedirectStandardError = true;
            start.StandardOutputEncoding = Encoding.UTF8;
            start.StandardErrorEncoding = Encoding.UTF8;

            using (Process process = new Process())
            {
                process.StartInfo = start;
                StringBuilder logs = new StringBuilder();
                process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e) { if (e.Data != null) logs.AppendLine(e.Data); };
                process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e) { if (e.Data != null) logs.AppendLine(e.Data); };
                if (!process.Start()) throw new InvalidOperationException("无法启动 Real-ESRGAN。");
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                using (cancellationToken.Register(delegate { try { if (!process.HasExited) process.Kill(); } catch { } }))
                {
                    await Task.Run(delegate { process.WaitForExit(); }, cancellationToken).ConfigureAwait(false);
                }
                if (process.ExitCode != 0) throw new InvalidOperationException("Real-ESRGAN 运行失败：\n" + logs.ToString());
            }

            if (!File.Exists(outputPath)) throw new IOException("Real-ESRGAN 没有生成输出图片。");
            using (Image image = Image.FromFile(outputPath)) return new Bitmap(image);
        }

        private static async Task DownloadAsync(string url, string destination, IProgress<InstallProgress> progress, CancellationToken cancellationToken)
        {
            Exception lastError = null;
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    using (HttpClientHandler handler = new HttpClientHandler())
                    using (HttpClient client = new HttpClient(handler))
                    {
                        handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                        handler.UseProxy = true;
                        client.Timeout = TimeSpan.FromMinutes(15);
                        client.DefaultRequestHeaders.UserAgent.ParseAdd("PhotoSense/1.0.15");
                        using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url))
                        {
                            request.Headers.ConnectionClose = true;
                            using (HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
                            {
                                response.EnsureSuccessStatusCode();
                                long total = response.Content.Headers.ContentLength.GetValueOrDefault(-1L);
                                using (Stream input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                                using (FileStream output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, 131072, true))
                                {
                                    byte[] buffer = new byte[131072];
                                    long received = 0;
                                    while (true)
                                    {
                                        int count = await input.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
                                        if (count <= 0) break;
                                        await output.WriteAsync(buffer, 0, count, cancellationToken).ConfigureAwait(false);
                                        received += count;
                                        int percent = total > 0 ? Math.Min(90, (int)(received * 90L / total)) : 45;
                                        string size = (received / 1048576d).ToString("0.0") + " MB";
                                        Report(progress, "正在下载 Real-ESRGAN · " + size + (total > 0 ? " / " + (total / 1048576d).ToString("0.0") + " MB" : ""), percent);
                                    }
                                }
                            }
                        }
                    }
                    if (!File.Exists(destination) || new FileInfo(destination).Length < 1024) throw new InvalidDataException("Real-ESRGAN 下载包为空或不完整。");
                    return;
                }
                catch (OperationCanceledException ex)
                {
                    if (cancellationToken.IsCancellationRequested) throw;
                    lastError = new TimeoutException("Real-ESRGAN 下载超时。", ex);
                }
                catch (Exception ex) { lastError = ex; }

                if (attempt < 3)
                {
                    Report(progress, "Real-ESRGAN 下载连接中断，正在重试（" + attempt + "/2）…", 0);
                    await Task.Delay(1000 * attempt, cancellationToken).ConfigureAwait(false);
                }
            }
            throw new HttpRequestException("Real-ESRGAN 下载连续 3 次失败。已启用 TLS 1.2 和系统代理；请检查安全软件/代理，或在设置中更换下载 URL。\n\n底层信息：" + (lastError == null ? "未知网络错误" : lastError.Message), lastError);
        }

        private static void ExtractZipSafely(string archivePath, string destination)
        {
            string root = Path.GetFullPath(destination).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            using (FileStream stream = File.OpenRead(archivePath))
            using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read, false))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    string target = Path.GetFullPath(Path.Combine(destination, entry.FullName));
                    if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("安装包包含不安全的文件路径。");
                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        Directory.CreateDirectory(target);
                        continue;
                    }
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    entry.ExtractToFile(target, true);
                }
            }
        }

        private static void ValidateInstallation(string executable)
        {
            if (!File.Exists(executable)) throw new InvalidDataException("Real-ESRGAN 主程序缺失。");
            using (FileStream stream = File.OpenRead(executable))
            {
                if (stream.Length < 2 || stream.ReadByte() != 'M' || stream.ReadByte() != 'Z') throw new InvalidDataException("Real-ESRGAN 主程序校验失败，不是有效的 Windows 可执行文件。");
            }

            string root = Path.GetDirectoryName(executable);
            if (FindFile(root, "realesrgan-x4plus.param") == null || FindFile(root, "realesrgan-x4plus.bin") == null)
            {
                throw new InvalidDataException("Real-ESRGAN 默认模型文件缺失，安装包可能不完整。");
            }
        }

        private static bool IsValidInstallation(string executable)
        {
            try { ValidateInstallation(executable); return true; }
            catch { return false; }
        }

        private static string FindFile(string root, string name)
        {
            if (!Directory.Exists(root)) return null;
            string[] files = Directory.GetFiles(root, name, SearchOption.AllDirectories);
            return files.Length > 0 ? files[0] : null;
        }

        private static void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            foreach (string file in Directory.GetFiles(source)) File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), true);
            foreach (string directory in Directory.GetDirectories(source)) CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
        }

        private static string GetDefaultDataDirectory()
        {
            string overridden = Environment.GetEnvironmentVariable("PIXELPATCH_DATA_DIR");
            if (!string.IsNullOrWhiteSpace(overridden)) return overridden;
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PixelPatch Studio");
        }

        private static void Report(IProgress<InstallProgress> progress, string message, int percent)
        {
            if (progress != null) progress.Report(new InstallProgress(message, percent));
        }

        private static void TryDeleteDirectory(string directory)
        {
            try { if (Directory.Exists(directory)) Directory.Delete(directory, true); } catch { }
        }
    }
}
