#nullable enable

using System.IO;
using KnittedPicture.Game.Qa;
using UnityEditor;
using UnityEngine;

namespace KnittedPicture.Editor
{
    /// <summary>
    /// Меню Knitted → QA: запустить внутриигрового бота (он сам входит в Play Mode), остановить, открыть отчёт.
    /// Бот нажимает настоящие кнопки, сохраняет скриншоты ошибок и пишет report.html в persistentDataPath/qa.
    /// </summary>
    public static class KnittedQa
    {
        [MenuItem("Knitted/QA/Бот: 5 минут")]
        public static void Run5() => Run(5f);

        [MenuItem("Knitted/QA/Бот: 30 минут")]
        public static void Run30() => Run(30f);

        [MenuItem("Knitted/QA/Остановить и записать отчёт")]
        public static void Stop() => QaBot.StopNow();

        [MenuItem("Knitted/QA/Открыть папку отчётов")]
        public static void Open()
        {
            var dir = Path.Combine(Application.persistentDataPath, "qa");
            Directory.CreateDirectory(dir);
            EditorUtility.RevealInFinder(dir);
        }

        private static void Run(float minutes)
        {
            if (EditorApplication.isPlaying)
            {
                QaBot.Launch(minutes, System.Environment.TickCount);
                return;
            }

            PlayerPrefs.SetFloat(QaBot.PrefsKey, minutes);
            PlayerPrefs.Save();
            EditorApplication.isPlaying = true;
        }
    }
}
