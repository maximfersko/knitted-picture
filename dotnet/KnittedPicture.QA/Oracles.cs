using System;
using System.Collections.Generic;
using System.Linq;
using KnittedPicture.Core;

namespace KnittedPicture.QA
{
    /// <summary>Насколько серьёзна находка. CI краснеет от Crash и Bug.</summary>
    public enum Severity
    {
        /// <summary>Ядро бросило исключение на действии, доступном из игры.</summary>
        Crash,

        /// <summary>Нарушено правило игры на действии, доступном из игры.</summary>
        Bug,

        /// <summary>Ядро не защищено от кривого вызова (индекс вне границ, NaN): из интерфейса недостижимо, но опасно.</summary>
        Robustness,

        /// <summary>Подозрительно, но не обязательно ошибка: подсказка не нашла пути, долгий шаг.</summary>
        Warning,

        /// <summary>Для сведения.</summary>
        Info,
    }

    /// <summary>Нарушение: Kind — стабильный ключ (по нему сворачиваются повторы и ужимается партия).</summary>
    public sealed class Violation
    {
        public Violation(string kind, Severity severity, string title, string detail)
        {
            Kind = kind;
            Severity = severity;
            Title = title;
            Detail = detail;
        }

        public string Kind { get; }

        public Severity Severity { get; }

        public string Title { get; }

        public string Detail { get; }

        public int MarkColumn = -1;
        public int MarkSlot = -1;
        public int MarkCell = -1;
    }

    /// <summary>Что было до действия: всё, с чем оракулы сравнивают «после».</summary>
    public sealed class Before
    {
        public Before(LoomGame g)
        {
            Full = State.Full(g);
            Board = State.Board(g);
            Status = g.Status;
            Taps = g.Taps;
            Version = g.Version;
            Knitted = g.KnittedCount;
            Yarn = State.Yarn(g);
            SlotCount = g.Slots.Count;
            FreeSlots = g.FreeSlots;
            TimeLeft = g.TimeLeft;
            TimerStarted = g.TimerStarted;
            CanTap = Enumerable.Range(0, g.ColumnCount).Select(g.CanTap).ToArray();
            // Значения, а не ссылки: действие меняет сами клубки.
            Fronts = Enumerable.Range(0, g.ColumnCount).Select(c => g.Column(c).Count > 0 ? g.Column(c)[0] : null).ToArray();
            FrontKnots = Fronts.Select(s => s?.Knots ?? 0).ToArray();
            FrontLocked = Fronts.Select(s => s?.Locked ?? false).ToArray();
            FrontFrozen = Fronts.Select(s => s?.Frozen ?? false).ToArray();
            FrontText = Fronts.Select(Oracles.Describe).ToArray();
            ColumnSizes = Enumerable.Range(0, g.ColumnCount).Select(c => g.Column(c).Count).ToArray();
            Active = Enumerable.Range(0, g.Width).Select(g.ActiveFrontY).ToArray();
            FrontY = Enumerable.Range(0, g.Width).Select(g.FrontY).ToArray();
            Done = new bool[g.Width * g.Height];
            for (var y = 0; y < g.Height; y++)
            {
                for (var x = 0; x < g.Width; x++)
                {
                    Done[y * g.Width + x] = g.IsKnitted(x, y);
                }
            }

            Loops = State.Spools(g).ToDictionary(s => s.Id, s => s.Loops);
            Colors = State.Spools(g).ToDictionary(s => s.Id, s => (s.Color, s.Rainbow, s.Thick));
            OnLoom = new HashSet<int>(g.Slots.Where(s => s != null).Select(s => s!.Id));
            Busy = g.IsBusy;
            Warmups = g.Slots.Select(s => s?.Warmup ?? -1).ToArray();
        }

        /// <summary>«Полёт» клубка в каждом слоте (-1 — слот пуст).</summary>
        public int[] Warmups { get; }

        public string Full { get; }

        public string Board { get; }

        public GameStatus Status { get; }

        public int Taps { get; }

        public int Version { get; }

        public int Knitted { get; }

        public int Yarn { get; }

        public int SlotCount { get; }

        public int FreeSlots { get; }

        public float TimeLeft { get; }

        public bool TimerStarted { get; }

        public bool[] CanTap { get; }

        /// <summary>Передние клубки (ссылки: годятся только для Id; значения — в Front* ниже).</summary>
        public Spool?[] Fronts { get; }

