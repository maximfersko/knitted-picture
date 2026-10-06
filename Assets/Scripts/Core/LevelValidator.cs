#nullable enable

using System.Collections.Generic;
using System.Linq;

namespace KnittedPicture.Core
{
    /// <summary>
    /// Проверка уровня до игры: размеры, цвета, канва, что петель в клубках хватает на рисунок
    /// и что особые клубки не создают очевидных тупиков.
    /// </summary>
    public static class LevelValidator
    {
        public const int MaxColors = 10;
        public const int MaxSide = 32;
        public const int MaxColumns = 6;
        public const int MaxKnots = 3;
        public const int MaxTimeLimit = 900;
        public const int MaxIce = 30;

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

            if (level.par < 0)
            {
                errors.Add($"цель {level.par} тапов");
            }

            var stitchesByColor = new int[colorCount];
            if (level.picture == null || level.picture.Length != level.height)
            {
                errors.Add($"в картинке {level.picture?.Length ?? 0} рядов, ожидалось {level.height}");
                return errors;
            }

            var canvas = level.canvas ?? new string[0];
            if (canvas.Length != 0 && canvas.Length != level.height)
            {
                errors.Add($"в канве {canvas.Length} рядов, ожидалось {level.height}");
                return errors;
            }

            for (var y = 0; y < canvas.Length; y++)
            {
                if (canvas[y] == null || canvas[y].Length != level.width || canvas[y].Any(ch => ch != '0' && ch != '1'))
                {
                    errors.Add($"канва, ряд {y}: нужно {level.width} символов '0' или '1'");
                    return errors;
                }
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
                    else if (canvas.Length == 0 || canvas[y][x] == '0')
                    {
                        stitchesByColor[color]++;
                    }
                }
            }

            if (stitchesByColor.Sum() == 0)
            {
                errors.Add("вся картинка — ткань, вязать нечего");
            }

            foreach (var cell in level.gold ?? new int[0])
            {
                if (cell < 0 || cell >= level.width * level.height)
                {
                    errors.Add($"золотая петля {cell} вне картинки");
                }
                else if (canvas.Length > 0 && canvas[cell / level.width][cell % level.width] == '1')
                {
                    errors.Add($"золотая петля {cell} на ткани");
                }
            }

            var spools = level.spools ?? new SpoolData[0];
            if (spools.Length == 0)
            {
                errors.Add("на поле нет клубков");
                return errors;
            }

            var loopsByColor = new int[colorCount];
            var rainbowLoops = 0;
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

                if (s.ice < 0 || s.ice > MaxIce)
                {
                    errors.Add($"клубок цвета {s.color}: лёд {s.ice}, нужно 0..{MaxIce}");
                }

                if (s.ice > 0 && (s.rainbow || s.hidden || s.link != 0 || s.locked || s.key || s.knots > 0))
                {
                    errors.Add($"ледяной клубок цвета {s.color} не может быть радужным, скрытым, связанным, с замком, ключом или узелком");
                }

                if (s.thick && s.link != 0)
                {
                    errors.Add($"толстый клубок цвета {s.color} не может быть в связке");
                }

                if (s.rainbow)
                {
                    rainbowLoops += s.loops;
                }
                else
                {
                    loopsByColor[s.color] += s.loops;
                }
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

            // Пряжи может быть больше, чем нужно (лишняя уходит со станка), но не меньше.
            for (var c = 0; c < colorCount; c++)
            {
                if (loopsByColor[c] + rainbowLoops < stitchesByColor[c])
                {
                    errors.Add($"цвет {c}: в клубках {loopsByColor[c]} петель (и {rainbowLoops} радужных), в картинке {stitchesByColor[c]}");
                }
            }

            if (loopsByColor.Sum() + rainbowLoops < stitchesByColor.Sum())
            {
                errors.Add($"пряжи на {loopsByColor.Sum() + rainbowLoops} петель, а вязать {stitchesByColor.Sum()}");
            }

            // Лёд тает только от петель своего цвета: если их меньше, чем льда, клубок не оттает никогда.
            foreach (var s in spools.Where(s => s.ice > 0 && s.color >= 0 && s.color < colorCount))
            {
                if (s.ice > stitchesByColor[s.color])
                {
                    errors.Add($"ледяной клубок цвета {s.color}: льда {s.ice}, а петель цвета в рисунке {stitchesByColor[s.color]}");
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
