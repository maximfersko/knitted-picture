#nullable enable

using System;

namespace KnittedPicture.Game
{
    /// <summary>
    /// Объёмные значки-иллюстрации в стиле клубков: свет сверху слева, металл и камни с бликами,
    /// мягкая тень снизу. Кубок достижений, камни редкости пряжи, звезда уровня мотка, плетёная корзинка,
    /// меланжевый клубок. Координаты в шейдерах — y вверх.
    /// </summary>
    public static class IconRaster
    {
        /// <summary>Свет сверху слева (y вверх).</summary>
        private const float LightX = -0.6f;
        private const float LightY = 0.8f;

        /// <summary>Наложить цвет c с покрытием формы d (меньше нуля — внутри) поверх result.</summary>
        private static Rgba Layer(Rgba result, float d, Rgba c, float alpha = 1f)
        {
            var cover = Draw.Cover(d) * alpha;
            return cover > 0f ? Rgba.Over(c.WithAlpha(c.A * cover), result) : result;
        }

        /// <summary>Мягкая тень: размытое пятно формы d шириной soft.</summary>
        private static Rgba Shadow(Rgba result, float d, float soft, float alpha)
        {
            var a = Draw.Clamp01(1f - (d + soft * 0.3f) / soft) * alpha;
            return a > 0f ? Rgba.Over(new Rgba(0.1f, 0.14f, 0.2f, a), result) : result;
        }

        /// <summary>
        /// «Подушка»: у края формы нормаль смотрит наружу, грань к свету светлее, от света темнее —
        /// плоская фигура становится выпуклой. width — ширина скоса.
        /// </summary>
        private static Rgba Pillow(Func<float, float, float> sdf, float x, float y, Rgba color, float width, float strength = 0.4f)
        {
            var d = sdf(x, y);
            var gx = sdf(x + 0.5f, y) - sdf(x - 0.5f, y);
            var gy = sdf(x, y + 0.5f) - sdf(x, y - 0.5f);
            var len = MathF.Sqrt(gx * gx + gy * gy) + 1e-5f;
            var facing = (gx * LightX + gy * LightY) / len;
            var edge = 1f - Draw.SmoothStep(0f, width, -d);
            return color.Shade(strength * facing * edge);
        }

        // ---------- Кубок ----------