        public int[] FrontKnots { get; }

        public bool[] FrontLocked { get; }

        public bool[] FrontFrozen { get; }

        public string[] FrontText { get; }

        public int[] ColumnSizes { get; }

        public int[] Active { get; }

        /// <summary>Нижняя несвязанная клетка рисунка каждого столбца (без учёта натяжения).</summary>
        public int[] FrontY { get; }

        public bool[] Done { get; }

        public Dictionary<int, int> Loops { get; }

        public Dictionary<int, (int Color, bool Rainbow, bool Thick)> Colors { get; }

        public HashSet<int> OnLoom { get; }

        public bool Busy { get; }
    }

    /// <summary>
    /// Оракулы: правила, которые обязаны выполняться после любого действия. Каждый — отдельная проверка
    /// со своим ключом: так отчёт говорит, что именно сломалось, а ужатие ищет ровно эту поломку.
    /// Дорогие проверки (копия партии до конца, решатель) идут не на каждом действии: deep = true — на каждом.
    /// </summary>
    public sealed class Oracles
    {
        private readonly List<Violation> _found = new List<Violation>();

        public IReadOnlyList<Violation> Found => _found;

        private void Fail(string kind, Severity severity, string title, string detail, int column = -1, int slot = -1, int cell = -1) =>
            _found.Add(new Violation(kind, severity, title, detail) { MarkColumn = column, MarkSlot = slot, MarkCell = cell });

        public void Clear() => _found.Clear();

        /// <summary>Действие бросило исключение.</summary>
        public void Exception(QaAction a, Exception e) =>
            Fail(a.IsRobustness ? "throw-robust:" + e.GetType().Name : "throw:" + a.Kind + ":" + e.GetType().Name,
                a.IsRobustness ? Severity.Robustness : Severity.Crash,
                a.IsRobustness ? $"ядро падает на кривом вызове «{a}»" : $"ядро упало на «{a.Kind}»",
                $"{e.GetType().Name}: {e.Message}\n{FirstFrames(e)}");

        private static string FirstFrames(Exception e) =>
            string.Join("\n", (e.StackTrace ?? string.Empty).Split('\n').Take(6).Select(l => l.Trim()));

        /// <summary>Все проверки одного действия. sample — дорогие проверки на этом действии.</summary>
        public void Check(LoomGame g, Before b, QaAction a, Outcome o, Stack<string> undo, bool sample)
        {
            Basic(g);
            Moves(g, b, a, o);
            Knitting(g, b, a, o);
            Yarn(g, b, a);
            Status(g, b, a, o);
            Timer(g, b, a);
            Undo(g, a, o, undo);
            Purity(g);
            if (sample)
            {
                Future(g);
                Boosters(g);
                Liveness(g);
                CloneIndependence(g);
            }
        }

        // ---------- Всегда ----------

