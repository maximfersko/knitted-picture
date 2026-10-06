#nullable enable

using System.Collections.Generic;

namespace KnittedPicture.Core
{
    public enum MissionKind
    {
        /// <summary>Пройти N уровней (любых, и повторы тоже).</summary>
        Win,

        /// <summary>Пройти N уровней на три звезды.</summary>
        ThreeStars,

        /// <summary>Поймать N золотых петель.</summary>
        Gold,

        /// <summary>Включить «Разгон» N раз.</summary>
        Rush,

        /// <summary>Разогнать комбо до ×N за один уровень.</summary>
        Combo,

        /// <summary>Связать N петель.</summary>
        Stitches,

        /// <summary>Пройти N уровней без бустеров и спасений.</summary>
        Clean,
    }

    /// <summary>Одно задание дня: что сделать, сколько нужно и сколько уже сделано.</summary>
    public readonly struct Mission
    {
        public Mission(MissionKind kind, int target, int value)
        {
            Kind = kind;
            Target = target;
            Value = System.Math.Min(value, target);
        }

        public MissionKind Kind { get; }

        public int Target { get; }

        public int Value { get; }

        public bool Done => Value >= Target;

        public string Text => DailyMissions.Text(Kind, Target);
    }

    /// <summary>
    /// Задания дня: три простых дела, которые ведут в саму игру, а не «зашёл и забрал».
    /// Набор на день выбирается по дате (одинаковый весь день), за все три — награда.
    /// Мелкие счётчики, которые раньше были достижениями (разгон, золото, комбо, петли), живут здесь.
    /// </summary>
    public static class DailyMissions
    {
        public const int Count = 3;

        /// <summary>С какого уровня открываются задания: после первых обучающих.</summary>
        public const int FromLevel = 4;

        /// <summary>Золотые петли появляются на 6-м уровне: задание про золото — только после него.</summary>
        public const int GoldFromLevel = 7;

        /// <summary>Набор заданий на день: первое — лёгкое (пройти уровни или связать петли), остальные разные.</summary>
        public static List<(MissionKind kind, int target)> Roll(int day, int nextLevel)
        {
            var rng = new System.Random(day * 31 + 7);
            var easy = new[] { MissionKind.Win, MissionKind.Stitches };
            var pool = new List<MissionKind> { MissionKind.ThreeStars, MissionKind.Rush, MissionKind.Combo, MissionKind.Clean };
            if (nextLevel >= GoldFromLevel)
            {
                pool.Add(MissionKind.Gold);
            }

            var first = easy[rng.Next(easy.Length)];
            pool.Add(first == MissionKind.Win ? MissionKind.Stitches : MissionKind.Win);
            var result = new List<(MissionKind, int)> { (first, Target(first, rng)) };
            while (result.Count < Count)
            {
                var kind = pool[rng.Next(pool.Count)];
                pool.Remove(kind);
                result.Add((kind, Target(kind, rng)));
            }

            return result;
        }

        /// <summary>Награда за все три задания: монеты и бустер (только открытый, по кругу по дням).</summary>
        public static Reward RewardFor(int day, int nextLevel)
        {
            var open = new List<BoosterType>();
            foreach (var type in new[] { BoosterType.Slot, BoosterType.Scissors, BoosterType.Undo })
            {
                if (nextLevel >= Economy.UnlockLevel(type))
                {
                    open.Add(type);
                }
            }

            return open.Count == 0 ? Reward.CoinsOnly(80) : new Reward(60, open[((day % open.Count) + open.Count) % open.Count], 1);
        }

        public static string Text(MissionKind kind, int n)
        {
            switch (kind)
            {
                case MissionKind.Win: return $"Пройди {n} {Plural(n, "уровень", "уровня", "уровней")}";
                case MissionKind.ThreeStars: return n == 1 ? "Пройди уровень на три звезды" : $"Пройди {n} {Plural(n, "уровень", "уровня", "уровней")} на три звезды";
                case MissionKind.Gold: return $"Поймай {n} {Plural(n, "золотую петлю", "золотые петли", "золотых петель")}";
                case MissionKind.Rush: return $"Включи «Разгон» {n} {Plural(n, "раз", "раза", "раз")}";
                case MissionKind.Combo: return $"Разгони комбо до ×{n}";
                case MissionKind.Stitches: return $"Свяжи {n} {Plural(n, "петлю", "петли", "петель")}";
                default: return n == 1 ? "Пройди уровень без бустеров" : $"Пройди {n} {Plural(n, "уровень", "уровня", "уровней")} без бустеров";
            }
        }

        private static int Target(MissionKind kind, System.Random rng)
        {
            switch (kind)
            {
                case MissionKind.Win: return 2 + rng.Next(2);
                case MissionKind.ThreeStars: return 1 + rng.Next(2);
                case MissionKind.Gold: return 3 + rng.Next(3);
                case MissionKind.Rush: return 2 + rng.Next(2);
                case MissionKind.Combo: return 6 + rng.Next(3);
                case MissionKind.Stitches: return 150 + 50 * rng.Next(3);
                default: return 1 + rng.Next(2);
            }
        }

        private static string Plural(int n, string one, string few, string many)
        {
            var mod10 = n % 10;
            var mod100 = n % 100;
            if (mod10 == 1 && mod100 != 11)
            {
                return one;
            }

            return mod10 >= 2 && mod10 <= 4 && (mod100 < 12 || mod100 > 14) ? few : many;
        }
    }
}
