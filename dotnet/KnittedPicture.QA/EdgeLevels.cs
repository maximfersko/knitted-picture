using System;
using System.Collections.Generic;
using System.Linq;
using KnittedPicture.Core;

namespace KnittedPicture.QA
{
    /// <summary>
    /// Пограничные уровни: то, чего нет среди 50 настоящих, но что валидатор пропускает, а значит, игра
    /// обязана выдержать. Крайние размеры и числа, все особые клубки разом, мутации настоящих уровней.
    /// Каждый уровень проходит валидатор: если валидатор пропустил, а игра ломается, это ошибка.
    /// </summary>
    public static class EdgeLevels
    {
        public static List<LevelData> Build(IReadOnlyList<LevelData> shipped, int seed, int mutants, out int rejected)
        {
            var rng = new Random(seed * 977 + 13);
            var all = new List<LevelData>();
            all.AddRange(Handmade());
            for (var i = 0; i < mutants; i++)
            {
                all.Add(Mutate(shipped[rng.Next(shipped.Count)], rng));
                all.Add(Random(rng));
            }

            var valid = new List<LevelData>();
            rejected = 0;
            foreach (var level in all)
            {
                // Валидатор не должен падать ни на чём: если упадёт, исключение поймает Program и запишет находку.
                var errors = LevelValidator.Validate(level);
                if (errors.Count == 0)
                {
                    level.id = 5000 + valid.Count;
                    valid.Add(level);
                }
                else
                {
                    rejected++;
                }
            }

            return valid;
        }

        private static SpoolData S(int column, int color, int loops) => new SpoolData { column = column, color = color, loops = loops };

        private static LevelData L(string name, int w, int h, string[] colors, string[] picture, int slots, params SpoolData[] spools) => new LevelData
        {
            name = name,
            width = w,
            height = h,
            colors = colors,
            picture = picture,
            slots = slots,
            spools = spools,
        };

        private static readonly string[] Two = { "#E8475F", "#3D8FD1" };

