#nullable enable

using System.Collections.Generic;
using KnittedPicture.Core;
using UnityEngine;
using UnityEngine.UI;

namespace KnittedPicture.Game
{
    /// <summary>Как прошёл уровень: для строк награды и подсказки про третью звезду.</summary>
    public sealed class LevelStats
    {
        public LevelStats(int rushCoins, int goldCoins, int goldCaught, int goldTotal, int taps, int par, int bestCombo, bool helped,
            int stitches, int rushes)
        {
            Stitches = stitches;
            Rushes = rushes;
            RushCoins = rushCoins;
            GoldCoins = goldCoins;
            GoldCaught = goldCaught;
            GoldTotal = goldTotal;
            Taps = taps;
            Par = par;
            BestCombo = bestCombo;
            Helped = helped;
        }

        public int RushCoins { get; }

        public int GoldCoins { get; }

        public int GoldCaught { get; }

        public int GoldTotal { get; }

        public int Taps { get; }

        /// <summary>Цель по тапам, 0 — без цели.</summary>
        public int Par { get; }

        public int BestCombo { get; }

        /// <summary>Сколько петель связано и сколько раз включался «Разгон» — для достижений.</summary>
        public int Stitches { get; }

        public int Rushes { get; }

        /// <summary>Были бустеры или спасение.</summary>
        public bool Helped { get; }

        public bool OverPar => Par > 0 && Taps > Par;
    }

    /// <summary>Что нового открылось этой победой: достижение ждёт в коллекциях, сундук готов.</summary>
    public sealed class WinNews
    {
        public WinNews(bool achievement, bool chest)
        {
            Achievement = achievement;
            Chest = chest;
        }

        public bool Achievement { get; }

        public bool Chest { get; }
    }

    /// <summary>
    /// Победа: картинка в пяльцах, звёзды по одной, награда по строкам. Монеты вылетают из той
    /// строки, за которую получены, и по очереди летят в счётчик. Дальше — моток пряжи и сундук.
    /// Главная кнопка — «Дальше» с эскизом следующей картинки; домой — кнопка в углу.
    /// </summary>
    public sealed class WinScreen : ScreenView
    {
        private const float RowH = 30f;

        private readonly Text _coins;
        private readonly List<(RectTransform from, int amount)> _flights = new List<(RectTransform, int)>();
        private int _shownCoins;

