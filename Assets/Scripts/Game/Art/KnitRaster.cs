#nullable enable

using System;

namespace KnittedPicture.Game
{
    /// <summary>
    /// Рисовалки вязального полотна 2.0: петля-«галочка», живая петля на спице, спица с латунным
    /// набалдашником, кручёная нить (полоса для верёвки), бумага схемы и деревянная рамка доски.
    /// Свет сверху слева, тени цветные (густой тот же оттенок), ворс по контуру.
    /// </summary>
    public static class KnitRaster
    {
        /// <summary>Размер петли в пикселях. Петля выше клетки: верхние дольки заходят в ряд выше, как у настоящего полотна.</summary>
        public const int StitchW = 96;

        public const int StitchH = 120;

        /// <summary>Где у петли нижний кончик (доля высоты от низа): по нему петля встаёт на низ своей клетки.</summary>
        public const float StitchTip = 14f / StitchH;

        /// <summary>
        /// Лицевая петля: две пухлые дольки-«ножки», сходятся внизу в «галочку». Каждая ножка круглая
        /// в сечении и скручена из нитей (косые бороздки), сверху слева блик на гребнях нитей, между
        /// ножками шов, у кончика складка, по контуру редкий ворс. Цвет запечён.
        /// </summary>
        public static Raster Stitch(Rgba color)
        {
            var r = new Raster(StitchW, StitchH);
            const float cx = StitchW / 2f;
            var fuzz = color.Shade(-0.05f);
            r.Paint((x, y) =>
            {
                var best = float.MaxValue;
                var other = float.MaxValue;
                var value = 0f;
                var gloss = 0f;
                var coverage = 0f;
                for (var side = -1; side <= 1; side += 2)
                {
                    var d = Draw.Capsule(x, y, cx + side * 4f, 14f, cx + side * 23f, StitchH - 27f, 12f, 25f, out var t, out var across);
                    var cover = Draw.Cover(d);
                    if (d < best)
                    {
                        other = best;
                        best = d;
                    }
                    else
                    {
                        other = MathF.Min(other, d);
                    }

                    if (cover <= coverage)
                    {
                        continue;
                    }

                    var profile = MathF.Sqrt(Draw.Clamp01(1f - across * across));
                    var phase = t * 7f - across * 0.7f * side;
                    var ply = MathF.Pow(MathF.Abs(MathF.Sin(phase * MathF.PI)), 0.45f);
                    var lit = Draw.Clamp01(0.5f - 0.5f * across);
                    var v = (0.7f + 0.3f * profile) * (0.78f + 0.22f * ply) * (0.86f + 0.18f * lit);
                    v *= 1f - 0.22f * Draw.Clamp01(1f - t * 3.5f) * (1f - profile * 0.5f);
                    value = v;
                    gloss = Draw.Clamp01(profile * ply - 0.55f) * 1.3f * lit * (0.4f + 0.6f * t);
                    coverage = cover;
                }

                if (coverage > 0f)
                {
                    // Шов между ножками: там, где обе почти на одном расстоянии.
                    var seam = Draw.Clamp01(1f - MathF.Abs(best - other) / 2.2f);
                    value *= 1f - 0.28f * seam;
                    var c = new Rgba(color.R * value, color.G * value, color.B * value);
                    c = Rgba.Lerp(c, Rgba.White, gloss * 0.55f);
                    return c.WithAlpha(coverage);
                }

                // Ворс: редкие волоски у самого края.
                if (best > 0f && best < 5f && Draw.Hash((int)x, (int)y) > 0.82f)
                {
                    return fuzz.WithAlpha(0.45f * (1f - best / 5f));
                }

                return Rgba.Clear;
            });
            return r;
        }