        /// <summary>Каждый клубок в одном месте, размеры, границы чисел, столбики растут снизу без дыр.</summary>
        private void Basic(LoomGame g)
        {
            var onField = new Dictionary<int, int>();
            for (var c = 0; c < g.ColumnCount; c++)
            {
                for (var d = 0; d < g.Column(c).Count; d++)
                {
                    var s = g.Column(c)[d];
                    onField[s.Id] = onField.TryGetValue(s.Id, out var n) ? n + 1 : 1;
                    if (s.Loops <= 0)
                    {
                        Fail("field-empty-spool", Severity.Bug, "на поле клубок без петель", $"клубок {s.Id} в колонке {c}, петель {s.Loops}", column: c);
                    }

                    if (s.Knots < 0 || s.Ice < 0)
                    {
                        Fail("negative-counter", Severity.Bug, "отрицательные узелки или лёд", $"клубок {s.Id}: узелков {s.Knots}, лёд {s.Ice}", column: c);
                    }

                    // Лёд тает от петель своего цвета: если их осталось меньше, чем льда, клубок не оттает никогда.
                    if (s.Ice > 0 && s.Ice > g.LeftOf(s.Color))
                    {
                        Fail("ice-never-melts", Severity.Bug, "лёд больше не растает",
                            $"клубок {s.Id}: лёд {s.Ice}, петель цвета {s.Color} осталось {g.LeftOf(s.Color)}", column: c);
                    }
                }

                // Передний клубок всегда показывает цвет: скрытый открывается, выходя вперёд.
                if (g.Column(c).Count > 0 && !g.Column(c)[0].IsColorVisible && g.Status == GameStatus.Playing)
                {
                    Fail("front-hidden", Severity.Bug, "передний клубок так и остался скрытым", $"колонка {c}, клубок {g.Column(c)[0].Id}", column: c);
                }
            }

            var onLoom = new Dictionary<int, int>();
            foreach (var s in g.Slots)
            {
                if (s != null)
                {
                    onLoom[s.Id] = onLoom.TryGetValue(s.Id, out var n) ? n + 1 : 1;
                }
            }

            foreach (var kv in onField)
            {
                if (kv.Value > 1 || onLoom.ContainsKey(kv.Key))
                {
                    Fail("spool-twice", Severity.Bug, "клубок в двух местах сразу", $"клубок {kv.Key}");
                }
            }

            var seen = new HashSet<int>();
            for (var i = 0; i < g.Slots.Count; i++)
            {
                var s = g.Slots[i];
                if (s == null || !seen.Add(s.Id))
                {
                    continue;
                }

                if (onLoom[s.Id] != s.Size)
                {
                    Fail("slot-size", Severity.Bug, "клубок занимает не столько слотов, каков его размер", $"клубок {s.Id} размера {s.Size} в {onLoom[s.Id]} слотах", slot: i);
                }

                if (s.Loops <= 0)
                {
                    Fail("loom-empty-spool", Severity.Bug, "пустой клубок остался на станке", $"слот {i}, клубок {s.Id}", slot: i);
                }

                if (s.Warmup < 0 || s.Warmup > LoomGame.WarmupSteps)
                {
                    Fail("warmup-range", Severity.Bug, "«полёт» клубка вне границ", $"слот {i}: warmup {s.Warmup}", slot: i);
                }
            }

            if (g.Slots.Count < 1 || g.Slots.Count > LoomGame.MaxSlots)
            {
                Fail("slot-count", Severity.Bug, "слотов вне 1..7", $"слотов {g.Slots.Count}");
            }

            if (g.KnittedCount < 0 || g.KnittedCount > g.TotalStitches)
            {
                Fail("knitted-range", Severity.Bug, "связанных петель вне границ", $"{g.KnittedCount} из {g.TotalStitches}");
            }

            // Столбики растут снизу вверх: над несвязанной клеткой рисунка нет связанных.
            for (var x = 0; x < g.Width; x++)
            {
                var gap = false;
                for (var y = g.Height - 1; y >= 0; y--)
                {
                    if (g.IsFabric(x, y))
                    {
                        continue;
                    }

                    if (!g.IsKnitted(x, y))
                    {
                        gap = true;
                    }
                    else if (gap)
                    {
                        Fail("column-gap", Severity.Bug, "в столбике дыра: связано выше несвязанной клетки", $"столбец {x}, ряд {y}", cell: y * g.Width + x);
                        break;
                    }

                    if (g.ColorAt(x, y) < 0 || g.ColorAt(x, y) >= g.ColorCount)
                    {
                        Fail("color-range", Severity.Bug, "цвет клетки вне палитры", $"({x},{y}) = {g.ColorAt(x, y)}", cell: y * g.Width + x);
                    }
                }
            }

            if (g.Status == GameStatus.Playing && g.KnittedCount < g.TotalStitches && g.FrontColors().Count == 0)
            {
                Fail("no-front", Severity.Bug, "идёт игра, а вязать нечего: фронта нет", $"связано {g.KnittedCount} из {g.TotalStitches}");
            }
        }

        // ---------- Ходы ----------

