#nullable enable

using System.Collections.Generic;
using System.Linq;
using KnittedPicture.Core;
using UnityEngine;
using UnityEngine.UI;

namespace KnittedPicture.Game
{
    /// <summary>
    /// Экран уровня: картинка сверху, станок посередине, поле клубков и бустеры внизу.
    /// Время идёт непрерывно: станок вяжет шагами, клубки работают параллельно, а тапать
    /// можно в любой момент — быстрые и точные цепочки и есть удовольствие от игры.
    /// </summary>
    public sealed class GameScreen : ScreenView
    {
        private const float Pad = 16f;
        private const int VisibleRows = 3;

        /// <summary>Секунд на шаг вязки: каждый клубок делает ~15 петель в секунду.</summary>
        private const float StepInterval = 0.065f;

        /// <summary>Полёт нитки от клубка к петле.</summary>
        private const float BeadFlight = 0.2f;

        private readonly LevelData _level;
        private readonly LoomGame _game;
        private readonly Color[] _colors;
        private readonly List<FieldSpool> _field = new List<FieldSpool>();
        private readonly Dictionary<BoosterType, BoosterButton> _boosters = new Dictionary<BoosterType, BoosterButton>();
        private readonly HashSet<int> _arriving = new HashSet<int>();

        private PictureView _picture = null!;
        private RectTransform _pictureCard = null!;
        private RectTransform _progressFill = null!;
        private Text _progressText = null!;
        private float _progressWidth;

        private RectTransform _loomBg = null!;
        private Image _loomAlert = null!;
        private Text _loomLabel = null!;
        private RectTransform[] _slotFrames = new RectTransform[0];
        private Image[] _slotOutlines = new Image[0];
        private SlotSpool?[] _slotViews = new SlotSpool?[0];
        private float _slotSpool;

        private RectTransform _fieldContent = null!;
        private RectTransform _fx = null!;
        private Text _coins = null!;

        private RectTransform? _timerMoth;
        private Text? _timerText;
        private float _timerX;
        private float _timerW;

        private float _top;
        private float _loomY;
        private float _fieldY;
        private float _fieldH;
        private float _colW;
        private float _spoolSize;

        private float _stepClock;
        private float _hitStop;
        private float _alertClock;
        private bool _ended;
        private bool _failShown;
        private bool _scissors;
        private float _idle;
        private int _combo;
        private int _comboCoins;
        private int _boostersUsed;
        private int _rescues;
        private RectTransform? _hint;
        private RectTransform? _hintHand;
        private RectTransform? _tip;
        private RectTransform? _scissorsBanner;

        public GameScreen(App app, LevelData level, LoomGame game)
            : base(app, "Game")
        {
            _level = level;
            _game = game;
            _colors = level.colors.Select(Palette.Hex).ToArray();

            BuildHud();
            if (_game.TimeLimit > 0)
            {
                BuildTimer();
            }

            ComputeLayout();
            BuildPicture();
            BuildLoom();
            BuildField();
            BuildBoosters();
            _fx = Ui.Stretch(Ui.Node(Root, "Fx"));
            RefreshAll();
            ShowIntro();
        }

        public override void Tick(float dt)
        {
            if (_ended || _failShown)
            {
                return;
            }

            if (_hitStop > 0)
            {
                _hitStop -= dt;
                return;
            }

            if (_game.TimeLimit > 0)
            {
                _game.Tick(dt);
                RefreshTimer();
                if (_game.Status == GameStatus.TimeUp)
                {
                    OnTimeUp();
                    return;
                }
            }

            if (_game.Status == GameStatus.Playing)
            {
                _stepClock += dt;
                var guard = 0;
                while (_stepClock >= StepInterval && guard++ < 4)
                {
                    _stepClock -= StepInterval;
                    var step = _game.Step();
                    Present(step);
                    if (step.Status != GameStatus.Playing)
                    {
                        AfterStatus(step.Status);
                        return;
                    }

                    if (!step.Progress)
                    {
                        _stepClock = 0;
                        break;
                    }
                }
            }

            UpdateLoomAlert(dt);

            if (!_game.IsBusy && _hint == null && _game.Status == GameStatus.Playing)
            {
                _idle += dt;
                var threshold = _level.id <= 3 ? 1.5f : _level.id <= 5 ? 8f : -1f;
                if (threshold > 0 && _idle > threshold)
                {
                    ShowHint();
                }
            }
        }

        public override bool OnBack()
        {
            if (_scissors)
            {
                SetScissors(false);
            }
            else if (!_ended)
            {
                App.ShowPopup(new PausePopup(App, Restart));
            }

            return true;
        }

        // ---------- Построение ----------

