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
        /// <param name="art">Объёмная картинка вместо значка (медаль достижения, полочка коллекции, кубок).</param>
        public RewardPopup(App app, string title, Icon icon, Granted granted, Action? closed = null, Sprite? art = null)
            : base(app)
        {
            Closed = closed;

            // Тёплый свет за наградой: лучи медленно вращаются из-за карточки, мягкое свечение под значком.
            // Ярко, но спокойно: без вспышек на весь экран.
            const float badgeSize = 84f;
            var badgeCy = Cursor + badgeSize / 2f;
            var rays = Ui.Image(Card, "Rays", Art.Rays, Palette.WithAlpha(Palette.CoinSoft, 0f));
            Ui.PlaceCenter(rays.rectTransform, CardWidth / 2f, badgeCy, 520, 520);
            rays.rectTransform.SetAsFirstSibling();
            var raysRt = rays.rectTransform;
            Tweens.Run(raysRt, 40f, t => raysRt.localRotation = Quaternion.Euler(0, 0, -360f * t), null, null, 0f, true);
            Tweens.Run(rays, 0.6f, t => rays.color = Palette.WithAlpha(Palette.CoinSoft, 0.55f * t), Ease.OutCubic);
            var glow = Ui.Image(Card, "Glow", Art.Glow, Palette.WithAlpha(Palette.Coin, 0f));
            Ui.PlaceCenter(glow.rectTransform, CardWidth / 2f, badgeCy, 190, 190);
            Tweens.Run(glow, 0.5f, t => glow.color = Palette.WithAlpha(Palette.Coin, 0.4f * t), Ease.OutCubic);
            var glowRt = glow.rectTransform;
            Tweens.Run(glowRt, 2.4f, t => glowRt.localScale = Vector3.one * (1f + 0.08f * Ease.Bump(t)), null, null, 0.5f, true);

            RectTransform badge;
            if (art != null)
            {
                badge = Ui.PlaceCenter(Ui.Node(Card, "IconBadge"), CardWidth / 2f, Cursor + badgeSize / 2f, badgeSize, badgeSize);
                Ui.Stretch(Ui.Image(badge, "Art", art, Color.white).rectTransform);
                Space(badgeSize + 14f);
            }
            else
            {
                badge = AddIcon(icon, Palette.CoinShadow, Palette.CoinSoft, badgeSize);
            }

            Tweens.Scale(badge, 0.2f, 1f, 0.45f, Ease.OutBack);
            Tweens.Run(badge, 0.6f, t => badge.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * Mathf.PI * 5f) * 10f * (1f - t)), null, null, 0.35f);
            Tweens.Delay(badge, 0.2f, () => Fx.Sparkle(Card, Ui.WorldCenter(badge), Palette.Coin, 14, 70f));

            // Три звёздочки мерцают вокруг значка.
            var twinkles = new[] { (-56f, -30f, 16f), (58f, -18f, 12f), (46f, 34f, 10f) };
            for (var i = 0; i < twinkles.Length; i++)
            {
                var (dx, dy, size) = twinkles[i];
                var star = Ui.Image(Card, "Twinkle", Art.Icon(Icon.Star), Palette.Coin);
                Ui.PlaceCenter(star.rectTransform, CardWidth / 2f + dx, badgeCy + dy, size, size);
                var srt = star.rectTransform;
                srt.localScale = Vector3.zero;
                Tweens.Run(srt, 1.6f, t => srt.localScale = Vector3.one * Ease.Bump(t), null, null, 0.4f + i * 0.45f, true);
            }

            AddTitle(title, 24);

            var items = Items(app, granted);
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
                // Каждая плитка «выпрыгивает» с лёгким подскоком и искрами своего цвета.
                cell.localScale = Vector3.zero;
                var rest = cell.anchoredPosition;
                var sparkColor = i == 0 ? Palette.Coin : captionColor == Palette.Ink ? Palette.Primary : captionColor;
                Tweens.Delay(cell, 0.35f + i * 0.2f, () =>
                {
                    Tweens.Scale(cell, 0.3f, 1f, 0.35f, Ease.OutBack);
                    Tweens.Run(cell, 0.35f, t => cell.anchoredPosition = rest + new Vector2(0, 14f * Ease.Bump(t)));
                    Fx.Sparkle(Card, cell.TransformPoint(new Vector3(itemW / 2f, -31f, 0)), sparkColor, 8, 46f);
                    Sfx.Play(i == 0 ? Sound.Coin : Sound.Star, 0.55f);
                });
            }

            Space(112);
            var coinsFrom = items.Count > 0 && granted.Reward.Coins > 0 ? (RectTransform)Card.Find("Item") : null;
            AddPrimary(L.T("Забрать")).OnClick(Close);

            // Монеты летят из плитки в счётчик экрана при любом закрытии (кнопка, крестик, «назад»): награда «переезжает»
            // к игроку. Монеты уже начислены при получении награды, здесь только полёт, поэтому дважды они не прибавятся.
            var after = closed;
            Closed = () =>
            {
                if (coinsFrom != null && granted.Reward.Coins > 0)
                {
                    var from = coinsFrom.localScale.x > 0.5f ? coinsFrom.TransformPoint(new Vector3(46f, -30f, 0)) : Ui.WorldCenter(Card);
                    app.FlyCoins(from, granted.Reward.Coins);
                }

                after?.Invoke();
            };
            Finish(8);
            Sfx.Play(Sound.Win, 0.6f);
        }

        protected override bool CloseOnDim => false;

        public static List<(Action<RectTransform> build, string caption, Color color)> Items(App app, Granted granted)
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
                var caption = granted.NewYarn ? L.F("{0}\nновый моток!", yarn.Name)
                    : granted.YarnLevelUp ? L.F("{0}\nуровень {1}!", yarn.Name, app.Progress.YarnLevel(yarn.Id))
                    : granted.BecameGolden ? L.F("{0}\nзолотой!", yarn.Name)
                    : granted.GoldStep > 0 ? L.F("{0}\nзолото {1}/{2}", yarn.Name, granted.GoldStep, YarnCatalog.GoldenAfter)
                    : L.F("{0}\nповтор: +{1}", yarn.Name, granted.DuplicateCoins);
                items.Add((cell =>
                {
                    YarnView.Aura(cell, yarn.Rarity, 46, 30, 56);
                    YarnView.Create(cell, yarn, 46, 30, 56, true);
                }, caption, granted.NewYarn ? YarnView.RarityColor(yarn.Rarity) : Palette.Ink2));
            }

            return items;
        }
    }

    /// <summary>Подарок дня: неделя подарков, сегодняшний подсвечен; пропуск дня — пауза, а не сброс.</summary>
    public sealed class DailyGiftPopup : Popup
    {
        public DailyGiftPopup(App app, Action changed)
            : base(app)
        {
            AddIcon(Icon.Gift, Palette.Danger, Palette.DangerSoft, 72);
            Ui.InfoButton(Card, CardWidth - 30, 30, L.T("Как работает подарок"),
                L.T("Подарки идут по кругу 7 дней и растут. Пропустишь день, не страшно: неделя продолжится с того же места. ") +
                L.T("На 7-й день: 150 монет и моток пряжи."), 26);
            AddTitle(L.T("Подарок дня"), 24);

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

                Ui.Label(cell, isToday ? L.T("Сегодня") : L.F("День {0}", day + 1), 12, isToday ? Palette.Primary : claimed ? Palette.Ink3 : Palette.Ink2, 0, 4, cellW, 18);
                var reward = DailyGift.Days[day];
                if (reward.Yarn == DailyGift.RandomYarn)
                {
                    Ui.IconImage(cell, Icon.Gift, Palette.Danger, cellW / 2f, 40, 30);
                    Ui.Fit(Ui.Label(cell, L.F("+{0} и моток", reward.Coins), 11, Palette.Ink, 0, 56, cellW, 18), 8);
                }
                else if (reward.Booster != null)
                {
                    Ui.IconImage(cell, BoosterInfo.Icon(reward.Booster.Value), Palette.Primary, cellW / 2f, 40, 26);
                    Ui.Fit(Ui.Label(cell, BoosterInfo.Name(reward.Booster.Value), 11, Palette.Ink, 0, 56, cellW, 18), 8);
                }
                else
                {
                    Ui.Coin(cell, cellW / 2f, 40, 28);
                    Ui.Fit(Ui.Label(cell, "+" + reward.Coins, 13, Palette.Ink, 0, 56, cellW, 18), 8);
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
                AddPrimary(L.T("Забрать")).OnClick(() =>
                {
                    var granted = app.Progress.ClaimDaily(today, app.Random);
                    Analytics.Event("daily_gift", ("day", index + 1), ("level", app.Progress.CurrentLevel));
                    app.Save();
                    Close();
                    if (granted != null)
                    {
                        app.ShowPopup(new RewardPopup(app, L.F("День {0}", index + 1), Icon.Gift, granted.Value, changed));
                    }
                });
            }
            else
            {
                AddLimit(() => L.F("Следующий подарок через {0}", Limits.ResetIn(Limits.NextMidnight())));
                AddPrimary(L.T("Понятно")).OnClick(Close);
            }

            Finish(8);
        }
    }
}

