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

        /// <summary>Лучшее число тапов по уровням (индекс — id минус 1), 0 — нет данных.</summary>
        public List<int> bestTaps = new List<int>();

        /// <summary>Уровни, пройденные хоть раз без бустеров и спасений.</summary>
        public List<int> cleanWins = new List<int>();

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

        /// <summary>«Узор дня»: последний связанный день, дни подряд, лучшая серия и сколько узоров всего.</summary>
        public int patternDay = -10;
        public int patternStreak;
        public int patternBest;
        public int patternsDone;

        /// <summary>Задания дня: на какой день выбраны, какие (вид и цель), сколько сделано, забрана ли награда.</summary>
        public int missionDay = -10;
        public List<int> missionKinds = new List<int>();
        public List<int> missionTargets = new List<int>();
        public List<int> missionValues = new List<int>();
        public bool missionsClaimed;

        /// <summary>Сколько подарков дня забрано всего: по нему идёт неделя подарков (пропуск — пауза, не сброс).</summary>
        public int dailyClaims;

        public int chestsOpened;

        /// <summary>Новых картинок подряд без мотка: с каждой шанс растёт, на четвёртой моток гарантирован.</summary>
        public int yarnDry;

        /// <summary>Случайных мотков подряд без эпического: на двенадцатом эпический гарантирован.</summary>
        public int yarnsSinceEpic;

        /// <summary>Моток «в работе»: его бонус действует в уровнях. -1 — ещё нет.</summary>
        public int activeYarn = -1;
        public List<int> achievementsClaimed = new List<int>();
        public List<int> yarnSetsClaimed = new List<int>();
        public List<int> collectionsClaimed = new List<int>();

        /// <summary>Счётчики для достижений.</summary>
        public int totalStitches;
        public int rushCount;
        public int goldCaught;
        public int bestCombo;

        public bool soundOn = true;

        /// <summary>Вибрация (тактильный отклик) включена.</summary>
        public bool hapticsOn = true;

        /// <summary>Удобство: значки-фигуры у цветов (различать клубки не только по цвету).</summary>
        public bool colorMarks;

        /// <summary>Удобство: меньше движения — без покачиваний, всплытий, пружин и тряски.</summary>
        public bool reduceMotion;

        /// <summary>Удобство: мелкий текст крупнее (где помещается).</summary>
        public bool largeText;

        /// <summary>Удобство: рука-подсказка, если долго нет хода (на первых уровнях — всегда, это обучение).</summary>
        public bool idleHints = true;

        /// <summary>Удобство: экономия заряда — 30 кадров в секунду вместо 60.</summary>
        public bool batterySaver;

        /// <summary>Напоминания на телефон включены; спрашивали ли уже разрешение у системы.</summary>
        public bool remindersOn = true;
        public bool remindersAsked;
        public int totalWins;
        public int winsSinceInterstitial;

        /// <summary>Куплено «Без рекламы»: межуровневой рекламы больше нет (видео за награду — по желанию).</summary>
        public bool adsRemoved;

        /// <summary>Согласие на персональную рекламу: 0 — ещё не спрашивали, 1 — да, 2 — нет.</summary>
        public int adsConsent;

        /// <summary>Сколько раз начинали каждый уровень (индекс — номер уровня − 1): для аналитики «с какой попытки проходят».</summary>
        public List<int> attempts = new List<int>();
    }
}
