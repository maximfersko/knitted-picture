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
        public WinNews(bool achievement, bool chest, bool mission = false, bool allMissions = false)
        {
            Achievement = achievement;
            Chest = chest;
            Mission = mission;
            AllMissions = allMissions;
        }

        public bool Achievement { get; }

        /// <summary>Этой победой выполнено задание дня; все три — награда ждёт.</summary>
        public bool Mission { get; }

        public bool AllMissions { get; }

        public bool Chest { get; }
    }

    /// <summary>
    /// Победа: картинка в пяльцах, звёзды по одной, награда по строкам. Монеты вылетают из той
    /// строки, за которую получены, и по очереди летят в счётчик. Дальше — моток пряжи и сундук.
    /// Главная кнопка — «Дальше» с эскизом следующей картинки; домой — кнопка в углу.
    /// </summary>
    public sealed class WinScreen : ScreenView
    {
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

            // Монеты по источникам: одна крупная сумма и мелкие плашки «откуда» — вместо стены строк.
            var goldBonus = stats.GoldCaught * YarnCatalog.GoldBonus(app.Progress.PerkPower(YarnPerk.Gold));
            goldBonus = Mathf.Clamp(goldBonus, 0, stats.GoldCoins);
            var parts = new List<(string name, int coins)> { ("за картинку", reward.BaseCoins) };
            if (stats.RushCoins > 0)
            {
                parts.Add(($"разгон ×{stats.BestCombo}", stats.RushCoins));
            }

            if (stats.GoldCoins - goldBonus > 0)
            {
                parts.Add(($"золото {stats.GoldCaught}/{stats.GoldTotal}", stats.GoldCoins - goldBonus));
            }

            if (reward.PerkCoins + goldBonus > 0)
            {
                parts.Add(("пряжа", reward.PerkCoins + goldBonus));
            }

            if (reward.YarnCoins > 0)
            {
                parts.Add(("повтор мотка", reward.YarnCoins));
            }

            var compact = H < 760f || parts.Count >= 4;
            var hoopY = compact ? 52f : 64f;

            // Всё под пяльцами должно уместиться над «Дальше»: на коротких экранах уменьшаются пяльцы.
            var cardEstimate = 52f + (parts.Count > 3 ? 2 : 1) * 34f + 12f + TileGap + TileH;
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

            y = CoinsCard(parts, y);
            BuildTiles(reward, y + TileGap);

            FlyAll();
            BuildNext(app, level);

            if (news.AllMissions)
            {
                Tweens.Delay(Root, 2.2f, () => app.Toast("Все задания дня выполнены! Награда ждёт на главной"));
            }
            else if (news.Chest)
            {
                Tweens.Delay(Root, 2.2f, () => app.Toast("Звёздный сундук готов! Открой его на главной"));
            }
            else if (news.Achievement)
            {
                Tweens.Delay(Root, 2.2f, () => app.Toast("Новое достижение! Награда ждёт в коллекциях"));
            }
            else if (news.Mission)
            {
                Tweens.Delay(Root, 2.2f, () => app.Toast($"Задание дня выполнено: {app.Progress.MissionsDone(app.Today)} из {DailyMissions.Count}"));
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

        /// <summary>
        /// Карточка монет: слева «Награда», справа крупная сумма; ниже — плашки «откуда» в строку с переносом.
        /// Монетки вылетают из своих плашек в счётчик. Возвращает низ карточки.
        /// </summary>
        private float CoinsCard(List<(string name, int coins)> parts, float y)
        {
            var cardW = W - 32f;
            var total = 0;
            foreach (var (_, coins) in parts)
            {
                total += coins;
            }

            var card = Ui.Card(Root, "Reward", 16, y, cardW, 100, 20, Palette.Card, Palette.CardShadow);
            Ui.Label(card, "НАГРАДА", 12, Palette.Ink3, 18, 12, 120, 26, true, TextAnchor.MiddleLeft);
            var sum = Ui.Label(card, "+" + total, 26, Palette.Ink, cardW - 16 - 120, 6, 120, 38, true, TextAnchor.MiddleRight);
            sum.horizontalOverflow = HorizontalWrapMode.Overflow;
            Ui.Coin(card, cardW - 16 - sum.preferredWidth - 18f, 25, 26);

            // Плашки источников: «за картинку +20», «разгон ×11 +4»… — переносятся на вторую строку, если не влезли.
            const float chipH = 28f;
            const float gap = 6f;
            var x = 14f;
            var rowY = 50f;
            foreach (var (name, coins) in parts)
            {
                var chip = Ui.Box(card, "Part", chipH / 2f, Palette.Surface).rectTransform;
                var label = Ui.Label(chip, name, 13, Palette.Ink2, false);
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                var value = Ui.Label(chip, "+" + coins, 13, Palette.Ink, true);
                value.horizontalOverflow = HorizontalWrapMode.Overflow;
                var nameW = label.preferredWidth;
                var valueW = value.preferredWidth;
                var w = 10f + nameW + 6f + valueW + 4f + 16f + 8f;
                if (x + w > cardW - 14f && x > 14f)
                {
                    x = 14f;
                    rowY += chipH + gap;
                }

                Ui.Place(chip, x, rowY, w, chipH);
                Ui.Place(label.rectTransform, 10, 0, nameW + 2f, chipH);
                label.alignment = TextAnchor.MiddleLeft;
                Ui.Place(value.rectTransform, 10 + nameW + 6f, 0, valueW + 2f, chipH);
                value.alignment = TextAnchor.MiddleLeft;
                var coin = Ui.Coin(chip, w - 8f - 8f, chipH / 2f, 16);
                if (coins > 0)
                {
                    _flights.Add((coin, coins));
                }

                x += w + gap;
            }

            var cardH = rowY + chipH + 14f;
            card.sizeDelta = new Vector2(cardW, cardH);
            foreach (Transform child in card)
            {
                if (child.name == "Shadow" || child.name == "Face")
                {
                    ((RectTransform)child).sizeDelta = new Vector2(cardW, cardH);
                }
            }

            return y + cardH;
        }

        private const float TileH = 62f;
        private const float TileGap = 10f;

        /// <summary>Под монетами — две плитки рядом: моток (новый, новый уровень или повтор) и прогресс сундука.</summary>
        private void BuildTiles(WinReward reward, float y)
        {
            var hasYarn = reward.Yarn >= 0;
            var tileW = hasYarn ? (W - 32f - TileGap) / 2f : W - 32f;
            var x = 16f;
            if (hasYarn)
            {
                var yarn = YarnCatalog.All[reward.Yarn];
                var tile = Ui.Card(Root, "YarnTile", x, y, tileW, TileH, 18, Palette.Card, Palette.CardShadow, 3f);
                var ball = YarnView.Create(tile, yarn, 30, TileH / 2f, 34, true);
                ball.localScale = Vector3.zero;
                Tweens.Delay(ball, 1.0f, () =>
                {
                    Tweens.Scale(ball, 0.2f, 1f, 0.35f, Ease.OutBack);
                    Fx.Sparkle(Root, Ui.WorldCenter(ball), Palette.Hex(yarn.Hex), 10, 30f);
                    Sfx.Play(Sound.Star, 0.6f);
                });
                var caption = reward.NewYarn ? "Новый моток"
                    : reward.YarnLevelUp ? $"Уровень {App.Progress.YarnLevel(yarn.Id)}!"
                    : "Повтор мотка";
                Ui.Fit(Ui.Label(tile, caption, 12, reward.NewYarn ? YarnView.RarityColor(yarn.Rarity) : reward.YarnLevelUp ? Palette.Primary : Palette.Ink2,
                    54, 10, tileW - 62, 18, true, TextAnchor.MiddleLeft), 9);
                Ui.Fit(Ui.Label(tile, yarn.Name, 15, Palette.Ink, 54, 28, tileW - 62, 22, true, TextAnchor.MiddleLeft), 10);
                x += tileW + TileGap;
            }

            var progress = App.Progress;
            var value = Mathf.Min(progress.ChestProgress, ProgressService.ChestEvery);
            var ready = progress.CanOpenChest;
            var chest = Ui.Card(Root, "ChestTile", x, y, tileW, TileH, 18, ready ? Palette.CoinSoft : Palette.Card, Palette.CardShadow, 3f);
            Ui.IconImage(chest, Icon.Chest, Palette.CoinShadow, 28, TileH / 2f, 26);
            Ui.Fit(Ui.Label(chest, ready ? "Сундук готов!" : "Сундук", 12, ready ? Palette.CoinShadow : Palette.Ink2, 50, 10, tileW - 58, 18, true,
                TextAnchor.MiddleLeft), 9);
            Ui.Label(chest, $"{value}/{ProgressService.ChestEvery} ★", 13, Palette.Ink, 50, 27, tileW - 58, 18, true, TextAnchor.MiddleLeft);
            Ui.ProgressBar(chest, 50, 47, tileW - 64, 6, (float)value / ProgressService.ChestEvery, Palette.Coin);
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
