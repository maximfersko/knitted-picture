#nullable enable
using System;
namespace KnittedPicture.Game
{
    /// <summary>Общая геометрия и материал кручёной связки.</summary>
    public static class LinkCordRaster
    {
        public static (float X, float Y) Point(float ax, float ay, float bx, float by, float sag, float t) =>
            (ax + (bx - ax) * t, ay + (by - ay) * t + sag * 4f * t * (1f - t));

        /// <summary>Ограниченная сетка с общими нормалями и UV по пройденной длине.</summary>
        public struct Sample
        {
            public float X, Y, Nx, Ny, U;
        }
        public const int MaxSegments = 64;
        public const float Period = 20f;
        public static int Fill(Sample[] samples, float ax, float ay, float bx, float by, float sag, float pixelScale = 1f)
        {
            var dx = bx - ax;
            var dy = by - ay;
            var estimated = MathF.Sqrt(dx * dx + dy * dy) + MathF.Abs(sag) * 2f;
            var count = Math.Min(MaxSegments, Math.Max(8, (int)MathF.Ceiling(estimated * MathF.Sqrt(Math.Max(0.25f, pixelScale)) / 8f)));
            var length = 0f;
            for (var i = 0; i <= count; i++)
            {
                var t = (float)i / count;
                var p = Point(ax, ay, bx, by, sag, t);
                var tx = dx;
                var ty = dy + 4f * sag * (1f - 2f * t);
                var norm = MathF.Sqrt(tx * tx + ty * ty);
                if (norm < 0.0001f) { tx = 1f; ty = 0f; norm = 1f; }
                if (i > 0)
                {
                    var px = p.X - samples[i - 1].X;
                    var py = p.Y - samples[i - 1].Y;
                    length += MathF.Sqrt(px * px + py * py);
                }
                samples[i] = new Sample { X = p.X, Y = p.Y, Nx = -ty / norm, Ny = tx / norm, U = length / Period };
            }
            return count;
        }

        /// <summary>Бесшовная матовая лента с мягким краем и тонкими волокнами.</summary>
        public static Raster Tile()
        {
            const int w = 128, h = 48;
            var r = new Raster(w, h);
            r.Paint((x, y) =>
            {
                var across = (y - h / 2f) / (h / 2f - 1f);
                var cover = Draw.SmoothStep(1f, 0.86f, MathF.Abs(across));
                var profile = MathF.Sqrt(Draw.Clamp01(1f - across * across));
                var phase = 2f * MathF.PI * (x / w * 2f + across * 0.23f);
                var helix = MathF.Sin(phase);
                var fiber = MathF.Sin(phase * 3f + across * 10f);
                var shade = Draw.Clamp01(0.63f + 0.20f * profile + 0.11f * helix + 0.025f * fiber);
                return new Rgba(shade, shade, shade, cover);
            });
            return r;
        }

        /// <summary>Небольшой объёмный узел, окрашиваемый цветом пары.</summary>
        public static Raster Knot()
        {
            const int size = 32;
            var r = new Raster(size, size);
            r.Paint((x, y) =>
            {
                var d = Draw.Circle(x, y, 16f, 16f, 13f);
                var cover = Draw.Cover(d);
                var shade = 0.55f + 0.35f * Draw.Clamp01((y - 4f) / 22f);
                if (MathF.Abs(x - y + 2f) < 2.5f) { shade *= 0.72f; }
                return new Rgba(shade, shade, shade, cover);
            });
            return r;
        }
    }
}
