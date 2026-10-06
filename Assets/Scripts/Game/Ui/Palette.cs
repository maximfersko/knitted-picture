#nullable enable

using UnityEngine;

namespace KnittedPicture.Game
{
    /// <summary>
    /// Цвета интерфейса: лавандовый фон, белые карточки с контуром и плотной тенью,
    /// вложенные плитки — на светло-лавандовой подложке. Яркое — только пряжа и главные кнопки.
    /// Три уровня поверхностей: Ground (экран) → Card (карточка) → Surface (плитка внутри карточки).
    /// </summary>
    public static class Palette
    {
        public static readonly Color Ground = Hex("#EDE7FF");
        public static readonly Color Card = Color.white;
        public static readonly Color CardShadow = Hex("#D3C8F3");

        /// <summary>Тонкий контур белых карточек: не теряются ни на фоне, ни на белом окне.</summary>
        public static readonly Color Line = Hex("#DDD5F7");

        /// <summary>Плитка внутри белой карточки или окна.</summary>
        public static readonly Color Surface = Hex("#F5F2FF");
        public static readonly Color Ink = Hex("#2B2350");
        public static readonly Color Ink2 = Hex("#544C7A");
        public static readonly Color Ink3 = Hex("#8A81BA");
        public static readonly Color LoomBg = Hex("#E4DCFF");
        public static readonly Color SlotEmpty = Hex("#CFC5F3");
        public static readonly Color Track = Hex("#E7E1F8");
        public static readonly Color Primary = Hex("#17945F");
        public static readonly Color PrimaryShadow = Hex("#0F6E46");
        public static readonly Color PrimarySoft = Hex("#DCF3E7");
        public static readonly Color Coin = Hex("#FFC23C");
        public static readonly Color CoinShadow = Hex("#D98E0A");
        public static readonly Color CoinSoft = Hex("#FFF0C7");
        public static readonly Color Danger = Hex("#E0306E");
        public static readonly Color DangerSoft = Hex("#FFDDE9");
        public static readonly Color Link = Hex("#8B5CF6");
        public static readonly Color Hidden = Hex("#CFC8EA");
        public static readonly Color Wood = Hex("#F2C48D");
        public static readonly Color WoodDark = Hex("#D9955A");
        public static readonly Color Dim = new Color(0.17f, 0.14f, 0.31f, 0.62f);

        public static Color Hex(string hex)
        {
            return ColorUtility.TryParseHtmlString(hex, out var color) ? color : Color.magenta;
        }

        public static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        /// <summary>Светлый ли цвет: на светлых клубках линии и цифры рисуем тёмным.</summary>
        public static bool IsLight(Color color) => 0.299f * color.r + 0.587f * color.g + 0.114f * color.b > 0.8f;
    }
}
