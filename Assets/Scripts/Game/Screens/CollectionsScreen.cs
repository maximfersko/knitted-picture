#nullable enable

using KnittedPicture.Core;
using UnityEngine;

namespace KnittedPicture.Game
{
    public enum CollectionsTab
    {
        Album,
        Yarn,
        Achievements,
    }

    /// <summary>
    /// Коллекции: альбом картинок, «Корзинка» редкой пряжи и достижения.
    /// На каждой вкладке — награды, которые можно забрать; красные точки показывают, где они ждут.
    /// </summary>
    public sealed class CollectionsScreen : ScreenView
    {
        private const float TabsY = 62f;
        private const float ContentY = 116f;

        private readonly CollectionsTab _tab;

        public CollectionsScreen(App app, CollectionsTab tab)
            : base(app, "Collections")
        {
            _tab = tab;
            Ui.RoundButton(Root, Icon.Back, 16, 8).OnClick(app.ShowHome);
            Ui.Label(Root, "Коллекции", 22, Palette.Ink, 66, 8, W - 66 - 128, 44);
            Ui.CoinPill(Root, app.Progress.Coins, W - 16, 8);
            BuildTabs();

            var content = Ui.Scroll(Root, 0, ContentY, W, H - ContentY);
            float height;
            switch (tab)
            {
                case CollectionsTab.Yarn:
                    height = BuildYarn(content);
                    break;
                case CollectionsTab.Achievements:
                    height = BuildAchievements(content);
                    break;
                default:
                    height = BuildAlbum(content);
                    break;
            }

            content.sizeDelta = new Vector2(W, Mathf.Max(height + 24f, H - ContentY));
        }

        private void BuildTabs()
        {
            var tabs = new[]
            {
                (CollectionsTab.Album, "Альбом", Pending(CollectionsTab.Album)),
                (CollectionsTab.Yarn, "Пряжа", Pending(CollectionsTab.Yarn)),
                (CollectionsTab.Achievements, "Достижения", Pending(CollectionsTab.Achievements)),
            };
            // Подложка вкладок темнее фона, активная вкладка — белая с тенью: видно, где ты.
            var track = Ui.Box(Root, "Tabs", 22, Palette.LoomBg);
            Ui.Place(track.rectTransform, 16, TabsY, W - 32, 44);
            Ui.Outline(track.rectTransform, 22, Palette.Line, 1.5f);
            var tabW = (W - 32 - 8) / tabs.Length;
            for (var i = 0; i < tabs.Length; i++)
            {
                var (tab, title, pending) = tabs[i];
                var active = tab == _tab;
                var x = 16 + 4 + i * tabW;
                if (active)
                {
                    var shadow = Ui.Box(Root, "TabShadow", 18, Palette.CardShadow);
                    Ui.Place(shadow.rectTransform, x, TabsY + 6, tabW, 36);
                }

                var face = Ui.Box(Root, "Tab" + tab, 18, active ? Palette.Card : Color.clear, true);
                Ui.Place(face.rectTransform, x, TabsY + 4, tabW, 36);
                Ui.Label(face.rectTransform, title, 15, active ? Palette.Ink : Palette.Ink2, 0, 0, tabW, 36);
                if (pending > 0)
                {
                    Ui.Badge(face.rectTransform, tabW - 12, 6, pending);
                }

                var target = tab;
                face.gameObject.AddComponent<Tap>().OnClick(() =>
                {
                    if (target != _tab)
                    {
                        App.ShowCollections(target);
                    }
                });
            }
        }

        private int Pending(CollectionsTab tab)
        {
            var progress = App.Progress;
            var count = 0;
            switch (tab)
            {
                case CollectionsTab.Album:
                    for (var i = 0; i < Collections.All.Length; i++)
                    {
                        count += progress.CanClaimCollection(i) ? 1 : 0;
                    }

                    break;
                case CollectionsTab.Yarn:
                    foreach (var set in YarnCatalog.Sets)
                    {
                        count += progress.CanClaimSet(set) ? 1 : 0;
                    }

                    break;
                default:
                    foreach (var a in Achievements.All)
                    {
                        count += progress.CanClaimAchievement(a) ? 1 : 0;
                    }

                    break;
            }

            return count;
        }

        // ---------- Альбом ----------

