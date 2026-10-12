using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using KnittedPicture.Core;

namespace KnittedPicture.Playtest
{
    /// <summary>
    /// «Хаос-игрок»: случайные тапы, ножницы, срезы со станка, лишние слоты, отмены, время и шаги станка —
    /// всё, что может сделать человек, в случайном порядке. После каждого действия проверяются правила
    /// (инварианты). Каждая партия идёт от своего зерна, поэтому любую находку можно повторить: --repro.
    /// </summary>
    public sealed class RulesCheck
    {
        private const int MaxActions = 600;
        private readonly Findings _f;
        private readonly int _seed;

        public RulesCheck(Findings findings, int seed)
        {
            _f = findings;
            _seed = seed;
        }

        public static int RunSeed(int baseSeed, int levelId, int run) => unchecked(baseSeed * 7919 + levelId * 101 + run * 31337);

        public void Run(IReadOnlyList<LevelData> levels, int runsPerLevel)
        {
            foreach (var level in levels)
            {
                if (!Possible(new LoomGame(level)))
                {
                    _f.Fail("уровень непроходим с самого начала: не хватает пряжи", $"уровень {level.id}");
                }

                for (var run = 0; run < runsPerLevel; run++)
                {
                    var final = Play(level, run, verbose: false);

                    // Детерминизм: та же партия от того же зерна обязана закончиться так же.
                    if (run < 3 && Play(level, run, verbose: false) != final)
                    {
                        _f.Fail("одна и та же партия закончилась по-разному", $"уровень {level.id}", ReproArgs(level.id, run));
                    }
                }
            }
        }

        public string ReproArgs(int levelId, int run) => $"--repro {levelId}:{run} --seed {_seed}";

