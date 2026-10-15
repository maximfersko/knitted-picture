#nullable enable

using UnityEngine;

namespace KnittedPicture.Game
{
    /// <summary>
    /// Настройки удобства, которые читают экраны и виды при построении. Хранятся в сохранении (PlayerProgress),
    /// здесь — их действующие значения. Набор взят из Game Accessibility Guidelines и Xbox Accessibility Guidelines
    /// для игры на телефоне: без лишнего движения, крупный текст, плюс экономия заряда для слабых телефонов.
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
            Application.targetFrameRate = data.batterySaver ? 30 : 60;
        }
    }
}
