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
            Repair(_data);
        }

        /// <summary>
        /// Починить сохранение при загрузке: от старой или новой версии игры, после сбоя записи. Пустые списки
        /// вместо отсутствующих, ничего отрицательного, звёзд за уровень 0..3. Обычное сохранение не меняется.
        /// </summary>
        private static void Repair(PlayerProgress d)
        {
            d.frames ??= new System.Collections.Generic.List<int>();
            d.frames.RemoveAll(f => f <= 0 || f >= AlbumFrames.All.Length);
            if (d.frame != 0 && !d.frames.Contains(d.frame))
            {
                d.frame = 0;
            }

            d.doubleCount = System.Math.Max(0, d.doubleCount);
            d.videoBoosterCount = System.Math.Max(0, d.videoBoosterCount);

            if (d.language != L.Russian && d.language != L.English)
            {
                d.language = string.Empty;
            }

            d.completed ??= new System.Collections.Generic.List<int>();
            d.introsShown ??= new System.Collections.Generic.List<int>();
            d.stars ??= new System.Collections.Generic.List<int>();
            d.bestTaps ??= new System.Collections.Generic.List<int>();
            d.cleanWins ??= new System.Collections.Generic.List<int>();
            d.yarns ??= new System.Collections.Generic.List<int>();
            d.missionKinds ??= new System.Collections.Generic.List<int>();
            d.missionTargets ??= new System.Collections.Generic.List<int>();
            d.missionValues ??= new System.Collections.Generic.List<int>();
            d.achievementsClaimed ??= new System.Collections.Generic.List<int>();
            d.yarnSetsClaimed ??= new System.Collections.Generic.List<int>();
            d.collectionsClaimed ??= new System.Collections.Generic.List<int>();
            d.attempts ??= new System.Collections.Generic.List<int>();
            d.exhibitLevels ??= new System.Collections.Generic.List<int>();
            d.exhibitFrames ??= new System.Collections.Generic.List<int>();
            d.seasonPoints = System.Math.Max(0, d.seasonPoints);
            d.seasonClaimed = System.Math.Max(0, System.Math.Min(Season.Steps, d.seasonClaimed));
            d.exhibitMedal = System.Math.Max(0, System.Math.Min(3, d.exhibitMedal));
            if (d.nextLevel < 1)
            {
                d.nextLevel = 1;
            }

            d.coins = System.Math.Max(0, d.coins);
            d.slotBoosters = System.Math.Max(0, d.slotBoosters);
            d.scissorsBoosters = System.Math.Max(0, d.scissorsBoosters);
            d.undoBoosters = System.Math.Max(0, d.undoBoosters);
            d.timeBoosters = System.Math.Max(0, d.timeBoosters);
            d.chestsOpened = System.Math.Max(0, d.chestsOpened);
            d.weekStitches = System.Math.Max(0, d.weekStitches);
            d.weekClaimed = System.Math.Max(0, System.Math.Min(WeeklyMarathon.Targets.Length, d.weekClaimed));
            for (var i = 0; i < d.stars.Count; i++)
            {
                d.stars[i] = System.Math.Max(0, System.Math.Min(3, d.stars[i]));
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

        /// <summary>Добавить бустеры в запас (отрицательное — списать, но не ниже нуля).</summary>
        public void AddStock(BoosterType type, int count)
        {
            switch (type)
            {
                case BoosterType.Slot: _data.slotBoosters = System.Math.Max(0, _data.slotBoosters + count); break;
                case BoosterType.Scissors: _data.scissorsBoosters = System.Math.Max(0, _data.scissorsBoosters + count); break;
                case BoosterType.Undo: _data.undoBoosters = System.Math.Max(0, _data.undoBoosters + count); break;
                case BoosterType.Time: _data.timeBoosters = System.Math.Max(0, _data.timeBoosters + count); break;
            }
        }

        /// <summary>Хватит ли на бустер: из запаса или за монеты.</summary>
        public bool CanAfford(BoosterType type) => Stock(type) > 0 || _data.coins >= Economy.Price(type);

        /// <summary>Хватит ли на спасение: бустер в запасе или монеты по цене с ростом (Economy.RescuePrice).</summary>
        public bool CanAffordRescue(BoosterType type, int rescuesBefore) =>
            Stock(type) > 0 || _data.coins >= Economy.RescuePrice(type, rescuesBefore);

        /// <summary>Оплатить спасение: сначала бустер из запаса, иначе монеты по растущей цене.</summary>
        public Payment PayRescue(BoosterType type, int rescuesBefore)
        {
            if (Stock(type) > 0)
            {
                AddStock(type, -1);
                return Payment.FromStock;
            }

            var price = Economy.RescuePrice(type, rescuesBefore);
            if (_data.coins < price)
            {
                return Payment.NotEnough;
            }

            _data.coins -= price;
            return Payment.WithCoins;
        }

        /// <summary>Сколько ещё раз сегодня можно удвоить награду за видео.</summary>
        public int DoublesLeft(int today) =>
            _data.doubleDay == today ? System.Math.Max(0, Economy.DoublesPerDay - _data.doubleCount) : Economy.DoublesPerDay;

        /// <summary>Удвоение за видео случилось: засчитать в дневной лимит (новый день — счёт с нуля).</summary>
        public void RecordDouble(int today)
        {
            if (_data.doubleDay != today)
            {
                _data.doubleDay = today;
                _data.doubleCount = 0;
            }

            _data.doubleCount++;
        }

        /// <summary>Сколько ещё бустеров за видео можно взять сегодня.</summary>
        public int VideoBoostersLeft(int today) =>
            _data.videoBoosterDay == today ? System.Math.Max(0, Economy.VideoBoostersPerDay - _data.videoBoosterCount) : Economy.VideoBoostersPerDay;

        public void RecordVideoBooster(int today)
        {
            if (_data.videoBoosterDay != today)
            {
                _data.videoBoosterDay = today;
                _data.videoBoosterCount = 0;
            }

            _data.videoBoosterCount++;
        }

        /// <summary>
        /// Есть ли рамка: деревянная всегда, купленные и сезонные — в сохранении, рамка коллекции — как только
        /// у всех картинок коллекции по три звезды.
        /// </summary>
        public bool FrameOwned(int id)
        {
            if (id == 0 || _data.frames.Contains(id))
            {
                return true;
            }

            var info = AlbumFrames.Get(id);
            return info.Id == id && info.Source == FrameSource.Collection && IsCollectionPerfect(info.Tag);
        }

        /// <summary>У всех картинок коллекции по три звезды.</summary>
        public bool IsCollectionPerfect(int index)
        {
            if (index < 0 || index >= Collections.All.Length)
            {
                return false;
            }

            var collection = Collections.All[index];
            for (var id = collection.FirstLevel; id <= collection.LastLevel; id++)
            {
                if (StarsOf(id) < 3)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Сколько рамок коллекций уже открыто: экран победы сравнивает до и после.</summary>
        public int CollectionFramesOwned
        {
            get
            {
                var n = 0;
                for (var i = 0; i < Collections.All.Length; i++)
                {
                    n += IsCollectionPerfect(i) ? 1 : 0;
                }

                return n;
            }
        }

        public FrameInfo SelectedFrame => AlbumFrames.Get(_data.frame);

        /// <summary>Купить рамку за монеты (и сразу выбрать). false — уже есть или не хватает монет.</summary>
        public bool BuyFrame(int id)
        {
            var info = AlbumFrames.Get(id);
            if (info.Id != id || info.Source != FrameSource.Shop || FrameOwned(id) || _data.coins < info.Price)
            {
                return false;
            }

            _data.coins -= info.Price;
            _data.frames.Add(id);
            _data.frame = id;
            return true;
        }

        public bool SelectFrame(int id)
        {
            if (!FrameOwned(id))
            {
                return false;
            }

            // Рамка коллекции записывается в сохранение при выборе: так она переживает загрузку (Repair).
            if (id != 0 && !_data.frames.Contains(id))
            {
                _data.frames.Add(id);
            }

            _data.frame = id;
            return true;
        }

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

        /// <summary>Начислить монеты (отрицательное — списать, но не ниже нуля).</summary>
        public void AddCoins(int amount)
        {
            _data.coins = System.Math.Max(0, _data.coins + amount);
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
            // После посылки бабушки первая новая картинка — с полным тёплым стартом: мягкий вход обратно.
            if (_data.comebackWarm && !IsCompleted(levelId))
            {
                return new WarmStart(IsBoosterOpen(BoosterType.Slot, levelId), IsBoosterOpen(BoosterType.Scissors, levelId),
                    IsBoosterOpen(BoosterType.Undo, levelId));
            }

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

            // Повтор сначала поднимает уровень мотка, на максимальном уровне копит к золотой версии,
            // и только у золотого мотка становится монетами.
            var maxedBefore = YarnLevel(yarn) >= YarnCatalog.MaxLevel;
            var goldenBefore = IsGolden(yarn);
            var isNew = AddYarn(yarn);
            var goldStep = !isNew && maxedBefore && !goldenBefore ? GoldProgress(yarn) : 0;
            var duplicate = isNew || !maxedBefore || !goldenBefore ? 0 : YarnCatalog.DuplicateCoins(YarnCatalog.All[yarn].Rarity);
            _data.coins += duplicate;
            return new Granted(granted, isNew, duplicate, goldStep);
        }

        // ---------- Корзинка пряжи ----------

        public int YarnCount(int yarn) => yarn >= 0 && yarn < _data.yarns.Count ? _data.yarns[yarn] : 0;

        public bool HasYarn(int yarn) => YarnCount(yarn) > 0;

        /// <summary>Уровень мотка 0..3: сколько штук собрано, но не больше максимума.</summary>
        public int YarnLevel(int yarn) => System.Math.Min(YarnCount(yarn), YarnCatalog.MaxLevel);

        /// <summary>Шаги к золотой версии: повторы сверх максимального уровня, 0..YarnCatalog.GoldenAfter.</summary>
        public int GoldProgress(int yarn) => System.Math.Max(0, System.Math.Min(YarnCount(yarn) - YarnCatalog.MaxLevel, YarnCatalog.GoldenAfter));

        /// <summary>Золотая версия мотка: особый вид и бонус сильнее на 1.</summary>
        public bool IsGolden(int yarn) => YarnCount(yarn) >= YarnCatalog.MaxLevel + YarnCatalog.GoldenAfter;

        /// <summary>Моток «в работе», -1 — нет (или его нет в каталоге этой версии игры).</summary>
        public int ActiveYarn => _data.activeYarn < YarnCatalog.Count && HasYarn(_data.activeYarn) ? _data.activeYarn : -1;

        /// <summary>Взять моток в работу: его бонус будет действовать в уровнях.</summary>
        public bool SetActiveYarn(int yarn)
        {
            if (yarn >= YarnCatalog.Count || !HasYarn(yarn))
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
            return PerksOpen && yarn >= 0 && YarnCatalog.PerkOf(yarn) == perk ? YarnCatalog.Power(yarn, YarnLevel(yarn), IsGolden(yarn)) : 0;
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

        /// <summary>Сколько разных мотков без сезонных: для «Корзинка полна».</summary>
        public int BaseYarnKinds
        {
            get
            {
                var kinds = 0;
                for (var i = 0; i < YarnCatalog.Count; i++)
                {
                    kinds += HasYarn(i) && !YarnCatalog.All[i].Seasonal ? 1 : 0;
                }

                return kinds;
            }
        }

        /// <summary>Положить моток в корзинку. true — такого ещё не было.</summary>
        public bool AddYarn(int yarn)
        {
            if (yarn < 0 || yarn >= YarnCatalog.Count)
            {
                return false;
            }

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
        public int RollYarn(System.Random rng) => RollYarn(rng, Rarity.Common);

        /// <summary>Случайный моток не ниже редкости floor (посылка после перерыва, награды сезона).</summary>
        public int RollYarn(System.Random rng, Rarity floor)
        {
            var epicOnly = _data.yarnsSinceEpic >= YarnCatalog.EpicPity;
            if (epicOnly && floor < Rarity.Epic)
            {
                floor = Rarity.Epic;
            }

            var total = 0;
            var weights = new int[YarnCatalog.Count];
            for (var i = 0; i < weights.Length; i++)
            {
                var rarity = YarnCatalog.All[i].Rarity;
                weights[i] = rarity < floor ? 0 : YarnCatalog.RollWeight(i, HasYarn(i));
                total += weights[i];
            }

            var pick = PickWeighted(weights, total, rng);
            _data.yarnsSinceEpic = YarnCatalog.All[pick].Rarity >= Rarity.Epic ? 0 : _data.yarnsSinceEpic + 1;
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

        public static readonly Reward CollectionReward = new Reward(80, null, 0, DailyGift.RandomYarn);

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

        /// <summary>Редкость сундука, который откроется следующим.</summary>
        public Rarity NextChestRarity => Chest.RarityOf(_data.chestsOpened);

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

        /// <summary>
        /// Узор на сегодня уже связан. «Не раньше», а не «тот же день»: перевод часов назад не открывает узор заново
        /// (иначе награду можно получать по кругу). Если часы раньше убегали вперёд, узор откроется после той даты.
        /// </summary>
        public bool PatternDone(int today) => _data.patternDay >= today;

        /// <summary>Узор связан на дату позже сегодняшней (часы переводили назад).</summary>
        public bool PatternAhead(int today) => _data.patternDay > today;

        /// <summary>Дни подряд: сегодняшний или вчерашний узор продолжает серию, пропуск обрывает.</summary>
        public int PatternStreak(int today) =>
            _data.patternDay >= today - 1 ? _data.patternStreak : 0;

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
            RecordSeason(today, Season.PatternPoints);
            return Grant(new Reward(DailyPattern.RewardCoins, null, 0, yarn), rng);
        }

        // ---------- Знакомство с механиками ----------

        /// <summary>
        /// Какая механика только что открылась и ещё не показывалась: по одной за раз, в порядке открытия.
        /// Помечает показанной. null — знакомить не с чем. Так игрок узнаёт про них постепенно, а не все сразу.
        /// </summary>
        public Feature? TakeFeatureIntro()
        {
            foreach (var (feature, open) in new[]
                     {
                         (Feature.Missions, MissionsOpen),
                         (Feature.Season, SeasonOpen),
                         (Feature.Pattern, PatternOpen),
                         (Feature.Exhibition, ExhibitionOpen),
                     })
            {
                var id = 200 + (int)feature;
                if (open && !_data.introsShown.Contains(id))
                {
                    _data.introsShown.Add(id);
                    return feature;
                }
            }

            return null;
        }

        // ---------- Вязальный сезон ----------

        public bool SeasonOpen => _data.nextLevel >= Season.FromLevel;

        /// <summary>Сохранённый сезон — сегодняшний (часы назад — сезон замирает, как марафон).</summary>
        private bool SeasonCurrent(int today) => _data.seasonId == Season.SeasonOf(today);

        public int SeasonPoints(int today) =>
            SeasonCurrent(today) ? _data.seasonPoints : Season.SeasonOf(today) > _data.seasonId ? 0 : _data.seasonPoints;

        public int SeasonClaimed(int today) =>
            SeasonCurrent(today) ? _data.seasonClaimed : Season.SeasonOf(today) > _data.seasonId ? 0 : _data.seasonClaimed;

        /// <summary>Сколько ступеней пройдено по очкам (не больше Season.Steps).</summary>
        public int SeasonReached(int today) => System.Math.Min(Season.Steps, SeasonPoints(today) / Season.StepPoints);

        /// <summary>Очки сезона: новый сезон — счёт с нуля, незабранное прошлого сгорает.</summary>
        public void RecordSeason(int today, int points)
        {
            if (!SeasonOpen || points <= 0)
            {
                return;
            }

            var season = Season.SeasonOf(today);
            if (season > _data.seasonId)
            {
                _data.seasonId = season;
                _data.seasonPoints = 0;
                _data.seasonClaimed = 0;
            }

            if (season == _data.seasonId)
            {
                _data.seasonPoints = (int)System.Math.Min(int.MaxValue, (long)_data.seasonPoints + points);
            }
        }

        public bool CanClaimSeason(int today) => SeasonOpen && SeasonCurrent(today) && _data.seasonClaimed < SeasonReached(today);

        /// <summary>Сколько ступеней можно забрать прямо сейчас.</summary>
        public int SeasonWaiting(int today) => CanClaimSeason(today) ? SeasonReached(today) - _data.seasonClaimed : 0;

        /// <summary>
        /// Забрать следующую ступень сезона. Рамка финала кладётся в рамки (уже есть — монетами),
        /// сезонный моток — в корзинку (повтор копит к золотой версии). null — нечего забирать.
        /// </summary>
        public SeasonClaim? ClaimSeason(int today, System.Random rng)
        {
            if (!CanClaimSeason(today))
            {
                return null;
            }

            var theme = Season.ThemeOf(_data.seasonId);
            var step = Season.Step(_data.seasonClaimed + 1, theme);
            _data.seasonClaimed++;
            if (step.Prize == SeasonPrize.SeasonFrame)
            {
                var frame = AlbumFrames.ForSeason(theme);
                if (FrameOwned(frame.Id))
                {
                    return new SeasonClaim(step, Grant(Reward.CoinsOnly(Season.FrameOwnedCoins), rng), -1);
                }

                _data.frames.Add(frame.Id);
                return new SeasonClaim(step, new Granted(Reward.CoinsOnly(0), false, 0), frame.Id);
            }

            return new SeasonClaim(step, Grant(step.Reward, rng), -1);
        }

        // ---------- Выставка недели ----------

        public bool ExhibitionOpen => _data.nextLevel >= Exhibition.FromLevel && _data.completed.Count >= Exhibition.Pictures;

        public bool ExhibitionDone(int today) => _data.exhibitWeek >= WeeklyMarathon.WeekOf(today);

        public int ExhibitionMedal(int today) => _data.exhibitWeek == WeeklyMarathon.WeekOf(today) ? _data.exhibitMedal : 0;

        public int ExhibitionScore(int today) => _data.exhibitWeek == WeeklyMarathon.WeekOf(today) ? _data.exhibitScore : 0;

        /// <summary>Оценка набора: три картинки в рамках (рамки должны быть у игрока, картинки — готовы и разные).</summary>
        public int ExhibitionPreview(int today, IReadOnlyList<LevelData> pictures, IReadOnlyList<int> frames)
        {
            var theme = Exhibition.ThemeOf(WeeklyMarathon.WeekOf(today));
            var score = 0;
            for (var i = 0; i < pictures.Count && i < frames.Count; i++)
            {
                score += Exhibition.Score(pictures[i], StarsOf(pictures[i].id), AlbumFrames.Get(frames[i]), theme);
            }

            return score;
        }

        /// <summary>
        /// Выставить картинки: раз в неделю. Медаль даёт награду, выставка — очки сезона. null — уже выставлялись
        /// на этой неделе или набор неверный (не три разные готовые картинки, чужая рамка).
        /// </summary>
        public ExhibitionResult? SubmitExhibition(int today, IReadOnlyList<LevelData> pictures, IReadOnlyList<int> frames, System.Random rng)
        {
            if (!ExhibitionOpen || ExhibitionDone(today) || pictures.Count != Exhibition.Pictures || frames.Count != Exhibition.Pictures)
            {
                return null;
            }

            var ids = new HashSet<int>();
            for (var i = 0; i < Exhibition.Pictures; i++)
            {
                if (!IsCompleted(pictures[i].id) || !ids.Add(pictures[i].id) || !FrameOwned(frames[i]))
                {
                    return null;
                }
            }

            var score = ExhibitionPreview(today, pictures, frames);
            var medal = Exhibition.MedalOf(score);
            _data.exhibitWeek = WeeklyMarathon.WeekOf(today);
            _data.exhibitScore = score;
            _data.exhibitMedal = medal;
            _data.exhibitLevels = new List<int>();
            _data.exhibitFrames = new List<int>();
            for (var i = 0; i < Exhibition.Pictures; i++)
            {
                _data.exhibitLevels.Add(pictures[i].id);
                _data.exhibitFrames.Add(frames[i]);
            }

            RecordSeason(today, Season.ExhibitionPoints);
            Granted? granted = medal > 0 ? Grant(Exhibition.Rewards[medal - 1], rng) : (Granted?)null;
            return new ExhibitionResult(score, medal, granted);
        }

        // ---------- Посылка после перерыва ----------

        /// <summary>Сколько дней без игры нужно для посылки от бабушки.</summary>
        public const int ComebackDays = 3;

        /// <summary>Посылка не чаще, чем раз в столько дней: редко играющим она не становится вторым подарком дня.</summary>
        public const int ComebackCooldown = 14;

        /// <summary>
        /// Игрок зашёл в игру: если его не было ComebackDays дней и больше, ждёт посылка. Запоминает день.
        /// Часы назад посылку не дают и последний день не сдвигают назад.
        /// </summary>
        public bool Visit(int today)
        {
            if (_data.lastDay >= 0 && today - _data.lastDay >= ComebackDays && _data.completed.Count > 0 && !_data.comebackPending &&
                today - _data.comebackLastDay >= ComebackCooldown)
            {
                _data.comebackPending = true;
                _data.comebackLastDay = today;
                _data.comebackDays = today - _data.lastDay;
            }

            _data.lastDay = System.Math.Max(_data.lastDay, today);
            return _data.comebackPending;
        }

        public bool ComebackPending => _data.comebackPending;

        /// <summary>Редкость посылки: на ступень выше следующего сундука (не ниже необычной).</summary>
        public Rarity ComebackRarity => (Rarity)System.Math.Max((int)Rarity.Uncommon, System.Math.Min((int)Rarity.Legendary, (int)NextChestRarity + 1));

        /// <summary>
        /// Открыть посылку: монеты как в сундуке этой редкости (середина), бустер из открытых и моток не ниже
        /// её редкости (легендарная — не ниже эпической). Следующая новая картинка — с полным тёплым стартом.
        /// </summary>
        public Granted? ClaimComeback(System.Random rng)
        {
            if (!_data.comebackPending)
            {
                return null;
            }

            var rarity = ComebackRarity;
            var range = Chest.CoinRange(rarity);
            var open = new List<BoosterType>();
            foreach (BoosterType type in System.Enum.GetValues(typeof(BoosterType)))
            {
                if (IsBoosterOpen(type, CurrentLevel))
                {
                    open.Add(type);
                }
            }

            BoosterType? booster = open.Count > 0 ? open[rng.Next(open.Count)] : (BoosterType?)null;
            var floor = rarity > Rarity.Epic ? Rarity.Epic : rarity;
            var yarn = RollYarn(rng, floor);
            _data.comebackPending = false;
            _data.comebackWarm = true;
            return Grant(new Reward((range.Min + range.Max) / 2, booster, Chest.BoosterCountOf(rarity), yarn), rng);
        }

        // ---------- Недельные вехи (бывший марафон) ----------

        public bool MarathonOpen => _data.nextLevel >= WeeklyMarathon.FromLevel;

        /// <summary>
        /// Сохранённая неделя — сегодняшняя. Если часы перевели назад (сегодня раньше сохранённой недели),
        /// марафон замирает: петли не копятся и награды не выдаются, пока не наступит та неделя.
        /// </summary>
        private bool MarathonWeek(int today) => _data.weekId == WeeklyMarathon.WeekOf(today);

        /// <summary>Петли за эту неделю (новая неделя — 0, даже если её ещё не начали).</summary>
        public int MarathonStitches(int today) =>
            MarathonWeek(today) ? _data.weekStitches : WeeklyMarathon.WeekOf(today) > _data.weekId ? 0 : _data.weekStitches;

        /// <summary>Сколько недельных вех уже пройдено (каждая — ступень сезона).</summary>
        public int MarathonClaimed(int today) =>
            MarathonWeek(today) ? _data.weekClaimed : WeeklyMarathon.WeekOf(today) > _data.weekId ? 0 : _data.weekClaimed;

        /// <summary>Петли связаны (победа или узор дня): прибавить к марафону этой недели.</summary>
        public void RecordMarathon(int today, int stitches)
        {
            if (!MarathonOpen || stitches <= 0)
            {
                return;
            }

            var week = WeeklyMarathon.WeekOf(today);
            if (week > _data.weekId)
            {
                _data.weekId = week;
                _data.weekStitches = 0;
                _data.weekClaimed = 0;
            }

            if (week == _data.weekId)
            {
                _data.weekStitches = (int)System.Math.Min(int.MaxValue, (long)_data.weekStitches + stitches);

                // Веха недели — сразу ступень сезона, забирать отдельно не нужно.
                while (_data.weekClaimed < WeeklyMarathon.Targets.Length && _data.weekStitches >= WeeklyMarathon.Targets[_data.weekClaimed])
                {
                    _data.weekClaimed++;
                    RecordSeason(today, WeeklyMarathon.TierPoints);
                }
            }
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
            RecordSeason(today, Season.MissionsPoints);
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
        public WinReward CompleteLevel(int levelId, bool hard, int stars = 3, int comboCoins = 0, int today = -1)
        {
            // Такого уровня нет: ничего не засчитываем (ошибка вызова не должна ронять экран победы).
            if (levelId < 1 || levelId > LevelCount)
            {
                return new WinReward(0, 0, _data.streak, 0, false);
            }

            stars = System.Math.Max(0, System.Math.Min(3, stars));
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

            if (firstTime)
            {
                _data.comebackWarm = false;
            }

            // Сезон: звёзды за новую картинку, прибавка звёзд за улучшение, 1 очко за простой повтор.
            if (today >= 0)
            {
                RecordSeason(today, firstTime ? stars : stars > previousStars ? stars - previousStars : Season.ReplayPoints);
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
                nextChance,
                yarn.GoldStep);
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
