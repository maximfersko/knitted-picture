#nullable enable

using System;

namespace KnittedPicture.Game
{
    /// <summary>
    /// Рамки альбома (AlbumFrames): у каждой свой узор и форма, а не только цвет. Багет — выпуклая «молдинга»
    /// со светом сверху слева и стыками на углах, внутри паспарту. Узоры: волокна дерева, плетение льна,
    /// горошек, клетка «виши», вязаная коса, пуговицы на углах, цветы с волнистым краем, сердечки,
    /// звёзды на ночном небе и золотой багет с розетками и камнями. Номер стиля = номер рамки.
    /// Растр квадратный (72 × 72 точки макета), картинка ставится в проём: см. Opening.
    /// </summary>
    public static class FrameRaster
    {
        public const int Size = 72;

        /// <summary>Сколько проёма (доля стороны) свободно под картинку: паспарту чуть шире, чем картинка.</summary>
        public const float Opening = 50f / Size;

        public const int Styles = 10;

        private const float C = Size / 2f;
        private const float Outer = 34f;
        private const float Band = 7.5f;
        private const float Inner = Outer - Band;
        private const float Mid = (Outer + Inner) / 2f;

        private static readonly (float X, float Y) Light = (-0.58f, 0.81f);

        private readonly struct Look
        {
            public Look(string face, string edge, string accent, string mat)
            {
                Face = Rgba.Hex(face);
                Edge = Rgba.Hex(edge);
                Accent = Rgba.Hex(accent);
                Mat = Rgba.Hex(mat);
            }

            public Rgba Face { get; }

            public Rgba Edge { get; }

            public Rgba Accent { get; }

            public Rgba Mat { get; }
        }

        private static readonly Look[] Looks =
        {
            new Look("#D9A066", "#8E5A2E", "#B07040", "#FFFFFF"), // Дерево
            new Look("#E8DCC2", "#A8946C", "#CBB994", "#FFFDF7"), // Лён
            new Look("#3D9BE9", "#1F5FA3", "#FFFFFF", "#FFFFFF"), // Горошек
            new Look("#FFFFFF", "#A42C31", "#E2474B", "#FFFFFF"), // Клетка
            new Look("#3E9E6A", "#22603F", "#74CC9A", "#FFFFFF"), // Косичка
            new Look("#2BA39A", "#186560", "#FFC93C", "#FFFFFF"), // Пуговки
            new Look("#F25C9F", "#B23272", "#FFFFFF", "#FFFFFF"), // Цветочки
            new Look("#B8323F", "#6E1520", "#FF9CB0", "#FFF8F4"), // Сердечки
            new Look("#22397A", "#101C44", "#FFD34D", "#FFFFFF"), // Звёздная ночь
            new Look("#E9B744", "#8A5C0C", "#FFF0B0", "#FFFDF3"), // Золото
        };

        public static Raster Frame(int style)
        {
            style = style < 0 || style >= Styles ? 0 : style;
            var look = Looks[style];
            return Raster.Render(Size, Size, 3, (x, y) => Shade(style, look, x, y));
        }

