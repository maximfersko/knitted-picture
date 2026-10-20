#nullable enable

namespace KnittedPicture.Core
{
    public enum BoosterType
    {
        /// <summary>+1 слот на станке.</summary>
        Slot,

        /// <summary>Достать любой клубок с поля.</summary>
        Scissors,

        /// <summary>Отменить последний ход.</summary>
        Undo,

        /// <summary>+20 секунд, когда моль добралась до картинки.</summary>
        Time,
    }

    /// <summary>Все числа экономики в одном месте, чтобы их было легко крутить после тестов.</summary>
    public static class Economy
    {
        public const int StartCoins = 50;
        public const int WinReward = 20;
        public const int HardWinReward = 40;

        /// <summary>Повторное прохождение даёт мало: альбом не превращается в ферму монет.</summary>
        public const int ReplayReward = 5;

        public const int TimeBonusSeconds = 20;

        /// <summary>Сколько бустеров дарим, когда бустер открывается.</summary>
        public const int FreeOnUnlock = 1;

        /// <summary>Межуровневая реклама: не раньше этого уровня и не чаще раза в столько побед.</summary>
        public const int InterstitialFromLevel = 10;
        public const int InterstitialEveryWins = 2;

        public static int Price(BoosterType type)
        {
            switch (type)
            {
                case BoosterType.Slot: return 300;
                case BoosterType.Scissors: return 360;
                case BoosterType.Undo: return 180;
                case BoosterType.Time: return 240;
                default: return 0;
            }
        }

        /// <summary>С какого уровня бустер доступен.</summary>
        /// <summary>
        /// Редкость бустера по силе: отмена хода — обычный, +1 место и +20 секунд — необычные, ножницы
        /// (достать любой клубок) — редкий. Видна на карточках выбора в сундуке и в описании.
        /// </summary>
        public static Rarity RarityOf(BoosterType type) => type switch
        {
            BoosterType.Undo => Rarity.Common,
            BoosterType.Scissors => Rarity.Rare,
            _ => Rarity.Uncommon,
        };

        public static int UnlockLevel(BoosterType type)
        {
            switch (type)
            {
                case BoosterType.Slot: return 5;
                case BoosterType.Scissors: return 8;
                case BoosterType.Time: return 17;
                case BoosterType.Undo: return 12;
                default: return 1;
            }
        }

        public static int Reward(bool hard) => hard ? HardWinReward : WinReward;

        /// <summary>
        /// Спасение после затора или моли дорожает внутри одного уровня, как продолжение в Royal Match и King:
        /// первое — по цене бустера, второе — вдвое, третье и дальше — втрое. Проигрыш снова чего-то стоит,
        /// а одно спасение на трудном месте остаётся по карману. Бустер из запаса — без доплаты.
        /// </summary>
        public static int RescuePrice(BoosterType type, int rescuesBefore) =>
            Price(type) * System.Math.Min(1 + System.Math.Max(0, rescuesBefore), MaxRescueMultiplier);

        public const int MaxRescueMultiplier = 3;

        /// <summary>Спасение за видео — одно на уровень: видео помогает, но не заменяет игру.</summary>
        public const int VideoRescuesPerLevel = 1;

        /// <summary>
        /// С какой попытки нового уровня «застрявшему» один раз дарят помощь: бесплатные ножницы (не снимают звезду)
        /// и подсказку хода на эту попытку.
        /// </summary>
        public const int StuckHelpFromAttempt = 4;

        /// <summary>«Удвоить награду за видео» — не больше стольких раз в день: иначе видео печатают монеты.</summary>
        public const int DoublesPerDay = 5;

        /// <summary>Бустер за видео в окне «Не хватает монет» — не больше стольких раз в день.</summary>
        public const int VideoBoostersPerDay = 3;

        /// <summary>Золотая петля, связанная во время комбо.</summary>
        public const int GoldCoins = 3;


        /// <summary>
        /// Звёзды: 3 — без бустеров, 2 — с бустерами, 1 — со спасением после затора или моли.
        /// Цели по тапам нет: на этом станке почти каждый тап — нужный клубок, цель не отличала хорошую игру от плохой.
        /// </summary>
        public static int Stars(int boostersUsed, int rescues)
        {
            if (rescues > 0)
            {
                return 1;
            }

            return boostersUsed > 0 ? 2 : 3;
        }
    }
}
