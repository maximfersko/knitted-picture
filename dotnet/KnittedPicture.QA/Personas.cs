using System;
using System.Collections.Generic;
using System.Linq;
using KnittedPicture.Core;

namespace KnittedPicture.QA
{
    /// <summary>
    /// «Личность» игрока: как он выбирает следующее действие. Разные личности ломают игру по-разному
    /// (процедурные персоны из исследований автоматического плейтеста): новичок, знаток, торопыга,
    /// вредитель, любитель бустеров, часовщик, решатель, исследователь новизны и ломатель API.
    /// </summary>
    public abstract class Persona
    {
        public abstract string Name { get; }

        public abstract string About { get; }

        /// <summary>Следующее действие. Может вернуть null — личность сдалась (тогда эпизод кончается).</summary>
        public abstract QaAction? Next(LoomGame g, Random rng);

        public static IReadOnlyList<Persona> All(Novelty novelty) => new Persona[]
        {
            new Monkey(), new Novice(), new Expert(), new Speedrunner(), new Saboteur(), new BoosterFan(),
            new Clockmaker(), new Follower(), new Explorer(novelty), new Breaker(),
        };

        protected static List<int> Tappable(LoomGame g) => Enumerable.Range(0, g.ColumnCount).Where(g.CanTap).ToList();

        protected static QaAction Wait(Random rng) => QaAction.Step(rng.Next(1, 5));

        protected static Spool? Front(LoomGame g, int c) => g.Column(c).Count > 0 ? g.Column(c)[0] : null;

        /// <summary>Любое действие интерфейса со случайными, но допустимыми по смыслу параметрами.</summary>
        protected static QaAction AnyUi(LoomGame g, Random rng)
        {
            var roll = rng.NextDouble();
            if (roll < 0.45)
            {
                return QaAction.Tap(rng.Next(g.ColumnCount));
            }

            if (roll < 0.52)
            {
                var c = rng.Next(g.ColumnCount);
                return QaAction.Pull(c, g.Column(c).Count > 0 ? rng.Next(g.Column(c).Count) : 0);
            }

            if (roll < 0.58)
            {
                return QaAction.Cut(rng.Next(g.Slots.Count));
            }

            if (roll < 0.62)
            {
                return QaAction.AddSlot();
            }

            if (roll < 0.69)
            {
                return QaAction.Undo();
            }

            if (roll < 0.74)
            {
                return QaAction.Tick((float)Math.Round(rng.NextDouble() * 4, 2));
            }

            if (roll < 0.76 && g.Status == GameStatus.TimeUp)
            {
                return QaAction.AddTime(20f);
            }

            if (roll < 0.78)
            {
                return QaAction.Hint();
            }

            return Wait(rng);
        }
    }

    /// <summary>Обезьяна: случайные действия интерфейса без разбора (классический monkey-тест).</summary>
    public sealed class Monkey : Persona
    {
        public override string Name => "обезьяна";

        public override string About => "случайные действия интерфейса без разбора";

        public override QaAction? Next(LoomGame g, Random rng) => AnyUi(g, rng);
    }

    /// <summary>Новичок: тапает то, что можно тапнуть, ждёт станок, бустеров почти не знает.</summary>
    public sealed class Novice : Persona
    {
        public override string Name => "новичок";

        public override string About => "тапает доступное, ждёт станок, бустеры редко";

        public override QaAction? Next(LoomGame g, Random rng)
        {
            if (g.Status == GameStatus.Jammed)
            {
                return rng.NextDouble() < 0.5 ? QaAction.Undo() : null;
            }

            if (g.Status == GameStatus.TimeUp)
            {
                return rng.NextDouble() < 0.5 ? QaAction.AddTime(20f) : null;
            }

            var taps = Tappable(g);
            if (taps.Count > 0 && rng.NextDouble() < 0.6)
            {
                return QaAction.Tap(taps[rng.Next(taps.Count)]);
            }

            return rng.NextDouble() < 0.15 ? QaAction.Tick(0.5f) : Wait(rng);
        }
    }

    /// <summary>Знаток: короткие нужные клубки первыми, узелки заранее, при заторе — правильный бустер.</summary>
    public sealed class Expert : Persona
    {
        public override string Name => "знаток";

        public override string About => "нужные клубки, короткие первыми; при заторе — верный бустер";

        public override QaAction? Next(LoomGame g, Random rng)
        {
            if (g.Status == GameStatus.Jammed)
            {
                if (g.SlotWouldHelp && g.Slots.Count < LoomGame.MaxSlots)
                {
                    return QaAction.AddSlot();
                }

                for (var s = 0; s < g.Slots.Count; s++)
                {
                    var spool = g.Slots[s];
                    if (spool != null && (g.IsForeign(spool) || g.IsSurplus(spool) || !g.HasWork(spool)))
                    {
                        return QaAction.Cut(s);
                    }
                }

                return g.CanUndo ? QaAction.Undo() : null;
            }

            if (g.Status == GameStatus.TimeUp)
            {
                return QaAction.AddTime(20f);
            }

            var best = -1;
            var bestScore = double.MaxValue;
            foreach (var c in Tappable(g))
            {
                var s = Front(g, c)!;
                var score = s.Knots > 0 ? 0 : g.HasWork(s) ? s.Loops : 1000;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = c;
                }
            }