        /// <summary>
        /// Кубок достижения 64×64: чаша с ручками на ножке и двухступенчатом пьедестале, металл с бликом-полосой
        /// по цилиндру, на чаше выпуклая звезда. gold — золотой (можно забрать, получено), иначе серебряный (в процессе).
        /// </summary>
        public static Raster Trophy(bool gold)
        {
            var mid = gold ? Rgba.Hex("#FFC23C") : Rgba.Hex("#C3C7DE");
            var light = gold ? Rgba.Hex("#FFF0B0") : Rgba.Hex("#F7F8FF");
            var deep = gold ? Rgba.Hex("#C27406") : Rgba.Hex("#7F84A8");
            const float cx = 32f;
            return Raster.Render(64, 64, 3, (x, y) =>
            {
                var result = Rgba.Clear;
                result = Shadow(result, Draw.Ellipse(x, y, cx, 7.5f, 15f, 2.4f), 4f, 0.32f);

                // Металл по цилиндру: тёмные края, светлая полоса левее середины, узкий блик.
                Rgba Metal(float u, float v)
                {
                    var c = Rgba.Lerp(deep, mid, Draw.SmoothStep(-1.05f, -0.55f, u) * (1f - Draw.SmoothStep(0.45f, 1.05f, u)));
                    c = Rgba.Lerp(c, light, MathF.Exp(-MathF.Pow((u + 0.42f) / 0.2f, 2f)) * 0.85f);
                    c = Rgba.Lerp(c, Rgba.White, MathF.Exp(-MathF.Pow((u + 0.5f) / 0.06f, 2f)) * 0.7f);
                    return c.Shade(-0.18f * Draw.Clamp01(-v));
                }

                // Пьедестал: нижняя ступень, верхняя ступень.
                var base1 = Draw.RoundBox(x, y, cx, 10.5f, 15f, 3.6f, 2.2f);
                result = Layer(result, base1, Metal((x - cx) / 15f, -0.6f).Shade(-0.12f));
                var base2 = Draw.RoundBox(x, y, cx, 15.5f, 10.5f, 2.4f, 1.6f);
                result = Layer(result, base2, Metal((x - cx) / 10.5f, -0.2f));

                // Ножка с узлом.
                var stem = MathF.Min(Draw.RoundBox(x, y, cx, 21f, 2.6f, 5f, 1.2f), Draw.Ellipse(x, y, cx, 22.5f, 4.6f, 2.6f));
                result = Layer(result, stem, Metal((x - cx) / 4.6f, -0.1f));

                // Ручки: кольца по бокам чаши, задняя половина темнее.
                foreach (var side in new[] { -1f, 1f })
                {
                    var ring = MathF.Abs(Draw.Ellipse(x, y, cx + side * 16.5f, 43f, 6.2f, 7.4f)) - 2f;
                    var clip = side * (x - cx) - 15f;
                    var handle = MathF.Max(ring, -clip);
                    var shade = side < 0f ? 0.1f : -0.2f;
                    result = Layer(result, handle, mid.Shade(shade));
                    result = Layer(result, MathF.Max(MathF.Abs(Draw.Ellipse(x, y, cx + side * 16.5f, 43.6f, 6.2f, 7.4f)) - 0.7f, -clip),
                        light.WithAlpha(0.6f));
                }

                // Чаша: прямые стенки сверху, круглое дно; срез по кромке.
                var bowl = MathF.Max(MathF.Min(Draw.RoundBox(x, y, cx, 46f, 15f, 9f, 2f), Draw.Circle(x, y, cx, 40f, 15f)), y - 55f);
                var u0 = (x - cx) / 15f;
                result = Layer(result, bowl, Metal(u0, (y - 45f) / 12f));

                // Звезда на чаше: выпуклая, светлая с тёмным низом.
                float Star(float px, float py) => Star5(px - cx, py - 43f, 7.2f, 0.48f) - 0.6f;
                var star = Star(x, y);
                if (star < 1f)
                {
                    var starColor = Pillow(Star, x, y, gold ? Rgba.Hex("#FFE27A") : Rgba.Hex("#E9EBFA"), 2.2f, 0.6f);
                    result = Layer(result, star + 0.6f, deep.WithAlpha(0.5f));
                    result = Layer(result, star, starColor);
                }

                // Кромка: светлый ободок и тёмное горлышко внутри.
                result = Layer(result, Draw.Ellipse(x, y, cx, 55f, 15.6f, 3.2f), light.Shade(0.2f));
                result = Layer(result, Draw.Ellipse(x, y, cx, 55.2f, 13.2f, 2.1f), deep.Shade(-0.25f));
                return result;
            });
        }

        // ---------- Камни редкости ----------