        private static Rgba Shade(int style, Look look, float x, float y)
        {
            var result = Rgba.Clear;
            var outer = OuterShape(style, x, y);
            result = Shadow(result, OuterShape(style, x, y + 1.6f), 3.2f, 0.28f);

            var inner = Draw.RoundBox(x, y, C, C, Inner, Inner, 4.5f);

            // Координаты вдоль багета: s — вдоль стороны, depth — от центра, u — поперёк (−1 у проёма, +1 снаружи).
            var dx = x - C;
            var dy = y - C;
            var vertical = MathF.Abs(dx) > MathF.Abs(dy);
            var s = vertical ? dy : dx;
            var depth = vertical ? MathF.Abs(dx) : MathF.Abs(dy);
            var u = Draw.Clamp((depth - Mid) / (Band / 2f), -1.3f, 1.3f);
            var nx = vertical ? MathF.Sign(dx) : 0f;
            var ny = vertical ? 0f : MathF.Sign(dy);
            var facing = nx * Light.X + ny * Light.Y;

            // Багет: узор, затем объём молдинги (скруглённый профиль и свет по стороне) и стык на углу.
            if (outer < 1f)
            {
                var band = Pattern(style, look, x, y, s, depth, u);
                var profile = MathF.Cos(Draw.Clamp(u, -1f, 1f) * MathF.PI / 2f);
                band = band.Shade(0.14f * (profile - 0.75f) + 0.2f * Draw.Clamp(u, -1f, 1f) * facing);
                if (style == 0 || style == 4 || style == 9)
                {
                    var seam = MathF.Abs(MathF.Abs(dx) - MathF.Abs(dy));
                    band = band.Shade(-0.28f * (1f - Draw.SmoothStep(0.15f, 0.6f, seam)));
                }

                // Кромки: снаружи и у проёма — тонкая тёмная линия, чтобы рамка читалась на любом фоне.
                band = Rgba.Lerp(band, look.Edge, 0.85f * Draw.SmoothStep(-1.3f, -0.4f, outer));
                band = Rgba.Lerp(band, look.Edge, 0.7f * (1f - Draw.SmoothStep(0.3f, 1.1f, inner)));
                result = Layer(result, outer, band);
            }

            // Паспарту и тень от багета на нём.
            if (inner < 0.5f)
            {
                var mat = look.Mat.Shade(-0.16f * Draw.SmoothStep(-2.4f, -0.3f, inner) * (0.6f + 0.4f * MathF.Max(0f, -dy / Inner + 0.4f)));
                result = Layer(result, inner, mat);
            }

            return Ornaments(style, look, x, y, result);
        }

        /// <summary>Контур рамки: у цветочной — волнистый край, у остальных — скруглённый квадрат.</summary>
        private static float OuterShape(int style, float x, float y)
        {
            if (style == 6)
            {
                var dx = x - C;
                var dy = y - C;
                var s = MathF.Abs(dx) > MathF.Abs(dy) ? dy : dx;
                var bump = 0.5f + 0.5f * MathF.Cos(2f * MathF.PI * s / 6f);
                return Draw.RoundBox(x, y, C, C, Outer - 1.6f, Outer - 1.6f, 9f) - 1.6f * bump;
            }

            return Draw.RoundBox(x, y, C, C, Outer, Outer, 10f);
        }

