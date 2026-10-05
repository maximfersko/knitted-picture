#nullable enable

using KnittedPicture.Core;
using UnityEngine;
using UnityEngine.UI;

namespace KnittedPicture.Game
{
    /// <summary>
    /// Победа: картинка в пяльцах, звёзды по одной, награда по строкам, монеты летят в счётчик.
    /// Главная кнопка — «Дальше»; видео за двойную награду — тихая вторичная.
    /// </summary>
    public sealed class WinScreen : ScreenView
    {
        private readonly Text _coins;
        private int _shownCoins;

        public WinScreen(App app, LevelData level, WinReward reward)
            : base(app, "Win")
        {
            Sfx.Play(Sound.Win);
            _shownCoins = app.Progress.Coins - reward.Total;
            _coins = Ui.CoinPill(Root, _shownCoins, W - 16, 8);
            Confetti();

            var compact = H < 760f;
            var hoop = compact ? 190f : 236f;
            var hoopY = compact ? 58f : 72f;
            var hoopRoot = Ui.PlaceCenter(Ui.Node(Root, "Hoop"), W / 2f, hoopY + hoop / 2f, hoop, hoop);
            var clamp = Ui.Box(hoopRoot, "Clamp", 6, Palette.WoodDark);
            Ui.PlaceCenter(clamp.rectTransform, hoop / 2f, 2f, 36, 22);
            var cloth = Ui.Image(hoopRoot, "Cloth", Art.Circle, Palette.Hex(level.colors[0]));
            Ui.PlaceCenter(cloth.rectTransform, hoop / 2f, hoop / 2f, hoop - 8, hoop - 8);
            MiniPicture.Create(hoopRoot, level, hoop / 2f, hoop / 2f, hoop * 0.66f);
            var ring = Ui.Image(hoopRoot, "Ring", Art.Ring, Palette.Wood);
            Ui.PlaceCenter(ring.rectTransform, hoop / 2f, hoop / 2f, hoop + 14, hoop + 14);
            Tweens.Scale(hoopRoot, 0.6f, 1f, 0.45f);

            var y = hoopY + hoop + 14f;
            BuildStars(y, reward.Stars);
            y += 50f;

            Ui.Label(Root, reward.NewRecord ? "Новый рекорд!" : "Готово!", 38, Palette.Ink, 16, y, W - 32, 46);
            y += 44f;
            Ui.Label(Root, $"«{level.name}» теперь в альбоме", 16, Palette.Ink2, 16, y, W - 32, 24, false);
            y += 34f;

            var card = Ui.Card(Root, "Reward", 16, y, W - 32, 0, 20, Palette.Card, Palette.CardShadow);
            var rowY = 12f;
            rowY = Row(card, rowY, "За картинку", reward.BaseCoins);
            if (reward.ComboCoins > 0)
            {
                rowY = Row(card, rowY, "Комбо", reward.ComboCoins);
            }

            if (reward.StreakCoins > 0)
            {
                rowY = Row(card, rowY, $"Серия побед ×{reward.Streak}", reward.StreakCoins);
            }

            var collection = Collections.Of(level.id);
            var done = 0;
            for (var id = collection.FirstLevel; id <= collection.LastLevel; id++)
            {
                if (app.Progress.IsCompleted(id))
                {
                    done++;
                }
            }

            Ui.Label(card, collection.Name, 14, Palette.Ink2, 16, rowY + 4, W - 140, 22, true, TextAnchor.MiddleLeft);
            Ui.Label(card, $"{done} из {collection.Count}", 14, Palette.Ink2, W - 148, rowY + 4, 100, 22, true, TextAnchor.MiddleRight);
            Ui.ProgressBar(card, 16, rowY + 32, W - 64, 8, (float)done / collection.Count, Palette.Primary);
            var cardH = rowY + 54f;
            card.sizeDelta = new Vector2(W - 32, cardH);
            foreach (Transform child in card)
            {
                if (child.name == "Shadow" || child.name == "Face")
                {
                    ((RectTransform)child).sizeDelta = new Vector2(W - 32, cardH);
                }
            }

            FlyCoins(card, reward.Total);

            var nextY = H - 24f - 48f - 8f - 64f;
            Ui.Button(Root, app.Progress.AllDone ? "На главную" : "Дальше", 20, nextY, W - 40, 64, Palette.Primary, Palette.PrimaryShadow, Color.white, 22)
                .OnClick(() => app.ContinueAfterWin(level.id));

            var doubleTap = Ui.TextButton(Root, "Удвоить награду за видео", 20, H - 24f - 48f, W - 40, 48, Palette.Ink2, 15, Icon.Video);
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

                    app.Progress.AddCoins(reward.Total);
                    app.Save();
                    Sfx.Play(Sound.Coin);
                    FlyCoins(card, reward.Total);
                    doubleTap.gameObject.SetActive(false);
                });
            });
        }

        public override bool OnBack()
        {
            App.ShowHome();
            return true;
        }

        private void BuildStars(float y, int stars)
        {
            const float size = 40f;
            for (var i = 0; i < 3; i++)
            {
                var x = W / 2f + (i - 1) * (size + 14f);
                var earned = i < stars;
                var star = Ui.Image(Root, "Star", Art.Icon(Icon.Star), earned ? Palette.Coin : Palette.Track);
                Ui.PlaceCenter(star.rectTransform, x, y + size / 2f + (i == 1 ? -6f : 0f), size * (i == 1 ? 1.2f : 1f), size * (i == 1 ? 1.2f : 1f));
                if (!earned)
                {
                    continue;
                }

                star.rectTransform.localScale = Vector3.zero;
                var rt = star.rectTransform;
                Tweens.Delay(rt, 0.35f + i * 0.22f, () =>
                {
                    Sfx.Play(Sound.Star, 0.8f);
                    Tweens.Scale(rt, 0.2f, 1f, 0.32f);
                    Fx.Sparkle(Root, Ui.WorldCenter(rt), Palette.Coin, 8, 34f);
                });
            }
        }

        private float Row(RectTransform card, float y, string title, int coins)
        {
            Ui.Label(card, title, 16, Palette.Ink, 16, y, W - 160, 28, true, TextAnchor.MiddleLeft);
            Ui.Coin(card, W - 32 - 74, y + 14, 20);
            Ui.Label(card, "+" + coins, 17, Palette.Ink, W - 32 - 60, y, 46, 28, true, TextAnchor.MiddleRight);
            return y + 30f;
        }

        /// <summary>Монетки летят из карточки награды в счётчик, число растёт по мере прилёта.</summary>
        private void FlyCoins(RectTransform from, int amount)
        {
            var count = Mathf.Clamp(amount / 5, 3, 10);
            var target = Ui.WorldCenter((RectTransform)_coins.transform.parent);
            var start = Ui.WorldCenter(from);
            var perCoin = Mathf.Max(1, amount / count);
            var given = 0;
            for (var i = 0; i < count; i++)
            {
                var coin = Ui.Coin(Root, 0, 0, 22);
                coin.position = start;
                var last = i == count - 1;
                var portion = last ? amount - given : perCoin;
                given += portion;
                var jitter = new Vector3(Random.Range(-60f, 60f), Random.Range(-20f, 30f), 0) * Root.lossyScale.x;
                Tweens.Move(coin, start + jitter, target, 0.5f, Ease.InOutCubic, () =>
                {
                    Object.Destroy(coin.gameObject);
                    _shownCoins += portion;
                    _coins.text = _shownCoins.ToString();
                    Tweens.Punch(_coins.transform.parent, 0.12f, 0.15f);
                    Sfx.Play(Sound.Coin, 0.4f);
                }, 0.8f + i * 0.07f);
            }
        }

        private void Confetti()
        {
            var colors = new[] { "#FF6FA5", "#6CB8FF", "#FF9A3C", "#FFC23C", "#17945F", "#8B5CF6" };
            var random = new System.Random(7);
            for (var i = 0; i < 40; i++)
            {
                var size = 6f + (float)random.NextDouble() * 9f;
                var dot = Ui.Image(Root, "Confetti", i % 3 == 0 ? Art.Icon(Icon.Star) : Art.Circle, Palette.Hex(colors[i % colors.Length]));
                var x0 = W / 2f;
                var y0 = 190f;
                var vx = ((float)random.NextDouble() - 0.5f) * W * 1.5f;
                var vy = -280f - (float)random.NextDouble() * 300f;
                var spin = ((float)random.NextDouble() - 0.5f) * 720f;
                Ui.PlaceCenter(dot.rectTransform, x0, y0, size, size);
                var rt = dot.rectTransform;
                Tweens.Run(rt, 1.8f, t =>
                {
                    var x = x0 + vx * t;
                    var y = y0 + vy * t + 640f * t * t;
                    rt.anchoredPosition = new Vector2(x, -y);
                    rt.localRotation = Quaternion.Euler(0, 0, spin * t);
                    dot.color = Palette.WithAlpha(dot.color, 1f - t * t);
                }, null, () => Object.Destroy(rt.gameObject), i * 0.01f);
            }
        }
    }
}
