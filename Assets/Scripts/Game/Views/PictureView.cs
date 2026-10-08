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
    /// Картинка на станке. Ткань-основа связана с начала: гладкая канва без петель, чуть светлее рисунка.
    /// Связанные петли рисунка — с фактурой вязки; следующая петля каждого столбика — в полном цвете
    /// и в рамке (столбики вяжутся снизу вверх), клетка за ней — бледнее, остальное — эскиз
    /// (у загадки — пустые «дырки» в ткани).
    /// Петля становится вязаной, когда к ней долетает нитка (до этого она «в пути»).
    /// Золотые петли отмечены кольцом, пока не связаны.
    /// </summary>
    public sealed class PictureView
    {
        // Следующая петля каждого столбика — в полном цвете и в рамке: оттенок, который нужен,
        // сравнивается с клубком один в один (бледные клетки путали близкие зелёные и коричневые).
        private const float FrontAlpha = 1f;
        private const float NextAlpha = 0.42f;
        private const float SketchAlpha = 0.2f;

        /// <summary>Насколько ткань светлее рисунка: картинка «проявляется» на ней.</summary>
        private const float FabricLighten = 0.22f;

        private readonly LoomGame _game;
        private readonly Color[] _colors;
        private readonly Image[] _cells;

        /// <summary>Рамка на клетке, которая свяжется следующей в столбике (по одной на столбик).</summary>
        private readonly Image[] _fronts;
        private readonly HashSet<int> _pending = new HashSet<int>();
        private readonly Dictionary<int, Image> _gold = new Dictionary<int, Image>();

        /// <summary>Золото ловится только во время комбо: тогда кольца яркие и пульсируют, иначе тусклые.</summary>
        private bool _goldLive = true;
        private readonly float _cell;
        private readonly float _step;

        // Натяжение полотна: пунктир, выше которого столбики ждут, и подсветка клеток, которые их держат.
        private RectTransform? _tension;
        private RectTransform? _lagLayer;
        private Image[]? _lagRings;

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

            // Рамки следующих петель: видно, что столбик вяжется снизу вверх и куда ляжет петля.
            _fronts = new Image[game.Width];
            for (var x = 0; x < game.Width; x++)
            {
                var mark = Ui.Image(Root, "Next", Art.RoundRectOutline(Mathf.Max(3f, cell * 0.22f), 1.6f), Color.white);
                mark.type = Image.Type.Sliced;
                mark.pixelsPerUnitMultiplier = Art.Density;
                Ui.Place(mark.rectTransform, x * _step + 2f, 0f, cell - 4f, cell - 4f);
                mark.enabled = false;
                _fronts[x] = mark;
            }

            for (var y = 0; y < game.Height; y++)
            {
                for (var x = 0; x < game.Width; x++)
                {
                    if (game.IsGold(x, y))
                    {
                        AddGoldRing(x, y);
                    }
                }
            }

            if (game.Band > 0)
            {
                BuildTension();
            }

            Refresh();
        }

        public RectTransform Root { get; }

        /// <summary>Натяжение прямо сейчас держит хотя бы один столбик (пунктир виден).</summary>
        public bool TensionHolding { get; private set; }

        /// <summary>Пунктир натяжения, если он сейчас виден: к нему привязывается подсказка.</summary>
        public RectTransform? TensionLine => TensionHolding ? _tension : null;

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

            UpdateTension();
        }

        /// <summary>Перерисовать все столбцы, не трогая петли «в пути» (натяжение меняет активные фронты).</summary>
        public void RefreshColumns()
        {
            for (var x = 0; x < _game.Width; x++)
            {
                RefreshColumn(x);
            }

            UpdateTension();
        }

        public void RefreshColumn(int x)
        {
            var front = _game.FrontY(x);
            var active = _game.ActiveFrontY(x) >= 0;
            var next = _game.AheadY(x, 1);
            var mark = _fronts[x];
            mark.enabled = active && front >= 0;
            if (mark.enabled)
            {
                var frontColor = _colors[_game.ColorAt(x, front)];
                mark.rectTransform.anchoredPosition = new Vector2(x * _step + 2f, -(front * _step + 2f));

                // На светлой клетке белая рамка не видна — тогда тёмная.
                mark.color = Luminance(frontColor) > 0.78f ? Palette.WithAlpha(Palette.Ink, 0.45f) : Palette.WithAlpha(Color.white, 0.9f);
            }

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

        private static float Luminance(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;

        /// <summary>
        /// Натяжение полотна видно глазами: когда столбик упирается в предел, поперёк картинки
        /// появляется пунктир (выше него не вяжется), а клетки самого нижнего ряда, которые держат
        /// остальных, мягко пульсируют рамкой. Когда никто не ждёт, ничего не показывается.
        /// </summary>
        private void BuildTension()
        {
            _lagLayer = Ui.Stretch(Ui.Node(Root, "Lagging"));
            var lagGroup = _lagLayer.gameObject.AddComponent<CanvasGroup>();
            lagGroup.blocksRaycasts = false;
            var radius = Mathf.Max(3f, _cell * 0.24f);
            _lagRings = new Image[_game.Width];
            for (var x = 0; x < _game.Width; x++)
            {
                var ring = Ui.Image(_lagLayer, "Lag", Art.RoundRectOutline(radius, 2), Palette.Ink);
                ring.type = Image.Type.Sliced;
                ring.pixelsPerUnitMultiplier = Art.Density;
                ring.enabled = false;
                _lagRings[x] = ring;
            }

            Tweens.Run(_lagLayer, 1.2f, t => lagGroup.alpha = 0.3f + 0.7f * Ease.Bump(t), null, null, 0f, true);

            // Пунктир — «натянутая нить» с булавками по краям.
            var width = Width + 8f;
            _tension = Ui.Node(Root, "Tension");
            Ui.Place(_tension, -4f, 0f, width, 8f);
            var dashColor = Palette.WithAlpha(Palette.Ink, 0.6f);
            const float dash = 6f;
            const float space = 4f;
            for (var dx = 4f; dx < width - 4f; dx += dash + space)
            {
                var piece = Ui.Box(_tension, "Dash", 1.25f, dashColor);
                Ui.Place(piece.rectTransform, dx, 2.75f, Mathf.Min(dash, width - 4f - dx), 2.5f);
            }

            foreach (var px in new[] { 0f, width })
            {
                var pin = Ui.Image(_tension, "Pin", Art.Circle, Palette.Ink);
                Ui.PlaceCenter(pin.rectTransform, px, 4f, 8f, 8f);
            }

            _tension.gameObject.SetActive(false);
            _lagLayer.gameObject.SetActive(false);
        }

        private void UpdateTension()
        {
            if (_tension == null || _lagLayer == null || _lagRings == null)
            {
                return;
            }

            var held = false;
            for (var x = 0; x < _game.Width && !held; x++)
            {
                held = _game.IsHeld(x);
            }

            TensionHolding = held;
            _tension.gameObject.SetActive(held);
            _lagLayer.gameObject.SetActive(held);
            if (!held)
            {
                return;
            }

            // Столбики с фронтом выше ряда limit ждут: пунктир проходит по верхнему краю этого ряда.
            var lowest = _game.LowestFront;
            var limit = lowest - _game.Band;
            var gap = _step - _cell;
            Ui.Place(_tension, -4f, limit * _step - gap / 2f - 4f, Width + 8f, 8f);
            for (var x = 0; x < _game.Width; x++)
            {
                var ring = _lagRings[x];
                ring.enabled = _game.FrontY(x) == lowest;
                if (ring.enabled)
                {
                    Ui.Place(ring.rectTransform, x * _step - 2f, lowest * _step - 2f, _cell + 4f, _cell + 4f);
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

        /// <summary>Отмена хода развязала золотую петлю: кольцо возвращается.</summary>
        public void RestoreGold(int x, int y)
        {
            if (_game.IsGold(x, y) && !_gold.ContainsKey(Key(x, y)))
            {
                AddGoldRing(x, y);
            }
        }

        private void AddGoldRing(int x, int y)
        {
            var ring = Ui.Image(Root, "Gold", Art.Ring, GoldColor(_goldLive));
            Ui.PlaceCenter(ring.rectTransform, x * _step + _cell / 2f, y * _step + _cell / 2f, _cell * 0.9f, _cell * 0.9f);
            var rt = ring.rectTransform;
            Tweens.Run(rt, 1.2f, t => rt.localScale = Vector3.one * (_goldLive ? 1f + 0.14f * Ease.Bump(t) : 0.92f), null, null, (x + y) * 0.07f, true);
            _gold[Key(x, y)] = ring;
        }

        /// <summary>Комбо включилось или сгорело: золотые кольца загораются или тускнеют.</summary>
        public void SetGoldLive(bool live)
        {
            if (_goldLive == live)
            {
                return;
            }

            _goldLive = live;
            foreach (var ring in _gold.Values)
            {
                ring.color = GoldColor(live);
            }
        }

        private static Color GoldColor(bool live) => live ? Palette.Coin : Palette.WithAlpha(Palette.CoinShadow, 0.4f);

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

        /// <summary>
        /// Цвет довязан: его клетки пробегает светлая волна снизу вверх — маленький праздник посреди уровня.
        /// Возвращает центр довязанного цвета на экране (для надписи) или null, если клеток нет.
        /// </summary>
        public Vector3? CelebrateColor(int color)
        {
            const float perRow = 0.035f;
            var sum = Vector3.zero;
            var count = 0;
            for (var y = _game.Height - 1; y >= 0; y--)
            {
                for (var x = 0; x < _game.Width; x++)
                {
                    if (_game.IsFabric(x, y) || _game.ColorAt(x, y) != color)
                    {
                        continue;
                    }

                    var image = _cells[y * _game.Width + x];
                    var baseColor = _colors[color];
                    var delay = (_game.Height - 1 - y) * perRow;
                    Tweens.Run(image.rectTransform, 0.3f, t =>
                    {
                        var bump = Ease.Bump(t);
                        image.color = Color.Lerp(baseColor, Color.white, 0.6f * bump);
                        image.rectTransform.localScale = Vector3.one * (1f + 0.18f * bump);
                    }, null, () =>
                    {
                        image.color = baseColor;
                        image.rectTransform.localScale = Vector3.one;
                    }, delay);
                    sum += Ui.WorldCenter(image.rectTransform);
                    count++;
                }
            }

            return count > 0 ? sum / count : (Vector3?)null;
        }

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
