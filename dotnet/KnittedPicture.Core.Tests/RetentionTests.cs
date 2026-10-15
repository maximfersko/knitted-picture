using System.Linq;
using KnittedPicture.Core;
using Xunit;

namespace KnittedPicture.Core.Tests
{
    /// <summary>Сезон, выставка недели, посылка после перерыва, рамки коллекций и золотая пряжа.</summary>
    public class RetentionTests
    {
        private static ProgressService Fresh(int levels = 65) => new ProgressService(new PlayerProgress(), levelCount: levels);

        private static LevelData Picture(int id, params string[] colors) => new LevelData
        {
            id = id,
            width = 2,
            height = 1,
            colors = colors,
            picture = new[] { "01" },
        };

        [Fact]
        public void Season_track_has_rarity_ladder_and_ends_with_season_yarn_and_frame()
        {
            var steps = Enumerable.Range(1, Season.Steps).Select(n => Season.Step(n, SeasonTheme.Winter)).ToList();
            Assert.Equal(SeasonPrize.SeasonFrame, steps[Season.Steps - 1].Prize);
            Assert.Equal(SeasonPrize.SeasonYarn, steps[Season.Steps - 2].Prize);
            Assert.Equal(Season.YarnOf(SeasonTheme.Winter), steps[Season.Steps - 2].Reward.Yarn);
            Assert.True(YarnCatalog.All[Season.YarnOf(SeasonTheme.Winter)].Seasonal);
            Assert.Equal(Rarity.Epic, steps[9].Rarity);
            Assert.Equal(Rarity.Rare, steps[4].Rarity);
            Assert.All(steps, s => Assert.True(s.Rarity >= Rarity.Common));

            // Время года — по календарю середины сезона.
            for (var season = 600; season < 640; season++)
            {
                var mid = new System.DateTime(1970, 1, 1).AddDays(Season.StartDay(season) + Season.Days / 2).Month;
                var theme = Season.ThemeOf(season);
                Assert.Equal(mid is 12 or 1 or 2, theme == SeasonTheme.Winter);
                Assert.Equal(mid is 6 or 7 or 8, theme == SeasonTheme.Summer);
            }
        }

        [Fact]
        public void Season_points_come_from_wins_and_reset_with_new_season()
        {
            var p = Fresh();
            p.Data.nextLevel = Season.FromLevel;
            var day = Season.StartDay(700);
            p.CompleteLevel(5, false, 3, 0, day);
            Assert.Equal(3, p.SeasonPoints(day));
            p.CompleteLevel(5, false, 3, 0, day);
            Assert.Equal(3 + Season.ReplayPoints, p.SeasonPoints(day));

            p.RecordSeason(day, Season.StepPoints * 2);
            Assert.Equal(2, p.SeasonWaiting(day));
            var first = p.ClaimSeason(day, new System.Random(1));
            Assert.NotNull(first);
            Assert.Equal(1, first!.Value.Step.Number);
            Assert.Equal(1, p.SeasonWaiting(day));

            // Новый сезон: счёт с нуля, незабранное прошлого не выдаётся.
            var next = Season.StartDay(701);
            Assert.Equal(0, p.SeasonPoints(next));
            Assert.Null(p.ClaimSeason(next, new System.Random(1)));
        }

        [Fact]
        public void Season_final_gives_frame_once_then_coins()
        {
            var p = Fresh();
            p.Data.nextLevel = Season.FromLevel;
            var day = Season.StartDay(710);
            p.RecordSeason(day, Season.PointsFor(Season.Steps));
            SeasonClaim? last = null;
            while (p.CanClaimSeason(day))
            {
                last = p.ClaimSeason(day, new System.Random(3));
            }

            var frame = AlbumFrames.ForSeason(Season.ThemeOf(710));
            Assert.Equal(frame.Id, last!.Value.Frame);
            Assert.True(p.FrameOwned(frame.Id));
            Assert.True(p.HasYarn(Season.YarnOf(Season.ThemeOf(710))));

            // Тот же сезон через год: рамка уже есть — монеты.
            var again = Season.StartDay(710 + 13);
            Assert.Equal(Season.ThemeOf(710), Season.ThemeOf(723));
            p.RecordSeason(again, Season.PointsFor(Season.Steps));
            while (p.CanClaimSeason(again))
            {
                last = p.ClaimSeason(again, new System.Random(3));
            }

            Assert.Equal(-1, last!.Value.Frame);
            Assert.Equal(Season.FrameOwnedCoins, last.Value.Granted.TotalCoins);
        }

        [Fact]
        public void Seasonal_yarn_never_drops_randomly()
        {
            var p = Fresh();
            var rng = new System.Random(5);
            for (var i = 0; i < 2000; i++)
            {
                Assert.False(YarnCatalog.All[p.RollYarn(rng)].Seasonal);
            }

            for (var n = 0; n < 100; n++)
            {
                Assert.DoesNotContain(Chest.Roll(n, 60, _ => false).Yarns, y => YarnCatalog.All[y].Seasonal);
            }

            Assert.Equal(16, YarnCatalog.BaseCount);
        }

