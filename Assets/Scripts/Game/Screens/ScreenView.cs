#nullable enable

using UnityEngine;

namespace KnittedPicture.Game
{
    /// <summary>Экран: строит себя в колонке App.Frame и уничтожается при переходе.</summary>
    public abstract class ScreenView
    {
        protected ScreenView(App app, string name)
        {
            App = app;
            Root = Ui.Layer(Ui.Stretch(Ui.Node(app.Frame, name)), true);
        }

        public RectTransform Root { get; }

        protected App App { get; }

        protected float W => App.Width;

        protected float H => App.Height;

        public virtual void Tick(float dt)
        {
        }

        private readonly System.Collections.Generic.Queue<System.Action> _deferred = new System.Collections.Generic.Queue<System.Action>();

        /// <summary>
        /// Построить позже: то, что ниже первого экрана (карточки альбома, строки достижений, плитки пряжи),
        /// достраивается в следующих кадрах по несколько миллисекунд. Экран открывается сразу, без долгого кадра,
        /// и анимация перехода не дёргается.
        /// </summary>
        protected void Defer(System.Action build) => _deferred.Enqueue(build);

        /// <summary>Раз в кадр (App.Update): достроить отложенное, не дольше ~4 мс за кадр.</summary>
        public void StepDeferred()
        {
            if (_deferred.Count == 0)
            {
                return;
            }

            if (Root == null)
            {
                _deferred.Clear();
                return;
            }

            var watch = System.Diagnostics.Stopwatch.StartNew();
            do
            {
                _deferred.Dequeue()();
            }
            while (_deferred.Count > 0 && watch.ElapsedMilliseconds < 4);
        }

        /// <summary>Кнопка «назад» на Android. false — уйти на главную.</summary>
        public virtual bool OnBack() => false;

        public virtual void Dispose()
        {
            Object.Destroy(Root.gameObject);
        }

        /// <summary>Время перелистывания между вкладками.</summary>
        private const float PageSlide = 0.3f;

        /// <summary>Разносить ли блоки экрана волной при входе. Экраны со своей постановкой входа отключают.</summary>
        protected virtual bool CascadeOnEnter => true;

        /// <summary>
        /// Вход: экран проявляется, а его блоки (шапка, карточки, кнопки) всплывают на место волной сверху вниз,
        /// с шагом в несколько сотых секунды. Фоны во весь экран только проявляются.
        /// По вкладке (slide ±1) блоки разом въезжают сбоку, без волны.
        /// Нижняя панель вкладок общая для разделов: при переходе между ними она не мигает и не ездит, на ней только
        /// переезжает метка вкладки (hadBar — у прошлого экрана панель была). С уровня панель выезжает снизу.
        /// </summary>
        public void Enter(int slide = 0, bool hadBar = false)
        {
            // Между вкладками — как перелистывание страниц: экран целиком въезжает сбоку, старый уезжает в другую
            // сторону. Двигается только корень экрана (у него свой Canvas): содержимое не пересобирается каждый кадр,
            // прозрачность не меняется — плавно даже на слабых телефонах. Панель вкладок общая: она едет навстречу
            // корню и остаётся на месте.
            if (slide != 0 && !Tweens.Calm)
            {
                var tabs = Root.Find("TabBar") as RectTransform;
                var distance = Root.rect.width;
                Tweens.Offset(Root, new Vector2(slide * distance, 0f), Vector2.zero, PageSlide, Ease.OutCubic);
                if (tabs != null)
                {
                    Ui.Layer(tabs, true);
                    if (hadBar)
                    {
                        Tweens.Offset(tabs, new Vector2(-slide * distance, 0f), Vector2.zero, PageSlide, Ease.OutCubic);
                    }
                    else
                    {
                        Tweens.Offset(tabs, new Vector2(0, -(tabs.sizeDelta.y + 12f)), Vector2.zero, 0.42f, Ease.OutSoftBack);
                    }
                }

                return;
            }

            var group = Ui.Group(Root);
            Tweens.Fade(group, 0f, 1f, slide != 0 ? 0.16f : 0.2f);
            var bar = Root.Find("TabBar") as RectTransform;
            if (Tweens.Calm)
            {
                // «Меньше движения»: только мягкое проявление, ничего не едет.
                if (bar != null && hadBar)
                {
                    Ui.Group(bar).ignoreParentGroups = true;
                }

                return;
            }

            if (bar != null && hadBar)
            {
                var own = Ui.Group(bar);
                own.ignoreParentGroups = true;
            }
            else if (bar != null)
            {
                Tweens.Offset(bar, new Vector2(0, -(bar.sizeDelta.y + 12f)), Vector2.zero, 0.42f, Ease.OutSoftBack);
            }

            if (slide == 0 && !CascadeOnEnter)
            {
                return;
            }

            var corners = new Vector3[4];
            Root.GetWorldCorners(corners);
            var top = corners[1].y;
            var height = Mathf.Max(1f, corners[1].y - corners[0].y);
            for (var i = 0; i < Root.childCount; i++)
            {
                if (!(Root.GetChild(i) is RectTransform child) || !child.gameObject.activeSelf || child == bar)
                {
                    continue;
                }

                if (slide != 0)
                {
                    Tweens.Offset(child, new Vector2(slide * 56f, 0), Vector2.zero, 0.34f, Ease.OutQuint);
                    continue;
                }

                child.GetWorldCorners(corners);
                var size = corners[1].y - corners[0].y;
                if (size <= 0.5f || size > height * 0.8f)
                {
                    continue;
                }

                var depth = Mathf.Clamp01((top - corners[1].y) / height);
                Tweens.Appear(child, 0.02f + 0.24f * depth, 18f);
            }
        }