        /// <summary>Сыграть одну партию; verbose — печатать каждое действие и состояние (режим --repro).</summary>
        public string Play(LevelData level, int run, bool verbose)
        {
            var rng = new Random(RunSeed(_seed, level.id, run));
            var g = new LoomGame(level);
            var history = new Stack<string>();
            var log = new List<string>();
            var wasPossible = Possible(g);
            var repro = ReproArgs(level.id, run);

            for (var step = 0; step < MaxActions && (g.Status == GameStatus.Playing || g.Status == GameStatus.Jammed || g.Status == GameStatus.TimeUp); step++)
            {
                string Ctx() => $"уровень {level.id}, партия {run}, действие {step}: … {string.Join(" ", log.TakeLast(6))}";
                var before = Snap(g);
                var roll = rng.NextDouble();
                string action;
                if (g.Status == GameStatus.TimeUp)
                {
                    if (rng.NextDouble() < 0.5)
                    {
                        g.AddTime(20);
                        action = "время+20";
                    }
                    else
                    {
                        break;
                    }
                }
                else if (roll < 0.55)
                {
                    var c = rng.Next(g.ColumnCount);
                    var res = g.TapColumn(c);
                    action = $"тап{c}:{(res.Accepted ? res.Kind.ToString() : res.Reason.ToString())}";
                    if (res.Accepted)
                    {
                        history.Push(before);
                        _f.Count("тапов принято");
                    }

                    if (res.Accepted && g.Status == GameStatus.Jammed)
                    {
                        _f.Fail("тап принят при заторе", Ctx(), repro);
                    }
                }
                else if (roll < 0.62)
                {
                    var c = rng.Next(g.ColumnCount);
                    var depth = g.Column(c).Count > 0 ? rng.Next(g.Column(c).Count) : 0;
                    var res = g.PullSpool(c, depth);
                    action = $"ножницы{c}/{depth}:{(res.Accepted ? "да" : res.Reason.ToString())}";
                    if (res.Accepted)
                    {
                        history.Push(before);
                        _f.Count("ножницы с поля");
                    }
                }
                else if (roll < 0.66)
                {
                    var s = rng.Next(g.Slots.Count);
                    var spool = g.Slots[s];
                    var needed = spool != null && !g.IsForeign(spool) && !g.IsSurplus(spool);
                    var returns = spool != null && g.ReturnsToField(spool);
                    var possibleBefore = Possible(g);
                    var ok = g.CutFromLoom(s);
                    action = $"срез{s}:{ok}";
                    if (ok)
                    {
                        history.Push(before);
                        _f.Count("срезов со станка");
                        if (needed && !returns)
                        {
                            _f.Fail("нужный клубок срезан и не вернулся на поле", Ctx(), repro);
                        }

                        if (possibleBefore && !Possible(g))
                        {
                            _f.Fail("срез со станка сделал уровень непроходимым", Ctx(), repro);
                        }
                    }
                }
                else if (roll < 0.69)
                {
                    action = $"слот:{g.AddSlot()}";
                }
                else if (roll < 0.75)
                {
                    if (history.Count > 0 && g.CanUndo)
                    {
                        var expected = history.Pop();
                        g.Undo();
                        action = "отмена";
                        if (StripStatus(Snap(g)) != StripStatus(expected))
                        {
                            _f.Fail("отмена не вернула прежнее состояние", Ctx() + $"\n    ожидалось {expected}\n    стало     {Snap(g)}", repro);
                        }
                    }
                    else
                    {
                        action = "отмена-";
                    }
                }
                else if (roll < 0.8)
                {
                    g.Tick((float)(rng.NextDouble() * 5));
                    action = "время";
                }
                else
                {
                    var n = rng.Next(1, 8);
                    action = $"шаг×{n}";
                    for (var i = 0; i < n; i++)
                    {
                        var keyBefore = Snap(g);
                        var result = g.Step();
                        if (!result.Progress && g.Status == GameStatus.Playing)
                        {
                            if (!g.IsBusy && !Enumerable.Range(0, g.ColumnCount).Any(g.CanTap))
                            {
                                _f.Fail("тупик без затора: ходов нет, а игра «идёт»", Ctx(), repro);
                            }

                            if (Snap(g) != keyBefore)
                            {
                                _f.Fail("шаг без прогресса изменил состояние", Ctx(), repro);
                            }
                        }
                    }
                }

                log.Add(action);
                if (verbose)
                {
                    Console.WriteLine($"{step,4}  {action,-24} {Snap(g)}");
                }

                Invariants(g, Ctx() + " " + action, repro);
                if (StripStatus(Snap(g.Clone())) != StripStatus(Snap(g)))
                {
                    _f.Fail("копия партии отличается от оригинала", Ctx(), repro);
                }

                if (wasPossible && !Possible(g) && !action.StartsWith("срез"))
                {
                    _f.Fail($"уровень стал непроходимым не из-за среза ({action.Split(':')[0]})", Ctx(), repro);
                }

                wasPossible &= Possible(g);
            }

            _f.Count("партий");
            if (g.Status == GameStatus.Won)
            {
                _f.Count("побед хаос-игрока");
            }

            return Snap(g);
        }

        /// <summary>Правила, которые должны выполняться всегда, в любой момент партии.</summary>
        private void Invariants(LoomGame g, string ctx, string repro)
        {
            // Каждый клубок ровно в одном месте; на станке занимает столько слотов, каков его размер.
            var onField = new Dictionary<int, int>();
            var onLoom = new Dictionary<int, int>();
            for (var c = 0; c < g.ColumnCount; c++)
            {
                foreach (var s in g.Column(c))
                {
                    onField[s.Id] = onField.TryGetValue(s.Id, out var n) ? n + 1 : 1;
                }
            }

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
                    _f.Fail("клубок в двух местах сразу", ctx, repro);
                }
            }

            var seen = new HashSet<int>();
            foreach (var s in g.Slots)
            {
                if (s == null || !seen.Add(s.Id))
                {
                    continue;
                }

                if (onLoom[s.Id] != s.Size)
                {
                    _f.Fail($"клубок размера {s.Size} занимает {onLoom[s.Id]} слота", ctx, repro);
                }

                if (s.Loops <= 0)
                {
                    _f.Fail("пустой клубок остался на станке", ctx, repro);
                }
            }

