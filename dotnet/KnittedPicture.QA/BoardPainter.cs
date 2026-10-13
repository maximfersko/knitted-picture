using System;
using System.Collections.Generic;
using System.Linq;
using KnittedPicture.Core;
using KnittedPicture.Game;

namespace KnittedPicture.QA
{
    /// <summary>
    /// «Скриншот» партии: картинка (связанное — цветом, остальное бледно, фронт обведён), станок со слотами
    /// и поле с колонками клубков — со всеми значками (узелки, замок, лёд, связка, толстый, ключ, скрытый,
    /// радуга, полёт). Красная рамка — то место, где сработал оракул. Рисуется тем же растром, что спрайты игры.
    /// </summary>
    public static class BoardPainter
    {
        private static readonly Rgba Back = Rgba.Hex("#F7F3FF");
        private static readonly Rgba Ink = Rgba.Hex("#2B2350");
        private static readonly Rgba Faint = Rgba.Hex("#D9D2EE");
        private static readonly Rgba Mark = Rgba.Hex("#FF2D55");
        private static readonly Rgba Hidden = Rgba.Hex("#B9B4C8");
        private static readonly Rgba Ice = Rgba.Hex("#7FD8F0");
        private static readonly Rgba Gold = Rgba.Hex("#F5B800");

        private const int Pad = 12;
        private const int SlotSize = 36;
        private const int ColumnW = 42;
        private const int SpoolR = 15;
        private const int MaxDepth = 7;

        public static Raster Render(Frame f)
        {
            var cell = Math.Clamp(240 / Math.Max(f.Width, f.Height), 6, 20);
            var pictureW = f.Width * cell;
            var pictureH = f.Height * cell;
            var loomW = f.Slots.Count * (SlotSize + 6);
            var fieldW = f.Columns.Count * ColumnW;
            var depth = Math.Min(MaxDepth, f.Columns.Select(c => c.Count).DefaultIfEmpty(0).Max());
            var status = f.Status switch
            {
                GameStatus.Won => "WON",
                GameStatus.Jammed => "JAMMED",
                GameStatus.TimeUp => "TIME UP",
                _ => "PLAYING",
            };
            var head = $"{status} TAPS {f.Taps} {f.Knitted}/{f.Total}" + (f.TimeLimit > 0 ? $" T{f.TimeLeft:0.0}" : string.Empty);
            var width = Math.Max(Math.Max(300, PixelFont.Measure(head, 2) + Pad * 2), Math.Max(pictureW, Math.Max(loomW, fieldW)) + Pad * 2);
            var height = Pad + 18 + pictureH + 16 + SlotSize + 18 + depth * (SpoolR * 2 + 6) + 30 + Pad;
            var c = new Canvas(width, height);
            c.Fill(0, 0, width, height, Back);

            // Шапка: статус, тапы, петли, время.
            c.Text(head, Pad, Pad, f.Status == GameStatus.Playing ? Ink : f.Status == GameStatus.Won ? Rgba.Hex("#1FA67A") : Mark);

            // Картинка.
            var px = (width - pictureW) / 2;
            var py = Pad + 18;
            for (var y = 0; y < f.Height; y++)
            {
                for (var x = 0; x < f.Width; x++)
                {
                    var i = y * f.Width + x;
                    var color = Color(f, f.Cells[i]);
                    var fill = f.Fabric[i] ? Rgba.Lerp(color, Rgba.White, 0.55f)
                        : f.Done[i] ? color
                        : Rgba.Lerp(color, Rgba.White, 0.78f);
                    c.Fill(px + x * cell, py + y * cell, cell - 1, cell - 1, fill);
                    if (f.Fabric[i] && cell >= 8)
                    {
                        c.Fill(px + x * cell + cell / 2 - 1, py + y * cell + cell / 2 - 1, 2, 2, Rgba.Lerp(color, Ink, 0.3f));
                    }

                    if (f.Gold[i] && !f.Done[i])
                    {
                        c.Ring(px + x * cell + cell / 2f, py + y * cell + cell / 2f, cell * 0.32f, 1.5f, Gold);
                    }
                }

                // Фронт каждого столбца: действующий — тёмная рамка, удержанный натяжением — серая.
            }

            for (var x = 0; x < f.Width; x++)
            {
                if (f.Front[x] >= 0)
                {
                    c.Frame(px + x * cell - 1, py + f.Front[x] * cell - 1, cell + 1, cell + 1, f.Active[x] >= 0 ? Ink : Faint, 2);
                }
            }

            if (f.MarkCell >= 0)
            {
                c.Frame(px + f.MarkCell % f.Width * cell - 3, py + f.MarkCell / f.Width * cell - 3, cell + 5, cell + 5, Mark, 3);
            }

            // Станок.
            var ly = py + pictureH + 16;
            var lx = (width - loomW) / 2;
            for (var s = 0; s < f.Slots.Count; s++)
            {
                var sx = lx + s * (SlotSize + 6);
                c.Fill(sx, ly, SlotSize, SlotSize, Rgba.Hex("#EDE6FF"));
                c.Frame(sx, ly, SlotSize, SlotSize, Faint, 2);
                var spool = f.Slots[s];
                if (spool != null)
                {
                    if (f.Tails.Contains(s))
                    {
                        c.Fill(sx + 4, ly + SlotSize / 2 - 3, SlotSize - 8, 6, Color(f, spool.Color));
                    }
                    else
                    {
                        Spool(c, f, spool, sx + SlotSize / 2f, ly + SlotSize / 2f);
                        if (spool.Warmup > 0)
                        {
                            c.Text("~" + spool.Warmup, sx + 1, ly - 1, Ink, 1);
                        }

                        if (!spool.Work)
                        {
                            c.Text("X", sx + SlotSize - 7, ly + SlotSize - 8, Mark, 1);
                        }
                    }
                }

                if (s == f.MarkSlot)
                {
                    c.Frame(sx - 3, ly - 3, SlotSize + 6, SlotSize + 6, Mark, 3);
                }
            }

            // Поле: передний клубок сверху, ближе к станку.
            var fy = ly + SlotSize + 18;
            var fx = (width - fieldW) / 2;
            for (var col = 0; col < f.Columns.Count; col++)
            {
                var column = f.Columns[col];
                var cx = fx + col * ColumnW + ColumnW / 2f;
                for (var d = 0; d < Math.Min(MaxDepth, column.Count); d++)
                {
                    Spool(c, f, column[d], cx, fy + SpoolR + d * (SpoolR * 2 + 6));
                }

                if (column.Count > MaxDepth)
                {
                    c.Text("+" + (column.Count - MaxDepth), (int)cx - 8, fy + MaxDepth * (SpoolR * 2 + 6), Ink, 1);
                }

                if (column.Count == 0)
                {
                    c.Text("-", (int)cx - 3, fy + 8, Faint, 2);
                }

                if (col == f.MarkColumn)
                {
                    c.Frame(fx + col * ColumnW + 1, fy - 4, ColumnW - 2, Math.Max(1, Math.Min(MaxDepth, column.Count)) * (SpoolR * 2 + 6) + 6, Mark, 3);
                }
            }

            c.Text(f.Label.Length > 48 ? f.Label.Substring(0, 48) : f.Label, Pad, height - Pad - 14, Ink);
            return c.Raster;
        }

