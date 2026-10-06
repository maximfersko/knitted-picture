#nullable enable

using KnittedPicture.Core;
using UnityEngine;

namespace KnittedPicture.Game
{
    /// <summary>
    /// Главная: логотип, ряд «зачем заходить» (подарок дня, звёздный сундук, коллекции —
    /// с красными точками, когда что-то ждёт), текущая коллекция и большая кнопка «Играть»
    /// с эскизом следующей картинки.
    /// </summary>
    public sealed class HomeScreen : ScreenView
    {
        public HomeScreen(App app)
            : base(app, "Home")
        {
            var progress = app.Progress;
            Ui.RoundButton(Root, Icon.Settings, 16, 8).OnClick(() => app.ShowPopup(new SettingsPopup(app)));
            Ui.CoinPill(Root, progress.Coins, W - 16, 8);

            // Логотип: клубок и название.
            var compact = H < 740f;
            var ball = compact ? 64f : 84f;
            var logoY = compact ? 56f : 66f;
            var yarn = Ui.Image(Root, "Logo", Art.YarnBall, Palette.Hex("#FF6FA5"));
            Ui.PlaceCenter(yarn.rectTransform, W / 2f, logoY + ball / 2f, ball, ball);
            Tweens.Run(yarn, 3.2f, t => yarn.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * Mathf.PI * 2f) * 4f), null, null, 0, true);
            var titleY = logoY + ball + 6f;
            Ui.Label(Root, "Вязаная картинка", compact ? 28 : 32, Palette.Ink, 16, titleY, W - 32, 40);
            Ui.Label(Root, "Петля за петлёй — к готовой вещи", 15, Palette.Ink2, 16, titleY + 38f, W - 32, 22, false);

