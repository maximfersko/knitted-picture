#nullable enable

using System;
using KnittedPicture.Core;
using UnityEngine;

namespace KnittedPicture.Game
{
    /// <summary>
    /// Задания дня: три строки с прогрессом, награда за все три и кнопка, которая ведёт в игру.
    /// Выполненное задание — зелёное с галочкой; когда готовы все три — «Забрать».
    /// </summary>
    public sealed class MissionsPopup : Popup
    {
        public MissionsPopup(App app, Action changed)
            : base(app)
        {
            var progress = app.Progress;
            var today = app.Today;
            AddIcon(Icon.Check, Palette.Primary, Palette.PrimarySoft, 64);
            AddTitle(L.T("Задания дня"), 24);

            var missions = progress.Missions(today);
            for (var i = 0; i < missions.Count; i++)
            {
                var m = missions[i];
                var row = Ui.Box(Card, "Mission", 16, m.Done ? Palette.PrimarySoft : Palette.Surface).rectTransform;
                Ui.Place(row, 24, Cursor, Inner, 58);
                Ui.Outline(row, 16, m.Done ? Palette.Primary : Palette.Line, m.Done ? 2f : 1.5f);
                var mark = Ui.Image(row, "Mark", Art.Circle, m.Done ? Palette.Primary : Palette.Card);
                Ui.PlaceCenter(mark.rectTransform, 26, 29, 30, 30);
                if (m.Done)
                {
                    Ui.IconImage(row, Icon.Check, Color.white, 26, 29, 16);
                }
                else
                {
                    Ui.Outline(mark.rectTransform, 15, Palette.Line, 1.5f);
                    Ui.Label(row, (i + 1).ToString(), 14, Palette.Ink2, 11, 14, 30, 30);
                }

                Ui.Fit(Ui.Label(row, m.Text, 14, Palette.Ink, 52, 8, Inner - 52 - 64, 22, true, TextAnchor.MiddleLeft), 10);
                Ui.ProgressBar(row, 52, 36, Inner - 52 - 74, 7, (float)m.Value / m.Target, m.Done ? Palette.Primary : Palette.Link);
                Ui.Label(row, $"{m.Value}/{m.Target}", 13, m.Done ? Palette.Primary : Palette.Ink2, Inner - 70, 0, 58, 58, true, TextAnchor.MiddleRight);
                Space(66f);
            }

            // Награда за все три: монеты и бустер.
            var reward = progress.MissionsReward(today);
            var box = Ui.Box(Card, "Reward", 16, Palette.CoinSoft).rectTransform;
            Ui.Place(box, 24, Cursor + 2f, Inner, 44);
            Ui.Outline(box, 16, Palette.Coin, 1.5f);
            Ui.Label(box, L.T("За все три:"), 14, Palette.Ink2, 14, 0, 110, 44, true, TextAnchor.MiddleLeft);
            var coinsLabel = Ui.Label(box, "+" + reward.Coins, 16, Palette.Ink, 124, 0, 60, 44, true, TextAnchor.MiddleLeft);
            coinsLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            Ui.Coin(box, 124 + coinsLabel.preferredWidth + 14f, 22, 20);
            if (reward.Booster != null)
            {
                var bx = 124 + coinsLabel.preferredWidth + 46f;
                var circle = Ui.Image(box, "Booster", Art.Circle, Palette.PrimarySoft);
                Ui.PlaceCenter(circle.rectTransform, bx, 22, 28, 28);
                Ui.IconImage(box, BoosterInfo.Icon(reward.Booster.Value), Palette.Primary, bx, 22, 16);
            }

            Space(58f);

            if (progress.CanClaimMissions(today))
            {
                AddPrimary(L.T("Забрать")).OnClick(() =>
                {
                    var granted = progress.ClaimMissions(today, app.Random);
                    Analytics.Event("missions_claim", ("level", progress.CurrentLevel));
                    app.Save();
                    Close();
                    if (granted != null)
                    {
                        app.ShowPopup(new RewardPopup(app, L.T("Задания дня"), Icon.Check, granted.Value, changed));
                    }
                });
            }
            else if (progress.MissionsClaimed(today))
            {
                AddText(L.T("Награда получена"), 14, 1);
                AddLimit(() => L.F("Новые задания через {0}", Limits.ResetIn(Limits.NextMidnight())));
                AddPrimary(L.T("Понятно"), 54).OnClick(Close);
            }
            else if (!progress.AllDone)
            {
                AddPrimary(L.T("Играть")).OnClick(() =>
                {
                    Close();
                    app.StartLevel(progress.CurrentLevel);
                });
            }
            else
            {
                AddPrimary(L.T("Понятно"), 54).OnClick(Close);
            }

            Finish(8);
        }
    }
}
