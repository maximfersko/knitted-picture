#nullable enable

using System;

namespace KnittedPicture.Game
{
    /// <summary>
    /// Мета 2.0: вязаная миниатюра картинки (петли-«галочки» по клеткам — для пялец, обложек коллекций,
    /// альбома), вязаная шкатулка-сундук (косы, лента и вязка в цвет редкости, латунный замок),
    /// подарок в вязаной коробке с розовым бантом, монета с клубком, деревянные пяльцы с латунным винтом.
    /// </summary>
    public static class MetaRaster
    {
        /// <summary>Квадратные клетки миниатюры, как на игровом поле.</summary>
        public const int MiniCellW = 16;
        public const int MiniCellH = 16;

        // Форма нитей считается один раз на материал, затем красится для любой картинки.
        private static readonly Lazy<Raster>[,,] MiniTiles = BuildTiles();
        private static readonly Lazy<Raster> MiniCanvas = new Lazy<Raster>(KnitRaster.CanvasTile);

        private static Lazy<Raster>[,,] BuildTiles()
        {
            var result = new Lazy<Raster>[3, 2, 2];
            for (var variant = 0; variant < 3; variant++)
            for (var detail = 0; detail < 2; detail++)
            for (var gloss = 0; gloss < 2; gloss++)
            {
                var v = variant;
                var coarse = detail != 0;
                var shine = gloss != 0;
                result[variant, detail, gloss] = new Lazy<Raster>(() => KnitRaster.TextileTile(v, coarse, shine));
            }
            return result;
        }

