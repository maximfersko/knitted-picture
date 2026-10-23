#nullable enable

namespace KnittedPicture.Core
{
    public sealed partial class ProgressService
    {
        // ---------- Воронка первых минут (аналитика) ----------

        /// <summary>Шаг воронки ещё не отправлялся? Если да, отмечает отправленным и возвращает true (один раз за установку).</summary>
        public bool TakeFunnel(FunnelStep step)
        {
            var bit = (int)step;
            if (bit < 0 || bit > 30)
            {
                return false;
            }

            var mask = 1 << bit;
            if ((_data.funnel & mask) != 0)
            {
                return false;
            }

            _data.funnel |= mask;
            return true;
        }

        /// <summary>Шаг воронки уже отправлен.</summary>
        public bool FunnelSent(FunnelStep step)
        {
            var bit = (int)step;
            return bit >= 0 && bit <= 30 && (_data.funnel & (1 << bit)) != 0;
        }

        /// <summary>Засчитать запуск игры; возвращает порядковый номер запуска (1 — первый).</summary>
        public int CountLaunch() => ++_data.launches;
    }
}
