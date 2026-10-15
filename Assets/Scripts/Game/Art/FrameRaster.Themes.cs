#nullable enable

using System;

namespace KnittedPicture.Game
{
    /// <summary>
    /// Тематические рамки: четыре сезонные (снежинки со снежной шапкой, первоцветы, подсолнухи, листопад) и по одной
    /// на каждую коллекцию альбома (пуговицы, цветы, лапки, вишенки, волны, облака, ёлочки, звёзды, домики,
    /// сердечки, спицы, компас, планеты). Мотив повторяется по середине багета, крупный — на углах.
    /// </summary>
    public static partial class FrameRaster
    {
        /// <summary>Шаг мотивов вдоль багета.</summary>
        private const float MotifStep = 9f;

        private static Rgba ThemePattern(int style, Look look, float x, float y, float s, float depth, float u)
        {
            var c = look.Face.Shade(0.05f * MathF.Sin(depth * MathF.PI * 1.25f) - 0.02f);
            switch (style)
            {
                case 10:
                {
                    // Иней: к внешнему краю светлее, искры инея.
                    c = Rgba.Lerp(look.Face, Rgba.White, 0.25f + 0.25f * Draw.Clamp01(u));
                    var frost = Draw.Hash((int)(x * 2.2f), (int)(y * 2.2f));
                    c = frost > 0.93f ? Rgba.Lerp(c, Rgba.White, 0.8f) : c;
                    break;
                }

                case 12:
                    // Солнечные лучи-полосы поперёк багета.
                    c = look.Face.Shade(0.1f * MathF.Sin(s * 1.4f) - 0.03f);
                    break;
                case 18:
                {
                    // Море: белые гребни волн вдоль багета.
                    var crest = MathF.Abs(u - 0.38f * MathF.Sin(s * 0.9f)) - 0.13f;
                    c = Rgba.Lerp(look.Face.Shade(-0.08f + 0.12f * u), Rgba.White, 0.9f * Draw.Cover(crest * Band / 2f));
                    break;
                }

                case 26:
                {
                    // Космос: тёмное небо с мелкими звёздами.
                    var twinkle = Draw.Hash((int)(x * 1.4f), (int)(y * 1.4f));
                    c = twinkle > 0.975f ? Rgba.Lerp(look.Face, Rgba.White, 0.75f) : look.Face.Shade(-0.1f + 0.1f * u);
                    break;
                }
            }

            // Мелкие мотивы по середине багета, ближе к углам — место для крупных.
            var sn = MathF.Round(s / MotifStep) * MotifStep;
            if (MathF.Abs(sn) > 2f * MotifStep)
            {
                return c;
            }

            var dx = x - C;
            var dy = y - C;
            var vertical = MathF.Abs(dx) > MathF.Abs(dy);
            var mx = vertical ? C + MathF.Sign(dx) * Mid : C + sn;
            var my = vertical ? C + sn : C + MathF.Sign(dy) * Mid;
            return Motif(style, look, x, y, mx, my, 3f, c);
        }

        private static Rgba ThemeOrnaments(int style, Look look, float x, float y, Rgba result)
        {
            // Зима: снежная шапка по верхнему краю.
            if (style == 10 && y > C + Outer - 7.5f)
            {
                var wave = C + Outer - 4.4f - 1.6f * (0.5f + 0.5f * MathF.Cos(x * 0.8f));
                result = Shadow(result, MathF.Max(Draw.RoundBox(x, y, C, C, Outer, Outer, 10f), wave - 0.8f - y), 1.2f, 0.18f);
                var cap = MathF.Max(Draw.RoundBox(x, y, C, C, Outer + 0.6f, Outer + 0.6f, 10.5f), wave - y);
                result = Layer(result, cap, Rgba.Lerp(Rgba.Hex("#EAF6FF"), Rgba.White, Draw.Clamp01((y - wave) / 3f)));
            }

            foreach (var (bx, by) in Corners)
            {
                if (MathF.Abs(x - bx) > 7.5f || MathF.Abs(y - by) > 7.5f)
                {
                    continue;
                }

                result = Shadow(result, Draw.Circle(x, y, bx, by - 0.9f, 4.4f), 1.8f, 0.22f);
                result = Motif(style, look, x, y, bx, by, 4.8f, result);
            }

            return result;
        }

