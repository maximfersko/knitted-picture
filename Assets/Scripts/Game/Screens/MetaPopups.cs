#nullable enable

using System;
using System.Collections.Generic;
using KnittedPicture.Core;
using UnityEngine;

namespace KnittedPicture.Game
{
    /// <summary>Что получил игрок: монеты, бустер, моток — каждое в своей плитке, по очереди.</summary>
    public sealed class RewardPopup : Popup
    {
        public RewardPopup(App app, string title, Icon icon, Granted granted, Action? closed = null)
            : base(app)
        {
            var badge = AddIcon(icon, Palette.CoinShadow, Palette.CoinSoft, 84);
            Tweens.Run(badge, 0.5f, t => badge.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * Mathf.PI * 6f) * 12f * (1f - t)));
            Fx.Sparkle(Card, Ui.WorldCenter(badge), Palette.Coin, 12, 60f);
            AddTitle(title, 24);

            var items = Items(granted);
            const float itemW = 92f;
            var x = CardWidth / 2f - items.Count * itemW / 2f;
            var y = Cursor;
            for (var i = 0; i < items.Count; i++)
            {
                var (build, caption, captionColor) = items[i];
                var cell = Ui.Place(Ui.Node(Card, "Item"), x + i * itemW, y, itemW, 104);
                var tile = Ui.Box(cell, "Tile", 16, Palette.Surface);
                Ui.Place(tile.rectTransform, 6, 0, itemW - 12, 62);
                Ui.Outline(tile.rectTransform, 16, Palette.Line, 1.5f);
                build(cell);
                var label = Ui.Label(cell, caption, 13, captionColor, 0, 66, itemW, 38, true, TextAnchor.UpperCenter);
                label.lineSpacing = 0.85f;
                cell.localScale = Vector3.zero;
                Tweens.Delay(cell, 0.15f + i * 0.18f, () =>
                {
                    Tweens.Scale(cell, 0.3f, 1f, 0.3f, Ease.OutBack);
                    Sfx.Play(Sound.Coin, 0.5f);
                });
            }

            Space(112);
            AddPrimary("Забрать").OnClick(() =>
            {
                Close();
                closed?.Invoke();
            });
            Finish(8);
            Sfx.Play(Sound.Win, 0.6f);
        }

        protected override bool CloseOnDim => false;

        public static List<(Action<RectTransform> build, string caption, Color color)> Items(Granted granted)
        {
            var reward = granted.Reward;
            var items = new List<(Action<RectTransform>, string, Color)>();
            if (reward.Coins > 0)
            {
                items.Add((cell => Ui.Coin(cell, 46, 30, 48), "+" + reward.Coins, Palette.Ink));
            }

            if (reward.Booster != null)
            {
                var type = reward.Booster.Value;
                items.Add((cell =>
                {
                    var circle = Ui.Image(cell, "Back", Art.Circle, Palette.PrimarySoft);
                    Ui.PlaceCenter(circle.rectTransform, 46, 30, 56, 56);
                    Ui.IconImage(cell, BoosterInfo.Icon(type), Palette.Primary, 46, 30, 30);
                }, $"{BoosterInfo.Name(type)} ×{reward.BoosterCount}", Palette.Ink));
            }

            if (reward.Yarn >= 0)
            {
                var yarn = YarnCatalog.All[reward.Yarn];
                var caption = granted.NewYarn ? $"{yarn.Name}\nновый моток!" : $"{yarn.Name}\nповтор: +{granted.DuplicateCoins}";
                items.Add((cell => YarnView.Create(cell, yarn, 46, 30, 56, true), caption, granted.NewYarn ? YarnView.RarityColor(yarn.Rarity) : Palette.Ink2));
            }

            return items;
        }
    }

    /// <summary>Подарок дня: неделя подарков, сегодняшний подсвечен, пропуск дня начинает неделю заново.</summary>
    public sealed class DailyGiftPopup : Popup
    {
        public DailyGiftPopup(App app, Action changed)
            : base(app)
        {
            AddIcon(Icon.Gift, Palette.Danger, Palette.DangerSoft, 72);
            AddTitle("Подарок дня", 24);
            AddText("Заходи каждый день — подарки растут. Пропустишь день — неделя начнётся заново", 14, 2.4f);

            var today = app.Today;
            var index = app.Progress.DailyIndex(today);
            var canClaim = app.Progress.CanClaimDaily(today);
            const float gap = 6f;
            var cellW = (Inner - gap * 3) / 4f;
            const float cellH = 78f;
            var y = Cursor;
            for (var day = 0; day < DailyGift.Days.Length; day++)
            {
                var row = day / 4;
                var col = day % 4;
                var rowCount = row == 0 ? 4 : 3;
                var rowX = 24 + (Inner - (rowCount * cellW + (rowCount - 1) * gap)) / 2f;
                var claimed = day < index || (!canClaim && day == index);
                var isToday = day == index && canClaim;
                // Сегодня — зелёная рамка, впереди — плитки на подложке, забранные — приглушены.
                var face = isToday ? Palette.PrimarySoft : claimed ? Palette.Track : Palette.Surface;
                var cell = Ui.Card(Card, "Day" + day, rowX + col * (cellW + gap), y + row * (cellH + gap), cellW, cellH, 14, face,
                    isToday ? Palette.PrimaryShadow : Palette.CardShadow, isToday ? 3f : 2f);
                if (isToday)
                {
                    Ui.Outline(cell, 14, Palette.Primary, 2.5f);
                }

                Ui.Label(cell, isToday ? "Сегодня" : $"День {day + 1}", 12, isToday ? Palette.Primary : claimed ? Palette.Ink3 : Palette.Ink2, 0, 4, cellW, 18);
                var reward = DailyGift.Days[day];
                if (reward.Yarn == DailyGift.RandomYarn)
                {
                    Ui.IconImage(cell, Icon.Gift, Palette.Danger, cellW / 2f, 40, 30);
                    Ui.Label(cell, $"+{reward.Coins} и моток", 11, Palette.Ink, 0, 56, cellW, 18);
                }
                else if (reward.Booster != null)
                {
                    Ui.IconImage(cell, BoosterInfo.Icon(reward.Booster.Value), Palette.Primary, cellW / 2f, 40, 26);
                    Ui.Label(cell, BoosterInfo.Name(reward.Booster.Value), 11, Palette.Ink, 0, 56, cellW, 18);
                }
                else
                {
                    Ui.Coin(cell, cellW / 2f, 40, 28);
                    Ui.Label(cell, "+" + reward.Coins, 13, Palette.Ink, 0, 56, cellW, 18);
                }

                if (claimed)
                {
                    var veil = Ui.Box(cell, "Veil", 14, Palette.WithAlpha(Palette.Track, 0.55f));
                    Ui.Stretch(veil.rectTransform);
                    var check = Ui.Image(cell, "Done", Art.Circle, Palette.Primary);
                    Ui.PlaceCenter(check.rectTransform, cellW - 12, 12, 20, 20);
                    Ui.IconImage(cell, Icon.Check, Color.white, cellW - 12, 12, 14);
                }

                if (isToday)
                {
                    Tweens.Run(cell, 1.2f, t => cell.localScale = Vector3.one * (1f + 0.05f * Ease.Bump(t)), null, null, 0f, true);
                }
            }

            Space(2 * cellH + gap + 18f);
            if (canClaim)
            {
                AddPrimary("Забрать").OnClick(() =>
                {
                    var granted = app.Progress.ClaimDaily(today, app.Random);
                    app.Save();
                    Close();
                    if (granted != null)
                    {
                        app.ShowPopup(new RewardPopup(app, $"День {index + 1}", Icon.Gift, granted.Value, changed));
                    }
                });
            }
            else
            {
                AddText("Следующий подарок — завтра", 15, 1);
                AddPrimary("Понятно").OnClick(Close);
            }

            Finish(8);
        }
    }

    /// <summary>Звёздный сундук: копится звёздами за уровни.</summary>
    public static class StarChest
    {
        public static void Open(App app, Action changed)
        {
            if (!app.Progress.CanOpenChest)
            {
                var left = ProgressService.ChestEvery - app.Progress.ChestProgress;
                app.Toast($"До сундука ещё {left} ★ — проходи уровни на три звезды");
                return;
            }

            var granted = app.Progress.OpenChest(app.Random);
            app.Save();
            if (granted != null)
            {
                app.ShowPopup(new RewardPopup(app, "Звёздный сундук", Icon.Chest, granted.Value, changed));
            }
        }
    }
}

