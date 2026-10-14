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

        /// <summary>Кнопка «назад» на Android. false — уйти на главную.</summary>
        public virtual bool OnBack() => false;

        public virtual void Dispose()
        {
            Object.Destroy(Root.gameObject);
        }

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
            var group = Ui.Group(Root);
            Tweens.Fade(group, 0f, 1f, slide != 0 ? 0.16f : 0.2f);
            var bar = Root.Find("TabBar") as RectTransform;
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
            if (slide != 0)
            {
                var bar = Root.Find("TabBar");
                for (var i = 0; i < Root.childCount; i++)
                {
                    if (Root.GetChild(i) is RectTransform child && child != bar)
                    {
                        Tweens.Offset(child, Vector2.zero, new Vector2(-slide * 40f, 0), 0.16f, Ease.OutCubic);
                    }
                }
            }

            Tweens.Run(Root, 0.16f, t =>
            {
                group.alpha = from * (1f - t);
                if (slide == 0)
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
                    Close();
                }
            });

            CardWidth = bottomSheet ? Mathf.Min(app.CanvasWidth, 430f) : Mathf.Min(app.Width - 40f, 340f);
            Card = Ui.Node(Root, "Card");
            var face = Ui.Box(Card, "Face", bottomSheet ? 30 : 26, Palette.Card, true);
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
            if (_bottomSheet)
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

        public void Close()
        {
            App.RemovePopup(this);
            _group.blocksRaycasts = false;

            // Закрыли, пока окно ещё выпрыгивало: выезд останавливается и не спорит с уходом.
            Tweens.Kill(Card, Motion);
            Tweens.Kill(_group);
            if (_bottomSheet)
            {
                Tweens.Offset(Card, Vector2.zero, new Vector2(0, -(Card.sizeDelta.y + 40f)), 0.22f, Ease.InCubic);
            }
            else
            {
                var scale = Card.localScale.x;
                Tweens.Run(Card, 0.16f, t => Card.localScale = Vector3.one * Mathf.Lerp(scale, 0.92f, t), Ease.InCubic);
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

            // Закрыть можно не только тапом мимо окна: крестик 44 pt / 48 dp в углу, как принято на iOS и Android.
            if (Closable)
            {
                Ui.RoundButton(Card, Icon.Close, CardWidth - 44f - 10f, 10f, 44f, true).OnClick(Close);
            }
        }

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

        protected Tap AddPrimary(string text, float height = 60f)
        {
            var tap = Ui.Button(Card, text, 24, _cursor, Inner, height, Palette.Primary, Palette.PrimaryShadow, Color.white, 20);
            _cursor += height + 16f;
            return tap;
        }

        protected Tap AddSecondary(string text, Icon? icon = null, float height = 54f)
        {
            var root = Ui.Card(Card, "Secondary", 24, _cursor, Inner, height, 20, Palette.Card, Palette.CardShadow, 3f, true);
            Ui.Outline(root, 20, Palette.CardShadow, 2f);
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
