#nullable enable

using System;

namespace KnittedPicture.Game
{
    /// <summary>
    /// Живые рамки (эпические и легендарные, анимация — FrameFx): подробный рисунок самой рамки и спрайты для эффектов.
    /// Светлячки — тёмный бархат с вышитой лозой и банками-фонариками на углах; Северное сияние — ночное небо, ели
    /// понизу и ледяные кристаллы; Камин — кирпичная кладка, деревянная полка сверху со свечами; Жар-птица — хохлома:
    /// чёрный лак, золотая «травка» с завитками и красными ягодами, перья жар-птицы на углах; Волшебный клубок —
    /// сливовая вязка «косичками», радужный кручёный шнур снаружи и клубки со спицами на углах.
    /// </summary>
    public static partial class FrameRaster
    {
        /// <summary>Первая живая рамка: дальше рисунок — здесь.</summary>
        public const int FirstMagic = 27;

        private static Rgba MagicPattern(int style, Look look, float x, float y, float s, float depth, float u)
        {
            var across = u * Band / 2f;
            switch (style)
            {
                case 27:
                {
                    // Бархат: мягкий ворс, к внешнему краю темнее; по середине — вышитая лоза с листьями.
                    var c = look.Face.Shade(0.06f * (Draw.Noise(x * 1.7f, y * 1.7f) - 0.5f) - 0.1f * Draw.Clamp01(u));
                    var wave = 1.3f * MathF.Sin(s * 0.55f);
                    var stem = MathF.Abs(across - wave) - 0.35f;
                    c = Rgba.Lerp(c, look.Accent.Shade(-0.25f), Draw.Cover(stem));
                    var sn = MathF.Round(s / 5.7f) * 5.7f;
                    var side = ((int)MathF.Round(s / 5.7f) & 1) == 0 ? 1f : -1f;
                    var ly = 1.3f * MathF.Sin(sn * 0.55f) + side * 1.1f;
                    var leaf = Draw.Ellipse(s, across, sn + 0.6f, ly, 1.5f, 0.75f, side * 35f);
                    if (leaf < 1f)
                    {
                        var vein = MathF.Abs((s - sn - 0.6f) * MathF.Sin(side * 35f * MathF.PI / 180f) - (across - ly) * MathF.Cos(side * 35f * MathF.PI / 180f));
                        var leafColor = look.Accent.Shade(-0.1f + 0.15f * (1f - Draw.Clamp01(vein / 0.6f)));
                        c = Rgba.Lerp(c, leafColor, Draw.Cover(leaf));
                    }

                    // Стежки вышивки: мелкая штриховка по лозе.
                    var stitch = 0.5f + 0.5f * MathF.Sin((s + across) * 4f);
                    return stem < 0.2f ? c.Shade(0.06f * stitch) : c;
                }

                case 28:
                {
                    // Ночь: сверху глубже, к низу светлее у горизонта; мелкие звёзды; понизу — ряд елей.
                    var c = Rgba.Lerp(look.Face.Shade(0.12f), look.Face.Shade(-0.12f), Draw.Clamp01(y / Size));
                    var starCell = Draw.Hash((int)(x * 1.6f), (int)(y * 1.6f));
                    if (starCell > 0.985f)
                    {
                        c = Rgba.Lerp(c, Rgba.Hex("#E9FBFF"), 0.4f + 0.5f * Draw.Hash((int)(x * 3f), (int)(y * 5f)));
                    }

                    // Отсвет сияния, запечённый в верхнюю планку (живая часть — FrameFx).
                    if (y > C)
                    {
                        var glow = Draw.Clamp01(u * 0.5f + 0.5f) * (0.5f + 0.5f * MathF.Sin(x * 0.7f + MathF.Sin(x * 0.23f) * 2f));
                        c = Rgba.Lerp(c, look.Accent.Shade(-0.3f), 0.25f * glow);
                    }

                    // Ели вдоль нижней планки и низа боковых: от внешнего края к проёму.
                    if (y < C + 8f)
                    {
                        var tn = MathF.Round(s / 4.2f) * 4.2f;
                        var h = 4.2f + 1.6f * Draw.Hash((int)tn + 40, 3);
                        var baseY = Band / 2f + 0.3f;
                        var apex = baseY - h;
                        var t = Draw.Clamp01((baseY - across) / h);
                        var halfW = 1.9f * (1f - t) * (0.85f + 0.15f * MathF.Sin(t * 18f));
                        var tree = MathF.Max(MathF.Abs(s - tn) - halfW, MathF.Max(apex - across, across - baseY));
                        c = Rgba.Lerp(c, Rgba.Hex("#06102A"), Draw.Cover(tree));
                        var snow = MathF.Max(tree, MathF.Abs(across - (apex + h * 0.45f)) - 0.3f);
                        c = Rgba.Lerp(c, Rgba.Hex("#CFE6FF"), 0.7f * Draw.Cover(snow));
                    }

                    return c;
                }

                case 29:
                {
                    // Кирпичная кладка: два ряда поперёк багета, кирпичи вдоль, со сдвигом; швы раствора светлые.
                    var row = across < 0f ? 0 : 1;
                    var rowCentre = row == 0 ? -Band / 4f : Band / 4f;
                    var offset = row == 0 ? 0f : 2.6f;
                    var bn = MathF.Floor((s + offset) / 5.2f);
                    var bs = (s + offset) - (bn + 0.5f) * 5.2f;
                    var brick = Draw.RoundBox(bs, across, 0f, rowCentre, 2.25f, Band / 4f - 0.45f, 0.6f);
                    var tint = Draw.Hash((int)bn + row * 31, row + 7);
                    var brickColor = Rgba.Lerp(look.Face, Rgba.Hex("#C25E48"), tint).Shade(0.08f * (Draw.Noise(x * 2.4f, y * 2.4f) - 0.5f));
                    brickColor = brickColor.Shade(0.1f * Draw.Clamp01(-brick / 1.2f) - 0.05f);

                    // Копоть: к проёму внизу темнее (над огнём).
                    var soot = y < C ? 0.18f * Draw.Clamp01(-u) * Draw.Clamp01((C - y) / Inner) : 0f;
                    var mortar = look.Accent.Shade(-0.08f * Draw.Noise(x * 3f, y * 3f));
                    return Rgba.Lerp(mortar, brickColor, Draw.Cover(brick)).Shade(-soot);
                }

                case 30:
                {
                    // Хохлома: чёрный лак с бликом, по нему золотая «травка» — волна-стебель, завитки, листья
                    // и красные ягоды. По краям — тонкие золотые канты.
                    var c = look.Face.Shade(0.1f * MathF.Pow(Draw.Clamp01(1f - MathF.Abs(u - 0.2f)), 3f));
                    var wave = 1.2f * MathF.Sin(s * 0.62f);
                    var stem = MathF.Abs(across - wave) - 0.32f;
                    var gold = GoldAt(look, x, y);
                    c = Rgba.Lerp(c, gold, Draw.Cover(stem));
                    var k = MathF.Round(s * 0.62f / MathF.PI);
                    var peak = k * MathF.PI / 0.62f;
                    var dirAcross = (k % 2 == 0) ? 1f : -1f;

                    // Завиток над гребнем волны: кольцо, открытое с одной стороны.
                    var curlX = peak + 0.8f;
                    var curlY = 1.2f * dirAcross + dirAcross * 0.9f;
                    var ring = MathF.Abs(Draw.Circle(s, across, curlX, curlY, 1.05f)) - 0.26f;
                    var open = Draw.Circle(s, across, curlX + 0.8f, curlY - dirAcross * 0.4f, 0.7f);
                    c = Rgba.Lerp(c, gold, Draw.Cover(MathF.Max(ring, -open)));

                    // Листики у впадин и ягоды-гроздья между.
                    var leaf = Draw.Ellipse(s, across, peak - 1.4f, -dirAcross * 0.9f, 0.95f, 0.42f, dirAcross * 50f);
                    c = Rgba.Lerp(c, gold.Shade(-0.1f), Draw.Cover(leaf));
                    // Всё внутри своей полуволны (±2.5 от гребня), иначе соседняя обрежет рисунок.
                    var bx = peak + 0.6f;
                    var by = -dirAcross * 1.75f;
                    for (var i = 0; i < 3; i++)
                    {
                        var ox = bx + (i == 0 ? -0.5f : 0.55f);
                        var oy = by + (i == 0 ? 0f : i == 1 ? 0.85f : -0.85f) * dirAcross;
                        var berry = Draw.Circle(s, across, ox, oy, 0.82f);
                        if (berry < 1f)
                        {
                            var lit = Draw.Clamp01(0.5f - 0.6f * (s - ox) + 0.6f * (across - oy));
                            c = Rgba.Lerp(c, Rgba.Hex("#D7262E").Shade(-0.3f + 0.45f * lit), Draw.Cover(berry));
                            c = Rgba.Lerp(c, Rgba.White, 0.7f * Draw.Cover(Draw.Circle(s, across, ox - 0.2f, oy + 0.2f, 0.16f)));
                        }
                    }

                    var piping = MathF.Min(MathF.Abs(u - 0.86f), MathF.Abs(u + 0.86f)) * Band / 2f - 0.28f;
                    return Rgba.Lerp(c, gold, Draw.Cover(piping));
                }

                default:
                {
                    // Вязка «косичками» (V-петли) сливового цвета; снаружи — кручёный радужный шнур.
                    var col = MathF.Floor(s / 1.6f);
                    var fx = s / 1.6f - col - 0.5f;
                    var fy = across / 1.4f;
                    var vx = MathF.Abs(fx) - 0.5f * ((fy - MathF.Floor(fy)) - 0.5f);
                    var loop = 0.5f + 0.5f * MathF.Cos(vx * MathF.PI * 2f);
                    var c = look.Face.Shade(-0.12f + 0.2f * loop - 0.05f * Draw.Clamp01(-u));
                    if (u > 0.3f)
                    {
                        var t = (u - 0.3f) / 0.7f;
                        var twist = (s * 0.9f + t * 2.4f) / 1.7f;
                        var ply = twist - MathF.Floor(twist);
                        var hue = (Perimeter(x, y) + MathF.Floor(twist) * 0.004f) % 1f;
                        var cord = Rainbow(hue).Shade(-0.25f + 0.4f * MathF.Sin(ply * MathF.PI));
                        var edgeShade = MathF.Sin(Draw.Clamp01(t) * MathF.PI);
                        cord = cord.Shade(-0.25f * (1f - edgeShade));
                        c = Rgba.Lerp(c, cord, Draw.SmoothStep(0.25f, 0.4f, u));
                    }

                    // Золотой бисер вдоль проёма.
                    if (u < -0.55f)
                    {
                        var bn = MathF.Round(s / 1.8f) * 1.8f;
                        var bead = MathF.Sqrt((s - bn) * (s - bn) + (across + Band / 2f - 0.95f) * (across + Band / 2f - 0.95f)) - 0.6f;
                        c = Rgba.Lerp(c, look.Accent.Shade(0.1f - 0.3f * Draw.Clamp01((s - bn + 0.3f) / 0.9f)), Draw.Cover(bead));
                    }

                    return c;
                }
            }
        }

