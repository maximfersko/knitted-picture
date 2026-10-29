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
    /// Своя сборка (KnittedPicture.Haptics.asmdef) нужна ради KNITTED_ANDROIDJNI: тонкая вибрация на Android
    /// требует встроенного модуля Android JNI. Если он выключен в Package Manager, игра всё равно собирается,
    /// а на Android остаётся только системная вибрация на победу и затор.
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
#elif UNITY_ANDROID && !UNITY_EDITOR && KNITTED_ANDROIDJNI
            PlayAndroid(haptic);
#elif UNITY_ANDROID && !UNITY_EDITOR
            if (haptic >= Haptic.Success)
            {
                Handheld.Vibrate();
            }
#endif
        }

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void KnittedHaptic(int kind);
#endif

#if UNITY_ANDROID && !UNITY_EDITOR && KNITTED_ANDROIDJNI
        /// <summary>Вибромотор: получаем один раз и держим, а не просим у системы на каждый толчок.</summary>
        private static AndroidJavaObject? _vibrator;
        private static int _sdk;

        /// <summary>Длительность (мс) и амплитуда каждого вида отклика, по порядку <see cref="Haptic"/>.</summary>
        private static readonly (long Ms, int Amplitude)[] Patterns =
        {
            (12L, 70),
            (18L, 100),
            (30L, 170),
            (55L, 210),
            (70L, 240),
        };

        /// <summary>Готовые эффекты вибрации (Android 8 и новее) по одному на вид отклика: на лету их не создаём.</summary>
        private static AndroidJavaObject[]? _effects;

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
                    if (_sdk >= 26)
                    {
                        // Класс нужен только чтобы создать эффекты; после этого он не держится.
                        using var effects = new AndroidJavaClass("android.os.VibrationEffect");
                        var made = new AndroidJavaObject[Patterns.Length];
                        for (var i = 0; i < Patterns.Length; i++)
                        {
                            made[i] = effects.CallStatic<AndroidJavaObject>("createOneShot", Patterns[i].Ms, Patterns[i].Amplitude);
                        }

                        _effects = made;
                    }
                }

                var index = Mathf.Clamp((int)haptic, 0, Patterns.Length - 1);
                if (_effects != null)
                {
                    _vibrator.Call("vibrate", _effects[index]);
                }
                else
                {
                    _vibrator.Call("vibrate", Patterns[index].Ms);
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
