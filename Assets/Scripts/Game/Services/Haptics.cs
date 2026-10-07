#nullable enable

using UnityEngine;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace KnittedPicture.Game
{
    public enum Haptic
    {
        /// <summary>Лёгкий тап: клубок поехал на станок, клубок довязал.</summary>
        Tap,

        /// <summary>Мягкий толчок: отказ, золотая петля.</summary>
        Soft,

        /// <summary>Заметный: цвет довязан, «Разгон», ножницы.</summary>
        Medium,

        /// <summary>Победа.</summary>
        Success,

        /// <summary>Затор или моль.</summary>
        Warning,
    }

    /// <summary>
    /// Тактильный отклик. Половину мобильных игр играют без звука, и вибрация берёт на себя его роль:
    /// тап «чувствуется», завершение цвета и победа — ощутимы. На iPhone — системные генераторы отклика
    /// (Plugins/iOS/KnittedHaptics.mm), на Android — короткий импульс вибромотора. В редакторе ничего не делает.
    /// </summary>
    public static class Haptics
    {
        /// <summary>Лёгкие отклики не чаще раза в 50 мс: поток петель не должен превращаться в жужжание.</summary>
        private const float MinGap = 0.05f;

        private static float _last = -1f;

#if UNITY_ANDROID
        /// <summary>Никогда не true: ссылка на Handheld.Vibrate нужна, чтобы Unity добавил разрешение VIBRATE в манифест Android.</summary>
        private static bool _neverTrue;
#endif

        public static bool Enabled { get; set; } = true;

        public static void Play(Haptic haptic)
        {
            if (!Enabled)
            {
                return;
            }

            var now = Time.unscaledTime;
            if (haptic <= Haptic.Soft && now - _last < MinGap)
            {
                return;
            }

            _last = now;
#if UNITY_ANDROID
            if (_neverTrue)
            {
                Handheld.Vibrate();
            }
#endif

#if UNITY_IOS && !UNITY_EDITOR
            KnittedHaptic((int)haptic);
#elif UNITY_ANDROID && !UNITY_EDITOR
            PlayAndroid(haptic);
#endif
        }

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void KnittedHaptic(int kind);
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
        private static AndroidJavaObject? _vibrator;
        private static int _sdk;

        private static void PlayAndroid(Haptic haptic)
        {
            try
            {
                if (_vibrator == null)
                {
                    using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                    using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                    _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                    using var version = new AndroidJavaClass("android.os.Build$VERSION");
                    _sdk = version.GetStatic<int>("SDK_INT");
                }

                var (ms, amplitude) = haptic switch
                {
                    Haptic.Tap => (12L, 70),
                    Haptic.Soft => (18L, 100),
                    Haptic.Medium => (30L, 170),
                    Haptic.Success => (55L, 210),
                    _ => (70L, 240),
                };

                if (_sdk >= 26)
                {
                    using var effects = new AndroidJavaClass("android.os.VibrationEffect");
                    using var effect = effects.CallStatic<AndroidJavaObject>("createOneShot", ms, amplitude);
                    _vibrator.Call("vibrate", effect);
                }
                else
                {
                    _vibrator.Call("vibrate", ms);
                }
            }
            catch (System.Exception)
            {
                // Нет вибромотора или запрет — просто без отклика.
                Enabled = false;
            }
        }
#endif
    }
}
