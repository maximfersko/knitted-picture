using System;
using System.Linq;
using Xunit;

namespace KnittedPicture.Core.Tests
{
    /// <summary>Корзинка пряжи, сундук, ежедневный подарок, достижения и коллекции.</summary>
    public class MetaTests
    {
        private static ProgressService Fresh() => new ProgressService(new PlayerProgress(), levelCount: 15);

        [Fact]
        public void First_win_drops_a_new_yarn_and_replay_does_not()
        {
            var progress = Fresh();

            var first = progress.CompleteLevel(1, hard: false);
            Assert.InRange(first.Yarn, 0, YarnCatalog.Count - 1);
            Assert.True(first.NewYarn);
            Assert.Equal(1, progress.YarnKinds);

            var replay = progress.CompleteLevel(1, hard: false);
            Assert.Equal(-1, replay.Yarn);
        }

        [Fact]
        public void Duplicate_yarn_turns_into_coins()
        {
            var progress = Fresh();
            progress.AddYarn(0);
            var coins = progress.Coins;

            var granted = progress.Grant(new Reward(0, null, 0, yarn: 0), new Random(1));

            Assert.False(granted.NewYarn);
            Assert.Equal(YarnCatalog.DuplicateCoins(Rarity.Common), granted.DuplicateCoins);
            Assert.Equal(coins + granted.DuplicateCoins, progress.Coins);
            Assert.Equal(2, progress.YarnCount(0));
        }

        [Fact]
        public void Full_yarn_set_can_be_claimed_once()
        {
            var progress = Fresh();
            var set = YarnCatalog.Sets[0];
            for (var i = set.First; i < set.First + set.Count; i++)
            {
                progress.AddYarn(i);
            }

            Assert.True(progress.CanClaimSet(set));
            Assert.NotNull(progress.ClaimSet(set, new Random(1)));
            Assert.Null(progress.ClaimSet(set, new Random(1)));
        }

        [Fact]
        public void Star_chest_opens_every_twelve_stars()
        {
            var progress = Fresh();
            for (var id = 1; id <= 3; id++)
            {
                progress.CompleteLevel(id, false, stars: 3);
            }

            Assert.Equal(9, progress.ChestProgress);
            Assert.False(progress.CanOpenChest);

            progress.CompleteLevel(4, false, stars: 3);
            Assert.True(progress.CanOpenChest);

            var coins = progress.Coins;
            var contents = progress.PeekChest();
            Assert.False(contents.Big);
            Assert.Equal(Chest.Choices, contents.Yarns.Length);
            Assert.Null(progress.ClaimChest(contents.Boosters.Length, 0));
            var granted = progress.ClaimChest(0, 1);
            Assert.NotNull(granted);
            Assert.Equal(contents.Boosters[0], granted!.Value.Reward.Booster);
            Assert.Equal(contents.Yarns[1], granted.Value.Reward.Yarn);
            Assert.True(progress.Coins > coins);
            Assert.Equal(0, progress.ChestProgress);
            Assert.False(progress.CanOpenChest);
        }

        [Fact]
        public void Chest_contents_are_stable_distinct_and_big_every_fourth()
        {
            var progress = Fresh();
            var first = progress.PeekChest();
            var again = progress.PeekChest();
            Assert.Equal(first.Coins, again.Coins);
            Assert.Equal(first.Yarns, again.Yarns);
            Assert.Equal(first.Boosters, again.Boosters);

            for (var n = 0; n < 40; n++)
            {
                var contents = Chest.Roll(n, 50, _ => false);
                Assert.Equal(Chest.IsBig(n), contents.Big);
                Assert.Equal(Chest.Choices, contents.Boosters.Length);
                Assert.Equal(contents.Boosters.Length, new System.Collections.Generic.HashSet<BoosterType>(contents.Boosters).Count);
                Assert.Equal(Chest.Choices, new System.Collections.Generic.HashSet<int>(contents.Yarns).Count);
                if (contents.Big)
                {
                    Assert.Equal(2, contents.BoosterCount);
                    Assert.Contains(contents.Yarns, y => YarnCatalog.All[y].Rarity != Rarity.Common);
                }
            }

            Assert.True(Chest.IsBig(3));
            Assert.False(Chest.IsBig(4));

            // Бустеры — только уже открытые: на 6-м уровне есть лишь «+1 слот».
            Assert.Equal(new[] { BoosterType.Slot }, Chest.Roll(0, 6, _ => false).Boosters);
        }

