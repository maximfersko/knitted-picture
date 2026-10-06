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

        // ---------- Статистика и достижения ----------

        public int TotalStars
        {
            get
            {
                var total = 0;
                foreach (var s in _data.stars)
                {
                    total += s;
                }

                return total;
            }
        }

        public int ThreeStarLevels
        {
            get
            {
                var count = 0;
                foreach (var s in _data.stars)
                {
                    if (s >= 3)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public int CompletedCollections
        {
            get
            {
                var count = 0;
                foreach (var collection in Collections.All)
                {
                    if (IsCollectionDone(collection))
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        /// <summary>Лучшее число тапов на уровне, 0 — ещё не проходил (или данных нет).</summary>
        public int BestTaps(int levelId) => levelId >= 1 && levelId <= _data.bestTaps.Count ? _data.bestTaps[levelId - 1] : 0;

        /// <summary>Проходил ли уровень без бустеров и спасений.</summary>
        public bool WasClean(int levelId) => _data.cleanWins.Contains(levelId);

        /// <summary>Запомнить рекорд уровня: лучшие тапы и чистое прохождение.</summary>
        public void RecordResult(int levelId, int taps, bool clean)
        {
            while (_data.bestTaps.Count < levelId)
            {
                _data.bestTaps.Add(0);
            }

            var best = _data.bestTaps[levelId - 1];
            if (taps > 0 && (best == 0 || taps < best))
            {
                _data.bestTaps[levelId - 1] = taps;
            }

            if (clean && !_data.cleanWins.Contains(levelId))
            {
                _data.cleanWins.Add(levelId);
            }
        }

        /// <summary>Записать итоги пройденного уровня в счётчики достижений.</summary>
        public void RecordLevel(int stitches, int rushes, int goldCaught, int bestCombo)
        {
            _data.totalStitches += stitches;
            _data.rushCount += rushes;
            _data.goldCaught += goldCaught;
            _data.bestCombo = System.Math.Max(_data.bestCombo, bestCombo);
        }

        public int AchievementValue(AchievementInfo a) => System.Math.Min(a.Value(this), a.Target);

        public bool IsAchievementDone(AchievementInfo a) => a.Value(this) >= a.Target;

        public bool IsAchievementClaimed(AchievementInfo a) => _data.achievementsClaimed.Contains(a.Id);

        public bool CanClaimAchievement(AchievementInfo a) => IsAchievementDone(a) && !IsAchievementClaimed(a);

        public Granted? ClaimAchievement(AchievementInfo a, System.Random rng)
        {
            if (!CanClaimAchievement(a))
            {
                return null;
            }

            _data.achievementsClaimed.Add(a.Id);
            return Grant(a.Reward, rng);
        }

        // ---------- Награды ----------

        /// <summary>Выдать награду. Случайный моток разыгрывается здесь; повтор превращается в монеты.</summary>
        public Granted Grant(Reward reward, System.Random rng)
        {
            _data.coins += reward.Coins;
            if (reward.Booster != null)
            {
                AddStock(reward.Booster.Value, reward.BoosterCount);
            }

            var yarn = reward.Yarn == DailyGift.RandomYarn ? RollYarn(rng) : reward.Yarn;
            var granted = new Reward(reward.Coins, reward.Booster, reward.BoosterCount, yarn);
            if (yarn < 0)
            {
                return new Granted(granted, false, 0);
            }

            var isNew = AddYarn(yarn);
            var duplicate = isNew ? 0 : YarnCatalog.DuplicateCoins(YarnCatalog.All[yarn].Rarity);
            _data.coins += duplicate;
            return new Granted(granted, isNew, duplicate);
        }

        // ---------- Корзинка пряжи ----------

        public int YarnCount(int yarn) => yarn >= 0 && yarn < _data.yarns.Count ? _data.yarns[yarn] : 0;

        public bool HasYarn(int yarn) => YarnCount(yarn) > 0;

        public int YarnKinds
        {
            get
            {
                var kinds = 0;
                for (var i = 0; i < YarnCatalog.Count; i++)
                {
                    if (HasYarn(i))
                    {
                        kinds++;
                    }
                }

                return kinds;
            }
        }

        /// <summary>Положить моток в корзинку. true — такого ещё не было.</summary>
        public bool AddYarn(int yarn)
        {
            while (_data.yarns.Count <= yarn)
            {
                _data.yarns.Add(0);
            }

            _data.yarns[yarn]++;
            return _data.yarns[yarn] == 1;
        }

        /// <summary>Случайный моток: редкие выпадают реже, недостающие — втрое чаще.</summary>
        public int RollYarn(System.Random rng)
        {
            var total = 0;
            var weights = new int[YarnCatalog.Count];
            for (var i = 0; i < weights.Length; i++)
            {
                weights[i] = YarnCatalog.Weight(YarnCatalog.All[i].Rarity) * (HasYarn(i) ? 1 : 3);
                total += weights[i];
            }

            var roll = rng.Next(total);
            for (var i = 0; i < weights.Length; i++)
            {
                roll -= weights[i];
                if (roll < 0)
                {
                    return i;
                }
            }

            return weights.Length - 1;
        }

        public bool IsSetDone(YarnSet set)
        {
            for (var i = set.First; i < set.First + set.Count; i++)
            {
                if (!HasYarn(i))
                {
                    return false;
                }
            }

            return true;
        }

        public bool CanClaimSet(YarnSet set) => IsSetDone(set) && !_data.yarnSetsClaimed.Contains(set.Id);

        public bool IsSetClaimed(YarnSet set) => _data.yarnSetsClaimed.Contains(set.Id);

        public Granted? ClaimSet(YarnSet set, System.Random rng)
        {
            if (!CanClaimSet(set))
            {
                return null;
            }

            _data.yarnSetsClaimed.Add(set.Id);
            return Grant(set.Reward, rng);
        }

        // ---------- Коллекции альбома ----------

        public static readonly Reward CollectionReward = new Reward(100, BoosterType.Slot, 1, DailyGift.RandomYarn);

        public bool IsCollectionDone(CollectionInfo collection)
        {
            for (var id = collection.FirstLevel; id <= collection.LastLevel; id++)
            {
                if (!IsCompleted(id))
                {
                    return false;
                }
            }

            return true;
        }

        public bool CanClaimCollection(int index) =>
            index >= 0 && index < Collections.All.Length && IsCollectionDone(Collections.All[index]) && !_data.collectionsClaimed.Contains(index);

        public bool IsCollectionClaimed(int index) => _data.collectionsClaimed.Contains(index);

        public Granted? ClaimCollection(int index, System.Random rng)
        {
            if (!CanClaimCollection(index))
            {
                return null;
            }

            _data.collectionsClaimed.Add(index);
            return Grant(CollectionReward, rng);
        }

        // ---------- Звёздный сундук ----------

        /// <summary>Сколько звёзд нужно на сундук.</summary>
        public const int ChestEvery = 12;

        /// <summary>Звёзды к следующему сундуку (могут быть больше ChestEvery, если сундук не открыт).</summary>
        public int ChestProgress => System.Math.Max(0, TotalStars - _data.chestsOpened * ChestEvery);

        public bool CanOpenChest => ChestProgress >= ChestEvery;

        public Granted? OpenChest(System.Random rng)
        {
            if (!CanOpenChest)
            {
                return null;
            }

            _data.chestsOpened++;
            var boosters = new[] { BoosterType.Slot, BoosterType.Scissors, BoosterType.Undo };
            var reward = new Reward(60 + 10 * rng.Next(5), boosters[rng.Next(boosters.Length)], 1, DailyGift.RandomYarn);
            return Grant(reward, rng);
        }

        // ---------- Ежедневный подарок ----------

        public bool CanClaimDaily(int today) => _data.dailyLastDay != today;

        /// <summary>Какой день недели подарков будет следующим (0..6): пропуск дня начинает заново.</summary>
        public int DailyIndex(int today)
        {
            var continues = _data.dailyLastDay == today - 1 || _data.dailyLastDay == today;
            var streak = continues ? _data.dailyStreak : 0;
            if (_data.dailyLastDay == today)
            {
                streak--;
            }

            return ((streak % DailyGift.Days.Length) + DailyGift.Days.Length) % DailyGift.Days.Length;
        }

        public Granted? ClaimDaily(int today, System.Random rng)
        {
            if (!CanClaimDaily(today))
            {
                return null;
            }

            var index = DailyIndex(today);
            _data.dailyStreak = _data.dailyLastDay == today - 1 ? _data.dailyStreak + 1 : 1;
            _data.dailyBest = System.Math.Max(_data.dailyBest, _data.dailyStreak);
            _data.dailyLastDay = today;
            return Grant(DailyGift.Days[index], rng);
        }

        /// <summary>Сколько наград ждёт в коллекциях: достижения, наборы пряжи, коллекции альбома.</summary>
        public int PendingClaims
        {
            get
            {
                var count = 0;
                foreach (var a in Achievements.All)
                {
                    if (CanClaimAchievement(a))
                    {
                        count++;
                    }
                }

                foreach (var set in YarnCatalog.Sets)
                {
                    if (CanClaimSet(set))
                    {
                        count++;
                    }
                }

                for (var i = 0; i < Collections.All.Length; i++)
                {
                    if (CanClaimCollection(i))
                    {
                        count++;
                    }
                }

                return count;
            }
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

            // За новую картинку — моток в корзинку.
            var yarn = new Granted(default, false, 0);
            if (firstTime)
            {
                yarn = Grant(new Reward(0, null, 0, DailyGift.RandomYarn), new System.Random(levelId * 97 + _data.totalWins));
            }

            var reward = new WinReward(
                firstTime ? Economy.Reward(hard) : Economy.ReplayReward,
                comboCoins,
                Economy.StreakBonus(_data.streak),
                _data.streak,
                stars,
                stars > previousStars && !firstTime,
                firstTime ? yarn.Reward.Yarn : -1,
                yarn.NewYarn,
                yarn.DuplicateCoins);
            _data.coins += reward.BaseCoins + reward.ComboCoins + reward.StreakCoins;
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
        public WinReward(int baseCoins, int comboCoins, int streakCoins, int streak, int stars, bool newRecord,
            int yarn = -1, bool newYarn = false, int yarnCoins = 0)
        {
            Yarn = yarn;
            NewYarn = newYarn;
            YarnCoins = yarnCoins;
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

        /// <summary>Моток за новую картинку, -1 — без мотка.</summary>
        public int Yarn { get; }

        public bool NewYarn { get; }

        /// <summary>Монеты за повторный моток (уже начислены вместе с ним).</summary>
        public int YarnCoins { get; }

        public int Total => BaseCoins + ComboCoins + StreakCoins + YarnCoins;
    }
}