        /// <summary>
        /// Уход: экран гаснет и чуть уменьшается, пока поверх проявляется новый; нажать на него уже нельзя.
        /// По вкладке блоки уезжают в сторону, противоположную новым. Потом экран уничтожается, как при Dispose.
        /// </summary>
        public void Leave(int slide = 0)
        {
            var group = Ui.Group(Root);
            group.blocksRaycasts = false;
            group.interactable = false;
            var from = group.alpha;
            Tweens.Kill(Root);
            Tweens.Kill(group);
            if (slide != 0 && !Tweens.Calm)
            {
                // Перелистывание: уезжает целиком (см. Enter), панель вкладок стоит, потом экран уничтожается.
                var tabs = Root.Find("TabBar") as RectTransform;
                var distance = Root.rect.width;
                Tweens.Offset(Root, Vector2.zero, new Vector2(-slide * distance, 0f), PageSlide, Ease.OutCubic, Dispose);
                if (tabs != null)
                {
                    Ui.Layer(tabs, true);
                    Tweens.Offset(tabs, Vector2.zero, new Vector2(slide * distance, 0f), PageSlide, Ease.OutCubic);
                }

                return;
            }

            Tweens.Run(Root, 0.16f, t =>
            {
                group.alpha = from * (1f - t);
                if (slide == 0 && !Tweens.Calm)
                {
                    Root.localScale = Vector3.one * (1f - 0.025f * t);
                }
            }, Ease.OutCubic, Dispose);
        }
    }

    /// <summary>
    /// Всплывающее окно поверх экрана: затемнение и карточка. Карточка строится
    /// сверху вниз через Add*, высота считается сама.
    /// </summary>
    public abstract class Popup
    {
        private readonly bool _bottomSheet;
        private readonly CanvasGroup _group;
        private float _cursor;

