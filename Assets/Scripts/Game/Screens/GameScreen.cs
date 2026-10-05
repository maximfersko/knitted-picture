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
    /// Модель (LoomGame) меняется мгновенно, экран догоняет её анимациями;
    /// пока анимация идёт, новые тапы не принимаются.
    /// </summary>
    public sealed class GameScreen : ScreenView
    {
        private const float Pad = 16f;
        private const int VisibleRows = 3;

        private readonly LevelData _level;
        private readonly LoomGame _game;
        private readonly Color[] _colors;
        private readonly List<FieldSpool> _field = new List<FieldSpool>();
        private readonly Dictionary<BoosterType, BoosterButton> _boosters = new Dictionary<BoosterType, BoosterButton>();

        private PictureView _picture = null!;
        private RectTransform _pictureCard = null!;
        private RectTransform _progressFill = null!;
        private Text _progressText = null!;
        private float _progressWidth;

        private RectTransform _loomBg = null!;
        private Text _loomLabel = null!;
        private RectTransform[] _slotFrames = new RectTransform[0];
        private Image[] _slotOutlines = new Image[0];
        private SpoolView?[] _slotViews = new SpoolView?[0];
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

        private int _busy;
        private bool _ended;
        private bool _scissors;
        private bool _timeUpShown;
        private float _idle;
        private RectTransform? _hint;
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
            if (_ended)
            {
                return;
            }

            if (_game.TimeLimit > 0)
            {
                _game.Tick(dt);
                RefreshTimer();
                if (_game.Status == GameStatus.TimeUp && _busy == 0 && !_timeUpShown)
                {
                    _timeUpShown = true;
                    OnTimeUp();
                    return;
                }
            }

            if (_busy == 0 && _game.Status == GameStatus.Playing && _hint == null)
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
            _loomLabel = Ui.Label(Root, "", 13, Palette.Ink2, W - Pad - 126, _loomY, 120, 20, true, TextAnchor.MiddleRight);
            _loomBg = Ui.Box(Root, "Loom", 20, Palette.LoomBg).rectTransform;
            Ui.Place(_loomBg, Pad, _loomY + 22f, W - 2 * Pad, 74);
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
            _slotViews = new SpoolView?[n];
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

            RefreshLoom(null);
            RefreshField();
            RefreshBoosters();
            RefreshProgress(_game.KnittedCount);
            _coins.text = App.Progress.Coins.ToString();
        }

        private void RefreshProgress(int knitted)
        {
            var fraction = (float)knitted / _game.TotalStitches;
            _progressFill.sizeDelta = new Vector2(Mathf.Max(8f, _progressWidth * fraction), 8f);
            _progressText.text = Mathf.FloorToInt(fraction * 100f) + "%";
        }

        /// <summary>Перерисовать станок. display — временное состояние во время анимации вязания.</summary>
        private void RefreshLoom(SlotEntry?[]? display)
        {
            var used = 0;
            for (var i = 0; i < _slotFrames.Length; i++)
            {
                if (_slotViews[i] != null)
                {
                    Object.Destroy(_slotViews[i]!.Root.gameObject);
                    _slotViews[i] = null;
                }

                var entry = display != null ? display[i] : _game.Slots[i] != null ? new SlotEntry(_game.Slots[i]!, _game.Slots[i]!.Loops) : null;
                _slotOutlines[i].enabled = entry == null;
                if (entry == null)
                {
                    continue;
                }

                used++;
                var view = SpoolView.Create(_slotFrames[i], _slotSpool);
                Ui.PlaceCenter(view.Root, _slotFrames[i].sizeDelta.x / 2f, 29f, _slotSpool, _slotSpool);
                view.Bind(entry.Spool, _colors[entry.Spool.Color], true);
                view.SetLoops(entry.Loops);
                _slotViews[i] = view;
            }

            _loomLabel.text = $"{used} из {_slotFrames.Length}";
        }

        private void RefreshField()
        {
            Ui.Clear(_fieldContent);
            _field.Clear();
            _hint = null;
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

        // ---------- Ходы ----------

        private void OnSpoolTap(int column, int depth)
        {
            if (_busy > 0 || _ended || App.HasPopups)
            {
                return;
            }

            _idle = 0;
            HideTip();
            var before = SnapshotSlots();
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
                }

                AfterMove(result.Status);
                return;
            }

            AnimatePlacement(result, before);
        }

        private void AnimatePlacement(MoveResult result, SlotEntry?[] before)
        {
            _busy++;
            Sfx.Play(Sound.Pick);

            var display = (SlotEntry?[])before.Clone();
            var flyers = new List<(SpoolView view, Vector3 from, Vector3 to)>();
            foreach (var placed in result.Placed)
            {
                var loops = placed.Spool.Loops + result.Knitted.Count(s => s.Slot == placed.Slot);
                display[placed.Slot] = new SlotEntry(placed.Spool, loops);

                var source = _field.FirstOrDefault(f => f.Column == placed.Column && f.Depth == placed.Depth);
                var from = source != null ? Ui.WorldCenter(source.View.Root) : Ui.WorldCenter(_fieldContent);
                var flyer = SpoolView.Create(_fx, _spoolSize);
                flyer.Bind(placed.Spool, _colors[placed.Spool.Color], true);
                flyer.SetLoops(loops);
                flyers.Add((flyer, from, Ui.WorldCenter(_slotFrames[placed.Slot])));
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

            var landed = 0;
            foreach (var (view, from, to) in flyers)
            {
                var flyer = view;
                Tweens.Move(flyer.Root, from, to, 0.24f, Ease.OutCubic, () =>
                {
                    Object.Destroy(flyer.Root.gameObject);
                    landed++;
                    if (landed == flyers.Count)
                    {
                        RefreshLoom(display);
                        KnitSequence(result, display);
                    }
                });
                Tweens.Run(flyer.Root, 0.24f, t => flyer.Root.localScale = Vector3.one * Mathf.Lerp(1f, _slotSpool / _spoolSize, t));
            }
        }

        private void KnitSequence(MoveResult result, SlotEntry?[] display)
        {
            var stitches = result.Knitted;
            var startKnitted = _game.KnittedCount - stitches.Count;
            var interval = stitches.Count == 0 ? 0f : Mathf.Min(0.045f, 0.75f / stitches.Count);
            for (var i = 0; i < stitches.Count; i++)
            {
                var stitch = stitches[i];
                var index = i;
                Tweens.Delay(Root, i * interval, () =>
                {
                    _picture.Knit(stitch.X, stitch.Y);
                    var next = startKnitted + index + 1;
                    if (next < _game.TotalStitches)
                    {
                        _picture.ShowCursorAt(next % _game.Width, _game.Height - 1 - next / _game.Width);
                    }
                    else
                    {
                        _picture.HideCursor();
                    }

                    RefreshProgress(next);
                    Sfx.Play(Sound.Stitch, 0.6f);

                    var entry = display[stitch.Slot];
                    if (entry == null)
                    {
                        return;
                    }

                    entry.Loops--;
                    var view = _slotViews[stitch.Slot];
                    view?.SetLoops(entry.Loops);
                    if (entry.Loops <= 0 && view != null)
                    {
                        display[stitch.Slot] = null;
                        _slotViews[stitch.Slot] = null;
                        _slotOutlines[stitch.Slot].enabled = true;
                        view.HideBadges();
                        Sfx.Play(Sound.SpoolDone, 0.7f);
                        Tweens.Scale(view.Root, 1f, 0f, 0.18f, Ease.OutCubic, () => Object.Destroy(view.Root.gameObject));
                    }
                });
            }

            Tweens.Delay(Root, stitches.Count * interval + 0.15f, () =>
            {
                _busy--;
                RefreshAll();
                AfterMove(result.Status);
            });
        }

        private void AfterMove(GameStatus status)
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
                    if (!_timeUpShown)
                    {
                        _timeUpShown = true;
                        OnTimeUp();
                    }

                    break;
            }
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
                        : "На станке нет свободного слота");
                    break;
            }
        }

        // ---------- Победа и поражение ----------

        private void OnWon()
        {
            _ended = true;
            HideHint();
            HideTip();
            _picture.HideCursor();
            Tweens.Punch(_pictureCard, 0.06f, 0.45f);
            Tweens.Delay(Root, 0.9f, () => App.OnLevelWon(_level));
        }

        private void OnJam()
        {
            _busy++;
            HideHint();
            Sfx.Play(Sound.Jam);
            Tweens.Shake(_loomBg, 10f, 0.4f);
            Tweens.Delay(Root, 0.55f, () =>
            {
                _busy--;
                ShowFail(FailKind.Jam);
            });
        }

        private void OnTimeUp()
        {
            HideHint();
            Sfx.Play(Sound.Jam);
            if (_timerMoth != null)
            {
                Tweens.Punch(_timerMoth, 0.4f, 0.4f);
            }

            Tweens.Delay(Root, 0.4f, () => ShowFail(FailKind.TimeUp));
        }

        private void ShowFail(FailKind kind)
        {
            var canRescue = kind == FailKind.TimeUp || _game.Slots.Count < LoomGame.MaxSlots;
            App.ShowPopup(new FailPopup(App, kind, canRescue, () => Rescue(kind), Restart));
        }

        private void Rescue(FailKind kind)
        {
            if (kind == FailKind.Jam)
            {
                _game.AddSlot();
                RebuildSlots();
                RefreshAll();
                Sfx.Play(Sound.Unlock);
                var last = _slotFrames[_slotFrames.Length - 1];
                Tweens.Scale(last, 0.4f, 1f, 0.3f);
            }
            else
            {
                _game.AddTime(Economy.TimeBonusSeconds);
                _timeUpShown = false;
                RefreshTimer();
                RefreshAll();
            }

            AfterMove(_game.Status);
        }

        private void Restart()
        {
            App.StartLevel(_level.id);
        }

        // ---------- Бустеры ----------

        private void OnBooster(BoosterType type)
        {
            if (_busy > 0 || _ended)
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
                        _game.AddSlot();
                        RebuildSlots();
                        RefreshAll();
                        Sfx.Play(Sound.Unlock);
                        Tweens.Scale(_slotFrames[_slotFrames.Length - 1], 0.4f, 1f, 0.3f);
                    }

                    break;
            }
        }

        private void NotEnough(BoosterType type)
        {
            App.ShowPopup(new NotEnoughPopup(App, type, RefreshBoosters));
        }

        private void SetScissors(bool on)
        {
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
        }

        private void HideHint()
        {
            if (_hint != null)
            {
                Object.Destroy(_hint.gameObject);
                _hint = null;
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

        private SlotEntry?[] SnapshotSlots()
        {
            var result = new SlotEntry?[_game.Slots.Count];
            for (var i = 0; i < result.Length; i++)
            {
                var spool = _game.Slots[i];
                result[i] = spool != null ? new SlotEntry(spool, spool.Loops) : null;
            }

            return result;
        }

        private sealed class SlotEntry
        {
            public SlotEntry(Spool spool, int loops)
            {
                Spool = spool;
                Loops = loops;
            }

            public Spool Spool { get; }

            public int Loops { get; set; }
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
            private readonly RectTransform _badge;

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

                _badge = Ui.Place(Ui.Node(root, "Badge"), w - 40f, -8f, 46, 22);
                _badgeBack = Ui.Box(_badge, "Back", 11, Palette.Ink);
                Ui.Stretch(_badgeBack.rectTransform);
                _badgeText = Ui.Label(_badge, "", 12, Color.white);
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