        private void BuildHud()
        {
            Ui.RoundButton(Root, Icon.Pause, Pad, 8).OnClick(() =>
            {
                if (!_ended)
                {
                    App.ShowPopup(new PausePopup(App, Restart));
                }
            });

            var text = _level.hard ? $"Уровень {_level.id} · сложный" : $"Уровень {_level.id}";
            var width = _level.hard ? 200f : 140f;
            var pill = Ui.Card(Root, "Level", W / 2f - width / 2f, 8, width, 44, 22,
                _level.hard ? Palette.DangerSoft : Palette.Card, Palette.CardShadow, _level.hard ? 0f : 2f);
            Ui.Label(pill, text, 17, _level.hard ? Palette.Danger : Palette.Ink, 0, 0, width, 44);
            _coins = Ui.CoinPill(Root, App.Progress.Coins, W - Pad, 8);
        }

        private void BuildTimer()
        {
            const float y = 60f;
            _timerX = 44f;
            _timerW = W - 44f - 74f;
            var yarn = Ui.Image(Root, "Target", Art.YarnBall, _colors[0]);
            Ui.PlaceCenter(yarn.rectTransform, 26, y + 13, 24, 24);
            var track = Ui.Box(Root, "TimerTrack", 5, Palette.Track);
            Ui.Place(track.rectTransform, _timerX, y + 8, _timerW, 10);
            _timerMoth = Ui.IconImage(Root, Icon.Moth, Palette.Ink, _timerX + _timerW, y + 13, 28).rectTransform;
            _timerText = Ui.Label(Root, "", 16, Palette.Ink, W - Pad - 56, y, 56, 26, true, TextAnchor.MiddleRight);
            RefreshTimer();
        }

        private void ComputeLayout()
        {
            _top = _game.TimeLimit > 0 ? 96f : 62f;
            var boostersY = H - 10f - 60f;
            _colW = (W - 2 * Pad - 16f) / _game.ColumnCount;
            _spoolSize = Mathf.Min(52f, _colW - 12f);

            for (var attempt = 0; attempt < 4; attempt++)
            {
                _fieldH = VisibleRows * (_spoolSize + 10f) + 14f;
                _fieldY = boostersY - 12f - _fieldH;
                _loomY = _fieldY - 12f - 96f;
                if (_loomY - 10f - _top >= 170f || _spoolSize <= 40f)
                {
                    break;
                }

                _spoolSize -= 4f;
            }
        }

        private void BuildPicture()
        {
            var availH = _loomY - 10f - _top;
            var step = Mathf.Min((W - 2 * Pad - 24f) / _game.Width, (availH - 24f - 26f) / _game.Height, 30f);
            var gap = Mathf.Max(1f, Mathf.Round(step * 0.1f));
            var cell = step - gap;
            var gridW = _game.Width * step - gap;
            var gridH = _game.Height * step - gap;
            var cardW = gridW + 24f;
            var cardH = gridH + 24f + 26f;
            var y = _top + Mathf.Max(0f, (availH - cardH) / 2f);

            _pictureCard = Ui.Card(Root, "PictureCard", (W - cardW) / 2f, y, cardW, cardH, 22, Palette.Card, Palette.CardShadow);
            _picture = new PictureView(_pictureCard, _game, _colors, cell, gap);
            Ui.Place(_picture.Root, 12, 12, gridW, gridH);

            _progressWidth = cardW - 24f - 48f;
            var bar = Ui.Box(_pictureCard, "ProgressTrack", 4, Palette.Track);
            Ui.Place(bar.rectTransform, 12, 12 + gridH + 13, _progressWidth, 8);
            _progressFill = Ui.Box(bar.rectTransform, "Fill", 4, Palette.Primary).rectTransform;
            Ui.Place(_progressFill, 0, 0, 8, 8);
            _progressText = Ui.Label(_pictureCard, "0%", 13, Palette.Ink2, cardW - 12 - 44, 12 + gridH + 5, 44, 24, true, TextAnchor.MiddleRight);
        }

        private void BuildLoom()
        {
            Ui.Label(Root, "Станок", 13, Palette.Ink2, Pad + 6, _loomY, 120, 20, true, TextAnchor.MiddleLeft);
            _loomLabel = Ui.Label(Root, "", 13, Palette.Ink2, W - Pad - 186, _loomY, 180, 20, true, TextAnchor.MiddleRight);
            _loomBg = Ui.Box(Root, "Loom", 20, Palette.LoomBg).rectTransform;
            Ui.Place(_loomBg, Pad, _loomY + 22f, W - 2 * Pad, 74);
            _loomAlert = Ui.Image(Root, "LoomAlert", Art.RoundRectOutline(20, 3), Color.clear);
            _loomAlert.type = Image.Type.Sliced;
            _loomAlert.pixelsPerUnitMultiplier = Art.Density;
            Ui.Place(_loomAlert.rectTransform, Pad - 2, _loomY + 20f, W - 2 * Pad + 4, 78);
            RebuildSlots();
        }

