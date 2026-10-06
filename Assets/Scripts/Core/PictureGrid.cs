#nullable enable

using System;

namespace KnittedPicture.Core
{
    /// <summary>
    /// Картинка и её фронт вязки. Каждый столбец вяжется снизу вверх, клетки ткани-основы
    /// (канва) связаны с самого начала и пропускаются — фронт столбца это самая нижняя
    /// несвязанная клетка рисунка. Одни и те же правила для игры, генератора уровней и ботов.
    /// </summary>
    public sealed class PictureGrid
    {
        /// <summary>Любой цвет: так ищет работу радужный клубок.</summary>
        public const int AnyColor = -1;

        private readonly int[] _pixels;
        private readonly bool[] _fabric;

        /// <summary>Для каждого столбца — ряды клеток рисунка снизу вверх.</summary>
        private readonly int[][] _order;
        private readonly int[] _rank;
        private readonly int[] _totalByColor;
        private int[] _done;
        private int[] _leftByColor;

        public PictureGrid(int width, int height, string[] picture, string[]? canvas, int band = 0)
        {
            Width = width;
            Height = height;
            Band = band;
            _pixels = new int[width * height];
            _fabric = new bool[width * height];
            var colors = 0;
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var color = picture[y][x] - '0';
                    _pixels[y * width + x] = color;
                    colors = Math.Max(colors, color + 1);
                    _fabric[y * width + x] = canvas != null && canvas.Length == height && canvas[y][x] == '1';
                }
            }

            _totalByColor = new int[colors];
            _rank = new int[width * height];
            _order = new int[width][];
            for (var x = 0; x < width; x++)
            {
                var count = 0;
                for (var y = 0; y < height; y++)
                {
                    if (!_fabric[y * width + x])
                    {
                        count++;
                    }
                }

                _order[x] = new int[count];
                var i = 0;
                for (var y = height - 1; y >= 0; y--)
                {
                    var cell = y * width + x;
                    if (_fabric[cell])
                    {
                        _rank[cell] = -1;
                        continue;
                    }

                    _rank[cell] = i;
                    _order[x][i++] = y;
                    _totalByColor[_pixels[cell]]++;
                    Total++;
                }
            }

            _done = new int[width];
            _leftByColor = (int[])_totalByColor.Clone();
        }

        private PictureGrid(PictureGrid other)
        {
            Band = other.Band;
            Width = other.Width;
            Height = other.Height;
            Total = other.Total;
            _pixels = other._pixels;
            _fabric = other._fabric;
            _order = other._order;
            _rank = other._rank;
            _totalByColor = other._totalByColor;
            _done = (int[])other._done.Clone();
            _leftByColor = (int[])other._leftByColor.Clone();
            Knitted = other.Knitted;
        }

        public int Width { get; }

        public int Height { get; }

        /// <summary>
        /// Натяжение полотна: столбец вяжется, только если его фронт не выше самого нижнего
        /// фронта больше чем на столько рядов. 0 — без ограничения.
        /// </summary>
        public int Band { get; }

        /// <summary>Сколько клеток рисунка нужно связать (без ткани).</summary>
        public int Total { get; }

        public int Knitted { get; private set; }

        public bool IsComplete => Knitted == Total;

        public static PictureGrid From(LevelData level) => new PictureGrid(level.width, level.height, level.picture, level.canvas, level.band);

        public PictureGrid Clone() => new PictureGrid(this);

        public int ColorAt(int x, int y) => _pixels[y * Width + x];

        /// <summary>Клетка ткани-основы: связана с начала, клубки её не вяжут.</summary>
        public bool IsFabric(int x, int y) => _fabric[y * Width + x];

        public bool IsKnitted(int x, int y)
        {
            var rank = _rank[y * Width + x];
            return rank < 0 || rank < _done[x];
        }

        /// <summary>Сколько клеток рисунка связано в столбце.</summary>
        public int Done(int x) => _done[x];

        /// <summary>Ряд фронта столбца: следующая клетка, которая свяжется. -1 — столбец готов.</summary>
        public int FrontY(int x) => _done[x] < _order[x].Length ? _order[x][_done[x]] : -1;

        /// <summary>Самый нижний ряд среди фронтов (наибольший y); -1 — всё связано.</summary>
        public int LowestFront()
        {
            var lowest = -1;
            for (var x = 0; x < Width; x++)
            {
                lowest = Math.Max(lowest, FrontY(x));
            }

            return lowest;
        }

        /// <summary>Фронт столбца, если его можно вязать прямо сейчас (с учётом натяжения), иначе -1.</summary>
        public int ActiveFrontY(int x) => ActiveFrontY(x, Band > 0 ? LowestFront() : -1);

        private int ActiveFrontY(int x, int lowest)
        {
            var y = FrontY(x);
            return y >= 0 && (Band <= 0 || y >= lowest - Band) ? y : -1;
        }

        /// <summary>Ряд клетки, которая свяжется в столбце через depth петель после фронта; -1 — таких нет.</summary>
        public int AheadY(int x, int depth)
        {
            var i = _done[x] + depth;
            return i < _order[x].Length ? _order[x][i] : -1;
        }

        /// <summary>Сколько петель этого цвета ещё не связано.</summary>
        public int Left(int color) => color >= 0 && color < _leftByColor.Length ? _leftByColor[color] : 0;

        /// <summary>Сколько клеток этого цвета в рисунке (без ткани).</summary>
        public int TotalOf(int color) => color >= 0 && color < _totalByColor.Length ? _totalByColor[color] : 0;

        /// <summary>
        /// Столбец, где цвет на фронте: самый низкий фронт, при равенстве — левый. -1 — нигде.
        /// <see cref="AnyColor"/> — самый низкий фронт любого цвета.
        /// </summary>
        public int FindTarget(int color)
        {
            var best = -1;
            var bestY = -1;
            var lowest = Band > 0 ? LowestFront() : -1;
            for (var x = 0; x < Width; x++)
            {
                var y = ActiveFrontY(x, lowest);
                if (y < 0 || (color != AnyColor && _pixels[y * Width + x] != color))
                {
                    continue;
                }

                if (y > bestY)
                {
                    best = x;
                    bestY = y;
                }
            }

            return best;
        }

        /// <summary>Связать фронт столбца; возвращает ряд связанной клетки.</summary>
        public int Knit(int x)
        {
            var y = FrontY(x);
            _done[x]++;
            _leftByColor[_pixels[y * Width + x]]--;
            Knitted++;
            return y;
        }

        internal int[] SaveState() => (int[])_done.Clone();

        internal void LoadState(int[] done)
        {
            _done = (int[])done.Clone();
            _leftByColor = (int[])_totalByColor.Clone();
            Knitted = 0;
            for (var x = 0; x < Width; x++)
            {
                for (var i = 0; i < _done[x]; i++)
                {
                    _leftByColor[_pixels[_order[x][i] * Width + x]]--;
                }

                Knitted += _done[x];
            }
        }

        internal void AppendState(System.Text.StringBuilder sb)
        {
            foreach (var d in _done)
            {
                sb.Append(d).Append('.');
            }
        }
    }
}
