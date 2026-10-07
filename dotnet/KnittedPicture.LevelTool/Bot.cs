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
        /// <summary>Затор раньше этой доли картинки — «ранний»: игрок проиграл, толком не начав.</summary>
        public const double EarlyJamProgress = 0.3;

        public static double WinRate(LevelData level, int runs, int seed) => Profile(level, runs, seed).Win;

        /// <summary>
        /// Доля побед «среднего игрока» и доля его партий, оборванных ранним затором.
        /// Ранний затор ощущается лотереей; затор под конец — «почти получилось», он зовёт сыграть ещё.
        /// </summary>
        public static (double Win, double EarlyJam) Profile(LevelData level, int runs, int seed)
        {
            var rng = new Random(seed);
            var wins = 0;
            var early = 0;
            for (var run = 0; run < runs; run++)
            {
                var game = Play(level, rng);
                if (game.Status == GameStatus.Won)
                {
                    wins++;
                }
                else if (game.Status == GameStatus.Jammed && game.KnittedCount < game.TotalStitches * EarlyJamProgress)
                {
                    early++;
                }
            }

            return ((double)wins / runs, (double)early / runs);
        }

        /// <summary>
        /// Доля побед игрока, который идёт «по подсветке»: берёт передний клубок, которому сразу есть работа
        /// (в игре такие клубки «дышат»), а если таких нет — узелок, скрытый или цвет, нужный вот-вот.
        /// Если на обычном уровне такая честная игра часто проигрывает, уровень ощущается нечестным.
        /// </summary>
        public static double FollowerWinRate(LevelData level, int runs, int seed) => Rate(level, runs, seed * 13 + 5, skilled: false);

        /// <summary>
        /// Доля побед игрока, который знает два приёма: короткие клубки первыми (быстрее освобождают станок)
        /// и узелки развязывать заранее, пока станок занят. На сложном уровне приёмы должны помогать,
        /// но не решать его целиком; на «стене» без приёма не обойтись даже знающему.
        /// </summary>
        public static double SkilledWinRate(LevelData level, int runs, int seed) => Rate(level, runs, seed * 17 + 9, skilled: true);

        private static double Rate(LevelData level, int runs, int seed, bool skilled)
        {
            var rng = new Random(seed);
            var wins = 0;
            for (var run = 0; run < runs; run++)
            {
                var game = new LoomGame(level);
                for (var step = 0; step < 1000 && game.Status == GameStatus.Playing; step++)
                {
                    var options = new List<int>();
                    for (var c = 0; c < game.ColumnCount; c++)
                    {
                        if (game.CanTap(c))
                        {
                            options.Add(c);
                        }
                    }

                    if (options.Count == 0)
                    {
                        break;
                    }

                    game.TapColumn(skilled ? Skilled(game, options, rng) : Follower(game, options, rng));
                    game.Settle();
                }

                if (game.Status == GameStatus.Won)
                {
                    wins++;
                }
            }

            return (double)wins / runs;
        }

        private static int Follower(LoomGame game, List<int> options, Random rng)
        {
            var soon = game.SoonColors(2);
            double Score(int c)
            {
                var spool = game.Column(c)[0];
                if (spool.Knots > 0)
                {
                    return 2;
                }

                if (!spool.IsColorVisible)
                {
                    return 1.5;
                }

                if (game.IsForeign(spool))
                {
                    return 0;
                }

                if (game.IsSurplus(spool))
                {
                    return 0.5;
                }

                return game.HasWork(spool) ? 10 : soon.Contains(spool.Color) ? 3 : 1;
            }

            var best = options.Max(Score);
            var top = options.Where(c => Score(c) == best).ToList();
            return top[rng.Next(top.Count)];
        }

        private static int Skilled(LoomGame game, List<int> options, Random rng)
        {
            var working = options.Where(c => game.Column(c)[0].Knots == 0 && game.Column(c)[0].IsColorVisible && game.HasWork(game.Column(c)[0])).ToList();
            var knots = options.Where(c => game.Column(c)[0].Knots > 0).ToList();
            if (knots.Count > 0 && (working.Count == 0 || rng.NextDouble() < 0.5))
            {
                return knots[rng.Next(knots.Count)];
            }

            if (working.Count == 0)
            {
                return Follower(game, options, rng);
            }

            var shortest = working.Min(c => game.Column(c)[0].Loops);
            var top = working.Where(c => game.Column(c)[0].Loops == shortest).ToList();
            return top[rng.Next(top.Count)];
        }

        /// <summary>
        /// Доля побед игрока, который тапает наугад: любой доступный клубок с равной вероятностью.
        /// Если она высокая, уровень проходится без мыслей.
        /// </summary>
        public static double RandomWinRate(LevelData level, int runs, int seed)
        {
            var rng = new Random(seed * 7 + 1);
            var wins = 0;
            for (var run = 0; run < runs; run++)
            {
                var game = new LoomGame(level);
                for (var step = 0; step < 1000 && game.Status == GameStatus.Playing; step++)
                {
                    var options = new List<int>();
                    for (var c = 0; c < game.ColumnCount; c++)
                    {
                        if (game.CanTap(c))
                        {
                            options.Add(c);
                        }
                    }

                    if (options.Count == 0)
                    {
                        break;
                    }

                    game.TapColumn(options[rng.Next(options.Count)]);
                    game.Settle();
                }

                if (game.Status == GameStatus.Won)
                {
                    wins++;
                }
            }

            return (double)wins / runs;
        }

        private static LoomGame Play(LevelData level, Random rng)
        {
            var game = new LoomGame(level);
            for (var step = 0; step < 1000 && game.Status == GameStatus.Playing; step++)
            {
                var soon = game.SoonColors(2);
                var options = new List<int>();
                var weights = new List<double>();
                for (var c = 0; c < game.ColumnCount; c++)
                {
                    if (!game.CanTap(c))
                    {
                        continue;
                    }

                    var spool = game.Column(c)[0];
                    double weight;
                    if (spool.Knots > 0)
                    {
                        weight = 2;
                    }
                    else if (!spool.IsColorVisible)
                    {
                        weight = 1.5;
                    }
                    else if (game.IsForeign(spool))
                    {
                        weight = 0.3;
                    }
                    else if (game.IsSurplus(spool))
                    {
                        weight = 0.5;
                    }
                    else if (game.HasWork(spool))
                    {
                        weight = 8;
                    }
                    else if (soon.Contains(spool.Color))
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
                game.Settle();
            }

            return game;
        }
    }
}
