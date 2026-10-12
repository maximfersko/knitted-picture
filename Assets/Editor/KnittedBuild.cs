#nullable enable

using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace KnittedPicture.Editor
{
    /// <summary>
    /// Сборка под магазины одной кнопкой: меню Knitted → Сборка.
    /// - RuStore: STORE_RUSTORE + реклама Яндекса + покупки RuStore Pay (если SDK установлены).
    /// - Google Play: STORE_GOOGLEPLAY + реклама Яндекса, покупок нет.
    /// Символы передаются только в эту сборку (extraScriptingDefines) и не меняют проект, поэтому в редакторе
    /// игра по-прежнему с заглушками. Номер сборки (versionCode) растёт на 1 при каждой сборке для магазина —
    /// оба магазина принимают только возрастающий номер. Ключ подписи берётся из Signing/signing.json
    /// (папка в .gitignore, в репозиторий не попадает); как его создать — docs/store/RELEASE.md.
    /// </summary>
    public static class KnittedBuild
    {
        private const string SigningFile = "Signing/signing.json";

        [MenuItem("Knitted/Сборка/RuStore (APK)")]
        public static void RuStoreApk() => Build("RuStore", "STORE_RUSTORE", appBundle: false);

        [MenuItem("Knitted/Сборка/RuStore (AAB)")]
        public static void RuStoreAab() => Build("RuStore", "STORE_RUSTORE", appBundle: true);

        [MenuItem("Knitted/Сборка/Google Play (AAB)")]
        public static void GooglePlayAab() => Build("GooglePlay", "STORE_GOOGLEPLAY", appBundle: true);

        [MenuItem("Knitted/Сборка/Проверить, какие SDK найдены")]
        public static void ShowSdks()
        {
            EditorUtility.DisplayDialog("SDK в проекте",
                $"Реклама Яндекса: {(HasType("YandexMobileAds.RewardedAdLoader") ? "найдена" : "НЕ найдена")}\n" +
                $"RuStore Pay: {(HasType("RuStore.PayClient.RuStorePayClient") ? "найден" : "НЕ найден")}\n" +
                $"Аналитика AppMetrica: {(HasType("Io.AppMetrica.AppMetrica") ? "найдена" : "НЕ найдена")}\n" +
                $"Ключ подписи ({SigningFile}): {(File.Exists(SigningFile) ? "есть" : "НЕТ")}", "OK");
        }

        private static void Build(string store, string storeSymbol, bool appBundle)
        {
            if (!ApplySigning())
            {
                return;
            }

            var defines = new System.Collections.Generic.List<string> { storeSymbol };
            if (HasType("YandexMobileAds.RewardedAdLoader"))
            {
                defines.Add("KNITTED_YANDEX_ADS");
            }
            else if (!EditorUtility.DisplayDialog("Нет рекламы Яндекса",
                         "Плагин Yandex Mobile Ads не найден: сборка выйдет без рекламы (с заглушкой). Продолжить?", "Собрать", "Отмена"))
            {
                return;
            }

            if (HasType("Io.AppMetrica.AppMetrica"))
            {
                defines.Add("KNITTED_APPMETRICA");
            }

            if (storeSymbol == "STORE_RUSTORE" && HasType("RuStore.PayClient.RuStorePayClient"))
            {
                defines.Add("KNITTED_RUSTORE_PAY");
            }

            var android = NamedBuildTarget.Android;
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.bundleVersionCode += 1;
            EditorUserBuildSettings.buildAppBundle = appBundle;
            AssetDatabase.SaveAssets();

            var version = $"{PlayerSettings.bundleVersion}-{PlayerSettings.Android.bundleVersionCode}";
            var folder = Path.Combine("Builds", store);
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, $"knitted-picture-{store.ToLowerInvariant()}-{version}.{(appBundle ? "aab" : "apk")}");
            var options = new BuildPlayerOptions
            {
                scenes = EnsureScene(),
                locationPathName = path,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None,
                extraScriptingDefines = defines.ToArray(),
            };

            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result == BuildResult.Succeeded)
            {
                EditorUtility.RevealInFinder(path);
                Debug.Log($"[Build] {store}: {path} ({string.Join(", ", defines)})");
            }
            else
            {
                EditorUtility.DisplayDialog("Сборка не удалась", $"{report.summary.result}: смотри Console.", "OK");
            }
        }

        [Serializable]
        private sealed class Signing
        {
            public string keystore = "";
            public string keystorePass = "";
            public string alias = "";
            public string aliasPass = "";
        }

        /// <summary>Подпись релизным ключом: без неё магазины сборку не примут.</summary>
        private static bool ApplySigning()
        {
            if (!File.Exists(SigningFile))
            {
                EditorUtility.DisplayDialog("Нет ключа подписи",
                    $"Не найден {SigningFile}. Создай ключ и файл по инструкции docs/store/RELEASE.md (шаг «Ключ подписи»).", "OK");
                return false;
            }

            var signing = JsonUtility.FromJson<Signing>(File.ReadAllText(SigningFile));
            if (!File.Exists(signing.keystore))
            {
                EditorUtility.DisplayDialog("Нет файла ключа", $"В {SigningFile} указан {signing.keystore}, но такого файла нет.", "OK");
                return false;
            }

            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = signing.keystore;
            PlayerSettings.Android.keystorePass = signing.keystorePass;
            PlayerSettings.Android.keyaliasName = signing.alias;
            PlayerSettings.Android.keyaliasPass = signing.aliasPass;
            return true;
        }

        /// <summary>
        /// Игра строится кодом (App запускается сам после загрузки сцены), но Unity нужна хотя бы одна сцена
        /// в сборке. Если список сцен пуст — создаём пустую Assets/Scenes/Main.unity и добавляем её.
        /// </summary>
        private static string[] EnsureScene()
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length > 0)
            {
                return scenes;
            }

            const string path = "Assets/Scenes/Main.unity";
            if (!File.Exists(path))
            {
                // Новая сцена заменит открытую — сначала предложить сохранить изменения в ней.
                if (!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    throw new OperationCanceledException("Сборка отменена: сцена не сохранена.");
                }

                Directory.CreateDirectory("Assets/Scenes");
                var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                    UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, path);
            }

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(path, true) };
            return new[] { path };
        }

        private static bool HasType(string fullName) =>
            AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetType(fullName, false) != null);
    }
}
