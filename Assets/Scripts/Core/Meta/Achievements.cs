#nullable enable

using System;

namespace KnittedPicture.Core
{
    public sealed class AchievementInfo
    {
        public AchievementInfo(int id, string title, string description, int target, Func<ProgressService, int> value, Reward reward)
        {
            Id = id;
            Title = title;
            Description = description;
            Target = target;
            Value = value;
            Reward = reward;
        }

        public int Id { get; }

        public string Title { get; }

        public string Description { get; }

        public int Target { get; }

        /// <summary>Текущее значение счётчика у игрока.</summary>
        public Func<ProgressService, int> Value { get; }

        public Reward Reward { get; }
    }

    /// <summary>
    /// Достижения: крупные вехи на много сессий вперёд, у каждой — награда. Мелкие счётчики
    /// (разгон, золото, комбо, петли) перешли в задания дня. Номера не меняются: на них держатся сохранения.
    /// </summary>
    public static class Achievements
    {
        public static readonly AchievementInfo[] All =
        {
            new AchievementInfo(0, "Первая петля", "Довяжи первую картинку", 1, p => p.Data.completed.Count, Reward.CoinsOnly(10)),
            new AchievementInfo(1, "Пять картинок", "Довяжи 5 картинок", 5, p => p.Data.completed.Count, Reward.CoinsOnly(25)),
            new AchievementInfo(5, "Безупречно", "Пройди 5 уровней на 3 звезды", 5, p => p.ThreeStarLevels, new Reward(25, BoosterType.Slot, 1)),
            new AchievementInfo(8, "Шесть мотков", "Собери 6 разных мотков", 6, p => p.YarnKinds, Reward.CoinsOnly(50)),
            new AchievementInfo(9, "Целая коллекция", "Собери любую коллекцию альбома", 1, p => p.CompletedCollections, Reward.CoinsOnly(50)),
            new AchievementInfo(10, "Каждый день", "Забирай подарок 7 дней подряд", 7, p => p.Data.dailyBest, new Reward(75, BoosterType.Slot, 1)),
            new AchievementInfo(11, "Мастерство", "Пройди 25 уровней на 3 звезды", 25, p => p.ThreeStarLevels, new Reward(150, BoosterType.Slot, 2)),
            new AchievementInfo(12, "Полальбома", "Довяжи 25 картинок", 25, p => p.Data.completed.Count, new Reward(75, BoosterType.Scissors, 1)),
            new AchievementInfo(13, "Весь альбом", $"Довяжи все {Collections.All[Collections.All.Length - 1].LastLevel} картинок", Collections.All[Collections.All.Length - 1].LastLevel, p => p.Data.completed.Count, new Reward(250, BoosterType.Slot, 2)),
            new AchievementInfo(14, "Пять коллекций", "Собери 5 коллекций альбома", 5, p => p.CompletedCollections, new Reward(100, BoosterType.Undo, 2)),
            new AchievementInfo(15, "Корзинка полна", "Собери все мотки пряжи", 16, p => p.YarnKinds, Reward.CoinsOnly(200)),
        };
    }

    /// <summary>Ежедневный подарок: 7 дней по кругу; пропуск дня ставит неделю на паузу.</summary>
    public static class DailyGift
    {
        public static readonly Reward[] Days =
        {
            Reward.CoinsOnly(25),
            new Reward(0, BoosterType.Undo, 1),
            Reward.CoinsOnly(40),
            new Reward(0, BoosterType.Scissors, 1),
            Reward.CoinsOnly(60),
            new Reward(0, BoosterType.Slot, 1),
            new Reward(120, null, 0, yarn: -2),
        };

        /// <summary>yarn = -2 у награды — «случайный моток»: разыгрывается при получении.</summary>
        public const int RandomYarn = -2;
    }
}