namespace KnittedPicture.Game
{
    /// <summary>
    /// Карточка пройденного уровня: картинка, звёзды, чего не хватает до трёх и «Перевязать».
    /// Не даёт случайно запустить старый уровень одним тапом.
    /// </summary>
    public sealed class LevelInfoPopup : Popup
    {
        public LevelInfoPopup(App app, KnittedPicture.Core.LevelData level)
            : base(app)
        {
            const float size = 150f;
            var frame = Ui.Card(Card, "Frame", CardWidth / 2f - size / 2f, Cursor, size, size, 24, Palette.Surface, Palette.CardShadow, 3f);
            MiniPicture.Create(frame, level, size / 2f, size / 2f, size - 24f);
            Space(size + 18f);

            AddTitle(level.name, 24);
            var collection = KnittedPicture.Core.Collections.Of(level.id);
            var sub = AddText($"Уровень {level.id} · {collection.Name}", 14, 1);
            sub.color = Palette.Ink2;

            var stars = app.Progress.StarsOf(level.id);
            var y = Cursor - 6f;
            for (var i = 0; i < 3; i++)
            {
                var star = Ui.Image(Card, "Star", Art.Icon(Icon.Star), i < stars ? Palette.Coin : Palette.Track);
                Ui.PlaceCenter(star.rectTransform, CardWidth / 2f + (i - 1) * 40f, y + 16f, i == 1 ? 38f : 32f, i == 1 ? 38f : 32f);
            }

            Space(40f);
            var hint = stars >= 3
                ? "Три звезды — лучший результат"
                : level.par > 0
                    ? $"Третья звезда — без бустеров и за {level.par} тапов"
                    : "Третья звезда — без бустеров и спасений";
            AddText(hint, 14, 1.2f);
            AddPrimary(stars >= 3 ? "Перевязать" : "Перевязать на три звезды").OnClick(() => app.StartLevel(level.id));
            AddLink("Закрыть").OnClick(Close);
            Finish(8);
        }

        /// <summary>Тап по плитке уровня где угодно: закрытый — подсказка, текущий — сразу в игру, пройденный — карточка.</summary>
        public static void OnTile(App app, int levelId, RectTransform tile)
        {
            var progress = app.Progress;
            if (!app.Levels.Has(levelId))
            {
                return;
            }

            if (!progress.IsAvailable(levelId))
            {
                Tweens.Shake(tile, 6f, 0.3f);
                app.Toast($"Уровень {levelId} пока закрыт — сейчас ты на уровне {progress.CurrentLevel}");
                return;
            }

            if (!progress.IsCompleted(levelId))
            {
                app.StartLevel(levelId);
                return;
            }

            app.ShowPopup(new LevelInfoPopup(app, app.Levels.Get(levelId)));
        }
    }
}
