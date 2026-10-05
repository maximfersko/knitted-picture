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
        /// <summary>Больше слотов не помещается на экран.</summary>
        public const int MaxSlots = 7;

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
            TimeLimit = level.timeLimit;
            TimeLeft = level.timeLimit;

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
                _columns[s.column].Add(new Spool(nextId++, s.color, s.loops, s.hidden, s.link, s.knots, s.locked, s.key));
            }

            _slots = Enumerable.Repeat<Spool?>(null, level.slots).ToList();
            RevealFronts(null);
            UpdateStatus();
        }

        private LoomGame(LoomGame other)
        {
            Id = other.Id;
            Width = other.Width;
            Height = other.Height;
            ColorCount = other.ColorCount;
            TimeLimit = other.TimeLimit;
            TimeLeft = other.TimeLeft;
            TimerStarted = other.TimerStarted;
            _pixels = other._pixels;
            _slots = other._slots.Select(s => s?.Clone()).ToList();
            _columns = other._columns.Select(c => c.Select(s => s.Clone()).ToList()).ToList();
            _knitted = other._knitted;
            _status = other._status;
        }

        public int Id { get; }

        public int Width { get; }

        public int Height { get; }

        public int ColorCount { get; }

        public int TotalStitches => _pixels.Length;

        public int KnittedCount => _knitted;

        public GameStatus Status => _status;

        public IReadOnlyList<Spool?> Slots => _slots;

        public int FreeSlots => _slots.Count(s => s == null);

        public int ColumnCount => _columns.Count;

        public bool CanUndo => _history.Count > 0 && (_status == GameStatus.Playing || _status == GameStatus.Jammed);

        /// <summary>Секунды на уровень, 0 — без моли.</summary>
        public float TimeLimit { get; }

        public float TimeLeft { get; private set; }

        /// <summary>Таймер идёт с первого действия игрока, а не с открытия уровня.</summary>
        public bool TimerStarted { get; private set; }

        /// <summary>Цвет следующей петли или -1, если картинка довязана.</summary>
        public int NeededColor => _knitted < TotalStitches ? ColorAtStitch(_knitted) : -1;

        /// <summary>Колонка поля, передний клубок — с индексом 0.</summary>
        public IReadOnlyList<Spool> Column(int index) => _columns[index];

        /// <summary>Цвет клетки картинки; y = 0 — верхний ряд.</summary>
        public int ColorAt(int x, int y) => _pixels[y * Width + x];

        public bool IsKnitted(int x, int y) => StitchIndex(x, y) < _knitted;

        /// <summary>Порядковый номер петли в вязании: 0 — нижняя левая.</summary>
        public int StitchIndex(int x, int y) => (Height - 1 - y) * Width + x;

        /// <summary>Клетка, которая вяжется сейчас; (-1, -1), если всё довязано.</summary>
        public (int X, int Y) CurrentStitch =>
            _knitted < TotalStitches ? (_knitted % Width, Height - 1 - _knitted / Width) : (-1, -1);

        /// <summary>Цвета ближайших петель по порядку, без повторов подряд — для подсказок и ботов.</summary>
        public List<int> UpcomingColors(int stitches)
        {
            var result = new List<int>();
            var end = Math.Min(TotalStitches, _knitted + stitches);
            for (var i = _knitted; i < end; i++)
            {
                var color = ColorAtStitch(i);
                if (result.Count == 0 || result[result.Count - 1] != color)
                {
                    result.Add(color);
                }
            }

            return result;
        }

        /// <summary>Можно ли сейчас тапнуть колонку обычным тапом.</summary>
        public bool CanTap(int column) => Check(column, 0, false) == RejectReason.None;

        /// <summary>Отправить передний клубок колонки на станок (или развязать его узелок).</summary>
        public MoveResult TapColumn(int column) => Take(column, 0, false);

        /// <summary>
        /// Бустер «Ножницы»: достать клубок с любой глубины колонки.
        /// Ножницы режут узелки, замок и связку — клубок едет на станок один.
        /// </summary>
        public MoveResult PullSpool(int column, int depth) => Take(column, depth, true);

        /// <summary>+1 слот на станке. Отмена хода купленный слот не забирает.</summary>
        public bool AddSlot()
        {
            if (_status == GameStatus.Won || _status == GameStatus.TimeUp || _slots.Count >= MaxSlots)
            {
                return false;
            }

            _slots.Add(null);
            UpdateStatus();
            return true;
        }

        /// <summary>Время идёт только во время игры и только после первого действия.</summary>
        public void Tick(float seconds)
        {
            if (TimeLimit <= 0 || !TimerStarted || _status != GameStatus.Playing)
            {
                return;
            }

            TimeLeft = Math.Max(0f, TimeLeft - seconds);
            if (TimeLeft <= 0f)
            {
                _status = GameStatus.TimeUp;
            }
        }

        /// <summary>Добавить время (спасение после моли).</summary>
        public void AddTime(float seconds)
        {
            if (TimeLimit <= 0 || _status == GameStatus.Won)
            {
                return;
            }

            TimeLeft += seconds;
            if (_status == GameStatus.TimeUp)
            {
                _status = GameStatus.Playing;
                UpdateStatus();
            }
        }

        /// <summary>Отменить последний ход (бустер «Отменить»).</summary>
        public bool Undo()
        {
            if (!CanUndo)
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
            _status = GameStatus.Playing;
            UpdateStatus();
            return true;
        }

        /// <summary>Копия текущего состояния без истории ходов — для подсказки.</summary>
        public LoomGame Clone() => new LoomGame(this);

        /// <summary>Ключ состояния для поиска решения: одинаковые ключи — одинаковое будущее.</summary>
        internal string StateKey()
        {
            var sb = new StringBuilder();
            sb.Append(_knitted).Append('|');
            foreach (var column in _columns)
            {
                sb.Append(column.Count);
                if (column.Count > 0)
                {
                    sb.Append('k').Append(column[0].Knots);
                    if (column[0].Locked)
                    {
                        sb.Append('L');
                    }
                }

                sb.Append(',');
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

        private RejectReason Check(int column, int depth, bool scissors)
        {
            if (_status != GameStatus.Playing)
            {
                return RejectReason.GameOver;
            }

            if (column < 0 || column >= _columns.Count || depth < 0 || depth >= _columns[column].Count)
            {
                return RejectReason.EmptyColumn;
            }

            var spool = _columns[column][depth];
            if (scissors)
            {
                return FreeSlots > 0 ? RejectReason.None : RejectReason.NoFreeSlot;
            }

            if (spool.Locked)
            {
                return RejectReason.Locked;
            }

            if (spool.Knots > 0)
            {
                return RejectReason.None;
            }

            if (spool.Link != 0)
            {
                var partnerColumn = FindPartnerColumn(spool, column);
                if (partnerColumn < 0)
                {
                    return RejectReason.LinkNotReady;
                }

                return FreeSlots >= 2 ? RejectReason.None : RejectReason.NoFreeSlot;
            }

            return FreeSlots > 0 ? RejectReason.None : RejectReason.NoFreeSlot;
        }

        private MoveResult Take(int column, int depth, bool scissors)
        {
            var reason = Check(column, depth, scissors);
            if (reason != RejectReason.None)
            {
                return MoveResult.Rejected(reason, _status);
            }

            SaveSnapshot();
            TimerStarted = true;
            var spool = _columns[column][depth];

            if (!scissors && spool.Knots > 0)
            {
                spool.Knots--;
                UpdateStatus();
                return MoveResult.Knot(spool, _status);
            }

            var placed = new List<Placement>(2);
            if (scissors)
            {
                spool.Knots = 0;
                spool.Locked = false;
                CutLink(spool);
                _columns[column].RemoveAt(depth);
                placed.Add(new Placement(spool, column, depth, PutOnLoom(spool)));
            }
            else if (spool.Link != 0)
            {
                var partnerColumn = FindPartnerColumn(spool, column);
                var partner = _columns[partnerColumn][0];
                _columns[column].RemoveAt(0);
                _columns[partnerColumn].RemoveAt(0);
                placed.Add(new Placement(spool, column, 0, PutOnLoom(spool)));
                placed.Add(new Placement(partner, partnerColumn, 0, PutOnLoom(partner)));
            }
            else
            {
                _columns[column].RemoveAt(0);
                placed.Add(new Placement(spool, column, 0, PutOnLoom(spool)));
            }

            var unlocked = new List<int>();
            if (placed.Any(p => p.Spool.IsKey))
            {
                foreach (var s in _columns.SelectMany(c => c).Where(s => s.Locked))
                {
                    s.Locked = false;
                    unlocked.Add(s.Id);
                }
            }

            var revealed = new List<int>();
            RevealFronts(revealed);
            var knitted = KnitWhilePossible();
            UpdateStatus();
            return MoveResult.Place(placed, knitted, unlocked, revealed, _status);
        }

        private int PutOnLoom(Spool spool)
        {
            var slot = _slots.IndexOf(null);
            _slots[slot] = spool;
            spool.Revealed = true;
            return slot;
        }

        private int FindPartnerColumn(Spool spool, int column)
        {
            for (var c = 0; c < _columns.Count; c++)
            {
                if (c == column || _columns[c].Count == 0)
                {
                    continue;
                }

                var front = _columns[c][0];
                if (front.Link == spool.Link && !front.Locked && front.Knots == 0)
                {
                    return c;
                }
            }

            return -1;
        }

        private void CutLink(Spool spool)
        {
            if (spool.Link == 0)
            {
                return;
            }

            var link = spool.Link;
            foreach (var s in _columns.SelectMany(c => c).Where(s => s.Link == link))
            {
                s.Link = 0;
            }

            spool.Link = 0;
        }

        private void RevealFronts(List<int>? revealed)
        {
            foreach (var column in _columns)
            {
                if (column.Count > 0 && !column[0].IsColorVisible)
                {
                    column[0].Revealed = true;
                    revealed?.Add(column[0].Id);
                }
            }
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
            if (_status == GameStatus.TimeUp)
            {
                return;
            }

            if (_knitted == TotalStitches)
            {
                _status = GameStatus.Won;
            }
            else
            {
                _status = CanAct() ? GameStatus.Playing : GameStatus.Jammed;
            }
        }

        /// <summary>Есть ли хоть одно действие без бустеров.</summary>
        private bool CanAct()
        {
            var free = FreeSlots;
            if (free == 0)
            {
                return false;
            }

            for (var c = 0; c < _columns.Count; c++)
            {
                if (_columns[c].Count == 0)
                {
                    continue;
                }

                var front = _columns[c][0];
                if (front.Locked)
                {
                    continue;
                }

                if (front.Knots > 0 || front.Link == 0)
                {
                    return true;
                }

                if (free >= 2 && FindPartnerColumn(front, c) >= 0)
                {
                    return true;
                }
            }

            return false;
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

        private void SaveSnapshot()
        {
            _history.Push(new Snapshot(
                _slots.Select(s => s?.Clone()).ToList(),
                _columns.Select(c => c.Select(s => s.Clone()).ToList()).ToList(),
                _knitted));
        }

        private sealed class Snapshot
        {
            public Snapshot(List<Spool?> slots, List<List<Spool>> columns, int knitted)
            {
                Slots = slots;
                Columns = columns;
                Knitted = knitted;
            }

            public List<Spool?> Slots { get; }

            public List<List<Spool>> Columns { get; }

            public int Knitted { get; }
        }
    }
}
