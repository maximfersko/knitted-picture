#nullable enable

using System;
using KnittedPicture.Core;
using UnityEngine;

namespace KnittedPicture.Game
{
    /// <summary>
    /// Вязальный марафон: кубок, сколько петель связано за неделю, три ступени с наградами и сколько дней
    /// осталось. Готовая ступень — «Забрать», дальше — «Играть», чтобы копить петли.
    /// </summary>
    public sealed class MarathonPopup : Popup
    {
        public MarathonPopup(App app, Action changed)
            : base(app)
        {
            var progress = app.Progress;
            var today = app.Today;
            var stitches = progress.MarathonStitches(today);
            var claimed = progress.MarathonClaimed(today);
            var targets = WeeklyMarathon.Targets;

            const float cup = 72f;
            var trophy = Ui.Image(Card, "Trophy", Art2.Trophy(true), Color.white);
            Ui.PlaceCenter(trophy.rectTransform, CardWidth / 2f, Cursor + cup / 2f, cup, cup);
            Tweens.Scale(trophy.rectTransform, 0.6f, 1f, 0.45f, Ease.OutBack, null, 0.1f);
            Space(cup + 10f);
            AddTitle(L.T("Вязальный марафон"), 22);
            var days = WeeklyMarathon.DaysLeft(today);
            AddText(L.F("Вяжи петли на любых уровнях. Новая неделя начнётся через {0} {1}", days, Days(days)), 14, 2);

            // Общая полоска до последней ступени: с отметками ступеней.
            var last = targets[targets.Length - 1];
            var bar = Ui.ProgressBar(Card, 24, Cursor, Inner, 12, Mathf.Min(1f, (float)stitches / last), Palette.Link);
            for (var i = 0; i < targets.Length - 1; i++)
            {
                var tick = Ui.Box(Card, "Tick", 1, Palette.Card).rectTransform;
                Ui.Place(tick, 24 + Inner * targets[i] / last - 1f, Cursor, 2f, 12f);
            }

            Space(18f);
            var count = Ui.Label(Card, L.F("{0} из {1} петель", Mathf.Min(stitches, last), last), 13, Palette.Ink2, 24, Cursor, Inner, 18, true, TextAnchor.MiddleRight);
            Ui.RollTo(count, Mathf.Min(stitches, last), 0.6f, L.F("{{0}} из {0} петель", last), 0.2f);
            Space(26f);

            for (var i = 0; i < targets.Length; i++)
            {
                var done = i < claimed;
                var ready = !done && stitches >= targets[i] && i == claimed;
                var reward = WeeklyMarathon.Rewards[i];
                var row = Ui.Box(Card, "Tier", 14, ready ? Palette.CoinSoft : done ? Palette.PrimarySoft : Palette.Surface).rectTransform;
                Ui.Place(row, 24, Cursor, Inner, 48);
                Ui.Outline(row, 14, ready ? Palette.Coin : done ? Palette.Primary : Palette.Line, ready ? 2f : 1.5f);
                Ui.Label(row, L.F("{0} петель", targets[i]), 14, Palette.Ink, 14, 0, 110, 48, true, TextAnchor.MiddleLeft);
                var x = 130f;
                if (reward.Coins > 0)
                {
                    var coins = Ui.Label(row, "+" + reward.Coins, 14, Palette.Ink, x, 0, 50, 48, true, TextAnchor.MiddleLeft);
                    coins.horizontalOverflow = HorizontalWrapMode.Overflow;
                    x += coins.preferredWidth + 4f;
                    Ui.Coin(row, x + 9f, 24, 18);
                    x += 24f;
                }

                if (reward.Booster != null)
                {
                    var art = Ui.Image(row, "Booster", Art2.Booster(reward.Booster.Value), Color.white);
                    Ui.PlaceCenter(art.rectTransform, x + 12f, 24, 26, 26);
                    x += 30f;
                }

                if (reward.Yarn != -1)
                {
                    var ball = Ui.Image(row, "Yarn", Art2.YarnBall(Palette.Link), Color.white);
                    Ui.PlaceCenter(ball.rectTransform, x + 12f, 24, 24, 24);
                }

                if (done)
                {
                    var mark = Ui.Image(row, "Done", Art.Circle, Palette.Primary);
                    Ui.PlaceCenter(mark.rectTransform, Inner - 24f, 24, 24, 24);
                    Ui.IconImage(row, Icon.Check, Color.white, Inner - 24f, 24, 14);
                }

                Space(56f);
            }

            Space(4f);
            if (progress.CanClaimMarathon(today))
            {
                var claim = AddPrimary(L.T("Забрать"));
                Ui.Sheen((RectTransform)claim.transform, Inner, 60f, 3.2f, 0.5f);
                claim.OnClick(() =>
                {
                    var granted = progress.ClaimMarathon(today, app.Random);
                    Analytics.Event("marathon_claim", ("tier", claimed + 1), ("level", progress.CurrentLevel));
                    app.Save();
                    Close();
                    if (granted != null)
                    {
                        app.ShowPopup(new RewardPopup(app, L.T("Вязальный марафон"), Icon.Trophy, granted.Value, changed));
                    }
                });
            }
            else if (claimed >= targets.Length)
            {
                AddText(L.T("Все ступени этой недели взяты. Новый марафон в понедельник"), 14, 1);
                AddPrimary(L.T("Понятно"), 54).OnClick(Close);
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

        private static string Days(int n)
        {
            return L.Plural(n, "день", "дня", "дней");
        }
    }
}
