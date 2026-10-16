#nullable enable

using KnittedPicture.Core;
using UnityEngine;

namespace KnittedPicture.Game
{
    public sealed partial class CollectionsScreen
    {
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
                (L.T("Можно забрать"), 0),
                (L.T("В процессе"), 1),
                (L.T("Получено"), 2),
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
                    // Строки ниже первого экрана — в следующих кадрах; высота строки известна заранее.
                    var rowY = y;
                    var item = a;
                    if (rowY > H)
                    {
                        Defer(() => AchievementRow(content, item, rowY));
                        y = rowY + AchievementRowH + 10f;
                    }
                    else
                    {
                        y = AchievementRow(content, item, rowY);
                    }
                }

                y += 10f;
            }

            return y;

            int Rank(AchievementInfo a) => progress.CanClaimAchievement(a) ? 0 : progress.IsAchievementClaimed(a) ? 2 : 1;
        }

        private const float AchievementRowH = 72f;

        private float AchievementRow(RectTransform content, AchievementInfo a, float y)
        {
            const float rowH = AchievementRowH;
            const float rightW = 112f;
            var progress = App.Progress;
            var claimed = progress.IsAchievementClaimed(a);
            var can = progress.CanClaimAchievement(a);
            var w = W - 32f;
            var row = Ui.Card(content, "Achievement", 16, y, w, rowH, 18, can ? Palette.CoinSoft : Palette.Card, Palette.CardShadow, 3f);
            if (can)
            {
                Ui.Outline(row, 18, Palette.Coin, 2f);
            }

            // Диск медали в растре выше середины (снизу — ленты): кольцо, сияние и камень ставятся по центру диска,
            // а не по центру картинки, иначе кольцо «съезжает» вниз относительно медали.
            const float medal = 56f;
            var discY = rowH / 2f - medal * 4f / 64f;
            var discR = medal * 25f / 64f;

            // Медаль со своим рисунком: золотая, когда можно забрать (на мягком сиянии, с искрами) и когда получена
            // (с зелёной галочкой), серебряная и чуть бледнее — пока в процессе.
            if (can)
            {
                var glow = Ui.Image(row, "Glow", Art.Glow, Palette.WithAlpha(Palette.Coin, 0.5f));
                var glowRt = glow.rectTransform;
                Ui.PlaceCenter(glowRt, 36, discY, 76, 76);
                Tweens.Run(glowRt, 2.2f, t => glowRt.localScale = Vector3.one * (0.9f + 0.14f * Ease.Bump(t)), null, null, 0f, true);
            }

            // Кольцо цвета редкости за медалью и камень редкости у её края: видно, насколько веха ценная.
            if (a.Rarity != Rarity.Common)
            {
                var halo = Ui.Image(row, "RarityRing", Art.Ring, Palette.WithAlpha(RarityStyle.Color(a.Rarity), claimed || can ? 0.6f : 0.35f));
                Ui.PlaceCenter(halo.rectTransform, 36, discY, discR * 2f + 10f, discR * 2f + 10f);
            }

            var cup = Ui.Image(row, "Trophy", Art2.Medal(Art2.BadgeOf(a), can || claimed), Palette.WithAlpha(Color.white, can || claimed ? 1f : 0.85f));
            var cupRt = cup.rectTransform;
            Ui.PlaceCenter(cupRt, 36, rowH / 2f, medal, medal);
            var gem = Ui.Image(row, "RarityGem", Art2.Gem(a.Rarity), Color.white);
            // Камень — на кольце слева сверху: не закрывает ленты и не прилипает к краю карточки.
            var ringR = (discR + 5f) * 0.93f;
            Ui.PlaceCenter(gem.rectTransform, 36f - ringR * 0.7071f, discY - ringR * 0.7071f, 18, 18);
            if (can)
            {
                // Кубок, который ждёт, раз в пару секунд подмигивает искрой и чуть подпрыгивает.
                var spark = Ui.Image(row, "Spark", Art2.Sparkle, Palette.Hex("#FFF3B0"));
                var sparkRt = spark.rectTransform;
                Ui.PlaceCenter(sparkRt, 46, rowH / 2f - 12f, 16, 16);
                Tweens.Run(sparkRt, 2.4f, t =>
                {
                    var flash = t < 0.3f ? Ease.Bump(t / 0.3f) : 0f;
                    sparkRt.localScale = Vector3.one * (0.1f + flash);
                    sparkRt.localRotation = Quaternion.Euler(0f, 0f, 120f * t);
                }, null, null, 0.4f, true);
                Tweens.Run(cupRt, 2.4f, t => cupRt.localScale = Vector3.one * (1f + 0.08f * (t > 0.5f && t < 0.75f ? Ease.Bump((t - 0.5f) / 0.25f) : 0f)),
                    null, null, 0f, true);
            }
            else if (claimed)
            {
                var tick = Ui.Image(row, "Done", Art.Circle, Palette.Primary);
                Ui.PlaceCenter(tick.rectTransform, 52, rowH / 2f + 14f, 18, 18);
                Ui.IconImage(row, Icon.Check, Color.white, 52, rowH / 2f + 14f, 11);
            }

            // Готовое к получению: полоска уже полная и не нужна, кнопка компактнее и стоит с отступом от текста.
            const float claimW = 100f;
            var textW = can ? w - 68f - claimW - 28f : w - 68f - rightW - 26f;
            var hasBar = !claimed && !can;
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
                var claimButton = Ui.Button(row, L.T("Забрать"), w - claimW - 14f, 17, claimW, 36, Palette.Primary, Palette.PrimaryShadow, Color.white, 15);
                claimButton.OnClick(() => Claim(progress.ClaimAchievement(target, App.Random), target.Title, Icon.Trophy, CollectionsTab.Achievements,
                    Art2.Medal(Art2.BadgeOf(target), true)));
                Ui.Sheen((RectTransform)claimButton.transform, claimW, 36f, 3.2f, 0.6f + y * 0.002f);
            }
            else if (claimed)
            {
                var chip = Ui.Card(row, "Done", right + 4, 22, rightW - 4, 28, 14, Palette.PrimarySoft, Palette.PrimarySoft, 0f);
                Ui.IconImage(chip, Icon.Check, Palette.Primary, 16, 14, 14);
                Ui.Label(chip, L.T("Получено"), 12, Palette.Primary, 26, 0, rightW - 32, 28, true, TextAnchor.MiddleLeft);
            }
            else
            {
                Ui.Label(row, $"{value}/{a.Target}", 13, Palette.Ink2, right, 8, rightW, 20, true, TextAnchor.MiddleRight);
                RewardInline(row, a.Reward, right + rightW, 42);
            }

            return y + rowH + 10f;
        }

        /// <summary>
        /// Награда в строке достижения — колонками: плашка монет одной ширины всегда на одном месте,
        /// справа от неё — место под значок бустера (пустое, если бустера нет). Так монеты стоят ровно в ряд.
        /// </summary>
        private static void RewardInline(RectTransform parent, Reward reward, float rightEdge, float cy)
        {
            const float boosterColumn = 32f;
            const float chipW = 78f;
            if (reward.Booster != null)
            {
                var badge = Ui.Image(parent, "Booster", Art.Circle, Palette.PrimarySoft);
                Ui.PlaceCenter(badge.rectTransform, rightEdge - 12, cy, 28, 28);
                var art = Ui.Image(parent, "BoosterArt", Art2.Booster(reward.Booster.Value), Color.white);
                Ui.PlaceCenter(art.rectTransform, rightEdge - 12, cy, 24, 24);
            }

            if (reward.Coins > 0)
            {
                var chip = Ui.Box(parent, "Coins", 12, Palette.CoinSoft).rectTransform;
                Ui.PlaceCenter(chip, rightEdge - boosterColumn - chipW / 2f, cy, chipW, 24);
                Ui.Label(chip, "+" + reward.Coins, 14, Palette.Ink, 6, 0, chipW - 36, 24, true, TextAnchor.MiddleRight);
                Ui.Coin(chip, chipW - 16, 12, 17);
            }
        }
    }
}
