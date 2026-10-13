using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using KnittedPicture.Core;

namespace KnittedPicture.QA
{
    /// <summary>Нарушение в партии: на каком действии случилось впервые.</summary>
    public sealed class Hit
    {
        public Hit(Violation violation, int index)
        {
            Violation = violation;
            Index = index;
        }

        public Violation Violation { get; }

        /// <summary>Номер действия (с 0), после которого нарушение обнаружено.</summary>
        public int Index { get; }
    }

    /// <summary>Итог одной партии.</summary>
    public sealed class EpisodeResult
    {
        public readonly List<QaAction> Actions = new List<QaAction>();
        public readonly List<Hit> Hits = new List<Hit>();
        public readonly List<string> Snapshots = new List<string>();
        public GameStatus Status;
        public double SlowestStepMs;
        public double SlowestHintMs;
        public string SlowestWhat = string.Empty;
    }

    /// <summary>Настройки прогона оракулов.</summary>
    public sealed class Depth
    {
        /// <summary>Доля действий с дорогими проверками (копия до конца, бустеры затора, живость). 1 — на каждом.</summary>
        public double Expensive = 0.2;

        /// <summary>Доля действий с проверкой «путь решателя выигрывает» (самая дорогая).</summary>
        public double Solver = 0.02;

        public static readonly Depth Full = new Depth { Expensive = 1, Solver = 1 };
    }

    public static class Episode
    {
        /// <summary>Дорогие нарушения: при ужатии их надо проверять на каждом действии.</summary>
        public static readonly HashSet<string> ExpensiveKinds = new HashSet<string>
        {
            "wincertain-mutates", "clone-differs", "settle-endless", "wincertain-wrong", "slot-would-help-lie", "slot-would-help-miss",
            "scissors-lie", "busy-forever", "clone-shares-state", "solver-path-rejected", "solver-path-loses",
        };

        /// <summary>Сыграть партию личностью. После победы — ещё несколько действий: «после конца» ничего не должно меняться.</summary>
        public static EpisodeResult Play(LevelData level, Persona persona, int seed, int maxActions, Depth depth, Coverage coverage, Novelty novelty)
        {
            var rng = new Random(seed);
            var g = new LoomGame(level);
            var result = new EpisodeResult();
            var afterEnd = 0;
            var memo = new Memo();
            for (var i = 0; i < maxActions; i++)
            {
                QaAction? next;
                if (g.Status == GameStatus.Won)
                {
                    if (afterEnd++ >= 6)
                    {
                        break;
                    }

                    next = new Monkey().Next(g, rng);
                }
                else
                {
                    next = persona.Next(g, rng);
                }

                if (next == null)
                {
                    break;
                }

                result.Actions.Add(next.Value);
                if (!Run(g, next.Value, i, seed, depth, result, coverage, memo))
                {
                    break;
                }

                novelty.Visit(Explorer.Key(g));
            }

            result.Status = g.Status;
            return result;
        }

        /// <summary>Повторить записанные действия (для ужатия, детерминизма и «скриншотов»). frames — кадр после каждого действия.</summary>
        public static EpisodeResult Replay(LevelData level, IReadOnlyList<QaAction> actions, int seed, Depth depth, List<Frame>? frames = null)
        {
            var g = new LoomGame(level);
            var result = new EpisodeResult();
            frames?.Add(State.Capture(g, level, "START"));
            var memo = new Memo();
            for (var i = 0; i < actions.Count; i++)
            {
                result.Actions.Add(actions[i]);
                var ok = Run(g, actions[i], i, seed, depth, result, null, memo);
                frames?.Add(State.Capture(g, level, actions[i].Label));
                if (!ok)
                {
                    break;
                }
            }

            result.Status = g.Status;
            return result;
        }

        private sealed class Memo
        {
            public readonly Stack<string> Undo = new Stack<string>();
            public ActionKind LastAccepted = ActionKind.Step;

            /// <summary>Был кривой вызов: дальше партия могла испортиться им, а не игрой, и все находки — «защита».</summary>
            public bool Tainted;
        }

        /// <summary>Одно действие с проверками. false — партию дальше вести нельзя (ядро упало).</summary>
        private static bool Run(LoomGame g, QaAction a, int index, int seed, Depth depth, EpisodeResult result, Coverage? coverage, Memo memo)
        {
            var oracles = new Oracles();
            var before = new Before(g);
            Outcome outcome;
            var clock = Stopwatch.StartNew();
            try
            {
                outcome = ActionRunner.Apply(g, a);
            }
            catch (Exception e)
            {
                oracles.Exception(a, e);
                Record(result, oracles, index, memo.Tainted);
                return false;
            }

            var ms = clock.Elapsed.TotalMilliseconds;
            if (a.Kind == ActionKind.Hint && ms > result.SlowestHintMs)
            {
                result.SlowestHintMs = ms;
            }
            else if (a.Kind != ActionKind.Hint && ms > result.SlowestStepMs)
            {
                result.SlowestStepMs = ms;
                result.SlowestWhat = a.ToString();
            }

            if (outcome.Accepted && (a.Kind is ActionKind.Tap or ActionKind.Pull or ActionKind.Cut || a.Kind == ActionKind.Hint && outcome.Move != null))
            {
                memo.Undo.Push(before.Board);
            }

            // Выборка дорогих проверок зависит только от зерна и номера действия: повтор проверяет то же самое.
            var roll = new Random(unchecked(seed * 31 + index * 7919)).NextDouble();
            try
            {
                oracles.Check(g, before, a, outcome, memo.Undo, roll < depth.Expensive);
                if (roll < depth.Solver)
                {
                    oracles.HintLeadsToWin(g);
                }
            }
            catch (Exception e)
            {
                oracles.Exception(QaAction.Hint(), e);
            }

            if (coverage != null)
            {
                coverage.Observe(g, before, a, outcome);
                if (a.Kind == ActionKind.Undo && outcome.Flag == true && memo.LastAccepted == ActionKind.Cut)
                {
                    coverage.UndoAfterCut();
                }

                if (roll < depth.Expensive && g.Status == GameStatus.Playing && g.WinIsCertain())
                {
                    coverage.WinCertain();
                }
            }

            if (outcome.Accepted && a.Kind is not (ActionKind.Step or ActionKind.Tick))
            {
                memo.LastAccepted = a.Kind;
            }

            result.Snapshots.Add(State.Full(g));
            Record(result, oracles, index, memo.Tainted);

            // Кривой вызов, который ядро чисто отклонило, ничего не испортил — находки после него остаются ошибками.
            memo.Tainted |= a.IsRobustness && result.Snapshots[^1] != before.Full;
            return true;
        }

