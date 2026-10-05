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

            // Нарезаем клубки, вяжя картинку по правилам игры: берём цвет с фронта
            // и вяжем им, пока клубок не наберёт свою длину или цвет не кончится на фронте.
            // Полученный порядок клубков проходим даже с одним слотом.
            var heights = new int[width];
            var total = width * height;
            var knitted = 0;
            var order = new List<Draft>();
            while (knitted < total)
            {
                var front = new List<int>();
                for (var x = 0; x < width; x++)
                {
                    if (heights[x] < height)
                    {
                        var c = picture[height - 1 - heights[x]][x] - '0';
                        if (!front.Contains(c))
                        {
                            front.Add(c);
                        }
                    }
                }

                var color = front[rng.Next(front.Count)];
                var spoolsLeft = Math.Max(1, design.spools - order.Count);
                var average = (double)(total - knitted) / spoolsLeft;
                var target = Math.Max(1, (int)Math.Round(average * (0.7 + rng.NextDouble() * 0.6)));
                var loops = 0;
                while (loops < target)
                {
                    var best = -1;
                    for (var x = 0; x < width; x++)
                    {
                        if (heights[x] < height && picture[height - 1 - heights[x]][x] - '0' == color &&
                            (best < 0 || heights[x] < heights[best]))
                        {
                            best = x;
                        }
                    }

                    if (best < 0)
                    {
                        break;
                    }

                    heights[best]++;
                    loops++;
                    knitted++;
                }

                order.Add(new Draft { Color = color, Loops = loops, Order = order.Count });
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
            AddHidden(design.hidden, columns, rng);

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
                    }))
                    .ToArray(),
            };
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

        private static bool IsPlain(Draft d) => d.Link == 0 && d.Knots == 0 && !d.Locked && !d.Key;

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

        private static void AddHidden(int count, List<List<Draft>> columns, Random rng)
        {
            var candidates = columns.SelectMany(col => col.Skip(1)).OrderBy(_ => rng.Next()).Take(count);
            foreach (var d in candidates)
            {
                d.Hidden = true;
            }
        }

        private static int ColumnOf(List<List<Draft>> columns, Draft d) => columns.FindIndex(col => col.Contains(d));

        private static int DepthOf(List<List<Draft>> columns, Draft d) => columns[ColumnOf(columns, d)].IndexOf(d);
    }
}
