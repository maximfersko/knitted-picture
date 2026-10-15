#nullable enable

using System;
using System.Collections.Generic;

namespace KnittedPicture.Core
{
    /// <summary>Тема выставки: название и цвета пряжи (из YarnColors), которые ей подходят.</summary>
    public sealed class ExhibitionTheme
    {
        public ExhibitionTheme(string name, params string[] colorNames)
        {
            _name = name;
            var hexes = new List<string>();
            foreach (var colorName in colorNames)
            {
                foreach (var (hex, n) in YarnColors.All)
                {
                    if (n == colorName)
                    {
                        hexes.Add(hex);
                    }
                }
            }

            Colors = hexes.ToArray();
        }

        private readonly string _name;

        public string Name => L.T(_name);

        /// <summary>Hex цветов темы (из YarnColors.All).</summary>
        public string[] Colors { get; }

        public bool Fits(string hex)
        {
            foreach (var c in Colors)
            {
                if (string.Equals(c, hex, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// «Выставка недели»: у недели своя тема («Море», «Осень»…), игрок выставляет три готовые картинки альбома,
    /// каждую в своей рамке, и получает оценку жюри. Очки за картинку: цвета в тему (до 60), звёзды (по 10)
    /// и рамка (редкость по 6, рамка коллекции этой картинки ещё +6). Три картинки — до 360 очков; медаль:
    /// бронза от 150 (20 монет), серебро от 210 (40), золото от 270 (60 и моток). Раз в неделю; оценку видно до того, как выставить.
    /// Рамки и пряжа начинают работать в игре, а не только лежать в коллекции.
    /// </summary>
    public static class Exhibition
    {
        public const int Pictures = 3;

        /// <summary>С какого уровня открыта: нужно хотя бы три готовые картинки и пара рамок на выбор.</summary>
        public const int FromLevel = 6;

        public const int ColorPoints = 60;
        public const int StarPoints = 10;
        public const int RarityPoints = 6;
        public const int MatchPoints = 6;

        public static readonly int[] MedalScores = { 150, 210, 270 };

        public static readonly ExhibitionTheme[] Themes =
        {
            new ExhibitionTheme("Море", "Голубой", "Синий", "Белый"),
            new ExhibitionTheme("Осень", "Оранжевый", "Коричневый", "Красный", "Жёлтый"),
            new ExhibitionTheme("Сад", "Зелёный", "Розовый", "Жёлтый", "Красный"),
            new ExhibitionTheme("Ночь", "Чёрный", "Синий", "Фиолетовый", "Жёлтый"),
            new ExhibitionTheme("Сладости", "Розовый", "Коричневый", "Белый", "Красный"),
            new ExhibitionTheme("Зима", "Белый", "Голубой", "Серый", "Синий"),
            new ExhibitionTheme("Солнце", "Жёлтый", "Оранжевый", "Красный"),
            new ExhibitionTheme("Лес", "Зелёный", "Коричневый", "Оранжевый"),
        };

        /// <summary>Награды за бронзу, серебро и золото.</summary>
        public static readonly Reward[] Rewards =
        {
            Reward.CoinsOnly(20),
            Reward.CoinsOnly(40),
            new Reward(60, null, 0, DailyGift.RandomYarn),
        };

        public static ExhibitionTheme ThemeOf(int week) => Themes[((week % Themes.Length) + Themes.Length) % Themes.Length];

        /// <summary>Доля клеток картинки, чей цвет подходит теме (0..1).</summary>
        public static float Share(LevelData level, ExhibitionTheme theme)
        {
            var total = 0;
            var fit = 0;
            foreach (var row in level.picture)
            {
                foreach (var ch in row)
                {
                    var index = ch - '0';
                    if (index < 0 || index >= level.colors.Length)
                    {
                        continue;
                    }

                    total++;
                    fit += theme.Fits(level.colors[index]) ? 1 : 0;
                }
            }

            return total == 0 ? 0f : (float)fit / total;
        }

        /// <summary>Очки одной картинки в рамке.</summary>
        public static int Score(LevelData level, int stars, FrameInfo frame, ExhibitionTheme theme)
        {
            var color = (int)Math.Round(ColorPoints * Share(level, theme));
            var star = Math.Max(0, Math.Min(3, stars)) * StarPoints;
            var rarity = (int)frame.Rarity * RarityPoints;
            var match = frame.Source == FrameSource.Collection && Collections.All[frame.Tag].Contains(level.id) ? MatchPoints : 0;
            return color + star + rarity + match;
        }

        /// <summary>Медаль по сумме: 0 — без медали, 1 — бронза, 2 — серебро, 3 — золото.</summary>
        public static int MedalOf(int score)
        {
            var medal = 0;
            foreach (var need in MedalScores)
            {
                medal += score >= need ? 1 : 0;
            }

            return medal;
        }

        public static string MedalName(int medal) => medal switch
        {
            3 => L.T("Золото"),
            2 => L.T("Серебро"),
            1 => L.T("Бронза"),
            _ => L.T("Без медали"),
        };
    }
}
