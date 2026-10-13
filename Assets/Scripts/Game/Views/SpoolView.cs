#nullable enable

using KnittedPicture.Core;
using UnityEngine;
using UnityEngine.UI;

namespace KnittedPicture.Game
{
    /// <summary>
    /// Клубок 2.0: глянцевый шар из кручёной нити с ворсом и завитком, мягкая лавандовая тень под ним.
    /// На клубок «надевается» то, что им управляет: лента связки с бантиком (цвет пары), замок на ремешке,
    /// ключик, узелки сверху, ледяной пузырь с трещинами (чем меньше льда, тем больше трещин),
    /// бумажная обёртка «?» у скрытого цвета. Число петель — на бирке-строчке справа внизу.
    /// Задние ряды поля — пастельные, без бирок: главное видно у передних.
    /// </summary>
    public sealed class SpoolView
    {
        private readonly Image _shadow;
        private readonly Image _ball;
        private readonly Text _question;
        private readonly Image _ribbon;
        private readonly Image _lock;
        private readonly Image _key;
        private readonly Image _knots;
        private readonly Image _ice;
        private readonly RectTransform _badge;
        private readonly Text _badgeText;
        private readonly RectTransform _double;
        private readonly RectTransform _surplus;
        private readonly RectTransform _foreign;
        private readonly CanvasGroup _group;
        private Color _color;
        private bool _deep;
        private bool _thick;
        private bool _rainbow;
        private bool _visible = true;
        private bool _canvas;

        private SpoolView(RectTransform root, float size)
        {
            Root = root;
            Size = size;
            _group = root.gameObject.AddComponent<CanvasGroup>();

            // Спрайт шире шара: в нём место под ворс, завиток и тень. Шар занимает BallFill стороны.
            var art = size / Art2.BallFill;
            var cx = size / 2f;
            var cy = size / 2f - size * 0.02f;

            _shadow = Ui.Image(root, "Shadow", Art2.BallShadow, Color.white);
            Ui.PlaceCenter(_shadow.rectTransform, cx + size * 0.04f, size * 0.98f, size * 0.95f, size * 0.3f);

            _ball = Image(root, "Ball", Art2.Ball(Color.white), art, cx, cy, true);
            _question = Ui.Label(root, "?", size * 0.56f, Palette.Link, 0, -size * 0.02f, size, size);
            _ribbon = Image(root, "Ribbon", Art2.Ribbon, art, cx, cy);
            _lock = Image(root, "Lock", Art2.Padlock, art, cx, cy);
            _key = Image(root, "Key", Art2.KeyCharm, art, cx, cy);
            _knots = Image(root, "Knots", Art2.Knots(1), art, cx, cy);
            _ice = Image(root, "Ice", Art2.Ice(0), art, cx, cy);

            var badgeH = Mathf.Round(size * 0.44f);
            _badge = Ui.Place(Ui.Node(root, "Badge"), size - badgeH * 0.78f, size - badgeH * 0.86f, badgeH, badgeH);
            Ui.Stretch(Ui.Image(_badge, "Back", Art2.Badge, Color.white).rectTransform);
            _badgeText = Ui.Label(_badge, "", badgeH * 0.5f, Palette.Ink);
            Ui.Stretch(_badgeText.rectTransform);

            _surplus = Marker(root, "Surplus", Palette.Primary, Icon.Check, -badgeH * 0.15f, size - badgeH * 0.8f, badgeH * 0.85f);
            _foreign = Marker(root, "Foreign", Palette.Danger, Icon.Close, -badgeH * 0.15f, size - badgeH * 0.8f, badgeH * 0.85f);

            // Толстый клубок займёт два места: метка «×2» слева внизу.
            _double = Ui.Place(Ui.Node(root, "Double"), -badgeH * 0.25f, size - badgeH * 0.8f, badgeH * 1.2f, badgeH * 0.8f);
            Ui.Stretch(Ui.Box(_double, "Back", badgeH * 0.4f, Palette.Ink).rectTransform);
            Ui.Stretch(Ui.Label(_double, "×2", badgeH * 0.45f, Color.white).rectTransform);
        }

        public RectTransform Root { get; }

        public float Size { get; }

        public Tap Tap => Root.TryGetComponent<Tap>(out var tap) ? tap : Root.gameObject.AddComponent<Tap>();

        public static SpoolView Create(RectTransform parent, float size)
        {
            var root = Ui.Node(parent, "Spool");
            root.sizeDelta = new Vector2(size, size);
            return new SpoolView(root, size);
        }

