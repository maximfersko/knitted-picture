#nullable enable

using System.Collections.Generic;
using KnittedPicture.Core;
using UnityEngine;
using UnityEngine.UI;

namespace KnittedPicture.Game
{
    public enum TileState
    {
        Done,
        Current,
        Locked,
    }

    /// <summary>Маленькая картинка уровня (альбом, главная, победа) — одна текстура вместо сотни клеток.</summary>
    public static class MiniPicture
    {
        private static readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>();

        public static RawImage Create(RectTransform parent, LevelData level, float cx, float cy, float size, bool faded = false)
        {
            // У «узора дня» номер один на все дни — ключ включает и картинку, чтобы не показать вчерашнюю.
            var key = level.id + ":" + level.name + (faded ? "f" : "");
            if (!Textures.TryGetValue(key, out var texture) || texture == null)
            {
                texture = Art.PictureTexture(level, done: !faded);
                Textures[key] = texture;
            }

            var rt = Ui.Node(parent, "Mini");
            var raw = rt.gameObject.AddComponent<RawImage>();
            raw.texture = texture;
            raw.raycastTarget = false;
            var aspect = (float)level.width / level.height;
            var w = aspect >= 1 ? size : size * aspect;
            var h = aspect >= 1 ? size / aspect : size;
            Ui.PlaceCenter(rt, cx, cy, w, h);
            return raw;
        }

        /// <summary>Силуэт закрытой картинки цветом color: видно, что там будет, но не что именно.</summary>
        public static RawImage Silhouette(RectTransform parent, LevelData level, float cx, float cy, float size, Color color)
        {
            var key = level.id + ":" + level.name + "s";
            if (!Textures.TryGetValue(key, out var texture) || texture == null)
            {
                texture = Art.SilhouetteTexture(level);
                Textures[key] = texture;
            }

            var rt = Ui.Node(parent, "Silhouette");
            var raw = rt.gameObject.AddComponent<RawImage>();
            raw.texture = texture;
            raw.color = color;
            raw.raycastTarget = false;
            var aspect = (float)level.width / level.height;
            Ui.PlaceCenter(rt, cx, cy, aspect >= 1 ? size : size * aspect, aspect >= 1 ? size / aspect : size);
            return raw;
        }

        /// <summary>
        /// Деревянная полочка под рядом плиток: картинки «стоят» на ней, как в настоящем альбоме-витрине.
        /// Рисуется до плиток, чтобы они были поверх.
        /// </summary>
        public static void Shelf(RectTransform parent, float x, float y, float w)
        {
            var lip = Ui.Box(parent, "ShelfLip", 3, Palette.WoodDark);
            Ui.Place(lip.rectTransform, x, y + 2f, w, 8);
            var plank = Ui.Box(parent, "Shelf", 3, Palette.Wood);
            Ui.Place(plank.rectTransform, x, y, w, 7);
        }

        /// <summary>На сколько плашка звёзд выступает под плиткой: подписи ставим ниже.</summary>
        public const float StarsOverhang = 11f;

        /// <summary>
        /// Три звезды на белой плашке-«ленточке» по центру нижнего края плитки: ровный ряд,
        /// заработанные — золотые, пустые — лавандовые; плашка не даёт им потеряться на картинке.
        /// </summary>
        public static void Stars(RectTransform parent, float cx, float cy, int stars, float size)
        {
            var gap = Mathf.Round(size * 0.2f);
            var w = 3 * size + 2 * gap + 12f;
            var h = size + 8f;
            var pill = Ui.Card(parent, "Stars", cx - w / 2f, cy - h / 2f, w, h, h / 2f, Palette.Card, Palette.CardShadow, 1.5f);
            for (var i = 0; i < 3; i++)
            {
                var star = Ui.Image(pill, "Star", Art.Icon(Icon.Star), i < stars ? Palette.Coin : Palette.Inactive);
                Ui.PlaceCenter(star.rectTransform, 6f + size / 2f + i * (size + gap), h / 2f, size, size);
            }
        }

        /// <summary>Плитка альбома: готовая картинка, текущая (бледная, в рамке) или замок.</summary>

        public static Tap Tile(RectTransform parent, LevelData level, TileState state, float x, float y, float size, int stars = 0)
        {
            // Готовая картинка — в деревянной рамке с белым паспарту, как на стене; текущая — бледный эскиз
            // в зелёной рамке; закрытая — силуэт, чтобы было любопытно, что там.
            var face = state == TileState.Done ? Palette.Wood : state == TileState.Locked ? Palette.Track : Palette.Surface;
            var root = Ui.Card(parent, "Tile" + level.id, x, y, size, size, 14, face, Palette.CardShadow, state == TileState.Locked ? 0f : 2f, true);
            var inner = size - 14f;
            switch (state)
            {
                case TileState.Done:
                    var mat = Ui.Box(root, "Mat", 10, Palette.Card);
                    Ui.Place(mat.rectTransform, 4f, 4f, size - 8f, size - 8f);
                    var bevel = Ui.Outline(root, 14, Palette.WoodDark, 1.5f);
                    bevel.raycastTarget = false;
                    Create(root, level, size / 2f, size / 2f, inner);
                    break;
                case TileState.Current:
                    Create(root, level, size / 2f, size / 2f, inner, faded: true);
                    if (level.mystery)
                    {
                        Ui.Label(root, "?", size * 0.4f, Palette.Ink2, 0, 0, size, size);
                    }

                    var ring = Ui.Image(root, "Ring", Art.RoundRectOutline(14, 3), Palette.Primary);
                    ring.type = Image.Type.Sliced;
                    ring.pixelsPerUnitMultiplier = Art.Density;
                    Ui.Place(ring.rectTransform, 0, 0, size, size);
                    break;
                default:
                    if (level.mystery)
                    {
                        Ui.IconImage(root, Icon.Lock, Palette.Ink3, size / 2f, size / 2f, size * 0.32f);
                    }
                    else
                    {
                        Silhouette(root, level, size / 2f, size / 2f, inner, Palette.WithAlpha(Palette.Ink3, 0.32f));
                        var lockBack = Ui.Image(root, "LockBack", Art.Circle, Palette.Card);
                        Ui.PlaceCenter(lockBack.rectTransform, size - 13f, size - 13f, 20f, 20f);
                        Ui.IconImage(root, Icon.Lock, Palette.Ink3, size - 13f, size - 13f, 12f);
                    }

                    break;
            }

            if (state == TileState.Done && stars > 0)
            {
                Stars(root, size / 2f, size, stars, Mathf.Clamp(size * 0.18f, 10f, 13f));
            }

            var tap = root.gameObject.AddComponent<Tap>();
            // Закрытая плитка тоже отвечает на тап — подсказкой, когда она откроется.
            tap.PressFeedback = state != TileState.Locked;
            return tap;
        }
    }
}
