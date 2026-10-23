#nullable enable

using System.Collections.Generic;
using System.Linq;

namespace KnittedPicture.Core
{
    public sealed partial class ProgressService
    {
        // ---------- Знакомство с механиками ----------

        /// <summary>Сколько уровней после открытия механика считается новой: дальше игрок уже нашёл её сам.</summary>
        public const int IntroWindow = 3;

        /// <summary>
        /// Какая механика только что открылась и ещё не показывалась. Ненавязчиво: только новому игроку в момент
        /// открытия (не дальше <see cref="IntroWindow"/> уровней от него) и не больше одной за день. Механики,
        /// открытые давно (игрок обновил игру или уже освоился), помечаются показанными молча. Помечает показанной;
        /// null — знакомить не с чем.
        /// </summary>
        public Feature? TakeFeatureIntro(int today)
        {
            Feature? result = null;
            foreach (var (feature, open, from) in new[]
                     {
                         (Feature.Missions, MissionsOpen, DailyMissions.FromLevel),
                         (Feature.Season, SeasonOpen, Season.FromLevel),
                         (Feature.Pattern, PatternOpen, DailyPattern.FromLevel),
                         (Feature.Exhibition, ExhibitionOpen, Exhibition.FromLevel),
                     })
            {
                var id = 200 + (int)feature;
                if (!open || _data.introsShown.Contains(id))
                {
                    continue;
                }

                if (_data.nextLevel > from + IntroWindow)
                {
                    _data.introsShown.Add(id);
                    continue;
                }

                if (result == null && _data.introDay != today)
                {
                    _data.introsShown.Add(id);
                    _data.introDay = today;
                    result = feature;
                }
            }

            return result;
        }

        /// <summary>
        /// Знакомство с затором: совет «Места кончились, а работы нет?» показывается один раз, когда станок впервые
        /// заполнился на первых уровнях. Помечает показанным; false — уже показывали.
        /// </summary>
        public bool TakeLoomFullIntro()
        {
            const int introId = 300;
            if (_data.introsShown.Contains(introId))
            {
                return false;
            }

            _data.introsShown.Add(introId);
            return true;
        }
    }
}