        /// <summary>Каждая коллекция — карточка-страница альбома: шапка, пять картинок с подписями, награда.</summary>
        private float BuildAlbum(RectTransform content)
        {
            var progress = App.Progress;
            var y = 6f;
            var cardW = W - 32f;
            const float gap = 8f;
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
                var card = Ui.Card(content, collection.Name, 16, y, cardW, cardH, 24, Palette.Card, Palette.CardShadow, 3f);
                Header(card, cardW, collection.Name, $"{done} из {collection.Count} картинок", done == collection.Count ? "Собрана" : null,
                    $"{stars}/{collection.Count * 3}");

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
                    tap.OnClick(() => LevelInfoPopup.OnTile(App, levelId, rt));
                    var name = Ui.Label(card, state == TileState.Locked ? $"Ур. {id}" : level.name, 11,
                        state == TileState.Current ? Palette.Primary : state == TileState.Locked ? Palette.Ink3 : Palette.Ink2,
                        x - 6, tilesY + tile + MiniPicture.StarsOverhang + 3f, tile + 12, 26, state == TileState.Current, TextAnchor.UpperCenter);
                    name.lineSpacing = 0.85f;
                }

                CollectionReward.Build(App, card, c, 12, stripY, cardW - 24, () => App.ShowCollections(CollectionsTab.Album));
                y += cardH + 16f;
            }