        private static Rgba Pattern(int style, Look look, float x, float y, float s, float depth, float u)
        {
            switch (style)
            {
                case 0:
                {
                    // Волокна дерева идут вдоль бруска, с сучками и неровностью.
                    var wobble = Draw.Noise(s * 0.18f + 7f, depth * 0.6f) * 3.2f;
                    var grain = 0.5f + 0.5f * MathF.Sin(depth * 2.4f + wobble);
                    var c = Rgba.Lerp(look.Face, look.Accent, 0.5f * MathF.Pow(grain, 3f));
                    return c.Shade(0.05f * (Draw.Noise(x * 0.9f, y * 0.9f) - 0.5f));
                }

                case 1:
                {
                    // Полотняное плетение: нитки поочерёдно сверху и снизу.
                    var a = MathF.Floor(x * 0.9f);
                    var b = MathF.Floor(y * 0.9f);
                    var over = ((int)(a + b) & 1) == 0;
                    var fx = x * 0.9f - a;
                    var fy = y * 0.9f - b;
                    var thread = over ? MathF.Sin(fy * MathF.PI) : MathF.Sin(fx * MathF.PI);
                    var c = Rgba.Lerp(look.Accent, look.Face, 0.45f + 0.55f * thread);
                    return c.Shade(0.06f * (Draw.Hash((int)(x * 3f), (int)(y * 3f)) - 0.5f));
                }

                case 2:
                {
                    // Белый горошек ровной сеткой, ряды со сдвигом.
                    var row = MathF.Floor(y / 4.6f);
                    var ox = ((int)row & 1) == 0 ? 0f : 2.3f;
                    var gx = (x + ox) % 4.6f - 2.3f;
                    var gy = y - (row + 0.5f) * 4.6f;
                    var dot = MathF.Sqrt(gx * gx + gy * gy) - 1.15f;
                    return Rgba.Lerp(look.Face, look.Accent, Draw.Cover(dot));
                }

                case 3:
                {
                    // Клетка «виши»: полосы в обе стороны, на пересечении — густой цвет.
                    var hx = Draw.SmoothStep(-0.25f, 0.25f, MathF.Sin(x * MathF.PI / 2.3f));
                    var hy = Draw.SmoothStep(-0.25f, 0.25f, MathF.Sin(y * MathF.PI / 2.3f));
                    var k = hx * hy > 0.5f ? 1f : 0.45f * (hx + hy);
                    return Rgba.Lerp(look.Face, look.Accent, k);
                }

                case 4:
                {
                    // Вязаная коса: две пряди перекрещиваются вдоль багета на «резинке».
                    var bg = look.Face.Shade(0.07f * MathF.Sin(s * MathF.PI / 1.1f) - 0.08f);
                    var phase = 2f * MathF.PI * s / 8f;
                    var c1 = 0.5f * MathF.Sin(phase);
                    var front = MathF.Cos(phase) > 0f;
                    var result = bg;
                    for (var pass = 0; pass < 2; pass++)
                    {
                        var first = pass == 0 ? !front : front;
                        var centre = first ? c1 : -c1;
                        var across = MathF.Abs(u - centre);
                        var d = (across - 0.42f) * Band / 2f;
                        var round = MathF.Sqrt(MathF.Max(0f, 1f - across * across / (0.42f * 0.42f)));
                        var strand = look.Accent.Shade(-0.3f + 0.35f * round + 0.08f * MathF.Sin(s * 3.2f + u * 4f));
                        result = Rgba.Lerp(result, look.Face.Shade(-0.35f), 0.5f * Draw.Cover(d - 0.6f) * (pass == 1 ? 1f : 0f));
                        result = Rgba.Lerp(result, strand, Draw.Cover(d));
                    }

                    return result;
                }

                case 5:
                {
                    // Платочная вязка: рубчики поперёк багета.
                    return look.Face.Shade(0.09f * MathF.Sin(depth * MathF.PI * 1.25f) - 0.02f);
                }

                case 6:
                {
                    // Цветочки по середине багета: пять лепестков, жёлтая серединка.
                    var c = look.Face.Shade(0.04f * MathF.Sin(x * 1.3f) * MathF.Sin(y * 1.3f));
                    var sn = MathF.Round(s / 9f) * 9f;
                    var fx = s - sn;
                    var fy = (depth - Mid);
                    return Flower(c, look, fx, fy, 2.6f);
                }

                case 7:
                {
                    // Мелкие сердечки сеткой.
                    var row = MathF.Floor(y / 6f);
                    var ox = ((int)row & 1) == 0 ? 0f : 3f;
                    var gx = (x + ox) % 6f - 3f;
                    var gy = y - (row + 0.5f) * 6f;
                    var heart = Heart(gx, gy, 0f, 0f, 1.25f);
                    return Rgba.Lerp(look.Face.Shade(0.05f * u), look.Accent, Draw.Cover(heart));
                }

                case 8:
                {
                    // Ночное небо: звёздочки с разбросом и мелкие точки.
                    var c = Rgba.Lerp(look.Face.Shade(-0.15f), look.Face.Shade(0.1f), (y / Size));
                    var cx = MathF.Floor(x / 6.5f);
                    var cy = MathF.Floor(y / 6.5f);
                    var jx = (cx + 0.3f + 0.4f * Draw.Hash((int)cx, (int)cy)) * 6.5f;
                    var jy = (cy + 0.3f + 0.4f * Draw.Hash((int)cy + 17, (int)cx + 5)) * 6.5f;
                    var size = 1.1f + 0.6f * Draw.Hash((int)cx + 3, (int)cy + 9);
                    var star = Star(x, y, jx, jy, size, 0.45f);
                    c = Rgba.Lerp(c, look.Accent, Draw.Cover(star));
                    var twinkle = Draw.Hash((int)(x * 1.4f), (int)(y * 1.4f));
                    return twinkle > 0.985f ? Rgba.Lerp(c, Rgba.White, 0.7f) : c;
                }

                default:
                {
                    // Золото: металл с витым жгутом снаружи и гладкой фаской у проёма.
                    var c = Rgba.Lerp(look.Edge, look.Face, 0.55f + 0.45f * MathF.Cos(u * 1.4f));
                    if (u > 0.05f)
                    {
                        var twist = (s + depth * 1.2f) / 2.4f;
                        var t = twist - MathF.Floor(twist);
                        c = c.Shade(0.28f * MathF.Sin(t * MathF.PI) - 0.14f);
                    }
                    else
                    {
                        c = Rgba.Lerp(c, look.Accent, 0.35f * (1f + u));
                    }

                    return c;
                }
            }
        }