        [Fact]
        public void Duplicates_on_max_level_build_golden_yarn_then_give_coins()
        {
            var p = Fresh();
            var yarn = 0;
            for (var i = 0; i < YarnCatalog.MaxLevel; i++)
            {
                p.Grant(new Reward(0, null, 0, yarn), new System.Random(1));
            }

            for (var step = 1; step <= YarnCatalog.GoldenAfter; step++)
            {
                var g = p.Grant(new Reward(0, null, 0, yarn), new System.Random(1));
                Assert.Equal(step, g.GoldStep);
                Assert.Equal(0, g.DuplicateCoins);
                Assert.False(g.YarnLevelUp);
            }

            Assert.True(p.IsGolden(yarn));
            var power = YarnCatalog.Power(yarn, YarnCatalog.MaxLevel, true);
            Assert.Equal(YarnCatalog.Power(yarn, YarnCatalog.MaxLevel) + 1, power);
            var after = p.Grant(new Reward(0, null, 0, yarn), new System.Random(1));
            Assert.Equal(0, after.GoldStep);
            Assert.Equal(YarnCatalog.DuplicateCoins(YarnCatalog.All[yarn].Rarity), after.DuplicateCoins);
        }

        [Fact]
        public void Collection_frame_opens_with_all_stars_and_survives_reload()
        {
            var p = Fresh();
            var frame = AlbumFrames.ForCollection(0);
            Assert.False(p.FrameOwned(frame.Id));
            Assert.False(p.BuyFrame(frame.Id));
            for (var id = 1; id <= 5; id++)
            {
                p.CompleteLevel(id, false, id == 5 ? 2 : 3);
            }

            Assert.False(p.FrameOwned(frame.Id));
            p.CompleteLevel(5, false, 3);
            Assert.True(p.FrameOwned(frame.Id));
            Assert.True(p.SelectFrame(frame.Id));
            var reloaded = new ProgressService(p.Data, 65);
            Assert.Equal(frame.Id, reloaded.SelectedFrame.Id);
        }

        [Fact]
        public void Exhibition_scores_theme_colors_stars_and_frames_once_a_week()
        {
            var p = Fresh();
            p.Data.nextLevel = 10;
            var week = 3000;
            var day = week * 7 - 3;
            Assert.Equal(week, WeeklyMarathon.WeekOf(day));
            var theme = Exhibition.ThemeOf(week);
            var fit = theme.Colors[0];
            var other = YarnColors.All.Select(c => c.Hex).First(h => !theme.Fits(h));
            var pictures = new[] { Picture(1, fit, fit), Picture(2, fit, other), Picture(3, other, other) };
            foreach (var pic in pictures)
            {
                p.CompleteLevel(pic.id, false, 3);
            }

            Assert.Equal(60, Exhibition.Score(pictures[0], 0, AlbumFrames.Get(0), theme));
            Assert.Equal(30 + 30 + 6 * 4, Exhibition.Score(pictures[1], 3, AlbumFrames.Get(9), theme));

            var frames = new[] { 0, 0, 0 };
            Assert.Null(p.SubmitExhibition(day, new[] { pictures[0], pictures[0], pictures[1] }, frames, new System.Random(1)));
            Assert.Null(p.SubmitExhibition(day, pictures, new[] { 0, 9, 0 }, new System.Random(1)));
            var preview = p.ExhibitionPreview(day, pictures, frames);
            var result = p.SubmitExhibition(day, pictures, frames, new System.Random(1));
            Assert.NotNull(result);
            Assert.Equal(preview, result!.Value.Score);
            Assert.Equal(Exhibition.MedalOf(preview), result.Value.Medal);
            Assert.Null(p.SubmitExhibition(day, pictures, frames, new System.Random(1)));
            Assert.False(p.ExhibitionDone(day + 7));
        }

        [Fact]
        public void Comeback_parcel_after_three_days_is_one_tier_higher_and_warms_next_level()
        {
            var p = Fresh();
            p.CompleteLevel(1, false, 3);
            Assert.False(p.Visit(100));
            Assert.False(p.Visit(102));
            Assert.True(p.Visit(105));
            Assert.Equal(3, p.Data.comebackDays);
            Assert.True(p.ComebackRarity > p.NextChestRarity);
            var coins = p.Coins;
            var granted = p.ClaimComeback(new System.Random(4));
            Assert.NotNull(granted);
            Assert.True(p.Coins > coins);
            Assert.True(YarnCatalog.All[granted!.Value.Reward.Yarn].Rarity >= Rarity.Uncommon);
            Assert.Null(p.ClaimComeback(new System.Random(4)));
            Assert.True(p.WarmStartFor(p.CurrentLevel).Any || p.CurrentLevel < Economy.UnlockLevel(BoosterType.Slot));
            p.CompleteLevel(p.CurrentLevel, false, 3);
            Assert.False(p.Data.comebackWarm);

            // Часы назад: посылки нет, последний день не сдвигается назад.
            Assert.False(p.Visit(90));
            Assert.Equal(105, p.Data.lastDay);

            // Снова перерыв, но раньше двух недель с прошлой посылки — без посылки; позже — снова есть.
            Assert.False(p.Visit(110));
            Assert.True(p.Visit(105 + ProgressService.ComebackCooldown + 3));
        }
    }
}