        /// <summary>Мотив темы размера r с центром (cx, cy) поверх result: свет сверху слева, тёмная кромка.</summary>
        private static Rgba Motif(int style, Look look, float x, float y, float cx, float cy, float r, Rgba result)
        {
            var dx = x - cx;
            var dy = y - cy;
            if (dx * dx + dy * dy > (r + 2f) * (r + 2f))
            {
                return result;
            }

            var lit = Draw.Clamp01(0.5f - 0.3f * dx / r + 0.4f * dy / r);
            switch (style)
            {
                case 11:
                case 15:
                    return Flower(result, look, dx, dy, r);
                case 14:
                    return Button(result, look, x, y, cx, cy, r * 0.85f);
                case 10:
                {
                    // Снежинка: три перекрещенные палочки с веточками и серединкой.
                    var d = float.MaxValue;
                    for (var i = 0; i < 3; i++)
                    {
                        var a = i * MathF.PI / 3f + MathF.PI / 2f;
                        var ex = MathF.Cos(a) * r;
                        var ey = MathF.Sin(a) * r;
                        d = MathF.Min(d, Draw.Segment(x, y, cx - ex, cy - ey, cx + ex, cy + ey));
                        for (var sd = -1; sd <= 1; sd += 2)
                        {
                            var side = (float)sd;
                            var bx = cx + side * ex * 0.6f;
                            var by = cy + side * ey * 0.6f;
                            var b1 = a + 0.6f;
                            var b2 = a - 0.6f;
                            d = MathF.Min(d, Draw.Segment(x, y, bx, by, bx + side * MathF.Cos(b1) * r * 0.35f, by + side * MathF.Sin(b1) * r * 0.35f));
                            d = MathF.Min(d, Draw.Segment(x, y, bx, by, bx + side * MathF.Cos(b2) * r * 0.35f, by + side * MathF.Sin(b2) * r * 0.35f));
                        }
                    }

                    d -= r * 0.11f;
                    result = Layer(result, d - 0.5f, look.Edge.WithAlpha(0.5f));
                    return Layer(result, d, Rgba.Lerp(Rgba.Hex("#DDF2FF"), Rgba.White, lit));
                }

                case 12:
                {
                    // Подсолнух: двенадцать жёлтых лепестков, коричневая серединка с семечками.
                    var dist = MathF.Sqrt(dx * dx + dy * dy);
                    var angle = MathF.Atan2(dy, dx);
                    var petals = dist - r * (0.7f + 0.3f * MathF.Abs(MathF.Cos(6f * angle)));
                    var petal = look.Accent.Shade(-0.2f + 0.3f * lit);
                    petal = Rgba.Lerp(petal, look.Edge, 0.7f * Draw.SmoothStep(-0.7f, -0.2f, petals));
                    result = Layer(result, petals, petal);
                    var core = dist - r * 0.45f;
                    var seeds = 0.5f + 0.5f * MathF.Sin(dx * 3.2f) * MathF.Sin(dy * 3.2f);
                    return Layer(result, core, Rgba.Hex("#6B3A12").Shade(-0.15f + 0.25f * seeds + 0.2f * lit));
                }

                case 13:
                {
                    // Лист: овал с прожилкой; цвет — от жёлтого до красного, у каждого свой.
                    var tone = Draw.Hash((int)(cx * 3f), (int)(cy * 3f));
                    var color = tone < 0.33f ? Rgba.Hex("#E8331F") : tone < 0.66f ? Rgba.Hex("#F2A33A") : Rgba.Hex("#FFC93C");
                    var leaf = Draw.Ellipse(x, y, cx, cy, r, r * 0.52f, 35f + 60f * tone);
                    var c = color.Shade(-0.25f + 0.35f * lit);
                    c = Rgba.Lerp(c, look.Edge, 0.7f * Draw.SmoothStep(-0.7f, -0.2f, leaf));
                    result = Layer(result, leaf, c);
                    var a = (35f + 60f * tone) * MathF.PI / 180f;
                    var vein = Draw.Segment(x, y, cx - MathF.Cos(a) * r * 0.8f, cy - MathF.Sin(a) * r * 0.8f, cx + MathF.Cos(a) * r * 0.8f, cy + MathF.Sin(a) * r * 0.8f) - 0.18f;
                    return Layer(result, vein, color.Shade(-0.45f), Draw.Clamp01(-leaf));
                }

                case 16:
                {
                    // Лапка: подушечка и три пальчика.
                    var pad = Draw.Ellipse(x, y, cx, cy - r * 0.25f, r * 0.55f, r * 0.42f);
                    var toes = MathF.Min(Draw.Circle(x, y, cx - r * 0.55f, cy + r * 0.3f, r * 0.24f),
                        MathF.Min(Draw.Circle(x, y, cx, cy + r * 0.55f, r * 0.24f), Draw.Circle(x, y, cx + r * 0.55f, cy + r * 0.3f, r * 0.24f)));
                    return Solid(result, MathF.Min(pad, toes), look.Accent, look.Edge, lit);
                }

                case 17:
                {
                    // Вишенки: две ягоды на веточках.
                    var stem = MathF.Min(Draw.Segment(x, y, cx - r * 0.35f, cy - r * 0.15f, cx + r * 0.1f, cy + r * 0.8f),
                        Draw.Segment(x, y, cx + r * 0.4f, cy - r * 0.1f, cx + r * 0.1f, cy + r * 0.8f)) - 0.22f;
                    result = Layer(result, stem, Rgba.Hex("#3E8A3A"));
                    var berries = MathF.Min(Draw.Circle(x, y, cx - r * 0.4f, cy - r * 0.35f, r * 0.42f), Draw.Circle(x, y, cx + r * 0.42f, cy - r * 0.3f, r * 0.42f));
                    result = Solid(result, berries, look.Accent, Rgba.Hex("#8A1420"), lit);
                    return Layer(result, Draw.Circle(x, y, cx - r * 0.55f, cy - r * 0.2f, r * 0.12f), Rgba.White.WithAlpha(0.8f));
                }

                case 18:
                {
                    // Ракушка-пузырёк: кольцо с бликом.
                    var ring = MathF.Abs(Draw.Circle(x, y, cx, cy, r * 0.7f)) - r * 0.17f;
                    result = Layer(result, ring, Rgba.Lerp(Rgba.Hex("#CFEFFF"), Rgba.White, lit));
                    return Layer(result, Draw.Circle(x, y, cx - r * 0.3f, cy + r * 0.3f, r * 0.15f), Rgba.White);
                }

                case 19:
                {
                    // Облачко.
                    var cloud = MathF.Min(Draw.Circle(x, y, cx, cy + r * 0.15f, r * 0.5f),
                        MathF.Min(Draw.Circle(x, y, cx - r * 0.5f, cy - r * 0.1f, r * 0.36f), Draw.Circle(x, y, cx + r * 0.5f, cy - r * 0.1f, r * 0.38f)));
                    cloud = MathF.Min(cloud, Draw.RoundBox(x, y, cx, cy - r * 0.25f, r * 0.7f, r * 0.2f, r * 0.2f));
                    return Solid(result, cloud, look.Accent, look.Edge, lit, 0.4f);
                }

                case 20:
                {
                    // Ёлочка: два яруса и ствол.
                    var trunk = Draw.RoundBox(x, y, cx, cy - r * 0.8f, r * 0.14f, r * 0.2f, 0.2f);
                    result = Layer(result, trunk, Rgba.Hex("#6B4428"));
                    var tree = MathF.Min(Draw.Triangle(x, y, cx - r * 0.75f, cy - r * 0.6f, cx + r * 0.75f, cy - r * 0.6f, cx, cy + r * 0.2f),
                        Draw.Triangle(x, y, cx - r * 0.55f, cy - r * 0.05f, cx + r * 0.55f, cy - r * 0.05f, cx, cy + r * 0.9f));
                    return Solid(result, tree, look.Accent, look.Edge, lit);
                }

                case 21:
                    return Solid(result, Star(x, y, cx, cy, r * 0.95f, 0.48f), look.Accent, Rgba.Hex("#B77A00"), lit);
                case 22:
                {
                    // Домик: стены, крыша и окошко.
                    var walls = Draw.RoundBox(x, y, cx, cy - r * 0.3f, r * 0.55f, r * 0.45f, 0.3f);
                    result = Solid(result, walls, look.Accent, look.Edge, lit);
                    var roof = Draw.Triangle(x, y, cx - r * 0.8f, cy + r * 0.12f, cx + r * 0.8f, cy + r * 0.12f, cx, cy + r * 0.85f);
                    result = Solid(result, roof, Rgba.Hex("#C9503A"), Rgba.Hex("#6E2216"), lit);
                    return Layer(result, Draw.RoundBox(x, y, cx, cy - r * 0.3f, r * 0.15f, r * 0.15f, 0.1f), Rgba.Hex("#FFD34D"));
                }

                case 23:
                    return Solid(result, Heart(x, y, cx, cy, r * 0.75f), look.Accent, look.Edge, lit);
                case 24:
                {
                    // Две скрещённые спицы с шариками на концах.
                    var a = r * 0.75f;
                    var needles = MathF.Min(Draw.Segment(x, y, cx - a, cy - a, cx + a, cy + a), Draw.Segment(x, y, cx - a, cy + a, cx + a, cy - a)) - r * 0.11f;
                    result = Solid(result, needles, Rgba.Hex("#D9DEE6"), look.Edge, lit);
                    var knobs = MathF.Min(Draw.Circle(x, y, cx + a, cy + a, r * 0.2f), Draw.Circle(x, y, cx - a, cy + a, r * 0.2f));
                    return Solid(result, knobs, look.Accent, Rgba.Hex("#8A5A00"), lit);
                }

                case 25:
                {
                    // Компас: ромб, половинки разного цвета.
                    var rhomb = (MathF.Abs(dx) * 1.6f + MathF.Abs(dy) - r) / 1.9f;
                    var half = dy > 0f ? Rgba.Hex("#E8333A") : Rgba.Hex("#F4F6F8");
                    result = Solid(result, rhomb, half, look.Edge, lit);
                    return Layer(result, Draw.Circle(x, y, cx, cy, r * 0.13f), look.Accent);
                }

                default:
                {
                    // Планета с кольцом.
                    var ring = MathF.Abs(Draw.Ellipse(x, y, cx, cy, r, r * 0.32f, -20f)) - r * 0.07f;
                    var back = ring;
                    var planet = Draw.Circle(x, y, cx, cy, r * 0.55f);
                    var front = dy < -0.1f * dx ? ring : float.MaxValue;
                    result = Layer(result, back, look.Accent.Shade(0.3f));
                    result = Solid(result, planet, look.Accent, Rgba.Hex("#4B2A8A"), lit);
                    return Layer(result, front, look.Accent.Shade(0.4f));
                }
            }
        }

        /// <summary>Объёмная заливка формы d: свет lit, тёмная кромка edge.</summary>
        private static Rgba Solid(Rgba result, float d, Rgba color, Rgba edge, float lit, float rim = 0.7f)
        {
            if (d > 1f)
            {
                return result;
            }

            var c = color.Shade(-0.22f + 0.32f * lit);
            c = Rgba.Lerp(c, edge, rim * Draw.SmoothStep(-0.7f, -0.2f, d));
            return Layer(result, d, c);
        }
    }
}
