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

        public bool soundOn = true;
        public int totalWins;
        public int winsSinceInterstitial;
    }
}
