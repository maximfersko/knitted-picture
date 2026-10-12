#nullable enable

using System.Collections.Generic;
using System.Linq;

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

        /// <summary>
        /// Доступен ли бустер в уровне levelId: уровень не раньше открытия бустера,
        /// или бустер уже открыт дальше по прогрессу (тогда он есть и при перепрохождении старых уровней).
        /// </summary>
        public bool IsBoosterOpen(BoosterType type, int levelId) =>
            IsUnlocked(type, levelId) || _data.introsShown.Contains((int)type);

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

        /// <summary>
        /// «Тёплая серия»: победы подряд на новых уровнях дают бесплатный старт следующего нового уровня.
        /// 1 победа — лишний слот на станке, 2 — ещё и бесплатные ножницы, 3 и больше — ещё и бесплатная отмена.
        /// Только для открытых бустеров; на перепрохождении старых уровней серия не действует.
        /// </summary>
        public WarmStart WarmStartFor(int levelId)
        {
            if (IsCompleted(levelId) || _data.streak <= 0)
            {
                return default;
            }

            var tier = System.Math.Min(_data.streak, WarmStart.MaxTier);
            return new WarmStart(
                tier >= 1 && IsBoosterOpen(BoosterType.Slot, levelId),
                tier >= 2 && IsBoosterOpen(BoosterType.Scissors, levelId),
                tier >= 3 && IsBoosterOpen(BoosterType.Undo, levelId));
        }

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

            // Повтор сначала поднимает уровень мотка, и только на максимальном уровне становится монетами.
            var maxedBefore = YarnLevel(yarn) >= YarnCatalog.MaxLevel;
            var isNew = AddYarn(yarn);
            var duplicate = isNew || !maxedBefore ? 0 : YarnCatalog.DuplicateCoins(YarnCatalog.All[yarn].Rarity);
            _data.coins += duplicate;
            return new Granted(granted, isNew, duplicate);
        }

        // ---------- Корзинка пряжи ----------

        public int YarnCount(int yarn) => yarn >= 0 && yarn < _data.yarns.Count ? _data.yarns[yarn] : 0;

        public bool HasYarn(int yarn) => YarnCount(yarn) > 0;

        /// <summary>Уровень мотка 0..3: сколько штук собрано, но не больше максимума.</summary>
        public int YarnLevel(int yarn) => System.Math.Min(YarnCount(yarn), YarnCatalog.MaxLevel);

        /// <summary>Моток «в работе», -1 — нет.</summary>
        public int ActiveYarn => HasYarn(_data.activeYarn) ? _data.activeYarn : -1;

        /// <summary>Взять моток в работу: его бонус будет действовать в уровнях.</summary>
        public bool SetActiveYarn(int yarn)
        {
            if (!HasYarn(yarn))
            {
                return false;
            }

            _data.activeYarn = yarn;
            return true;
        }

        /// <summary>Бонусы пряжи уже открыты: игрок дошёл до <see cref="YarnCatalog.PerksFromLevel"/>.</summary>
        public bool PerksOpen => _data.nextLevel >= YarnCatalog.PerksFromLevel;

        /// <summary>Сила бонуса perk от мотка в работе; 0 — бонусы ещё закрыты, у мотка другой бонус или мотка нет.</summary>
        public int PerkPower(YarnPerk perk)
        {
            var yarn = ActiveYarn;
            return PerksOpen && yarn >= 0 && YarnCatalog.PerkOf(yarn) == perk ? YarnCatalog.Power(yarn, YarnLevel(yarn)) : 0;
        }

        /// <summary>
        /// Знакомство с натяжением полотна — один раз, в тот момент, когда оно впервые остановило столбик.
        /// </summary>
        public bool TakeTensionIntro()
        {
            const int introId = 101;
            if (_data.introsShown.Contains(introId))
            {
                return false;
            }

            _data.introsShown.Add(introId);
            return true;
        }

        /// <summary>Знакомство с бонусами пряжи — один раз, на уровне, где они открываются.</summary>
        public bool TakePerksIntro(int levelId)
        {
            const int introId = 100;
            if (levelId != YarnCatalog.PerksFromLevel || _data.introsShown.Contains(introId))
            {
                return false;
            }

            _data.introsShown.Add(introId);
            return true;
        }

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

            // Первый моток сразу берётся в работу: бонус начинает действовать без лишних шагов.
            if (!HasYarn(_data.activeYarn))
            {
                _data.activeYarn = yarn;
            }
            return _data.yarns[yarn] == 1;
        }

        /// <summary>
        /// Случайный моток: редкие выпадают реже, ненайденные в полтора раза чаще. Если двенадцать мотков
        /// подряд не было эпического, следующий — эпический (защита от невезения).
        /// </summary>
        public int RollYarn(System.Random rng)
        {
            var epicOnly = _data.yarnsSinceEpic >= YarnCatalog.EpicPity;
            var total = 0;
            var weights = new int[YarnCatalog.Count];
            for (var i = 0; i < weights.Length; i++)
            {
                var rarity = YarnCatalog.All[i].Rarity;
                weights[i] = epicOnly && rarity != Rarity.Epic ? 0 : YarnCatalog.RollWeight(rarity, HasYarn(i));
                total += weights[i];
            }

            var pick = PickWeighted(weights, total, rng);
            _data.yarnsSinceEpic = YarnCatalog.All[pick].Rarity == Rarity.Epic ? 0 : _data.yarnsSinceEpic + 1;
            return pick;
        }

        private static int PickWeighted(int[] weights, int total, System.Random rng)
        {

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

        /// <summary>Сколько сундуков уже открыто: номер следующего.</summary>
        public int ChestsOpened => _data.chestsOpened;

        /// <summary>Следующий сундук — большой (каждый Chest.Cycle-й).</summary>
        public bool NextChestBig => Chest.IsBig(_data.chestsOpened);

        /// <summary>Что лежит в сундуке, который откроется следующим. Не меняет сохранение.</summary>
        public ChestContents PeekChest() => Chest.Roll(_data.chestsOpened, CurrentLevel, HasYarn);

        /// <summary>
        /// Открыть сундук: монеты, выбранный бустер и выбранный моток (повтор — монетами).
        /// null — сундук ещё не накоплен или выбор вне списка.
        /// </summary>
        public Granted? ClaimChest(int boosterChoice, int yarnChoice)
        {
            if (!CanOpenChest)
            {
                return null;
            }

            var contents = PeekChest();
            if (boosterChoice < 0 || boosterChoice >= contents.Boosters.Length || yarnChoice < 0 || yarnChoice >= contents.Yarns.Length)
            {
                return null;
            }

            _data.chestsOpened++;
            var reward = new Reward(contents.Coins, contents.Boosters[boosterChoice], contents.BoosterCount, contents.Yarns[yarnChoice]);
            return Grant(reward, new System.Random(0));
        }

        // ---------- Ежедневный подарок ----------

        /// <summary>
        /// Подарок можно забрать раз в новый день. Перевод часов на день назад нового подарка не даёт;
        /// если часы когда-то убежали далеко вперёд, а потом вернулись, подарок не блокируется навсегда.
        /// </summary>
        public bool CanClaimDaily(int today) => today > _data.dailyLastDay || _data.dailyLastDay - today > 1;

        /// <summary>
        /// Какой день недели подарков сегодня (0..6). Пропуск дня — пауза, а не сброс: вернувшийся
        /// игрок продолжает с того же места недели, его не наказывают за перерыв.
        /// </summary>
        public int DailyIndex(int today)
        {
            var claims = _data.dailyLastDay == today ? _data.dailyClaims - 1 : _data.dailyClaims;
            return ((claims % DailyGift.Days.Length) + DailyGift.Days.Length) % DailyGift.Days.Length;
        }

        public Granted? ClaimDaily(int today, System.Random rng)
        {
            if (!CanClaimDaily(today))
            {
                return null;
            }

            var index = DailyIndex(today);
            _data.dailyClaims++;

            // Дни подряд считаем отдельно — для достижения «Каждый день».
            _data.dailyStreak = _data.dailyLastDay == today - 1 ? _data.dailyStreak + 1 : 1;
            _data.dailyBest = System.Math.Max(_data.dailyBest, _data.dailyStreak);
            _data.dailyLastDay = today;
            return Grant(DailyGift.Days[index], rng);
        }

        // ---------- Узор дня ----------

        public bool PatternOpen => _data.nextLevel >= DailyPattern.FromLevel;

        public bool PatternDone(int today) => _data.patternDay == today;

        /// <summary>Дни подряд: сегодняшний или вчерашний узор продолжает серию, пропуск обрывает.</summary>
        public int PatternStreak(int today) =>
            _data.patternDay == today || _data.patternDay == today - 1 ? _data.patternStreak : 0;

        /// <summary>Узор дня связан: монеты, а каждый 7-й день подряд — ещё и моток. Повторно в тот же день — ничего.</summary>
        public Granted? CompletePattern(int today, System.Random rng)
        {
            if (!PatternOpen || PatternDone(today))
            {
                return null;
            }

            _data.patternStreak = _data.patternDay == today - 1 ? _data.patternStreak + 1 : 1;
            _data.patternBest = System.Math.Max(_data.patternBest, _data.patternStreak);
            _data.patternDay = today;
            _data.patternsDone++;
            var yarn = _data.patternStreak % DailyPattern.YarnEvery == 0 ? DailyGift.RandomYarn : -1;
            return Grant(new Reward(DailyPattern.RewardCoins, null, 0, yarn), rng);
        }

        // ---------- Задания дня ----------

        public bool MissionsOpen => _data.nextLevel >= DailyMissions.FromLevel;

        /// <summary>Задания на сегодня (новый день — новый набор, прогресс с нуля).</summary>
        public List<Mission> Missions(int today)
        {
            EnsureMissions(today);
            var list = new List<Mission>();
            for (var i = 0; i < _data.missionKinds.Count; i++)
            {
                list.Add(new Mission((MissionKind)_data.missionKinds[i], _data.missionTargets[i], _data.missionValues[i]));
            }

            return list;
        }

        public int MissionsDone(int today) => Missions(today).Count(m => m.Done);

        public bool CanClaimMissions(int today) =>
            MissionsOpen && MissionsDone(today) == DailyMissions.Count && !_data.missionsClaimed;

        public bool MissionsClaimed(int today)
        {
            EnsureMissions(today);
            return _data.missionsClaimed;
        }

        public Reward MissionsReward(int today) => DailyMissions.RewardFor(today, _data.nextLevel);

        public Granted? ClaimMissions(int today, System.Random rng)
        {
            if (!CanClaimMissions(today))
            {
                return null;
            }

            _data.missionsClaimed = true;
            return Grant(MissionsReward(today), rng);
        }

        /// <summary>Засчитать пройденный уровень в задания дня. Возвращает, сколько заданий стало выполнено именно сейчас.</summary>
        public int RecordMissions(int today, int stars, bool clean, int goldCaught, int rushes, int bestCombo, int stitches)
        {
            if (!MissionsOpen)
            {
                return 0;
            }

            EnsureMissions(today);
            var newlyDone = 0;
            for (var i = 0; i < _data.missionKinds.Count; i++)
            {
                var before = _data.missionValues[i];
                var target = _data.missionTargets[i];
                var value = before;
                switch ((MissionKind)_data.missionKinds[i])
                {
                    case MissionKind.Win: value += 1; break;
                    case MissionKind.ThreeStars: value += stars >= 3 ? 1 : 0; break;
                    case MissionKind.Gold: value += goldCaught; break;
                    case MissionKind.Rush: value += rushes; break;
                    case MissionKind.Combo: value = System.Math.Max(value, bestCombo); break;
                    case MissionKind.Stitches: value += stitches; break;
                    case MissionKind.Clean: value += clean ? 1 : 0; break;
                }

                _data.missionValues[i] = System.Math.Min(value, target);
                if (before < target && value >= target)
                {
                    newlyDone++;
                }
            }

            return newlyDone;
        }

        private void EnsureMissions(int today)
        {
            if (_data.missionDay == today && _data.missionKinds.Count == DailyMissions.Count)
            {
                return;
            }

            _data.missionDay = today;
            _data.missionKinds.Clear();
            _data.missionTargets.Clear();
            _data.missionValues.Clear();
            foreach (var (kind, target) in DailyMissions.Roll(today, _data.nextLevel))
            {
                _data.missionKinds.Add((int)kind);
                _data.missionTargets.Add(target);
                _data.missionValues.Add(0);
            }

            _data.missionsClaimed = false;
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

            // Серия растёт и приносит монеты только за новые уровни: перепрохождение старых
            // не должно превращаться в ферму (повтор и так даёт мало).
            if (firstTime)
            {
                _data.streak++;
            }

            // За новую картинку — моток с шансом: первые картинки и сложные уровни всегда, остальные
            // 35%, а после каждой пустой победы шанс растёт; четвёртая подряд — наверняка.
            var yarn = new Granted(default, false, 0);
            var dropped = false;
            var nextChance = 0;
            if (firstTime)
            {
                var rng = new System.Random(levelId * 97 + _data.totalWins);
                if (rng.Next(100) < YarnCatalog.DropChance(levelId, hard, _data.yarnDry))
                {
                    yarn = Grant(new Reward(0, null, 0, DailyGift.RandomYarn), rng);
                    dropped = true;
                    _data.yarnDry = 0;
                }
                else
                {
                    _data.yarnDry++;
                    nextChance = YarnCatalog.DropChance(levelId + 1, false, _data.yarnDry);
                }
            }

            var reward = new WinReward(
                firstTime ? Economy.Reward(hard) : Economy.ReplayReward,
                comboCoins,
                _data.streak,
                stars,
                stars > previousStars && !firstTime,
                dropped ? yarn.Reward.Yarn : -1,
                yarn.NewYarn,
                yarn.DuplicateCoins,
                firstTime ? YarnCatalog.CoinsBonus(PerkPower(YarnPerk.Coins)) : 0,
                nextChance);
            _data.coins += reward.BaseCoins + reward.ComboCoins + reward.PerkCoins;
            _data.totalWins++;
            _data.winsSinceInterstitial++;
            return reward;
        }

        /// <summary>Пора ли показать межуровневую рекламу после этой победы. Сбрасывает счётчик, если да.</summary>
        /// <summary>Начата новая попытка уровня; возвращает её номер (1 — первая).</summary>
        public int NextAttempt(int levelId)
        {
            if (levelId < 1)
            {
                return 1;
            }

            while (_data.attempts.Count < levelId)
            {
                _data.attempts.Add(0);
            }

            return ++_data.attempts[levelId - 1];
        }

        public bool TakeInterstitialSlot(int levelId)
        {
            if (_data.adsRemoved || levelId < Economy.InterstitialFromLevel || _data.winsSinceInterstitial < Economy.InterstitialEveryWins)
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
        public WinReward(int baseCoins, int comboCoins, int streak, int stars, bool newRecord,
            int yarn = -1, bool newYarn = false, int yarnCoins = 0, int perkCoins = 0, int nextYarnChance = 0)
        {
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

        /// <summary>Повтор мотка поднял его уровень (монет за повтор нет).</summary>
        public bool YarnLevelUp => Yarn >= 0 && !NewYarn && YarnCoins == 0;

        public int Total => BaseCoins + ComboCoins + YarnCoins + PerkCoins;
    }
}
