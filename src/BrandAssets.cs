using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;

namespace PixelPatchStudio
{
    internal static class BrandAssets
    {
        private const string LogoResourceName = "PixelPatchStudio.PixelPatchLogo.jpg";
        private static readonly Bitmap logo = LoadLogo();

        public static Bitmap Logo { get { return logo; } }

        public static void DrawLogo(Graphics graphics, RectangleF bounds)
        {
            if (graphics == null || bounds.Width <= 0f || bounds.Height <= 0f) return;
            GraphicsState state = graphics.Save();
            try
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                using (GraphicsPath clip = new GraphicsPath())
                {
                    clip.AddEllipse(bounds);
                    graphics.SetClip(clip, CombineMode.Intersect);
                    graphics.DrawImage(logo, bounds, new RectangleF(0f, 0f, logo.Width, logo.Height), GraphicsUnit.Pixel);
                }
            }
            finally
            {
                graphics.Restore(state);
            }
        }

        private static Bitmap LoadLogo()
        {
            Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(LogoResourceName);
            if (stream != null)
            {
                using (stream)
                using (Image image = Image.FromStream(stream))
                {
                    return new Bitmap(image);
                }
            }

            Bitmap fallback = new Bitmap(128, 128, PixelFormat.Format32bppArgb);
            using (Graphics graphics = Graphics.FromImage(fallback))
            using (Brush fill = new SolidBrush(Color.FromArgb(229, 92, 161)))
            using (Pen ring = new Pen(Color.White, 8f))
            using (Font font = new Font("Arial", 56f, FontStyle.Bold, GraphicsUnit.Pixel))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.Clear(Color.Transparent);
                graphics.FillEllipse(fill, 5, 5, 118, 118);
                graphics.DrawEllipse(ring, 6, 6, 116, 116);
                TextRendererHelper.DrawCentered(graphics, "!", font, Color.White, new Rectangle(0, 0, 128, 128));
            }
            return fallback;
        }

        private static class TextRendererHelper
        {
            public static void DrawCentered(Graphics graphics, string text, Font font, Color color, Rectangle bounds)
            {
                using (Brush brush = new SolidBrush(color))
                using (StringFormat format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    graphics.DrawString(text, font, brush, bounds, format);
                }
            }
        }
    }
}
