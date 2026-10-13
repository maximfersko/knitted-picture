#nullable enable

using System;

namespace KnittedPicture.Game
{
    /// <summary>
    /// Картинка в памяти: цвет и прозрачность по пикселям, строка 0 — нижняя (как у текстур Unity).
    /// Рисовалки 2.0 (петли, клубки, станок, шкатулка) пишут сюда без единой ссылки на движок:
    /// тот же код рисует спрайты в игре и превью в PNG (dotnet/KnittedPicture.ArtPreview), так что
    /// внешний вид проверяется без Unity, ровно тем кодом, что попадёт в сборку.
    /// </summary>
    public sealed class Raster
    {
        public Raster(int width, int height)
        {
            Width = width;
            Height = height;
            Pixels = new Rgba[width * height];
        }

        public int Width { get; }

        public int Height { get; }

        /// <summary>Цвет без домножения на прозрачность: (r, g, b) — сам цвет, a — покрытие.</summary>
        public Rgba[] Pixels { get; }

        public Rgba this[int x, int y]
        {
            get => Pixels[y * Width + x];
            set => Pixels[y * Width + x] = value;
        }

        /// <summary>Положить цвет поверх (обычное наложение «source over»).</summary>
        public void Blend(int x, int y, Rgba top)
        {
            if (top.A <= 0f || x < 0 || y < 0 || x >= Width || y >= Height)
            {
                return;
            }

            var i = y * Width + x;
            Pixels[i] = Rgba.Over(top, Pixels[i]);
        }

        /// <summary>Пройти все пиксели: (x, y) — центр пикселя, y вверх.</summary>
        public void Paint(Func<float, float, Rgba> shader)
        {
            for (var py = 0; py < Height; py++)
            {
                for (var px = 0; px < Width; px++)
                {
                    var top = shader(px + 0.5f, py + 0.5f);
                    if (top.A > 0f)
                    {
                        var i = py * Width + px;
                        Pixels[i] = Rgba.Over(top, Pixels[i]);
                    }
                }
            }
        }

        /// <summary>Байты RGBA32 построчно снизу вверх — для Texture2D.SetPixelData и для PNG.</summary>
        public byte[] ToBytes()
        {
            var bytes = new byte[Width * Height * 4];
            for (var i = 0; i < Pixels.Length; i++)
            {
                var p = Pixels[i];
                bytes[i * 4] = Byte(p.R);
                bytes[i * 4 + 1] = Byte(p.G);
                bytes[i * 4 + 2] = Byte(p.B);
                bytes[i * 4 + 3] = Byte(p.A);
            }

            return bytes;
        }

        private static byte Byte(float v) => (byte)MathF.Round(Draw.Clamp01(v) * 255f);
    }

    /// <summary>Цвет с прозрачностью для растров 2.0.</summary>
    public readonly struct Rgba
    {
        public Rgba(float r, float g, float b, float a = 1f)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        public float R { get; }

        public float G { get; }

        public float B { get; }

        public float A { get; }

        public static Rgba Clear => new Rgba(0f, 0f, 0f, 0f);

        public static Rgba White => new Rgba(1f, 1f, 1f);

        public static Rgba Hex(string hex)
        {
            hex = hex.TrimStart('#');
            var v = Convert.ToInt32(hex.Substring(0, 6), 16);
            var a = hex.Length >= 8 ? Convert.ToInt32(hex.Substring(6, 2), 16) / 255f : 1f;
            return new Rgba(((v >> 16) & 255) / 255f, ((v >> 8) & 255) / 255f, (v & 255) / 255f, a);
        }

        public Rgba WithAlpha(float a) => new Rgba(R, G, B, a);

        public Rgba Scale(float k) => new Rgba(R * k, G * k, B * k, A);

        public static Rgba Lerp(Rgba a, Rgba b, float t)
        {
            t = Draw.Clamp01(t);
            return new Rgba(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t, a.A + (b.A - a.A) * t);
        }

        /// <summary>Наложение top поверх bottom.</summary>
        public static Rgba Over(Rgba top, Rgba bottom)
        {
            var a = top.A + bottom.A * (1f - top.A);
            if (a <= 0f)
            {
                return Clear;
            }

            var k = bottom.A * (1f - top.A);
            return new Rgba((top.R * top.A + bottom.R * k) / a, (top.G * top.A + bottom.G * k) / a, (top.B * top.A + bottom.B * k) / a, a);
        }

        /// <summary>Светлее (t > 0, к белому) или темнее (t &lt; 0, к густому оттенку того же цвета, не к серому).</summary>
        public Rgba Shade(float t)
        {
            if (t >= 0f)
            {
                return Lerp(this, new Rgba(1f, 1f, 1f, A), t);
            }

            // Тень — тот же оттенок, насыщеннее и темнее: умножение на себя даёт густой цвет без серости.
            var deep = new Rgba(R * R * 0.85f, G * G * 0.85f, B * B * 0.85f, A);
            return Lerp(this, deep, -t);
        }
    }

    /// <summary>Математика для рисовалок: формы со знаковым расстоянием (меньше нуля — внутри), шум, кривые.</summary>
    public static class Draw
    {
        public static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

        public static float Clamp(float v, float min, float max) => v < min ? min : v > max ? max : v;

        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);

