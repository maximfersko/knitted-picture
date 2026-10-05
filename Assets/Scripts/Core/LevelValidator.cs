#nullable enable

using System.Collections.Generic;

namespace KnittedPicture.Core
{
    /// <summary>Проверка уровня до игры: размеры, цвета и что петель в клубках ровно столько, сколько в картинке.</summary>
    public static class LevelValidator
    {
        public const int MaxColors = 10;
        public const int MaxSide = 32;
        public const int MaxSlots = 8;

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

            if (level.slots < 1 || level.slots > MaxSlots)
            {
                errors.Add($"слотов {level.slots}, нужно 1..{MaxSlots}");
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
                if (s.column < 0)
                {
                    errors.Add($"клубок в колонке {s.column}");
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

                loopsByColor[s.color] += s.loops;
                if (s.column > maxColumn)
                {
                    maxColumn = s.column;
                }
            }

            var usedColumns = new bool[maxColumn + 1];
            foreach (var s in spools)
            {
                if (s.column >= 0)
                {
                    usedColumns[s.column] = true;
                }
            }

            for (var i = 0; i < usedColumns.Length; i++)
            {
                if (!usedColumns[i])
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

            return errors;
        }
    }
}
