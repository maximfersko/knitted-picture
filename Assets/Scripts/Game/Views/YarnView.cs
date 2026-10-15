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
            var sprite = rainbow ? Art2.YarnBallRainbow
                : yarn.Look == YarnLook.Melange ? Art2.YarnBallMelange(main, second)
                : Art2.YarnBall(main);
            Ui.Stretch(Ui.Image(root, "Ball", sprite, Color.white).rectTransform);

            // Блёстки и эпическая пряжа: искры-звёздочки по очереди вспыхивают, поворачиваясь, и гаснут.
            if (yarn.Look == YarnLook.Sparkle || yarn.Rarity == Rarity.Epic)
            {
                var spots = new[] { (0.2f, 0.22f, 0.3f), (0.84f, 0.3f, 0.24f), (0.68f, 0.86f, 0.26f), (0.3f, 0.74f, 0.18f) };
                var tint = yarn.Look == YarnLook.Sparkle ? Color.Lerp(second, Color.white, 0.35f) : Palette.Hex("#FFE27A");
                for (var i = 0; i < spots.Length; i++)
                {
                    var (x, y, s) = spots[i];
                    var star = Ui.Image(root, "Spark", Art2.Sparkle, tint);
                    Ui.PlaceCenter(star.rectTransform, size * x, size * y, size * s, size * s);
                    var rt = star.rectTransform;
                    var image = star;
                    Tweens.Run(rt, 2.2f, t =>
                    {
                        var flash = t < 0.35f ? Ease.Bump(t / 0.35f) : 0f;
                        rt.localScale = Vector3.one * (0.15f + 0.95f * flash);
                        rt.localRotation = Quaternion.Euler(0f, 0f, 90f * t);
                        image.color = Palette.WithAlpha(tint, 0.25f + 0.75f * flash);
                    }, null, null, i * 0.55f, true);
                }
            }

            return root;
        }

        public static Color RarityColor(Rarity rarity) =>
            rarity == Rarity.Common ? Palette.Ink2 : rarity == Rarity.Rare ? Palette.Link : Palette.CoinShadow;
    }
}