        private void RebuildSlots()
        {
            Ui.Clear(_loomBg);
            var n = _game.Slots.Count;
            const float gap = 6f;
            var slotW = (W - 2 * Pad - 16f - gap * (n - 1)) / n;
            _slotSpool = Mathf.Min(44f, slotW - 10f);
            _slotFrames = new RectTransform[n];
            _slotOutlines = new Image[n];
            _slotViews = new SlotSpool?[n];
            for (var i = 0; i < n; i++)
            {
                var frame = Ui.Box(_loomBg, "Slot" + i, 14, Palette.Card);
                Ui.Place(frame.rectTransform, 8 + i * (slotW + gap), 8, slotW, 58);
                var outline = Ui.Image(frame.rectTransform, "Outline", Art.RoundRectOutline(14, 2), Palette.SlotEmpty);
                outline.type = Image.Type.Sliced;
                outline.pixelsPerUnitMultiplier = Art.Density;
                Ui.Stretch(outline.rectTransform);
                _slotFrames[i] = frame.rectTransform;
                _slotOutlines[i] = outline;
            }
        }

        private void BuildField()
        {
            var card = Ui.Card(Root, "Field", Pad, _fieldY, W - 2 * Pad, _fieldH, 24, Palette.Card, Palette.CardShadow);
            _fieldContent = Ui.Stretch(Ui.Node(card, "Content"));
        }

        private void BuildBoosters()
        {
            var types = new[] { BoosterType.Scissors, BoosterType.Undo, BoosterType.Slot };
            const float gap = 10f;
            var w = (W - 2 * Pad - gap * 2) / 3f;
            var y = H - 10f - 60f;
            for (var i = 0; i < types.Length; i++)
            {
                var type = types[i];
                var button = new BoosterButton(Root, type, Pad + i * (w + gap), y, w);
                button.Tap.OnClick(() => OnBooster(type));
                _boosters[type] = button;
            }
        }

        private void ShowIntro()
        {
            if (!string.IsNullOrEmpty(_level.tip))
            {
                var width = Mathf.Min(W - 40f, 340f);
                _tip = Ui.Card(Root, "Tip", (W - width) / 2f, _loomY - 76f, width, 64, 18, Palette.Ink, Palette.Ink, 0f);
                var label = Ui.Label(_tip, _level.tip, 15, Color.white, 14, 0, width - 28, 64);
                label.lineSpacing = 0.9f;
                var group = _tip.gameObject.AddComponent<CanvasGroup>();
                group.blocksRaycasts = false;
                Tweens.Fade(group, 0f, 1f, 0.3f, null, 0.3f);
                var tip = _tip;
                Tweens.Delay(tip, 7f, HideTip);
            }

            if (_level.hard)
            {
                var banner = Ui.Card(Root, "Hard", W / 2f - 110f, _top + 40f, 220, 56, 28, Palette.Danger, Palette.Danger, 0f);
                Ui.Label(banner, "Сложный уровень", 20, Color.white, 0, 0, 220, 56);
                var group = banner.gameObject.AddComponent<CanvasGroup>();
                group.blocksRaycasts = false;
                Tweens.Scale(banner, 0.6f, 1f, 0.3f);
                Tweens.Fade(group, 1f, 0f, 0.4f, () => Object.Destroy(banner.gameObject), 1.6f);
            }
        }

        // ---------- Обновление ----------

        private void RefreshAll()
        {
            _picture.Refresh();
            if (_slotFrames.Length != _game.Slots.Count)
            {
                RebuildSlots();
            }

            _arriving.Clear();
            SyncLoom(true);
            RefreshField();
            RefreshBoosters();
            RefreshProgress();
            _coins.text = App.Progress.Coins.ToString();
        }

        private void RefreshProgress()
        {
            var fraction = (float)_picture.LandedCount / _game.TotalStitches;
            _progressFill.sizeDelta = new Vector2(Mathf.Max(8f, _progressWidth * fraction), 8f);
            _progressText.text = Mathf.FloorToInt(fraction * 100f) + "%";
        }

