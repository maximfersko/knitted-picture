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
        private readonly Image[] _glosses;
        private readonly TextileRelief _relief;
        private readonly bool _coarseTextile;
        private readonly int[] _textileVariants;

        /// <summary>Рамка на клетке, которая свяжется следующей в столбике (по одной на столбик).</summary>
        private readonly Image[] _fronts;
        private readonly HashSet<int> _pending = new HashSet<int>();
        private readonly HashSet<int> _animating = new HashSet<int>();
        private const int CellChannel = 32;
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
        private Image[]? _heldKnots;
        private GameObject? _tensionHelp;

        public PictureView(RectTransform parent, LoomGame game, Color[] colors, float cell, float gap)
        {
            _game = game;
            _colors = colors;
            _cell = cell;
            _step = cell + gap;
            Root = Ui.Node(parent, "Picture");
            Root.sizeDelta = new Vector2(Width, Height);
            // Масштаб макета фиксируется при создании вида, до победного подъёма карточки.
            _coarseTextile = cell * Mathf.Abs(Root.lossyScale.x) < 18f;

            _cells = new Image[game.Width * game.Height];
            _glosses = new Image[_cells.Length];
            _textileVariants = BuildTextileVariants();
            for (var y = 0; y < game.Height; y++)
            {
                for (var x = 0; x < game.Width; x++)
                {
                    var image = Ui.Image(Root, "Cell", Art.FabricCell, Color.white);
                    // Опора в центре клетки: петля впрыгивает и качается вокруг своей середины.
                    Ui.PlaceCenter(image.rectTransform, x * _step + cell / 2f, y * _step + cell / 2f, cell, cell);
                    _cells[y * game.Width + x] = image;

                    // Блики петли — отдельным слоем поверх окрашенной клетки: краска не гасит блеск.
                    var gloss = Ui.Image(image.rectTransform, "Gloss", null, Color.white);
                    _glosses[y * game.Width + x] = gloss;
                    Ui.Stretch(gloss.rectTransform);
                    gloss.enabled = false;
                }
            }

            var reliefNode = Ui.Place(Ui.Node(Root, "TextileRelief"), 0f, 0f, Width, Height);
            _relief = reliefNode.gameObject.AddComponent<TextileRelief>();
            _relief.Configure(game.Width, game.Height, _step, _cell,
                (x, y) => _game.IsKnitted(x, y) && !_game.IsFabric(x, y) && !_pending.Contains(Key(x, y)));

            // Рамки следующих петель: видно, что столбик вяжется снизу вверх и куда ляжет петля.
            _fronts = new Image[game.Width];
            for (var x = 0; x < game.Width; x++)
            {
                var mark = Ui.Image(Root, "Next", Art.RoundRectOutline(Mathf.Max(3f, cell * 0.22f), 0.8f), Color.white);
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

        /// <summary>Кнопка объяснения видна только пока натяжение удерживает столбик.</summary>
        public void SetTensionHelp(GameObject help)
        {
            _tensionHelp = help;
            help.SetActive(TensionHolding);
        }

        public float Width => _game.Width * _step - (_step - _cell);

        public float Height => _game.Height * _step - (_step - _cell);

        /// <summary>Связано и показано на экране (без петель «в пути»).</summary>
        public int LandedCount => _game.KnittedCount - _pending.Count;

        public void Refresh()
        {
            _pending.Clear();
            _relief.SetVerticesDirty();
            _animating.Clear();
            // Полная перерисовка (в том числе после отмены) прекращает старые вспышки клеток.
            // Иначе отложенный твин снова окрашивает уже распущенную петлю как связанную.
            foreach (var cell in _cells)
            {
                Tweens.Kill(cell.rectTransform, CellChannel);
                cell.rectTransform.localScale = Vector3.one;
                cell.rectTransform.localRotation = Quaternion.identity;
            }
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
                mark.color = Luminance(frontColor) > 0.78f ? Palette.WithAlpha(Palette.Ink, 0.4f) : Palette.WithAlpha(Color.white, 0.7f);
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
                    SetTextile(image, x, y);
                    if (!_animating.Contains(Key(x, y))) { image.color = color; }
                    continue;
                }

                image.sprite = Art.FabricCell;
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
        /// остальных, отмечены спокойной рамкой. Когда никто не ждёт, ничего не показывается.
        /// </summary>
        private void BuildTension()
        {
            _lagLayer = Ui.Stretch(Ui.Node(Root, "Lagging"));
            var lagGroup = _lagLayer.gameObject.AddComponent<CanvasGroup>();
            lagGroup.blocksRaycasts = false;
            var radius = Mathf.Max(3f, _cell * 0.24f);
            _lagRings = new Image[_game.Width];
            _heldThreads = new Image[_game.Width];
            _heldKnots = new Image[_game.Width];
            var cord = Art2.Get("tension-cord", KnitRaster.RopeTile);
            for (var x = 0; x < _game.Width; x++)
            {
                _heldThreads[x] = Ui.Image(_lagLayer, "HeldThread", cord, Palette.TensionCord);
                _heldThreads[x].type = Image.Type.Tiled;
                _heldThreads[x].pixelsPerUnitMultiplier = 4f;
                _heldKnots[x] = Ui.Image(_lagLayer, "Knot", Art.Circle, Palette.TensionCord);
                _heldKnots[x].enabled = false;
                _heldThreads[x].enabled = false;
                var ring = Ui.Image(_lagLayer, "Lag", Art.RoundRectOutline(radius, 2), Palette.PrimaryShadow);
                ring.type = Image.Type.Sliced;
                ring.pixelsPerUnitMultiplier = Art.Density;
                ring.enabled = false;
                _lagRings[x] = ring;
            }

            // Рамки остаются видимыми без постоянного движения.
            lagGroup.alpha = 0.75f;

            // Тонкий крученый шнур и небольшие деревянные крепления.
            var width = Width + 8f;
            _tension = Ui.Node(Root, "Tension");
            Ui.Place(_tension, -4f, 0f, width, 8f);
            var shadow = Ui.Box(_tension, "ContactShadow", 2f, Palette.WithAlpha(Palette.Ink, 0.14f));
            Ui.Place(shadow.rectTransform, 4f, 4f, width - 8f, 3f);
            var thread = Ui.Image(_tension, "Thread", cord, Palette.TensionCord);
            thread.type = Image.Type.Tiled;
            thread.pixelsPerUnitMultiplier = 4f;
            Ui.Place(thread.rectTransform, 4f, 2f, width - 8f, 4f);

            foreach (var px in new[] { 0f, width })
            {
                var pin = Ui.Image(_tension, "Pin", Art.Circle, Palette.WoodDark);
                Ui.PlaceCenter(pin.rectTransform, px, 4f, 6f, 6f);
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
            if (_tensionHelp != null)
            {
                _tensionHelp.SetActive(held);
            }
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
                _heldKnots![x].enabled = thread.enabled;
                if (thread.enabled)
                {
                    var top = _game.FrontY(x) * _step + _cell;
                    var end = limit * _step - gap / 2f;
                    var length = Mathf.Max(0f, end - top);
                    Ui.PlaceCenter(thread.rectTransform, x * _step + _cell / 2f, top + length / 2f, length, 4f);
                    thread.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    Ui.PlaceCenter(_heldKnots[x].rectTransform, x * _step + _cell / 2f, end, 5f, 5f);
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
            Tweens.Kill(rt, 35);
            Tweens.Run(rt, 0.35f, t =>
            {
                rt.localScale = Vector3.one * (caught ? 1f + 0.9f * t : 1f - 0.6f * t);
                ring.color = Palette.WithAlpha(caught ? Palette.Coin : Palette.Ink3, 1f - t);
            }, null, () => Object.Destroy(rt.gameObject));
        }

        /// <summary>Убрать уже засчитанное кольцо при восстановлении представления без повторного эффекта.</summary>
        public void ForgetGold(int x, int y)
        {
            var key = Key(x, y);
            if (_gold.TryGetValue(key, out var ring))
            {
                _gold.Remove(key);
                Tweens.Kill(ring.rectTransform, 35);
                Object.Destroy(ring.gameObject);
            }
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
            Tweens.Run(rt, 1.2f, t => rt.localScale = Vector3.one * (_goldLive ? 1f + 0.14f * Ease.Bump(t) : 0.92f), null, null, (x + y) * 0.07f, true, 35);
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
            _relief.SetVerticesDirty();
            RefreshColumn(x);
        }

        /// <summary>Нитка долетела: петля «впрыгивает» с фактурой вязки.</summary>
        public bool Land(int x, int y)
        {
            if (!_pending.Remove(Key(x, y)))
            {
                return false;
            }

            _relief.SetVerticesDirty();

            if (!_game.IsKnitted(x, y))
            {
                RefreshColumn(x);
                return false;
            }

            var image = _cells[y * _game.Width + x];
            SetTextile(image, x, y);
            image.color = _colors[_game.ColorAt(x, y)];
            // Петля «впрыгивает»: из маленькой с перелётом, с поворотом-покачиванием и вспышкой цвета.
            var color = _colors[_game.ColorAt(x, y)];
            var rt = image.rectTransform;
            var tilt = (x + y) % 2 == 0 ? 2.5f : -2.5f;
            Tweens.Kill(rt, CellChannel);
            _animating.Add(Key(x, y));
            Tweens.Run(rt, 0.26f, t =>
            {
                rt.localScale = Tweens.Calm ? Vector3.one : Vector3.one * Mathf.Lerp(0.88f, 1f, Ease.OutCubic(t));
                rt.localRotation = Tweens.Calm ? Quaternion.identity : Quaternion.Euler(0f, 0f, tilt * (1f - t) * Mathf.Cos(t * Mathf.PI * 2f));
                image.color = Color.Lerp(Color.Lerp(color, Color.white, 0.18f), color, t);
            }, null, () =>
            {
                rt.localScale = Vector3.one;
                rt.localRotation = Quaternion.identity;
                image.color = color;
                _animating.Remove(Key(x, y));
            }, channel: CellChannel);

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
                var key = Key(x, y);
                if (!_animating.Add(key)) { continue; }

                // После «впрыгивания» последней петли (0,26 с), чтобы не спорить с ней за цвет и размер.
                var delay = 0.3f + (_game.Height - 1 - y) * 0.022f;
                var stitch = _colors[_game.ColorAt(x, y)];
                var baseColor = _game.IsFabric(x, y) ? Palette.Fabric(stitch) : stitch;
                Tweens.Run(rt, 0.2f, t =>
                {
                    var bump = Ease.Bump(t);
                    image.color = Color.Lerp(baseColor, Color.white, 0.5f * bump);
                    rt.localScale = Tweens.Calm ? Vector3.one : Vector3.one * (1f + 0.025f * bump);
                }, null, () =>
                {
                    image.color = baseColor;
                    rt.localScale = Vector3.one;
                    _animating.Remove(key);
                }, delay, channel: CellChannel);
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
                var key = Key(x, y);
                if (!_animating.Add(key)) { continue; }
                var baseColor = _colors[_game.ColorAt(x, y)];
                Tweens.Run(rt, 0.28f, t =>
                {
                    var bump = Ease.Bump(t);
                    image.color = Color.Lerp(baseColor, Color.white, 0.45f * bump);
                    rt.localScale = Tweens.Calm ? Vector3.one : Vector3.one * (1f + 0.025f * bump);
                }, null, () =>
                {
                    image.color = baseColor;
                    rt.localScale = Vector3.one;
                    _animating.Remove(key);
                }, x * 0.03f, channel: CellChannel);
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
                    if (_game.IsKnitted(x, y))
                    {
                        SetTextile(cell, x, y);
                    }
                    Fx.Sparkle(fx, Ui.WorldCenter(cell.rectTransform), color, 5, _cell);
                    var key = Key(x, y);
                    if (!_animating.Add(key)) { return; }
                    Tweens.Run(cell.rectTransform, 0.3f, t =>
                    {
                        var bump = Ease.Bump(t);
                        cell.color = Color.Lerp(color, Color.white, 0.6f * bump);
                        cell.rectTransform.localScale = Vector3.one * (1f + 0.2f * bump);
                    }, null, () =>
                    {
                        cell.color = color;
                        cell.rectTransform.localScale = Vector3.one;
                        _animating.Remove(key);
                    }, channel: CellChannel);
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
                    var key = Key(x, y);
                    if (_pending.Contains(key) || !_animating.Add(key)) { continue; }
                    var baseColor = _colors[color];
                    var delay = (_game.Height - 1 - y) * perRow;
                    Tweens.Run(image.rectTransform, 0.3f, t =>
                    {
                        var bump = Ease.Bump(t);
                        image.color = Color.Lerp(baseColor, Color.white, 0.6f * bump);
                        image.rectTransform.localScale = Tweens.Calm ? Vector3.one : Vector3.one * (1f + 0.035f * bump);
                    }, null, () =>
                    {
                        image.color = baseColor;
                        image.rectTransform.localScale = Vector3.one;
                        _animating.Remove(key);
                    }, delay, channel: CellChannel);
                    sum += Ui.WorldCenter(image.rectTransform);
                    count++;
                }
            }

            return count > 0 ? sum / count : (Vector3?)null;
        }

        /// <summary>Готовая работа ловит свет: мягкий блик проходит по фактуре, затем вызывается продолжение.</summary>
        public void Reveal(Action done)
        {
            var sheen = Ui.Image(Root, "FinishedSheen", Art.Glow, Color.clear);
            Ui.PlaceCenter(sheen.rectTransform, Width * 0.1f, Height * 0.5f, Width * 0.32f, Height);
            Tweens.Run(sheen, 0.5f, t =>
            {
                sheen.rectTransform.anchoredPosition = new Vector2(Width * Mathf.Lerp(0.1f, 0.9f, t), -Height * 0.5f);
                sheen.color = Palette.WithAlpha(Color.white, Mathf.Sin(t * Mathf.PI) * (Tweens.Calm ? 0.12f : 0.42f));
            }, null, () =>
            {
                Object.Destroy(sheen.gameObject);
                done();
            });
        }

        /// <summary>Победная волна: ряды снизу вверх вспыхивают белым и возвращаются.</summary>
        public void Wave(Action done)
        {
            Refresh();
            const float perRow = 0.045f;
            for (var y = _game.Height - 1; y >= 0; y--)
            {
                var row = _game.Height - 1 - y;
                for (var x = 0; x < _game.Width; x++)
                {
                    var image = _cells[y * _game.Width + x];
                    var color = _colors[_game.ColorAt(x, y)];
                    SetTextile(image, x, y);
                    image.rectTransform.localScale = Vector3.one;
                    Tweens.Run(image.rectTransform, 0.32f, t =>
                    {
                        var bump = Ease.Bump(t);
                        image.color = Color.Lerp(color, Color.white, 0.22f * bump);
                        image.rectTransform.localScale = Tweens.Calm ? Vector3.one : Vector3.one * (1f + 0.06f * bump);
                    }, null, () => { image.color = color; image.rectTransform.localScale = Vector3.one; image.rectTransform.localRotation = Quaternion.identity; }, row * perRow, channel: CellChannel);
                }
            }

            Tweens.Delay(Root, _game.Height * perRow + 0.35f, done);
        }

        private int Key(int x, int y) => y * _game.Width + x;

        /// <summary>Назначить согласованные основу и блик по материалу области и размеру клетки на экране.</summary>
        private void SetTextile(Image image, int x, int y)
        {
            // Screen Space Overlay уже включает масштаб Canvas в lossyScale.
            var key = Key(x, y);
            var variant = _textileVariants[key];
            image.sprite = Art2.Textile(variant, _coarseTextile, false);
            var gloss = _glosses[key];
            gloss.sprite = Art2.Textile(variant, _coarseTextile, true);
            gloss.enabled = true;
        }

        /// <summary>Материалы областей исходной картинки остаются стабильными при отмене.</summary>
        private int[] BuildTextileVariants() => KnitRaster.TextileVariants(_game.Width, _game.Height,
            (x, y) => _game.ColorAt(x, y), (x, y) => _game.IsFabric(x, y), _game.Mystery);

        private static void Gloss(Image cell, bool on)
        {
            if (cell.transform.childCount > 0)
            {
                cell.transform.GetChild(0).GetComponent<Image>().enabled = on;
            }
        }
    }

    /// <summary>Один меш мягкой тени по внешним границам уже показанной вязки.</summary>
    public sealed class TextileRelief : MaskableGraphic
    {
        private int _width;
        private int _height;
        private float _step;
        private float _cell;
        private Func<int, int, bool>? _shown;

        public void Configure(int width, int height, float step, float cell, Func<int, int, bool> shown)
        {
            _width = width;
            _height = height;
            _step = step;
            _cell = cell;
            _shown = shown;
            raycastTarget = false;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_shown == null) { return; }
            var thickness = 0.85f / Mathf.Max(0.001f, Mathf.Abs(rectTransform.lossyScale.x));
            for (var y = 0; y < _height; y++)
            for (var x = 0; x < _width; x++)
            {
                if (!_shown(x, y)) { continue; }
                var left = x * _step;
                var top = -y * _step;
                if (x + 1 < _width && !_shown(x + 1, y))
                {
                    Quad(vh, left + _cell, top, left + _cell + thickness, top - _cell, true);
                }
                if (y + 1 < _height && !_shown(x, y + 1))
                {
                    Quad(vh, left, top - _cell, left + _cell, top - _cell - thickness, false);
                }
            }
        }

        private static void Quad(VertexHelper vh, float left, float top, float right, float bottom, bool vertical)
        {
            var index = vh.currentVertCount;
            Color32 shade = Palette.WithAlpha(Palette.Ink, KnitRaster.ReliefAlpha);
            Color32 clear = Palette.WithAlpha(Palette.Ink, 0f);
            vh.AddVert(new Vector3(left, top), shade, Vector4.zero);
            vh.AddVert(new Vector3(right, top), vertical ? clear : shade, Vector4.zero);
            vh.AddVert(new Vector3(right, bottom), clear, Vector4.zero);
            vh.AddVert(new Vector3(left, bottom), vertical ? shade : clear, Vector4.zero);
            vh.AddTriangle(index, index + 1, index + 2);
            vh.AddTriangle(index, index + 2, index + 3);
        }
    }
}
