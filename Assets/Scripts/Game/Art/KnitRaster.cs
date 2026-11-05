 #nullable enable

using System;
using System.Collections.Generic;

namespace KnittedPicture.Game
{
    /// <summary>
    /// Рисовалки вязального полотна 2.0: петля-«галочка», живая петля на спице, спица с латунным
    /// набалдашником, кручёная нить (полоса для верёвки), бумага схемы и деревянная рамка доски.
    /// Свет сверху слева, тени цветные (густой тот же оттенок), ворс по контуру.
    /// </summary>
    public static class KnitRaster
    {
        /// <summary>Непрозрачность контактной тени на границе готовой пряжи и канвы.</summary>
        public const float ReliefAlpha = 0.16f;
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
        /// Бесшовный участок тонкой вязки: девять петель внутри игровой клетки без фаски по её периметру.
        /// Серая основа красится цветом пряжи, узкие мягкие блики лежат отдельным слоем.
        /// </summary>
        public static Raster StitchTile(bool gloss)
        {
            const int s = 96;
            const float cx = s / 2f;
            return Raster.Render(s, s, 2, (x, y) =>
            {
                // Цветная область состоит из тонкой вязки, а не одной выпуклой плитки.
                // Три повторения по каждой оси дают непрерывную фактуру на соседних клетках.
                x = (x % 32f) * 3f;
                y = (y % 32f) * 3f;
                const float cover = 1f;

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
                            // Контраст долек и кручения выше: петля читается и в маленькой клетке.
                            var fibre = MathF.Sin((t * 29f + across * 2.1f * side) * MathF.PI) * 0.018f * profile;
                            value = (0.68f + 0.32f * profile) * (0.8f + 0.2f * ply) * (0.88f + 0.14f * lit) + fibre;
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
                    // Между петлями — глубина полотна: заметно темнее.
                    value = 0.6f;
                }
                else
                {
                    // Шов между дольками — глубокая чёткая бороздка.
                    value *= 1f - 0.42f * Draw.Clamp01(1f - MathF.Abs(best - other) / 3f);
                }

                if (gloss)
                {
                    return Rgba.White.WithAlpha(shine * 0.28f * cover);
                }

                // Средняя яркость — как была: цвет петли должен совпадать с цветом клубка, по нему их сравнивают.
                var v = Draw.Clamp01(value * 1.27f);
                return new Rgba(v, v, v, cover);
            });
        }

        /// <summary>
        /// Клетка ткани-основы: непрерывная канва полотняного переплетения, серая (красится). Нити основы
        /// и утка по очереди сверху и снизу: верхняя — круглая трубочка, нижняя у пересечения в тени.
        /// Тон каждой нити чуть свой и плавно гуляет вдоль неё (утолщения пряжи), без пиксельного шума.
        /// </summary>
        /// <summary>Нейтральное полотно: одна периодическая модель формы для цвета и отдельного блика.</summary>
        public static Raster TextileTile(int variant, bool coarse, bool gloss)
        {
            if (variant == 2)
            {
                // Одна крупная ёлочка на карту: 64 единицы модели при разрешении 128 пикселей.
                return Raster.Render(64, 64, 2, (x, y) =>
                {
                    SampleTextile(x, y, variant, coarse, out var body, out var shine);
                    return gloss ? Rgba.White.WithAlpha(shine) : new Rgba(body, body, body, 1f);
                });
            }
            // Продольные нити: вычисляем один период, затем собираем карту 128×128.
            const int period = 32;
            var tile = Raster.Render(period / 2, period / 2, 2, (x, y) =>
            {
                SampleTextile(x * 2f, y * 2f, variant, coarse, out var body, out var shine);
                return gloss ? Rgba.White.WithAlpha(shine) : new Rgba(body, body, body, 1f);
            });
            var result = new Raster(128, 128);
            for (var y = 0; y < result.Height; y++)
            for (var x = 0; x < result.Width; x++)
            {
                result[x, y] = tile[x % period, y % period];
            }
            return result;
        }