        private static Rgba MagicOrnaments(int style, Look look, float x, float y, Rgba result)
        {
            switch (style)
            {
                case 27:
                    foreach (var (bx, by) in Corners)
                    {
                        result = Jar(result, x, y, bx, by);
                    }

                    return result;

                case 28:
                    foreach (var (bx, by) in Corners)
                    {
                        result = Crystal(result, x, y, bx, by, 5.4f);
                    }

                    return result;

                case 29:
                    return Mantel(result, look, x, y);

                case 30:
                    foreach (var (bx, by) in Corners)
                    {
                        result = Feather(result, look, x, y, bx, by);
                    }

                    return result;

                default:
                {
                    var colors = new[] { "#FF6FA8", "#33C6B8", "#FFC531", "#9B6BFF" };
                    for (var i = 0; i < Corners.Length; i++)
                    {
                        var (bx, by) = Corners[i];
                        bx -= MathF.Sign(bx - C) * 0.9f;
                        by -= MathF.Sign(by - C) * 0.9f;
                        result = Ball(result, x, y, bx, by, Rgba.Hex(colors[i]), i);
                    }

                    return result;
                }
            }
        }

        /// <summary>Доля пути по периметру рамки (0..1, по часовой от верхнего левого угла) — для радуги по кругу.</summary>
        private static float Perimeter(float x, float y)
        {
            var a = MathF.Atan2(-(y - C), x - C);
            return (a / (2f * MathF.PI) + 1.125f) % 1f;
        }

