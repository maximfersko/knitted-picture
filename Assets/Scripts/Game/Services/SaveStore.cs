#nullable enable

using KnittedPicture.Core;
using UnityEngine;

namespace KnittedPicture.Game
{
    /// <summary>Сохранение прогресса в PlayerPrefs одной JSON-строкой.</summary>
    public static class SaveStore
    {
        private const string Key = "knitted_picture_progress_v1";

        /// <summary>Сохранение уже лежит в PlayerPrefs: загружено или записано в этой сессии.</summary>
        private static bool _stored;

        /// <summary>
        /// Сохранение стёрли снаружи, пока игра работала (в редакторе Edit → Clear All PlayerPrefs,
        /// на телефоне «Очистить данные»). Тогда прогресс в памяти устарел: его нельзя записывать обратно.
        /// </summary>
        public static bool WasCleared => _stored && !PlayerPrefs.HasKey(Key);

        public static PlayerProgress Load()
        {
            var json = PlayerPrefs.GetString(Key, string.Empty);
            _stored = !string.IsNullOrEmpty(json);
            if (string.IsNullOrEmpty(json))
            {
                return new PlayerProgress();
            }

            try
            {
                return JsonUtility.FromJson<PlayerProgress>(json) ?? new PlayerProgress();
            }
            catch (System.ArgumentException)
            {
                Debug.LogWarning("Сохранение повреждено, начинаем заново");
                return new PlayerProgress();
            }
        }

        public static void Save(PlayerProgress progress)
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(progress));
            PlayerPrefs.Save();
            _stored = true;
        }

        public static void Reset()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
            _stored = false;
        }
    }
}