        /// <summary>Один материал на связную область исходного цвета; загадка не раскрывает скрытую форму.</summary>
        public static int[] TextileVariants(int width, int height, Func<int, int, int> colorAt, Func<int, int, bool> isFabric, bool mystery)
        {
            var variants = new int[width * height];
            var visited = new bool[variants.Length];
            var queue = new Queue<int>();
            var component = new List<int>();
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var start = (y * width + x);
                    if (visited[start] || isFabric(x, y)) { continue; }
                    var color = colorAt(x, y);
                    var minX = x;
                    var maxX = x;
                    var minY = y;
                    var maxY = y;
                    component.Clear();
                    queue.Enqueue(start);
                    visited[start] = true;
                    while (queue.Count > 0)
                    {
                        var key = queue.Dequeue();
                        var px = key % width;
                        var py = key / width;
                        component.Add(key);
                        minX = Math.Min(minX, px);
                        maxX = Math.Max(maxX, px);
                        minY = Math.Min(minY, py);
                        maxY = Math.Max(maxY, py);
                        Visit(px - 1, py);
                        Visit(px + 1, py);
                        Visit(px, py - 1);
                        Visit(px, py + 1);
                    }
                    var w = maxX - minX + 1;
                    var h = maxY - minY + 1;
                    var variant = mystery ? color % 3 : w > h * 1.5f ? 2 : h > w * 1.5f ? 0 : 1;
                    foreach (var key in component) { variants[key] = variant; }

                    void Visit(int px, int py)
                    {
                        if (px < 0 || py < 0 || px >= width || py >= height) { return; }
                        var key = (py * width + px);
                        if (visited[key] || isFabric(px, py) || colorAt(px, py) != color) { return; }
                        visited[key] = true;
                        queue.Enqueue(key);
                    }
                }
            }
            return variants;
        }

        /// <summary>Направленные волокна с круглым сечением и единым светом в координатах полотна.</summary>
        private static void SampleTextile(float x, float y, int variant, bool coarse, out float body, out float gloss)
        {
            if (variant == 2)
            {
                SampleHerringbone(x, y, coarse, out body, out gloss);
                return;
            }
            const float pitch = 32f;
            float coordinate;
            float along;
            float nxAxis;
            float nyAxis;
            if (variant == 0)
            {
                // Четыре длинные вертикальные нити на клетку.
                coordinate = x;
                along = y;
                nxAxis = 1f;
                nyAxis = 0f;
            }
            else
            {
                coordinate = x + y;
                along = y - x;
                nxAxis = 0.7071068f;
                nyAxis = 0.7071068f;
            }

            var ripple = MathF.Sin(y * (2f * MathF.PI / 32f)) * 0.35f;
            var phase = (coordinate + ripple) / pitch;
            var across = 2f * (phase - MathF.Floor(phase)) - 1f;
            var magnitude = MathF.Abs(across);
            var profile = MathF.Sqrt(Draw.Clamp01(1f - across * across));
            var normalAcross = across;
            var turn = along * (2f * MathF.PI / 32f);
            normalAcross = Draw.Clamp(across + (coarse ? 0.07f : 0.13f) * MathF.Sin(across * 9f + turn) * profile, -1f, 1f);
            // Узкая контактная тень между нитями не оставляет прозрачных прорезей.
            var groove = Draw.SmoothStep(0.84f, 1f, magnitude);
            var micro = 0f;
            if (!coarse)
            {
                // Косая скрутка и тонкие волокна внутри сечения, с целым числом периодов на карту.
                var twist = turn;
                micro = 0.055f * MathF.Sin(across * 7f + twist) * profile;
                micro += 0.012f * MathF.Sin(across * 14f - twist * 2f) * profile;
            }
            var normalZ = MathF.Sqrt(Draw.Clamp01(1f - normalAcross * normalAcross));
            var light = Draw.Clamp01(-0.38f * nxAxis * normalAcross + 0.48f * nyAxis * normalAcross + 0.79f * normalZ);
            body = Draw.Clamp01(0.65f + 0.18f * profile + 0.10f * light - 0.06f * groove + micro);
            gloss = Draw.Clamp01(profile * light * 0.055f);
        }

        /// <summary>Отдельные округлые стежки ёлочки перекрываются у соседних рядов.</summary>
        private static void SampleHerringbone(float x, float y, bool coarse, out float body, out float gloss)
        {
            var px = x - MathF.Floor(x / 64f) * 64f;
            var py = y - MathF.Floor(y / 64f) * 64f;
            void Layer(float shift, out float value, out float shine, out float opacity)
            {
                var best = -1f;
                var nx = 0f;
                var ny = 0f;
                var along = 0f;
                var tangentX = 0f;
                var tangentY = 1f;
                opacity = 0f;
                for (var row = -2; row <= 2; row++)
                for (var col = -1; col <= 1; col++)
                for (var side = -1; side <= 1; side += 2)
                {
                    var offset = (row & 1) == 0 ? 0f : 32f;
                    var ax = 32f + col * 64f + offset + shift + side * 2f;
                    var ay = row * 32f - 2f + shift + (side > 0 ? 2f : 0f);
                    var bx = 32f + col * 64f + offset + shift + side * 24f;
                    var by = row * 32f + 58f + shift + (side > 0 ? 2f : 0f);
                    const float radius = 9.5f;
                    const float extent = radius + 0.5f;
                    if (px < MathF.Min(ax, bx) - extent || px > MathF.Max(ax, bx) + extent ||
                        py < MathF.Min(ay, by) - extent || py > MathF.Max(ay, by) + extent)
                    {
                        continue;
                    }
                    var distance = Draw.Capsule(px, py, ax, ay, bx, by, radius, radius, out var t, out _);
                    var cover = Draw.Clamp01(0.5f - distance);
                    if (cover <= 0f) { continue; }
                    var dx = (px - (ax + (bx - ax) * t)) / radius;
                    var dy = (py - (ay + (by - ay) * t)) / radius;
                    var height = (0.2f + 0.8f * MathF.Sqrt(Draw.Clamp01(1f - dx * dx - dy * dy)) + (side > 0 ? 0.03f : 0f)) * cover;
                    if (height > best)
                    {
                        var length = MathF.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay));
                        best = height; nx = dx; ny = dy; along = t; opacity = cover;
                        tangentX = (bx - ax) / length;
                        tangentY = (by - ay) / length;
                    }
                }
                if (best < 0f) { value = 0.78f; shine = 0f; return; }
                var z = MathF.Sqrt(Draw.Clamp01(1f - nx * nx - ny * ny));
                {
                    var across = -tangentY * nx + tangentX * ny;
                    var helix = along * 8f * MathF.PI + across * 7f;
                    var shiftNormal = (coarse ? 0.07f : 0.14f) * MathF.Sin(helix) * z;
                    nx -= tangentY * shiftNormal;
                    ny += tangentX * shiftNormal;
                    var length = MathF.Sqrt(nx * nx + ny * ny + z * z);
                    nx /= length;
                    ny /= length;
                    z /= length;
                }
                var light = Draw.Clamp01(-0.38f * nx + 0.48f * ny + 0.79f * z);
                var twist = coarse ? 0f : 0.035f * MathF.Sin(along * 8f * MathF.PI + nx * 7f) * z;
                value = 0.64f + 0.21f * z + 0.1f * light + twist;
                shine = z * light * 0.055f;
            }

            Layer(16f, out var lower, out var lowerShine, out var lowerCover);
            Layer(0f, out var upper, out var upperShine, out var upperCover);
            var under = Draw.Lerp(0.78f, lower * 0.96f, lowerCover);
            body = Draw.Clamp01(Draw.Lerp(under, upper, upperCover));
            gloss = Draw.Lerp(lowerShine * lowerCover * 0.7f, upperShine, upperCover);
        }

        public static Raster CanvasTile()
        {
            const int s = 96;
            const float period = 12f;
            return Raster.Render(s, s, 2, (x, y) =>
            {
                const float cover = 1f;

                var u = x / period;
                var w = y / period;
                var i = (int)MathF.Floor(u);
                var j = (int)MathF.Floor(w);
                var fu = u - i;
                var fw = w - j;

                // Уток идёт по горизонтали (ряд j), основа — по вертикали (столбец i).
                var weftProfile = MathF.Sqrt(Draw.Clamp01(1f - MathF.Pow(2f * fw - 1f, 2f) * 1.15f));
                var warpProfile = MathF.Sqrt(Draw.Clamp01(1f - MathF.Pow(2f * fu - 1f, 2f) * 1.15f));
                var weftOver = ((i + j) & 1) == 0;

                // Нить, что сверху, выгибается горбиком над пересечением; нижняя уходит в тень у краёв верхней.
                var arch = MathF.Sin(MathF.PI * (weftOver ? fu : fw));
                var topProfile = weftOver ? weftProfile : warpProfile;
                var underProfile = weftOver ? warpProfile : weftProfile;
                var tone = weftOver
                    ? 0.03f * (Draw.Noise(x / 9f, j * 3.7f) - 0.5f) + 0.03f * (Draw.Hash(j, 17) - 0.5f)
                    : 0.03f * (Draw.Noise(i * 3.7f, y / 9f) - 0.5f) + 0.03f * (Draw.Hash(i, 29) - 0.5f);
                float v;
                if (topProfile > 0.02f)
                {
                    v = 0.82f + 0.16f * topProfile * (0.75f + 0.25f * arch) + tone;

                    // Свет сверху слева: верхняя кромка нити светлее.
                    var slope = weftOver ? 2f * fw - 1f : 1f - 2f * fu;
                    v += 0.03f * slope * topProfile;
                }
                else
                {
                    v = 0.81f + 0.07f * underProfile + tone;
                }

                // Щели между нитями — глубже.
                v -= 0.09f * (1f - Draw.SmoothStep(0f, 0.35f, MathF.Max(topProfile, underProfile)));

                // Без фаски по границе игровой клетки: соседние области выглядят общей тканью.
                v = Draw.Clamp01(v * 1.05f);
                return new Rgba(v, v, v, cover);
            });
        }

        /// <summary>
        /// Положение по высоте для плашки «девять частей» 0..1: нижние углы 0..0,5, середина ровно 0,5, верхние
        /// углы 0,5..1. Плавно по всей высоте — без ступеньки посередине (у растянутой середины ровный тон).
        /// </summary>
        private static float SliceT(float y, int size, int border)
        {
            if (y < border)
            {
                return 0.5f * y / border;
            }

            if (y >= size - border)
            {
                return 0.5f + 0.5f * (y - (size - border)) / border;
            }

            return 0.5f;
        }

        /// <summary>
        /// Карточка-«подушка» для Image.Type.Sliced (белая, красится цветом карточки): сверху светлее, к низу
        /// чуть темнее, по верхнему краю тонкий блик, по нижнему мягкая тень внутрь. Перелив целиком в
        /// растягиваемой середине, поэтому он идёт на всю высоту карточки любого размера. r — радиус в пикселях.
        /// </summary>
        public static Raster SoftCard(int r)
        {
            var size = r * 2 + 4;
            var border = r + 1;
            var half = size / 2f;
            var raster = new Raster(size, size);

            // Ширины в пикселях текстуры (3 px = 1 точка макета): кромка — чуть меньше точки, чтобы край был резким.
            const float rim = 2.2f;
            raster.Paint((x, y) =>
            {
                var d = Draw.RoundBox(x, y, half, half, half, half, r);
                var cover = Draw.Cover(d);
                if (cover <= 0f)
                {
                    return Rgba.Clear;
                }

                // Тело: сверху светлее, к низу темнее — объём заметнее, чем был (0,985 → 0,945).
                var t = SliceT(y, size, border);
                var v = Draw.Lerp(0.93f, 1f, t);
                var inside = -d;
                var up = Draw.Clamp01((y - half) / half * 2f);
                var down = Draw.Clamp01((half - y) / half * 2f);

                // Блик по верхнему краю — тонкая светлая полоса сразу под кромкой.
                v = Draw.Lerp(v, 1f, up * Draw.Clamp01(1f - MathF.Abs(inside - (rim + 1.6f)) / 1.3f));

                // Тень у нижнего края внутри: дно плашки «уходит» вниз.
                v *= 1f - 0.08f * down * (1f - Draw.SmoothStep(0f, 7f, inside));

                // Кромка: резкая тёмная линия по всему контуру — плашка не расплывается на светлом фоне.
                var edge = 1f - Draw.SmoothStep(rim - 0.8f, rim + 0.4f, inside);
                v *= 1f - 0.16f * edge;
                return new Rgba(v, v, v, cover);
            });
            return raster;
        }

        /// <summary>
        /// Вдавленная плашка для Image.Type.Sliced (дорожки полосок, слоты станка, лоток, переключатели, ячейки):
        /// сверху внутри тень от края, к низу светлее, по нижнему краю тонкая светлая кромка. Белая — красится.
        /// </summary>
        public static Raster Inset(int r)
        {
            var size = r * 2 + 4;
            var border = r + 1;
            var half = size / 2f;
            var raster = new Raster(size, size);
            raster.Paint((x, y) =>
            {
                var d = Draw.RoundBox(x, y, half, half, half, half, r);
                var cover = Draw.Cover(d);
                if (cover <= 0f)
                {
                    return Rgba.Clear;
                }

                var t = SliceT(y, size, border);
                var v = Draw.Lerp(0.975f, 0.9f, t);
                var inside = -d;
                var up = Draw.Clamp01((y - half) / half * 2f);
                var down = Draw.Clamp01((half - y) / half * 2f);

                // Тень от верхнего края внутрь — глубже, чем была; по контуру резкая тёмная кромка.
                v *= 1f - 0.22f * up * (1f - Draw.SmoothStep(0f, MathF.Min(7f, r * 0.9f + 1f), inside));
                v *= 1f - 0.12f * (1f - Draw.SmoothStep(1.2f, 2.4f, inside));
                v = Draw.Lerp(v, 1f, down * Draw.Clamp01(1f - MathF.Abs(inside - 2.6f) / 1.2f));
                return new Rgba(v, v, v, cover);
            });
            return raster;
        }

        /// <summary>
        /// Глянцевая («леденец») плашка для заполнений полосок: заметно темнее к низу; блик даёт отдельный слой
        /// <see cref="GlossOverlay"/>, потому что окраска цветом не может сделать светлее белого.
        /// </summary>
        public static Raster Gloss(int r)
        {
            var size = r * 2 + 4;
            var border = r + 1;
            var half = size / 2f;
            var raster = new Raster(size, size);
            raster.Paint((x, y) =>
            {
                var d = Draw.RoundBox(x, y, half, half, half, half, r);
                var cover = Draw.Cover(d);
                if (cover <= 0f)
                {
                    return Rgba.Clear;
                }

                var t = SliceT(y, size, border);
                var v = Draw.Lerp(0.8f, 1f, Draw.SmoothStep(0f, 1f, (y - 0.5f) / (size - 1f)) * 0.5f + t * 0.5f);
                var inside = -d;
                var down = Draw.Clamp01((half - y) / half * 2f);
                v *= 1f - 0.1f * down * (1f - Draw.SmoothStep(0f, 3f, inside));
                return new Rgba(v, v, v, cover);
            });
            return raster;
        }

        /// <summary>Блик «леденца»: белая полупрозрачная полоса в верхней половине, со скруглением как у плашки.</summary>
        public static Raster GlossOverlay(int r)
        {
            var size = r * 2 + 4;
            var half = size / 2f;
            var raster = new Raster(size, size);
            raster.Paint((x, y) =>
            {
                var hh = MathF.Max(1f, half * 0.42f);
                var cy = size - 1.4f - hh;
                var d = Draw.RoundBox(x, y, half, cy, half - 2.2f, hh, MathF.Max(1f, r * 0.7f));
                var cover = Draw.Cover(d);
                if (cover <= 0f)
                {
                    return Rgba.Clear;
                }

                // Блик плотнее к верху и с чётким нижним краем: глянец читается, а не «туманит» плашку.
                var fade = Draw.Clamp01((y - (cy - hh)) / (2f * hh));
                return Rgba.White.WithAlpha(cover * (0.16f + 0.34f * fade));
            });
            return raster;
        }

        /// <summary>Круглая выпуклая «пуговица» (кнопки паузы, настроек): сверху светлее, по краю мягкий обод.</summary>
        public static Raster SoftDisc()
        {
            const int s = 64;
            return Raster.Render(s, s, 3, (x, y) =>
            {
                const float c = s / 2f;
                var d = Draw.Circle(x, y, c, c, c - 1f);
                var cover = Draw.Cover(d);
                if (cover <= 0f)
                {
                    return Rgba.Clear;
                }

                var ny = (y - c) / c;
                var inside = -d;
                var v = Draw.Lerp(0.88f, 1f, 0.5f + 0.5f * ny);
                v = Draw.Lerp(v, 1f, Draw.Clamp01(ny * 2f) * Draw.Clamp01(1f - MathF.Abs(inside - 2.4f) / 1.2f));
                v *= 1f - 0.1f * Draw.Clamp01(-ny * 2f) * (1f - Draw.SmoothStep(0f, 6f, inside));

                // Резкий обод: пуговица не сливается с белым фоном.
                v *= 1f - 0.18f * (1f - Draw.SmoothStep(0.6f, 1.6f, inside));
                return new Rgba(v, v, v, cover);
            });
        }

        /// <summary>Выпуклая клетка картинки (эскиз и фронт): 48×48, скругление 11, свет сверху. Белая — красится.</summary>
        public static Raster SoftTile()
        {
            const int s = 48;
            return Raster.Render(s, s, 2, (x, y) =>
            {
                var d = Draw.RoundBox(x, y, s / 2f, s / 2f, s / 2f, s / 2f, 11f);
                var cover = Draw.Cover(d);
                if (cover <= 0f)
                {
                    return Rgba.Clear;
                }

                var ny = (y - s / 2f) / (s / 2f);
                var inside = -d;
                var v = Draw.Lerp(0.9f, 1f, 0.5f + 0.5f * ny);
                v *= 1f - 0.06f * Draw.Clamp01(-ny * 2f) * (1f - Draw.SmoothStep(0f, 5f, inside));
                return new Rgba(v, v, v, cover);
            });
        }

        /// <summary>
        /// Бирка на ниточке для числа: бумажный ярлычок с дырочкой слева, из неё к клубку уходит нитка.
        /// Белая (красится цветом), бумага чуть светлее сверху, мягкая тень снизу. 72×40 точек, тело — правые 58.
        /// </summary>
        public static Raster Tag()
        {
            const int w = 72;
            const int h = 40;
            return Raster.Render(w, h, 3, (x, y) =>
            {
                var result = Rgba.Clear;

                // Тень под ярлычком.
                var shadowD = Draw.RoundBox(x, y + 1.6f, 44f, 20f, 25f, 14f, 7f);
                var shadow = Draw.Clamp01(1f - (shadowD + 1f) / 3.5f);
                if (shadow > 0f)
                {
                    result = new Rgba(0.1f, 0.14f, 0.2f, 0.16f * shadow);
                }

                // Нитка из дырочки к клубку: вверх-влево, мягкой дугой.
                var thread = MathF.Min(Draw.Segment(x, y, 24f, 21f, 14f, 27f), Draw.Segment(x, y, 14f, 27f, 2f, 30f));
                var threadCover = Draw.Cover(thread - 1.1f);
                if (threadCover > 0f)
                {
                    result = Rgba.Over(new Rgba(0.4f, 0.43f, 0.48f, threadCover), result);
                }

                // Ярлычок: скошенный слева, как бирка.
                var body = Draw.RoundBox(x, y, 44f, 20f, 25f, 14f, 7f);
                var tip = Draw.RoundBox(x, y, 21f, 20f, 6f, 9f, 5f);
                var d = MathF.Min(body, tip);
                var cover = Draw.Cover(d);
                if (cover > 0f)
                {
                    var v = Draw.Lerp(0.93f, 1f, Draw.Clamp01((y - 6f) / 26f));
                    v *= 1f - 0.06f * (1f - Draw.SmoothStep(0f, 3f, -d)) * Draw.Clamp01((20f - y) / 10f);
                    var paper = new Rgba(v, v, v, cover);

                    // Дырочка с колечком.
                    var hole = Draw.Circle(x, y, 22f, 20f, 2.6f);
                    if (hole < 1.6f)
                    {
                        var ring = Draw.Clamp01(1f - MathF.Abs(hole - 0.6f) / 1.1f);
                        paper = Rgba.Lerp(paper, new Rgba(0.7f, 0.72f, 0.76f, cover), ring);
                        paper = paper.WithAlpha(cover * Draw.Clamp01(hole + 0.5f));
                    }

                    result = Rgba.Over(paper, result);
                    if (threadCover > 0f && x < 23f)
                    {
                        // Нитка проходит сквозь дырочку и ложится поверх бумаги у края.
                        result = Rgba.Over(new Rgba(0.4f, 0.43f, 0.48f, threadCover * 0.9f), result);
                    }
                }

                return result;
            });
        }

        /// <summary>Лунка под значок бустера: мягкая светло-серая чаша, тень внутри сверху, светлая кромка снизу.</summary>
        public static Raster IconWell()
        {
            const int s = 132;
            var inside = Rgba.Hex("#EDF0F4");
            var shade = Rgba.Hex("#CED5DE");
            return Raster.Render(s / 3, s / 3, 3, (x, y) =>
            {
                const float c = s / 6f;
                var d = Draw.Circle(x, y, c, c, c - 1f);
                var cover = Draw.Cover(d);
                if (cover <= 0f)
                {
                    return Rgba.Clear;
                }

                var ny = (y - c) / c;
                var col = Rgba.Lerp(inside, shade, Draw.Clamp01(ny * 0.9f + 0.1f) * Draw.SmoothStep(-9f, -1f, d) + Draw.Clamp01(ny) * 0.25f);
                col = Rgba.Lerp(col, Rgba.White, Draw.Clamp01(-ny) * Draw.SmoothStep(-4f, -0.5f, d) * 0.8f);
                return col.WithAlpha(cover);
            });
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
