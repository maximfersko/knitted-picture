#nullable enable

using KnittedPicture.Core;
using UnityEngine;

namespace KnittedPicture.Game
{
    public sealed partial class CollectionsScreen
    {
        // ---------- Пряжа ----------

        /// <summary>
        /// Сверху — плетёная корзинка с найденными мотками и общим прогрессом, ниже — наборы карточками.
        /// Плитка мотка окрашена редкостью: у редкой мягкое сияние, у эпической — медленные лучи; найденные
        /// мотки чуть покачиваются, каждый в своём ритме, искры блёсток вспыхивают по очереди. При входе
        /// плитки выпрыгивают волной. «Меньше движения» всё это останавливает.
        /// </summary>
        private float BuildYarn(RectTransform content)
        {
            var progress = App.Progress;
            var cardW = W - 32f;
            var y = 6f;
            var entering = App.PreviousScreen != typeof(CollectionsScreen);

            const float bannerH = 150f;
            var banner = Ui.Card(content, "Basket", 16, y, cardW, bannerH, 24, Palette.Card, Palette.CardShadow, 3f);
            BuildBasket(banner, progress);
            const float textX = 118f;
            Ui.Fit(Ui.Label(banner, L.T("Корзинка пряжи"), 18, Palette.Ink, textX, 14, cardW - textX - 48, 26, true, TextAnchor.MiddleLeft), 13);
            var kinds = Ui.Label(banner, L.F("{0} из {1} мотков", progress.YarnKinds, YarnCatalog.Count), 13, Palette.Ink2, textX, 40, cardW - textX - 48, 18, false,
                TextAnchor.MiddleLeft);
            if (entering)
            {
                kinds.text = L.F("0 из {0} мотков", YarnCatalog.Count);
                Ui.RollTo(kinds, progress.YarnKinds, 0.8f, L.F("{{0}} из {0} мотков", YarnCatalog.Count), 0.15f);
            }
            var fill = Ui.ProgressBar(banner, textX, 66, cardW - textX - 18, 10, (float)progress.YarnKinds / YarnCatalog.Count, Palette.Link);
            if (entering && !Tweens.Calm)
            {
                // Полоска набирается до своего значения.
                var full = fill.sizeDelta.x;
                Tweens.Run(fill, 0.8f, t => fill.sizeDelta = new Vector2(Mathf.Max(10f, full * t), fill.sizeDelta.y), Ease.OutCubic, null, 0.15f);
            }

            Ui.InfoButton(banner, cardW - 30, 26, L.T("Пряжа и её бонусы"),
                L.T("Мотки выпадают за новые картинки, в звёздном сундуке и на 7-й день подарков. ") +
                L.F("С уровня {0} моток «в работе» даёт бонус: монеты за картинку или дороже золотые петли. ", YarnCatalog.PerksFromLevel) +
                L.T("Повтор поднимает уровень мотка до 3, потом 5 повторов делают моток золотым (бонус сильнее), а у золотого повтор даёт монеты. Полный набор приносит награду."), 26);
            ActiveYarnStrip(banner, 12, 98, cardW - 24, () => App.ShowCollections(CollectionsTab.Yarn));
            y += bannerH + 12f;

            // Книга находок: вся пряжа и все рамки по редкости.
            {
                const float stripH = 56f;
                var found = 0;
                for (var i = 0; i < YarnCatalog.Count; i++)
                {
                    found += progress.HasYarn(i) ? 1 : 0;
                }

                foreach (var f in AlbumFrames.All)
                {
                    found += progress.FrameOwned(f.Id) ? 1 : 0;
                }

                var total = YarnCatalog.Count + AlbumFrames.All.Length;
                var book = Ui.Card(content, "Finds", 16, y, cardW, stripH, 18, Palette.Card, Palette.CardShadow, 3f, true);
                var icon = Ui.Image(book, "Book", Art2.AlbumBook, Color.white);
                Ui.PlaceCenter(icon.rectTransform, 32, stripH / 2f, 38, 38);
                Ui.Fit(Ui.Label(book, L.T("Книга находок"), 16, Palette.Ink, 62, 0, cardW - 170, stripH, true, TextAnchor.MiddleLeft), 11);
                Ui.Label(book, L.F("{0} из {1}", found, total), 14, Palette.Ink2, cardW - 120, 0, 84, stripH, true, TextAnchor.MiddleRight);
                Ui.IconImage(book, Icon.Back, Palette.Ink3, cardW - 22, stripH / 2f, 16).rectTransform.localRotation = Quaternion.Euler(0f, 0f, 180f);
                book.gameObject.AddComponent<Tap>().OnClick(() => App.ShowPopup(new FindsPopup(App)));
                y += stripH + 16f;
            }

            const float gap = 8f;
            var setIndex = 0;
            var tileIndex = 0;
            foreach (var set in YarnCatalog.Sets)
            {
                var owned = 0;
                for (var i = set.First; i < set.First + set.Count; i++)
                {
                    owned += progress.HasYarn(i) ? 1 : 0;
                }

                var cellW = (cardW - 28f - gap * (set.Count - 1)) / set.Count;
                const float cellH = 140f;
                var cellsY = 66f;
                var stripY = cellsY + cellH + 14f;
                var cardH = stripY + 48f + 14f;
                var cardY = y;
                var target = set;
                var firstTile = tileIndex;
                var order = setIndex;
                var ownedCount = owned;

                // Наборы ниже первого экрана достраиваются в следующих кадрах (Defer).
                void BuildSet()
                {
                    var card = Ui.Card(content, target.Name, 16, cardY, cardW, cardH, 24, Palette.Card, Palette.CardShadow, 3f);
                    Header(card, cardW, target.Name, L.F("{0} из {1} мотков", ownedCount, target.Count), ownedCount == target.Count ? L.T("Собран") : null, "");
                    for (var i = 0; i < target.Count; i++)
                    {
                        var yarn = YarnCatalog.All[target.First + i];
                        var x = 14 + i * (cellW + gap);
                        var cell = YarnTile(card, yarn, x, cellsY, cellW, cellH, firstTile + i);
                        if (entering)
                        {
                            Tweens.Scale(cell, 0.82f, 1f, 0.42f, Ease.OutBack, null, 0.08f + order * 0.1f + i * 0.05f);
                        }
                    }

                    var booster = BoosterInfo.Name(target.Reward.Booster!.Value).ToLower();
                    RewardStrip.Build(card, 12, stripY, cardW - 24, L.F("За весь набор: {0} {3} и {1} ×{2}", target.Reward.Coins, booster, target.Reward.BoosterCount, YarnCatalog.CoinsWord(target.Reward.Coins)),
                        (float)ownedCount / target.Count, progress.CanClaimSet(target), progress.IsSetClaimed(target), L.T("Набор собран!"),
                        () => Claim(progress.ClaimSet(target, App.Random), L.F("Набор «{0}»", target.Name), Icon.Gift, CollectionsTab.Yarn));
                }

                if (cardY > H)
                {
                    Defer(BuildSet);
                }
                else
                {
                    BuildSet();
                }

                tileIndex += set.Count;
                y += cardH + 16f;
                setIndex++;
            }

            return y;
        }

