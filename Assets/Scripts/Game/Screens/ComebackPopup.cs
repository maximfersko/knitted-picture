#nullable enable

using System;
using KnittedPicture.Core;
using UnityEngine;

namespace KnittedPicture.Game
{
    /// <summary>
    /// «Посылка от бабушки» после перерыва в 3 дня и больше: коробка с плашкой редкости (на ступень выше
    /// следующего сундука), что внутри и обещание мягкого входа: следующая новая картинка с бесплатными
    /// бустерами. По практике игр с возвратом игроков: подарок сразу при входе и лёгкая первая победа.
    /// </summary>
    public sealed class ComebackPopup : Popup
    {
        public ComebackPopup(App app, Action? closed)
            : base(app)
        {
            Closed = closed;
            var progress = app.Progress;
            var rarity = progress.ComebackRarity;

            const float size = 110f;
            YarnView.Aura(Card, rarity, CardWidth / 2f, Cursor + size / 2f, size * 0.85f);
            var gift = Ui.Image(Card, "Gift", Art2.Gift, Color.white);
            Ui.PlaceCenter(gift.rectTransform, CardWidth / 2f, Cursor + size / 2f, size, size);
            var giftRt = gift.rectTransform;
            Tweens.Run(giftRt, 1.8f, t => giftRt.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * Mathf.PI * 4f) * 6f * Ease.Bump(t)), null, null, 0.3f, true);
            Space(size + 8f);
            AddTitle(L.T("Посылка от бабушки"), 23);
            var days = progress.Data.comebackDays;
            AddText(L.F("Тебя не было {0} {1}. Бабушка соскучилась и прислала посылку", days, L.Plural(days, "день", "дня", "дней")), 14, 2);
            RarityStyle.Chip(Card, rarity, CardWidth / 2f, Cursor + 10f, 0f, Gender.Feminine);
            Space(28f);
            var range = Chest.CoinRange(rarity);
            AddText(L.F("Внутри: {0} {3}, бустер ×{1} и моток не хуже, чем «{2}»", (range.Min + range.Max) / 2, Chest.BoosterCountOf(rarity),
                Rarities.Name(rarity > Rarity.Epic ? Rarity.Epic : rarity, Gender.Masculine), YarnCatalog.CoinsWord((range.Min + range.Max) / 2)), 13, 2);
            AddText(L.T("А следующая новая картинка начнётся с бесплатными бустерами"), 13, 2);
            Space(4f);
            var open = AddPrimary(L.T("Открыть"));
            Ui.Sheen((RectTransform)open.transform, Inner, 60f, 3.2f, 0.4f);
            open.OnClick(() =>
            {
                var granted = progress.ClaimComeback(app.Random);
                Analytics.Event("comeback_claim", ("days", days), ("rarity", rarity.ToString()));
                app.Save();
                var after = Closed;
                Closed = null;
                Close();
                if (granted != null)
                {
                    app.ShowPopup(new RewardPopup(app, L.T("Посылка от бабушки"), Icon.Gift, granted.Value, after, Art2.Gift));
                }
                else
                {
                    after?.Invoke();
                }
            });
            Finish(16);
        }
    }

    /// <summary>
    /// «Книга находок»: вся пряжа и все рамки по ступеням редкости. Найденное — как есть (золотые мотки с короной),
    /// ненайденное — тёмный силуэт и «?», у рамок — откуда они берутся. Видно, чего не хватает до полной книги.
    /// </summary>
    public sealed class FindsPopup : Popup
    {
        public FindsPopup(App app)
            : base(app, bottomSheet: true)
        {
            var progress = app.Progress;
            var yarnFound = 0;
            for (var i = 0; i < YarnCatalog.Count; i++)
            {
                yarnFound += progress.HasYarn(i) ? 1 : 0;
            }

            var framesFound = 0;
            foreach (var f in AlbumFrames.All)
            {
                framesFound += progress.FrameOwned(f.Id) ? 1 : 0;
            }

            var found = yarnFound + framesFound;
            var total = YarnCatalog.Count + AlbumFrames.All.Length;
            AddTitle(L.T("Книга находок"), 24);
            AddText(L.F("Найдено {0} из {1}", found, total), 14, 1);
            Ui.ProgressBar(Card, 24, Cursor, Inner, 10, (float)found / total, Palette.Coin);
            Space(22f);

            var viewH = RoomFor(6f + 8f);
            var content = Ui.Scroll(Card, 0, Cursor, CardWidth, viewH);
            const int columns = 4;
            const float gap = 10f;
            var cellW = Mathf.Floor((Inner - gap * (columns - 1)) / columns);
            var y = 4f;
            var built = 0;

            y = Heading(content, L.T("Пряжа"), yarnFound, YarnCatalog.Count, y);
            foreach (var tier in Rarities.All)
            {
                var items = new System.Collections.Generic.List<int>();
                for (var i = 0; i < YarnCatalog.Count; i++)
                {
                    if (YarnCatalog.All[i].Rarity == tier)
                    {
                        items.Add(i);
                    }
                }

                if (items.Count == 0)
                {
                    continue;
                }

                y = TierRow(content, tier, items.FindAll(progress.HasYarn).Count, items.Count, y, Gender.Feminine);
                for (var k = 0; k < items.Count; k++)
                {
                    var yarn = YarnCatalog.All[items[k]];
                    var x = 24f + (k % columns) * (cellW + gap);
                    var cy = y + (k / columns) * (cellW + 24f + gap);
                    Stagger(built++, 12, () => YarnCell(app, content, yarn, x, cy, cellW));
                }

                y += ((items.Count + columns - 1) / columns) * (cellW + 24f + gap) + 6f;
            }

            y = Heading(content, L.T("Рамки"), framesFound, AlbumFrames.All.Length, y + 8f);
            foreach (var tier in Rarities.All)
            {
                var items = new System.Collections.Generic.List<FrameInfo>();
                foreach (var f in AlbumFrames.All)
                {
                    if (f.Rarity == tier)
                    {
                        items.Add(f);
                    }
                }

                if (items.Count == 0)
                {
                    continue;
                }

                y = TierRow(content, tier, items.FindAll(f => progress.FrameOwned(f.Id)).Count, items.Count, y, Gender.Feminine);
                for (var k = 0; k < items.Count; k++)
                {
                    var x = 24f + (k % columns) * (cellW + gap);
                    var cy = y + (k / columns) * (cellW + 34f + gap);
                    var frame = items[k];
                    Stagger(built++, 12, () => FrameCell(app, content, frame, x, cy, cellW));
                }

                y += ((items.Count + columns - 1) / columns) * (cellW + 34f + gap) + 6f;
            }

            content.sizeDelta = new Vector2(CardWidth, y + 12f);
            Space(viewH + 6f);
            Finish(8);
        }

        private float Heading(RectTransform content, string title, int found, int total, float y)
        {
            Ui.Label(content, title, 18, Palette.Ink, 24, y, Inner - 80, 26, true, TextAnchor.MiddleLeft);
            Ui.Label(content, $"{found}/{total}", 14, Palette.Ink2, 24 + Inner - 80, y, 80, 26, true, TextAnchor.MiddleRight);
            return y + 32f;
        }

        private float TierRow(RectTransform content, Rarity tier, int found, int total, float y, Gender gender)
        {
            var gem = Ui.Image(content, "Gem", Art2.Gem(tier), Color.white);
            Ui.PlaceCenter(gem.rectTransform, 34, y + 11f, 18, 18);
            var name = Rarities.Name(tier, gender);
            Ui.Label(content, char.ToUpper(name[0]) + name.Substring(1), 14, RarityStyle.Ink(tier), 48, y, Inner - 100, 22, true, TextAnchor.MiddleLeft);
            Ui.Label(content, $"{found}/{total}", 13, Palette.Ink3, 24 + Inner - 60, y, 60, 22, true, TextAnchor.MiddleRight);
            return y + 28f;
        }

        private void YarnCell(App app, RectTransform content, YarnInfo yarn, float x, float y, float w)
        {
            var has = app.Progress.HasYarn(yarn.Id);
            var cell = Ui.Box(content, "Yarn", 14, has ? Color.Lerp(RarityStyle.Soft(yarn.Rarity), Color.white, 0.4f) : Palette.Surface, true).rectTransform;
            Ui.Place(cell, x, y, w, w);
            var ball = w * 0.66f;
            if (has)
            {
                YarnView.Create(cell, yarn, w / 2f, w / 2f, ball, true);
                var id = yarn.Id;
                cell.gameObject.AddComponent<Tap>().OnClick(() => app.ShowPopup(new YarnCardPopup(app, id, () => { })));
            }
            else
            {
                // Силуэт: форма клубка тёмной тенью и «?».
                var shadow = Ui.Image(cell, "Silhouette", Art2.YarnBall(Color.white), new Color(0.1f, 0.12f, 0.18f, 0.22f));
                Ui.PlaceCenter(shadow.rectTransform, w / 2f, w / 2f, ball, ball);
                Ui.Label(cell, "?", ball * 0.5f, Palette.Ink3, 0, 0, w, w);
            }

            Ui.Fit(Ui.Label(content, has ? yarn.Name : "???", 11, has ? Palette.Ink : Palette.Ink3, x - 2f, y + w + 2f, w + 4f, 18), 8);
        }

        private void FrameCell(App app, RectTransform content, FrameInfo frame, float x, float y, float w)
        {
            var has = app.Progress.FrameOwned(frame.Id);
            var root = Ui.Place(Ui.Node(content, "Frame"), x, y, w, w);
            MiniPicture.Framed(root, frame.Id, w, null);
            if (!has)
            {
                // Силуэт рамки: затемнение и замок.
                if (root.GetComponentInChildren<UnityEngine.UI.Image>() is { } image)
                {
                    image.color = new Color(0.1f, 0.12f, 0.18f, 0.3f);
                }

                Ui.IconImage(root, Icon.Lock, Palette.Ink3, w / 2f, w / 2f, w * 0.26f);
            }

            var how = has ? frame.Name
                : frame.Source == FrameSource.Season ? L.T("финал сезона")
                : frame.Source == FrameSource.Collection ? L.F("все звёзды «{0}»", Collections.All[frame.Tag].Name)
                : $"{frame.Price} {YarnCatalog.CoinsWord(frame.Price)}";
            var label = Ui.Label(content, how, 10, has ? Palette.Ink : Palette.Ink3, x - 2f, y + w + 2f, w + 4f, 30);
            label.lineSpacing = 0.85f;
            Ui.Fit(label, 8);
        }
    }
}
