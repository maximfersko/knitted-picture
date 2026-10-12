using System.Collections.Generic;
using KnittedPicture.Core;
using KnittedPicture.LevelTool;

namespace KnittedPicture.Playtest
{
    /// <summary>Профиль сложности уровня: как часто его проходят разные игроки.</summary>
    public sealed class LevelProfile
    {
        public int Id;
        public string Name = "";
        public bool Hard;
        public bool Timed;
        public double Random;
        public double Average;
        public double EarlyJam;
        public double Follower;
        public double Skilled;
        public readonly List<string> Flags = new List<string>();
    }

    /// <summary>
    /// Четыре игрока из генератора уровней (LevelTool/Bot): наугад, «средний», «по подсветке», «знающий».
    /// Отмечаются уровни, которые выбиваются из кривой: проходятся без мыслей, ощущаются нечестными,
    /// часто обрываются затором в самом начале или «сложный» проще обычного.
    /// </summary>
    public static class Difficulty
    {
        public static List<LevelProfile> Run(IReadOnlyList<LevelData> levels, int runs, int seed)
        {
            var result = new List<LevelProfile>();
            foreach (var level in levels)
            {
                var (average, early) = Bot.Profile(level, runs, seed + level.id);
                var p = new LevelProfile
                {
                    Id = level.id,
                    Name = level.name,
                    Hard = level.hard,
                    Timed = level.timeLimit > 0,
                    Random = Bot.RandomWinRate(level, runs, seed + level.id),
                    Average = average,
                    EarlyJam = early,
                    Follower = Bot.FollowerWinRate(level, runs, seed + level.id),
                    Skilled = Bot.SkilledWinRate(level, runs, seed + level.id),
                };

                if (level.id > 5 && p.Random > 0.7)
                {
                    p.Flags.Add("проходится наугад");
                }

                if (!p.Hard && level.id > 3 && p.Follower < 0.3)
                {
                    p.Flags.Add("честная игра часто проигрывает");
                }

                if (p.EarlyJam > 0.35)
                {
                    p.Flags.Add("частый затор в самом начале");
                }

                if (p.Hard && p.Average > 0.8)
                {
                    p.Flags.Add("«сложный», но лёгкий");
                }

                if (p.Skilled + 0.15 < p.Follower)
                {
                    p.Flags.Add("приём «короткие клубки первыми» тут вредит");
                }

                result.Add(p);
            }

            return result;
        }
    }
}