        public WinScreen(App app, LevelData level, WinReward reward, LevelStats stats, WinNews news)
            : base(app, "Win")
        {
            Sfx.Play(Sound.Win);
            _shownCoins = app.Progress.Coins - reward.Total;
            Ui.RoundButton(Root, Icon.Home, 16, 8).OnClick(app.ShowHome);
            _coins = Ui.CoinPill(Root, _shownCoins, W - 16, 8);
            Confetti();

            var rows = 1 + (stats.RushCoins > 0 ? 1 : 0) + (stats.GoldCoins > 0 ? 1 : 0) + (reward.StreakCoins > 0 ? 1 : 0) + (reward.Yarn >= 0 ? 1 : 0)
                + (reward.PerkCoins > 0 ? 1 : 0);
            var compact = H < 760f || rows >= 4;
            var hoopY = compact ? 52f : 64f;

            // Всё под пяльцами (звёзды, заголовок, подсказка, карточка наград) должно уместиться над «Дальше»:
            // на коротких экранах с множеством строк пяльцы уменьшаются, а не карточка уезжает под кнопку.
            var lines = 1 + (stats.RushCoins > 0 ? 1 : 0) + (stats.GoldCoins > 0 ? 1 : 0) + (reward.StreakCoins > 0 ? 1 : 0) + (reward.PerkCoins > 0 ? 1 : 0);
            var cardEstimate = 10f + lines * RowH + (reward.Yarn >= 0 ? 44f : 0f) + 4f + 38f + 10f;
            var nextTop = H - 24f - 44f - 6f - 70f;
            var room = nextTop - 12f - hoopY - 10f - 48f - 40f - 30f - cardEstimate;
            var hoop = Mathf.Clamp(room, 110f, compact ? 168f : 220f);
            var hoopRoot = Ui.PlaceCenter(Ui.Node(Root, "Hoop"), W / 2f, hoopY + hoop / 2f, hoop, hoop);
            var clamp = Ui.Box(hoopRoot, "Clamp", 6, Palette.WoodDark);
            Ui.PlaceCenter(clamp.rectTransform, hoop / 2f, 2f, 36, 22);
            var cloth = Ui.Image(hoopRoot, "Cloth", Art.Circle, Palette.Hex(level.colors[0]));
            Ui.PlaceCenter(cloth.rectTransform, hoop / 2f, hoop / 2f, hoop - 8, hoop - 8);
            MiniPicture.Create(hoopRoot, level, hoop / 2f, hoop / 2f, hoop * 0.66f);
            var ring = Ui.Image(hoopRoot, "Ring", Art.Ring, Palette.Wood);
            Ui.PlaceCenter(ring.rectTransform, hoop / 2f, hoop / 2f, hoop + 14, hoop + 14);
            Tweens.Scale(hoopRoot, 0.6f, 1f, 0.45f);

            var y = hoopY + hoop + 10f;
            BuildStars(y, reward.Stars);
            y += 48f;

            Ui.Label(Root, reward.NewRecord ? "Новый рекорд!" : "Готово!", compact ? 32 : 36, Palette.Ink, 16, y, W - 32, 42);
            y += 40f;
            Ui.Fit(Ui.Label(Root, StarsHint(level, reward.Stars, stats), 15, Palette.Ink2, 16, y, W - 32, 22, false), 11);
            y += 30f;

            var card = Ui.Card(Root, "Reward", 16, y, W - 32, 0, 20, Palette.Card, Palette.CardShadow);
            var rowY = 10f;
            rowY = Row(card, rowY, "За картинку", reward.BaseCoins);
            if (stats.RushCoins > 0)
            {
                rowY = Row(card, rowY, $"Разгон · лучшее комбо ×{stats.BestCombo}", stats.RushCoins);
            }

            if (stats.GoldCoins > 0)
            {
                rowY = Row(card, rowY, $"Золотые петли {stats.GoldCaught}/{stats.GoldTotal}", stats.GoldCoins);
            }

            if (reward.StreakCoins > 0)
            {
                rowY = Row(card, rowY, $"Серия побед ×{reward.Streak}", reward.StreakCoins);
            }

            if (reward.PerkCoins > 0 && app.Progress.ActiveYarn >= 0)
            {
                rowY = Row(card, rowY, $"Пряжа «{YarnCatalog.All[app.Progress.ActiveYarn].Name}»", reward.PerkCoins);
            }

            if (reward.Yarn >= 0)
            {
                rowY = YarnRow(card, rowY, reward);
            }

            rowY = ChestRow(card, rowY + 4f);
            var cardH = rowY + 10f;
            card.sizeDelta = new Vector2(W - 32, cardH);
            foreach (Transform child in card)
            {
                if (child.name == "Shadow" || child.name == "Face")
                {
                    ((RectTransform)child).sizeDelta = new Vector2(W - 32, cardH);
                }
            }

            FlyAll();
            BuildNext(app, level);

            if (news.Chest)
            {
                Tweens.Delay(Root, 2.2f, () => app.Toast("Звёздный сундук готов! Открой его на главной"));
            }
            else if (news.Achievement)
            {
                Tweens.Delay(Root, 2.2f, () => app.Toast("Новое достижение! Награда ждёт в коллекциях"));
            }
        }

        public override bool OnBack()
        {
            App.ShowHome();
            return true;
        }

