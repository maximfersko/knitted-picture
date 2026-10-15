#nullable enable

using System;

namespace KnittedPicture.Game
{
    /// <summary>Рисунки медалей: что изображено в центре.</summary>
    public enum BadgeArt
    {
        /// <summary>Одна петля «галочкой» на спице — первая картинка.</summary>
        Stitch,

        /// <summary>Картинка в рамке с сердечком — несколько картинок.</summary>
        Picture,

        /// <summary>Три звезды — уровни на 3 звезды.</summary>
        Stars,

        /// <summary>Два клубка — собранные мотки.</summary>
        Balls,

        /// <summary>Закрытый альбом с сердечком — целая коллекция.</summary>
        Album,

        /// <summary>Листок календаря с галочкой — подарок 7 дней подряд.</summary>
        Calendar,

        /// <summary>Корона — мастерство.</summary>
        Crown,

        /// <summary>Раскрытый альбом — половина альбома.</summary>
        OpenBook,

        /// <summary>Альбом со звездой и закладкой — весь альбом.</summary>
        StarBook,

        /// <summary>Стопка из трёх альбомов — пять коллекций.</summary>
        Books,

        /// <summary>Корзинка с клубками — вся пряжа.</summary>
        Basket,

        /// <summary>Рамка-коллекция: три картинки на полочке — награда за коллекцию альбома.</summary>
        Shelf,
    }

    public static partial class IconRaster
    {
        /// <summary>
        /// Медаль достижения 64×64 (рисуется втрое крупнее): две ленты снизу, металлический обод (золото —
        /// можно забрать или получено, серебро — в процессе), выпуклая эмаль своего цвета и свой рисунок
        /// в центре — у каждого достижения своя медаль, а не один кубок на всех. Форма у всех общая,
        /// как у медалей одной серии.
        /// </summary>
        public static Raster Badge(BadgeArt art, bool gold)
        {
            var metal = gold ? Rgba.Hex("#F2B630") : Rgba.Hex("#BFC5D2");
            var metalLight = gold ? Rgba.Hex("#FFF1B8") : Rgba.Hex("#F6F8FC");
            var metalDeep = gold ? Rgba.Hex("#A8680A") : Rgba.Hex("#727B8C");
            var enamel = EnamelOf(art);
            var cream = Rgba.Hex("#FFF8EA");
            const float cx = 32f;
            const float cy = 36f;
            return Raster.Render(64, 64, 3, (x, y) =>
            {
                var result = Rgba.Clear;
                result = Shadow(result, Draw.Circle(x, y, cx, cy - 2.2f, 25f), 4f, 0.3f);

                // Ленты: два хвоста с вырезом «ласточкин хвост», уходят из-под медали вниз.
                foreach (var side in new[] { -1f, 1f })
                {
                    var tail = Draw.RoundBox(x, y, cx + side * 9f, 12f, 6.5f, 11f, 1.2f);
                    var notch = Draw.Circle(x, y, cx + side * 9f, -1.5f, 6.8f);
                    tail = MathF.Max(tail, -notch);
                    var tilt = (x - (cx + side * 9f)) / 6.5f;
                    var ribbon = enamel.Shade(-0.22f + 0.12f * tilt * side);
                    result = Layer(result, tail, ribbon.Shade(-0.35f * (1f - Draw.SmoothStep(-1.6f, -0.5f, tail))));
                }

                // Обод: металл с фаской — свет сверху слева, кромка резкая.
                var rim = Draw.Circle(x, y, cx, cy, 25f);
                var nx = (x - cx) / 25f;
                var ny = (y - cy) / 25f;
                var lit = Draw.Clamp01(0.5f - 0.45f * nx + 0.55f * ny);
                var ring = Rgba.Lerp(metalDeep, metal, 0.35f + 0.65f * lit);
                ring = Rgba.Lerp(ring, metalLight, MathF.Pow(lit, 4f) * 0.7f);
                ring = ring.Shade(-0.35f * Draw.SmoothStep(-1.6f, -0.4f, rim));
                result = Layer(result, rim, ring);

                // Бороздка между ободом и эмалью и сама эмаль — выпуклая, светлее к верху слева.
                var face = Draw.Circle(x, y, cx, cy, 19.5f);
                result = Layer(result, face + 1.2f, metalDeep.Shade(-0.2f));
                var enamelLit = Draw.Clamp01(0.55f - 0.4f * nx + 0.5f * ny);
                var enamelColor = enamel.Shade(-0.18f + 0.3f * enamelLit);
                result = Layer(result, face, enamelColor);

                // Блик на эмали — дуга сверху слева.
                var gloss = MathF.Max(Draw.Circle(x, y, cx, cy, 17.5f), -Draw.Circle(x, y, cx + 3f, cy - 3.5f, 17.5f));
                result = Layer(result, gloss, Rgba.White.WithAlpha(0.28f));

                // Рисунок: сливочный, выпуклый, с тенью вниз на эмали.
                result = Emblem(art, x, y, cx, cy, result, cream, enamel);

                // Искра на ободе.
                var spark = Draw.Circle(x, y, cx - 15f, cy + 17f, 1.6f);
                result = Layer(result, spark, Rgba.White.WithAlpha(gold ? 0.9f : 0.7f));
                return result;
            });
        }

