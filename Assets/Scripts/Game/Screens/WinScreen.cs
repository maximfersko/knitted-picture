#nullable enable

using KnittedPicture.Core;
using UnityEngine;

namespace KnittedPicture.Game
{
    /// <summary>
    /// Победа: картинка в пяльцах, награда, прогресс коллекции.
    /// Главная кнопка — «Дальше»; видео за двойную награду — тихая вторичная.
    /// </summary>
    public sealed class WinScreen : ScreenView
    {
        public WinScreen(App app, LevelData level, int reward)
            : base(app, "Win")
        {
            Sfx.Play(Sound.Win);
            var coins = Ui.CoinPill(Root, app.Progress.Coins, W - 16, 8);
            Confetti();

            var compact = H < 720f;
            var hoop = compact ? 220f : 260f;
            var hoopY = compact ? 70f : 90f;
            var hoopRoot = Ui.PlaceCenter(Ui.Node(Root, "Hoop"), W / 2f, hoopY + hoop / 2f, hoop, hoop);
            var clamp = Ui.Box(hoopRoot, "Clamp", 6, Palette.WoodDark);
            Ui.PlaceCenter(clamp.rectTransform, hoop / 2f, 2f, 36, 22);
            var cloth = Ui.Image(hoopRoot, "Cloth", Art.Circle, Palette.Hex(level.colors[0]));
            Ui.PlaceCenter(cloth.rectTransform, hoop / 2f, hoop / 2f, hoop - 8, hoop - 8);
            MiniPicture.Create(hoopRoot, level, hoop / 2f, hoop / 2f, hoop * 0.66f);
            var ring = Ui.Image(hoopRoot, "Ring", Art.Ring, Palette.Wood);
            Ui.PlaceCenter(ring.rectTransform, hoop / 2f, hoop / 2f, hoop + 14, hoop + 14);
            Tweens.Scale(hoopRoot, 0.6f, 1f, 0.45f);

            var y = hoopY + hoop + 22f;
            Ui.Label(Root, "Готово!", 44, Palette.Ink, 16, y, W - 32, 52);
            y += 54f;
            Ui.Label(Root, $"«{level.name}» теперь в альбоме", 17, Palette.Ink2, 16, y, W - 32, 26, false);
            y += 40f;

            var pill = Ui.Card(Root, "Reward", W / 2f - 60f, y, 120, 46, 23, Palette.CoinSoft, Palette.CoinSoft, 0f);
            Ui.Coin(pill, 26, 23, 28);
            var rewardText = Ui.Label(pill, "+" + reward, 22, Palette.Ink, 44, 0, 70, 46, true, TextAnchor.MiddleLeft);
            Tweens.Scale(pill, 0.4f, 1f, 0.35f, null, null, 0.35f);
            y += 64f;

            var collection = Collections.Of(level.id);
            var done = 0;
            for (var id = collection.FirstLevel; id <= collection.LastLevel; id++)
            {
                if (app.Progress.IsCompleted(id))
                {
                    done++;
                }
            }

            var card = Ui.Card(Root, "Collection", 16, y, W - 32, 74, 20, Palette.Card, Palette.CardShadow);
            Ui.Label(card, collection.Name, 16, Palette.Ink, 16, 12, W - 140, 24, true, TextAnchor.MiddleLeft);
            Ui.Label(card, $"{done} из {collection.Count}", 14, Palette.Ink2, W - 148, 12, 100, 24, true, TextAnchor.MiddleRight);
            Ui.ProgressBar(card, 16, 46, W - 64, 10, (float)done / collection.Count, Palette.Primary);

            var nextY = H - 24f - 48f - 8f - 64f;
            Ui.Button(Root, app.Progress.AllDone ? "На главную" : "Дальше", 20, nextY, W - 40, 64, Palette.Primary, Palette.PrimaryShadow, Color.white, 22)
                .OnClick(() => app.ContinueAfterWin(level.id));

            Tap? doubleTap = null;
            doubleTap = Ui.TextButton(Root, "Удвоить награду за видео", 20, H - 24f - 48f, W - 40, 48, Palette.Ink2, 15, Icon.Video);
            doubleTap.OnClick(() =>
            {
                doubleTap.Interactable = false;
                app.Ads.ShowRewarded("double_reward", ok =>
                {
                    if (!ok)
                    {
                        doubleTap.Interactable = true;
                        return;
                    }

                    app.Progress.AddCoins(reward);
                    app.Save();
                    Sfx.Play(Sound.Coin);
                    rewardText.text = "+" + reward * 2;
                    coins.text = app.Progress.Coins.ToString();
                    Tweens.Punch(pill, 0.2f, 0.3f);
                    doubleTap.gameObject.SetActive(false);
                });
            });
        }

        public override bool OnBack()
        {
            App.ShowHome();
            return true;
        }

        private void Confetti()
        {
            var colors = new[] { "#FF6FA5", "#6CB8FF", "#FF9A3C", "#FFC23C", "#17945F", "#8B5CF6" };
            var random = new System.Random(7);
            for (var i = 0; i < 28; i++)
            {
                var size = 6f + (float)random.NextDouble() * 8f;
                var dot = Ui.Image(Root, "Confetti", Art.Circle, Palette.Hex(colors[i % colors.Length]));
                var x0 = W / 2f;
                var y0 = 200f;
                var vx = ((float)random.NextDouble() - 0.5f) * W * 1.3f;
                var vy = -260f - (float)random.NextDouble() * 260f;
                Ui.PlaceCenter(dot.rectTransform, x0, y0, size, size);
                var rt = dot.rectTransform;
                Tweens.Run(rt, 1.6f, t =>
                {
                    var x = x0 + vx * t;
                    var y = y0 + vy * t + 620f * t * t;
                    rt.anchoredPosition = new Vector2(x, -y);
                    dot.color = Palette.WithAlpha(dot.color, 1f - t * t);
                }, null, () => Object.Destroy(rt.gameObject), i * 0.012f);
            }
        }
    }
}