        protected Popup(App app, bool bottomSheet = false)
        {
            App = app;
            _bottomSheet = bottomSheet;
            Root = Ui.Stretch(Ui.Node(app.PopupLayer, GetType().Name));
            _group = Root.gameObject.AddComponent<CanvasGroup>();
            var dim = Ui.Image(Root, "Dim", null, Palette.Dim, true);
            Ui.Stretch(dim.rectTransform);
            var dimTap = dim.gameObject.AddComponent<Tap>();
            dimTap.ClickSound = false;
            dimTap.PressFeedback = false;
            dimTap.OnClick(() =>
            {
                if (Closable && CloseOnDim)
                {
                    Dismiss();
                }
            });

            CardWidth = bottomSheet ? Mathf.Min(app.CanvasWidth, Mathf.Max(430f, app.Width)) : Mathf.Min(app.Width - 40f, app.Tablet ? 400f : 340f);
            Card = Ui.Node(Root, "Card");
            // Радиус окна и шторки 30: крестик (44) и «назад» стоят в углу с отступом 8, их радиус 22, 22 + 8 = 30, углы концентричны.
            var face = Ui.Box(Card, "Face", 30, Palette.Card, true);
            Ui.Stretch(face.rectTransform);
            if (bottomSheet)
            {
                Card.anchorMin = new Vector2(0.5f, 0f);
                Card.anchorMax = new Vector2(0.5f, 0f);
                Card.pivot = new Vector2(0.5f, 0f);
                Card.anchoredPosition = new Vector2(0, -30f);
            }
            else
            {
                Card.anchorMin = new Vector2(0.5f, 0.5f);
                Card.anchorMax = new Vector2(0.5f, 0.5f);
                Card.pivot = new Vector2(0.5f, 0.5f);
                Card.anchoredPosition = Vector2.zero;
            }

            _cursor = 28f;
            Sfx.Play(Sound.Puff, 0.35f);

            // До Enter окно прозрачно: содержимое ещё строится, а появление зависит от итоговой высоты.
            _group.alpha = 0f;
        }

        /// <summary>
        /// Появление (зовёт App.ShowPopup, когда окно построено): затемнение проявляется, карточка выпрыгивает
        /// на пружине с лёгким перелётом (лист снизу выезжает на всю свою высоту), а строки карточки
        /// всплывают по очереди сверху вниз.
        /// </summary>
        public void Enter()
        {
            Tweens.Fade(_group, 0f, 1f, 0.2f);
            if (Tweens.Calm)
            {
                // «Меньше движения»: окно просто проявляется.
            }
            else if (_bottomSheet)
            {
                Tweens.Offset(Card, new Vector2(0, -(Card.sizeDelta.y + 40f)), Vector2.zero, 0.42f, Ease.OutSoftBack, null, 0f, Motion);
            }
            else
            {
                Card.localScale = Vector3.one * 0.86f;
                Tweens.Run(Card, 0.44f, t => Card.localScale = Vector3.one * Mathf.LerpUnclamped(0.86f, 1f, t), Ease.Spring, null, 0f, false, Motion);
                Tweens.Offset(Card, new Vector2(0, -24f), Vector2.zero, 0.36f, Ease.OutSoftBack, null, 0f, Motion);
            }

            var order = 0;
            for (var i = 0; i < Card.childCount; i++)
            {
                if (Card.GetChild(i) is RectTransform child && child.gameObject.activeSelf && child.name != "Face")
                {
                    Tweens.Appear(child, 0.06f + Mathf.Min(0.24f, order++ * 0.035f), 10f, 0.3f);
                }
            }
        }

        /// <summary>Канал твинов появления и ухода карточки: закрытие останавливает только их, а не таймеры окна.</summary>
        private const int Motion = 17;

        public RectTransform Root { get; }

        public virtual bool Closable => true;

        protected virtual bool CloseOnDim => true;

        protected App App { get; }

        protected RectTransform Card { get; }

        protected float CardWidth { get; }

        protected float Inner => CardWidth - 48f;

        /// <summary>Где начнётся следующий элемент карточки.</summary>
        protected float Cursor => _cursor;

        /// <summary>
        /// Что сделать, когда окно закрылось — любым способом: кнопкой, тапом мимо или «назад» на Android.
        /// Так экран под окном всегда обновится (например, после полученной награды).
        /// </summary>
        protected System.Action? Closed { get; set; }

        /// <summary>
        /// Родительское окно вложенной шторки («Настройки» для «Удобства» и «Об игре», «Пауза» для «Удобства» из уровня).
        /// Задано — крестик, тап мимо и системная «Назад» возвращают в него, а не закрывают всё.
        /// </summary>
        protected System.Action? Back { get; set; }

