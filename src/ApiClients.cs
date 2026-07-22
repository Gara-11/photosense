using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace PixelPatchStudio
{
    internal static class ApiUrl
    {
        public static string Combine(string baseUrl, string endpoint)
        {
            if (string.IsNullOrWhiteSpace(baseUrl)) throw new ArgumentException("接口 URL 不能为空。");
            if (string.IsNullOrWhiteSpace(endpoint)) return baseUrl.TrimEnd('/');
            Uri baseUri;
            if (!Uri.TryCreate(baseUrl.Trim(), UriKind.Absolute, out baseUri)) return baseUrl.TrimEnd('/') + "/" + endpoint.TrimStart('/');

            string[] baseSegments = baseUri.AbsolutePath.Trim('/').Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            string[] endpointSegments = endpoint.Trim('/').Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            int overlap = 0;
            int maximum = Math.Min(baseSegments.Length, endpointSegments.Length);
            for (int length = maximum; length > 0; length--)
            {
                bool matches = true;
                for (int i = 0; i < length; i++)
                {
                    if (!string.Equals(baseSegments[baseSegments.Length - length + i], endpointSegments[i], StringComparison.OrdinalIgnoreCase)) { matches = false; break; }
                }
                if (matches) { overlap = length; break; }
            }

            StringBuilder path = new StringBuilder(baseUri.GetLeftPart(UriPartial.Authority).TrimEnd('/'));
            foreach (string segment in baseSegments) path.Append('/').Append(segment);
            for (int i = overlap; i < endpointSegments.Length; i++) path.Append('/').Append(endpointSegments[i]);
            return path.ToString();
        }
    }

    internal sealed class ImageApiClient : IDisposable
    {
        private readonly HttpClient http;
        private readonly JavaScriptSerializer json;

        public ImageApiClient(int timeoutSeconds)
        {
            HttpClientHandler handler = new HttpClientHandler();
            handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            handler.UseProxy = true;
            http = new HttpClient(handler);
            http.Timeout = TimeSpan.FromSeconds(Math.Max(30, timeoutSeconds));
            json = new JavaScriptSerializer();
            json.MaxJsonLength = int.MaxValue;
            json.RecursionLimit = 200;
        }

        public Task<Bitmap> GenerateAsync(Bitmap source, Bitmap selectionMask, string prompt, AppSettings settings, string apiKey, CancellationToken cancellationToken)
        {
            return GenerateAsync(source, selectionMask, null, prompt, settings, apiKey, cancellationToken);
        }

        public Task<Bitmap> GenerateAsync(Bitmap source, Bitmap selectionMask, Bitmap referenceImage, string prompt, AppSettings settings, string apiKey, CancellationToken cancellationToken)
        {
            if (settings.SelectedProvider == ApiProvider.GptImage2)
                return GenerateOpenAiAsync(source, selectionMask, referenceImage, prompt, settings, apiKey, cancellationToken);
            return GenerateGeminiAsync(source, selectionMask, referenceImage, prompt, settings, apiKey, cancellationToken);
        }

        public void Dispose()
        {
            http.Dispose();
        }

        private async Task<Bitmap> GenerateOpenAiAsync(Bitmap source, Bitmap selectionMask, Bitmap referenceImage, string prompt, AppSettings settings, string apiKey, CancellationToken cancellationToken)
        {
            byte[] imageBytes;
            byte[] maskBytes;
            byte[] referenceBytes = null;
            using (Bitmap requestSource = ResizeForApi(source, 2048, InterpolationMode.HighQualityBicubic))
            using (Bitmap requestSelection = ResizeForApi(selectionMask, 2048, InterpolationMode.NearestNeighbor))
            using (Bitmap apiMask = ImageComposer.PrepareOpenAiMask(requestSelection))
            {
                imageBytes = Png(requestSource);
                maskBytes = Png(apiMask);
            }
            if (referenceImage != null)
            {
                using (Bitmap requestReference = ResizeForApi(referenceImage, 1280, InterpolationMode.HighQualityBicubic))
                    referenceBytes = Png(requestReference);
            }

            using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, ApiUrl.Combine(settings.BaseUrl, settings.Endpoint)))
            using (MultipartFormDataContent form = BuildOpenAiForm(settings, prompt, imageBytes, maskBytes, referenceBytes))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                request.Headers.ExpectContinue = false;
                request.Headers.ConnectionClose = true;
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                request.Content = form;

                try
                {
                    using (HttpResponseMessage response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
                    {
                        string body = await ReadTextContentAsync(response.Content, cancellationToken).ConfigureAwait(false);
                        if (!response.IsSuccessStatusCode) throw ApiException(response, body, request.RequestUri == null ? null : request.RequestUri.ToString());
                        object parsed = ParseJson(body);
                        string base64 = FindNamedString(parsed, "b64_json");
                        if (!string.IsNullOrEmpty(base64)) return BitmapFromBytes(Convert.FromBase64String(base64));
                        string url = FindNamedString(parsed, "url");
                        if (!string.IsNullOrEmpty(url)) return BitmapFromBytes(await DownloadBytesAsync(url, cancellationToken).ConfigureAwait(false));
                        throw new InvalidDataException("接口响应中没有找到生成图片（b64_json/url）。");
                    }
                }
                catch (OperationCanceledException ex)
                {
                    if (cancellationToken.IsCancellationRequested) throw;
                    throw new TimeoutException("图像接口超过设置的等待时间。请求地址：" + ApiUrl.Combine(settings.BaseUrl, settings.Endpoint), ex);
                }
                catch (HttpRequestException ex) { throw NetworkException(settings, imageBytes.Length + maskBytes.Length + (referenceBytes == null ? 0 : referenceBytes.Length), ex); }
                catch (IOException ex) { throw NetworkException(settings, imageBytes.Length + maskBytes.Length + (referenceBytes == null ? 0 : referenceBytes.Length), ex); }
            }
        }

        internal static MultipartFormDataContent BuildOpenAiForm(AppSettings settings, string prompt, byte[] imageBytes, byte[] maskBytes, byte[] referenceBytes)
        {
            MultipartFormDataContent form = new MultipartFormDataContent();
            form.Add(new StringContent(settings.Model), "model");
            form.Add(new StringContent(BuildOpenAiPrompt(prompt, referenceBytes != null)), "prompt");
            form.Add(new StringContent("high"), "quality");
            form.Add(new StringContent("auto"), "size");
            form.Add(new StringContent("png"), "output_format");

            ByteArrayContent image = new ByteArrayContent(imageBytes);
            image.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            form.Add(image, "image[]", "source.png");
            if (referenceBytes != null)
            {
                ByteArrayContent reference = new ByteArrayContent(referenceBytes);
                reference.Headers.ContentType = new MediaTypeHeaderValue("image/png");
                form.Add(reference, "image[]", "style-reference.png");
            }
            ByteArrayContent mask = new ByteArrayContent(maskBytes);
            mask.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            form.Add(mask, "mask", "mask.png");
            return form;
        }

        private async Task<Bitmap> GenerateGeminiAsync(Bitmap source, Bitmap selectionMask, Bitmap referenceImage, string prompt, AppSettings settings, string apiKey, CancellationToken cancellationToken)
        {
            using (Bitmap requestSource = ResizeForApi(source, 2048, InterpolationMode.HighQualityBicubic))
            using (Bitmap requestMask = ResizeForApi(selectionMask, 2048, InterpolationMode.NearestNeighbor))
            using (Bitmap requestReference = referenceImage == null ? null : ResizeForApi(referenceImage, 1280, InterpolationMode.HighQualityBicubic))
            {
                string requestUrl = GeminiRequestUrl(settings);
                string guardedPrompt = BuildGeminiPrompt(prompt, requestReference != null);
                string sourceData = Convert.ToBase64String(Png(requestSource));
                string maskData = Convert.ToBase64String(Png(requestMask));
                string referenceData = requestReference == null ? null : Convert.ToBase64String(Png(requestReference));
                Dictionary<string, object> payload = BuildGeminiPayload(settings, guardedPrompt, sourceData, maskData, referenceData);

                using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, requestUrl))
                {
                    request.Headers.TryAddWithoutValidation("x-goog-api-key", apiKey);
                    request.Headers.ExpectContinue = false;
                    request.Headers.ConnectionClose = true;
                    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    string serialized = json.Serialize(payload);
                    request.Content = new StringContent(serialized, Encoding.UTF8, "application/json");
                    try
                    {
                        using (HttpResponseMessage response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
                        {
                            string body = await ReadTextContentAsync(response.Content, cancellationToken).ConfigureAwait(false);
                            if (!response.IsSuccessStatusCode) throw ApiException(response, body, requestUrl);
                            object parsed = ParseJson(body);
                            string base64 = FindImageData(parsed);
                            if (string.IsNullOrEmpty(base64)) throw new InvalidDataException("Nano Banana 响应中没有找到图片数据。");
                            return BitmapFromBytes(Convert.FromBase64String(base64));
                        }
                    }
                    catch (OperationCanceledException ex)
                    {
                        if (cancellationToken.IsCancellationRequested) throw;
                        throw new TimeoutException("图像接口超过设置的等待时间。请求地址：" + requestUrl, ex);
                    }
                    catch (HttpRequestException ex) { throw NetworkException(settings, Encoding.UTF8.GetByteCount(serialized), ex, requestUrl); }
                    catch (IOException ex) { throw NetworkException(settings, Encoding.UTF8.GetByteCount(serialized), ex, requestUrl); }
                }
            }
        }

        internal static bool UsesGeminiGenerateContent(AppSettings settings)
        {
            string endpoint = settings == null ? "" : settings.GeminiEndpoint ?? "";
            return endpoint.IndexOf("generateContent", StringComparison.OrdinalIgnoreCase) >= 0 ||
                endpoint.IndexOf("{model}", StringComparison.OrdinalIgnoreCase) >= 0 ||
                endpoint.TrimEnd('/').EndsWith("/models", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool UsesGeminiImageConfig(AppSettings settings)
        {
            string protocol = GeminiResolutionProtocol.Normalize(settings == null ? null : settings.GeminiResolutionProtocol);
            if (protocol == "ImageConfig") return true;
            if (protocol == "ResponseFormat") return false;
            Uri baseUri;
            return settings != null && Uri.TryCreate(settings.GeminiBaseUrl, UriKind.Absolute, out baseUri) &&
                baseUri.Host.EndsWith("vectorengine.ai", StringComparison.OrdinalIgnoreCase);
        }

        internal static string GeminiResolutionWarning(AppSettings settings, int width, int height)
        {
            if (settings == null || settings.SelectedProvider != ApiProvider.NanoBanana ||
                !GeminiResolution.IsClearlyBelowRequested(settings.GeminiImageSize, width, height)) return null;
            string requested = GeminiResolution.Normalize(settings.GeminiImageSize);
            string protocol = UsesGeminiGenerateContent(settings)
                ? (UsesGeminiImageConfig(settings) ? "Image Config" : "Response Format")
                : "Interactions response_format";
            return "已请求 Nano Banana " + requested + "，但中转返回的原始图片只有 " + width + " × " + height +
                "，明显低于原生 " + requested + "。\n\n当前分辨率协议：" + protocol +
                "\n这通常表示中转没有识别或透传分辨率参数，本次请求仍可能已经计费。" +
                "\n如果启用了 Real-ESRGAN，后续只会进行本地放大，并不代表 Nano Banana 原生 " + requested + "。";
        }

        internal static Dictionary<string, object> BuildGeminiPayload(AppSettings settings, string guardedPrompt, string sourceData, string maskData)
        {
            return BuildGeminiPayload(settings, guardedPrompt, sourceData, maskData, null);
        }

        internal static Dictionary<string, object> BuildGeminiPayload(AppSettings settings, string guardedPrompt, string sourceData, string maskData, string referenceData)
        {
            Dictionary<string, object> payload = new Dictionary<string, object>();
            string imageSize = GeminiResolution.Normalize(settings == null ? null : settings.GeminiImageSize);
            if (UsesGeminiGenerateContent(settings))
            {
                List<object> parts = new List<object>
                {
                    new Dictionary<string, object> { { "text", guardedPrompt } },
                    new Dictionary<string, object> { { "inline_data", new Dictionary<string, object> { { "mime_type", "image/png" }, { "data", sourceData } } } },
                    new Dictionary<string, object> { { "inline_data", new Dictionary<string, object> { { "mime_type", "image/png" }, { "data", maskData } } } }
                };
                if (!string.IsNullOrEmpty(referenceData))
                {
                    parts.Add(new Dictionary<string, object> { { "text", "STYLE_REFERENCE_ONLY: use only its palette, lighting, texture and materials. Never copy any person, face, body, silhouette, subject, pose, clothing, object layout or composition from it." } });
                    parts.Add(new Dictionary<string, object> { { "inline_data", new Dictionary<string, object> { { "mime_type", "image/png" }, { "data", referenceData } } } });
                }
                payload["contents"] = new object[]
                {
                    new Dictionary<string, object>
                    {
                        { "role", "user" },
                        { "parts", parts.ToArray() }
                    }
                };
                Dictionary<string, object> generationConfig = new Dictionary<string, object>
                {
                    { "responseModalities", new object[] { "TEXT", "IMAGE" } }
                };
                if (imageSize != "Auto")
                {
                    if (UsesGeminiImageConfig(settings))
                    {
                        generationConfig["imageConfig"] = new Dictionary<string, object> { { "imageSize", imageSize } };
                    }
                    else
                    {
                        generationConfig["responseFormat"] = new Dictionary<string, object>
                        {
                            { "image", new Dictionary<string, object> { { "imageSize", imageSize } } }
                        };
                    }
                }
                payload["generationConfig"] = generationConfig;
            }
            else
            {
                payload["model"] = NormalizeGeminiModel(settings == null ? null : settings.Model);
                List<object> input = new List<object>
                {
                    new Dictionary<string, object> { { "type", "text" }, { "text", guardedPrompt } },
                    new Dictionary<string, object> { { "type", "image" }, { "mime_type", "image/png" }, { "data", sourceData } },
                    new Dictionary<string, object> { { "type", "image" }, { "mime_type", "image/png" }, { "data", maskData } }
                };
                if (!string.IsNullOrEmpty(referenceData))
                {
                    input.Add(new Dictionary<string, object> { { "type", "text" }, { "text", "STYLE_REFERENCE_ONLY: use only its palette, lighting, texture and materials. Never copy any person, face, body, silhouette, subject, pose, clothing, object layout or composition from it." } });
                    input.Add(new Dictionary<string, object> { { "type", "image" }, { "mime_type", "image/png" }, { "data", referenceData } });
                }
                payload["input"] = input.ToArray();
                Dictionary<string, object> responseFormat = new Dictionary<string, object> { { "type", "image" } };
                if (imageSize != "Auto") responseFormat["image_size"] = imageSize;
                payload["response_format"] = responseFormat;
            }
            return payload;
        }

        internal static string GeminiRequestUrl(AppSettings settings)
        {
            if (settings == null) throw new ArgumentNullException("settings");
            string endpoint = (settings.GeminiEndpoint ?? "").Trim();
            string model = NormalizeGeminiModel(settings.GeminiModel);
            if (UsesGeminiGenerateContent(settings))
            {
                int placeholder = endpoint.IndexOf("{model}", StringComparison.OrdinalIgnoreCase);
                if (placeholder >= 0)
                    endpoint = endpoint.Substring(0, placeholder) + Uri.EscapeDataString(model) + endpoint.Substring(placeholder + "{model}".Length);
                else if (endpoint.TrimEnd('/').EndsWith("/models", StringComparison.OrdinalIgnoreCase))
                    endpoint = endpoint.TrimEnd('/') + "/" + Uri.EscapeDataString(model) + ":generateContent";
            }

            Uri absoluteEndpoint;
            if (Uri.TryCreate(endpoint, UriKind.Absolute, out absoluteEndpoint)) return absoluteEndpoint.ToString();
            string baseUrl = (settings.GeminiBaseUrl ?? "").Trim();
            Uri baseUri;
            if (endpoint.TrimStart('/').StartsWith("v1beta/", StringComparison.OrdinalIgnoreCase) &&
                Uri.TryCreate(baseUrl, UriKind.Absolute, out baseUri) &&
                string.Equals(baseUri.AbsolutePath.Trim('/'), "v1", StringComparison.OrdinalIgnoreCase))
                baseUrl = baseUri.GetLeftPart(UriPartial.Authority);
            return ApiUrl.Combine(baseUrl, endpoint);
        }

        private static string NormalizeGeminiModel(string model)
        {
            string value = (model ?? "").Trim().Trim('/');
            if (value.StartsWith("models/", StringComparison.OrdinalIgnoreCase)) value = value.Substring("models/".Length);
            if (value.EndsWith(":generateContent", StringComparison.OrdinalIgnoreCase))
                value = value.Substring(0, value.Length - ":generateContent".Length).TrimEnd('/');
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Nano Banana 模型不能为空。");
            return value;
        }

        private object ParseJson(string body)
        {
            try { return json.DeserializeObject(body); }
            catch (Exception ex) { throw new InvalidDataException("接口返回了无法解析的数据：" + Short(body), ex); }
        }

        private static Exception ApiException(HttpResponseMessage response, string body, string requestUrl)
        {
            string message = FindErrorMessage(body);
            string address = string.IsNullOrWhiteSpace(requestUrl) ? "" : "\n请求地址：" + requestUrl;
            return new InvalidOperationException("接口请求失败（" + (int)response.StatusCode + " " + response.ReasonPhrase + "）：" + message + address);
        }

        internal static string BuildOpenAiPrompt(string prompt, bool hasReference)
        {
            string guarded = (prompt ?? "").Trim() + "\n\nEdit only the transparent area of the supplied mask. Preserve every unmasked pixel, the subject's identity, position, pose, anatomy, camera, crop, perspective and composition exactly. Return a complete image aligned pixel-for-pixel with the input.";
            if (!hasReference) return guarded;
            return guarded + " The first image is the editable original and the second image is a style reference only. Use the reference only for palette, lighting, texture and materials. Never copy any person, face, body, silhouette, subject, pose, clothing, object placement or composition from the reference. Do not add duplicate people, reflections, translucent figures, double exposures or ghost remnants.";
        }

        internal static string BuildGeminiPrompt(string prompt, bool hasReference)
        {
            string imageDescription = hasReference
                ? "There are three images after this instruction: the original image, a binary mask, then a style reference image."
                : "There are two images after this instruction: the original image, then a binary mask.";
            string guarded = (prompt ?? "").Trim() + "\n\n" + imageDescription + " In the mask, white is the ONLY region allowed to change and black must remain untouched. Keep the same canvas size. Preserve the person's identity, exact position, pose, anatomy, clothing outside the white mask, camera, crop, perspective and composition. Return one complete image precisely aligned with the original.";
            if (!hasReference) return guarded;
            return guarded + " Use the third image only for palette, lighting, texture and materials. Ignore every person, face, body, silhouette, subject, pose, clothing, object placement and composition in it. Do not add duplicate people, reflections, translucent figures, double exposures or ghost remnants.";
        }

        private static byte[] Png(Bitmap bitmap)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                bitmap.Save(stream, ImageFormat.Png);
                return stream.ToArray();
            }
        }

        internal static Bitmap ResizeForApi(Bitmap image, int maxEdge, InterpolationMode interpolationMode)
        {
            double scale = Math.Min(1d, maxEdge / (double)Math.Max(image.Width, image.Height));
            int width = Math.Max(1, (int)Math.Round(image.Width * scale));
            int height = Math.Max(1, (int)Math.Round(image.Height * scale));
            Bitmap result = new Bitmap(width, height, PixelFormat.Format24bppRgb);
            try
            {
                if (image.HorizontalResolution > 0 && image.VerticalResolution > 0) result.SetResolution(image.HorizontalResolution, image.VerticalResolution);
            }
            catch { }
            using (Graphics graphics = Graphics.FromImage(result))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = interpolationMode;
                graphics.PixelOffsetMode = interpolationMode == InterpolationMode.NearestNeighbor ? PixelOffsetMode.Half : PixelOffsetMode.HighQuality;
                graphics.DrawImage(image, new Rectangle(0, 0, width, height), 0, 0, image.Width, image.Height, GraphicsUnit.Pixel);
            }
            return result;
        }

        private static async Task<string> ReadTextContentAsync(HttpContent content, CancellationToken cancellationToken)
        {
            const long maximumBytes = 512L * 1024 * 1024;
            long declared = content.Headers.ContentLength.GetValueOrDefault(-1L);
            if (declared > maximumBytes) throw new InvalidDataException("接口响应超过 512 MB，已停止读取以避免内存耗尽。");
            using (Stream input = await content.ReadAsStreamAsync().ConfigureAwait(false))
            using (MemoryStream output = declared > 0 && declared < int.MaxValue ? new MemoryStream((int)declared) : new MemoryStream())
            {
                byte[] buffer = new byte[131072];
                while (true)
                {
                    int read = await input.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
                    if (read <= 0) break;
                    if (output.Length + read > maximumBytes) throw new InvalidDataException("接口响应超过 512 MB，已停止读取以避免内存耗尽。");
                    await output.WriteAsync(buffer, 0, read, cancellationToken).ConfigureAwait(false);
                }
                return Encoding.UTF8.GetString(output.ToArray());
            }
        }

        private async Task<byte[]> DownloadBytesAsync(string url, CancellationToken cancellationToken)
        {
            Exception last = null;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url))
                    {
                        request.Headers.ConnectionClose = true;
                        using (HttpResponseMessage response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
                        {
                            response.EnsureSuccessStatusCode();
                            using (Stream input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                            using (MemoryStream output = new MemoryStream())
                            {
                                byte[] buffer = new byte[131072];
                                while (true)
                                {
                                    int read = await input.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
                                    if (read <= 0) break;
                                    await output.WriteAsync(buffer, 0, read, cancellationToken).ConfigureAwait(false);
                                }
                                return output.ToArray();
                            }
                        }
                    }
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    last = ex;
                }
                if (attempt < 2) await Task.Delay(750 * (attempt + 1), cancellationToken).ConfigureAwait(false);
            }
            throw new HttpRequestException("下载生成图片时连接连续中断。", last);
        }

        private static Exception NetworkException(AppSettings settings, long requestBytes, Exception innerException, string requestUrl = null)
        {
            string address = string.IsNullOrWhiteSpace(requestUrl) ? ApiUrl.Combine(settings.BaseUrl, settings.Endpoint) : requestUrl;
            string message = "图像接口传输中断。已将超大 Photoshop 文档缩放到最长边 2048 像素再上传，本次请求约 " +
                (requestBytes / 1048576d).ToString("0.0") + " MB。\n\n请求地址：" + address +
                "\n请检查中转服务是否支持长时间图像请求。软件不会自动重发生成请求，以免重复扣费；可确认后手动重试。\n\n底层信息：" + innerException.Message;
            return new HttpRequestException(message, innerException);
        }

        private static Bitmap BitmapFromBytes(byte[] bytes)
        {
            using (MemoryStream stream = new MemoryStream(bytes))
            using (Image image = Image.FromStream(stream, true, true))
            {
                return new Bitmap(image);
            }
        }

        private static string FindNamedString(object node, string name)
        {
            IDictionary<string, object> dictionary = node as IDictionary<string, object>;
            if (dictionary != null)
            {
                object value;
                if (dictionary.TryGetValue(name, out value) && value is string) return (string)value;
                foreach (object child in dictionary.Values)
                {
                    string found = FindNamedString(child, name);
                    if (!string.IsNullOrEmpty(found)) return found;
                }
            }
            IEnumerable list = node as IEnumerable;
            if (list != null && !(node is string))
            {
                foreach (object child in list)
                {
                    string found = FindNamedString(child, name);
                    if (!string.IsNullOrEmpty(found)) return found;
                }
            }
            return null;
        }

        internal static string FindImageData(object node)
        {
            string last = null;
            IDictionary<string, object> dictionary = node as IDictionary<string, object>;
            if (dictionary != null)
            {
                object mime;
                object data;
                bool image = (dictionary.TryGetValue("mime_type", out mime) || dictionary.TryGetValue("mimeType", out mime)) && Convert.ToString(mime).StartsWith("image/", StringComparison.OrdinalIgnoreCase);
                if (image && dictionary.TryGetValue("data", out data) && data is string) last = (string)data;
                object output;
                if (dictionary.TryGetValue("output_image", out output))
                {
                    IDictionary<string, object> imageObject = output as IDictionary<string, object>;
                    if (imageObject != null && imageObject.TryGetValue("data", out data) && data is string) last = (string)data;
                }
                foreach (object child in dictionary.Values)
                {
                    string found = FindImageData(child);
                    if (!string.IsNullOrEmpty(found)) last = found;
                }
                return last;
            }
            IEnumerable list = node as IEnumerable;
            if (list != null && !(node is string))
            {
                foreach (object child in list)
                {
                    string found = FindImageData(child);
                    if (!string.IsNullOrEmpty(found)) last = found;
                }
            }
            return last;
        }

        private static string FindErrorMessage(string body)
        {
            try
            {
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                serializer.MaxJsonLength = int.MaxValue;
                string message = FindNamedString(serializer.DeserializeObject(body), "message");
                if (!string.IsNullOrWhiteSpace(message)) return message;
            }
            catch { }
            return Short(body);
        }

        private static string Short(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "空响应";
            text = text.Trim();
            return text.Length <= 500 ? text : text.Substring(0, 500) + "…";
        }
    }
}