        /// <summary>Отклонённый ход ничего не меняет, причина отказа правдива, принятый ведёт себя как обещано.</summary>
        private void Moves(LoomGame g, Before b, QaAction a, Outcome o)
        {
            if (o.Move == null)
            {
                if (o.Flag == false && State.Full(g) != b.Full)
                {
                    Fail("rejected-mutates:" + a.Kind, Severity.Bug, $"отказ «{a.Kind}» всё же изменил партию", $"до  {b.Full}\nпосле {State.Full(g)}");
                }

                return;
            }

            var m = o.Move;
            var column = a.Kind == ActionKind.Hint ? o.HintColumn : a.A;
            if (!m.Accepted)
            {
                if (State.Full(g) != b.Full)
                {
                    Fail("rejected-mutates:" + a.Kind, Severity.Bug, "отклонённый ход изменил партию", $"{a}: {m.Reason}\nдо  {b.Full}\nпосле {State.Full(g)}", column: column);
                }

                if (a.Kind == ActionKind.Hint)
                {
                    Fail("hint-rejected", Severity.Bug, "подсказка посоветовала ход, который игра не принимает", $"колонка {column}: {m.Reason}", column: column);
                }

                if (column >= 0 && column < b.ColumnSizes.Length)
                {
                    var reasonOk = m.Reason switch
                    {
                        RejectReason.GameOver => b.Status != GameStatus.Playing,
                        RejectReason.EmptyColumn => a.Kind == ActionKind.Tap ? b.ColumnSizes[column] == 0 : a.B >= b.ColumnSizes[column],
                        RejectReason.Locked => a.Kind == ActionKind.Pull || b.FrontLocked[column],
                        RejectReason.Frozen => a.Kind == ActionKind.Pull || b.FrontFrozen[column],
                        RejectReason.NoFreeSlot => b.FreeSlots < 4,
                        _ => true,
                    };
                    if (!reasonOk)
                    {
                        Fail("reject-reason:" + m.Reason, Severity.Bug, $"неправдивая причина отказа «{m.Reason}»",
                            $"{a}: передний клубок {b.FrontText[column]}, свободно слотов {b.FreeSlots}, статус {b.Status}", column: column);
                    }
                }

                if (a.Kind == ActionKind.Tap && column >= 0 && column < b.CanTap.Length && b.CanTap[column])
                {
                    Fail("cantap-mismatch", Severity.Bug, "CanTap обещал ход, а тап отклонён", $"колонка {column}: {m.Reason}", column: column);
                }

                return;
            }

            if (a.Kind == ActionKind.Tap && column >= 0 && column < b.CanTap.Length && !b.CanTap[column])
            {
                Fail("cantap-mismatch", Severity.Bug, "CanTap запрещал ход, а тап принят", $"колонка {column}", column: column);
            }

            if (g.Taps != b.Taps + 1 || g.Version <= b.Version)
            {
                Fail("accepted-counters", Severity.Bug, "принятый ход не посчитан", $"тапов {b.Taps}→{g.Taps}, версия {b.Version}→{g.Version}");
            }

            if (b.Status != GameStatus.Playing && !(a.Kind == ActionKind.Pull && b.Status == GameStatus.Jammed))
            {
                Fail("move-after-end", Severity.Bug, $"ход принят при статусе {b.Status}", a.ToString(), column: column);
            }

            if (m.Kind == MoveKind.KnotUntied)
            {
                if (m.Knotted == null || m.Knotted.Knots != b.FrontKnots[column] - 1 || g.Column(column).Count == 0 || g.Column(column)[0].Id != m.Knotted.Id)
                {
                    Fail("knot", Severity.Bug, "узелок развязан неправильно", $"колонка {column}: {b.FrontText[column]} → {Describe(m.Knotted)}", column: column);
                }

                return;
            }

            foreach (var p in m.Placed)
            {
                var slot = g.Slots.ToList().FindIndex(s => s?.Id == p.Spool.Id);
                if (slot < 0)
                {
                    Fail("placed-missing", Severity.Bug, "клубок «поставлен», но на станке его нет", $"клубок {p.Spool.Id}", column: p.Column);
                }
                else if (p.Spool.Warmup <= 0)
                {
                    Fail("placed-no-flight", Severity.Bug, "поставленный клубок начал вязать без «полёта»", $"клубок {p.Spool.Id}", slot: slot);
                }

                if (g.Column(p.Column).Any(s => s.Id == p.Spool.Id))
                {
                    Fail("placed-still-on-field", Severity.Bug, "поставленный клубок остался на поле", $"клубок {p.Spool.Id}", column: p.Column);
                }
            }

            if (m.Placed.Count == 2 && m.Placed[0].Spool.Link != 0 && m.Placed[0].Spool.Link != m.Placed[1].Spool.Link && a.Kind != ActionKind.Pull)
            {
                Fail("link-pair", Severity.Bug, "связанные клубки уехали не парой", $"{Describe(m.Placed[0].Spool)} + {Describe(m.Placed[1].Spool)}");
            }
        }

        // ---------- Вязка ----------

