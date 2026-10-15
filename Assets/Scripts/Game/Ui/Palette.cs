#nullable enable

using UnityEngine;

namespace KnittedPicture.Game
{
    /// <summary>
    /// Цвета интерфейса: нейтральный светло-серый фон с холодным оттенком, белые карточки с контуром и тенью,
    /// вложенные плитки — на светло-серой подложке, текст графитовый. Акцент — спокойный синий, опасное — красный,
    /// главное действие — зелёное. Яркое — только пряжа и главные кнопки: интерфейс не уводит в пастель.
    /// Три уровня поверхностей: Ground (экран) → Card (карточка) → Surface (плитка внутри карточки).
    /// </summary>
    public static class Palette
    {
        public static readonly Color Ground = Hex("#E9ECF0");

        /// <summary>
        /// Фон по коллекциям: узор тот же, меняется только оттенок — у каждой коллекции своё настроение.
        /// Все оттенки светлые (как Ground), текст и карточки на них читаются одинаково.
        /// </summary>
        private static readonly Color[] Grounds =
        {
            Hex("#E9ECF0"), // Уютные мелочи — светлый графит
            Hex("#E2EBE4"), // Сад — шалфей
            Hex("#EDE7DF"), // Зверята — песок
            Hex("#EEE5E0"), // Вкусняшки — глина
            Hex("#DFEAEF"), // Море — морская волна
            Hex("#E2E8F1"), // Небо — холодный голубой
            Hex("#E3E9DD"), // Лес — мох
            Hex("#EEEADC"), // Праздник — лён
            Hex("#E9E4DE"), // Уют — какао с молоком
            Hex("#E4E7EC"), // Друзья — сталь
            Hex("#ECE6DC"), // Мастерская — лён
            Hex("#E0EBF2"), // Путешествия — небо над морем
            Hex("#E2E3EE"), // Космос — холодный сумрак
        };

        /// <summary>Фон уровня: по его коллекции.</summary>
        public static Color GroundOfLevel(int levelId) =>
            GroundOf(System.Array.IndexOf(KnittedPicture.Core.Collections.All, KnittedPicture.Core.Collections.Of(levelId)));

        public static Color GroundOf(int collection) => Grounds[(collection % Grounds.Length + Grounds.Length) % Grounds.Length];
        public static readonly Color Card = Color.white;
        public static readonly Color CardShadow = Hex("#CDD3DB");
        /// <summary>Мягкая размытая тень белых карточек: холодная графитовая и прозрачная.</summary>
        public static readonly Color SoftShadow = new Color(0.16f, 0.22f, 0.3f, 0.18f);

        /// <summary>Тонкий контур белых карточек: не теряются ни на фоне, ни на белом окне.</summary>
        public static readonly Color Line = Hex("#D8DDE4");

        /// <summary>Плитка внутри белой карточки или окна.</summary>
        public static readonly Color Surface = Hex("#F3F5F8");
        public static readonly Color Ink = Hex("#1E2732");
        public static readonly Color Ink2 = Hex("#4A5563");
        // Вторичный текст: контраст с белым 4.7:1 (WCAG AA для мелкого текста — от 4.5:1).
        public static readonly Color Ink3 = Hex("#677382");
        public static readonly Color LoomBg = Hex("#E0E5EB");
        public static readonly Color SlotEmpty = Hex("#C5CDD7");
        public static readonly Color Track = Hex("#E2E6EB");
        public static readonly Color Primary = Hex("#17945F");
        public static readonly Color PrimaryShadow = Hex("#0F6E46");
        public static readonly Color PrimarySoft = Hex("#DCF3E7");
        public static readonly Color Coin = Hex("#FFC23C");
        public static readonly Color CoinShadow = Hex("#D98E0A");
        public static readonly Color CoinSoft = Hex("#FFF0C7");
        /// <summary>Золотой текст: читается и на белом, и на CoinSoft (CoinShadow для текста слишком светлый).</summary>
        public static readonly Color CoinInk = Hex("#965C00");
        /// <summary>Неактивные отметки (пустые звёзды, точки листания, пустые деления): заметны на белом и на фоне.</summary>
        public static readonly Color Inactive = Hex("#8E99A6");
        /// <summary>Заливка круглых кнопок на белой карточке: белая кнопка с тенью на белом окне пропадает.</summary>
        public static readonly Color ButtonFill = Hex("#E5E9EF");
        public static readonly Color Danger = Hex("#D7383F");
        public static readonly Color DangerSoft = Hex("#FBE0DF");
        public static readonly Color Link = Hex("#2F6FDE");

        /// <summary>Цвета пар связанных клубков: у каждой пары свой, чтобы было видно, кто с кем связан.</summary>
        private static readonly Color[] LinkPairs = { Hex("#2F6FDE"), Hex("#0EA5A4"), Hex("#F97316"), Hex("#64748B") };

        /// <summary>Цвет пары связанных клубков по номеру связки (1, 2, 3…).</summary>
        public static Color LinkPair(int link) => LinkPairs[(System.Math.Max(1, link) - 1) % LinkPairs.Length];
        public static readonly Color Hidden = Hex("#C8CFD8");
        public static readonly Color Wood = Hex("#F2C48D");
        public static readonly Color WoodDark = Hex("#D9955A");
        public static readonly Color Dim = new Color(0.08f, 0.11f, 0.15f, 0.62f);

        public static Color Hex(string hex)
        {
            return ColorUtility.TryParseHtmlString(hex, out var color) ? color : Color.magenta;
        }

        /// <summary>
        /// Пряжа ярче: насыщенность ×1,15 и чуть светлее — цвета картинки и клубков сочные,
        /// но оттенок тот же (близкие цвета уровня не путаются).
        /// </summary>
        public static Color Vivid(Color color)
        {
            Color.RGBToHSV(color, out var h, out var s, out var v);
            return Color.HSVToRGB(h, Mathf.Clamp01(s * 1.15f), Mathf.Clamp01(v * 1.04f + 0.01f));
        }

        /// <summary>Ткань ярче: насыщенность ×1,3 и едва заметно светлее — живой цвет, а не выцветший.</summary>
        public static Color Fabric(Color color)
        {
            Color.RGBToHSV(color, out var h, out var s, out var v);
            var vivid = Color.HSVToRGB(h, Mathf.Clamp01(s * 1.3f), v);
            return Color.Lerp(vivid, Color.white, 0.03f);
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