            if ((g.Status == GameStatus.Won) != (g.KnittedCount == g.TotalStitches))
            {
                _f.Fail("«победа» не совпадает с готовой картинкой", ctx + " " + g.Status, repro);
            }

            if (g.Status == GameStatus.Jammed && g.IsBusy)
            {
                _f.Fail("затор, хотя станку есть работа", ctx, repro);
            }

            if (g.KnittedCount < 0 || g.KnittedCount > g.TotalStitches)
            {
                _f.Fail("число связанных петель вне границ", ctx, repro);
            }

            for (var c = 0; c < g.ColumnCount; c++)
            {
                for (var d = 0; d < g.Column(c).Count; d++)
                {
                    var sp = g.Column(c)[d];
                    if (sp.Ice > 0 && sp.Ice > g.LeftOf(sp.Color) - sp.Loops)
                    {
                        _f.Fail("лёд больше не растает", ctx + $" (лёд {sp.Ice}, петель цвета осталось {g.LeftOf(sp.Color)})", repro);
                    }
                }
            }

            if (g.Status == GameStatus.Playing && g.KnittedCount < g.TotalStitches && g.FrontColors().Count == 0)
            {
                _f.Fail("во время игры нет ни одной клетки, которую можно вязать", ctx, repro);
            }
        }

        /// <summary>Хватает ли пряжи каждого цвета, чтобы закончить картинку (радужная покрывает любую нехватку).</summary>
        public static bool Possible(LoomGame g)
        {
            var supply = new Dictionary<int, int>();
            var rainbow = 0;
            var seen = new HashSet<int>();
            var all = Enumerable.Range(0, g.ColumnCount).SelectMany(c => g.Column(c)).Concat(g.Slots.Where(s => s != null).Select(s => s!));
            foreach (var s in all)
            {
                if (!seen.Add(s.Id))
                {
                    continue;
                }

                if (s.Rainbow)
                {
                    rainbow += s.Loops;
                }
                else
                {
                    supply[s.Color] = (supply.TryGetValue(s.Color, out var n) ? n : 0) + s.Loops;
                }
            }

            var deficit = 0;
            for (var c = 0; c < g.ColorCount; c++)
            {
                deficit += Math.Max(0, g.LeftOf(c) - (supply.TryGetValue(c, out var n) ? n : 0));
            }

            return deficit <= rainbow;
        }

        /// <summary>Снимок состояния: высоты столбиков, клубки на поле и на станке, тапы и статус.</summary>
        public static string Snap(LoomGame g)
        {
            var sb = new StringBuilder();
            for (var x = 0; x < g.Width; x++)
            {
                sb.Append(g.ColumnHeight(x)).Append('.');
            }

            sb.Append('|');
            for (var c = 0; c < g.ColumnCount; c++)
            {
                foreach (var s in g.Column(c))
                {
                    sb.Append($"{s.Id}:{s.Loops}:{s.Knots}:{s.Locked}:{s.Ice}:{s.Link}:{s.IsColorVisible},");
                }

                sb.Append(';');
            }

            sb.Append('|');
            var slots = string.Concat(g.Slots.Select(s => (s == null ? "_" : $"{s.Id}:{s.Loops}:{s.Warmup}") + ","));
            while (slots.EndsWith("_,"))
            {
                slots = slots.Substring(0, slots.Length - 2);
            }

            sb.Append(slots).Append('|').Append(g.Taps).Append(g.Status);
            return sb.ToString();
        }

        /// <summary>Без тапов и статуса: отмена возвращает поле и станок, а статус пересчитывается заново.</summary>
        private static string StripStatus(string snap) => snap.Substring(0, snap.LastIndexOf('|'));
    }
}
