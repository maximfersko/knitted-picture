#nullable enable

namespace KnittedPicture.Core
{
    /// <summary>
    /// Бесплатный старт уровня за «тёплую серию» побед. Подарок серии не считается бустером:
    /// звёзды за него не снимаются. Проигрыш с перезапуском или уход посреди уровня серию обнуляют.
    /// </summary>
    public readonly struct WarmStart
    {
        /// <summary>Сколько ступеней у серии: дальше подарок не растёт, но и не сгорает.</summary>
        public const int MaxTier = 3;

        public WarmStart(bool extraSlot, bool freeScissors, bool freeUndo)
        {
            ExtraSlot = extraSlot;
            FreeScissors = freeScissors;
            FreeUndo = freeUndo;
        }

        /// <summary>Станок начинает уровень с лишним слотом.</summary>
        public bool ExtraSlot { get; }

        /// <summary>Одни ножницы на этот уровень — бесплатно.</summary>
        public bool FreeScissors { get; }

        /// <summary>Одна отмена на этот уровень — бесплатно.</summary>
        public bool FreeUndo { get; }

        public bool Any => ExtraSlot || FreeScissors || FreeUndo;
    }
}