        /// <summary>Украшения поверх багета: пуговицы, большие цветы, сердце, звёзды и розетки на углах.</summary>
        private static Rgba Ornaments(int style, Look look, float x, float y, Rgba result)
        {
            var k = Mid;
            switch (style)
            {
                case 5:
                    foreach (var (bx, by, r) in new[]
                             {
                                 (C - k, C - k, 5f), (C + k, C - k, 5f), (C - k, C + k, 5f), (C + k, C + k, 5f),
                                 (C, C + k, 3.4f), (C, C - k, 3.4f), (C - k, C, 3.4f), (C + k, C, 3.4f),
                             })
                    {
                        result = Button(result, look, x, y, bx, by, r);
                    }

                    return result;

                case 6:
                    foreach (var (bx, by) in new[] { (C - k, C - k), (C + k, C - k), (C - k, C + k), (C + k, C + k) })
                    {
                        result = Shadow(result, Draw.Circle(x, y, bx, by - 0.8f, 4.4f), 1.6f, 0.25f);
                        result = Flower(result, look, x - bx, y - by, 4.2f);
                    }

                    return result;

                case 7:
                {
                    var hx = C;
                    var hy = C + k;
                    result = Shadow(result, Heart(x, y, hx, hy - 1f, 5.5f), 1.6f, 0.3f);
                    var heart = Heart(x, y, hx, hy, 5.5f);
                    if (heart < 1f)
                    {
                        var lit = Draw.Clamp01(0.5f - 0.12f * (x - hx) + 0.12f * (y - hy));
                        var c = Rgba.Hex("#FF4F6E").Shade(-0.25f + 0.4f * lit);
                        c = Rgba.Lerp(c, look.Edge, 0.8f * Draw.SmoothStep(-1.2f, -0.35f, heart));
                        result = Layer(result, heart, c);
                        result = Layer(result, Draw.Ellipse(x, y, hx - 2.5f, hy + 1.9f, 1.4f, 0.8f, 35f), Rgba.White.WithAlpha(0.75f));
                    }

                    return result;
                }

                case 8:
                    foreach (var (bx, by) in new[] { (C - k, C - k), (C + k, C - k), (C - k, C + k), (C + k, C + k) })
                    {
                        result = Shadow(result, Star(x, y, bx, by - 0.8f, 5.6f, 0.5f), 1.5f, 0.3f);
                        var star = Star(x, y, bx, by, 5.6f, 0.5f);
                        if (star < 1f)
                        {
                            var lit = Draw.Clamp01(0.5f - 0.1f * (x - bx) + 0.12f * (y - by));
                            var c = look.Accent.Shade(-0.3f + 0.45f * lit);
                            c = Rgba.Lerp(c, Rgba.Hex("#B77A00"), 0.8f * Draw.SmoothStep(-1f, -0.3f, star));
                            result = Layer(result, star, c);
                        }
                    }

                    return result;

                case 9:
                {
                    // Бусины по кромке проёма.
                    var dx = x - C;
                    var dy = y - C;
                    var vertical = MathF.Abs(dx) > MathF.Abs(dy);
                    var s = vertical ? dy : dx;
                    var depth = vertical ? MathF.Abs(dx) : MathF.Abs(dy);
                    var sn = MathF.Round(s / 2.6f) * 2.6f;
                    var bead = MathF.Sqrt((s - sn) * (s - sn) + (depth - Inner - 1.1f) * (depth - Inner - 1.1f)) - 0.95f;
                    if (MathF.Abs(s) < Inner - 2f)
                    {
                        var lit = Draw.Clamp01(0.5f - 0.5f * (s - sn) * (vertical ? 0f : 1f) + 0.5f * (depth - Inner - 1.1f) * (vertical ? 0f : MathF.Sign(dy)));
                        result = Layer(result, bead, Rgba.Lerp(look.Edge, look.Accent, 0.3f + 0.7f * lit));
                    }

                    foreach (var (bx, by) in new[] { (C - k + 0.5f, C - k + 0.5f), (C + k - 0.5f, C - k + 0.5f), (C - k + 0.5f, C + k - 0.5f), (C + k - 0.5f, C + k - 0.5f) })
                    {
                        result = Rosette(result, look, x, y, bx, by);
                    }

                    return result;
                }

                default:
                    return result;
            }
        }

