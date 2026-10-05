#nullable enable

using System;

namespace KnittedPicture.Core
{
    /// <summary>
    /// Уровень в том виде, в каком он лежит в JSON.
    /// Публичные поля, чтобы его читали и Unity JsonUtility, и System.Text.Json в тестах.
    /// </summary>
    [Serializable]
    public sealed class LevelData
    {
        public int id;
        public int width;
        public int height;

        /// <summary>Цвета пряжи в hex. Индекс цвета — цифра в <see cref="picture"/>.</summary>
        public string[] colors = Array.Empty<string>();

        /// <summary>Картинка сверху вниз: строка на ряд, символ '0'..'9' — индекс цвета.</summary>
        public string[] picture = Array.Empty<string>();

        /// <summary>Сколько слотов на станке в начале уровня.</summary>
        public int slots = 5;

        /// <summary>Клубки на поле. Внутри колонки первый в списке — передний, его можно тапнуть.</summary>
        public SpoolData[] spools = Array.Empty<SpoolData>();
    }

    [Serializable]
    public sealed class SpoolData
    {
        public int column;
        public int color;
        public int loops;
    }
}
