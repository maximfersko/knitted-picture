using System.Linq;
using Xunit;

namespace KnittedPicture.Core.Tests
{
    /// <summary>Границы новых правил: порядок слотов, отмена наград, безопасный отложенный ввод.</summary>
    public class ResponsiveMechanicsTests
    {
        private static LevelData Level(params SpoolData[] spools) => new LevelData
        {
            width = 4, height = 3, slots = 4,
            colors = new[] { "#FFFFFF", "#000000", "#FF0000" },
            picture = new[] { "1111", "0000", "0000" },
            spools = spools,
        };

        private static SpoolData Spool(int column, int color, int loops, bool thick = false, bool rainbow = false) =>
            new SpoolData { column = column, color = color, loops = loops, thick = thick, rainbow = rainbow };

        private static void Land(LoomGame game)
        {
            for (var i = 0; i < LoomGame.WarmupSteps; i++)
            {
                game.Step();
            }
        }

        [Fact]
        public void Synergy_is_symmetric_even_when_first_neighbor_completes()
        {
            var game = new LoomGame(Level(Spool(0, 0, 1), Spool(1, 0, 7), Spool(2, 1, 4)));
            game.TapColumn(0);
            game.TapColumn(1);
            Assert.False(game.HasSlotSynergy(0));
            Land(game);
            Assert.True(game.HasSlotSynergy(0));
            Assert.True(game.HasSlotSynergy(1));
            var step = game.Step();
            Assert.Equal(1, step.Stitches.Count(s => s.SpoolId == 0));
            Assert.Equal(2, step.Stitches.Count(s => s.SpoolId == 1));
            Assert.False(game.HasSlotSynergy(1));
            Assert.Single(game.Step().Stitches);
        }

        [Fact]
        public void Thick_tail_provides_neighbor_bonus_without_self_synergy()
        {
            var game = new LoomGame(Level(Spool(0, 0, 4, thick: true), Spool(1, 0, 4), Spool(2, 1, 4)));
            game.TapColumn(0);
            Land(game);
            Assert.False(game.HasSlotSynergy(0));
            game.TapColumn(1);
            // Свой хвост не ускоряет клубок и сосед ещё летит.
            Assert.False(game.HasSlotSynergy(0));
            var ready = new LoomGame(Level(Spool(0, 0, 4, thick: true), Spool(1, 0, 4), Spool(2, 1, 4)));
            ready.TapColumn(0);
            ready.TapColumn(1);
            Land(ready);
            Assert.True(ready.HasSlotSynergy(0));
            Assert.True(ready.HasSlotSynergy(2));
            var step = ready.Step();
            Assert.Equal(3, step.Stitches.Count(s => s.SpoolId == 0));
            Assert.Equal(2, step.Stitches.Count(s => s.SpoolId == 1));
        }

        [Fact]
        public void Rainbow_matches_neighbor_but_foreign_and_gaps_do_not()
        {
            var rainbow = new LoomGame(Level(Spool(0, 0, 8), Spool(1, 1, 4, rainbow: true)));
            rainbow.TapColumn(0);
            rainbow.TapColumn(1);
            Land(rainbow);
            Assert.True(rainbow.HasSlotSynergy(0));

            var foreign = new LoomGame(Level(Spool(0, 0, 8), Spool(1, 2, 4), Spool(2, 1, 4)));
            foreign.TapColumn(0);
            foreign.TapColumn(1);
            Land(foreign);
            Assert.False(foreign.HasSlotSynergy(0));
            Assert.False(foreign.HasSlotSynergy(1));

            var gap = new LoomGame(Level(Spool(0, 0, 4), Spool(1, 1, 4), Spool(2, 0, 4)));
            gap.TapColumn(0);
            gap.TapColumn(1);
            gap.TapColumn(2);
            gap.CutFromLoom(1);
            Land(gap);
            Assert.False(gap.HasSlotSynergy(0));
            Assert.False(gap.HasSlotSynergy(2));
        }

