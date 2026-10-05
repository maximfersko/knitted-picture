#nullable enable

namespace KnittedPicture.Core
{
    public enum Payment
    {
        /// <summary>Не хватило ни бустеров, ни монет.</summary>
        NotEnough,

        /// <summary>Списан бустер из запаса.</summary>
        FromStock,

        /// <summary>Списаны монеты.</summary>
        WithCoins,
    }

    /// <summary>Правила прогресса и кошелька поверх сохранения. Без Unity — проверяется тестами.</summary>
    public sealed class ProgressService
    {
        private readonly PlayerProgress _data;

        public ProgressService(PlayerProgress data, int levelCount)
        {
            _data = data;
            LevelCount = levelCount;
            if (_data.nextLevel < 1)
            {
                _data.nextLevel = 1;
            }
        }

        public PlayerProgress Data => _data;

        public int LevelCount { get; }

        public int Coins => _data.coins;

        /// <summary>Уровень, который предлагает кнопка «Играть».</summary>
        public int CurrentLevel => _data.nextLevel > LevelCount ? LevelCount : _data.nextLevel;

        /// <summary>Пройдены все уровни, которые есть в игре.</summary>
        public bool AllDone => _data.nextLevel > LevelCount;

        public bool SoundOn
        {
            get => _data.soundOn;
            set => _data.soundOn = value;
        }

        public bool IsCompleted(int levelId) => _data.completed.Contains(levelId);

        public bool IsAvailable(int levelId) => levelId >= 1 && levelId <= LevelCount && levelId <= _data.nextLevel;

        public bool IsUnlocked(BoosterType type, int levelId) => levelId >= Economy.UnlockLevel(type);

        public int Stock(BoosterType type)
        {
            switch (type)
            {
                case BoosterType.Slot: return _data.slotBoosters;
                case BoosterType.Scissors: return _data.scissorsBoosters;
                case BoosterType.Undo: return _data.undoBoosters;
                case BoosterType.Time: return _data.timeBoosters;
                default: return 0;
            }
        }

        public void AddStock(BoosterType type, int count)
        {
            switch (type)
            {
                case BoosterType.Slot: _data.slotBoosters += count; break;
                case BoosterType.Scissors: _data.scissorsBoosters += count; break;
                case BoosterType.Undo: _data.undoBoosters += count; break;
                case BoosterType.Time: _data.timeBoosters += count; break;
            }
        }

        /// <summary>Хватит ли на бустер: из запаса или за монеты.</summary>
        public bool CanAfford(BoosterType type) => Stock(type) > 0 || _data.coins >= Economy.Price(type);

        /// <summary>Списать бустер: сначала из запаса, потом монетами.</summary>
        public Payment Pay(BoosterType type)
        {
            if (Stock(type) > 0)
            {
                AddStock(type, -1);
                return Payment.FromStock;
            }

            var price = Economy.Price(type);
            if (_data.coins >= price)
            {
                _data.coins -= price;
                return Payment.WithCoins;
            }

            return Payment.NotEnough;
        }

        public void AddCoins(int amount)
        {
            _data.coins += amount;
        }

        /// <summary>
        /// Бустер, который открывается на этом уровне и ещё не был показан.
        /// Отмечает показ и дарит бесплатные штуки. null — показывать нечего.
        /// </summary>
        public BoosterType? TakeBoosterIntro(int levelId)
        {
            foreach (BoosterType type in System.Enum.GetValues(typeof(BoosterType)))
            {
                if (Economy.UnlockLevel(type) == levelId && !_data.introsShown.Contains((int)type))
                {
                    _data.introsShown.Add((int)type);
                    AddStock(type, Economy.FreeOnUnlock);
                    return type;
                }
            }

            return null;
        }

        public int Streak => _data.streak;

        /// <summary>Лучший результат уровня в звёздах, 0 — не пройден.</summary>
        public int StarsOf(int levelId) => levelId >= 1 && levelId <= _data.stars.Count ? _data.stars[levelId - 1] : 0;

        /// <summary>Перезапуск уровня обрывает серию побед.</summary>
        public void BreakStreak()
        {
            _data.streak = 0;
        }

        /// <summary>Засчитать победу и начислить награду.</summary>
        public WinReward CompleteLevel(int levelId, bool hard, int stars = 3, int comboCoins = 0)
        {
            var firstTime = !_data.completed.Contains(levelId);
            if (firstTime)
            {
                _data.completed.Add(levelId);
            }

            if (levelId + 1 > _data.nextLevel)
            {
                _data.nextLevel = levelId + 1;
            }

            while (_data.stars.Count < levelId)
            {
                _data.stars.Add(0);
            }

            var previousStars = _data.stars[levelId - 1];
            if (stars > previousStars)
            {
                _data.stars[levelId - 1] = stars;
            }

            _data.streak++;
            var reward = new WinReward(
                firstTime ? Economy.Reward(hard) : Economy.ReplayReward,
                comboCoins,
                Economy.StreakBonus(_data.streak),
                _data.streak,
                stars,
                stars > previousStars && !firstTime);
            _data.coins += reward.Total;
            _data.totalWins++;
            _data.winsSinceInterstitial++;
            return reward;
        }

        /// <summary>Пора ли показать межуровневую рекламу после этой победы. Сбрасывает счётчик, если да.</summary>
        public bool TakeInterstitialSlot(int levelId)
        {
            if (levelId < Economy.InterstitialFromLevel || _data.winsSinceInterstitial < Economy.InterstitialEveryWins)
            {
                return false;
            }

            _data.winsSinceInterstitial = 0;
            return true;
        }
    }

    /// <summary>Из чего сложилась награда за победу — экран победы показывает строки.</summary>
    public readonly struct WinReward
    {
        public WinReward(int baseCoins, int comboCoins, int streakCoins, int streak, int stars, bool newRecord)
        {
            BaseCoins = baseCoins;
            ComboCoins = comboCoins;
            StreakCoins = streakCoins;
            Streak = streak;
            Stars = stars;
            NewRecord = newRecord;
        }

        public int BaseCoins { get; }

        public int ComboCoins { get; }

        public int StreakCoins { get; }

        public int Streak { get; }

        public int Stars { get; }

        /// <summary>Перепрошёл уровень лучше прежнего.</summary>
        public bool NewRecord { get; }

        public int Total => BaseCoins + ComboCoins + StreakCoins;
    }
}
