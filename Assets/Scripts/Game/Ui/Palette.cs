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
        /// <summary>Строчка-пунктир по краю белых карточек и окон.</summary>
        public static readonly Color Stitch = Hex("#D6CCF5");
        /// <summary>Строчка на белых кнопках: заметнее, кнопка должна читаться как кнопка.</summary>
        public static readonly Color StitchStrong = Hex("#B9ABEA");

        /// <summary>Плитка внутри белой карточки или окна.</summary>
        public static readonly Color Surface = Hex("#F5F2FF");
        public static readonly Color Ink = Hex("#2B2350");
        public static readonly Color Ink2 = Hex("#544C7A");
        // Вторичный текст: контраст с белым 4.7:1 (WCAG AA для мелкого текста — от 4.5:1).
        public static readonly Color Ink3 = Hex("#756CA6");
        public static readonly Color LoomBg = Hex("#E4DCFF");
        public static readonly Color SlotEmpty = Hex("#CFC5F3");
        public static readonly Color Track = Hex("#E7E1F8");
        public static readonly Color Primary = Hex("#17945F");
        public static readonly Color PrimaryShadow = Hex("#0F6E46");
        public static readonly Color PrimarySoft = Hex("#DCF3E7");
        public static readonly Color Coin = Hex("#FFC23C");
        public static readonly Color CoinShadow = Hex("#D98E0A");
        public static readonly Color CoinSoft = Hex("#FFF0C7");
        /// <summary>Золотой текст: читается и на белом, и на CoinSoft (CoinShadow для текста слишком светлый).</summary>
        public static readonly Color CoinInk = Hex("#965C00");
        /// <summary>Неактивные отметки (пустые звёзды, точки листания, пустые деления): заметны на белом и на фоне.</summary>
        public static readonly Color Inactive = Hex("#9488C8");
        /// <summary>Заливка круглых кнопок на белой карточке: белая кнопка с тенью на белом окне пропадает.</summary>
        public static readonly Color ButtonFill = Hex("#EAE4FF");
        public static readonly Color Danger = Hex("#E0306E");
        public static readonly Color DangerSoft = Hex("#FFDDE9");
        public static readonly Color Link = Hex("#8B5CF6");

        /// <summary>Цвета пар связанных клубков: у каждой пары свой, чтобы было видно, кто с кем связан.</summary>
        private static readonly Color[] LinkPairs = { Hex("#8B5CF6"), Hex("#0EA5A4"), Hex("#F97316"), Hex("#2563EB") };

        /// <summary>Цвет пары связанных клубков по номеру связки (1, 2, 3…).</summary>
        public static Color LinkPair(int link) => LinkPairs[(System.Math.Max(1, link) - 1) % LinkPairs.Length];
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