        /// <summary>
        /// Закрыть по желанию игрока (крестик, тап мимо, «Назад»): у вложенной шторки это возврат в родительское окно,
        /// у остальных окон — обычное закрытие. Окно меняется в том же кадре, поэтому таймер уровня не успевает пойти.
        /// </summary>
        public void Dismiss()
        {
            var back = Back;
            Close();
            back?.Invoke();
        }

        private bool _closed;

        /// <summary>Окно ещё открыто: не закрыто и не уничтожено (экран мог перестроиться, пока шло видео).</summary>
        public bool IsOpen => !_closed && Root != null;


        public void Close()
        {
            // Повторный Close (или Close уже уничтоженного окна) ничего не трогает.
            if (_closed || Root == null)
            {
                return;
            }

            _closed = true;
            App.RemovePopup(this);
            _group.blocksRaycasts = false;

            // Закрыли, пока окно ещё выпрыгивало: выезд останавливается и не спорит с уходом.
            Tweens.Kill(Card, Motion);
            Tweens.Kill(_group);

            // Уход с замедлением (OutCubic): на выходе окно сразу реагирует на касание, а не разгоняется из покоя.
            if (Tweens.Calm)
            {
                // Только гаснет.
            }
            else if (_bottomSheet)
            {
                Tweens.Offset(Card, Vector2.zero, new Vector2(0, -(Card.sizeDelta.y + 40f)), 0.22f, Ease.OutCubic);
            }
            else
            {
                var scale = Card.localScale.x;
                Tweens.Run(Card, 0.16f, t => Card.localScale = Vector3.one * Mathf.Lerp(scale, 0.92f, t), Ease.OutCubic);
            }

            Tweens.Fade(_group, _group.alpha, 0f, 0.17f, Destroy);
            var closed = Closed;
            Closed = null;
            closed?.Invoke();
        }

        public void Destroy()
        {
            if (Root != null)
            {
                Object.Destroy(Root.gameObject);
            }
        }

        /// <summary>Закончить карточку: выставить высоту по содержимому.</summary>
        protected void Finish(float bottomPadding = 24f)
        {
            var extra = _bottomSheet ? 30f + App.BottomInset : 0f;
            Card.sizeDelta = new Vector2(CardWidth, _cursor + bottomPadding + extra);

            // Закрыть можно не только тапом мимо окна: крестик 44 pt / 48 dp в углу (отступ 8), как принято на iOS и Android.
            if (Closable)
            {
                Ui.RoundButton(Card, Icon.Close, CardWidth - 44f - 8f, 8f, 44f, true).OnClick(Dismiss);
            }
        }

        /// <summary>
        /// Сколько высоты осталось под прокручиваемый список, чтобы окно целиком влезло в экран: от уже занятого
        /// (Cursor) и того, что будет ниже списка (footer — кнопки и отступы), с зазором сверху. Не меньше 140.
        /// </summary>
        protected float RoomFor(float footer, float max = float.MaxValue) => Mathf.Clamp(App.Height - 28f - Cursor - footer, 140f, max);

        /// <summary>
        /// Построить элемент списка номер index: первые immediate — сразу, остальные по PerFrame за кадр следом.
        /// Длинные окна (рамки, книга находок) открываются без рывка, а дальние плитки достраиваются, пока окно выезжает.
        /// </summary>
        protected void Stagger(int index, int immediate, System.Action build)
        {
            if (index < immediate)
            {
                build();
                return;
            }

            Tweens.Delay(Card, 0.017f * (1 + (index - immediate) / PerFrame), build);
        }

        private const int PerFrame = 4;

        protected void Space(float height)
        {
            _cursor += height;
        }

        protected RectTransform AddIcon(Icon icon, Color color, Color background, float size = 68f)
        {
            var root = Ui.PlaceCenter(Ui.Node(Card, "IconBadge"), CardWidth / 2f, _cursor + size / 2f, size, size);
            Ui.Stretch(Ui.Image(root, "Back", Art.Circle, background).rectTransform);
            Ui.IconImage(root, icon, color, size / 2f, size / 2f, size * 0.52f);
            _cursor += size + 14f;
            return root;
        }

