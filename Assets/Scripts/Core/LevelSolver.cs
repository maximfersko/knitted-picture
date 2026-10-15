#nullable enable

using System.Collections.Generic;

namespace KnittedPicture.Core
{
    /// <summary>
    /// Поиск решения перебором тапов без бустеров. Модель хода: тап, затем станок вяжет,
    /// пока не встанет. Нужен для проверки уровней и для подсказки на первых уровнях.
    /// </summary>
    public static class LevelSolver
    {
        /// <summary>
        /// Последовательность колонок для тапа, которая проходит уровень,
        /// или null, если решения нет (или перебор упёрся в maxStates).
        /// </summary>
        public static IReadOnlyList<int>? Solve(LevelData level, int maxStates = 300_000) =>
            Solve(new LoomGame(level), maxStates);

        /// <summary>Решение из текущего состояния игры; сама игра не меняется.</summary>
        public static IReadOnlyList<int>? Solve(LoomGame game, int maxStates = 300_000)
        {
            var copy = game.Clone();
            copy.Settle();
            var visited = new HashSet<string>();
            var path = new List<int>();
            return Search(copy, visited, path, maxStates, null, int.MaxValue) ? path : null;
        }

        /// <summary>
        /// Колонка для следующего тапа по пути к победе или -1, если пути нет. maxMilliseconds — предел по времени:
        /// перебор останавливается, даже если не упёрся в maxStates (на слабом телефоне он в 5–10 раз медленнее).
        /// </summary>
        public static int Hint(LoomGame game, int maxStates = 20_000, int maxMilliseconds = int.MaxValue)
        {
            var copy = game.Clone();
            copy.Settle();
            var path = new List<int>();
            var clock = System.Diagnostics.Stopwatch.StartNew();
            return Search(copy, new HashSet<string>(), path, maxStates, clock, maxMilliseconds) && path.Count > 0 ? path[0] : -1;
        }

        /// <summary>
        /// Наименьшее найденное число тапов до победы (перебор с отсечением по уже найденному лучшему).
        /// Если перебор упёрся в maxNodes, ответ — лучшее из найденного, а не обязательно минимум.
        /// -1 — решения не нашлось.
        /// </summary>
        public static int FewestTaps(LevelData level, int maxNodes = 400_000)
        {
            var game = new LoomGame(level);
            game.Settle();
            var best = int.MaxValue;
            var seen = new Dictionary<string, int>();
            var nodes = 0;
            Shortest(game, seen, ref best, ref nodes, maxNodes);
            return best == int.MaxValue ? -1 : best;
        }

        private static void Shortest(LoomGame game, Dictionary<string, int> seen, ref int best, ref int nodes, int maxNodes)
        {
            if (game.Status == GameStatus.Won)
            {
                best = System.Math.Min(best, game.Taps);
                return;
            }

            // До победы нужен хотя бы ещё один тап: ветку, которая не обгонит лучшее, можно не смотреть.
            if (game.Status != GameStatus.Playing || game.Taps + 1 >= best || nodes++ >= maxNodes)
            {
                return;
            }

            var key = game.StateKey();
            if (seen.TryGetValue(key, out var taps) && taps <= game.Taps)
            {
                return;
            }

            seen[key] = game.Taps;
            for (var pass = 0; pass < 2; pass++)
            {
                for (var column = 0; column < game.ColumnCount; column++)
                {
                    var spool = game.Column(column).Count > 0 ? game.Column(column)[0] : null;
                    var preferred = spool != null && game.HasWork(spool);
                    if (preferred != (pass == 0) || !game.TapColumn(column).Accepted)
                    {
                        continue;
                    }

                    game.Settle();
                    Shortest(game, seen, ref best, ref nodes, maxNodes);
                    game.Undo();
                }
            }
        }

        private static bool Search(LoomGame game, HashSet<string> visited, List<int> path, int maxStates, System.Diagnostics.Stopwatch? clock,
            int maxMilliseconds)
        {
            if (game.Status == GameStatus.Won)
            {
                return true;
            }

            if (game.Status != GameStatus.Playing || visited.Count >= maxStates || !visited.Add(game.StateKey()))
            {
                return false;
            }

            // Время проверяем раз в 256 состояний: часы дороже, чем кажется.
            if (clock != null && (visited.Count & 255) == 0 && clock.ElapsedMilliseconds > maxMilliseconds)
            {
                return false;
            }

            // Сначала колонки с цветом, который можно вязать сразу: решение находится быстрее.
            for (var pass = 0; pass < 2; pass++)
            {
                for (var column = 0; column < game.ColumnCount; column++)
                {
                    var spool = game.Column(column).Count > 0 ? game.Column(column)[0] : null;
                    var preferred = spool != null && game.HasWork(spool);
                    if (preferred != (pass == 0) || !game.TapColumn(column).Accepted)
                    {
                        continue;
                    }

                    game.Settle();
                    path.Add(column);
                    if (Search(game, visited, path, maxStates, clock, maxMilliseconds))
                    {
                        return true;
                    }

                    path.RemoveAt(path.Count - 1);
                    game.Undo();
                }
            }

            return false;
        }
    }
}
