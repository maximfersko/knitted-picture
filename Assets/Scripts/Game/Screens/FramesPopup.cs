#nullable enable

using KnittedPicture.Core;
using UnityEngine;

namespace KnittedPicture.Game
{
    /// <summary>
    /// Рамки для картинок альбома: сетка 3 × 4 с образцом (сердечко из первого уровня в этой рамке), названием
    /// и кнопкой — «Выбрана», «Выбрать» или цена. Купленная рамка сразу выбирается. Монеты уходят навсегда,
    /// на сложность рамка не влияет: это «сток» монет без давления (см. docs/ECONOMY.md).
    /// </summary>
    public sealed class FramesPopup : Popup
    {
        public FramesPopup(App app)
            : base(app, bottomSheet: true)
        {
            AddTitle(L.T("Рамки для альбома"), 24);
            var balance = AddText(L.F("У тебя {0} монет", app.Progress.Coins), 15, 1);
            balance.color = Palette.CoinInk;
            Space(4f);

            const int columns = 3;
            const float gap = 10f;
            var cellW = Mathf.Floor((Inner - gap * (columns - 1)) / columns);
            var preview = Mathf.Min(72f, cellW - 16f);
            var cellH = preview + 74f;
            var sample = app.Levels.Get(1);
            var frames = AlbumFrames.All;
            var top = Cursor;
            for (var i = 0; i < frames.Length; i++)
            {
                var frame = frames[i];
                var x = 24f + (i % columns) * (cellW + gap);
                var y = top + (i / columns) * (cellH + gap);
                Cell(app, frame, x, y, cellW, preview, sample);
            }

            Space(((frames.Length + columns - 1) / columns) * (cellH + gap));

            // Альбом под окном перерисуется с новой рамкой.
            Closed = app.RefreshScreen;
            Finish(12);
        }

        private void Cell(App app, FrameInfo frame, float x, float y, float w, float preview, LevelData sample)
        {
            var progress = app.Progress;
            var owned = progress.FrameOwned(frame.Id);
            var selected = progress.SelectedFrame.Id == frame.Id;

            // Образец: багет, паспарту, кромка и картинка — так же, как плитка альбома.
            var px = x + (w - preview) / 2f;
            var tile = Ui.Card(Card, "Frame" + frame.Id, px, y, preview, preview, 14, Palette.Hex(frame.Face), Palette.CardShadow, 2f);
            var mat = Ui.Box(tile, "Mat", 10, Palette.Hex(frame.Mat));
            Ui.Place(mat.rectTransform, 4f, 4f, preview - 8f, preview - 8f);
            Ui.Outline(tile, 14, Palette.Hex(frame.Edge), 1.5f).raycastTarget = false;
            MiniPicture.Create(tile, sample, preview / 2f, preview / 2f, preview - 14f);
            if (selected)
            {
                var ring = Ui.Outline(tile, 14, Palette.Primary, 3f);
                ring.raycastTarget = false;
            }

            Ui.Fit(Ui.Label(Card, frame.Name, 13, Palette.Ink, x, y + preview + 6f, w, 18), 10);

            var by = y + preview + 28f;
            const float bh = 34f;
            if (selected)
            {
                Ui.Label(Card, L.T("Выбрана"), 13, Palette.Primary, x, by, w, bh);
                return;
            }

            if (owned)
            {
                Ui.Button(Card, L.T("Выбрать"), x + 4f, by, w - 8f, bh, Palette.Surface, Palette.CardShadow, Palette.Ink, 14).OnClick(() =>
                {
                    progress.SelectFrame(frame.Id);
                    app.Save();
                    Reopen(app);
                });
                return;
            }

            var affordable = progress.Coins >= frame.Price;
            var buy = Ui.Button(Card, frame.Price.ToString(), x + 4f, by, w - 8f, bh,
                affordable ? Palette.Coin : Palette.Track, affordable ? Palette.CoinShadow : Palette.CardShadow,
                affordable ? Palette.CoinInk : Palette.Ink3, 14);
            buy.OnClick(() =>
            {
                if (!progress.BuyFrame(frame.Id))
                {
                    app.Toast(L.F("Не хватает монет: нужно {0}, у тебя {1}", frame.Price, progress.Coins));
                    return;
                }

                Analytics.Event("frame_buy", ("frame", frame.Id), ("price", frame.Price));
                app.Save();
                Sfx.Play(Sound.Coin);
                Haptics.Play(Haptic.Success);
                Reopen(app);
            });
        }

        private void Reopen(App app)
        {
            Closed = null;
            Close();
            app.RefreshScreen();
            app.ShowPopup(new FramesPopup(app));
        }
    }
}