        /// <summary>Руками собранные крайние случаи.</summary>
        public static IEnumerable<LevelData> Handmade()
        {
            // Минимум: 2×2 одного цвета, один слот, один клубок ровно на картинку.
            yield return L("мин: 2×2, 1 слот, 1 клубок", 2, 2, new[] { "#E8475F" }, new[] { "00", "00" }, 1, S(0, 0, 4));

            // Один слот и неверный порядок: без бустеров тупик, с бустерами — проходится.
            yield return L("1 слот, порядок наоборот", 2, 2, Two, new[] { "11", "00" }, 1, S(0, 1, 2), S(0, 0, 2));

            // Толстый клубок при одном слоте: поставить нельзя никогда.
            yield return L("толстый при 1 слоте", 2, 2, new[] { "#E8475F" }, new[] { "00", "00" }, 1,
                new SpoolData { column = 0, color = 0, loops = 4, thick = true }, S(1, 0, 4));

            // Максимум слотов и колонок, по клубку на петлю.
            var many = new List<SpoolData>();
            for (var i = 0; i < 24; i++)
            {
                many.Add(S(i % 6, i % 2, 1));
            }

            yield return L("7 слотов, 6 колонок, клубки по 1 петле", 4, 6, Two, new[] { "0101", "1010", "0101", "1010", "0101", "1010" }, 7, many.ToArray());

            // Огромная картинка 32×32 из одного цвета: производительность шага и довязки.
            var big = Enumerable.Repeat(new string('0', 32), 32).ToArray();
            yield return L("32×32 одним цветом", 32, 32, new[] { "#5AA9F0" }, big, 7,
                Enumerable.Range(0, 6).Select(c => S(c, 0, 1024 / 6 + 1)).ToArray());

            // Только радуга.
            yield return L("только радужные", 3, 3, new[] { "#E8475F", "#3D8FD1", "#FFD23F" }, new[] { "012", "120", "201" }, 3,
                new SpoolData { column = 0, color = 0, loops = 5, rainbow = true }, new SpoolData { column = 1, color = 1, loops = 5, rainbow = true });

            // Всё скрыто.
            yield return L("всё скрыто", 2, 3, Two, new[] { "01", "10", "01" }, 3,
                new SpoolData { column = 0, color = 0, loops = 3, hidden = true }, new SpoolData { column = 0, color = 1, loops = 3, hidden = true },
                new SpoolData { column = 1, color = 1, loops = 1, hidden = true });

            // Замки на всём, кроме ключа в самом конце соседней колонки.
            yield return L("замки и поздний ключ", 2, 2, Two, new[] { "01", "01" }, 2,
                new SpoolData { column = 0, color = 0, loops = 2, locked = true }, new SpoolData { column = 1, color = 1, loops = 1 },
                new SpoolData { column = 1, color = 1, loops = 1, key = true });

            // Лёд ровно на все петли своего цвета.
            yield return L("лёд на все петли цвета", 2, 2, Two, new[] { "00", "11" }, 2,
                S(0, 0, 2), new SpoolData { column = 1, color = 1, loops = 2, ice = 2 }, S(0, 1, 2));

            // Три узелка на каждом.
            yield return L("узелки максимум", 2, 2, Two, new[] { "01", "01" }, 2,
                new SpoolData { column = 0, color = 0, loops = 2, knots = 3 }, new SpoolData { column = 1, color = 1, loops = 2, knots = 3 });

            // Связка, где пара в разных колонках на разной глубине.
            yield return L("связка на разной глубине", 2, 2, Two, new[] { "01", "01" }, 3,
                new SpoolData { column = 0, color = 0, loops = 2, link = 1 }, S(1, 1, 1),
                new SpoolData { column = 1, color = 1, loops = 1, link = 1 });

            // Канва всюду, кроме одной клетки.
            yield return new LevelData
            {
                name = "канва всюду, кроме одной клетки", width = 3, height = 3, colors = Two,
                picture = new[] { "000", "010", "000" }, canvas = new[] { "111", "101", "111" }, slots = 2,
                spools = new[] { S(0, 1, 1), S(1, 0, 3) },
            };

            // Натяжение 1 на высокой узкой картинке.
            yield return new LevelData
            {
                name = "натяжение 1, высокая", width = 2, height = 8, colors = Two, band = 1, slots = 2,
                picture = new[] { "01", "10", "01", "10", "01", "10", "01", "10" },
                spools = new[] { S(0, 0, 8), S(1, 1, 8) },
            };

            // Моль за секунду.
            yield return new LevelData
            {
                name = "моль за 1 секунду", width = 2, height = 2, colors = Two, timeLimit = 1, slots = 2,
                picture = new[] { "01", "01" }, spools = new[] { S(0, 0, 2), S(1, 1, 2) },
            };

            // Чужая пряжа (цвет только ткани) и лишние клубки.
            yield return new LevelData
            {
                name = "чужая и лишняя пряжа", width = 2, height = 2, colors = new[] { "#E8475F", "#F4EEE4" }, slots = 2,
                picture = new[] { "00", "11" }, canvas = new[] { "00", "11" },
                spools = new[] { S(0, 1, 3), S(0, 0, 4), S(1, 0, 2), S(1, 1, 1) },
            };

            // Загадка с золотом на каждой клетке.
            yield return new LevelData
            {
                name = "загадка, всё золото", width = 2, height = 2, colors = Two, mystery = true, slots = 2,
                picture = new[] { "01", "10" }, gold = new[] { 0, 1, 2, 3 }, spools = new[] { S(0, 0, 2), S(1, 1, 2) },
            };
        }

