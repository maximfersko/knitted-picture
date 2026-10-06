#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace KnittedPicture.Core
{
    /// <summary>
    /// «Узор дня»: каждый день новый уровень, одинаковый у всех игроков. Картинка берётся из альбома
    /// по дате, а клубки нарезаются заново прямо в игре: по правилам вязки, в том порядке, в котором
    /// они понадобятся станку, — так уровень заведомо проходим. Пара перестановок добавляет задачу,
    /// решатель её перепроверяет; если решения не нашлось — берётся раскладка без перестановок.
    /// </summary>
    public static class DailyPattern
    {
        /// <summary>Номер уровня «узора»: вне кампании, звёзды и рекорды кампании не трогает.</summary>
        public const int LevelId = 1000;

        /// <summary>Открывается после 5-го уровня: игрок уже знает основы.</summary>
        public const int FromLevel = 6;

        /// <summary>Каждый 7-й день серии подряд — моток в подарок.</summary>
        public const int YarnEvery = 7;

        public const int RewardCoins = 50;

        /// <summary>Какая картинка альбома сегодня (индекс 0..count-1): по дню, без повторов подряд.</summary>
        public static int PictureIndex(int day, int count) => ((day * 7 + 3) % count + count) % count;

        public static LevelData Build(LevelData source, int day)
        {
            for (var attempt = 0; attempt < 6; attempt++)
            {
                var level = Make(source, day, attempt, swaps: 2);
                if (LevelValidator.Validate(level).Count == 0 && LevelSolver.Solve(level, 20_000) != null)
                {
                    return level;
                }
            }

            // Без перестановок раскладка проходима по построению (клубки лежат в порядке нужды).
            return Make(source, day, 0, swaps: 0);
        }

        private static LevelData Make(LevelData source, int day, int attempt, int swaps)
        {
            var rng = new Random(day * 7919 + attempt * 104_729 + 101);
            var grid = PictureGrid.From(source);
            var spoolCount = Math.Max(8, Math.Min(22, grid.Total / 8));
            var columns = Math.Max(3, Math.Min(5, source.spools.Max(s => s.column) + 1));

            // Нарезка по правилам вязки: цвет с активного фронта, клубок вяжет, пока не наберёт длину.
            var order = new List<(int color, int loops)>();
            while (!grid.IsComplete)
            {
                var front = new List<int>();
                for (var x = 0; x < grid.Width; x++)
                {
                    var y = grid.ActiveFrontY(x);
                    if (y >= 0 && !front.Contains(grid.ColorAt(x, y)))
                    {
                        front.Add(grid.ColorAt(x, y));
                    }
                }

                var color = front[rng.Next(front.Count)];
                var spoolsLeft = Math.Max(1, spoolCount - order.Count);
                var average = (double)(grid.Total - grid.Knitted) / spoolsLeft;
                var target = Math.Max(1, (int)Math.Round(average * (0.7 + rng.NextDouble() * 0.6)));
                var loops = 0;
                while (loops < target)
                {
                    var x = grid.FindTarget(color);
                    if (x < 0)
                    {
                        break;
                    }

                    grid.Knit(x);
                    loops++;
                }

                order.Add((color, loops));
            }

            // По колонкам с сохранением порядка: первые клубки — по одному в каждую колонку.
            var cols = Enumerable.Range(0, columns).Select(_ => new List<int>()).ToList();
            var first = Enumerable.Range(0, columns).OrderBy(_ => rng.Next()).ToList();
            for (var i = 0; i < order.Count; i++)
            {
                if (i < columns)
                {
                    cols[first[i]].Add(i);
                    continue;
                }

                var best = 0;
                var bestWeight = -1.0;
                for (var c = 0; c < columns; c++)
                {
                    var weight = rng.NextDouble() / Math.Pow(1 + cols[c].Count, 2);
                    if (weight > bestWeight)
                    {
                        best = c;
                        bestWeight = weight;
                    }
                }

                cols[best].Add(i);
            }

            for (var i = 0; i < swaps; i++)
            {
                var candidates = cols.Where(col => col.Count >= 2).ToList();
                if (candidates.Count == 0)
                {
                    break;
                }

                var col = candidates[rng.Next(candidates.Count)];
                var at = rng.Next(col.Count - 1);
                (col[at], col[at + 1]) = (col[at + 1], col[at]);
            }

            // Немного особых клубков: пара скрытых в глубине и один узелок — узор не скучный, но и не тяжёлый.
            var hidden = new HashSet<int>();
            var deep = cols.SelectMany(col => col.Skip(1)).ToList();
            for (var i = 0; i < 2 && deep.Count > 0; i++)
            {
                var pick = deep[rng.Next(deep.Count)];
                hidden.Add(pick);
                deep.Remove(pick);
            }

            var knotted = rng.Next(order.Count);
            var spools = new List<SpoolData>();
            for (var c = 0; c < columns; c++)
            {
                foreach (var i in cols[c])
                {
                    spools.Add(new SpoolData
                    {
                        column = c,
                        color = order[i].color,
                        loops = order[i].loops,
                        hidden = hidden.Contains(i),
                        knots = i == knotted ? 1 : 0,
                    });
                }
            }

            // Две золотые петли среди клеток рисунка.
            var cells = new List<int>();
            for (var y = 0; y < source.height; y++)
            {
                for (var x = 0; x < source.width; x++)
                {
                    var fabric = source.canvas != null && source.canvas.Length == source.height && source.canvas[y][x] == '1';
                    if (!fabric)
                    {
                        cells.Add(y * source.width + x);
                    }
                }
            }

            var gold = cells.OrderBy(_ => rng.Next()).Take(2).OrderBy(c => c).ToArray();

            return new LevelData
            {
                id = LevelId,
                name = source.name,
                tip = string.Empty,
                width = source.width,
                height = source.height,
                colors = source.colors,
                picture = source.picture,
                canvas = source.canvas ?? Array.Empty<string>(),
                band = source.band,
                gold = gold,
                slots = Math.Max(3, Math.Min(5, source.slots)),
                spools = spools.ToArray(),
            };
        }
    }
}