        /// <summary>Привести клубки на станке к модели: новые появляются, ушедшие исчезают, числа обновляются.</summary>
        private void SyncLoom(bool rebuild = false)
        {
            var used = 0;
            for (var i = 0; i < _slotFrames.Length; i++)
            {
                var spool = _game.Slots[i];
                var view = _slotViews[i];
                if (view != null && (rebuild || spool == null || view.SpoolId != spool.Id))
                {
                    Object.Destroy(view.View.Root.gameObject);
                    _slotViews[i] = null;
                    view = null;
                }

                if (spool != null)
                {
                    used++;
                }

                if (spool != null && view == null && !_arriving.Contains(spool.Id))
                {
                    var created = SpoolView.Create(_slotFrames[i], _slotSpool);
                    Ui.PlaceCenter(created.Root, _slotFrames[i].sizeDelta.x / 2f, 29f, _slotSpool, _slotSpool);
                    created.Bind(spool, _colors[spool.Color], true);
                    _slotViews[i] = new SlotSpool(created, spool.Id, spool.Color);
                    view = _slotViews[i];
                }

                if (view != null && spool != null)
                {
                    view.View.SetLoops(spool.Loops);
                }

                _slotOutlines[i].enabled = spool == null;
            }

            var free = _slotFrames.Length - used;
            _loomLabel.text = free == 1 && _slotFrames.Length > 1 ? "Осталось 1 место!" : $"{used} из {_slotFrames.Length}";
            _loomLabel.color = free <= 1 ? Palette.Danger : Palette.Ink2;
        }

        private void RefreshField()
        {
            Ui.Clear(_fieldContent);
            _field.Clear();
            _hint = null;
            _hintHand = null;
            var front = _game.FrontColors();
            for (var c = 0; c < _game.ColumnCount; c++)
            {
                var column = _game.Column(c);
                var shown = Mathf.Min(VisibleRows, column.Count);
                for (var d = 0; d < shown; d++)
                {
                    var spool = column[d];
                    var view = SpoolView.Create(_fieldContent, _spoolSize);
                    var (x, y) = FieldPosition(c, d);
                    Ui.PlaceCenter(view.Root, x, y, _spoolSize, _spoolSize);
                    view.Bind(spool, _colors[spool.Color], d == 0 || _scissors);
                    var tap = view.Tap;
                    tap.ClickSound = false;
                    var columnIndex = c;
                    var depth = d;
                    tap.OnClick(() => OnSpoolTap(columnIndex, depth));
                    _field.Add(new FieldSpool(view, spool.Id, c, d));

                    // Передний клубок, которому сразу есть работа, слегка «дышит».
                    if (d == 0 && spool.IsColorVisible && front.Contains(spool.Color) && !spool.Locked)
                    {
                        var root = view.Root;
                        Tweens.Run(root, 1.4f, t => root.localScale = Vector3.one * (1f + 0.04f * Ease.Bump(t)), null, null, c * 0.12f, true);
                    }
                }

                if (column.Count > VisibleRows)
                {
                    var (x, y) = FieldPosition(c, VisibleRows - 1);
                    var more = Ui.Card(_fieldContent, "More", x - 18f, y + _spoolSize / 2f - 10f, 36, 20, 10, Palette.Ink, Palette.Ink, 0f);
                    Ui.Label(more, "+" + (column.Count - VisibleRows), 12, Color.white, 0, 0, 36, 20);
                }
            }
        }

        private (float X, float Y) FieldPosition(int column, int depth)
        {
            var rowH = _spoolSize + 10f;
            return (8f + _colW * (column + 0.5f), 7f + rowH * depth + rowH / 2f);
        }

        private void RefreshBoosters()
        {
            foreach (var pair in _boosters)
            {
                var type = pair.Key;
                var unlocked = App.Progress.IsUnlocked(type, _level.id);
                var available = type == BoosterType.Undo ? _game.CanUndo
                    : type == BoosterType.Slot ? _game.Slots.Count < LoomGame.MaxSlots
                    : true;
                pair.Value.Refresh(unlocked, App.Progress.Stock(type), Economy.Price(type), Economy.UnlockLevel(type),
                    available, type == BoosterType.Scissors && _scissors);
            }
        }

        private void RefreshTimer()
        {
            if (_timerMoth == null || _timerText == null)
            {
                return;
            }

            var left = Mathf.Max(0f, _game.TimeLeft);
            var fraction = _game.TimeLimit > 0 ? left / _game.TimeLimit : 0f;
            _timerMoth.anchoredPosition = new Vector2(_timerX + _timerW * fraction, _timerMoth.anchoredPosition.y);
            var seconds = Mathf.CeilToInt(left);
            _timerText.text = $"{seconds / 60}:{seconds % 60:00}";
            _timerText.color = fraction < 0.25f ? Palette.Danger : Palette.Ink;
        }

        /// <summary>Рамка станка: оранжевая, когда осталось одно место, красная — когда мест нет.</summary>
        private void UpdateLoomAlert(float dt)
        {
            _alertClock += dt;
            var free = _game.FreeSlots;
            if (free >= 2 || _slotFrames.Length <= 1)
            {
                _loomAlert.color = Color.clear;
                return;
            }

            var color = free == 1 ? Palette.Hex("#FF9A3C") : Palette.Danger;
            var pulse = 0.45f + 0.55f * Ease.Bump(Mathf.Repeat(_alertClock / 0.9f, 1f));
            _loomAlert.color = Palette.WithAlpha(color, pulse);
        }