namespace KnittedPicture.Game
{
    /// <summary>
    /// Карточка пройденного уровня. Сверху — картинка, название и звёзды; ниже — особенности уровня
    /// плашками и понятный разбор: за что даётся каждая звезда, что уже получено, твой рекорд.
    /// Кнопка «Перевязать» не даёт случайно запустить старый уровень одним тапом.
    /// </summary>
    public sealed class LevelInfoPopup : Popup
    {
        public LevelInfoPopup(App app, KnittedPicture.Core.LevelData level)
            : base(app)
        {
            var progress = app.Progress;
            var stars = progress.StarsOf(level.id);
            var collection = KnittedPicture.Core.Collections.Of(level.id);

            // Шапка: картинка слева, справа название, номер и звёзды.
            const float pic = 108f;
            var top = Cursor - 8f;
            var frame = Ui.Card(Card, "Frame", 24, top, pic, pic, 20, Palette.Surface, Palette.CardShadow, 3f);
            MiniPicture.Create(frame, level, pic / 2f, pic / 2f, pic - 18f);
            var textX = 24 + pic + 16;
            var textW = CardWidth - textX - 20;
            // Длинные названия («Мордочка котика») ужимаются в свою строку и не наезжают на подпись.
            Ui.Fit(Ui.Label(Card, level.Title, 21, Palette.Ink, textX, top + 6, textW, 28, true, TextAnchor.MiddleLeft), 14);
            Ui.Fit(Ui.Label(Card, L.F("Уровень {0} · {1}", level.id, collection.Name), 13, Palette.Ink2, textX, top + 36, textW, 20, false,
                TextAnchor.MiddleLeft), 10);
            for (var i = 0; i < 3; i++)
            {
                var star = Ui.Image(Card, "Star", Art.Icon(Icon.Star), i < stars ? Palette.Coin : Palette.Track);
                Ui.PlaceCenter(star.rectTransform, textX + 14 + i * 32, top + 80, 28, 28);
            }

            Space(pic + 6f);

            // Особенности уровня — чтобы было понятно, что ждёт.
            var chips = new List<(string text, Color back, Color ink)>();
            if (level.hard)
            {
                chips.Add((L.T("Сложный"), Palette.DangerSoft, Palette.Danger));
            }

            if (level.timeLimit > 0)
            {
                chips.Add((L.F("Моль {0} с", level.timeLimit), Palette.Surface, Palette.Ink));
            }

            if (level.mystery)
            {
                chips.Add((L.T("Загадка"), Palette.Hex("#E1EAF8"), Palette.Link));
            }

            if (level.gold.Length > 0)
            {
                chips.Add((L.F("Золото ×{0}", level.gold.Length), Palette.CoinSoft, Palette.CoinShadow));
            }

            var chipX = 24f;
            foreach (var (text, back, ink) in chips)
            {
                // Ширина — по настоящей ширине текста: плашка не обрезает и не переносит подпись.
                var chip = Ui.Box(Card, "Chip", 12, back).rectTransform;
                var label = Ui.Label(chip, text, 12, ink);
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                var w = Mathf.Max(text.Length * 7.2f, label.preferredWidth) + 20f;
                if (chipX + w > CardWidth - 24f)
                {
                    UnityEngine.Object.Destroy(chip.gameObject);
                    break;
                }

                Ui.Place(chip, chipX, Cursor, w, 26);
                Ui.Stretch(label.rectTransform);
                chipX += w + 6f;
            }

            Space(38f);

            // Разбор звёзд: каждая строка — условие и отметка, получено ли.
            Ui.Label(Card, L.T("Как получить звёзды"), 14, Palette.Ink, 24, Cursor, Inner - 40, 20, true, TextAnchor.MiddleLeft);
            Ui.InfoButton(Card, 24 + Inner - 13, Cursor + 10, L.T("Повтор уровня"),
                L.F("Повтор даёт +{0} {1}. Сохраняется лучший результат по звёздам. ", KnittedPicture.Core.Economy.ReplayReward, YarnCatalog.CoinsWord(KnittedPicture.Core.Economy.ReplayReward)) +
                L.T("Три звезды: пройди уровень без бустеров и без спасения."), 24);
            Space(28f);
            var third = L.T("Без бустеров");
            StarRow(1, L.T("Довязать картинку"), stars >= 1);
            StarRow(2, L.T("Без спасения после затора или моли"), stars >= 2);
            StarRow(3, third, stars >= 3);

            Space(6f);

            AddPrimary(stars >= 3 ? L.T("Перевязать") : L.T("Перевязать на три звезды")).OnClick(() => app.StartLevel(level.id));
            Finish(6);
        }

