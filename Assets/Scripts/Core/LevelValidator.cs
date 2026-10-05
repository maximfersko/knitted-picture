#nullable enable

using System.Collections.Generic;
using System.Linq;

namespace KnittedPicture.Core
{
    /// <summary>
    /// Проверка уровня до игры: размеры, цвета, что петель в клубках ровно столько,
    /// сколько в картинке, и что особые клубки не создают тупиков.
    /// </summary>
    public static class LevelValidator
    {
        public const int MaxColors = 10;
        public const int MaxSide = 32;
        public const int MaxColumns = 6;
        public const int MaxKnots = 3;
        public const int MaxTimeLimit = 900;

        public static List<string> Validate(LevelData? level)
        {
            var errors = new List<string>();
            if (level == null)
            {
                errors.Add("уровень не задан");
                return errors;
            }

            if (level.width < 2 || level.width > MaxSide || level.height < 2 || level.height > MaxSide)
            {
                errors.Add($"размер {level.width}×{level.height} вне 2..{MaxSide}");
                return errors;
            }

            var colorCount = level.colors?.Length ?? 0;
            if (colorCount < 1 || colorCount > MaxColors)
            {
                errors.Add($"цветов {colorCount}, нужно 1..{MaxColors}");
                return errors;
            }

            if (level.slots < 1 || level.slots > LoomGame.MaxSlots)
            {
                errors.Add($"слотов {level.slots}, нужно 1..{LoomGame.MaxSlots}");
            }

            if (level.timeLimit < 0 || level.timeLimit > MaxTimeLimit)
            {
                errors.Add($"таймер {level.timeLimit} с вне 0..{MaxTimeLimit}");
            }

            var stitchesByColor = new int[colorCount];
            if (level.picture == null || level.picture.Length != level.height)
            {
                errors.Add($"в картинке {level.picture?.Length ?? 0} рядов, ожидалось {level.height}");
                return errors;
            }

            for (var y = 0; y < level.height; y++)
            {
                var row = level.picture[y] ?? string.Empty;
                if (row.Length != level.width)
                {
                    errors.Add($"ряд {y}: длина {row.Length}, ожидалось {level.width}");
                    continue;
                }

                for (var x = 0; x < row.Length; x++)
                {
                    var color = row[x] - '0';
                    if (color < 0 || color >= colorCount)
                    {
                        errors.Add($"ряд {y}, клетка {x}: цвет '{row[x]}' не из палитры");
                    }
                    else
                    {
                        stitchesByColor[color]++;
                    }
                }
            }

            var spools = level.spools ?? new SpoolData[0];
            if (spools.Length == 0)
            {
                errors.Add("на поле нет клубков");
                return errors;
            }

            var loopsByColor = new int[colorCount];
            var maxColumn = -1;
            foreach (var s in spools)
            {
                if (s.column < 0 || s.column >= MaxColumns)
                {
                    errors.Add($"клубок в колонке {s.column}, нужно 0..{MaxColumns - 1}");
                    continue;
                }

                if (s.color < 0 || s.color >= colorCount)
                {
                    errors.Add($"клубок цвета {s.color} не из палитры");
                    continue;
                }

                if (s.loops < 1)
                {
                    errors.Add($"клубок цвета {s.color}: петель {s.loops}");
                    continue;
                }

                if (s.knots < 0 || s.knots > MaxKnots)
                {
                    errors.Add($"клубок цвета {s.color}: узелков {s.knots}, нужно 0..{MaxKnots}");
                }

                loopsByColor[s.color] += s.loops;
                if (s.column > maxColumn)
                {
                    maxColumn = s.column;
                }
            }

            for (var i = 0; i <= maxColumn; i++)
            {
                if (!spools.Any(s => s.column == i))
                {
                    errors.Add($"колонка {i} пустая");
                }
            }

            for (var c = 0; c < colorCount; c++)
            {
                if (loopsByColor[c] != stitchesByColor[c])
                {
                    errors.Add($"цвет {c}: в клубках {loopsByColor[c]} петель, в картинке {stitchesByColor[c]}");
                }
            }

            ValidateLinks(spools, errors);
            ValidateLocks(spools, errors);
            return errors;
        }

        private static void ValidateLinks(SpoolData[] spools, List<string> errors)
        {
            foreach (var group in spools.Where(s => s.link != 0).GroupBy(s => s.link))
            {
                var pair = group.ToList();
                if (pair.Count != 2)
                {
                    errors.Add($"связка {group.Key}: клубков {pair.Count}, нужно 2");
                    continue;
                }

                if (pair[0].column == pair[1].column)
                {
                    errors.Add($"связка {group.Key}: оба клубка в одной колонке");
                }

                if (pair.Any(s => s.locked || s.knots > 0 || s.key))
                {
                    errors.Add($"связка {group.Key}: связанный клубок не может быть с замком, ключом или узелком");
                }
            }
        }

        private static void ValidateLocks(SpoolData[] spools, List<string> errors)
        {
            var hasLocks = spools.Any(s => s.locked);
            var keys = spools.Where(s => s.key).ToList();
            if (hasLocks && keys.Count == 0)
            {
                errors.Add("есть замки, но нет ключа");
            }

            if (keys.Any(s => s.locked))
            {
                errors.Add("клубок с ключом не может быть под замком");
            }

            // Ключ за замком в своей колонке — тупик: до ключа не добраться.
            foreach (var key in keys)
            {
                var column = spools.Where(s => s.column == key.column).ToList();
                var keyDepth = column.IndexOf(key);
                if (column.Take(keyDepth).Any(s => s.locked))
                {
                    errors.Add($"ключ в колонке {key.column} стоит за замком");
                }
            }
        }
    }
}