        /// <summary>
        /// Огранённый камень 48×48 для плашки редкости: площадка сверху, грани короны и павильона с разным светом,
        /// искра-блик. tier: 0 — обычная (дымчатый кварц), 1 — редкая (сапфир), 2 — эпическая (янтарь-топаз).
        /// </summary>
        public static Raster Gem(int tier)
        {
            var color = tier switch
            {
                2 => Rgba.Hex("#FFB320"),
                1 => Rgba.Hex("#3B7BE0"),
                _ => Rgba.Hex("#9AA6C2"),
            };
            const float cx = 24f;
            const float girdle = 30f;
            const float top = 40f;
            const float point = 6f;
            const float halfW = 19f;
            const float tableW = 9f;
            return Raster.Render(48, 48, 3, (x, y) =>
            {
                var result = Rgba.Clear;
                result = Shadow(result, Draw.Ellipse(x, y, cx, 5.5f, 10f, 1.8f), 3f, 0.25f);

                var crown = Draw.Triangle(x, y, cx - tableW, top, cx - halfW, girdle, cx + halfW, girdle);
                crown = MathF.Min(crown, Draw.Triangle(x, y, cx - tableW, top, cx + halfW, girdle, cx + tableW, top));
                var pavilion = Draw.Triangle(x, y, cx - halfW, girdle, cx, point, cx + halfW, girdle);
                var gem = MathF.Min(crown, pavilion) - 0.4f;
                if (gem > 1.5f)
                {
                    return result;
                }

                Rgba face;
                var dx = x - cx;
                if (y >= girdle)
                {
                    // Корона: площадка светлая, боковые грани — левая к свету, правая в тени.
                    var k = (y - girdle) / (top - girdle);
                    var tableEdge = Draw.Lerp(halfW, tableW, k);
                    if (MathF.Abs(dx) < tableW * 0.92f + (1f - k) * 2f && y > girdle + 2.5f)
                    {
                        face = color.Shade(0.42f);
                    }
                    else
                    {
                        face = dx < 0f ? color.Shade(0.2f) : color.Shade(-0.25f);
                    }

                    if (MathF.Abs(MathF.Abs(dx) - tableEdge) < 0.6f)
                    {
                        face = face.Shade(0.25f);
                    }
                }
                else
                {
                    // Павильон: веер граней к острию, свет меняется от грани к грани.
                    var s = dx / (girdle - point + 1e-3f) * ((girdle - point) / MathF.Max(0.5f, y - point + 1f));
                    var slice = MathF.Floor(Draw.Clamp((s + 1.1f) * 2f, 0f, 4f));
                    var tones = new[] { 0.05f, -0.1f, 0.15f, -0.3f, -0.45f };
                    face = color.Shade(tones[(int)slice]);
                }

                // Внутренний отсвет снизу и тонкая тёмная кромка.
                face = Rgba.Lerp(face, color.Shade(0.5f), MathF.Exp(-MathF.Pow((y - 14f) / 5f, 2f)) * MathF.Exp(-dx * dx / 30f) * 0.5f);
                result = Layer(result, gem + 0.9f, color.Shade(-0.6f));
                result = Layer(result, gem, face);

                // Искра: четырёхлучевой блик на площадке.
                var sx = x - (cx - 4f);
                var sy = y - 36f;
                var spark = MathF.Max(0f, 1f - MathF.Abs(sx) / 4f - MathF.Abs(sy) * 1.5f) + MathF.Max(0f, 1f - MathF.Abs(sy) / 4f - MathF.Abs(sx) * 1.5f);
                if (spark > 0f)
                {
                    result = Rgba.Over(Rgba.White.WithAlpha(Draw.Clamp01(spark) * 0.95f * Draw.Cover(gem)), result);
                }

                return result;
            });
        }

        // ---------- Звезда уровня ----------

        /// <summary>Выпуклая звезда-розетка 48×48 для уровня мотка: синяя с золотым ободком; цифра — подписью сверху.</summary>
        public static Raster LevelStar()
        {
            const float c = 24f;
            var face = Rgba.Hex("#2F5FB8");
            var rim = Rgba.Hex("#FFC23C");
            return Raster.Render(48, 48, 3, (x, y) =>
            {
                var result = Rgba.Clear;
                float Outer(float px, float py) => Star5(px - c, py - c + 1.5f, 21f, 0.62f) - 2.4f;
                float Inner(float px, float py) => Star5(px - c, py - c + 1.5f, 17.5f, 0.62f) - 2f;
                result = Shadow(result, Star5(x - c, y - c + 3.5f, 21f, 0.62f) - 2.4f, 4f, 0.35f);
                result = Layer(result, Outer(x, y), Pillow(Outer, x, y, rim, 2.4f, 0.55f));
                var inner = Inner(x, y);
                if (inner < 1f)
                {
                    var tone = Pillow(Inner, x, y, face, 5f, 0.45f);
                    tone = Rgba.Lerp(tone, tone.Shade(0.25f), Draw.Clamp01((y - c) / 18f));
                    result = Layer(result, inner, tone);
                }

                return result;
            });
        }

