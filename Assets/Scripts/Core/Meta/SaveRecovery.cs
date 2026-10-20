#nullable enable

using System;

namespace KnittedPicture.Core
{
    /// <summary>Чем кончилась загрузка сохранения.</summary>
    public enum LoadOutcome
    {
        /// <summary>Основная запись цела (или это первый запуск).</summary>
        Ok,

        /// <summary>Основная запись повреждена, прогресс взят из резервной копии.</summary>
        Restored,

        /// <summary>Повреждены и основная запись, и резервная: начинаем с нового прогресса.</summary>
        Lost,
    }

    /// <summary>
    /// Выбор между основной записью сохранения и резервной копией. Чистая функция без PlayerPrefs и JsonUtility:
    /// разбор строки передаёт вызывающий, поэтому правило проверяется тестами в dotnet, без Unity.
    /// </summary>
    public static class SaveRecovery
    {
        /// <summary>
        /// Основная читается — Ok. Основная пуста или не читается, резервная читается — Restored (из резервной).
        /// Пусты обе — первый запуск: Ok и прогресса нет (null). Основная не читается и резервная тоже — Lost.
        /// parse возвращает null или бросает исключение, если строку разобрать нельзя.
        /// </summary>
        public static (LoadOutcome Outcome, T? Value) Choose<T>(string? main, string? backup, Func<string, T?> parse)
            where T : class
        {
            T? value = null;
            if (!string.IsNullOrEmpty(main))
            {
                value = TryParse(main!, parse);
                if (value != null)
                {
                    return (LoadOutcome.Ok, value);
                }
            }

            if (!string.IsNullOrEmpty(backup))
            {
                value = TryParse(backup!, parse);
                if (value != null)
                {
                    return (LoadOutcome.Restored, value);
                }
            }

            return string.IsNullOrEmpty(main) ? (LoadOutcome.Ok, null) : (LoadOutcome.Lost, null);
        }

        private static T? TryParse<T>(string json, Func<string, T?> parse)
            where T : class
        {
            try
            {
                return parse(json);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