        /// <summary>
        /// Живая петля на спице: дуга «∩» из той же кручёной нити, ножки уходят вниз к полотну.
        /// Рисуется поверх спицы, середина дуги — над ней.
        /// </summary>
        public static Raster Loop(Rgba color)
        {
            const int w = 64;
            const int h = 60;
            var r = new Raster(w, h);
            const float cx = w / 2f;
            const float cy = 26f;
            const float radius = 17f;
            const float thick = 7.5f;
            r.Paint((x, y) =>
            {
                // Центральная линия: полукруг сверху и две прямые ножки вниз.
                float d;
                float along;
                if (y >= cy)
                {
                    var dist = MathF.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    d = MathF.Abs(dist - radius);
                    along = MathF.Atan2(y - cy, x - cx) / MathF.PI;
                }
                else
                {
                    var left = MathF.Abs(x - (cx - radius));
                    var right = MathF.Abs(x - (cx + radius));
                    d = MathF.Min(left, right);
                    along = left < right ? 1f + (cy - y) / 30f : -(cy - y) / 30f;
                    if (y < 2f)
                    {
                        d = 99f;
                    }
                }

                var sd = d - thick;
                var cover = Draw.Cover(sd);
                if (cover <= 0f)
                {
                    return Rgba.Clear;
                }

                var across = Draw.Clamp(d / thick, 0f, 1f);
                var profile = MathF.Sqrt(Draw.Clamp01(1f - across * across));
                var ply = MathF.Pow(MathF.Abs(MathF.Sin((along * 5f + across * 0.4f) * MathF.PI)), 0.45f);
                var v = (0.66f + 0.34f * profile) * (0.8f + 0.2f * ply);
                var lit = y > cy + radius * 0.4f ? 0.12f : 0f;
                var c = color.Scale(v);
                c = Rgba.Lerp(c, Rgba.White, Draw.Clamp01(profile * ply - 0.6f) * 1.2f + lit * profile);
                return c.WithAlpha(cover);
            });
            return r;
        }

        /// <summary>
        /// Спица для 9-slice: светлое медовое дерево, блик вдоль, левый край заострён, правый скруглён
        /// (на нём сидит набалдашник). Высота 30 пикселей, края по 30.
        /// </summary>
        public static Raster Needle()
        {
            const int w = 120;
            const int h = 30;
            var r = new Raster(w, h);
            var light = Rgba.Hex("#FBE3B4");
            var dark = Rgba.Hex("#D9A866");
            r.Paint((x, y) =>
            {
                const float cy = h / 2f;
                const float half = 9f;
                float d;
                if (x < 30f)
                {
                    // Конический кончик: от точки слева до полной толщины.
                    var k = Draw.Clamp01((x - 2f) / 28f);
                    var thickness = 1.5f + (half - 1.5f) * MathF.Sqrt(k);
                    d = MathF.Max(MathF.Abs(y - cy) - thickness, 2f - x);
                }
                else
                {
                    d = Draw.RoundBox(x, y, (30f + w) / 2f - 1f, cy, (w - 30f) / 2f - 1f, half, half);
                }

                var cover = Draw.Cover(d);
                if (cover <= 0f)
                {
                    return Rgba.Clear;
                }

                var v = (y - cy) / half;
                var c = Rgba.Lerp(dark, light, 0.5f + 0.5f * v);
                c = Rgba.Lerp(c, Rgba.White, Draw.Clamp01(1f - MathF.Abs(v - 0.45f) * 4f) * 0.55f);
                c = c.Scale(0.97f + 0.06f * Draw.Noise(x * 0.25f, y * 2f));
                return c.WithAlpha(cover);
            });
            return r;
        }

        /// <summary>Латунный набалдашник спицы: шарик с бликом и тёплой тенью.</summary>
        public static Raster Knob()
        {
            const int s = 48;
            var r = new Raster(s, s);
            var brass = Rgba.Hex("#E2B04A");
            r.Paint((x, y) =>
            {
                var d = Draw.Circle(x, y, s / 2f, s / 2f, s / 2f - 3f);
                var cover = Draw.Cover(d);
                if (cover <= 0f)
                {
                    return Rgba.Clear;
                }

                var nx = (x - s / 2f) / (s / 2f - 3f);
                var ny = (y - s / 2f) / (s / 2f - 3f);
                var nz = MathF.Sqrt(Draw.Clamp01(1f - nx * nx - ny * ny));
                var lambert = Draw.Clamp01(-0.45f * nx + 0.55f * ny + 0.7f * nz);
                var c = brass.Shade(-0.35f * (1f - lambert));
                var spec = MathF.Pow(Draw.Clamp01(-0.35f * nx + 0.5f * ny + 0.8f * nz), 24f);
                c = Rgba.Lerp(c, Rgba.White, spec * 0.8f);
                return c.WithAlpha(cover);
            });
            return r;
        }

