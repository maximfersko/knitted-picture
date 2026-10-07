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
        public ChestContents(bool big, int coins, BoosterType[] boosters, int boosterCount, int[] yarns)
        {
            Big = big;
            Coins = coins;
            Boosters = boosters;
            BoosterCount = boosterCount;
            Yarns = yarns;
        }

        /// <summary>Большой сундук: каждый ChestCycle-й. Больше монет, бустеров и хотя бы один редкий моток.</summary>
        public bool Big { get; }

        public int Coins { get; }

        /// <summary>Бустеры на выбор (разные, только уже открытые).</summary>
        public BoosterType[] Boosters { get; }

        /// <summary>Сколько штук выбранного бустера.</summary>
        public int BoosterCount { get; }

        /// <summary>Мотки на выбор (разные).</summary>
        public int[] Yarns { get; }
    }

    /// <summary>Правила звёздного сундука: цикл маленьких и больших, что в них кладётся.</summary>
    public static class Chest
    {
        /// <summary>Каждый четвёртый сундук — большой: игрок видит цикл и ждёт его.</summary>
        public const int Cycle = 4;

        /// <summary>Сколько вариантов на выбор.</summary>
        public const int Choices = 3;

        public static bool IsBig(int chestNumber) => chestNumber % Cycle == Cycle - 1;

        /// <summary>
        /// Собрать сундук номер chestNumber (с нуля). Мотки тянутся по тем же весам, что и везде:
        /// редкие реже, ненайденные в полтора раза чаще. В большом — хотя бы один редкий или эпический.
        /// </summary>
        public static ChestContents Roll(int chestNumber, int level, System.Func<int, bool> hasYarn)
        {
            var rng = new System.Random(chestNumber * 7919 + 13);
            var big = IsBig(chestNumber);
            var coins = big ? 120 + 10 * rng.Next(5) : 50 + 10 * rng.Next(5);

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
            var needRare = big;
            while (yarns.Count < Choices && yarns.Count < YarnCatalog.Count)
            {
                var total = 0;
                var weights = new int[YarnCatalog.Count];
                for (var i = 0; i < weights.Length; i++)
                {
                    var rarity = YarnCatalog.All[i].Rarity;
                    if (yarns.Contains(i) || (needRare && yarns.Count == Choices - 1 && rarity == Rarity.Common))
                    {
                        continue;
                    }

                    weights[i] = YarnCatalog.RollWeight(rarity, hasYarn(i));
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
                        needRare &= YarnCatalog.All[i].Rarity == Rarity.Common;
                        break;
                    }
                }
            }

            Shuffle(yarns, rng);
            return new ChestContents(big, coins, boosters.ToArray(), big ? 2 : 1, yarns.ToArray());
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
