#nullable enable

using System.Collections.Generic;

namespace KnittedPicture.Core
{
    /// <summary>
    /// Поиск решения перебором тапов. Нужен для проверки уровней:
    /// каждый уровень в игре должен проходиться без бустеров.
    /// </summary>
    public static class LevelSolver
    {
        /// <summary>
        /// Последовательность колонок для тапа, которая проходит уровень,
        /// или null, если решения нет (или перебор упёрся в maxStates).
        /// </summary>
        public static IReadOnlyList<int>? Solve(LevelData level, int maxStates = 200_000)
        {
            var game = new LoomGame(level);
            var visited = new HashSet<string>();
            var path = new List<int>();
            return Search(game, visited, path, maxStates) ? path : null;
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

            for (var column = 0; column < game.ColumnCount; column++)
            {
                if (!game.TapColumn(column).Accepted)
                {
                    continue;
                }

                path.Add(column);
                if (Search(game, visited, path, maxStates))
                {
                    return true;
                }

                path.RemoveAt(path.Count - 1);
                game.Undo();
            }

            return false;
        }
    }
}
