#nullable enable

using KnittedPicture.Core;
using UnityEngine;

namespace KnittedPicture.Game
{
    /// <summary>
    /// Рамки для картинок альбома: разделы «За монеты», «За все звёзды коллекции» и «Сезонные», сетка по 3 с образцом (сердечко из первого уровня в этой рамке — у каждой
    /// свой узор и форма), названием, плашкой редкости и кнопкой — «Выбрана», «Выбрать» или цена. Купленная рамка сразу выбирается. Монеты уходят навсегда,
    /// на сложность рамка не влияет: это «сток» монет без давления (см. docs/ECONOMY.md).
    /// </summary>
    public sealed class FramesPopup : Popup
    {
        public FramesPopup(App app)
            : base(app, bottomSheet: true)
        {
            AddTitle(L.T("Рамки для альбома"), 24);
            var coins = app.Progress.Coins;
            var balance = AddText(L.F("У тебя {0} {1}", coins, YarnCatalog.CoinsWord(coins)), 15, 1);
            balance.color = Palette.CoinInk;
            Space(4f);

            // Три раздела: магазин, за коллекции, сезонные. Список длинный — прокручивается.
            const int columns = 3;
            const float gap = 10f;
            var cellW = Mathf.Floor((Inner - gap * (columns - 1)) / columns);
            var preview = Mathf.Min(84f, cellW - 12f);
            var cellH = preview + 100f;
            var sample = app.Levels.Get(1);
            var viewH = RoomFor(4f + 12f);
            var content = Ui.Scroll(Card, 0, Cursor, CardWidth, viewH);
            var y = 4f;
            var built = 0;
            foreach (var (source, title, note) in new[]
                     {
                         (FrameSource.Shop, L.T("За монеты"), string.Empty),
                         (FrameSource.Collection, L.T("За все звёзды коллекции"), L.T("Три звезды на каждой картинке коллекции")),
                         (FrameSource.Season, L.T("Сезонные"), L.T("Финал «Вязального сезона» своего времени года")),
                     })
            {
                Ui.Label(content, title, 17, Palette.Ink, 24, y, Inner, 24, true, TextAnchor.MiddleLeft);
                y += 26f;
                if (note.Length > 0)
                {
                    Ui.Fit(Ui.Label(content, note, 12, Palette.Ink3, 24, y, Inner, 18, false, TextAnchor.MiddleLeft), 9);
                    y += 22f;
                }

                y += 6f;
                var index = 0;
                foreach (var frame in AlbumFrames.All)
                {
                    if (frame.Source != source)
                    {
                        continue;
                    }

                    var x = 24f + (index % columns) * (cellW + gap);
                    var cy = y + (index / columns) * (cellH + gap);
                    var target = frame;
                    Stagger(built++, 9, () => Cell(app, content, target, x, cy, cellW, preview, sample));
                    index++;
                }

                y += ((index + columns - 1) / columns) * (cellH + gap) + 10f;
            }

            content.sizeDelta = new Vector2(CardWidth, y);
            Space(viewH + 4f);

            // Альбом под окном перерисуется с новой рамкой.
            Closed = app.RefreshScreen;
            Finish(12);
        }

        private void Cell(App app, RectTransform parent, FrameInfo frame, float x, float y, float w, float preview, LevelData sample)
        {
            var progress = app.Progress;
            var owned = progress.FrameOwned(frame.Id);
            var selected = progress.SelectedFrame.Id == frame.Id;

            // Образец: рамка со своим узором, в проёме — картинка; выбранная — в зелёном кольце.
            var px = x + (w - preview) / 2f;
            if (selected)
            {
                var ring = Ui.Image(parent, "Selected", Art.RoundRectOutline(16, 3), Palette.Primary);
                ring.type = UnityEngine.UI.Image.Type.Sliced;
                ring.pixelsPerUnitMultiplier = Art.Density;
                Ui.Place(ring.rectTransform, px - 5f, y - 5f, preview + 10f, preview + 10f);
            }

            var tile = Ui.Place(Ui.Node(parent, "Frame" + frame.Id), px, y, preview, preview);
            MiniPicture.Framed(tile, frame.Id, preview, sample);
            Ui.Fit(Ui.Label(parent, frame.Name, 13, Palette.Ink, x, y + preview + 8f, w, 18), 10);
            RarityStyle.Chip(parent, frame.Rarity, x + w / 2f, y + preview + 38f, w - 4f);

            var by = y + preview + 56f;
            const float bh = 34f;
            if (selected)
            {
                Ui.Label(parent, L.T("Выбрана"), 13, Palette.Primary, x, by, w, bh);
                return;
            }

            if (owned)
            {
                Ui.Button(parent, L.T("Выбрать"), x + 4f, by, w - 8f, bh, Palette.Surface, Palette.CardShadow, Palette.Ink, 14).OnClick(() =>
                {
                    progress.SelectFrame(frame.Id);
                    app.Save();
                    Reopen(app);
                });
                return;
            }

            if (frame.Source != FrameSource.Shop)
            {
                // Не продаётся: откуда она берётся.
                var how = frame.Source == FrameSource.Season ? L.T("финал сезона") : L.F("все звёзды «{0}»", Collections.All[frame.Tag].Name);
                var lockIcon = Ui.Image(parent, "Lock", Art.Icon(Icon.Lock), Palette.Ink3);
                Ui.PlaceCenter(lockIcon.rectTransform, x + 14f, by + bh / 2f, 14, 14);
                var label = Ui.Label(parent, how, 11, Palette.Ink3, x + 22f, by, w - 24f, bh, true, TextAnchor.MiddleLeft);
                label.lineSpacing = 0.85f;
                Ui.Fit(label, 8);
                return;
            }

            var affordable = progress.Coins >= frame.Price;
            var buy = Ui.Button(parent, frame.Price.ToString(), x + 4f, by, w - 8f, bh,
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
