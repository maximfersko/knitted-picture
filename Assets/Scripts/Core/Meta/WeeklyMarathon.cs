#nullable enable

namespace KnittedPicture.Core
{
    /// <summary>
    /// Вязальный марафон: недельное событие. Петли, связанные за неделю на любых уровнях (и в узоре дня),
    /// копятся к трём ступеням с наградами. Неделя начинается в понедельник; новая неделя — новый счёт.
    /// Даёт цель тем, кто уже прошёл всё, и повод зайти на выходных.
    /// </summary>
    public static class WeeklyMarathon
    {
        /// <summary>С какого уровня открыт: к этому времени игрок знает игру и копит петли быстро.</summary>
        public const int FromLevel = 8;

        /// <summary>Сколько петель за неделю нужно до каждой ступени (по нарастающей).</summary>
        public static readonly int[] Targets = { 300, 900, 1800 };

        public static readonly Reward[] Rewards =
        {
            Reward.CoinsOnly(60),
            new Reward(0, BoosterType.Scissors, 1),
            new Reward(100, null, 0, yarn: DailyGift.RandomYarn),
        };

        /// <summary>Номер недели по дню (с 1970 года): 1 января 1970 — четверг, неделя с понедельника.</summary>
        public static int WeekOf(int day) => (int)System.Math.Floor((day + 3) / 7.0);

        /// <summary>Сколько дней осталось до новой недели, считая сегодняшний (7 — сегодня понедельник).</summary>
        public static int DaysLeft(int day) => 7 - (((day + 3) % 7) + 7) % 7;
    }
}
