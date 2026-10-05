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
            var colorCount = keys.Count;

            var sequence = new List<int>(width * height);
            for (var y = height - 1; y >= 0; y--)
            {
                for (var x = 0; x < width; x++)
                {
                    sequence.Add(picture[y][x] - '0');
                }
            }

            var counts = new int[colorCount];
            foreach (var c in sequence)
            {
                counts[c]++;
            }

            var perColor = SpoolsPerColor(counts, design.spools);
            var queues = new List<Queue<Draft>>();
            for (var c = 0; c < colorCount; c++)
            {
                var lengths = Split(counts[c], perColor[c], rng);
                queues.Add(new Queue<Draft>(lengths.Select(l => new Draft { Color = c, Loops = l })));
            }

            // Порядок, в котором станок потребует клубки.
            var order = new List<Draft>();
            var remaining = new int[colorCount];
            foreach (var c in sequence)
            {
                if (remaining[c] == 0)
                {
                    var next = queues[c].Dequeue();
                    next.Order = order.Count;
                    order.Add(next);
                    remaining[c] = next.Loops;
                }

                remaining[c]--;
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

        private static int[] SpoolsPerColor(int[] counts, int target)
        {
            var total = counts.Sum();
            var result = counts.Select(n => n == 0 ? 0 : Math.Max(1, Math.Min(n, (int)Math.Round((double)target * n / total)))).ToArray();
            while (result.Sum() > target && result.Any(k => k > 1))
            {
                var i = Array.IndexOf(result, result.Max());
                result[i]--;
            }

            while (result.Sum() < target)
            {
                var best = -1;
                var bestLoad = 0.0;
                for (var c = 0; c < counts.Length; c++)
                {
                    if (result[c] == 0 || result[c] >= counts[c])
                    {
                        continue;
                    }

                    var load = (double)counts[c] / result[c];
                    if (load > bestLoad)
                    {
                        best = c;
                        bestLoad = load;
                    }
                }

                if (best < 0)
                {
                    break;
                }

                result[best]++;
            }

            return result;
        }

        private static List<int> Split(int total, int parts, Random rng)
        {
            if (parts <= 0)
            {
                return new List<int>();
            }

            var weights = Enumerable.Range(0, parts).Select(_ => 0.65 + rng.NextDouble() * 0.7).ToList();
            var sum = weights.Sum();
            var result = weights.Select(w => Math.Max(1, (int)Math.Round(total * w / sum))).ToList();
            var diff = total - result.Sum();
            var i = 0;
            while (diff != 0)
            {
                var index = i % parts;
                if (diff > 0)
                {
                    result[index]++;
                    diff--;
                }
                else if (result[index] > 1)
                {
                    result[index]--;
                    diff++;
                }

                i++;
            }

            return result.OrderBy(_ => rng.Next()).ToList();
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
