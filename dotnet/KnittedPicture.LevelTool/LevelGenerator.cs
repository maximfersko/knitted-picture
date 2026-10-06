using System;
using System.Collections.Generic;
using System.Linq;
using KnittedPicture.Core;

namespace KnittedPicture.LevelTool
{
    /// <summary>
    /// Собирает поле под картинку. Клубки раскладываются по колонкам в порядке,
    /// в котором они понадобятся станку, — так уровень заведомо решаем; дальше
    /// перестановки и особые клубки добавляют сложности, а решатель это перепроверяет.
    /// </summary>
    public static class LevelGenerator
    {
        private sealed class Draft
        {
            public int Color;
            public int Loops;
            public int Order;
            public bool Hidden;
            public int Link;
            public int Knots;
            public bool Locked;
            public bool Key;
            public bool Rainbow;
            public bool Thick;
            public int Ice;
            public bool Extra;
        }

        public static LevelData Generate(LevelDesign design, int seed)
        {
            var rng = new Random(seed);
            var keys = design.palette.Select(p => p.key[0]).ToList();
            var picture = design.picture
                .Select(row => new string(row.Select(ch => Digit(keys, ch, design.id)).ToArray()))
                .ToArray();

            var width = picture[0].Length;
            var height = picture.Length;
            var fabricColors = design.fabric.Select(ch => Digit(keys, ch, design.id) - '0').ToHashSet();
            var canvas = fabricColors.Count == 0
                ? Array.Empty<string>()
                : picture.Select(row => new string(row.Select(ch => fabricColors.Contains(ch - '0') ? '1' : '0').ToArray())).ToArray();

            // Нарезаем клубки, вяжя картинку по правилам игры: берём цвет с фронта
            // и вяжем им, пока клубок не наберёт свою длину или цвет не кончится на фронте.
            // Полученный порядок клубков проходим даже с одним слотом.
            var grid = new PictureGrid(width, height, picture, canvas, design.band);
            var rainbowAt = PickIndices(design.rainbow, 2, design.spools - 1, rng);
            var thickAt = PickIndices(design.thick, 1, design.spools - 1, rng, rainbowAt);
            var order = new List<Draft>();
            while (!grid.IsComplete)
            {
                var index = order.Count;
                var rainbow = rainbowAt.Contains(index);
                var thick = !rainbow && thickAt.Contains(index);
                var front = new List<int>();
                for (var x = 0; x < width; x++)
                {
                    var y = grid.ActiveFrontY(x);
                    if (y >= 0 && !front.Contains(grid.ColorAt(x, y)))
                    {
                        front.Add(grid.ColorAt(x, y));
                    }
                }

                // Толстому — самый «длинный» цвет: ему нужно много петель подряд.
                var color = thick ? front.OrderByDescending(grid.Left).First() : front[rng.Next(front.Count)];
                var spoolsLeft = Math.Max(1, design.spools - order.Count);
                var average = (double)(grid.Total - grid.Knitted) / spoolsLeft;
                var scale = rainbow ? 0.45 : thick ? 2.4 : 1.0;
                var target = Math.Max(rainbow ? 2 : 1, (int)Math.Round(average * scale * (0.7 + rng.NextDouble() * 0.6)));
                var loops = 0;
                while (loops < target)
                {
                    var x = grid.FindTarget(rainbow ? PictureGrid.AnyColor : color);
                    if (x < 0)
                    {
                        break;
                    }

                    grid.Knit(x);
                    loops++;
                }

                order.Add(new Draft { Color = rainbow ? 0 : color, Loops = loops, Order = index, Rainbow = rainbow, Thick = thick && loops >= 8 });
            }

            var motifColors = Enumerable.Range(0, keys.Count).Where(c => grid.TotalOf(c) > 0).ToList();
            var averageLoops = order.Average(d => d.Loops);
            var extras = new List<Draft>();
            for (var i = 0; i < design.extra; i++)
            {
                extras.Add(new Draft
                {
                    Color = motifColors[rng.Next(motifColors.Count)],
                    Loops = Math.Max(2, (int)Math.Round(averageLoops * (0.4 + rng.NextDouble() * 0.6))),
                    Order = order.Count + i,
                    Extra = true,
                });
            }

            // Чужая пряжа — цвета ткани, в рисунке её нет.
            var fabricList = fabricColors.OrderBy(c => c).ToList();
            for (var i = 0; i < design.junk && fabricList.Count > 0; i++)
            {
                extras.Add(new Draft
                {
                    Color = fabricList[i % fabricList.Count],
                    Loops = 2 + rng.Next(5),
                    Order = order.Count + extras.Count,
                    Extra = true,
                });
            }

            var columns = Enumerable.Range(0, design.columns).Select(_ => new List<Draft>()).ToList();
            var firstColumns = Enumerable.Range(0, design.columns).OrderBy(_ => rng.Next()).ToList();
            for (var i = 0; i < order.Count; i++)
            {
                if (i < design.columns)
                {
                    columns[firstColumns[i]].Add(order[i]);
                    continue;
                }

                var weights = columns.Select(col => 1.0 / Math.Pow(1 + col.Count, 2)).ToList();
                columns[Pick(weights, rng)].Add(order[i]);
            }

            // Лишние и чужие клубки — неглубоко: их приходится обходить или брать в удачный момент.
            foreach (var extra in extras)
            {
                var col = columns[rng.Next(columns.Count)];
                col.Insert(Math.Min(col.Count, rng.Next(3)), extra);
            }

            for (var i = 0; i < design.swaps; i++)
            {
                var candidates = columns.Where(col => col.Count >= 2).ToList();
                if (candidates.Count == 0)
                {
                    break;
                }

                var col = candidates[rng.Next(candidates.Count)];
                var at = rng.Next(col.Count - 1);
                (col[at], col[at + 1]) = (col[at + 1], col[at]);
            }

            AddLocks(design.locks, columns, order, rng);
            AddLinks(design.links, columns, order, rng);
            AddKnots(design.knots, order, rng);
            AddIce(design.ice, order, grid, rng);
            AddHidden(design.hidden, columns, rng);

            var gold = PickGold(design.gold, grid, rng);

            return new LevelData
            {
                id = design.id,
                name = design.name,
                tip = design.tip,
                hard = design.hard,
                timeLimit = design.timeLimit,
                width = width,
                height = height,
                colors = design.palette.Select(p => p.hex).ToArray(),
                picture = picture,
                canvas = canvas,
                band = design.playBand > 0 ? design.playBand : design.band,
                gold = gold,
                mystery = design.mystery,
                slots = design.slots,
                spools = columns
                    .SelectMany((col, index) => col.Select(d => new SpoolData
                    {
                        column = index,
                        color = d.Color,
                        loops = d.Loops,
                        hidden = d.Hidden,
                        link = d.Link,
                        knots = d.Knots,
                        locked = d.Locked,
                        key = d.Key,
                        rainbow = d.Rainbow,
                        thick = d.Thick,
                        ice = d.Ice,
                    }))
                    .ToArray(),
            };
        }