        /// <summary>Миниатюра с общей фактурой поля и ограниченной длинной стороной.</summary>
        public static Raster Mini(int w, int h, Rgba?[] colors, bool[] canvas, int maxSize = 128, int[]? variants = null,
            bool relief = true, Rgba? reliefColor = null)
        {
            var cell = Math.Max(1, maxSize / Math.Max(w, h));
            var result = new Raster(w * cell, h * cell);
            var bodyMaps = new float[]?[3];
            var glossMaps = new float[]?[3];
            float[]? fabricMap = null;
            var detail = cell < 18 ? 1 : 0;
            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    var index = y * w + x;
                    var color = colors[index];
                    if (color == null) { continue; }
                    var variant = variants != null ? variants[index] : 0;
                    if (canvas[index])
                    {
                        fabricMap ??= FilterTile(MiniCanvas.Value, cell, false);
                    }
                    else if (bodyMaps[variant] == null)
                    {
                        bodyMaps[variant] = FilterTile(MiniTiles[variant, detail, 0].Value, cell, false);
                        glossMaps[variant] = FilterTile(MiniTiles[variant, detail, 1].Value, cell, true);
                    }
                    for (var py = 0; py < cell; py++)
                    {
                        for (var px = 0; px < cell; px++)
                        {
                            var sampleIndex = py * cell + px;
                            var baseSample = canvas[index] ? fabricMap![sampleIndex] : bodyMaps[variant]![sampleIndex];
                            var shade = color.Value.Scale(baseSample);
                            result[x * cell + px, (h - 1 - y) * cell + py] = canvas[index]
                                ? shade : Rgba.Lerp(shade, Rgba.White, glossMaps[variant]![sampleIndex]);
                        }
                    }
                }
            }
            if (relief)
            {
                var shadow = (reliefColor ?? Rgba.Hex("#1E2732")).WithAlpha(KnitRaster.ReliefAlpha * 0.5f);
                for (var y = 0; y < h; y++)
                for (var x = 0; x < w; x++)
                {
                    var index = y * w + x;
                    if (canvas[index] || colors[index] == null) { continue; }
                    if (x + 1 < w && canvas[index + 1])
                    {
                        for (var py = 0; py < cell; py++)
                        {
                            result.Blend((x + 1) * cell, (h - 1 - y) * cell + py, shadow);
                        }
                    }
                    if (y + 1 < h && canvas[index + w])
                    {
                        for (var px = 0; px < cell; px++)
                        {
                            result.Blend(x * cell + px, (h - 1 - y) * cell - 1, shadow);
                        }
                    }
                }
            }
            return result;
        }

        /// <summary>Усреднить точную площадь исходных пикселей для каждого пикселя клетки.</summary>
        private static float[] FilterTile(Raster source, int size, bool alpha)
        {
            var result = new float[size * size];
            for (var y = 0; y < size; y++)
            {
                var y0 = (float)y * source.Height / size;
                var y1 = (float)(y + 1) * source.Height / size;
                for (var x = 0; x < size; x++)
                {
                    var x0 = (float)x * source.Width / size;
                    var x1 = (float)(x + 1) * source.Width / size;
                    var sum = 0f;
                    for (var sy = (int)Math.Floor(y0); sy < Math.Ceiling(y1); sy++)
                    {
                        var wy = Math.Min(y1, sy + 1f) - Math.Max(y0, sy);
                        for (var sx = (int)Math.Floor(x0); sx < Math.Ceiling(x1); sx++)
                        {
                            var wx = Math.Min(x1, sx + 1f) - Math.Max(x0, sx);
                            var sample = source[sx, sy];
                            sum += (alpha ? sample.A : sample.R) * wx * wy;
                        }
                    }
                    result[y * size + x] = sum / ((x1 - x0) * (y1 - y0));
                }
            }
            return result;
        }

        /// <summary>
        /// Корпус шкатулки w×h пикселей: вязка «косами» цвета морской волны, металлическая лента с заклёпками (цвет по редкости:
        /// сталь, серебро или золото, см. ChestBand), латунная окантовка снизу с бликом, ножки, замочек с ключевиной.
        /// По контуру — резкая тёмная кромка, у каждой детали — блик сверху: сундук чёткий и на маленькой плитке.
        /// </summary>
        public static Raster ChestBody(int w, int h, int tier)
        {
            var r = new Raster(w, h);
            var teal = ChestKnit(tier);
            var band = ChestBand(tier);
            var brass = Rgba.Hex("#E2B04A");
            var s = h / 118f;
            r.Paint((x, y) =>
            {
                var top = h - y; // от верха вниз
                var body = Draw.RoundBox(x, y, w / 2f, h / 2f + 4f * s, w / 2f - 2f, h / 2f - 6f * s, 14f * s);
                var cover = Draw.Cover(body);
                if (cover <= 0f)
                {
                    // Ножки.
                    var foot = MathF.Min(Draw.RoundBox(x, y, 22f * s, 6f * s, 14f * s, 6f * s, 4f * s), Draw.RoundBox(x, y, w - 22f * s, 6f * s, 14f * s, 6f * s, 4f * s));
                    var fc = Draw.Cover(foot);
                    return fc > 0f ? brass.Shade(-0.25f - 0.25f * Draw.SmoothStep(-2.4f * s, -0.8f * s, foot)).WithAlpha(fc) : Rgba.Clear;
                }

                Rgba c;
                var bandTop = 30f * s;
                var bandBottom = 46f * s;
                var trim = h - 26f * s;
                if (top > trim)
                {
                    // Латунная окантовка снизу.
                    var t = (top - trim) / (26f * s);
                    c = brass.Shade(0.15f - 0.4f * t);

                    // Блик по верхнему краю окантовки и тёмная щель над ней.
                    var fromTrim = top - trim;
                    c = Rgba.Lerp(c, Rgba.White, 0.45f * Draw.Clamp01(1f - MathF.Abs(fromTrim - 2.6f * s) / (1.2f * s)));
                    c = c.Shade(-0.35f * Draw.Clamp01(1f - fromTrim / (1f * s)));
                }
                else if (top > bandTop && top < bandBottom)
                {
                    // Металлическая лента: блик по верху, тень по низу, тёмные кромки; заклёпки вместо строчки.
                    var bt = (top - bandTop) / (bandBottom - bandTop);
                    c = band.Shade(0.22f - 0.3f * bt);
                    c = Rgba.Lerp(c, Rgba.White, 0.35f * Draw.Clamp01(1f - MathF.Abs(bt - 0.18f) / 0.12f));
                    var bandEdge = MathF.Min(top - bandTop, bandBottom - top);
                    c = c.Shade(-0.3f * Draw.Clamp01(1f - bandEdge / (1.1f * s)));

                    var pitch = 30f * s;
                    var mid = (bandTop + bandBottom) / 2f;
                    var rx = (x - w / 2f) - MathF.Round((x - w / 2f) / pitch) * pitch;
                    var rivet = MathF.Sqrt(rx * rx + (top - mid) * (top - mid)) - 2.6f * s;
                    if (rivet < 0.5f && MathF.Abs(x - w / 2f) > 20f * s)
                    {
                        var rlit = Draw.Clamp01(0.5f - (rx + (top - mid)) / (5f * s));
                        var rc = band.Shade(-0.25f + 0.55f * rlit);
                        c = Rgba.Lerp(c, rc, Draw.Cover(rivet));
                    }
                }
                else
                {
                    // Вязка косами: вертикальные жгуты, каждый из двух перекручивающихся прядей.
                    var cable = 26f * s;
                    var u = (x % cable) / cable - 0.5f;
                    var wave = MathF.Sin((top / (14f * s) + (x % (cable * 2f) < cable ? 0f : 0.5f)) * MathF.PI);
                    var strand = MathF.Abs(u - 0.18f * wave);
                    var v = 0.78f + 0.22f * MathF.Sqrt(Draw.Clamp01(1f - strand * 4f));
                    var groove = MathF.Abs(u) > 0.44f ? 0.82f : 1f;
                    c = teal.Shade((v - 1f) * 0.9f + (groove - 1f));
                }

                // Свет сверху слева, тень у краёв, резкая тёмная кромка по контуру.
                var light = 0.12f * (1f - x / w) * (1f - top / h);
                c = Rgba.Lerp(c, Rgba.White, light);
                c = c.Shade(-0.12f * Draw.SmoothStep(-10f * s, 0f, body));
                c = c.Shade(-0.35f * Draw.SmoothStep(-1.8f * s, -0.6f * s, body));

                // Замочек по центру сверху, на ленте.
                var lockBody = Draw.RoundBox(x, y, w / 2f, h - 40f * s, 13f * s, 17f * s, 5f * s);
                if (lockBody < 0.5f)
                {
                    var lc = brass.Shade(0.15f - 0.35f * Draw.Clamp01((h - 40f * s - y + 17f * s) / (34f * s)));
                    lc = lc.Shade(-0.35f * Draw.SmoothStep(-1.6f * s, -0.5f * s, lockBody));
                    lc = Rgba.Lerp(lc, Rgba.White, 0.4f * Draw.Clamp01(1f - MathF.Abs(lockBody + 2.6f * s) / (1f * s)) * Draw.Clamp01((y - (h - 40f * s)) / (10f * s)));
                    var hole = MathF.Min(Draw.Circle(x, y, w / 2f, h - 36f * s, 3.4f * s), Draw.RoundBox(x, y, w / 2f, h - 43f * s, 1.6f * s, 5f * s, 1f));
                    if (hole < 0f)
                    {
                        lc = Rgba.Hex("#3A2618");
                    }

                    c = Rgba.Lerp(c, lc, Draw.Cover(lockBody));
                }

                return c.WithAlpha(cover);
            });
            return r;
        }

        /// <summary>
        /// Цвета сундука по редкости (tier — ступень Rarity): вязка и металл ленты. Обычный — морская волна и сталь,
        /// необычный — зелёный и сталь, редкий — синий и серебро, эпический — фиолетовый и золото,
        /// легендарный — тёмный янтарь и яркое золото.
        /// </summary>
        public static Rgba ChestKnit(int tier) => tier switch
        {
            1 => Rgba.Hex("#2E7A4A"),
            2 => Rgba.Hex("#2D4E9A"),
            3 => Rgba.Hex("#5B3192"),
            4 => Rgba.Hex("#9A5A14"),
            _ => Rgba.Hex("#2F6E73"),
        };

        public static Rgba ChestBand(int tier) => tier switch
        {
            1 => Rgba.Hex("#7FA391"),
            2 => Rgba.Hex("#B7C8E4"),
            3 => Rgba.Hex("#E7B64E"),
            4 => Rgba.Hex("#FFD24F"),
            _ => Rgba.Hex("#6F93B5"),
        };

        /// <summary>Крышка шкатулки: купол той же вязки с глянцем, кромка снизу (у эпического и легендарного — золотая).</summary>
        public static Raster ChestLid(int w, int h, int tier)
        {
            var r = new Raster(w, h);
            var teal = ChestKnit(tier);
            var edge = tier >= 3 ? ChestBand(tier) : ChestKnit(tier).Shade(-0.35f);
            var s = h / 74f;
            r.Paint((x, y) =>
            {
                var d = Draw.RoundBox(x, y, w / 2f, h / 2f, w / 2f - 2f, h / 2f - 2f, 30f * s);
                var flat = Draw.RoundBox(x, y, w / 2f, h * 0.25f, w / 2f - 2f, h * 0.25f - 2f, 6f * s);
                d = y < h * 0.5f ? flat : d;
                var cover = Draw.Cover(d);
                if (cover <= 0f)
                {
                    return Rgba.Clear;
                }

                var v = (y / h);
                var c = teal.Shade(-0.2f + 0.3f * v);
                if (y < 10f * s)
                {
                    c = edge.Shade(-0.1f);
                }

                // Блик: чёткий край, а не размытое пятно.
                var gloss = Draw.Ellipse(x, y, w * 0.35f, h * 0.72f, w * 0.2f, 6f * s, -4f);
                if (gloss < 0.5f)
                {
                    c = Rgba.Lerp(c, Rgba.White, 0.42f * Draw.Cover(gloss) * (0.55f + 0.45f * Draw.Clamp01(-gloss / (3f * s))));
                }

                // Светлая полоса под верхним краем купола и резкая тёмная кромка по контуру.
                c = Rgba.Lerp(c, Rgba.White, 0.18f * Draw.Clamp01(1f - MathF.Abs(-d - 3.2f * s) / (1.2f * s)) * Draw.Clamp01((y - h * 0.5f) / (h * 0.3f)));
                c = c.Shade(-0.14f * Draw.SmoothStep(-8f * s, 0f, d));
                c = c.Shade(-0.35f * Draw.SmoothStep(-1.8f * s, -0.6f * s, d));
                return c.WithAlpha(cover);
            });
            return r;
        }

        /// <summary>
        /// Подарок: коробка в голубой вязке и крышка шире неё, красная лента крест-накрест и бант с узелком. 192×192,
        /// рисуется вдвое крупнее (сглаженные края и мелкая вязка без ступенек). У каждой детали — резкая тёмная кромка,
        /// свет сверху слева, тень от крышки на коробке, блики на ленте и петлях банта.
        /// </summary>
        public static Raster Gift()
        {
            const int s = 192;
            var blue = Rgba.Hex("#3D7FC4");
            var red = Rgba.Hex("#D9483E");
            return Raster.Render(s, s, 2, (x, y) =>
            {
                var result = Rgba.Clear;

                // Деталь: заливка с кромкой — у самого края темнее на rim, ширина кромки 1,6 точки.
                void Layer(float d, Rgba c, float rim = 0.35f)
                {
                    var cover = Draw.Cover(d);
                    if (cover > 0f)
                    {
                        var edged = c.Shade(-rim * Draw.SmoothStep(-2.2f, -0.6f, d));
                        result = Rgba.Over(edged.WithAlpha(cover), result);
                    }
                }

                // Вязка: вертикальные столбики петель (мягкие волны) и ряды; сглаженные, без ступенек.
                float Knit(float px, float py)
                {
                    var col = MathF.Abs(MathF.Sin(px / 6f * MathF.PI));
                    var row = 1f - Draw.Clamp01(MathF.Abs(((py / 9f) % 1f) - 0.5f) / 0.12f);
                    return col * 0.09f + row * 0.05f;
                }

                // Коробка: свет слева, тень от крышки сверху, к низу темнее.
                var box = Draw.RoundBox(x, y, 96f, 58f, 62f, 46f, 10f);
                var side = Draw.Clamp01((x - 34f) / 124f);
                var underLid = 1f - Draw.Clamp01((98f - y) / 10f);
                Layer(box, blue.Shade(-0.04f - 0.16f * side - Knit(x, y) - 0.22f * underLid - 0.08f * Draw.Clamp01((60f - y) / 50f)));

                // Крышка: светлее, блик по верхнему краю.
                var lid = Draw.RoundBox(x, y, 96f, 112f, 70f, 14f, 8f);
                var lidLit = Draw.Clamp01(1f - MathF.Abs(y - 121f) / 3f);
                Layer(lid, Rgba.Lerp(blue.Shade(0.06f - 0.12f * Draw.Clamp01((x - 26f) / 140f) - Knit(x, y) * 0.5f), Rgba.White, 0.28f * lidLit));

                // Лента по коробке и крышке: блестящая полоса посередине, тёмные края.
                var ribbon = MathF.Min(Draw.RoundBox(x, y, 96f, 66f, 10f, 60f, 3f), Draw.RoundBox(x, y, 96f, 112f, 10f, 14f, 3f));
                var stripe = Draw.Clamp01(1f - MathF.Abs(x - 93f) / 2.5f);
                Layer(ribbon, Rgba.Lerp(red.Shade(-0.04f - 0.1f * Draw.Clamp01((x - 86f) / 20f)), Rgba.White, 0.32f * stripe));

                // Бант: две петли с тёмной серединой и бликом, узелок поверх.
                for (var k = -1; k <= 1; k += 2)
                {
                    var cx = 96f + k * 24f;
                    var loop = Draw.Ellipse(x, y, cx, 140f, 22f, 15f, -20f * k);
                    var lit = Draw.Clamp01((y - 128f) / 24f);
                    var shine = Draw.Clamp01(1f - MathF.Abs(Draw.Ellipse(x, y, cx - k * 3f, 145f, 14f, 7f, -20f * k)) / 2f) * lit;
                    Layer(loop, Rgba.Lerp(red.Shade(-0.12f + 0.16f * lit), Rgba.White, 0.35f * shine));
                    Layer(Draw.Ellipse(x, y, cx + k * 2f, 139f, 10f, 5.5f, -20f * k), red.Shade(-0.45f), 0.2f);
                }

                var knot = Draw.Circle(x, y, 96f, 132f, 11f);
                var knotLit = Draw.Clamp01(1f - Draw.Circle(x, y, 92f, 136f, 4f) / 6f);
                Layer(knot, Rgba.Lerp(red.Shade(0.02f), Rgba.White, 0.35f * knotLit));
                return result;
            });
        }

        /// <summary>
        /// Монета: золото с фаской по ободу (сверху слева светлая, снизу справа тёмная), утопленное поле и выпуклый
        /// клубок с тремя витками, резкая тёмная кромка по контуру и искра блика. 128×128, рисуется втрое крупнее.
        /// </summary>
        public static Raster Coin()
        {
            const int s = 128;
            var gold = Rgba.Hex("#FFC23C");
            var deep = Rgba.Hex("#C27C08");
            return Raster.Render(s, s, 3, (x, y) =>
            {
                var d = Draw.Circle(x, y, 64f, 64f, 60f);
                var cover = Draw.Cover(d);
                if (cover <= 0f)
                {
                    return Rgba.Clear;
                }

                var nx = (x - 64f) / 60f;
                var ny = (y - 64f) / 60f;
                var lightDir = Draw.Clamp01(0.5f - 0.5f * nx + 0.5f * ny);

                // Поле монеты: чуть утоплено, к низу справа темнее.
                var c = gold.Shade(-0.04f - 0.16f * (1f - lightDir));

                // Обод шириной 9: фаска — освещённая половина светлее, теневая темнее; граница с полем чёткая.
                var rimInner = d + 9f;
                if (rimInner > -0.5f)
                {
                    var rimColor = Rgba.Lerp(deep, gold, 0.25f + 0.6f * lightDir).Shade(0.08f * lightDir);
                    var t = Draw.Clamp01((rimInner + 0.5f) / 1.2f);
                    c = Rgba.Lerp(c, rimColor, t);

                    // Тонкая тень по внутреннему краю обода (поле ниже обода).
                    c = c.Shade(-0.3f * Draw.Clamp01(1f - MathF.Abs(rimInner) / 1.4f));
                }

                // Клубок: выпуклый круг со светом сверху слева и тремя витками.
                var ball = Draw.Circle(x, y, 64f, 64f, 26f);
                if (ball < 0.5f)
                {
                    var bx = (x - 64f) / 26f;
                    var by = (y - 64f) / 26f;
                    var lit = Draw.Clamp01(0.5f - 0.45f * bx + 0.45f * by);
                    var bc = gold.Shade(-0.08f + 0.18f * lit);
                    var wrap = MathF.Min(MathF.Abs(Draw.Circle(x, y, 40f, 64f, 26f)), MathF.Abs(Draw.Circle(x, y, 88f, 64f, 26f)));
                    wrap = MathF.Min(wrap, MathF.Abs(y - 64f - (x - 64f) * 0.3f));
                    bc = Rgba.Lerp(bc, deep, Draw.Cover(wrap - 1.6f) * 0.9f);
                    c = Rgba.Lerp(c, bc, Draw.Cover(ball));
                }

                // Контур клубка — тёмная линия.
                c = Rgba.Lerp(c, deep, Draw.Cover(MathF.Abs(ball) - 1.4f));

                // Искра блика сверху слева и резкая тёмная кромка монеты.
                c = Rgba.Lerp(c, Rgba.White, MathF.Pow(Draw.Clamp01(-0.5f * nx + 0.6f * ny), 6f) * 0.55f);
                var spark = Draw.Circle(x, y, 38f, 92f, 4f);
                c = Rgba.Lerp(c, Rgba.White, 0.7f * Draw.Clamp01(1f - (spark + 4f) / 6f));
                c = Rgba.Lerp(c, deep.Shade(-0.25f), Draw.SmoothStep(-1.8f, -0.5f, d));
                return c.WithAlpha(cover);
            });
        }

        /// <summary>
        /// Пяльцы: деревянный обруч (внешний и внутренний), латунный винт-зажим сверху; середина прозрачна —
        /// под ней ткань с вышитой картинкой. 384×400 (сверху место под винт).
        /// </summary>
        public static Raster Hoop()
        {
            const int w = 384;
            const int h = 400;
            var r = new Raster(w, h);
            var wood = Rgba.Hex("#F2C48D");
            var dark = Rgba.Hex("#D9955A");
            var brass = Rgba.Hex("#E2B04A");
            const float cx = w / 2f;
            const float cy = 188f;
            r.Paint((x, y) =>
            {
                var dist = MathF.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                var result = Rgba.Clear;
                var outer = MathF.Abs(dist - 172f) - 12f;
                var inner = MathF.Abs(dist - 158f) - 4f;
                var oc = Draw.Cover(outer);
                if (oc > 0f)
                {
                    var across = (dist - 172f) / 12f;
                    var lit = Draw.Clamp01(0.5f + 0.5f * ((x - cx) * -0.5f + (y - cy) * 0.6f) / dist);
                    var c = wood.Shade(-0.25f * MathF.Abs(across) + 0.12f * lit - 0.08f);
                    c = c.Scale(0.97f + 0.05f * Draw.Noise(MathF.Atan2(y - cy, x - cx) * 40f, dist / 3f));
                    result = c.WithAlpha(oc);
                }

                var ic = Draw.Cover(inner);
                if (ic > 0f && dist < 166f)
                {
                    result = Rgba.Over(dark.WithAlpha(ic), result);
                }

                // Винт сверху: латунная колодка и барашек.
                var clamp = Draw.RoundBox(x, y, cx, cy + 176f, 22f, 16f, 6f);
                var knob = Draw.RoundBox(x, y, cx, cy + 198f, 12f, 8f, 4f);
                var cc = Draw.Cover(MathF.Min(clamp, knob));
                if (cc > 0f)
                {
                    var lit = Draw.Clamp01((y - (cy + 160f)) / 46f);
                    result = Rgba.Over(brass.Shade(-0.2f + 0.35f * lit).WithAlpha(cc), result);
                }

                return result;
            });
            return r;
        }
    }
}