        [Fact]
        public void Reset_progress_starts_chest_and_meta_from_scratch()
        {
            var progress = Fresh();
            for (var id = 1; id <= 8; id++)
            {
                progress.CompleteLevel(id, false, stars: 3);
            }

            var first = progress.PeekChest();
            progress.ClaimChest(0, 0);
            progress.ClaimChest(0, 0);
            Assert.Equal(2, progress.ChestsOpened);

            // Сброс в игре — новый PlayerProgress: сундук, звёзды, пряжа и награды с нуля.
            var reset = Fresh();
            Assert.Equal(0, reset.ChestsOpened);
            Assert.Equal(0, reset.ChestProgress);
            Assert.False(reset.CanOpenChest);
            Assert.False(reset.NextChestBig);
            Assert.Equal(0, reset.TotalStars);
            Assert.Equal(0, reset.YarnKinds);
            Assert.Equal(0, reset.PendingClaims);
            Assert.Equal(Economy.StartCoins, reset.Coins);
            Assert.Equal(first.Coins, reset.PeekChest().Coins);
        }

        [Fact]
        public void Daily_gift_once_a_day_and_streak_resets_after_a_missed_day()
        {
            var progress = Fresh();
            var rng = new Random(5);

            Assert.Equal(0, progress.DailyIndex(100));
            Assert.NotNull(progress.ClaimDaily(100, rng));
            Assert.Null(progress.ClaimDaily(100, rng));
            Assert.Equal(1, progress.DailyIndex(101));

            progress.ClaimDaily(101, rng);
            progress.ClaimDaily(102, rng);
            Assert.Equal(3, progress.DailyIndex(103));
            Assert.Equal(3, progress.Data.dailyBest);

            Assert.Equal(0, progress.DailyIndex(105));
            progress.ClaimDaily(105, rng);
            Assert.Equal(1, progress.Data.dailyStreak);
            Assert.Equal(3, progress.Data.dailyBest);
        }

        [Fact]
        public void Seventh_daily_gift_brings_a_yarn()
        {
            var progress = Fresh();
            Granted? last = null;
            for (var day = 0; day < 7; day++)
            {
                last = progress.ClaimDaily(200 + day, new Random(day));
            }

            Assert.True(last!.Value.Reward.Yarn >= 0);
        }

        [Fact]
        public void Achievements_unlock_from_stats_and_claim_once()
        {
            var progress = Fresh();
            var first = Achievements.All.First(a => a.Title == "Первая петля");
            Assert.False(progress.CanClaimAchievement(first));

            progress.CompleteLevel(1, false);
            Assert.True(progress.CanClaimAchievement(first));
            Assert.Equal(1, progress.PendingClaims);

            Assert.NotNull(progress.ClaimAchievement(first, new Random(1)));
            Assert.False(progress.CanClaimAchievement(first));
            Assert.Equal(0, progress.PendingClaims);

            progress.RecordLevel(stitches: 1200, rushes: 1, goldCaught: 0, bestCombo: 6);
            Assert.True(progress.CanClaimAchievement(Achievements.All.First(a => a.Title == "Тысяча петель")));
            Assert.True(progress.CanClaimAchievement(Achievements.All.First(a => a.Title == "Разгон!")));
        }

        [Fact]
        public void Finished_album_collection_gives_a_reward()
        {
            var progress = Fresh();
            for (var id = 1; id <= 5; id++)
            {
                progress.CompleteLevel(id, false);
            }

            Assert.True(progress.CanClaimCollection(0));
            Assert.False(progress.CanClaimCollection(1));
            Assert.NotNull(progress.ClaimCollection(0, new Random(1)));
            Assert.True(progress.IsCollectionClaimed(0));
        }

        [Fact]
        public void Level_record_keeps_fewest_taps_and_clean_win()
        {
            var progress = Fresh();
            Assert.Equal(0, progress.BestTaps(3));

            progress.RecordResult(3, taps: 15, clean: false);
            progress.RecordResult(3, taps: 18, clean: true);

            Assert.Equal(15, progress.BestTaps(3));
            Assert.True(progress.WasClean(3));
            Assert.False(progress.WasClean(2));
        }

        [Fact]
        public void Achievement_and_yarn_ids_match_their_positions()
        {
            Assert.Equal(Enumerable.Range(0, Achievements.All.Length), Achievements.All.Select(a => a.Id));
            Assert.Equal(Enumerable.Range(0, YarnCatalog.Count), YarnCatalog.All.Select(y => y.Id));
            Assert.Equal(YarnCatalog.Count, YarnCatalog.Sets.Sum(s => s.Count));
        }
    }
}
