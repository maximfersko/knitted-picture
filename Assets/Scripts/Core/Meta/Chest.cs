#nullable enable

using System.Collections.Generic;

namespace KnittedPicture.Core
{
    /// <summary>
    /// Что лежит в звёздном сундуке. Монеты — сразу, а из бустеров и мотков игрок
    /// выбирает по одному из трёх. Содержимое не случайно при каждом открытии:
    /// оно зависит от номера сундука и коллекции игрока, поэтому выход из игры
    /// посреди открытия ничего не перекатывает.
    /// </summary>
    public sealed class ChestContents
    {
        public ChestContents(Rarity rarity, int coins, BoosterType[] boosters, int boosterCount, int[] yarns)
        {
            Rarity = rarity;
            Coins = coins;
            Boosters = boosters;
            BoosterCount = boosterCount;
            Yarns = yarns;
        }

        /// <summary>Редкость сундука (Chest.RarityOf): от неё монеты, число бустеров и гарантия мотка.</summary>
        public Rarity Rarity { get; }

        /// <summary>Большой сундук: редкий и выше, каждый Chest.Cycle-й.</summary>
        public bool Big => Rarity >= Rarity.Rare;

        public int Coins { get; }

        /// <summary>Бустеры на выбор (разные, только уже открытые).</summary>
        public BoosterType[] Boosters { get; }

        /// <summary>Сколько штук выбранного бустера.</summary>
        public int BoosterCount { get; }

        /// <summary>Мотки на выбор (разные).</summary>
        public int[] Yarns { get; }
    }

    /// <summary>
    /// Правила звёздного сундука. У каждого сундука редкость — та же лестница, что у пряжи и рамок, и она
    /// заранее видна на пути сундуков: игрок ждёт следующий ценный. Каждый 4-й — большой (редкий), из них
    /// каждый 3-й эпический, а каждый 48-й — легендарный; среди маленьких через один — необычный.
    /// На 48 сундуков: 24 обычных, 12 необычных, 8 редких, 3 эпических, 1 легендарный.
    /// </summary>
    public static class Chest
    {
        /// <summary>Каждый четвёртый сундук — большой: игрок видит цикл и ждёт его.</summary>
        public const int Cycle = 4;

        /// <summary>Сколько вариантов на выбор.</summary>
        public const int Choices = 3;

        public static bool IsBig(int chestNumber) => RarityOf(chestNumber) >= Rarity.Rare;

        /// <summary>Редкость сундука номер chestNumber (с нуля).</summary>
        public static Rarity RarityOf(int chestNumber)
        {
            if (chestNumber % (Cycle * 12) == Cycle * 12 - 1)
            {
                return Rarity.Legendary;
            }

            if (chestNumber % (Cycle * 3) == Cycle * 3 - 1)
            {
                return Rarity.Epic;
            }

            if (chestNumber % Cycle == Cycle - 1)
            {
                return Rarity.Rare;
            }

            return chestNumber % 2 == 1 ? Rarity.Uncommon : Rarity.Common;
        }

        /// <summary>Сколько монет бывает в сундуке: показываем на экране сундука, катаем в Roll.</summary>
        public static (int Min, int Max) CoinRange(Rarity rarity) => rarity switch
        {
            Rarity.Uncommon => (40, 70),
            Rarity.Rare => (80, 120),
            Rarity.Epic => (120, 160),
            Rarity.Legendary => (200, 250),
            _ => (30, 60),
        };

        /// <summary>Сколько штук выбранного бустера: у эпического и легендарного — по два.</summary>
        public static int BoosterCountOf(Rarity rarity) => rarity >= Rarity.Epic ? 2 : 1;

        /// <summary>
        /// Гарантия мотка: хотя бы один из трёх не ниже этой редкости. Обычный — без гарантии,
        /// легендарный — сама легендарная пряжа среди вариантов.
        /// </summary>
        public static Rarity YarnFloor(Rarity rarity) => rarity;

        /// <summary>
        /// Собрать сундук номер chestNumber (с нуля). Мотки тянутся по тем же весам, что и везде:
        /// редкие реже, ненайденные в полтора раза чаще. Хотя бы один моток не ниже редкости сундука (YarnFloor).
        /// </summary>
        public static ChestContents Roll(int chestNumber, int level, System.Func<int, bool> hasYarn)
        {
            var rng = new System.Random(chestNumber * 7919 + 13);
            var rarity = RarityOf(chestNumber);
            var range = CoinRange(rarity);
            var coins = range.Min + 10 * rng.Next((range.Max - range.Min) / 10 + 1);

            var boosters = new List<BoosterType>();
            foreach (BoosterType type in System.Enum.GetValues(typeof(BoosterType)))
            {
                if (level >= Economy.UnlockLevel(type))
                {
                    boosters.Add(type);
                }
            }

            if (boosters.Count == 0)
            {
                boosters.Add(BoosterType.Slot);
            }

            Shuffle(boosters, rng);
            if (boosters.Count > Choices)
            {
                boosters.RemoveRange(Choices, boosters.Count - Choices);
            }

            var yarns = new List<int>();
            var floor = YarnFloor(rarity);
            var needRare = floor > Rarity.Common;
            while (yarns.Count < Choices && yarns.Count < YarnCatalog.Count)
            {
                var total = 0;
                var weights = new int[YarnCatalog.Count];
                for (var i = 0; i < weights.Length; i++)
                {
                    var yarnRarity = YarnCatalog.All[i].Rarity;
                    if (yarns.Contains(i) || (needRare && yarns.Count == Choices - 1 && yarnRarity < floor))
                    {
                        continue;
                    }

                    weights[i] = YarnCatalog.RollWeight(i, hasYarn(i));
                    total += weights[i];
                }

                if (total == 0)
                {
                    break;
                }

                var roll = rng.Next(total);
                for (var i = 0; i < weights.Length; i++)
                {
                    roll -= weights[i];
                    if (roll < 0)
                    {
                        yarns.Add(i);
                        needRare &= YarnCatalog.All[i].Rarity < floor;
                        break;
                    }
                }
            }

            Shuffle(yarns, rng);
            return new ChestContents(rarity, coins, boosters.ToArray(), BoosterCountOf(rarity), yarns.ToArray());
        }

        private static void Shuffle<T>(List<T> list, System.Random rng)
        {
            for (var i = list.Count - 1; i > 0; i--)
            {
                var j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