        /// <summary>Лента кадров: последние кадры перед находкой, слева направо, с подписью действия под каждым.</summary>
        public static Raster Film(IReadOnlyList<Frame> frames, int last = 8)
        {
            var shown = frames.Skip(Math.Max(0, frames.Count - last)).Select(Render).ToList();
            const int perRow = 4;
            var rows = (shown.Count + perRow - 1) / perRow;
            var cellW = shown.Max(r => r.Width) + 8;
            var cellH = shown.Max(r => r.Height) + 8;
            var film = new Raster(cellW * Math.Min(perRow, shown.Count), cellH * rows);
            KnittedPicture.ArtPreview.Png.Fill(film, Rgba.Hex("#CFC6EA"));
            for (var i = 0; i < shown.Count; i++)
            {
                var x = i % perRow * cellW + 4;
                var top = i / perRow * cellH + 4;
                var r = shown[i];
                for (var yy = 0; yy < r.Height; yy++)
                {
                    for (var xx = 0; xx < r.Width; xx++)
                    {
                        film[x + xx, film.Height - 1 - (top + (r.Height - 1 - yy))] = r[xx, yy];
                    }
                }
            }

            return film;
        }

        private static Rgba Color(Frame f, int index) =>
            index >= 0 && index < f.Colors.Length ? Rgba.Hex(f.Colors[index]) : Hidden;