        /// <summary>Мутация настоящего уровня: слоты, порядок, особые свойства, лишняя пряжа.</summary>
        public static LevelData Mutate(LevelData source, Random rng)
        {
            var l = Copy(source);
            l.name = source.name + " (мутант)";
            var spools = l.spools.ToList();
            for (var k = rng.Next(1, 5); k > 0; k--)
            {
                switch (rng.Next(10))
                {
                    case 0:
                        l.slots = rng.Next(1, LoomGame.MaxSlots + 1);
                        break;
                    case 1:
                        // Перемешать порядок в колонке.
                        spools = spools.OrderBy(s => s.column).ThenBy(_ => rng.Next()).ToList();
                        break;
                    case 2:
                        spools[rng.Next(spools.Count)].hidden ^= true;
                        break;
                    case 3:
                        var knot = spools[rng.Next(spools.Count)];
                        knot.knots = knot.link == 0 && !knot.locked && !knot.key && knot.ice == 0 ? rng.Next(0, 4) : knot.knots;
                        break;
                    case 4:
                        spools.Add(new SpoolData { column = rng.Next(0, spools.Max(s => s.column) + 1), color = rng.Next(l.colors.Length), loops = rng.Next(1, 9) });
                        break;
                    case 5:
                        var thick = spools[rng.Next(spools.Count)];
                        thick.thick = thick.link == 0 && !thick.thick;
                        break;
                    case 6:
                        l.band = rng.Next(0, 3);
                        break;
                    case 7:
                        l.timeLimit = rng.Next(3) switch { 0 => 0, 1 => 3, _ => 45 };
                        break;
                    case 8:
                        var r = spools[rng.Next(spools.Count)];
                        r.rainbow = r.ice == 0 && !r.rainbow;
                        break;
                    default:
                        l.mystery ^= true;
                        break;
                }
            }

            l.spools = spools.ToArray();
            return l;
        }

        /// <summary>Случайный уровень с нуля: маленький, плотный по особым клубкам.</summary>
        public static LevelData Random(Random rng)
        {
            var w = rng.Next(2, 7);
            var h = rng.Next(2, 7);
            var colors = rng.Next(1, 4);
            var palette = new[] { "#E8475F", "#3D8FD1", "#FFD23F", "#1FA67A" }.Take(colors).ToArray();
            var picture = Enumerable.Range(0, h).Select(_ => new string(Enumerable.Range(0, w).Select(__ => (char)('0' + rng.Next(colors))).ToArray())).ToArray();
            var need = new int[colors];
            foreach (var row in picture)
            {
                foreach (var ch in row)
                {
                    need[ch - '0']++;
                }
            }

            var columns = rng.Next(1, 7);
            var spools = new List<SpoolData>();
            for (var c = 0; c < colors; c++)
            {
                var left = need[c] + rng.Next(0, 3);
                while (left > 0)
                {
                    var loops = Math.Min(left, rng.Next(1, 7));
                    spools.Add(new SpoolData { column = rng.Next(columns), color = c, loops = loops });
                    left -= loops;
                }
            }

            for (var i = 0; i < columns; i++)
            {
                if (spools.All(s => s.column != i))
                {
                    spools.Add(new SpoolData { column = i, color = rng.Next(colors), loops = 1 });
                }
            }

            spools = spools.OrderBy(_ => rng.Next()).ToList();
            foreach (var s in spools)
            {
                var roll = rng.Next(12);
                if (roll == 0) s.hidden = true;
                if (roll == 1) s.knots = rng.Next(1, 4);
                if (roll == 2) s.thick = true;
                if (roll == 3) s.rainbow = true;
            }

            return new LevelData
            {
                name = "случайный",
                width = w,
                height = h,
                colors = palette,
                picture = picture,
                slots = rng.Next(1, LoomGame.MaxSlots + 1),
                band = rng.Next(4) == 0 ? 1 : 0,
                timeLimit = rng.Next(5) == 0 ? rng.Next(2, 30) : 0,
                spools = spools.ToArray(),
            };
        }

        public static LevelData Copy(LevelData s) => new LevelData
        {
            id = s.id,
            name = s.name,
            tip = s.tip,
            hard = s.hard,
            timeLimit = s.timeLimit,
            width = s.width,
            height = s.height,
            colors = s.colors.ToArray(),
            picture = s.picture.ToArray(),
            canvas = s.canvas?.ToArray() ?? Array.Empty<string>(),
            gold = s.gold?.ToArray() ?? Array.Empty<int>(),
            mystery = s.mystery,
            band = s.band,
            slots = s.slots,
            spools = s.spools.Select(p => new SpoolData
            {
                column = p.column, color = p.color, loops = p.loops, hidden = p.hidden, link = p.link, knots = p.knots,
                locked = p.locked, key = p.key, rainbow = p.rainbow, thick = p.thick, ice = p.ice,
            }).ToArray(),
        };
    }
}