        private static Rgba Button(Rgba result, Look look, float x, float y, float bx, float by, float r)
        {
            result = Shadow(result, Draw.Circle(x, y, bx, by - 0.8f, r), 1.4f, 0.3f);
            var d = Draw.Circle(x, y, bx, by, r);
            if (d > 1f)
            {
                return result;
            }

            var lit = Draw.Clamp01(0.5f - 0.35f * (x - bx) / r + 0.45f * (y - by) / r);
            var c = look.Accent.Shade(-0.3f + 0.4f * lit);
            c = Rgba.Lerp(c, Rgba.Hex("#A8740A"), 0.8f * Draw.SmoothStep(-0.9f, -0.25f, d));
            result = Layer(result, d, c);
            // Ободок пуговицы и четыре дырочки.
            var dish = Draw.Circle(x, y, bx, by, r * 0.62f);
            result = Layer(result, MathF.Abs(dish) - 0.22f, look.Accent.Shade(-0.35f), 0.6f);
            var h = r * 0.24f;
            foreach (var (ox, oy) in new[] { (-h, -h), (h, -h), (-h, h), (h, h) })
            {
                result = Layer(result, Draw.Circle(x, y, bx + ox, by + oy, r * 0.11f + 0.2f), Rgba.Hex("#8A5A00"));
            }

            return result;
        }

        private static Rgba Flower(Rgba result, Look look, float fx, float fy, float r)
        {
            var dist = MathF.Sqrt(fx * fx + fy * fy);
            if (dist > r + 1f)
            {
                return result;
            }

            var angle = MathF.Atan2(fy, fx) + MathF.PI / 2f;
            var petal = r * (0.62f + 0.38f * MathF.Abs(MathF.Cos(2.5f * angle)));
            var d = dist - petal;
            var c = Rgba.Lerp(look.Accent, look.Edge, 0.18f * (dist / r));
            c = Rgba.Lerp(c, look.Edge, 0.6f * Draw.SmoothStep(-0.8f, -0.2f, d));
            result = Layer(result, d, c);
            var centre = dist - r * 0.3f;
            return Layer(result, centre, Rgba.Hex("#FFC531").Shade(0.2f - 0.4f * (dist / r)));
        }

