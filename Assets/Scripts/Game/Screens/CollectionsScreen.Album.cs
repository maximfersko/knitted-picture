#nullable enable

using KnittedPicture.Core;
using UnityEngine;

namespace KnittedPicture.Game
{
    public sealed partial class CollectionsScreen
    {
        // ---------- Альбом ----------

        /// <summary>Каждая коллекция — карточка-страница альбома: шапка, пять картинок с подписями, награда.</summary>
        private float BuildAlbum(RectTransform content)
        {
            var progress = App.Progress;
            var y = 6f;
            var cardW = W - 32f;
            const float gap = 8f;
            var justAdded = App.JustAdded;

            void BuildActions()
            {
                const float height = 48f;
                var count = progress.ExhibitionOpen ? 2 : 1;
                var width = (cardW - (count - 1) * 8f) / count;
                var frames = Ui.TextButton(content, L.T("Рамки"), 16f, y, width, height, Palette.Ink, 15);
                frames.OnClick(() => App.ShowPopup(new FramesPopup(App)));
                if (progress.ExhibitionOpen)
                {
                    var label = progress.ExhibitionDone(App.Today) ? L.T("Выставка") + " ✓" : L.T("Выставка");
                    var exhibition = Ui.TextButton(content, label, 16f + width + 8f, y, width, height, Palette.Ink, 15);
                    exhibition.OnClick(() => App.ShowPopup(new ExhibitionPopup(App, App.RefreshScreen)));
                }
                y += height + 16f;
            }

            for (var c = 0; c < Collections.All.Length; c++)
            {
                var collection = Collections.All[c];
                var tile = Mathf.Floor((cardW - 28f - gap * (collection.Count - 1)) / collection.Count);
                var done = 0;
                var stars = 0;
                for (var id = collection.FirstLevel; id <= collection.LastLevel; id++)
                {
                    done += progress.IsCompleted(id) ? 1 : 0;
                    stars += progress.StarsOf(id);
                }

                var tilesY = 66f;
                var stripY = tilesY + tile + MiniPicture.StarsOverhang + 34f;
                var cardH = stripY + 48f + 14f;
                var cardY = y;
                var index = c;

                // Карточки ниже первого экрана достраиваются в следующих кадрах (Defer): альбом открывается сразу.
                void BuildCard()
                {
                var card = Ui.Card(content, collection.Name, 16, cardY, cardW, cardH, 24, Palette.Card, Palette.CardShadow, 3f);
                Header(card, cardW, collection.Name, L.F("{0} из {1} картинок", done, collection.Count), done == collection.Count ? L.T("Собрана") : null,
                    $"{stars}/{collection.Count * 3}");

                MiniPicture.Shelf(card, 8f, tilesY + tile - 4f, collection.Count * (tile + gap) - gap + 12f);
                for (var i = 0; i < collection.Count; i++)
                {
                    var id = collection.FirstLevel + i;
                    if (!App.Levels.Has(id))
                    {
                        continue;
                    }

                    var level = App.Levels.Get(id);
                    var state = progress.IsCompleted(id) ? TileState.Done : progress.IsAvailable(id) ? TileState.Current : TileState.Locked;
                    var x = 14 + i * (tile + gap);
                    var levelId = id;
                    var tap = MiniPicture.Tile(card, level, state, x, tilesY, tile, progress.StarsOf(id));
                    var rt = (RectTransform)tap.transform;
                    if (id == justAdded)
                    {
                        NewInAlbum(card, rt, x, tilesY, tile);
                    }
                    tap.OnClick(() => LevelInfoPopup.OnTile(App, levelId, rt));
                    var name = Ui.Label(card, state == TileState.Locked ? L.F("Ур. {0}", id) : level.Title, 11,
                        state == TileState.Current ? Palette.Primary : state == TileState.Locked ? Palette.Ink3 : Palette.Ink2,
                        x - 3, tilesY + tile + MiniPicture.StarsOverhang + 3f, tile + 6, 26, state == TileState.Current, TextAnchor.UpperCenter);
                    name.lineSpacing = 0.85f;

                    // Длинное слово («Клубничка», «Подсолнух») не наезжает на соседнее: ужимается в свою ширину.
                    Ui.Fit(name, 8);
                }

                CollectionReward.Build(App, card, index, 12, stripY, cardW - 24, () => App.ShowCollections(CollectionsTab.Album));
                }

                if (cardY > H)
                {
                    Defer(BuildCard);
                }
                else
                {
                    BuildCard();
                }

                y += cardH + 16f;
                if (c == 0)
                {
                    BuildActions();
                }
            }
            if (Collections.All.Length == 0)
            {
                BuildActions();
            }

            // Подсветка новой картинки — один раз.
            App.JustAdded = 0;
            return y;
        }

