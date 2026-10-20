using Xunit;

namespace KnittedPicture.Core.Tests
{
    public class ProgressServiceTests
    {
        [Fact]
        public void Reset_starts_progress_over_but_keeps_player_settings()
        {
            var old = new PlayerProgress
            {
                soundOn = false, musicOn = false, language = "en", hapticsOn = false, reduceMotion = true, largeText = true,
                idleHints = false, batterySaver = true, remindersOn = false, remindersAsked = true, adsRemoved = true, adsConsent = 2,
                coins = 999, totalWins = 40,
            };
            old.completed.Add(1);

            var fresh = old.FreshKeepingSettings();

            Assert.False(fresh.soundOn);
            Assert.False(fresh.musicOn);
            Assert.Equal("en", fresh.language);
            Assert.False(fresh.hapticsOn);
            Assert.True(fresh.reduceMotion);
            Assert.True(fresh.largeText);
            Assert.False(fresh.idleHints);
            Assert.True(fresh.batterySaver);
            Assert.False(fresh.remindersOn);
            Assert.True(fresh.remindersAsked);
            Assert.True(fresh.adsRemoved);
            Assert.Equal(2, fresh.adsConsent);
            Assert.Empty(fresh.completed);
            Assert.Equal(new PlayerProgress().coins, fresh.coins);
            Assert.Equal(0, fresh.totalWins);
        }

        private static ProgressService Fresh() => new ProgressService(new PlayerProgress(), levelCount: 15);

        [Fact]
        public void Stuck_help_comes_from_the_fourth_attempt_of_a_new_level_and_only_once()
        {
            var progress = Fresh();
            for (var attempt = 1; attempt < Economy.StuckHelpFromAttempt; attempt++)
            {
                Assert.Equal(attempt, progress.NextAttempt(3));
                Assert.False(progress.TakeStuckHelp(3, attempt));
            }

            var fourth = progress.NextAttempt(3);
            Assert.Equal(Economy.StuckHelpFromAttempt, fourth);
            Assert.True(progress.TakeStuckHelp(3, fourth));
            Assert.True(progress.StuckHelped(3));

            // Дальше тот же уровень помощи не даёт, а другой уровень считает свои попытки сам.
            Assert.False(progress.TakeStuckHelp(3, progress.NextAttempt(3)));
            Assert.False(progress.TakeStuckHelp(4, progress.NextAttempt(4)));
            Assert.Contains(3, progress.Data.stuckHelped);
        }

        [Fact]
        public void Stuck_help_is_not_given_on_a_level_already_completed()
        {
            var progress = Fresh();
            progress.CompleteLevel(1, hard: false, stars: 3);
            for (var i = 0; i < 6; i++)
            {
                progress.NextAttempt(1);
            }

            Assert.False(progress.TakeStuckHelp(1, 6));
            Assert.False(progress.StuckHelped(1));
        }

        [Fact]
        public void Missing_stars_count_all_levels_and_point_at_the_first_incomplete_picture()
        {
            var progress = Fresh();
            Assert.Equal(progress.LevelCount * 3, progress.MissingStars());
            Assert.Equal(1, progress.FirstUnderThreeStars());
            progress.CompleteLevel(1, hard: false, stars: 3);
            progress.CompleteLevel(2, hard: false, stars: 1);
            Assert.Equal(progress.LevelCount * 3 - 4, progress.MissingStars());
            Assert.Equal(2, progress.FirstUnderThreeStars());
        }

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

            Assert.Equal(Economy.WinReward, reward.Total);
            Assert.Equal(Economy.StartCoins + Economy.WinReward, progress.Coins);
            Assert.Equal(2, progress.CurrentLevel);
            Assert.True(progress.IsCompleted(1));
        }

