#nullable enable

using System;
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
            _balance = AddText(BalanceText(app), 15, 1);
            _balance.color = Palette.CoinInk;
            Space(4f);

            // Три раздела: магазин, за коллекции, сезонные. Список длинный — прокручивается.
            _cellW = Mathf.Floor((Inner - Gap * (Columns - 1)) / Columns);
            _preview = Mathf.Min(84f, _cellW - 12f);
            _sample = app.Levels.Get(1);
            var viewH = RoomFor(4f + 12f);
            _content = Ui.Scroll(Card, 0, Cursor, CardWidth, viewH);
            Fill(app, true);
            Space(viewH + 4f);

            // Альбом под окном перерисуется с новой рамкой.
            Closed = app.RefreshScreen;
            Finish(12);
        }

        private const int Columns = 3;
        private const float Gap = 10f;

        private readonly UnityEngine.UI.Text _balance;
        private readonly RectTransform _content;
        private readonly LevelData _sample;
        private readonly float _cellW;
        private readonly float _preview;

        /// <summary>Номер перестройки: отложенные достройки ячеек от прежней сборки игнорируются.</summary>
        private int _fillVersion;

        /// <summary>Сбросить подпись единственной «вооружённой» кнопки покупки (null — вооружённой нет).</summary>
        private Action? _disarm;

        private static string BalanceText(App app)
        {
            var coins = app.Progress.Coins;
            return L.F("У тебя {0} {1}", coins, YarnCatalog.CoinsWord(coins));
        }

        /// <summary>
        /// Собрать ячейки в прокрутку. staggered — при открытии окна часть ячеек достраивается следом (без рывка);
        /// при выборе и покупке ячейки собираются сразу и на месте: окно не закрывается, прокрутка остаётся.
        /// </summary>
        private void Fill(App app, bool staggered)
        {
            var content = _content;
            var version = ++_fillVersion;
            _disarm = null;
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
                var cellH = _preview + 100f;
                foreach (var frame in AlbumFrames.All)
                {
                    if (frame.Source != source)
                    {
                        continue;
                    }

                    var x = 24f + (index % Columns) * (_cellW + Gap);
                    var cy = y + (index / Columns) * (cellH + Gap);
                    var target = frame;
                    if (staggered)
                    {
                        Stagger(built++, 9, () =>
                        {
                            if (version == _fillVersion)
                            {
                                Cell(app, _content, target, x, cy, _cellW, _preview, _sample);
                            }
                        });
                    }
                    else
                    {
                        Cell(app, _content, target, x, cy, _cellW, _preview, _sample);
                    }

                    index++;
                }

                y += ((index + Columns - 1) / Columns) * (cellH + Gap) + 10f;
            }

            content.sizeDelta = new Vector2(CardWidth, y);
        }

        /// <summary>Перестроить ячейки на месте после выбора или покупки: без закрытия окна, звука и анимации входа.</summary>
        private void Refill(App app)
        {
            _balance.text = BalanceText(app);
            for (var i = _content.childCount - 1; i >= 0; i--)
            {
                var child = _content.GetChild(i).gameObject;
                child.SetActive(false);
                UnityEngine.Object.Destroy(child);
            }

            Fill(app, false);
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
                    Refill(app);
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
            var buyLabel = Ui.Fit(buy.GetComponentInChildren<UnityEngine.UI.Text>(), 9);
            var priceText = buyLabel.text;
            var armed = false;
            var armedAt = 0f;
            void Disarm()
            {
                if (armed && buyLabel != null)
                {
                    armed = false;
                    buyLabel.text = priceText;
                }
            }

            buy.OnClick(() =>
            {
                if (!affordable)
                {
                    var deficit = Mathf.Max(1, frame.Price - progress.Coins);
                    Ui.Deny((RectTransform)buy.transform, L.F("Не хватает {0} {1}", deficit, YarnCatalog.CoinsGenitive(deficit)));
                    return;
                }

                if (!armed)
                {
                    // Первый тап спрашивает, второй покупает; без второго за 3 с кнопка возвращается к цене.
                    // Вооружённая кнопка одна: прежней сразу возвращается цена.
                    _disarm?.Invoke();
                    armed = true;
                    armedAt = Time.unscaledTime;
                    _disarm = Disarm;
                    buyLabel.text = L.F("Купить за {0}?", frame.Price);
                    Haptics.Play(Haptic.Medium);
                    // Таймер прежнего вооружения не снимает новое: сверяем момент вооружения.
                    var armedNow = armedAt;
                    Tweens.Delay(buy, 3f, () =>
                    {
                        if (armed && armedAt == armedNow)
                        {
                            Disarm();
                        }
                    });
                    return;
                }

                // Двойной тап не покупает: второе касание принимается не раньше чем через 0,3 с.
                if (Time.unscaledTime - armedAt < 0.3f)
                {
                    return;
                }

                if (!progress.BuyFrame(frame.Id))
                {
                    var lack = frame.Price - progress.Coins;
                    Ui.Deny((RectTransform)buy.transform, lack > 0 ? L.F("Не хватает {0} {1}", lack, YarnCatalog.CoinsGenitive(lack)) : null);
                    Refill(app);
                    return;
                }

                Analytics.Event("frame_buy", ("frame", frame.Id), ("price", frame.Price));
                app.Save();
                Sfx.Play(Sound.Coin);
                Haptics.Play(Haptic.Success);
                Refill(app);
            });
        }
    }
}
