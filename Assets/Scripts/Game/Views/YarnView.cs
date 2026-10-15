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

            // Блёстки, редкая и эпическая пряжа: искры-звёздочки по очереди вспыхивают, поворачиваясь, и гаснут.
            // У редкой их две, голубоватые; у эпической и блестящей — четыре, тёплые или цвета блёсток.
            if (yarn.Look == YarnLook.Sparkle || yarn.Rarity != Rarity.Common)
            {
                var all = new[] { (0.2f, 0.22f, 0.3f), (0.84f, 0.3f, 0.24f), (0.68f, 0.86f, 0.26f), (0.3f, 0.74f, 0.18f) };
                var few = yarn.Look != YarnLook.Sparkle && yarn.Rarity == Rarity.Rare;
                var spots = few ? new[] { all[0], all[2] } : all;
                var tint = yarn.Look == YarnLook.Sparkle ? Color.Lerp(second, Color.white, 0.35f)
                    : yarn.Rarity == Rarity.Rare ? Palette.Hex("#D6ECFF") : Palette.Hex("#FFE27A");
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

        /// <summary>
        /// Свечение редкости за мотком (ставится до мотка, чтобы лечь под него). По практике игр с редкостью эффект
        /// нарастает с ценностью, цвет дублируется камнем и подписью: обычная — без эффекта; редкая — голубое
        /// дышащее сияние, волна света раз в несколько секунд и две искры на орбите; эпическая — тёплое сияние,
        /// медленные лучи, волна и три искры. Без вспышек на весь экран; «Меньше движения» всё останавливает.
        /// </summary>
        public static RectTransform? Aura(RectTransform parent, Rarity rarity, float cx, float cy, float size, float delay = 0f)
        {
            if (rarity == Rarity.Common)
            {
                return null;
            }

            var epic = rarity == Rarity.Epic;
            var color = epic ? Palette.Coin : Palette.Hex("#4CA6F0");
            var root = Ui.PlaceCenter(Ui.Node(parent, "Aura"), cx, cy, size, size);

            if (epic)
            {
                var rays = Ui.Image(root, "Rays", Art.Rays, Palette.WithAlpha(color, 0.3f));
                var raysRt = rays.rectTransform;
                Ui.PlaceCenter(raysRt, size / 2f, size / 2f, size * 1.9f, size * 1.9f);
                Tweens.Run(raysRt, 24f, t => raysRt.localRotation = Quaternion.Euler(0f, 0f, -360f * t), null, null, 0f, true);
            }

            // Сияние дышит: чуть растёт и ярчает.
            var glow = Ui.Image(root, "Glow", Art.Glow, Palette.WithAlpha(color, epic ? 0.42f : 0.34f));
            var glowRt = glow.rectTransform;
            Ui.PlaceCenter(glowRt, size / 2f, size / 2f, size * 1.75f, size * 1.75f);
            var baseAlpha = epic ? 0.42f : 0.34f;
            Tweens.Run(glowRt, 2.6f, t =>
            {
                var b = Ease.Bump(t);
                glowRt.localScale = Vector3.one * (0.94f + 0.1f * b);
                glow.color = Palette.WithAlpha(color, baseAlpha * (0.8f + 0.3f * b));
            }, null, null, delay, true);

            // Волна: светлое кольцо расходится от мотка и гаснет («Меньше движения» — без волны: стоячее кольцо лишнее).
            var wave = Ui.Image(root, "Wave", Art.Ring, Palette.WithAlpha(color, 0f));
            wave.enabled = !Tweens.Calm;
            var waveRt = wave.rectTransform;
            Ui.PlaceCenter(waveRt, size / 2f, size / 2f, size, size);
            Tweens.Run(waveRt, epic ? 2.4f : 3.2f, t =>
            {
                var k = Mathf.Clamp01(t / 0.45f);
                waveRt.localScale = Vector3.one * (0.95f + 0.6f * Ease.OutCubic(k));
                wave.color = Palette.WithAlpha(color, t < 0.45f ? 0.55f * (1f - k) : 0f);
            }, null, null, delay + 0.4f, true);

            // Искры на орбите: медленно обходят моток и по очереди вспыхивают.
            var orbit = Ui.PlaceCenter(Ui.Node(root, "Orbit"), size / 2f, size / 2f, size, size);
            var count = epic ? 3 : 2;
            var sparkColor = epic ? Palette.Hex("#FFF1A8") : Palette.Hex("#D6ECFF");
            for (var i = 0; i < count; i++)
            {
                var angle = i * Mathf.PI * 2f / count + 0.6f;
                var spark = Ui.Image(orbit, "Spark", Art2.Sparkle, sparkColor);
                var sparkRt = spark.rectTransform;
                var s = size * (epic ? 0.24f : 0.2f);
                Ui.PlaceCenter(sparkRt, size / 2f + Mathf.Cos(angle) * size * 0.66f, size / 2f + Mathf.Sin(angle) * size * 0.66f, s, s);
                var image = spark;
                Tweens.Run(sparkRt, 1.9f, t =>
                {
                    var flash = t < 0.4f ? Ease.Bump(t / 0.4f) : 0f;
                    sparkRt.localScale = Vector3.one * (0.2f + 0.9f * flash);
                    image.color = Palette.WithAlpha(sparkColor, 0.2f + 0.8f * flash);
                }, null, null, delay + i * 0.63f, true);
            }

            Tweens.Run(orbit, epic ? 9f : 12f, t => orbit.localRotation = Quaternion.Euler(0f, 0f, 360f * t), null, null, 0f, true);
            return root;
        }

        public static Color RarityColor(Rarity rarity) =>
            rarity == Rarity.Common ? Palette.Ink2 : rarity == Rarity.Rare ? Palette.Link : Palette.CoinShadow;
    }
}
