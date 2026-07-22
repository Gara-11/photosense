using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.IO;

namespace PixelPatchStudio
{
    internal static class ImageComposer
    {
        public static Bitmap Composite(Bitmap source, Bitmap generated, Bitmap mask)
        {
            using (Bitmap src = ToArgb(source, source.Width, source.Height, InterpolationMode.HighQualityBicubic))
            using (Bitmap gen = ToArgb(generated, source.Width, source.Height, InterpolationMode.HighQualityBicubic))
            using (Bitmap msk = ToArgb(mask, source.Width, source.Height, InterpolationMode.NearestNeighbor))
            {
                Bitmap output = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
                Process(src, gen, msk, output, false);
                return output;
            }
        }

        public static Bitmap CreatePatch(Bitmap generated, Bitmap mask)
        {
            using (Bitmap gen = ToArgb(generated, mask.Width, mask.Height, InterpolationMode.HighQualityBicubic))
            using (Bitmap msk = ToArgb(mask, mask.Width, mask.Height, InterpolationMode.NearestNeighbor))
            using (Bitmap blank = new Bitmap(mask.Width, mask.Height, PixelFormat.Format32bppArgb))
            {
                Bitmap output = new Bitmap(mask.Width, mask.Height, PixelFormat.Format32bppArgb);
                Process(blank, gen, msk, output, true);
                return output;
            }
        }

        public static Bitmap PrepareOpenAiMask(Bitmap selectionMask)
        {
            using (Bitmap mask = ToArgb(selectionMask, selectionMask.Width, selectionMask.Height, InterpolationMode.NearestNeighbor))
            {
                Bitmap output = new Bitmap(mask.Width, mask.Height, PixelFormat.Format32bppArgb);
                Rectangle rect = new Rectangle(0, 0, mask.Width, mask.Height);
                BitmapData inputData = mask.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                BitmapData outputData = output.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                try
                {
                    int inputStride = Math.Abs(inputData.Stride);
                    int outputStride = Math.Abs(outputData.Stride);
                    byte[] input = new byte[mask.Width * 4];
                    byte[] result = new byte[mask.Width * 4];
                    for (int y = 0; y < mask.Height; y++)
                    {
                        IntPtr inputRow = IntPtr.Add(inputData.Scan0, y * inputStride);
                        IntPtr outputRow = IntPtr.Add(outputData.Scan0, y * outputStride);
                        Marshal.Copy(inputRow, input, 0, input.Length);
                        for (int x = 0; x < mask.Width; x++)
                        {
                            int i = x * 4;
                            byte selected = Math.Max(input[i], Math.Max(input[i + 1], input[i + 2]));
                            result[i] = 255;
                            result[i + 1] = 255;
                            result[i + 2] = 255;
                            result[i + 3] = (byte)(255 - selected); // OpenAI edits transparent mask pixels.
                        }
                        Marshal.Copy(result, 0, outputRow, result.Length);
                    }
                }
                finally
                {
                    mask.UnlockBits(inputData);
                    output.UnlockBits(outputData);
                }
                return output;
            }
        }

        public static Bitmap PrepareStyleReference(Bitmap reference, int maxEdge)
        {
            if (reference == null) throw new ArgumentNullException("reference");
            if (maxEdge < 256) throw new ArgumentOutOfRangeException("maxEdge");
            double scale = Math.Min(1d, maxEdge / (double)Math.Max(reference.Width, reference.Height));
            int width = Math.Max(1, (int)Math.Round(reference.Width * scale));
            int height = Math.Max(1, (int)Math.Round(reference.Height * scale));
            using (Bitmap normalized = ToArgb(reference, width, height, InterpolationMode.HighQualityBicubic))
            {
                Bitmap output = new Bitmap(width, height, PixelFormat.Format24bppRgb);
                int grid = Math.Max(2, Math.Min(8, Math.Min(width, height)));
                int cells = grid * grid;
                using (Graphics graphics = Graphics.FromImage(output))
                {
                    graphics.CompositingMode = CompositingMode.SourceCopy;
                    graphics.CompositingQuality = CompositingQuality.HighQuality;
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    for (int destinationIndex = 0; destinationIndex < cells; destinationIndex++)
                    {
                        int sourceIndex = (destinationIndex * 17 + 11) % cells;
                        int destinationColumn = destinationIndex % grid;
                        int destinationRow = destinationIndex / grid;
                        int sourceColumn = sourceIndex % grid;
                        int sourceRow = sourceIndex / grid;
                        Rectangle destination = GridCell(width, height, grid, destinationColumn, destinationRow);
                        Rectangle source = GridCell(width, height, grid, sourceColumn, sourceRow);
                        graphics.DrawImage(normalized, destination, source, GraphicsUnit.Pixel);
                    }
                }
                return output;
            }
        }