        private static void Record(EpisodeResult result, Oracles oracles, int index, bool tainted)
        {
            foreach (var found in oracles.Found)
            {
                var v = !tainted || found.Severity is Severity.Robustness or Severity.Warning or Severity.Info
                    ? found
                    : new Violation(found.Kind + "~после-кривого-вызова", Severity.Robustness, found.Title + " (после кривого вызова)", found.Detail)
                    {
                        MarkColumn = found.MarkColumn, MarkSlot = found.MarkSlot, MarkCell = found.MarkCell,
                    };
                if (result.Hits.All(h => h.Violation.Kind != v.Kind))
                {
                    result.Hits.Add(new Hit(v, index));
                }
            }
        }
    }

    /// <summary>
    /// Ужатие партии до минимального воспроизведения: ddmin (Zeller, Hildebrandt) по списку действий,
    /// затем упрощение чисел. Любая подпоследовательность исполнима (недопустимое ядро отклоняет),
    /// поэтому предикат простой: «повтор снова даёт то же нарушение».
    /// </summary>
    public static class Shrinker
    {
        public static List<QaAction> Shrink(LevelData level, IReadOnlyList<QaAction> actions, string kind, int seed, int budget, out int tests)
        {
            var depth = Episode.ExpensiveKinds.Contains(kind) ? Depth.Full : new Depth { Expensive = 0, Solver = 0 };
            var spent = 0;
            bool Fails(List<QaAction> candidate)
            {
                spent++;
                return Episode.Replay(level, candidate, seed, depth).Hits.Any(h => h.Violation.Kind == kind);
            }

            // Шаги станка по одному: так ddmin выкидывает лишние шаги, а не целые пачки.
            var current = new List<QaAction>();
            foreach (var a in actions)
            {
                if (a.Kind == ActionKind.Step && a.A > 1 && a.A <= 64)
                {
                    current.AddRange(Enumerable.Repeat(QaAction.Step(1), a.A));
                }
                else
                {
                    current.Add(a);
                }
            }

            if (!Fails(current))
            {
                // Нарушение зависело от выборки дорогих проверок или от личности — оставляем как есть.
                tests = spent;
                return actions.ToList();
            }

            var n = 2;
            while (current.Count >= 2 && spent < budget)
            {
                var chunk = (int)Math.Ceiling(current.Count / (double)n);
                var reduced = false;
                for (var start = 0; start < current.Count && spent < budget; start += chunk)
                {
                    var subset = current.Skip(start).Take(chunk).ToList();
                    if (subset.Count < current.Count && Fails(subset))
                    {
                        current = subset;
                        n = 2;
                        reduced = true;
                        break;
                    }

                    var complement = current.Take(start).Concat(current.Skip(start + chunk)).ToList();
                    if (complement.Count < current.Count && Fails(complement))
                    {
                        current = complement;
                        n = Math.Max(n - 1, 2);
                        reduced = true;
                        break;
                    }
                }

                if (!reduced)
                {
                    if (n >= current.Count)
                    {
                        break;
                    }

                    n = Math.Min(current.Count, n * 2);
                }
            }

            // Числа проще: время короче, глубина ножниц меньше.
            for (var i = 0; i < current.Count && spent < budget; i++)
            {
                var a = current[i];
                QaAction? simpler = a.Kind switch
                {
                    ActionKind.Tick when !float.IsNaN(a.T) && a.T > 1f => QaAction.Tick(MathF.Round(a.T / 2f, 2)),
                    ActionKind.Pull when a.B > 0 => QaAction.Pull(a.A, a.B - 1),
                    ActionKind.Step when a.A > 1 => QaAction.Step(a.A - 1),
                    _ => null,
                };
                if (simpler != null)
                {
                    var trial = current.ToList();
                    trial[i] = simpler.Value;
                    if (Fails(trial))
                    {
                        current = trial;
                        i--;
                    }
                }
            }

            // Слитые шаги проверяются иначе (оракул вязки смотрит одиночный шаг): сливаем, только если нарушение остаётся.
            var compressed = Compress(current);
            tests = spent + 1;
            return compressed.Count < current.Count && Fails(compressed) ? compressed : current;
        }

        /// <summary>Подряд идущие шаги — обратно в один «step N»: короче читать.</summary>
        public static List<QaAction> Compress(List<QaAction> actions)
        {
            var result = new List<QaAction>();
            foreach (var a in actions)
            {
                if (a.Kind == ActionKind.Step && result.Count > 0 && result[^1].Kind == ActionKind.Step)
                {
                    result[^1] = QaAction.Step(result[^1].A + a.A);
                }
                else
                {
                    result.Add(a);
                }
            }

            return result;
        }
    }
}
