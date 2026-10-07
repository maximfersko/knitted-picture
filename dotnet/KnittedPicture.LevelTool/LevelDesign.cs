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

        /// <summary>Натяжение полотна: на сколько рядов фронт может обогнать самый нижний. 0 — без ограничения.</summary>
        public int band;

        /// <summary>
        /// Натяжение в игре, если оно мягче, чем при нарезке клубков (0 — то же, что band).
        /// Клубки режутся с band, а играется уровень с playBand: раскладка та же, но столбцам свободнее.
        /// </summary>
        public int playBand;

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

        /// <summary>Чужая пряжа: клубки цвета ткани. Взятый на станок — занимает слот насовсем.</summary>
        public int junk;

        /// <summary>Лишние клубки сверх картинки: стоят тапа и места на станке, зато их можно не брать.</summary>
        public int extra;

        /// <summary>Коридоры качества решений: доля ходов без выбора и доля «роковых» ходов.</summary>
        public double maxForced = 1.0;
        public double minDanger;
        public double maxDanger = 1.0;

        /// <summary>Сколько партий может выиграть игрок, который тапает наугад: выше — уровень проходится без мыслей.</summary>
        public double maxRandom = 1.0;

        /// <summary>
        /// Сколько партий «среднего игрока» может оборваться затором, пока не связана и треть картинки.
        /// Сложность пусть живёт ближе к концу: ранний проигрыш ощущается лотереей, поздний — «почти!».
        /// </summary>
        public double maxEarlyJam = 1.0;

        /// <summary>
        /// Коридор побед игрока «по подсветке» (всегда берёт клубок, которому сразу есть работа).
        /// Обычный уровень: честная игра почти всегда проходит (minFollower ≈ 0,7).
        /// Сложный: подсветка одна не спасает (maxFollower ≈ 0,4).
        /// </summary>
        public double minFollower;
        public double maxFollower = 1.0;

        /// <summary>
        /// Коридор побед игрока, который знает приёмы (короткие первыми, узелки заранее).
        /// Сложный уровень: приёмы помогают, но не решают всё (≈ 0,2..0,6) — без «стен» и без уровней на один трюк.
        /// </summary>
        public double minSkilled;
        public double maxSkilled = 1.0;

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