        private static Rgba Rainbow(float h)
        {
            h = (h % 1f + 1f) % 1f * 6f;
            var i = (int)h;
            var f = h - i;
            var (r, g, b) = i switch
            {
                0 => (1f, f, 0f),
                1 => (1f - f, 1f, 0f),
                2 => (0f, 1f, f),
                3 => (0f, 1f - f, 1f),
                4 => (f, 0f, 1f),
                _ => (1f, 0f, 1f - f),
            };
            return Rgba.Lerp(new Rgba(r, g, b), Rgba.White, 0.18f);
        }

        /// <summary>Золото с переливом: светлее сверху слева, с бликами по диагонали.</summary>
        private static Rgba GoldAt(Look look, float x, float y)
        {
            var t = 0.5f + 0.5f * MathF.Sin((x - y) * 0.35f);
            return Rgba.Lerp(Rgba.Hex("#C8901E"), look.Accent, 0.4f + 0.6f * t);
        }

        /// <summary>Стеклянная банка-фонарик со светлячками: крышка, тёплый свет внутри, блик на стекле.</summary>
        private static Rgba Jar(Rgba result, float x, float y, float bx, float by)
        {
            if (MathF.Abs(x - bx) > 7f || MathF.Abs(y - by) > 7.5f)
            {
                return result;
            }

            var body = Draw.RoundBox(x, y, bx, by - 0.6f, 4f, 4.6f, 1.8f);
            result = Shadow(result, Draw.RoundBox(x, y + 0.9f, bx, by - 0.6f, 4f, 4.6f, 1.8f), 1.6f, 0.35f);
            if (body < 1f)
            {
                var r = MathF.Sqrt((x - bx) * (x - bx) + (y - by + 1f) * (y - by + 1f));
                var light = Draw.Clamp01(1f - r / 4.6f);
                var glass = Rgba.Lerp(Rgba.Hex("#3E6B4A"), Rgba.Hex("#FFF2A0"), 0.25f + 0.75f * light * light);
                glass = Rgba.Lerp(glass, Rgba.Hex("#DDF3E4"), 0.6f * Draw.SmoothStep(-0.9f, -0.2f, body));
                result = Layer(result, body, glass);

                // Пара светлячков внутри и блик.
                result = Layer(result, Draw.Circle(x, y, bx - 1.2f, by - 1.6f, 0.55f), Rgba.Hex("#FFFBD6"));
                result = Layer(result, Draw.Circle(x, y, bx + 1.3f, by + 0.4f, 0.45f), Rgba.Hex("#FFFBD6"));
                result = Layer(result, Draw.RoundBox(x, y, bx - 2.6f, by - 0.4f, 0.4f, 2.6f, 0.4f), Rgba.White.WithAlpha(0.65f));
            }

            // Крышка с бечёвкой.
            var lid = Draw.RoundBox(x, y, bx, by + 4.4f, 3.4f, 0.95f, 0.5f);
            var lidLit = Draw.Clamp01(0.5f + 0.4f * (y - by - 4.4f));
            result = Solid(result, lid, Rgba.Hex("#9A6A3A").Shade(0.1f * lidLit), Rgba.Hex("#4A2E14"), lidLit);
            return Layer(result, Draw.RoundBox(x, y, bx, by + 3.3f, 3.6f, 0.3f, 0.2f), Rgba.Hex("#D9C49A"));
        }