            if (best >= 0 && (bestScore < 1000 || g.FreeSlots > 2) && (!g.IsBusy || g.FreeSlots > 0))
            {
                return QaAction.Tap(best);
            }

            return Wait(rng);
        }
    }

    /// <summary>Торопыга: тапает всё подряд, не дожидаясь станка (быстрые пальцы при анимации).</summary>
    public sealed class Speedrunner : Persona
    {
        public override string Name => "торопыга";

        public override string About => "серии тапов без ожидания станка";

        public override QaAction? Next(LoomGame g, Random rng)
        {
            if (g.Status is GameStatus.Jammed or GameStatus.TimeUp)
            {
                return rng.NextDouble() < 0.5 ? QaAction.Undo() : QaAction.AddSlot();
            }

            return rng.NextDouble() < 0.85 ? QaAction.Tap(rng.Next(g.ColumnCount)) : QaAction.Step(1);
        }
    }

    /// <summary>Вредитель: берёт ненужное, забивает станок, потом выкручивается срезами и отменой.</summary>
    public sealed class Saboteur : Persona
    {
        public override string Name => "вредитель";

        public override string About => "берёт ненужное, забивает станок, выкручивается бустерами";

        public override QaAction? Next(LoomGame g, Random rng)
        {
            if (g.Status == GameStatus.Jammed)
            {
                var roll = rng.NextDouble();
                if (roll < 0.35 && g.Slots.Count > 0)
                {
                    return QaAction.Cut(rng.Next(g.Slots.Count));
                }

                if (roll < 0.6)
                {
                    var c = rng.Next(g.ColumnCount);
                    return QaAction.Pull(c, g.Column(c).Count > 0 ? g.Column(c).Count - 1 : 0);
                }

                return roll < 0.8 ? QaAction.Undo() : QaAction.AddSlot();
            }

            var worst = Tappable(g).OrderByDescending(c => Front(g, c) is { } s && !g.HasWork(s) ? 1 : 0).ThenBy(_ => rng.Next()).FirstOrDefault(-1);
            if (worst >= 0 && rng.NextDouble() < 0.7)
            {
                return QaAction.Tap(worst);
            }

            return rng.NextDouble() < 0.2 ? QaAction.Hint() : Wait(rng);
        }
    }

    /// <summary>Любитель бустеров: штормы отмен, срез сразу после хода (клубок ещё «летит»), ножницы из глубины, слоты до упора.</summary>
    public sealed class BoosterFan : Persona
    {
        private int _undoStorm;

        public override string Name => "бустерщик";

        public override string About => "шторм отмен, срез в полёте, ножницы из глубины, слоты до упора";

        public override QaAction? Next(LoomGame g, Random rng)
        {
            if (_undoStorm > 0)
            {
                _undoStorm--;
                return QaAction.Undo();
            }

            var roll = rng.NextDouble();
            if (roll < 0.08)
            {
                _undoStorm = rng.Next(2, 12);
                return QaAction.Undo();
            }

            if (roll < 0.25)
            {
                // Срез ровно того слота, куда только что встал клубок: он ещё «летит».
                var flying = Enumerable.Range(0, g.Slots.Count).Where(s => g.Slots[s] is { Warmup: > 0 }).ToList();
                return QaAction.Cut(flying.Count > 0 ? flying[rng.Next(flying.Count)] : rng.Next(g.Slots.Count));
            }

            if (roll < 0.4)
            {
                var c = rng.Next(g.ColumnCount);
                return QaAction.Pull(c, Math.Max(0, g.Column(c).Count - 1 - rng.Next(2)));
            }

            if (roll < 0.5)
            {
                return QaAction.AddSlot();
            }

            if (roll < 0.75)
            {
                var taps = Tappable(g);
                return taps.Count > 0 ? QaAction.Tap(taps[rng.Next(taps.Count)]) : Wait(rng);
            }

            return Wait(rng);
        }
    }

    /// <summary>Часовщик: большие скачки времени (телефон свернули), моль, спасение, ходы и отмена при съеденной картинке.</summary>
    public sealed class Clockmaker : Persona
    {
        public override string Name => "часовщик";

        public override string About => "скачки времени, моль, спасение, ходы при TimeUp";

        public override QaAction? Next(LoomGame g, Random rng)
        {
            var roll = rng.NextDouble();
            if (g.Status == GameStatus.TimeUp)
            {
                return roll < 0.4 ? QaAction.AddTime(rng.Next(2) == 0 ? 20f : 0.5f)
                    : roll < 0.6 ? QaAction.Tap(rng.Next(g.ColumnCount))
                    : roll < 0.75 ? QaAction.Undo()
                    : roll < 0.85 ? QaAction.Tick(30f)
                    : QaAction.Step(2);
            }

            if (roll < 0.3)
            {
                return QaAction.Tick(rng.Next(3) switch { 0 => 0.016f, 1 => 7.5f, _ => 120f });
            }

            var taps = Tappable(g);
            if (taps.Count > 0 && roll < 0.7)
            {
                return QaAction.Tap(taps[rng.Next(taps.Count)]);
            }

            return g.Status == GameStatus.Jammed ? QaAction.Undo() : Wait(rng);
        }
    }

    /// <summary>Решатель: ждёт, пока станок встанет, и ходит по подсказке. На уровне без моли обязан выиграть.</summary>
    public sealed class Follower : Persona
    {
        private int _hintedAt = -1;

        public override string Name => "решатель";

        public override string About => "ходит только по подсказке; без моли обязан выиграть";

        /// <summary>Подсказка промолчала (станок стоит, а ход по ней не случился): решатель сдаётся.</summary>
        public bool GaveUp { get; private set; }

        public override QaAction? Next(LoomGame g, Random rng)
        {
            if (g.Status != GameStatus.Playing)
            {
                return null;
            }

            if (g.IsBusy)
            {
                return QaAction.Step(4);
            }

            if (_hintedAt == g.Version)
            {
                GaveUp = true;
                return null;
            }

            _hintedAt = g.Version;
            return QaAction.Hint();
        }
    }

    /// <summary>Ломатель: кривые индексы и время (NaN, −5, бесконечность) — проверка защиты ядра от ошибок интерфейса.</summary>
    public sealed class Breaker : Persona
    {
        public override string Name => "ломатель";

        public override string About => "кривые индексы и время: защита ядра";

        public override QaAction? Next(LoomGame g, Random rng)
        {
            if (rng.NextDouble() < 0.5)
            {
                return AnyUi(g, rng);
            }

            return rng.Next(8) switch
            {
                0 => QaAction.Tap(rng.Next(2) == 0 ? -1 : g.ColumnCount),
                1 => QaAction.Pull(rng.Next(g.ColumnCount), g.Column(0).Count + 3),
                2 => QaAction.Pull(-1, 0),
                3 => QaAction.Cut(rng.Next(2) == 0 ? -1 : g.Slots.Count + 2),
                4 => QaAction.Tick(rng.Next(3) switch { 0 => float.NaN, 1 => -5f, _ => float.PositiveInfinity }),
                5 => QaAction.AddTime(rng.Next(2) == 0 ? -30f : float.NaN),
                6 => QaAction.Step(rng.Next(50, 400)),
                _ => QaAction.Tick(0f),
            };
        }
    }

    /// <summary>
    /// Память новизны для исследователя: сколько раз видели каждое состояние доски.
    /// Общая на все партии уровня — как «любопытство» RL-агентов, только без обучения.
    /// </summary>
    public sealed class Novelty
    {
        private readonly Dictionary<string, int> _visits = new Dictionary<string, int>();

        public int Seen(string key) => _visits.TryGetValue(key, out var n) ? n : 0;

        public void Visit(string key) => _visits[key] = Seen(key) + 1;

        public int Count => _visits.Count;

        public void Reset() => _visits.Clear();
    }

    /// <summary>
    /// Исследователь: пробует каждое возможное действие на копии и выбирает то, что ведёт в наименее
    /// виданное состояние (поиск новизны, novelty search). Так бот сам лезет в редкие углы игры.
    /// </summary>
    public sealed class Explorer : Persona
    {
        private readonly Novelty _novelty;

        public Explorer(Novelty novelty)
        {
            _novelty = novelty;
        }

        public override string Name => "исследователь";

        public override string About => "выбирает ход в самое невиданное состояние (поиск новизны)";

        public override QaAction? Next(LoomGame g, Random rng)
        {
            if (rng.NextDouble() < 0.1)
            {
                return AnyUi(g, rng);
            }

            var candidates = new List<QaAction>();
            for (var c = 0; c < g.ColumnCount; c++)
            {
                candidates.Add(QaAction.Tap(c));
                if (g.Column(c).Count > 1)
                {
                    candidates.Add(QaAction.Pull(c, g.Column(c).Count - 1));
                }
            }

            for (var s = 0; s < g.Slots.Count; s++)
            {
                if (g.Slots[s] != null)
                {
                    candidates.Add(QaAction.Cut(s));
                }
            }

            candidates.Add(QaAction.AddSlot());
            candidates.Add(QaAction.Undo());
            candidates.Add(QaAction.Step(1));
            candidates.Add(QaAction.Step(4));
            if (g.TimeLimit > 0)
            {
                candidates.Add(QaAction.Tick(10f));
                candidates.Add(QaAction.AddTime(20f));
            }

            QaAction? best = null;
            var bestScore = double.MaxValue;
            foreach (var a in candidates)
            {
                var copy = g.Clone();
                var o = ActionRunner.Apply(copy, a);
                if (!o.Accepted && a.Kind != ActionKind.Step)
                {
                    continue;
                }

                var score = _novelty.Seen(Key(copy)) + rng.NextDouble() * 0.5;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = a;
                }
            }

            return best ?? (g.Status == GameStatus.Playing ? QaAction.Step(1) : null);
        }

        public static string Key(LoomGame g) => State.Board(g) + g.Status;
    }
}
