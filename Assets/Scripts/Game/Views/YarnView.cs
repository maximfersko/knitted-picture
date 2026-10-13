#nullable enable

using KnittedPicture.Core;
using UnityEngine;

namespace KnittedPicture.Game
{
    /// <summary>Моток из «Корзинки»: однотонный, меланж, с блёстками или радужный. Не найденный — серый с «?».</summary>
    public static class YarnView
    {
        public static RectTransform Create(RectTransform parent, YarnInfo yarn, float cx, float cy, float size, bool owned)
        {
            var root = Ui.PlaceCenter(Ui.Node(parent, "Yarn" + yarn.Id), cx, cy, size, size);
            if (!owned)
            {
                Ui.Stretch(Ui.Image(root, "Ball", Art2.YarnBall(Palette.Track), Color.white).rectTransform);
                Ui.Label(root, "?", size * 0.45f, Palette.Ink3, 0, 0, size, size);
                return root;
            }

            var main = Palette.Hex(yarn.Hex);
            var second = Palette.Hex(yarn.Hex2);
            var rainbow = yarn.Look == YarnLook.Rainbow;
            Ui.Stretch(Ui.Image(root, "Ball", rainbow ? Art2.YarnBallRainbow : Art2.YarnBall(main), Color.white).rectTransform);
            if (yarn.Look == YarnLook.Melange)
            {
                var ring = Ui.Image(root, "Melange", Art.Ring, Palette.WithAlpha(second, 0.9f));
                Ui.PlaceCenter(ring.rectTransform, size / 2f, size / 2f, size * 0.7f, size * 0.7f);
            }

            if (yarn.Look == YarnLook.Sparkle || yarn.Rarity == Rarity.Epic)
            {
                var spots = new[] { (0.22f, 0.2f, 0.26f), (0.82f, 0.3f, 0.2f), (0.7f, 0.84f, 0.22f) };
                for (var i = 0; i < spots.Length; i++)
                {
                    var (x, y, s) = spots[i];
                    var star = Ui.Image(root, "Spark", Art.Icon(Icon.Star), yarn.Look == YarnLook.Sparkle ? second : Palette.Coin);
                    Ui.PlaceCenter(star.rectTransform, size * x, size * y, size * s, size * s);
                    var rt = star.rectTransform;
                    Tweens.Run(rt, 1.4f, t => rt.localScale = Vector3.one * (0.6f + 0.5f * Ease.Bump(t)), null, null, i * 0.35f, true);
                }
            }

            return root;
        }

        public static Color RarityColor(Rarity rarity) =>
            rarity == Rarity.Common ? Palette.Ink2 : rarity == Rarity.Rare ? Palette.Link : Palette.CoinShadow;
    }
}
