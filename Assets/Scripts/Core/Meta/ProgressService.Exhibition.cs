#nullable enable

using System.Collections.Generic;
using System.Linq;

namespace KnittedPicture.Core
{
    public sealed partial class ProgressService
    {
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
    }
}
