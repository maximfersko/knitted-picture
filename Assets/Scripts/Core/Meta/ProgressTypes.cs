#nullable enable

using System.Collections.Generic;
using System.Linq;

namespace KnittedPicture.Core
{
    /// <summary>Механики, с которыми знакомим по одной, когда они открываются.</summary>
    public enum Feature
    {
        Season,
        Missions,
        Pattern,
        Exhibition,
    }

    /// <summary>Забранная ступень сезона: сама ступень, что выдано и рамка (-1 — без рамки).</summary>
    public readonly struct SeasonClaim
    {
        public SeasonClaim(SeasonStep step, Granted granted, int frame)
        {
            Step = step;
            Granted = granted;
            Frame = frame;
        }

        public SeasonStep Step { get; }

        public Granted Granted { get; }

        public int Frame { get; }
    }

    /// <summary>Итог выставки: очки, медаль и награда (null — без медали).</summary>
    public readonly struct ExhibitionResult
    {
        public ExhibitionResult(int score, int medal, Granted? granted)
        {
            Score = score;
            Medal = medal;
            Granted = granted;
        }

        public int Score { get; }

        public int Medal { get; }

        public Granted? Granted { get; }
    }

    /// <summary>Из чего сложилась награда за победу — экран победы показывает строки.</summary>
    public readonly struct WinReward
    {
        public WinReward(int baseCoins, int comboCoins, int streak, int stars, bool newRecord,
            int yarn = -1, bool newYarn = false, int yarnCoins = 0, int perkCoins = 0, int nextYarnChance = 0, int goldStep = 0)
        {
            GoldStep = goldStep;
            NextYarnChance = nextYarnChance;
            PerkCoins = perkCoins;
            Yarn = yarn;
            NewYarn = newYarn;
            YarnCoins = yarnCoins;
            BaseCoins = baseCoins;
            ComboCoins = comboCoins;
            Streak = streak;
            Stars = stars;
            NewRecord = newRecord;
        }

        public int BaseCoins { get; }

        public int ComboCoins { get; }


        public int Streak { get; }

        public int Stars { get; }

        /// <summary>Перепрошёл уровень лучше прежнего.</summary>
        public bool NewRecord { get; }

        /// <summary>Моток за новую картинку, -1 — без мотка.</summary>
        public int Yarn { get; }

        public bool NewYarn { get; }

        /// <summary>Моток в этот раз не выпал: шанс на следующей новой картинке, в процентах (0 — неприменимо).</summary>
        public int NextYarnChance { get; }

        /// <summary>Монеты за повторный моток (уже начислены вместе с ним).</summary>
        public int YarnCoins { get; }

        /// <summary>Бонус пряжи «в работе» (Тёплая копилка) за новую картинку.</summary>
        public int PerkCoins { get; }

        /// <summary>Повтор на максимальном уровне: шаг к золотой версии (0 — не было).</summary>
        public int GoldStep { get; }

        /// <summary>Повтор мотка поднял его уровень (монет за повтор нет).</summary>
        public bool YarnLevelUp => Yarn >= 0 && !NewYarn && YarnCoins == 0 && GoldStep == 0;

        public int Total => BaseCoins + ComboCoins + YarnCoins + PerkCoins;
    }
}