        /// <summary>
        /// Корзинка в шапке: задняя стенка с ручкой, до трёх найденных мотков (сначала тот, что «в работе»,
        /// дальше последние найденные) и передняя плетёная стенка поверх. Мотки чуть покачиваются.
        /// </summary>
        private static void BuildBasket(RectTransform banner, ProgressService progress)
        {
            const float bx = 8f;
            const float by = 4f;
            const float bw = 104f;
            const float bh = bw * 96f / 120f;
            Ui.Place(Ui.Image(banner, "BasketBack", Art2.BasketBack, Color.white).rectTransform, bx, by, bw, bh);

            var shown = new System.Collections.Generic.List<int>();
            if (progress.ActiveYarn >= 0 && progress.HasYarn(progress.ActiveYarn))
            {
                shown.Add(progress.ActiveYarn);
            }

            for (var id = YarnCatalog.Count - 1; id >= 0 && shown.Count < 3; id--)
            {
                if (progress.HasYarn(id) && !shown.Contains(id))
                {
                    shown.Add(id);
                }
            }

            // Горловина корзинки в растре — на высоте 46 из 96 (снизу): мотки лежат чуть выше неё.
            var rim = by + bh * (1f - 46f / 96f);
            float[] xs = { 0.5f, 0.3f, 0.7f };
            float[] lift = { 12f, 6f, 6f };
            for (var i = shown.Count - 1; i >= 0; i--)
            {
                var size = i == 0 ? 36f : 30f;
                var view = YarnView.Create(banner, YarnCatalog.All[shown[i]], bx + bw * xs[i], rim - lift[i], size, true);
                var rest = view.anchoredPosition;
                var phase = i * 0.33f;
                Tweens.Run(view, 3.2f, t =>
                {
                    var wave = Mathf.Sin((t + phase) * Mathf.PI * 2f);
                    view.anchoredPosition = rest + new Vector2(0f, 1.8f * wave);
                    view.localRotation = Quaternion.Euler(0f, 0f, 4f * Mathf.Sin((t + phase) * Mathf.PI * 2f + 1f));
                }, null, null, 0f, true);
            }

            Ui.Place(Ui.Image(banner, "BasketFront", Art2.BasketFront, Color.white).rectTransform, bx, by, bw, bh);
        }

