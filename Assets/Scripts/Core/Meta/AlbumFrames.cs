#nullable enable

namespace KnittedPicture.Core
{
    /// <summary>
    /// Рамка для картинок альбома: свой узор и форма (рисует FrameRaster по номеру), редкость и цена.
    /// Чистое украшение, на игру не влияет.
    /// </summary>
    /// <summary>Откуда берётся рамка: магазин за монеты, финал сезона или все звёзды коллекции альбома.</summary>
    public enum FrameSource
    {
        Shop,
        Season,
        Collection,
    }

    public sealed class FrameInfo
    {
        public FrameInfo(int id, string name, Rarity rarity, int price, FrameSource source = FrameSource.Shop, int tag = -1)
        {
            Id = id;
            _name = name;
            Rarity = rarity;
            Price = price;
            Source = source;
            Tag = tag;
        }

        public FrameSource Source { get; }

        /// <summary>Сезон — SeasonTheme, коллекция — её номер в Collections.All; у магазинных -1.</summary>
        public int Tag { get; }

        private readonly string _name;

        public int Id { get; }

        /// <summary>Название на языке игры (исходник — русский).</summary>
        public string Name => L.T(_name);

        public Rarity Rarity { get; }

        public int Price { get; }
    }

    /// <summary>
    /// Рамки альбома за монеты — главный «сток» монет без давления: монеты уходят из игры навсегда, а сложность
    /// не меняется (не pay-to-win). Цены лесенкой от 400 до 7 000. Деревянная — бесплатно. Узоры и форма —
    /// FrameRaster. Редкость видна по виду: обычные и необычные — простой узор, редкие — тематический узор
    /// с украшениями, эпические — живой эффект, легендарные — подробная рамка со свечением, бегущим светом и искрами.
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
            new FrameInfo(6, "Цветочки", Rarity.Rare, 2000),
            new FrameInfo(7, "Сердечки", Rarity.Rare, 2200),
            new FrameInfo(8, "Звёздная ночь", Rarity.Rare, 2500),
            new FrameInfo(9, "Золотой багет", Rarity.Epic, 2800),

            // Сезонные: только за финал «Вязального сезона» своего времени года. Эпические: живой эффект
            // (снег, лепестки, пыльца, листья).
            new FrameInfo(10, "Снежинки", Rarity.Epic, 0, FrameSource.Season, (int)SeasonTheme.Winter),
            new FrameInfo(11, "Первоцветы", Rarity.Epic, 0, FrameSource.Season, (int)SeasonTheme.Spring),
            new FrameInfo(12, "Подсолнухи", Rarity.Epic, 0, FrameSource.Season, (int)SeasonTheme.Summer),
            new FrameInfo(13, "Листопад", Rarity.Epic, 0, FrameSource.Season, (int)SeasonTheme.Autumn),

            // За все звёзды коллекции: своя тематическая рамка у каждой коллекции альбома (рисунок — FrameRaster).
            new FrameInfo(14, "Уютные мелочи", Rarity.Rare, 0, FrameSource.Collection, 0),
            new FrameInfo(15, "Сад", Rarity.Rare, 0, FrameSource.Collection, 1),
            new FrameInfo(16, "Зверята", Rarity.Rare, 0, FrameSource.Collection, 2),
            new FrameInfo(17, "Вкусняшки", Rarity.Rare, 0, FrameSource.Collection, 3),
            new FrameInfo(18, "Море", Rarity.Rare, 0, FrameSource.Collection, 4),
            new FrameInfo(19, "Небо", Rarity.Rare, 0, FrameSource.Collection, 5),
            new FrameInfo(20, "Лес", Rarity.Rare, 0, FrameSource.Collection, 6),
            new FrameInfo(21, "Праздник", Rarity.Rare, 0, FrameSource.Collection, 7),
            new FrameInfo(22, "Уют", Rarity.Rare, 0, FrameSource.Collection, 8),
            new FrameInfo(23, "Друзья", Rarity.Rare, 0, FrameSource.Collection, 9),
            new FrameInfo(24, "Мастерская", Rarity.Rare, 0, FrameSource.Collection, 10),
            new FrameInfo(25, "Путешествия", Rarity.Rare, 0, FrameSource.Collection, 11),
            new FrameInfo(26, "Космос", Rarity.Rare, 0, FrameSource.Collection, 12),

            // Живые рамки: у каждой своя задумка и анимация (FrameFx). Эпические — один сильный эффект,
            // легендарные — подробный рисунок, свечение вокруг, бегущий свет и искры.
            new FrameInfo(27, "Светлячки", Rarity.Epic, 3200),
            new FrameInfo(28, "Северное сияние", Rarity.Epic, 3600),
            new FrameInfo(29, "Камин", Rarity.Epic, 4000),
            new FrameInfo(30, "Жар-птица", Rarity.Legendary, 5500),
            new FrameInfo(31, "Волшебный клубок", Rarity.Legendary, 7000),
        };

        /// <summary>Рамка коллекции номер collection (за все звёзды её картинок).</summary>
        public static FrameInfo ForCollection(int collection)
        {
            foreach (var frame in All)
            {
                if (frame.Source == FrameSource.Collection && frame.Tag == collection)
                {
                    return frame;
                }
            }

            return All[0];
        }

        /// <summary>Сезонная рамка времени года theme.</summary>
        public static FrameInfo ForSeason(SeasonTheme theme)
        {
            foreach (var frame in All)
            {
                if (frame.Source == FrameSource.Season && frame.Tag == (int)theme)
                {
                    return frame;
                }
            }

            return All[0];
        }

        public static FrameInfo Get(int id) => id >= 0 && id < All.Length ? All[id] : All[0];
    }
}
