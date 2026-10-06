using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace KnittedPicture.Core.Tests
{
    public class LoomGameTests
    {
        // 2×2. Столбец 0 снизу вверх: 1, 0. Столбец 1 снизу вверх: 0, 1.
        // Колонка поля 0 — клубок цвета 0 (2 петли), колонка 1 — цвета 1 (2 петли).
        internal static LevelData TwoByTwo(int slots) => new LevelData
        {
            id = 99,
            width = 2,
            height = 2,
            colors = new[] { "#6CB8FF", "#FF6FA5" },
            picture = new[] { "01", "10" },
            slots = slots,
            spools = new[]
            {
                new SpoolData { column = 0, color = 0, loops = 2 },
                new SpoolData { column = 1, color = 1, loops = 2 },
            },
        };

        // 2×4: в каждом столбце цвета чередуются — один клубок без другого застрянет.
        private static LevelData Zebra(int slots) => new LevelData
        {
            id = 98,
            width = 2,
            height = 4,
            colors = new[] { "#000000", "#FFFFFF" },
            picture = new[] { "10", "01", "10", "01" },
            slots = slots,
            spools = new[]
            {
                new SpoolData { column = 0, color = 0, loops = 4 },
                new SpoolData { column = 1, color = 1, loops = 4 },
            },
        };

        private static List<Completion> SettleCollecting(LoomGame game)
        {
            var completions = new List<Completion>();
            while (game.Status == GameStatus.Playing)
            {
                var step = game.Step();
                completions.AddRange(step.Completed);
                if (!step.Progress)
                {
                    break;
                }
            }

            return completions;
        }

        [Fact]
        public void Starts_with_empty_loom_and_every_column_front_open()
        {
            var game = new LoomGame(TwoByTwo(slots: 2));

            Assert.Equal(GameStatus.Playing, game.Status);
            Assert.All(game.Slots, s => Assert.Null(s));
            Assert.Equal(new HashSet<int> { 0, 1 }, game.FrontColors());
            Assert.Equal(1, game.FrontY(0));
            Assert.Equal(0, game.KnittedCount);
        }

        [Fact]
        public void Tap_places_spool_which_starts_knitting_after_warmup()
        {
            var game = new LoomGame(TwoByTwo(slots: 2));

            var result = game.TapColumn(1);

            Assert.Equal(MoveKind.Placed, result.Kind);
            var placed = Assert.Single(result.Placed);
            Assert.Equal(0, placed.Slot);
            Assert.Equal(0, game.KnittedCount);
            for (var i = 0; i < LoomGame.WarmupSteps; i++)
            {
                var warm = game.Step();
                Assert.True(warm.Progress);
                Assert.Empty(warm.Stitches);
            }

            var stitch = Assert.Single(game.Step().Stitches);
            Assert.Equal((0, 1, 1), (stitch.X, stitch.Y, stitch.Color));
            Assert.True(game.IsKnitted(0, 1));
        }

        [Fact]
        public void Spools_on_the_loom_knit_in_parallel()
        {
            var game = new LoomGame(TwoByTwo(slots: 2));
            game.TapColumn(1);
            game.TapColumn(0);
            for (var i = 0; i < LoomGame.WarmupSteps; i++)
            {
                game.Step();
            }

            var step = game.Step();

            Assert.Equal(2, step.Stitches.Count);
        }

        [Fact]
        public void Spools_open_cells_for_each_other()
        {
            var alone = new LoomGame(Zebra(slots: 2));
            alone.TapColumn(0);
            alone.Settle();
            Assert.Equal(1, alone.KnittedCount);
            Assert.True(alone.Slots[0]!.Stalled);

            var together = new LoomGame(Zebra(slots: 2));
            together.TapColumn(0);
            together.TapColumn(1);
            together.Settle();
            Assert.Equal(GameStatus.Won, together.Status);
        }

        [Fact]
        public void Perfect_spool_is_one_that_never_waited()
        {
            var game = new LoomGame(TwoByTwo(slots: 2));
            game.TapColumn(1);
            var first = SettleCollecting(game);
            Assert.Empty(first);

            game.TapColumn(0);
            var completions = SettleCollecting(game);

            Assert.Equal(GameStatus.Won, game.Status);
            Assert.Contains(completions, c => c.Perfect);
            Assert.Contains(completions, c => !c.Perfect);
        }

        [Fact]
        public void Full_loom_without_front_colors_jams()
        {
            var game = new LoomGame(TwoByTwo(slots: 1));

            game.TapColumn(0);
            game.Settle();

            Assert.Equal(1, game.KnittedCount);
            Assert.Equal(GameStatus.Jammed, game.Status);
            Assert.Equal(RejectReason.GameOver, game.TapColumn(1).Reason);
        }

        [Fact]
        public void Extra_slot_rescues_a_jam()
        {
            var game = new LoomGame(TwoByTwo(slots: 1));
            game.TapColumn(0);
            game.Settle();

            Assert.True(game.AddSlot());
            Assert.Equal(GameStatus.Playing, game.Status);

            game.TapColumn(1);
            game.Settle();
            Assert.Equal(GameStatus.Won, game.Status);
        }

        [Fact]
        public void Slots_stop_at_the_maximum()
        {
            var game = new LoomGame(TwoByTwo(slots: LoomGame.MaxSlots));

            Assert.False(game.AddSlot());
            Assert.Equal(LoomGame.MaxSlots, game.Slots.Count);
        }

        [Fact]
        public void Undo_rewinds_the_move_and_everything_knitted_after_it()
        {
            var game = new LoomGame(TwoByTwo(slots: 2));
            var spoolId = game.Column(1)[0].Id;
            game.TapColumn(1);
            game.Settle();
            Assert.Equal(1, game.KnittedCount);

            Assert.True(game.Undo());

            Assert.Equal(0, game.KnittedCount);
            Assert.All(game.Slots, s => Assert.Null(s));
            var restored = Assert.Single(game.Column(1));
            Assert.Equal(spoolId, restored.Id);
            Assert.Equal(2, restored.Loops);
            Assert.False(game.CanUndo);
        }

        [Fact]
        public void Undo_leaves_a_jam_and_keeps_a_bought_slot()
        {
            var game = new LoomGame(TwoByTwo(slots: 1));
            game.TapColumn(0);
            game.Settle();
            game.AddSlot();

            game.Undo();

            Assert.Equal(GameStatus.Playing, game.Status);
            Assert.Equal(2, game.Slots.Count);
            Assert.Single(game.Column(0));
        }

        [Fact]
        public void Rejected_tap_leaves_no_history()
        {
            var game = new LoomGame(TwoByTwo(slots: 2));
            game.TapColumn(1);

            var result = game.TapColumn(1);

            Assert.Equal(RejectReason.EmptyColumn, result.Reason);
            Assert.True(game.Undo());
            Assert.False(game.CanUndo);
        }

        [Fact]
        public void Clone_is_independent()
        {
            var game = new LoomGame(TwoByTwo(slots: 2));
            var copy = game.Clone();

            copy.TapColumn(1);
            copy.Settle();

            Assert.Equal(0, game.KnittedCount);
            Assert.Single(game.Column(1));
            Assert.Equal(1, copy.KnittedCount);
        }

        [Fact]
        public void Invalid_level_is_rejected()
        {
            var level = TwoByTwo(slots: 2);
            level.spools[0].loops = 1;

            var error = Assert.Throws<System.ArgumentException>(() => new LoomGame(level));
            Assert.Contains("цвет 0", error.Message);
        }
    }
}