        /// <summary>
        /// Плитка мотка: фон по редкости (обычная — светлая, дальше — оттенок своей ступени: зелёный, синий,
        /// фиолетовый, золото; от редкой — сияние, от эпической — лучи), клубок на мягкой тени покачивается, звезда уровня, камень редкости. Не найден — бледный
        /// силуэт с «?». «В работе» — зелёная рамка дышит. Тап — карточка мотка.
        /// </summary>
        private RectTransform YarnTile(RectTransform card, YarnInfo yarn, float x, float y, float cellW, float cellH, int index)
        {
            var progress = App.Progress;
            var has = progress.HasYarn(yarn.Id);
            var back = !has ? Palette.WithAlpha(Palette.Surface, 0.6f)
                : yarn.Rarity == Rarity.Common ? Palette.Surface
                : Color.Lerp(RarityStyle.Soft(yarn.Rarity), Color.white, 0.35f);
            var cell = Ui.Box(card, "Yarn", 16, back, true).rectTransform;
            Ui.Place(cell, x, y, cellW, cellH);
            var border = !has ? Palette.WithAlpha(Palette.Line, 0.7f)
                : yarn.Rarity == Rarity.Common ? Palette.Line
                : Palette.WithAlpha(RarityStyle.Color(yarn.Rarity), yarn.Rarity >= Rarity.Epic ? 0.7f : 0.4f);
            Ui.Outline(cell, 16, border, 1.5f);

            const float ballY = 46f;
            const float ballSize = 58f;
            if (has)
            {
                // Свечение редкости: голубое у редкой, тёплое с лучами у эпической (YarnView.Aura).
                YarnView.Aura(cell, yarn.Rarity, cellW / 2f, ballY, ballSize, index * 0.21f, lite: true);
            }

            var shadow = Ui.Image(cell, "Shadow", Art2.BallShadow, Palette.WithAlpha(Color.white, has ? 1f : 0.5f));
            var shadowRt = shadow.rectTransform;
            Ui.PlaceCenter(shadowRt, cellW / 2f + 2f, ballY + ballSize * 0.46f, ballSize * 0.9f, ballSize * 0.26f);
            var ball = YarnView.Create(cell, yarn, cellW / 2f, ballY, ballSize, has);
            if (has)
            {
                // Покачивание: клубок чуть приподнимается, тень под ним сжимается; у каждого свой ритм.
                var rest = ball.anchoredPosition;
                var period = 2.6f + (index % 3) * 0.35f;
                Tweens.Run(ball, period, t =>
                {
                    var lift = 0.5f - 0.5f * Mathf.Cos(t * Mathf.PI * 2f);
                    ball.anchoredPosition = rest + new Vector2(0f, 3f * lift);
                    shadowRt.localScale = new Vector3(1f - 0.12f * lift, 1f - 0.12f * lift, 1f);
                }, null, null, index * 0.37f, true);
            }

            Ui.Fit(Ui.Label(cell, has ? yarn.Name : "???", 13, has ? Palette.Ink : Palette.Ink3, 2, 84, cellW - 4, 20), 10);
            RarityStyle.Chip(cell, yarn.Rarity, cellW / 2f, 118f, cellW - 8f);

            // Уровень мотка — на выпуклой звезде в углу.
            if (has)
            {
                var star = Ui.Image(cell, "Level", Art2.LevelStar, Color.white);
                Ui.PlaceCenter(star.rectTransform, cellW - 17f, 17f, 30f, 30f);
                Ui.Label(star.rectTransform, progress.YarnLevel(yarn.Id).ToString(), 12, Color.white, 0, 2, 30, 26);
            }

            if (progress.ActiveYarn == yarn.Id)
            {
                var ring = Ui.Image(cell, "ActiveRing", Art.RoundRectOutline(16, 2.5f), Palette.Primary);
                ring.type = UnityEngine.UI.Image.Type.Sliced;
                ring.pixelsPerUnitMultiplier = Art.Density;
                Ui.Stretch(ring.rectTransform);
                Tweens.Run(ring, 2.2f, t => ring.color = Palette.WithAlpha(Palette.Primary, 0.55f + 0.45f * Ease.Bump(t)), null, null, 0f, true);
                var mark = Ui.Image(cell, "Active", Art.Circle, Palette.Primary);
                Ui.PlaceCenter(mark.rectTransform, 15, 15, 20, 20);
                Ui.IconImage(cell, Icon.Check, Color.white, 15, 15, 12);
            }

            // Тап — карточка с характеристикой; у ненайденного видно, за чем охотиться.
            var id = yarn.Id;
            cell.gameObject.AddComponent<Tap>().OnClick(() =>
                App.ShowPopup(new YarnCardPopup(App, id, () => App.ShowCollections(CollectionsTab.Yarn))));
            return cell;
        }
    }
}