        private static Rgba Rosette(Rgba result, Look look, float x, float y, float bx, float by)
        {
            const float r = 6.2f;
            var dx = x - bx;
            var dy = y - by;
            var dist = MathF.Sqrt(dx * dx + dy * dy);
            result = Shadow(result, dist - r - 0.6f + (dy < 0 ? 0f : 0.8f), 1.8f, 0.3f);
            if (dist > r + 1.5f)
            {
                return result;
            }

            // Восемь лепестков золота, внутри — гранёный рубин с бликом.
            var angle = MathF.Atan2(dy, dx);
            var lobes = r * (0.82f + 0.18f * MathF.Cos(8f * angle));
            var d = dist - lobes;
            var lit = Draw.Clamp01(0.5f - 0.4f * dx / r + 0.5f * dy / r);
            var gold = Rgba.Lerp(look.Edge, look.Face, 0.35f + 0.65f * lit);
            gold = Rgba.Lerp(gold, look.Accent, MathF.Pow(lit, 3f) * 0.6f);
            gold = gold.Shade(-0.25f * MathF.Max(0f, MathF.Cos(8f * angle)) * (dist / r));
            gold = Rgba.Lerp(gold, look.Edge, 0.8f * Draw.SmoothStep(-0.9f, -0.25f, d));
            result = Layer(result, d, gold);

            var gem = dist - 2.9f;
            var facet = MathF.Floor((angle + MathF.PI) / (MathF.PI / 3f));
            var ruby = Rgba.Hex("#D3204F").Shade(-0.25f + 0.12f * ((int)facet % 3) + 0.25f * lit);
            ruby = Rgba.Lerp(ruby, Rgba.Hex("#7A0F2A"), 0.8f * Draw.SmoothStep(-0.8f, -0.2f, gem));
            result = Layer(result, gem, ruby);
            return Layer(result, Draw.Circle(x, y, bx - 1f, by + 1f, 0.75f), Rgba.White.WithAlpha(0.9f));
        }

        /// <summary>Сердечко размера s (примерно полуширина) с центром (cx, cy), острием вниз.</summary>
        private static float Heart(float x, float y, float cx, float cy, float s)
        {
            var left = Draw.Circle(x, y, cx - 0.5f * s, cy + 0.3f * s, 0.56f * s);
            var right = Draw.Circle(x, y, cx + 0.5f * s, cy + 0.3f * s, 0.56f * s);
            var tip = Draw.Triangle(x, y, cx - 1.02f * s, cy + 0.12f * s, cx + 1.02f * s, cy + 0.12f * s, cx, cy - 1f * s);
            return MathF.Min(MathF.Min(left, right), tip);
        }

        /// <summary>Пятиконечная звезда лучом вверх: r — радиус до луча, inner — доля внутреннего радиуса.</summary>
        private static float Star(float x, float y, float cx, float cy, float r, float inner)
        {
            const float k1x = 0.809016994f;
            const float k1y = -0.587785252f;
            var px = MathF.Abs(x - cx);
            var py = y - cy;
            var dot = MathF.Max(k1x * px + k1y * py, 0f) * 2f;
            px -= dot * k1x;
            py -= dot * k1y;
            dot = MathF.Max(-k1x * px + k1y * py, 0f) * 2f;
            px -= dot * -k1x;
            py -= dot * k1y;
            px = MathF.Abs(px);
            py -= r;
            var bax = inner * -k1y;
            var bay = inner * k1x - 1f;
            var h = Draw.Clamp((px * bax + py * bay) / (bax * bax + bay * bay), 0f, r);
            var ex = px - bax * h;
            var ey = py - bay * h;
            return MathF.Sqrt(ex * ex + ey * ey) * MathF.Sign(py * bax - px * bay);
        }

        private static Rgba Layer(Rgba result, float d, Rgba c, float alpha = 1f)
        {
            var cover = Draw.Cover(d) * alpha;
            return cover > 0f ? Rgba.Over(c.WithAlpha(c.A * cover), result) : result;
        }

        private static Rgba Shadow(Rgba result, float d, float soft, float alpha)
        {
            var a = Draw.Clamp01(1f - (d + soft * 0.3f) / soft) * alpha;
            return a > 0f ? Rgba.Over(new Rgba(0.1f, 0.14f, 0.2f, a), result) : result;
        }
    }
}