        private static HashSet<int> PickIndices(int count, int from, int to, Random rng, HashSet<int>? exclude = null)
        {
            return Enumerable.Range(from, Math.Max(0, to - from))
                .Where(i => exclude == null || !exclude.Contains(i))
                .OrderBy(_ => rng.Next())
                .Take(count)
                .ToHashSet();
        }

        /// <summary>Золотые петли: по одной в разных рядах рисунка, чтобы их было видно по всей картинке.</summary>
        private static int[] PickGold(int count, PictureGrid grid, Random rng)
        {
            var cells = new List<int>();
            for (var y = 0; y < grid.Height; y++)
            {
                for (var x = 0; x < grid.Width; x++)
                {
                    if (!grid.IsFabric(x, y))
                    {
                        cells.Add(y * grid.Width + x);
                    }
                }
            }

            var picked = new List<int>();
            foreach (var cell in cells.OrderBy(_ => rng.Next()))
            {
                if (picked.Count >= count)
                {
                    break;
                }

                if (picked.All(p => Math.Abs(p / grid.Width - cell / grid.Width) >= 2))
                {
                    picked.Add(cell);
                }
            }

            picked.Sort();
            return picked.ToArray();
        }

        private static char Digit(List<char> keys, char ch, int levelId)
        {
            var index = keys.IndexOf(ch);
            if (index < 0)
            {
                throw new InvalidOperationException($"уровень {levelId}: буквы '{ch}' нет в палитре");
            }

            return (char)('0' + index);
        }

