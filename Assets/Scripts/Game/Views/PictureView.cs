#nullable enable

using KnittedPicture.Core;
using UnityEngine;
using UnityEngine.UI;

namespace KnittedPicture.Game
{
    /// <summary>
    /// Картинка на станке. Связанные петли — с фактурой вязки, следующие два ряда
    /// подсвечены подсказкой, остальное — бледный эскиз. Текущая петля в рамке.
    /// </summary>
    public sealed class PictureView
    {
        private readonly LoomGame _game;
        private readonly Color[] _colors;
        private readonly Image[] _cells;
        private readonly RectTransform _cursor;
        private readonly float _cell;
        private readonly float _step;

        public PictureView(RectTransform parent, LoomGame game, Color[] colors, float cell, float gap)
        {
            _game = game;
            _colors = colors;
            _cell = cell;
            _step = cell + gap;
            Root = Ui.Node(parent, "Picture");
            Root.sizeDelta = new Vector2(Width, Height);

            _cells = new Image[game.Width * game.Height];
            for (var y = 0; y < game.Height; y++)
            {
                for (var x = 0; x < game.Width; x++)
                {
                    var image = Ui.Image(Root, "Cell", Art.PlainCell, Color.white);
                    Ui.Place(image.rectTransform, x * _step, y * _step, cell, cell);
                    _cells[y * game.Width + x] = image;
                }
            }

            _cursor = Ui.Node(Root, "Cursor");
            var halo = Ui.Image(_cursor, "Halo", Art.RoundRectOutline(6, 3), Color.white);
            halo.type = UnityEngine.UI.Image.Type.Sliced;
            halo.pixelsPerUnitMultiplier = Art.Density;
            Ui.Place(halo.rectTransform, -4, -4, cell + 8, cell + 8);
            var ring = Ui.Image(_cursor, "Ring", Art.RoundRectOutline(5, 2.5f), Palette.Ink);
            ring.type = UnityEngine.UI.Image.Type.Sliced;
            ring.pixelsPerUnitMultiplier = Art.Density;
            Ui.Place(ring.rectTransform, -3, -3, cell + 6, cell + 6);
            Refresh();
        }

        public RectTransform Root { get; }

        public float Width => _game.Width * _step - (_step - _cell);

        public float Height => _game.Height * _step - (_step - _cell);

        /// <summary>Перерисовать все клетки по состоянию игры.</summary>
        public void Refresh()
        {
            var preview = _game.KnittedCount + _game.Width * 2;
            for (var y = 0; y < _game.Height; y++)
            {
                for (var x = 0; x < _game.Width; x++)
                {
                    var stitch = _game.StitchIndex(x, y);
                    var color = _colors[_game.ColorAt(x, y)];
                    var image = _cells[y * _game.Width + x];
                    image.rectTransform.localScale = Vector3.one;
                    if (stitch < _game.KnittedCount)
                    {
                        image.sprite = Art.StitchCell;
                        image.color = color;
                    }
                    else
                    {
                        image.sprite = Art.PlainCell;
                        image.color = Palette.WithAlpha(color, stitch < preview ? 0.38f : 0.14f);
                    }
                }
            }

            MoveCursor();
        }

        /// <summary>Петля связалась: клетка «впрыгивает» с фактурой вязки.</summary>
        public void Knit(int x, int y)
        {
            var image = _cells[y * _game.Width + x];
            image.sprite = Art.StitchCell;
            image.color = _colors[_game.ColorAt(x, y)];
            Tweens.Scale(image.rectTransform, 0.55f, 1f, 0.18f);
        }

        public void MoveCursor()
        {
            var (cx, cy) = _game.CurrentStitch;
            _cursor.gameObject.SetActive(cx >= 0);
            if (cx >= 0)
            {
                Ui.Place(_cursor, cx * _step, cy * _step, _cell, _cell);
                _cursor.SetSiblingIndex(_cursor.parent.childCount - 1);
            }
        }

        public void ShowCursorAt(int x, int y)
        {
            Ui.Place(_cursor, x * _step, y * _step, _cell, _cell);
        }

        public void HideCursor()
        {
            _cursor.gameObject.SetActive(false);
        }
    }
}
