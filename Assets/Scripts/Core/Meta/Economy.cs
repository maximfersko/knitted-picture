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