            var y = BuildMetaRow(titleY + 76f);
            BuildCollection(y + 16f);
            BuildPlay();
        }

        /// <summary>Подарок дня, сундук и коллекции — три плитки с отметками.</summary>
        private float BuildMetaRow(float y)
        {
            var progress = App.Progress;
            const float gap = 8f;
            const float h = 92f;
            var w = (W - 32f - gap * 2) / 3f;

            var canDaily = progress.CanClaimDaily(App.Today);
            var gift = Tile(16, y, w, h, Icon.Gift, Palette.Danger, "Подарок", canDaily ? "забери!" : "завтра", canDaily ? 1 : 0);
            gift.OnClick(() => App.ShowPopup(new DailyGiftPopup(App, App.ShowHome)));

            var chestReady = progress.CanOpenChest;
            var chestProgress = Mathf.Min(progress.ChestProgress, ProgressService.ChestEvery);
            var chest = Tile(16 + w + gap, y, w, h, Icon.Chest, Palette.CoinShadow, "Сундук",
                chestReady ? "открой!" : $"{chestProgress}/{ProgressService.ChestEvery} ★", chestReady ? 1 : 0,
                (float)chestProgress / ProgressService.ChestEvery);
            chest.OnClick(() => StarChest.Open(App, App.ShowHome));

            var pending = progress.PendingClaims;
            var kinds = progress.YarnKinds;
            var collections = Tile(16 + 2 * (w + gap), y, w, h, Icon.Trophy, Palette.Link, "Коллекции",
                $"пряжа {kinds}/{YarnCatalog.Count}", pending);
            collections.OnClick(() => App.ShowCollections(FirstPendingTab()));

            return y + h;
        }

        /// <summary>Открыть коллекции там, где ждёт награда.</summary>
        private CollectionsTab FirstPendingTab()
        {
            var progress = App.Progress;
            foreach (var a in Achievements.All)
            {
                if (progress.CanClaimAchievement(a))
                {
                    return CollectionsTab.Achievements;
                }
            }

            foreach (var set in YarnCatalog.Sets)
            {
                if (progress.CanClaimSet(set))
                {
                    return CollectionsTab.Yarn;
                }
            }

            return CollectionsTab.Album;
        }

        private Tap Tile(float x, float y, float w, float h, Icon icon, Color tint, string title, string caption, int badge, float fill = -1f)
        {
            var card = Ui.Card(Root, title, x, y, w, h, 20, Palette.Card, Palette.CardShadow, 3f, true);
            var circle = Ui.Image(card, "Back", Art.Circle, Palette.WithAlpha(tint, 0.14f));
            Ui.PlaceCenter(circle.rectTransform, w / 2f, 30, 44, 44);
            var iconImage = Ui.IconImage(card, icon, tint, w / 2f, 30, 26);
            Ui.Label(card, title, 14, Palette.Ink, 0, 54, w, 18);
            if (fill >= 0f && badge == 0)
            {
                Ui.ProgressBar(card, 14, 76, w - 28, 6, fill, Palette.Coin);
            }
            else
            {
                Ui.Label(card, caption, 12, badge > 0 ? Palette.Danger : Palette.Ink2, 0, 71, w, 16, badge > 0);
            }

            if (badge > 0)
            {
                Ui.Badge(card, w - 10, 8, badge);
                var rt = iconImage.rectTransform;
                Tweens.Run(rt, 1.6f, t => rt.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * Mathf.PI * 4f) * 10f * Ease.Bump(t)), null, null, 0.4f, true);
            }

            return card.gameObject.AddComponent<Tap>();
        }

        /// <summary>Какую коллекцию показывает карусель; null — ту, где текущий уровень.</summary>
        private static int? _page;

        private RectTransform _carousel = null!;
        private float _carouselY;
        private float _carouselH;

        /// <summary>
        /// Карусель коллекций между плитками и «Играть»: стрелки, свайп и точки листают коллекции,
        /// тап по картинке — карточка уровня (пройденный), сразу игра (текущий) или подсказка (закрытый).
        /// Внизу — полоска награды за коллекцию.
        /// </summary>
        private void BuildCollection(float y)
        {
            var bottom = H - 24f - 84f - 16f;
            _carouselY = y;
            _carouselH = Mathf.Max(200f, bottom - y);
            _carousel = Ui.Place(Ui.Node(Root, "Carousel"), 0, y, W, _carouselH);
            var page = _page ?? IndexOf(Collections.Of(App.Progress.CurrentLevel));
            BuildPage(Mathf.Clamp(page, 0, Collections.All.Length - 1), 0);

            // Серия побед подряд: чем длиннее, тем больше монет — повод не перезапускать.
            var progress = App.Progress;
            if (progress.Streak >= 2)
            {
                var chipW = Mathf.Min(W - 32f, 300f);
                var chip = Ui.Card(Root, "Streak", (W - chipW) / 2f, bottom - 34f, chipW, 32, 16, Palette.CoinSoft, Palette.CoinSoft, 0f);
                Ui.IconImage(chip, Icon.Star, Palette.CoinShadow, 22, 16, 18);
                var bonus = Economy.StreakBonus(progress.Streak + 1);
                Ui.Label(chip, $"Серия побед: {progress.Streak} · +{bonus} за следующую", 14, Palette.Ink, 36, 0, chipW - 44, 32, true, TextAnchor.MiddleLeft);
            }
        }

        /// <summary>После уровня главная снова показывает коллекцию текущего уровня.</summary>
        public static void ResetCarousel() => _page = null;

        private static int IndexOf(CollectionInfo collection) => System.Array.IndexOf(Collections.All, collection);

        /// <summary>Построить страницу карусели. from: 0 — без анимации, 1 — въезд справа, -1 — слева.</summary>
        private void BuildPage(int index, int from)
        {
            _page = index;
            Ui.Clear(_carousel);
            var progress = App.Progress;
            var collection = Collections.All[index];
            var cardW = W - 32f;
            const float gap = 8f;
            var tile = Mathf.Min(64f, Mathf.Floor((cardW - 24f - gap * (collection.Count - 1)) / collection.Count));
            var tilesW = collection.Count * tile + (collection.Count - 1) * gap;
            var cardH = 58f + tile + 30f + 56f + 22f;
            var spare = Mathf.Max(0f, _carouselH - cardH - (progress.Streak >= 2 ? 44f : 0f));
            var card = Ui.Card(_carousel, "Collection", 16, spare * 0.35f, cardW, cardH, 24, Palette.Card, Palette.CardShadow, 3f, true);

            var done = 0;
            var stars = 0;
            for (var id = collection.FirstLevel; id <= collection.LastLevel; id++)
            {
                done += progress.IsCompleted(id) ? 1 : 0;
                stars += progress.StarsOf(id);
            }

            // Шапка: стрелки по краям, название и счёт по центру.
            Ui.Label(card, collection.Name, 18, Palette.Ink, 56, 10, cardW - 112, 24);
            Ui.Label(card, $"{done} из {collection.Count} · {stars}/{collection.Count * 3} ★", 13, Palette.Ink2, 56, 32, cardW - 112, 18, false);
            Arrow(card, 10, index > 0, () => Turn(index - 1), false);
            Arrow(card, cardW - 50, index < Collections.All.Length - 1, () => Turn(index + 1), true);

            var x0 = (cardW - tilesW) / 2f;
            for (var i = 0; i < collection.Count; i++)
            {
                var id = collection.FirstLevel + i;
                if (!App.Levels.Has(id))
                {
                    continue;
                }

                var level = App.Levels.Get(id);
                var state = progress.IsCompleted(id) ? TileState.Done : progress.IsAvailable(id) ? TileState.Current : TileState.Locked;
                var levelId = id;
                var x = x0 + i * (tile + gap);
                var tap = MiniPicture.Tile(card, level, state, x, 58, tile, progress.StarsOf(id));
                var rt = (RectTransform)tap.transform;
                tap.OnClick(() => LevelInfoPopup.OnTile(App, levelId, rt));
                var name = Ui.Label(card, state == TileState.Locked ? $"Ур. {id}" : level.name, 11,
                    state == TileState.Current ? Palette.Primary : Palette.Ink2, x - 6, 58 + tile + 6, tile + 12, 26, state == TileState.Current,
                    TextAnchor.UpperCenter);
                name.lineSpacing = 0.85f;
            }

            // Награда за коллекцию: прогресс или «Забрать».
            var barY = 58f + tile + 34f;
            var bar = Ui.Box(card, "Reward", 16, progress.CanClaimCollection(index) ? Palette.CoinSoft : Palette.Surface);
            Ui.Place(bar.rectTransform, 12, barY, cardW - 24, 48);
            Ui.IconImage(bar.rectTransform, progress.IsCollectionClaimed(index) ? Icon.Check : Icon.Gift,
                progress.IsCollectionClaimed(index) ? Palette.Primary : Palette.CoinShadow, 24, 24, 22);
            if (progress.CanClaimCollection(index))
            {
                Ui.Outline(bar.rectTransform, 16, Palette.Coin, 2f);
                Ui.Label(bar.rectTransform, "Коллекция собрана!", 14, Palette.Ink, 44, 0, cardW - 24 - 150, 48, true, TextAnchor.MiddleLeft);
                Ui.Button(bar.rectTransform, "Забрать", cardW - 24 - 100, 6, 92, 36, Palette.Primary, Palette.PrimaryShadow, Color.white, 15)
                    .OnClick(() =>
                    {
                        var granted = progress.ClaimCollection(index, App.Random);
                        App.Save();
                        if (granted != null)
                        {
                            App.ShowPopup(new RewardPopup(App, collection.Name, Icon.Album, granted.Value, App.ShowHome));
                        }
                    });
            }
            else if (progress.IsCollectionClaimed(index))
            {
                Ui.Label(bar.rectTransform, "Награда за коллекцию получена", 13, Palette.Ink2, 44, 0, cardW - 24 - 56, 48, true, TextAnchor.MiddleLeft);
            }
            else
            {
                Ui.Label(bar.rectTransform, "Собери все 5 — монеты, слот и моток", 12, Palette.Ink2, 44, 6, cardW - 24 - 56, 18, false, TextAnchor.MiddleLeft);
                Ui.ProgressBar(bar.rectTransform, 44, 30, cardW - 24 - 60, 7, (float)done / collection.Count, Palette.Primary);
            }

            // Точки: где мы среди коллекций. Тап по точке — сразу туда.
            var dotsY = barY + 48f + 12f;
            for (var i = 0; i < Collections.All.Length; i++)
            {
                var active = i == index;
                var dot = Ui.Box(card, "Dot", 4, active ? Palette.Ink : Palette.SlotEmpty, true);
                var dotW = active ? 22f : 8f;
                var dotX = cardW / 2f + (i - (Collections.All.Length - 1) / 2f) * 16f;
                Ui.PlaceCenter(dot.rectTransform, dotX, dotsY, dotW, 8);
                var target = i;
                dot.gameObject.AddComponent<Tap>().OnClick(() => Turn(target));
            }

            var swipe = card.gameObject.AddComponent<Swipe>();
            // Свайп влево — следующая коллекция, вправо — предыдущая.
            swipe.Swiped += dir => Turn(index - dir);

            if (from != 0)
            {
                var start = card.anchoredPosition;
                var group = card.gameObject.AddComponent<CanvasGroup>();
                Tweens.Run(card, 0.25f, t =>
                {
                    card.anchoredPosition = start + new Vector2(from * 60f * (1f - t), 0);
                    group.alpha = t;
                }, Ease.OutCubic);
            }
        }

        /// <summary>Листнуть карусель: новая страница въезжает с той стороны, куда листаем.</summary>
        private void Turn(int index)
        {
            if (index < 0 || index >= Collections.All.Length || _page == null || index == _page.Value)
            {
                return;
            }

            Sfx.Play(Sound.Pick, 0.5f);
            BuildPage(index, index > _page.Value ? 1 : -1);
        }

        private static void Arrow(RectTransform card, float x, bool enabled, System.Action action, bool forward)
        {
            var button = Ui.RoundButton(card, Icon.Back, x, 8, 40);
            if (forward)
            {
                // Отражаем только значок: масштаб самой кнопки нужен для отклика на нажатие.
                var icon = button.transform.GetChild(button.transform.childCount - 1);
                icon.localScale = new Vector3(-1f, 1f, 1f);
            }

            var group = button.gameObject.AddComponent<CanvasGroup>();
            group.alpha = enabled ? 1f : 0.3f;
            button.Interactable = enabled;
            button.OnClick(action);
        }

        /// <summary>«Играть» с эскизом следующей картинки — видно, ради чего идёшь дальше.</summary>
        private void BuildPlay()
        {
            var progress = App.Progress;
            var buttonY = H - 24f - 84f;
            if (progress.AllDone)
            {
                var done = Ui.Button(Root, "", 20, buttonY, W - 40, 84, Palette.Primary, Palette.PrimaryShadow, Color.white);
                Ui.Label((RectTransform)done.transform, "Все картинки связаны!", 22, Color.white, 0, 14, W - 40, 30);
                Ui.Label((RectTransform)done.transform, "Собирай звёзды и пряжу в коллекциях", 14, Color.white, 0, 46, W - 40, 22, false);
                done.OnClick(App.ShowAlbum);
                return;
            }

            var level = progress.CurrentLevel;
            var play = Ui.Button(Root, "", 20, buttonY, W - 40, 84, Palette.Primary, Palette.PrimaryShadow, Color.white);
            var face = (RectTransform)play.transform;
            if (App.Levels.Has(level))
            {
                var data = App.Levels.Get(level);
                var frame = Ui.Box(face, "Preview", 14, Palette.Card);
                Ui.Place(frame.rectTransform, 12, 10, 64, 64);
                MiniPicture.Create(frame.rectTransform, data, 32, 32, 54, faded: true);
                if (data.mystery)
                {
                    Ui.Label(frame.rectTransform, "?", 30, Palette.Ink2, 0, 0, 64, 64);
                }
            }

            Ui.Label(face, "Играть", 28, Color.white, 76, 12, W - 40 - 88, 36);
            var hard = App.Levels.Has(level) && App.Levels.Get(level).hard;
            Ui.Label(face, hard ? $"Уровень {level} · сложный" : $"Уровень {level}", 15, Color.white, 76, 46, W - 40 - 88, 22, false);
            play.OnClick(() => App.StartLevel(level));
            Tweens.Run(play, 1.6f, t => play.transform.localScale = Vector3.one * (1f + 0.025f * Ease.Bump(t)), null, null, 0.6f, true);
        }
    }
}
