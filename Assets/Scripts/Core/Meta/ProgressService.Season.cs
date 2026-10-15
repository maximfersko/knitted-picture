#nullable enable

using System.Collections.Generic;
using System.Linq;

namespace KnittedPicture.Core
{
    public sealed partial class ProgressService
    {
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
    }
}
