#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KnittedPicture.Core
{
    /// <summary>
    /// Правила игры без движка.
    /// Картинка вяжется столбиками снизу вверх: у каждого столбца свой фронт — самая нижняя
    /// несвязанная клетка рисунка (ткань-основа связана с начала). Игрок отправляет клубки
    /// на станок, а станок шаг за шагом вяжет: за шаг каждый клубок делает петлю своего цвета
    /// на самом низком фронте, где этот цвет есть. Так одновременно нужны несколько цветов,
    /// а клубки помогают друг другу, открывая новые клетки. Пряжи может быть больше,
    /// чем нужно: когда цвет в картинке кончается, лишние клубки уходят со станка.
    /// </summary>
    public sealed class LoomGame
    {
        /// <summary>Больше слотов не помещается на экран.</summary>
        public const int MaxSlots = 7;

        /// <summary>Сколько шагов клубок «летит» на станок, прежде чем начать вязать.</summary>
        public const int WarmupSteps = 3;

        private readonly PictureGrid _grid;
        private readonly bool[] _gold;
        private readonly Stack<Snapshot> _history = new Stack<Snapshot>();
        private List<Spool?> _slots;
        private List<List<Spool>> _columns;
        private GameStatus _status = GameStatus.Playing;

        public LoomGame(LevelData level)
        {
            var errors = LevelValidator.Validate(level);
            if (errors.Count > 0)
            {
                throw new ArgumentException("Уровень некорректен: " + string.Join("; ", errors), nameof(level));
            }

            Id = level.id;
            ColorCount = level.colors.Length;
            TimeLimit = level.timeLimit;
            TimeLeft = level.timeLimit;
            Mystery = level.mystery;
            _grid = PictureGrid.From(level);
            _gold = new bool[level.width * level.height];
            foreach (var cell in level.gold ?? Array.Empty<int>())
            {
                _gold[cell] = true;
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
                _columns[s.column].Add(new Spool(nextId++, s.color, s.loops, s.hidden, s.link, s.knots, s.locked, s.key, s.rainbow, s.thick, s.ice));
            }

            _slots = Enumerable.Repeat<Spool?>(null, level.slots).ToList();
            RevealFronts(null);
            Reevaluate();
        }

        private LoomGame(LoomGame other)
        {
            Id = other.Id;
            ColorCount = other.ColorCount;
            TimeLimit = other.TimeLimit;
            TimeLeft = other.TimeLeft;
            TimerStarted = other.TimerStarted;
            Mystery = other.Mystery;
            Taps = other.Taps;
            _grid = other._grid.Clone();
            _gold = other._gold;
            _slots = CloneSlots(other._slots);
            _columns = other._columns.Select(c => c.Select(s => s.Clone()).ToList()).ToList();
            _status = other._status;
        }

        public int Id { get; }

        public int Width => _grid.Width;

        public int Height => _grid.Height;

        public int ColorCount { get; }

        /// <summary>Сколько петель нужно связать (клетки ткани не в счёт).</summary>
        public int TotalStitches => _grid.Total;

        public int KnittedCount => _grid.Knitted;

        public GameStatus Status => _status;

        /// <summary>Слоты станка. Толстый клубок стоит в двух слотах сразу — см. <see cref="IsTail"/>.</summary>
        public IReadOnlyList<Spool?> Slots => _slots;

        public int FreeSlots => _slots.Count(s => s == null);

        public int ColumnCount => _columns.Count;

        public bool CanUndo => _history.Count > 0 && (_status == GameStatus.Playing || _status == GameStatus.Jammed);

        /// <summary>Секунды на уровень, 0 — без моли.</summary>
        public float TimeLimit { get; }

        public float TimeLeft { get; private set; }

        /// <summary>Таймер идёт с первого действия игрока, а не с открытия уровня.</summary>
        public bool TimerStarted { get; private set; }

        /// <summary>Эскиз рисунка скрыт — картинка-загадка.</summary>
        public bool Mystery { get; }

        /// <summary>Принятые тапы: клубок на станок, узелок, ножницы.</summary>
        public int Taps { get; private set; }

        /// <summary>Есть ли на станке работа: клубок долетает, может связать петлю или уходит как лишний.</summary>
        public bool IsBusy => _slots.Any(s => s != null && (s.Warmup > 0 || HasWork(s) || IsSurplus(s)));

        /// <summary>Колонка поля, передний клубок — с индексом 0.</summary>
        public IReadOnlyList<Spool> Column(int index) => _columns[index];

        /// <summary>Цвет клетки картинки; y = 0 — верхний ряд.</summary>
        public int ColorAt(int x, int y) => _grid.ColorAt(x, y);

        /// <summary>Клетка ткани-основы: связана с начала уровня.</summary>
        public bool IsFabric(int x, int y) => _grid.IsFabric(x, y);

        public bool IsGold(int x, int y) => _gold[y * Width + x];

        /// <summary>Сколько петель рисунка связано в столбце картинки.</summary>
        public int ColumnHeight(int x) => _grid.Done(x);

        public bool IsKnitted(int x, int y) => _grid.IsKnitted(x, y);

        /// <summary>Ряд фронта столбца: следующая клетка, которая свяжется. -1 — столбец готов.</summary>
        public int FrontY(int x) => _grid.FrontY(x);

        /// <summary>Ряд клетки, которая свяжется после фронта через depth петель; -1 — таких нет.</summary>
        public int AheadY(int x, int depth) => _grid.AheadY(x, depth);

        /// <summary>Сколько петель этого цвета ещё не связано.</summary>
        public int LeftOf(int color) => _grid.Left(color);

        /// <summary>Второй слот толстого клубка: в нём тот же клубок, что и в предыдущем занятом им слоте.</summary>
        public bool IsTail(int slot)
        {
            var spool = _slots[slot];
            return spool != null && _slots.IndexOf(spool) != slot;
        }

        /// <summary>Второй слот, который занимает клубок из слота slot; -1 — клубок обычный.</summary>
        public int TailOf(int slot)
        {
            var spool = _slots[slot];
            return spool != null && spool.Thick ? _slots.LastIndexOf(spool) : -1;
        }

        /// <summary>Клубку прямо сейчас есть работа на фронте.</summary>
        public bool HasWork(Spool spool) =>
            spool.Rainbow ? !_grid.IsComplete : _grid.FindTarget(spool.Color) >= 0;

        /// <summary>Лишняя пряжа: этот цвет был в рисунке, но уже довязан — клубок уйдёт со станка.</summary>
        public bool IsSurplus(Spool spool) => !spool.Rainbow && _grid.TotalOf(spool.Color) > 0 && _grid.Left(spool.Color) == 0;

        /// <summary>
        /// Чужая пряжа: этого цвета в рисунке нет вовсе (например, цвет ткани).
        /// На станке такой клубок занимает слот, пока его не срежут ножницами.
        /// </summary>
        public bool IsForeign(Spool spool) => !spool.Rainbow && _grid.TotalOf(spool.Color) == 0;

        /// <summary>Цвета, которые прямо сейчас можно вязать хоть в одном столбце.</summary>
        public HashSet<int> FrontColors()
        {
            var result = new HashSet<int>();
            for (var x = 0; x < Width; x++)
            {
                var y = _grid.ActiveFrontY(x);
                if (y >= 0)
                {
                    result.Add(_grid.ColorAt(x, y));
                }
            }

            return result;
        }

        /// <summary>Фронт столбца, если его можно вязать прямо сейчас (натяжение полотна), иначе -1.</summary>
        public int ActiveFrontY(int x) => _grid.ActiveFrontY(x);

        /// <summary>Натяжение полотна: на сколько рядов фронт может обогнать самый нижний. 0 — без ограничения.</summary>
        public int Band => _grid.Band;

        /// <summary>Ряд самого отстающего фронта (самый нижний); -1 — картинка готова.</summary>
        public int LowestFront => _grid.LowestFront();

        /// <summary>Столбец ждёт соседей: фронт есть, но натяжение не даёт его вязать.</summary>
        public bool IsHeld(int x) => _grid.FrontY(x) >= 0 && _grid.ActiveFrontY(x) < 0;

        /// <summary>Цвета на фронте и сразу за ним — для подсказок и ботов.</summary>
        public HashSet<int> SoonColors(int depth)
        {
            var result = new HashSet<int>();
            for (var x = 0; x < Width; x++)
            {
                for (var d = 0; d < depth; d++)
                {
                    var y = _grid.AheadY(x, d);
                    if (y >= 0)
                    {
                        result.Add(_grid.ColorAt(x, y));
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Совет после затора — самая конкретная причина, которую игрок мог обойти:
        /// места есть, но всё впереди заблокировано; чужая пряжа на станке, клубки ждут натяжения,
        /// впереди узелки; иначе общее «короткие первыми».
        /// Боты показали, что эти приёмы и отличают знающего игрока от того, кто просто идёт по подсветке.
        /// </summary>
        public JamAdvice Advice()
        {
            if (FreeSlots > 0)
            {
                return JamAdvice.Blocked;
            }

            var onLoom = _slots.Where(s => s != null).Select(s => s!).Distinct().ToList();
            if (onLoom.Any(IsForeign))
            {
                return JamAdvice.ForeignYarn;
            }

            if (Band > 0 && onLoom.Any(s => !s.Rainbow && WaitsForTension(s.Color)))
            {
                return JamAdvice.Tension;
            }

            if (_columns.Any(c => c.Count > 0 && c[0].Knots > 0))
            {
                return JamAdvice.Knots;
            }

            return JamAdvice.ShortFirst;
        }

        /// <summary>Цвет стоит на фронте столбца, который держит натяжение полотна.</summary>
        private bool WaitsForTension(int color)
        {
            for (var x = 0; x < Width; x++)
            {
                if (IsHeld(x) && _grid.ColorAt(x, _grid.FrontY(x)) == color)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Затор: поможет ли ещё один слот (иначе спасение «+1 слот» бесполезно).</summary>
        public bool SlotWouldHelp => _slots.Count < MaxSlots && !_grid.IsComplete && CanAct(FreeSlots + 1);

        /// <summary>
        /// Затор: помогут ли ножницы — достать клубок с поля (хоть замороженный или под замком)
        /// или срезать клубок со станка, чтобы освободить место для обычного тапа.
        /// </summary>
        public bool ScissorsWouldHelp
        {
            get
            {
                if (_grid.IsComplete || _columns.All(c => c.Count == 0))
                {
                    return false;
                }

                var free = FreeSlots;
                return _columns.Any(c => c.Any(s => s.Size <= free)) || (free < _slots.Count && CanAct(free + 1));
            }
        }

        /// <summary>Можно ли сейчас тапнуть колонку обычным тапом.</summary>
        public bool CanTap(int column) => Check(column, 0, false) == RejectReason.None;

        /// <summary>Отправить передний клубок колонки на станок (или развязать его узелок).</summary>
        public MoveResult TapColumn(int column) => Take(column, 0, false);

        /// <summary>
        /// Бустер «Ножницы»: достать клубок с любой глубины колонки.
        /// Ножницы режут узелки, замок, лёд и связку — клубок едет на станок один.
        /// Работают и при заторе: игра продолжается.
        /// </summary>
        public MoveResult PullSpool(int column, int depth) => Take(column, depth, true);

        /// <summary>
        /// Один шаг вязки: каждый клубок на станке (по порядку слотов) делает петлю своего цвета
        /// в самом низком столбце, где этот цвет на фронте (толстый — две, радужный — любого цвета).
        /// Пустые клубки уходят; лишние — тоже, когда их цвет в картинке кончился.
        /// </summary>
        public StepResult Step()
        {
            if (_status != GameStatus.Playing)
            {
                return new StepResult(Array.Empty<Stitch>(), Array.Empty<Completion>(), Array.Empty<int>(), false, _status);
            }

            var stitches = new List<Stitch>();
            var completed = new List<Completion>();
            var thawed = new List<int>();
            var progress = false;
            for (var i = 0; i < _slots.Count; i++)
            {
                var spool = _slots[i];
                if (spool == null || IsTail(i))
                {
                    continue;
                }

                if (spool.Warmup > 0)
                {
                    spool.Warmup--;
                    progress = true;
                    continue;
                }

                if (IsSurplus(spool))
                {
                    completed.Add(new Completion(spool.Id, i, TailOf(i), false, spool.Loops));
                    Free(spool);
                    progress = true;
                    continue;
                }

                var knitted = false;
                for (var k = 0; k < spool.Size && spool.Loops > 0; k++)
                {
                    var x = _grid.FindTarget(spool.Rainbow ? PictureGrid.AnyColor : spool.Color);
                    if (x < 0)
                    {
                        break;
                    }

                    var y = _grid.Knit(x);
                    var color = _grid.ColorAt(x, y);
                    spool.Loops--;
                    stitches.Add(new Stitch(x, y, color, i, spool.Id));
                    Thaw(color, thawed);
                    knitted = true;
                }

                if (!knitted)
                {
                    spool.Stalled = true;
                    continue;
                }

                progress = true;
                if (spool.Loops == 0)
                {
                    completed.Add(new Completion(spool.Id, i, TailOf(i), !spool.Stalled, 0));
                    Free(spool);
                }
            }

            if (_grid.IsComplete)
            {
                _status = GameStatus.Won;
            }
            else if (!progress && !CanAct())
            {
                _status = GameStatus.Jammed;
            }

            return new StepResult(stitches, completed, thawed, progress, _status);
        }

        /// <summary>Вязать, пока станок не встанет. Для решателя, ботов и тестов.</summary>
        public void Settle()
        {
            while (_status == GameStatus.Playing && Step().Progress)
            {
            }
        }

        /// <summary>+1 слот на станке. Отмена хода купленный слот не забирает.</summary>
        public bool AddSlot()
        {
            if (_status == GameStatus.Won || _status == GameStatus.TimeUp || _slots.Count >= MaxSlots)
            {
                return false;
            }

            _slots.Add(null);
            Reevaluate();
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
                Reevaluate();
            }
        }

        /// <summary>
        /// Ножницы по станку: срезать клубок со слота (например, чужую пряжу).
        /// Можно и при заторе — тогда игра продолжается.
        /// </summary>
        public bool CutFromLoom(int slot)
        {
            if ((_status != GameStatus.Playing && _status != GameStatus.Jammed) || slot < 0 || slot >= _slots.Count || _slots[slot] == null)
            {
                return false;
            }

            SaveSnapshot();
            Free(_slots[slot]!);
            _status = GameStatus.Playing;
            Reevaluate();
            return true;
        }

        /// <summary>Отменить последний ход (бустер «Отменить»): вместе с ним откатывается и связанное после него.</summary>
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
            _grid.LoadState(snapshot.Done);
            Taps = snapshot.Taps;
            _status = GameStatus.Playing;
            Reevaluate();
            return true;
        }

        /// <summary>Копия текущего состояния без истории ходов — для подсказки.</summary>
        public LoomGame Clone() => new LoomGame(this);

        /// <summary>Ключ состояния для поиска решения (после Settle): одинаковые ключи — одинаковое будущее.</summary>
        internal string StateKey()
        {
            var sb = new StringBuilder();
            _grid.AppendState(sb);
            sb.Append('|');
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

                for (var d = 0; d < column.Count; d++)
                {
                    if (column[d].Ice > 0)
                    {
                        sb.Append('i').Append(d).Append(':').Append(column[d].Ice);
                    }
                }

                sb.Append(',');
            }

            sb.Append('|');
            for (var i = 0; i < _slots.Count; i++)
            {
                var spool = _slots[i];
                if (spool == null)
                {
                    sb.Append('_');
                }
                else if (IsTail(i))
                {
                    sb.Append('T');
                }
                else
                {
                    sb.Append(spool.Rainbow ? "R" : spool.Color.ToString()).Append(':').Append(spool.Loops);
                }

                sb.Append(',');
            }

            return sb.ToString();
        }

        private RejectReason Check(int column, int depth, bool scissors)
        {
            if (_status != GameStatus.Playing && !(scissors && _status == GameStatus.Jammed))
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
                return FreeSlots >= spool.Size ? RejectReason.None : RejectReason.NoFreeSlot;
            }

            if (spool.Locked)
            {
                return RejectReason.Locked;
            }

            if (spool.Frozen)
            {
                return RejectReason.Frozen;
            }

            if (spool.Knots > 0)
            {
                return RejectReason.None;
            }

            if (spool.Link != 0)
            {
                if (FindPartnerColumn(spool, column) < 0)
                {
                    return RejectReason.LinkNotReady;
                }

                return FreeSlots >= 2 ? RejectReason.None : RejectReason.NoFreeSlot;
            }

            return FreeSlots >= spool.Size ? RejectReason.None : RejectReason.NoFreeSlot;
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
            Taps++;
            var spool = _columns[column][depth];

            if (!scissors && spool.Knots > 0)
            {
                spool.Knots--;
                return MoveResult.Knot(spool, _status);
            }

            var placed = new List<Placement>(2);
            if (scissors)
            {
                _status = GameStatus.Playing;
                spool.Knots = 0;
                spool.Locked = false;
                spool.Ice = 0;
                CutLink(spool);
                _columns[column].RemoveAt(depth);
                placed.Add(PutOnLoom(spool, column, depth));
            }
            else if (spool.Link != 0)
            {
                var partnerColumn = FindPartnerColumn(spool, column);
                var partner = _columns[partnerColumn][0];
                _columns[column].RemoveAt(0);
                _columns[partnerColumn].RemoveAt(0);
                placed.Add(PutOnLoom(spool, column, 0));
                placed.Add(PutOnLoom(partner, partnerColumn, 0));
            }
            else
            {
                _columns[column].RemoveAt(0);
                placed.Add(PutOnLoom(spool, column, 0));
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
            return MoveResult.Place(placed, unlocked, revealed, _status);
        }

        /// <summary>Поставить клубок в первый свободный слот; толстому — ещё и соседний (или любой свободный).</summary>
        private Placement PutOnLoom(Spool spool, int column, int depth)
        {
            var slot = _slots.IndexOf(null);
            if (spool.Thick)
            {
                // Толстому лучше два соседних слота: так он и выглядит одним клубком.
                for (var i = 0; i + 1 < _slots.Count; i++)
                {
                    if (_slots[i] == null && _slots[i + 1] == null)
                    {
                        slot = i;
                        break;
                    }
                }
            }

            _slots[slot] = spool;
            var tail = -1;
            if (spool.Thick)
            {
                tail = slot + 1 < _slots.Count && _slots[slot + 1] == null ? slot + 1 : _slots.IndexOf(null);
                _slots[tail] = spool;
            }

            spool.Revealed = true;
            spool.Warmup = WarmupSteps;
            spool.Stalled = false;
            return new Placement(spool, column, depth, slot, tail);
        }

        private void Free(Spool spool)
        {
            for (var i = 0; i < _slots.Count; i++)
            {
                if (ReferenceEquals(_slots[i], spool))
                {
                    _slots[i] = null;
                }
            }
        }

        /// <summary>Связали петлю цвета: лёд на клубках этого цвета подтаивает.</summary>
        private void Thaw(int color, List<int> thawed)
        {
            foreach (var column in _columns)
            {
                foreach (var spool in column)
                {
                    if (spool.Ice > 0 && !spool.Rainbow && spool.Color == color)
                    {
                        spool.Ice--;
                        if (spool.Ice == 0)
                        {
                            thawed.Add(spool.Id);
                        }
                    }
                }
            }
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

        /// <summary>Пересчитать статус вне шага вязки (после отмены, слота, времени).</summary>
        private void Reevaluate()
        {
            if (_status == GameStatus.TimeUp)
            {
                return;
            }

            if (_grid.IsComplete)
            {
                _status = GameStatus.Won;
            }
            else
            {
                _status = IsBusy || CanAct() ? GameStatus.Playing : GameStatus.Jammed;
            }
        }

        /// <summary>Есть ли хоть одно действие без бустеров.</summary>
        private bool CanAct() => CanAct(FreeSlots);

        /// <summary>Есть ли действие без бустеров, будь на станке free свободных слотов.</summary>
        private bool CanAct(int free)
        {
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
                if (front.Locked || front.Frozen)
                {
                    continue;
                }

                if (front.Knots > 0)
                {
                    return true;
                }

                if (front.Link == 0 && free >= front.Size)
                {
                    return true;
                }

                if (front.Link != 0 && free >= 2 && FindPartnerColumn(front, c) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private void SaveSnapshot()
        {
            _history.Push(new Snapshot(
                CloneSlots(_slots),
                _columns.Select(c => c.Select(s => s.Clone()).ToList()).ToList(),
                _grid.SaveState(),
                Taps));
        }

        /// <summary>Копия слотов: толстый клубок в двух слотах остаётся одним объектом.</summary>
        private static List<Spool?> CloneSlots(List<Spool?> slots)
        {
            var copies = new Dictionary<int, Spool>();
            return slots.Select(s =>
            {
                if (s == null)
                {
                    return null;
                }

                if (!copies.TryGetValue(s.Id, out var copy))
                {
                    copy = s.Clone();
                    copies[s.Id] = copy;
                }

                return copy;
            }).ToList();
        }

        private sealed class Snapshot
        {
            public Snapshot(List<Spool?> slots, List<List<Spool>> columns, int[] done, int taps)
            {
                Slots = slots;
                Columns = columns;
                Done = done;
                Taps = taps;
            }

            public List<Spool?> Slots { get; }

            public List<List<Spool>> Columns { get; }

            public int[] Done { get; }

            public int Taps { get; }
        }
    }
}
