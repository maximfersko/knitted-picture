#nullable enable

using UnityEngine;

namespace KnittedPicture.Game
{
    /// <summary>
    /// Поведение по правилам платформы. Лежит в сборке KnittedPicture.Haptics ради define KNITTED_ANDROIDJNI:
    /// без модуля Android JNI игра всё равно собирается.
    /// </summary>
    public static class Platform
    {
        /// <summary>
        /// Системная «Назад» на главном экране Android: свернуть игру, как делают приложения на Android,
        /// а не закрыть её и не оставить нажатие без ответа. На iOS системной «Назад» нет.
        /// </summary>
        public static void MoveToBack()
        {
#if UNITY_ANDROID && !UNITY_EDITOR && KNITTED_ANDROIDJNI
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                activity.Call<bool>("moveTaskToBack", true);
            }
            catch (System.Exception)
            {
                Application.Quit();
            }
#elif UNITY_ANDROID && !UNITY_EDITOR
            Application.Quit();
#endif
        }
    }
}
