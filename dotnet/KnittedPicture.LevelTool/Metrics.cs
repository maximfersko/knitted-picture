using System.Collections.Generic;
using System.Linq;
using KnittedPicture.Core;

namespace KnittedPicture.LevelTool
{
    /// <summary>
    /// Качество решений на уровне — по пути решателя: сколько цветов на фронте,
    /// как часто выбор очевиден (полезный клубок один) и как часто ход роковой
    /// (после него уровень уже не пройти без бустеров).
    /// </summary>
    public sealed class Metrics
    {
        private const int DangerCheckStates = 2500;

        public double FrontColors { get; private set; }

        /// <summary>Доля решений, где полезный клубок (с работой на фронте) не больше одного.</summary>
        public double Forced { get; private set; }

        /// <summary>Доля доступных ходов, после которых решения уже нет.</summary>
        public double Danger { get; private set; }

        public int Decisions { get; private set; }

        public static Metrics Measure(LevelData level, IReadOnlyList<int> solution)
        {
            var game = new LoomGame(level);
            game.Settle();
            var fronts = 0.0;
            var forced = 0;
            var legal = 0;
            var fatal = 0;
            foreach (var move in solution)
            {
                var options = Enumerable.Range(0, game.ColumnCount).Where(game.CanTap).ToList();
                var useful = options.Count(c => game.Column(c)[0].Knots == 0 && game.HasWork(game.Column(c)[0]));
                fronts += game.FrontColors().Count;
                if (useful <= 1)
                {
                    forced++;
                }

                foreach (var option in options)
                {
                    var probe = game.Clone();
                    probe.TapColumn(option);
                    probe.Settle();
                    legal++;
                    if (probe.Status != GameStatus.Won && LevelSolver.Solve(probe, DangerCheckStates) == null && Exhausted(probe))
                    {
                        fatal++;
                    }
                }

                game.TapColumn(move);
                game.Settle();
            }

            var decisions = solution.Count;
            return new Metrics
            {
                Decisions = decisions,
                FrontColors = decisions == 0 ? 0 : fronts / decisions,
                Forced = decisions == 0 ? 0 : (double)forced / decisions,
                Danger = legal == 0 ? 0 : (double)fatal / legal,
            };
        }

        /// <summary>
        /// Решатель мог упереться в лимит состояний — тогда «нет решения» не доказано.
        /// Перепроверяем с запасом: роковым считаем только то, что не решилось и с большим лимитом.
        /// </summary>
        private static bool Exhausted(LoomGame probe) => LevelSolver.Solve(probe, DangerCheckStates * 8) == null;
    }
}
