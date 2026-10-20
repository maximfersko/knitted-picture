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

        /// <summary>
        /// Планшет: макет выше (1000 вместо 844), чтобы всё не раздувалось, и колонка шире, чтобы поле и списки
        /// заняли место, а не плавали узкой полосой. На iPad 1 единица макета ≈ 1 pt, как на телефоне.
        /// </summary>
        private const float TabletHeight = 1000f;
        private const float TabletMaxWidth = 540f;

        /// <summary>Планшет: короткая сторона от 3,6 дюйма (у телефонов 2,5–3,1). Без dpi — по пропорциям.</summary>
        private const float TabletShortSideInches = 3.6f;

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

        /// <summary>Идёт покупка или ручная проверка покупок: повторные тапы по «Купить» и «Восстановить» не реагируют.</summary>
        private DateTime _purchaseUntil = DateTime.MinValue;

        /// <summary>Номер последнего запроса к магазину: поздний ответ старого запроса не снимает занятость нового.</summary>
        private int _purchaseToken;

        /// <summary>Сколько секунд запрос к магазину считается идущим, если ответ не пришёл (оплата может быть долгой).</summary>
        private const double PurchaseTimeout = 120.0;

        private bool PurchaseBusy => DateTime.UtcNow < _purchaseUntil;

        private int BeginPurchase()
        {
            _purchaseUntil = DateTime.UtcNow.AddSeconds(PurchaseTimeout);
            return ++_purchaseToken;
        }

        private void EndPurchase(int token)
        {
            if (token == _purchaseToken)
            {
                _purchaseUntil = DateTime.MinValue;
            }
        }

        /// <summary>
        /// Купить «Без рекламы»; после оплаты межуровневой рекламы больше нет. Отмену игрок видит коротким тостом
        /// («деньги не списаны»), сбой объясняется окном с путями «Восстановить покупки» и «Написать нам».
        /// </summary>
        public void BuyNoAds(Action? done = null)
        {
            if (PurchaseBusy)
            {
                Toast(L.T("Подожди, покупка ещё идёт"));
                return;
            }

            var token = BeginPurchase();
            Purchases.Buy(StoreConfig.NoAdsProduct, result =>
            {
                EndPurchase(token);
                Analytics.Event("purchase", ("product", StoreConfig.NoAdsProduct), ("ok", result == BuyResult.Ok), ("result", result.ToString()));
                switch (result)
                {
                    case BuyResult.Ok:
                        Progress.Data.adsRemoved = true;
                        Save();
                        Toast(L.T("Готово: рекламы между уровнями больше не будет. Спасибо!"));
                        break;
                    case BuyResult.Cancelled:
                        Toast(L.T("Покупка отменена, деньги не списаны"));
                        break;
                    default:
                        var support = StoreLinks.Support(this) != null;
                        ShowPopup(new InfoPopup(this, L.T("Покупка не прошла"),
                            support ? L.T("Проверь интернет и способ оплаты. Если деньги списались, тапни «Восстановить покупки» или напиши нам")
                                : L.T("Проверь интернет и способ оплаты. Если деньги списались, тапни «Восстановить покупки»"), support));
                        break;
                }

                done?.Invoke();
            });
        }

        /// <summary>
        /// Вернуть разовые покупки (переустановка, новый телефон, сброс прогресса). tell — игрок нажал сам:
        /// ответ тостом, повторные тапы пока идёт запрос не реагируют. Тихая проверка при запуске молчит и в том числе о сбое.
        /// </summary>
        public void RestorePurchases(bool tell)
        {
            var token = 0;
            if (tell)
            {
                if (PurchaseBusy)
                {
                    Toast(L.T("Подожди, покупка ещё идёт"));
                    return;
                }

                token = BeginPurchase();
            }

            Purchases.Restore((result, owned) =>
            {
                if (tell)
                {
                    EndPurchase(token);
                }

                var noAds = System.Linq.Enumerable.Contains(owned, StoreConfig.NoAdsProduct);
                if (noAds && !Progress.Data.adsRemoved)
                {
                    Progress.Data.adsRemoved = true;
                    Save();
                }

                if (tell)
                {
                    Toast(result == RestoreResult.Failed ? L.T("Не удалось проверить покупки. Проверь интернет и попробуй ещё раз")
                        : noAds ? L.T("Покупка «Без рекламы» восстановлена") : L.T("Покупок не найдено"));
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

        /// <summary>Сколько монет уже прозвенело в текущем перелёте: тон растёт.</summary>
        private int _coinChain;

        /// <summary>Картинка, только что впервые попавшая в альбом: альбом один раз подсветит её рамку (0 — нет).</summary>
        public int JustAdded { get; set; }

        /// <summary>Тип предыдущего экрана: главная «выпрыгивает» только при входе, а не при каждом обновлении.</summary>
        public System.Type? PreviousScreen { get; private set; }

        /// <summary>
        /// Монетки вылетают из точки и летят в счётчик; число растёт по мере прилёта. Монеты живут
        /// в слое окон, поэтому долетают, даже если экран под ними перестроился (окно награды закрылось).
        /// </summary>
        public void FlyCoins(Vector3 fromWorld, int amount)
        {
            _coinChain = 0;
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
                var portion = i == count - 1 ? amount - given : perCoin;
                given += portion;
                var jitter = new Vector3(UnityEngine.Random.Range(-34f, 34f), UnityEngine.Random.Range(-8f, 30f), 0) * PopupLayer.lossyScale.x;
                var delay = 0.05f + i * 0.06f;
                var fallback = fromWorld + jitter;

                // Цель берём каждый кадр: под окном экран мог перестроиться, счётчик — новый.
                Fx.CoinFlight(coin, fromWorld, fromWorld + jitter,
                    () => CoinCounter != null ? Ui.WorldCenter((RectTransform)CoinCounter.transform.parent) : fallback, delay, () =>
                {
                    Destroy(coin.gameObject);
                    CoinsInFlight -= portion;
                    if (CoinCounter != null)
                    {
                        Ui.RollTo(CoinCounter, Progress.Coins - CoinsInFlight, 0.22f);
                        Tweens.Punch(CoinCounter.transform.parent, 0.12f, 0.15f);
                    }

                    // Монеты подряд звенят всё выше.
                    Sfx.Play(Sound.Coin, 0.4f, 1f + Mathf.Min(0.5f, _coinChain++ * 0.05f));
                });
            }
        }

        /// <summary>Сторона, с которой въедет следующий экран (переход по вкладкам), 0 — обычный вход.</summary>
        private int _slide;

        /// <summary>Откуда полетит эскиз картинки в следующий уровень (мировые углы) и сам уровень.</summary>
        private (Vector3 Min, Vector3 Max, LevelData Level)? _hero;

        /// <summary>Следующий экран — уровень, открытый кнопкой с эскизом: эскиз перелетит на место картинки.</summary>
        public void HeroFrom(RectTransform preview, LevelData level)
        {
            var corners = new Vector3[4];
            preview.GetWorldCorners(corners);
            _hero = (corners[0], corners[2], level);
        }

        /// <summary>
        /// Общий элемент перехода: эскиз картинки вылетает с кнопки «Играть», растёт и ложится на место картинки
        /// уровня, а сама картинка проявляется под ним. Так в топовых головоломках вход в уровень выглядит
        /// одним движением, а не сменой экранов.
        /// </summary>
        private void FlyHero(GameScreen game, (Vector3 Min, Vector3 Max, LevelData Level) hero)
        {
            var layer = PopupLayer;
            var unit = Mathf.Max(0.0001f, layer.lossyScale.x);
            var corners = new Vector3[4];
            game.PictureRoot.GetWorldCorners(corners);
            var toCenter = (corners[0] + corners[2]) / 2f;
            var toSize = (Vector2)(corners[2] - corners[0]) / unit;
            var fromCenter = (hero.Min + hero.Max) / 2f;
            var fromSize = (Vector2)(hero.Max - hero.Min) / unit;
            if (toSize.x <= 1f || toSize.y <= 1f)
            {
                return;
            }

            // Эскиз той же пропорции, что картинка: вписан в кнопочный квадрат и растёт до размера картинки.
            var aspect = toSize.x / toSize.y;
            var start = aspect >= 1f ? new Vector2(fromSize.x, fromSize.x / aspect) : new Vector2(fromSize.y * aspect, fromSize.y);
            var image = MiniPicture.Create(layer, hero.Level, 0f, 0f, Mathf.Max(start.x, start.y), true);
            var rt = image.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = start;
            rt.position = fromCenter;
            image.raycastTarget = false;
            var lift = Mathf.Max(60f, toSize.y * 0.25f) * unit;
            Tweens.Run(rt, 0.5f, t =>
            {
                var e = Ease.InOutCubic(t);
                var mid = (fromCenter + toCenter) / 2f + new Vector3(0f, lift, 0f);
                var u = 1f - e;
                rt.position = u * u * fromCenter + 2f * u * e * mid + e * e * toCenter;
                rt.sizeDelta = Vector2.LerpUnclamped(start, toSize, Ease.OutCubic(t));
                rt.localRotation = Quaternion.Euler(0f, 0f, 6f * Mathf.Sin(t * Mathf.PI));
                image.color = new Color(1f, 1f, 1f, t < 0.75f ? 1f : 1f - (t - 0.75f) / 0.25f);
            }, null, () => Destroy(rt.gameObject));
        }

        /// <summary>Строящийся сейчас экран открыт вкладкой (±1) или обычно (0): панель вкладок решает, переезжать ли метке.</summary>
        public int PendingSlide => _slide;

        /// <summary>
        /// Когда закончатся переходы экранов и окон (Time.unscaledTime). До этого элементы ещё проявляются и всплывают,
        /// и проверки раскладки (QA-бот) их не судят.
        /// </summary>
        public float SettledAt { get; private set; }

        private void Unsettle(float seconds)
        {
            SettledAt = Mathf.Max(SettledAt, Time.unscaledTime + seconds);
        }

        /// <summary>Следующий экран открывается вкладкой: въедет со стороны вкладки, а старый уедет в другую.</summary>
        public void SlideNext(int direction)
        {
            _slide = direction;
        }

        public float Width { get; private set; }

        /// <summary>Экран планшета или раскрытого складного телефона.</summary>
        public bool Tablet { get; private set; }

        public float Height { get; private set; }

        /// <summary>Отступ снизу до безопасной зоны (полоска жестов на iPhone).</summary>
        public float BottomInset { get; private set; }

        public float CanvasWidth { get; private set; }

        public bool HasPopups => _popups.Count > 0;

        /// <summary>Открытый экран (для QA-бота и диагностики).</summary>
        public ScreenView? CurrentScreen => _screen;

        /// <summary>Сегодняшний день (с 1970 года) по местному времени — для подарка дня.</summary>
        public int Today => (int)(DateTime.Now.Date - new DateTime(1970, 1, 1)).TotalDays;

        /// <summary>Случай для наград: мотки, сундуки.</summary>
        public System.Random Random { get; } = new System.Random();

        private void Awake()
        {
            Instance = this;

            // Плоский интерфейс: сглаживание и анизотропия ничего не дают, а стоят памяти и заполнения.
            QualitySettings.antiAliasing = 0;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
            if (Application.isMobilePlatform)
            {
                Screen.orientation = ScreenOrientation.Portrait;
            }

            Tweens.Install(gameObject);

            // Форма клубка (640 px, четыре слоя намотки) рисуется в фоне, пока грузится остальное.
            System.Threading.Tasks.Task.Run(BallRaster.Warm);

            Reminders.CancelAll();
            Sfx.Install(gameObject);
            Levels = new LevelRepository();
            Progress = new ProgressService(SaveStore.Load(), Levels.Count);

            // Значки, медали, монета, подарок, рамки — заранее и в фоне, по приоритету (сначала главная и выбранная
            // рамка альбома): при первом открытии альбома или наград кадр не тратится на рисование.
            Art2.Warm(Progress.Data.frame);
            Sfx.Enabled = Progress.SoundOn;
            Music.Install(gameObject, Progress.Data.musicOn);
            Haptics.Enabled = Progress.Data.hapticsOn;
            Comfort.Apply(Progress.Data);
            L.Set(LanguageFor(Progress.Data.language));
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

                // После перерыва в 3+ дня — посылка от бабушки, за ней подарок дня. Иначе подарок дня встречает
                // игрока сразу при входе — повод заходить каждый день.
                if (Progress.Visit(Today))
                {
                    ShowPopup(new ComebackPopup(this, ShowDailyIfWaiting));
                }
                else
                {
                    OfferDailyGift();
                }
            }
            else
            {
                ShowIntro();
            }

            // Заставка поверх всего: нитка сматывается в клубок, пока в фоне рисуются клубки.
            Splash.Show(_canvas!, Palette.Ground);
            Unsettle(2.2f);
        }

        private void ShowDailyIfWaiting()
        {
            ShowHome();
            OfferDailyGift();
        }

        /// <summary>Подарок дня встречает игрока сразу, как только он уже прошёл хотя бы одну картинку: при входе и при возврате в новый день.</summary>
        private void OfferDailyGift()
        {
            // Окно уже открыто (например, подарок показали раньше): второго поверх не ставим.
            if (_popups.Count > 0)
            {
                return;
            }

            if (Progress.CanClaimDaily(Today) && Progress.Data.completed.Count > 0)
            {
                ShowPopup(new DailyGiftPopup(this, ShowHome));
            }
        }

        /// <summary>День, в который игру свернули (-1 — не сворачивали): по нему видно, что за время паузы наступил новый.</summary>
        private int _pausedDay = -1;

        /// <summary>Новый день наступил при свёрнутой игре, но главная была закрыта (уровень, окно): подарок покажет следующая главная.</summary>
        private bool _newDayPending;

        private void Update()
        {
            while (_mainThread.TryDequeue(out var action))
            {
                action();
            }

            Art2.Pump();

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

            _screen?.StepDeferred();
            Comfort.Governor(Time.unscaledDeltaTime);
            Comfort.Idle(Time.unscaledDeltaTime, Touched(), _screen is GameScreen);
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                Save();
                (_screen as GameScreen)?.AutoPause();
                Reminders.Schedule(Progress, Today);
                _pausedDay = Today;
            }
            else
            {
                Reminders.CancelAll();
                if (Progress == null)
                {
                    return;
                }

                // Пока игра была свёрнута, наступил новый день (часы назад новым днём не считаются).
                var newDay = _pausedDay >= 0 && Today > _pausedDay;
                _pausedDay = -1;

                // Вернулись после долгого перерыва, не перезапуская игру: посылка на главной.
                var comeback = Progress.Visit(Today);
                if (_popups.Count > 0 || !(_screen is HomeScreen))
                {
                    _newDayPending |= newDay;
                    return;
                }

                if (comeback)
                {
                    ShowPopup(new ComebackPopup(this, ShowDailyIfWaiting));
                }
                else if (newDay)
                {
                    // Как при холодном старте: главная перестраивается (значки «ждёт» и подписи за новый день), подарок дня встречает.
                    _newDayPending = false;
                    RefreshScreen();
                    OfferDailyGift();
                }
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

        public void ShowHome()
        {
            Show(() => new HomeScreen(this));

            // Новый день наступил, пока игра была свёрнута посреди уровня или окна: подарок встречает на главной.
            if (_newDayPending && _popups.Count == 0)
            {
                _newDayPending = false;
                OfferDailyGift();
            }
        }

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

            // «Не сдаёмся»: с 4-й попытки нового уровня один раз дарим помощь на эту попытку.
            var stuck = Progress.TakeStuckHelp(levelId, attempt);
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
            if (stuck)
            {
                // Бесплатные ножницы, если они на этом уровне открыты, идут тем же путём, что подарок серии: запас
                // и звезда не тратятся. Подсказку хода включает сам экран по run.StuckHelp.
                run.StuckHelp = true;
                if (Progress.IsBoosterOpen(BoosterType.Scissors, levelId))
                {
                    run.FreeScissors = System.Math.Max(run.FreeScissors, 1);
                }

                Analytics.Event("stuck_help", ("level", levelId), ("attempts", attempt));
            }

            Analytics.Event("level_start", ("level", levelId), ("attempt", attempt), ("hard", level.hard), ("timed", level.timeLimit > 0),
                ("completed_before", Progress.IsCompleted(levelId)), ("warm", warm.Any));
            Show(() => new GameScreen(this, level, game, run));
            var perksIntro = Progress.TakePerksIntro(levelId);
            if (perksIntro)
            {
                Save();
            }

            if (intro != null)
            {
                // Новый бустер и бонусы пряжи на одном уровне: окна идут по очереди, второе после закрытия первого.
                ShowPopup(new BoosterIntroPopup(this, intro.Value, perksIntro ? () => ShowPopup(new YarnPerksIntroPopup(this)) : (Action?)null));
            }
            else if (perksIntro)
            {
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
            Progress.RecordMarathon(Today, stats.Stitches);
            var granted = Progress.CompletePattern(Today, Random);
            Save();
            ShowHome();
            if (granted != null)
            {
                var streak = Progress.PatternStreak(Today);
                ShowPopup(new RewardPopup(this, streak > 1 ? L.F("Узор дня · {0} дн. подряд", streak) : L.T("Узор дня"), Icon.Star, granted.Value, ShowHome));
            }
            else
            {
                Toast(L.F("Узор дня уже связан. Новый через {0}", Limits.ResetIn(Limits.NextMidnight())));
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
            var wasDone = Progress.IsCompleted(level.id);
            var oldStars = Progress.StarsOf(level.id);
            JustAdded = wasDone ? JustAdded : level.id;
            Progress.RecordLevel(stats.Stitches, stats.Rushes, stats.GoldCaught, stats.BestCombo);
            Progress.RecordMarathon(Today, stats.Stitches);
            Progress.RecordResult(level.id, stats.Taps, !stats.Helped);
            var missionsDone = Progress.RecordMissions(Today, stars, !stats.Helped, stats.GoldCaught, stats.Rushes, stats.BestCombo, stats.Stitches);
            var framesBefore = Progress.CollectionFramesOwned;
            var reward = Progress.CompleteLevel(level.id, level.hard, stars, stats.RushCoins + stats.GoldCoins, Today);
            Save();
            var news = new WinNews(Progress.PendingClaims > pendingBefore, Progress.CanOpenChest && !chestBefore, missionsDone > 0,
                Progress.CanClaimMissions(Today), wasDone && stars > oldStars, wasDone);
            Show(() => new WinScreen(this, level, reward, stats, news));

            // Все звёзды коллекции — её рамка открыта.
            if (Progress.CollectionFramesOwned > framesBefore)
            {
                var collection = System.Array.IndexOf(Collections.All, Collections.Of(level.id));
                var frame = AlbumFrames.ForCollection(collection);
                Toast(L.F("Новая рамка «{0}»: все звёзды коллекции!", frame.Name));
                Analytics.Event("collection_frame", ("collection", collection));
            }
        }

        /// <summary>«Дальше» уже нажали: реклама или переход идут, повторный вход не нужен. Сбрасывается при любой смене экрана.</summary>
        private bool _continuing;

        /// <summary>Номер последнего «Дальше»: страховочный таймер старого нажатия не трогает новое.</summary>
        private int _continueId;

        /// <summary>
        /// «Дальше» после победы: при необходимости реклама, потом следующий уровень. Повторный тап (двойной тап,
        /// тап во время рекламы) игнорируется: иначе уровень стартовал бы дважды, а межуровневая реклама тратилась бы заново.
        /// Если ответ рекламы не пришёл за 30 секунд, переход всё равно случится (один раз).
        /// </summary>
        public void ContinueAfterWin(int levelId)
        {
            if (_continuing)
            {
                return;
            }

            _continuing = true;
            var id = ++_continueId;
            var finished = false;

            void Next()
            {
                if (finished || id != _continueId || !_continuing)
                {
                    return;
                }

                finished = true;
                _continuing = false;
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
                After(30f, () =>
                {
                    if (id == _continueId && _continuing)
                    {
                        Next();
                    }
                });
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
            popup.Enter();
            Unsettle(0.6f);
            return popup;
        }

        public void RemovePopup(Popup popup)
        {
            _popups.Remove(popup);
            Unsettle(0.3f);
        }

        /// <summary>Идёт показ видео за награду: второй тап по кнопке «за видео» не запускает второй показ.</summary>
        private DateTime _rewardedUntil = DateTime.MinValue;

        /// <summary>Номер последнего показа: поздний ответ старого не снимает занятость нового.</summary>
        private int _rewardedToken;

        /// <summary>Сколько секунд показ видео считается идущим, если ответ не пришёл.</summary>
        private const double RewardedTimeout = 60.0;

        /// <summary>
        /// Единый путь видео за награду. granted вызывается только после Watched (лимиты и награду тратит он);
        /// при Skipped и Unavailable игрок получает тост с причиной, а окно, из которого он пришёл, остаётся как было.
        /// notGranted (необязательно) вызывается в этих двух случаях, чтобы вернуть кнопку в исходное состояние.
        /// </summary>
        public void ShowRewarded(string placement, Action granted, Action? notGranted = null)
        {
            if (DateTime.UtcNow < _rewardedUntil)
            {
                Toast(L.T("Видео ещё идёт"));
                notGranted?.Invoke();
                return;
            }

            _rewardedUntil = DateTime.UtcNow.AddSeconds(RewardedTimeout);
            var token = ++_rewardedToken;
            Ads.ShowRewarded(placement, result =>
            {
                if (token == _rewardedToken)
                {
                    _rewardedUntil = DateTime.MinValue;
                }

                switch (result)
                {
                    case RewardedResult.Watched:
                        granted();
                        break;
                    case RewardedResult.Skipped:
                        Toast(L.T("Награда приходит за видео, досмотренное до конца"));
                        notGranted?.Invoke();
                        break;
                    default:
                        Toast(L.T("Видео сейчас нет. Проверь интернет или попробуй чуть позже"));
                        notGranted?.Invoke();
                        break;
                }
            });
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

            // Плашка падает сверху на пружине и, отслужив, уплывает вверх, растворяясь.
            Tweens.Fade(group, 0f, 1f, 0.18f);
            var hold = Mathf.Clamp(1.2f + text.Length * 0.025f, 1.9f, 3.6f);
            if (!Tweens.Calm)
            {
                Tweens.Offset(root, new Vector2(0, 30f), Vector2.zero, 0.42f, Ease.OutSoftBack);
                root.localScale = Vector3.one * 0.92f;
                Tweens.Run(root, 0.4f, t => root.localScale = Vector3.one * Mathf.LerpUnclamped(0.92f, 1f, t), Ease.Spring);
                Tweens.Offset(root, Vector2.zero, new Vector2(0, 18f), 0.3f, Ease.InCubic, null, hold);
            }

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

        /// <summary>Применить настройки удобства и сохранить.</summary>
        public void ApplyComfort()
        {
            Comfort.Apply(Progress.Data);
            Save();
        }

        /// <summary>
        /// Перестроить открытый экран на месте (крупный текст ложится только на новые подписи). Уровень не трогаем:
        /// партия идёт, перестройка его сбросила бы — там текст обновится на следующем экране.
        /// </summary>
        public void RefreshScreen()
        {
            if (_route != null && !(_screen is GameScreen))
            {
                Show(_route);
            }
        }

        public void SetMusic(bool on)
        {
            Progress.Data.musicOn = on;
            Music.Enabled = on;
            Save();
        }

        /// <summary>Сменить язык: весь интерфейс строится заново, настройки открываются снова.</summary>
        public void SetLanguage(string language)
        {
            Progress.Data.language = language;
            Save();

            // Весь интерфейс строится заново. Чтобы не было рывков (шторка уезжает, главная пересобирается,
            // шторка выезжает снова), экран на мгновение закрывается заливкой цвета фона: под ней всё
            // перестраивается без видимых анимаций, и заливка мягко уходит.
            var cover = Ui.Image(_canvas!, "LanguageCover", null, _background != null ? _background.color : Palette.Ground, true);
            Ui.Stretch(cover.rectTransform);
            cover.transform.SetAsLastSibling();
            var group = cover.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            Tweens.Fade(group, 0f, 1f, 0.16f, () =>
            {
                L.Set(LanguageFor(language));
                CloseAllPopups();
                ShowHome();
                ShowPopup(new SettingsPopup(this));
                cover.transform.SetAsLastSibling();
                Tweens.Fade(group, 1f, 0f, 0.28f, () => Destroy(cover.gameObject), 0.35f);
            });
        }

        /// <summary>
        /// Выбранный язык, а если не выбран — язык телефона: русский для русского, украинского и белорусского,
        /// иначе английский. QA-бот ищет кнопки по русским подписям, поэтому в его сборке всегда русский.
        /// </summary>
        private static string LanguageFor(string saved)
        {
#if KNITTED_QA
            return L.Russian;
#else
            if (saved == L.Russian || saved == L.English)
            {
                return saved;
            }

            switch (Application.systemLanguage)
            {
                case SystemLanguage.Russian:
                case SystemLanguage.Ukrainian:
                case SystemLanguage.Belarusian:
                    return L.Russian;
                default:
                    return L.English;
            }
#endif
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
            // Настройки, покупка и ответ про рекламу — не игровой прогресс: после сброса они остаются,
            // и переключатели в настройках показывают то, что действительно работает.
            var fresh = Progress.Data.FreshKeepingSettings();
            SaveStore.Reset();
            Progress = new ProgressService(fresh, Levels.Count);
            Sfx.Enabled = Progress.SoundOn;
            Music.Enabled = Progress.Data.musicOn;
            Haptics.Enabled = Progress.Data.hapticsOn;
            Comfort.Apply(Progress.Data);
            HomeScreen.ResetCarousel();
            SaveStore.Save(Progress.Data);
            ShowIntro();
        }

        /// <summary>
        /// Оттенок фона: экран задаёт свой (главная и уровень — по коллекции), остальные — общий светло-серый.
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
            _continuing = false;
            SetGround(Palette.Ground);
            CloseAllPopups();
            PreviousScreen = _screen?.GetType();
            var old = _screen;
            _route = route;
            _screen = route();

            // Тот же экран перестраивается (главная после награды, перезапуск уровня) — мгновенно, без мигания;
            // другой — старый гаснет под новым, а новый проявляется, и его блоки всплывают волной.
            var slide = _slide;
            _slide = 0;
            Unsettle(0.65f);
            if (old != null && old.GetType() == _screen.GetType() && slide == 0)
            {
                old.Dispose();
            }
            else
            {
                var hadBar = old != null && old.Root.Find("TabBar") != null;
                old?.Leave(slide);
                _screen.Enter(slide, hadBar);
            }

            var hero = _hero;
            _hero = null;
            if (hero != null && _screen is GameScreen level && !Tweens.Calm)
            {
                FlyHero(level, hero.Value);
            }

            // На уровне экран не гаснет, пока игрок думает над ходом; вне уровня — как в системе.
            Screen.sleepTimeout = _screen is GameScreen ? SleepTimeout.NeverSleep : SleepTimeout.SystemSetting;

            // Старый экран снят: лишние текстуры клубков освобождаем, когда он исчезнет со сцены.
            Tweens.Delay(this, 0.45f, Art2.Trim);
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
                    top.Dismiss();
                }

                return;
            }

            if (_screen != null && !_screen.OnBack())
            {
                ShowHome();
            }
        }

        /// <summary>Палец или мышь нажаты в этом кадре (для простоя: касание возвращает полную частоту кадров).</summary>
        private static bool Touched()
        {
#if ENABLE_INPUT_SYSTEM
            var pointer = UnityEngine.InputSystem.Pointer.current;
            return pointer != null && pointer.press.isPressed;
#else
            return Input.GetMouseButton(0) || Input.touchCount > 0;
#endif
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
                Lean(Camera.main);
                return;
            }

            var go = new GameObject("Camera");
            go.transform.SetParent(transform, false);
            var camera = go.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Palette.Ground;
            camera.orthographic = true;
            Lean(camera);
            go.tag = "MainCamera";
        }

        /// <summary>
        /// Камера только чистит экран цветом фона (интерфейс рисует Canvas поверх): ей нечего отсекать и
        /// незачем держать буферы сглаживания и HDR — это лишняя память и пропускная способность на слабом GPU.
        /// </summary>
        private static void Lean(Camera camera)
        {
            camera.cullingMask = 0;
            camera.allowMSAA = false;
            camera.allowHDR = false;
            camera.useOcclusionCulling = false;
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
            _popupLayer = Ui.Layer(Ui.Stretch(Ui.Node(_canvas, "Popups")), true);
            ApplyLayout();
        }

        /// <summary>
        /// Масштаб: на вытянутых телефонах ширина макета 390, на широких экранах — высота 844 (на планшете 1000).
        /// Контент — колонка внутри безопасной зоны, не шире 430 (на планшете 540).
        /// </summary>
        private void ApplyLayout()
        {
            _screenSize = new Vector2Int(Screen.width, Screen.height);
            _safeArea = Screen.safeArea;
            Tablet = IsTablet();
            var designHeight = Tablet ? TabletHeight : DesignHeight;
            var tall = (float)Screen.height / Mathf.Max(1, Screen.width) >= designHeight / DesignWidth;
            var scale = tall ? Screen.width / DesignWidth : Screen.height / designHeight;
            _scaler!.scaleFactor = scale;

            CanvasWidth = Screen.width / scale;
            var left = _safeArea.xMin / scale;
            var bottom = _safeArea.yMin / scale;
            var safeWidth = _safeArea.width / scale;
            Width = Mathf.Min(safeWidth, Tablet ? TabletMaxWidth : MaxWidth);
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

        private static bool IsTablet()
        {
            float shortSide = Mathf.Min(Screen.width, Screen.height);
            float longSide = Mathf.Max(Screen.width, Screen.height);
            if (Screen.dpi > 0f)
            {
                return shortSide / Screen.dpi >= TabletShortSideInches;
            }

            return longSide / Mathf.Max(1f, shortSide) < 1.7f;
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
