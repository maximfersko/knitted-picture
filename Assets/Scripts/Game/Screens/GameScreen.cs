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
    public sealed partial class GameScreen : ScreenView
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

        /// <summary>Через сколько секунд бездействия подсказка хода сама появляется на «застрявшей» попытке.</summary>
        private const float StuckNudge = 8f;

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

        /// <summary>Значок мотка на спицах в шапке (null — бонусы закрыты или мотка нет).</summary>
        private RectTransform? _yarnBadge;

        private YarnPerk _yarnPerk;

        /// <summary>Сколько мотка прибавляет к золотой петле: из значка вылетает именно эта часть.</summary>
        private int _yarnBonus;

        /// <summary>Значок моли в конце полоски времени: подпись, чья это полоска. Сама моль живёт на картинке.</summary>
        private Image? _timerIcon;
        private RectTransform? _timerFill;
        private Image? _timerFillImage;

        /// <summary>Сколько секунд сейчас написано на таймере: текст меняем раз в секунду, а не строим строку каждый кадр.</summary>
        private int _timerShown = -1;

        /// <summary>Что сейчас на плашке комбо (число и «Разгон»): текст и сдвиг — только при смене.</summary>
        private int _comboShown = -1;

        private static readonly Color TimerWarm = Palette.Hex("#FF9A3C");
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

            BuildTextileBackdrop();
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
                _mothLayer = Ui.Layer(Ui.Stretch(Ui.Node(Root, "MothLayer")), true);
                var step = _picture.Root.sizeDelta.x / Mathf.Max(1, _game.Width);
                _moth = MothView.Create(_mothLayer, Mathf.Clamp(step * 1.15f, 22f, 30f));
                _moth.Hide();

                // Поверх поля, но под эффектами и окнами: край экрана краснеет, когда время на исходе.
                _mothEdge = Ui.Image(Root, "MothEdge", Art.EdgeGlow(40), Color.clear);
                _mothEdge.type = Image.Type.Sliced;
                _mothEdge.pixelsPerUnitMultiplier = Art.Density;
                Ui.Stretch(_mothEdge.rectTransform);
            }

            _fx = Ui.Layer(Ui.Stretch(Ui.Node(Root, "Fx")), false);
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
            CancelResponsiveInput();
            if (!_ended && !_failShown && !App.HasPopups && _game.Status == GameStatus.Playing)
            {
                App.ShowPopup(new PausePopup(App, Restart, LeaveHome, StreakWarning()));
            }
        }

        /// <summary>Для какой версии партии уже проверили, что победа ещё не решена.</summary>
        private int _finishVersion = -1;

        public override void Tick(float dt)
        {
            if (App.HasPopups)
            {
                CancelResponsiveInput();
                return;
            }

            UpdateResponsiveInput(dt);
            if (_ended || _failShown)
            {
                return;
            }

            ShowNextGameplayToast();

            if (Tweens.Frozen)
            {
                // Хит-стоп (затор, последняя петля, старт «Разгона»): станок на миг замирает вместе с анимациями.
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
                    if (!_finishing && !_scissors && !App.HasPopups)
                    {
                        var buffered = _responsive.Flush(_game, Time.unscaledTime);
                        if (buffered != null)
                        {
                            PresentMove(buffered);
                        }
                    }
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

                    // Будущее партии меняется только от действий игрока: «победа не решена» для той же версии
                    // не пересчитываем (копия партии и довязка до конца — самое дорогое в кадре на слабом телефоне).
                    if (_game.IsBusy && _finishVersion != _game.Version)
                    {
                        _finishVersion = _game.Version;
                        if (_game.WinIsCertain())
                        {
                            StartFinish();
                        }
                    }
                }
            }

            var wasRush = _tempo.Rush;
            if (_tempo.Update(dt, _game.IsBusy || _arriving.Count > 0))
            {
                OnComboBurned(wasRush);
            }

            UpdateLoomAlert(dt);
            MaybeLoomFullTip();
            UpdateCombo(dt);
            _picture.SetGoldLive(_tempo.Active);

            if (!_finishing && !_game.IsBusy && _hint == null && _game.Status == GameStatus.Playing && _hintFailedAt != _game.Taps)
            {
                _idle += dt;
                // Дальше — мягкий толчок, если игрок долго не делает ничего: 20 секунд без хода, станок стоит.
                // На первых пяти уровнях это обучение — всегда; дальше — по настройке «Подсказка хода» (раздел «Подсказки» в «Удобстве»).
                // Самый первый ход на первом уровне: рука появляется раньше, чтобы новичок не стоял в растерянности.
                var firstMove = _level.id == 1 && _game.Taps == 0 && _run.Attempt == 1;
                var threshold = _level.id == 1 ? (firstMove ? 2.4f : 3f) : _level.id <= 3 ? 6f : _level.id <= 5 ? 12f : Comfort.IdleHints ? IdleNudge : 0f;
                if (_run.StuckHelp)
                {
                    // Застрявшему подсказка нужна раньше и даже при выключенной настройке.
                    threshold = threshold > 0f ? Mathf.Min(threshold, StuckNudge) : StuckNudge;
                }

                if (threshold > 0 && _idle > threshold)
                {
                    ShowHint();
                }
            }
        }

        /// <summary>Картинка уровня: сюда прилетает эскиз с кнопки «Играть».</summary>
        public RectTransform PictureRoot => _picture.Root;

        public override bool OnBack()
        {
            CancelResponsiveInput();
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
            private readonly Image _suggestion;

            public BoosterButton(RectTransform parent, BoosterType type, float x, float y, float w)
            {
                var root = Ui.Card(parent, "Booster" + type, x, y, w, 60, 18, Palette.Card, Palette.CardShadow, 3f, true);
                _face = root.Find("Face").GetComponent<Image>();
                _group = root.gameObject.AddComponent<CanvasGroup>();
                var label = Ui.Label(root, type == BoosterType.Slot ? L.T("Место") : BoosterInfo.Name(type), 14, Palette.Ink);
                var textW = label.preferredWidth;
                var well = Mathf.Min(44f, w * 0.38f);
                var total = well + 6f + textW;
                var left = Mathf.Max(6f, w / 2f - total / 2f);

                // Цветной детальный значок в мягкой лунке: латунные ножницы, стрелка из нити, лунка с плюсом.
                var bowl = Ui.Image(root, "Well", Art2.IconWell, Color.white);
                Ui.PlaceCenter(bowl.rectTransform, left + well / 2f, 30, well, well);
                var art = Ui.Image(root, "Icon", Art2.Booster(type), Color.white);
                Ui.PlaceCenter(art.rectTransform, left + well / 2f, 30, well * 0.8f, well * 0.8f);
                Ui.Place(label.rectTransform, left + well + 6f, 0, Mathf.Min(textW + 4f, w - left - well - 8f), 60);
                label.alignment = TextAnchor.MiddleLeft;

                // Запас или цена — на бирке, как число петель у клубков.
                var badge = Ui.Place(Ui.Node(root, "Badge"), w - 44f, -8f, 47, 26);
                _badgeBack = Ui.Image(badge, "Back", Art2.Tag, Palette.Ink);
                Ui.Stretch(_badgeBack.rectTransform);
                _badgeText = Ui.Fit(Ui.Label(badge, "", 11, Color.white), 8);
                Ui.Place(_badgeText.rectTransform, 47f * Art2.TagTextFrom, 0, 47f * (0.97f - Art2.TagTextFrom), 26);
                Tap = root.gameObject.AddComponent<Tap>();
                _suggestion = Outline(root, Palette.CoinShadow).GetComponent<Image>();
                _suggestion.enabled = false;
            }

            public Tap Tap { get; }

            /// <summary>Спокойная обводка полезного бустера до затора, без изменения доступности и цены.</summary>
            public void Suggest(bool useful) => _suggestion.enabled = useful && !Dimmed;

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
                    _badgeText.text = L.T("ур. ") + unlockLevel;
                    return;
                }

                _group.alpha = available ? 1f : 0.55f;
                if (free > 0)
                {
                    // Подарок «тёплой серии»: зелёная плашка, бесплатно.
                    _badgeBack.color = Palette.Primary;
                    _badgeText.color = Color.white;
                    _badgeText.text = L.T("даром");
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

        /// <summary>Спасений за видео в этой попытке (не больше Economy.VideoRescuesPerLevel).</summary>
        public int VideoRescues { get; set; }

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

        /// <summary>
        /// «Не сдаёмся»: с 4-й попытки нового уровня (один раз на уровень) на эту попытку подарены бесплатные ножницы
        /// (как у тёплой серии, звезду не снимают) и подсказка хода включена независимо от настройки, раньше обычного.
        /// </summary>
        public bool StuckHelp { get; set; }

        /// <summary>Номер попытки этого уровня (для аналитики) и когда она началась.</summary>
        public int Attempt { get; set; } = 1;

        public float StartedAt { get; set; } = UnityEngine.Time.realtimeSinceStartup;

        /// <summary>Это «Узор дня», а не уровень кампании: своя награда, серия кампании не трогается.</summary>
        public bool Pattern { get; set; }
    }
}