        /// <summary>
        /// Картинка, только что попавшая в альбом: рамка трижды вспыхивает золотом и гаснет, над ней ярлык «Новая»,
        /// из рамки искры. Один раз — при первом заходе в альбом после победы.
        /// </summary>
        private void NewInAlbum(RectTransform card, RectTransform tile, float x, float y, float size)
        {
            var ring = Ui.Image(card, "NewRing", Art.RoundRectOutline(10, 3), Palette.Coin);
            ring.type = UnityEngine.UI.Image.Type.Sliced;
            ring.pixelsPerUnitMultiplier = Art.Density;
            Ui.Place(ring.rectTransform, x - 4f, y - 4f, size + 8f, size + 8f);
            ring.raycastTarget = false;
            Tweens.Run(ring, 2.4f, t =>
            {
                var pulse = Mathf.Abs(Mathf.Sin(t * Mathf.PI * 3f)) * (t < 0.83f ? 1f : (1f - t) / 0.17f);
                ring.color = Palette.WithAlpha(Palette.Coin, 0.15f + 0.85f * pulse);
            }, null, () => ring.color = Palette.WithAlpha(Palette.Coin, 0.4f), 0.35f);

            var label = Ui.Box(card, "NewLabel", 9, Palette.Danger).rectTransform;
            Ui.Place(label, x + size / 2f - 26f, y - 12f, 52f, 18f);
            Ui.Label(label, L.T("Новая"), 11, Color.white, 0, 0, 52, 18);
            Tweens.Scale(label, 0f, 1f, 0.4f, Ease.OutBack, null, 0.35f);
            Tweens.Delay(tile, 0.5f, () => Fx.Sparkle(Root, Ui.WorldCenter(tile), Palette.Coin, 10, size * 0.7f));
        }

        /// <summary>Полоска «В работе»: моток, его бонус; тап — карточка мотка. Её же видно на экране сундука.</summary>
        public static void ActiveYarnStrip(RectTransform parent, float x, float y, float w, System.Action refresh)
        {
            var app = App.Instance;
            if (app == null)
            {
                return;
            }

            var progress = app.Progress;
            var active = progress.ActiveYarn;
            var open = progress.PerksOpen;
            var strip = Ui.Box(parent, "Active", 14, active >= 0 && open ? Palette.PrimarySoft : Palette.Surface, active >= 0).rectTransform;
            Ui.Place(strip, x, y, w, 40);
            if (active < 0 || !open)
            {
                // До 5-го уровня пряжа — просто красивая коллекция: про бонусы только одна тихая строка.
                var text = !open ? L.F("Бонусы пряжи откроются на уровне {0}", YarnCatalog.PerksFromLevel) : L.T("На спицах пока ничего: найди первый моток");
                Ui.Fit(Ui.Label(strip, text, 12, Palette.Ink2, 12, 0, w - 24, 40, false, TextAnchor.MiddleLeft), 9);
                return;
            }

            var yarn = YarnCatalog.All[active];
            var perk = YarnCatalog.PerkOf(active);
            YarnView.Create(strip, yarn, 22, 20, 26, true);
            Ui.Fit(Ui.Label(strip, L.F("На спицах: {0} · ур. {1}", yarn.Name, progress.YarnLevel(active)), 13, Palette.Ink, 42, 3, w - 52, 18, true,
                TextAnchor.MiddleLeft), 10);
            Ui.Fit(Ui.Label(strip, YarnCatalog.PerkText(perk, YarnCatalog.Power(active, progress.YarnLevel(active))), 11, Palette.Primary,
                42, 20, w - 52, 16, false, TextAnchor.MiddleLeft), 8);
            strip.gameObject.AddComponent<Tap>().OnClick(() =>
                app.ShowPopup(new YarnCardPopup(app, active, refresh)));
        }

        /// <summary>
        /// Шапка карточки: название и подпись слева; справа — зелёная плашка «Собрана»
        /// или счёт со звездой.
        /// </summary>
        private static void Header(RectTransform card, float cardW, string title, string subtitle, string? doneChip, string score)
        {
            // Плашка справа — по ширине текста; название и подпись занимают остаток строки с зазором 12
            // и ужимаются, если не влезают, но на плашку не заходят.
            var chipText = doneChip ?? (score.Length > 0 ? score : null);
            var chipW = 0f;
            if (chipText != null)
            {
                var done = doneChip != null;
                var label = Ui.Label(card, chipText, 13, done ? Palette.Primary : Palette.Ink, true, TextAnchor.MiddleLeft);
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                chipW = Mathf.Min(Mathf.Ceil(label.preferredWidth) + 42f, cardW * 0.42f);
                var back = done ? Palette.PrimarySoft : Palette.Surface;
                var chip = Ui.Card(card, done ? "Done" : "Score", cardW - 16 - chipW, 18, chipW, 28, 14, back, back, 0f);
                if (done)
                {
                    Ui.IconImage(chip, Icon.Check, Palette.Primary, 18, 14, 14);
                }
                else
                {
                    Ui.IconImage(chip, Icon.Star, Palette.Coin, 18, 14, 16);
                }

                label.rectTransform.SetParent(chip, false);
                Ui.Place(label.rectTransform, 30, 0, chipW - 36, 28);
            }

            var textW = cardW - 16 - (chipW > 0 ? chipW + 12f : 0f) - 16;
            Ui.Fit(Ui.Label(card, title, 18, Palette.Ink, 16, 12, textW, 26, true, TextAnchor.MiddleLeft), 13);
            Ui.Fit(Ui.Label(card, subtitle, 13, Palette.Ink2, 16, 36, textW, 20, false, TextAnchor.MiddleLeft), 10);
        }
    }
}
