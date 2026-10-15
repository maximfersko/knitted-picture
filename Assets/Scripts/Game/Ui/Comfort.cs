#nullable enable

using UnityEngine;

namespace KnittedPicture.Game
{
    /// <summary>
    /// Настройки удобства, которые читают экраны и виды при построении. Хранятся в сохранении (PlayerProgress),
    /// здесь — их действующие значения. Набор взят из Game Accessibility Guidelines и Xbox Accessibility Guidelines
    /// для игры на телефоне: без лишнего движения, крупный текст, плюс экономия заряда для слабых телефонов.
    /// Здесь же частота кадров: частота экрана (до 120), а если телефон не успевает — 60.
    /// </summary>
    public static class Comfort
    {
        /// <summary>Меньше движения (дублирует Tweens.Calm для читаемости в экранах).</summary>
        public static bool ReduceMotion => Tweens.Calm;

        /// <summary>Подсказка рукой при долгой паузе (кроме обучающих уровней, там она всегда).</summary>
        public static bool IdleHints { get; private set; } = true;

        public static void Apply(KnittedPicture.Core.PlayerProgress data)
        {
            Tweens.Calm = data.reduceMotion;
            Ui.LargeText = data.largeText;
            IdleHints = data.idleHints;

            // Интерфейс без быстрых движений: 30 кадров хватает, а батарея и нагрев телефона заметно меньше.
            // targetFrameRate работает, только когда кадры не привязаны к частоте экрана: на телефонах Unity и так
            // это игнорирует, а в редакторе и на планшетах с высоким уровнем качества vSync включён.
            QualitySettings.vSyncCount = 0;
            _saver = data.batterySaver;
            Application.targetFrameRate = data.batterySaver ? 30 : _cap;
        }

        private static bool _saver;

        /// <summary>
        /// Частота кадров без экономии: частота экрана (90–120 Гц у современных телефонов — анимации заметно глаже),
        /// но не больше 120 и не меньше 60. Если телефон не успевает (Governor), опускаемся до 60 до конца сеанса.
        /// </summary>
        private static int _cap = ScreenRate();

        private static float _slowFor;

        private static int ScreenRate()
        {
#if UNITY_2022_2_OR_NEWER
            var rate = (int)System.Math.Round(Screen.currentResolution.refreshRateRatio.value);
#else
            var rate = Screen.currentResolution.refreshRate;
#endif
            return Mathf.Clamp(rate, 60, 120);
        }

        /// <summary>
        /// Раз в кадр: если при высокой частоте кадры три секунды подряд заметно длиннее нужного, частота падает
        /// до 60 — на слабом телефоне ровные 60 лучше, чем рваные 90–120.
        /// </summary>
        public static void Governor(float dt)
        {
            // Первые секунды (загрузка, рисование графики) и одиночные долгие кадры (строится экран) не в счёт:
            // смотрим только на устойчивую нехватку времени.
            if (_saver || _cap <= 60 || Time.realtimeSinceStartup < 8f || dt > 0.1f)
            {
                return;
            }

            _slowFor = dt > 1.3f / _cap ? _slowFor + dt : Mathf.Max(0f, _slowFor - dt * 0.5f);
            if (_slowFor > 3f)
            {
                _cap = 60;
                Application.targetFrameRate = 60;
            }
        }
    }
}
