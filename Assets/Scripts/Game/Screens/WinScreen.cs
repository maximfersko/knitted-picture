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
        public LevelStats(int rushCoins, int goldCoins, int goldCaught, int goldTotal, int taps, int bestCombo, bool helped,
            int stitches, int rushes)
        {
            Stitches = stitches;
            Rushes = rushes;
            RushCoins = rushCoins;
            GoldCoins = goldCoins;
            GoldCaught = goldCaught;
            GoldTotal = goldTotal;
            Taps = taps;
            BestCombo = bestCombo;
            Helped = helped;
        }

        public int RushCoins { get; }

        public int GoldCoins { get; }

        public int GoldCaught { get; }

        public int GoldTotal { get; }

        public int Taps { get; }

        public int BestCombo { get; }

        /// <summary>Сколько петель связано и сколько раз включался «Разгон» — для достижений.</summary>
        public int Stitches { get; }

        public int Rushes { get; }

        /// <summary>Были бустеры или спасение.</summary>
        public bool Helped { get; }
    }

    /// <summary>Что нового открылось этой победой: достижение ждёт в коллекциях, сундук готов.</summary>
    public sealed class WinNews
    {
        public WinNews(bool achievement, bool chest, bool mission = false, bool allMissions = false, bool record = false, bool replay = false)
        {
            Record = record;
            Replay = replay;
            Achievement = achievement;
            Chest = chest;
            Mission = mission;
            AllMissions = allMissions;
        }

        public bool Achievement { get; }

        /// <summary>Перепрохождение со звёздами больше прежних: штамп «Рекорд!».</summary>
        public bool Record { get; }

        /// <summary>Повтор уже пройденной картинки: в альбоме сохраняется лучший результат.</summary>
        public bool Replay { get; }

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

        /// <summary>Сколько монет уже долетело: каждая следующая звенит чуть выше.</summary>
        private int _coinChain;

        /// <summary>Крупная сумма награды: монеты летят в счётчик одним потоком отсюда.</summary>
        private RectTransform? _sumFrom;

        public WinScreen(App app, LevelData level, WinReward reward, LevelStats stats, WinNews news)
            : base(app, "Win")
        {
            Sfx.Play(Sound.Win);
            app.SetLevelBackdrop(level);

            _shownCoins = app.Progress.Coins - reward.Total;
            Ui.RoundButton(Root, Icon.Home, 16, 8).OnClick(app.ShowHome);

            // Ширина пилюли по самой большой сумме, до которой счётчик дорастёт на этом экране: итог после награды
            // и, если удвоение за видео ещё доступно, вместе с удвоенной частью. Левый край пилюли не прыгает.
            var finalCoins = app.Progress.Coins + (app.Progress.DoublesLeft(app.Today) > 0 ? reward.Total : 0);
            _coins = Ui.CoinPill(Root, _shownCoins, W - 16, 8, finalCoins);
            Confetti();

            // Монеты по источникам: одна крупная сумма и мелкие плашки «откуда» — вместо стены строк.
            var goldBonus = stats.GoldCaught * YarnCatalog.GoldBonus(app.Progress.PerkPower(YarnPerk.Gold));
            goldBonus = Mathf.Clamp(goldBonus, 0, stats.GoldCoins);
            var parts = new List<(string name, int coins)> { (L.T("за картинку"), reward.BaseCoins) };
            if (stats.RushCoins > 0)
            {
                parts.Add((L.F("разгон ×{0}", stats.BestCombo), stats.RushCoins));
            }

            if (stats.GoldCoins - goldBonus > 0)
            {
                parts.Add((L.F("золото {0}/{1}", stats.GoldCaught, stats.GoldTotal), stats.GoldCoins - goldBonus));
            }

            if (reward.PerkCoins + goldBonus > 0)
            {
                parts.Add((L.T("пряжа"), reward.PerkCoins + goldBonus));
            }

            if (reward.YarnCoins > 0)
            {
                parts.Add((L.T("повтор мотка"), reward.YarnCoins));
            }

            var compact = H < 760f || parts.Count >= 4;
            var hoopY = compact ? 52f : 64f;

            // Всё под пяльцами должно уместиться над «Дальше»: на коротких экранах уменьшаются пяльцы.
            var cardHeight = CoinsCard(parts, reward, 0f, out var rewardCard);
            var hoop = WinLayoutRaster.Hoop(W, H, cardHeight, compact, 0f);
            var hoopRoot = Ui.PlaceCenter(Ui.Node(Root, "Hoop"), W / 2f, hoopY + hoop / 2f, hoop, hoop);
            // Пяльцы: кремовая ткань, на ней вязаная миниатюра петлями, сверху деревянный обруч с латунным винтом.
            var cloth = Ui.Image(hoopRoot, "Cloth", Art.Circle, Palette.Hex("#FBF5EA"));
            Ui.PlaceCenter(cloth.rectTransform, hoop / 2f, hoop / 2f, hoop - 6, hoop - 6);

            // Картинка проявляется так же, как вязалась (змейкой по рядам); тап по ней (во время показа домотать до конца,
            // потом показать снова) и кнопка «смотреть» показывают снова. Ткань тап не ловит: её зона закрывала бы кнопку ▶.
            var aspect = (float)level.width / Mathf.Max(1, level.height);
            var shortSide = Mathf.Min(aspect, 1f / aspect);
            // Внутренняя кромка растра — примерно 0,90 диаметра пялец; оставляем запас по диагонали.
            var imageSize = (hoop * 0.86f) / Mathf.Sqrt(1f + shortSide * shortSide);
            var replay = KnitReplay.Create(hoopRoot, level, hoop / 2f, hoop / 2f, imageSize);
            replay.Celebrate = false;
            void ReplayAgain()
            {
                replay.Play(3.5f);
                Analytics.Event("replay_view", ("from", "win"), ("level", level.id));
            }

            replay.IdleTap = ReplayAgain;
            var ring = Ui.Image(hoopRoot, "Hoop", Art2.Hoop, Color.white);
            Ui.PlaceCenter(ring.rectTransform, hoop / 2f, hoop / 2f - hoop * 0.035f, hoop * 1.12f, hoop * 1.12f * 400f / 384f);
            Tweens.Scale(hoopRoot, 0.6f, 1f, 0.45f);

            // Кнопка «посмотреть, как вязалось» у правого нижнего края пялец; на малых пяльцах (меньше 140) — справа от них
            // на уровне центра. Звёзды ниже (hoopY + hoop + 10), «Домой» и монеты выше, поэтому ни с чем не пересекается.
            // Левый край кнопки не заходит на зону картинки (replay.Root ловит тап), поэтому на небольших пяльцах она сдвинута вправо.
            var playX = W / 2f + Mathf.Max(hoop / 2f - 34f, replay.Root.sizeDelta.x / 2f + 2f);
            var playButton = hoop >= 140f
                ? Ui.RoundButton(Root, Icon.Play, playX, hoopY + hoop - 44f, 44f)
                : Ui.RoundButton(Root, Icon.Play, W / 2f + hoop * 0.56f + 6f, hoopY + hoop / 2f - 22f, 44f);
            playButton.OnClick(ReplayAgain);

            // Картинка довязалась — ткань «натягивается»: пяльцы коротко пружинят, искры по кругу.
            var ringRt = ring.rectTransform;
            replay.Play(1.35f, 0.25f, () =>
            {
                Tweens.Punch(hoopRoot, 0.06f, 0.3f);
                Tweens.Punch(ringRt, 0.05f, 0.3f);
                Fx.Sparkle(Root, Ui.WorldCenter(hoopRoot), Palette.Coin, 6, hoop * 0.42f);
                Haptics.Play(Haptic.Success);
            });

            var y = WinLayoutRaster.StarsTop(hoopY, hoop);
            BuildStars(y, reward.Stars, news.Record);
            y += 48f;

            Ui.Label(Root, reward.NewRecord ? L.T("Новый рекорд!") : L.T("Готово!"), compact ? 32 : 36, Palette.Ink, 16, y, W - 32, 42);
            y += 40f;
            var hintHeight = WinLayoutRaster.HintHeight(H, cardHeight, compact, 0f);
            Ui.Fit(Ui.Label(Root, StarsHint(level, reward.Stars, stats, news.Replay), 15, Palette.Ink2, 16, y, W - 32, Mathf.Min(22f, hintHeight), false), 11);
            y += hintHeight;

            Ui.Place(rewardCard, 16f, y, W - 32f, cardHeight);
            y += cardHeight;


            FlyAll();
            BuildNext(app, level);

            if (news.AllMissions)
            {
                Tweens.Delay(Root, 2.2f, () => app.Toast(L.T("Все задания дня выполнены! Награда ждёт на главной")));
            }
            else if (news.Chest)
            {
                Tweens.Delay(Root, 2.2f, () => app.Toast(L.F("{0} готов! Открой его на главной", ChestScreen.Title(app.Progress.NextChestRarity))));
            }
            else if (news.Achievement)
            {
                Tweens.Delay(Root, 2.2f, () => app.Toast(L.T("Новое достижение! Награда ждёт в альбоме")));
            }
            else if (news.Mission)
            {
                Tweens.Delay(Root, 2.2f, () => app.Toast(L.F("Задание дня выполнено: {0} из {1}", app.Progress.MissionsDone(app.Today), DailyMissions.Count)));
            }
        }

        /// <summary>У экрана своя постановка входа: волна блоков ей бы мешала.</summary>
        protected override bool CascadeOnEnter => false;

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
            RectTransform? heroFrame = null;
            LevelData? heroData = null;
            next.OnClick(() =>
            {
                // Двойной тап: после первого кнопка не реагирует (App.ContinueAfterWin тоже защищён от повторного входа).
                if (!next.Interactable)
                {
                    return;
                }

                next.Interactable = false;

                // Эскиз следующей картинки перелетит с кнопки на станок, как с «Играть» на главной.
                if (heroFrame != null && heroData != null)
                {
                    app.HeroFrom(heroFrame, heroData);
                }

                app.ContinueAfterWin(level.id);
            });
            Ui.Sheen(face, W - 40, 70f, 4f, 2.4f);
            var nextId = app.Progress.CurrentLevel;
            if (!app.Progress.AllDone && app.Levels.Has(nextId))
            {
                var data = app.Levels.Get(nextId);

                // Клубки следующего уровня красятся в фоне, пока идёт празднование.
                Art2.Prefetch(data);
                var frame = Ui.Box(face, "Preview", 12, Palette.Card);
                Ui.Place(frame.rectTransform, 10, 9, 52, 52);
                MiniPicture.Create(frame.rectTransform, data, 26, 26, 44, faded: true);
                heroFrame = frame.rectTransform;
                heroData = data;
                if (data.mystery)
                {
                    Ui.Label(frame.rectTransform, "?", 26, Palette.Ink2, 0, 0, 52, 52);
                }

                Ui.Label(face, L.T("Дальше"), 22, Color.white, 62, 10, W - 40 - 72, 30);
                Ui.Label(face, data.hard ? L.F("Уровень {0} · сложный", nextId) : L.F("Уровень {0}", nextId), 13, Color.white, 62, 38, W - 40 - 72, 20, false);
            }
            else
            {
                // Все картинки связаны: сколько звёзд ещё можно собрать (или что их уже все собрали).
                var missing = app.Progress.MissingStars();
                Ui.Label(face, L.T("На главную"), 22, Color.white, 0, 8, W - 40, 30);
                Ui.Fit(Ui.Label(face, missing > 0 ? L.F("Ещё {0} ★ до всех звёзд", missing) : L.T("Все звёзды собраны! Загляни в узор дня"),
                    13, Color.white, 10, 40, W - 60, 22, false), 10);
            }

            // Удвоение за видео — до Economy.DoublesPerDay раз в день: видео помогает, но не печатает монеты.
            // Лимит выбран — не пропадает молча, а становится строкой с причиной и временем обновления.
            if (app.Progress.DoublesLeft(app.Today) <= 0)
            {
                DoubleStatus(app);
                return;
            }

            Analytics.Event("offer_view", ("placement", "double_reward"));
            var doubleTap = Ui.TextButton(Root, L.T("Удвоить награду за видео"), 20, H - 24f - 44f, W - 40, 44, Palette.Ink2, 15, Icon.Video);
            var total = _flights.Count > 0 ? TotalFlights() : 0;
            doubleTap.DeniedHint = L.T("Видео уже загружается");
            doubleTap.OnClick(() =>
            {
                doubleTap.Interactable = false;
                // Лимит дня тратится только после досмотренного видео; иначе кнопка возвращается как была, а тост объясняет почему.
                app.ShowRewarded("double_reward", () =>
                {
                    if (Root == null)
                    {
                        return;
                    }

                    app.Progress.AddCoins(total);
                    app.Progress.RecordDouble(app.Today);
                    app.Save();
                    Sfx.Play(Sound.Coin);
                    Fly((RectTransform)doubleTap.transform, total, 0f);
                    doubleTap.gameObject.SetActive(false);
                    DoubleStatus(app);
                }, () => doubleTap.Interactable = true);
            });
        }

        /// <summary>
        /// Вместо кнопки «Удвоить»: сколько удвоений за видео уже сделано сегодня; когда лимит выбран, ещё и когда он обновится.
        /// </summary>
        private void DoubleStatus(App app)
        {
            string Text()
            {
                var used = Economy.DoublesPerDay - app.Progress.DoublesLeft(app.Today);
                var line = L.F("Удвоить за видео: {0} из {1} на сегодня", used, Economy.DoublesPerDay);
                return used >= Economy.DoublesPerDay ? line + " · " + L.F("новые через {0}", Limits.ResetIn(Limits.NextMidnight())) : line;
            }

            Limits.Line(Root, Text(), 20, H - 24f - 44f + 2f, W - 40, Text, 40f, TextAnchor.MiddleCenter);
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
        private static string StarsHint(LevelData level, int stars, LevelStats stats, bool replay)
        {
            if (stars < 3 && stats.Helped)
            {
                return L.T("Третья звезда: пройди без бустеров и спасений");
            }

            // Повтор: картинка уже была в альбоме, там остаётся лучший результат.
            return replay ? L.T("Лучший результат сохранён в альбоме") : L.F("«{0}» теперь в альбоме", level.Title);
        }

        /// <summary>
        /// Звёзды: пустые гнёзда видны сразу, заработанные золотые падают в них сверху по очереди с поворотом
        /// и отскоком, каждая звенит выше предыдущей и коротко толкает в руку. Рекорд — штамп после звёзд.
        /// </summary>
        private void BuildStars(float y, int stars, bool record)
        {
            const float size = 42f;
            RectTransform? middle = null;
            for (var i = 0; i < 3; i++)
            {
                var x = W / 2f + (i - 1) * (size + 12f);
                var big = i == 1 ? 1.2f : 1f;
                var cy = y + size / 2f + (i == 1 ? -6f : 0f);
                var slot = Ui.Image(Root, "StarSlot", Art2.RewardStar(false), Color.white);
                Ui.PlaceCenter(slot.rectTransform, x, cy, size * big, size * big);
                if (i == 1)
                {
                    middle = slot.rectTransform;
                }

                if (i >= stars)
                {
                    continue;
                }

                var star = Ui.Image(Root, "Star", Art2.RewardStar(true), Color.white);
                var rt = star.rectTransform;
                Ui.PlaceCenter(rt, x, cy, size * big, size * big);
                rt.localScale = Vector3.zero;
                var index = i;
                Tweens.Delay(rt, 0.35f + i * 0.28f, () =>
                {
                    var rest = rt.anchoredPosition;
                    var drop = Tweens.Calm ? 0f : 2f;
                    var turn = index == 1 ? 0f : index == 0 ? -1f : 1f;
                    var landed = false;
                    Tweens.Run(rt, 0.5f, t =>
                    {
                        // Звон и тычок в руку в момент касания гнезда (t = 0.55), а не в начале падения.
                        if (!landed && t >= 0.55f)
                        {
                            landed = true;
                            Sfx.Play(Sound.Star, 0.8f, 1f + index * 0.12f);
                            Haptics.Play(Haptic.Soft);
                        }

                        // Падает в гнездо, отскакивает и встаёт, на лету довернувшись.
                        var fall = t < 0.55f ? 1f - Ease.InCubic(t / 0.55f) : 0f;
                        var bounce = t >= 0.55f ? Ease.Jolt((t - 0.55f) / 0.45f) : 0f;
                        rt.anchoredPosition = rest + new Vector2(0f, drop * fall + 2f * Mathf.Max(0f, bounce));
                        rt.localScale = Vector3.one * Mathf.Min(1f, 0.4f + t * 2.2f);
                        rt.localRotation = Quaternion.Euler(0f, 0f, 8f * turn * fall);
                    }, null, () =>
                    {
                        rt.anchoredPosition = rest;
                        rt.localScale = Vector3.one;
                        rt.localRotation = Quaternion.identity;
                        Fx.Sparkle(Root, Ui.WorldCenter(rt), Palette.Coin, 8, 34f);
                    });
                });
            }

            if (record && middle != null)
            {
                Stamp(middle, 0.45f + stars * 0.28f + 0.35f);
            }
        }

        /// <summary>Штамп «Рекорд!» рядом со звёздами: падает с размаху под наклоном, экран чуть вздрагивает.</summary>
        private void Stamp(RectTransform near, float delay)
        {
            const float w = 108f;
            const float h = 34f;
            var stamp = Ui.Node(Root, "Record");
            Ui.PlaceCenter(stamp, near.anchoredPosition.x + 92f, -near.anchoredPosition.y - 22f, w, h);
            var face = Ui.Box(stamp, "Face", 10, Palette.Danger);
            Ui.Stretch(face.rectTransform);
            Ui.Outline(stamp, 10, Color.white, 2f);
            Ui.Label(stamp, L.T("Рекорд!"), 17, Color.white, 0, 0, w, h);
            stamp.localRotation = Quaternion.Euler(0f, 0f, -12f);
            stamp.localScale = Vector3.zero;
            Tweens.Delay(stamp, delay, () =>
            {
                Sfx.Play(Sound.Needles, 0.9f);
                Haptics.Play(Haptic.Medium);
                Tweens.Run(stamp, 0.32f, t => stamp.localScale = Vector3.one * Mathf.LerpUnclamped(2.2f, 1f, Ease.OutBack(t)));
                if (!Tweens.Calm)
                {
                    Tweens.Shake(Root, 4f, 0.25f);
                }
            });
        }

        /// <summary>
        /// Карточка монет: слева «Награда», справа крупная сумма; ниже — плашки «откуда» в строку с переносом.
        /// Монетки вылетают из своих плашек в счётчик. Возвращает низ карточки.
        /// </summary>
        private float CoinsCard(List<(string name, int coins)> parts, WinReward reward, float y, out RectTransform rewardCard)
        {
            var cardW = W - 32f;
            var total = 0;
            foreach (var (_, coins) in parts)
            {
                total += coins;
            }

            var card = Ui.Card(Root, "Reward", 16, y, cardW, 100, 20, Palette.Card, Palette.CardShadow);
            rewardCard = card;
            Ui.Label(card, L.T("НАГРАДА"), 12, Palette.Ink3, 18, 12, 120, 26, true, TextAnchor.MiddleLeft);
            var sum = Ui.Label(card, "+" + total, 26, Palette.Ink, cardW - 16 - 120, 6, 120, 38, true, TextAnchor.MiddleRight);
            // Монета стоит по ширине итоговой суммы (замер до набегания): текст выровнен вправо, и монета
            // не уезжает, пока число растёт от нуля.
            sum.horizontalOverflow = HorizontalWrapMode.Overflow;
            var sumWidth = sum.preferredWidth;
            _sumFrom = Ui.Coin(card, cardW - 16 - sumWidth - 18f, 25, 26);

            // Сумма набегает с нуля, пока монеты не полетели: видно, из чего она складывается.
            sum.text = "+0";
            Ui.RollTo(sum, total, 0.6f, "+{0}", 0.25f);

            // Источники и дополнительные награды доступны по явному нажатию.
            foreach (var (_, coins) in parts)
            {
                if (coins > 0)
                {
                    _flights.Add((_sumFrom, coins));
                }
            }
            var details = Ui.TextButton(card, L.T("Подробнее"), 14f, 48f, cardW - 28f, 44f, Palette.Ink2, 13);
            details.OnClick(() =>
            {
                var lines = new List<string>();
                foreach (var (name, coins) in parts)
                {
                    lines.Add(name + ": +" + coins);
                }
                if (reward.Yarn >= 0)
                {
                    var yarn = YarnCatalog.All[reward.Yarn];
                    var caption = reward.NewYarn ? L.T("Новый моток")
                        : reward.YarnLevelUp ? L.F("Уровень {0}!", App.Progress.YarnLevel(yarn.Id))
                        : reward.GoldStep >= YarnCatalog.GoldenAfter ? L.T("Золотой моток!")
                        : reward.GoldStep > 0 ? L.F("Золото {0}/{1}", reward.GoldStep, YarnCatalog.GoldenAfter)
                        : L.T("Повтор мотка");
                    lines.Add(caption + ": " + yarn.Name);
                }
                else if (reward.NextYarnChance > 0)
                {
                    lines.Add(reward.NextYarnChance >= 100 ? L.T("Моток в следующий раз") + ": " + L.T("наверняка")
                        : L.T("Моток не выпал") + ": " + L.F("дальше шанс {0}%", reward.NextYarnChance));
                }
                var progress = App.Progress;
                lines.Add((progress.CanOpenChest ? L.T("Сундук готов!") : L.T("Сундук")) + ": " +
                    Mathf.Min(progress.ChestProgress, ProgressService.ChestEvery) + "/" + ProgressService.ChestEvery + " ★");
                App.ShowPopup(new InfoPopup(App, L.T("НАГРАДА"), string.Join("\n", lines)));
            });
            const float cardH = 96f;
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

        /// <summary>Под монетами — две плитки рядом: моток (новый, новый уровень, повтор или «не выпал, шанс растёт») и прогресс сундука.</summary>
        private void BuildTiles(WinReward reward, float y)
        {
            var hasYarn = reward.Yarn >= 0;
            var missed = !hasYarn && reward.NextYarnChance > 0;
            var tileW = hasYarn || missed ? (W - 32f - TileGap) / 2f : W - 32f;
            var x = 16f;
            if (missed)
            {
                // Моток не выпал: честно показать, что шанс растёт (на четвёртой картинке подряд — наверняка).
                var tile = Ui.Card(Root, "YarnTile", x, y, tileW, TileH, 18, Palette.Surface, Palette.CardShadow, 3f);
                var ball = Ui.Image(tile, "Ball", Art2.YarnBall(Palette.Track), Color.white);
                Ui.PlaceCenter(ball.rectTransform, 30, TileH / 2f, 34, 34);
                Ui.Label(tile, "?", 16, Palette.Ink3, 13, TileH / 2f - 17f, 34, 34);
                Ui.Fit(Ui.Label(tile, reward.NextYarnChance >= 100 ? L.T("Моток в следующий раз") : L.T("Моток не выпал"), 12, Palette.Ink2,
                    54, 10, tileW - 62, 18, true, TextAnchor.MiddleLeft), 9);
                Ui.Fit(Ui.Label(tile, reward.NextYarnChance >= 100 ? L.T("наверняка") : L.F("дальше шанс {0}%", reward.NextYarnChance), 15, Palette.Ink,
                    54, 28, tileW - 62, 22, true, TextAnchor.MiddleLeft), 10);
                x += tileW + TileGap;
            }

            if (hasYarn)
            {
                var yarn = YarnCatalog.All[reward.Yarn];
                var tile = Ui.Card(Root, "YarnTile", x, y, tileW, TileH, 18, Palette.Card, Palette.CardShadow, 3f);
                // Редкий и эпический моток — со свечением редкости, оно появляется вместе с мотком.
                var aura = YarnView.Aura(tile, yarn.Rarity, 30, TileH / 2f, 34);
                if (aura != null)
                {
                    aura.localScale = Vector3.zero;
                }

                var ball = YarnView.Create(tile, yarn, 30, TileH / 2f, 34, true);
                ball.localScale = Vector3.zero;
                Tweens.Delay(ball, 1.0f, () =>
                {
                    Tweens.Scale(ball, 0.2f, 1f, 0.35f, Ease.OutBack);
                    if (aura != null)
                    {
                        Tweens.Scale(aura, 0.2f, 1f, 0.45f, Ease.OutBack);
                    }

                    Fx.Sparkle(Root, Ui.WorldCenter(ball), Palette.Hex(yarn.Hex), 10, 30f);
                    Sfx.Play(Sound.Star, 0.6f);
                });
                var caption = reward.NewYarn ? L.T("Новый моток")
                    : reward.YarnLevelUp ? L.F("Уровень {0}!", App.Progress.YarnLevel(yarn.Id))
                    : reward.GoldStep >= YarnCatalog.GoldenAfter ? L.T("Золотой моток!")
                    : reward.GoldStep > 0 ? L.F("Золото {0}/{1}", reward.GoldStep, YarnCatalog.GoldenAfter)
                    : L.T("Повтор мотка");
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
            Ui.Fit(Ui.Label(chest, ready ? L.T("Сундук готов!") : L.T("Сундук"), 12, ready ? Palette.CoinInk : Palette.Ink2, 50, 10, tileW - 58, 18, true,
                TextAnchor.MiddleLeft), 9);
            Ui.Label(chest, $"{value}/{ProgressService.ChestEvery} ★", 13, Palette.Ink, 50, 27, tileW - 58, 18, true, TextAnchor.MiddleLeft);
            Ui.ProgressBar(chest, 50, 47, tileW - 64, 6, (float)value / ProgressService.ChestEvery, Palette.Coin);
        }

        /// <summary>Строки отдают монеты по очереди: сначала «за картинку», потом бонусы.</summary>
        private void FlyAll()
        {
            // Один поток монет из крупной суммы, а не очередь из каждой плашки: короче и спокойнее.
            if (_sumFrom != null)
            {
                Fly(_sumFrom, TotalFlights(), 0.6f);
                return;
            }

            var delay = 0.6f;
            foreach (var (from, amount) in _flights)
            {
                Fly(from, amount, delay);
                delay += 0.2f;
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
                var last = i == count - 1;
                var portion = last ? amount - given : perCoin;
                given += portion;
                var jitter = new Vector3(Random.Range(-30f, 30f), Random.Range(-6f, 28f), 0) * Root.lossyScale.x;
                var itemDelay = delay + i * 0.06f;
                Fx.CoinFlight(coin, start, start + jitter, () => target, itemDelay, () =>
                {
                    Object.Destroy(coin.gameObject);
                    _shownCoins += portion;
                    Ui.RollTo(_coins, _shownCoins, 0.2f);
                    Tweens.Punch(_coins.transform.parent, 0.12f, 0.15f);
                    Sfx.Play(Sound.Coin, 0.4f, 1f + Mathf.Min(0.5f, _coinChain++ * 0.04f));
                });
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
