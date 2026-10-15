#nullable enable

using System.Collections.Generic;
using System.Linq;

namespace KnittedPicture.Core
{
    public sealed partial class ProgressService
    {
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
    }
}