        /// <summary>Строка разбора: N звёзд, условие, зелёная галочка или пустой кружок.</summary>
        private void StarRow(int count, string text, bool done)
        {
            var row = Ui.Box(Card, "StarRow", 14, done ? Palette.PrimarySoft : Palette.Surface).rectTransform;
            Ui.Place(row, 24, Cursor, Inner, 36);
            for (var i = 0; i < count; i++)
            {
                Ui.IconImage(row, Icon.Star, done ? Palette.Coin : Palette.Ink3, 16 + i * 13, 18, 14);
            }

            var label = Ui.Fit(Ui.Label(row, text, 13, done ? Palette.Ink : Palette.Ink2, 58, 0, Inner - 58 - 44, 36, done, TextAnchor.MiddleLeft), 10);
            label.lineSpacing = 0.85f;
            var mark = Ui.Image(row, "Mark", done ? Art.Circle : Art.Ring, done ? Palette.Primary : Palette.Inactive);
            Ui.PlaceCenter(mark.rectTransform, Inner - 22, 18, 22, 22);
            if (done)
            {
                Ui.IconImage(row, Icon.Check, Color.white, Inner - 22, 18, 14);
            }

            Space(42f);
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
                Sfx.Play(Sound.Deny, 0.5f);
                Haptics.Play(Haptic.Soft);
                app.Toast(L.F("Уровень {0} пока закрыт. Сейчас ты на уровне {1}", levelId, progress.CurrentLevel));
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

namespace KnittedPicture.Game
{
    /// <summary>Короткая справка по кнопке «i»: значок, заголовок, пара строк и «Понятно». Закрывается и тапом мимо.</summary>
    public sealed class InfoPopup : Popup
    {
        /// <summary>offerSupport: под «Понятно» ссылка «Написать нам», если в сборке задан адрес поддержки (как в «Об игре»).</summary>
        public InfoPopup(App app, string title, string text, bool offerSupport = false)
            : base(app)
        {
            AddIcon(Icon.Info, Palette.Link, Palette.Hex("#E1EAF8"), 52);
            AddTitle(title, 19);
            AddText(text, 14, 1);
            AddPrimary(L.T("Понятно"), 50).OnClick(Close);
            var mail = offerSupport ? StoreLinks.Support(app) : null;
            if (mail != null)
            {
                AddLink(L.T("Написать нам")).OnClick(() => Application.OpenURL(mail));
            }

            Finish(4);
        }
    }
}