        private static Rectangle GridCell(int width, int height, int grid, int column, int row)
        {
            int left = column * width / grid;
            int top = row * height / grid;
            int right = (column + 1) * width / grid;
            int bottom = (row + 1) * height / grid;
            return new Rectangle(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top));
        }

        public static List<PatchTile> SavePatchTiles(Bitmap patch, string directory, int tileSize, float resolution)
        {
            if (tileSize < 256) throw new ArgumentOutOfRangeException("tileSize");
            Directory.CreateDirectory(directory);
            List<PatchTile> tiles = new List<PatchTile>();
            for (int y = 0; y < patch.Height; y += tileSize)
            {
                int height = Math.Min(tileSize, patch.Height - y);
                for (int x = 0; x < patch.Width; x += tileSize)
                {
                    int width = Math.Min(tileSize, patch.Width - x);
                    Rectangle rectangle = new Rectangle(x, y, width, height);
                    if (!HasVisiblePixel(patch, rectangle)) continue;
                    string path = Path.Combine(directory, "tile-" + y + "-" + x + ".png");
                    using (Bitmap tile = patch.Clone(rectangle, PixelFormat.Format32bppArgb))
                    {
                        tile.SetResolution(resolution, resolution);
                        tile.Save(path, ImageFormat.Png);
                    }
                    tiles.Add(new PatchTile { ImagePath = path, X = x, Y = y, Width = width, Height = height });
                }
            }
            return tiles;
        }