        private static Rgba EnamelOf(BadgeArt art)
        {
            switch (art)
            {
                case BadgeArt.Stitch: return Rgba.Hex("#D9483E");
                case BadgeArt.Picture: return Rgba.Hex("#2F6FDE");
                case BadgeArt.Stars: return Rgba.Hex("#7B4CC2");
                case BadgeArt.Balls: return Rgba.Hex("#2E9E5B");
                case BadgeArt.Album: return Rgba.Hex("#14958F");
                case BadgeArt.Calendar: return Rgba.Hex("#E07A1F");
                case BadgeArt.Crown: return Rgba.Hex("#5A3BA8");
                case BadgeArt.OpenBook: return Rgba.Hex("#2A7FC4");
                case BadgeArt.StarBook: return Rgba.Hex("#C02F4A");
                case BadgeArt.Books: return Rgba.Hex("#3D8B4E");
                case BadgeArt.Basket: return Rgba.Hex("#B9652A");
                default: return Rgba.Hex("#1F8C78");
            }
        }

        /// <summary>Рисунок в центре медали: форма-«подушка» сливочного цвета с тенью и деталями цвета эмали.</summary>
        private static Rgba Emblem(BadgeArt art, float x, float y, float cx, float cy, Rgba result, Rgba cream, Rgba enamel)
        {
            var detail = enamel.Shade(-0.25f);

            // Нарисовать форму: тень вниз, затем выпуклую заливку.
            Rgba Shape(Rgba r, Func<float, float, float> sdf, Rgba color)
            {
                r = Layer(r, sdf(x, y + 1.1f), new Rgba(0f, 0f, 0f, 0.25f));
                return Layer(r, sdf(x, y), Pillow(sdf, x, y, color, 1.6f, 0.45f));
            }

            switch (art)
            {
                case BadgeArt.Stitch:
                {
                    // Две дольки петли «галочкой» и спица над ними.
                    float Leg(float px, float py, float side) =>
                        Draw.Capsule(px, py, cx + side * 1.6f, cy - 9f, cx + side * 8.5f, cy + 7f, 3.2f, 5.2f, out _, out _);
                    result = Shape(result, (px, py) => Leg(px, py, -1f), cream);
                    result = Shape(result, (px, py) => Leg(px, py, 1f), cream.Shade(-0.06f));
                    result = Layer(result, Draw.Segment(x, y, cx, cy - 8f, cx, cy + 4f) - 0.5f, detail.WithAlpha(0.5f));
                    result = Shape(result, (px, py) => Draw.Capsule(px, py, cx - 12f, cy + 11f, cx + 12f, cy + 11f, 1.4f, 1.4f, out _, out _),
                        Rgba.Hex("#E8C38A"));
                    return result;
                }

                case BadgeArt.Picture:
                {
                    result = Shape(result, (px, py) => Draw.RoundBox(px, py, cx, cy, 11f, 10f, 2f), cream);
                    result = Layer(result, Draw.RoundBox(x, y, cx, cy, 7.5f, 6.5f, 1f), detail.WithAlpha(0.35f));
                    return Shape(result, (px, py) => Heart(px - cx, py - cy, 5f), Rgba.Hex("#FF6FB7"));
                }

                case BadgeArt.Stars:
                {
                    result = Shape(result, (px, py) => Star5(px - (cx - 9.5f), py - (cy - 3f), 5f, 0.48f) - 0.4f, cream.Shade(-0.04f));
                    result = Shape(result, (px, py) => Star5(px - (cx + 9.5f), py - (cy - 3f), 5f, 0.48f) - 0.4f, cream.Shade(-0.04f));
                    return Shape(result, (px, py) => Star5(px - cx, py - (cy + 1.5f), 8f, 0.48f) - 0.6f, Rgba.Hex("#FFE27A"));
                }

                case BadgeArt.Balls:
                {
                    result = Ball(result, x, y, cx + 6f, cy - 1f, 7.5f, Rgba.Hex("#FFD419"), Shape);
                    return Ball(result, x, y, cx - 5f, cy + 2f, 8.5f, cream, Shape);
                }

                case BadgeArt.Album:
                case BadgeArt.StarBook:
                {
                    result = Shape(result, (px, py) => Draw.RoundBox(px, py, cx + 1f, cy, 10f, 12f, 2f), cream);
                    result = Layer(result, Draw.RoundBox(x, y, cx - 7f, cy, 1.4f, 12f, 0.6f), detail.WithAlpha(0.45f));
                    if (art == BadgeArt.StarBook)
                    {
                        // Закладка-лента сверху и звезда на обложке.
                        result = Shape(result, (px, py) => MathF.Max(Draw.RoundBox(px, py, cx + 6f, cy + 10f, 2f, 6f, 0.4f),
                            -Draw.Circle(px, py, cx + 6f, cy + 3.2f, 2.2f)), Rgba.Hex("#FFD419"));
                        return Shape(result, (px, py) => Star5(px - (cx + 1.5f), py - (cy - 1f), 6.5f, 0.48f) - 0.5f, Rgba.Hex("#FFE27A"));
                    }

                    return Shape(result, (px, py) => Heart(px - (cx + 1.5f), py - cy, 5f), Rgba.Hex("#FF6FB7"));
                }

                case BadgeArt.Calendar:
                {
                    result = Shape(result, (px, py) => Draw.RoundBox(px, py, cx, cy - 1f, 11f, 11f, 2.2f), cream);
                    result = Layer(result, MathF.Max(Draw.RoundBox(x, y, cx, cy + 7f, 11f, 3f, 2.2f), cy + 4f - y), detail);
                    foreach (var side in new[] { -1f, 1f })
                    {
                        result = Shape(result, (px, py) => Draw.RoundBox(px, py, cx + side * 5.5f, cy + 11f, 1.3f, 3f, 1.3f), cream.Shade(-0.1f));
                    }

                    var tick = MathF.Min(Draw.Segment(x, y, cx - 5.5f, cy - 2f, cx - 1.5f, cy - 6f), Draw.Segment(x, y, cx - 1.5f, cy - 6f, cx + 6f, cy + 1.5f)) - 1.6f;
                    return Layer(result, tick, Rgba.Hex("#2DB34A"));
                }

                case BadgeArt.Crown:
                {
                    float CrownShape(float px, float py)
                    {
                        var band = Draw.RoundBox(px, py, cx, cy - 7f, 11f, 3f, 1f);
                        var body = Draw.RoundBox(px, py, cx, cy - 1f, 11f, 5f, 1f);
                        var peaks = MathF.Min(MathF.Min(Spike(px, py, cx - 9f), Spike(px, py, cx)), Spike(px, py, cx + 9f));
                        return MathF.Min(band, MathF.Min(body, peaks));
                    }

                    float Spike(float px, float py, float sx) =>
                        MathF.Max(MathF.Abs(px - sx) * 1.6f + (py - (cy + 10f)), cy - 2f - py);

                    result = Shape(result, CrownShape, Rgba.Hex("#FFD419"));
                    foreach (var sx in new[] { cx - 9f, cx, cx + 9f })
                    {
                        result = Shape(result, (px, py) => Draw.Circle(px, py, sx, cy + 10.5f, 1.8f), cream);
                    }

                    return Layer(result, Draw.Circle(x, y, cx, cy - 7f, 1.8f), Rgba.Hex("#D9483E"));
                }

                case BadgeArt.OpenBook:
                {
                    foreach (var side in new[] { -1f, 1f })
                    {
                        float Page(float px, float py) => MathF.Max(Draw.RoundBox(px, py, cx + side * 6.5f, cy, 6.5f, 9f, 1.4f),
                            -(py - (cy - 9f) + (px - cx) * side * 0.18f));
                        result = Shape(result, Page, side < 0 ? cream : cream.Shade(-0.08f));
                        for (var line = 0; line < 3; line++)
                        {
                            var ly = cy + 4f - line * 4f;
                            result = Layer(result, Draw.Segment(x, y, cx + side * 3f, ly, cx + side * 10f, ly) - 0.5f, detail.WithAlpha(0.45f));
                        }
                    }

                    return Layer(result, Draw.Segment(x, y, cx, cy - 9f, cx, cy + 8f) - 0.6f, detail);
                }

                case BadgeArt.Books:
                {
                    var colors = new[] { Rgba.Hex("#FFD419"), cream, Rgba.Hex("#FF6FB7") };
                    for (var i = 0; i < 3; i++)
                    {
                        var by = cy - 8f + i * 6.5f;
                        var shift = (i - 1) * 1.5f;
                        result = Shape(result, (px, py) => Draw.RoundBox(px, py, cx + shift, by, 11f - i, 2.8f, 1.2f), colors[i]);
                        result = Layer(result, Draw.RoundBox(x, y, cx + shift + 6f - i, by, 1.2f, 2.8f, 0.5f), detail.WithAlpha(0.4f));
                    }

                    return result;
                }

                case BadgeArt.Basket:
                {
                    result = Ball(result, x, y, cx - 5f, cy + 2f, 6f, Rgba.Hex("#FF6FB7"), Shape);
                    result = Ball(result, x, y, cx + 5f, cy + 3f, 6f, Rgba.Hex("#4CC3F5"), Shape);
                    float BasketShape(float px, float py) =>
                        MathF.Max(Draw.RoundBox(px, py, cx, cy - 5f, 12f, 6f, 2.5f), MathF.Abs(px - cx) * 0.25f - (cy + 1f - py) - 1f);
                    result = Shape(result, BasketShape, Rgba.Hex("#E8B46A"));
                    for (var i = -2; i <= 2; i++)
                    {
                        result = Layer(result, Draw.Segment(x, y, cx + i * 4.4f, cy - 10f, cx + i * 4.8f, cy) - 0.5f, Rgba.Hex("#A8682E").WithAlpha(0.7f));
                    }

                    return result;
                }

                default:
                {
                    // Полочка с тремя рамками: средняя выше.
                    result = Shape(result, (px, py) => Draw.RoundBox(px, py, cx, cy - 9f, 13f, 1.6f, 0.8f), Rgba.Hex("#E8C38A"));
                    var heights = new[] { 5.5f, 7.5f, 5.5f };
                    var hearts = new[] { Rgba.Hex("#FF6FB7"), Rgba.Hex("#FFD419"), Rgba.Hex("#4CC3F5") };
                    for (var i = 0; i < 3; i++)
                    {
                        var fx = cx + (i - 1) * 9f;
                        var fy = cy - 7.4f + heights[i];
                        result = Shape(result, (px, py) => Draw.RoundBox(px, py, fx, fy, 4f, heights[i], 1f), cream);
                        result = Layer(result, Draw.Circle(x, y, fx, fy, 2.2f), hearts[i]);
                    }

                    return result;
                }
            }
        }

