#nullable enable

using System;

namespace KnittedPicture.Core
{
    /// <summary>Время года сезона: от него название, сезонная рамка и сезонный моток.</summary>
    public enum SeasonTheme
    {
        Winter,
        Spring,
        Summer,
        Autumn,
    }

    /// <summary>Что даёт ступень сезона.</summary>
    public enum SeasonPrize
    {
        Coins,
        Booster,
        Yarn,
        Big,
        SeasonYarn,
        SeasonFrame,
    }

    /// <summary>Ступень дорожки: редкость (цвет плашки), что даёт и сама награда.</summary>
    public readonly struct SeasonStep
    {
        public SeasonStep(int number, Rarity rarity, SeasonPrize prize, Reward reward)
        {
            Number = number;
            Rarity = rarity;
            Prize = prize;
            Reward = reward;
        }

        /// <summary>Номер ступени с единицы.</summary>
        public int Number { get; }

        public Rarity Rarity { get; }

        public SeasonPrize Prize { get; }

        public Reward Reward { get; }
    }

    /// <summary>
    /// «Вязальный сезон» — бесплатная дорожка на 4 недели, как сезонные пропуски в Royal Match и Clash Royale,
    /// но без платной ветки. Очки сезона: звёзды за новые картинки и улучшение результата, 1 за повтор,
    /// 3 за узор дня, 5 за задания дня и 5 за выставку недели. Каждые StepPoints очков — ступень. Награды идут
    /// по лестнице редкости: обычные (монеты) и необычные (монеты, на 8-й, 16-й и 24-й — бустер) чередуются,
    /// каждая пятая — редкая (моток), 10-я и 20-я — эпические (60 монет и бустер), 29-я — сезонный легендарный
    /// моток, 30-я — сезонная рамка. За полный сезон около 520 монет, 5 бустеров и 4 мотка; вместе с выставкой
    /// доход на 3–8% выше, чем до сезона, у редко играющих до 18% (цели и симуляция — docs/ECONOMY.md). Сезон
    /// закончился — незабранное сгорает, начинается новый.
    /// </summary>
    public static class Season
    {
        public const int Days = 28;

        public const int Steps = 30;

        /// <summary>
        /// Очков на ступень. Обычный игрок (3 уровня в день, около 7 очков) проходит дорожку примерно за 24 дня;
        /// кто прошёл все уровни, добирает узорами, заданиями, выставкой и повторами.
        /// </summary>
        public const int StepPoints = 6;

        /// <summary>С какого уровня открыт: игрок уже знает звёзды и награды.</summary>
        public const int FromLevel = 4;

        public const int ReplayPoints = 1;
        public const int PatternPoints = 3;
        public const int MissionsPoints = 5;
        public const int ExhibitionPoints = 5;

        /// <summary>Монеты вместо рамки, если сезонная рамка этого времени года уже есть (сезоны повторяются через год).</summary>
        public const int FrameOwnedCoins = 400;

        /// <summary>Первый сезон начинается в понедельник 5 января 1970 (день 4).</summary>
        private const int Epoch = 4;

        public static int SeasonOf(int day) => (int)Math.Floor((day - Epoch) / (double)Days);

        public static int StartDay(int season) => Epoch + season * Days;

        /// <summary>Сколько дней осталось, считая сегодняшний.</summary>
        public static int DaysLeft(int day) => StartDay(SeasonOf(day) + 1) - day;

        /// <summary>Время года — по середине сезона.</summary>
        public static SeasonTheme ThemeOf(int season)
        {
            var month = new DateTime(1970, 1, 1).AddDays(StartDay(season) + Days / 2).Month;
            return month switch
            {
                12 or 1 or 2 => SeasonTheme.Winter,
                3 or 4 or 5 => SeasonTheme.Spring,
                6 or 7 or 8 => SeasonTheme.Summer,
                _ => SeasonTheme.Autumn,
            };
        }

        public static string Name(SeasonTheme theme) => theme switch
        {
            SeasonTheme.Winter => L.T("Зимний сезон"),
            SeasonTheme.Spring => L.T("Весенний сезон"),
            SeasonTheme.Summer => L.T("Летний сезон"),
            _ => L.T("Осенний сезон"),
        };

        /// <summary>Сезонный легендарный моток времени года.</summary>
        public static int YarnOf(SeasonTheme theme) => 16 + (int)theme;

        public static int PointsFor(int step) => step * StepPoints;

        /// <summary>Ступень номер number (1..Steps).</summary>
        public static SeasonStep Step(int number, SeasonTheme theme)
        {
            if (number == Steps)
            {
                return new SeasonStep(number, Rarity.Legendary, SeasonPrize.SeasonFrame, Reward.CoinsOnly(0));
            }

            if (number == Steps - 1)
            {
                return new SeasonStep(number, Rarity.Legendary, SeasonPrize.SeasonYarn, new Reward(0, null, 0, YarnOf(theme)));
            }

            if (number % 10 == 0)
            {
                var booster = number == 10 ? BoosterType.Scissors : BoosterType.Slot;
                return new SeasonStep(number, Rarity.Epic, SeasonPrize.Big, new Reward(60, booster, 1));
            }

            if (number % 5 == 0)
            {
                return new SeasonStep(number, Rarity.Rare, SeasonPrize.Yarn, new Reward(20, null, 0, DailyGift.RandomYarn));
            }

            if (number % 2 == 0)
            {
                // Необычные: бустер на 8-й, 16-й и 24-й, иначе монеты — бустеров не больше, чем нужно (см. docs/ECONOMY.md).
                if (number % 8 == 0)
                {
                    var booster = (number / 8 % 3) switch
                    {
                        0 => BoosterType.Undo,
                        1 => BoosterType.Slot,
                        _ => BoosterType.Scissors,
                    };
                    return new SeasonStep(number, Rarity.Uncommon, SeasonPrize.Booster, new Reward(0, booster, 1));
                }

                return new SeasonStep(number, Rarity.Uncommon, SeasonPrize.Coins, Reward.CoinsOnly(20));
            }

            return new SeasonStep(number, Rarity.Common, SeasonPrize.Coins, Reward.CoinsOnly(10 + number / 3));
        }
    }
}