        /// <summary>Ледяной кристалл-звезда: восемь лучей, грани светлые и тёмные, искра в середине.</summary>
        private static Rgba Crystal(Rgba result, float x, float y, float bx, float by, float r)
        {
            var dx = x - bx;
            var dy = y - by;
            var dist = MathF.Sqrt(dx * dx + dy * dy);
            if (dist > r + 2f)
            {
                return result;
            }

            var a = MathF.Atan2(dy, dx);
            var spikes = MathF.Pow(MathF.Abs(MathF.Cos(2f * a)), 6f);
            var minor = MathF.Pow(MathF.Abs(MathF.Cos(2f * a + MathF.PI / 2f)), 6f);
            var edge = r * (0.32f + 0.68f * spikes + 0.3f * minor);
            var d = dist - edge;
            result = Shadow(result, d + (dy < 0f ? 0f : 0.8f), 1.4f, 0.3f);
            var facet = MathF.Sin(a * 4f) > 0f ? 0.18f : -0.12f;
            var ice = Rgba.Lerp(Rgba.Hex("#7FD8F0"), Rgba.Hex("#F2FFFF"), Draw.Clamp01(0.55f + facet + 0.3f * (1f - dist / r)));
            ice = Rgba.Lerp(ice, Rgba.Hex("#2E7FA8"), 0.7f * Draw.SmoothStep(-0.6f, -0.15f, d));
            result = Layer(result, d, ice);
            return Layer(result, dist - 0.9f, Rgba.White);
        }

