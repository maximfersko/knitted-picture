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
            Ui.Label(Root, "Коллекции", 24, Palette.Ink, 70, 8, W - 140, 44);
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

        private float BuildAlbum(RectTransform content)
        {
            var progress = App.Progress;
            var y = 8f;
            const float gap = 10f;
            for (var c = 0; c < Collections.All.Length; c++)
            {
                var collection = Collections.All[c];
                var tile = Mathf.Floor((W - 32f - gap * (collection.Count - 1)) / collection.Count);
                var done = 0;
                for (var id = collection.FirstLevel; id <= collection.LastLevel; id++)
                {
                    done += progress.IsCompleted(id) ? 1 : 0;
                }

                Ui.Label(content, collection.Name, 18, Palette.Ink, 18, y, W - 140, 26, true, TextAnchor.MiddleLeft);
                Ui.Label(content, $"{done} из {collection.Count}", 15, Palette.Ink2, W - 118, y, 100, 26, true, TextAnchor.MiddleRight);
                y += 34f;

                for (var i = 0; i < collection.Count; i++)
                {
                    var id = collection.FirstLevel + i;
                    if (!App.Levels.Has(id))
                    {
                        continue;
                    }

                    var level = App.Levels.Get(id);
                    var state = progress.IsCompleted(id) ? TileState.Done : progress.IsAvailable(id) ? TileState.Current : TileState.Locked;
                    var x = 16 + i * (tile + gap);
                    var levelId = id;
                    MiniPicture.Tile(content, level, state, x, y, tile, progress.StarsOf(id)).OnClick(() => App.StartLevel(levelId));
                    var name = Ui.Label(content, state == TileState.Locked ? "???" : level.name, 11, Palette.Ink2, x - 4, y + tile + 7, tile + 8, 28,
                        false, TextAnchor.UpperCenter);
                    name.lineSpacing = 0.85f;
                }

                y += tile + 40f;
                var index = c;
                y = RewardBar(content, y, "Награда за коллекцию: 100 монет, слот и моток",
                    progress.CanClaimCollection(c), progress.IsCollectionClaimed(c), (float)done / collection.Count,
                    () => Claim(progress.ClaimCollection(index, App.Random), collection.Name, Icon.Album, CollectionsTab.Album));
                y += 18f;
            }

            return y;
        }

        // ---------- Пряжа ----------

        private float BuildYarn(RectTransform content)
        {
            var progress = App.Progress;
            var y = 4f;
            var intro = Ui.Label(content, $"Мотков: {progress.YarnKinds} из {YarnCatalog.Count}. Выпадают за новые картинки, из сундука и подарков. Повтор — монеты",
                14, Palette.Ink2, 20, y, W - 40, 40, false);
            intro.lineSpacing = 0.9f;
            y += 50f;

            foreach (var set in YarnCatalog.Sets)
            {
                var owned = 0;
                for (var i = set.First; i < set.First + set.Count; i++)
                {
                    owned += progress.HasYarn(i) ? 1 : 0;
                }

                Ui.Label(content, set.Name, 18, Palette.Ink, 18, y, W - 140, 26, true, TextAnchor.MiddleLeft);
                Ui.Label(content, $"{owned} из {set.Count}", 15, Palette.Ink2, W - 118, y, 100, 26, true, TextAnchor.MiddleRight);
                y += 34f;

                const float gap = 8f;
                var cellW = (W - 32f - gap * (set.Count - 1)) / set.Count;
                const float cellH = 124f;
                for (var i = 0; i < set.Count; i++)
                {
                    var yarn = YarnCatalog.All[set.First + i];
                    var has = progress.HasYarn(yarn.Id);
                    var cell = Ui.Card(content, "Yarn", 16 + i * (cellW + gap), y, cellW, cellH, 16, has ? Palette.Card : Palette.Track,
                        Palette.CardShadow, has ? 2f : 0f);
                    YarnView.Create(cell, yarn, cellW / 2f, 38, 56, has);
                    var name = Ui.Label(cell, has ? yarn.Name : "???", 13, has ? Palette.Ink : Palette.Ink3, 2, 72, cellW - 4, 20);
                    name.lineSpacing = 0.85f;
                    Ui.Label(cell, YarnCatalog.RarityName(yarn.Rarity), 11, YarnView.RarityColor(yarn.Rarity), 2, 92, cellW - 4, 16, false);
                    var count = progress.YarnCount(yarn.Id);
                    if (count > 1)
                    {
                        var chip = Ui.Card(cell, "Count", cellW - 34, 6, 28, 20, 10, Palette.Ink, Palette.Ink, 0f);
                        Ui.Label(chip, "×" + count, 11, Color.white, 0, 0, 28, 20);
                    }

                    if (has)
                    {
                        var info = yarn;
                        cell.gameObject.AddComponent<Tap>().OnClick(() => App.Toast($"{info.Name} — {info.Description}"));
                    }
                }

                y += cellH + 12f;
                var target = set;
                var rewardText = $"За набор: {set.Reward.Coins} монет и {BoosterInfo.Name(set.Reward.Booster!.Value).ToLower()} ×{set.Reward.BoosterCount}";
                y = RewardBar(content, y, rewardText, progress.CanClaimSet(set), progress.IsSetClaimed(set), (float)owned / set.Count,
                    () => Claim(progress.ClaimSet(target, App.Random), "Набор «" + target.Name + "»", Icon.Gift, CollectionsTab.Yarn));
                y += 18f;
            }

            return y;
        }

        // ---------- Достижения ----------

        private float BuildAchievements(RectTransform content)
        {
            var progress = App.Progress;
            var y = 4f;
            const float rowH = 76f;

            // Сначала то, что можно забрать, потом незавершённые, в конце — полученные.
            var ordered = new System.Collections.Generic.List<AchievementInfo>(Achievements.All);
            ordered.Sort((a, b) => Rank(a).CompareTo(Rank(b)) != 0 ? Rank(a).CompareTo(Rank(b)) : a.Id.CompareTo(b.Id));
            foreach (var a in ordered)
            {
                var claimed = progress.IsAchievementClaimed(a);
                var can = progress.CanClaimAchievement(a);
                var row = Ui.Card(content, "Achievement", 16, y, W - 32, rowH - 8, 18, claimed ? Palette.Track : Palette.Card, Palette.CardShadow,
                    claimed ? 0f : 2f);
                var icon = Ui.Image(row, "Icon", Art.Circle, can ? Palette.CoinSoft : claimed ? Palette.PrimarySoft : Palette.Track);
                Ui.PlaceCenter(icon.rectTransform, 34, (rowH - 8) / 2f, 44, 44);
                Ui.IconImage(row, claimed ? Icon.Check : Icon.Trophy, can ? Palette.CoinShadow : claimed ? Palette.Primary : Palette.Ink3, 34, (rowH - 8) / 2f, 24);
                Ui.Label(row, a.Title, 15, Palette.Ink, 64, 8, W - 32 - 64 - 110, 20, true, TextAnchor.MiddleLeft);
                Ui.Label(row, a.Description, 12, Palette.Ink2, 64, 28, W - 32 - 64 - 110, 18, false, TextAnchor.MiddleLeft);
                var value = progress.AchievementValue(a);
                Ui.ProgressBar(row, 64, 52, W - 32 - 64 - 120, 6, (float)value / a.Target, can || claimed ? Palette.Primary : Palette.Link);

                var right = W - 32 - 100;
                if (can)
                {
                    var target = a;
                    Ui.Button(row, "Забрать", right, 14, 88, 40, Palette.Primary, Palette.PrimaryShadow, Color.white, 15)
                        .OnClick(() => Claim(progress.ClaimAchievement(target, App.Random), target.Title, Icon.Trophy, CollectionsTab.Achievements));
                }
                else if (!claimed)
                {
                    Ui.Label(row, $"{value}/{a.Target}", 13, Palette.Ink2, right, 4, 88, 22, true, TextAnchor.MiddleRight);
                    RewardHint(row, a.Reward, right, 30);
                }

                y += rowH;
            }

            return y;

            int Rank(AchievementInfo a) => progress.CanClaimAchievement(a) ? 0 : progress.IsAchievementClaimed(a) ? 2 : 1;
        }

        private static void RewardHint(RectTransform parent, Reward reward, float x, float y)
        {
            var text = reward.Coins > 0 ? "+" + reward.Coins : "";
            Ui.Label(parent, text, 14, Palette.Ink, x, y, 60, 24, true, TextAnchor.MiddleRight);
            if (reward.Coins > 0)
            {
                Ui.Coin(parent, x + 72, y + 12, 18);
            }

            if (reward.Booster != null)
            {
                Ui.IconImage(parent, BoosterInfo.Icon(reward.Booster.Value), Palette.Primary, x + 72, y + 34, 16);
            }
        }

        // ---------- Общее ----------

        /// <summary>Полоска награды под коллекцией: прогресс, «Забрать» или «Получено».</summary>
        private float RewardBar(RectTransform content, float y, string text, bool canClaim, bool claimed, float fraction, System.Action claim)
        {
            var bar = Ui.Card(content, "RewardBar", 16, y, W - 32, 52, 18, canClaim ? Palette.CoinSoft : Palette.Card, Palette.CardShadow, 2f);
            Ui.IconImage(bar, claimed ? Icon.Check : Icon.Gift, claimed ? Palette.Primary : Palette.CoinShadow, 24, 26, 22);
            if (canClaim)
            {
                Ui.Label(bar, "Награда готова!", 15, Palette.Ink, 44, 0, W - 32 - 160, 52, true, TextAnchor.MiddleLeft);
                Ui.Button(bar, "Забрать", W - 32 - 104, 8, 92, 36, Palette.Primary, Palette.PrimaryShadow, Color.white, 15).OnClick(claim);
            }
            else if (claimed)
            {
                Ui.Label(bar, "Награда получена", 14, Palette.Ink2, 44, 0, W - 32 - 60, 52, true, TextAnchor.MiddleLeft);
            }
            else
            {
                var label = Ui.Label(bar, text, 12, Palette.Ink2, 44, 6, W - 32 - 56, 30, false, TextAnchor.MiddleLeft);
                label.lineSpacing = 0.85f;
                Ui.ProgressBar(bar, 44, 38, W - 32 - 60, 6, fraction, Palette.Primary);
            }

            return y + 52f;
        }

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
}