        private static int Pick(List<double> weights, Random rng)
        {
            var roll = rng.NextDouble() * weights.Sum();
            for (var i = 0; i < weights.Count; i++)
            {
                roll -= weights[i];
                if (roll <= 0)
                {
                    return i;
                }
            }

            return weights.Count - 1;
        }

        private static bool IsPlain(Draft d) => d.Link == 0 && d.Knots == 0 && !d.Locked && !d.Key && !d.Rainbow && !d.Thick && d.Ice == 0;

        private static void AddLocks(int count, List<List<Draft>> columns, List<Draft> order, Random rng)
        {
            if (count <= 0)
            {
                return;
            }

            // Ключ — из первой трети очереди и неглубоко, замки — на клубках, нужных позже.
            var keyCandidates = order.Take(Math.Max(2, order.Count / 3))
                .Where(d => DepthOf(columns, d) <= 1)
                .ToList();
            if (keyCandidates.Count == 0)
            {
                return;
            }

            var key = keyCandidates[rng.Next(keyCandidates.Count)];
            key.Key = true;
            var keyColumn = ColumnOf(columns, key);
            var keyDepth = DepthOf(columns, key);

            var lockCandidates = order
                .Where(d => d.Order > key.Order + 1 && IsPlain(d))
                .Where(d => ColumnOf(columns, d) != keyColumn || DepthOf(columns, d) > keyDepth)
                .OrderBy(_ => rng.Next())
                .Take(count);
            foreach (var d in lockCandidates)
            {
                d.Locked = true;
            }
        }

        private static void AddLinks(int count, List<List<Draft>> columns, List<Draft> order, Random rng)
        {
            var linkId = 1;
            var starts = Enumerable.Range(0, order.Count - 1).OrderBy(_ => rng.Next()).ToList();
            foreach (var i in starts)
            {
                if (linkId > count)
                {
                    break;
                }

                var a = order[i];
                var b = order[i + 1];
                if (!IsPlain(a) || !IsPlain(b) || ColumnOf(columns, a) == ColumnOf(columns, b))
                {
                    continue;
                }

                a.Link = linkId;
                b.Link = linkId;
                linkId++;
            }
        }

        private static void AddKnots(int count, List<Draft> order, Random rng)
        {
            foreach (var d in order.Where(IsPlain).OrderBy(_ => rng.Next()).Take(count))
            {
                d.Knots = rng.NextDouble() < 0.25 ? 2 : 1;
            }
        }

        /// <summary>
        /// Лёд — на клубках, которым по задуманному порядку предшествует достаточно петель
        /// того же цвета: к моменту, когда клубок нужен, он уже оттает.
        /// </summary>
        private static void AddIce(int count, List<Draft> order, PictureGrid grid, Random rng)
        {
            var candidates = order
                .Where(d => IsPlain(d) && !d.Hidden)
                .Select(d => (draft: d, before: order.Where(o => o.Order < d.Order && !o.Rainbow && o.Color == d.Color).Sum(o => o.Loops)))
                .Where(c => c.before >= 3)
                .OrderBy(_ => rng.Next())
                .Take(count);
            foreach (var (draft, before) in candidates)
            {
                draft.Ice = Math.Min(Math.Min(12, grid.TotalOf(draft.Color)), Math.Max(2, (int)Math.Round(before * (0.4 + rng.NextDouble() * 0.4))));
            }
        }

        private static void AddHidden(int count, List<List<Draft>> columns, Random rng)
        {
            var candidates = columns.SelectMany(col => col.Skip(1)).Where(d => d.Ice == 0).OrderBy(_ => rng.Next()).Take(count);
            foreach (var d in candidates)
            {
                d.Hidden = true;
            }
        }

        private static int ColumnOf(List<List<Draft>> columns, Draft d) => columns.FindIndex(col => col.Contains(d));

        private static int DepthOf(List<List<Draft>> columns, Draft d) => columns[ColumnOf(columns, d)].IndexOf(d);
    }
}
