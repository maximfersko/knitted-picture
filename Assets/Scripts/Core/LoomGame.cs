#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KnittedPicture.Core
{
    /// <summary>
    /// Правила игры без движка. Игрок отправляет передние клубки колонок на станок,
    /// станок сам вяжет картинку: нижний ряд первым, каждый ряд слева направо.
    /// Каждая петля берёт одну единицу нитки из клубка своего цвета.
    /// </summary>
    public sealed class LoomGame
    {
        private readonly int[] _pixels;
        private readonly Stack<Snapshot> _history = new Stack<Snapshot>();
        private List<Spool?> _slots;
        private List<List<Spool>> _columns;
        private int _knitted;
        private GameStatus _status = GameStatus.Playing;

        public LoomGame(LevelData level)
        {
            var errors = LevelValidator.Validate(level);
            if (errors.Count > 0)
            {
                throw new ArgumentException("Уровень некорректен: " + string.Join("; ", errors), nameof(level));
            }

            Id = level.id;
            Width = level.width;
            Height = level.height;
            ColorCount = level.colors.Length;

            _pixels = new int[Width * Height];
            for (var y = 0; y < Height; y++)
            {
                for (var x = 0; x < Width; x++)
                {
                    _pixels[y * Width + x] = level.picture[y][x] - '0';
                }
            }

            var columnCount = level.spools.Max(s => s.column) + 1;
            _columns = new List<List<Spool>>(columnCount);
            for (var i = 0; i < columnCount; i++)
            {
                _columns.Add(new List<Spool>());
            }

            var nextId = 0;
            foreach (var s in level.spools)
            {
                _columns[s.column].Add(new Spool(nextId++, s.color, s.loops));
            }

            _slots = Enumerable.Repeat<Spool?>(null, level.slots).ToList();
        }

        public int Id { get; }

        public int Width { get; }

        public int Height { get; }

        public int ColorCount { get; }

        public int TotalStitches => _pixels.Length;

        public int KnittedCount => _knitted;

        public GameStatus Status => _status;

        public IReadOnlyList<Spool?> Slots => _slots;

        public int ColumnCount => _columns.Count;

        public bool CanUndo => _history.Count > 0;

        /// <summary>Цвет следующей петли или -1, если картинка довязана.</summary>
        public int NeededColor => _knitted < TotalStitches ? ColorAtStitch(_knitted) : -1;

        /// <summary>Колонка поля, передний клубок — с индексом 0.</summary>
        public IReadOnlyList<Spool> Column(int index) => _columns[index];

        /// <summary>Цвет клетки картинки; y = 0 — верхний ряд.</summary>
        public int ColorAt(int x, int y) => _pixels[y * Width + x];

        public bool IsKnitted(int x, int y) => StitchIndex(x, y) < _knitted;

        /// <summary>Отправить передний клубок колонки в первый свободный слот.</summary>
        public MoveResult TapColumn(int column) => PullSpool(column, 0);

        /// <summary>
        /// Достать клубок с любой глубины колонки (бустер «Ножницы»).
        /// depth = 0 — обычный тап по переднему клубку.
        /// </summary>
        public MoveResult PullSpool(int column, int depth)
        {
            if (_status != GameStatus.Playing
                || column < 0 || column >= _columns.Count
                || depth < 0 || depth >= _columns[column].Count)
            {
                return MoveResult.Rejected(_status);
            }

            var slot = _slots.IndexOf(null);
            if (slot < 0)
            {
                return MoveResult.Rejected(_status);
            }

            SaveSnapshot();
            var spool = _columns[column][depth];
            _columns[column].RemoveAt(depth);
            _slots[slot] = spool;

            var knitted = KnitWhilePossible();
            UpdateStatus();
            return new MoveResult(true, spool, slot, knitted, _status);
        }

        /// <summary>
        /// +1 слот на станке (бустер или спасение при заторе).
        /// Отмена хода купленный слот не забирает.
        /// </summary>
        public bool AddSlot()
        {
            if (_status == GameStatus.Won)
            {
                return false;
            }

            _slots.Add(null);
            if (_status == GameStatus.Jammed)
            {
                _status = GameStatus.Playing;
            }

            return true;
        }

        /// <summary>Отменить последний ход (бустер «Отменить»).</summary>
        public bool Undo()
        {
            if (_history.Count == 0)
            {
                return false;
            }

            var snapshot = _history.Pop();
            var slots = snapshot.Slots;
            while (slots.Count < _slots.Count)
            {
                slots.Add(null);
            }

            _slots = slots;
            _columns = snapshot.Columns;
            _knitted = snapshot.Knitted;
            _status = snapshot.Status;
            return true;
        }

        /// <summary>Ключ состояния для поиска решения: одинаковые ключи — одинаковое будущее.</summary>
        internal string StateKey()
        {
            var sb = new StringBuilder();
            sb.Append(_knitted).Append('|');
            foreach (var column in _columns)
            {
                sb.Append(column.Count).Append(',');
            }

            sb.Append('|');
            foreach (var spool in _slots)
            {
                if (spool == null)
                {
                    sb.Append('_');
                }
                else
                {
                    sb.Append(spool.Color).Append(':').Append(spool.Loops);
                }

                sb.Append(',');
            }

            return sb.ToString();
        }

        private List<Stitch> KnitWhilePossible()
        {
            var result = new List<Stitch>();
            while (_knitted < TotalStitches)
            {
                var color = ColorAtStitch(_knitted);
                var slot = FindSlotWithColor(color);
                if (slot < 0)
                {
                    break;
                }

                var spool = _slots[slot]!;
                spool.Loops--;
                var x = _knitted % Width;
                var y = Height - 1 - _knitted / Width;
                result.Add(new Stitch(x, y, color, slot));
                _knitted++;

                if (spool.Loops == 0)
                {
                    _slots[slot] = null;
                }
            }

            return result;
        }

        private void UpdateStatus()
        {
            if (_knitted == TotalStitches)
            {
                _status = GameStatus.Won;
            }
            else if (_slots.IndexOf(null) < 0 || _columns.All(c => c.Count == 0))
            {
                _status = GameStatus.Jammed;
            }
            else
            {
                _status = GameStatus.Playing;
            }
        }

        private int FindSlotWithColor(int color)
        {
            for (var i = 0; i < _slots.Count; i++)
            {
                if (_slots[i]?.Color == color)
                {
                    return i;
                }
            }

            return -1;
        }

        private int ColorAtStitch(int index)
        {
            var x = index % Width;
            var y = Height - 1 - index / Width;
            return _pixels[y * Width + x];
        }

        private int StitchIndex(int x, int y) => (Height - 1 - y) * Width + x;

        private void SaveSnapshot()
        {
            _history.Push(new Snapshot(
                _slots.Select(s => s?.Clone()).ToList(),
                _columns.Select(c => c.Select(s => s.Clone()).ToList()).ToList(),
                _knitted,
                _status));
        }

        private sealed class Snapshot
        {
            public Snapshot(List<Spool?> slots, List<List<Spool>> columns, int knitted, GameStatus status)
            {
                Slots = slots;
                Columns = columns;
                Knitted = knitted;
                Status = status;
            }

            public List<Spool?> Slots { get; }

            public List<List<Spool>> Columns { get; }

            public int Knitted { get; }

            public GameStatus Status { get; }
        }
    }
}