            return y;
        }

        /// <summary>
        /// Шапка карточки: название и подпись слева; справа — зелёная плашка «Собрана»
        /// или счёт со звездой.
        /// </summary>
        private static void Header(RectTransform card, float cardW, string title, string subtitle, string? doneChip, string score)
        {
            Ui.Fit(Ui.Label(card, title, 18, Palette.Ink, 16, 12, cardW - 140, 26, true, TextAnchor.MiddleLeft), 13);
            Ui.Fit(Ui.Label(card, subtitle, 13, Palette.Ink2, 16, 36, cardW - 140, 20, false, TextAnchor.MiddleLeft), 10);
            if (doneChip != null)
            {
                var chip = Ui.Card(card, "Done", cardW - 16 - 104, 18, 104, 28, 14, Palette.PrimarySoft, Palette.PrimarySoft, 0f);
                Ui.IconImage(chip, Icon.Check, Palette.Primary, 18, 14, 14);
                Ui.Label(chip, doneChip, 13, Palette.Primary, 30, 0, 70, 28, true, TextAnchor.MiddleLeft);
            }
            else if (score.Length > 0)
            {
                var chip = Ui.Card(card, "Score", cardW - 16 - 92, 18, 92, 28, 14, Palette.Surface, Palette.Surface, 0f);
                Ui.IconImage(chip, Icon.Star, Palette.Coin, 18, 14, 16);
                Ui.Label(chip, score, 13, Palette.Ink, 30, 0, 58, 28, true, TextAnchor.MiddleLeft);
            }
        }

        // ---------- Пряжа ----------

        /// <summary>Сверху — корзинка с общим прогрессом, ниже — наборы карточками: мотки, редкость, награда.</summary>
        private float BuildYarn(RectTransform content)
        {
            var progress = App.Progress;
            var cardW = W - 32f;
            var y = 6f;

            var banner = Ui.Card(content, "Basket", 16, y, cardW, 80, 24, Palette.Card, Palette.CardShadow, 3f);
            var circle = Ui.Image(banner, "Back", Art.Circle, Palette.DangerSoft);
            Ui.PlaceCenter(circle.rectTransform, 44, 40, 56, 56);
            var logo = Ui.Image(banner, "Ball", Art.YarnBall, Palette.Hex("#FF6FA5"));
            Ui.PlaceCenter(logo.rectTransform, 44, 40, 38, 38);
            Ui.Label(banner, "Корзинка пряжи", 17, Palette.Ink, 84, 12, cardW - 140, 24, true, TextAnchor.MiddleLeft);
            Ui.Label(banner, $"{progress.YarnKinds} из {YarnCatalog.Count} мотков", 13, Palette.Ink2, 84, 34, cardW - 140, 18, false, TextAnchor.MiddleLeft);
            Ui.ProgressBar(banner, 84, 58, cardW - 104, 7, (float)progress.YarnKinds / YarnCatalog.Count, Palette.Danger);
            Ui.InfoButton(banner, cardW - 30, 26, "Откуда пряжа",
                "Моток выпадает за каждую новую картинку, из звёздного сундука и на 7-й день подарков. " +
                "Повтор превращается в монеты: 10, 20 или 40 по редкости. За полный набор дают награду.", 26);
            y += 80f + 16f;

            const float gap = 8f;
            foreach (var set in YarnCatalog.Sets)
            {
                var owned = 0;
                for (var i = set.First; i < set.First + set.Count; i++)
                {
                    owned += progress.HasYarn(i) ? 1 : 0;
                }

                var cellW = (cardW - 28f - gap * (set.Count - 1)) / set.Count;
                const float cellH = 128f;
                var cellsY = 66f;
                var stripY = cellsY + cellH + 14f;
                var cardH = stripY + 48f + 14f;
                var card = Ui.Card(content, set.Name, 16, y, cardW, cardH, 24, Palette.Card, Palette.CardShadow, 3f);
                Header(card, cardW, set.Name, $"{owned} из {set.Count} мотков", owned == set.Count ? "Собран" : null, "");

                for (var i = 0; i < set.Count; i++)
                {
                    var yarn = YarnCatalog.All[set.First + i];
                    var has = progress.HasYarn(yarn.Id);
                    var x = 14 + i * (cellW + gap);
                    var cell = Ui.Box(card, "Yarn", 16, has ? Palette.Surface : Palette.WithAlpha(Palette.Surface, 0.6f), has).rectTransform;
                    Ui.Place(cell, x, cellsY, cellW, cellH);
                    Ui.Outline(cell, 16, has ? Palette.Line : Palette.WithAlpha(Palette.Line, 0.7f), 1.5f);
                    YarnView.Create(cell, yarn, cellW / 2f, 38, 52, has);

                    Ui.Label(cell, has ? yarn.Name : "???", 13, has ? Palette.Ink : Palette.Ink3, 2, 70, cellW - 4, 20);
                    RarityChip(cell, yarn.Rarity, cellW / 2f, 104f);
                    var count = progress.YarnCount(yarn.Id);
                    if (count > 1)
                    {
                        var chip = Ui.Card(cell, "Count", cellW - 32, 6, 26, 18, 9, Palette.Ink, Palette.Ink, 0f);
                        Ui.Label(chip, "×" + count, 11, Color.white, 0, 0, 26, 18);
                    }

                    if (has)
                    {
                        var info = yarn;
                        cell.gameObject.AddComponent<Tap>().OnClick(() => App.Toast($"{info.Name}: {info.Description}"));
                    }
                }

                var target = set;
                var booster = BoosterInfo.Name(set.Reward.Booster!.Value).ToLower();
                RewardStrip.Build(card, 12, stripY, cardW - 24, $"За весь набор: {set.Reward.Coins} монет и {booster} ×{set.Reward.BoosterCount}",
                    (float)owned / set.Count, progress.CanClaimSet(set), progress.IsSetClaimed(set), "Набор собран!",
                    () => Claim(progress.ClaimSet(target, App.Random), "Набор «" + target.Name + "»", Icon.Gift, CollectionsTab.Yarn));
                y += cardH + 16f;
            }

            return y;
        }

        /// <summary>Плашка редкости: обычная — серая, редкая — фиолетовая, эпическая — золотая.</summary>
        private static void RarityChip(RectTransform parent, Rarity rarity, float cx, float cy)
        {
            var back = rarity == Rarity.Common ? Palette.Track : rarity == Rarity.Rare ? Palette.Hex("#EDE4FF") : Palette.CoinSoft;
            var text = YarnCatalog.RarityName(rarity);
            var w = text.Length * 6.6f + 18f;
            var chip = Ui.Box(parent, "Rarity", 10, back).rectTransform;
            Ui.PlaceCenter(chip, cx, cy, w, 20);
            Ui.Label(chip, text, 11, YarnView.RarityColor(rarity), 0, 0, w, 20);
        }

        // ---------- Достижения ----------

        /// <summary>
        /// Три секции: «Можно забрать» (золотые карточки с кнопкой), «В процессе» (прогресс и награда),
        /// «Получено» (белые карточки с зелёной плашкой — не теряются на фоне).
        /// </summary>
        private float BuildAchievements(RectTransform content)
        {
            var progress = App.Progress;
            var y = 4f;
            var sections = new[]
            {
                ("Можно забрать", 0),
                ("В процессе", 1),
                ("Получено", 2),
            };
            foreach (var (title, rank) in sections)
            {
                var items = new System.Collections.Generic.List<AchievementInfo>();
                foreach (var a in Achievements.All)
                {
                    if (Rank(a) == rank)
                    {
                        items.Add(a);
                    }
                }

                if (items.Count == 0)
                {
                    continue;
                }

                Ui.Label(content, $"{title} · {items.Count}", 14, Palette.Ink2, 20, y, W - 40, 24, true, TextAnchor.MiddleLeft);
                y += 28f;
                foreach (var a in items)
                {
                    y = AchievementRow(content, a, y);
                }

                y += 10f;
            }

            return y;

            int Rank(AchievementInfo a) => progress.CanClaimAchievement(a) ? 0 : progress.IsAchievementClaimed(a) ? 2 : 1;
        }

        private float AchievementRow(RectTransform content, AchievementInfo a, float y)
        {
            const float rowH = 72f;
            const float rightW = 96f;
            var progress = App.Progress;
            var claimed = progress.IsAchievementClaimed(a);
            var can = progress.CanClaimAchievement(a);
            var w = W - 32f;
            var row = Ui.Card(content, "Achievement", 16, y, w, rowH, 18, can ? Palette.CoinSoft : Palette.Card, Palette.CardShadow, 3f);
            if (can)
            {
                Ui.Outline(row, 18, Palette.Coin, 2f);
            }

            var circle = Ui.Image(row, "Icon", Art.Circle, can ? Palette.Coin : claimed ? Palette.PrimarySoft : Palette.Surface);
            Ui.PlaceCenter(circle.rectTransform, 36, rowH / 2f, 44, 44);
            Ui.IconImage(row, claimed ? Icon.Check : Icon.Trophy, can ? Color.white : claimed ? Palette.Primary : Palette.Ink3, 36, rowH / 2f, 24);

            var textW = w - 68f - rightW - 8f;
            var hasBar = !claimed;
            var titleY = hasBar ? 9f : 14f;
            Ui.Fit(Ui.Label(row, a.Title, 15, Palette.Ink, 68, titleY, textW, 20, true, TextAnchor.MiddleLeft), 11);
            Ui.Fit(Ui.Label(row, a.Description, 12, Palette.Ink2, 68, titleY + 21, textW, 18, false, TextAnchor.MiddleLeft), 9);
            var value = progress.AchievementValue(a);
            if (hasBar)
            {
                Ui.ProgressBar(row, 68, 54, textW, 6, (float)value / a.Target, can ? Palette.Primary : Palette.Link);
            }

            var right = w - rightW - 10f;
            if (can)
            {
                var target = a;
                Ui.Button(row, "Забрать", right, 16, rightW, 40, Palette.Primary, Palette.PrimaryShadow, Color.white, 15)
                    .OnClick(() => Claim(progress.ClaimAchievement(target, App.Random), target.Title, Icon.Trophy, CollectionsTab.Achievements));
            }
            else if (claimed)
            {
                var chip = Ui.Card(row, "Done", right + 4, 22, rightW - 4, 28, 14, Palette.PrimarySoft, Palette.PrimarySoft, 0f);
                Ui.IconImage(chip, Icon.Check, Palette.Primary, 16, 14, 14);
                Ui.Label(chip, "Получено", 12, Palette.Primary, 26, 0, rightW - 32, 28, true, TextAnchor.MiddleLeft);
            }
            else
            {
                Ui.Label(row, $"{value}/{a.Target}", 13, Palette.Ink2, right, 8, rightW, 20, true, TextAnchor.MiddleRight);
                RewardInline(row, a.Reward, right + rightW, 42);
            }

            return y + rowH + 10f;
        }

        /// <summary>Награда в одну строку, выровненная по правому краю: «+50 [монета] [бустер]».</summary>
        private static void RewardInline(RectTransform parent, Reward reward, float rightEdge, float cy)
        {
            var x = rightEdge;
            if (reward.Booster != null)
            {
                var badge = Ui.Image(parent, "Booster", Art.Circle, Palette.PrimarySoft);
                Ui.PlaceCenter(badge.rectTransform, x - 12, cy, 24, 24);
                Ui.IconImage(parent, BoosterInfo.Icon(reward.Booster.Value), Palette.Primary, x - 12, cy, 14);
                x -= 30f;
            }

            if (reward.Coins > 0)
            {
                // Число и монета — одной плашкой, вплотную друг к другу.
                var text = "+" + reward.Coins;
                var w = text.Length * 8.5f + 34f;
                var chip = Ui.Box(parent, "Coins", 12, Palette.CoinSoft).rectTransform;
                Ui.PlaceCenter(chip, x - w / 2f, cy, w, 24);
                Ui.Label(chip, text, 14, Palette.Ink, 6, 0, w - 30, 24, true, TextAnchor.MiddleRight);
                Ui.Coin(chip, w - 14, 12, 17);
            }
        }

        // ---------- Общее ----------

        private void Claim(Granted? granted, string title, Icon icon, CollectionsTab tab)
        {
            if (granted == null)
            {
                return;
            }

            App.Save();
            App.ShowPopup(new RewardPopup(App, title, icon, granted.Value, () => App.ShowCollections(tab)));
        }
    }

    /// <summary>
    /// Полоска награды внизу карточки: прогресс с подписью, «Забрать» (золотая рамка) или «Награда получена».
    /// Одна на главной, в альбоме и в пряже.
    /// </summary>
    public static class RewardStrip
    {
        public static void Build(RectTransform parent, float x, float y, float w, string text, float fraction, bool canClaim, bool claimed,
            string readyText, System.Action claim)
        {
            var bar = Ui.Box(parent, "Reward", 16, canClaim ? Palette.CoinSoft : Palette.Surface).rectTransform;
            Ui.Place(bar, x, y, w, 48);
            Ui.Outline(bar, 16, canClaim ? Palette.Coin : Palette.Line, canClaim ? 2f : 1.5f);
            var iconBack = Ui.Image(bar, "IconBack", Art.Circle, claimed ? Palette.PrimarySoft : Palette.CoinSoft);
            Ui.PlaceCenter(iconBack.rectTransform, 26, 24, 32, 32);
            Ui.IconImage(bar, claimed ? Icon.Check : Icon.Gift, claimed ? Palette.Primary : Palette.CoinShadow, 26, 24, 18);
            if (canClaim)
            {
                Ui.Fit(Ui.Label(bar, readyText, 14, Palette.Ink, 50, 12, w - 160, 24, true, TextAnchor.MiddleLeft), 10);
                Ui.Button(bar, "Забрать", w - 100, 6, 92, 36, Palette.Primary, Palette.PrimaryShadow, Color.white, 15).OnClick(claim);
            }
            else if (claimed)
            {
                Ui.Label(bar, "Награда получена", 14, Palette.Ink2, 50, 0, w - 60, 48, true, TextAnchor.MiddleLeft);
            }
            else
            {
                Ui.Fit(Ui.Label(bar, text, 12, Palette.Ink2, 50, 6, w - 60, 18, false, TextAnchor.MiddleLeft), 9);
                Ui.ProgressBar(bar, 50, 30, w - 64, 7, fraction, Palette.Primary);
            }
        }
    }

    /// <summary>Награда за коллекцию альбома: полоска прогресса и «Забрать» с окном награды.</summary>
    public static class CollectionReward
    {
        public static void Build(App app, RectTransform parent, int index, float x, float y, float w, System.Action refresh)
        {
            var progress = app.Progress;
            var collection = Collections.All[index];
            var done = 0;
            for (var id = collection.FirstLevel; id <= collection.LastLevel; id++)
            {
                done += progress.IsCompleted(id) ? 1 : 0;
            }

            RewardStrip.Build(parent, x, y, w, $"Собери все {collection.Count}: монеты, слот и моток", (float)done / collection.Count,
                progress.CanClaimCollection(index), progress.IsCollectionClaimed(index), "Коллекция собрана!", () =>
                {
                    var granted = progress.ClaimCollection(index, app.Random);
                    app.Save();
                    if (granted != null)
                    {
                        app.ShowPopup(new RewardPopup(app, collection.Name, Icon.Album, granted.Value, refresh));
                    }
                });
        }
    }
}
