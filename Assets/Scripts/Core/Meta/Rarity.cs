#nullable enable

namespace KnittedPicture.Core
{
    /// <summary>
    /// Редкость — одна лестница на всю игру: пряжа, рамки альбома, достижения, бустеры. Пять ступеней
    /// и цвета как в большинстве игр (серый, зелёный, синий, фиолетовый, золото), чтобы игрок узнавал их
    /// без объяснений. См. docs/GDD.md, «Редкость».
    /// </summary>
    public enum Rarity
    {
        Common,
        Uncommon,
        Rare,
        Epic,
        Legendary,
    }

    /// <summary>Род слова, к которому относится редкость: «редкая пряжа», «редкий бустер», «редкое достижение».</summary>
    public enum Gender
    {
        Feminine,
        Masculine,
        Neuter,
    }

    public static class Rarities
    {
        public static readonly Rarity[] All = { Rarity.Common, Rarity.Uncommon, Rarity.Rare, Rarity.Epic, Rarity.Legendary };

        /// <summary>Название ступени в нужном роде (в английском — одно слово на все роды).</summary>
        public static string Name(Rarity rarity, Gender gender = Gender.Feminine)
        {
            switch (rarity)
            {
                case Rarity.Uncommon:
                    return gender == Gender.Feminine ? L.T("необычная") : gender == Gender.Masculine ? L.T("необычный") : L.T("необычное");
                case Rarity.Rare:
                    return gender == Gender.Feminine ? L.T("редкая") : gender == Gender.Masculine ? L.T("редкий") : L.T("редкое");
                case Rarity.Epic:
                    return gender == Gender.Feminine ? L.T("эпическая") : gender == Gender.Masculine ? L.T("эпический") : L.T("эпическое");
                case Rarity.Legendary:
                    return gender == Gender.Feminine ? L.T("легендарная") : gender == Gender.Masculine ? L.T("легендарный") : L.T("легендарное");
                default:
                    return gender == Gender.Feminine ? L.T("обычная") : gender == Gender.Masculine ? L.T("обычный") : L.T("обычное");
            }
        }

        /// <summary>Цвет ступени (основной, для текста и обводки): серый, зелёный, синий, фиолетовый, золото.</summary>
        public static string Hex(Rarity rarity) => rarity switch
        {
            Rarity.Uncommon => "#2E9E4F",
            Rarity.Rare => "#2F6FDE",
            Rarity.Epic => "#8E44D6",
            Rarity.Legendary => "#D98A00",
            _ => "#7D8590",
        };

        /// <summary>Светлый фон ступени (плашки, подложки карточек).</summary>
        public static string SoftHex(Rarity rarity) => rarity switch
        {
            Rarity.Uncommon => "#E2F4E6",
            Rarity.Rare => "#E1EAF8",
            Rarity.Epic => "#F0E4FA",
            Rarity.Legendary => "#FFF0CC",
            _ => "#EEF0F3",
        };
    }
}