        protected UnityEngine.UI.Text AddTitle(string text, float size = 26f)
        {
            // Заголовок в самом верху закрываемого окна — на одной высоте с крестиком: оставляем по 58
            // с обеих сторон (симметрично, чтобы текст остался по центру) и ужимаем шрифт, если не влезает.
            var nearClose = Closable && _cursor < 54f;
            var side = nearClose ? 58f : 24f;
            var label = Ui.Label(Card, text, size, Palette.Ink, side, _cursor, CardWidth - 2f * side, size + 10);
            if (nearClose)
            {
                Ui.Fit(label, Mathf.RoundToInt(size * 0.7f));
            }

            _cursor += size + 18f;
            return label;
        }

        /// <summary>
        /// Абзац текста. lines — сколько строк ждём; если текст длиннее (длинное описание, узкий экран),
        /// высота берётся по замеру, и следующий элемент не наезжает на текст.
        /// </summary>
        protected UnityEngine.UI.Text AddText(string text, float size = 16f, float lines = 2f)
        {
            var height = size * 1.3f * lines;
            var label = Ui.Label(Card, text, size, Palette.Ink2, 24, _cursor, Inner, height);
            var measured = label.preferredHeight;
            if (measured > height)
            {
                height = measured;
                label.rectTransform.sizeDelta = new Vector2(Inner, height);
            }

            _cursor += height + 16f;
            return label;
        }

        /// <summary>
        /// Строка лимита или ожидания под текстом (<see cref="Limits.Line"/>): одна или две строки, сама обновляется раз в минуту.
        /// text собирает строку заново, поэтому «через 5 ч» не застывает, пока окно открыто.
        /// </summary>
        protected UnityEngine.UI.Text AddLimit(System.Func<string> text)
        {
            var label = Limits.Line(Card, text(), 24, _cursor, Inner, text);
            var height = label.preferredHeight > 20f ? 32f : 18f;
            _cursor += height + 12f;
            return label;
        }

        protected Tap AddPrimary(string text, float height = 60f)
        {
            var tap = Ui.Button(Card, text, 24, _cursor, Inner, height, Palette.Primary, Palette.PrimaryShadow, Color.white, 20);
            _cursor += height + 16f;
            return tap;
        }

        protected Tap AddSecondary(string text, Icon? icon = null, float height = 54f)
        {
            // Как PairButton: плашка Surface на мягкой тени, без обводки.
            var root = Ui.Card(Card, "Secondary", 24, _cursor, Inner, height, 20, Palette.Surface, Palette.CardShadow, 3f, true);
            var label = Ui.Label(root, text, 17, Palette.Ink, icon != null ? 14 : 0, 0, Inner, height);
            if (icon != null)
            {
                Ui.IconImage(root, icon.Value, Palette.Ink, Inner / 2f - label.preferredWidth / 2f - 6f, height / 2f, 20);
            }

            _cursor += height + 12f;
            return root.gameObject.AddComponent<Tap>();
        }

        /// <summary>
        /// Две равные кнопки рядом: обычно «Заново» и «Домой». Иконка над подписью —
        /// так обе читаются сразу и не путаются с главной кнопкой.
        /// </summary>
        protected (Tap left, Tap right) AddPair(string leftText, Icon leftIcon, string rightText, Icon rightIcon, float height = 64f)
        {
            const float gap = 10f;
            var w = (Inner - gap) / 2f;
            var left = PairButton(24, w, height, leftText, leftIcon);
            var right = PairButton(24 + w + gap, w, height, rightText, rightIcon);
            _cursor += height + 14f;
            return (left, right);
        }

        private Tap PairButton(float x, float w, float h, string text, Icon icon)
        {
            var root = Ui.Card(Card, "Pair", x, _cursor, w, h, 18, Palette.Surface, Palette.CardShadow, 3f, true);
            Ui.IconImage(root, icon, Palette.Ink, w / 2f, 22, 22);
            Ui.Label(root, text, 14, Palette.Ink, 0, 34, w, 24);
            return root.gameObject.AddComponent<Tap>();
        }

        protected Tap AddLink(string text, Icon? icon = null)
        {
            var tap = Ui.TextButton(Card, text, 24, _cursor, Inner, 44, Palette.Ink2, 15, icon);
            _cursor += 48f;
            return tap;
        }
    }
}
