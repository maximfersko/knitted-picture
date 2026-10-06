#nullable enable

using System;
using System.Collections.Generic;
using KnittedPicture.Core;
#if UNITY_ANDROID && !UNITY_EDITOR
using Unity.Notifications.Android;
using UnityEngine.Android;
#endif

namespace KnittedPicture.Game
{
    /// <summary>
    /// Напоминания на телефон (локальные, без сервера). Ставятся, когда игру сворачивают или закрывают,
    /// и снимаются, когда игрок вернулся. Немного и по делу: подарок или узор дня завтра вечером,
    /// серия узоров, которую жалко потерять, готовый сундук и мягкое «возвращайся» через три дня.
    /// На Android — пакет com.unity.mobile.notifications; в редакторе и на других платформах — тихо ничего.
    /// </summary>
    public static class Reminders
    {
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
                ? (tomorrowEvening, "Новый узор дня", "Сегодняшняя картинка уже ждёт. Свяжем?")
                : (tomorrowEvening, "Подарок дня", "Загляни за подарком: он уже ждёт"));

            // Серия узоров не связана сегодня — напомнить вечером, пока ещё не поздно.
            var streak = progress.PatternStreak(today);
            var todayEvening = now.Date.AddHours(20);
            if (progress.PatternOpen && streak >= 2 && !progress.PatternDone(today) && todayEvening > now.AddMinutes(30))
            {
                plan.Add((todayEvening, "Серия узоров", $"Уже {streak} дн. подряд. Свяжи сегодняшний узор, чтобы не прервать серию"));
            }

            if (progress.CanOpenChest)
            {
                plan.Add((now.AddHours(3), "Звёздный сундук готов", "Внутри монеты, бустер на выбор и моток пряжи"));
            }

            plan.Add((now.Date.AddDays(3).AddHours(EveningHour), "Клубки скучают", "Свяжем ещё одну картинку?"));
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
#if UNITY_ANDROID && !UNITY_EDITOR
            AndroidNotificationCenter.CancelAllNotifications();
#endif
        }

        /// <summary>Спросить разрешение на уведомления (Android 13+) — один раз, когда игрок уже втянулся.</summary>
        public static void AskPermission()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            const string permission = "android.permission.POST_NOTIFICATIONS";
            if (!Permission.HasUserAuthorizedPermission(permission))
            {
                Permission.RequestUserPermission(permission);
            }
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private const string Channel = "reminders";
        private static bool _channel;

        private static void Send(string title, string text, DateTime at)
        {
            if (!_channel)
            {
                AndroidNotificationCenter.RegisterNotificationChannel(
                    new AndroidNotificationChannel(Channel, "Напоминания", "Подарки, узор дня и сундук", Importance.Default));
                _channel = true;
            }

            AndroidNotificationCenter.SendNotification(new AndroidNotification(title, text, at), Channel);
        }
#else
        private static void Send(string title, string text, DateTime at)
        {
        }
#endif
    }
}
