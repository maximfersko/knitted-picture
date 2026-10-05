#nullable enable

namespace KnittedPicture.Core
{
    /// <summary>Коллекция альбома: несколько уровней подряд под общим названием.</summary>
    public sealed class CollectionInfo
    {
        public CollectionInfo(string name, int firstLevel, int lastLevel)
        {
            Name = name;
            FirstLevel = firstLevel;
            LastLevel = lastLevel;
        }

        public string Name { get; }

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
