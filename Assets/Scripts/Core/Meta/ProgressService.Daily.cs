#nullable enable

using System.Collections.Generic;
using System.Linq;

namespace KnittedPicture.Core
{
    public sealed partial class ProgressService
    {
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
    }
}