        /// <summary>Каждая петля — на своём месте, своим цветом, из своего клубка; клубок теряет ровно столько петель.</summary>
        private void Knitting(LoomGame g, Before b, QaAction a, Outcome o)
        {
            if (a.Kind != ActionKind.Step)
            {
                if (g.KnittedCount != b.Knitted && a.Kind != ActionKind.Undo)
                {
                    Fail("knit-outside-step", Severity.Bug, "петли связались не шагом станка", $"{a}: {b.Knitted} → {g.KnittedCount}");
                }

                return;
            }

            if (o.Steps.Count == 1)
            {
                var r = o.Steps[0];
                var cells = new HashSet<int>();
                var byColumn = new Dictionary<int, List<int>>();
                var bySpool = new Dictionary<int, int>();
                foreach (var st in r.Stitches)
                {
                    var i = st.Y * g.Width + st.X;
                    if (!cells.Add(i))
                    {
                        Fail("stitch-twice", Severity.Bug, "одна клетка связана дважды за шаг", $"({st.X},{st.Y})", cell: i);
                    }

                    if (b.Done[i])
                    {
                        Fail("stitch-done", Severity.Bug, "связана уже связанная клетка", $"({st.X},{st.Y})", cell: i);
                    }

                    if (!g.IsKnitted(st.X, st.Y))
                    {
                        Fail("stitch-lost", Severity.Bug, "петля «связана», но клетка пустая", $"({st.X},{st.Y})", cell: i);
                    }

                    if (b.Colors.TryGetValue(st.SpoolId, out var sp))
                    {
                        if (!sp.Rainbow && sp.Color != g.ColorAt(st.X, st.Y))
                        {
                            Fail("stitch-color", Severity.Bug, "петля связана чужим цветом", $"клубок {st.SpoolId} цвета {sp.Color} в клетку цвета {g.ColorAt(st.X, st.Y)}", cell: i);
                        }

                        if (!b.OnLoom.Contains(st.SpoolId))
                        {
                            Fail("stitch-not-on-loom", Severity.Bug, "петлю связал клубок не со станка", $"клубок {st.SpoolId}", cell: i);
                        }
                    }

                    bySpool[st.SpoolId] = bySpool.TryGetValue(st.SpoolId, out var n) ? n + 1 : 1;
                    if (!byColumn.TryGetValue(st.X, out var ys))
                    {
                        byColumn[st.X] = ys = new List<int>();
                    }

                    ys.Add(st.Y);
                }

                // Первая петля столбца — на его фронте. Натяжение здесь не сверяем: клубки вяжут по очереди,
                // и петля одного опускает нижний фронт, отпуская соседний столбец ещё в этом же шаге.
                foreach (var kv in byColumn)
                {
                    var lowest = kv.Value.Max();
                    if (b.FrontY[kv.Key] < 0 || lowest != b.FrontY[kv.Key])
                    {
                        Fail("stitch-front", Severity.Bug, "петля связана не на фронте столбца",
                            $"столбец {kv.Key}: фронт был {b.FrontY[kv.Key]}, связаны ряды {string.Join(",", kv.Value)}", cell: lowest * g.Width + kv.Key);
                    }
                }

                // Клубок теряет ровно по петле на каждую связанную (лишняя пряжа уходит со станка целиком).
                foreach (var kv in bySpool)
                {
                    var after = State.Spools(g).FirstOrDefault(s => s.Id == kv.Key);
                    var leftover = r.Completed.Where(c => c.SpoolId == kv.Key).Sum(c => c.Leftover);
                    var expected = b.Loops[kv.Key] - kv.Value - leftover;
                    var actual = after?.Loops ?? 0;
                    if (expected != actual && !(after == null && expected == 0))
                    {
                        Fail("stitch-loops", Severity.Bug, "клубок потерял не столько петель, сколько связал",
                            $"клубок {kv.Key}: было {b.Loops[kv.Key]}, связал {kv.Value}, лишних {leftover}, стало {actual}");
                    }

                    if (b.Colors[kv.Key].Thick ? kv.Value > 2 : kv.Value > 1)
                    {
                        Fail("stitch-speed", Severity.Bug, "клубок связал за шаг больше, чем может", $"клубок {kv.Key}: {kv.Value} петель");
                    }
                }

                if (g.KnittedCount - b.Knitted != r.Stitches.Count)
                {
                    Fail("stitch-count", Severity.Bug, "число петель шага не сходится со счётчиком", $"шаг {r.Stitches.Count}, счётчик {b.Knitted}→{g.KnittedCount}");
                }

                foreach (var c in r.Completed)
                {
                    if (c.Leftover > 0 && b.Colors.TryGetValue(c.SpoolId, out var sp) && !sp.Rainbow && g.LeftOf(sp.Color) > 0)
                    {
                        Fail("leftover-needed", Severity.Bug, "нужный клубок ушёл со станка недовязанным",
                            $"клубок {c.SpoolId} цвета {sp.Color}: ушло {c.Leftover} петель, а цвету осталось {g.LeftOf(sp.Color)}", slot: c.Slot);
                    }

                    if (g.Slots.Any(s => s?.Id == c.SpoolId))
                    {
                        Fail("completed-stays", Severity.Bug, "довязанный клубок остался на станке", $"клубок {c.SpoolId}", slot: c.Slot);
                    }
                }

                if (!r.Progress && State.Full(g) != b.Full && b.Status == g.Status)
                {
                    Fail("idle-step-mutates", Severity.Bug, "шаг «без прогресса» изменил партию", $"до  {b.Full}\nпосле {State.Full(g)}");
                }
            }

            if (g.KnittedCount < b.Knitted)
            {
                Fail("unknit", Severity.Bug, "связанные петли пропали", $"{b.Knitted} → {g.KnittedCount}");
            }
        }