        /// <summary>«Дальше» с эскизом следующей картинки и тихая кнопка «удвоить за видео».</summary>
        private void BuildNext(App app, LevelData level)
        {
            var nextY = H - 24f - 44f - 6f - 70f;
            var next = Ui.Button(Root, "", 20, nextY, W - 40, 70, Palette.Primary, Palette.PrimaryShadow, Color.white, 22);
            var face = (RectTransform)next.transform;
            next.OnClick(() => app.ContinueAfterWin(level.id));
            var nextId = app.Progress.CurrentLevel;
            if (!app.Progress.AllDone && app.Levels.Has(nextId))
            {
                var data = app.Levels.Get(nextId);
                var frame = Ui.Box(face, "Preview", 12, Palette.Card);
                Ui.Place(frame.rectTransform, 10, 9, 52, 52);
                MiniPicture.Create(frame.rectTransform, data, 26, 26, 44, faded: true);
                if (data.mystery)
                {
                    Ui.Label(frame.rectTransform, "?", 26, Palette.Ink2, 0, 0, 52, 52);
                }

                Ui.Label(face, "Дальше", 22, Color.white, 62, 10, W - 40 - 72, 30);
                Ui.Label(face, data.hard ? $"Уровень {nextId} · сложный" : $"Уровень {nextId}", 13, Color.white, 62, 38, W - 40 - 72, 20, false);
            }
            else
            {
                Ui.Label(face, "На главную", 22, Color.white, 0, 0, W - 40, 70);
            }

            var doubleTap = Ui.TextButton(Root, "Удвоить награду за видео", 20, H - 24f - 44f, W - 40, 44, Palette.Ink2, 15, Icon.Video);
            var total = _flights.Count > 0 ? TotalFlights() : 0;
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

                    app.Progress.AddCoins(total);
                    app.Save();
                    Sfx.Play(Sound.Coin);
                    Fly((RectTransform)doubleTap.transform, total, 0f);
                    doubleTap.gameObject.SetActive(false);
                });
            });
        }

        private int TotalFlights()
        {
            var total = 0;
            foreach (var (_, amount) in _flights)
            {
                total += amount;
            }

            return total;
        }

        /// <summary>Под заголовком: чего не хватило до трёх звёзд, иначе — про альбом.</summary>
        private static string StarsHint(LevelData level, int stars, LevelStats stats)
        {
            if (stars >= 3)
            {
                return stats.Par > 0 ? $"Ровно в цель: {stats.Taps} из {stats.Par} тапов" : $"«{level.name}» теперь в альбоме";
            }

            if (stats.OverPar)
            {
                return $"Третья звезда за {stats.Par} тапов (было {stats.Taps})";
            }

            return stats.Helped ? "Третья звезда: без бустеров и спасений" : $"«{level.name}» теперь в альбоме";
        }

        private void BuildStars(float y, int stars)
        {
            const float size = 38f;
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

        /// <summary>Строка награды; её монетка — точка вылета монет в счётчик.</summary>
        private float Row(RectTransform card, float y, string title, int coins)
        {
            Ui.Fit(Ui.Label(card, title, 16, Palette.Ink, 16, y, W - 160, RowH - 2, true, TextAnchor.MiddleLeft), 11);
            var coin = Ui.Coin(card, W - 32 - 74, y + RowH / 2f - 1f, 20);
            Ui.Label(card, "+" + coins, 17, Palette.Ink, W - 32 - 60, y, 46, RowH - 2, true, TextAnchor.MiddleRight);
            if (coins > 0)
            {
                _flights.Add((coin, coins));
            }

            return y + RowH;
        }

        /// <summary>Моток за новую картинку: новый — с блеском, повтор — монеты.</summary>
        private float YarnRow(RectTransform card, float y, WinReward reward)
        {
            var yarn = YarnCatalog.All[reward.Yarn];
            var ball = YarnView.Create(card, yarn, 28, y + 20f, 32, true);
            ball.localScale = Vector3.zero;
            Tweens.Delay(ball, 1.0f, () =>
            {
                Tweens.Scale(ball, 0.2f, 1f, 0.35f, Ease.OutBack);
                Fx.Sparkle(Root, Ui.WorldCenter(ball), Palette.Hex(yarn.Hex), 10, 30f);
                Sfx.Play(Sound.Star, 0.6f);
            });
            if (reward.NewYarn)
            {
                Ui.Label(card, $"Новый моток: {yarn.Name}", 15, Palette.Ink, 50, y + 4, W - 32 - 60, 20, true, TextAnchor.MiddleLeft);
                Ui.Label(card, $"{YarnCatalog.RarityName(yarn.Rarity)} · в корзинке {App.Progress.YarnKinds}/{YarnCatalog.Count}", 12,
                    YarnView.RarityColor(yarn.Rarity), 50, y + 22, W - 32 - 60, 18, false, TextAnchor.MiddleLeft);
                return y + 44f;
            }

            if (reward.YarnLevelUp)
            {
                // Повтор поднял уровень мотка — его бонус стал сильнее.
                var level = App.Progress.YarnLevel(yarn.Id);
                Ui.Fit(Ui.Label(card, $"{yarn.Name}: уровень {level}!", 15, Palette.Ink, 50, y + 4, W - 32 - 60, 20, true, TextAnchor.MiddleLeft), 11);
                Ui.Fit(Ui.Label(card, YarnCatalog.PerkText(YarnCatalog.PerkOf(yarn.Id), YarnCatalog.Power(yarn.Id, level)), 12,
                    Palette.Primary, 50, y + 22, W - 32 - 60, 18, false, TextAnchor.MiddleLeft), 9);
                return y + 44f;
            }

            Ui.Label(card, $"Повтор: {yarn.Name}", 15, Palette.Ink, 50, y + 10, W - 160, 22, true, TextAnchor.MiddleLeft);
            var coin = Ui.Coin(card, W - 32 - 74, y + 21f, 20);
            Ui.Label(card, "+" + reward.YarnCoins, 17, Palette.Ink, W - 32 - 60, y + 8, 46, 26, true, TextAnchor.MiddleRight);
            _flights.Add((coin, reward.YarnCoins));
            return y + 44f;
        }

        /// <summary>Прогресс звёздного сундука — повод собрать три звезды.</summary>
        private float ChestRow(RectTransform card, float y)
        {
            var progress = App.Progress;
            var value = Mathf.Min(progress.ChestProgress, ProgressService.ChestEvery);
            Ui.IconImage(card, Icon.Chest, Palette.CoinShadow, 28, y + 16, 24);
            var text = progress.CanOpenChest ? "Сундук готов! Открой на главной" : $"Звёздный сундук: {value}/{ProgressService.ChestEvery} ★";
            Ui.Label(card, text, 14, progress.CanOpenChest ? Palette.CoinShadow : Palette.Ink2, 50, y + 2, W - 32 - 60, 18, true, TextAnchor.MiddleLeft);
            Ui.ProgressBar(card, 50, y + 24, W - 32 - 66, 7, (float)value / ProgressService.ChestEvery, Palette.Coin);
            return y + 38f;
        }

        /// <summary>Строки отдают монеты по очереди: сначала «за картинку», потом бонусы.</summary>
        private void FlyAll()
        {
            var delay = 0.8f;
            foreach (var (from, amount) in _flights)
            {
                Fly(from, amount, delay);
                delay += 0.35f;
            }
        }

        /// <summary>Монетки вылетают из точки from и летят в счётчик; число растёт по мере прилёта.</summary>
        private void Fly(RectTransform from, int amount, float delay)
        {
            if (amount <= 0)
            {
                return;
            }

            var count = Mathf.Clamp(amount / 5, 2, 8);
            var target = Ui.WorldCenter((RectTransform)_coins.transform.parent);
            var perCoin = Mathf.Max(1, amount / count);
            var given = 0;
            Tweens.Delay(Root, delay, () => Tweens.Punch(from, 0.4f, 0.25f));
            for (var i = 0; i < count; i++)
            {
                var start = Ui.WorldCenter(from);
                var coin = Ui.Coin(Root, 0, 0, 20);
                coin.position = start;
                coin.localScale = Vector3.zero;
                var last = i == count - 1;
                var portion = last ? amount - given : perCoin;
                given += portion;
                var jitter = new Vector3(Random.Range(-24f, 24f), Random.Range(-10f, 18f), 0) * Root.lossyScale.x;
                var itemDelay = delay + i * 0.06f;
                Tweens.Delay(coin, itemDelay, () => coin.localScale = Vector3.one);
                Tweens.Move(coin, start + jitter, target, 0.55f, Ease.InOutCubic, () =>
                {
                    Object.Destroy(coin.gameObject);
                    _shownCoins += portion;
                    _coins.text = _shownCoins.ToString();
                    Tweens.Punch(_coins.transform.parent, 0.12f, 0.15f);
                    Sfx.Play(Sound.Coin, 0.4f);
                }, itemDelay);
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