        /// <summary>Каминная полка поверх верхней планки: брус с волокнами и две свечи по краям.</summary>
        private static Rgba Mantel(Rgba result, Look look, float x, float y)
        {
            var top = C + Outer;
            var shelf = Draw.RoundBox(x, y, C, top - 3.2f, Outer + 0.2f, 3.2f, 1.6f);
            result = Shadow(result, Draw.RoundBox(x, y + 1.4f, C, top - 3.2f, Outer, 3.2f, 1.6f), 2f, 0.35f);
            if (shelf < 1f)
            {
                var grain = 0.5f + 0.5f * MathF.Sin(y * 2.6f + Draw.Noise(x * 0.2f, y) * 3f);
                var wood = Rgba.Lerp(Rgba.Hex("#7A4520"), Rgba.Hex("#A8693A"), 0.4f + 0.4f * grain);
                wood = wood.Shade(0.12f * Draw.Clamp01((y - (top - 3.2f)) / 3.2f));
                wood = Rgba.Lerp(wood, Rgba.Hex("#3A1E0C"), 0.75f * Draw.SmoothStep(-0.9f, -0.25f, shelf));
                result = Layer(result, shelf, wood);
            }

            // Свечи стоят на полке (огонёк — живой, в FrameFx), между ними — веточка остролиста.
            foreach (var cx in new[] { C - Mid, C + Mid })
            {
                var candle = Draw.RoundBox(x, y, cx, top - 9f, 1.4f, 2.8f, 0.6f);
                var lit = Draw.Clamp01(0.5f - 0.4f * (x - cx));
                result = Solid(result, candle, Rgba.Hex("#FFF4DC"), Rgba.Hex("#B89A6A"), lit, 0.5f);
                result = Layer(result, Draw.Segment(x, y, cx, top - 6.2f, cx, top - 5.2f) - 0.18f, Rgba.Hex("#3A2A1A"));
            }

            foreach (var (lx, ang) in new[] { (C - 2.2f, 25f), (C + 2.2f, -25f) })
            {
                result = Layer(result, Draw.Ellipse(x, y, lx, top - 2.6f, 2.2f, 0.9f, ang), Rgba.Hex("#2F7A3A"));
            }

            result = Layer(result, Draw.Circle(x, y, C - 0.6f, top - 2.2f, 0.75f), Rgba.Hex("#D7262E"));
            return Layer(result, Draw.Circle(x, y, C + 0.6f, top - 2.9f, 0.75f), Rgba.Hex("#E8333A"));
        }

