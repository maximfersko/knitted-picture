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
            return Search(copy, visited, path, maxStates) ? path : null;
        }

        /// <summary>Колонка для следующего тапа по пути к победе или -1, если пути нет.</summary>
        public static int Hint(LoomGame game, int maxStates = 20_000)
        {
            var path = Solve(game, maxStates);
            return path != null && path.Count > 0 ? path[0] : -1;
        }

        private static bool Search(LoomGame game, HashSet<string> visited, List<int> path, int maxStates)
        {
            if (game.Status == GameStatus.Won)
            {
                return true;
            }

            if (game.Status != GameStatus.Playing || visited.Count >= maxStates || !visited.Add(game.StateKey()))
            {
                return false;
            }

            // Сначала колонки с цветом, который можно вязать сразу: решение находится быстрее.
            var front = game.FrontColors();
            for (var pass = 0; pass < 2; pass++)
            {
                for (var column = 0; column < game.ColumnCount; column++)
                {
                    var spool = game.Column(column).Count > 0 ? game.Column(column)[0] : null;
                    var preferred = spool != null && front.Contains(spool.Color);
                    if (preferred != (pass == 0) || !game.TapColumn(column).Accepted)
                    {
                        continue;
                    }

                    game.Settle();
                    path.Add(column);
                    if (Search(game, visited, path, maxStates))
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
