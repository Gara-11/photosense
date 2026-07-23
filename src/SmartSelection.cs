using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace PixelPatchStudio
{
    internal sealed class SmartRegion
    {
        public readonly int Width;
        public readonly int Height;
        public readonly byte[] Selected;
        public readonly Rectangle Bounds;
        public readonly Rectangle OriginalBounds;
        public readonly int PixelCount;

        public SmartRegion(int width, int height, byte[] selected, Rectangle bounds, Rectangle originalBounds, int pixelCount)
        {
            Width = width;
            Height = height;
            Selected = selected;
            Bounds = bounds;
            OriginalBounds = originalBounds;
            PixelCount = pixelCount;
        }
    }

    internal sealed class SmartSelectionEngine
    {
        private const int AnalysisMaxEdge = 1280;
        private readonly int originalWidth;
        private readonly int originalHeight;
        private readonly int width;
        private readonly int height;
        private readonly byte[] luminance;
        private readonly byte[] chromaBlue;
        private readonly byte[] chromaRed;
        private readonly byte[] edge;
        private readonly int[] regionQueue;

        public SmartSelectionEngine(Bitmap source)
        {
            if (source == null) throw new ArgumentNullException("source");
            originalWidth = source.Width;
            originalHeight = source.Height;
            float scale = Math.Min(1f, AnalysisMaxEdge / (float)Math.Max(source.Width, source.Height));
            width = Math.Max(1, (int)Math.Round(source.Width * scale));
            height = Math.Max(1, (int)Math.Round(source.Height * scale));
            luminance = new byte[width * height];
            chromaBlue = new byte[width * height];
            chromaRed = new byte[width * height];
            edge = new byte[width * height];
            regionQueue = new int[width * height];
            using (Bitmap analysis = Resize(source, width, height))
            {
                ReadColor(analysis);
            }
            BuildEdgeMap();
        }

        public SmartRegion Select(PointF originalPoint)
        {
            int seedX = Math.Max(0, Math.Min(width - 1, (int)(originalPoint.X * width / originalWidth)));
            int seedY = Math.Max(0, Math.Min(height - 1, (int)(originalPoint.Y * height / originalHeight)));
            int seed = seedY * width + seedX;
            int tolerance = SeedTolerance(seedX, seedY, seed);
            byte[] selected = new byte[width * height];
            int head = 0;
            int tail = 0;
            regionQueue[tail++] = seed;
            selected[seed] = 1;
            int minX = seedX;
            int maxX = seedX;
            int minY = seedY;
            int maxY = seedY;
            int count = 0;

            while (head < tail)
            {
                int current = regionQueue[head++];
                int x = current % width;
                int y = current / width;
                selected[current] = 255;
                count++;
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;

                if (x > 0) TryQueue(current - 1, current, seed, tolerance, selected, ref tail);
                if (x + 1 < width) TryQueue(current + 1, current, seed, tolerance, selected, ref tail);
                if (y > 0) TryQueue(current - width, current, seed, tolerance, selected, ref tail);
                if (y + 1 < height) TryQueue(current + width, current, seed, tolerance, selected, ref tail);
            }

            Rectangle bounds;
            selected = CleanRegion(selected, seedX, seedY, out count, out bounds);
            Rectangle originalBounds = bounds.IsEmpty
                ? Rectangle.Empty
                : Rectangle.FromLTRB(
                    Math.Max(0, bounds.Left * originalWidth / width),
                    Math.Max(0, bounds.Top * originalHeight / height),
                    Math.Min(originalWidth, (bounds.Right * originalWidth + width - 1) / width),
                    Math.Min(originalHeight, (bounds.Bottom * originalHeight + height - 1) / height));
            return new SmartRegion(width, height, selected, bounds, originalBounds, count);
        }

        public void ApplyRegion(Bitmap targetMask, SmartRegion region, bool add)
        {
            if (targetMask == null) throw new ArgumentNullException("targetMask");
            if (region == null || region.Bounds.IsEmpty || region.PixelCount == 0) return;
            if (targetMask.Width != originalWidth || targetMask.Height != originalHeight)
                throw new ArgumentException("蒙版尺寸与智能选区分析源不一致。", "targetMask");

            int left = Math.Max(0, region.Bounds.Left * originalWidth / width);
            int top = Math.Max(0, region.Bounds.Top * originalHeight / height);
            int right = Math.Min(originalWidth, (region.Bounds.Right * originalWidth + width - 1) / width);
            int bottom = Math.Min(originalHeight, (region.Bounds.Bottom * originalHeight + height - 1) / height);
            Rectangle full = new Rectangle(0, 0, originalWidth, originalHeight);
            BitmapData data = targetMask.LockBits(full, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            try
            {
                int stride = Math.Abs(data.Stride);
                int rowWidth = Math.Max(0, right - left);
                byte[] row = new byte[rowWidth * 4];
                byte value = add ? (byte)255 : (byte)0;
                for (int y = top; y < bottom; y++)
                {
                    int analysisY = Math.Min(height - 1, y * height / originalHeight);
                    IntPtr rowStart = IntPtr.Add(data.Scan0, y * stride + left * 4);
                    Marshal.Copy(rowStart, row, 0, row.Length);
                    for (int x = left; x < right; x++)
                    {
                        int analysisX = Math.Min(width - 1, x * width / originalWidth);
                        if (region.Selected[analysisY * width + analysisX] != 255) continue;
                        int offset = (x - left) * 4;
                        row[offset] = value;
                        row[offset + 1] = value;
                        row[offset + 2] = value;
                        row[offset + 3] = 255;
                    }
                    Marshal.Copy(row, 0, rowStart, row.Length);
                }
            }
            finally
            {
                targetMask.UnlockBits(data);
            }
        }

        public List<PointF> SnapSegment(PointF originalStart, PointF originalEnd)
        {
            PointF start = ToAnalysis(originalStart);
            PointF end = ToAnalysis(originalEnd);
            float dx = end.X - start.X;
            float dy = end.Y - start.Y;
            float distance = (float)Math.Sqrt(dx * dx + dy * dy);
            List<PointF> path = new List<PointF>();
            if (distance < 2f)
            {
                path.Add(originalStart);
                path.Add(originalEnd);
                return path;
            }

            float normalX = -dy / distance;
            float normalY = dx / distance;
            int steps = Math.Max(2, (int)Math.Ceiling(distance / 4f));
            const int searchRadius = 14;
            int previousOffset = 0;
            for (int step = 0; step <= steps; step++)
            {
                if (step == 0) { path.Add(originalStart); continue; }
                if (step == steps) { path.Add(originalEnd); continue; }
                float amount = step / (float)steps;
                float baseX = start.X + dx * amount;
                float baseY = start.Y + dy * amount;
                int bestX = Math.Max(0, Math.Min(width - 1, (int)Math.Round(baseX)));
                int bestY = Math.Max(0, Math.Min(height - 1, (int)Math.Round(baseY)));
                int bestScore = -1;
                int bestOffset = 0;
                for (int offset = -searchRadius; offset <= searchRadius; offset++)
                {
                    int candidateX = (int)Math.Round(baseX + normalX * offset);
                    int candidateY = (int)Math.Round(baseY + normalY * offset);
                    if (candidateX < 1 || candidateX >= width - 1 || candidateY < 1 || candidateY >= height - 1) continue;
                    int score = edge[candidateY * width + candidateX] * 5 -
                        Math.Abs(offset) * 3 -
                        Math.Abs(offset - previousOffset) * 18;
                    if (score <= bestScore) continue;
                    bestScore = score;
                    bestX = candidateX;
                    bestY = candidateY;
                    bestOffset = offset;
                }
                previousOffset = bestOffset;
                path.Add(ToOriginal(new PointF(bestX, bestY)));
            }

            for (int pass = 0; pass < 2; pass++)
            {
                PointF[] smoothed = path.ToArray();
                for (int i = 1; i < path.Count - 1; i++)
                {
                    PointF previous = path[i - 1];
                    PointF current = path[i];
                    PointF next = path[i + 1];
                    smoothed[i] = new PointF(
                        previous.X * 0.25f + current.X * 0.5f + next.X * 0.25f,
                        previous.Y * 0.25f + current.Y * 0.5f + next.Y * 0.25f);
                }
                path = new List<PointF>(smoothed);
            }
            float simplifyTolerance = Math.Max(0.75f, 1.25f * Math.Max(originalWidth / (float)width, originalHeight / (float)height));
            return SimplifyPath(path, simplifyTolerance);
        }

        private void TryQueue(int candidate, int previous, int seed, int tolerance, byte[] selected, ref int tail)
        {
            if (selected[candidate] != 0) return;
            selected[candidate] = 1;
            int seedDistance = ColorDistance(candidate, seed);
            int localDistance = ColorDistance(candidate, previous);
            int edgeStrength = edge[candidate];
            int softenedTolerance = tolerance + Math.Max(0, 18 - edgeStrength / 5);
            bool directlyMatches = seedDistance <= softenedTolerance;
            bool followsGradient = localDistance <= 20 && seedDistance <= tolerance * 2 && edgeStrength < 105;
            if (!directlyMatches && !followsGradient) return;
            if (edgeStrength > 150 && seedDistance > tolerance / 2) return;
            regionQueue[tail++] = candidate;
        }

        private byte[] CleanRegion(byte[] raw, int seedX, int seedY, out int count, out Rectangle bounds)
        {
            byte[] eroded = new byte[raw.Length];
            byte[] opened = new byte[raw.Length];
            for (int y = 1; y < height - 1; y++)
            {
                int row = y * width;
                for (int x = 1; x < width - 1; x++)
                {
                    int index = row + x;
                    if (raw[index] == 255 && raw[index - 1] == 255 && raw[index + 1] == 255 &&
                        raw[index - width] == 255 && raw[index + width] == 255)
                        eroded[index] = 255;
                }
            }
            for (int y = 1; y < height - 1; y++)
            {
                int row = y * width;
                for (int x = 1; x < width - 1; x++)
                {
                    int index = row + x;
                    if (eroded[index] == 255 || eroded[index - 1] == 255 || eroded[index + 1] == 255 ||
                        eroded[index - width] == 255 || eroded[index + width] == 255)
                        opened[index] = 255;
                }
            }

            int start = FindNearestSelected(opened, seedX, seedY, 14);
            if (start < 0) return NormalizeRawRegion(raw, out count, out bounds);
            byte[] clean = new byte[raw.Length];
            int head = 0;
            int tail = 0;
            regionQueue[tail++] = start;
            clean[start] = 255;
            int minX = start % width;
            int maxX = minX;
            int minY = start / width;
            int maxY = minY;
            count = 0;
            while (head < tail)
            {
                int current = regionQueue[head++];
                int x = current % width;
                int y = current / width;
                count++;
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
                QueueCleanNeighbor(current - 1, opened, clean, ref tail, x > 0);
                QueueCleanNeighbor(current + 1, opened, clean, ref tail, x + 1 < width);
                QueueCleanNeighbor(current - width, opened, clean, ref tail, y > 0);
                QueueCleanNeighbor(current + width, opened, clean, ref tail, y + 1 < height);
            }
            if (count < 16) return NormalizeRawRegion(raw, out count, out bounds);
            bounds = Rectangle.FromLTRB(minX, minY, maxX + 1, maxY + 1);
            return clean;
        }

        private int FindNearestSelected(byte[] selected, int seedX, int seedY, int radius)
        {
            int best = -1;
            int bestDistance = int.MaxValue;
            for (int y = Math.Max(0, seedY - radius); y <= Math.Min(height - 1, seedY + radius); y++)
            {
                for (int x = Math.Max(0, seedX - radius); x <= Math.Min(width - 1, seedX + radius); x++)
                {
                    int index = y * width + x;
                    if (selected[index] != 255) continue;
                    int dx = x - seedX;
                    int dy = y - seedY;
                    int distance = dx * dx + dy * dy;
                    if (distance >= bestDistance) continue;
                    bestDistance = distance;
                    best = index;
                }
            }
            return best;
        }

        private void QueueCleanNeighbor(int candidate, byte[] opened, byte[] clean, ref int tail, bool valid)
        {
            if (!valid || opened[candidate] != 255 || clean[candidate] != 0) return;
            clean[candidate] = 255;
            regionQueue[tail++] = candidate;
        }

        private byte[] NormalizeRawRegion(byte[] raw, out int count, out Rectangle bounds)
        {
            byte[] normalized = new byte[raw.Length];
            int minX = width;
            int maxX = -1;
            int minY = height;
            int maxY = -1;
            count = 0;
            for (int index = 0; index < raw.Length; index++)
            {
                if (raw[index] != 255) continue;
                normalized[index] = 255;
                int x = index % width;
                int y = index / width;
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
                count++;
            }
            bounds = count == 0 ? Rectangle.Empty : Rectangle.FromLTRB(minX, minY, maxX + 1, maxY + 1);
            return normalized;
        }

        private static List<PointF> SimplifyPath(List<PointF> path, float tolerance)
        {
            if (path == null || path.Count < 3) return path;
            bool[] keep = new bool[path.Count];
            keep[0] = true;
            keep[path.Count - 1] = true;
            MarkSimplified(path, 0, path.Count - 1, tolerance * tolerance, keep);
            List<PointF> simplified = new List<PointF>();
            for (int index = 0; index < path.Count; index++)
                if (keep[index]) simplified.Add(path[index]);
            return simplified;
        }

        private static void MarkSimplified(IList<PointF> path, int first, int last, float toleranceSquared, bool[] keep)
        {
            if (last <= first + 1) return;
            PointF start = path[first];
            PointF end = path[last];
            float dx = end.X - start.X;
            float dy = end.Y - start.Y;
            float lengthSquared = dx * dx + dy * dy;
            int farthest = -1;
            float maximumDistance = -1f;
            for (int index = first + 1; index < last; index++)
            {
                PointF point = path[index];
                float distance;
                if (lengthSquared <= 0.0001f)
                {
                    float px = point.X - start.X;
                    float py = point.Y - start.Y;
                    distance = px * px + py * py;
                }
                else
                {
                    float amount = Math.Max(0f, Math.Min(1f, ((point.X - start.X) * dx + (point.Y - start.Y) * dy) / lengthSquared));
                    float nearestX = start.X + amount * dx;
                    float nearestY = start.Y + amount * dy;
                    float px = point.X - nearestX;
                    float py = point.Y - nearestY;
                    distance = px * px + py * py;
                }
                if (distance <= maximumDistance) continue;
                maximumDistance = distance;
                farthest = index;
            }
            if (farthest < 0 || maximumDistance <= toleranceSquared) return;
            keep[farthest] = true;
            MarkSimplified(path, first, farthest, toleranceSquared, keep);
            MarkSimplified(path, farthest, last, toleranceSquared, keep);
        }

        private int SeedTolerance(int seedX, int seedY, int seed)
        {
            int sum = 0;
            int count = 0;
            for (int y = Math.Max(0, seedY - 4); y <= Math.Min(height - 1, seedY + 4); y++)
            {
                for (int x = Math.Max(0, seedX - 4); x <= Math.Min(width - 1, seedX + 4); x++)
                {
                    sum += ColorDistance(y * width + x, seed);
                    count++;
                }
            }
            int localVariation = count == 0 ? 0 : sum / count;
            return Math.Max(28, Math.Min(78, 30 + localVariation / 2));
        }

        private int ColorDistance(int first, int second)
        {
            return Math.Abs(luminance[first] - luminance[second]) +
                (Math.Abs(chromaBlue[first] - chromaBlue[second]) * 3) / 4 +
                (Math.Abs(chromaRed[first] - chromaRed[second]) * 3) / 4;
        }

        private void ReadColor(Bitmap bitmap)
        {
            Rectangle rectangle = new Rectangle(0, 0, width, height);
            BitmapData data = bitmap.LockBits(rectangle, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                int stride = Math.Abs(data.Stride);
                byte[] row = new byte[width * 4];
                for (int y = 0; y < height; y++)
                {
                    Marshal.Copy(IntPtr.Add(data.Scan0, y * stride), row, 0, row.Length);
                    for (int x = 0; x < width; x++)
                    {
                        int source = x * 4;
                        int target = y * width + x;
                        int blue = row[source];
                        int green = row[source + 1];
                        int red = row[source + 2];
                        luminance[target] = Clamp((77 * red + 150 * green + 29 * blue) >> 8);
                        chromaBlue[target] = Clamp(128 + ((-43 * red - 85 * green + 128 * blue) >> 8));
                        chromaRed[target] = Clamp(128 + ((128 * red - 107 * green - 21 * blue) >> 8));
                    }
                }
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
        }

        private void BuildEdgeMap()
        {
            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    int index = y * width + x;
                    int horizontal = Math.Abs(luminance[index + 1] - luminance[index - 1]);
                    int vertical = Math.Abs(luminance[index + width] - luminance[index - width]);
                    int chroma = Math.Abs(chromaBlue[index + 1] - chromaBlue[index - 1]) +
                        Math.Abs(chromaRed[index + 1] - chromaRed[index - 1]) +
                        Math.Abs(chromaBlue[index + width] - chromaBlue[index - width]) +
                        Math.Abs(chromaRed[index + width] - chromaRed[index - width]);
                    edge[index] = Clamp(horizontal + vertical + chroma / 3);
                }
            }
        }

        private PointF ToAnalysis(PointF point)
        {
            return new PointF(point.X * width / originalWidth, point.Y * height / originalHeight);
        }

        private PointF ToOriginal(PointF point)
        {
            return new PointF(point.X * originalWidth / width, point.Y * originalHeight / height);
        }

        private static Bitmap Resize(Bitmap source, int targetWidth, int targetHeight)
        {
            Bitmap result = new Bitmap(targetWidth, targetHeight, PixelFormat.Format32bppArgb);
            using (Graphics graphics = Graphics.FromImage(result))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.CompositingQuality = CompositingQuality.HighSpeed;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.DrawImage(source, new Rectangle(0, 0, targetWidth, targetHeight), 0, 0, source.Width, source.Height, GraphicsUnit.Pixel);
            }
            return result;
        }

        private static byte Clamp(int value)
        {
            return (byte)Math.Max(0, Math.Min(255, value));
        }
    }
}
