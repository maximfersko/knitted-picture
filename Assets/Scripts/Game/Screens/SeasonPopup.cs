#nullable enable

using System;
using KnittedPicture.Core;
using UnityEngine;

namespace KnittedPicture.Game
{
    /// <summary>
    /// «Вязальный сезон»: название времени года, сколько дней осталось, полоска очков до следующей ступени
    /// и дорожка из 30 ступеней (прокручивается к текущей). У ступени — плашка цвета редкости, награда картинкой
    /// и состояние: взята, можно забрать, впереди. Финал — сезонный моток и сезонная рамка крупно в шапке.
    /// </summary>
    public sealed class SeasonPopup : Popup
    {
        private const float RowH = 58f;

        public SeasonPopup(App app, Action changed)
            : base(app, bottomSheet: true)
        {
            var progress = app.Progress;
            var today = app.Today;
            var season = Season.SeasonOf(today);
            var theme = Season.ThemeOf(season);
            var points = progress.SeasonPoints(today);
            var claimed = progress.SeasonClaimed(today);
            var reached = progress.SeasonReached(today);
            Closed = changed;

            // Шапка: финальные награды сезона — рамка и моток — крупно, чтобы было ради чего играть.
            const float art = 76f;
            var frame = AlbumFrames.ForSeason(theme);
            var frameRoot = Ui.Place(Ui.Node(Card, "SeasonFrame"), CardWidth / 2f - art - 10f, Cursor, art, art);
            MiniPicture.Framed(frameRoot, frame.Id, art, app.Levels.Get(1));
            var yarn = YarnCatalog.All[Season.YarnOf(theme)];
            YarnView.Aura(Card, yarn.Rarity, CardWidth / 2f + art / 2f + 10f, Cursor + art / 2f, art * 0.8f);
            YarnView.Create(Card, yarn, CardWidth / 2f + art / 2f + 10f, Cursor + art / 2f, art * 0.8f, true);
            Space(art + 10f);
            AddTitle(Season.Name(theme), 22);
            var days = Season.DaysLeft(today);
            AddText(L.F("Финал: рамка «{0}» и моток «{1}». До конца сезона {2} {3}", frame.Name, yarn.Name, days,
                L.Plural(days, "день", "дня", "дней")), 13, 2);

            // Очки до следующей ступени.
            var inStep = reached >= Season.Steps ? Season.StepPoints : points - reached * Season.StepPoints;
            Ui.Label(Card, reached >= Season.Steps ? L.T("Все ступени пройдены") : L.F("Ступень {0} из {1}", reached + 1, Season.Steps), 14,
                Palette.Ink, 24, Cursor, Inner - 90, 20, true, TextAnchor.MiddleLeft);
            Ui.Label(Card, $"{inStep}/{Season.StepPoints}", 13, Palette.Ink2, 24 + Inner - 90, Cursor, 90, 20, true, TextAnchor.MiddleRight);
            Space(24f);
            Ui.ProgressBar(Card, 24, Cursor, Inner, 10, (float)inStep / Season.StepPoints, Palette.Coin);
            Space(18f);
            Ui.Fit(Ui.Label(Card, L.T("Очки: звёзды за картинки, узор дня, задания, выставка"), 12, Palette.Ink3, 24, Cursor, Inner, 18, false), 9);
            Space(26f);

            // Дорожка: прокручивается, открывается на текущей ступени.
            var viewH = Mathf.Min(RowH * 5.5f, app.Height * 0.38f);
            var content = Ui.Scroll(Card, 0, Cursor, CardWidth, viewH);
            content.sizeDelta = new Vector2(CardWidth, Season.Steps * RowH + 8f);
            for (var n = 1; n <= Season.Steps; n++)
            {
                StepRow(content, Season.Step(n, theme), n <= claimed, n <= reached && n > claimed, (n - 1) * RowH + 4f, frame, yarn);
            }

            var focus = Mathf.Clamp(claimed - 1, 0, Season.Steps - 5);
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, focus * RowH);
            Space(viewH + 14f);

            var waiting = progress.SeasonWaiting(today);
            if (waiting > 0)
            {
                var claim = AddPrimary(waiting > 1 ? L.F("Забрать ({0})", waiting) : L.T("Забрать"));
                Ui.Sheen((RectTransform)claim.transform, Inner, 60f, 3.2f, 0.5f);
                claim.OnClick(() => Claim(app, changed));
            }
            else
            {
                AddPrimary(L.T("Играть")).OnClick(() =>
                {
                    Close();
                    app.StartLevel(progress.CurrentLevel);
                });
            }

            Finish(8);
        }