        // ---------- Пряжа ----------

        /// <summary>Пряжа не появляется из ниоткуда: остаток + связанное не растёт (кроме отмены).</summary>
        private void Yarn(LoomGame g, Before b, QaAction a)
        {
            if (a.Kind == ActionKind.Undo)
            {
                return;
            }

            var now = State.Yarn(g) + g.KnittedCount;
            if (now > b.Yarn + b.Knitted)
            {
                Fail("yarn-created", Severity.Bug, "пряжа появилась из ниоткуда", $"{a}: пряжа+связано {b.Yarn + b.Knitted} → {now}");
            }
        }

        // ---------- Статус ----------

        private void Status(LoomGame g, Before b, QaAction a, Outcome o)
        {
            if ((g.Status == GameStatus.Won) != (g.KnittedCount == g.TotalStitches))
            {
                Fail("won-mismatch", Severity.Bug, "«победа» не совпадает с готовой картинкой", $"статус {g.Status}, связано {g.KnittedCount} из {g.TotalStitches}");
            }

            if (b.Status == GameStatus.Won && State.Full(g) != b.Full)
            {
                Fail("after-won", Severity.Bug, $"после победы «{a.Kind}» изменил партию", $"до  {b.Full}\nпосле {State.Full(g)}");
            }

            if (g.Status == GameStatus.Jammed)
            {
                if (g.IsBusy)
                {
                    Fail("jam-busy", Severity.Bug, "затор, хотя станку есть работа", State.Board(g));
                }

                if (Enumerable.Range(0, g.ColumnCount).Any(g.CanTap))
                {
                    Fail("jam-can-tap", Severity.Bug, "затор, хотя есть ход", State.Board(g));
                }
            }

            // Станок встал, ходов нет, а игра «идёт»: интерфейс будет ждать вечно.
            if (a.Kind == ActionKind.Step && o.Steps.Count > 0 && !o.Steps[o.Steps.Count - 1].Progress
                && g.Status == GameStatus.Playing && !g.IsBusy && !Enumerable.Range(0, g.ColumnCount).Any(g.CanTap))
            {
                Fail("dead-end-playing", Severity.Bug, "тупик без затора: ходов нет, станок стоит, а игра «идёт»", State.Board(g));
            }

            if (b.Status == GameStatus.Jammed && g.Status == GameStatus.Playing
                && a.Kind is not (ActionKind.Undo or ActionKind.Cut or ActionKind.Pull or ActionKind.AddSlot or ActionKind.AddTime))
            {
                Fail("jam-heals", Severity.Bug, $"затор прошёл сам от «{a.Kind}»", State.Board(g));
            }
        }

        // ---------- Время ----------

