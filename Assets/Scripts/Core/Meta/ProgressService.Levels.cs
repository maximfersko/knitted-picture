#nullable enable

using System.Collections.Generic;
using System.Linq;

namespace KnittedPicture.Core
{
    public sealed partial class ProgressService
    {
        // ---------- Победа и прохождение ----------

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
}
