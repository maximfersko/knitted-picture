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
            Root = Ui.Stretch(Ui.Node(app.Frame, name));
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
            // Окно как лоскут ткани: строчка по краю.
            Ui.Stitch(Card, bottomSheet ? 30 : 26, Palette.Stitch, 8f);
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
            Tweens.Fade(_group, 0f, 1f, 0.18f);
            if (bottomSheet)
            {
                var from = -400f;
                Tweens.Run(Card, 0.28f, t => Card.anchoredPosition = new Vector2(0, Mathf.Lerp(from, -30f, t)), Ease.OutCubic);
            }
            else
            {
                Tweens.Scale(Card, 0.9f, 1f, 0.22f);
            }
        }

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
            Root.GetComponent<CanvasGroup>().blocksRaycasts = false;
            Tweens.Fade(_group, 1f, 0f, 0.15f, Destroy);
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
            Ui.Stitch(root, 20, Palette.StitchStrong, 5f);
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
