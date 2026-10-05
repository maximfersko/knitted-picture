#nullable enable

using UnityEngine;

namespace KnittedPicture.Game
{
    /// <summary>Цвета интерфейса из макетов: светлый фон, белые карточки, яркое — только пряжа.</summary>
    public static class Palette
    {
        public static readonly Color Ground = Hex("#F4F1FF");
        public static readonly Color Card = Color.white;
        public static readonly Color CardShadow = Hex("#E4DEFA");
        public static readonly Color Ink = Hex("#2B2350");
        public static readonly Color Ink2 = Hex("#5E5880");
        public static readonly Color Ink3 = Hex("#A79DD6");
        public static readonly Color LoomBg = Hex("#E9E3FF");
        public static readonly Color SlotEmpty = Hex("#D9D1F7");
        public static readonly Color Track = Hex("#EEEAFB");
        public static readonly Color Primary = Hex("#17945F");
        public static readonly Color PrimaryShadow = Hex("#0F6E46");
        public static readonly Color PrimarySoft = Hex("#E6F6EE");
        public static readonly Color Coin = Hex("#FFC23C");
        public static readonly Color CoinShadow = Hex("#E59E12");
        public static readonly Color CoinSoft = Hex("#FFF4D6");
        public static readonly Color Danger = Hex("#E0306E");
        public static readonly Color DangerSoft = Hex("#FFE3EE");
        public static readonly Color Link = Hex("#8B5CF6");
        public static readonly Color Hidden = Hex("#CFC8EA");
        public static readonly Color Wood = Hex("#F2C48D");
        public static readonly Color WoodDark = Hex("#D9955A");
        public static readonly Color Dim = new Color(0.17f, 0.14f, 0.31f, 0.5f);

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
