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
    public sealed partial class ProgressService
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
    }
}
