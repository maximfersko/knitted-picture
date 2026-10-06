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
        private Func<ScreenView>? _route;
        private Vector2Int _screenSize;
        private Rect _safeArea;

        public static App? Instance { get; private set; }

        public LevelRepository Levels { get; private set; } = null!;

        public ProgressService Progress { get; private set; } = null!;

        public IAds Ads { get; private set; } = null!;

        /// <summary>Контентная колонка внутри безопасной зоны.</summary>
        public RectTransform Frame => _frame!;

        public RectTransform PopupLayer => _popupLayer!;

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
            Sfx.Install(gameObject);
            Levels = new LevelRepository();
            Progress = new ProgressService(SaveStore.Load(), Levels.Count);
            Sfx.Enabled = Progress.SoundOn;
            Ads = new StubAds(this);

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
            }
        }

        private void OnApplicationQuit()
        {
            if (!SaveStore.WasCleared)
            {
                Save();
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
            Save();
            var game = new LoomGame(level);
            var run = new LevelRun();
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

        public void OnLevelWon(LevelData level, int stars, LevelStats stats)
        {
            var pendingBefore = Progress.PendingClaims;
            var chestBefore = Progress.CanOpenChest;
            Progress.RecordLevel(stats.Stitches, stats.Rushes, stats.GoldCaught, stats.BestCombo);
            Progress.RecordResult(level.id, stats.Taps, !stats.Helped);
            var reward = Progress.CompleteLevel(level.id, level.hard, stars, stats.RushCoins + stats.GoldCoins);
            Save();
            var news = new WinNews(Progress.PendingClaims > pendingBefore, Progress.CanOpenChest && !chestBefore);
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

        /// <summary>
        /// Стереть прогресс: новое сохранение, все окна закрыты, экраны строятся заново
        /// (главная, сундук, коллекции читают только Progress — старого состояния не остаётся).
        /// </summary>
        public void ResetProgress()
        {
            CloseAllPopups();
            SaveStore.Reset();
            Progress = new ProgressService(new PlayerProgress(), Levels.Count);
            Sfx.Enabled = Progress.SoundOn;
            HomeScreen.ResetCarousel();
            SaveStore.Save(Progress.Data);
            ShowIntro();
        }

        private void Show(Func<ScreenView> route)
        {
            CloseAllPopups();
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