        // ---------- Корзинка ----------

        /// <summary>Задняя часть корзинки 120×96: ручка дугой и тёмное нутро — клубки кладутся между задней и передней.</summary>
        public static Raster BasketBack()
        {
            var wicker = Rgba.Hex("#D9A066");
            return Raster.Render(120, 96, 3, (x, y) =>
            {
                var result = Rgba.Clear;

                // Ручка — плетёная дуга над корзинкой.
                float Handle(float px, float py) => MathF.Max(MathF.Abs(Draw.Ellipse(px, py, 60f, 46f, 40f, 44f)) - 3.6f, 46f - py);
                var handle = Handle(x, y);
                if (handle < 1f)
                {
                    var twist = 0.5f + 0.5f * MathF.Sin(MathF.Atan2(y - 46f, x - 60f) * 46f);
                    var tone = Pillow(Handle, x, y, wicker, 3f, 0.45f).Shade(-0.18f * twist);
                    result = Layer(result, handle, tone);
                }

                // Нутро: тёмный овал горловины.
                result = Layer(result, Draw.Ellipse(x, y, 60f, 46f, 49f, 11f), wicker.Shade(-0.62f));
                return result;
            });
        }

        /// <summary>
        /// Передняя стенка корзинки 120×96: плетение (горизонтальные прутья через вертикальные стойки,
        /// «через один»), выпуклость по цилиндру, косичка по кромке, мягкая тень снизу.
        /// </summary>
        public static Raster BasketFront()
        {
            var wicker = Rgba.Hex("#D9A066");
            return Raster.Render(120, 96, 3, (x, y) =>
            {
                var result = Rgba.Clear;
                result = Shadow(result, Draw.Ellipse(x, y, 60f, 7f, 44f, 5f), 6f, 0.3f);

                // Стенка: трапеция, книзу уже, с круглым дном.
                var half = Draw.Lerp(39f, 49f, (y - 10f) / 36f);
                var wall = MathF.Max(MathF.Max(MathF.Abs(x - 60f) - half, 10f - y), y - 46f);
                wall = MathF.Max(wall, Draw.Ellipse(x, y, 60f, 30f, 52f, 26f));
                if (wall < 1f)
                {
                    var u = (x - 60f) / half;
                    var row = (int)MathF.Floor((y - 10f) / 6f);
                    var rv = ((y - 10f) / 6f) % 1f;
                    var col = (int)MathF.Floor((x - 60f + 100f) / 8f);
                    var over = ((row + col) & 1) == 0;

                    // Прут — трубочка: светлее посередине по высоте; там, где уходит под стойку, темнее.
                    var tube = MathF.Sin(rv * MathF.PI);
                    var tone = wicker.Shade(0.12f * tube - 0.22f * (1f - tube));
                    if (!over)
                    {
                        tone = tone.Shade(-0.18f);
                    }

                    // Стойки видны между прутьями.
                    var stake = MathF.Abs(((x - 60f + 100f) / 8f) % 1f - 0.5f);
                    if (!over && stake < 0.18f)
                    {
                        tone = wicker.Shade(0.05f);
                    }

                    tone = tone.Shade(-0.32f * u * u - 0.12f * Draw.Clamp01(-u) * 0f + 0.08f * Draw.Clamp01(-u));
                    tone = tone.Shade(-0.15f * Draw.Clamp01((24f - y) / 14f));
                    result = Layer(result, wall, tone);
                }

                // Косичка по кромке.
                var braid = Draw.Capsule(x, y, 12f, 46f, 108f, 46f, 5f, 5f, out _, out _);
                if (braid < 1f)
                {
                    var twist = 0.5f + 0.5f * MathF.Sin((x - y * 0.8f) * 0.7f);
                    var tone = wicker.Shade(0.18f * twist - 0.2f * (1f - twist));
                    tone = tone.Shade(0.15f * Draw.Clamp01((y - 46f) / 5f));
                    result = Layer(result, braid, tone);
                }

                return result;
            });
        }

