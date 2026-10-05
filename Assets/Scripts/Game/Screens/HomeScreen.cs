#nullable enable

using KnittedPicture.Core;
using UnityEngine;

namespace KnittedPicture.Game
{
    /// <summary>Главная: логотип, текущая коллекция и одна большая кнопка «Играть».</summary>
    public sealed class HomeScreen : ScreenView
    {
        public HomeScreen(App app)
            : base(app, "Home")
        {
            var progress = app.Progress;
            Ui.RoundButton(Root, Icon.Settings, 16, 8).OnClick(() => app.ShowPopup(new SettingsPopup(app)));
            Ui.CoinPill(Root, progress.Coins, W - 16, 8);

            // Логотип: клубок с хвостиком нитки и название в две строки.
            var compact = H < 720f;
            var ball = compact ? 92f : 116f;
            var logoY = compact ? 70f : 96f;
            var yarn = Ui.Image(Root, "Logo", Art.YarnBall, Palette.Hex("#FF6FA5"));
            Ui.PlaceCenter(yarn.rectTransform, W / 2f, logoY + ball / 2f, ball, ball);
            Tweens.Run(yarn, 3.2f, t => yarn.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * Mathf.PI * 2f) * 4f), null, null, 0, true);

            var titleY = logoY + ball + 12f;
            var title = Ui.Label(Root, "Вязаная\nкартинка", compact ? 34 : 40, Palette.Ink, 16, titleY, W - 32, compact ? 80 : 92);
            title.lineSpacing = 0.85f;
            var subtitleY = titleY + (compact ? 80f : 92f);
            Ui.Label(Root, "Петля за петлёй — к готовой вещи", 16, Palette.Ink2, 16, subtitleY, W - 32, 24, false);

            BuildCollection(subtitleY + 44f);

            var buttonY = H - 24f - 48f - 10f - 78f;
            if (progress.AllDone)
            {
                var done = Ui.Button(Root, "", 20, buttonY, W - 40, 78, Palette.Primary, Palette.PrimaryShadow, Color.white);
                Ui.Label((RectTransform)done.transform, "Все картинки связаны!", 22, Color.white, 0, 12, W - 40, 30);
                Ui.Label((RectTransform)done.transform, "Скоро новые — загляни в альбом", 14, Color.white, 0, 42, W - 40, 22, false);
                done.OnClick(app.ShowAlbum);
            }
            else
            {
                var level = progress.CurrentLevel;
                var play = Ui.Button(Root, "", 20, buttonY, W - 40, 78, Palette.Primary, Palette.PrimaryShadow, Color.white);
                Ui.Label((RectTransform)play.transform, "Играть", 28, Color.white, 0, 10, W - 40, 36);
                var hard = app.Levels.Has(level) && app.Levels.Get(level).hard;
                Ui.Label((RectTransform)play.transform, hard ? $"Уровень {level} · сложный" : $"Уровень {level}", 15, Color.white, 0, 44, W - 40, 22, false);
                play.OnClick(() => app.StartLevel(level));
                Tweens.Run(play, 1.6f, t => play.transform.localScale = Vector3.one * (1f + 0.025f * Ease.Bump(t)), null, null, 0.6f, true);
            }

            Ui.TextButton(Root, "Альбом", 20, H - 24f - 48f, W - 40, 48, Palette.Ink2, 16, Icon.Album).OnClick(app.ShowAlbum);
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
                MiniPicture.Tile(card, App.Levels.Get(id), state, 16 + i * (tile + gap), 50, tile).OnClick(() => App.StartLevel(levelId));
            }
        }
    }
}
