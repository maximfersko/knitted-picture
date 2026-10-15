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
            _applied = data.batterySaver ? 30 : _cap;
            Application.targetFrameRate = _applied;
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
            // В простое частота снижена нарочно: длинные кадры там не значат, что телефон не успевает.
            if (_saver || _cap <= 60 || Time.realtimeSinceStartup < 8f || dt > 0.1f || _idle >= IdleAfter)
            {
                return;
            }

            _slowFor = dt > 1.3f / _cap ? _slowFor + dt : Mathf.Max(0f, _slowFor - dt * 0.5f);
            if (_slowFor > 3f)
            {
                _cap = 60;
                _applied = 60;
                Application.targetFrameRate = 60;
            }
        }

        /// <summary>Сколько секунд без касаний считается простоем.</summary>
        private const float IdleAfter = 5f;

        private static float _idle;

        private static int _applied = -1;

        /// <summary>
        /// Простой: раз в кадр. 5 секунд без касаний — в меню частота падает до 30 (там только медленные «дышащие»
        /// украшения), в игре — до 60 (моль и станок движутся всё время). Касание сразу возвращает полную частоту.
        /// Телефон меньше греется и дольше держит заряд, пока игрок читает или думает.
        /// </summary>
        public static void Idle(float dt, bool touched, bool inGame)
        {
            _idle = touched ? 0f : _idle + Mathf.Min(dt, 0.1f);
            var full = _saver ? 30 : _cap;
            var target = _idle < IdleAfter ? full : Mathf.Min(full, inGame ? 60 : 30);
            if (target != _applied)
            {
                _applied = target;
                Application.targetFrameRate = target;
            }
        }
    }
}