        private static void Spool(Canvas c, Frame f, SpoolView s, float cx, float cy)
        {
            var color = !s.Visible ? Hidden : Color(f, s.Color);
            if (s.Rainbow && s.Visible)
            {
                c.Rainbow(cx, cy, SpoolR);
            }
            else
            {
                c.Disc(cx, cy, SpoolR, color);
            }

            if (s.Thick)
            {
                c.Ring(cx, cy, SpoolR + 3, 2f, Ink);
            }

            if (s.Link != 0)
            {
                c.Ring(cx, cy, SpoolR + 1, 2.5f, Rgba.Hex(s.Link % 2 == 0 ? "#8B5CF6" : "#00A3FF"));
            }

            if (s.Ice > 0)
            {
                c.Disc(cx, cy, SpoolR, Ice.WithAlpha(0.6f));
            }

            var light = color.R * 0.3f + color.G * 0.59f + color.B * 0.11f > 0.6f;
            var text = !s.Visible ? "?" : s.Loops.ToString();
            c.Text(text, (int)(cx - PixelFont.Measure(text, 1) / 2f) + 1, (int)cy - 3, light || s.Ice > 0 ? Ink : Rgba.White, 1);
            for (var k = 0; k < s.Knots; k++)
            {
                c.Disc(cx - 6 + k * 6, cy - SpoolR + 2, 2.2f, Ink);
            }

            if (s.Locked)
            {
                c.Fill((int)cx + 5, (int)cy + 5, 9, 8, Rgba.Hex("#7A6F9B"));
                c.Text("L", (int)cx + 7, (int)cy + 6, Rgba.White, 1);
            }

            if (s.Key)
            {
                c.Disc(cx + SpoolR - 3, cy - SpoolR + 3, 3.5f, Gold);
            }

            if (s.Ice > 0)
            {
                c.Text("I" + s.Ice, (int)cx - 7, (int)cy + 5, Ink, 1);
            }
        }

        /// <summary>Рисование в координатах «от левого верхнего угла» поверх растра (у него y растёт вверх).</summary>
        private sealed class Canvas
        {
            public Canvas(int w, int h)
            {
                Raster = new Raster(w, h);
            }

            public Raster Raster { get; }

            private int Y(int top) => Raster.Height - 1 - top;

            public void Fill(int x, int top, int w, int h, Rgba color)
            {
                for (var yy = top; yy < top + h; yy++)
                {
                    for (var xx = x; xx < x + w; xx++)
                    {
                        Raster.Blend(xx, Y(yy), color);
                    }
                }
            }

            public void Frame(int x, int top, int w, int h, Rgba color, int t)
            {
                Fill(x, top, w, t, color);
                Fill(x, top + h - t, w, t, color);
                Fill(x, top, t, h, color);
                Fill(x + w - t, top, t, h, color);
            }

            public void Disc(float cx, float cy, float r, Rgba color)
            {
                for (var yy = (int)(cy - r - 1); yy <= (int)(cy + r + 1); yy++)
                {
                    for (var xx = (int)(cx - r - 1); xx <= (int)(cx + r + 1); xx++)
                    {
                        var d = MathF.Sqrt((xx + 0.5f - cx) * (xx + 0.5f - cx) + (yy + 0.5f - cy) * (yy + 0.5f - cy)) - r;
                        var cover = Draw.Cover(d);
                        if (cover > 0f)
                        {
                            Raster.Blend(xx, Y(yy), color.WithAlpha(color.A * cover));
                        }
                    }
                }
            }

            public void Ring(float cx, float cy, float r, float t, Rgba color)
            {
                for (var yy = (int)(cy - r - t - 1); yy <= (int)(cy + r + t + 1); yy++)
                {
                    for (var xx = (int)(cx - r - t - 1); xx <= (int)(cx + r + t + 1); xx++)
                    {
                        var d = MathF.Abs(MathF.Sqrt((xx + 0.5f - cx) * (xx + 0.5f - cx) + (yy + 0.5f - cy) * (yy + 0.5f - cy)) - r) - t / 2f;
                        var cover = Draw.Cover(d);
                        if (cover > 0f)
                        {
                            Raster.Blend(xx, Y(yy), color.WithAlpha(color.A * cover));
                        }
                    }
                }
            }

            public void Rainbow(float cx, float cy, float r)
            {
                string[] hues = { "#E8475F", "#FF8A3D", "#FFD23F", "#1FA67A", "#3D8FD1", "#8E52D9" };
                for (var yy = (int)(cy - r - 1); yy <= (int)(cy + r + 1); yy++)
                {
                    for (var xx = (int)(cx - r - 1); xx <= (int)(cx + r + 1); xx++)
                    {
                        var dx = xx + 0.5f - cx;
                        var dy = yy + 0.5f - cy;
                        var cover = Draw.Cover(MathF.Sqrt(dx * dx + dy * dy) - r);
                        if (cover > 0f)
                        {
                            var a = (MathF.Atan2(dy, dx) / (MathF.PI * 2f) + 1f) % 1f;
                            Raster.Blend(xx, Y(yy), Rgba.Hex(hues[(int)(a * hues.Length) % hues.Length]).WithAlpha(cover));
                        }
                    }
                }
            }

            public void Text(string text, int x, int top, Rgba color, int scale = 2) =>
                PixelFont.Draw(Raster, text, x, Y(top), color, scale);
        }
    }
}
