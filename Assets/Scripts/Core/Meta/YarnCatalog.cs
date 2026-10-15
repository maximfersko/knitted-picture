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

    public sealed class YarnInfo
    {
        public YarnInfo(int id, string name, string description, string hex, string hex2, YarnLook look, Rarity rarity, bool seasonal = false)
        {
            Seasonal = seasonal;
            Id = id;
            _name = name;
            _description = description;
            Hex = hex;
            Hex2 = hex2;
            Look = look;
            Rarity = rarity;
        }

        public int Id { get; }

        private readonly string _name;
        private readonly string _description;

        public string Name => L.T(_name);

        public string Description => L.T(_description);

        public string Hex { get; }

        /// <summary>Второй цвет меланжа или блёсток.</summary>
        public string Hex2 { get; }

        public YarnLook Look { get; }

        public Rarity Rarity { get; }

        /// <summary>Сезонная пряжа: только награда «Вязального сезона», в случайных розыгрышах не бывает.</summary>
        public bool Seasonal { get; }
    }

    public sealed class YarnSet
    {
        public YarnSet(int id, string name, int first, int count, Reward reward)
        {
            Id = id;
            _name = name;
            First = first;
            Count = count;
            Reward = reward;
        }

        public int Id { get; }

        private readonly string _name;

        public string Name => L.T(_name);

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
            new YarnInfo(1, "Альпака", "Тёплая и пушистая, из Анд", "#C9A27E", "#E2C6A6", YarnLook.Melange, Rarity.Uncommon),
            new YarnInfo(2, "Мохер", "Воздушная, с длинным ворсом", "#E7B6D9", "#F6DDF0", YarnLook.Melange, Rarity.Uncommon),
            new YarnInfo(3, "Кашемир", "Самая нежная шерсть", "#B9A7E8", "#B9A7E8", YarnLook.Plain, Rarity.Epic),
            new YarnInfo(4, "Хлопок", "Лёгкий и прохладный", "#9FD8F5", "#9FD8F5", YarnLook.Plain, Rarity.Common),
            new YarnInfo(5, "Лён", "Прочный, с благородной фактурой", "#D8CBA4", "#BFAF84", YarnLook.Melange, Rarity.Common),
            new YarnInfo(6, "Бамбук", "Гладкий и блестящий", "#9BE0B4", "#D8F5E3", YarnLook.Sparkle, Rarity.Rare),
            new YarnInfo(7, "Шёлк", "Струится и переливается", "#FFB3B3", "#FFE1E1", YarnLook.Sparkle, Rarity.Epic),
            new YarnInfo(8, "Букле", "С кудрявыми петельками", "#FFC23C", "#FFE08A", YarnLook.Melange, Rarity.Uncommon),
            new YarnInfo(9, "Меланж", "Три оттенка в одной нитке", "#6CB8FF", "#FF6FA5", YarnLook.Melange, Rarity.Rare),
            new YarnInfo(10, "Люрекс", "Нитка с блёстками", "#8B5CF6", "#FFD86B", YarnLook.Sparkle, Rarity.Rare),
            new YarnInfo(11, "Радужная", "Секционное крашение, вся радуга", "#FF6FA5", "#6CB8FF", YarnLook.Rainbow, Rarity.Epic),
            new YarnInfo(12, "Твид", "Тёплая шерсть с цветными крапинками", "#8A6E5A", "#E8A65A", YarnLook.Melange, Rarity.Common),
            new YarnInfo(13, "Ангора", "Пушистая, как облачко", "#FFE3EC", "#FFFFFF", YarnLook.Sparkle, Rarity.Rare),
            new YarnInfo(14, "Пряжа-травка", "С длинными мягкими ворсинками", "#9BE0B4", "#2EBD7E", YarnLook.Melange, Rarity.Uncommon),
            new YarnInfo(15, "Золотая нить", "Блестит, как ёлочная гирлянда", "#FFC23C", "#FFF1A8", YarnLook.Sparkle, Rarity.Legendary),

            // Сезонные: по одной на каждое время года, только за финал «Вязального сезона».
            new YarnInfo(16, "Иней", "Сверкает, как морозный узор на окне", "#BFE6FF", "#FFFFFF", YarnLook.Sparkle, Rarity.Legendary, true),
            new YarnInfo(17, "Первоцвет", "Пахнет весной и талой водой", "#9FE07A", "#FF9CCB", YarnLook.Melange, Rarity.Legendary, true),
            new YarnInfo(18, "Солнечная", "Тёплая, как песок в июле", "#FFD24F", "#FF8A1F", YarnLook.Sparkle, Rarity.Legendary, true),
            new YarnInfo(19, "Листопад", "Все краски осеннего парка", "#E8792B", "#B5401F", YarnLook.Melange, Rarity.Legendary, true),
        };

        public static readonly YarnSet[] Sets =
        {
            new YarnSet(0, "Шерсть", 0, 4, new Reward(80, BoosterType.Slot, 1)),
            new YarnSet(1, "Хлопок и шёлк", 4, 4, new Reward(100, BoosterType.Scissors, 1)),
            new YarnSet(2, "Особая пряжа", 8, 4, new Reward(150, BoosterType.Undo, 2)),
            new YarnSet(3, "Праздничная", 12, 4, new Reward(200, BoosterType.Slot, 2)),
            new YarnSet(4, "Времена года", 16, 4, new Reward(400, BoosterType.Scissors, 2)),
        };

        /// <summary>Сколько мотков без сезонных: их можно найти в обычной игре (достижение «Корзинка полна»).</summary>
        public static int BaseCount
        {
            get
            {
                var n = 0;
                foreach (var yarn in All)
                {
                    n += yarn.Seasonal ? 0 : 1;
                }

                return n;
            }
        }

        /// <summary>Вес мотка номер yarn в случайном розыгрыше: сезонные не разыгрываются.</summary>
        public static int RollWeight(int yarn, bool owned) => All[yarn].Seasonal ? 0 : RollWeight(All[yarn].Rarity, owned);

        // ---------- Золотая версия ----------

        /// <summary>
        /// Сколько повторов сверх максимального уровня нужно для золотой версии мотка. Повтор на максимуме сначала
        /// копит к золотой версии (1/5 … 5/5) и только у золотого мотка становится монетами.
        /// </summary>
        public const int GoldenAfter = 5;

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

        /// <summary>
        /// Вес мотка при розыгрыше по редкости. Мотков: 4 обычных, 4 необычных, 4 редких, 3 эпических, 1 легендарный;
        /// доли ступеней — около 49% / 30% / 16% / 4% / 1%, как в типичных играх с лестницей редкости.
        /// </summary>
        public static int Weight(Rarity rarity) => rarity switch
        {
            Rarity.Common => 25,
            Rarity.Uncommon => 15,
            Rarity.Rare => 8,
            Rarity.Epic => 3,
            _ => 2,
        };

        /// <summary>Доля ступени среди всех мотков при розыгрыше, в процентах (без поправки на найденные): для экрана сундука.</summary>
        public static float TierPercent(Rarity rarity)
        {
            var total = 0;
            var tier = 0;
            foreach (var yarn in All)
            {
                if (yarn.Seasonal)
                {
                    continue;
                }

                total += Weight(yarn.Rarity);
                tier += yarn.Rarity == rarity ? Weight(yarn.Rarity) : 0;
            }

            return total == 0 ? 0f : 100f * tier / total;
        }

        /// <summary>
        /// Вес мотка с учётом того, есть ли он уже: ненайденные в полтора раза чаще (×3 против ×2).
        /// Мягкая защита от повторов: корзинка собирается, но не за пару вечеров.
        /// </summary>
        public static int RollWeight(Rarity rarity, bool owned) => Weight(rarity) * (owned ? 2 : 3);

        // ---------- Шансы мотка за новую картинку ----------

        /// <summary>Первые картинки всегда с мотком: игрок знакомится с корзинкой.</summary>
        public const int SureFirstLevels = 3;

        /// <summary>Шанс мотка за новую картинку после пустой победы растёт: 35%, 50%, 65%, а четвёртая — наверняка.</summary>
        public const int DropBasePercent = 35;

        public const int DropStepPercent = 15;

        public const int DropPity = 4;

        /// <summary>Двенадцать случайных мотков без эпического или легендарного — следующий из них.</summary>
        public const int EpicPity = 12;

        /// <summary>Шанс мотка за новую картинку, в процентах. Сложный уровень и первые картинки — всегда.</summary>
        public static int DropChance(int levelId, bool hard, int dry)
        {
            if (hard || levelId <= SureFirstLevels || dry + 1 >= DropPity)
            {
                return 100;
            }

            return System.Math.Min(100, DropBasePercent + DropStepPercent * dry);
        }

        public static string RarityName(Rarity rarity) => Rarities.Name(rarity);

        /// <summary>Монеты за повторный моток, когда он уже максимального уровня.</summary>
        public static int DuplicateCoins(Rarity rarity) => rarity switch
        {
            Rarity.Common => 10,
            Rarity.Uncommon => 15,
            Rarity.Rare => 20,
            Rarity.Epic => 40,
            _ => 80,
        };

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

        /// <summary>
        /// Сила бонуса: редкость (обычная и необычная 1, редкая 2, эпическая 3, легендарная 4) плюс уровень сверх
        /// первого; золотая версия — ещё +1.
        /// </summary>
        public static int Power(int yarn, int level, bool golden = false)
        {
            var rarity = All[yarn].Rarity switch
            {
                Rarity.Rare => 2,
                Rarity.Epic => 3,
                Rarity.Legendary => 4,
                _ => 1,
            };
            return level <= 0 ? 0 : rarity + System.Math.Min(level, MaxLevel) - 1 + (golden ? 1 : 0);
        }

        /// <summary>+монет за новую картинку.</summary>
        public static int CoinsBonus(int power) => 2 * power;

        /// <summary>+монет за каждую пойманную золотую петлю.</summary>
        public static int GoldBonus(int power) => power;

        public static string PerkTitle(YarnPerk perk) => perk == YarnPerk.Coins ? L.T("Тёплая копилка") : L.T("Золотые руки");

        /// <summary>«монета», «монеты» или «монет» — по числу.</summary>
        public static string CoinsWord(int n)
        {
            return L.Plural(n, "монета", "монеты", "монет");
        }

        /// <summary>Что даёт бонус при этой силе — коротко: для полоски «В работе», тостов и строки победы.</summary>
        public static string PerkText(YarnPerk perk, int power)
        {
            var n = perk == YarnPerk.Coins ? CoinsBonus(power) : GoldBonus(power);
            return perk == YarnPerk.Coins
                ? L.F("+{0} {1} за каждую новую картинку", n, CoinsWord(n))
                : L.F("+{0} {1} сверху за каждую золотую петлю", n, CoinsWord(n));
        }

        /// <summary>То же, но совсем коротко: для строки «следующий уровень».</summary>
        public static string PerkShort(YarnPerk perk, int power)
        {
            var n = perk == YarnPerk.Coins ? CoinsBonus(power) : GoldBonus(power);
            return perk == YarnPerk.Coins ? L.F("+{0} {1} за картинку", n, CoinsWord(n)) : L.F("+{0} {1} за золотую петлю", n, CoinsWord(n));
        }

        /// <summary>Понятное предложение для карточки мотка: как именно работает бонус.</summary>
        public static string PerkSentence(YarnPerk perk, int power)
        {
            var n = perk == YarnPerk.Coins ? CoinsBonus(power) : GoldBonus(power);
            return perk == YarnPerk.Coins
                ? L.F("Каждая новая картинка приносит ещё +{0} {1}", n, CoinsWord(n))
                : L.F("Каждая золотая петля, пойманная в комбо, даёт ещё +{0} {1}", n, CoinsWord(n));
        }
    }
}