        /// <summary>Перо жар-птицы на углу: капля, направленная наружу, — золото, огонь и тёмный «глазок».</summary>
        private static Rgba Feather(Rgba result, Look look, float x, float y, float bx, float by)
        {
            var dx = x - bx;
            var dy = y - by;
            if (MathF.Abs(dx) > 10f || MathF.Abs(dy) > 10f)
            {
                return result;
            }

            // Ось пера — диагональ наружу; координаты вдоль (a) и поперёк (b).
            var ox = MathF.Sign(bx - C) * 0.7071f;
            var oy = MathF.Sign(by - C) * 0.7071f;
            var a = dx * ox + dy * oy;
            var b = -dx * oy + dy * ox;
            // Капля: круглая голова с «глазком» у угла и сужение к острию внутрь багета. Целиком внутри растра.
            var head = Draw.Circle(a, b, -0.6f, 0f, 3.3f);
            var body = Draw.Capsule(a, b, -0.6f, 0f, -8.6f, 0f, 2.6f, 0.15f, out _, out _);
            var shape = Draw.SMin(head, body, 1.6f);
            result = Shadow(result, shape + 0.6f, 1.5f, 0.35f);
            if (shape < 1f)
            {
                var rings = MathF.Sqrt((a + 0.6f) * (a + 0.6f) + b * b);
                var c = rings < 1.1f ? Rgba.Hex("#1E1240")
                    : rings < 1.9f ? Rgba.Hex("#2E8F8A")
                    : rings < 2.6f ? Rgba.Hex("#E8333A")
                    : Rgba.Hex("#FF8A1E");
                c = Rgba.Lerp(c, GoldAt(look, x, y), Draw.SmoothStep(-1f, -0.15f, shape));

                // Бородки пера: тонкие штрихи наискосок от стержня.
                var barb = 0.5f + 0.5f * MathF.Sin((a + MathF.Abs(b) * 0.9f) * 5.5f);
                c = c.Shade(0.1f * barb - 0.05f);
                result = Layer(result, shape, c);
                result = Layer(result, Draw.Circle(a, b, -1.1f, 0.5f, 0.35f), Rgba.White.WithAlpha(0.85f));
            }

            return result;
        }

        /// <summary>Клубок на углу: шар с витками нити, свет сверху слева, и две спицы крест-накрест.</summary>
        private static Rgba Ball(Rgba result, float x, float y, float bx, float by, Rgba color, int seed)
        {
            if (MathF.Abs(x - bx) > 8.5f || MathF.Abs(y - by) > 8.5f)
            {
                return result;
            }

            const float r = 4.7f;
            foreach (var (ax, ay, cx, cy) in new[] { (-5.2f, -5.2f, 5.2f, 5.2f), (-5.2f, 5.2f, 5.2f, -5.2f) })
            {
                var needle = Draw.Segment(x, y, bx + ax, by + ay, bx + cx, by + cy) - 0.4f;
                result = Shadow(result, needle - 0.3f, 1f, 0.2f);
                result = Layer(result, needle, Rgba.Hex("#D8B27A"));
                result = Layer(result, Draw.Circle(x, y, bx + cx, by + cy, 0.85f), Rgba.Hex("#FF6FA8"));
            }

            var d = Draw.Circle(x, y, bx, by, r);
            result = Shadow(result, Draw.Circle(x, y + 0.9f, bx, by, r), 1.6f, 0.35f);
            if (d < 1f)
            {
                var dx = (x - bx) / r;
                var dy = (y - by) / r;
                var lit = Draw.Clamp01(0.55f - 0.45f * dx + 0.5f * dy);
                var angle = 0.6f + seed * 0.7f;
                var along = dx * MathF.Cos(angle) + dy * MathF.Sin(angle);
                var thread = 0.5f + 0.5f * MathF.Sin(along * 13f + MathF.Sin(dx * 3f) * 1.4f);
                var c = color.Shade(-0.3f + 0.4f * lit + 0.1f * thread);
                c = Rgba.Lerp(c, color.Shade(-0.45f), 0.7f * Draw.SmoothStep(-0.8f, -0.2f, d));
                result = Layer(result, d, c);
            }

            return result;
        }

        // ---------- Спрайты эффектов ----------