        public static bool HasSelection(Bitmap mask)
        {
            Rectangle rect = new Rectangle(0, 0, mask.Width, mask.Height);
            BitmapData data = mask.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                int stride = Math.Abs(data.Stride);
                byte[] bytes = new byte[mask.Width * 4];
                for (int y = 0; y < mask.Height; y++)
                {
                    Marshal.Copy(IntPtr.Add(data.Scan0, y * stride), bytes, 0, bytes.Length);
                    for (int x = 0; x < mask.Width; x++)
                    {
                        int i = x * 4;
                        if (bytes[i] != 0 || bytes[i + 1] != 0 || bytes[i + 2] != 0) return true;
                    }
                }
                return false;
            }
            finally
            {
                mask.UnlockBits(data);
            }
        }

        public static double SelectionCoverage(Bitmap mask)
        {
            const int sampleEdge = 512;
            double scale = Math.Min(1.0, sampleEdge / (double)Math.Max(mask.Width, mask.Height));
            int sampleWidth = Math.Max(1, (int)Math.Round(mask.Width * scale));
            int sampleHeight = Math.Max(1, (int)Math.Round(mask.Height * scale));
            using (Bitmap normalized = ToArgb(mask, sampleWidth, sampleHeight, InterpolationMode.NearestNeighbor))
            {
                Rectangle rect = new Rectangle(0, 0, normalized.Width, normalized.Height);
                BitmapData data = normalized.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                try
                {
                    int stride = Math.Abs(data.Stride);
                    byte[] bytes = new byte[normalized.Width * 4];
                    long selected = 0;
                    for (int y = 0; y < normalized.Height; y++)
                    {
                        Marshal.Copy(IntPtr.Add(data.Scan0, y * stride), bytes, 0, bytes.Length);
                        for (int x = 0; x < normalized.Width; x++)
                        {
                            int i = x * 4;
                            selected += Math.Max(bytes[i], Math.Max(bytes[i + 1], bytes[i + 2]));
                        }
                    }
                    return selected / (255.0 * normalized.Width * normalized.Height);
                }
                finally
                {
                    normalized.UnlockBits(data);
                }
            }
        }

        private static void Process(Bitmap source, Bitmap generated, Bitmap mask, Bitmap output, bool transparentOutside)
        {
            Rectangle rect = new Rectangle(0, 0, output.Width, output.Height);
            BitmapData sourceData = source.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            BitmapData generatedData = generated.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            BitmapData maskData = mask.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            BitmapData outputData = output.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                int sourceStride = Math.Abs(sourceData.Stride);
                int generatedStride = Math.Abs(generatedData.Stride);
                int maskStride = Math.Abs(maskData.Stride);
                int outputStride = Math.Abs(outputData.Stride);
                int rowLength = output.Width * 4;
                byte[] src = new byte[rowLength];
                byte[] gen = new byte[rowLength];
                byte[] msk = new byte[rowLength];
                byte[] dst = new byte[rowLength];
                for (int y = 0; y < output.Height; y++)
                {
                    Marshal.Copy(IntPtr.Add(sourceData.Scan0, y * sourceStride), src, 0, rowLength);
                    Marshal.Copy(IntPtr.Add(generatedData.Scan0, y * generatedStride), gen, 0, rowLength);
                    Marshal.Copy(IntPtr.Add(maskData.Scan0, y * maskStride), msk, 0, rowLength);
                    for (int x = 0; x < output.Width; x++)
                    {
                        int i = x * 4;
                        int amount = Math.Max(msk[i], Math.Max(msk[i + 1], msk[i + 2]));
                        if (transparentOutside)
                        {
                            dst[i] = gen[i];
                            dst[i + 1] = gen[i + 1];
                            dst[i + 2] = gen[i + 2];
                            dst[i + 3] = (byte)((gen[i + 3] * amount + 127) / 255);
                        }
                        else if (amount == 0)
                        {
                            dst[i] = src[i];
                            dst[i + 1] = src[i + 1];
                            dst[i + 2] = src[i + 2];
                            dst[i + 3] = src[i + 3];
                        }
                        else if (amount == 255)
                        {
                            dst[i] = gen[i];
                            dst[i + 1] = gen[i + 1];
                            dst[i + 2] = gen[i + 2];
                            dst[i + 3] = gen[i + 3];
                        }
                        else
                        {
                            int inverse = 255 - amount;
                            dst[i] = (byte)((gen[i] * amount + src[i] * inverse + 127) / 255);
                            dst[i + 1] = (byte)((gen[i + 1] * amount + src[i + 1] * inverse + 127) / 255);
                            dst[i + 2] = (byte)((gen[i + 2] * amount + src[i + 2] * inverse + 127) / 255);
                            dst[i + 3] = (byte)((gen[i + 3] * amount + src[i + 3] * inverse + 127) / 255);
                        }
                    }
                    Marshal.Copy(dst, 0, IntPtr.Add(outputData.Scan0, y * outputStride), rowLength);
                }
            }
            finally
            {
                source.UnlockBits(sourceData);
                generated.UnlockBits(generatedData);
                mask.UnlockBits(maskData);
                output.UnlockBits(outputData);
            }
        }

        private static bool HasVisiblePixel(Bitmap bitmap, Rectangle rectangle)
        {
            BitmapData data = bitmap.LockBits(rectangle, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                int stride = Math.Abs(data.Stride);
                byte[] row = new byte[rectangle.Width * 4];
                for (int y = 0; y < rectangle.Height; y++)
                {
                    Marshal.Copy(IntPtr.Add(data.Scan0, y * stride), row, 0, row.Length);
                    for (int x = 3; x < row.Length; x += 4) if (row[x] != 0) return true;
                }
                return false;
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
        }

        private static Bitmap ToArgb(Bitmap image, int width, int height, InterpolationMode mode)
        {
            Bitmap result = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using (Graphics graphics = Graphics.FromImage(result))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = mode;
                graphics.PixelOffsetMode = mode == InterpolationMode.NearestNeighbor ? PixelOffsetMode.Half : PixelOffsetMode.HighQuality;
                graphics.DrawImage(image, new Rectangle(0, 0, width, height), 0, 0, image.Width, image.Height, GraphicsUnit.Pixel);
            }
            return result;
        }
    }
}
