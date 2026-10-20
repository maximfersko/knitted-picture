#nullable enable

using System;
using KnittedPicture.Core;
using UnityEngine;
using UnityEngine.UI;

namespace KnittedPicture.Game
{
    /// <summary>
    /// Единый вид лимитов и ожиданий: «Использовано 2 из 3 · новые через 5 ч», «Следующий подарок через 7 ч».
    /// Игрок видит, сколько осталось и когда обновится, без давления: время округляется до часов и минут,
    /// никаких секундных таймеров и красного цвета; строка обновляется раз в минуту. Только показ: награды и сбросы
    /// по-прежнему считает ядро по дням (<see cref="App.Today"/>), перевод часов эти строки не меняет.
    /// </summary>
    public static class Limits
    {
        private static readonly DateTime Epoch = new DateTime(1970, 1, 1);

        /// <summary>Как часто пересчитывается живая строка, секунд.</summary>
        private const float RefreshSeconds = 60f;

        /// <summary>Сколько осталось до момента: «5 ч», «1 ч 20 мин», «40 мин», «3 дн.». Прошедшее время считается нулём.</summary>
        public static string ResetIn(DateTime until) =>
            Span(DateTime.SpecifyKind(until, DateTimeKind.Local) - DateTime.Now);

        /// <summary>Длительность человеческими словами: от двух дней дни, от часа часы (с минутами до трёх часов), иначе минуты.</summary>
        public static string Span(TimeSpan left)
        {
            var total = left < TimeSpan.Zero ? TimeSpan.Zero : left;
            if (total.TotalDays >= 2d)
            {
                // Дни округляются вверх: «2 дн.» не обещает раньше, чем на самом деле.
                return L.F("{0} дн.", (int)Math.Ceiling(total.TotalDays));
            }

            var hours = (int)total.TotalHours;
            if (hours >= 1)
            {
                return total.Minutes > 0 && hours < 3 ? L.F("{0} ч {1} мин", hours, total.Minutes) : L.F("{0} ч", hours);
            }

            return L.F("{0} мин", Math.Max(1, (int)total.TotalMinutes));
        }

        /// <summary>Ближайшая местная полночь: тогда начинается новый день (подарок, задания, узор, лимиты видео).</summary>
        public static DateTime NextMidnight() => DateTime.Now.Date.AddDays(1);

        /// <summary>Начало дня номер day (с 1970 года, как <see cref="App.Today"/>) по местному времени.</summary>
        public static DateTime StartOfDay(int day) => Epoch.AddDays(day);

        /// <summary>Понедельник 00:00: тогда меняется неделя (тема выставки, недельные вехи).</summary>
        public static DateTime NextMonday(int today) => StartOfDay(today + WeeklyMarathon.DaysLeft(today));

        /// <summary>Конец сезона: полночь после его последнего дня.</summary>
        public static DateTime SeasonEnd(int today) => StartOfDay(today + Season.DaysLeft(today));

        /// <summary>
        /// Счётчик лимита: «Использовано 2 из 3 · новые через 5 ч». Пока ничего не использовано, обновлять нечего,
        /// и «новые через …» не пишется.
        /// </summary>
        public static string Used(int used, int max, DateTime until) => used > 0
            ? L.F("Использовано {0} из {1} · новые через {2}", used, max, ResetIn(until))
            : L.F("Использовано {0} из {1}", used, max);

        /// <summary>
        /// Одна строка лимита или ожидания: Ink3, 13 с ужатием до 10, до двух строк. Все места показывают её одинаково.
        /// live — как собрать текст заново: тогда строка сама пересчитывается раз в минуту, пока жив её экран или окно.
        /// </summary>
        public static Text Line(RectTransform parent, string text, float x, float y, float w, Func<string>? live = null, float h = 32f,
            TextAnchor anchor = TextAnchor.UpperCenter)
        {
            var label = Ui.Label(parent, text, 13, Palette.Ink3, x, y, w, h, false, anchor);
            Ui.Fit(label, 10).lineSpacing = 0.9f;
            if (live != null && Tweens.Installed)
            {
                Schedule(label, live);
            }

            return label;
        }

        /// <summary>
        /// Раз в минуту, а не повторяющимся твином: следующий шаг планируется только после предыдущего, а с
        /// уничтоженным владельцем (экран перестроен, окно закрыто) цепочка просто обрывается.
        /// </summary>
        private static void Schedule(Text label, Func<string> live)
        {
            Tweens.Delay(label, RefreshSeconds, () =>
            {
                if (label == null)
                {
                    return;
                }

                label.text = live();
                Schedule(label, live);
            });
        }
    }
}
