#nullable enable

using System;
using System.Collections.Generic;

namespace KnittedPicture.Game
{
    /// <summary>
    /// Клубки 2.0 и всё, что на них надевается: шар, намотанный кручёной нитью, с глянцем сверху слева,
    /// цветной тенью снизу справа, ворсом по контуру и завитком нити слева внизу; накладки — лента
    /// связки с бантиком, замок на ремешке, ключик, узелки, ледяной пузырь, бумажная обёртка «?»,
    /// бирка-строчка для числа петель.
    /// </summary>
    public static class BallRaster
    {
        public const int Size = 160;

        /// <summary>Центр и радиус шара в пикселях (шар чуть выше центра: снизу место под завиток).</summary>
        public const float Cx = 84f;

        public const float Cy = 86f;

        public const float Radius = 62f;

        public enum Kind
        {
            Plain,
            Thick,
            Rainbow,

            /// <summary>Пряжа цвета ткани: намотана нитями канвы.</summary>
            Canvas,
        }

        public static Raster Ball(Rgba color, Kind kind = Kind.Plain)
        {
            var r = new Raster(Size, Size);
            var strand = kind == Kind.Thick ? 0.36f : 0.22f;
            const float band = 0.55f;
            var under = Normalize(0.3f, 0.9f, 0.3f);
            var over = Normalize(-0.9f, 0.3f, 0.3f);
            var light = Normalize(-0.55f, 0.6f, 0.6f);
            var halfway = Normalize(light.X, light.Y, light.Z + 1f);
            var tail = Tail();
            var fuzz = color.Shade(-0.1f);

            r.Paint((x, y) =>
            {
                var nx = (x - Cx) / Radius;
                var ny = (y - Cy) / Radius;
                var r2 = nx * nx + ny * ny;
                var d = MathF.Sqrt(r2) * Radius - Radius;
                var cover = Draw.Cover(d);
                Rgba result = Rgba.Clear;
                if (cover > 0f)
                {
                    var nz = MathF.Sqrt(MathF.Max(0f, 1f - r2));
                    var tOver = nx * over.X + ny * over.Y + nz * over.Z;
                    float value;
                    float index;
                    if (MathF.Abs(tOver) < band)
                    {
                        value = Strand(tOver, strand, out index) * (1f - 0.25f * Draw.SmoothStep(band - 0.06f, band, MathF.Abs(tOver)));
                        index += 100f;
                    }
                    else
                    {
                        var shadow = 1f - 0.22f * (1f - Draw.SmoothStep(band, band + 0.12f, MathF.Abs(tOver)));
                        value = Strand(nx * under.X + ny * under.Y + nz * under.Z, strand, out index) * shadow;
                    }

                    var lambert = Draw.Clamp01(nx * light.X + ny * light.Y + nz * light.Z);
                    var shine = MathF.Pow(Draw.Clamp01(nx * halfway.X + ny * halfway.Y + nz * halfway.Z), 16f);
                    var baseColor = kind switch
                    {
                        Kind.Rainbow => Hue(index * 0.11f),
                        _ => color,
                    };

                    if (kind == Kind.Canvas)
                    {
                        // Нити канвы: мелкая клетка поверх намотки.
                        var weave = MathF.Abs(MathF.Sin(x * 0.9f)) * MathF.Abs(MathF.Sin(y * 0.9f));
                        value *= 0.9f + 0.1f * weave;
                    }

                    // Светотень: густой тот же оттенок снизу справа, глянец сверху слева.
                    var shade = (value - 1f) * 0.75f + (lambert - 0.75f) * 0.7f;
                    var c = baseColor.Shade(shade);
                    c = Rgba.Lerp(c, Rgba.White, shine * 0.55f + Draw.Clamp01(value - 0.97f) * lambert * 0.25f);

                    // Мягкий обод по краю.
                    c = c.Shade(-0.2f * Draw.SmoothStep(-8f, -1f, d));
                    result = c.WithAlpha(cover);
                }

                // Ворс: волоски по контуру, чуть дальше снизу справа.
                if (d > -1f && d < 6f && Draw.Hash((int)x * 3, (int)y * 7) > 0.84f)
                {
                    var hair = fuzz.WithAlpha(0.5f * (1f - Draw.Clamp01(d / 6f)));
                    result = Rgba.Over(result, hair);
                }

                // Завиток нити слева внизу.
                var tailDistance = Polyline(tail, x, y, 7f);
                var tailCover = Draw.Cover(tailDistance - 4.2f);
                if (tailCover > 0f && (cover < 0.99f || tailDistance < 4f))
                {
                    var profile = MathF.Sqrt(Draw.Clamp01(1f - tailDistance * tailDistance / 17.6f));
                    var tailColor = (kind == Kind.Rainbow ? Hue(0.92f) : color).Shade(-0.15f + 0.3f * profile);
                    result = Rgba.Over(tailColor.WithAlpha(tailCover), result);
                }

                return result;
            });
            return r;
        }

