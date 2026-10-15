#nullable enable

using KnittedPicture.Core;
using UnityEngine;

namespace KnittedPicture.Game
{
    public enum EventKind
    {
        Gift,
        Missions,
        Chest,
        Pattern,
        Marathon,
    }

    /// <summary>
    /// Мини-иллюстрации событий на главной вместо линейных значков: коробка с бантом, листок заданий,
    /// маленький сундук, клубок со звездой. Цветные и объёмные, как клубки, — в одном мире с игрой.
    /// Собраны из готовых форм (круги, скруглённые плашки), без отдельных картинок.
    /// </summary>
    public static class EventArt
    {
        /// <summary>Иллюстрация в квадрате size с центром (cx, cy); возвращает узел — его можно качать.</summary>
        public static RectTransform Create(RectTransform parent, EventKind kind, float cx, float cy, float size)
        {
            var root = Ui.PlaceCenter(Ui.Node(parent, "Event" + kind), cx, cy, size, size);
            switch (kind)
            {
                case EventKind.Gift:
                    Gift(root, size);
                    break;
                case EventKind.Missions:
                    Missions(root, size);
                    break;
                case EventKind.Chest:
                    ChestView.Create(root, size / 2f, size * 0.54f, size * 0.86f, App.Instance != null ? App.Instance.Progress.NextChestRarity : Rarity.Common);
                    break;
                case EventKind.Marathon:
                    var cup = Ui.Image(root, "Trophy", Art2.Trophy(true), Color.white);
                    Ui.PlaceCenter(cup.rectTransform, size / 2f, size / 2f, size * 0.95f, size * 0.95f);
                    break;
                default:
                    Pattern(root, size);
                    break;
            }

            return root;
        }

        private static void Box(RectTransform root, float x, float y, float w, float h, float radius, Color color)
        {
            var box = Ui.Box(root, "Part", radius, color);
            Ui.Place(box.rectTransform, x, y, w, h);
        }

        private static void Gift(RectTransform root, float s)
        {
            // Подарок 2.0: коробка в голубой вязке с розовым бантом.
            var gift = Ui.Image(root, "Gift", Art2.Gift, Color.white);
            Ui.PlaceCenter(gift.rectTransform, s / 2f, s / 2f, s * 1.05f, s * 1.05f);
        }

        private static void Missions(RectTransform root, float s)
        {
            // Листок на планшете: прищепка сверху, три строки — две с галочкой, одна ждёт.
            Box(root, s * 0.18f, s * 0.12f, s * 0.64f, s * 0.8f, s * 0.1f, Palette.Primary);
            Box(root, s * 0.24f, s * 0.2f, s * 0.52f, s * 0.66f, s * 0.06f, Color.white);
            Box(root, s * 0.36f, s * 0.06f, s * 0.28f, s * 0.14f, s * 0.05f, Palette.PrimaryShadow);
            for (var i = 0; i < 3; i++)
            {
                var y = s * (0.34f + i * 0.17f);
                var done = i < 2;
                var mark = Ui.Image(root, "Mark", Art.Circle, done ? Palette.Primary : Palette.Inactive);
                Ui.PlaceCenter(mark.rectTransform, s * 0.35f, y, s * 0.12f, s * 0.12f);
                Box(root, s * 0.45f, y - s * 0.03f, s * 0.24f, s * 0.06f, s * 0.03f, done ? Palette.PrimarySoft : Palette.Track);
            }
        }

        private static void Pattern(RectTransform root, float s)
        {
            // Клубок цвета «узора дня» и золотая звезда сверху: новый узор каждый день.
            var ball = Ui.Image(root, "Ball", Art2.YarnBall(Palette.Link), Color.white);
            Ui.PlaceCenter(ball.rectTransform, s * 0.46f, s * 0.56f, s * 0.78f, s * 0.78f);
            var star = Ui.Image(root, "Star", Art.Icon(Icon.Star), Palette.Coin);
            Ui.PlaceCenter(star.rectTransform, s * 0.74f, s * 0.24f, s * 0.42f, s * 0.42f);
        }
    }
}
