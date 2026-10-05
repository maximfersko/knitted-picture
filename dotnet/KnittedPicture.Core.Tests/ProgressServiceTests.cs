using Xunit;

namespace KnittedPicture.Core.Tests
{
    public class ProgressServiceTests
    {
        private static ProgressService Fresh() => new ProgressService(new PlayerProgress(), levelCount: 15);

        [Fact]
        public void New_player_starts_at_level_one_with_start_coins()
        {
            var progress = Fresh();

            Assert.Equal(1, progress.CurrentLevel);
            Assert.Equal(Economy.StartCoins, progress.Coins);
            Assert.True(progress.IsAvailable(1));
            Assert.False(progress.IsAvailable(2));
        }

        [Fact]
        public void First_win_pays_full_reward_and_opens_next_level()
        {
            var progress = Fresh();

            var reward = progress.CompleteLevel(1, hard: false);

            Assert.Equal(Economy.WinReward, reward);
            Assert.Equal(Economy.StartCoins + Economy.WinReward, progress.Coins);
            Assert.Equal(2, progress.CurrentLevel);
            Assert.True(progress.IsCompleted(1));
        }

        [Fact]
        public void Hard_level_pays_more_and_replay_pays_little()
        {
            var progress = Fresh();

            Assert.Equal(Economy.HardWinReward, progress.CompleteLevel(5, hard: true));
            Assert.Equal(Economy.ReplayReward, progress.CompleteLevel(5, hard: true));
        }

        [Fact]
        public void Replaying_an_old_level_does_not_move_progress_back()
        {
            var progress = Fresh();
            progress.CompleteLevel(1, false);
            progress.CompleteLevel(2, false);

            progress.CompleteLevel(1, false);

            Assert.Equal(3, progress.CurrentLevel);
        }

        [Fact]
        public void After_the_last_level_everything_is_done()
        {
            var progress = new ProgressService(new PlayerProgress { nextLevel = 15 }, levelCount: 15);

            progress.CompleteLevel(15, hard: true);

            Assert.True(progress.AllDone);
            Assert.Equal(15, progress.CurrentLevel);
        }

        [Fact]
        public void Payment_uses_stock_first_then_coins()
        {
            var progress = Fresh();
            progress.AddStock(BoosterType.Undo, 1);
            progress.AddCoins(Economy.Price(BoosterType.Undo) - Economy.StartCoins);

            Assert.Equal(Payment.FromStock, progress.Pay(BoosterType.Undo));
            Assert.Equal(Payment.WithCoins, progress.Pay(BoosterType.Undo));
            Assert.Equal(0, progress.Coins);
            Assert.Equal(Payment.NotEnough, progress.Pay(BoosterType.Undo));
        }

        [Fact]
        public void Booster_intro_shows_once_and_gifts_a_booster()
        {
            var progress = Fresh();
            var level = Economy.UnlockLevel(BoosterType.Scissors);

            Assert.Null(progress.TakeBoosterIntro(level - 1));
            Assert.Equal(BoosterType.Scissors, progress.TakeBoosterIntro(level));
            Assert.Equal(Economy.FreeOnUnlock, progress.Stock(BoosterType.Scissors));
            Assert.Null(progress.TakeBoosterIntro(level));
        }

        [Fact]
        public void Interstitial_waits_for_its_level_and_every_second_win()
        {
            var progress = new ProgressService(new PlayerProgress { nextLevel = 9 }, levelCount: 15);

            progress.CompleteLevel(9, false);
            Assert.False(progress.TakeInterstitialSlot(9));

            progress.CompleteLevel(10, true);
            Assert.True(progress.TakeInterstitialSlot(10));

            progress.CompleteLevel(11, false);
            Assert.False(progress.TakeInterstitialSlot(11));

            progress.CompleteLevel(12, false);
            Assert.True(progress.TakeInterstitialSlot(12));
        }

        [Fact]
        public void Collections_cover_all_fifteen_levels()
        {
            for (var id = 1; id <= 15; id++)
            {
                Assert.True(Collections.Of(id).Contains(id));
            }

            Assert.Equal("Сад", Collections.Of(7).Name);
        }
    }
}
