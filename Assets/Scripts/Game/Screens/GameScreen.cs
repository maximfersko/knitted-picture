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
    /// можно в любой момент. Комбо — за темп: клубок за клубком, пока станок не простаивает;
    /// на ×5 включается «Разгон» — станок вяжет вдвое быстрее, петли приносят монеты.
    /// </summary>
    public sealed class GameScreen : ScreenView
    {
        private const float Pad = 16f;
        private const int VisibleRows = 3;

        /// <summary>
        /// Строка над станком (подпись «Станок» или плашка комбо, справа счётчик мест): станок начинается ниже,
        /// и плашка комбо не наезжает ни на картинку, ни на станок, ни на его подсветку.
        /// </summary>
        private const float LoomTop = 30f;

        private const float LoomH = 74f;

        private const float ComboW = 132f;

        /// <summary>Полоска под клубками для «+N» — сколько ещё ждёт в колонке.</summary>
        private const float DepthRow = 16f;

        /// <summary>Секунд на шаг вязки: каждый клубок делает ~15 петель в секунду.</summary>
        private const float StepInterval = 0.065f;

        /// <summary>Полёт нитки от клубка к петле.</summary>
        private const float BeadFlight = 0.26f;

        /// <summary>Во сколько раз быстрее вяжет станок в «Разгоне».</summary>
        private const float RushSpeed = 2f;

        /// <summary>Сколько секунд бездействия (станок стоит, ходов нет) до подсказки на уровнях с 6-го.</summary>
        private const float IdleNudge = 20f;

        private static readonly Color IceBlue = new Color(0.55f, 0.82f, 1f);
        private static readonly Color RushColor = new Color(1f, 0.6f, 0.15f);

        private readonly LevelData _level;
        private readonly LoomGame _game;
        private readonly Color[] _colors;
        private readonly List<FieldSpool> _field = new List<FieldSpool>();
        private readonly Dictionary<BoosterType, BoosterButton> _boosters = new Dictionary<BoosterType, BoosterButton>();
        private readonly HashSet<int> _arriving = new HashSet<int>();
        private readonly HashSet<int> _finishedColors = new HashSet<int>();

        /// <summary>Ножницы взяты из окна затора: срез засчитается как спасение.</summary>
        private bool _scissorsRescue;

        /// <summary>Какие пояснения уже показаны на этом уровне.</summary>
        /// <summary>
        /// Какие пояснения уже показаны. Общие на всю игровую сессию, а не на уровень: одно и то же
        /// «сначала уходит передний клубок» на каждом уровне раздражает.
        /// </summary>
        private static readonly HashSet<string> _told = new HashSet<string>();
        /// <summary>Счёт уровня, который переживает перестройку экрана (поворот, смена безопасной зоны).</summary>
        private readonly LevelRun _run;

        private TempoCombo _tempo => _run.Tempo;

        /// <summary>Монет за пойманную золотую петлю: база плюс бонус пряжи «в работе».</summary>
        private readonly int _goldPerLoop;

        private PictureView _picture = null!;
        private RectTransform _pictureCard = null!;
        private RectTransform _progressFill = null!;
        private Text _progressText = null!;
        private float _progressWidth;

        private RectTransform _loomBg = null!;
        private Image _loomBgImage = null!;
        private Image _loomAlert = null!;
        private Image _rushGlow = null!;
        private Text _loomTitle = null!;
        private Text _loomLabel = null!;
        private RectTransform[] _slotFrames = new RectTransform[0];
        private Image[] _slotOutlines = new Image[0];
        private SlotSpool?[] _slotViews = new SlotSpool?[0];
        private RectTransform?[] _tailViews = new RectTransform?[0];
        private float _slotSpool;

        /// <summary>Зазор между слотами станка: толстый клубок в двух соседних слотах встаёт прямо над ним.</summary>
        private const float SlotGap = 6f;

        private RectTransform _comboChip = null!;
        private Image _comboBack = null!;
        private Image _comboBolt = null!;
        private Text _comboText = null!;
        private RectTransform _comboHeat = null!;
        private CanvasGroup _comboGroup = null!;
        private float _rushClock;

        private Text? _goldGoal;

        private bool _foreignShown
        {
            get => _run.ForeignShown;
            set => _run.ForeignShown = value;
        }

        private int _goldCaught
        {
            get => _run.GoldCaught;
            set => _run.GoldCaught = value;
        }

        private int _goldCoins
        {
            get => _run.GoldCoins;
            set => _run.GoldCoins = value;
        }

        /// <summary>Золотые петли, которые уже засчитаны: клетка → поймана ли во время комбо.</summary>
        private Dictionary<int, bool> _goldDone => _run.GoldDone;

        private RectTransform _fieldContent = null!;
        private RectTransform _fx = null!;
        private Text _coins = null!;

        /// <summary>Значок моли в конце полоски времени: подпись, чья это полоска. Сама моль живёт на картинке.</summary>
        private Image? _timerIcon;
        private RectTransform? _timerFill;
        private Text? _timerText;

        /// <summary>Моль в последней четверти времени: показано «Моль близко!».</summary>
        private bool _mothNear;

        /// <summary>Красное свечение по краю экрана в последние секунды моли.</summary>
        private Image? _mothEdge;

        /// <summary>Живая моль на рамке картинки (только на уровнях с таймером).</summary>
        private MothView? _moth;
        private RectTransform? _mothLayer;
        private Vector2 _mothSpot = new Vector2(float.NaN, float.NaN);

        /// <summary>Моль ест полотно: время вышло, окно покажется после укусов.</summary>
        private bool _mothEating;

        /// <summary>Экран собран целиком (слой эффектов уже есть).</summary>
        private bool _built;

        /// <summary>Моль уже улетела (финиш или победа) и больше не вернётся.</summary>
        private bool _mothGone;

        /// <summary>
        /// Финиш: победа уже решена, станок довязывает сам. Поле и бустеры затихают, моль улетает,
        /// таймер стоит, вязка ускоряется — не нужно ждать и нечего бояться.
        /// </summary>
        private bool _finishing;
        private float _finishSpeed = 1f;
        private float _finishCheck;

        /// <summary>Подсветка причины затора на поле (кольца), убирается при спасении.</summary>
        private readonly List<RectTransform> _jamMarks = new List<RectTransform>();

        private int _lastTickSecond = -1;

        /// <summary>С какого остатка секунд начинается отсчёт: тик каждую секунду.</summary>
        private const float CountdownFrom = 10f;
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
        private int _boostersUsed
        {
            get => _run.BoostersUsed;
            set => _run.BoostersUsed = value;
        }

        private int _rescues
        {
            get => _run.Rescues;
            set => _run.Rescues = value;
        }

        /// <summary>Подсказка не нашла пути при стольких тапах: не искать снова, пока игрок не сходит.</summary>
        private int _hintFailedAt = -1;
        private RectTransform? _hint;
        private RectTransform? _hintHand;
        private RectTransform? _tip;

        /// <summary>Подсказку не гасить от тапа (объяснение правила посреди игры), только по таймеру.</summary>
        private bool _tipKeep;
        private float _tipShownAt;
        private RectTransform? _scissorsBanner;

        public GameScreen(App app, LevelData level, LoomGame game, LevelRun run)
            : base(app, "Game")
        {
            _level = level;
            _game = game;
            _run = run;
            // Пряжа «в работе» меняет только награды, не правила: уровень остаётся честным.
            _goldPerLoop = Economy.GoldCoins + YarnCatalog.GoldBonus(app.Progress.PerkPower(YarnPerk.Gold));
            _colors = level.colors.Select(c => Palette.Vivid(Palette.Hex(c))).ToArray();
            if (!run.Pattern)
            {
                app.SetGround(Palette.GroundOfLevel(level.id));
            }

            BuildHud();
            if (_game.TimeLimit > 0)
            {
                BuildTimer();
            }

            ComputeLayout();
            BuildGoals();
            BuildPicture();
            BuildLoom();
            BuildField();
            BuildBoosters();
            if (_game.TimeLimit > 0)
            {
                // Моль поверх картинки и поля, но под красным краем, эффектами и окнами.
                _mothLayer = Ui.Stretch(Ui.Node(Root, "MothLayer"));
                var step = _picture.Root.sizeDelta.x / Mathf.Max(1, _game.Width);
                _moth = MothView.Create(_mothLayer, Mathf.Clamp(step * 1.15f, 22f, 30f));
                _moth.Hide();

                // Поверх поля, но под эффектами и окнами: край экрана краснеет, когда время на исходе.
                _mothEdge = Ui.Image(Root, "MothEdge", Art.EdgeGlow(40), Color.clear);
                _mothEdge.type = Image.Type.Sliced;
                _mothEdge.pixelsPerUnitMultiplier = Art.Density;
                Ui.Stretch(_mothEdge.rectTransform);
            }

            _fx = Ui.Stretch(Ui.Node(Root, "Fx"));
            _built = true;
            RefreshAll();
            if (_moth != null)
            {
                // Новый уровень: моль прилетает и садится на рамку — видно, кто тут угроза. Перестройка — сразу на месте.
                if (_game.Taps == 0 && !_game.TimerStarted)
                {
                    Tweens.Delay(Root, 0.8f, () => MothArrive(false));
                }
                else
                {
                    MothArrive(true);
                }
            }

            // Экран перестроили посреди уровня: подсказки не повторяем, а затор или моль показываем снова.
            if (_game.Taps == 0)
            {
                ShowIntro();
            }

            if (_game.Status != GameStatus.Playing)
            {
                var status = _game.Status;
                Tweens.Delay(Root, 0.1f, () => AfterStatus(status));
            }
        }

        /// <summary>Игру свернули посреди уровня: ставим на паузу, чтобы моль не съела картинку, пока игрока нет.</summary>
        public void AutoPause()
        {
            if (!_ended && !_failShown && !App.HasPopups && _game.Status == GameStatus.Playing)
            {
                App.ShowPopup(new PausePopup(App, Restart, LeaveHome, StreakWarning()));
            }
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

            if (_game.TimeLimit > 0 && !_finishing)
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
                var interval = (_tempo.Rush ? StepInterval / RushSpeed : StepInterval) / _finishSpeed;
                var guard = 0;
                while (_stepClock >= interval && guard++ < 6)
                {
                    _stepClock -= interval;
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

            // Победа уже решена — финиш. Проверка на копии партии, четыре раза в секунду.
            if (!_finishing && _game.Status == GameStatus.Playing && _game.Taps > 0)
            {
                _finishCheck -= dt;
                if (_finishCheck <= 0f)
                {
                    _finishCheck = 0.25f;
                    if (_game.IsBusy && _game.WinIsCertain())
                    {
                        StartFinish();
                    }
                }
            }

            var wasRush = _tempo.Rush;
            if (_tempo.Update(dt, _game.IsBusy || _arriving.Count > 0))
            {
                OnComboBurned(wasRush);
            }

            UpdateLoomAlert(dt);
            UpdateCombo(dt);
            _picture.SetGoldLive(_tempo.Active);

            if (!_finishing && !_game.IsBusy && _hint == null && _game.Status == GameStatus.Playing && _hintFailedAt != _game.Taps)
            {
                _idle += dt;
                // Дальше — мягкий толчок, если игрок долго не делает ничего: 20 секунд без хода, станок стоит.
                var threshold = _level.id == 1 ? 3f : _level.id <= 3 ? 6f : _level.id <= 5 ? 12f : IdleNudge;
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
                App.ShowPopup(new PausePopup(App, Restart, LeaveHome, StreakWarning()));
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
                    App.ShowPopup(new PausePopup(App, Restart, LeaveHome, StreakWarning()));
                }
            });

            // Только номер: у сложного плашка красная. Подсказка уровня открывается тапом по плашке.
            var hasTip = !string.IsNullOrEmpty(_level.tip);
            var text = _run.Pattern ? "Узор дня" : _level.id.ToString();
            var width = _run.Pattern ? 120f : 64f;
            var pill = Ui.Card(Root, "Level", W / 2f - width / 2f, 8, width, 44, 22,
                _level.hard ? Palette.Danger : Palette.Card, Palette.CardShadow, _level.hard ? 0f : 2f, hasTip);
            Ui.Fit(Ui.Label(pill, text, 20, _level.hard ? Color.white : Palette.Ink, 6, 0, width - 12, 44), 12);
            if (hasTip)
            {
                // Подсказка уровня исчезает сама; вернуть её можно тапом по плашке уровня.
                pill.gameObject.AddComponent<Tap>().OnClick(() =>
                {
                    if (!_ended && !_failShown)
                    {
                        App.ShowPopup(new InfoPopup(App, "Подсказка уровня", _level.tip));
                    }
                });
            }
            // Монеты на уровне не показываем: цена бустера видна в окне покупки, заработанное — на победе.
            _coins = Ui.Label(Root, "", 1, Color.clear, 0, 0, 1, 1);
            _coins.enabled = false;
        }

        private void BuildTimer()
        {
            const float y = 60f;
            _timerX = 44f;
            _timerW = W - 44f - 100f;
            var yarn = Ui.Image(Root, "Target", Art2.YarnBall(_colors[0]), Color.white);
            Ui.PlaceCenter(yarn.rectTransform, 26, y + 13, 24, 24);
            var track = Ui.Box(Root, "TimerTrack", 5, Palette.Track);
            Ui.Place(track.rectTransform, _timerX, y + 8, _timerW, 10);
            _timerFill = Ui.Box(track.rectTransform, "Fill", 5, Palette.Primary).rectTransform;
            Ui.Place(_timerFill, 0, 0, _timerW, 10);
            _timerIcon = Ui.IconImage(Root, Icon.Moth, Palette.WithAlpha(Palette.Ink, 0.5f), _timerX + _timerW + 16f, y + 13, 18);
            _timerText = Ui.Label(Root, "", 16, Palette.Ink, W - Pad - 56, y, 56, 26, true, TextAnchor.MiddleRight);

            // Опора справа посередине: на сердцебиении цифры растут от своего края, а не уезжают.
            _timerText.rectTransform.pivot = new Vector2(1f, 0.5f);
            _timerText.rectTransform.anchoredPosition = new Vector2(W - Pad, -(y + 13f));
            RefreshTimer();
        }

        /// <summary>Плашки цели над картинкой больше нет: золотые кольца видны прямо на картинке, итог — на победе.</summary>
        private bool HasGoals => false;

        private float GoalsY => _game.TimeLimit > 0 ? 94f : 60f;

        /// <summary>Цель уровня над картинкой: золотые петли (одна плашка по центру).</summary>
        private void BuildGoals()
        {
            if (!HasGoals)
            {
                return;
            }

            const float chipW = 150f;
            var chip = Ui.Card(Root, "GoldGoal", W / 2f - chipW / 2f, GoalsY, chipW, 28, 14, Palette.Card, Palette.CardShadow, 2f, true);

            // Тап по цели — короткая справка; игра на это время стоит.
            var info = $"Петли в золотом кольце. Свяжи их, пока горит комбо (кольца яркие), и получишь +{_goldPerLoop} монет за каждую.";
            chip.gameObject.AddComponent<Tap>().OnClick(() => App.ShowPopup(new InfoPopup(App, "Золотые петли", info)));
            Ui.IconImage(chip, Icon.Star, Palette.Coin, 18, 14, 18);
            _goldGoal = Ui.Fit(Ui.Label(chip, "", 14, Palette.Ink, 32, 0, chipW - 38, 28, true, TextAnchor.MiddleLeft), 10);
            RefreshGoals();
        }

        private void RefreshGoals()
        {
            if (_goldGoal != null)
            {
                _goldGoal.text = $"Золото {_goldCaught}/{_level.gold.Length}";
            }
        }

        private void ComputeLayout()
        {
            _top = (_game.TimeLimit > 0 ? 96f : 62f) + (HasGoals ? 34f : 0f);
            // На первых уровнях бустеров нет: их место отдаём полю, а клубки делаем крупнее.
            var boosterRow = new[] { BoosterType.Scissors, BoosterType.Undo, BoosterType.Slot }.Any(t => App.Progress.IsBoosterOpen(t, BoosterLevel));
            var boostersY = boosterRow ? H - 10f - 60f : H - 4f;
            _colW = (W - 2 * Pad - 16f) / _game.ColumnCount;
            _spoolSize = Mathf.Min(boosterRow ? 52f : 58f, _colW - 12f);

            for (var attempt = 0; attempt < 4; attempt++)
            {
                _fieldH = VisibleRows * (_spoolSize + 10f) + 10f + DepthRow;
                _fieldY = boostersY - 16f - _fieldH;
                _loomY = _fieldY - 12f - LoomTop - LoomH;
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

            _progressWidth = cardW - 24f;
            var bar = Ui.Box(_pictureCard, "ProgressTrack", 4, Palette.Track);
            Ui.Place(bar.rectTransform, 12, 12 + gridH + 13, _progressWidth, 8);
            _progressFill = Ui.Box(bar.rectTransform, "Fill", 4, Palette.Primary).rectTransform;
            Ui.Place(_progressFill, 0, 0, 8, 8);
            _progressText = Ui.Label(_pictureCard, "0%", 13, Palette.Ink2, cardW - 12 - 44, 12 + gridH + 5, 44, 24, true, TextAnchor.MiddleRight);
        }

        private void BuildLoom()
        {
            // Подписи станка нет: слоты видны. Надпись появляется, только когда места кончаются.
            _loomTitle = Ui.Label(Root, "", 13, Palette.Ink2, Pad + 6, _loomY + 2f, 120, 24, true, TextAnchor.MiddleLeft);
            _loomLabel = Ui.Label(Root, "", 13, Palette.Ink2, W - Pad - 186, _loomY + 2f, 180, 24, true, TextAnchor.MiddleRight);
            _loomBgImage = Ui.Box(Root, "Loom", 20, Palette.LoomBg);
            _loomBg = _loomBgImage.rectTransform;
            Ui.Place(_loomBg, Pad, _loomY + LoomTop, W - 2 * Pad, LoomH);
            // Рамки тревоги и «Разгона» лежат ровно по краю станка, внутрь, а не выступают на соседей.
            _loomAlert = Ui.Image(Root, "LoomAlert", Art.RoundRectOutline(20, 3), Color.clear);
            _loomAlert.type = Image.Type.Sliced;
            _loomAlert.pixelsPerUnitMultiplier = Art.Density;
            Ui.Place(_loomAlert.rectTransform, Pad, _loomY + LoomTop, W - 2 * Pad, LoomH);
            _rushGlow = Ui.Image(Root, "RushGlow", Art.RoundRectOutline(20, 4), Color.clear);
            _rushGlow.type = Image.Type.Sliced;
            _rushGlow.pixelsPerUnitMultiplier = Art.Density;
            Ui.Place(_rushGlow.rectTransform, Pad, _loomY + LoomTop, W - 2 * Pad, LoomH);
            BuildComboChip();
            RebuildSlots();
        }

        /// <summary>
        /// Плашка комбо в строке над станком, на месте подписи «Станок»: «Комбо ×3» и полоска —
        /// сколько ещё станок может простоять. Компактная и целиком в своей строке.
        /// </summary>
        private void BuildComboChip()
        {
            _comboChip = Ui.Place(Ui.Node(Root, "Combo"), Pad + 2f, _loomY + 2f, ComboW, 24);
            _comboGroup = _comboChip.gameObject.AddComponent<CanvasGroup>();
            _comboGroup.blocksRaycasts = false;
            _comboGroup.alpha = 0f;
            _comboBack = Ui.Box(_comboChip, "Back", 13, Palette.Link);
            Ui.Stretch(_comboBack.rectTransform);
            _comboBolt = Ui.IconImage(_comboChip, Icon.Bolt, Color.white, 14, 11, 14);
            _comboText = Ui.Fit(Ui.Label(_comboChip, "", 13, Color.white, 24, 0, ComboW - 30, 19, true, TextAnchor.MiddleLeft), 10);
            var track = Ui.Box(_comboChip, "HeatTrack", 1.5f, Palette.WithAlpha(Color.white, 0.3f));
            Ui.Place(track.rectTransform, 10, 18, ComboW - 20, 3);
            _comboHeat = Ui.Box(track.rectTransform, "Heat", 1.5f, Color.white).rectTransform;
            Ui.Place(_comboHeat, 0, 0, ComboW - 20, 3);
        }

        private void RebuildSlots()
        {
            Ui.Clear(_loomBg);
            var n = _game.Slots.Count;
            const float gap = SlotGap;
            var slotW = (W - 2 * Pad - 16f - gap * (n - 1)) / n;
            _slotSpool = Mathf.Min(44f, slotW - 10f);
            _slotFrames = new RectTransform[n];
            _slotOutlines = new Image[n];
            _slotViews = new SlotSpool?[n];
            _tailViews = new RectTransform?[n];
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
                var slot = i;
                var tap = frame.gameObject.AddComponent<Tap>();
                frame.raycastTarget = true;
                tap.ClickSound = false;
                tap.PressFeedback = false;
                tap.OnClick(() => OnSlotTap(slot));
            }
        }

        private void BuildField()
        {
            var card = Ui.Card(Root, "Field", Pad, _fieldY, W - 2 * Pad, _fieldH, 24, Palette.Card, Palette.CardShadow);
            _fieldContent = Ui.Stretch(Ui.Node(card, "Content"));
        }

        private void BuildBoosters()
        {
            // Только открытые бустеры: закрытая серая кнопка «ур. 12» ничего не делает и только шумит.
            var types = new[] { BoosterType.Scissors, BoosterType.Undo, BoosterType.Slot }
                .Where(t => App.Progress.IsBoosterOpen(t, BoosterLevel)).ToArray();
            if (types.Length == 0)
            {
                return;
            }

            const float gap = 10f;
            var w = Mathf.Min((W - 2 * Pad - gap * 2) / 3f, (W - 2 * Pad - gap * (types.Length - 1)) / types.Length);
            var x0 = (W - (w * types.Length + gap * (types.Length - 1))) / 2f;
            var y = H - 10f - 60f;
            for (var i = 0; i < types.Length; i++)
            {
                var type = types[i];
                var button = new BoosterButton(Root, type, x0 + i * (w + gap), y, w);
                button.Tap.OnClick(() => OnBooster(type));
                _boosters[type] = button;
            }
        }

        private void ShowIntro()
        {
            if (!string.IsNullOrEmpty(_level.tip))
            {
                Tweens.Delay(Root, 0.6f, ShowTip);
            }

            // Один баннер на старте — самое важное: сложный уровень, иначе загадка, иначе подарок серии.
            var bannerY = _top + 40f;
            if (_level.hard)
            {
                Banner("Сложный уровень", Palette.Danger, bannerY);
            }
            else if (_level.mystery)
            {
                Banner("Загадка: что получится?", Palette.Link, bannerY);
            }
            else if (_run.Warm.Any)
            {
                Banner($"Серия ×{_run.WarmStreak}: {WarmGifts(_run.Warm)}", Palette.CoinShadow, bannerY, 0.3f);
            }
        }

        /// <summary>Уровень, по которому открыты бустеры: у «узора дня» — текущий уровень кампании.</summary>
        private int BoosterLevel => _run.Pattern ? App.Progress.CurrentLevel : _level.id;

        /// <summary>Серия есть и уровень новый: перезапуск или уход её обнулят — честно предупредить.</summary>
        private string? StreakWarning() =>
            !_run.Pattern && App.Progress.Streak > 0 && !App.Progress.IsCompleted(_level.id)
                ? $"«Заново» или «Домой» обнулят тёплую серию ×{App.Progress.Streak}"
                : null;

        /// <summary>Подарок серии словами: «+1 слот, ножницы и отмена».</summary>
        public static string WarmGifts(WarmStart warm)
        {
            var parts = new List<string>();
            if (warm.ExtraSlot)
            {
                parts.Add("+1 слот");
            }

            if (warm.FreeScissors)
            {
                parts.Add("ножницы");
            }

            if (warm.FreeUndo)
            {
                parts.Add("отмена");
            }

            return parts.Count <= 1 ? string.Concat(parts) : string.Join(", ", parts.Take(parts.Count - 1)) + " и " + parts[parts.Count - 1];
        }

        private void Banner(string text, Color color, float y, float delay = 0f)
        {
            var width = Mathf.Min(W - 2 * Pad, 320f);
            var banner = Ui.Card(Root, "Banner", W / 2f - width / 2f, y, width, 56, 28, color, color, 0f);
            Ui.Fit(Ui.Label(banner, text, 20, Color.white, 14, 0, width - 28, 56), 13);
            var group = banner.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            banner.localScale = Vector3.zero;
            Tweens.Scale(banner, 0.6f, 1f, 0.3f, Ease.OutBack, null, delay);
            Tweens.Fade(group, 1f, 0f, 0.4f, () => Object.Destroy(banner.gameObject), 1.6f + delay);
        }

        // ---------- Обновление ----------

        private void RefreshAll()
        {
            _picture.Refresh();
            RefreshGoals();
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
            _progressText.text = string.Empty;
        }

        /// <summary>Привести клубки на станке к модели: новые появляются, ушедшие исчезают, числа обновляются.</summary>
        private void SyncLoom(bool rebuild = false)
        {
            var used = 0;
            for (var i = 0; i < _slotFrames.Length; i++)
            {
                var spool = _game.Slots[i];
                var view = _slotViews[i];
                var tail = _tailViews[i];
                var isTail = _game.IsTail(i);
                if (tail != null && (rebuild || !isTail))
                {
                    Object.Destroy(tail.gameObject);
                    _tailViews[i] = null;
                    tail = null;
                }

                if (isTail)
                {
                    if (view != null)
                    {
                        Object.Destroy(view.View.Root.gameObject);
                        _slotViews[i] = null;
                    }

                    if (tail == null)
                    {
                        _tailViews[i] = BuildTail(i, spool!);
                    }

                    used++;
                    _slotOutlines[i].enabled = false;
                    continue;
                }

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
                    var size = SpoolSizeAt(i);
                    var created = SpoolView.Create(_slotFrames[i], size);
                    Ui.PlaceCenter(created.Root, SpoolX(i), 29f, size, size);
                    created.Bind(spool, _colors[spool.Color], true, false, _game.IsForeign(spool));
                    created.SetFabric(_game.IsForeign(spool));
                    created.HideDouble();
                    _slotViews[i] = new SlotSpool(created, spool.Id, spool.Rainbow ? -1 : spool.Color);
                    if (_game.IsForeign(spool) && !_foreignShown)
                    {
                        _foreignShown = true;
                        ToastOnce("foreign-loom", "Этой пряжи нет в рисунке: срежь её ножницами");
                    }
                    view = _slotViews[i];
                }

                if (view != null && spool != null)
                {
                    view.View.SetLoops(spool.Loops);
                    view.View.SetWaiting(!_game.IsForeign(spool) && !_game.HasWork(spool));
                }

                _slotOutlines[i].enabled = spool == null;
            }

            // Плашка толстого клубка бледнеет вместе с ним: ждёт весь «широкий клубок».
            for (var i = 0; i < _tailViews.Length; i++)
            {
                var plate = _tailViews[i];
                var owner = i < _game.Slots.Count ? _game.Slots[i] : null;
                if (plate == null || owner == null)
                {
                    continue;
                }

                var group = plate.GetComponent<CanvasGroup>() ?? plate.gameObject.AddComponent<CanvasGroup>();
                group.blocksRaycasts = false;
                group.alpha = !_game.IsForeign(owner) && !_game.HasWork(owner) ? 0.5f : 1f;
            }

            var free = _game.FreeSlots;
            _loomLabel.text = free == 1 && _slotFrames.Length > 1 ? "Осталось 1 место!" : free == 0 ? "Мест нет" : string.Empty;
            _loomLabel.color = free <= 1 ? Palette.Danger : Palette.Ink2;
        }

        /// <summary>Толстый клубок стоит в этом слоте и в соседнем справа: рисуем его одним широким клубком.</summary>
        private bool IsWide(int slot) => slot + 1 < _slotFrames.Length && _game.TailOf(slot) == slot + 1;

        /// <summary>Центр клубка по горизонтали внутри рамки слота: у широкого — над зазором между двумя слотами.</summary>
        private float SpoolX(int slot)
        {
            var w = _slotFrames[slot].sizeDelta.x;
            return IsWide(slot) ? w + SlotGap / 2f : w / 2f;
        }

        /// <summary>Широкий клубок чуть крупнее обычного: сразу видно, что он толстый.</summary>
        private float SpoolSizeAt(int slot) => IsWide(slot) ? Mathf.Min(_slotSpool * 1.15f, 50f) : _slotSpool;

        /// <summary>Куда летит клубок на станок: в центр слота, у широкого — между двумя слотами.</summary>
        private Vector3 SlotTarget(int slot) =>
            IsWide(slot)
                ? Vector3.Lerp(Ui.WorldCenter(_slotFrames[slot]), Ui.WorldCenter(_slotFrames[slot + 1]), 0.5f)
                : Ui.WorldCenter(_slotFrames[slot]);

        /// <summary>
        /// Второй слот толстого клубка. Если слоты соседние — одна широкая подкрашенная плашка на оба слота,
        /// клубок стоит посередине. Если толстому достались слоты врозь — подкрашенная рамка и нить к клубку.
        /// </summary>
        private RectTransform BuildTail(int slot, Spool spool)
        {
            var color = spool.Rainbow ? Palette.Coin : _colors[spool.Color];
            var head = _game.Slots.ToList().IndexOf(spool);
            if (head >= 0 && head + 1 == slot)
            {
                // Плашка живёт в рамке головного слота и рисуется под клубком; рамку — поверх соседней.
                var headFrame = _slotFrames[head];
                headFrame.SetAsLastSibling();
                var hw = headFrame.sizeDelta.x;
                var wide = Ui.Place(Ui.Node(headFrame, "Wide"), 0, 0, hw * 2f + SlotGap, headFrame.sizeDelta.y);
                wide.SetAsFirstSibling();
                Ui.Stretch(Ui.Box(wide, "Fill", 14, Color.Lerp(Palette.Card, color, 0.22f)).rectTransform);
                var edge = Ui.Image(wide, "Edge", Art.RoundRectOutline(14, 2), Palette.WithAlpha(color, 0.7f));
                edge.type = Image.Type.Sliced;
                edge.pixelsPerUnitMultiplier = Art.Density;
                Ui.Stretch(edge.rectTransform);
                Tweens.Scale(wide, 0.9f, 1f, 0.2f);
                return wide;
            }

            var frame = _slotFrames[slot];
            var root = Ui.Stretch(Ui.Node(frame, "Tail"));
            Ui.Stretch(Ui.Box(root, "Fill", 14, Palette.WithAlpha(color, 0.25f)).rectTransform);
            var outline = Ui.Image(root, "Edge", Art.RoundRectOutline(14, 2), Palette.WithAlpha(color, 0.7f));
            outline.type = Image.Type.Sliced;
            outline.pixelsPerUnitMultiplier = Art.Density;
            Ui.Stretch(outline.rectTransform);
            var toLeft = head >= 0 && head < slot;
            var w = frame.sizeDelta.x;
            var thread = Ui.Box(root, "Thread", 3, color);
            Ui.Place(thread.rectTransform, toLeft ? -8f : w / 2f, 26, w / 2f + 8f, 6);
            Tweens.Scale(root, 0.5f, 1f, 0.2f);
            return root;
        }

        private void RefreshField()
        {
            Ui.Clear(_fieldContent);
            _field.Clear();
            _hint = null;
            _hintHand = null;
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
                    view.Bind(spool, _colors[spool.Color], d == 0 || _scissors, _game.IsSurplus(spool));
                    view.SetFabric(spool.IsColorVisible && _game.IsForeign(spool));
                    if (d > 0 && !_scissors)
                    {
                        // Задние ряды тише и чуть меньше: главное на поле — передние клубки.
                        view.Quiet();
                        view.Root.localScale = Vector3.one * 0.86f;
                    }

                    var tap = view.Tap;
                    tap.ClickSound = false;
                    var columnIndex = c;
                    var depth = d;
                    tap.OnClick(() => OnSpoolTap(columnIndex, depth));
                    _field.Add(new FieldSpool(view, spool.Id, c, d));

                    // Передний клубок, которому сразу есть работа, слегка «дышит».
                    if (d == 0 && spool.IsColorVisible && _game.HasWork(spool) && !spool.Locked && !spool.Frozen)
                    {
                        var root = view.Root;
                        Tweens.Run(root, 1.4f, t => root.localScale = Vector3.one * (1f + 0.04f * Ease.Bump(t)), null, null, c * 0.12f, true);
                    }
                }

                // Сколько ещё клубков за видимыми — тихой плашкой под колонкой, не поверх клубков.
                if (column.Count > VisibleRows)
                {
                    var (x, _) = FieldPosition(c, 0);
                    var rowY = 7f + (_spoolSize + 10f) * VisibleRows;
                    var more = Ui.Box(_fieldContent, "More", 7, Palette.Surface).rectTransform;
                    Ui.PlaceCenter(more, x, rowY + DepthRow / 2f - 2f, 34, 15);
                    Ui.Label(more, "+" + (column.Count - VisibleRows), 11, Palette.Ink2, 0, 0, 34, 15);
                }
            }

            DrawLinkThreads();
        }

        /// <summary>
        /// Связанные клубки на поле соединены тонкой нитью (под клубками): видно, кто с кем в паре,
        /// даже когда пар несколько.
        /// </summary>
        private void DrawLinkThreads()
        {
            var pairs = new Dictionary<int, List<FieldSpool>>();
            foreach (var f in _field)
            {
                var column = _game.Column(f.Column);
                if (f.Depth >= column.Count || column[f.Depth].Id != f.SpoolId || column[f.Depth].Link == 0)
                {
                    continue;
                }

                var link = column[f.Depth].Link;
                if (!pairs.TryGetValue(link, out var list))
                {
                    list = new List<FieldSpool>();
                    pairs[link] = list;
                }

                list.Add(f);
            }

            foreach (var list in pairs.Values)
            {
                if (list.Count != 2)
                {
                    continue;
                }

                var (ax, ay) = FieldPosition(list[0].Column, list[0].Depth);
                var (bx, by) = FieldPosition(list[1].Column, list[1].Depth);
                var dx = bx - ax;
                var dy = by - ay;
                var length = Mathf.Sqrt(dx * dx + dy * dy);
                var link = _game.Column(list[0].Column)[list[0].Depth].Link;
                var thread = Ui.Box(_fieldContent, "LinkThread", 1.5f, Palette.WithAlpha(Palette.LinkPair(link), 0.6f)).rectTransform;
                Ui.PlaceCenter(thread, (ax + bx) / 2f, (ay + by) / 2f, length, 3f);

                // Координаты поля идут сверху вниз, поэтому угол со знаком минус.
                thread.localRotation = Quaternion.Euler(0f, 0f, -Mathf.Atan2(dy, dx) * Mathf.Rad2Deg);
                thread.SetAsFirstSibling();
            }
        }

        /// <summary>
        /// Где пара связанного клубка и что мешает: связанные едут на станок только вдвоём,
        /// когда оба впереди своих колонок. Без этого отказ выглядел как баг.
        /// </summary>
        private string LinkPartnerText(int column, int depth)
        {
            var (partnerColumn, partnerDepth) = FindLinkPartner(column, depth);
            if (partnerColumn < 0)
            {
                return "Связанные клубки едут на станок только вдвоём";
            }

            var where = $"в {partnerColumn + 1}-й колонке";
            if (partnerDepth > 0)
            {
                return $"Пара {where}, перед ней {partnerDepth} {Plural(partnerDepth, "клубок", "клубка", "клубков")}";
            }

            var partner = _game.Column(partnerColumn)[0];
            var block = partner.Locked ? "под замком" : partner.Knots > 0 ? "с узелком" : partner.Frozen ? "во льду" : "не готова";
            return $"Связанные едут вдвоём. Пара (кольцо того же цвета) {where} пока {block}";
        }

        private (int Column, int Depth) FindLinkPartner(int column, int depth)
        {
            var col = _game.Column(column);
            if (depth >= col.Count || col[depth].Link == 0)
            {
                return (-1, -1);
            }

            var spool = col[depth];
            for (var c = 0; c < _game.ColumnCount; c++)
            {
                var other = _game.Column(c);
                for (var d = 0; d < other.Count; d++)
                {
                    if (other[d].Id != spool.Id && other[d].Link == spool.Link)
                    {
                        return (c, d);
                    }
                }
            }

            return (-1, -1);
        }

        /// <summary>Отказ связанному клубку: подсветить его пару, которую надо освободить первой.</summary>
        private void PunchLinkPartner(int column, int depth)
        {
            var col = _game.Column(column);
            if (depth >= col.Count || col[depth].Link == 0)
            {
                return;
            }

            var spool = col[depth];
            foreach (var f in _field)
            {
                var other = _game.Column(f.Column);
                if (f.Depth < other.Count && other[f.Depth].Id == f.SpoolId && other[f.Depth].Id != spool.Id && other[f.Depth].Link == spool.Link)
                {
                    Tweens.Punch(f.View.Root, 0.25f, 0.35f);
                    Fx.Sparkle(_fx, Ui.WorldCenter(f.View.Root), Palette.LinkPair(spool.Link), 8, 30f);
                    return;
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
                var unlocked = App.Progress.IsBoosterOpen(type, BoosterLevel);
                var available = type == BoosterType.Undo ? _game.CanUndo
                    : type == BoosterType.Slot ? _game.Slots.Count < LoomGame.MaxSlots
                    : true;
                var free = type == BoosterType.Scissors ? _run.FreeScissors : type == BoosterType.Undo ? _run.FreeUndo : 0;
                pair.Value.Refresh(unlocked, App.Progress.Stock(type), Economy.Price(type), Economy.UnlockLevel(type),
                    available, type == BoosterType.Scissors && _scissors, free);
            }
        }

        private void RefreshTimer()
        {
            if (_timerFill == null || _timerText == null)
            {
                return;
            }

            var left = Mathf.Max(0f, _game.TimeLeft);
            var fraction = _game.TimeLimit > 0 ? Mathf.Clamp01(left / _game.TimeLimit) : 0f;
            var seconds = Mathf.CeilToInt(left);
            _timerText.text = $"{seconds / 60}:{seconds % 60:00}";
            _timerFill.sizeDelta = new Vector2(Mathf.Max(10f, _timerW * fraction), 10f);
            var fill = _timerFill.GetComponent<Image>();
            if (_finishing)
            {
                // Финиш: время больше не важно — полоска и цифры спокойные, край экрана гаснет.
                _timerText.color = Palette.Primary;
                _timerText.rectTransform.localScale = Vector3.one;
                fill.color = Palette.Primary;
                if (_mothEdge != null)
                {
                    _mothEdge.color = Color.clear;
                }

                return;
            }

            // Последняя четверть: моль близко. Один раз — надпись у самой моли и отклик, дальше она беспокойнее.
            var near = fraction < 0.25f && _game.TimerStarted && _game.Status == GameStatus.Playing;
            if (near && !_mothNear && _built)
            {
                Fx.FloatText(_fx, MothWorld() ?? Ui.WorldCenter(_timerText.rectTransform), "Моль близко!", Palette.Danger, 18f);
                _moth?.Startle();
                Sfx.Play(Sound.Deny, 0.5f);
                Haptics.Play(Haptic.Medium);
            }

            _mothNear = near;
            UpdateMoth(fraction);

            // Сердцебиение: на каждой новой секунде короткий толчок, к концу сильнее.
            var countdown = near && left <= CountdownFrom;
            var sinceTick = 1f - (left - Mathf.Floor(left));
            var pulse = near ? Mathf.Exp(-sinceTick * 6f) : 0f;
            _timerText.rectTransform.localScale = Vector3.one * (1f + (countdown ? 0.35f : 0.18f) * pulse);
            _timerText.color = near ? Palette.Danger : Palette.Ink;
            fill.color = near ? Color.Lerp(Palette.Danger, Palette.WithAlpha(Palette.Danger, 0.6f), pulse)
                : fraction < 0.5f ? Color.Lerp(Palette.Hex("#FF9A3C"), Palette.Primary, (fraction - 0.25f) / 0.25f)
                : Palette.Primary;
            if (_timerIcon != null)
            {
                _timerIcon.color = near ? Palette.WithAlpha(Palette.Danger, 0.7f + 0.3f * pulse) : Palette.WithAlpha(Palette.Ink, 0.5f);
            }

            if (_mothEdge != null)
            {
                var edge = countdown ? 0.3f + 0.4f * pulse : near ? 0.14f * pulse : 0f;
                _mothEdge.color = Palette.WithAlpha(Palette.Danger, edge);
            }

            // Последние 10 секунд: тик каждую секунду — вибрация (последние 5 — заметнее), щелчок
            // и укус моли по краю рамки: видно и слышно, что она уже грызёт.
            if (countdown && seconds != _lastTickSecond)
            {
                _lastTickSecond = seconds;
                Haptics.Play(seconds <= 5 ? Haptic.Medium : Haptic.Soft);
                Sfx.Play(Sound.Click, seconds <= 5 ? 0.9f : 0.6f);
                if (_moth != null && _moth.Shown && !_moth.Flying)
                {
                    _moth.Chew(1, _ =>
                    {
                        var at = MothWorld();
                        if (at != null)
                        {
                            Fx.Sparkle(_fx, at.Value, Palette.Hex("#F4E8D6"), 2, 8f);
                        }
                    }, null);
                }
            }
        }

        // ---------- Моль ----------

        /// <summary>
        /// Где сидит моль при такой доле оставшегося времени. Пока времени больше 60% — в правом верхнем
        /// углу рамки, спокойно. Дальше идёт по верхнему краю к середине короткими перебежками
        /// (семь остановок), к 15% — над серединой картинки, головой к полотну.
        /// </summary>
        private (Vector2 At, float Facing) MothPerch(float fraction)
        {
            var corners = new Vector3[4];
            _pictureCard.GetWorldCorners(corners);
            var bottomLeft = (Vector2)_mothLayer!.InverseTransformPoint(corners[0]);
            var topRight = (Vector2)_mothLayer.InverseTransformPoint(corners[2]);
            var span = _moth!.Span;
            var startX = topRight.x - span * 0.55f;
            var endX = (bottomLeft.x + topRight.x) / 2f;
            var walk = Mathf.InverseLerp(0.6f, 0.15f, fraction);
            walk = Mathf.Floor(walk * 7f + 0.001f) / 7f;
            var at = new Vector2(Mathf.Lerp(startX, endX, walk), topRight.y - 5f);

            // Голова к полотну: почти вниз, с наклоном к середине картинки.
            var facing = MothView.Facing(new Vector2((endX - at.x) * 0.35f, -(topRight.y - bottomLeft.y) * 0.5f));
            return (at, facing);
        }

        /// <summary>Моль следит за временем: перебирается на своё место и волнуется к концу.</summary>
        private void UpdateMoth(float fraction)
        {
            if (_moth == null || !_moth.Shown || _mothEating || _finishing || _moth.Flying)
            {
                return;
            }

            _moth.Agitation = Mathf.Clamp01(Mathf.InverseLerp(0.45f, 0.05f, fraction));
            var (at, facing) = MothPerch(fraction);
            if (Vector2.Distance(at, _mothSpot) > 0.5f)
            {
                _mothSpot = at;
                _moth.MoveTo(at, facing);
            }
        }

        private void MothArrive(bool instant)
        {
            if (_moth == null || _mothLayer == null || _ended || _finishing)
            {
                return;
            }

            var fraction = _game.TimeLimit > 0 ? Mathf.Clamp01(_game.TimeLeft / _game.TimeLimit) : 1f;
            var (at, facing) = MothPerch(fraction);
            _mothSpot = at;
            _moth.Agitation = Mathf.Clamp01(Mathf.InverseLerp(0.45f, 0.05f, fraction));
            if (instant)
            {
                _moth.Place(at, facing);
                return;
            }

            var rect = _mothLayer.rect;
            _moth.FlyIn(new Vector2(rect.xMax + 40f, rect.yMax - 40f), at, facing);
        }

        /// <summary>Улететь за правый верхний край: победа, финиш.</summary>
        private void MothLeave()
        {
            if (_moth == null || _mothLayer == null || !_moth.Shown || _mothGone)
            {
                return;
            }

            _mothGone = true;
            var rect = _mothLayer.rect;
            _moth.FlyAway(new Vector2(rect.xMax + 50f, rect.yMax + 30f));
        }

        private Vector3? MothWorld() =>
            _moth != null && _mothLayer != null && _moth.Shown ? _mothLayer.TransformPoint(_moth.Position) : (Vector3?)null;

        /// <summary>
        /// Какие петли съест моль: связанная клетка рисунка ближе всего к ней и соседняя связанная.
        /// Нет связанного рисунка — клетка ткани. Только картинка: модель партии не меняется.
        /// </summary>
        private List<(int X, int Y)> MothBites()
        {
            var bites = new List<(int X, int Y)>();
            if (_moth == null || _mothLayer == null)
            {
                return bites;
            }

            var from = _moth.Position;
            (int X, int Y)? Nearest(System.Func<int, int, bool> ok)
            {
                (int X, int Y)? best = null;
                var bestDistance = float.MaxValue;
                for (var y = 0; y < _game.Height; y++)
                {
                    for (var x = 0; x < _game.Width; x++)
                    {
                        if (!ok(x, y))
                        {
                            continue;
                        }

                        var d = Vector2.Distance(from, _mothLayer!.InverseTransformPoint(_picture.CellWorld(x, y)));
                        if (d < bestDistance)
                        {
                            bestDistance = d;
                            best = (x, y);
                        }
                    }
                }

                return best;
            }

            var first = Nearest((x, y) => _game.IsKnitted(x, y) && !_game.IsFabric(x, y)) ?? Nearest((x, y) => _game.IsFabric(x, y));
            if (first == null)
            {
                return bites;
            }

            bites.Add(first.Value);
            var (fx, fy) = first.Value;
            var fabric = _game.IsFabric(fx, fy);
            var second = Nearest((x, y) => Mathf.Abs(x - fx) + Mathf.Abs(y - fy) == 1
                && (fabric ? _game.IsFabric(x, y) : _game.IsKnitted(x, y) && !_game.IsFabric(x, y)));
            if (second != null)
            {
                bites.Add(second.Value);
            }

            return bites;
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

        /// <summary>Самый нижний недовязанный ряд на прошлом шаге (−1 — ещё не считали).</summary>
        private int _lowestRow = -1;

        /// <summary>Ряд закрыт целиком — когда долетят нитки, по нему слева направо пробегает светлая волна.</summary>
        private void ShimmerClosedRows()
        {
            var lowest = _game.LowestFront;
            if (_lowestRow >= 0 && lowest < _lowestRow)
            {
                for (var y = _lowestRow; y > lowest && y >= 0; y--)
                {
                    var row = y;
                    Tweens.Delay(_fx, BeadFlight, () => _picture.Shimmer(row));
                }
            }

            _lowestRow = lowest < 0 ? 0 : lowest;
        }

        private void Present(StepResult step)
        {
            var changed = new HashSet<int>();
            var rush = _tempo.Rush;
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

                // Золото засчитывается, если петля связана во время комбо.
                var gold = _game.IsGold(x, y);
                var caught = gold && _tempo.Active;

                // Нитка-клубочек летит от клубка к петле по дуге с хвостом; в «Разгоне» светится и крупнее.
                var beadColor = rush ? Color.Lerp(color, Color.white, 0.3f) : color;
                var beadSize = Mathf.Max(10f, _slotSpool * 0.28f) * (rush ? 1.25f : 1f);
                Fx.Bead(_fx, from, to, beadColor, beadSize, BeadFlight, () =>
                {
                    if (_picture.Land(x, y))
                    {
                        Fx.Sparkle(_fx, to, rush ? RushColor : color, rush ? 5 : 3, rush ? 24f : 16f);
                        Sfx.Play(Sound.Stitch, 0.5f);
                    }

                    if (gold)
                    {
                        OnGold(x, y, caught);
                    }

                    RefreshProgress();
                });

                if (_tempo.OnStitch())
                {
                    Fx.FloatText(_fx, from, "+1", Palette.CoinShadow, 16f);
                    Sfx.Play(Sound.Coin, 0.25f);
                }

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
                    var at = Ui.WorldCenter(slot.View.Root);
                    if (done.Leftover > 0)
                    {
                        // Лишняя пряжа: цвет довязан, клубок уходит недовязанным.
                        Fx.Poof(_fx, at, Palette.Ink3, _slotSpool);
                        Fx.FloatText(_fx, at, "лишняя пряжа", Palette.Ink2, 14f);
                    }
                    else
                    {
                        Fx.Poof(_fx, at, slot.Color >= 0 ? _colors[slot.Color] : Palette.Coin, _slotSpool * 1.3f);
                    }
                }

                Sfx.Play(Sound.SpoolDone, done.Leftover > 0 ? 0.35f : 0.7f);
                Haptics.Play(Haptic.Tap);
            }

            if (step.Stitches.Count > 0 || step.Completed.Count > 0)
            {
                SyncLoom();
            }

            if (_game.Band > 0 && changed.Count > 0)
            {
                // Натяжение: сдвинулся нижний фронт — у других столбцов могли включиться фронты.
                _picture.RefreshColumns();
                MaybeTensionTip();
            }
            else
            {
                foreach (var x in changed)
                {
                    _picture.RefreshColumn(x);
                }
            }

            if (step.Stitches.Count > 0)
            {
                _idle = 0;
                UpdateFieldIce();
                ShimmerClosedRows();
            }

            // Цвет довязан — клубки этого цвета на поле стали лишними: показать это.
            var finished = false;
            foreach (var stitch in step.Stitches)
            {
                if (_game.LeftOf(stitch.Color) == 0 && _finishedColors.Add(stitch.Color))
                {
                    finished = true;
                    CelebrateColor(stitch.Color);
                }
            }

            if (step.Thawed.Count > 0 || finished)
            {
                RefreshField();
            }

            foreach (var id in step.Thawed)
            {
                var view = FindFieldView(id);
                if (view != null)
                {
                    Tweens.Punch(view.Root, 0.25f, 0.3f);
                    Fx.Sparkle(_fx, Ui.WorldCenter(view.Root), IceBlue, 8, 30f);
                }

                Sfx.Play(Sound.Unlock, 0.7f);
            }
        }

        /// <summary>
        /// Цвет довязан: когда долетят последние нитки, его клетки пробегает волна, над ними — «Цвет готов!»,
        /// звук и ощутимый отклик. Последний цвет не празднуем отдельно — там сразу победная волна.
        /// </summary>
        private void CelebrateColor(int color)
        {
            if (_game.Status == GameStatus.Won)
            {
                return;
            }

            Tweens.Delay(Root, BeadFlight + 0.05f, () =>
            {
                if (_ended)
                {
                    return;
                }

                var at = _picture.CelebrateColor(color);
                if (at == null)
                {
                    return;
                }

                Fx.FloatText(_fx, at.Value, "Цвет готов!", Color.Lerp(_colors[color], Palette.Ink, 0.45f), 22f);
                Fx.Sparkle(_fx, at.Value, _colors[color], 10, 50f);
                Sfx.Play(Sound.Star, 0.55f);
                Haptics.Play(Haptic.Medium);
            });
        }

        /// <summary>Числа на льду тают вместе с петлями — без перестройки поля.</summary>
        private void UpdateFieldIce()
        {
            foreach (var f in _field)
            {
                var column = _game.Column(f.Column);
                if (f.Depth < column.Count && column[f.Depth].Id == f.SpoolId)
                {
                    f.View.SetIce(column[f.Depth].Ice);
                }
            }
        }

        private void OnGold(int x, int y, bool caught)
        {
            // Нитка могла долететь уже после отмены хода, а петля — засчитаться раньше: считаем каждую один раз.
            var key = y * _game.Width + x;
            if (!_game.IsKnitted(x, y) || _goldDone.ContainsKey(key))
            {
                return;
            }

            _goldDone[key] = caught;
            _picture.ResolveGold(x, y, caught);
            var at = _picture.CellWorld(x, y);
            if (!caught)
            {
                Fx.FloatText(_fx, at, "мимо", Palette.Ink3, 14f);
                return;
            }

            _goldCaught++;
            _goldCoins += _goldPerLoop;
            Fx.Sparkle(_fx, at, Palette.Coin, 10, 34f);
            Fx.FloatText(_fx, at, "+" + _goldPerLoop, Palette.CoinShadow, 20f);
            Sfx.Play(Sound.Coin, 0.7f);
            Haptics.Play(Haptic.Soft);
            RefreshGoals();
            if (_goldGoal != null)
            {
                Tweens.Punch(_goldGoal.transform.parent, 0.15f, 0.25f);
            }
        }

        // ---------- Комбо за темп ----------

        private void OnComboGrow(int combo)
        {
            if (combo < TempoCombo.ActiveFrom)
            {
                return;
            }

            var at = Ui.WorldCenter(_loomBg) + new Vector3(0, 46f * _fx.lossyScale.y, 0);
            if (combo == TempoCombo.RushFrom)
            {
                Fx.FloatText(_fx, at, "Разгон!", RushColor, 30f);
                Sfx.Play(Sound.Combo, 0.9f);
                Haptics.Play(Haptic.Medium);
                Tweens.Punch(_loomBg, 0.06f, 0.3f);
                Fx.Sparkle(_fx, Ui.WorldCenter(_loomBg), RushColor, 12, 140f);
            }
            else
            {
                // Без всплывающих «Комбо ×N» на каждый тап: серию показывает плашка над станком.
                Sfx.Play(Sound.Combo, 0.45f);
            }

            Tweens.Punch(_comboChip, 0.18f, 0.22f);
        }

        private void OnComboBurned(bool wasRush)
        {
            if (wasRush)
            {
                var at = Ui.WorldCenter(_loomBg) + new Vector3(0, 46f * _fx.lossyScale.y, 0);
                Fx.FloatText(_fx, at, "Разгон погас", Palette.Ink3, 16f);
            }
        }

        /// <summary>Плашка комбо, жар «Разгона» и подсветка станка — каждый кадр.</summary>
        private void UpdateCombo(float dt)
        {
            var active = _tempo.Active;
            var targetAlpha = active ? 1f : 0f;
            _comboGroup.alpha = Mathf.MoveTowards(_comboGroup.alpha, targetAlpha, dt * 6f);
            _loomTitle.enabled = _comboGroup.alpha < 0.5f;
            if (active)
            {
                var rush = _tempo.Rush;
                _comboText.text = rush ? $"Разгон ×{_tempo.Combo}" : $"Комбо ×{_tempo.Combo}";
                _comboBack.color = rush ? RushColor : Palette.Link;
                _comboBolt.enabled = rush;
                _comboText.rectTransform.anchoredPosition = new Vector2(rush ? 24f : 10f, 0f);
                _comboHeat.sizeDelta = new Vector2((ComboW - 20f) * _tempo.Heat, 3f);
            }

            if (_tempo.Rush)
            {
                _rushClock += dt;
                var pulse = 0.55f + 0.45f * Ease.Bump(Mathf.Repeat(_rushClock / 0.6f, 1f));
                _rushGlow.color = Palette.WithAlpha(RushColor, pulse);
                _loomBgImage.color = Color.Lerp(Palette.LoomBg, Palette.CoinSoft, 0.8f);
            }
            else
            {
                _rushClock = 0;
                _rushGlow.color = Color.clear;
                _loomBgImage.color = Palette.LoomBg;
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
            if (_ended || _failShown || _finishing || App.HasPopups)
            {
                return;
            }

            _idle = 0;
            // Подсказку о натяжении не гасим мгновенным тапом, но и не держим: через секунду любой ход её убирает.
            if (!_tipKeep || Time.unscaledTime - _tipShownAt > 1.2f)
            {
                HideTip();
            }

            HideHint();
            // Без ножниц работает только передний клубок. Тап по клубку из глубины раньше отправлял
            // передний — неожиданный ход, лишний тап к цели и, бывает, затор. Теперь — только подсказка.
            if (!_scissors && depth > 0)
            {
                var view = _field.FirstOrDefault(f => f.Column == column && f.Depth == depth);
                if (view != null)
                {
                    Tweens.Shake(view.View.Root, 5f, 0.25f);
                }

                Sfx.Play(Sound.Deny, 0.6f);
                Haptics.Play(Haptic.Soft);
                ToastOnce("depth", "Сначала уходит передний клубок");
                return;
            }

            var result = _scissors ? _game.PullSpool(column, depth) : _game.TapColumn(column);
            if (!result.Accepted)
            {
                Deny(column, depth, result.Reason);
                return;
            }

            if (_scissors)
            {
                PayScissors();
                _scissors = false;
                HideScissorsBanner();
            }

            RefreshGoals();

            if (result.Kind == MoveKind.KnotUntied)
            {
                Sfx.Play(Sound.Knot);
                Haptics.Play(Haptic.Tap);
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
            Haptics.Play(Haptic.Tap);
            OnComboGrow(_tempo.OnPlaced());
            var flights = new List<(Placement placed, Vector3 from)>();
            foreach (var placed in result.Placed)
            {
                var source = _field.FirstOrDefault(f => f.Column == placed.Column && f.Depth == placed.Depth);
                flights.Add((placed, source != null ? Ui.WorldCenter(source.View.Root) : Ui.WorldCenter(_fieldContent)));
                _arriving.Add(placed.Spool.Id);
            }

            RefreshField();
            RefreshBoosters();

            // Клубок с галочкой: его цвет уже довязан. Он съездит на станок и уйдёт — это не ошибка,
            // а способ расчистить колонку. Без пояснения исчезновение клубка выглядело багом.
            if (result.Placed.Any(p => _game.IsSurplus(p.Spool)))
            {
                ToastOnce("surplus", "Цвет довязан: клубок сразу уйдёт со станка");
            }

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

            // Полёт длится ровно столько, сколько станок «разогревает» клубок: в «Разгоне» шаги вдвое короче,
            // и раньше клубок начинал вязать из пустого слота, ещё не долетев.
            var flight = LoomGame.WarmupSteps * (_tempo.Rush ? StepInterval / RushSpeed : StepInterval);
            foreach (var (placed, from) in flights)
            {
                var spool = placed.Spool;
                var flyer = SpoolView.Create(_fx, _spoolSize);
                flyer.Bind(spool, _colors[spool.Color], true);
                var target = SlotTarget(placed.Slot);
                var scale = SpoolSizeAt(placed.Slot) / _spoolSize;
                Tweens.Run(flyer.Root, flight, t =>
                {
                    flyer.Root.localScale = Vector3.one * Mathf.Lerp(1f, scale, t) * (1f + 0.15f * Ease.Bump(t));
                }, null, null);
                Tweens.Move(flyer.Root, from, target, flight, Ease.OutCubic, () =>
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

        /// <summary>Тап по слоту станка: с ножницами — срезать клубок, без них — подсказка про чужую пряжу.</summary>
        private void OnSlotTap(int slot)
        {
            if (_ended || _failShown || _finishing || App.HasPopups || slot >= _game.Slots.Count)
            {
                return;
            }

            var spool = _game.Slots[slot];
            if (spool == null)
            {
                return;
            }

            if (!_scissors)
            {
                if (_game.IsForeign(spool))
                {
                    ToastOnce("foreign-loom", "Чужая пряжа: срежь её ножницами");
                }
                else if (spool.Warmup == 0 && !_game.HasWork(spool) && !_game.IsSurplus(spool))
                {
                    // Бледный клубок на станке: почему он не вяжет.
                    var held = Enumerable.Range(0, _game.Width).Any(x => _game.IsHeld(x) && _game.ColorAt(x, _game.FrontY(x)) == spool.Color);
                    ToastOnce("waiting", held
                        ? "Ждёт: сначала довяжи клетки ниже пунктира"
                        : "Ждёт своей очереди: его цвета пока нет внизу");
                }

                return;
            }

            var head = _game.Slots.ToList().IndexOf(spool);
            var view = _slotViews[head];
            var at = view != null ? Ui.WorldCenter(view.View.Root) : Ui.WorldCenter(_slotFrames[slot]);
            var back = _game.ReturnsToField(spool);
            if (!_game.CutFromLoom(slot))
            {
                return;
            }

            PayScissors();
            _scissors = false;
            HideScissorsBanner();
            Fx.Poof(_fx, at, Palette.Ink3, _slotSpool * 1.4f);
            if (back)
            {
                // Нужный рисунку клубок не пропадает: он вернулся вперёд своей колонки.
                App.Toast("Клубок вернулся на поле: его пряжа ещё нужна рисунку");
            }
            Sfx.Play(Sound.Knot);
            Haptics.Play(Haptic.Medium);
            SyncLoom();
            RefreshField();
            RefreshBoosters();
            if (back)
            {
                PunchField(spool.Id);
            }

            _coins.text = App.Progress.Coins.ToString();
            CheckStatus();
        }

        /// <summary>
        /// Пояснение показываем один раз за уровень на каждый случай: дальше отказ — просто встряска
        /// клубка и звук. Иначе за уровень набегал десяток одинаковых подсказок внизу экрана.
        /// </summary>
        private void ToastOnce(string key, string text)
        {
            if (_told.Add(key))
            {
                App.Toast(text);
            }
        }

        private void Deny(int column, int depth, RejectReason reason)
        {
            Sfx.Play(Sound.Deny);
            Haptics.Play(Haptic.Soft);
            var view = _field.FirstOrDefault(f => f.Column == column && f.Depth == depth);
            if (view != null)
            {
                Tweens.Shake(view.View.Root, 6f, 0.3f);
            }

            switch (reason)
            {
                case RejectReason.Locked:
                    ToastOnce("locked", "Под замком: сначала поставь на станок клубок с ключом");
                    break;
                case RejectReason.LinkNotReady:
                    ToastOnce("link", LinkPartnerText(column, depth));
                    PunchLinkPartner(column, depth);
                    break;
                case RejectReason.Frozen:
                    var ice = _game.Column(column)[0].Ice;
                    ToastOnce("ice", $"Во льду: оттает через {ice} {Plural(ice, "петлю", "петли", "петель")} этого цвета");
                    break;
                case RejectReason.NoFreeSlot:
                    var spool = depth < _game.Column(column).Count ? _game.Column(column)[depth] : null;
                    ToastOnce("noslot", spool != null && spool.Link != 0 && !_scissors
                        ? "Связанным клубкам нужно 2 свободных слота"
                        : spool != null && spool.Thick
                            ? "Толстому клубку нужно 2 свободных места"
                            : "Нет места: подожди, пока клубок довяжет");
                    Tweens.Shake(_loomBg, 5f, 0.25f);
                    break;
            }
        }

        // ---------- Победа и поражение ----------

        /// <summary>
        /// Финиш: всё нужное уже на станке, победа неизбежна. Игроку незачем ждать и нечего решать:
        /// поле и бустеры затихают и не нажимаются, таймер останавливается, моль улетает, станок
        /// плавно разгоняется до трёх с лишним раз. Если вязать почти нечего, тихо: без надписи.
        /// </summary>
        private void StartFinish()
        {
            _finishing = true;
            _idle = 0;
            HideHint();
            HideTip();
            SetScissors(false);
            RefreshTimer();
            MothLeave();

            var left = _game.TotalStitches - _game.KnittedCount;
            if (left >= 8)
            {
                Fx.FloatText(_fx, Ui.WorldCenter(_loomBg) + new Vector3(0f, 30f * _fx.lossyScale.y, 0f), "Финиш!", Palette.Primary, 24f);
                Sfx.Play(Sound.Combo, 0.6f);
                Haptics.Play(Haptic.Medium);
            }

            var field = _fieldContent.parent.GetComponent<CanvasGroup>();
            if (field == null)
            {
                field = _fieldContent.parent.gameObject.AddComponent<CanvasGroup>();
            }

            field.blocksRaycasts = false;
            Tweens.Fade(field, field.alpha, 0.45f, 0.35f);
            foreach (var button in _boosters.Values)
            {
                button.Dimmed = true;
            }

            RefreshBoosters();
            Tweens.Run(Root, 1.1f, t => _finishSpeed = Mathf.Lerp(1f, 3.4f, t), Ease.InOutCubic);
        }

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
            MothLeave();
            Tweens.Delay(Root, BeadFlight + 0.1f, () =>
            {
                _picture.Refresh();
                RefreshProgress();
                Sfx.Play(Sound.Unlock);
                Haptics.Play(Haptic.Success);
                _picture.Wave(() =>
                {
                    Tweens.Punch(_pictureCard, 0.06f, 0.4f);
                    Fx.Poof(_fx, Ui.WorldCenter(_pictureCard), Palette.Coin, _pictureCard.sizeDelta.x * 0.9f);
                    var stars = Economy.Stars(_boostersUsed, _rescues);
                    Analytics.Event(_run.Pattern ? "pattern_win" : "level_win", LevelParams(
                        ("stars", stars), ("boosters", _boostersUsed), ("rescues", _rescues),
                        ("best_combo", _tempo.Best), ("rushes", _tempo.RushCount), ("gold", _goldCaught), ("auto_finish", _finishing)));
                    var stats = new LevelStats(_tempo.RushCoins, _goldCoins, _goldCaught, _level.gold.Length, _game.Taps,
                        _tempo.Best, _boostersUsed > 0 || _rescues > 0, _game.TotalStitches, _tempo.RushCount);
                    Tweens.Delay(Root, 0.45f, () =>
                    {
                        if (_run.Pattern)
                        {
                            App.OnPatternWon(_level, stars, stats);
                        }
                        else
                        {
                            App.OnLevelWon(_level, stars, stats);
                        }
                    });
                });
            });
        }

        private void OnJam()
        {
            _failShown = true;
            SetScissors(false);
            HideHint();
            HideTip();
            _tempo.Break();
            _hitStop = 0.12f;
            Sfx.Play(Sound.Jam);
            Haptics.Play(Haptic.Warning);
            Tweens.Shake(_loomBg, 10f, 0.4f);
            Tweens.Shake(Root, 4f, 0.25f);
            foreach (var outline in _slotOutlines)
            {
                outline.enabled = true;
                var image = outline;
                Tweens.Run(image, 0.5f, t => image.color = Color.Lerp(Palette.Danger, Palette.SlotEmpty, t));
            }

            // Сначала причина прямо на поле (кольца и короткая выноска), потом окно. Тап пропускает.
            Tweens.Delay(Root, 0.45f, () =>
            {
                if (ExplainJam())
                {
                    Interlude(1.9f, () =>
                    {
                        HideTip();
                        ShowFail(FailKind.Jam);
                    });
                }
                else
                {
                    Tweens.Delay(Root, 0.15f, () => ShowFail(FailKind.Jam));
                }
            });
        }

        /// <summary>
        /// Затор: показать на поле, что случилось. Красное кольцо — где проблема, зелёное — что поможет,
        /// выноска в несколько слов. Один фокус, без длинных текстов: подробный совет — в окне.
        /// false — показать нечего (окно сразу).
        /// </summary>
        private bool ExplainJam()
        {
            ClearJamMarks();
            RectTransform? target = null;
            var text = "";
            var below = false;
            switch (_game.Advice())
            {
                case JamAdvice.Blocked:
                    for (var c = 0; c < _game.ColumnCount && target == null; c++)
                    {
                        var column = _game.Column(c);
                        var view = FieldView(c, 0);
                        if (column.Count == 0 || view == null)
                        {
                            continue;
                        }

                        var spool = column[0];
                        if (spool.Locked)
                        {
                            var problem = JamMark(view, Palette.Danger);
                            var key = _field.FirstOrDefault(f => f.Depth < _game.Column(f.Column).Count && _game.Column(f.Column)[f.Depth].IsKey);
                            target = key != null ? JamMark(key.View.Root, Palette.Primary) : problem;
                            text = key != null ? "Ключ от замка лежит глубже" : "Замок откроет клубок с ключом";
                        }
                        else if (spool.Frozen)
                        {
                            target = JamMark(view, Palette.Danger);
                            text = "Лёд тает от петель этого цвета";
                        }
                        else if (spool.Link != 0)
                        {
                            target = JamMark(view, Palette.Danger);
                            var (pc, pd) = FindLinkPartner(c, 0);
                            var partner = pc >= 0 ? FieldView(pc, pd) : null;
                            if (partner != null)
                            {
                                JamMark(partner, Palette.Primary);
                            }

                            text = "Пара этого клубка ещё не впереди";
                        }
                    }

                    break;

                case JamAdvice.ForeignYarn:
                    for (var i = 0; i < _slotViews.Length && target == null; i++)
                    {
                        var slot = _slotViews[i];
                        var spool = i < _game.Slots.Count ? _game.Slots[i] : null;
                        if (slot != null && spool != null && _game.IsForeign(spool))
                        {
                            target = JamMark(slot.View.Root, Palette.Danger);
                            text = "Эта пряжа картинке не нужна";
                        }
                    }

                    break;

                case JamAdvice.Tension:
                    var lowest = _game.LowestFront;
                    for (var x = 0; x < _game.Width && _jamMarks.Count < 3; x++)
                    {
                        if (lowest >= 0 && _game.FrontY(x) == lowest && !_game.IsHeld(x))
                        {
                            var mark = JamMark(_picture.CellRect(x, lowest), Palette.Primary);
                            target ??= mark;
                        }
                    }

                    text = "Сначала довяжи этот ряд";
                    below = true;
                    break;

                case JamAdvice.Knots:
                    for (var c = 0; c < _game.ColumnCount && target == null; c++)
                    {
                        var column = _game.Column(c);
                        var view = FieldView(c, 0);
                        if (column.Count > 0 && column[0].Knots > 0 && view != null)
                        {
                            target = JamMark(view, Palette.Danger);
                            text = "Узелки лучше развязывать заранее";
                        }
                    }

                    break;

                default:
                    // Станок забит не теми цветами: клубки на станке — красным, нужные картинке петли — зелёным.
                    foreach (var slot in _slotViews)
                    {
                        if (slot != null)
                        {
                            JamMark(slot.View.Root, Palette.Danger);
                        }
                    }

                    var shown = new HashSet<int>();
                    for (var x = 0; x < _game.Width && shown.Count < 3; x++)
                    {
                        var y = _game.ActiveFrontY(x);
                        if (y >= 0 && shown.Add(_game.ColorAt(x, y)))
                        {
                            JamMark(_picture.CellRect(x, y), Palette.Primary);
                        }
                    }

                    if (_jamMarks.Count > 0)
                    {
                        target = _loomBg;
                        text = "Станок занят, а картинке нужны другие цвета";
                    }

                    break;
            }

            if (target == null)
            {
                ClearJamMarks();
                return false;
            }

            ShowTip(text, target, below);
            _tipKeep = true;
            return true;
        }

        private RectTransform? FieldView(int column, int depth) =>
            _field.FirstOrDefault(f => f.Column == column && f.Depth == depth)?.View.Root;

        /// <summary>
        /// Пульсирующее кольцо поверх цели и мягкое свечение под ним. Появляется с лёгким «прыжком»,
        /// дальше дышит; живёт до спасения или до перезапуска. Возвращает саму цель — к ней крепится выноска.
        /// </summary>
        private RectTransform JamMark(RectTransform target, Color color)
        {
            var size = Mathf.Max(target.rect.width, target.rect.height) + 12f;
            var root = Ui.Node(_fx, "JamMark");
            root.sizeDelta = new Vector2(size, size);
            root.position = Ui.WorldCenter(target);
            var glow = Ui.Image(root, "Glow", Art.Glow, Palette.WithAlpha(color, 0.3f));
            Ui.PlaceCenter(glow.rectTransform, size / 2f, size / 2f, size * 1.7f, size * 1.7f);
            glow.rectTransform.anchorMin = glow.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            glow.rectTransform.anchoredPosition = Vector2.zero;
            var ring = Ui.Image(root, "Ring", Art.Ring, color);
            Ui.Stretch(ring.rectTransform);
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            var delay = 0.06f * _jamMarks.Count;
            root.localScale = Vector3.zero;
            Tweens.Scale(root, 0.4f, 1f, 0.32f, Ease.OutBack, null, delay);
            Tweens.Run(root, 0.9f, t =>
            {
                var bump = Ease.Bump(t);
                root.localScale = Vector3.one * (1f + 0.08f * bump);
                ring.color = Palette.WithAlpha(color, 0.75f + 0.25f * bump);
                glow.color = Palette.WithAlpha(color, 0.18f + 0.17f * bump);
            }, null, null, delay + 0.32f, true);
            _jamMarks.Add(root);
            return target;
        }

        private void ClearJamMarks()
        {
            foreach (var mark in _jamMarks)
            {
                if (mark == null)
                {
                    continue;
                }

                var group = mark.GetComponent<CanvasGroup>();
                var node = mark;
                Tweens.Fade(group, group.alpha, 0f, 0.2f, () => Object.Destroy(node.gameObject));
            }

            _jamMarks.Clear();
        }

        /// <summary>
        /// Короткая пауза перед окном (объяснение затора, моль ест полотно). Тап в любом месте её
        /// пропускает: кто понял сразу, не ждёт. then вызывается ровно один раз.
        /// </summary>
        private void Interlude(float seconds, System.Action then)
        {
            var overlay = Ui.Stretch(Ui.Node(Root, "Interlude"));
            var hit = overlay.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            hit.raycastTarget = true;
            var done = false;
            void Finish()
            {
                if (done)
                {
                    return;
                }

                done = true;
                Object.Destroy(overlay.gameObject);
                then();
            }

            var tap = overlay.gameObject.AddComponent<Tap>();
            tap.ClickSound = false;
            tap.PressFeedback = false;
            tap.OnClick(Finish);
            Tweens.Delay(overlay, seconds, Finish);
        }

        /// <summary>
        /// Время вышло: моль слетает с рамки на ближнюю связанную петлю, трижды кусает — дырка;
        /// перебегает на соседнюю, кусает дважды — вторая дырка. Потом окно. Тап пропускает сцену.
        /// </summary>
        private void OnTimeUp()
        {
            _failShown = true;
            SetScissors(false);
            HideHint();
            HideTip();
            _tempo.Break();
            if (_mothEdge != null)
            {
                _mothEdge.color = Palette.WithAlpha(Palette.Danger, 0.35f);
            }

            var bites = MothBites();
            if (_moth == null || _mothLayer == null || bites.Count == 0)
            {
                Sfx.Play(Sound.Jam);
                Haptics.Play(Haptic.Warning);
                Tweens.Delay(Root, 0.45f, () => ShowFail(FailKind.TimeUp));
                return;
            }

            _mothEating = true;
            _moth.Show();
            _moth.Agitation = 1f;
            Haptics.Play(Haptic.Medium);
            Sfx.Play(Sound.Deny, 0.7f);
            Interlude(2.6f, () =>
            {
                _mothEating = false;
                Sfx.Play(Sound.Jam);
                Haptics.Play(Haptic.Warning);
                ShowFail(FailKind.TimeUp);
            });
            MothEat(bites, 0);
        }

        /// <summary>Моль садится головой на петлю index и кусает; на последнем укусе — дырка.</summary>
        private void MothEat(List<(int X, int Y)> bites, int index)
        {
            if (!_mothEating || index >= bites.Count || _moth == null || _mothLayer == null)
            {
                return;
            }

            var (x, y) = bites[index];
            var cell = (Vector2)_mothLayer.InverseTransformPoint(_picture.CellWorld(x, y));
            var approach = cell - _moth.Position;
            var facing = approach.sqrMagnitude > 1f ? MothView.Facing(approach) : 180f;
            var forward = new Vector2(-Mathf.Sin(facing * Mathf.Deg2Rad), Mathf.Cos(facing * Mathf.Deg2Rad));
            var seat = cell - forward * _moth.Span * 0.3f;
            var chews = index == 0 ? 3 : 2;
            var moth = _moth;
            moth.MoveTo(seat, facing, () => moth.Chew(chews, bite =>
            {
                if (!_mothEating)
                {
                    return;
                }

                var world = _picture.CellWorld(x, y);
                var color = _colors[_game.ColorAt(x, y)];
                Fx.Sparkle(_fx, world, color, 2, 10f);
                Sfx.Play(Sound.Knot, 0.45f);
                Haptics.Play(Haptic.Soft);
                if (bite == chews)
                {
                    _picture.ShowHole(x, y);
                    Fx.Poof(_fx, world, color, moth.Span);
                    Haptics.Play(Haptic.Medium);
                }
            }, () => Tweens.Delay(Root, 0.12f, () => MothEat(bites, index + 1))));
        }

        /// <summary>
        /// Окно затора или моли. Спасение предлагается, только если оно правда поможет:
        /// лишний слот — когда после него есть ход, иначе ножницы (достать клубок изо льда или
        /// из-под замка, срезать чужую пряжу со станка). Если не поможет ничего — честно «заново».
        /// </summary>
        private void ShowFail(FailKind kind)
        {
            BoosterType? rescue = BoosterType.Time;
            var reason = "Время вышло. Отгони моль и довяжи картинку";
            if (kind == FailKind.Jam)
            {
                // Затор при свободных местах: станок пуст, но все передние клубки заперты.
                var blocked = _game.FreeSlots > 0;
                if (_game.SlotWouldHelp)
                {
                    rescue = BoosterType.Slot;
                    reason = "Слоты заняты, а нужного цвета на станке нет";
                }
                else if (_game.ScissorsWouldHelp && App.Progress.IsBoosterOpen(BoosterType.Scissors, BoosterLevel))
                {
                    rescue = BoosterType.Scissors;
                    reason = blocked
                        ? "Места на станке есть, но передние клубки заблокированы: под замком, во льду или без пары. Ножницы достанут любой клубок с поля"
                        : _picture.TensionHolding
                        ? "Столбики выше пунктира ждут нижний ряд, а его цвета на станке нет. Ножницы срежут лишний клубок со станка или достанут нужный с поля"
                        : "Станок занят, а картинка ждёт другой цвет. Ножницы срежут лишний клубок со станка или достанут нужный с поля";
                }
                else
                {
                    rescue = null;
                    reason = blocked
                        ? "Места на станке есть, но передние клубки заблокированы. Попробуй ещё раз"
                        : _game.Slots.Count >= LoomGame.MaxSlots
                        ? "Станок заполнен до предела. Попробуй ещё раз"
                        : "Нужной пряжи больше нет. Попробуй ещё раз";
                }
            }

            var percent = Mathf.FloorToInt(100f * _game.KnittedCount / _game.TotalStitches);

            // Каких цветов ждут столбцы, которые можно вязать прямо сейчас: игрок видит, чего не хватило.
            var needed = kind == FailKind.Jam && rescue != null
                ? _game.FrontColors().OrderBy(c => c).Select(c => _colors[c]).ToArray()
                : null;
            // Совет на следующую попытку: приём, которым этот затор можно было обойти.
            var advice = kind == FailKind.Jam ? AdviceText(_game.Advice()) : null;
            Analytics.Event("level_fail", LevelParams(
                ("reason", kind == FailKind.Jam ? "jam" : "time"),
                ("rescue_offered", rescue?.ToString() ?? "none"),
                ("jam_advice", kind == FailKind.Jam ? _game.Advice().ToString() : "")));
            App.ShowPopup(new FailPopup(App, kind, rescue, reason, percent, Rescue, Restart, LeaveHome,
                rescue == BoosterType.Scissors && _run.FreeScissors > 0, StreakWarning(), needed, advice));
        }

        private static string AdviceText(JamAdvice advice)
        {
            switch (advice)
            {
                case JamAdvice.Blocked:
                    return "Совет: не закапывай клубок с ключом, пару связанного и клубки цвета, от которого тает лёд. Без них передние клубки не сдвинуть";
                case JamAdvice.ForeignYarn:
                    return "Совет: клубки с узором ткани картинке не нужны. Не бери их, они только занимают слот";
                case JamAdvice.Tension:
                    return "Совет: сначала довязывай нижний ряд. Пока он отстаёт, столбики выше пунктира ждут, а их клубки зря занимают станок";
                case JamAdvice.Knots:
                    return "Совет: развязывай узелки заранее, пока станок вяжет. Тап по узелку не занимает слот";
                default:
                    return "Совет: бери сначала короткие клубки, число на клубке. Они быстрее освобождают станок";
            }
        }

        private void Rescue(BoosterType type)
        {
            _failShown = false;
            _mothEating = false;
            ClearJamMarks();
            Analytics.Event("rescue", LevelParams(("type", type.ToString())));
            if (type == BoosterType.Scissors)
            {
                // Ножницы оплачиваются, когда ими режут; затор снимается этим ходом.
                _scissorsRescue = true;
                SetScissors(true);
                return;
            }

            _rescues++;
            if (type == BoosterType.Slot)
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
                _lastTickSecond = -1;
                if (_mothEdge != null)
                {
                    _mothEdge.color = Color.clear;
                }

                // Моль спугнули: вздрагивает и улетает на своё место, дырки затягиваются.
                if (_picture.HasHoles)
                {
                    _picture.MendHoles(_fx);
                    Sfx.Play(Sound.Unlock);
                }

                if (_moth != null && _moth.Shown && _mothLayer != null)
                {
                    _moth.Startle();
                    var fraction = Mathf.Clamp01(_game.TimeLeft / _game.TimeLimit);
                    var (at, facing) = MothPerch(fraction);
                    _mothSpot = at;
                    _moth.Agitation = Mathf.Clamp01(Mathf.InverseLerp(0.45f, 0.05f, fraction));
                    _moth.FlyTo(at, facing);
                }

                RefreshTimer();
                RefreshAll();
            }

            if (_game.Status != GameStatus.Playing)
            {
                AfterStatus(_game.Status);
            }
        }

        /// <summary>Общие параметры событий уровня: какой уровень, какая попытка, сколько длится, сколько связано.</summary>
        private (string, object)[] LevelParams(params (string, object)[] extra)
        {
            var list = new List<(string, object)>
            {
                ("level", _run.Pattern ? 0 : _level.id),
                ("pattern", _run.Pattern),
                ("attempt", _run.Attempt),
                ("seconds", Mathf.RoundToInt(Time.realtimeSinceStartup - _run.StartedAt)),
                ("progress", Mathf.FloorToInt(100f * _game.KnittedCount / Mathf.Max(1, _game.TotalStitches))),
                ("taps", _game.Taps),
            };
            list.AddRange(extra);
            return list.ToArray();
        }

        private void TrackBooster(BoosterType type, bool free) =>
            Analytics.Event("booster", LevelParams(("type", type.ToString()), ("free", free)));

        private void Restart()
        {
            if (_run.Pattern)
            {
                App.StartPattern();
            }
            else
            {
                App.StartLevel(_level.id, restart: true);
            }
        }

        /// <summary>Уйти домой посреди уровня — как сдаться: серия побед обрывается (повтор её не трогает).</summary>
        private void LeaveHome()
        {
            if (!_ended)
            {
                Analytics.Event("level_quit", LevelParams());
            }

            if (!_run.Pattern && !_ended && _game.Taps > 0 && !App.Progress.IsCompleted(_level.id))
            {
                App.Progress.BreakStreak();
                App.Save();
            }

            App.ShowHome();
        }

        // ---------- Бустеры ----------

        private void OnBooster(BoosterType type)
        {
            if (_ended || _failShown || _finishing)
            {
                return;
            }

            if (!App.Progress.IsBoosterOpen(type, BoosterLevel))
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
                    else if (_run.FreeScissors > 0 || App.Progress.CanAfford(type))
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
                    else if (_run.FreeUndo == 0 && App.Progress.Pay(type) == Payment.NotEnough)
                    {
                        NotEnough(type);
                    }
                    else
                    {
                        // Подарок серии — сначала он, и звезда за него не снимается.
                        if (_run.FreeUndo > 0)
                        {
                            _run.FreeUndo--;
                        }
                        else
                        {
                            _boostersUsed++;
                        }

                        App.Save();
                        TrackBooster(BoosterType.Undo, free: false);
                        _game.Undo();
                        _tempo.Break();
                        _finishedColors.Clear();
                        RestoreUndoneGold();
                        Sfx.Play(Sound.Pick);
                        RefreshAll();
                        CheckStatus();
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
                        TrackBooster(BoosterType.Slot, free: false);
                        _game.AddSlot();
                        RebuildSlots();
                        RefreshAll();
                        Sfx.Play(Sound.Unlock);
                        Tweens.Scale(_slotFrames[_slotFrames.Length - 1], 0.4f, 1f, 0.3f);
                        CheckStatus();
                    }

                    break;
            }

            _coins.text = App.Progress.Coins.ToString();
        }

        /// <summary>Отмена развязала золотые петли: они снова в игре, а засчитанное за них снимается.</summary>
        private void RestoreUndoneGold()
        {
            foreach (var key in _goldDone.Keys.ToList())
            {
                int x = key % _game.Width, y = key / _game.Width;
                if (!_game.IsKnitted(x, y))
                {
                    _goldDone.Remove(key);
                    _picture.RestoreGold(x, y);
                }
            }

            _goldCaught = _goldDone.Values.Count(caught => caught);
            _goldCoins = _goldCaught * _goldPerLoop;
            RefreshGoals();
        }

        /// <summary>
        /// Отмена, ножницы или новый слот меняют состояние вне шага вязки: если после них станок
        /// встал (или картинка готова), показать это сразу, иначе экран «зависнет» без окна.
        /// </summary>
        private void CheckStatus()
        {
            if (_ended || _failShown)
            {
                return;
            }

            if (_game.Status == GameStatus.Won || _game.Status == GameStatus.Jammed)
            {
                AfterStatus(_game.Status);
            }
        }

        /// <summary>Ножницы сработали: сначала тратятся бесплатные от серии (без потери звезды), потом запас или монеты.</summary>
        private void PayScissors()
        {
            // Ножницы из окна затора — это спасение, как «+1 слот»: одна звезда, а не две.
            if (_scissorsRescue)
            {
                _scissorsRescue = false;
                _rescues++;
            }

            TrackBooster(BoosterType.Scissors, free: _run.FreeScissors > 0);
            if (_run.FreeScissors > 0)
            {
                _run.FreeScissors--;
            }
            else
            {
                App.Progress.Pay(BoosterType.Scissors);
                _boostersUsed++;
            }

            App.Save();
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

            if (!on)
            {
                // Передумал резать — спасение не засчитываем.
                _scissorsRescue = false;
            }

            _scissors = on;
            HideHint();
            if (on)
            {
                HideTip();
                _scissorsBanner = Ui.Card(Root, "ScissorsBanner", Pad, _fieldY - 50f, W - 2 * Pad, 40, 20, Palette.Primary, Palette.Primary, 0f);
                Ui.IconImage(_scissorsBanner, Icon.Scissors, Color.white, 24, 20, 20);
                Ui.Label(_scissorsBanner, "Выбери клубок на поле или на станке", 15, Color.white, 40, 0, W - 2 * Pad - 52, 40, true, TextAnchor.MiddleLeft);
                var group = _scissorsBanner.gameObject.AddComponent<CanvasGroup>();
                group.blocksRaycasts = false;
            }
            else
            {
                HideScissorsBanner();
            }

            RefreshField();
            RefreshBoosters();

            // Передумал резать, а станок стоит в заторе: вернуть окно затора.
            if (!on && !_failShown && !_ended && _game.Status == GameStatus.Jammed)
            {
                _failShown = true;
                ShowFail(FailKind.Jam);
            }
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
                // Пути нет (или перебор упёрся в предел): не перебирать заново каждый кадр — только после нового хода.
                _hintFailedAt = _game.Taps;
                _idle = 0;
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

        /// <summary>
        /// Подсказка уровня — лёгкая выноска рядом с тем, о чём она: над особым клубком на поле
        /// (с «?», узелком, замком, льдом…), под целью по золоту, над картинкой.
        /// Белая, небольшая, со стрелкой; не перехватывает тапы, исчезает сама или с первым ходом.
        /// Вернуть её можно тапом по плашке уровня.
        /// </summary>
        /// <summary>Подсказка уровня — в начале, пока не сделан первый тап.</summary>
        private void ShowTip()
        {
            if (_game.Taps > 0)
            {
                return;
            }

            ShowTip(_level.tip, null, false);
        }

        /// <summary>
        /// Натяжение впервые остановило столбик: объяснить правило прямо на пунктире. Один раз за игру;
        /// подсказка не гаснет от следующего тапа, чтобы её успели прочитать.
        /// </summary>
        private void MaybeTensionTip()
        {
            var line = _picture.TensionLine;
            if (line == null || _ended || _tip != null || App.HasPopups || !App.Progress.TakeTensionIntro())
            {
                return;
            }

            App.Save();
            ShowTip("Выше пунктира ждут, пока довяжут клетки внизу", line, false);
            _tipKeep = true;
        }

        private void ShowTip(string tipText, RectTransform? target, bool below)
        {
            if (_ended || _finishing || _tip != null || string.IsNullOrEmpty(tipText))
            {
                return;
            }

            _tipKeep = false;
            if (target == null)
            {
                (target, below) = TipTarget(tipText.ToLowerInvariant());
            }
            var maxW = Mathf.Min(W - 2 * Pad, 320f);
            var root = Ui.Node(Root, "Tip");
            _tip = root;

            var label = Ui.Label(root, tipText, 14, Palette.Ink, 40, 0, maxW - 52, 10, true, TextAnchor.MiddleLeft);
            label.lineSpacing = 0.9f;
            var textW = Mathf.Min(maxW - 52, Mathf.Max(label.preferredWidth, 60f));
            label.rectTransform.sizeDelta = new Vector2(textW, 10);
            var bubbleW = textW + 52f;
            var bubbleH = Mathf.Max(40f, label.preferredHeight + 18f);

            // Где цель на экране — в точках макета от левого верхнего угла экрана.
            float cx, top, bottom;
            if (target != null)
            {
                var corners = new Vector3[4];
                target.GetWorldCorners(corners);
                var a = Root.InverseTransformPoint(corners[0]);
                var b = Root.InverseTransformPoint(corners[2]);
                cx = (a.x + b.x) / 2f + W / 2f;
                top = H / 2f - b.y;
                bottom = H / 2f - a.y;
            }
            else
            {
                cx = W / 2f;
                top = bottom = _loomY + LoomTop + LoomH / 2f;
            }

            const float arrow = 9f;
            var x = Mathf.Clamp(cx - bubbleW / 2f, Pad, W - Pad - bubbleW);
            var y = target == null ? top - bubbleH / 2f : below ? bottom + arrow + 2f : top - bubbleH - arrow - 2f;
            Ui.Place(root, x, y, bubbleW, bubbleH);
            var arrowX = Mathf.Clamp(cx - x, 22f, bubbleW - 22f);

            var shadow = Ui.Box(root, "Shadow", 14, Palette.WithAlpha(Palette.Ink, 0.12f));
            Ui.Place(shadow.rectTransform, 0, 3, bubbleW, bubbleH);

            // Стрелка — повёрнутый квадратик с контуром; лицо выноски закрывает его половину.
            if (target != null)
            {
                var tail = Ui.Box(root, "Arrow", 3, Palette.Card).rectTransform;
                Ui.PlaceCenter(tail, arrowX, below ? 0f : bubbleH, 14, 14);
                tail.localRotation = Quaternion.Euler(0, 0, 45f);
                Ui.Outline(tail, 3, Palette.Line, 1.5f);
            }

            var face = Ui.Box(root, "Face", 14, Palette.Card);
            Ui.Place(face.rectTransform, 0, 0, bubbleW, bubbleH);
            Ui.Outline(face.rectTransform, 14, Palette.Line, 1.5f);
            if (target != null)
            {
                // Без контура в месте стыка: выноска и стрелка читаются одной фигурой.
                var patch = Ui.Box(root, "Patch", 2, Palette.Card).rectTransform;
                Ui.PlaceCenter(patch, arrowX, below ? 1.5f : bubbleH - 1.5f, 16, 4);
            }

            var bulb = Ui.Image(root, "Bulb", Art.Circle, Palette.CoinSoft);
            Ui.PlaceCenter(bulb.rectTransform, 22, bubbleH / 2f, 26, 26);
            Ui.IconImage(root, Icon.Info, Palette.CoinShadow, 22, bubbleH / 2f, 16);
            label.transform.SetAsLastSibling();
            Ui.Place(label.rectTransform, 40, 0, textW, bubbleH);

            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            Tweens.Fade(group, 0f, 1f, 0.25f);
            Tweens.Scale(root, 0.85f, 1f, 0.3f, Ease.OutBack);
            var rest = root.anchoredPosition;
            var lift = below ? -1f : 1f;
            Tweens.Run(root, 1.8f, t => root.anchoredPosition = rest + new Vector2(0, lift * 3f * Ease.Bump(t)), null, null, 0.3f, true);
            _tipShownAt = Time.unscaledTime;
            Tweens.Delay(root, Mathf.Clamp(1.5f + tipText.Length * 0.04f, 3f, 4.5f), HideTip);
        }

        /// <summary>
        /// К чему относится подсказка: по словам в ней ищем особый клубок на видимой части поля,
        /// цель по золоту, картинку. below — выноска под целью (цели наверху), иначе над ней.
        /// null — общий совет, выноска ложится на пустой станок.
        /// </summary>
        private (RectTransform? target, bool below) TipTarget(string tipText)
        {
            // Без учёта регистра: подсказка часто начинается с заглавной («Тапни клубок…»).
            var tip = tipText.ToLowerInvariant();
            System.Func<Spool, bool>? match = null;
            if (tip.Contains("?") || tip.Contains("скрыт"))
            {
                match = s => !s.IsColorVisible;
            }
            else if (tip.Contains("узел"))
            {
                match = s => s.Knots > 0;
            }
            else if (tip.Contains("замок") || tip.Contains("ключ"))
            {
                match = s => s.Locked || s.IsKey;
            }
            else if (tip.Contains("ледян") || tip.Contains("лёд"))
            {
                match = s => s.Frozen;
            }
            else if (tip.Contains("связан"))
            {
                match = s => s.Link != 0;
            }
            else if (tip.Contains("толст"))
            {
                match = s => s.Thick;
            }
            else if (tip.Contains("радуж"))
            {
                match = s => s.Rainbow;
            }
            else if (tip.Contains("цвета ткани") || tip.Contains("цвета фона") || tip.Contains("чуж"))
            {
                match = s => _game.IsForeign(s);
            }
            else if (tip.Contains("тапни клубок"))
            {
                match = s => _game.HasWork(s);
            }

            if (match != null)
            {
                foreach (var f in _field)
                {
                    var column = _game.Column(f.Column);
                    if (f.Depth < column.Count && column[f.Depth].Id == f.SpoolId && match(column[f.Depth]))
                    {
                        return (f.View.Root, false);
                    }
                }
            }

            if (tip.Contains("золот"))
            {
                return _goldGoal != null ? ((RectTransform)_goldGoal.transform.parent, true) : (_pictureCard, true);
            }

            if (tip.Contains("фронт") || tip.Contains("в рамке") || tip.Contains("снизу вверх"))
            {
                return (_pictureCard, true);
            }

            return (null, false);
        }

        private void HideTip()
        {
            if (_tip == null)
            {
                return;
            }

            var tip = _tip;
            _tip = null;
            _tipKeep = false;
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

        private static string Plural(int n, string one, string few, string many)
        {
            var mod10 = n % 10;
            var mod100 = n % 100;
            if (mod10 == 1 && mod100 != 11)
            {
                return one;
            }

            return mod10 >= 2 && mod10 <= 4 && (mod100 < 12 || mod100 > 14) ? few : many;
        }

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
                // Цветной детальный значок: латунные ножницы, стрелка из нити, лунка с плюсом.
                var art = Ui.Image(root, "Icon", Art2.Booster(type), Color.white);
                Ui.PlaceCenter(art.rectTransform, w / 2f - total / 2f + 11f, 30, 32, 32);
                Ui.Place(label.rectTransform, w / 2f - total / 2f + 28f, 0, textW + 4f, 60);
                label.alignment = TextAnchor.MiddleLeft;

                var badge = Ui.Place(Ui.Node(root, "Badge"), w - 38f, -6f, 40, 20);
                _badgeBack = Ui.Box(badge, "Back", 10, Palette.Ink);
                Ui.Stretch(_badgeBack.rectTransform);
                _badgeText = Ui.Label(badge, "", 11, Color.white);
                Ui.Stretch(_badgeText.rectTransform);
                Tap = root.gameObject.AddComponent<Tap>();
            }

            public Tap Tap { get; }

            /// <summary>Финиш: кнопка затихает и не нажимается.</summary>
            public bool Dimmed { get; set; }

            public void Refresh(bool unlocked, int stock, int price, int unlockLevel, bool available, bool active, int free = 0)
            {
                _face.color = active ? Palette.PrimarySoft : Palette.Card;
                _group.blocksRaycasts = !Dimmed;
                if (Dimmed)
                {
                    _group.alpha = 0.4f;
                    return;
                }

                if (!unlocked)
                {
                    _group.alpha = 0.55f;
                    _badgeBack.color = Palette.Track;
                    _badgeText.color = Palette.Ink2;
                    _badgeText.text = "ур. " + unlockLevel;
                    return;
                }

                _group.alpha = available ? 1f : 0.55f;
                if (free > 0)
                {
                    // Подарок «тёплой серии»: зелёная плашка, бесплатно.
                    _badgeBack.color = Palette.Primary;
                    _badgeText.color = Color.white;
                    _badgeText.text = "даром";
                }
                else if (stock > 0)
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

    /// <summary>
    /// Всё, что игрок «заработал» в попытке уровня кроме самой модели: бустеры, спасения, комбо, золото.
    /// Живёт вместе с LoomGame, а не с экраном: перестройка экрана не обнуляет счёт (и не дарит лишних звёзд).
    /// </summary>
    public sealed class LevelRun
    {
        public TempoCombo Tempo { get; } = new TempoCombo();

        public int BoostersUsed { get; set; }

        public int Rescues { get; set; }

        public int GoldCaught { get; set; }

        public int GoldCoins { get; set; }

        public Dictionary<int, bool> GoldDone { get; } = new Dictionary<int, bool>();

        public bool ForeignShown { get; set; }

        /// <summary>Подарок «тёплой серии» на этот уровень и длина серии на старте.</summary>
        public WarmStart Warm { get; set; }

        public int WarmStreak { get; set; }

        /// <summary>Бесплатные ножницы и отмены от серии: тратятся первыми и не снимают звезду.</summary>
        public int FreeScissors { get; set; }

        public int FreeUndo { get; set; }

        /// <summary>Номер попытки этого уровня (для аналитики) и когда она началась.</summary>
        public int Attempt { get; set; } = 1;

        public float StartedAt { get; set; } = UnityEngine.Time.realtimeSinceStartup;

        /// <summary>Это «Узор дня», а не уровень кампании: своя награда, серия кампании не трогается.</summary>
        public bool Pattern { get; set; }
    }
}
