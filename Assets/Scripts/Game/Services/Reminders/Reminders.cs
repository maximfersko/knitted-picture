#nullable enable

using System;
using System.Collections.Generic;
using KnittedPicture.Core;
#if UNITY_ANDROID && !UNITY_EDITOR && KNITTED_NOTIFICATIONS
using Unity.Notifications.Android;
using UnityEngine.Android;
#elif UNITY_IOS && !UNITY_EDITOR && KNITTED_NOTIFICATIONS
using Unity.Notifications.iOS;
#endif

namespace KnittedPicture.Game
{
    /// <summary>
    /// Напоминания на телефон (локальные, без сервера). Ставятся, когда игру сворачивают или закрывают,
    /// и снимаются, когда игрок вернулся. Немного и по делу: подарок или узор дня завтра вечером,
    /// серия узоров, которую жалко потерять, готовый сундук и мягкое «возвращайся» через три дня.
    /// На Android и iOS — пакет com.unity.mobile.notifications; в редакторе, на других платформах и без пакета —
    /// тихо ничего, а строки «Напоминания» в настройках нет (Supported), чтобы переключатель не обманывал. Своя сборка (KnittedPicture.Reminders.asmdef) включает код уведомлений только когда пакет
    /// установлен (versionDefines → KNITTED_NOTIFICATIONS), так что игра собирается и без него.
    /// </summary>
    public static class Reminders
    {
        /// <summary>Напоминания реально работают в этой сборке: есть пакет уведомлений и это телефон.</summary>
        public static bool Supported =>
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR && KNITTED_NOTIFICATIONS
            true;
#else
            false;
#endif

        /// <summary>Вечернее время напоминаний: после работы и учёбы.</summary>
        private const int EveningHour = 19;

        /// <summary>Что и когда напомнить — чистая логика, её легко проверить и поменять.</summary>
        public static List<(DateTime at, string title, string text)> Plan(ProgressService progress, int today, DateTime now)
        {
            var plan = new List<(DateTime, string, string)>();
            if (progress.Data.completed.Count == 0)
            {
                return plan;
            }

            var tomorrowEvening = now.Date.AddDays(1).AddHours(EveningHour);
            plan.Add(progress.PatternOpen
                ? (tomorrowEvening, L.T("Новый узор дня"), L.T("Сегодняшняя картинка уже ждёт. Свяжем?"))
                : (tomorrowEvening, L.T("Подарок дня"), L.T("Загляни за подарком: он уже ждёт")));

            // Серия узоров не связана сегодня — напомнить вечером, пока ещё не поздно.
            var streak = progress.PatternStreak(today);
            var todayEvening = now.Date.AddHours(20);
            if (progress.PatternOpen && streak >= 2 && !progress.PatternDone(today) && todayEvening > now.AddMinutes(30))
            {
                plan.Add((todayEvening, L.T("Серия узоров"), L.F("Уже {0} дн. подряд. Свяжи сегодняшний узор, чтобы не прервать серию", streak)));
            }

            if (progress.CanOpenChest)
            {
                plan.Add((now.AddHours(3), L.T("Звёздный сундук готов"), L.T("Внутри монеты, бустер на выбор и моток пряжи")));
            }

            plan.Add((now.Date.AddDays(3).AddHours(EveningHour), L.T("Клубки скучают"), L.T("Свяжем ещё одну картинку?")));
            return plan;
        }

        /// <summary>Поставить напоминания (игру сворачивают или закрывают).</summary>
        public static void Schedule(ProgressService progress, int today)
        {
            CancelAll();
            if (!progress.Data.remindersOn)
            {
                return;
            }

            foreach (var (at, title, text) in Plan(progress, today, DateTime.Now))
            {
                Send(title, text, at);
            }
        }

        /// <summary>Игрок вернулся: старые напоминания больше не нужны.</summary>
        public static void CancelAll()
        {
#if UNITY_ANDROID && !UNITY_EDITOR && KNITTED_NOTIFICATIONS
            AndroidNotificationCenter.CancelAllNotifications();
#elif UNITY_IOS && !UNITY_EDITOR && KNITTED_NOTIFICATIONS
            iOSNotificationCenter.RemoveAllScheduledNotifications();
            iOSNotificationCenter.RemoveAllDeliveredNotifications();
            iOSNotificationCenter.ApplicationBadge = 0;
#endif
        }

        /// <summary>Спросить разрешение на уведомления (Android 13+) — один раз, когда игрок уже втянулся.</summary>
        public static void AskPermission()
        {
#if UNITY_ANDROID && !UNITY_EDITOR && KNITTED_NOTIFICATIONS
            const string permission = "android.permission.POST_NOTIFICATIONS";
            if (!Permission.HasUserAuthorizedPermission(permission))
            {
                Permission.RequestUserPermission(permission);
            }
#elif UNITY_IOS && !UNITY_EDITOR && KNITTED_NOTIFICATIONS
            // Запрос живёт, пока система не ответит; повторный вызов после ответа ничего не показывает.
            _authorization ??= new AuthorizationRequest(AuthorizationOption.Alert | AuthorizationOption.Sound, false);
#endif
        }

#if UNITY_IOS && !UNITY_EDITOR && KNITTED_NOTIFICATIONS
        private static AuthorizationRequest? _authorization;
        private static int _sent;

        private static void Send(string title, string text, DateTime at)
        {
            var delay = at - DateTime.Now;
            if (delay <= TimeSpan.Zero)
            {
                return;
            }

            iOSNotificationCenter.ScheduleNotification(new iOSNotification
            {
                Identifier = "knitted-" + _sent++,
                Title = title,
                Body = text,
                ShowInForeground = false,
                Trigger = new iOSNotificationTimeIntervalTrigger { TimeInterval = delay, Repeats = false },
            });
        }
#endif

#if UNITY_ANDROID && !UNITY_EDITOR && KNITTED_NOTIFICATIONS
        private const string Channel = "reminders";
        private static bool _channel;

        private static void Send(string title, string text, DateTime at)
        {
            if (!_channel)
            {
                AndroidNotificationCenter.RegisterNotificationChannel(
                    new AndroidNotificationChannel(Channel, L.T("Напоминания"), L.T("Подарки, узор дня и сундук"), Importance.Default));
                _channel = true;
            }

            AndroidNotificationCenter.SendNotification(new AndroidNotification(title, text, at), Channel);
        }
#elif !(UNITY_IOS && !UNITY_EDITOR && KNITTED_NOTIFICATIONS)
        private static void Send(string title, string text, DateTime at)
        {
        }
#endif
    }
}
