#nullable enable

namespace KnittedPicture.Core
{
    /// <summary>Коллекция альбома: несколько уровней подряд под общим названием.</summary>
    public sealed class CollectionInfo
    {
        public CollectionInfo(string name, int firstLevel, int lastLevel)
        {
            _name = name;
            FirstLevel = firstLevel;
            LastLevel = lastLevel;
        }

        private readonly string _name;

        /// <summary>Название на языке игры (исходник — русский).</summary>
        public string Name => L.T(_name);

        public int FirstLevel { get; }

        public int LastLevel { get; }

        public int Count => LastLevel - FirstLevel + 1;

        public bool Contains(int levelId) => levelId >= FirstLevel && levelId <= LastLevel;
    }

    public static class Collections
    {
        public static readonly CollectionInfo[] All =
        {
            new CollectionInfo("Уютные мелочи", 1, 5),
            new CollectionInfo("Сад", 6, 10),
            new CollectionInfo("Зверята", 11, 15),
            new CollectionInfo("Вкусняшки", 16, 20),
            new CollectionInfo("Море", 21, 25),
            new CollectionInfo("Небо", 26, 30),
            new CollectionInfo("Лес", 31, 35),
            new CollectionInfo("Праздник", 36, 40),
            new CollectionInfo("Уют", 41, 45),
            new CollectionInfo("Друзья", 46, 50),
            new CollectionInfo("Мастерская", 51, 55),
            new CollectionInfo("Путешествия", 56, 60),
            new CollectionInfo("Космос", 61, 65),
        };

        public static CollectionInfo Of(int levelId)
        {
            foreach (var collection in All)
            {
                if (collection.Contains(levelId))
                {
                    return collection;
                }
            }

            return All[All.Length - 1];
        }
    }
}
