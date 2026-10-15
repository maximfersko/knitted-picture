#nullable enable

namespace KnittedPicture.Core
{
    /// <summary>
    /// Рамка для картинок альбома: свой узор и форма (рисует FrameRaster по номеру), редкость и цена.
    /// Чистое украшение, на игру не влияет.
    /// </summary>
    public sealed class FrameInfo
    {
        public FrameInfo(int id, string name, Rarity rarity, int price)
        {
            Id = id;
            _name = name;
            Rarity = rarity;
            Price = price;
        }

        private readonly string _name;

        public int Id { get; }

        /// <summary>Название на языке игры (исходник — русский).</summary>
        public string Name => L.T(_name);

        public Rarity Rarity { get; }

        public int Price { get; }
    }

    /// <summary>
    /// Рамки альбома за монеты — главный «сток» монет без давления: монеты уходят из игры навсегда, а сложность
    /// не меняется (не pay-to-win). Цены лесенкой от 400 до 5 500, всего около 22 тысяч: экономный игрок,
    /// у которого монеты копятся, собирает все рамки примерно за два месяца игры. Деревянная — бесплатно. Узоры и форма —
    /// FrameRaster: дерево, лён, горошек, клетка, вязаная коса, пуговицы, цветы, сердечки, звёзды, золото с розетками.
    /// </summary>
    public static class AlbumFrames
    {
        public static readonly FrameInfo[] All =
        {
            new FrameInfo(0, "Дерево", Rarity.Common, 0),
            new FrameInfo(1, "Лён", Rarity.Common, 400),
            new FrameInfo(2, "Горошек", Rarity.Uncommon, 700),
            new FrameInfo(3, "Клетка", Rarity.Uncommon, 1000),
            new FrameInfo(4, "Косичка", Rarity.Rare, 1400),
            new FrameInfo(5, "Пуговки", Rarity.Rare, 1800),
            new FrameInfo(6, "Цветочки", Rarity.Epic, 2400),
            new FrameInfo(7, "Сердечки", Rarity.Epic, 3000),
            new FrameInfo(8, "Звёздная ночь", Rarity.Epic, 4000),
            new FrameInfo(9, "Золотой багет", Rarity.Legendary, 5500),
        };

        public static FrameInfo Get(int id) => id >= 0 && id < All.Length ? All[id] : All[0];
    }
}
