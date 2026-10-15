#nullable enable

namespace KnittedPicture.Core
{
    /// <summary>
    /// Недельные вехи сезона (бывший «Вязальный марафон»): петли, связанные за неделю на любых уровнях и в узоре
    /// дня, копятся к трём вехам; каждая веха сама добавляет сезону целую ступень очков. Отдельной плитки и окна
    /// нет: одна шкала прогресса вместо двух (марафон и сезон делали одно и то же). Неделя с понедельника.
    /// </summary>
    public static class WeeklyMarathon
    {
        /// <summary>С какого уровня копятся вехи: к этому времени игрок знает игру и копит петли быстро.</summary>
        public const int FromLevel = 8;

        /// <summary>Сколько петель за неделю нужно до каждой вехи (по нарастающей).</summary>
        public static readonly int[] Targets = { 300, 900, 1800 };

        /// <summary>Очков сезона за веху: ровно одна ступень.</summary>
        public const int TierPoints = Season.StepPoints;

        /// <summary>Номер недели по дню (с 1970 года): 1 января 1970 — четверг, неделя с понедельника.</summary>
        public static int WeekOf(int day) => (int)System.Math.Floor((day + 3) / 7.0);

        /// <summary>Сколько дней осталось до новой недели, считая сегодняшний (7 — сегодня понедельник).</summary>
        public static int DaysLeft(int day) => 7 - (((day + 3) % 7) + 7) % 7;
    }
}