        // ---------- Меланж ----------

        /// <summary>
        /// Меланжевый клубок: два готовых клубка (основной цвет и второй) смешаны по тонким кручёным прожилкам,
        /// как пряжа из волокон двух цветов. Свет и нити — от самого клубка, смешивается только цвет.
        /// tight — кадр вокруг шара, как у Art2.YarnBall.
        /// </summary>
        public static Raster Melange(Rgba main, Rgba second, int x0, int y0, int size)
        {
            // Половина разрешения клубка: значку мотка (до ~90 точек) хватает, а рисуется вчетверо быстрее.
            var a = BallRaster.Ball(main).Crop(x0, y0, size, size);
            var b = BallRaster.Ball(second).Crop(x0, y0, size, size);
            var n = size / 2;
            var r = new Raster(n, n);
            r.Paint((x, y) =>
            {
                // Paint даёт центр пикселя: (px + 0.5, py + 0.5).
                var px = (int)x * 2;
                var py = (int)y * 2;
                var pa = Average(a, px, py, size);
                var pb = Average(b, px, py, size);

                // Короткие прожилки второго цвета пятнами, а не полосами через весь шар.
                var fx = (float)px;
                var fy = (float)py;
                var swirl = Draw.Noise(fx * 0.015f, fy * 0.015f) * 9f;
                var streak = MathF.Sin((fx * 0.6f + fy * 0.8f) * 0.21f + swirl) * 0.5f + 0.5f;
                var patches = Draw.SmoothStep(0.42f, 0.62f, Draw.Noise(fx * 0.028f + 17f, fy * 0.028f + 5f));
                var flecks = Draw.SmoothStep(0.72f, 0.86f, Draw.Noise(fx * 0.09f, fy * 0.09f));
                var mix = Draw.SmoothStep(0.55f, 0.9f, streak) * patches * 0.7f + flecks * 0.35f;
                return new Rgba(pa.R + (pb.R - pa.R) * mix, pa.G + (pb.G - pa.G) * mix, pa.B + (pb.B - pa.B) * mix, pa.A);
            });
            return r;
        }

        /// <summary>Среднее 2×2 пикселей (с учётом прозрачности): уменьшение без лесенки по контуру.</summary>
        private static Rgba Average(Raster src, int x, int y, int size)
        {
            float r = 0f, g = 0f, b = 0f, a = 0f;
            for (var dy = 0; dy < 2; dy++)
            {
                for (var dx = 0; dx < 2; dx++)
                {
                    var p = src.Pixels[Math.Min(size - 1, y + dy) * size + Math.Min(size - 1, x + dx)];
                    r += p.R * p.A;
                    g += p.G * p.A;
                    b += p.B * p.A;
                    a += p.A;
                }
            }

            return a > 0f ? new Rgba(r / a, g / a, b / a, a / 4f) : Rgba.Clear;
        }

        // ---------- Общее ----------

        /// <summary>Пятиконечная звезда (по Иниго Килесу), вершина вверх: r — до вершины, rf — доля внутреннего радиуса.</summary>
        public static float Star5(float px, float py, float r, float rf)
        {
            const float k1x = 0.809016994f;
            const float k1y = -0.587785252f;
            const float k2x = -k1x;
            const float k2y = k1y;
            px = MathF.Abs(px);
            var dot = MathF.Max(k1x * px + k1y * py, 0f);
            px -= 2f * dot * k1x;
            py -= 2f * dot * k1y;
            dot = MathF.Max(k2x * px + k2y * py, 0f);
            px -= 2f * dot * k2x;
            py -= 2f * dot * k2y;
            px = MathF.Abs(px);
            py -= r;
            var bax = rf * -k1y * r;
            var bay = rf * k1x * r - r;
            var h = Draw.Clamp((px * bax + py * bay) / (bax * bax + bay * bay), 0f, 1f);
            var dx = px - bax * h;
            var dy = py - bay * h;
            return MathF.Sqrt(dx * dx + dy * dy) * MathF.Sign(py * bax - px * bay);
        }
    }
}
