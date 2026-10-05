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
            var key = level.id + (faded ? "f" : "");
            if (!Textures.TryGetValue(key, out var texture) || texture == null)
            {
                texture = Art.PictureTexture(level, faded ? 0 : int.MaxValue);
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

        /// <summary>Плитка альбома: готовая картинка, текущая (бледная, в рамке) или замок.</summary>
        public static Tap Tile(RectTransform parent, LevelData level, TileState state, float x, float y, float size)
        {
            var root = Ui.Card(parent, "Tile" + level.id, x, y, size, size, 14,
                state == TileState.Locked ? Palette.Track : Palette.Card, Palette.CardShadow, state == TileState.Locked ? 0f : 2f, true);
            var inner = size - 14f;
            switch (state)
            {
                case TileState.Done:
                    Create(root, level, size / 2f, size / 2f, inner);
                    break;
                case TileState.Current:
                    Create(root, level, size / 2f, size / 2f, inner, faded: true);
                    var ring = Ui.Image(root, "Ring", Art.RoundRectOutline(14, 3), Palette.Primary);
                    ring.type = Image.Type.Sliced;
                    ring.pixelsPerUnitMultiplier = Art.Density;
                    Ui.Place(ring.rectTransform, 0, 0, size, size);
                    break;
                default:
                    Ui.IconImage(root, Icon.Lock, Palette.Ink3, size / 2f, size / 2f, size * 0.32f);
                    break;
            }

            var tap = root.gameObject.AddComponent<Tap>();
            tap.Interactable = state != TileState.Locked;
            return tap;
        }
    }
}