        [Fact]
        public void Hard_level_pays_more_and_replay_pays_little()
        {
            var progress = Fresh();

            Assert.Equal(Economy.HardWinReward, progress.CompleteLevel(5, hard: true).BaseCoins);
            Assert.Equal(Economy.ReplayReward, progress.CompleteLevel(5, hard: true).BaseCoins);
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
        public void Attempts_are_counted_per_level()
        {
            var progress = Fresh();
            Assert.Equal(1, progress.NextAttempt(3));
            Assert.Equal(2, progress.NextAttempt(3));
            Assert.Equal(1, progress.NextAttempt(1));
            Assert.Equal(3, progress.NextAttempt(3));
        }

        [Fact]
        public void No_interstitials_after_ads_are_removed()
        {
            var progress = new ProgressService(new PlayerProgress { nextLevel = 10, adsRemoved = true }, levelCount: 15);

            for (var level = 10; level <= 13; level++)
            {
                progress.CompleteLevel(level, false);
                Assert.False(progress.TakeInterstitialSlot(level));
            }
        }

        [Fact]
        public void Best_stars_are_kept_and_an_improvement_is_a_record()
        {
            var progress = Fresh();

            progress.CompleteLevel(3, false, stars: 2);
            Assert.Equal(2, progress.StarsOf(3));
            Assert.Equal(0, progress.StarsOf(2));

            Assert.False(progress.CompleteLevel(3, false, stars: 1).NewRecord);
            Assert.Equal(2, progress.StarsOf(3));
            Assert.True(progress.CompleteLevel(3, false, stars: 3).NewRecord);
            Assert.Equal(3, progress.StarsOf(3));
        }

        [Fact]
        public void Warm_streak_grows_its_start_gift_and_breaks_on_restart()
        {
            var progress = Fresh();
            for (var id = 1; id <= 8; id++)
            {
                progress.TakeBoosterIntro(id);
            }

            Assert.False(progress.WarmStartFor(9).Any);
            progress.CompleteLevel(9, false);
            var one = progress.WarmStartFor(10);
            Assert.True(one.ExtraSlot);
            Assert.False(one.FreeScissors);

            progress.CompleteLevel(10, false);
            Assert.True(progress.WarmStartFor(11).FreeScissors);
            Assert.False(progress.WarmStartFor(11).FreeUndo);

            // Отмена откроется только на 12-м: на 11-м третьей ступени нет, на 12-м есть.
            progress.CompleteLevel(11, false);
            Assert.False(progress.WarmStartFor(11).Any);
            progress.TakeBoosterIntro(12);
            Assert.True(progress.WarmStartFor(12).FreeUndo);

            // На старых уровнях подарок не действует, серия обнуляется перезапуском.
            Assert.False(progress.WarmStartFor(9).Any);
            progress.BreakStreak();
            Assert.False(progress.WarmStartFor(12).Any);
        }

        [Fact]
        public void Replays_neither_grow_nor_break_the_win_streak()
        {
            var progress = Fresh();
            progress.CompleteLevel(1, false);
            progress.CompleteLevel(2, false);
            Assert.Equal(2, progress.Streak);

            var replay = progress.CompleteLevel(1, false);
            Assert.Equal(Economy.ReplayReward, replay.Total);
            Assert.Equal(2, progress.Streak);
        }

        [Fact]
        public void Boosters_opened_later_stay_available_on_old_levels()
        {
            var progress = Fresh();
            Assert.False(progress.IsBoosterOpen(BoosterType.Slot, 3));
            Assert.True(progress.IsBoosterOpen(BoosterType.Slot, 5));

            progress.TakeBoosterIntro(5);
            Assert.True(progress.IsBoosterOpen(BoosterType.Slot, 3));
            Assert.False(progress.IsBoosterOpen(BoosterType.Scissors, 3));
        }

        [Fact]
        public void Stars_depend_on_help_used()
        {
            Assert.Equal(3, Economy.Stars(boostersUsed: 0, rescues: 0));
            Assert.Equal(2, Economy.Stars(boostersUsed: 1, rescues: 0));
            Assert.Equal(1, Economy.Stars(boostersUsed: 0, rescues: 1));
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
