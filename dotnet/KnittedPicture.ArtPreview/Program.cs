#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using KnittedPicture.Game;

namespace KnittedPicture.ArtPreview
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            if (args.Length == 2 && args[0] == "--ice-spools")
            {
                IceSpools(args[1]);
                return;
            }
            if (args.Length == 3 && args[0] == "--tension")
            {
                TensionComparison(args[1], args[2]);
                return;
            }
            if (args.Length == 2 && args[0] == "--textile-material")
            {
                TextileMaterial(args[1]);
                return;
            }
            if (args.Length > 0 && args[0] == "--bench")
            {
                Bench();
                return;
            }

            var dir = args.Length > 0 ? args[0] : "preview";
            Directory.CreateDirectory(dir);
            Classic(dir);
            Field(dir);
            Panels(dir);
            Icons(dir);
            YarnTiles(dir);
            Balls(dir);
            Loom(dir);
            Export(Path.Combine(dir, "sprites"));
            Meta(dir);
            Badges(dir);
            Frames(dir);
            MagicFrames(dir);
            Chests(dir);
        }

        /// <summary>Растровое приближение игрового расположения льда и клубка.</summary>
        private static void IceSpools(string dir)
        {
            Directory.CreateDirectory(dir);
            bool old = typeof(BallRaster).GetMethod("IceMask") == null;
            foreach (int size in new[] { 40, 52, 60 })
            foreach (var color in new[] { ("yellow", "#FFD23F"), ("dark", "#3D4FC4"), ("white", "#F4EEE4") })
            foreach (int stage in new[] { 0, 1, 2, 3 })
            {
                const int scale = 4;
                var canvas = new Raster(size * scale, size * scale);
                var ballLayer = new Raster(size * scale, size * scale);
                float art = size / 0.775f * scale, shift = art / 160f;
                float cx = size * scale / 2f - 4f * shift;
                float cy = size * scale / 2f - 6f * shift;
                Png.Put(ballLayer, BallRaster.Ball(Rgba.Hex(color.Item2)), cx - art / 2f, cy - art / 2f, art, art);
                float ice = size * 1.04f * scale, origin = (size * scale - ice) / 2f;
                if (!old)
                {
                    for (int y = 0; y < canvas.Height; y++)
                    for (int x = 0; x < canvas.Width; x++)
                    {
                        float dx = (x + 0.5f - size * scale / 2f) * 160f / ice;
                        float dy = (y + 0.5f - size * scale / 2f) * 160f / ice;
                        var pixel = ballLayer[x, y];
                        ballLayer[x, y] = pixel.WithAlpha(pixel.A * Draw.Cover(Draw.Circle(dx, dy, 0, 0, 78)));
                    }
                }
                Png.Fill(canvas, Rgba.Hex("#F7F1EA"));
                Png.Put(canvas, ballLayer, 0, 0, canvas.Width, canvas.Height);
                if (old)
                {
                    var block = new Raster(size * scale, size * scale);
                    block.Paint((x, y) => new Rgba(0.78f, 0.92f, 1f, 0.82f).WithAlpha(0.82f * Draw.Cover(Draw.RoundBox(x, y, size * scale / 2f, size * scale / 2f, ice / 2f, ice / 2f, size * 0.24f * scale))));
                    Png.Put(canvas, block, 0, 0, canvas.Width, canvas.Height);
                }
                if (old)
                {
                    var frost = new Raster(canvas.Width, canvas.Height);
                    frost.Paint((x, y) =>
                    {
                        float cx0 = size * scale / 2f, cy0 = cx0;
                        float outer = Draw.Cover(Draw.RoundBox(x, y, cx0, cy0, ice / 2f, ice / 2f, size * 0.24f * scale));
                        float inner = Draw.Cover(Draw.RoundBox(x, y, cx0, cy0, ice / 2f - 2f * scale, ice / 2f - 2f * scale, MathF.Max(0, size * 0.24f * scale - 2f * scale)));
                        return Rgba.White.WithAlpha(MathF.Max(0, outer - inner));
                    });
                    Png.Put(canvas, frost, 0, 0, canvas.Width, canvas.Height);
                }
                Png.Put(canvas, BallRaster.Ice(stage), origin, origin, ice, ice);
                // Геометрическое приближение значка; шрифт Unity в растровом экспорте недоступен.
                float sx = origin + size * 0.52f * scale;
                float sy = origin + ice - size * 0.36f * scale;
                float radius = size * 0.19f * scale;
                for (int arm = 0; arm < 3; arm++)
                {
                    float angle = arm * MathF.PI / 3f;
                    for (float t = -radius; t <= radius; t += 0.4f)
                    {
                        Disc(canvas, (int)(sx + MathF.Cos(angle) * t), (int)(sy + MathF.Sin(angle) * t), scale * 0.5f, Rgba.Hex("#3D8FD1"));
                    }
                }
                float dotX = origin + size * 0.86f * scale;
                float dotY = origin + ice - size * 0.18f * scale;
                Disc(canvas, (int)dotX, (int)dotY, (size * 0.3f + 4f) * scale / 2f, Rgba.White);
                Disc(canvas, (int)dotX, (int)dotY, size * 0.3f * scale / 2f, Rgba.Hex(color.Item2));
                Png.Save(canvas, Path.Combine(dir, $"{size}-{color.Item1}-{stage}.png"));
            }
        }

        /// <summary>Сравнение линии натяжения на двух раскладках с игровым растром шнура.</summary>
        private static void TensionComparison(string beforeDir, string afterDir)
        {
            Directory.CreateDirectory(beforeDir);
            Directory.CreateDirectory(afterDir);
            var rope = KnitRaster.RopeTile();
            var vertical = new Raster(rope.Height, rope.Width);
            for (var y = 0; y < rope.Height; y++)
            for (var x = 0; x < rope.Width; x++)
            {
                vertical[y, rope.Width - 1 - x] = rope[x, y];
            }
            foreach (var dense in new[] { false, true })
            foreach (var after in new[] { false, true })
            {
                const int cell = 40, cols = 8, rows = 7, left = 20, bottom = 20;
                const int line = bottom + 3 * cell;
                var image = new Raster(360, 320);
                Png.Fill(image, Rgba.Hex("#FFF9ED"));
                var colors = new[] { "#EFB8A6", "#DF6673", "#F5D798", "#ADC8A1" };
                for (var y = 0; y < rows; y++)
                for (var x = 0; x < cols; x++)
                {
                    var color = Rgba.Hex(colors[(x + y) % colors.Length]);
                    var tile = KnitRaster.TextileTile((x + y) % 3, false, false);
                    Png.Put(image, tile, left + x * cell, bottom + y * cell, cell, cell, color, y < 2 ? 0.9f : 0.4f);
                }
                var held = dense ? new[] { 0, 1, 2, 4, 5, 6 } : new[] { 1, 5 };
                foreach (var x in held)
                {
                    var cx = left + x * cell + cell / 2;
                    if (after)
                    {
                        for (var y = line; y < line + cell; y += 16)
                        {
                            Png.Put(image, vertical, cx - 2, y, 4, Math.Min(16, line + cell - y), Rgba.Hex("#986D50"));
                        }
                        Disc(image, cx, line, 2.5f, Rgba.Hex("#986D50"));
                    }
                    else
                    {
                        Rect(image, cx - 1, bottom + cell, 2, 3 * cell, Rgba.Hex("#B64A50"));
                    }
                }
                for (var x = 0; x < cols; x += dense ? 3 : 7)
                {
                    var bx = left + x * cell;
                    var c = Rgba.Hex(after ? "#0F6E46" : "#1E2732");
                    Rect(image, bx - 2, bottom + 2 * cell - 2, cell + 4, 2, c);
                    Rect(image, bx - 2, bottom + 3 * cell, cell + 4, 2, c);
                    Rect(image, bx - 2, bottom + 2 * cell, 2, cell, c);
                    Rect(image, bx + cell, bottom + 2 * cell, 2, cell, c);
                }
                if (after)
                {
                    Rect(image, left, line - 3, cols * cell, 3, new Rgba(0.12f, 0.15f, 0.19f, 0.14f));
                    for (var x = left; x < left + cols * cell; x += 16)
                    {
                        Png.Put(image, rope, x, line - 2, 16, 4, Rgba.Hex("#986D50"));
                    }
                }
                else
                {
                    Rect(image, left, line - 1, cols * cell, 2, Rgba.Hex("#1E2732"));
                }
                Disc(image, left, line, after ? 3 : 4, Rgba.Hex(after ? "#D9955A" : "#1E2732"));
                Disc(image, left + cols * cell, line, after ? 3 : 4, Rgba.Hex(after ? "#D9955A" : "#1E2732"));
                Png.Save(image, Path.Combine(after ? afterDir : beforeDir, dense ? "dense.png" : "sparse.png"));
            }
        }

        /// <summary>Прямоугольник для сравнительного листа.</summary>
        private static void Rect(Raster image, int x, int y, int w, int h, Rgba color)
        {
            for (var py = y; py < y + h; py++)
            for (var px = x; px < x + w; px++)
            {
                image.Blend(px, py, color);
            }
        }

        /// <summary>Небольшое круглое крепление для сравнительного листа.</summary>
        private static void Disc(Raster image, int x, int y, float radius, Rgba color)
        {
            for (var py = (int)(y - radius); py <= y + radius; py++)
            for (var px = (int)(x - radius); px <= x + radius; px++)
            {
                if ((px - x) * (px - x) + (py - y) * (py - y) <= radius * radius)
                {
                    image.Blend(px, py, color);
                }
            }
        }

        /// <summary>Прицельное сравнение общего материала и реальных картинок без Unity.</summary>
        private static void TextileMaterial(string dir)
        {
            Directory.CreateDirectory(dir);
            var sheet = new Raster(576, 768);
            var comparisonColors = new[] { "#FF6FB7", "#E8333A", "#FFFFFF", "#F5C542" };
            for (var variant = 0; variant < 3; variant++)
            {
                foreach (var coarse in new[] { false, true })
                {
                    var label = coarse ? "coarse" : "fine";
                    var body = KnitRaster.TextileTile(variant, coarse, false);
                    var gloss = KnitRaster.TextileTile(variant, coarse, true);
                    Png.Save(body, Path.Combine(dir, $"body-{variant}-{label}.png"));
                    Png.Save(gloss, Path.Combine(dir, $"gloss-{variant}-{label}.png"));
                    if (!coarse)
                    {
                        for (var row = 0; row < comparisonColors.Length; row++)
                        for (var py = 0; py < 3; py++)
                        for (var px = 0; px < 3; px++)
                        {
                            var sx = variant * 192 + px * 64;
                            var sy = (3 - row) * 192 + py * 64;
                            Png.Put(sheet, body, sx, sy, 64, 64, Rgba.Hex(comparisonColors[row]));
                            Png.Put(sheet, gloss, sx, sy, 64, 64);
                        }
                    }
                    if (variant != 0 || coarse) { continue; }
                    foreach (var color in new[] { ("pink", "#FF6FB7"), ("red", "#E8333A") })
                    {
                        var swatch = new Raster(720, 720);
                        for (var y = 0; y < 6; y++)
                        for (var x = 0; x < 6; x++)
                        {
                            Png.Put(swatch, body, x * 120, y * 120, 120, 120, Rgba.Hex(color.Item2));
                            Png.Put(swatch, gloss, x * 120, y * 120, 120, 120);
                        }
                        Png.Save(swatch, Path.Combine(dir, $"swatch-{color.Item1}.png"));
                    }
                }
            }
            Png.Save(sheet, Path.Combine(dir, "variant-sheet.png"));
            foreach (var id in new[] { 1, 24 })
            {
                using var json = JsonDocument.Parse(File.ReadAllText($"Assets/Resources/Levels/level_{id:000}.json"));
                var root = json.RootElement;
                var rows = root.GetProperty("picture").EnumerateArray().Select(e => e.GetString()!).ToArray();
                var masks = root.GetProperty("canvas").EnumerateArray().Select(e => e.GetString()!).ToArray();
                var palette = root.GetProperty("colors").EnumerateArray().Select(e => Rgba.Hex(e.GetString()!)).ToArray();
                var w = root.GetProperty("width").GetInt32();
                var h = root.GetProperty("height").GetInt32();
                var colors = new Rgba?[w * h];
                var canvas = new bool[w * h];
                for (var y = 0; y < h; y++)
                for (var x = 0; x < w; x++)
                {
                    var i = y * w + x;
                    canvas[i] = masks[y][x] == '1';
                    colors[i] = canvas[i] ? Rgba.Hex("#EAE4D9") : palette[rows[y][x] - '0'];
                }
                var variants = KnitRaster.TextileVariants(w, h, (x, y) => rows[y][x] - '0', (x, y) => canvas[y * w + x], false);
                Png.Save(MetaRaster.Mini(w, h, colors, canvas, 512, variants), Path.Combine(dir, $"level-{id:000}-hero.png"));
                Png.Save(MetaRaster.Mini(w, h, colors, canvas, 660, variants), Path.Combine(dir, $"level-{id:000}-field-660.png"));
                Png.Save(MetaRaster.Mini(w, h, colors, canvas, 128, variants), Path.Combine(dir, $"level-{id:000}-small.png"));
                for (var i = 0; i < colors.Length; i++)
                {
                    if (!canvas[i]) { colors[i] = Rgba.Hex("#E2E6EB"); }
                }
                Png.Save(MetaRaster.Mini(w, h, colors, canvas, 512, new int[w * h], false), Path.Combine(dir, $"level-{id:000}-mystery-sketch.png"));
            }
        }

        /// <summary>Сундуки пяти редкостей в ряд: от обычного до легендарного.</summary>
        private static void Chests(string dir)
        {
            var img = new Raster(5 * 230 + 20, 200);
            Png.Fill(img, Rgba.Hex("#F3F5F8"));
            for (var tier = 0; tier < 5; tier++)
            {
                var x = 20 + tier * 230;
                Png.Put(img, MetaRaster.ChestBody(200, 107, tier), x, 20, 200, 107);
                Png.Put(img, MetaRaster.ChestLid(211, 67, tier), x - 5.5f, 20 + 107 - 18, 211, 67);
            }

            Png.Save(img, Path.Combine(dir, "chests.png"));
        }

        /// <summary>Старый дизайн, детальнее: картинка из вязаных клеток, ткань, искры.</summary>
        private static void Classic(string dir)
        {
            var shade = KnitRaster.StitchTile(false);
            var gloss = KnitRaster.StitchTile(true);
            var canvas = KnitRaster.CanvasTile();
            string[] heart =
            {
                "0000000000", "0110001100", "1111011110", "1111111111", "1111111111",
                "0111111110", "0011111100", "0001111000", "0000110000", "0000000000",
            };
            const int cell = 40, gap = 4;
            var img = new Raster(10 * (cell + gap) + 60, 10 * (cell + gap) + 60);
            Png.Fill(img, Rgba.Hex("#FFFFFF"));
            var red = Rgba.Hex("#F0405E");
            var cream = Rgba.Hex("#EDE3F7");
            for (var y = 0; y < 10; y++)
            {
                for (var x = 0; x < 10; x++)
                {
                    var px = 30 + x * (cell + gap);
                    var py = img.Height - 30 - (y + 1) * (cell + gap);
                    if (heart[y][x] == '1')
                    {
                        Png.Put(img, shade, px, py, cell, cell, red);
                        Png.Put(img, gloss, px, py, cell, cell);
                    }
                    else
                    {
                        Png.Put(img, canvas, px, py, cell, cell, cream);
                    }
                }
            }

            Png.Put(img, KnitRaster.Sparkle(), img.Width - 70, img.Height - 70, 40, 40, Rgba.Hex("#FFC23C"));
            Png.Save(img, Path.Combine(dir, "classic.png"));
        }

        /// <summary>
        /// Как на экране телефона: клубки поля в размере ~140 px и клетки картинки ~60 px (ткань и вязаные),
        /// справа — крупный клубок, чтобы рассмотреть нити, ворс и завиток.
        /// </summary>
        private static void Field(string dir)
        {
            var img = new Raster(1240, 700);
            Png.Fill(img, Rgba.Hex("#FFFFFF"));
            var shadow = BallRaster.Shadow();
            string[] row1 = { "#F2303F", "#F2303F", "#EFE3C4", "#C8102E", "#F5C542" };
            string[] row2 = { "#D9828E", "#FA98A0", "#D9828E", "#FA98A0", "#5FD7A5" };
            void Ball(Raster ball, float x, float y, float size)
            {
                Png.Put(img, shadow, x + size * 0.04f, y - size * 0.1f, size * 0.95f, size * 0.3f);
                Png.Put(img, ball, x - size * 0.1f, y - size * 0.12f, size / Art2BallFill, size / Art2BallFill);
            }

            for (var i = 0; i < 5; i++)
            {
                var canvas = i == 2;
                Ball(BallRaster.Ball(Rgba.Hex(row1[i]), canvas ? BallRaster.Kind.Canvas : BallRaster.Kind.Plain), 30 + i * 150, 520, 124);
                Ball(BallRaster.Ball(Rgba.Hex(row2[i])), 30 + i * 150, 370, 124);
            }

            Ball(BallRaster.Ball(Rgba.White, BallRaster.Kind.Rainbow), 30, 220, 124);
            Ball(BallRaster.Ball(Rgba.Hex("#C9C2E8")), 180, 220, 124);
            Ball(BallRaster.Ball(Rgba.Hex("#F2303F"), BallRaster.Kind.Thick), 330, 220, 124);
            Ball(BallRaster.Hidden(), 480, 220, 124);

            var tile = KnitRaster.CanvasTile();
            var stitch = KnitRaster.StitchTile(false);
            var gloss = KnitRaster.StitchTile(true);
            for (var i = 0; i < 9; i++)
            {
                var x = 30 + i * 66;
                if (i < 5)
                {
                    Png.Put(img, tile, x, 40, 60, 60, Rgba.Hex("#EED9A6"));
                    Png.Put(img, tile, x, 106, 60, 60, Rgba.Hex("#EED9A6"));
                }
                else
                {
                    Png.Put(img, stitch, x, 40, 60, 60, Rgba.Hex("#F2303F"));
                    Png.Put(img, gloss, x, 40, 60, 60);
                    Png.Put(img, stitch, x, 106, 60, 60, Rgba.Hex("#FA98A0"));
                    Png.Put(img, gloss, x, 106, 60, 60);
                }
            }

            Png.Put(img, BallRaster.Ball(Rgba.Hex("#F2303F")), 800, 230, 440, 440);
            Png.Put(img, tile, 800, 20, 200, 200, Rgba.Hex("#EED9A6"));
            Png.Put(img, stitch, 1020, 20, 200, 200, Rgba.Hex("#F2303F"));
            Png.Put(img, gloss, 1020, 20, 200, 200);
            Png.Save(img, Path.Combine(dir, "field.png"));
        }

        /// <summary>
        /// Карточки-«подушки», бирки на клубках и кнопки бустеров с лункой — как на игровом экране (2 px на точку).
        /// </summary>
        private static void Panels(string dir)
        {
            const float k = 2f;
            var img = new Raster((int)(390 * k), (int)(470 * k));
            Png.Fill(img, Rgba.Hex("#E9ECF0"));
            const int density = 3;
            void Relief(Func<int, Raster> make, float x, float top, float w, float h, float radius, Rgba tint)
            {
                var r = Math.Max(2, (int)MathF.Ceiling(radius * density));
                Sliced(img, make(r), r + 1, x * k, img.Height - (top + h) * k, w * k, h * k, (r + 1) * k / density, tint);
            }

            void Card(float x, float top, float w, float h, float radius, Rgba tint) => Relief(KnitRaster.SoftCard, x, top, w, h, radius, tint);

            // Поле с клубками и бирками.
            Card(12, 10, 366, 150, 24, Rgba.White);
            string[] colors = { "#F2303F", "#F2303F", "#C8102E", "#F5C542", "#5FD7A5" };
            var tag = KnitRaster.Tag();
            for (var i = 0; i < colors.Length; i++)
            {
                var size = 58f;
                var cx = 50 + i * 72;
                var art = size / (BallRaster.Radius * 2f / BallRaster.Size);
                var ball = BallRaster.Ball(Rgba.Hex(colors[i]));
                Png.Put(img, BallRaster.Shadow(), (cx - size * 0.45f) * k, img.Height - (40 + size * 1.05f) * k, size * 0.95f * k, size * 0.3f * k);
                Png.Put(img, ball, (cx - art / 2f - 2f) * k, img.Height - (40 + size / 2f + art / 2f) * k + 3f * k, art * k, art * k);
                var th = size * 0.42f * 1.1f;
                var tw = th * 1.8f;
                Png.Put(img, tag, (cx + size / 2f + size * 0.2f - tw) * k, img.Height - (40 + size - th * 0.75f + th) * k, tw * k, th * k, Rgba.Hex("#FFFDF7"));
            }

            // Бустеры: подушка, лунка, значок, бирка с ценой.
            var arts = new[] { LoomRaster.BoosterArt.Scissors, LoomRaster.BoosterArt.Undo, LoomRaster.BoosterArt.Slot };
            var well = KnitRaster.IconWell();
            for (var i = 0; i < 3; i++)
            {
                var x = 20 + i * 120f;
                Card(x, 200, 110, 60, 18, Rgba.White);
                Png.Put(img, well, (x + 14) * k, img.Height - (200 + 30 + 22) * k, 44 * k, 44 * k);
                Png.Put(img, LoomRaster.Booster(arts[i]), (x + 18) * k, img.Height - (200 + 30 + 18) * k, 36 * k, 36 * k);
                Png.Put(img, tag, (x + 110 - 40) * k, img.Height - (200 - 6 + 24) * k, 43 * k, 24 * k, i == 0 ? Rgba.Hex("#1E2732") : Rgba.Hex("#FFC23C"));
            }

            // Главная кнопка как в игре: «подошва» темнее, лицо, глянцевый блик (Ui.Button + Ui.Shine).
            Relief(KnitRaster.SoftCard, 20, 285, 350, 44, 22, Rgba.Hex("#0F6E46"));
            Card(20, 280, 350, 44, 22, Rgba.Hex("#17945F"));
            Relief(KnitRaster.GlossOverlay, 20, 280, 350, 44, 22, Rgba.White);

            // Объём: вдавленная дорожка с глянцевой заливкой, круглые кнопки, выпуклые клетки.
            Relief(KnitRaster.Inset, 20, 340, 220, 22, 11, Rgba.Hex("#E2E6EB"));
            Relief(KnitRaster.Gloss, 22, 342, 140, 18, 9, Rgba.Hex("#FFB43C"));
            Relief(KnitRaster.GlossOverlay, 22, 342, 140, 18, 9, Rgba.White);
            var disc = KnitRaster.SoftDisc();
            Png.Put(img, disc, 260 * k, img.Height - (330 + 44) * k, 44 * k, 44 * k, Rgba.White);
            Png.Put(img, disc, 316 * k, img.Height - (330 + 44) * k, 44 * k, 44 * k, Rgba.Hex("#8B6BE8"));
            var tile = KnitRaster.SoftTile();
            for (var i = 0; i < 6; i++)
            {
                Png.Put(img, tile, (20 + i * 58) * k, img.Height - (395 + 52) * k, 52 * k, 52 * k, i % 2 == 0 ? Rgba.White : Rgba.Hex("#E5E9EF"));
            }

            Png.Save(img, Path.Combine(dir, "panels.png"));
        }

        /// <summary>Объёмные значки: кубки, камни редкости, звезда уровня, корзинка с клубками, меланж.</summary>
        private static void Icons(string dir)
        {
            var img = new Raster(1200, 300);
            Png.Fill(img, Rgba.Hex("#F3F5F8"));
            Png.Put(img, IconRaster.Trophy(true), 20, 150, 128, 128);
            Png.Put(img, IconRaster.Trophy(false), 160, 150, 128, 128);
            for (var i = 0; i < 3; i++)
            {
                Png.Put(img, IconRaster.Gem(i), 300 + i * 90, 170, 80, 80);
            }

            Png.Put(img, IconRaster.LevelStar(), 580, 160, 100, 100);
            Png.Put(img, IconRaster.House(), 20, 10, 110, 110);
            Png.Put(img, IconRaster.Album(), 140, 10, 110, 110);
            Png.Put(img, IconRaster.Gear(), 260, 10, 110, 110);
            Png.Put(img, IconRaster.RewardStar(true), 390, 10, 110, 110);
            Png.Put(img, IconRaster.RewardStar(false), 510, 10, 110, 110);
            Png.Put(img, IconRaster.BasketBack(), 700, 40, 240, 192);
            string[] balls = { "#F2303F", "#5FD7A5", "#2F6FDE" };
            for (var i = 0; i < 3; i++)
            {
                var size = 64f;
                var art = size / Art2BallFill;
                Png.Put(img, BallRaster.Ball(Rgba.Hex(balls[i])), 760 + i * 52 - art / 2f + size / 2f, 120 - art / 2f + size / 2f + (i == 1 ? 10 : 0), art, art);
            }

            Png.Put(img, IconRaster.BasketFront(), 700, 40, 240, 192);
            Png.Put(img, IconRaster.Melange(Rgba.Hex("#F49AC1"), Rgba.Hex("#FFFFFF"), 40, 48, 592), 980, 60, 200, 200);
            Png.Save(img, Path.Combine(dir, "icons.png"));
        }

        /// <summary>Макет плиток раздела «Пряжа»: обычная, редкая (меланж), эпическая, не найдена (2 px на точку).</summary>
        private static void YarnTiles(string dir)
        {
            const float k = 2f;
            const float cellW = 81f;
            const float cellH = 140f;
            var img = new Raster((int)(4 * (cellW + 8) * k + 20 * k), (int)((cellH + 20) * k));
            Png.Fill(img, Rgba.Hex("#F3F5F8"));
            var glow = new Raster(128, 128);
            glow.Paint((x, y) =>
            {
                var t = Draw.Clamp01(1f - MathF.Sqrt((x - 64) * (x - 64) + (y - 64) * (y - 64)) / 64f);
                return Rgba.White.WithAlpha(t * t);
            });
            string[] backs = { "#F3F1FA", "#F3EDFF", "#FFF4D9", "#F3F1FA" };
            string[] glows = { "", "#2F6FDE", "#FFC23C", "" };
            var tag = 0;
            for (var i = 0; i < 4; i++)
            {
                var x0 = (10 + i * (cellW + 8)) * k;
                float Top(float topY, float h) => img.Height - (10 + topY + h) * k;
                var r = (int)MathF.Ceiling(16 * 3);
                Sliced(img, KnitRaster.SoftCard(r), r + 1, x0, Top(0, cellH), cellW * k, cellH * k, (r + 1) * k / 3f, Rgba.Hex(backs[i]));
                const float ballY = 46f;
                const float ball = 58f;
                if (glows[i].Length > 0)
                {
                    var g = ball * 1.7f;
                    Png.Put(img, glow, x0 + (cellW / 2f - g / 2f) * k, Top(ballY - g / 2f, g), g * k, g * k, Rgba.Hex(glows[i]).WithAlpha(0.28f));
                }

                Png.Put(img, BallRaster.Shadow(), x0 + (cellW / 2f - ball * 0.45f + 2f) * k, Top(ballY + ball * 0.46f - ball * 0.13f, ball * 0.26f), ball * 0.9f * k, ball * 0.26f * k);
                var art = ball / Art2BallFill;
                Raster ballArt = i switch
                {
                    1 => IconRaster.Melange(Rgba.Hex("#F49AC1"), Rgba.Hex("#FFFFFF"), 40, 48, 592),
                    2 => BallRaster.Ball(Rgba.Hex("#2F6FDE")),
                    3 => BallRaster.Ball(Rgba.Hex("#E6E2F0")),
                    _ => BallRaster.Ball(Rgba.Hex("#F4E4C1")),
                };
                if (i == 1)
                {
                    Png.Put(img, ballArt, x0 + (cellW / 2f - ball / 2f) * k, Top(ballY - ball / 2f, ball), ball * k, ball * k);
                }
                else
                {
                    Png.Put(img, ballArt, x0 + (cellW / 2f - art / 2f) * k, Top(ballY - art / 2f, art), art * k, art * k);
                }

                if (i < 3)
                {
                    Png.Put(img, IconRaster.LevelStar(), x0 + (cellW - 17f - 15f) * k, Top(2f, 30f), 30 * k, 30 * k);
                }

                var chipW = 70f;
                Sliced(img, KnitRaster.SoftCard(30), 31, x0 + (cellW / 2f - chipW / 2f) * k, Top(108f, 20f), chipW * k, 20 * k, 31 * k / 3f,
                    i == 2 ? Rgba.Hex("#FFF0C2") : i == 1 ? Rgba.Hex("#EDE4FF") : Rgba.Hex("#E6E2F2"));
                Png.Put(img, IconRaster.Gem(i == 3 ? 2 : i == 0 ? 0 : i), x0 + (cellW / 2f - chipW / 2f + 5f) * k, Top(110f, 16f), 16 * k, 16 * k);
                tag++;
            }

            Png.Save(img, Path.Combine(dir, "yarn-tiles.png"));
        }

        /// <summary>9-срез: углы в масштабе, края и середина растянуты.</summary>
        private static void Sliced(Raster dst, Raster src, int border, float x, float y, float w, float h, float corner, Rgba tint)
        {
            var cw = src.Width - 2 * border;
            var ch = src.Height - 2 * border;
            float[] sx = { 0, border, border + cw, src.Width };
            float[] dx = { x, x + corner, x + w - corner, x + w };
            float[] sy = { 0, border, border + ch, src.Height };
            float[] dy = { y, y + corner, y + h - corner, y + h };
            for (var j = 0; j < 3; j++)
            {
                for (var i = 0; i < 3; i++)
                {
                    var piece = src.Crop((int)sx[i], (int)sy[j], (int)(sx[i + 1] - sx[i]), (int)(sy[j + 1] - sy[j]));
                    Png.Put(dst, piece, dx[i], dy[j], dx[i + 1] - dx[i], dy[j + 1] - dy[j], tint);
                }
            }
        }

        private const float Art2BallFill = BallRaster.Radius * 2f / BallRaster.Size;

        /// <summary>Медали достижений: верхний ряд золотые, нижний серебряные, 2 точки на пиксель макета.</summary>
        private static void Badges(string dir)
        {
            var arts = (BadgeArt[])Enum.GetValues(typeof(BadgeArt));
            const int cell = 150;
            var img = new Raster(arts.Length * cell + 20, 2 * cell + 20);
            Png.Fill(img, Rgba.Hex("#F3F5F8"));
            for (var i = 0; i < arts.Length; i++)
            {
                Png.Put(img, IconRaster.Badge(arts[i], true), 10 + i * cell, cell + 10, 128, 128);
                Png.Put(img, IconRaster.Badge(arts[i], false), 10 + i * cell, 10, 128, 128);
            }

            Png.Save(img, Path.Combine(dir, "badges.png"));
        }

        /// <summary>
        /// Замер: сколько миллисекунд рисуется каждый растр игры в один поток (на телефоне в 5–10 раз дольше).
        /// Первый прогон прогревает JIT, считаем лучший из трёх. dotnet run -c Release -- --bench
        /// </summary>
        private static void Bench()
        {
            var cases = new System.Collections.Generic.List<(string Name, Func<Raster> Draw, int Count)>
            {
                ("Frame (×32)", () => FrameRaster.Frame(26), FrameRaster.Styles),
                ("Frame live (legendary)", () => FrameRaster.Frame(30), 1),
                ("Medal (×24)", () => IconRaster.Badge(BadgeArt.Basket, true), 24),
                ("Gem (×5)", () => IconRaster.Gem(4), 5),
                ("Trophy (×2)", () => IconRaster.Trophy(true), 2),
                ("ChestBody (×5)", () => MetaRaster.ChestBody(660, 354, 4), 5),
                ("ChestLid (×5)", () => MetaRaster.ChestLid(696, 222, 4), 5),
                ("Gift", MetaRaster.Gift, 1),
                ("Coin", MetaRaster.Coin, 1),
                ("Hoop", MetaRaster.Hoop, 1),
                ("Ball plain (×12)", () => BallRaster.Ball(Rgba.Hex("#E8333A")), 12),
                ("LevelStar", IconRaster.LevelStar, 1),
                ("House", IconRaster.House, 1),
                ("Album", IconRaster.Album, 1),
                ("Gear", IconRaster.Gear, 1),
                ("BasketBack", IconRaster.BasketBack, 1),
                ("BasketFront", IconRaster.BasketFront, 1),
                ("WoodFrame", KnitRaster.WoodFrame, 1),
                ("LoomBar", LoomRaster.Bar, 1),
                ("Sparkle", KnitRaster.Sparkle, 1),
            };
            var rows = new System.Collections.Generic.List<(string, double, double)>();
            foreach (var (name, draw, count) in cases)
            {
                var best = double.MaxValue;
                for (var i = 0; i < 3; i++)
                {
                    var clock = System.Diagnostics.Stopwatch.StartNew();
                    draw();
                    best = Math.Min(best, clock.Elapsed.TotalMilliseconds);
                }

                rows.Add((name, best, best * count));
            }

            rows.Sort((a, b) => b.Item3.CompareTo(a.Item3));
            Console.WriteLine($"{"растр",-20} {"одна, мс",9} {"все, мс",9}");
            foreach (var (name, one, all) in rows)
            {
                Console.WriteLine($"{name,-20} {one,9:0.0} {all,9:0.0}");
            }

            Console.WriteLine($"{"итого",-20} {"",9} {rows.Sum(r => r.Item3),9:0.0}");
        }

        /// <summary>Рамки альбома: все стили в ряд, крупно и в размере плитки.</summary>
        private static void Frames(string dir)
        {
            const int cell = 150;
            const int cols = 9;
            var rows = (FrameRaster.Styles + cols - 1) / cols;
            var img = new Raster(cols * cell + 20, rows * cell + 20);
            Png.Fill(img, Rgba.Hex("#F3F5F8"));
            for (var i = 0; i < FrameRaster.Styles; i++)
            {
                Png.Put(img, FrameRaster.Frame(i), 10 + (i % cols) * cell, 10 + (rows - 1 - i / cols) * cell, 140, 140);
            }

            Png.Save(img, Path.Combine(dir, "frames.png"));
        }

        /// <summary>Живые рамки крупно (статичный рисунок), сияние поверх «Северного сияния» и частицы эффектов.</summary>
        private static void MagicFrames(string dir)
        {
            const int cell = 300;
            var styles = new[] { 27, 28, 29, 30, 31 };
            var img = new Raster(styles.Length * cell + 20, cell + 120);
            Png.Fill(img, Rgba.Hex("#F3F5F8"));
            for (var i = 0; i < styles.Length; i++)
            {
                Png.Put(img, FrameRaster.Frame(styles[i]), 10 + i * cell, 110, 290, 290);
            }

            Png.Put(img, FrameRaster.Aurora(0), 10 + cell, 110, 290, 290, Rgba.Hex("#7CF5C4"), 0.7f);
            var tints = new[] { "#FFFFFF", "#FFFFFF", "#E8742A", "#FFFFFF", "#FF5A7A", "#5EB8FF", "#5A3A22", "#8FD3FF" };
            for (var k = 0; k < tints.Length; k++)
            {
                Png.Put(img, FrameRaster.Mote(k), 20 + k * 100, 10, 80, 80, Rgba.Hex(tints[k]));
            }

            Png.Save(img, Path.Combine(dir, "frames-magic.png"));
        }

        /// <summary>Мета: шкатулка (закрытая), подарок, монета, пяльцы с вязаным сердечком.</summary>
        private static void Meta(string dir)
        {
            var img = new Raster(1100, 420);
            Png.Fill(img, Rgba.Hex("#F3F5F8"));
            var body = MetaRaster.ChestBody(330, 177, 0);
            var lid = MetaRaster.ChestLid(348, 111, 0);
            Png.Put(img, body, 40, 60, 330, 177);
            Png.Put(img, lid, 31, 60 + 177 - 30, 348, 111);
            Png.Put(img, MetaRaster.Gift(), 400, 90, 192, 192);
            Png.Put(img, MetaRaster.Coin(), 610, 140, 96, 96);

            string[] heart =
            {
                "0000000000", "0110001100", "1111011110", "1111111111", "1111111111",
                "0111111110", "0011111100", "0001111000", "0000110000", "0000000000",
            };
            var colors = new Rgba?[100];
            var canvas = new bool[100];
            for (var y = 0; y < 10; y++)
            {
                for (var x = 0; x < 10; x++)
                {
                    colors[y * 10 + x] = heart[y][x] == '1' ? Rgba.Hex("#E8475F") : (Rgba?)null;
                }
            }

            var mini = MetaRaster.Mini(10, 10, colors, canvas);
            var cloth = new Raster(300, 300);
            cloth.Paint((x, y) => Rgba.Hex("#FBF5EA").WithAlpha(Draw.Cover(Draw.Circle(x, y, 150, 150, 148))));
            Png.Put(img, cloth, 760, 45, 300, 300);
            Png.Put(img, mini, 760 + 60, 45 + 70, 180, 180 * mini.Height / mini.Width);
            Png.Put(img, MetaRaster.Hoop(), 760 - 30, 45 - 30, 360, 375);
            Png.Save(img, Path.Combine(dir, "meta.png"));
        }

        /// <summary>Отдельные спрайты для сборки макета экрана (tools/design/screen_mock.py).</summary>
        private static void Export(string dir)
        {
            Directory.CreateDirectory(dir);
            void Save(string name, Raster r) => Png.Save(r, Path.Combine(dir, name + ".png"));
            foreach (var hex in Yarn)
            {
                var c = Rgba.Hex(hex);
                var key = hex.TrimStart('#');
                Save("stitch_" + key, KnitRaster.Stitch(c));
                Save("loop_" + key, KnitRaster.Loop(c));
                Save("ball_" + key, BallRaster.Ball(c));
                Save("deep_" + key, BallRaster.Ball(Rgba.Lerp(c, new Rgba(0.91f, 0.89f, 0.92f), 0.5f)));
            }

            Save("needle", KnitRaster.Needle());
            Save("knob", KnitRaster.Knob());
            Save("wood", KnitRaster.WoodFrame());
            Save("paper", KnitRaster.PaperTile());
            Save("rope", KnitRaster.RopeTile());
            Save("ribbon", BallRaster.Ribbon());
            Save("padlock", BallRaster.Padlock());
            Save("key", BallRaster.Key());
            Save("knots2", BallRaster.Knots(2));
            Save("ice1", BallRaster.Ice(1));
            Save("hidden", BallRaster.Hidden());
            Save("badge", BallRaster.Badge());
            Save("shadow", BallRaster.Shadow());
            Save("loombar", LoomRaster.Bar());
            Save("well", LoomRaster.Well());
            Save("glow", LoomRaster.Glow());
            Save("loomneedle", LoomRaster.Needle());
            Save("b_scissors", LoomRaster.Booster(LoomRaster.BoosterArt.Scissors));
            Save("b_undo", LoomRaster.Booster(LoomRaster.BoosterArt.Undo));
            Save("b_slot", LoomRaster.Booster(LoomRaster.BoosterArt.Slot));
            Save("frame", KnitRaster.DashedFrame(66, 4.8f, 18, 9.6f, 7.2f));
        }

        /// <summary>Станок на 5 слотов с двумя клубками и строкой бустеров.</summary>
        private static void Loom(string dir)
        {
            var img = new Raster(760, 330);
            Png.Fill(img, Rgba.Hex("#F7F1EA"));
            var needle = LoomRaster.Needle();
            Png.Put(img, needle, 300, 230, 420, 40);
            Png.Put(img, LoomRaster.Bar(), 30, 120, 700, 130);
            var well = LoomRaster.Well();
            var glow = LoomRaster.Glow();
            for (var i = 0; i < 5; i++)
            {
                var cx = 30 + 70 + i * 140f;
                Png.Put(img, well, cx - 46, 142, 92, 92);
                if (i == 4)
                {
                    Png.Put(img, glow, cx - 56, 132, 112, 112, Rgba.Hex("#FF9A3C"));
                }
            }

            Png.Put(img, BallRaster.Ball(Rgba.Hex("#E8475F")), 30 + 70 - 58, 132, 116, 116);
            Png.Put(img, BallRaster.Ball(Rgba.Hex("#5AA9F0")), 30 + 210 - 58, 132, 116, 116);
            var arts = new[] { LoomRaster.BoosterArt.Scissors, LoomRaster.BoosterArt.Undo, LoomRaster.BoosterArt.Slot, LoomRaster.BoosterArt.Time };
            for (var i = 0; i < arts.Length; i++)
            {
                var card = new Raster(120, 120);
                card.Paint((x, y) => Rgba.White.WithAlpha(Draw.Cover(Draw.RoundBox(x, y, 60, 60, 58, 58, 30))));
                Png.Put(img, card, 60 + i * 170, 6, 110, 110);
                Png.Put(img, LoomRaster.Booster(arts[i]), 75 + i * 170, 21, 80, 80);
            }

            Png.Save(img, Path.Combine(dir, "loom.png"));
        }

        private static readonly string[] Yarn =
        {
            "#E8475F", "#FF8A3D", "#FFD23F", "#8BD346", "#1FA67A", "#2EC4C9",
            "#5AA9F0", "#3D4FC4", "#8E52D9", "#F27BC0", "#8A5A3C", "#F4EEE4",
        };

        /// <summary>Лист клубков как страница «Клубки» в Figma: цвета, виды, накладки, задний ряд.</summary>
        private static void Balls(string dir)
        {
            const int cell = 96;
            var sheet = new Raster(cell * 8, cell * 4);
            Png.Fill(sheet, Rgba.Hex("#F7F1EA"));
            var shadow = BallRaster.Shadow();
            void Put(Raster ball, int col, int row, float alpha = 1f, float scale = 1f)
            {
                var x = col * cell + 8f;
                var y = (3 - row) * cell + 6f;
                var size = (cell - 16f) * scale;
                Png.Put(sheet, shadow, x + 10f, y - 2f, size * 0.8f, size * 0.25f, null, alpha);
                Png.Put(sheet, ball, x + (cell - 16f - size) / 2f, y + (cell - 16f - size) * 0.2f, size, size, null, alpha);
            }

            for (var i = 0; i < Yarn.Length; i++)
            {
                Put(BallRaster.Ball(Rgba.Hex(Yarn[i])), i % 8, i / 8);
            }

            Put(BallRaster.Ball(Rgba.White, BallRaster.Kind.Rainbow), 4, 1);
            Put(BallRaster.Ball(Rgba.Hex("#E9E0CF"), BallRaster.Kind.Canvas), 5, 1);
            Put(BallRaster.Ball(Rgba.Hex("#E8475F"), BallRaster.Kind.Thick), 6, 1, 1f, 1.1f);
            Put(BallRaster.Hidden(), 7, 1);

            var red = BallRaster.Ball(Rgba.Hex("#E8475F"));
            var ribbon = BallRaster.Ribbon();
            var overlays = new[]
            {
                (Raster?)null, ribbon, BallRaster.Padlock(), BallRaster.Key(), BallRaster.Knots(1), BallRaster.Knots(3),
                BallRaster.Ice(0), BallRaster.Ice(3),
            };
            for (var i = 0; i < overlays.Length; i++)
            {
                var ball = new Raster(BallRaster.Size, BallRaster.Size);
                Png.Put(ball, red, 0, 0, BallRaster.Size, BallRaster.Size);
                if (overlays[i] != null)
                {
                    Png.Put(ball, overlays[i]!, 0, 0, BallRaster.Size, BallRaster.Size, i == 1 ? Rgba.Hex("#2F6FDE") : (Rgba?)null);
                }

                Png.Put(ball, BallRaster.Badge(), 104, 4, 52, 52);
                Put(ball, i, 2);
            }

            // Задний ряд: бледнее (как в макете — пастель на фоне).
            for (var i = 0; i < 8; i++)
            {
                Put(BallRaster.Ball(Rgba.Lerp(Rgba.Hex(Yarn[i]), Rgba.Hex("#E9E2EA"), 0.5f)), i, 3, 0.85f, 0.88f);
            }

            Png.Save(sheet, Path.Combine(dir, "balls.png"));
        }

        /// <summary>Макет доски «Сердечко» как на экране Figma: 12×10, связано 5 рядов и часть шестого.</summary>
        private static void Board(string dir, Rgba red, Rgba blue)
        {
            const float k = 2f; // пикселей на точку макета
            const float bw = 346f, bh = 285f;
            var img = new Raster((int)(bw * k) + 80, (int)(bh * k) + 80);
            Png.Fill(img, Rgba.Hex("#F7F1EA"));
            var ox = 40f;
            var oy = 40f;

            // Рамка и бумага.
            var wood = KnitRaster.WoodFrame();
            Png.Put(img, wood, ox, oy, bw * k, bh * k);
            var paper = KnitRaster.PaperTile();
            const float m = 8f;
            for (var py = oy + m * k; py < oy + (bh - m) * k; py += 128)
            {
                for (var px = ox + m * k; px < ox + (bw - m) * k; px += 128)
                {
                    Png.Put(img, paper, px, py, MathF.Min(128, ox + (bw - m) * k - px), MathF.Min(128, oy + (bh - m) * k - py));
                }
            }

            // Сетка.
            string[] heart =
            {
                "000000000000", // row 10 (top)
                "001100001100",
                "011110011110",
                "111111111111",
                "111111111111",
                "011111111110",
                "001111111100",
                "000111111000",
                "000011110000",
                "000001100000", // row 1 (bottom)
            };
            const int cols = 12, rows = 10;
            const float header = 34f, left = 22f, right = 8f;
            var cw = MathF.Min((bw - 2 * m - left - right) / cols, 30f);
            var rh = cw * 0.82f;
            var gap = rh * 0.55f;
            var gx = m + left + ((bw - 2 * m - left - right) - cw * cols) / 2f;
            var needleRow = 6; // ряды 0..5 связаны у части столбцов
            var heights = new[] { 6, 6, 6, 6, 6, 6, 6, 5, 5, 5, 5, 5 };
            var ny = m + header + 3.3f * rh + gap / 2f; // от верха доски вниз
            float RowCenter(int r) => r < needleRow
                ? ny + gap / 2f + (needleRow - 1 - r) * rh + rh / 2f
                : ny - gap / 2f - (r - needleRow) * rh - rh / 2f;
            float Px(float xPt) => ox + xPt * k;
            float Py(float yPtDown) => oy + (bh - yPtDown) * k; // в пиксели, y вверх

            var cell = KnitRaster.Stitch(red);
            var cellB = KnitRaster.Stitch(blue);
            var sq = new Raster(30, 30);
            sq.Paint((x, y) => Rgba.White.WithAlpha(Draw.Cover(Draw.RoundBox(x, y, 15, 15, 13, 11, 6))));
            // Клетки схемы (непровязанные) и сетка.
            for (var r = 0; r < rows; r++)
            {
                for (var c = 0; c < cols; c++)
                {
                    var col = heart[rows - 1 - r][c] == '1' ? red : blue;
                    if (r < heights[c])
                    {
                        continue;
                    }

                    var cy = RowCenter(r);
                    if (cy < m + header || cy > bh - m)
                    {
                        continue;
                    }

                    var pale = Rgba.Lerp(col, Rgba.Hex("#FBF7EF"), 0.55f);
                    Png.Put(img, sq, Px(gx + c * cw + cw * 0.1f), Py(cy + rh * 0.4f), cw * 0.8f * k, rh * 0.8f * k, pale);
                }
            }

            // Полотно: снизу вверх, верхние ряды поверх нижних.
            var sw = cw * 1.06f;
            var sh = sw * KnitRaster.StitchH / KnitRaster.StitchW;
            for (var r = 0; r < rows; r++)
            {
                for (var c = 0; c < cols; c++)
                {
                    if (r >= heights[c])
                    {
                        continue;
                    }

                    var cy = RowCenter(r);
                    var bottom = cy + rh / 2f;
                    if (bottom - sh > bh - m)
                    {
                        continue;
                    }

                    var src = heart[rows - 1 - r][c] == '1' ? cell : cellB;
                    var cx = gx + c * cw + cw / 2f;
                    Png.Put(img, src, Px(cx - sw / 2f), Py(bottom + sh * KnitRaster.StitchTip * 0.6f), sw * k, sh * k);
                }
            }

            // Спица и живые петли.
            var needle = KnitRaster.Needle();
            var nh = MathF.Max(8f, rh * 0.42f);
            Png.Put(img, needle, Px(m - 2f), Py(ny + nh / 2f), (bw - m) * k, nh * k);
            Png.Put(img, KnitRaster.Knob(), Px(bw - m - 4f), Py(ny + nh * 0.9f), nh * 1.8f * k, nh * 1.8f * k);
            var loop = KnitRaster.Loop(red);
            for (var c = 0; c < cols; c++)
            {
                if (heights[c] != needleRow)
                {
                    continue;
                }

                var cx = gx + c * cw + cw / 2f;
                var lw = cw * 1.15f;
                var lh = lw * 60f / 64f;
                Png.Put(img, loop, Px(cx - lw / 2f), Py(ny + lh * 0.5f), lw * k, lh * k);
            }

            Png.Save(img, Path.Combine(dir, "board.png"));
        }
    }
}