        /// <summary>Скрытый цвет: клубок в бумажной обёртке со строчкой по краю (знак «?» рисует игра).</summary>
        public static Raster Hidden()
        {
            var r = new Raster(Size, Size);
            var paper = Rgba.Hex("#FBF8F2");
            var stitch = Rgba.Hex("#C9BEF5");
            r.Paint((x, y) =>
            {
                var d = Draw.Circle(x, y, Cx, Cy, Radius);
                var cover = Draw.Cover(d);
                if (cover <= 0f)
                {
                    return Rgba.Clear;
                }

                var nx = (x - Cx) / Radius;
                var ny = (y - Cy) / Radius;
                var lambert = Draw.Clamp01(-0.5f * nx + 0.55f * ny + 0.65f * MathF.Sqrt(Draw.Clamp01(1f - nx * nx - ny * ny)));
                var c = paper.Shade(-0.12f * (1f - lambert));
                var ring = MathF.Abs(d + 7f);
                var angle = MathF.Atan2(y - Cy, x - Cx);
                var dash = MathF.Sin(angle * 22f) > 0f;
                if (ring < 1.6f && dash)
                {
                    c = Rgba.Lerp(c, stitch, 0.9f);
                }

                return c.WithAlpha(cover);
            });
            return r;
        }

        /// <summary>Лента связки: фиолетовая полоса поперёк клубка и бантик снизу. Белая — красится цветом пары.</summary>
        public static Raster Ribbon()
        {
            var r = new Raster(Size, Size);
            r.Paint((x, y) =>
            {
                var curve = Cy - 8f + MathF.Pow((x - Cx) / Radius, 2f) * 10f;
                var inside = Draw.Circle(x, y, Cx, Cy, Radius + 1f) < 0f;
                var band = MathF.Abs(y - curve) - 9f;
                var cover = inside ? Draw.Cover(band) : 0f;
                var shade = 0.82f + 0.18f * Draw.Clamp01((y - curve + 9f) / 18f);

                // Бантик: две петли и узелок под лентой.
                var bow = MathF.Min(Draw.Ellipse(x, y, Cx - 12f, curve - 14f, 12f, 8f, 25f), Draw.Ellipse(x, y, Cx + 12f, curve - 14f, 12f, 8f, -25f));
                var tails = MathF.Min(Draw.Capsule(x, y, Cx - 3f, curve - 10f, Cx - 10f, curve - 30f, 4f, 3f, out _, out _),
                    Draw.Capsule(x, y, Cx + 3f, curve - 10f, Cx + 12f, curve - 28f, 4f, 3f, out _, out _));
                var knot = Draw.Circle(x, y, Cx, curve - 10f, 6f);
                var bowCover = Draw.Cover(MathF.Min(MathF.Min(bow, tails), knot));
                var all = MathF.Max(cover, bowCover);
                if (all <= 0f)
                {
                    return Rgba.Clear;
                }

                var v = bowCover > cover ? (knot < 0f ? 0.78f : 0.9f) : shade;
                return new Rgba(v, v, v, all);
            });
            return r;
        }

        /// <summary>Замок на ремешке: латунный навесной замочек посередине клубка.</summary>
        public static Raster Padlock()
        {
            var r = new Raster(Size, Size);
            var brass = Rgba.Hex("#E2B04A");
            var strap = Rgba.Hex("#8B6FD6");
            r.Paint((x, y) =>
            {
                var result = Rgba.Clear;
                var curve = Cy + 4f + MathF.Pow((x - Cx) / Radius, 2f) * 10f;
                if (Draw.Circle(x, y, Cx, Cy, Radius + 1f) < 0f)
                {
                    var band = Draw.Cover(MathF.Abs(y - curve) - 5f);
                    result = strap.Shade(-0.1f * Draw.Clamp01((curve - y) / 5f)).WithAlpha(band);
                }

                var shackle = MathF.Abs(Draw.Circle(x, y, Cx, Cy - 2f, 11f)) - 3.2f;
                if (y < Cy - 2f)
                {
                    shackle = 99f;
                }

                var body = Draw.RoundBox(x, y, Cx, Cy - 22f, 18f, 15f, 5f);
                var keyhole = MathF.Min(Draw.Circle(x, y, Cx, Cy - 18f, 3.4f), Draw.RoundBox(x, y, Cx, Cy - 26f, 1.6f, 5f, 1f));
                var lockCover = Draw.Cover(MathF.Min(shackle, body));
                if (lockCover > 0f)
                {
                    var lit = Draw.Clamp01((y - (Cy - 37f)) / 30f);
                    var c = body < 0f ? brass.Shade(-0.25f + 0.35f * lit) : Rgba.Hex("#A8742A").Shade(0.2f);
                    if (body < 0f && keyhole < 0f)
                    {
                        c = Rgba.Hex("#5B3A1A");
                    }

                    result = Rgba.Over(c.WithAlpha(lockCover), result);
                }

                return result;
            });
            return r;
        }

