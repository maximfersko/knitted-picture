#nullable enable

namespace KnittedPicture.Core
{
    public enum YarnLook
    {
        /// <summary>Однотонная.</summary>
        Plain,

        /// <summary>Меланж: два оттенка вперемешку.</summary>
        Melange,

        /// <summary>С блёстками.</summary>
        Sparkle,

        /// <summary>Секционная радуга.</summary>
        Rainbow,
    }

    public enum Rarity
    {
        Common,
        Rare,
        Epic,
    }

    public sealed class YarnInfo
    {
        public YarnInfo(int id, string name, string description, string hex, string hex2, YarnLook look, Rarity rarity)
        {
            Id = id;
            Name = name;
            Description = description;
            Hex = hex;
            Hex2 = hex2;
            Look = look;
            Rarity = rarity;
        }

        public int Id { get; }

        public string Name { get; }

        public string Description { get; }

        public string Hex { get; }

        /// <summary>Второй цвет меланжа или блёсток.</summary>
        public string Hex2 { get; }

        public YarnLook Look { get; }

        public Rarity Rarity { get; }
    }

    public sealed class YarnSet
    {
        public YarnSet(int id, string name, int first, int count, Reward reward)
        {
            Id = id;
            Name = name;
            First = first;
            Count = count;
            Reward = reward;
        }

        public int Id { get; }

        public string Name { get; }

        public int First { get; }

        public int Count { get; }

        /// <summary>Награда за полный набор.</summary>
        public Reward Reward { get; }

        public bool Contains(int yarn) => yarn >= First && yarn < First + Count;
    }

    /// <summary>
    /// «Корзинка»: редкая пряжа, которая выпадает за новые картинки, из сундуков и подарков.
    /// Три набора по четыре мотка; за полный набор — награда.
    /// </summary>
    public static class YarnCatalog
    {
        public static readonly YarnInfo[] All =
        {
            new YarnInfo(0, "Меринос", "Мягкая шерсть тонкорунных овец", "#F6E7D2", "#F6E7D2", YarnLook.Plain, Rarity.Common),
            new YarnInfo(1, "Альпака", "Тёплая и пушистая, из Анд", "#C9A27E", "#E2C6A6", YarnLook.Melange, Rarity.Common),
            new YarnInfo(2, "Мохер", "Воздушная, с длинным ворсом", "#E7B6D9", "#F6DDF0", YarnLook.Melange, Rarity.Rare),
            new YarnInfo(3, "Кашемир", "Самая нежная шерсть", "#B9A7E8", "#B9A7E8", YarnLook.Plain, Rarity.Epic),
            new YarnInfo(4, "Хлопок", "Лёгкий и прохладный", "#9FD8F5", "#9FD8F5", YarnLook.Plain, Rarity.Common),
            new YarnInfo(5, "Лён", "Прочный, с благородной фактурой", "#D8CBA4", "#BFAF84", YarnLook.Melange, Rarity.Common),
            new YarnInfo(6, "Бамбук", "Гладкий и блестящий", "#9BE0B4", "#D8F5E3", YarnLook.Sparkle, Rarity.Rare),
            new YarnInfo(7, "Шёлк", "Струится и переливается", "#FFB3B3", "#FFE1E1", YarnLook.Sparkle, Rarity.Epic),
            new YarnInfo(8, "Букле", "С кудрявыми петельками", "#FFC23C", "#FFE08A", YarnLook.Melange, Rarity.Common),
            new YarnInfo(9, "Меланж", "Три оттенка в одной нитке", "#6CB8FF", "#FF6FA5", YarnLook.Melange, Rarity.Rare),
            new YarnInfo(10, "Люрекс", "Нитка с блёстками", "#8B5CF6", "#FFD86B", YarnLook.Sparkle, Rarity.Rare),
            new YarnInfo(11, "Радужная", "Секционное крашение — вся радуга", "#FF6FA5", "#6CB8FF", YarnLook.Rainbow, Rarity.Epic),
        };

        public static readonly YarnSet[] Sets =
        {
            new YarnSet(0, "Шерсть", 0, 4, new Reward(150, BoosterType.Slot, 1)),
            new YarnSet(1, "Хлопок и шёлк", 4, 4, new Reward(200, BoosterType.Scissors, 1)),
            new YarnSet(2, "Особая пряжа", 8, 4, new Reward(300, BoosterType.Undo, 2)),
        };

        public static int Count => All.Length;

        public static YarnSet SetOf(int yarn)
        {
            foreach (var set in Sets)
            {
                if (set.Contains(yarn))
                {
                    return set;
                }
            }

            return Sets[0];
        }

        /// <summary>Вес выпадения: обычная — часто, эпическая — редко.</summary>
        public static int Weight(Rarity rarity) => rarity == Rarity.Common ? 6 : rarity == Rarity.Rare ? 3 : 1;

        public static string RarityName(Rarity rarity) => rarity == Rarity.Common ? "обычная" : rarity == Rarity.Rare ? "редкая" : "эпическая";

        /// <summary>Монеты за повторный моток.</summary>
        public static int DuplicateCoins(Rarity rarity) => rarity == Rarity.Common ? 10 : rarity == Rarity.Rare ? 20 : 40;
    }
}
