using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace PixelPatchStudio
{
    internal static class UiScale
    {
        internal static readonly int[] PercentValues = { 0, 75, 90, 100, 110, 125, 150, 175, 200 };
        internal static readonly string[] DisplayNames =
        {
            "自动（推荐，按屏幕 / DPI）",
            "75%（紧凑）",
            "90%",
            "100%（标准）",
            "110%（2K 推荐）",
            "125%",
            "150%",
            "175%",
            "200%（4K 大字号）"
        };

        public static int NormalizePercent(int value)
        {
            foreach (int allowed in PercentValues) if (value == allowed) return value;
            return 0;
        }

        public static int IndexOfPercent(int value)
        {
            value = NormalizePercent(value);
            for (int i = 0; i < PercentValues.Length; i++) if (PercentValues[i] == value) return i;
            return 0;
        }

        public static float ResolveFactor(int configuredPercent)
        {
            int overridePercent;
            string overridden = Environment.GetEnvironmentVariable("PHOTOSENSE_UI_SCALE");
            if (int.TryParse(overridden, out overridePercent) && overridePercent >= 50 && overridePercent <= 250)
                return overridePercent / 100f;

            configuredPercent = NormalizePercent(configuredPercent);
            if (configuredPercent > 0) return configuredPercent / 100f;
            Rectangle working = Screen.PrimaryScreen == null ? new Rectangle(0, 0, 1920, 1080) : Screen.PrimaryScreen.WorkingArea;
            float dpi = SystemDpi();
            float effectiveWidth = working.Width * 96f / dpi;
            float effectiveHeight = working.Height * 96f / dpi;
            if (effectiveWidth >= 3200f && effectiveHeight >= 1750f) return 1.35f;
            if (effectiveWidth >= 2350f && effectiveHeight >= 1250f) return 1.10f;
            if (effectiveWidth < 1700f || effectiveHeight < 900f) return 0.90f;
            return 1f;
        }

        public static int EffectivePercent(int configuredPercent)
        {
            return (int)Math.Round(ResolveFactor(configuredPercent) * 100f);
        }

        public static void Apply(Form form, int configuredPercent, bool keepDialogOnScreen)
        {
            if (form == null) return;
            float factor = ResolveFactor(configuredPercent);
            if (keepDialogOnScreen)
            {
                Rectangle working = Screen.PrimaryScreen == null ? new Rectangle(0, 0, 1920, 1080) : Screen.PrimaryScreen.WorkingArea;
                float fitWidth = working.Width * 0.92f / Math.Max(1, form.Width);
                float fitHeight = working.Height * 0.90f / Math.Max(1, form.Height);
                factor = Math.Min(factor, Math.Min(fitWidth, fitHeight));
                factor = Math.Max(0.75f, factor);
            }
            if (Math.Abs(factor - 1f) < 0.005f) return;

            List<FontScaleEntry> fonts = new List<FontScaleEntry>();
            CaptureExplicitFonts(form, fonts);
            form.SuspendLayout();
            try
            {
                form.Scale(new SizeF(factor, factor));
                foreach (FontScaleEntry entry in fonts)
                {
                    Font source = entry.Font;
                    entry.Control.Font = new Font(source.FontFamily, Math.Max(5f, source.Size * factor), source.Style, source.Unit, source.GdiCharSet, source.GdiVerticalFont);
                }
                Rectangle working = Screen.PrimaryScreen == null ? Rectangle.Empty : Screen.PrimaryScreen.WorkingArea;
                if (!working.IsEmpty)
                {
                    form.MinimumSize = new Size(
                        Math.Min(form.MinimumSize.Width, Math.Max(640, working.Width - 24)),
                        Math.Min(form.MinimumSize.Height, Math.Max(480, working.Height - 24)));
                }
            }
            finally
            {
                form.ResumeLayout(true);
                form.PerformLayout();
            }
        }

        private static void CaptureExplicitFonts(Control control, List<FontScaleEntry> result)
        {
            bool explicitFont = control.Parent == null || !object.ReferenceEquals(control.Font, control.Parent.Font);
            if (explicitFont) result.Add(new FontScaleEntry { Control = control, Font = control.Font });
            foreach (Control child in control.Controls) CaptureExplicitFonts(child, result);
        }

        private static float SystemDpi()
        {
            try
            {
                using (Graphics graphics = Graphics.FromHwnd(IntPtr.Zero)) return Math.Max(96f, graphics.DpiX);
            }
            catch { return 96f; }
        }

        private sealed class FontScaleEntry
        {
            public Control Control;
            public Font Font;
        }
    }
}
