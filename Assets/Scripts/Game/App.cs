#nullable enable

using System;
using System.Collections.Generic;
using KnittedPicture.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KnittedPicture.Game
{
    /// <summary>Создаёт игру при запуске любой сцены: сцена может быть пустой.</summary>
    public static class AppBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (App.Instance != null)
            {
                return;
            }

            var host = new GameObject("KnittedPicture");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<App>();
        }
    }

    /// <summary>
    /// Точка входа: сервисы, холст под безопасную зону экрана, переходы между экранами,
    /// всплывающие окна и тосты.
    /// </summary>
    public sealed class App : MonoBehaviour
    {
        /// <summary>Ширина макета; на широких экранах контент не шире MaxWidth.</summary>
        private const float DesignWidth = 390f;
        private const float DesignHeight = 844f;
        private const float MaxWidth = 430f;

        private readonly List<Popup> _popups = new List<Popup>();
        private CanvasScaler? _scaler;
        private RectTransform? _canvas;
        private RectTransform? _frame;
        private RectTransform? _popupLayer;
        private ScreenView? _screen;
        private RawImage? _pattern;
        private Image? _background;
        private Color _ground;
        private int _groundVersion;
        private Func<ScreenView>? _route;
        private Vector2Int _screenSize;
        private Rect _safeArea;

        public static App? Instance { get; private set; }

        public LevelRepository Levels { get; private set; } = null!;

        public ProgressService Progress { get; private set; } = null!;

        public IAds Ads { get; private set; } = null!;

        /// <summary>Покупки внутри игры (только сборка для RuStore с SDK; иначе — «покупок нет»).</summary>
        public IPurchases Purchases { get; private set; } = null!;

        private readonly System.Collections.Concurrent.ConcurrentQueue<Action> _mainThread = new System.Collections.Concurrent.ConcurrentQueue<Action>();

        /// <summary>
        /// Выполнить в главном потоке Unity: ответы рекламного и платёжного SDK могут приходить из своих потоков,
        /// а трогать интерфейс и сохранение можно только из главного.
        /// </summary>
        public void RunOnMain(Action action) => _mainThread.Enqueue(action);

        /// <summary>Выполнить через seconds секунд реального времени (пауза окон не мешает).</summary>
        public void After(float seconds, Action action) => Tweens.Delay(this, seconds, action);

        /// <summary>Разрешить или запретить персональную рекламу и запомнить ответ.</summary>
        public void SetAdsConsent(bool allowed)
        {
            Progress.Data.adsConsent = allowed ? 1 : 2;
            Analytics.Event("ads_consent", ("allowed", allowed));
            Save();
            Ads.SetPersonalized(allowed);
        }

        /// <summary>Купить «Без рекламы»; после оплаты межуровневой рекламы больше нет.</summary>
        public void BuyNoAds(Action? done = null)
        {
            Purchases.Buy(StoreConfig.NoAdsProduct, ok =>
            {
                Analytics.Event("purchase", ("product", StoreConfig.NoAdsProduct), ("ok", ok));
                if (ok)
                {
                    Progress.Data.adsRemoved = true;
                    Save();
                    Toast("Готово: рекламы между уровнями больше не будет. Спасибо!");
                }

                done?.Invoke();
            });
        }

        /// <summary>Вернуть разовые покупки (переустановка, новый телефон, сброс прогресса).</summary>
        public void RestorePurchases(bool tell)
        {
            Purchases.Restore(owned =>
            {
                var noAds = System.Linq.Enumerable.Contains(owned, StoreConfig.NoAdsProduct);
                if (noAds && !Progress.Data.adsRemoved)
                {
                    Progress.Data.adsRemoved = true;
                    Save();
                }

                if (tell)
                {
                    Toast(noAds ? "Покупка «Без рекламы» восстановлена" : "Покупок не найдено");
                }
            });
        }

        /// <summary>Контентная колонка внутри безопасной зоны.</summary>
        public RectTransform Frame => _frame!;

        public RectTransform PopupLayer => _popupLayer!;

        /// <summary>Счётчик монет на текущем экране (последний созданный Ui.CoinPill).</summary>
        public Text? CoinCounter { get; set; }

        /// <summary>Монеты в полёте: уже начислены, но счётчик покажет их по прилёту.</summary>
        public int CoinsInFlight { get; private set; }

        /// <summary>Тип предыдущего экрана: главная «выпрыгивает» только при входе, а не при каждом обновлении.</summary>
        public System.Type? PreviousScreen { get; private set; }

        /// <summary>
        /// Монетки вылетают из точки и летят в счётчик; число растёт по мере прилёта. Монеты живут
        /// в слое окон, поэтому долетают, даже если экран под ними перестроился (окно награды закрылось).
        /// </summary>
        public void FlyCoins(Vector3 fromWorld, int amount)
        {
            if (amount <= 0 || CoinCounter == null)
            {
                return;
            }

            var count = Mathf.Clamp(amount / 10, 3, 8);
            var perCoin = Mathf.Max(1, amount / count);
            var given = 0;
            CoinsInFlight += amount;
            CoinCounter.text = (Progress.Coins - CoinsInFlight).ToString();
            for (var i = 0; i < count; i++)
            {
                var coin = Ui.Coin(PopupLayer, 0, 0, 22);
                coin.position = fromWorld;
                coin.localScale = Vector3.zero;
                var portion = i == count - 1 ? amount - given : perCoin;
                given += portion;
                var jitter = new Vector3(UnityEngine.Random.Range(-28f, 28f), UnityEngine.Random.Range(-12f, 20f), 0) * PopupLayer.lossyScale.x;
                var delay = 0.05f + i * 0.06f;
                Tweens.Delay(coin, delay, () => coin.localScale = Vector3.one);
                var start = fromWorld + jitter;
                Tweens.Run(coin, 0.6f, t =>
                {
                    // Цель берём каждый кадр: под окном экран мог перестроиться, счётчик — новый.
                    var target = CoinCounter != null ? Ui.WorldCenter((RectTransform)CoinCounter.transform.parent) : start;
                    coin.position = Vector3.LerpUnclamped(start, target, t);
                }, Ease.InOutCubic, () =>
                {
                    Destroy(coin.gameObject);
                    CoinsInFlight -= portion;
                    if (CoinCounter != null)
                    {
                        CoinCounter.text = (Progress.Coins - CoinsInFlight).ToString();
                        Tweens.Punch(CoinCounter.transform.parent, 0.12f, 0.15f);
                    }

                    Sfx.Play(Sound.Coin, 0.4f);
                }, delay);
            }
        }

        /// <summary>Переход по вкладкам: новый экран коротко въезжает со стороны вкладки.</summary>
        public void SlideIn(int direction)
        {
            if (_screen == null || direction == 0)
            {
                return;
            }

            var rt = _screen.Root;
            var group = rt.GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = rt.gameObject.AddComponent<CanvasGroup>();
            }

            var offset = new Vector2(direction * 40f, 0);
            rt.anchoredPosition = offset;
            Tweens.Fade(group, 0f, 1f, 0.22f);
            Tweens.Run(rt, 0.26f, t => rt.anchoredPosition = Vector2.LerpUnclamped(offset, Vector2.zero, t), Ease.OutCubic);
        }

        public float Width { get; private set; }

        public float Height { get; private set; }

        /// <summary>Отступ снизу до безопасной зоны (полоска жестов на iPhone).</summary>
        public float BottomInset { get; private set; }

        public float CanvasWidth { get; private set; }

        public bool HasPopups => _popups.Count > 0;

        /// <summary>Сегодняшний день (с 1970 года) по местному времени — для подарка дня.</summary>
        public int Today => (int)(DateTime.Now.Date - new DateTime(1970, 1, 1)).TotalDays;

        /// <summary>Случай для наград: мотки, сундуки.</summary>
        public System.Random Random { get; } = new System.Random();

        private void Awake()
        {
            Instance = this;
            Application.targetFrameRate = 60;
            if (Application.isMobilePlatform)
            {
                Screen.orientation = ScreenOrientation.Portrait;
            }

            Tweens.Install(gameObject);
            Reminders.CancelAll();
            Sfx.Install(gameObject);
            Levels = new LevelRepository();
            Progress = new ProgressService(SaveStore.Load(), Levels.Count);
            Sfx.Enabled = Progress.SoundOn;
            Haptics.Enabled = Progress.Data.hapticsOn;
            Analytics.Init();
            Analytics.Event("app_open", ("level", Progress.CurrentLevel), ("wins", Progress.Data.totalWins), ("coins", Progress.Coins),
                ("store", StoreConfig.Flavor.ToString()), ("ads_removed", Progress.Data.adsRemoved), ("first", !Progress.Data.introSeen));
            Ads = AdsFactory.Create(this);
            Ads.SetPersonalized(Progress.Data.adsConsent == 1);
            Purchases = PurchasesFactory.Create(this);
            RestorePurchases(false);

            EnsureCamera();
            EnsureEventSystem();
            BuildCanvas();
            if (Progress.Data.introSeen)
            {
                ShowHome();

                // Подарок дня встречает игрока сразу при входе — повод заходить каждый день.
                if (Progress.CanClaimDaily(Today) && Progress.Data.completed.Count > 0)
                {
                    ShowPopup(new DailyGiftPopup(this, ShowHome));
                }
            }
            else
            {
                ShowIntro();
            }
        }

        private void Update()
        {
            while (_mainThread.TryDequeue(out var action))
            {
                action();
            }

            if (Screen.width != _screenSize.x || Screen.height != _screenSize.y || Screen.safeArea != _safeArea)
            {
                Rebuild();
            }

            if (BackPressed())
            {
                OnBack();
            }

            if (_popups.Count == 0)
            {
                // После сворачивания первый кадр может прийти с огромным шагом времени — не даём ему
                // разом съесть таймер моли или сжечь комбо.
                _screen?.Tick(Mathf.Min(Time.unscaledDeltaTime, 0.1f));
            }
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                Save();
                (_screen as GameScreen)?.AutoPause();
                Reminders.Schedule(Progress, Today);
            }
            else
            {
                Reminders.CancelAll();
            }
        }

        private void OnApplicationQuit()
        {
            if (!SaveStore.WasCleared)
            {
                Save();
                Reminders.Schedule(Progress, Today);
            }
        }

        private void OnApplicationFocus(bool focused)
        {
            if (focused && Progress != null && SaveStore.WasCleared)
            {
                ResetProgress();
            }
        }

        public void Save()
        {
            if (Progress == null)
            {
                return;
            }

            // Сохранение стёрли снаружи — не возвращаем старый прогресс, а начинаем с чистого листа.
            if (SaveStore.WasCleared)
            {
                ResetProgress();
                return;
            }

            SaveStore.Save(Progress.Data);
        }

        public void ShowHome() => Show(() => new HomeScreen(this));

        public void ShowAlbum() => ShowCollections(CollectionsTab.Album);

        /// <summary>Коллекции, открытые на вкладке tab; дальше вкладки листаются внутри экрана.</summary>
        public void ShowCollections(CollectionsTab tab) => Show(() => new CollectionsScreen(this, tab));

        /// <summary>Сундук и запасы: открыть сундук, посмотреть бустеры и корзинку.</summary>
        public void ShowChest() => Show(() => new ChestScreen(this));

        /// <summary>Вступление «как играть»: на первом запуске и из настроек.</summary>
        public void ShowIntro() => Show(() => new IntroScreen(this));

        /// <summary>Вступление закончено или пропущено: новичка сразу в первый уровень.</summary>
        public void FinishIntro()
        {
            Analytics.Event("intro_finish", ("first", !Progress.Data.introSeen));
            Progress.Data.introSeen = true;
            Save();
            if (Progress.IsCompleted(1))
            {
                ShowHome();
            }
            else
            {
                StartLevel(1);
            }
        }

        /// <summary>Начать уровень. restart — игрок перезапускает после неудачи: серия побед обрывается.</summary>
        public void StartLevel(int levelId, bool restart = false)
        {
            // Перезапуск нового уровня обрывает серию; перепрохождение старых её не трогает.
            if (restart && !Progress.IsCompleted(levelId))
            {
                Progress.BreakStreak();
            }

            if (!Levels.Has(levelId))
            {
                ShowHome();
                return;
            }

            HomeScreen.ResetCarousel();
            var level = Levels.Get(levelId);
            var intro = Progress.TakeBoosterIntro(levelId);
            var attempt = Progress.NextAttempt(levelId);
            Save();
            var game = new LoomGame(level);
            var run = new LevelRun { Attempt = attempt };

            // «Тёплая серия»: подарок на старт — лишний слот сразу, ножницы и отмена бесплатно.
            var warm = Progress.WarmStartFor(levelId);
            run.Warm = warm;
            run.WarmStreak = Progress.Streak;
            if (warm.ExtraSlot)
            {
                game.AddSlot();
            }

            run.FreeScissors = warm.FreeScissors ? 1 : 0;
            run.FreeUndo = warm.FreeUndo ? 1 : 0;
            Analytics.Event("level_start", ("level", levelId), ("attempt", attempt), ("hard", level.hard), ("timed", level.timeLimit > 0),
                ("completed_before", Progress.IsCompleted(levelId)), ("warm", warm.Any));
            Show(() => new GameScreen(this, level, game, run));
            if (intro != null)
            {
                ShowPopup(new BoosterIntroPopup(this, intro.Value));
            }
            else if (Progress.TakePerksIntro(levelId))
            {
                Save();
                ShowPopup(new YarnPerksIntroPopup(this));
            }
        }

        private int _patternDay = int.MinValue;
        private LevelData? _todayPattern;

        /// <summary>Сегодняшний «Узор дня»: строится один раз за день (картинка альбома по дате, клубки заново).</summary>
        public LevelData TodayPattern()
        {
            if (_todayPattern == null || _patternDay != Today)
            {
                var index = DailyPattern.PictureIndex(Today, Levels.Count);
                _todayPattern = DailyPattern.Build(Levels.Get(index + 1), Today);
                _patternDay = Today;
            }

            return _todayPattern;
        }

        /// <summary>Начать «Узор дня». Серия кампании и «тёплый старт» его не касаются.</summary>
        public void StartPattern()
        {
            var level = TodayPattern();
            var game = new LoomGame(level);
            var run = new LevelRun { Pattern = true };
            Analytics.Event("pattern_start", ("streak", Progress.PatternStreak(Today)));
            Show(() => new GameScreen(this, level, game, run));
        }

        /// <summary>Узор связан: награда (раз в день), задания дня засчитываются, назад на главную с окном награды.</summary>
        public void OnPatternWon(LevelData level, int stars, LevelStats stats)
        {
            Progress.RecordMissions(Today, stars, !stats.Helped, stats.GoldCaught, stats.Rushes, stats.BestCombo, stats.Stitches);
            Progress.RecordLevel(stats.Stitches, stats.Rushes, stats.GoldCaught, stats.BestCombo);
            var granted = Progress.CompletePattern(Today, Random);
            Save();
            ShowHome();
            if (granted != null)
            {
                var streak = Progress.PatternStreak(Today);
                ShowPopup(new RewardPopup(this, streak > 1 ? $"Узор дня · {streak} дн. подряд" : "Узор дня", Icon.Star, granted.Value, ShowHome));
            }
            else
            {
                Toast("Узор дня уже связан. Новый будет завтра");
            }
        }

        public void OnLevelWon(LevelData level, int stars, LevelStats stats)
        {
            // Разрешение на напоминания спрашиваем после третьей картинки: игрок уже понял, зачем возвращаться.
            if (!Progress.Data.remindersAsked && Progress.Data.completed.Count >= 2)
            {
                Progress.Data.remindersAsked = true;
                Reminders.AskPermission();
            }

            var pendingBefore = Progress.PendingClaims;
            var chestBefore = Progress.CanOpenChest;
            Progress.RecordLevel(stats.Stitches, stats.Rushes, stats.GoldCaught, stats.BestCombo);
            Progress.RecordResult(level.id, stats.Taps, !stats.Helped);
            var missionsDone = Progress.RecordMissions(Today, stars, !stats.Helped, stats.GoldCaught, stats.Rushes, stats.BestCombo, stats.Stitches);
            var reward = Progress.CompleteLevel(level.id, level.hard, stars, stats.RushCoins + stats.GoldCoins);
            Save();
            var news = new WinNews(Progress.PendingClaims > pendingBefore, Progress.CanOpenChest && !chestBefore, missionsDone > 0,
                Progress.CanClaimMissions(Today));
            Show(() => new WinScreen(this, level, reward, stats, news));
        }

        /// <summary>«Дальше» после победы: при необходимости реклама, потом следующий уровень.</summary>
        public void ContinueAfterWin(int levelId)
        {
            void Next()
            {
                if (Progress.AllDone)
                {
                    ShowHome();
                }
                else
                {
                    StartLevel(Progress.CurrentLevel);
                }
            }

            if (Progress.TakeInterstitialSlot(levelId))
            {
                Save();
                Ads.ShowInterstitial("level_complete", Next);
            }
            else
            {
                Next();
            }
        }

        public T ShowPopup<T>(T popup)
            where T : Popup
        {
            _popups.Add(popup);
            return popup;
        }

        public void RemovePopup(Popup popup)
        {
            _popups.Remove(popup);
        }

        /// <summary>Плашка «видео-реклама» вместо настоящего SDK.</summary>
        public void ShowRewardedStub(Action done)
        {
            ShowPopup(new RewardedStubPopup(this, done));
        }

        private RectTransform? _toast;

        /// <summary>
        /// Короткое сообщение сверху, исчезает само. Новое заменяет предыдущее (а не ложится поверх него),
        /// высота — по тексту, длинный текст держится на экране дольше.
        /// </summary>
        public void Toast(string text)
        {
            if (_toast != null)
            {
                Destroy(_toast.gameObject);
            }

            var width = Mathf.Min(Width - 48f, 320f);
            var root = Ui.Place(Ui.Node(PopupLayer, "Toast"), 0, 0, width, 52);
            _toast = root;
            root.anchorMin = new Vector2(0.5f, 1f);
            root.anchorMax = new Vector2(0.5f, 1f);
            root.pivot = new Vector2(0.5f, 1f);
            root.anchoredPosition = new Vector2(0, -(Screen.height / (_scaler != null ? _scaler.scaleFactor : 1f) - Height - BottomInset) - 64f);
            var face = Ui.Box(root, "Face", 18, Palette.WithAlpha(Palette.Ink, 0.92f));
            Ui.Stretch(face.rectTransform);
            var label = Ui.Label(root, text, 15, Color.white, 12, 0, width - 24, 52);
            label.lineSpacing = 0.9f;
            var height = Mathf.Max(52f, label.preferredHeight + 18f);
            root.sizeDelta = new Vector2(width, height);
            label.rectTransform.sizeDelta = new Vector2(width - 24, height);
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            Tweens.Fade(group, 0f, 1f, 0.18f);
            var hold = Mathf.Clamp(1.2f + text.Length * 0.025f, 1.9f, 3.6f);
            Tweens.Fade(group, 1f, 0f, 0.3f, () =>
            {
                if (_toast == root)
                {
                    _toast = null;
                }

                Destroy(root.gameObject);
            }, hold);
        }

        public void SetSound(bool on)
        {
            Progress.SoundOn = on;
            Sfx.Enabled = on;
            Save();
        }

        public void SetHaptics(bool on)
        {
            Progress.Data.hapticsOn = on;
            Haptics.Enabled = on;
            Save();
            Haptics.Play(Haptic.Medium);
        }

        /// <summary>
        /// Стереть прогресс: новое сохранение, все окна закрыты, экраны строятся заново
        /// (главная, сундук, коллекции читают только Progress — старого состояния не остаётся).
        /// </summary>
        public void ResetProgress()
        {
            CloseAllPopups();
            // Покупка и ответ про рекламу — не игровой прогресс: после сброса они остаются.
            var adsRemoved = Progress.Data.adsRemoved;
            var adsConsent = Progress.Data.adsConsent;
            SaveStore.Reset();
            Progress = new ProgressService(new PlayerProgress { adsRemoved = adsRemoved, adsConsent = adsConsent }, Levels.Count);
            Sfx.Enabled = Progress.SoundOn;
            Haptics.Enabled = Progress.Data.hapticsOn;
            HomeScreen.ResetCarousel();
            SaveStore.Save(Progress.Data);
            ShowIntro();
        }

        /// <summary>
        /// Оттенок фона: экран задаёт свой (главная и уровень — по коллекции), остальные — общий лавандовый.
        /// Смена плавная, чтобы при переходе не мигало.
        /// </summary>
        public void SetGround(Color color, bool instant = false)
        {
            if (_background == null || color == _ground)
            {
                return;
            }

            var from = _background.color;
            _ground = color;
            var image = _background;
            var version = ++_groundVersion;
            if (instant)
            {
                image.color = color;
                return;
            }

            // Новый оттенок отменяет недоигранный переход: старый твин просто перестаёт красить.
            Tweens.Run(image, 0.35f, t =>
            {
                if (version == _groundVersion)
                {
                    image.color = Color.Lerp(from, color, t);
                }
            });
        }

        private void Show(Func<ScreenView> route)
        {
            SetGround(Palette.Ground);
            CloseAllPopups();
            PreviousScreen = _screen?.GetType();
            _screen?.Dispose();
            _route = route;
            _screen = route();
        }

        private void CloseAllPopups()
        {
            for (var i = _popups.Count - 1; i >= 0; i--)
            {
                _popups[i].Destroy();
            }

            _popups.Clear();
        }

        private void OnBack()
        {
            if (_popups.Count > 0)
            {
                var top = _popups[_popups.Count - 1];
                if (top.Closable)
                {
                    top.Close();
                }

                return;
            }

            if (_screen != null && !_screen.OnBack())
            {
                ShowHome();
            }
        }

        private static bool BackPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                return true;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                return true;
            }
