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
            BuildCollection(y + 14f);
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

        private void BuildCollection(float y)
        {
            var progress = App.Progress;
            var collection = Collections.Of(progress.CurrentLevel);
            var cardW = W - 32f;
            var gap = 8f;
            var tile = Mathf.Floor((cardW - 32f - gap * (collection.Count - 1)) / collection.Count);
            var cardH = 16f + 28f + tile + 18f;
            var card = Ui.Card(Root, "Collection", 16, y, cardW, cardH, 24, Palette.Card, Palette.CardShadow);

            var done = 0;
            for (var id = collection.FirstLevel; id <= collection.LastLevel; id++)
            {
                if (progress.IsCompleted(id))
                {
                    done++;
                }
            }

            Ui.Label(card, collection.Name, 18, Palette.Ink, 16, 14, cardW - 120, 26, true, TextAnchor.MiddleLeft);
            Ui.Label(card, $"{done} из {collection.Count}", 15, Palette.Ink2, cardW - 116, 14, 100, 26, true, TextAnchor.MiddleRight);

            for (var i = 0; i < collection.Count; i++)
            {
                var id = collection.FirstLevel + i;
                if (!App.Levels.Has(id))
                {
                    continue;
                }

                var state = progress.IsCompleted(id) ? TileState.Done : progress.IsAvailable(id) ? TileState.Current : TileState.Locked;
                var levelId = id;
                MiniPicture.Tile(card, App.Levels.Get(id), state, 16 + i * (tile + gap), 50, tile, progress.StarsOf(id))
                    .OnClick(() => App.StartLevel(levelId));
            }

            // Серия побед подряд: чем длиннее, тем больше монет — повод не перезапускать.
            if (progress.Streak >= 2)
            {
                var chipW = Mathf.Min(W - 32f, 300f);
                var chip = Ui.Card(Root, "Streak", (W - chipW) / 2f, y + cardH + 12f, chipW, 34, 17, Palette.CoinSoft, Palette.CoinSoft, 0f);
                Ui.IconImage(chip, Icon.Star, Palette.CoinShadow, 22, 17, 18);
                var bonus = Economy.StreakBonus(progress.Streak + 1);
                Ui.Label(chip, $"Серия побед: {progress.Streak} · +{bonus} за следующую", 14, Palette.Ink, 36, 0, chipW - 44, 34, true, TextAnchor.MiddleLeft);
            }
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
