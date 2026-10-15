#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace KnittedPicture.Game
{
    /// <summary>
    /// Моль из частей, нарисованных кодом: крылья (правые, переднее и заднее; левые — те же, отражённые), пушистое тельце
    /// с глазками, перистый усик и дырка в полотне. Вид сверху, голова вверх. Цвета запечены: красить белым.
    /// Моль милая, а не страшная: карамельные передние крылья, пыльно-розовые задние, круглые глазки
    /// и румянец. Сливовый контур связывает её с цветами интерфейса.
    /// </summary>
    public static class MothArt
    {
        /// <summary>Размер крыла в точках макета при масштабе 1 (текстура в Art.Density раз крупнее).</summary>
        public static readonly Vector2 WingSize = new Vector2(WingW, WingH) / Art.Density;

        /// <summary>Где крыло крепится к телу (доля от размера): опора для взмаха.</summary>
        public static readonly Vector2 WingPivot = new Vector2(JointX / WingW, JointY / WingH);

        public static readonly Vector2 BodySize = new Vector2(BodyW, BodyH) / Art.Density;

        public static readonly Vector2 AntennaSize = new Vector2(AntennaW, AntennaH) / Art.Density;

        public static readonly Vector2 AntennaPivot = new Vector2(8f / AntennaW, 4f / AntennaH);

        /// <summary>Основание правого усика на тельце (в точках от центра тельца, y вверх).</summary>
        public static readonly Vector2 AntennaBase = new Vector2(33f - BodyW / 2f, 128f - BodyH / 2f) / Art.Density;

        /// <summary>Где на тельце крепятся крылья (в точках от центра тельца, y вверх).</summary>
        public static readonly Vector2 WingBase = new Vector2(4f, -10f) / Art.Density;

        private const float WingW = 144f;
        private const float WingH = 132f;
        private const float JointX = 8f;
        private const float JointY = 66f;
        private const float BodyW = 54f;
        private const float BodyH = 150f;
        private const float AntennaW = 40f;
        private const float AntennaH = 84f;

        private static readonly Color Outline = Hex(0x5E5248);
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        /// <summary>Переднее крыло (правое). Машет чуть раньше заднего: так взмах выглядит живым.</summary>
        public static Sprite ForeWing => Cached("fore", () => BuildWing(true));

        /// <summary>Заднее крыло (правое), лежит под передним.</summary>
        public static Sprite HindWing => Cached("hind", () => BuildWing(false));

        public static Sprite Body => Cached("body", BuildBody);

        public static Sprite Antenna => Cached("antenna", BuildAntenna);

        /// <summary>Прогрызенная петля: тёмная дырка с рваным краем и торчащими нитками.</summary>
        public static Sprite Hole => Cached("hole", BuildHole);

        private static Sprite Cached(string key, System.Func<Sprite> build)
        {
            if (!Cache.TryGetValue(key, out var sprite) || sprite == null)
            {
                sprite = build();
                Cache[key] = sprite;
            }

            return sprite;
        }

        /// <summary>
        /// Крыло: переднее и заднее — повёрнутые овалы, у корня слиты. Край слегка фестончатый, с клетчатой
        /// бахромой, как у настоящей моли. От корня к краю светлеет, по крылу расходятся жилки и тонкие
        /// штрихи чешуек, на каждом — «глазок». На переднем — светлая волнистая перевязь и тёмная полоса
        /// ближе к телу; заднее у стыка затенено передним.
        /// </summary>
        private static Sprite BuildWing(bool fore)
        {
            const int w = (int)WingW;
            const int h = (int)WingH;
            var rootColor = fore ? Hex(0x98785F) : Hex(0xA48E7C);
            var tipColor = fore ? Hex(0xE4CAA6) : Hex(0xE8DCCB);
            var pale = Hex(0xFFF3E0);
            var spotRing = Hex(0x6E5C4E);
            var spotFill = Hex(0xF4E8D6);
            var spotCore = Hex(0xA9836A);
            var veins = fore ? new[] { 8f, 20f, 32f, 44f, 56f } : new[] { -14f, -30f, -46f };
            var veinLength = fore ? 130f : 100f;
            var pixels = new Color32[w * h];
            for (var py = 0; py < h; py++)
            {
                for (var px = 0; px < w; px++)
                {
                    var x = px + 0.5f;
                    var y = py + 0.5f;
                    var dist = Mathf.Sqrt((x - JointX) * (x - JointX) + (y - JointY) * (y - JointY));
                    var angle = Mathf.Atan2(y - JointY, x - JointX);
                    var scallop = 0.7f * Mathf.Sin(angle * 34f) * SmoothStep(40f, 70f, dist);
                    var root = Mathf.Sqrt((x - 14f) * (x - 14f) + (y - 66f) * (y - 66f)) - 12f;
                    var foreShape = SMin(Ellipse(x, y, 76f, 92f, 64f, 29f, 26f), root, 10f) + scallop;
                    var shape = fore ? foreShape : SMin(Ellipse(x, y, 58f, 44f, 48f, 28f, -32f), root, 10f) + scallop;
                    var alpha = 0.97f * Mathf.Clamp01(0.5f - shape);
                    if (alpha <= 0f)
                    {
                        continue;
                    }

                    var color = Color.Lerp(rootColor, tipColor, Mathf.Clamp01(dist / 130f));
                    if (fore)
                    {
                        var inner = Mathf.Abs(dist - (48f + 3f * Mathf.Sin(angle * 7f)));
                        color *= 1f - 0.1f * Mathf.Clamp01(1f - inner / 5f);
                        var band = Mathf.Abs(dist - (78f + 4f * Mathf.Sin(angle * 9f)));
                        color = Color.Lerp(color, pale, 0.45f * Mathf.Clamp01(1f - band / 2.6f));
                    }
                    else if (foreShape < 5f)
                    {
                        color *= 0.86f + 0.14f * SmoothStep(-2f, 5f, foreShape);
                    }

                    var vein = 0f;
                    foreach (var a in veins)
                    {
                        var r = a * Mathf.Deg2Rad;
                        var d = DistanceToSegment(x, y, JointX, JointY, JointX + veinLength * Mathf.Cos(r), JointY + veinLength * Mathf.Sin(r));
                        vein = Mathf.Max(vein, Mathf.Clamp01(1.3f - d));
                    }

                    vein *= 1f - 0.6f * SmoothStep(60f, 120f, dist);
                    color *= 1f - 0.16f * vein;

                    // Чешуйки: короткие штрихи вдоль крыла, от корня наружу.
                    var scales = Hash(Mathf.FloorToInt(angle * 90f), Mathf.FloorToInt(dist / 3f));
                    color *= (0.95f + 0.08f * scales) * (0.98f + 0.04f * Hash(px, py));

                    if (fore)
                    {
                        color = Spot(color, x, y, 98f, 100f, 10f, 6.2f, spotRing, spotFill, spotCore);
                    }
                    else
                    {
                        color = Spot(color, x, y, 62f, 40f, 6.5f, 3.6f, spotRing, spotFill, spotCore);
                    }

                    // Бахрома по внешнему краю: светлые и тёмные пучки через один.
                    var fringe = shape > -4f && dist > 45f;
                    if (fringe)
                    {
                        color *= Mathf.Repeat(Mathf.Floor((angle + Mathf.PI) * 40f), 2f) < 1f ? 0.78f : 1.12f;
                    }

                    var line = shape > -2.2f ? Mathf.Clamp01(1.1f - Mathf.Abs(shape + 1.1f) + 0.5f) * (fringe ? 0.4f : 1f) : 0f;
                    color = Color.Lerp(color, Outline, line);
                    pixels[py * w + px] = Pixel(color, alpha);
                }
            }

            return Create(pixels, w, h, WingPivot);
        }

        /// <summary>«Глазок» на крыле: сливовое кольцо, светлая середина и розоватая точка.</summary>
        private static Color Spot(Color color, float x, float y, float cx, float cy, float ring, float fill,
            Color ringColor, Color fillColor, Color coreColor)
        {
            var d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
            color = Color.Lerp(color, ringColor, Mathf.Clamp01(ring - d + 0.5f));
            color = Color.Lerp(color, fillColor, Mathf.Clamp01(fill - d + 0.5f));
            var core = Mathf.Sqrt((x - cx - 1f) * (x - cx - 1f) + (y - cy - 1f) * (y - cy - 1f));
            return Color.Lerp(color, coreColor, Mathf.Clamp01(fill * 0.45f - core + 0.5f));
        }

        /// <summary>
        /// Тельце: полосатое брюшко, пушистая светлая грудка и голова с большими глазками-бусинами,
        /// бликами и румянцем. Край ворсистый: шум по контуру.
        /// </summary>
        private static Sprite BuildBody()
        {
            const int w = (int)BodyW;
            const int h = (int)BodyH;
            const float cx = BodyW / 2f;
            var belly = Hex(0xC2A78C);
            var chest = Hex(0xEADCC8);
            var face = Hex(0xD8C2A6);
            var eye = Hex(0x2A2A30);
            var blush = Hex(0xE2AE92);
            var pixels = new Color32[w * h];
            for (var py = 0; py < h; py++)
            {
                for (var px = 0; px < w; px++)
                {
                    var x = px + 0.5f;
                    var y = py + 0.5f;
                    var n = Hash(px, py);
                    var abdomen = Ellipse(x, y, cx, 56f, 13.5f, 34f, 0f);
                    var thorax = Mathf.Sqrt((x - cx) * (x - cx) + (y - 93f) * (y - 93f)) - 17f;
                    var head = Mathf.Sqrt((x - cx) * (x - cx) + (y - 120f) * (y - 120f)) - 14.5f;
                    var shape = SMin(SMin(abdomen, thorax, 5f), head, 4f);
                    var fuzz = (n - 0.5f) * 2.2f * SmoothStep(-3f, 0f, shape) + (n - 0.5f) * 1.5f;
                    shape += fuzz * 0.8f;
                    var alpha = Mathf.Clamp01(0.5f - shape);
                    if (alpha <= 0f)
                    {
                        continue;
                    }

                    Color color;
                    if (head < abdomen && head < thorax)
                    {
                        color = face;
                    }
                    else if (thorax < abdomen)
                    {
                        color = chest;
                    }
                    else
                    {
                        var stripe = y < 84f ? SmoothStep(0.55f, 0.95f, Mathf.Abs(Mathf.Sin((y - 54f) / 6.2f))) : 0f;
                        color = belly * (1f - 0.22f * stripe);
                    }

                    var lit = Mathf.Clamp01(1f - Mathf.Sqrt((x - 21f) * (x - 21f) + (y - 100f) * (y - 100f)) / 50f);
                    color *= (0.86f + 0.2f * lit) * (0.97f + 0.06f * n);

                    foreach (var side in new[] { -1f, 1f })
                    {
                        var ex = cx + side * 7.8f;
                        const float ey = 122f;
                        var d = Mathf.Sqrt((x - ex) * (x - ex) + (y - ey) * (y - ey));
                        color = Color.Lerp(color, eye, Mathf.Clamp01(6f - d + 0.5f));
                        var shine = Mathf.Sqrt((x - ex + 1.5f) * (x - ex + 1.5f) + (y - ey - 1.9f) * (y - ey - 1.9f));
                        color = Color.Lerp(color, Color.white, Mathf.Clamp01(1.9f - shine + 0.5f));
                        var cheek = Mathf.Sqrt((x - cx - side * 10.5f) * (x - cx - side * 10.5f) + (y - 112.5f) * (y - 112.5f));
                        color = Color.Lerp(color, blush, 0.45f * Mathf.Clamp01(3.2f - cheek + 0.5f));
                    }

                    var line = shape > -2.6f ? Mathf.Clamp01(1.3f - Mathf.Abs(shape + 1.3f) + 0.5f) : 0f;
                    color = Color.Lerp(color, Outline, line);
                    pixels[py * w + px] = Pixel(color, alpha);
                }
            }

            return Create(pixels, w, h, new Vector2(0.5f, 0.5f));
        }

        /// <summary>Перистый усик: изогнутый стержень с «гребёнкой» ворсинок, шире всего посередине.</summary>
        private static Sprite BuildAntenna()
        {
            const int w = (int)AntennaW;
            const int h = (int)AntennaH;
            var p0 = new Vector2(8f, 4f);
            var p1 = new Vector2(8f, 40f);
            var p2 = new Vector2(30f, 62f);
            var shaft = new List<Vector2>();
            for (var i = 0; i < 24; i++)
            {
                shaft.Add(Bezier(p0, p1, p2, i / 23f));
            }

            var barbs = new List<(Vector2 From, Vector2 To)>();
            for (var t = 0.18f; t < 0.97f; t += 0.055f)
            {
                var p = Bezier(p0, p1, p2, t);
                var tangent = (Bezier(p0, p1, p2, t + 0.01f) - p).normalized;
                var normal = new Vector2(-tangent.y, tangent.x);
                var length = 6.5f * Mathf.Max(0.25f, 1f - Mathf.Abs(t - 0.55f) * 1.6f);
                foreach (var side in new[] { -1f, 1f })
                {
                    barbs.Add((p, p + (normal * side * 0.82f + tangent * 0.57f) * length));
                }
            }

            var barbColor = Hex(0x8C6F68);
            var pixels = new Color32[w * h];
            for (var py = 0; py < h; py++)
            {
                for (var px = 0; px < w; px++)
                {
                    var x = px + 0.5f;
                    var y = py + 0.5f;
                    var d = float.MaxValue;
                    for (var i = 0; i + 1 < shaft.Count; i++)
                    {
                        d = Mathf.Min(d, DistanceToSegment(x, y, shaft[i].x, shaft[i].y, shaft[i + 1].x, shaft[i + 1].y));
                    }

                    var stem = Mathf.Clamp01(1.3f - d + 0.5f);
                    var tip = Mathf.Clamp01(2.6f - Mathf.Sqrt((x - p2.x) * (x - p2.x) + (y - p2.y) * (y - p2.y)) + 0.5f);
                    var barb = 0f;
                    foreach (var (from, to) in barbs)
                    {
                        barb = Mathf.Max(barb, Mathf.Clamp01(0.85f - DistanceToSegment(x, y, from.x, from.y, to.x, to.y) + 0.5f));
                    }

                    var alpha = Mathf.Max(Mathf.Max(stem, barb * 0.95f), tip);
                    if (alpha <= 0f)
                    {
                        continue;
                    }

                    pixels[py * w + px] = Pixel(stem >= barb || tip > 0f ? Outline : barbColor, alpha);
                }
            }

            return Create(pixels, w, h, AntennaPivot);
        }

        /// <summary>
        /// Дырка: неровный круг, внутри тёмная глубина (у верхнего края темнее — тень от петель),
        /// по краю короткие светлые ворсинки и три порванные нитки, свисающие внутрь.
        /// </summary>
        private static Sprite BuildHole()
        {
            const int size = 96;
            const float c = size / 2f;
            var deep = Hex(0x26262C);
            var fibre = Hex(0xF6EEE4);
            var threads = new List<(Vector2 From, Vector2 To)>();
            foreach (var (angle, length) in new[] { (0.6f, 13f), (2.4f, 11f), (4.3f, 14f) })
            {
                var rim = HoleRadius(angle);
                threads.Add((new Vector2(c + Mathf.Cos(angle) * (rim + 3f), c + Mathf.Sin(angle) * (rim + 3f)),
                    new Vector2(c + Mathf.Cos(angle + 0.35f) * (rim - length), c + Mathf.Sin(angle + 0.35f) * (rim - length))));
            }

            var pixels = new Color32[size * size];
            for (var py = 0; py < size; py++)
            {
                for (var px = 0; px < size; px++)
                {
                    var x = px + 0.5f;
                    var y = py + 0.5f;
                    var dx = x - c;
                    var dy = y - c;
                    var angle = Mathf.Atan2(dy, dx);
                    var radius = HoleRadius(angle);
                    var s = Mathf.Sqrt(dx * dx + dy * dy) - radius;
                    var inside = 0.9f * Mathf.Clamp01(0.5f - s);
                    var depth = Mathf.Clamp01((dy + radius) / (2f * radius));
                    var hole = deep * (0.75f + 0.45f * depth);

                    var k = Mathf.FloorToInt((angle + Mathf.PI) * 70f);
                    var hf = Hash(k, 7);
                    var reach = 2f + 5f * hf;
                    var fur = s > -1f && s < reach && hf > 0.35f ? (1f - Mathf.Clamp01(s / reach)) * 0.55f * (1f - inside) : 0f;

                    var thread = 0f;
                    foreach (var (from, to) in threads)
                    {
                        thread = Mathf.Max(thread, Mathf.Clamp01(2.2f - DistanceToSegment(x, y, from.x, from.y, to.x, to.y) + 0.5f));
                    }

                    var alpha = Mathf.Clamp01(inside + fur + thread);
                    if (alpha <= 0f)
                    {
                        continue;
                    }

                    var color = (hole * inside * (1f - thread) + fibre * (fur + thread)) / alpha;
                    pixels[py * size + px] = Pixel(color, alpha);
                }
            }

            return Create(pixels, size, size, new Vector2(0.5f, 0.5f));
        }

        private static float HoleRadius(float angle) =>
            29f + 3.5f * Mathf.Sin(3f * angle + 1f) + 2.5f * Mathf.Sin(5f * angle + 2.2f) + 1.6f * Mathf.Sin(9f * angle + 0.4f);

        /// <summary>Приближённое расстояние до повёрнутого овала (меньше нуля — внутри).</summary>
        private static float Ellipse(float x, float y, float cx, float cy, float a, float b, float degrees)
        {
            var r = degrees * Mathf.Deg2Rad;
            var dx = x - cx;
            var dy = y - cy;
            var u = dx * Mathf.Cos(r) + dy * Mathf.Sin(r);
            var v = -dx * Mathf.Sin(r) + dy * Mathf.Cos(r);
            return (Mathf.Sqrt(u / a * (u / a) + v / b * (v / b)) - 1f) * Mathf.Min(a, b);
        }

        /// <summary>Мягкое объединение двух форм: стык скругляется на k.</summary>
        private static float SMin(float a, float b, float k)
        {
            var h = Mathf.Clamp01(0.5f + 0.5f * (b - a) / k);
            return b * (1f - h) + a * h - k * h * (1f - h);
        }

        private static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, float t)
        {
            var u = 1f - t;
            return u * u * a + 2f * u * t * b + t * t * c;
        }

        private static float DistanceToSegment(float x, float y, float ax, float ay, float bx, float by)
        {
            var dx = bx - ax;
            var dy = by - ay;
            var t = Mathf.Clamp01(((x - ax) * dx + (y - ay) * dy) / (dx * dx + dy * dy));
            var ex = x - (ax + t * dx);
            var ey = y - (ay + t * dy);
            return Mathf.Sqrt(ex * ex + ey * ey);
        }

        private static float SmoothStep(float from, float to, float t)
        {
            t = Mathf.Clamp01((t - from) / (to - from));
            return t * t * (3f - 2f * t);
        }

        private static float Hash(int x, int y)
        {
            unchecked
            {
                var n = (uint)(x * 374761393 + y * 668265263);
                n = (n ^ (n >> 13)) * 1274126177u;
                return ((n ^ (n >> 16)) & 0xffff) / 65535f;
            }
        }

        private static Color Hex(int rgb) => new Color(((rgb >> 16) & 0xff) / 255f, ((rgb >> 8) & 0xff) / 255f, (rgb & 0xff) / 255f);

        private static Color32 Pixel(Color color, float alpha) =>
            new Color32((byte)(Mathf.Clamp01(color.r) * 255), (byte)(Mathf.Clamp01(color.g) * 255), (byte)(Mathf.Clamp01(color.b) * 255),
                (byte)(Mathf.Clamp01(alpha) * 255));

        private static Sprite Create(Color32[] pixels, int w, int h, Vector2 pivot)
        {
            // Моль маленькая, а текстура крупная: с mip-уровнями мелкие детали не рябят при уменьшении.
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, true)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
            };
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            return Sprite.Create(texture, new Rect(0, 0, w, h), pivot, 100f);
        }
    }
}
