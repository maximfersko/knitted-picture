using System;
using System.IO;
using KnittedPicture.Game;

namespace KnittedPicture.ArtPreview
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            var dir = args.Length > 0 ? args[0] : "preview";
            Directory.CreateDirectory(dir);
            var red = Rgba.Hex("#E8475F");
            var blue = Rgba.Hex("#5AA9F0");
            Png.Save(KnitRaster.Stitch(red), Path.Combine(dir, "stitch.png"));
            Png.Save(KnitRaster.Loop(red), Path.Combine(dir, "loop.png"));
            Png.Save(KnitRaster.Needle(), Path.Combine(dir, "needle.png"));
            Png.Save(KnitRaster.Knob(), Path.Combine(dir, "knob.png"));
            Png.Save(KnitRaster.RopeTile(), Path.Combine(dir, "rope.png"));
            Png.Save(KnitRaster.WoodFrame(), Path.Combine(dir, "wood.png"));
            Png.Save(KnitRaster.DashedFrame(90, 4, 18, 8, 6), Path.Combine(dir, "dashed.png"));
            Png.Save(KnitRaster.DashedFrame(660, 120, 4.5f, 36, 13.5f, 9), Path.Combine(dir, "stitched.png"));
            Png.Save(KnitRaster.DashedFrame(180, 9, 90, 18, 12), Path.Combine(dir, "ring.png"));
            Board(dir, red, blue);
            Balls(dir);
            Loom(dir);
            Export(Path.Combine(dir, "sprites"));
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
                    Png.Put(ball, overlays[i]!, 0, 0, BallRaster.Size, BallRaster.Size, i == 1 ? Rgba.Hex("#8B5CF6") : (Rgba?)null);
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