        /// <summary>Ключик, заткнутый в намотку справа сверху; латунь с бликом.</summary>
        public static Raster Key()
        {
            var r = new Raster(Size, Size);
            var brass = Rgba.Hex("#E2B04A");
            r.Paint((x, y) =>
            {
                var bow = MathF.Abs(Draw.Circle(x, y, Cx - 2f, Cy + 22f, 15f)) - 6f;
                var shaft = Draw.Capsule(x, y, Cx + 12f, Cy + 30f, Cx + 62f, Cy + 58f, 5f, 5f, out _, out _);
                var teeth = MathF.Min(Draw.RoundBox(x, y, Cx + 50f, Cy + 44f, 4f, 7f, 1.5f), Draw.RoundBox(x, y, Cx + 39f, Cy + 38f, 4f, 6f, 1.5f));
                var d = MathF.Min(MathF.Min(bow, shaft), teeth);
                var cover = Draw.Cover(d);
                if (cover <= 0f)
                {
                    return Rgba.Clear;
                }

                var c = brass.Shade(-0.25f * Draw.SmoothStep(-3f, 0f, d));
                c = Rgba.Lerp(c, Rgba.White, Draw.Clamp01(1f - MathF.Abs(d + 2f)) * 0.35f);
                return c.WithAlpha(cover);
            });
            return r;
        }

        /// <summary>Узелки сверху клубка: сколько count (1–3), сливовые шишечки с петелькой.</summary>
        public static Raster Knots(int count)
        {
            var r = new Raster(Size, Size);
            var plum = Rgba.Hex("#5B3F86");
            var spots = count switch
            {
                1 => new[] { 0f },
                2 => new[] { -20f, 20f },
                _ => new[] { -32f, 0f, 32f },
            };
            r.Paint((x, y) =>
            {
                var result = Rgba.Clear;
                foreach (var sx in spots)
                {
                    var kx = Cx + sx;
                    var ky = Cy + Radius - 10f - MathF.Abs(sx) * 0.3f;
                    var knot = Draw.Ellipse(x, y, kx, ky + 8f, 13f, 10f);
                    var loop = MathF.Abs(Draw.Circle(x, y, kx, ky + 21f, 9f)) - 3.2f;
                    var d = MathF.Min(knot, y > ky + 17f ? loop : 99f);
                    var cover = Draw.Cover(d);
                    if (cover <= 0f)
                    {
                        continue;
                    }

                    var lit = Draw.Clamp01((y - ky) / 14f);
                    result = Rgba.Over(plum.Shade(-0.15f + 0.35f * lit).WithAlpha(cover), result);
                }

                return result;
            });
            return r;
        }

        /// <summary>
        /// Ледяной пузырь: прозрачная голубая сфера чуть больше клубка, светлый обод, блик и трещины —
        /// чем меньше льда осталось (stage 0..3), тем больше трещин.
        /// </summary>
        public static Raster Ice(int stage)
        {
            var r = new Raster(Size, Size);
            var fill = Rgba.Hex("#BFF1F5");
            var rim = Rgba.Hex("#5CC8D8");
            var cracks = new List<(float, float, float, float)>
            {
                (Cx + 18f, Cy + Radius, Cx + 8f, Cy + 30f),
                (Cx + 8f, Cy + 30f, Cx + 20f, Cy + 12f),
                (Cx - 40f, Cy - 30f, Cx - 22f, Cy - 18f),
                (Cx - 22f, Cy - 18f, Cx - 26f, Cy + 2f),
                (Cx + 46f, Cy - 20f, Cx + 28f, Cy - 26f),
                (Cx - 6f, Cy - Radius, Cx - 2f, Cy - 40f),
            };
            var shown = Math.Min(cracks.Count, stage * 2);
            r.Paint((x, y) =>
            {
                var d = Draw.Circle(x, y, Cx, Cy, Radius + 6f);
                var cover = Draw.Cover(d);
                if (cover <= 0f)
                {
                    return Rgba.Clear;
                }

                var edge = Draw.SmoothStep(-10f, 0f, d);
                var c = Rgba.Lerp(fill.WithAlpha(0.32f), rim.WithAlpha(0.9f), edge * edge);
                var shine = Draw.Ellipse(x, y, Cx - 26f, Cy + 32f, 16f, 8f, -35f);
                if (shine < 0f)
                {
                    c = Rgba.Over(Rgba.White.WithAlpha(0.65f * Draw.Clamp01(-shine / 4f)), c);
                }

                for (var i = 0; i < shown; i++)
                {
                    var (ax, ay, bx, by) = cracks[i];
                    var crack = Draw.Segment(x, y, ax, ay, bx, by);
                    if (crack < 1.6f)
                    {
                        c = Rgba.Over(Rgba.White.WithAlpha(0.85f * (1f - crack / 1.6f)), c);
                    }
                }

                return c.WithAlpha(c.A * cover);
            });
            return r;
        }

