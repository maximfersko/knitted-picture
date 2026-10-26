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

        /// <summary>
        /// Миграция для обновившихся игроков: сохранение старой версии без счётчика запусков (launches == 0), но с игрой
        /// (вступление просмотрено или есть пройденные уровни) отмечает все шаги воронки отправленными, чтобы
        /// ftue_* не засоряли воронку «новых» игроков. Вызывать при запуске ДО <see cref="CountLaunch"/>.
        /// Возвращает true, если пометка сделана. У нового игрока и у тех, у кого запуски уже считаются, ничего не меняет.
        /// </summary>
        public bool MarkLegacyFunnel()
        {
            if (_data.launches != 0)
            {
                return false;
            }

            var played = _data.introSeen || (_data.completed != null && _data.completed.Count > 0);
            if (!played)
            {
                return false;
            }

            _data.funnel = 0x7FFFFFFF;
            return true;
        }

        /// <summary>Засчитать запуск игры; возвращает порядковый номер запуска (1 — первый).</summary>
        public int CountLaunch() => ++_data.launches;
    }
}
