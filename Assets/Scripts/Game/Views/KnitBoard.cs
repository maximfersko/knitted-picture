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
    /// Доска 2.0: деревянная рамка, на ней бумажная схема, а поверх — настоящее полотно на спице.
    ///
    /// Как читается: над спицей — схема следующих рядов (бледные клетки цвета, номера рядов слева),
    /// под спицей — связанное полотно из лицевых петель. Столбики вяжутся снизу вверх; спица стоит
    /// над самым высоким рядом, у столбиков, дошедших до неё, на спице живые петли. Отстающие столбики
    /// ниже спицы показывают свои клетки схемы — видно, чего они ждут. Следующая петля каждого
    /// столбика — клетка полного цвета в пунктирной рамке.
    ///
    /// Камера: пока картинка помещается целиком, полотно стоит, а спица поднимается ряд за рядом.
    /// Высокая картинка свисает со спицы, как настоящее вязание: новые ряды появляются под спицей,
    /// полотно плавно уходит вниз, над спицей всегда видно три ряда схемы, а самый отстающий столбик
    /// не уходит из кадра.
    ///
    /// Вязание петли (Knit → Land): петля рождается у спицы, соскальзывает вниз на своё место,
    /// вытягиваясь и садясь с пружинкой; петли ниже в столбике чуть подтягиваются, как ткань;
    /// столбик, дошедший до спицы, накидывает на неё живую петлю. Ряд закрыт — по нему пробегает волна.
    /// </summary>
    public sealed class KnitBoard
    {
        private const float Margin = 7f;
        private const float Header = 30f;
        private const float LabelW = 20f;
        private const float SchemaRows = 3.2f;
        private const float LandTime = 0.22f;

        private static readonly Color PaperInk = Palette.Hex("#FBF7EF");

        private readonly LoomGame _game;
        private readonly Color[] _colors;
        private readonly float _width;
        private readonly float _height;
        private readonly float _gridX;
        private readonly float _cw;
        private readonly float _rh;
        private readonly float _gap;
        private readonly float _viewH;

        private readonly RectTransform _content;
        private readonly RectTransform[] _schemaRows;
        private readonly RectTransform[] _fabricRows;
        private readonly RectTransform[] _labelRows;
        private readonly Image[] _squares;
        private readonly Image[] _stitches;
        private readonly RectTransform _overlay;
        private readonly Image[] _fronts;
        private readonly Image[] _loops;
        private readonly RectTransform _needle;
        private readonly Text _rowText;
        private readonly HashSet<int> _pending = new HashSet<int>();
        private readonly Dictionary<int, Image> _gold = new Dictionary<int, Image>();
        private readonly List<(int X, int Y, Image Hole)> _holes = new List<(int X, int Y, Image Hole)>();
        private readonly List<Tug> _tugs = new List<Tug>();

        private RectTransform? _tension;
        private bool _goldLive = true;
        private float _n;
        private float _ny;
        private float _nTarget;
        private float _nyTarget;
        private int _shownRow = -1;

        /// <summary>Спица «приседает» на каждой петле: пружина смещения вниз, точки.</summary>
        private float _dip;
        private float _dipVelocity;
        private bool _won;
        private int _lowestRow = -1;

        public KnitBoard(RectTransform parent, LoomGame game, Color[] colors, string title, float width, float height)
        {
            _game = game;
            _colors = colors;
            _width = width;
            _height = height;
            Root = Ui.Node(parent, "KnitBoard");
            Root.sizeDelta = new Vector2(width, height);

            var frame = Ui.Image(Root, "Wood", Art2.WoodFrame, Color.white);
            frame.type = Image.Type.Sliced;
            frame.pixelsPerUnitMultiplier = Art.Density;
            Ui.Stretch(frame.rectTransform);

            var view = Ui.Place(Ui.Node(Root, "View"), Margin, Margin, width - 2 * Margin, height - 2 * Margin);
            view.gameObject.AddComponent<RectMask2D>();
            _viewH = height - 2 * Margin;
            var paper = view.gameObject.AddComponent<RawImage>();
            paper.texture = Art2.Paper;
            paper.raycastTarget = false;
            paper.uvRect = new Rect(0f, 0f, (width - 2 * Margin) * Art.Density / 128f, _viewH * Art.Density / 128f);

            // Сетка: ширина столбика ограничена 30 точками, высота ряда — как у настоящей петли (шире, чем выше).
            var gridW = width - 2 * Margin - LabelW - 8f;
            _cw = Mathf.Min(gridW / game.Width, 30f);
            _rh = _cw * 0.82f;
            _gap = Mathf.Max(_rh * 0.55f, 9f);
            _gridX = LabelW + (gridW - _cw * game.Width) / 2f;

            _content = Ui.Stretch(Ui.Node(view, "Content"));
            var schema = Layer(_content, "Schema");
            var labels = Layer(_content, "Labels");
            var fabric = Layer(_content, "Fabric");
            _overlay = Layer(_content, "Overlay");

            var w = game.Width;
            var h = game.Height;
            _schemaRows = new RectTransform[h];
            _fabricRows = new RectTransform[h];
            _labelRows = new RectTransform[h];
            _squares = new Image[w * h];
            _stitches = new Image[w * h];

            // Ряды снизу вверх: у полотна верхний ряд должен лежать поверх нижнего (дольки заходят вверх).
            for (var r = 0; r < h; r++)
            {
                var y = h - 1 - r;
                _schemaRows[r] = Row(schema, "SchemaRow" + r);
                _fabricRows[r] = Row(fabric, "FabricRow" + r);
                _labelRows[r] = Row(labels, "Label" + r);
                var number = Ui.Label(_labelRows[r], (r + 1).ToString(), 12, Palette.Ink3, 0, -_rh / 2f, LabelW, _rh, true, TextAnchor.MiddleCenter);
                Ui.Fit(number, 8);

                for (var x = 0; x < w; x++)
                {
                    var cx = _gridX + x * _cw + _cw / 2f;
                    var square = Ui.Box(_schemaRows[r], "Cell", Mathf.Max(3f, _cw * 0.2f), Color.white);
                    Ui.PlaceCenter(square.rectTransform, cx, 0f, _cw * 0.78f, _rh * 0.74f);
                    _squares[y * w + x] = square;

                    Image stitch;
                    if (game.IsFabric(x, y))
                    {
                        // Ткань-основа: гладкая канва, готова с самого начала.
                        stitch = Ui.Image(_fabricRows[r], "Canvas", Art.FabricCell, Palette.Fabric(colors[game.ColorAt(x, y)]));
                        Ui.PlaceCenter(stitch.rectTransform, cx, 0f, _cw * 0.98f, _rh * 0.98f);
                    }
                    else
                    {
                        stitch = Ui.Image(_fabricRows[r], "Stitch", Art2.Stitch(colors[game.ColorAt(x, y)]), Color.white);
                        var sw = _cw * 1.06f;
                        var sh = sw * KnitRaster.StitchH / KnitRaster.StitchW;
                        var rt = stitch.rectTransform;
                        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                        rt.pivot = new Vector2(0.5f, KnitRaster.StitchTip * 0.6f);
                        rt.sizeDelta = new Vector2(sw, sh);
                        rt.anchoredPosition = new Vector2(cx, -_rh / 2f);
                    }

                    stitch.enabled = false;
                    _stitches[y * w + x] = stitch;
                }
            }

            // Шапка схемы поверх рядов (у высокой картинки ряды уходят под неё): своя полоска бумаги,
            // название и какой ряд вяжется.
            var header = Ui.Place(Ui.Node(view, "Header"), 0f, 0f, width - 2 * Margin, Header);
            var headerPaper = header.gameObject.AddComponent<RawImage>();
            headerPaper.texture = Art2.Paper;
            headerPaper.raycastTarget = false;
            headerPaper.uvRect = new Rect(0f, (_viewH - Header) * Art.Density / 128f, (width - 2 * Margin) * Art.Density / 128f, Header * Art.Density / 128f);
            var titleLabel = Ui.Label(header, "Схема · «" + title + "»", 16, Palette.Ink2, 12, 4, width * 0.62f, 24, true, TextAnchor.MiddleLeft);
            Ui.Fit(titleLabel, 11);
            _rowText = Ui.Label(header, "", 13, Palette.Ink3, width - 2 * Margin - 120, 4, 108, 24, true, TextAnchor.MiddleRight);

            // Следующие петли столбиков: рамка-строчка поверх клетки.
            _fronts = new Image[w];
            for (var x = 0; x < w; x++)
            {
                var size = _rh * 0.98f;
                var front = Ui.Image(_overlay, "Next", Art2.DashedFrame(size, size * 0.28f), Palette.WithAlpha(Palette.Ink, 0.6f));
                var frt = front.rectTransform;
                frt.anchorMin = frt.anchorMax = new Vector2(0f, 1f);
                frt.sizeDelta = new Vector2(size, size);
                front.enabled = false;
                _fronts[x] = front;

                // Рамка следующей петли тихо дышит: видно, куда ляжет петля, но не дёргает глаз.
                Tweens.Run(frt, 1.4f, t => frt.localScale = Vector3.one * (1f + 0.06f * Ease.Bump(t)), null, null, x * 0.09f, true);
            }

            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
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

            // Спица: поверх рамки (набалдашник выходит за край), живые петли — на ней.
            _needle = Ui.Node(Root, "Needle");
            _needle.anchorMin = _needle.anchorMax = new Vector2(0f, 1f);
            _needle.pivot = new Vector2(0f, 0.5f);
            _needle.sizeDelta = new Vector2(width, 1f);
            var thickness = Mathf.Max(9f, _rh * 0.62f);
            var rod = Ui.Image(_needle, "Rod", Art2.Needle, Color.white);
            rod.type = Image.Type.Sliced;
            rod.pixelsPerUnitMultiplier = 30f / thickness;
            Ui.PlaceCenter(rod.rectTransform, width / 2f + 2f, 0f, width - 4f, thickness);
            var knob = Ui.Image(_needle, "Knob", Art2.Knob, Color.white);
            Ui.PlaceCenter(knob.rectTransform, width + 1f, 0f, thickness * 1.15f, thickness * 1.15f);

            _loops = new Image[w];
            for (var x = 0; x < w; x++)
            {
                var loop = Ui.Image(_needle, "Loop", Art2.Loop(Color.white), Color.white);
                var lw = _cw * 1.12f;
                Ui.PlaceCenter(loop.rectTransform, Margin + _gridX + x * _cw + _cw / 2f, -lw * 0.06f, lw, lw * 60f / 64f);
                loop.enabled = false;
                _loops[x] = loop;
            }

            Root.gameObject.AddComponent<KnitBoardDriver>().Board = this;
            Refresh();
            _n = _nTarget;
            _ny = _nyTarget;
            Layout();
        }

        public RectTransform Root { get; }

        /// <summary>
        /// Какой высоты доска нужна картинке целиком (шапка, все ряды, зазор под спицу, поля), при ширине width.
        /// Если места больше — доска не растягивается: лишнее остаётся столу.
        /// </summary>
        public static float NeededHeight(LoomGame game, float width)
        {
            var gridW = width - 2 * Margin - LabelW - 8f;
            var cw = Mathf.Min(gridW / game.Width, 30f);
            var rh = cw * 0.82f;
            var gap = Mathf.Max(rh * 0.55f, 9f);
            return 2 * Margin + Header + game.Height * rh + gap + 14f;
        }

        /// <summary>Нижние углы доски (в мире, чуть внутрь по рамке): через них рабочие нити поднимаются по краю.</summary>
        public (Vector3 Left, Vector3 Right) Corners()
        {
            var corners = new Vector3[4];
            Root.GetWorldCorners(corners);
            var inset = (corners[2].x - corners[0].x) / _width * (Margin * 0.5f);
            return (new Vector3(corners[0].x + inset, corners[0].y, 0f), new Vector3(corners[2].x - inset, corners[0].y, 0f));
        }

        /// <summary>Ширина столбика в точках (под неё — размер моли и колец).</summary>
        public float CellSize => _cw;

        public bool TensionHolding { get; private set; }

        public RectTransform? TensionLine => TensionHolding ? _tension : null;

        /// <summary>Связано и показано на экране (без петель «в пути»).</summary>
        public int LandedCount => _game.KnittedCount - _pending.Count;

        public bool HasHoles => _holes.Count > 0;

        // ---------- Состояние клеток ----------

        public void Refresh()
        {
            _pending.Clear();
            RefreshColumns();
        }

        public void RefreshColumns()
        {
            for (var x = 0; x < _game.Width; x++)
            {
                RefreshColumn(x);
            }
        }

        public void RefreshColumn(int x)
        {
            var w = _game.Width;
            var front = _game.FrontY(x);
            var active = _game.ActiveFrontY(x) >= 0;
            var next = _game.AheadY(x, 1);
            for (var y = 0; y < _game.Height; y++)
            {
                var i = y * w + x;
                var color = _colors[_game.ColorAt(x, y)];
                var pending = _pending.Contains(i);
                var knitted = _game.IsFabric(x, y) || (_game.IsKnitted(x, y) && !pending);
                var stitch = _stitches[i];
                if (knitted != stitch.enabled)
                {
                    stitch.enabled = knitted;
                    stitch.rectTransform.localScale = Vector3.one;
                }

                if (_gold.TryGetValue(i, out var gold))
                {
                    gold.enabled = !_game.IsKnitted(x, y) || pending;
                }

                var square = _squares[i];
                square.enabled = !knitted;
                if (knitted)
                {
                    continue;
                }

                var paleness = pending || (y == front && active) ? 0f
                    : y == front || y == next ? 0.45f
                    : 0.62f;
                square.color = _game.Mystery && paleness > 0.5f
                    ? Palette.WithAlpha(Palette.Ink, 0.05f)
                    : Color.Lerp(color, PaperInk, paleness);
            }

            var mark = _fronts[x];
            mark.enabled = active && front >= 0;

            // Спица поднялась — живые петли переезжают: у столбиков ниже спицы их больше нет.
            var needle = _nTarget;
            Aim();
            if (Mathf.Abs(needle - _nTarget) > 0.01f)
            {
                for (var i = 0; i < _game.Width; i++)
                {
                    UpdateLoop(i);
                }
            }
            else
            {
                UpdateLoop(x);
            }

            UpdateTension();
            PlaceMarks();
        }

        /// <summary>Петля связана в модели, нитка ещё тянется — клетка «в пути».</summary>
        public void MarkPending(int x, int y)
        {
            _pending.Add(y * _game.Width + x);
            RefreshColumn(x);
        }

        /// <summary>
        /// Нить дошла: петля рождается у спицы и соскальзывает на место, вытягиваясь, с пружинкой
        /// на посадке; две петли ниже в столбике чуть подтягиваются. false — петли уже нет (отмена хода).
        /// </summary>
        public bool Land(int x, int y)
        {
            var i = y * _game.Width + x;
            if (!_pending.Remove(i))
            {
                return false;
            }

            RefreshColumn(x);
            if (!_game.IsKnitted(x, y))
            {
                return false;
            }

            var stitch = _stitches[i];
            var rt = stitch.rectTransform;
            var r = _game.Height - 1 - y;
            var drop = Mathf.Max(_rh * 0.6f, RowY(r) - (_ny + _gap / 2f));
            var rest = StitchRest(x);
            Tweens.Run(rt, LandTime, t =>
            {
                // Падение со спицы с лёгким перелётом, вытянутая на лету и сплюснутая на посадке.
                var fall = Ease.OutBack(t);
                rt.anchoredPosition = rest + new Vector2(0f, drop * (1f - fall));
                var stretch = t < 0.6f ? Mathf.Lerp(1.25f, 0.9f, t / 0.6f) : Mathf.Lerp(0.9f, 1f, (t - 0.6f) / 0.4f);
                var squeeze = t < 0.25f ? Mathf.Lerp(0.55f, 1f, t / 0.25f) : 1f;
                rt.localScale = new Vector3(squeeze * (2f - stretch) * 0.5f + 0.5f * squeeze, stretch * squeeze, 1f);
            }, null, () =>
            {
                rt.anchoredPosition = rest;
                rt.localScale = Vector3.one;
                TugBelow(x, r);
            });

            UpdateLoop(x);

            // Толчок спицы: петля снята — спица чуть кивает вниз и возвращается.
            _dipVelocity += Mathf.Max(18f, _rh * 1.1f);
            return true;
        }

        /// <summary>Петля село — ткань ниже в столбике чуть подтягивается и отпускает (на две петли вниз).</summary>
        private void TugBelow(int x, int r)
        {
            for (var k = 1; k <= 2; k++)
            {
                var below = r - k;
                if (below < 0)
                {
                    break;
                }

                var y = _game.Height - 1 - below;
                var image = _stitches[y * _game.Width + x];
                if (!image.enabled || _game.IsFabric(x, y))
                {
                    continue;
                }

                _tugs.Add(new Tug(image.rectTransform, StitchRest(x), Mathf.Max(1.2f, _rh * 0.07f) / k, 0.05f * (k - 1)));
            }
        }

        // ---------- Где что на экране ----------

        /// <summary>Центр клетки в мире (для искр, моли, подсказок).</summary>
        public Vector3 CellWorld(int x, int y) => _overlay.TransformPoint(CellLocal(x, _game.Height - 1 - y));

        /// <summary>Клетка как цель выноски: квадрат схемы, если не связана, иначе петля.</summary>
        public RectTransform CellRect(int x, int y)
        {
            var i = y * _game.Width + x;
            return _squares[i].enabled ? _squares[i].rectTransform : _stitches[i].rectTransform;
        }

        /// <summary>
        /// Куда тянется рабочая нить для петли (x, y): точка на спице над столбиком, а если столбик
        /// отстаёт — верх его клетки.
        /// </summary>
        public Vector3 KnitPoint(int x, int y)
        {
            var r = _game.Height - 1 - y;
            var cx = _gridX + x * _cw + _cw / 2f;
            var top = Mathf.Max(RowY(r) - _rh * 0.5f, _ny);
            return _overlay.TransformPoint(new Vector3(cx, -top, 0f));
        }

        /// <summary>Где петля стоит в своём ряду: нижний кончик — на низу клетки.</summary>
        private Vector2 StitchRest(int x) => new Vector2(_gridX + x * _cw + _cw / 2f, -_rh / 2f);

        private Vector3 CellLocal(int x, int r) => new Vector3(_gridX + x * _cw + _cw / 2f, -RowY(r), 0f);

        /// <summary>
        /// Центр ряда r (снизу, с нуля) от верха окна вниз. Спица — между рядами n−1 и n, вокруг неё
        /// зазор; при дробном n (спица едет вверх) зазор плавно переезжает на ряд выше.
        /// </summary>
        private float RowY(float r)
        {
            var side = Mathf.Clamp((_n - 0.5f - r) * 2f, -1f, 1f);
            return _ny + (_n - 1f - r) * _rh + _rh / 2f + _gap / 2f * side;
        }

        // ---------- Камера ----------

        /// <summary>Пересчитать, где должна стоять спица: над самым высоким фронтом, отстающий столбик в кадре.</summary>
        private void Aim()
        {
            var h = _game.Height;
            var top = 0;
            var lowest = h;
            for (var x = 0; x < _game.Width; x++)
            {
                var front = _game.FrontY(x);
                var r = front >= 0 ? h - 1 - front : h;
                top = Math.Max(top, r);
                lowest = Math.Min(lowest, r);
            }

            _nTarget = top;
            var gridBottom = _viewH - 3f;
            var content = h * _rh + _gap;
            var available = gridBottom - Header;
            float ny;
            if (content <= available)
            {
                // Картинка помещается: стоит по центру, спица поднимается ряд за рядом.
                var topEdge = Header + (available - content) / 2f;
                ny = topEdge + (h - top) * _rh + _gap / 2f;
            }
            else
            {
                // Высокая: полотно свисает со спицы, над ней три ряда схемы, снизу без пустоты.
                var anchored = gridBottom - top * _rh - _gap / 2f;
                var schemaVisible = Header + _gap / 2f + Mathf.Min(SchemaRows, h - top) * _rh;
                ny = Mathf.Max(anchored, schemaVisible);
            }

            // Самый отстающий столбик всегда в кадре.
            var lowestVisible = gridBottom - _gap / 2f - (top - lowest + 1) * _rh;
            ny = Mathf.Min(ny, lowestVisible);
            _nyTarget = Mathf.Max(ny, Header + _gap / 2f);

            var row = Math.Min(h, lowest + 1);
            if (row != _shownRow)
            {
                _shownRow = row;
                _rowText.text = lowest >= h ? "готово" : $"ряд {row} из {h}";
                Tweens.Punch(_rowText.rectTransform, 0.12f, 0.25f);
            }

            // Ряд закрыт целиком: по нему пробегает светлая волна.
            if (_lowestRow >= 0 && lowest > _lowestRow)
            {
                for (var r = _lowestRow; r < lowest && r < h; r++)
                {
                    Shimmer(r);
                }
            }

            _lowestRow = lowest;
        }

        private void Shimmer(int r)
        {
            var y = _game.Height - 1 - r;
            for (var x = 0; x < _game.Width; x++)
            {
                var image = _stitches[y * _game.Width + x];
                if (!image.enabled)
                {
                    continue;
                }

                var rt = image.rectTransform;
                Tweens.Run(rt, 0.26f, t => rt.localScale = Vector3.one * (1f + 0.07f * Ease.Bump(t)), null,
                    () => rt.localScale = Vector3.one, 0.12f + x * 0.025f);
            }
        }

        /// <summary>Кадр: камера мягко догоняет цель, ряды и всё, что на клетках, встают на места.</summary>
        internal void Tick(float dt)
        {
            // Пружина кивка спицы (недодемпфированная: лёгкий отскок, а не тряска).
            const float w = 30f;
            _dipVelocity += (-w * w * _dip - 2f * 0.55f * w * _dipVelocity) * dt;
            _dip = Mathf.Clamp(_dip + _dipVelocity * dt, -1f, 3.5f);
            if (_needle.gameObject.activeSelf && !_won)
            {
                _needle.anchoredPosition = new Vector2(0f, -(Margin + _ny + _dip));
            }

            var k = 1f - Mathf.Exp(-dt * 10f);
            var moved = Mathf.Abs(_n - _nTarget) > 0.001f || Mathf.Abs(_ny - _nyTarget) > 0.05f;
            if (moved)
            {
                _n = Mathf.Lerp(_n, _nTarget, k);
                _ny = Mathf.Lerp(_ny, _nyTarget, k);
                Layout();
            }

            for (var i = _tugs.Count - 1; i >= 0; i--)
            {
                var tug = _tugs[i];
                tug.Time += dt;
                var t = (tug.Time - tug.Delay) / 0.2f;
                if (t < 0f)
                {
                    _tugs[i] = tug;
                    continue;
                }

                if (t >= 1f || tug.Target == null)
                {
                    if (tug.Target != null)
                    {
                        tug.Target.anchoredPosition = tug.Rest;
                    }

                    _tugs.RemoveAt(i);
                    continue;
                }

                tug.Target.anchoredPosition = tug.Rest + new Vector2(0f, -tug.Amount * Ease.Bump(t));
                _tugs[i] = tug;
            }
        }

        private void Layout()
        {
            for (var r = 0; r < _game.Height; r++)
            {
                var y = -RowY(r);
                _schemaRows[r].anchoredPosition = new Vector2(0f, y);
                _fabricRows[r].anchoredPosition = new Vector2(0f, y);
                _labelRows[r].anchoredPosition = new Vector2(0f, y);

                // Номера рядов — только у схемы над спицей.
                var label = Mathf.Clamp01(r - _n + 1f);
                _labelRows[r].localScale = Vector3.one * Mathf.Max(0.001f, label);
            }

            _needle.anchoredPosition = new Vector2(0f, -(Margin + _ny + _dip));
            PlaceMarks();
        }

        /// <summary>Рамки следующих петель, золото, дырки и нить натяжения — по клеткам.</summary>
        private void PlaceMarks()
        {
            for (var x = 0; x < _game.Width; x++)
            {
                var mark = _fronts[x];
                if (mark.enabled)
                {
                    var front = _game.FrontY(x);
                    var p = CellLocal(x, _game.Height - 1 - front);
                    mark.rectTransform.anchoredPosition = new Vector2(p.x, p.y);
                }
            }

            foreach (var pair in _gold)
            {
                var y = pair.Key / _game.Width;
                var x = pair.Key % _game.Width;
                var p = CellLocal(x, _game.Height - 1 - y);
                pair.Value.rectTransform.anchoredPosition = new Vector2(p.x, p.y);
            }

            foreach (var (x, y, hole) in _holes)
            {
                var p = CellLocal(x, _game.Height - 1 - y);
                hole.rectTransform.anchoredPosition = new Vector2(p.x, p.y);
            }

            PlaceTension();
        }

        /// <summary>Слой на всё окно с началом координат в левом верхнем углу (клетки считаются от него).</summary>
        private static RectTransform Layer(RectTransform parent, string name)
        {
            var layer = Ui.Stretch(Ui.Node(parent, name));
            layer.pivot = new Vector2(0f, 1f);
            return layer;
        }

        private static RectTransform Row(RectTransform parent, string name)
        {
            var row = Ui.Node(parent, name);
            row.anchorMin = row.anchorMax = new Vector2(0f, 1f);
            row.pivot = new Vector2(0f, 1f);
            row.sizeDelta = new Vector2(1f, 1f);
            return row;
        }

        // ---------- Спица ----------

        /// <summary>
        /// Живая петля на спице у столбика, который до неё дошёл: цвета его верхней петли.
        /// Появляется с маленьким «накидом» — петля обвивает спицу.
        /// </summary>
        private void UpdateLoop(int x)
        {
            var h = _game.Height;
            var front = _game.FrontY(x);
            var r = front >= 0 ? h - 1 - front : h;
            var loop = _loops[x];
            var show = r > 0 && r >= Mathf.RoundToInt(_nTarget);
            Color color = default;
            if (show)
            {
                var y = h - r;
                show = y >= 0 && y < h && !_pending.Contains(y * _game.Width + x) && !_game.IsFabric(x, y);
                if (show)
                {
                    color = _colors[_game.ColorAt(x, y)];
                }
            }

            if (!show)
            {
                loop.enabled = false;
                return;
            }

            var sprite = Art2.Loop(color);
            if (!loop.enabled || loop.sprite != sprite)
            {
                loop.sprite = sprite;
                loop.enabled = true;
                Tweens.Scale(loop.rectTransform, 0.2f, 1f, 0.2f, Ease.OutBack);
            }
        }

        // ---------- Натяжение ----------

        private void BuildTension()
        {
            // Натянутая нить с двумя булавками: выше неё столбики ждут нижний ряд.
            _tension = Ui.Node(_overlay, "Tension");
            _tension.anchorMin = _tension.anchorMax = new Vector2(0f, 1f);
            _tension.pivot = new Vector2(0f, 0.5f);
            var width = _cw * _game.Width + 10f;
            _tension.sizeDelta = new Vector2(width, 8f);
            var thread = Ui.Box(_tension, "Thread", 1f, Palette.WithAlpha(Palette.Danger, 0.75f));
            Ui.Place(thread.rectTransform, 4f, 3f, width - 8f, 2f);
            foreach (var px in new[] { 4f, width - 4f })
            {
                var pin = Ui.Image(_tension, "Pin", Art.Circle, Palette.Danger);
                Ui.PlaceCenter(pin.rectTransform, px, 4f, 7f, 7f);
            }

            _tension.gameObject.SetActive(false);
        }

        private void UpdateTension()
        {
            if (_tension == null)
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
            PlaceTension();
        }

        private void PlaceTension()
        {
            if (_tension == null || !TensionHolding)
            {
                return;
            }

            // Нить над рядом limit: выше неё столбики ждут.
            var lowest = _game.LowestFront;
            var limitRow = _game.Height - 1 - (lowest - _game.Band);
            _tension.anchoredPosition = new Vector2(_gridX - 5f, -(RowY(limitRow) - _rh / 2f));
        }

        // ---------- Золото ----------

        public void ResolveGold(int x, int y, bool caught)
        {
            var key = y * _game.Width + x;
            if (!_gold.TryGetValue(key, out var ring))
            {
                return;
            }

            _gold.Remove(key);
            var rt = ring.rectTransform;
            Tweens.Run(rt, 0.35f, t =>
            {
                rt.localScale = Vector3.one * (caught ? 1f + 0.9f * t : 1f - 0.6f * t);
                ring.color = Palette.WithAlpha(caught ? Palette.Coin : Palette.Ink3, 1f - t);
            }, null, () => Object.Destroy(rt.gameObject));
        }

        public void RestoreGold(int x, int y)
        {
            if (_game.IsGold(x, y) && !_gold.ContainsKey(y * _game.Width + x))
            {
                AddGoldRing(x, y);
                Layout();
            }
        }

        private void AddGoldRing(int x, int y)
        {
            var ring = Ui.Image(_overlay, "Gold", Art.Ring, GoldColor(_goldLive));
            var rt = ring.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(_cw * 0.95f, _cw * 0.95f);
            Tweens.Run(rt, 1.2f, t => rt.localScale = Vector3.one * (_goldLive ? 1f + 0.14f * Ease.Bump(t) : 0.92f), null, null, (x + y) * 0.07f, true);
            _gold[y * _game.Width + x] = ring;
        }

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

        // ---------- Праздники ----------

        /// <summary>Цвет довязан: его петли по очереди снизу вверх «вздыхают». Возвращает центр цвета на экране.</summary>
        public Vector3? CelebrateColor(int color)
        {
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

                    var rt = _stitches[y * _game.Width + x].rectTransform;
                    var delay = (_game.Height - 1 - y) * 0.035f;
                    Tweens.Run(rt, 0.3f, t => rt.localScale = Vector3.one * (1f + 0.16f * Ease.Bump(t)), null,
                        () => rt.localScale = Vector3.one, delay);
                    sum += CellWorld(x, y);
                    count++;
                }
            }

            return count > 0 ? sum / count : (Vector3?)null;
        }

        /// <summary>
        /// Победа: спица уезжает вправо, все ряды встают под ней, полотно целиком отъезжает
        /// (уменьшается к центру окна), чтобы картинка была видна вся; потом снизу вверх проходит волна.
        /// </summary>
        public void Wave(Action done)
        {
            var h = _game.Height;
            var available = _viewH - 3f - Header;
            var fit = Mathf.Min(1f, available / (h * _rh + _gap));

            // Центр картинки после масштаба к центру окна должен встать в середину свободного места.
            var middle = Header + available / 2f;
            var centre = _viewH / 2f + (middle - _viewH / 2f) / fit;
            var endNy = centre - h * _rh / 2f - _gap / 2f;
            var startN = _n;
            var startNy = _ny;
            var needleFrom = _needle.anchoredPosition;
            _nTarget = h;
            _nyTarget = endNy;
            _won = true;
            Tweens.Run(Root, 0.5f, t =>
            {
                _n = Mathf.Lerp(startN, h, t);
                _ny = Mathf.Lerp(startNy, endNy, t);
                _content.localScale = Vector3.one * Mathf.Lerp(1f, fit, t);
                Layout();
                _needle.anchoredPosition = new Vector2(needleFrom.x + _width * 1.1f * t * t, needleFrom.y);
                foreach (var front in _fronts)
                {
                    front.enabled = false;
                }
            }, Ease.InOutCubic, () =>
            {
                _needle.gameObject.SetActive(false);
                for (var r = 0; r < h; r++)
                {
                    var y = h - 1 - r;
                    for (var x = 0; x < _game.Width; x++)
                    {
                        var rt = _stitches[y * _game.Width + x].rectTransform;
                        Tweens.Run(rt, 0.32f, t => rt.localScale = Vector3.one * (1f + 0.22f * Ease.Bump(t)), null,
                            () => rt.localScale = Vector3.one, r * 0.045f);
                    }
                }

                Tweens.Delay(Root, h * 0.045f + 0.35f, done);
            });
        }

        // ---------- Моль ----------

        public void ShowHole(int x, int y)
        {
            var hole = Ui.Image(_overlay, "Hole", MothArt.Hole, Color.white);
            var rt = hole.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(_cw * 0.9f, _cw * 0.9f);
            rt.localRotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 360f));
            _holes.Add((x, y, hole));
            Layout();
            Tweens.Run(rt, 0.28f, t => rt.localScale = Vector3.one * (t < 0.6f ? Mathf.Lerp(0.2f, 1.12f, t / 0.6f) : Mathf.Lerp(1.12f, 1f, (t - 0.6f) / 0.4f)));
        }

        public void MendHoles(RectTransform fx)
        {
            var i = 0;
            foreach (var (x, y, hole) in _holes)
            {
                var rt = hole.rectTransform;
                var color = _colors[_game.ColorAt(x, y)];
                var turn = rt.localRotation.eulerAngles.z;
                var at = CellWorld(x, y);
                var stitch = _stitches[y * _game.Width + x].rectTransform;
                Tweens.Run(rt, 0.35f, t =>
                {
                    rt.localScale = Vector3.one * (1f - t);
                    rt.localRotation = Quaternion.Euler(0f, 0f, turn + 120f * t);
                }, Ease.InOutCubic, () =>
                {
                    Object.Destroy(hole.gameObject);
                    Fx.Sparkle(fx, at, color, 5, _cw);
                    Tweens.Run(stitch, 0.3f, t => stitch.localScale = Vector3.one * (1f + 0.2f * Ease.Bump(t)), null,
                        () => stitch.localScale = Vector3.one);
                }, 0.12f * i++);
            }

            _holes.Clear();
        }

        private struct Tug
        {
            public Tug(RectTransform target, Vector2 rest, float amount, float delay)
            {
                Target = target;
                Rest = rest;
                Amount = amount;
                Delay = delay;
                Time = 0f;
            }

            public RectTransform Target;
            public Vector2 Rest;
            public float Amount;
            public float Delay;
            public float Time;
        }
    }

    /// <summary>Кадры доски: камера и подтягивание ткани идут в реальном времени, независимо от паузы.</summary>
    public sealed class KnitBoardDriver : MonoBehaviour
    {
        public KnitBoard? Board;

        private void Update()
        {
            Board?.Tick(Mathf.Min(Time.unscaledDeltaTime, 0.05f));
        }
    }
}