        /// <summary>
        /// Полоса кручёной нити для верёвки (повторяется по длине): четыре витка на плитку, круглое
        /// сечение, бороздки между нитями. Белая — красится цветом пряжи вершинами меша.
        /// </summary>
        public static Raster RopeTile()
        {
            const int w = 64;
            const int h = 16;
            var r = new Raster(w, h);
            r.Paint((x, y) =>
            {
                var across = (y - h / 2f) / (h / 2f - 1f);
                var cover = Draw.Clamp01((1f - MathF.Abs(across)) * (h / 2f - 1f) + 0.5f);
                if (cover <= 0f)
                {
                    return Rgba.Clear;
                }

                var profile = MathF.Sqrt(Draw.Clamp01(1f - across * across));
                var ply = MathF.Pow(MathF.Abs(MathF.Sin((x / w * 4f + across * 0.55f) * MathF.PI)), 0.45f);
                var v = (0.68f + 0.32f * profile) * (0.78f + 0.22f * ply);
                v += Draw.Clamp01(profile * ply - 0.6f) * 0.35f * Draw.Clamp01(across + 0.6f);
                return new Rgba(v, v, v, cover);
            });
            return r;
        }

        /// <summary>Бумага схемы: тёплый кремовый лист, едва заметные волокна и крапинки. Плитка 128×128, повторяется.</summary>
        public static Raster PaperTile()
        {
            const int s = 128;
            var r = new Raster(s, s);
            var paper = Rgba.Hex("#FBF7EF");
            r.Paint((x, y) =>
            {
                // Тайлы шума с периодом 128: плитка повторяется без шва.
                var n = Draw.Noise((x % s) / 16f, (y % s) / 16f) * 0.6f + Draw.Noise(x / 32f, y / 32f) * 0.4f;
                var speck = Draw.Hash((int)x, (int)y) > 0.998f ? 0.035f : 0f;
                return paper.Scale(1f - 0.012f * n - speck);
            });
            return r;
        }

        /// <summary>
        /// Деревянная доска под схему для 9-slice: светлый клён с волокнами вдоль, скругление, внутренняя
        /// тень по краю, блик по верхней кромке. Края по 96 пикселей, середина однородная.
        /// </summary>
        public static Raster WoodFrame()
        {
            const int s = 224;
            const float radius = 72f;
            var r = new Raster(s, s);
            var light = Rgba.Hex("#E9C792");
            var dark = Rgba.Hex("#C8955C");
            r.Paint((x, y) =>
            {
                var d = Draw.RoundBox(x, y, s / 2f, s / 2f, s / 2f - 2f, s / 2f - 2f, radius);
                var cover = Draw.Cover(d);
                if (cover <= 0f)
                {
                    return Rgba.Clear;
                }

                // Волокна идут вдоль доски; в середине (растягиваемой) — однородно по горизонтали.
                var grain = Draw.Noise(x / 60f, y / 3.2f) * 0.7f + Draw.Noise(x / 18f, y / 1.6f) * 0.3f;
                var rings = MathF.Abs(MathF.Sin((y / 7f + grain * 3f) * MathF.PI));
                var c = Rgba.Lerp(dark, light, 0.55f + 0.25f * grain + 0.2f * rings);
                var edge = Draw.SmoothStep(-10f, 0f, d);
                c = c.Shade(-0.18f * edge);
                if (y > s - 14f)
                {
                    c = Rgba.Lerp(c, Rgba.White, 0.25f * Draw.SmoothStep(s - 14f, s - 4f, y) * (1f - edge));
                }

                return c.WithAlpha(cover);
            });
            return r;
        }