        /// <summary>
        /// Частица эффекта рамки, 24 × 24: 0 — снежинка, 1 — лепесток, 2 — лист, 3 — перо-искра, 4 — сердечко, 5 — бабочка,
        /// 6 — лапка, 7 — пузырёк. Белые красятся в FrameFx.
        /// </summary>
        public static Raster Mote(int kind)
        {
            const int size = 24;
            const float c = size / 2f;
            return Raster.Render(size, size, 2, (x, y) =>
            {
                var dx = x - c;
                var dy = y - c;
                switch (kind)
                {
                    case 0:
                    {
                        // Шесть лучей с веточками.
                        var r = MathF.Sqrt(dx * dx + dy * dy);
                        var a = MathF.Atan2(dy, dx);
                        var sector = MathF.Round(a / (MathF.PI / 3f)) * (MathF.PI / 3f);
                        var along = r * MathF.Cos(a - sector);
                        var off = MathF.Abs(r * MathF.Sin(a - sector));
                        var arm = MathF.Max(off - 0.9f, along - 9.5f);
                        var twig = MathF.Abs(MathF.Abs(off) - (along - 5f) * 0.8f) - 0.7f;
                        twig = MathF.Max(twig, MathF.Max(4.6f - along, along - 8f));
                        var d = MathF.Min(MathF.Min(arm, twig), r - 2.2f);
                        return Rgba.White.WithAlpha(Draw.Cover(d));
                    }

                    case 1:
                    {
                        // Лепесток-капля с прожилкой.
                        var d = Draw.Ellipse(x, y, c, c, 9f, 5.2f, 30f);
                        var vein = MathF.Abs(Draw.Ellipse(x, y, c, c, 9f, 0.01f, 30f));
                        var col = Rgba.Lerp(Rgba.Hex("#FFC2DD"), Rgba.Hex("#FF7FB6"), Draw.Clamp01((dx + dy) / 18f + 0.5f));
                        col = Rgba.Lerp(col, Rgba.Hex("#FFFFFF"), 0.3f * Draw.Clamp01(1f - vein / 1.5f));
                        return col.WithAlpha(Draw.Cover(d));
                    }

                    case 2:
                    {
                        // Кленовый лист: пять долей, черешок и прожилки.
                        var r = MathF.Sqrt(dx * dx + (dy + 1f) * (dy + 1f));
                        var a = MathF.Atan2(dy + 1f, dx) - MathF.PI / 2f;
                        var lobes = 6.5f + 3.5f * MathF.Pow(MathF.Abs(MathF.Cos(a * 2.5f)), 2f) - 2.5f * MathF.Max(0f, -MathF.Cos(a));
                        var d = r - lobes;
                        var stalk = MathF.Max(MathF.Abs(dx) - 0.6f, MathF.Max(dy + 1f, -dy - 10f));
                        d = MathF.Min(d, stalk);
                        var vein = MathF.Abs(MathF.Sin(a * 2.5f)) * r;
                        var col = Rgba.White.Shade(-0.12f * Draw.Clamp01(1f - vein / 1.2f));
                        return col.WithAlpha(Draw.Cover(d));
                    }

                    case 4:
                    {
                        // Сердечко: белое (красится), объёмное — светлее сверху слева, блик.
                        var d = Heart(x, y, c, c + 1f, 9f);
                        var lit = Draw.Clamp01(0.55f - 0.04f * dx + 0.05f * dy);
                        var col = Rgba.White.Shade(-0.28f + 0.3f * lit);
                        col = Rgba.Lerp(col, Rgba.White, 0.9f * Draw.Cover(Draw.Ellipse(x, y, c - 4f, c + 4.5f, 2.2f, 1.2f, 35f)));
                        return col.WithAlpha(Draw.Cover(d));
                    }

                    case 5:
                    {
                        // Бабочка (вид сверху): два верхних и два нижних крыла, тельце; узор на крыльях. Белая, красится;
                        // взмах — сжатие по X в FrameFx.
                        var ax = MathF.Abs(dx);
                        var upper = Draw.Ellipse(ax, y, 5.4f, c + 3.2f, 5.2f, 4.2f, -25f);
                        var lower = Draw.Ellipse(ax, y, 4.2f, c - 3.6f, 3.6f, 3f, 30f);
                        var wings = MathF.Min(upper, lower);
                        var body = Draw.Ellipse(x, y, c, c, 0.9f, 6.5f, 0f);
                        var spot = Draw.Circle(ax, y, 6.2f, c + 3.6f, 1.5f);
                        var col = Rgba.White.Shade(-0.18f * Draw.Clamp01(ax / 10f));
                        col = Rgba.Lerp(col, Rgba.White.Shade(-0.45f), 0.8f * Draw.SmoothStep(-1.2f, -0.3f, wings));
                        col = Rgba.Lerp(col, Rgba.White.Shade(-0.5f), Draw.Cover(spot));
                        col = Rgba.Lerp(col, Rgba.Hex("#3A2A3A"), Draw.Cover(body));
                        return col.WithAlpha(Draw.Cover(MathF.Min(wings, body)));
                    }

                    case 6:
                    {
                        // Отпечаток лапки: подушечка и четыре пальчика. Белый, красится.
                        var pad = Draw.Ellipse(x, y, c, c - 3f, 5.2f, 4.2f, 0f);
                        var toes = MathF.Min(MathF.Min(Draw.Ellipse(x, y, c - 6.2f, c + 2.6f, 1.9f, 2.4f, 25f), Draw.Ellipse(x, y, c - 2.2f, c + 5.6f, 2f, 2.6f, 8f)),
                            MathF.Min(Draw.Ellipse(x, y, c + 2.2f, c + 5.6f, 2f, 2.6f, -8f), Draw.Ellipse(x, y, c + 6.2f, c + 2.6f, 1.9f, 2.4f, -25f)));
                        return Rgba.White.WithAlpha(Draw.Cover(MathF.Min(pad, toes)));
                    }

                    case 7:
                    {
                        // Пузырёк: тонкая светлая оболочка, к краю плотнее, блик сверху слева.
                        var r = MathF.Sqrt(dx * dx + dy * dy);
                        var shell = Draw.Clamp01((r - 6f) / 4f) * Draw.Cover(r - 10f);
                        var glint = Draw.Cover(Draw.Ellipse(x, y, c - 3.6f, c + 3.6f, 2.2f, 1.2f, 45f));
                        return Rgba.White.WithAlpha(MathF.Max(0.15f * Draw.Cover(r - 10f), 0.85f * shell) + glint * 0.9f);
                    }

                    default:
                    {
                        // Искра-пёрышко: вытянутая капля с ярким ядром.
                        var d = Draw.Ellipse(x, y, c, c, 10f, 3.6f, 90f);
                        var core = Draw.Clamp01(1f - MathF.Sqrt(dx * dx * 4f + dy * dy * 0.5f) / 8f);
                        return Rgba.Lerp(Rgba.Hex("#FFB040"), Rgba.Hex("#FFF6C8"), core).WithAlpha(Draw.Cover(d) * (0.4f + 0.6f * core));
                    }
                }
            });
        }

