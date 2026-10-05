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
                case BoosterType.Slot: return 100;
                case BoosterType.Scissors: return 120;
                case BoosterType.Undo: return 60;
                case BoosterType.Time: return 80;
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
                case BoosterType.Time: return 10;
                case BoosterType.Undo: return 12;
                default: return 1;
            }
        }

        public static int Reward(bool hard) => hard ? HardWinReward : WinReward;

        /// <summary>Комбо: начиная с этой длины серии каждый идеальный клубок даёт монету.</summary>
        public const int ComboCoinsFrom = 3;

        /// <summary>Серия побед подряд без перезапуска: +5 монет за каждую победу в серии, максимум +15.</summary>
        public const int StreakStep = 5;
        public const int StreakMax = 15;

        public static int StreakBonus(int streak) => System.Math.Min(System.Math.Max(0, streak - 1) * StreakStep, StreakMax);

        /// <summary>Звёзды: 3 — чисто, 2 — с бустерами, 1 — со спасением после затора или моли.</summary>
        public static int Stars(int boostersUsed, int rescues) => rescues > 0 ? 1 : boostersUsed > 0 ? 2 : 3;
    }
}