        private static Image Image(RectTransform root, string name, Sprite sprite, float art, float cx, float cy, bool raycast = false)
        {
            var image = Ui.Image(root, name, sprite, Color.white, raycast);
            Ui.PlaceCenter(image.rectTransform, cx, cy, art, art);
            return image;
        }

        private static RectTransform Marker(RectTransform root, string name, Color color, Icon icon, float x, float y, float size)
        {
            var marker = Ui.Place(Ui.Node(root, name), x, y, size, size);
            Ui.Stretch(Ui.Image(marker, "Back", Art.Circle, color).rectTransform);
            Ui.IconImage(marker, icon, Color.white, size / 2f, size / 2f, size * 0.66f);
            return marker;
        }

        /// <summary>
        /// Показать клубок. front — передний в колонке (полный цвет, бирка, все накладки);
        /// surplus — его цвет в картинке уже довязан, клубок лишний.
        /// </summary>
        public void Bind(Spool spool, Color color, bool front, bool surplus = false, bool foreign = false)
        {
            _visible = spool.IsColorVisible;
            _color = color;
            _deep = !front;
            _thick = spool.Thick;
            _rainbow = spool.Rainbow;
            _canvas = false;
            UpdateBall();
            _question.gameObject.SetActive(!_visible);

            SetIce(spool.Ice);
            _surplus.gameObject.SetActive(surplus && _visible);
            _foreign.gameObject.SetActive(foreign);
            _double.gameObject.SetActive(spool.Thick && !surplus && !foreign && front);

            // Число петель — только у тех, кого можно взять сейчас: задние ряды без цифр, меньше шума.
            _badge.gameObject.SetActive(_visible && front);
            _badgeText.text = spool.Loops.ToString();

            _knots.gameObject.SetActive(spool.Knots > 0);
            if (spool.Knots > 0)
            {
                _knots.sprite = Art2.Knots(spool.Knots);
            }

            _lock.gameObject.SetActive(spool.Locked);
            _key.gameObject.SetActive(spool.IsKey);
            _ribbon.gameObject.SetActive(spool.Link != 0);
            _ribbon.color = Palette.LinkPair(spool.Link);
            _group.alpha = surplus && front ? 0.8f : 1f;
        }

        private void UpdateBall()
        {
            _ball.sprite = !_visible ? Art2.HiddenBall
                : _rainbow ? Art2.RainbowBall
                : _canvas ? Art2.CanvasBall(_color)
                : Art2.Ball(_color, _deep, _thick);
            _ball.color = _deep && (_rainbow || !_visible) ? new Color(1f, 1f, 1f, 0.7f) : Color.white;
        }

        /// <summary>Лёд: пузырь вокруг клубка; трещин тем больше, чем меньше петель осталось до оттаивания.</summary>
        public void SetIce(int ice)
        {
            _ice.gameObject.SetActive(ice > 0);
            if (ice > 0)
            {
                _ice.sprite = Art2.Ice(Mathf.Clamp(4 - ice, 0, 3));
            }
        }

        /// <summary>Чужая пряжа цвета ткани: клубок намотан нитями канвы.</summary>
        public void SetFabric(bool fabric)
        {
            if (_canvas == fabric)
            {
                return;
            }

            _canvas = fabric;
            UpdateBall();
        }

        /// <summary>Метку «×2» на станке не показываем: там толстый клубок и так стоит на двух местах.</summary>
        public void HideDouble()
        {
            _double.gameObject.SetActive(false);
        }

        /// <summary>Клубок на станке ждёт: его цвета сейчас нет на фронте. Бледнее, чтобы было видно, что он не вяжет.</summary>
        public void SetWaiting(bool waiting)
        {
            _group.alpha = waiting ? 0.55f : 1f;
        }

        public void SetLoops(int loops)
        {
            _badgeText.text = loops.ToString();
        }

        /// <summary>
        /// Задний ряд поля — спокойнее: без узелков, замков и «×2», пока клубок не вышел вперёд.
        /// Цвет, «?», лёд и лента связки остаются: по ним видно, что ждёт дальше.
        /// </summary>
        public void Quiet()
        {
            _knots.gameObject.SetActive(false);
            _double.gameObject.SetActive(false);
            _surplus.gameObject.SetActive(false);
            _shadow.color = new Color(1f, 1f, 1f, 0.5f);
        }

        public void HideBadges()
        {
            _badge.gameObject.SetActive(false);
            _knots.gameObject.SetActive(false);
            _lock.gameObject.SetActive(false);
            _key.gameObject.SetActive(false);
            _ribbon.gameObject.SetActive(false);
            _ice.gameObject.SetActive(false);
            _surplus.gameObject.SetActive(false);
            _foreign.gameObject.SetActive(false);
            _double.gameObject.SetActive(false);
        }
    }
}
