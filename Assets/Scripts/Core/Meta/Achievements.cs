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

    /// <summary>Достижения: цели на много сессий вперёд, у каждой — награда.</summary>
    public static class Achievements
    {
        public static readonly AchievementInfo[] All =
        {
            new AchievementInfo(0, "Первая петля", "Довяжи первую картинку", 1, p => p.Data.completed.Count, Reward.CoinsOnly(20)),
            new AchievementInfo(1, "Пять картинок", "Довяжи 5 картинок", 5, p => p.Data.completed.Count, Reward.CoinsOnly(50)),
            new AchievementInfo(2, "Разгон!", "Разгони станок до комбо ×5", 1, p => p.Data.rushCount, Reward.CoinsOnly(30)),
            new AchievementInfo(3, "Скоростная вязка", "Включи «Разгон» 15 раз", 15, p => p.Data.rushCount, new Reward(40, BoosterType.Scissors, 1)),
            new AchievementInfo(4, "Золотые руки", "Поймай 10 золотых петель", 10, p => p.Data.goldCaught, Reward.CoinsOnly(60)),
            new AchievementInfo(5, "Перфекционист", "5 уровней на три звезды", 5, p => p.ThreeStarLevels, new Reward(50, BoosterType.Slot, 1)),
            new AchievementInfo(6, "Тысяча петель", "Свяжи 1000 петель", 1000, p => p.Data.totalStitches, Reward.CoinsOnly(80)),
            new AchievementInfo(7, "Комбо ×10", "Держи серию до ×10", 10, p => p.Data.bestCombo, new Reward(50, BoosterType.Undo, 1)),
            new AchievementInfo(8, "Собиратель", "Собери 6 разных мотков", 6, p => p.YarnKinds, Reward.CoinsOnly(100)),
            new AchievementInfo(9, "Целая коллекция", "Собери коллекцию альбома", 1, p => p.CompletedCollections, Reward.CoinsOnly(100)),
            new AchievementInfo(10, "Каждый день", "Подарок 7 дней подряд", 7, p => p.Data.dailyBest, new Reward(150, BoosterType.Slot, 1)),
            new AchievementInfo(11, "Мастерица", "Все уровни на три звезды", 15, p => p.ThreeStarLevels, Reward.CoinsOnly(300)),
        };
    }

    /// <summary>Ежедневный подарок: 7 дней по кругу, пропуск дня возвращает к первому.</summary>
    public static class DailyGift
    {
        public static readonly Reward[] Days =
        {
            Reward.CoinsOnly(30),
            new Reward(0, BoosterType.Undo, 1),
            Reward.CoinsOnly(50),
            new Reward(0, BoosterType.Scissors, 1),
            Reward.CoinsOnly(80),
            new Reward(0, BoosterType.Slot, 1),
            new Reward(150, null, 0, yarn: -2),
        };

        /// <summary>yarn = -2 у награды — «случайный моток»: разыгрывается при получении.</summary>
        public const int RandomYarn = -2;
    }
}
