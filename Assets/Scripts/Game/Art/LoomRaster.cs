#nullable enable

using System;

namespace KnittedPicture.Game
{
    /// <summary>
    /// Станок 2.0 и бустеры: деревянный брусок-«таблетка» (9-slice), выточенная лунка под клубок,
    /// свечение лунки (последнее место, мест нет, «Разгон»), две спицы на станке; цветные иконки
    /// бустеров — латунные ножницы, фиолетовая стрелка отмены из нити, лунка с плюсом, часы.
    /// </summary>
    public static class LoomRaster
    {
        /// <summary>Брусок станка для 9-slice: медовое дерево, блик сверху, тень снизу, края 96 пикселей.</summary>
        public static Raster Bar()
        {
            const int w = 224;
            const int h = 192;
            var r = new Raster(w, h);
            var light = Rgba.Hex("#F2CF97");
            var dark = Rgba.Hex("#D59A58");
            r.Paint((x, y) =>
            {
                var d = Draw.RoundBox(x, y, w / 2f, h / 2f + 6f, w / 2f - 2f, h / 2f - 8f, 90f);
                var shadow = Draw.RoundBox(x, y, w / 2f, h / 2f - 2f, w / 2f - 4f, h / 2f - 8f, 90f);
                var cover = Draw.Cover(d);
                if (cover <= 0f)
                {
                    // Тень-подложка снизу.
                    var s = Draw.Cover(shadow);
                    return s > 0f ? dark.Shade(-0.35f).WithAlpha(s) : Rgba.Clear;
                }

                var v = (y - 14f) / (h - 20f);
                var grain = Draw.Noise(x / 70f, y / 4f) * 0.7f + Draw.Noise(x / 20f, y / 2f) * 0.3f;
                var c = Rgba.Lerp(dark, light, 0.35f + 0.5f * v + 0.15f * grain);
                c = c.Shade(-0.15f * Draw.SmoothStep(-8f, 0f, d));
                var shine = Draw.Clamp01(1f - MathF.Abs(y - (h - 26f)) / 5f) * Draw.Clamp01((-d - 10f) / 10f);
                c = Rgba.Lerp(c, Rgba.White, shine * 0.45f);
                return c.WithAlpha(cover);
            });
            return r;
        }

        /// <summary>Лунка под клубок: выточенная чаша, тень сверху внутри, светлая кромка снизу.</summary>
        public static Raster Well()
        {
            const int s = 128;
            var r = new Raster(s, s);
            var inside = Rgba.Hex("#D9A266");
            var rim = Rgba.Hex("#F8DDB0");
            r.Paint((x, y) =>
            {
                var d = Draw.Circle(x, y, s / 2f, s / 2f, s / 2f - 4f);
                var cover = Draw.Cover(d);
                if (cover <= 0f)
                {
                    return Rgba.Clear;
                }

                var ny = (y - s / 2f) / (s / 2f);
                var depth = Draw.Clamp01(0.5f + 0.5f * ny);
                var c = inside.Shade(-0.25f * depth + 0.1f * (1f - depth));
                var edge = Draw.SmoothStep(-7f, -1f, d);
                c = Rgba.Lerp(c, ny < 0f ? rim : inside.Shade(-0.3f), edge);
                return c.WithAlpha(cover);
            });
            return r;
        }

        /// <summary>Мягкое кольцо-свечение лунки: белое, красится цветом тревоги или «Разгона».</summary>
        public static Raster Glow()
        {
            const int s = 128;
            var r = new Raster(s, s);
            r.Paint((x, y) =>
            {
                var d = Draw.Circle(x, y, s / 2f, s / 2f, 44f);
                var ring = MathF.Abs(d) - 3f;
                var a = Draw.Cover(ring) * 0.95f + Draw.Clamp01(1f - MathF.Abs(d) / 18f) * 0.45f;
                return Rgba.White.WithAlpha(Draw.Clamp01(a));
            });
            return r;
        }

        /// <summary>Спица станка: тонкая, с латунным шариком на конце (лежит наискосок над бруском).</summary>
        public static Raster Needle()
        {
            const int w = 256;
            const int h = 24;
            var r = new Raster(w, h);
            var wood = Rgba.Hex("#E9B977");
            var brass = Rgba.Hex("#D9A441");
            r.Paint((x, y) =>
            {
                var rod = Draw.Capsule(x, y, 10f, h / 2f, w - 22f, h / 2f, 1.2f, 3.6f, out var t, out var across);
                var knob = Draw.Circle(x, y, w - 14f, h / 2f, 8f);
                var result = Rgba.Clear;
                var rc = Draw.Cover(rod);
                if (rc > 0f)
                {
                    var c = wood.Shade(-0.25f * across + 0.1f);
                    result = c.WithAlpha(rc);
                }

                var kc = Draw.Cover(knob);
                if (kc > 0f)
                {
                    var lit = Draw.Clamp01((y - h / 2f + 8f) / 16f);
                    result = Rgba.Over(brass.Shade(-0.25f + 0.4f * lit).WithAlpha(kc), result);
                }

                return result;
            });
            return r;
        }

        public enum BoosterArt
        {
            Scissors,
            Undo,
            Slot,
            Time,
        }

