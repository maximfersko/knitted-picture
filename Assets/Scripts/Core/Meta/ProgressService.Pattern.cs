#nullable enable

using System.Collections.Generic;
using System.Linq;

namespace KnittedPicture.Core
{
    public sealed partial class ProgressService
    {
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
    }
}