        [Fact]
        public void Disconnected_thick_tail_matches_neighbor_at_its_own_position()
        {
            var game = new LoomGame(Level(Spool(0, 0, 2), Spool(1, 1, 4), Spool(2, 0, 2),
                Spool(3, 0, 4), Spool(4, 0, 4, thick: true)));
            for (var i = 0; i < 4; i++) { game.TapColumn(i); }
            game.CutFromLoom(0);
            game.CutFromLoom(2);
            var move = game.TapColumn(4);
            Assert.Equal(0, move.Placed[0].Slot);
            Assert.Equal(2, move.Placed[0].TailSlot);
            Land(game);
            Assert.True(game.HasSlotSynergy(0));
            Assert.True(game.HasSlotSynergy(3));
            Assert.Equal(3, game.Step().Stitches.Count(s => s.SpoolId == 4));
        }

        [Fact]
        public void Neighbor_landing_during_step_does_not_grant_bonus_until_next_step()
        {
            var game = new LoomGame(Level(Spool(0, 0, 4), Spool(1, 0, 4), Spool(2, 1, 4)));
            // Первый слот освободится, пока второй клубок долетает.
            game.TapColumn(2);
            game.TapColumn(1);
            game.Step();
            game.CutFromLoom(0);
            game.TapColumn(0);
            game.Step();
            game.Step();
            // Левый ещё летит один шаг; правый уже готов.
            Assert.Equal(1, game.Slots[0]!.Warmup);
            Assert.Equal(0, game.Slots[1]!.Warmup);
            Assert.Single(game.Step().Stitches);
            Assert.Equal(4, game.Step().Stitches.Count);
        }

        [Fact]
        public void Foreign_yarn_counts_six_steps_after_landing_and_clone_keeps_progress()
        {
            var level = Level(Spool(0, 2, 100, thick: true), Spool(1, 0, 8), Spool(2, 1, 4));
            level.slots = 2;
            var game = new LoomGame(level);
            game.TapColumn(0);
            Land(game);
            for (var i = 0; i < LoomGame.ForeignLifetime - 1; i++)
            {
                var step = game.Step();
                Assert.True(step.Progress);
                Assert.Empty(step.Stitches);
                Assert.Empty(step.Completed);
                Assert.True(game.IsBusy);
                Assert.Equal(GameStatus.Playing, game.Status);
            }

            var clone = game.Clone();
            var completion = Assert.Single(game.Step().Completed);
            Assert.True(completion.Unraveled);
            Assert.False(completion.Perfect);
            Assert.Equal(100, completion.Leftover);
            Assert.Equal(2, game.FreeSlots);
            Assert.Equal(0, game.KnittedCount);
            Assert.True(Assert.Single(clone.Step().Completed).Unraveled);
        }

        [Fact]
        public void Undo_restores_foreign_countdown()
        {
            var game = new LoomGame(Level(Spool(0, 2, 5), Spool(1, 0, 8), Spool(2, 1, 4)));
            game.TapColumn(0);
            Land(game);
            game.Step();
            game.TapColumn(1);
            game.Step();
            Assert.Equal(2, game.Slots[0]!.UnravelSteps);
            Assert.True(game.Undo());
            Assert.Equal(1, game.Slots[0]!.UnravelSteps);
        }

        [Fact]
        public void Gold_time_is_capped_and_cannot_be_farmed_with_undo_or_clone()
        {
            var level = Level(Spool(0, 0, 8), Spool(1, 1, 4));
            level.gold = new[] { 8 };
            level.timeLimit = 10;
            var game = new LoomGame(level);
            game.TapColumn(0);
            game.Tick(1f);
            Land(game);
            Assert.Equal(1f, game.Step().TimeAdded);
            Assert.Equal(10f, game.TimeLeft);
            Assert.True(game.Undo());
            game.TapColumn(0);
            game.Tick(4f);
            Land(game);
            var clone = game.Clone();
            Assert.Equal(0f, game.Step().TimeAdded);
            Assert.Equal(0f, clone.Step().TimeAdded);
            Assert.Equal(6f, game.TimeLeft);
        }

        private static LoomGame AlmostFree()
        {
            var level = Level(Spool(0, 0, 1), Spool(1, 0, 7), Spool(2, 1, 4));
            level.slots = 1;
            var game = new LoomGame(level);
            game.TapColumn(0);
            Land(game);
            return game;
        }

