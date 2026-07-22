using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace PixelPatchStudio
{
    internal sealed class SettingsStore
    {
        private readonly JavaScriptSerializer json = new JavaScriptSerializer();

        public string DataDirectory
        {
            get
            {
                string overridden = Environment.GetEnvironmentVariable("PIXELPATCH_DATA_DIR");
                if (!string.IsNullOrWhiteSpace(overridden)) return overridden;
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PixelPatch Studio");
            }
        }

        private string SettingsPath { get { return Path.Combine(DataDirectory, "settings.json"); } }
        private string CredentialsPath { get { return Path.Combine(DataDirectory, "credentials.bin"); } }

        public AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    AppSettings loaded = json.Deserialize<AppSettings>(File.ReadAllText(SettingsPath, Encoding.UTF8));
                    if (loaded != null) return Normalize(loaded);
                }
            }
            catch { }
            return Normalize(new AppSettings());
        }

        public void Save(AppSettings settings)
        {
            Directory.CreateDirectory(DataDirectory);
            string temp = SettingsPath + ".tmp";
            File.WriteAllText(temp, json.Serialize(Normalize(settings)), new UTF8Encoding(false));
            if (File.Exists(SettingsPath)) File.Replace(temp, SettingsPath, null);
            else File.Move(temp, SettingsPath);
        }

        public string GetApiKey(ApiProvider provider)
        {
            Dictionary<string, string> values = LoadCredentials();
            string value;
            return values.TryGetValue(provider.ToString(), out value) ? value : "";
        }

        public void SetApiKey(ApiProvider provider, string apiKey)
        {
            Dictionary<string, string> values = LoadCredentials();
            if (string.IsNullOrWhiteSpace(apiKey)) values.Remove(provider.ToString());
            else values[provider.ToString()] = apiKey.Trim();
            SaveCredentials(values);
        }

        private Dictionary<string, string> LoadCredentials()
        {
            try
            {
                if (!File.Exists(CredentialsPath)) return new Dictionary<string, string>();
                byte[] encrypted = File.ReadAllBytes(CredentialsPath);
                byte[] plain = ProtectedData.Unprotect(encrypted, Entropy(), DataProtectionScope.CurrentUser);
                Dictionary<string, string> result = json.Deserialize<Dictionary<string, string>>(Encoding.UTF8.GetString(plain));
                return result ?? new Dictionary<string, string>();
            }
            catch
            {
                return new Dictionary<string, string>();
            }
        }

        private void SaveCredentials(Dictionary<string, string> values)
        {
            Directory.CreateDirectory(DataDirectory);
            byte[] plain = Encoding.UTF8.GetBytes(json.Serialize(values));
            byte[] encrypted = ProtectedData.Protect(plain, Entropy(), DataProtectionScope.CurrentUser);
            File.WriteAllBytes(CredentialsPath, encrypted);
        }

        private static byte[] Entropy()
        {
            return Encoding.UTF8.GetBytes("PixelPatch Studio credentials v1");
        }

        private static AppSettings Normalize(AppSettings value)
        {
            if (string.IsNullOrWhiteSpace(value.Provider)) value.Provider = "GPT Image 2";
            if (string.IsNullOrWhiteSpace(value.OpenAiBaseUrl)) value.OpenAiBaseUrl = "https://api.openai.com";
            if (string.IsNullOrWhiteSpace(value.OpenAiEndpoint)) value.OpenAiEndpoint = "/v1/images/edits";
            if (string.IsNullOrWhiteSpace(value.OpenAiModel)) value.OpenAiModel = "gpt-image-2";
            if (string.IsNullOrWhiteSpace(value.GeminiBaseUrl)) value.GeminiBaseUrl = "https://api.vectorengine.ai";
            if (string.IsNullOrWhiteSpace(value.GeminiEndpoint)) value.GeminiEndpoint = "/v1beta/models/{model}:generateContent";
            if (string.IsNullOrWhiteSpace(value.GeminiModel)) value.GeminiModel = "gemini-3.1-flash-image";
            value.GeminiBaseUrl = value.GeminiBaseUrl.Trim().TrimEnd('/');
            value.GeminiEndpoint = value.GeminiEndpoint.Trim();
            value.GeminiModel = value.GeminiModel.Trim();
            value.GeminiImageSize = GeminiResolution.Normalize(value.GeminiImageSize);
            value.GeminiResolutionProtocol = GeminiResolutionProtocol.Normalize(value.GeminiResolutionProtocol);
            if (value.GeminiModel.EndsWith(":generateContent", StringComparison.OrdinalIgnoreCase))
                value.GeminiModel = value.GeminiModel.Substring(0, value.GeminiModel.Length - ":generateContent".Length).TrimEnd('/');
            Uri geminiBase;
            if (Uri.TryCreate(value.GeminiBaseUrl, UriKind.Absolute, out geminiBase) &&
                geminiBase.Host.EndsWith("vectorengine.ai", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(geminiBase.AbsolutePath.Trim('/'), "v1", StringComparison.OrdinalIgnoreCase))
                value.GeminiBaseUrl = geminiBase.GetLeftPart(UriPartial.Authority);
            string geminiEndpoint = value.GeminiEndpoint.TrimEnd('/');
            if (string.Equals(geminiEndpoint, "/v1beta/models", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(geminiEndpoint, "v1beta/models", StringComparison.OrdinalIgnoreCase) ||
                (value.GeminiBaseUrl.IndexOf("vectorengine.ai", StringComparison.OrdinalIgnoreCase) >= 0 &&
                 geminiEndpoint.EndsWith("/v1beta/interactions", StringComparison.OrdinalIgnoreCase)))
                value.GeminiEndpoint = "/v1beta/models/{model}:generateContent";
            if (string.IsNullOrWhiteSpace(value.RealEsrganDownloadUrl)) value.RealEsrganDownloadUrl = RealEsrganService.OfficialDownloadUrl;
            if (string.IsNullOrWhiteSpace(value.RealEsrganModel)) value.RealEsrganModel = "realesrgan-x4plus";
            if (value.RealEsrganScale < 2 || value.RealEsrganScale > 4) value.RealEsrganScale = 4;
            if (value.ApiTimeoutSeconds < 30) value.ApiTimeoutSeconds = 600;
            if (value.BrushSize < 1) value.BrushSize = 80;
            value.UiScalePercent = UiScale.NormalizePercent(value.UiScalePercent);
            return value;
        }
    }
}
