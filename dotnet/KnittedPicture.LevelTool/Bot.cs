using System;
using System.Collections.Generic;
using System.Linq;
using KnittedPicture.Core;

namespace KnittedPicture.LevelTool
{
    /// <summary>
    /// Бот «средний игрок»: чаще берёт клубки с нужным сейчас или скоро цветом,
    /// но иногда ошибается. Доля его побед — мерило сложности уровня.
    /// </summary>
    public static class Bot
    {
        public static double WinRate(LevelData level, int runs, int seed)
        {
            var rng = new Random(seed);
            var wins = 0;
            for (var run = 0; run < runs; run++)
            {
                if (Play(level, rng))
                {
                    wins++;
                }
            }

            return (double)wins / runs;
        }

        private static bool Play(LevelData level, Random rng)
        {
            var game = new LoomGame(level);
            for (var step = 0; step < 1000 && game.Status == GameStatus.Playing; step++)
            {
                var upcoming = game.UpcomingColors(game.Width * 2).Take(3).ToList();
                var options = new List<int>();
                var weights = new List<double>();
                for (var c = 0; c < game.ColumnCount; c++)
                {
                    if (!game.CanTap(c))
                    {
                        continue;
                    }

                    var front = game.Column(c)[0];
                    double weight;
                    if (front.Knots > 0)
                    {
                        weight = 2;
                    }
                    else if (!front.IsColorVisible)
                    {
                        weight = 1.5;
                    }
                    else if (upcoming.Count > 0 && front.Color == upcoming[0])
                    {
                        weight = 8;
                    }
                    else if (upcoming.Contains(front.Color))
                    {
                        weight = 3;
                    }
                    else
                    {
                        weight = 1;
                    }

                    options.Add(c);
                    weights.Add(weight);
                }

                if (options.Count == 0)
                {
                    break;
                }

                var roll = rng.NextDouble() * weights.Sum();
                var pick = options[options.Count - 1];
                for (var i = 0; i < options.Count; i++)
                {
                    roll -= weights[i];
                    if (roll <= 0)
                    {
                        pick = options[i];
                        break;
                    }
                }

                game.TapColumn(pick);
            }

            return game.Status == GameStatus.Won;
        }
    }
}
