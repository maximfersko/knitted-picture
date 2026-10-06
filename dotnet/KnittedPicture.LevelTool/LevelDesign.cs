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

        /// <summary>Буквы цветов ткани-основы: эти клетки связаны с начала, вяжется только рисунок.</summary>
        public string fabric = string.Empty;

        /// <summary>Загадка: эскиз рисунка скрыт.</summary>
        public bool mystery;

        /// <summary>Сколько золотых петель разбросать по рисунку.</summary>
        public int gold;

        public int slots = 4;
        public int columns = 4;

        /// <summary>Сколько клубков на поле.</summary>
        public int spools = 4;

        /// <summary>Сколько раз переставить соседние клубки в колонках — чем больше, тем запутаннее очередь.</summary>
        public int swaps;

        public int hidden;
        public int knots;
        public int links;
        public int locks;
        public int rainbow;
        public int thick;
        public int ice;

        /// <summary>Лишние клубки сверх картинки: стоят тапа и места на станке, зато их можно не брать.</summary>
        public int extra;

        /// <summary>Цель по тапам на третью звезду = лучший найденный путь + столько тапов запаса.</summary>
        public int parSlack = 1;

        /// <summary>Коридоры качества решений: доля ходов без выбора и доля «роковых» ходов.</summary>
        public double maxForced = 1.0;
        public double minDanger;
        public double maxDanger = 1.0;

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
