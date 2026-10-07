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

    /// <summary>
    /// Чем помогает пряжа «в работе». Только заметные монеты — их видно строкой на экране победы.
    /// Правила головоломки не меняются: уровни и три звезды остаются честными.
    /// </summary>
    public enum YarnPerk
    {
        /// <summary>Тёплая копилка: монеты за каждую новую картинку.</summary>
        Coins,

        /// <summary>Золотые руки: золотые петли дороже.</summary>
        Gold,
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
            new YarnInfo(11, "Радужная", "Секционное крашение, вся радуга", "#FF6FA5", "#6CB8FF", YarnLook.Rainbow, Rarity.Epic),
            new YarnInfo(12, "Твид", "Тёплая шерсть с цветными крапинками", "#8A6E5A", "#E8A65A", YarnLook.Melange, Rarity.Common),
            new YarnInfo(13, "Ангора", "Пушистая, как облачко", "#FFE3EC", "#FFFFFF", YarnLook.Sparkle, Rarity.Rare),
            new YarnInfo(14, "Пряжа-травка", "С длинными мягкими ворсинками", "#9BE0B4", "#2EBD7E", YarnLook.Melange, Rarity.Rare),
            new YarnInfo(15, "Золотая нить", "Блестит, как ёлочная гирлянда", "#FFC23C", "#FFF1A8", YarnLook.Sparkle, Rarity.Epic),
        };

        public static readonly YarnSet[] Sets =
        {
            new YarnSet(0, "Шерсть", 0, 4, new Reward(150, BoosterType.Slot, 1)),
            new YarnSet(1, "Хлопок и шёлк", 4, 4, new Reward(200, BoosterType.Scissors, 1)),
            new YarnSet(2, "Особая пряжа", 8, 4, new Reward(300, BoosterType.Undo, 2)),
            new YarnSet(3, "Праздничная", 12, 4, new Reward(400, BoosterType.Slot, 2)),
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

        /// <summary>Монеты за повторный моток, когда он уже максимального уровня.</summary>
        public static int DuplicateCoins(Rarity rarity) => rarity == Rarity.Common ? 10 : rarity == Rarity.Rare ? 20 : 40;

        // ---------- Характеристики ----------

        /// <summary>Уровень мотка растёт от повторов: 1 моток — ур. 1, второй — ур. 2, третий — ур. 3.</summary>
        public const int MaxLevel = 3;

        /// <summary>
        /// Бонус мотка — общий для набора: тёплые наборы («Шерсть», «Праздничная») копят монеты,
        /// гладкие и блестящие («Хлопок и шёлк», «Особая пряжа») делают дороже золото.
        /// </summary>
        public static YarnPerk PerkOf(int yarn) => SetOf(yarn).Id % 3 == 0 ? YarnPerk.Coins : YarnPerk.Gold;

        /// <summary>С этого уровня моток «в работе» начинает давать бонус: новичка не грузим лишним.</summary>
        public const int PerksFromLevel = 9;

        /// <summary>Сила бонуса 1..5: редкость (1, 2, 3) плюс уровень сверх первого.</summary>
        public static int Power(int yarn, int level)
        {
            var rarity = All[yarn].Rarity == Rarity.Common ? 1 : All[yarn].Rarity == Rarity.Rare ? 2 : 3;
            return level <= 0 ? 0 : rarity + System.Math.Min(level, MaxLevel) - 1;
        }

        /// <summary>+монет за новую картинку.</summary>
        public static int CoinsBonus(int power) => 2 * power;

        /// <summary>+монет за каждую пойманную золотую петлю.</summary>
        public static int GoldBonus(int power) => power;

        public static string PerkTitle(YarnPerk perk) => perk == YarnPerk.Coins ? "Тёплая копилка" : "Золотые руки";

        /// <summary>«монета», «монеты» или «монет» — по числу.</summary>
        public static string CoinsWord(int n)
        {
            var mod10 = n % 10;
            var mod100 = n % 100;
            if (mod10 == 1 && mod100 != 11)
            {
                return "монета";
            }

            return mod10 >= 2 && mod10 <= 4 && (mod100 < 12 || mod100 > 14) ? "монеты" : "монет";
        }

        /// <summary>Что даёт бонус при этой силе — коротко: для полоски «В работе», тостов и строки победы.</summary>
        public static string PerkText(YarnPerk perk, int power)
        {
            var n = perk == YarnPerk.Coins ? CoinsBonus(power) : GoldBonus(power);
            return perk == YarnPerk.Coins
                ? $"+{n} {CoinsWord(n)} за каждую новую картинку"
                : $"+{n} {CoinsWord(n)} сверху за каждую золотую петлю";
        }

        /// <summary>То же, но совсем коротко: для строки «следующий уровень».</summary>
        public static string PerkShort(YarnPerk perk, int power)
        {
            var n = perk == YarnPerk.Coins ? CoinsBonus(power) : GoldBonus(power);
            return perk == YarnPerk.Coins ? $"+{n} {CoinsWord(n)} за картинку" : $"+{n} {CoinsWord(n)} за золотую петлю";
        }

        /// <summary>Понятное предложение для карточки мотка: как именно работает бонус.</summary>
        public static string PerkSentence(YarnPerk perk, int power)
        {
            var n = perk == YarnPerk.Coins ? CoinsBonus(power) : GoldBonus(power);
            return perk == YarnPerk.Coins
                ? $"Каждая новая картинка приносит ещё +{n} {CoinsWord(n)}"
                : $"Каждая золотая петля, пойманная в комбо, даёт ещё +{n} {CoinsWord(n)}";
        }
    }
}
