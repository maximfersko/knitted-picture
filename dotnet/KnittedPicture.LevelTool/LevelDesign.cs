using System;
using System.Collections.Generic;

namespace KnittedPicture.LevelTool
{
    /// <summary>Исходник уровня в levels-src: картинка буквами и параметры, из которых собирается поле.</summary>
    public sealed class LevelDesign
    {
        public int id;
        public string name = string.Empty;
        public string tip = string.Empty;
        public bool hard;
        public int timeLimit;

        /// <summary>Буква в картинке → цвет. Порядок задаёт индексы цветов.</summary>
        public List<PaletteEntry> palette = new List<PaletteEntry>();

        public string[] picture = Array.Empty<string>();

        public int slots = 5;
        public int columns = 4;

        /// <summary>Сколько клубков на поле.</summary>
        public int spools = 4;

        /// <summary>Сколько раз переставить соседние клубки в колонках — чем больше, тем запутаннее очередь.</summary>
        public int swaps;

        public int hidden;
        public int knots;
        public int links;
        public int locks;

        /// <summary>Допустимая доля побед бота: так задаётся сложность.</summary>
        public double minWin;
        public double maxWin = 1.0;

        public int seed = 1;
    }

    public sealed class PaletteEntry
    {
        public string key = string.Empty;
        public string hex = string.Empty;
    }
}
