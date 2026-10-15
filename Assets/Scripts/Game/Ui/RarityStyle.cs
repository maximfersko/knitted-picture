#nullable enable

using KnittedPicture.Core;
using UnityEngine;

namespace KnittedPicture.Game
{
    /// <summary>
    /// Цвета редкости в интерфейсе (одни на всю игру, см. Rarities): основной — свечение и обводки, мягкий —
    /// подложки, чернила — текст (темнее основного: контраст не ниже 4,5:1 и на белом, и на своей подложке, WCAG AA).
    /// </summary>
    public static class RarityStyle
    {
        public static Color Color(Rarity rarity) => Palette.Hex(Rarities.Hex(rarity));

        public static Color Soft(Rarity rarity) => Palette.Hex(Rarities.SoftHex(rarity));

        public static Color Ink(Rarity rarity) => rarity switch
        {
            Rarity.Uncommon => Palette.Hex("#1E7537"),
            Rarity.Rare => Palette.Hex("#2559C2"),
            Rarity.Epic => Palette.Hex("#7A33C2"),
            Rarity.Legendary => Palette.Hex("#8A5700"),
            _ => Palette.Hex("#5F6670"),
        };

        /// <summary>Цвет искр: светлый оттенок своей ступени.</summary>
        public static Color Spark(Rarity rarity) => rarity switch
        {
            Rarity.Uncommon => Palette.Hex("#D8F5DF"),
            Rarity.Rare => Palette.Hex("#D6ECFF"),
            Rarity.Epic => Palette.Hex("#F0DBFF"),
            _ => Palette.Hex("#FFF1A8"),
        };

        /// <summary>
        /// Плашка редкости: огранённый камень своей ступени и подпись её цветом, в нужном роде
        /// («редкая пряжа», «редкий бустер», «редкое достижение»). maxW — не шире плитки.
        /// </summary>
        public static RectTransform Chip(RectTransform parent, Rarity rarity, float cx, float cy, float maxW = 0f, Gender gender = Gender.Feminine)
        {
            var text = Rarities.Name(rarity, gender);
            var chip = Ui.Box(parent, "Rarity", 10, Soft(rarity)).rectTransform;
            var label = Ui.Label(chip, text, 11, Ink(rarity), true, TextAnchor.MiddleLeft);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            const float gem = 16f;
            var w = Mathf.Max(text.Length * 6.6f, label.preferredWidth) + gem + 16f;
            if (maxW > 0f && w > maxW)
            {
                // «легендарная» не вылезает за узкую плитку: плашка по ширине плитки, текст ужимается.
                w = maxW;
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                Ui.Fit(label, 8);
            }

            Ui.PlaceCenter(chip, cx, cy, w, 20);
            var icon = Ui.Image(chip, "Gem", Art2.Gem(rarity), UnityEngine.Color.white);
            Ui.PlaceCenter(icon.rectTransform, 5f + gem / 2f, 9.5f, gem, gem);
            Ui.Place(label.rectTransform, 6f + gem, 0f, w - gem - 10f, 20f);
            return chip;
        }
    }
}
