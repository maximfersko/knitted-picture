#nullable enable

using System;
using System.Collections.Generic;

namespace KnittedPicture.Core
{
    /// <summary>Сохранение игрока. Публичные поля — для JsonUtility.</summary>
    [Serializable]
    public sealed class PlayerProgress
    {
        public int version = 1;

        /// <summary>Самый дальний открытый уровень.</summary>
        public int nextLevel = 1;

        public int coins = Economy.StartCoins;
        public int slotBoosters;
        public int scissorsBoosters;
        public int undoBoosters;
        public int timeBoosters;

        public List<int> completed = new List<int>();

        /// <summary>Бустеры, для которых уже показали окно «Новый бустер».</summary>
        public List<int> introsShown = new List<int>();

        /// <summary>Лучшие звёзды по уровням: индекс — id уровня минус 1.</summary>
        public List<int> stars = new List<int>();

        /// <summary>Побед подряд без перезапуска уровня.</summary>
        public int streak;

        /// <summary>Вступление «как играть» уже показано.</summary>
        public bool introSeen;

        /// <summary>Мотки «Корзинки»: индекс — номер мотка в каталоге, значение — сколько штук.</summary>
        public List<int> yarns = new List<int>();

        /// <summary>День (с 1970 года), когда последний раз забран ежедневный подарок.</summary>
        public int dailyLastDay = -10;

        /// <summary>Сколько дней подряд забран подарок и лучшая такая серия.</summary>
        public int dailyStreak;
        public int dailyBest;

        public int chestsOpened;
        public List<int> achievementsClaimed = new List<int>();
        public List<int> yarnSetsClaimed = new List<int>();
        public List<int> collectionsClaimed = new List<int>();

        /// <summary>Счётчики для достижений.</summary>
        public int totalStitches;
        public int rushCount;
        public int goldCaught;
        public int bestCombo;

        public bool soundOn = true;
        public int totalWins;
        public int winsSinceInterstitial;
    }
}
