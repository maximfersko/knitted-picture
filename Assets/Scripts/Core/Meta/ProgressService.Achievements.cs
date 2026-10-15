#nullable enable

using System.Collections.Generic;
using System.Linq;

namespace KnittedPicture.Core
{
    public sealed partial class ProgressService
    {
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
    }
}
