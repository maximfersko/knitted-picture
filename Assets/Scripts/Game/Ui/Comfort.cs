#nullable enable

using UnityEngine;

namespace KnittedPicture.Game
{
    /// <summary>
    /// Настройки удобства, которые читают экраны и виды при построении. Хранятся в сохранении (PlayerProgress),
    /// здесь — их действующие значения. Набор взят из Game Accessibility Guidelines и Xbox Accessibility Guidelines
    /// для игры, где всё решает цвет: не только цветом (значки), без лишнего движения, крупный текст,
    /// плюс экономия заряда для слабых телефонов.
    /// </summary>
    public static class Comfort
    {
        /// <summary>Значки-фигуры на клубках и на петлях, которые вяжутся следующими.</summary>
        public static bool ColorMarks { get; private set; }

        /// <summary>Меньше движения (дублирует Tweens.Calm для читаемости в экранах).</summary>
        public static bool ReduceMotion => Tweens.Calm;

        /// <summary>Подсказка рукой при долгой паузе (кроме обучающих уровней, там она всегда).</summary>
        public static bool IdleHints { get; private set; } = true;

        public static void Apply(KnittedPicture.Core.PlayerProgress data)
        {
            ColorMarks = data.colorMarks;
            Tweens.Calm = data.reduceMotion;
            Ui.LargeText = data.largeText;
            IdleHints = data.idleHints;

            // Интерфейс без быстрых движений: 30 кадров хватает, а батарея и нагрев телефона заметно меньше.
            Application.targetFrameRate = data.batterySaver ? 30 : 60;
        }
    }
}
