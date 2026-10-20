#nullable enable

using KnittedPicture.Core;
using UnityEngine;

namespace KnittedPicture.Game
{
    /// <summary>
    /// Сохранение прогресса в PlayerPrefs одной JSON-строкой. Перед каждой записью предыдущая удачная копируется
    /// в резервный ключ; если основная запись не читается, берётся резервная, а нечитаемая строка сохраняется
    /// в отдельный ключ «_broken» (на случай, если поддержка сможет её разобрать).
    /// </summary>
    public static class SaveStore
    {
        private const string Key = "knitted_picture_progress_v1";
        private const string BackupKey = Key + "_backup";
        private const string BrokenKey = Key + "_broken";

        /// <summary>Сохранение уже лежит в PlayerPrefs: загружено или записано в этой сессии.</summary>
        private static bool _stored;

        /// <summary>Последняя удачная запись (загруженная или записанная): станет резервной при следующей записи.</summary>
        private static string _lastGood = string.Empty;

        /// <summary>Чем кончилась последняя загрузка: цела, восстановлена из копии или потеряна.</summary>
        public static LoadOutcome Outcome { get; private set; } = LoadOutcome.Ok;

        /// <summary>Игрок ещё не видел сообщение о восстановлении или потере (его показывает главная, один раз).</summary>
        public static bool NoticePending { get; private set; }

        /// <summary>Сообщение показано: больше не напоминаем.</summary>
        public static void NoticeShown() => NoticePending = false;

        /// <summary>
        /// Сохранение стёрли снаружи, пока игра работала (в редакторе Edit → Clear All PlayerPrefs,
        /// на телефоне «Очистить данные»). Тогда прогресс в памяти устарел: его нельзя записывать обратно.
        /// </summary>
        public static bool WasCleared => _stored && !PlayerPrefs.HasKey(Key);

        public static PlayerProgress Load()
        {
            var main = PlayerPrefs.GetString(Key, string.Empty);
            var backup = PlayerPrefs.GetString(BackupKey, string.Empty);
            _stored = !string.IsNullOrEmpty(main);
            var (outcome, progress) = SaveRecovery.Choose(main, backup, Parse);
            Outcome = outcome;
            NoticePending = outcome != LoadOutcome.Ok;
            switch (outcome)
            {
                case LoadOutcome.Restored:
                    Debug.LogWarning("Сохранение повреждено, взята резервная копия");
                    KeepBroken(main);
                    _lastGood = backup;
                    break;
                case LoadOutcome.Lost:
                    Debug.LogWarning("Сохранение и резервная копия повреждены, начинаем заново");
                    KeepBroken(main);
                    _lastGood = string.Empty;
                    break;
                default:
                    _lastGood = main;
                    break;
            }

            return progress ?? new PlayerProgress();
        }

        public static void Save(PlayerProgress progress)
        {
            var json = JsonUtility.ToJson(progress);

            // Предыдущая удачная запись уходит в резервную копию; саму основную запись не трогаем до этого момента.
            if (!string.IsNullOrEmpty(_lastGood) && _lastGood != json)
            {
                PlayerPrefs.SetString(BackupKey, _lastGood);
            }

            PlayerPrefs.SetString(Key, json);
            PlayerPrefs.Save();
            _lastGood = json;
            _stored = true;
        }

        public static void Reset()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.DeleteKey(BackupKey);
            PlayerPrefs.DeleteKey(BrokenKey);
            PlayerPrefs.Save();
            _stored = false;
            _lastGood = string.Empty;
            Outcome = LoadOutcome.Ok;
            NoticePending = false;
        }

        private static PlayerProgress? Parse(string json) => JsonUtility.FromJson<PlayerProgress>(json);

        private static void KeepBroken(string raw)
        {
            if (!string.IsNullOrEmpty(raw))
            {
                PlayerPrefs.SetString(BrokenKey, raw);
                PlayerPrefs.Save();
            }
        }
    }
}