        /// <summary>
        /// Строчка: пунктир по скруглённому прямоугольнику w×h пикселей (радиус radius). Штрихи равномерны
        /// по всему контуру и не растягиваются: рисуется под конкретный размер. Белая — красится.
        /// </summary>
        public static Raster DashedFrame(int w, int h, float stroke, float radius, float dash, float gap)
        {
            var r = new Raster(w, h);
            var cx = w / 2f;
            var cy = h / 2f;
            var hw = cx - stroke;
            var hh = cy - stroke;
            radius = MathF.Min(radius, MathF.Min(hw, hh));
            var a = hw - radius;
            var b = hh - radius;
            var perimeter = 4f * a + 4f * b + 2f * MathF.PI * radius;
            var count = MathF.Max(4f, MathF.Round(perimeter / (dash + gap)));
            var period = perimeter / count;
            var on = period * dash / (dash + gap);
            r.Paint((x, y) =>
            {
                var d = MathF.Abs(Draw.RoundBox(x, y, cx, cy, hw, hh, radius)) - stroke / 2f;
                var cover = Draw.Cover(d);
                if (cover <= 0f)
                {
                    return Rgba.Clear;
                }

                var along = Along(x - cx, y - cy, a, b, radius);
                var phase = along % period;
                var dashCover = Draw.Clamp01(MathF.Min(phase, on - phase) + 0.5f);
                return Rgba.White.WithAlpha(cover * dashCover);
            });
            return r;
        }

        /// <summary>Квадратная строчка (рамка клетки, кольцо при radius = половине стороны).</summary>
        public static Raster DashedFrame(int size, float stroke, float radius, float dash, float gap) =>
            DashedFrame(size, size, stroke, radius, dash, gap);

        /// <summary>Длина пути по контуру скруглённого прямоугольника до ближайшей к точке (px, py) точки контура.</summary>
        private static float Along(float px, float py, float a, float b, float r)
        {
            var arc = MathF.PI * r / 2f;
            if (MathF.Abs(px) <= a)
            {
                return py >= 0f ? px + a : 2f * a + arc + 2f * b + arc + (a - px);
            }

            if (MathF.Abs(py) <= b)
            {
                return px > 0f ? 2f * a + arc + (b - py) : 4f * a + 3f * arc + 2f * b + (py + b);
            }

            // Угол: дуга вокруг центра угла, по часовой.
            var sx = px > 0f ? 1f : -1f;
            var sy = py > 0f ? 1f : -1f;
            var angle = MathF.Atan2((py - sy * b) * sy, (px - sx * a) * sx);
            var t = angle / (MathF.PI / 2f);
            if (sx > 0f && sy > 0f)
            {
                return 2f * a + (1f - t) * arc;
            }

            if (sx > 0f)
            {
                return 2f * a + arc + 2f * b + t * arc;
            }

            if (sy < 0f)
            {
                return 4f * a + 2f * arc + 2f * b + (1f - t) * arc;
            }

            return 4f * a + 3f * arc + 4f * b + t * arc;
        }