        // ---------- Вязка ----------

        private void Present(StepResult step)
        {
            var changed = new HashSet<int>();
            foreach (var stitch in step.Stitches)
            {
                changed.Add(stitch.X);
                _picture.MarkPending(stitch.X, stitch.Y);
                var slot = _slotViews[stitch.Slot];
                var from = slot != null ? Ui.WorldCenter(slot.View.Root) : Ui.WorldCenter(_slotFrames[stitch.Slot]);
                var to = _picture.CellWorld(stitch.X, stitch.Y);
                var color = _colors[stitch.Color];
                var x = stitch.X;
                var y = stitch.Y;
                Fx.Bead(_fx, from, to, color, Mathf.Max(8f, _slotSpool * 0.24f), BeadFlight, () =>
                {
                    if (_picture.Land(x, y))
                    {
                        Fx.Sparkle(_fx, to, color);
                        Sfx.Play(Sound.Stitch, 0.5f);
                    }

                    RefreshProgress();
                });

                if (slot != null)
                {
                    Wobble(slot.View.Root);
                }
            }

            foreach (var done in step.Completed)
            {
                var slot = _slotViews[done.Slot];
                if (slot != null)
                {
                    Fx.Poof(_fx, Ui.WorldCenter(slot.View.Root), _colors[slot.Color], _slotSpool * 1.3f);
                }

                Sfx.Play(Sound.SpoolDone, 0.7f);
                OnSpoolDone(done);
            }

            if (step.Stitches.Count > 0 || step.Completed.Count > 0)
            {
                SyncLoom();
            }

            foreach (var x in changed)
            {
                _picture.RefreshColumn(x);
            }

            if (step.Stitches.Count > 0)
            {
                _idle = 0;
            }
        }

        private void OnSpoolDone(Completion done)
        {
            if (!done.Perfect)
            {
                _combo = 0;
                return;
            }

            _combo++;
            if (_combo < 2)
            {
                return;
            }

            var at = Ui.WorldCenter(_loomBg) + new Vector3(0, 46f * _fx.lossyScale.y, 0);
            var text = _combo >= 5 ? $"Безупречно! ×{_combo}" : _combo >= 3 ? $"Отлично! ×{_combo}" : $"Комбо ×{_combo}";
            Fx.FloatText(_fx, at, text, _combo >= 3 ? Palette.Primary : Palette.Link, _combo >= 3 ? 24f : 20f);
            if (_combo >= Economy.ComboCoinsFrom)
            {
                _comboCoins++;
                Sfx.Play(Sound.Combo, 0.6f);
            }
        }

