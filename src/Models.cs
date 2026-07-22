using System;
using System.Web.Script.Serialization;

namespace PixelPatchStudio
{
    internal enum ApiProvider
    {
        GptImage2,
        NanoBanana
    }

    internal enum PaintMode
    {
        Add,
        Erase
    }

    internal static class GeminiResolution
    {
        internal static readonly string[] Values = { "Auto", "1K", "2K", "4K" };
        internal static readonly object[] DisplayNames = { "自动（接口默认）", "1K", "2K", "4K" };

        internal static string Normalize(string value)
        {
            string candidate = (value ?? "").Trim();
            if (string.Equals(candidate, "1K", StringComparison.OrdinalIgnoreCase)) return "1K";
            if (string.Equals(candidate, "2K", StringComparison.OrdinalIgnoreCase)) return "2K";
            if (string.Equals(candidate, "4K", StringComparison.OrdinalIgnoreCase)) return "4K";
            return "Auto";
        }

        internal static int IndexOf(string value)
        {
            string normalized = Normalize(value);
            for (int i = 0; i < Values.Length; i++) if (Values[i] == normalized) return i;
            return 0;
        }

        internal static bool IsClearlyBelowRequested(string value, int width, int height)
        {
            string normalized = Normalize(value);
            long pixels = Math.Max(0, width) * (long)Math.Max(0, height);
            if (normalized == "4K") return pixels < 10000000L;
            if (normalized == "2K") return pixels < 2500000L;
            return false;
        }
    }

    internal static class GeminiResolutionProtocol
    {
        internal static readonly string[] Values = { "Auto", "ResponseFormat", "ImageConfig" };
        internal static readonly object[] DisplayNames = { "自动兼容", "Response Format", "Image Config" };

        internal static string Normalize(string value)
        {
            string candidate = (value ?? "").Trim().Replace(" ", "");
            if (string.Equals(candidate, "ResponseFormat", StringComparison.OrdinalIgnoreCase)) return "ResponseFormat";
            if (string.Equals(candidate, "ImageConfig", StringComparison.OrdinalIgnoreCase)) return "ImageConfig";
            return "Auto";
        }

        internal static int IndexOf(string value)
        {
            string normalized = Normalize(value);
            for (int i = 0; i < Values.Length; i++) if (Values[i] == normalized) return i;
            return 0;
        }
    }

    [Serializable]
    internal sealed class AppSettings
    {
        public string Provider = "GPT Image 2";
        public string OpenAiBaseUrl = "https://api.openai.com";
        public string OpenAiEndpoint = "/v1/images/edits";
        public string OpenAiModel = "gpt-image-2";
        public string GeminiBaseUrl = "https://api.vectorengine.ai";
        public string GeminiEndpoint = "/v1beta/models/{model}:generateContent";
        public string GeminiModel = "gemini-3.1-flash-image";
        public string GeminiImageSize = "Auto";
        public string GeminiResolutionProtocol = "Auto";
        public string RealEsrganPath = "";
        public string RealEsrganDownloadUrl = RealEsrganService.OfficialDownloadUrl;
        public string RealEsrganModel = "realesrgan-x4plus";
        public int RealEsrganScale = 4;
        public int ApiTimeoutSeconds = 600;
        public int BrushSize = 80;
        public int UiScalePercent = 0;

        [ScriptIgnore]
        public ApiProvider SelectedProvider
        {
            get { return Provider == "Nano Banana" ? ApiProvider.NanoBanana : ApiProvider.GptImage2; }
        }

        [ScriptIgnore]
        public string BaseUrl
        {
            get { return SelectedProvider == ApiProvider.GptImage2 ? OpenAiBaseUrl : GeminiBaseUrl; }
        }

        [ScriptIgnore]
        public string Endpoint
        {
            get { return SelectedProvider == ApiProvider.GptImage2 ? OpenAiEndpoint : GeminiEndpoint; }
        }

        [ScriptIgnore]
        public string Model
        {
            get { return SelectedProvider == ApiProvider.GptImage2 ? OpenAiModel : GeminiModel; }
        }
    }

    internal sealed class PhotoshopImage
    {
        public string ImagePath;
        public string DocumentName;
        public int Width;
        public int Height;
        public int DocumentId;
        public double Resolution = 96d;
    }

    internal sealed class PatchTile
    {
        public string ImagePath;
        public int X;
        public int Y;
        public int Width;
        public int Height;
    }
}