        /// <summary>Цветная иконка бустера 128×128.</summary>
        public static Raster Booster(BoosterArt art)
        {
            const int s = 128;
            var r = new Raster(s, s);
            var brass = Rgba.Hex("#E2B04A");
            var brassDark = Rgba.Hex("#A8742A");
            var steel = Rgba.Hex("#C9CDE0");
            var purple = Rgba.Hex("#7A4FE0");
            var wood = Rgba.Hex("#E9B977");
            var green = Rgba.Hex("#17945F");
            var teal = Rgba.Hex("#5CC8D8");
            r.Paint((x, y) =>
            {
                var result = Rgba.Clear;
                void Layer(float d, Rgba c)
                {
                    var cover = Draw.Cover(d);
                    if (cover > 0f)
                    {
                        result = Rgba.Over(c.WithAlpha(cover), result);
                    }
                }

                switch (art)
                {
                    case BoosterArt.Scissors:
                    {
                        // Лезвия крест-накрест сверху, латунные кольца-ручки снизу.
                        var blade1 = Draw.Capsule(x, y, 54f, 60f, 34f, 112f, 5f, 2f, out _, out _);
                        var blade2 = Draw.Capsule(x, y, 62f, 60f, 82f, 112f, 5f, 2f, out _, out _);
                        Layer(blade1, steel.Shade(-0.15f));
                        Layer(blade2, steel);
                        var ring1 = MathF.Abs(Draw.Circle(x, y, 50f, 40f, 17f)) - 6f;
                        var ring2 = MathF.Abs(Draw.Circle(x, y, 78f, 32f, 17f)) - 6f;
                        Layer(ring1, brass.Shade(-0.1f + 0.25f * Draw.Clamp01((y - 30f) / 30f)));
                        Layer(ring2, brass.Shade(-0.05f + 0.25f * Draw.Clamp01((y - 22f) / 30f)));
                        Layer(Draw.Circle(x, y, 58f, 62f, 5f), brassDark);
                        break;
                    }

                    case BoosterArt.Undo:
                    {
                        // Стрелка из толстой нити: дуга через верх справа налево, наконечник вниз слева.
                        var dist = Draw.Circle(x, y, 68f, 56f, 30f);
                        var angle = MathF.Atan2(y - 56f, x - 68f);
                        var arc = MathF.Abs(dist) - 9f;
                        if (angle < -0.9f || angle > MathF.PI - 0.05f)
                        {
                            arc = 99f;
                        }

                        var head = Draw.Triangle(x, y, 22f, 62f, 54f, 62f, 38f, 34f);
                        var c = purple.Shade(0.05f + 0.25f * Draw.Clamp01((y - 30f) / 60f));
                        Layer(MathF.Min(arc, head), c);
                        var ply = MathF.Abs(MathF.Sin(angle * 9f + dist * 0.25f));
                        if (arc < -1f && ply > 0.82f)
                        {
                            result = Rgba.Over(Rgba.White.WithAlpha(0.22f), result);
                        }

                        Layer(Draw.Circle(x, y, 68f + 30f * MathF.Cos(-0.9f), 56f + 30f * MathF.Sin(-0.9f), 9f), purple.Shade(-0.15f));
                        break;
                    }

                    case BoosterArt.Slot:
                    {
                        // Деревянная лунка-шайба и зелёный плюс в кружке.
                        Layer(Draw.Ellipse(x, y, 60f, 50f, 44f, 26f), wood.Shade(-0.25f));
                        Layer(Draw.Ellipse(x, y, 60f, 58f, 44f, 26f), wood);
                        Layer(Draw.Ellipse(x, y, 60f, 60f, 26f, 14f), wood.Shade(-0.3f));
                        Layer(Draw.Circle(x, y, 96f, 86f, 17f), Rgba.White);
                        Layer(Draw.Circle(x, y, 96f, 86f, 14f), green);
                        Layer(MathF.Min(Draw.RoundBox(x, y, 96f, 86f, 8f, 2.6f, 1.3f), Draw.RoundBox(x, y, 96f, 86f, 2.6f, 8f, 1.3f)), Rgba.White);
                        break;
                    }

                    default:
                    {
                        // Карманные часы на бирюзовой подставке.
                        Layer(Draw.Ellipse(x, y, 64f, 22f, 40f, 9f), teal.Shade(-0.1f));
                        Layer(Draw.RoundBox(x, y, 64f, 104f, 9f, 6f, 3f), brassDark);
                        Layer(Draw.Circle(x, y, 64f, 60f, 36f), brass.Shade(-0.1f + 0.2f * Draw.Clamp01((y - 30f) / 60f)));
                        Layer(Draw.Circle(x, y, 64f, 60f, 28f), Rgba.Hex("#FFF8EA"));
                        Layer(Draw.Segment(x, y, 64f, 60f, 64f, 80f) - 2.6f, Rgba.Hex("#2B2350"));
                        Layer(Draw.Segment(x, y, 64f, 60f, 78f, 54f) - 2.6f, Rgba.Hex("#2B2350"));
                        Layer(Draw.Circle(x, y, 64f, 60f, 3.5f), Rgba.Hex("#E0306E"));
                        break;
                    }
                }

                return result;
            });
            return r;
        }
    }
}