        /// <summary>Бирка числа петель: белый кружок с пунктирной строчкой по краю.</summary>
        public static Raster Badge()
        {
            const int s = 64;
            var r = new Raster(s, s);
            var stitch = Rgba.Hex("#9488C8");
            r.Paint((x, y) =>
            {
                var d = Draw.Circle(x, y, s / 2f, s / 2f, s / 2f - 2f);
                var cover = Draw.Cover(d);
                if (cover <= 0f)
                {
                    return Rgba.Clear;
                }

                var c = Rgba.White.Shade(-0.04f * Draw.Clamp01((s / 2f - y) / (s / 2f)));
                var ring = MathF.Abs(d + 5f);
                var angle = MathF.Atan2(y - s / 2f, x - s / 2f);
                if (ring < 1.3f && MathF.Sin(angle * 14f) > 0f)
                {
                    c = Rgba.Lerp(c, stitch, 0.85f * (1f - ring / 1.3f));
                }

                return c.WithAlpha(cover);
            });
            return r;
        }

        /// <summary>Мягкая лавандовая тень под клубком (овал).</summary>
        public static Raster Shadow()
        {
            const int w = 96;
            const int h = 32;
            var r = new Raster(w, h);
            var shade = Rgba.Hex("#5B45AE");
            r.Paint((x, y) =>
            {
                var d = Draw.Ellipse(x, y, w / 2f, h / 2f, w / 2f - 2f, h / 2f - 2f);
                var t = Draw.Clamp01(-d / 14f);
                return shade.WithAlpha(0.22f * t * t);
            });
            return r;
        }

        private static float Strand(float t, float width, out float index)
        {
            var k = MathF.Asin(Draw.Clamp(t, -1f, 1f)) / width;
            index = MathF.Floor(k);
            var profile = MathF.Sin(MathF.PI * (k - index));
            return 0.6f + 0.4f * MathF.Sqrt(MathF.Max(0f, profile));
        }

        private static Rgba Hue(float h)
        {
            h = h - MathF.Floor(h);
            var r = Draw.Clamp01(MathF.Abs(h * 6f - 3f) - 1f);
            var g = Draw.Clamp01(2f - MathF.Abs(h * 6f - 2f));
            var b = Draw.Clamp01(2f - MathF.Abs(h * 6f - 4f));
            return Rgba.Lerp(new Rgba(r, g, b), Rgba.White, 0.25f);
        }

        private static (float X, float Y, float Z) Normalize(float x, float y, float z)
        {
            var l = MathF.Sqrt(x * x + y * y + z * z);
            return (x / l, y / l, z / l);
        }

        /// <summary>Завиток: выходит из клубка слева внизу и закручивается колечком.</summary>
        private static List<(float X, float Y)> Tail()
        {
            var points = new List<(float X, float Y)>();
            for (var i = 0; i <= 40; i++)
            {
                var t = i / 40f;
                var angle = 3.75f + t * 4.6f;
                var radius = 22f - 12f * t;
                var ox = Cx - 52f + 18f * t;
                var oy = Cy - 50f;
                points.Add((ox + MathF.Cos(angle) * radius * (1f - 0.4f * t), oy + MathF.Sin(angle) * radius * 0.8f));
            }

            return points;
        }

        private static float Polyline(List<(float X, float Y)> line, float x, float y, float limit)
        {
            var best = float.MaxValue;
            for (var i = 0; i + 1 < line.Count; i++)
            {
                var (ax, ay) = line[i];
                var (bx, by) = line[i + 1];
                if (x < MathF.Min(ax, bx) - limit || x > MathF.Max(ax, bx) + limit || y < MathF.Min(ay, by) - limit || y > MathF.Max(ay, by) + limit)
                {
                    continue;
                }

                best = MathF.Min(best, Draw.Segment(x, y, ax, ay, bx, by));
            }

            return best;
        }
    }
}