        [Fact]
        public void Buffer_executes_once_when_slot_really_frees()
        {
            var game = AlmostFree();
            var input = new ResponsiveInput();
            var reject = game.TapColumn(1);
            Assert.True(input.Buffer(game, 1, reject.Reason, 10f, 0.04f, 0.065f));
            Assert.Null(input.Flush(game, 10.02f));
            Assert.Equal(1, game.Taps);
            game.Step();
            Assert.True(input.Flush(game, 10.04f)!.Accepted);
            Assert.Equal(2, game.Taps);
            Assert.Null(input.Flush(game, 10.05f));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Expired_or_cancelled_buffer_never_makes_a_move(bool cancel)
        {
            var game = AlmostFree();
            var input = new ResponsiveInput();
            Assert.True(input.Buffer(game, 1, RejectReason.NoFreeSlot, 0f, 0.04f, 0.065f));
            game.Step();
            if (cancel) { input.Clear(); }
            Assert.Null(input.Flush(game, cancel ? 0.04f : 0.151f));
            Assert.False(input.Pending);
            Assert.Equal(1, game.Taps);
        }

        [Fact]
        public void Other_action_invalidates_buffer_even_if_same_column_remains()
        {
            var game = AlmostFree();
            var input = new ResponsiveInput();
            Assert.True(input.Buffer(game, 1, RejectReason.NoFreeSlot, 0f, 0.04f, 0.065f));
            game.AddSlot();
            Assert.Null(input.Flush(game, 0.05f));
            Assert.Equal(1, game.Taps);
        }

        [Fact]
        public void Buffer_does_not_hide_real_rejection_or_wait_for_distant_release()
        {
            var game = AlmostFree();
            var input = new ResponsiveInput();
            Assert.False(input.Buffer(game, 1, RejectReason.Locked, 0f, 0.04f, 0.065f));
            Assert.False(input.Buffer(game, 1, RejectReason.NoFreeSlot, 0f, 0.2f, 0.065f));
            game.Step();
            game.TapColumn(2);
            Land(game);
            Assert.False(input.Buffer(game, 1, RejectReason.NoFreeSlot, 0f, 0.04f, 0.065f));
        }

        [Fact]
        public void Buffer_requires_enough_slots_for_the_whole_pair()
        {
            var level = Level(Spool(0, 0, 1), Spool(1, 1, 2), Spool(2, 1, 2), Spool(3, 0, 7));
            level.slots = 2;
            level.spools[1].link = 1;
            level.spools[2].link = 1;
            var game = new LoomGame(level);
            game.TapColumn(0);
            game.TapColumn(3);
            Land(game);
            var input = new ResponsiveInput();
            // Один слот освободится сейчас, второго в пределах 150 мс ещё не будет.
            Assert.False(input.Buffer(game, 1, RejectReason.NoFreeSlot, 0f, 0.04f, 0.065f));
        }

        [Fact]
        public void Preview_matches_first_working_step_and_does_not_change_game()
        {
            var game = new LoomGame(Level(Spool(0, 0, 8, thick: true), Spool(1, 1, 4)));
            var preview = ResponsiveInput.Preview(game, 0);
            Assert.Equal(new[] { 0, 1 }, preview.Slots);
            Assert.Equal(0, game.Taps);
            Assert.Equal(0, game.KnittedCount);
            Assert.False(game.TimerStarted);
            Assert.False(game.CanUndo);
            game.TapColumn(0);
            Land(game);
            Assert.Equal(game.Step().Stitches.Select(s => (s.X, s.Y)), preview.Stitches.Select(s => (s.X, s.Y)));
        }

        [Fact]
        public void Preview_respects_tension_and_does_not_reveal_mystery_rows()
        {
            var level = Level(Spool(0, 0, 8, thick: true), Spool(1, 1, 4));
            level.width = 2;
            level.height = 6;
            level.picture = new[] { "01", "01", "01", "01", "00", "00" };
            level.band = 1;
            level.mystery = true;
            var game = new LoomGame(level);
            var preview = ResponsiveInput.Preview(game, 0);
            Assert.All(preview.Stitches, s => Assert.Equal(game.ActiveFrontY(s.X), s.Y));
            Assert.Empty(ResponsiveInput.Preview(game, -1).Stitches);
        }
    }
}
