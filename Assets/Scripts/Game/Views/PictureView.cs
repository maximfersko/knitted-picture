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

        private readonly LoomGame _game;
        private readonly Color[] _colors;
        private readonly Image[] _cells;

        /// <summary>Рамка на клетке, которая свяжется следующей в столбике (по одной на столбик).</summary>
        private readonly Image[] _fronts;
        private readonly HashSet<int> _pending = new HashSet<int>();
        private readonly Dictionary<int, Image> _gold = new Dictionary<int, Image>();
        private readonly List<(int X, int Y, Image Hole)> _holes = new List<(int X, int Y, Image Hole)>();

        /// <summary>Золото ловится только во время комбо: тогда кольца яркие и пульсируют, иначе тусклые.</summary>
        private bool _goldLive = true;
        private bool _mysteryPeek;

        /// <summary>Perfect на две секунды показывает следующий ряд загадки, сохраняя скрытыми дальние клетки.</summary>
        public void SetMysteryPeek(bool visible)
        {
            if (!_game.Mystery || _mysteryPeek == visible)
            {
                return;
            }

            _mysteryPeek = visible;
            RefreshColumns();
        }
        private readonly float _cell;
        private readonly float _step;

        // Натяжение полотна: линия, выше которой столбики ждут, и подсветка клеток, которые их держат.
        private RectTransform? _tension;
        private RectTransform? _lagLayer;
        private Image[]? _lagRings;
        private Image[]? _heldThreads;

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
                    // Опора в центре клетки: петля впрыгивает и качается вокруг своей середины.
                    Ui.PlaceCenter(image.rectTransform, x * _step + cell / 2f, y * _step + cell / 2f, cell, cell);
                    _cells[y * game.Width + x] = image;

                    // Блики петли — отдельным слоем поверх окрашенной клетки: краска не гасит блеск.
                    var gloss = Ui.Image(image.rectTransform, "Gloss", Art.StitchGloss, Color.white);
                    Ui.Stretch(gloss.rectTransform);
                    gloss.enabled = false;
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

        /// <summary>Натяжение прямо сейчас держит хотя бы один столбик (линия видна).</summary>
        public bool TensionHolding { get; private set; }

        /// <summary>Видимая линия натяжения: к ней привязывается подсказка.</summary>
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
            mark.enabled = front >= 0;
            if (mark.enabled)
            {
                var frontColor = _colors[_game.ColorAt(x, front)];
                mark.rectTransform.anchoredPosition = new Vector2(x * _step + 2f, -(front * _step + 2f));

                // На светлой клетке белая рамка не видна — тогда тёмная.
                mark.color = Luminance(frontColor) > 0.78f ? Palette.WithAlpha(Palette.Ink, 0.45f) : Palette.WithAlpha(Color.white, 0.9f);
                if (!active)
                {
                    mark.color = Palette.Danger;
                }
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
                    Gloss(image, false);
                    image.color = Palette.Fabric(color);
                    continue;
                }

                if (_game.IsKnitted(x, y) && !pending)
                {
                    image.sprite = Art.StitchCell;
                    Gloss(image, true);
                    image.color = color;
                    continue;
                }

                image.sprite = Art.PlainCell;
                Gloss(image, false);
                if (pending || (y == front && active))
                {
                    image.color = Palette.WithAlpha(color, FrontAlpha);
                }
                else if (y == front)
                {
                    // Фронт есть, но столбец ждёт соседей — натяжение полотна.
                    image.color = Palette.WithAlpha(color, NextAlpha);
                }
                else if (_game.Mystery && !(_mysteryPeek && y == next))
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
        /// появляется линия натяжения (выше неё не вяжется), а клетки самого нижнего ряда, которые держат
        /// остальных, мягко пульсируют рамкой. Когда никто не ждёт, ничего не показывается.
        /// </summary>
        private void BuildTension()
        {
            _lagLayer = Ui.Stretch(Ui.Node(Root, "Lagging"));
            var lagGroup = _lagLayer.gameObject.AddComponent<CanvasGroup>();
            lagGroup.blocksRaycasts = false;
            var radius = Mathf.Max(3f, _cell * 0.24f);
            _lagRings = new Image[_game.Width];
            _heldThreads = new Image[_game.Width];
            for (var x = 0; x < _game.Width; x++)
            {
                _heldThreads[x] = Ui.Box(_lagLayer, "HeldThread", 1f, Palette.WithAlpha(Palette.Danger, 0.7f));
                _heldThreads[x].enabled = false;
                var ring = Ui.Image(_lagLayer, "Lag", Art.RoundRectOutline(radius, 2), Palette.Ink);
                ring.type = Image.Type.Sliced;
                ring.pixelsPerUnitMultiplier = Art.Density;
                ring.enabled = false;
                _lagRings[x] = ring;
            }

            // «Меньше движения»: рамки не мигают, а горят ровно.
            lagGroup.alpha = 0.85f;
            Tweens.Run(_lagLayer, 1.2f, t => lagGroup.alpha = Tweens.Calm ? 0.85f : 0.3f + 0.7f * Ease.Bump(t), null, null, 0f, true);

            // Линия натяжения — «натянутая нить» с булавками по краям.
            var width = Width + 8f;
            _tension = Ui.Node(Root, "Tension");
            Ui.Place(_tension, -4f, 0f, width, 8f);
            var threadColor = Palette.WithAlpha(Palette.Ink, 0.6f);
            var thread = Ui.Box(_tension, "Thread", 1.25f, threadColor);
            Ui.Place(thread.rectTransform, 4f, 2.75f, width - 8f, 2.5f);

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

            // Столбики с фронтом выше ряда limit ждут: линия натяжения проходит по верхнему краю этого ряда.
            var lowest = _game.LowestFront;
            var limit = lowest - _game.Band;
            var gap = _step - _cell;
            Ui.Place(_tension, -4f, limit * _step - gap / 2f - 4f, Width + 8f, 8f);
            for (var x = 0; x < _game.Width; x++)
            {
                var ring = _lagRings[x];
                var thread = _heldThreads![x];
                thread.enabled = _game.IsHeld(x);
                if (thread.enabled)
                {
                    var top = _game.FrontY(x) * _step + _cell;
                    Ui.Place(thread.rectTransform, x * _step + _cell / 2f - 1f, top, 2f, lowest * _step + _cell / 2f - top);
                }
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
            Gloss(image, true);
            image.color = _colors[_game.ColorAt(x, y)];
            // Петля «впрыгивает»: из маленькой с перелётом, с поворотом-покачиванием и вспышкой цвета.
            var color = _colors[_game.ColorAt(x, y)];
            var rt = image.rectTransform;
            var tilt = (x + y) % 2 == 0 ? 10f : -10f;
            Tweens.Kill(rt, 32);
            Tweens.Run(rt, 0.26f, t =>
            {
                rt.localScale = Tweens.Calm ? Vector3.one : Vector3.one * Mathf.LerpUnclamped(0.4f, 1f, Ease.OutBack(t));
                rt.localRotation = Tweens.Calm ? Quaternion.identity : Quaternion.Euler(0f, 0f, tilt * (1f - t) * Mathf.Cos(t * Mathf.PI * 2f));
                image.color = Color.Lerp(Color.Lerp(color, Color.white, 0.55f), color, t);
            }, null, () =>
            {
                rt.localScale = Vector3.one;
                rt.localRotation = Quaternion.identity;
                image.color = color;
            }, channel: 32);

            // Столбик довязан до верха и ни одна его нитка не в пути: снизу вверх пробегает блик, как застёжка.
            if (_game.FrontY(x) < 0 && _game.Status != GameStatus.Won && !ColumnPending(x))
            {
                Zip(x);
            }

            return true;
        }

        private bool ColumnPending(int x)
        {
            for (var y = 0; y < _game.Height; y++)
            {
                if (_pending.Contains(Key(x, y)))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Блик по готовому столбику снизу вверх: петли по очереди светлеют и чуть подрастают.</summary>
        private void Zip(int x)
        {
            for (var y = _game.Height - 1; y >= 0; y--)
            {
                var image = _cells[y * _game.Width + x];
                var rt = image.rectTransform;

                // После «впрыгивания» последней петли (0,26 с), чтобы не спорить с ней за цвет и размер.
                var delay = 0.3f + (_game.Height - 1 - y) * 0.022f;
                var stitch = _colors[_game.ColorAt(x, y)];
                var baseColor = _game.IsFabric(x, y) ? Palette.Fabric(stitch) : stitch;
                Tweens.Run(rt, 0.2f, t =>
                {
                    var bump = Ease.Bump(t);
                    image.color = Color.Lerp(baseColor, Color.white, 0.5f * bump);
                    rt.localScale = Vector3.one * (1f + 0.08f * bump);
                }, null, () =>
                {
                    image.color = baseColor;
                    rt.localScale = Vector3.one;
                }, delay);
            }
        }

        /// <summary>Ряд y закрыт: петли по очереди слева направо вспыхивают и чуть подпрыгивают.</summary>
        public void Shimmer(int y)
        {
            if (y < 0 || y >= _game.Height)
            {
                return;
            }

            for (var x = 0; x < _game.Width; x++)
            {
                var image = _cells[y * _game.Width + x];
                if (!_game.IsKnitted(x, y) || _pending.Contains(Key(x, y)))
                {
                    continue;
                }

                var rt = image.rectTransform;
                var baseColor = image.color;
                Tweens.Run(rt, 0.28f, t =>
                {
                    var bump = Ease.Bump(t);
                    image.color = Color.Lerp(baseColor, Color.white, 0.45f * bump);
                    rt.localScale = Vector3.one * (1f + 0.1f * bump);
                }, null, () =>
                {
                    image.color = baseColor;
                    rt.localScale = Vector3.one;
                }, x * 0.03f);
            }
        }

        public Vector3 CellWorld(int x, int y) => Ui.WorldCenter(_cells[y * _game.Width + x].rectTransform);

        /// <summary>Клетка как цель для подсветки и выноски.</summary>
        public RectTransform CellRect(int x, int y) => _cells[y * _game.Width + x].rectTransform;

        public bool HasHoles => _holes.Count > 0;

        /// <summary>
        /// Моль прогрызла петлю: поверх клетки появляется дырка с рваным краем. Только картинка —
        /// модель не меняется: спасение «штопает» дырки, перезапуск начинает с чистого полотна.
        /// </summary>
        public void ShowHole(int x, int y)
        {
            var hole = Ui.Image(Root, "Hole", MothArt.Hole, Color.white);
            var rt = hole.rectTransform;
            Ui.PlaceCenter(rt, x * _step + _cell / 2f, y * _step + _cell / 2f, _cell * 0.92f, _cell * 0.92f);
            rt.localRotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 360f));
            Tweens.Run(rt, 0.28f, t => rt.localScale = Vector3.one * (t < 0.6f ? Mathf.Lerp(0.2f, 1.12f, t / 0.6f) : Mathf.Lerp(1.12f, 1f, (t - 0.6f) / 0.4f)));
            _holes.Add((x, y, hole));
        }

        /// <summary>Заштопать дырки: каждая затягивается к центру, по клетке пробегает светлая вспышка, из неё искры.</summary>
        public void MendHoles(RectTransform fx)
        {
            var i = 0;
            foreach (var (x, y, hole) in _holes)
            {
                var rt = hole.rectTransform;
                var cell = _cells[Key(x, y)];
                var color = cell.color;
                var turn = rt.localRotation.eulerAngles.z;
                var delay = 0.12f * i++;
                Tweens.Run(rt, 0.35f, t =>
                {
                    rt.localScale = Vector3.one * (1f - t);
                    rt.localRotation = Quaternion.Euler(0f, 0f, turn + 120f * t);
                }, Ease.InOutCubic, () =>
                {
                    Object.Destroy(hole.gameObject);
                    Fx.Sparkle(fx, Ui.WorldCenter(cell.rectTransform), color, 5, _cell);
                    Tweens.Run(cell.rectTransform, 0.3f, t =>
                    {
                        var bump = Ease.Bump(t);
                        cell.color = Color.Lerp(color, Color.white, 0.6f * bump);
                        cell.rectTransform.localScale = Vector3.one * (1f + 0.2f * bump);
                    }, null, () =>
                    {
                        cell.color = color;
                        cell.rectTransform.localScale = Vector3.one;
                    });
                }, delay);
            }

            _holes.Clear();
        }

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
                    Gloss(image, true);
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

        private static void Gloss(Image cell, bool on)
        {
            if (cell.transform.childCount > 0)
            {
                cell.transform.GetChild(0).GetComponent<Image>().enabled = on;
            }
        }
    }
}
