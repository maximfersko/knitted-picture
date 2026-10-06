#nullable enable

using KnittedPicture.Core;
using UnityEngine;
using UnityEngine.UI;

namespace KnittedPicture.Game
{
    /// <summary>
    /// Клубок на экране: шарик пряжи, число петель, значки узелка, замка, ключа, связки,
    /// льда и толстой нити. Радужный — в полоску. Закрытые клубки (не впереди) приглушены,
    /// лишние (их цвет в картинке уже довязан) — отмечены галочкой.
    /// </summary>
    public sealed class SpoolView
    {
        private readonly Image _ball;
        private readonly Text _question;
        private readonly RectTransform _badge;
        private readonly Text _badgeText;
        private readonly RectTransform _knot;
        private readonly Text _knotText;
        private readonly RectTransform _lock;
        private readonly RectTransform _key;
        private readonly Image _linkRing;
        private readonly Image _thickRing;
        private readonly RectTransform _ice;
        private readonly Text _iceText;
        private readonly RectTransform _surplus;
        private readonly RectTransform _foreign;
        private readonly CanvasGroup _group;

        private SpoolView(RectTransform root, float size)
        {
            Root = root;
            Size = size;
            _group = root.gameObject.AddComponent<CanvasGroup>();

            _linkRing = Ui.Image(root, "LinkRing", Art.Ring, Palette.Link);
            Ui.PlaceCenter(_linkRing.rectTransform, size / 2f, size / 2f, size + 10, size + 10);

            // Толстая нить: второй виток вокруг клубка.
            _thickRing = Ui.Image(root, "ThickRing", Art.Ring, Color.white);
            Ui.PlaceCenter(_thickRing.rectTransform, size / 2f, size / 2f, size + 8, size + 8);

            _ball = Ui.Image(root, "Ball", Art.YarnBall, Color.white, true);
            Ui.PlaceCenter(_ball.rectTransform, size / 2f, size / 2f, size, size);

            _question = Ui.Label(root, "?", size * 0.5f, Palette.Ink, 0, 0, size, size);

            _lock = Ui.PlaceCenter(Ui.Node(root, "Lock"), size / 2f, size / 2f, size * 0.62f, size * 0.62f);
            Ui.Stretch(Ui.Image(_lock, "Back", Art.Circle, Palette.WithAlpha(Palette.Ink, 0.8f)).rectTransform);
            Ui.IconImage(_lock, Icon.Lock, Color.white, size * 0.31f, size * 0.31f, size * 0.38f);

            _ice = Ui.PlaceCenter(Ui.Node(root, "Ice"), size / 2f, size / 2f, size * 1.04f, size * 1.04f);
            Ui.Stretch(Ui.Box(_ice, "Block", size * 0.24f, new Color(0.78f, 0.92f, 1f, 0.82f)).rectTransform);
            var frost = Ui.Image(_ice, "Frost", Art.RoundRectOutline(size * 0.24f, 2), Color.white);
            frost.type = Image.Type.Sliced;
            frost.pixelsPerUnitMultiplier = Art.Density;
            Ui.Stretch(frost.rectTransform);
            Ui.IconImage(_ice, Icon.Snowflake, Palette.Hex("#3D8FD1"), size * 0.52f, size * 0.36f, size * 0.42f);
            _iceText = Ui.Label(_ice, "", size * 0.3f, Palette.Hex("#1F5F96"), 0, size * 0.56f, size * 1.04f, size * 0.4f);

            var badgeH = Mathf.Round(size * 0.42f);
            _badge = Ui.Place(Ui.Node(root, "Badge"), size - badgeH * 0.95f, size - badgeH * 0.75f, badgeH * 1.15f, badgeH);
            Ui.Stretch(Ui.Box(_badge, "Back", badgeH / 2f, Color.white).rectTransform);
            _badgeText = Ui.Label(_badge, "", badgeH * 0.62f, Palette.Ink);
            Ui.Stretch(_badgeText.rectTransform);

            _knot = Ui.Place(Ui.Node(root, "Knot"), -badgeH * 0.3f, -badgeH * 0.3f, badgeH * 1.25f, badgeH);
            Ui.Stretch(Ui.Box(_knot, "Back", badgeH / 2f, Palette.Danger).rectTransform);
            Ui.IconImage(_knot, Icon.Knot, Color.white, badgeH * 0.42f, badgeH / 2f, badgeH * 0.7f);
            _knotText = Ui.Label(_knot, "", badgeH * 0.55f, Color.white, badgeH * 0.62f, 0, badgeH * 0.6f, badgeH);

            _key = Ui.Place(Ui.Node(root, "Key"), size - badgeH * 0.8f, -badgeH * 0.3f, badgeH, badgeH);
            Ui.Stretch(Ui.Image(_key, "Back", Art.Circle, Palette.Coin).rectTransform);
            Ui.IconImage(_key, Icon.Key, Palette.Ink, badgeH / 2f, badgeH / 2f, badgeH * 0.7f);

            _surplus = Ui.Place(Ui.Node(root, "Surplus"), -badgeH * 0.2f, size - badgeH * 0.8f, badgeH, badgeH);
            Ui.Stretch(Ui.Image(_surplus, "Back", Art.Circle, Palette.Primary).rectTransform);
            Ui.IconImage(_surplus, Icon.Check, Color.white, badgeH / 2f, badgeH / 2f, badgeH * 0.7f);

            _foreign = Ui.Place(Ui.Node(root, "Foreign"), -badgeH * 0.2f, size - badgeH * 0.8f, badgeH, badgeH);
            Ui.Stretch(Ui.Image(_foreign, "Back", Art.Circle, Palette.Danger).rectTransform);
            Ui.IconImage(_foreign, Icon.Close, Color.white, badgeH / 2f, badgeH / 2f, badgeH * 0.62f);
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

        /// <summary>
        /// Показать клубок. front — передний в колонке (полностью яркий);
        /// surplus — его цвет в картинке уже довязан, клубок лишний.
        /// </summary>
        public void Bind(Spool spool, Color color, bool front, bool surplus = false, bool foreign = false)
        {
            var visible = spool.IsColorVisible;
            _ball.sprite = spool.Rainbow && visible ? Art.RainbowBall : Art.YarnBall;
            _ball.color = !visible ? Palette.Hidden : spool.Rainbow ? Color.white : color;
            _question.gameObject.SetActive(!visible);

            _thickRing.gameObject.SetActive(spool.Thick);
            _thickRing.color = visible && !spool.Rainbow ? Color.Lerp(color, Palette.Ink, 0.25f) : Palette.Ink3;

            SetIce(spool.Ice);
            _surplus.gameObject.SetActive(surplus && visible);
            _foreign.gameObject.SetActive(foreign);

            // Число петель — только у тех, кого можно взять сейчас: задние ряды без цифр, меньше шума.
            _badge.gameObject.SetActive(visible && front);
            _badgeText.text = spool.Loops.ToString();

            _knot.gameObject.SetActive(spool.Knots > 0);
            _knotText.text = spool.Knots > 1 ? spool.Knots.ToString() : string.Empty;

            _lock.gameObject.SetActive(spool.Locked);
            _key.gameObject.SetActive(spool.IsKey);
            _linkRing.gameObject.SetActive(spool.Link != 0);

            _group.alpha = front ? surplus ? 0.75f : 1f : 0.5f;
        }

        /// <summary>Лёд с числом петель до оттаивания; 0 — льда нет.</summary>
        public void SetIce(int ice)
        {
            _ice.gameObject.SetActive(ice > 0);
            _iceText.text = ice.ToString();
        }

        public void SetLoops(int loops)
        {
            _badgeText.text = loops.ToString();
        }

        public void HideBadges()
        {
            _badge.gameObject.SetActive(false);
            _knot.gameObject.SetActive(false);
            _lock.gameObject.SetActive(false);
            _key.gameObject.SetActive(false);
            _linkRing.gameObject.SetActive(false);
            _ice.gameObject.SetActive(false);
            _surplus.gameObject.SetActive(false);
            _foreign.gameObject.SetActive(false);
        }
    }
}
