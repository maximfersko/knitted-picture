#nullable enable

namespace KnittedPicture.Core
{
    /// <summary>Рамка для картинок альбома: цвет багета, кромки и паспарту. Чистое украшение, на игру не влияет.</summary>
    public sealed class FrameInfo
    {
        public FrameInfo(int id, string name, string face, string edge, string mat, int price)
        {
            Id = id;
            _name = name;
            Face = face;
            Edge = edge;
            Mat = mat;
            Price = price;
        }

        private readonly string _name;

        public int Id { get; }

        /// <summary>Название на языке игры (исходник — русский).</summary>
        public string Name => L.T(_name);

        public string Face { get; }

        public string Edge { get; }

        public string Mat { get; }

        public int Price { get; }
    }

    /// <summary>
    /// Рамки альбома за монеты — главный «сток» монет без давления: монеты уходят из игры навсегда, а сложность
    /// не меняется (не pay-to-win). Цены лесенкой от 400 до 5 500, всего около 22 тысяч: экономный игрок,
    /// у которого монеты копятся, собирает все рамки примерно за два месяца игры. Деревянная — бесплатно.
    /// </summary>
    public static class AlbumFrames
    {
        public static readonly FrameInfo[] All =
        {
            new FrameInfo(0, "Дерево", "#F2C48D", "#D9955A", "#FFFFFF", 0),
            new FrameInfo(1, "Белая", "#F7F7F5", "#C9CDD3", "#FFFFFF", 400),
            new FrameInfo(2, "Графит", "#3A3F47", "#1E2228", "#FFFFFF", 700),
            new FrameInfo(3, "Голубая", "#7CC8EE", "#3E8FBF", "#FFFFFF", 1000),
            new FrameInfo(4, "Зелёная", "#5DBB72", "#2E8746", "#FFFFFF", 1400),
            new FrameInfo(5, "Розовая", "#F48BBE", "#C9578E", "#FFFFFF", 1800),
            new FrameInfo(6, "Вишня", "#9E2B3A", "#6B1724", "#FFF8EE", 2400),
            new FrameInfo(7, "Орех", "#6B4428", "#47291A", "#FFF8EE", 3000),
            new FrameInfo(8, "Серебро", "#C7CCD3", "#8C939C", "#FFFFFF", 4000),
            new FrameInfo(9, "Золото", "#E8B64A", "#A87A16", "#FFFDF5", 5500),
        };

        public static FrameInfo Get(int id) => id >= 0 && id < All.Length ? All[id] : All[0];
    }
}