        private void StepRow(RectTransform content, SeasonStep step, bool done, bool ready, float y, FrameInfo frame, YarnInfo yarn)
        {
            var w = CardWidth - 48f;
            var row = Ui.Box(content, "Step" + step.Number, 14, ready ? RarityStyle.Soft(step.Rarity) : done ? Palette.PrimarySoft : Palette.Surface).rectTransform;
            Ui.Place(row, 24, y, w, RowH - 8f);
            Ui.Outline(row, 14, ready ? RarityStyle.Color(step.Rarity) : step.Rarity == Rarity.Common ? Palette.Line : Palette.WithAlpha(RarityStyle.Color(step.Rarity), 0.5f),
                ready ? 2f : 1.5f);
            var h = RowH - 8f;

            // Номер ступени на камне её редкости.
            var gem = Ui.Image(row, "Gem", Art2.Gem(step.Rarity), Color.white);
            Ui.PlaceCenter(gem.rectTransform, 24, h / 2f, 30, 30);
            Ui.Label(row, step.Number.ToString(), 11, Color.white, 9, h / 2f - 8f, 30, 16);

            // Награда: картинка и подпись.
            const float iconX = 64f;
            var reward = step.Reward;
            string text;
            switch (step.Prize)
            {
                case SeasonPrize.SeasonFrame:
                {
                    var root = Ui.Place(Ui.Node(row, "Frame"), iconX - 18f, h / 2f - 18f, 36, 36);
                    MiniPicture.Framed(root, frame.Id, 36, null);
                    text = L.F("Рамка «{0}»", frame.Name);
                    break;
                }

                case SeasonPrize.SeasonYarn:
                    YarnView.Create(row, yarn, iconX, h / 2f, 32, true);
                    text = L.F("Моток «{0}»", yarn.Name);
                    break;
                case SeasonPrize.Yarn:
                {
                    var ball = Ui.Image(row, "Yarn", Art2.YarnBall(RarityStyle.Color(Rarity.Rare)), Color.white);
                    Ui.PlaceCenter(ball.rectTransform, iconX, h / 2f, 30, 30);
                    text = L.F("Моток пряжи и {0} {1}", reward.Coins, YarnCatalog.CoinsWord(reward.Coins));
                    break;
                }

                case SeasonPrize.Booster:
                case SeasonPrize.Big:
                {
                    var art = Ui.Image(row, "Booster", Art2.Booster(reward.Booster!.Value), Color.white);
                    Ui.PlaceCenter(art.rectTransform, iconX, h / 2f, 32, 32);
                    text = reward.Coins > 0
                        ? L.F("{0} ×{1} и {2} {3}", BoosterInfo.Name(reward.Booster.Value), reward.BoosterCount, reward.Coins, YarnCatalog.CoinsWord(reward.Coins))
                        : $"{BoosterInfo.Name(reward.Booster.Value)} ×{reward.BoosterCount}";
                    break;
                }

                default:
                    Ui.Coin(row, iconX, h / 2f, 28);
                    text = $"{reward.Coins} {YarnCatalog.CoinsWord(reward.Coins)}";
                    break;
            }

            var labelW = w - iconX - 26f - 44f;
            var label = Ui.Label(row, text, 13, step.Rarity >= Rarity.Rare ? RarityStyle.Ink(step.Rarity) : Palette.Ink, iconX + 24f, 0, labelW, h, true, TextAnchor.MiddleLeft);
            label.lineSpacing = 0.9f;
            Ui.Fit(label, 9);

            if (done)
            {
                var mark = Ui.Image(row, "Done", Art.Circle, Palette.Primary);
                Ui.PlaceCenter(mark.rectTransform, w - 24f, h / 2f, 24, 24);
                Ui.IconImage(row, Icon.Check, Color.white, w - 24f, h / 2f, 14);
            }
            else if (ready)
            {
                Ui.Badge(row, w - 24f, h / 2f, 0);
            }
            else
            {
                Ui.IconImage(row, Icon.Lock, Palette.Ink3, w - 24f, h / 2f, 16);
            }
        }

        private void Claim(App app, Action changed)
        {
            var result = app.Progress.ClaimSeason(app.Today, app.Random);
            if (result == null)
            {
                return;
            }

            var claim = result.Value;
            Analytics.Event("season_claim", ("step", claim.Step.Number), ("rarity", claim.Step.Rarity.ToString()));
            app.Save();
            Closed = null;
            Close();

            // После награды — снова дорожка, если ещё есть что забрать.
            void Back()
            {
                changed();
                if (app.Progress.SeasonWaiting(app.Today) > 0)
                {
                    app.ShowPopup(new SeasonPopup(app, changed));
                }
            }

            var title = L.F("Ступень {0}", claim.Step.Number);
            if (claim.Frame >= 0)
            {
                Sfx.Play(Sound.Win, 0.6f);
                Haptics.Play(Haptic.Success);
                app.ShowPopup(new FrameRewardPopup(app, claim.Frame, Back));
                return;
            }

            app.ShowPopup(new RewardPopup(app, title, Icon.Star, claim.Granted, Back));
        }
    }

    /// <summary>Новая рамка: крупно, с сиянием редкости; «Выбрать» сразу ставит её в альбом.</summary>
    public sealed class FrameRewardPopup : Popup
    {
        public FrameRewardPopup(App app, int frameId, Action? closed)
            : base(app)
        {
            Closed = closed;
            var frame = AlbumFrames.Get(frameId);
            const float size = 120f;
            YarnView.Aura(Card, frame.Rarity, CardWidth / 2f, Cursor + size / 2f, size * 0.9f);
            var root = Ui.Place(Ui.Node(Card, "Frame"), CardWidth / 2f - size / 2f, Cursor, size, size);
            MiniPicture.Framed(root, frame.Id, size, app.Levels.Get(1));
            Tweens.Scale(root, 0.6f, 1f, 0.45f, Ease.OutBack, null, 0.05f);
            Space(size + 12f);
            AddTitle(L.T("Новая рамка!"), 24);
            AddText(frame.Name, 16, 1);
            RarityStyle.Chip(Card, frame.Rarity, CardWidth / 2f, Cursor + 10f);
            Space(30f);
            AddPrimary(L.T("Поставить в альбом")).OnClick(() =>
            {
                app.Progress.SelectFrame(frameId);
                app.Save();
                Close();
            });
            AddLink(L.T("Позже")).OnClick(Close);
            Finish(16);
        }
    }
}
