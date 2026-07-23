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

        public static Bitmap RelightComposite(Bitmap source, Bitmap lightingGuide, Bitmap mask, int detailProtectionPercent)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (lightingGuide == null) throw new ArgumentNullException("lightingGuide");
            if (mask == null) throw new ArgumentNullException("mask");
            int protection = Math.Max(70, Math.Min(100, detailProtectionPercent));
            int guideMaxEdge = Math.Max(8, Math.Min(320, Math.Max(source.Width, source.Height) / 12));
            double scale = Math.Min(1d, guideMaxEdge / (double)Math.Max(source.Width, source.Height));
            int guideWidth = Math.Max(1, (int)Math.Round(source.Width * scale));
            int guideHeight = Math.Max(1, (int)Math.Round(source.Height * scale));

            using (Bitmap originalSmall = ToArgb(source, guideWidth, guideHeight, InterpolationMode.HighQualityBicubic))
            using (Bitmap generatedSmall = ToArgb(lightingGuide, guideWidth, guideHeight, InterpolationMode.HighQualityBicubic))
            using (Bitmap originalLight = ToArgb(originalSmall, source.Width, source.Height, InterpolationMode.Bilinear))
            using (Bitmap generatedLight = ToArgb(generatedSmall, source.Width, source.Height, InterpolationMode.Bilinear))
            using (Bitmap src = ToArgb(source, source.Width, source.Height, InterpolationMode.HighQualityBicubic))
            using (Bitmap msk = ToArgb(mask, source.Width, source.Height, InterpolationMode.NearestNeighbor))
            {
                Bitmap output = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
                ProcessRelight(src, originalLight, generatedLight, msk, output, protection);
                return output;
            }
        }

        public static Bitmap UnionMasks(Bitmap first, Bitmap second)
        {
            if (first == null && second == null) throw new ArgumentNullException("first");
            if (first == null) return ToArgb(second, second.Width, second.Height, InterpolationMode.NearestNeighbor);
            if (second == null) return ToArgb(first, first.Width, first.Height, InterpolationMode.NearestNeighbor);
            using (Bitmap a = ToArgb(first, first.Width, first.Height, InterpolationMode.NearestNeighbor))
            using (Bitmap b = ToArgb(second, first.Width, first.Height, InterpolationMode.NearestNeighbor))
            {
                byte[] av = ReadMask(a);
                byte[] bv = ReadMask(b);
                for (int i = 0; i < av.Length; i++) av[i] = Math.Max(av[i], bv[i]);
                return WriteMask(av, first.Width, first.Height, first.HorizontalResolution, first.VerticalResolution);
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

        public static Bitmap ExpandMask(Bitmap mask, int radius)
        {
            return MorphMask(mask, radius, true);
        }

        public static Bitmap ContractMask(Bitmap mask, int radius)
        {
            return MorphMask(mask, radius, false);
        }

        public static Bitmap FeatherMask(Bitmap mask, int radius)
        {
            if (mask == null) throw new ArgumentNullException("mask");
            int normalizedRadius = Math.Max(1, Math.Min(128, radius));
            byte[] grayscale = ReadMask(mask);
            byte[] horizontal = BoxBlurHorizontal(grayscale, mask.Width, mask.Height, normalizedRadius);
            byte[] vertical = BoxBlurVertical(horizontal, mask.Width, mask.Height, normalizedRadius);
            return WriteMask(vertical, mask.Width, mask.Height, mask.HorizontalResolution, mask.VerticalResolution);
        }

        public static Bitmap SmoothMask(Bitmap mask, int radius)
        {
            if (mask == null) throw new ArgumentNullException("mask");
            int normalizedRadius = Math.Max(1, Math.Min(128, radius));
            byte[] grayscale = ReadMask(mask);
            byte[] horizontal = BoxBlurHorizontal(grayscale, mask.Width, mask.Height, normalizedRadius);
            byte[] vertical = BoxBlurVertical(horizontal, mask.Width, mask.Height, normalizedRadius);
            for (int i = 0; i < vertical.Length; i++) vertical[i] = vertical[i] >= 128 ? (byte)255 : (byte)0;
            return WriteMask(vertical, mask.Width, mask.Height, mask.HorizontalResolution, mask.VerticalResolution);
        }

        private static Bitmap MorphMask(Bitmap mask, int radius, bool expand)
        {
            if (mask == null) throw new ArgumentNullException("mask");
            int normalizedRadius = Math.Max(1, Math.Min(128, radius));
            byte[] grayscale = ReadMask(mask);
            byte[] horizontal = MorphHorizontal(grayscale, mask.Width, mask.Height, normalizedRadius, expand);
            byte[] vertical = MorphVertical(horizontal, mask.Width, mask.Height, normalizedRadius, expand);
            return WriteMask(vertical, mask.Width, mask.Height, mask.HorizontalResolution, mask.VerticalResolution);
        }

        private static byte[] MorphHorizontal(byte[] input, int width, int height, int radius, bool maximum)
        {
            byte[] output = new byte[input.Length];
            int[] deque = new int[width];
            for (int y = 0; y < height; y++)
            {
                int row = y * width;
                int head = 0;
                int tail = 0;
                int addedThrough = -1;
                for (int x = 0; x < width; x++)
                {
                    int right = Math.Min(width - 1, x + radius);
                    while (addedThrough < right)
                    {
                        int index = ++addedThrough;
                        byte value = input[row + index];
                        while (tail > head && (maximum ? input[row + deque[tail - 1]] <= value : input[row + deque[tail - 1]] >= value)) tail--;
                        deque[tail++] = index;
                    }
                    int left = x - radius;
                    while (tail > head && deque[head] < left) head++;
                    output[row + x] = !maximum && (left < 0 || x + radius >= width) ? (byte)0 : input[row + deque[head]];
                }
            }
            return output;
        }

        private static byte[] MorphVertical(byte[] input, int width, int height, int radius, bool maximum)
        {
            byte[] output = new byte[input.Length];
            int[] deque = new int[height];
            for (int x = 0; x < width; x++)
            {
                int head = 0;
                int tail = 0;
                int addedThrough = -1;
                for (int y = 0; y < height; y++)
                {
                    int bottom = Math.Min(height - 1, y + radius);
                    while (addedThrough < bottom)
                    {
                        int index = ++addedThrough;
                        byte value = input[index * width + x];
                        while (tail > head && (maximum ? input[deque[tail - 1] * width + x] <= value : input[deque[tail - 1] * width + x] >= value)) tail--;
                        deque[tail++] = index;
                    }
                    int top = y - radius;
                    while (tail > head && deque[head] < top) head++;
                    output[y * width + x] = !maximum && (top < 0 || y + radius >= height) ? (byte)0 : input[deque[head] * width + x];
                }
            }
            return output;
        }

        private static byte[] BoxBlurHorizontal(byte[] input, int width, int height, int radius)
        {
            byte[] output = new byte[input.Length];
            for (int y = 0; y < height; y++)
            {
                int row = y * width;
                long sum = 0;
                int right = Math.Min(width - 1, radius);
                for (int x = 0; x <= right; x++) sum += input[row + x];
                for (int x = 0; x < width; x++)
                {
                    int left = Math.Max(0, x - radius);
                    right = Math.Min(width - 1, x + radius);
                    output[row + x] = (byte)((sum + (right - left + 1) / 2) / (right - left + 1));
                    int remove = x - radius;
                    int add = x + radius + 1;
                    if (remove >= 0) sum -= input[row + remove];
                    if (add < width) sum += input[row + add];
                }
            }
            return output;
        }

        private static byte[] BoxBlurVertical(byte[] input, int width, int height, int radius)
        {
            byte[] output = new byte[input.Length];
            for (int x = 0; x < width; x++)
            {
                long sum = 0;
                int bottom = Math.Min(height - 1, radius);
                for (int y = 0; y <= bottom; y++) sum += input[y * width + x];
                for (int y = 0; y < height; y++)
                {
                    int top = Math.Max(0, y - radius);
                    bottom = Math.Min(height - 1, y + radius);
                    output[y * width + x] = (byte)((sum + (bottom - top + 1) / 2) / (bottom - top + 1));
                    int remove = y - radius;
                    int add = y + radius + 1;
                    if (remove >= 0) sum -= input[remove * width + x];
                    if (add < height) sum += input[add * width + x];
                }
            }
            return output;
        }

        private static byte[] ReadMask(Bitmap mask)
        {
            using (Bitmap normalized = ToArgb(mask, mask.Width, mask.Height, InterpolationMode.NearestNeighbor))
            {
                Rectangle rectangle = new Rectangle(0, 0, normalized.Width, normalized.Height);
                BitmapData data = normalized.LockBits(rectangle, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                try
                {
                    byte[] grayscale = new byte[normalized.Width * normalized.Height];
                    byte[] row = new byte[normalized.Width * 4];
                    int stride = Math.Abs(data.Stride);
                    for (int y = 0; y < normalized.Height; y++)
                    {
                        Marshal.Copy(IntPtr.Add(data.Scan0, y * stride), row, 0, row.Length);
                        int target = y * normalized.Width;
                        for (int x = 0; x < normalized.Width; x++)
                        {
                            int sourceIndex = x * 4;
                            grayscale[target + x] = Math.Max(row[sourceIndex], Math.Max(row[sourceIndex + 1], row[sourceIndex + 2]));
                        }
                    }
                    return grayscale;
                }
                finally
                {
                    normalized.UnlockBits(data);
                }
            }
        }

        private static Bitmap WriteMask(byte[] grayscale, int width, int height, float horizontalResolution, float verticalResolution)
        {
            Bitmap output = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            if (horizontalResolution > 0 && verticalResolution > 0) output.SetResolution(horizontalResolution, verticalResolution);
            Rectangle rectangle = new Rectangle(0, 0, width, height);
            BitmapData data = output.LockBits(rectangle, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                byte[] row = new byte[width * 4];
                int stride = Math.Abs(data.Stride);
                for (int y = 0; y < height; y++)
                {
                    int source = y * width;
                    for (int x = 0; x < width; x++)
                    {
                        byte value = grayscale[source + x];
                        int target = x * 4;
                        row[target] = value;
                        row[target + 1] = value;
                        row[target + 2] = value;
                        row[target + 3] = 255;
                    }
                    Marshal.Copy(row, 0, IntPtr.Add(data.Scan0, y * stride), row.Length);
                }
            }
            finally
            {
                output.UnlockBits(data);
            }
            return output;
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

        private static void ProcessRelight(Bitmap source, Bitmap originalLight, Bitmap generatedLight, Bitmap mask, Bitmap output, int protection)
        {
            Rectangle rectangle = new Rectangle(0, 0, output.Width, output.Height);
            BitmapData sourceData = source.LockBits(rectangle, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            BitmapData originalData = originalLight.LockBits(rectangle, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            BitmapData generatedData = generatedLight.LockBits(rectangle, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            BitmapData maskData = mask.LockBits(rectangle, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            BitmapData outputData = output.LockBits(rectangle, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                int rowLength = output.Width * 4;
                byte[] src = new byte[rowLength];
                byte[] original = new byte[rowLength];
                byte[] generated = new byte[rowLength];
                byte[] msk = new byte[rowLength];
                byte[] dst = new byte[rowLength];
                double strength = Math.Max(0.2d, Math.Min(1d, (110d - protection) / 40d));
                for (int y = 0; y < output.Height; y++)
                {
                    Marshal.Copy(IntPtr.Add(sourceData.Scan0, y * Math.Abs(sourceData.Stride)), src, 0, rowLength);
                    Marshal.Copy(IntPtr.Add(originalData.Scan0, y * Math.Abs(originalData.Stride)), original, 0, rowLength);
                    Marshal.Copy(IntPtr.Add(generatedData.Scan0, y * Math.Abs(generatedData.Stride)), generated, 0, rowLength);
                    Marshal.Copy(IntPtr.Add(maskData.Scan0, y * Math.Abs(maskData.Stride)), msk, 0, rowLength);
                    for (int x = 0; x < output.Width; x++)
                    {
                        int i = x * 4;
                        int amount = Math.Max(msk[i], Math.Max(msk[i + 1], msk[i + 2]));
                        if (amount == 0)
                        {
                            dst[i] = src[i];
                            dst[i + 1] = src[i + 1];
                            dst[i + 2] = src[i + 2];
                            dst[i + 3] = src[i + 3];
                            continue;
                        }

                        double blend = amount / 255d * strength;
                        for (int channel = 0; channel < 3; channel++)
                        {
                            int delta = generated[i + channel] - original[i + channel];
                            delta = Math.Max(-88, Math.Min(88, delta));
                            dst[i + channel] = ClampByte((int)Math.Round(src[i + channel] + delta * blend));
                        }
                        dst[i + 3] = src[i + 3];
                    }
                    Marshal.Copy(dst, 0, IntPtr.Add(outputData.Scan0, y * Math.Abs(outputData.Stride)), rowLength);
                }
            }
            finally
            {
                source.UnlockBits(sourceData);
                originalLight.UnlockBits(originalData);
                generatedLight.UnlockBits(generatedData);
                mask.UnlockBits(maskData);
                output.UnlockBits(outputData);
            }
        }

        private static byte ClampByte(int value)
        {
            return (byte)Math.Max(0, Math.Min(255, value));
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
