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

        /// <summary>Название картинки для альбома: «Сердечко», «Котик».</summary>
        public string name = string.Empty;

        /// <summary>Подсказка на старте уровня; пустая — без подсказки.</summary>
        public string tip = string.Empty;

        /// <summary>Название на языке игры (в файле уровня — русское).</summary>
        public string Title => L.T(name);

        /// <summary>Подсказка на языке игры.</summary>
        public string Tip => L.T(tip);

        /// <summary>Сложный уровень: плашка на старте и двойная награда.</summary>
        public bool hard;

        /// <summary>Секунды до того, как моль доберётся до картинки; 0 — без таймера.</summary>
        public int timeLimit;

        public int width;
        public int height;

        /// <summary>Цвета пряжи в hex. Индекс цвета — цифра в <see cref="picture"/>.</summary>
        public string[] colors = Array.Empty<string>();

        /// <summary>Картинка сверху вниз: строка на ряд, символ '0'..'9' — индекс цвета.</summary>
        public string[] picture = Array.Empty<string>();

        /// <summary>
        /// Канва: строки как у картинки, '1' — клетка ткани-основы, она связана с самого начала;
        /// '0' — клетка рисунка, её вяжут клубки. Пусто — вязать всю картинку.
        /// </summary>
        public string[] canvas = Array.Empty<string>();

        /// <summary>Золотые петли: индексы клеток (ряд × ширина + столбец). Бонус, если связать их во время комбо.</summary>
        public int[] gold = Array.Empty<int>();

        /// <summary>Загадка: эскиз рисунка скрыт, видно только фронт и силуэт на ткани.</summary>
        public bool mystery;

        /// <summary>
        /// Натяжение полотна: фронт столбца вяжется, только если он не выше самого нижнего фронта
        /// больше чем на столько рядов. Картинка растёт «полосой», порядок цветов важен. 0 — без ограничения.
        /// </summary>
        public int band;

        /// <summary>Сколько слотов на станке в начале уровня.</summary>
        public int slots = 4;

        /// <summary>Клубки на поле. Внутри колонки первый в списке — передний, его можно тапнуть.</summary>
        public SpoolData[] spools = Array.Empty<SpoolData>();
    }

    [Serializable]
    public sealed class SpoolData
    {
        public int column;
        public int color;
        public int loops;

        /// <summary>Цвет скрыт («?»), пока клубок не окажется впереди.</summary>
        public bool hidden;

        /// <summary>Номер связки: два клубка с одинаковым номером едут на станок только вместе. 0 — без связки.</summary>
        public int link;

        /// <summary>Узелки: каждый тап развязывает один, клубок едет, когда узелков не осталось.</summary>
        public int knots;

        /// <summary>Под замком: не тапается, пока на станок не встанет клубок с ключом.</summary>
        public bool locked;

        /// <summary>Клубок с ключом: когда встаёт на станок, открывает все замки.</summary>
        public bool key;

        /// <summary>Радужный: вяжет любой цвет на фронте. Поле color не используется.</summary>
        public bool rainbow;

        /// <summary>Толстый: занимает 2 слота и вяжет по 2 петли за шаг.</summary>
        public bool thick;

        /// <summary>Ледяной: не тапается, пока станок не свяжет столько петель его цвета. 0 — без льда.</summary>
        public int ice;
    }
}