#endif
            return false;
        }

        private void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            var go = new GameObject("EventSystem");
            go.transform.SetParent(transform, false);
            go.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>().AssignDefaultActions();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        /// <summary>Пустая сцена без камеры: заводим свою, чтобы экран очищался цветом фона.</summary>
        private void EnsureCamera()
        {
            if (Camera.main != null)
            {
                Camera.main.backgroundColor = Palette.Ground;
                return;
            }

            var go = new GameObject("Camera");
            go.transform.SetParent(transform, false);
            var camera = go.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Palette.Ground;
            camera.orthographic = true;
            go.tag = "MainCamera";
        }

        private void BuildCanvas()
        {
            var go = new GameObject("Canvas", typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(transform, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            _scaler = go.AddComponent<CanvasScaler>();
            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            _scaler.referencePixelsPerUnit = 100f;
            go.AddComponent<GraphicRaycaster>();
            _canvas = (RectTransform)go.transform;

            var background = Ui.Image(_canvas, "Background", null, Palette.Ground);
            Ui.Stretch(background.rectTransform);
            _background = background;
            _ground = Palette.Ground;
            var pattern = Ui.Node(_canvas, "Pattern").gameObject.AddComponent<RawImage>();
            pattern.texture = Art.KnitPattern;
            pattern.raycastTarget = false;
            Ui.Stretch(pattern.rectTransform);
            _pattern = pattern;
            _frame = Ui.Node(_canvas, "Frame");
            _popupLayer = Ui.Stretch(Ui.Node(_canvas, "Popups"));
            ApplyLayout();
        }

        /// <summary>
        /// Масштаб: на вытянутых телефонах ширина макета 390, на широких экранах — высота 844.
        /// Контент — колонка внутри безопасной зоны, не шире 430.
        /// </summary>
        private void ApplyLayout()
        {
            _screenSize = new Vector2Int(Screen.width, Screen.height);
            _safeArea = Screen.safeArea;
            var tall = (float)Screen.height / Mathf.Max(1, Screen.width) >= DesignHeight / DesignWidth;
            var scale = tall ? Screen.width / DesignWidth : Screen.height / DesignHeight;
            _scaler!.scaleFactor = scale;

            CanvasWidth = Screen.width / scale;
            var left = _safeArea.xMin / scale;
            var bottom = _safeArea.yMin / scale;
            var safeWidth = _safeArea.width / scale;
            Width = Mathf.Min(safeWidth, MaxWidth);
            Height = _safeArea.height / scale;
            BottomInset = bottom;

            var frame = _frame!;
            frame.anchorMin = Vector2.zero;
            frame.anchorMax = Vector2.zero;
            frame.pivot = Vector2.zero;
            frame.anchoredPosition = new Vector2(left + (safeWidth - Width) / 2f, bottom);
            frame.sizeDelta = new Vector2(Width, Height);

            if (_pattern != null)
            {
                _pattern.uvRect = new Rect(0, 0, CanvasWidth / 40f, Screen.height / scale / 40f);
            }
        }

        private void Rebuild()
        {
            ApplyLayout();
            if (_route != null)
            {
                CloseAllPopups();
                _screen?.Dispose();
                _screen = _route();
            }
        }
    }
}