        /// <summary>Клубок на медали: круг-подушка и три витка нитки цветом потемнее.</summary>
        private static Rgba Ball(Rgba result, float x, float y, float bx, float by, float r, Rgba color,
            Func<Rgba, Func<float, float, float>, Rgba, Rgba> shape)
        {
            result = shape(result, (px, py) => Draw.Circle(px, py, bx, by, r), color);
            var inside = Draw.Circle(x, y, bx, by, r - 0.8f);
            if (inside < 0f)
            {
                var wraps = MathF.Min(MathF.Abs(Draw.Circle(x, y, bx - r * 0.9f, by, r)), MathF.Abs(Draw.Circle(x, y, bx + r * 0.9f, by, r)));
                wraps = MathF.Min(wraps, MathF.Abs(y - by - (x - bx) * 0.35f));
                result = Layer(result, wraps - 0.55f, color.Shade(-0.3f));
            }

            return result;
        }

        /// <summary>Сердечко размера s с центром в (0, 0): знаковое расстояние.</summary>
        private static float Heart(float px, float py, float s)
        {
            px = MathF.Abs(px) / s;
            py = (py + s * 0.55f) / s;
            float d;
            if (py + px > 1f)
            {
                var dx = px - 0.25f;
                var dy = py - 0.75f;
                d = MathF.Sqrt(dx * dx + dy * dy) - MathF.Sqrt(2f) / 4f;
            }
            else
            {
                var a = px * px + (py - 1f) * (py - 1f);
                var m = MathF.Max(px + py, 0f) * 0.5f;
                var b = (px - m) * (px - m) + (py - m) * (py - m);
                d = MathF.Sqrt(MathF.Min(a, b)) * MathF.Sign(px - py);
            }

            return d * s;
        }
    }
}