        private void Timer(LoomGame g, Before b, QaAction a)
        {
            if (float.IsNaN(g.TimeLeft) || float.IsInfinity(g.TimeLeft) || g.TimeLeft < 0f)
            {
                Fail(a.IsRobustness ? "timer-broken-robust" : "timer-broken", a.IsRobustness ? Severity.Robustness : Severity.Bug,
                    "время моли сломалось (NaN, бесконечность или меньше нуля)", $"{a}: осталось {g.TimeLeft}");
            }

            if (g.TimeLimit <= 0 && g.Status == GameStatus.TimeUp)
            {
                Fail("timeup-no-timer", Severity.Bug, "моль съела уровень без таймера", a.ToString());
            }

            if (a.Kind == ActionKind.Tick && !b.TimerStarted && Math.Abs(g.TimeLeft - b.TimeLeft) > 1e-4f)
            {
                Fail("timer-before-start", Severity.Bug, "время моли пошло до первого хода", $"{b.TimeLeft} → {g.TimeLeft}");
            }

            if (a.Kind == ActionKind.Tick && g.TimeLeft > b.TimeLeft + 1e-4f)
            {
                Fail(a.IsRobustness ? "timer-back-robust" : "timer-back", a.IsRobustness ? Severity.Robustness : Severity.Bug,
                    "время моли пошло назад", $"{a}: {b.TimeLeft} → {g.TimeLeft}");
            }

            if (g.Status == GameStatus.TimeUp && g.TimeLeft > 0f)
            {
                Fail("timeup-with-time", Severity.Bug, "моль «съела», а время ещё есть", $"осталось {g.TimeLeft}");
            }

            if (b.Status == GameStatus.Won && g.Status != GameStatus.Won)
            {
                Fail("won-lost", Severity.Bug, "победа отменилась", $"{a}: {g.Status}");
            }

            if (g.Slots.Count < b.SlotCount)
            {
                Fail("slot-lost", Severity.Bug, "купленный слот пропал", $"{a}: слотов {b.SlotCount} → {g.Slots.Count}");
            }
        }

        // ---------- Отмена ----------

        /// <summary>Отмена возвращает поле и станок ровно к состоянию до отменённого хода.</summary>
        private void Undo(LoomGame g, QaAction a, Outcome o, Stack<string> undo)
        {
            if (a.Kind != ActionKind.Undo || o.Flag != true)
            {
                return;
            }

            if (undo.Count == 0)
            {
                Fail("undo-from-nowhere", Severity.Bug, "отмена сработала, хотя отменять нечего", State.Board(g));
                return;
            }

            var expected = undo.Pop();
            if (State.Board(g) != expected)
            {
                Fail("undo-inexact", Severity.Bug, "отмена не вернула прежнее состояние", $"ожидалось {expected}\nстало     {State.Board(g)}");
            }
        }

        // ---------- Вопросы к партии ничего не меняют ----------

        private void Purity(LoomGame g)
        {
            var before = State.Full(g);
            _ = g.IsBusy;
            _ = g.FreeSlots;
            _ = g.FrontColors();
            _ = g.SoonColors(2);
            _ = g.SlotWouldHelp;
            _ = g.ScissorsWouldHelp;
            _ = g.Advice();
            for (var c = 0; c < g.ColumnCount; c++)
            {
                _ = g.CanTap(c);
            }

            if (State.Full(g) != before)
            {
                Fail("query-mutates", Severity.Bug, "вопрос к партии (IsBusy, CanTap, Advice…) изменил её", $"до  {before}\nпосле {State.Full(g)}");
            }
        }

        // ---------- Дорогие: будущее партии ----------

        /// <summary>«Победа решена» правдива и ничего не меняет; копия совпадает с оригиналом.</summary>
        private void Future(LoomGame g)
        {
            var before = State.Full(g);
            var certain = g.WinIsCertain();
            if (State.Full(g) != before)
            {
                Fail("wincertain-mutates", Severity.Bug, "проверка «победа решена» изменила партию", before);
            }

            var copy = g.Clone();
            if (State.Full(copy) != before)
            {
                Fail("clone-differs", Severity.Bug, "копия партии отличается от оригинала", $"оригинал {before}\nкопия    {State.Full(copy)}");
            }

            if (g.Status != GameStatus.Playing)
            {
                return;
            }

            var guard = g.TotalStitches * 4 + 64;
            while (copy.Status == GameStatus.Playing && guard-- > 0 && copy.Step().Progress)
            {
            }

            if (guard <= 0)
            {
                Fail("settle-endless", Severity.Bug, "станок вяжет без конца: довязка не останавливается", State.Board(g));
            }

            if (certain != (copy.Status == GameStatus.Won))
            {
                Fail("wincertain-wrong", Severity.Bug, "«победа решена» ошибается",
                    $"WinIsCertain = {certain}, а без игрока партия кончается {copy.Status}");
            }
        }