        private void Wobble(RectTransform root)
        {
            Tweens.Run(root, 0.12f, t =>
            {
                root.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * Mathf.PI * 2f) * 9f);
                root.localScale = Vector3.one * (1f + 0.06f * Ease.Bump(t));
            }, null, () =>
            {
                root.localRotation = Quaternion.identity;
                root.localScale = Vector3.one;
            });
        }

        // ---------- Ходы ----------

        private void OnSpoolTap(int column, int depth)
        {
            if (_ended || _failShown || App.HasPopups)
            {
                return;
            }

            _idle = 0;
            HideTip();
            HideHint();
            var result = _scissors ? _game.PullSpool(column, depth) : _game.TapColumn(column);
            if (!result.Accepted)
            {
                Deny(column, depth, result.Reason);
                return;
            }

            if (_scissors)
            {
                App.Progress.Pay(BoosterType.Scissors);
                App.Save();
                _boostersUsed++;
                _scissors = false;
                HideScissorsBanner();
            }

            if (result.Kind == MoveKind.KnotUntied)
            {
                Sfx.Play(Sound.Knot);
                RefreshField();
                RefreshBoosters();
                var view = FindFieldView(result.Knotted!.Id);
                if (view != null)
                {
                    Tweens.Punch(view.Root, 0.2f, 0.25f);
                    Fx.Sparkle(_fx, Ui.WorldCenter(view.Root), Palette.Danger, 6, 26f);
                }

                return;
            }

            Sfx.Play(Sound.Pick);
            var flights = new List<(Placement placed, Vector3 from)>();
            foreach (var placed in result.Placed)
            {
                var source = _field.FirstOrDefault(f => f.Column == placed.Column && f.Depth == placed.Depth);
                flights.Add((placed, source != null ? Ui.WorldCenter(source.View.Root) : Ui.WorldCenter(_fieldContent)));
                _arriving.Add(placed.Spool.Id);
            }

            RefreshField();
            RefreshBoosters();
            foreach (var id in result.Revealed)
            {
                PunchField(id);
            }

            if (result.Unlocked.Count > 0)
            {
                Sfx.Play(Sound.Unlock);
                foreach (var id in result.Unlocked)
                {
                    PunchField(id);
                }
            }

            foreach (var (placed, from) in flights)
            {
                var spool = placed.Spool;
                var flyer = SpoolView.Create(_fx, _spoolSize);
                flyer.Bind(spool, _colors[spool.Color], true);
                var target = Ui.WorldCenter(_slotFrames[placed.Slot]);
                var scale = _slotSpool / _spoolSize;
                Tweens.Run(flyer.Root, LoomGame.WarmupSteps * StepInterval, t =>
                {
                    flyer.Root.localScale = Vector3.one * Mathf.Lerp(1f, scale, t) * (1f + 0.15f * Ease.Bump(t));
                }, null, null);
                Tweens.Move(flyer.Root, from, target, LoomGame.WarmupSteps * StepInterval, Ease.OutCubic, () =>
                {
                    Object.Destroy(flyer.Root.gameObject);
                    _arriving.Remove(spool.Id);
                    SyncLoom();
                    var slot = _slotViews[placed.Slot];
                    if (slot != null && slot.SpoolId == spool.Id)
                    {
                        Tweens.Scale(slot.View.Root, 1.25f, 1f, 0.18f);
                    }
                });
            }

            SyncLoom();
        }

        private void Deny(int column, int depth, RejectReason reason)
        {
            Sfx.Play(Sound.Deny);
            var view = _field.FirstOrDefault(f => f.Column == column && f.Depth == depth);
            if (view != null)
            {
                Tweens.Shake(view.View.Root, 6f, 0.3f);
            }

            switch (reason)
            {
                case RejectReason.Locked:
                    App.Toast("Под замком: сначала поставь на станок клубок с ключом");
                    break;
                case RejectReason.LinkNotReady:
                    App.Toast("Связанный клубок ещё не впереди своей колонки");
                    break;
                case RejectReason.NoFreeSlot:
                    var front = _game.Column(column).Count > 0 ? _game.Column(column)[0] : null;
                    App.Toast(front != null && front.Link != 0 && !_scissors
                        ? "Связанным клубкам нужно 2 свободных слота"
                        : "На станке нет места — подожди, пока клубок довяжет");
                    Tweens.Shake(_loomBg, 5f, 0.25f);
                    break;
            }
        }

        // ---------- Победа и поражение ----------

        private void AfterStatus(GameStatus status)
        {
            switch (status)
            {
                case GameStatus.Won:
                    OnWon();
                    break;
                case GameStatus.Jammed:
                    OnJam();
                    break;
                case GameStatus.TimeUp:
                    OnTimeUp();
                    break;
            }
        }

        private void OnWon()
        {
            _ended = true;
            HideHint();
            HideTip();
            SetScissors(false);
            Tweens.Delay(Root, BeadFlight + 0.1f, () =>
            {
                _picture.Refresh();
                RefreshProgress();
                Sfx.Play(Sound.Unlock);
                _picture.Wave(() =>
                {
                    Tweens.Punch(_pictureCard, 0.06f, 0.4f);
                    Fx.Poof(_fx, Ui.WorldCenter(_pictureCard), Palette.Coin, _pictureCard.sizeDelta.x * 0.9f);
                    var stars = Economy.Stars(_boostersUsed, _rescues);
                    Tweens.Delay(Root, 0.45f, () => App.OnLevelWon(_level, stars, _comboCoins));
                });
            });
        }

        private void OnJam()
        {
            _failShown = true;
            HideHint();
            _combo = 0;
            _hitStop = 0.12f;
            Sfx.Play(Sound.Jam);
            Tweens.Shake(_loomBg, 10f, 0.4f);
            Tweens.Shake(Root, 4f, 0.25f);
            foreach (var outline in _slotOutlines)
            {
                outline.enabled = true;
                var image = outline;
                Tweens.Run(image, 0.5f, t => image.color = Color.Lerp(Palette.Danger, Palette.SlotEmpty, t));
            }

            Tweens.Delay(Root, 0.6f, () => ShowFail(FailKind.Jam));
        }

        private void OnTimeUp()
        {
            _failShown = true;
            HideHint();
            Sfx.Play(Sound.Jam);
            if (_timerMoth != null)
            {
                Tweens.Punch(_timerMoth, 0.4f, 0.4f);
            }

            Tweens.Delay(Root, 0.45f, () => ShowFail(FailKind.TimeUp));
        }

        private void ShowFail(FailKind kind)
        {
            var canRescue = kind == FailKind.TimeUp || _game.Slots.Count < LoomGame.MaxSlots;
            var percent = Mathf.FloorToInt(100f * _game.KnittedCount / _game.TotalStitches);
            App.ShowPopup(new FailPopup(App, kind, canRescue, percent, () => Rescue(kind), Restart));
        }

        private void Rescue(FailKind kind)
        {
            _rescues++;
            _failShown = false;
            if (kind == FailKind.Jam)
            {
                _game.AddSlot();
                RebuildSlots();
                RefreshAll();
                Sfx.Play(Sound.Unlock);
                Tweens.Scale(_slotFrames[_slotFrames.Length - 1], 0.4f, 1f, 0.3f);
            }
            else
            {
                _game.AddTime(Economy.TimeBonusSeconds);
                RefreshTimer();
                RefreshAll();
            }

            if (_game.Status != GameStatus.Playing)
            {
                AfterStatus(_game.Status);
            }
        }

        private void Restart()
        {
            App.StartLevel(_level.id, restart: true);
        }

        // ---------- Бустеры ----------

        private void OnBooster(BoosterType type)
        {
            if (_ended || _failShown)
            {
                return;
            }

            if (!App.Progress.IsUnlocked(type, _level.id))
            {
                App.Toast($"Откроется на уровне {Economy.UnlockLevel(type)}");
                return;
            }

            switch (type)
            {
                case BoosterType.Scissors:
                    if (_scissors)
                    {
                        SetScissors(false);
                    }
                    else if (App.Progress.CanAfford(type))
                    {
                        SetScissors(true);
                    }
                    else
                    {
                        NotEnough(type);
                    }

                    break;

                case BoosterType.Undo:
                    if (!_game.CanUndo)
                    {
                        App.Toast("Пока нечего отменять");
                    }
                    else if (App.Progress.Pay(type) == Payment.NotEnough)
                    {
                        NotEnough(type);
                    }
                    else
                    {
                        App.Save();
                        _boostersUsed++;
                        _game.Undo();
                        Sfx.Play(Sound.Pick);
                        RefreshAll();
                    }

                    break;

                case BoosterType.Slot:
                    if (_game.Slots.Count >= LoomGame.MaxSlots)
                    {
                        App.Toast($"На станке уже {LoomGame.MaxSlots} слотов");
                    }
                    else if (App.Progress.Pay(type) == Payment.NotEnough)
                    {
                        NotEnough(type);
                    }
                    else
                    {
                        App.Save();
                        _boostersUsed++;
                        _game.AddSlot();
                        RebuildSlots();
                        RefreshAll();
                        Sfx.Play(Sound.Unlock);
                        Tweens.Scale(_slotFrames[_slotFrames.Length - 1], 0.4f, 1f, 0.3f);
                    }

                    break;
            }

            _coins.text = App.Progress.Coins.ToString();
        }

        private void NotEnough(BoosterType type)
        {
            App.ShowPopup(new NotEnoughPopup(App, type, RefreshBoosters));
        }

        private void SetScissors(bool on)
        {
            if (_scissors == on)
            {
                return;
            }

            _scissors = on;
            HideHint();
            if (on)
            {
                HideTip();
                _scissorsBanner = Ui.Card(Root, "ScissorsBanner", Pad, _fieldY - 50f, W - 2 * Pad, 40, 20, Palette.Primary, Palette.Primary, 0f);
                Ui.IconImage(_scissorsBanner, Icon.Scissors, Color.white, 24, 20, 20);
                Ui.Label(_scissorsBanner, "Выбери любой клубок на поле", 15, Color.white, 40, 0, W - 2 * Pad - 52, 40, true, TextAnchor.MiddleLeft);
                var group = _scissorsBanner.gameObject.AddComponent<CanvasGroup>();
                group.blocksRaycasts = false;
            }
            else
            {
                HideScissorsBanner();
            }

            RefreshField();
            RefreshBoosters();
        }

        private void HideScissorsBanner()
        {
            if (_scissorsBanner != null)
            {
                Object.Destroy(_scissorsBanner.gameObject);
                _scissorsBanner = null;
            }
        }

        // ---------- Подсказки ----------

        private void ShowHint()
        {
            var column = LevelSolver.Hint(_game);
            if (column < 0)
            {
                return;
            }

            var (x, y) = FieldPosition(column, 0);
            var ring = Ui.Image(_fieldContent, "Hint", Art.Ring, Palette.Primary);
            var size = _spoolSize + 16f;
            Ui.PlaceCenter(ring.rectTransform, x, y, size, size);
            _hint = ring.rectTransform;
            var hint = _hint;
            Tweens.Run(hint, 0.9f, t => hint.localScale = Vector3.one * (1f + 0.12f * Ease.Bump(t)), null, null, 0f, true);

            // На первом уровне — рука, как во вступлении: тапни сюда.
            if (_level.id == 1)
            {
                var hand = Ui.Node(_fieldContent, "HintHand");
                Ui.PlaceCenter(hand, x + size * 0.35f, y + size * 0.45f, 52, 52);
                var halo = Ui.Image(hand, "Halo", Art.Icon(Icon.Hand), Color.white);
                Ui.PlaceCenter(halo.rectTransform, 26, 26, 60, 60);
                var icon = Ui.Image(hand, "Hand", Art.Icon(Icon.Hand), Palette.Ink);
                Ui.PlaceCenter(icon.rectTransform, 26, 26, 52, 52);
                var start = hand.anchoredPosition;
                Tweens.Run(hand, 0.9f, t =>
                {
                    hand.anchoredPosition = start + new Vector2(6f, -6f) * Ease.Bump(t);
                    hand.localScale = Vector3.one * (1f - 0.1f * Ease.Bump(t));
                }, null, null, 0f, true);
                _hintHand = hand;
            }
        }

        private void HideHint()
        {
            if (_hint != null)
            {
                Object.Destroy(_hint.gameObject);
                _hint = null;
            }

            if (_hintHand != null)
            {
                Object.Destroy(_hintHand.gameObject);
                _hintHand = null;
            }

            _idle = 0;
        }

        private void HideTip()
        {
            if (_tip == null)
            {
                return;
            }

            var tip = _tip;
            _tip = null;
            var group = tip.GetComponent<CanvasGroup>();
            Tweens.Fade(group, group.alpha, 0f, 0.2f, () => Object.Destroy(tip.gameObject));
        }

        private void PunchField(int spoolId)
        {
            var view = FindFieldView(spoolId);
            if (view != null)
            {
                Tweens.Scale(view.Root, 0.6f, 1f, 0.3f);
            }
        }

        private SpoolView? FindFieldView(int spoolId) => _field.FirstOrDefault(f => f.SpoolId == spoolId)?.View;

        private sealed class SlotSpool
        {
            public SlotSpool(SpoolView view, int spoolId, int color)
            {
                View = view;
                SpoolId = spoolId;
                Color = color;
            }

            public SpoolView View { get; }

            public int SpoolId { get; }

            public int Color { get; }
        }

        private sealed class FieldSpool
        {
            public FieldSpool(SpoolView view, int spoolId, int column, int depth)
            {
                View = view;
                SpoolId = spoolId;
                Column = column;
                Depth = depth;
            }

            public SpoolView View { get; }

            public int SpoolId { get; }

            public int Column { get; }

            public int Depth { get; }
        }

        /// <summary>Кнопка бустера: иконка, подпись и бейдж — запас, цена или «ур. N».</summary>
        private sealed class BoosterButton
        {
            private readonly Image _face;
            private readonly CanvasGroup _group;
            private readonly Image _badgeBack;
            private readonly Text _badgeText;

            public BoosterButton(RectTransform parent, BoosterType type, float x, float y, float w)
            {
                var root = Ui.Card(parent, "Booster" + type, x, y, w, 60, 18, Palette.Card, Palette.CardShadow, 3f, true);
                _face = root.Find("Face").GetComponent<Image>();
                _group = root.gameObject.AddComponent<CanvasGroup>();
                var label = Ui.Label(root, type == BoosterType.Slot ? "Слот" : BoosterInfo.Name(type), 14, Palette.Ink);
                var textW = label.preferredWidth;
                var total = 22f + 6f + textW;
                Ui.IconImage(root, BoosterInfo.Icon(type), Palette.Ink, w / 2f - total / 2f + 11f, 30, 22);
                Ui.Place(label.rectTransform, w / 2f - total / 2f + 28f, 0, textW + 4f, 60);
                label.alignment = TextAnchor.MiddleLeft;

                var badge = Ui.Place(Ui.Node(root, "Badge"), w - 40f, -8f, 46, 22);
                _badgeBack = Ui.Box(badge, "Back", 11, Palette.Ink);
                Ui.Stretch(_badgeBack.rectTransform);
                _badgeText = Ui.Label(badge, "", 12, Color.white);
                Ui.Stretch(_badgeText.rectTransform);
                Tap = root.gameObject.AddComponent<Tap>();
            }

            public Tap Tap { get; }

            public void Refresh(bool unlocked, int stock, int price, int unlockLevel, bool available, bool active)
            {
                _face.color = active ? Palette.PrimarySoft : Palette.Card;
                if (!unlocked)
                {
                    _group.alpha = 0.55f;
                    _badgeBack.color = Palette.Track;
                    _badgeText.color = Palette.Ink2;
                    _badgeText.text = "ур. " + unlockLevel;
                    return;
                }

                _group.alpha = available ? 1f : 0.55f;
                if (stock > 0)
                {
                    _badgeBack.color = Palette.Ink;
                    _badgeText.color = Color.white;
                    _badgeText.text = stock.ToString();
                }
                else
                {
                    _badgeBack.color = Palette.Coin;
                    _badgeText.color = Palette.Ink;
                    _badgeText.text = price.ToString();
                }
            }
        }
    }
}