        public static float SmoothStep(float from, float to, float t)
        {
            t = Clamp01((t - from) / (to - from));
            return t * t * (3f - 2f * t);
        }

        /// <summary>Покрытие пикселя по знаковому расстоянию: мягкий край шириной в пиксель.</summary>
        public static float Cover(float distance) => Clamp01(0.5f - distance);

        /// <summary>Повторяемый шум 0..1 по целым координатам.</summary>
        public static float Hash(int x, int y)
        {
            unchecked
            {
                var n = (uint)(x * 374761393 + y * 668265263);
                n = (n ^ (n >> 13)) * 1274126177u;
                return ((n ^ (n >> 16)) & 0xffff) / 65535f;
            }
        }

        /// <summary>Плавный шум (значения в узлах сетки, сглаженная интерполяция): волокна, зерно дерева, бумага.</summary>
        public static float Noise(float x, float y)
        {
            var ix = (int)MathF.Floor(x);
            var iy = (int)MathF.Floor(y);
            var fx = x - ix;
            var fy = y - iy;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            var a = Hash(ix, iy);
            var b = Hash(ix + 1, iy);
            var c = Hash(ix, iy + 1);
            var d = Hash(ix + 1, iy + 1);
            return a + (b - a) * fx + (c - a) * fy + (a - b - c + d) * fx * fy;
        }

        public static float Circle(float x, float y, float cx, float cy, float r) =>
            MathF.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r;

        /// <summary>Приближённое расстояние до повёрнутого овала.</summary>
        public static float Ellipse(float x, float y, float cx, float cy, float a, float b, float degrees = 0f)
        {
            var r = degrees * MathF.PI / 180f;
            var dx = x - cx;
            var dy = y - cy;
            var u = dx * MathF.Cos(r) + dy * MathF.Sin(r);
            var v = -dx * MathF.Sin(r) + dy * MathF.Cos(r);
            return (MathF.Sqrt(u / a * (u / a) + v / b * (v / b)) - 1f) * MathF.Min(a, b);
        }

        /// <summary>Скруглённый прямоугольник с центром (cx, cy), полуразмерами hw, hh и радиусом r.</summary>
        public static float RoundBox(float x, float y, float cx, float cy, float hw, float hh, float r)
        {
            var qx = MathF.Abs(x - cx) - hw + r;
            var qy = MathF.Abs(y - cy) - hh + r;
            var ox = MathF.Max(qx, 0f);
            var oy = MathF.Max(qy, 0f);
            return MathF.Sqrt(ox * ox + oy * oy) + MathF.Min(MathF.Max(qx, qy), 0f) - r;
        }

        /// <summary>Сужающаяся капсула от A (радиус ra) к B (радиус rb). t — доля пути вдоль оси, across — поперёк (−1..1).</summary>
        public static float Capsule(float x, float y, float ax, float ay, float bx, float by, float ra, float rb, out float t, out float across)
        {
            var dx = bx - ax;
            var dy = by - ay;
            var l2 = dx * dx + dy * dy;
            var len = MathF.Sqrt(l2);
            t = Clamp01(((x - ax) * dx + (y - ay) * dy) / l2);
            var r = ra + (rb - ra) * t;
            var px = ax + t * dx;
            var py = ay + t * dy;
            across = ((x - ax) * -dy + (y - ay) * dx) / len / MathF.Max(r, 0.001f);
            return MathF.Sqrt((x - px) * (x - px) + (y - py) * (y - py)) - r;
        }

        /// <summary>Треугольник (вершины против часовой или по часовой): расстояние до ближайшей стороны, внутри меньше нуля.</summary>
        public static float Triangle(float x, float y, float ax, float ay, float bx, float by, float cx, float cy)
        {
            var area = (bx - ax) * (cy - ay) - (by - ay) * (cx - ax);
            var sign = area < 0f ? -1f : 1f;
            float Edge(float px, float py, float qx, float qy)
            {
                var ex = qx - px;
                var ey = qy - py;
                var len = MathF.Sqrt(ex * ex + ey * ey);
                return -sign * (ex * (y - py) - ey * (x - px)) / len;
            }

            return MathF.Max(MathF.Max(Edge(ax, ay, bx, by), Edge(bx, by, cx, cy)), Edge(cx, cy, ax, ay));
        }

        public static float Segment(float x, float y, float ax, float ay, float bx, float by)
        {
            var dx = bx - ax;
            var dy = by - ay;
            var t = Clamp01(((x - ax) * dx + (y - ay) * dy) / (dx * dx + dy * dy));
            var ex = x - (ax + t * dx);
            var ey = y - (ay + t * dy);
            return MathF.Sqrt(ex * ex + ey * ey);
        }

        /// <summary>Мягкое объединение форм: стык скругляется на k.</summary>
        public static float SMin(float a, float b, float k)
        {
            var h = Clamp01(0.5f + 0.5f * (b - a) / k);
            return b * (1f - h) + a * h - k * h * (1f - h);
        }

        /// <summary>Точка квадратичной кривой Безье.</summary>
        public static (float X, float Y) Bezier(float ax, float ay, float bx, float by, float cx, float cy, float t)
        {
            var u = 1f - t;
            return (u * u * ax + 2f * u * t * bx + t * t * cx, u * u * ay + 2f * u * t * by + t * t * cy);
        }
    }
}