        /// <summary>
        /// Клетка связанной картинки (старый дизайн, детальнее): скруглённый квадрат, в нём лицевая петля —
        /// две пухлые кручёные дольки «галочкой», шов между ними, складка у кончика, тёмная кромка.
        /// Серая — красится цветом пряжи (вспышки и волны работают через цвет), блики — отдельный слой.
        /// </summary>
        public static Raster StitchTile(bool gloss)
        {
            const int s = 96;
            const float cx = s / 2f;
            var r = new Raster(s, s);
            r.Paint((x, y) =>
            {
                var box = Draw.RoundBox(x, y, cx, cx, cx - 1f, cx - 1f, 20f);
                var cover = Draw.Cover(box);
                if (cover <= 0f)
                {
                    return Rgba.Clear;
                }

                var best = float.MaxValue;
                var other = float.MaxValue;
                var value = 0.74f;
                var shine = 0f;
                for (var side = -1; side <= 1; side += 2)
                {
                    var d = Draw.Capsule(x, y, cx + side * 3f, 12f, cx + side * 24f, s - 16f, 11f, 22f, out var t, out var across);
                    if (d < best)
                    {
                        other = best;
                        best = d;
                        if (d < 0.5f)
                        {
                            var profile = MathF.Sqrt(Draw.Clamp01(1f - across * across));
                            var ply = MathF.Pow(MathF.Abs(MathF.Sin((t * 6f - across * 0.7f * side) * MathF.PI)), 0.45f);
                            var lit = Draw.Clamp01(0.5f - 0.5f * across);
                            value = (0.8f + 0.2f * profile) * (0.86f + 0.14f * ply) * (0.9f + 0.12f * lit);
                            value *= 1f - 0.2f * Draw.Clamp01(1f - t * 3.5f) * (1f - profile * 0.5f);
                            shine = Draw.Clamp01(profile * ply - 0.55f) * 1.4f * lit * (0.4f + 0.6f * t);
                        }
                    }
                    else
                    {
                        other = MathF.Min(other, d);
                    }
                }

                if (best >= 0.5f)
                {
                    // Между петлями — глубина полотна: темнее.
                    value = 0.72f;
                }
                else
                {
                    value *= 1f - 0.25f * Draw.Clamp01(1f - MathF.Abs(best - other) / 2.2f);
                }

                // Кромка клетки чуть темнее: клетки читаются отдельно, полотно — объёмно.
                value *= 1f - 0.16f * Draw.SmoothStep(-7f, 0f, box);
                if (gloss)
                {
                    return Rgba.White.WithAlpha(shine * 0.6f * cover);
                }

                var v = Draw.Clamp01(value * 1.16f);
                return new Rgba(v, v, v, cover);
            });
            return r;
        }

        /// <summary>Клетка ткани-основы: гладкая канва из переплетённых нитей, серая (красится), скруглённая.</summary>
        public static Raster CanvasTile()
        {
            const int s = 96;
            var r = new Raster(s, s);
            r.Paint((x, y) =>
            {
                var box = Draw.RoundBox(x, y, s / 2f, s / 2f, s / 2f - 1f, s / 2f - 1f, 20f);
                var cover = Draw.Cover(box);
                if (cover <= 0f)
                {
                    return Rgba.Clear;
                }

                // Полотняное переплетение: нити по очереди сверху и снизу.
                var cellX = (int)MathF.Floor(x / 8f);
                var cellY = (int)MathF.Floor(y / 8f);
                var u = (x % 8f) / 8f - 0.5f;
                var w = (y % 8f) / 8f - 0.5f;
                var horizontal = (cellX + cellY) % 2 == 0;
                var across = horizontal ? w : u;
                var thread = MathF.Sqrt(Draw.Clamp01(1f - across * across * 4f));
                var v = 0.84f + 0.16f * thread;
                v *= 0.97f + 0.05f * Draw.Hash((int)x, (int)y);
                v *= 1f - 0.12f * Draw.SmoothStep(-7f, 0f, box);
                return new Rgba(v, v, v, cover);
            });
            return r;
        }

        /// <summary>Искра: мягкая четырёхлучевая звёздочка с ярким центром. Белая — красится.</summary>
        public static Raster Sparkle()
        {
            const int s = 64;
            var r = new Raster(s, s);
            r.Paint((x, y) =>
            {
                var dx = MathF.Abs(x - s / 2f) / (s / 2f);
                var dy = MathF.Abs(y - s / 2f) / (s / 2f);
                var star = MathF.Pow(dx, 0.5f) + MathF.Pow(dy, 0.5f);
                var a = Draw.Clamp01((1f - star) * 3f);
                var core = Draw.Clamp01(1f - MathF.Sqrt(dx * dx + dy * dy) * 3.5f);
                return Rgba.White.WithAlpha(Draw.Clamp01(a + core));
            });
            return r;
        }
    }
}
