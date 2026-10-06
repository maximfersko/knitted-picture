#nullable enable

using System;
using System.Collections.Generic;
using KnittedPicture.Core;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace KnittedPicture.Game
{
    /// <summary>
    /// Картинка на станке. Ткань-основа связана с начала и чуть светлее рисунка, связанные петли
    /// рисунка — с фактурой вязки; фронт каждого столбца (что вяжется следующим) яркий, клетка
    /// за ним — бледнее, остальное — едва видный эскиз (у загадки — пустые «дырки» в ткани).
    /// Петля становится вязаной, когда к ней долетает нитка (до этого она «в пути»).
    /// Золотые петли отмечены кольцом, пока не связаны.
    /// </summary>
    public sealed class PictureView
    {
        private const float FrontAlpha = 0.62f;
        private const float NextAlpha = 0.3f;
        private const float SketchAlpha = 0.12f;

        /// <summary>Насколько ткань светлее рисунка: картинка «проявляется» на ней.</summary>
        private const float FabricLighten = 0.22f;

        private readonly LoomGame _game;
        private readonly Color[] _colors;
        private readonly Image[] _cells;
        private readonly HashSet<int> _pending = new HashSet<int>();
        private readonly Dictionary<int, Image> _gold = new Dictionary<int, Image>();
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

            for (var y = 0; y < game.Height; y++)
            {
                for (var x = 0; x < game.Width; x++)
                {
                    if (!game.IsGold(x, y))
                    {
                        continue;
                    }

                    var ring = Ui.Image(Root, "Gold", Art.Ring, Palette.Coin);
                    Ui.PlaceCenter(ring.rectTransform, x * _step + cell / 2f, y * _step + cell / 2f, cell * 0.9f, cell * 0.9f);
                    var rt = ring.rectTransform;
                    Tweens.Run(rt, 1.2f, t => rt.localScale = Vector3.one * (1f + 0.12f * Ease.Bump(t)), null, null, (x + y) * 0.07f, true);
                    _gold[Key(x, y)] = ring;
                }
            }

            Refresh();
        }

        public RectTransform Root { get; }

        public float Width => _game.Width * _step - (_step - _cell);

        public float Height => _game.Height * _step - (_step - _cell);

        /// <summary>Связано и показано на экране (без петель «в пути»).</summary>
        public int LandedCount => _game.KnittedCount - _pending.Count;

        public void Refresh()
        {
            _pending.Clear();
            for (var x = 0; x < _game.Width; x++)
            {
                RefreshColumn(x);
            }
        }

        /// <summary>Перерисовать все столбцы, не трогая петли «в пути» (натяжение меняет активные фронты).</summary>
        public void RefreshColumns()
        {
            for (var x = 0; x < _game.Width; x++)
            {
                RefreshColumn(x);
            }
        }

        public void RefreshColumn(int x)
        {
            var front = _game.FrontY(x);
            var active = _game.ActiveFrontY(x) >= 0;
            var next = _game.AheadY(x, 1);
            for (var y = 0; y < _game.Height; y++)
            {
                var image = _cells[y * _game.Width + x];
                var color = _colors[_game.ColorAt(x, y)];
                var pending = _pending.Contains(Key(x, y));
                image.rectTransform.localScale = Vector3.one;
                if (_gold.TryGetValue(Key(x, y), out var gold))
                {
                    gold.enabled = !_game.IsKnitted(x, y) || pending;
                }

                if (_game.IsFabric(x, y))
                {
                    image.sprite = Art.FabricCell;
                    image.color = Color.Lerp(color, Color.white, FabricLighten);
                    continue;
                }

                if (_game.IsKnitted(x, y) && !pending)
                {
                    image.sprite = Art.StitchCell;
                    image.color = color;
                    continue;
                }

                image.sprite = Art.PlainCell;
                if (pending || (y == front && active))
                {
                    image.color = Palette.WithAlpha(color, FrontAlpha);
                }
                else if (y == front)
                {
                    // Фронт есть, но столбец ждёт соседей — натяжение полотна.
                    image.color = Palette.WithAlpha(color, NextAlpha);
                }
                else if (_game.Mystery)
                {
                    // Загадка: видно только фронт, остальное — дырки в ткани.
                    image.color = Palette.WithAlpha(Palette.Ink, 0.07f);
                }
                else if (y == next)
                {
                    image.color = Palette.WithAlpha(color, NextAlpha);
                }
                else
                {
                    image.color = Palette.WithAlpha(color, SketchAlpha);
                }
            }
        }

        /// <summary>Золотая петля связана: кольцо гаснет — вспышкой, если поймана во время комбо.</summary>
        public void ResolveGold(int x, int y, bool caught)
        {
            if (!_gold.TryGetValue(Key(x, y), out var ring))
            {
                return;
            }

            _gold.Remove(Key(x, y));
            var rt = ring.rectTransform;
            Tweens.Run(rt, 0.35f, t =>
            {
                rt.localScale = Vector3.one * (caught ? 1f + 0.9f * t : 1f - 0.6f * t);
                ring.color = Palette.WithAlpha(caught ? Palette.Coin : Palette.Ink3, 1f - t);
            }, null, () => Object.Destroy(rt.gameObject));
        }

        /// <summary>Петля связана в модели, нитка летит — клетка пока «в пути».</summary>
        public void MarkPending(int x, int y)
        {
            _pending.Add(Key(x, y));
            RefreshColumn(x);
        }

        /// <summary>Нитка долетела: петля «впрыгивает» с фактурой вязки.</summary>
        public bool Land(int x, int y)
        {
            if (!_pending.Remove(Key(x, y)))
            {
                return false;
            }

            if (!_game.IsKnitted(x, y))
            {
                RefreshColumn(x);
                return false;
            }

            var image = _cells[y * _game.Width + x];
            image.sprite = Art.StitchCell;
            image.color = _colors[_game.ColorAt(x, y)];
            Tweens.Run(image.rectTransform, 0.22f, t =>
                image.rectTransform.localScale = Vector3.one * (t < 0.5f ? Mathf.Lerp(0.5f, 1.18f, t * 2f) : Mathf.Lerp(1.18f, 1f, (t - 0.5f) * 2f)));
            return true;
        }

        public Vector3 CellWorld(int x, int y) => Ui.WorldCenter(_cells[y * _game.Width + x].rectTransform);

        /// <summary>Победная волна: ряды снизу вверх вспыхивают белым и возвращаются.</summary>
        public void Wave(Action done)
        {
            const float perRow = 0.045f;
            for (var y = _game.Height - 1; y >= 0; y--)
            {
                var row = _game.Height - 1 - y;
                for (var x = 0; x < _game.Width; x++)
                {
                    var image = _cells[y * _game.Width + x];
                    var color = _colors[_game.ColorAt(x, y)];
                    image.sprite = Art.StitchCell;
                    image.rectTransform.localScale = Vector3.one;
                    Tweens.Run(image.rectTransform, 0.32f, t =>
                    {
                        var bump = Ease.Bump(t);
                        image.color = Color.Lerp(color, Color.white, 0.75f * bump);
                        image.rectTransform.localScale = Vector3.one * (1f + 0.22f * bump);
                    }, null, () => image.color = color, row * perRow);
                }
            }

            Tweens.Delay(Root, _game.Height * perRow + 0.35f, done);
        }

        private int Key(int x, int y) => y * _game.Width + x;
    }
}
