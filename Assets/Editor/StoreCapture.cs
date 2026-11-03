#nullable enable

using System.IO;
using UnityEditor;
using UnityEngine;

namespace KnittedPicture.Editor
{
    /// <summary>Сохраняет реальный Game View для магазинных карточек без подмены интерфейса иллюстрацией.</summary>
    public static class StoreCapture
    {
        [MenuItem("Tools/Store/Capture 1 Gameplay")]
        private static void Gameplay() => Capture("1");

        [MenuItem("Tools/Store/Capture 2 Finished picture")]
        private static void Finished() => Capture("2");

        [MenuItem("Tools/Store/Capture 3 Collection")]
        private static void Collection() => Capture("3");

        private static void Capture(string name)
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogWarning("Store capture requires Play Mode and the desired screen in Game View.");
                return;
            }
            var project = Directory.GetParent(Application.dataPath)!.FullName;
            var folder = Path.Combine(project, "docs", "store", "raw");
            Directory.CreateDirectory(folder);
            // Новые снимки не перезаписывают уже отобранные материалы.
            var path = Path.Combine(folder, name + ".png");
            for (var suffix = 2; File.Exists(path); suffix++)
            {
                path = Path.Combine(folder, name + "-" + suffix + ".png");
            }
            ScreenCapture.CaptureScreenshot(path, 2);
            Debug.Log("Store screenshot requested: " + path);
        }
    }
}
