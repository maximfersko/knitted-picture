using System;
using System.Collections.Generic;
using System.Globalization;
using KnittedPicture.Core;

namespace KnittedPicture.QA
{
    /// <summary>Что может сделать игрок (и игра за него) с партией.</summary>
    public enum ActionKind
    {
        /// <summary>Тап по колонке поля.</summary>
        Tap,

        /// <summary>Ножницы по полю: достать клубок из глубины колонки.</summary>
        Pull,

        /// <summary>Ножницы по станку: срезать клубок со слота.</summary>
        Cut,

        /// <summary>Бустер «+1 слот».</summary>
        AddSlot,

        /// <summary>Бустер «Отменить».</summary>
        Undo,

        /// <summary>Шаги вязки станка (время идёт, игрок ждёт).</summary>
        Step,

        /// <summary>Время моли идёт (секунды).</summary>
        Tick,

        /// <summary>Спасение после моли (+секунды за видео).</summary>
        AddTime,

        /// <summary>Тап по колонке, которую советует подсказка (решатель).</summary>
        Hint,
    }

    /// <summary>
    /// Одно действие. Записывается строкой («tap 2», «step 3»), так что любую партию можно сохранить,
    /// повторить и ужать. Недопустимое действие ядро просто отклоняет, поэтому любая подпоследовательность
    /// партии тоже исполнима: на этом держится ужатие ddmin.
    /// </summary>
    public readonly struct QaAction : IEquatable<QaAction>
    {
        public QaAction(ActionKind kind, int a = 0, int b = 0, float t = 0f)
        {
            Kind = kind;
            A = a;
            B = b;
            T = t;
        }

        public ActionKind Kind { get; }

        public int A { get; }

        public int B { get; }

        public float T { get; }

        /// <summary>Действие, недоступное из интерфейса (кривые индексы, отрицательное время): проверка защиты ядра.</summary>
        public bool IsRobustness =>
            (Kind == ActionKind.Tick || Kind == ActionKind.AddTime) && (float.IsNaN(T) || float.IsInfinity(T) || T < 0f)
            || (Kind == ActionKind.Tap || Kind == ActionKind.Pull || Kind == ActionKind.Cut) && (A < 0 || A >= 64 || B < 0);

        public static QaAction Tap(int column) => new QaAction(ActionKind.Tap, column);

        public static QaAction Pull(int column, int depth) => new QaAction(ActionKind.Pull, column, depth);

        public static QaAction Cut(int slot) => new QaAction(ActionKind.Cut, slot);

        public static QaAction AddSlot() => new QaAction(ActionKind.AddSlot);

        public static QaAction Undo() => new QaAction(ActionKind.Undo);

        public static QaAction Step(int n = 1) => new QaAction(ActionKind.Step, n);

        public static QaAction Tick(float seconds) => new QaAction(ActionKind.Tick, 0, 0, seconds);

        public static QaAction AddTime(float seconds) => new QaAction(ActionKind.AddTime, 0, 0, seconds);

        public static QaAction Hint() => new QaAction(ActionKind.Hint);

        public override string ToString() => Kind switch
        {
            ActionKind.Tap => $"tap {A}",
            ActionKind.Pull => $"pull {A} {B}",
            ActionKind.Cut => $"cut {A}",
            ActionKind.AddSlot => "slot",
            ActionKind.Undo => "undo",
            ActionKind.Step => $"step {A}",
            ActionKind.Tick => $"tick {T.ToString("R", CultureInfo.InvariantCulture)}",
            ActionKind.AddTime => $"time {T.ToString("R", CultureInfo.InvariantCulture)}",
            ActionKind.Hint => "hint",
            _ => Kind.ToString(),
        };

        /// <summary>Подпись на «скриншоте»: латиница, коротко.</summary>
        public string Label => ToString().ToUpperInvariant();

        public static QaAction Parse(string text)
        {
            var p = text.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            int I(int i) => int.Parse(p[i], CultureInfo.InvariantCulture);
            float F(int i) => p[i] switch
            {
                "NaN" => float.NaN,
                "∞" or "Infinity" => float.PositiveInfinity,
                "-∞" or "-Infinity" => float.NegativeInfinity,
                _ => float.Parse(p[i], CultureInfo.InvariantCulture),
            };
            return p[0] switch
            {
                "tap" => Tap(I(1)),
                "pull" => Pull(I(1), I(2)),
                "cut" => Cut(I(1)),
                "slot" => AddSlot(),
                "undo" => Undo(),
                "step" => Step(I(1)),
                "tick" => Tick(F(1)),
                "time" => AddTime(F(1)),
                "hint" => Hint(),
                _ => throw new FormatException("неизвестное действие: " + text),
            };
        }

        public bool Equals(QaAction other) => Kind == other.Kind && A == other.A && B == other.B && T.Equals(other.T);

        public override bool Equals(object? obj) => obj is QaAction other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Kind, A, B, T);
    }

    /// <summary>Что вернуло ядро на действие: для оракулов и покрытия.</summary>
    public sealed class Outcome
    {
        public MoveResult? Move;
        public bool? Flag;
        public readonly List<StepResult> Steps = new List<StepResult>();

        /// <summary>Действие что-то сделало (ход принят, бустер сработал, шаг с прогрессом).</summary>
        public bool Accepted;

        /// <summary>Колонка, которую посоветовала подсказка (-1 — совета нет).</summary>
        public int HintColumn = -1;
    }

    public static class ActionRunner
    {
        /// <summary>Выполнить действие на партии. Исключения ядра не ловим: их ловит эпизод и считает падением.</summary>
        public static Outcome Apply(LoomGame g, QaAction a)
        {
            var o = new Outcome();
            switch (a.Kind)
            {
                case ActionKind.Tap:
                    o.Move = g.TapColumn(a.A);
                    o.Accepted = o.Move.Accepted;
                    break;
                case ActionKind.Pull:
                    o.Move = g.PullSpool(a.A, a.B);
                    o.Accepted = o.Move.Accepted;
                    break;
                case ActionKind.Cut:
                    o.Flag = g.CutFromLoom(a.A);
                    o.Accepted = o.Flag.Value;
                    break;
                case ActionKind.AddSlot:
                    o.Flag = g.AddSlot();
                    o.Accepted = o.Flag.Value;
                    break;
                case ActionKind.Undo:
                    o.Flag = g.Undo();
                    o.Accepted = o.Flag.Value;
                    break;
                case ActionKind.Step:
                    for (var i = 0; i < Math.Max(1, a.A); i++)
                    {
                        var r = g.Step();
                        o.Steps.Add(r);
                        o.Accepted |= r.Progress;
                    }

                    break;
                case ActionKind.Tick:
                    g.Tick(a.T);
                    o.Accepted = true;
                    break;
                case ActionKind.AddTime:
                    g.AddTime(a.T);
                    o.Accepted = true;
                    break;
                case ActionKind.Hint:
                    // Как в игре: подсказка показывается, только когда станок стоит и партия идёт.
                    if (g.IsBusy || g.Status != GameStatus.Playing)
                    {
                        break;
                    }

                    o.HintColumn = LevelSolver.Hint(g);
                    if (o.HintColumn >= 0)
                    {
                        o.Move = g.TapColumn(o.HintColumn);
                        o.Accepted = o.Move.Accepted;
                    }

                    break;
            }

            return o;
        }
    }
}
