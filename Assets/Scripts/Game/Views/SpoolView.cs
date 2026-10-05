#nullable enable

using KnittedPicture.Core;
using UnityEngine;
using UnityEngine.UI;

namespace KnittedPicture.Game
{
    /// <summary>
    /// Клубок на экране: шарик пряжи, число петель, значки узелка, замка, ключа и связки.
    /// Закрытые клубки (не впереди) приглушены.
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
        private readonly CanvasGroup _group;

        private SpoolView(RectTransform root, float size)
        {
            Root = root;
            Size = size;
            _group = root.gameObject.AddComponent<CanvasGroup>();

            _linkRing = Ui.Image(root, "LinkRing", Art.Ring, Palette.Link);
            Ui.PlaceCenter(_linkRing.rectTransform, size / 2f, size / 2f, size + 10, size + 10);

            _ball = Ui.Image(root, "Ball", Art.YarnBall, Color.white, true);
            Ui.PlaceCenter(_ball.rectTransform, size / 2f, size / 2f, size, size);

            _question = Ui.Label(root, "?", size * 0.5f, Palette.Ink, 0, 0, size, size);

            _lock = Ui.PlaceCenter(Ui.Node(root, "Lock"), size / 2f, size / 2f, size * 0.62f, size * 0.62f);
            Ui.Stretch(Ui.Image(_lock, "Back", Art.Circle, Palette.WithAlpha(Palette.Ink, 0.8f)).rectTransform);
            Ui.IconImage(_lock, Icon.Lock, Color.white, size * 0.31f, size * 0.31f, size * 0.38f);

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

        /// <summary>Показать клубок. front — передний в колонке (полностью яркий).</summary>
        public void Bind(Spool spool, Color color, bool front)
        {
            var visible = spool.IsColorVisible;
            _ball.color = visible ? color : Palette.Hidden;
            _question.gameObject.SetActive(!visible);

            _badge.gameObject.SetActive(visible);
            _badgeText.text = spool.Loops.ToString();

            _knot.gameObject.SetActive(spool.Knots > 0);
            _knotText.text = spool.Knots > 1 ? spool.Knots.ToString() : string.Empty;

            _lock.gameObject.SetActive(spool.Locked);
            _key.gameObject.SetActive(spool.IsKey);
            _linkRing.gameObject.SetActive(spool.Link != 0);

            _group.alpha = front ? 1f : 0.5f;
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
        }
    }
}