        /// <summary>Подсказки затора не врут: «слот поможет» — поможет, «ножницы помогут» — есть что резать.</summary>
        private void Boosters(LoomGame g)
        {
            if (g.Status != GameStatus.Jammed)
            {
                return;
            }

            var withSlot = g.Clone();
            var added = withSlot.AddSlot();
            var slotHelps = added && (Enumerable.Range(0, withSlot.ColumnCount).Any(withSlot.CanTap) || withSlot.IsBusy);
            if (g.SlotWouldHelp && !slotHelps)
            {
                Fail("slot-would-help-lie", Severity.Bug, "окно затора обещает, что слот поможет, а он не помогает", State.Board(g));
            }

            if (!g.SlotWouldHelp && slotHelps)
            {
                Fail("slot-would-help-miss", Severity.Warning, "слот помог бы, но окно затора его не предлагает", State.Board(g));
            }

            if (g.ScissorsWouldHelp)
            {
                var any = false;
                for (var c = 0; c < g.ColumnCount && !any; c++)
                {
                    for (var d = 0; d < g.Column(c).Count && !any; d++)
                    {
                        any |= g.Clone().PullSpool(c, d).Accepted;
                    }
                }

                for (var s = 0; s < g.Slots.Count && !any; s++)
                {
                    var copy = g.Clone();
                    any |= copy.CutFromLoom(s) && (Enumerable.Range(0, copy.ColumnCount).Any(copy.CanTap) || copy.IsBusy);
                }

                if (!any)
                {
                    Fail("scissors-lie", Severity.Bug, "окно затора обещает, что ножницы помогут, а резать нечего", State.Board(g));
                }
            }
        }

        /// <summary>Станок «занят» — значит, за несколько шагов что-то свяжет; иначе экран будет ждать вечно.</summary>
        private void Liveness(LoomGame g)
        {
            if (g.Status != GameStatus.Playing || !g.IsBusy)
            {
                return;
            }

            var copy = g.Clone();
            for (var i = 0; i < LoomGame.WarmupSteps + 3; i++)
            {
                if (copy.Step().Progress)
                {
                    return;
                }
            }

            Fail("busy-forever", Severity.Bug, "станок «занят», но не вяжет: игра зависнет без затора", State.Board(g));
        }

        /// <summary>Копия живёт своей жизнью: ход на копии не трогает оригинал.</summary>
        private void CloneIndependence(LoomGame g)
        {
            var before = State.Full(g);
            var copy = g.Clone();
            for (var c = 0; c < copy.ColumnCount; c++)
            {
                copy.TapColumn(c);
            }

            copy.AddSlot();
            copy.Step();
            copy.Tick(5f);
            if (copy.Slots.Count > 0)
            {
                copy.CutFromLoom(0);
            }

            copy.Undo();
            if (State.Full(g) != before)
            {
                Fail("clone-shares-state", Severity.Bug, "ход на копии партии изменил оригинал", $"до  {before}\nпосле {State.Full(g)}");
            }
        }

        /// <summary>Подсказка ведёт к победе: путь решателя, сыгранный на копии, выигрывает.</summary>
        public void HintLeadsToWin(LoomGame g)
        {
            if (g.Status != GameStatus.Playing)
            {
                return;
            }

            var path = LevelSolver.Solve(g, 20_000);
            if (path == null)
            {
                return;
            }

            var copy = g.Clone();
            copy.Settle();
            foreach (var column in path)
            {
                if (!copy.TapColumn(column).Accepted)
                {
                    Fail("solver-path-rejected", Severity.Bug, "путь решателя содержит недопустимый ход", $"колонка {column} в пути {string.Join(",", path)}", column: column);
                    return;
                }

                copy.Settle();
            }

            if (copy.Status != GameStatus.Won)
            {
                Fail("solver-path-loses", Severity.Bug, "путь решателя не выигрывает", $"путь {string.Join(",", path)} кончился {copy.Status}");
            }
        }

        public static string Describe(Spool? s) => s == null
            ? "нет"
            : $"#{s.Id} цвет {s.Color} петель {s.Loops}{(s.Knots > 0 ? $" узелков {s.Knots}" : "")}{(s.Locked ? " замок" : "")}{(s.Ice > 0 ? $" лёд {s.Ice}" : "")}"
              + $"{(s.Link != 0 ? $" связка {s.Link}" : "")}{(s.Rainbow ? " радуга" : "")}{(s.Thick ? " толстый" : "")}{(s.IsKey ? " ключ" : "")}{(!s.IsColorVisible ? " скрыт" : "")}";
    }
}
