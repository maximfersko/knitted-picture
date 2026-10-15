#nullable enable

using System.Collections.Generic;
using System.Linq;

namespace KnittedPicture.Core
{
    public sealed partial class ProgressService
    {
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
    }
}