        /// <summary>
        /// Завеса северного сияния поверх рамки: вертикальные лучи в верхней планке и верху боковых, к низу гаснут.
        /// Белая, красится в FrameFx; variant — свой рисунок лучей (два слоя переливаются по очереди).
        /// </summary>
        public static Raster Aurora(int variant)
        {
            return Raster.Render(Size, Size, 2, (x, y) =>
            {
                var outer = Draw.RoundBox(x, y, C, C, Outer - 0.5f, Outer - 0.5f, 10f);
                var inner = Draw.RoundBox(x, y, C, C, Inner + 0.3f, Inner + 0.3f, 4.5f);
                if (outer > 0.5f || inner < -0.5f)
                {
                    return Rgba.Clear;
                }

                var phase = variant * 2.1f;
                var rays = 0.5f + 0.5f * MathF.Sin(x * 0.9f + phase + 2.2f * MathF.Sin(x * 0.17f + phase));
                rays = MathF.Pow(rays, 2.2f);
                var height = Draw.Clamp01((y - C + 6f) / (Outer + 6f));
                var curtain = MathF.Pow(height, 1.6f) * (0.35f + 0.65f * rays);
                var band = Draw.Cover(outer) * Draw.Clamp01(inner + 0.5f);
                return Rgba.White.WithAlpha(curtain * band);
            });
        }
    }
}
