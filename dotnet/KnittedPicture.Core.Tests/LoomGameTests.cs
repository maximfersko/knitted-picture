using System.Linq;
using Xunit;

namespace KnittedPicture.Core.Tests
{
    public class LoomGameTests
    {
        // 2×2, петли идут снизу вверх, слева направо: 1, 0, 0, 1.
        // Колонка 0 — клубок цвета 0 (2 петли), колонка 1 — цвета 1 (2 петли).
        private static LevelData TwoByTwo(int slots) => new LevelData
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

        [Fact]
        public void Starts_with_empty_loom_and_bottom_left_stitch_needed()
        {
            var game = new LoomGame(TwoByTwo(slots: 2));

            Assert.Equal(GameStatus.Playing, game.Status);
            Assert.Equal(2, game.Slots.Count);
            Assert.All(game.Slots, s => Assert.Null(s));
            Assert.Equal(1, game.NeededColor);
            Assert.Equal(0, game.KnittedCount);
        }

        [Fact]
        public void Tap_moves_front_spool_to_first_free_slot_and_knits_matching_stitches()
        {
            var game = new LoomGame(TwoByTwo(slots: 2));

            var result = game.TapColumn(1);

            Assert.True(result.Accepted);
            Assert.Equal(0, result.Slot);
            Assert.Equal(1, result.Spool!.Color);
            var stitch = Assert.Single(result.Knitted);
            Assert.Equal((0, 1), (stitch.X, stitch.Y));
            Assert.Equal(1, game.Slots[0]!.Loops);
            Assert.Empty(game.Column(1));
            Assert.True(game.IsKnitted(0, 1));
            Assert.False(game.IsKnitted(1, 1));
        }

        [Fact]
        public void Empty_spool_frees_its_slot_and_finished_picture_wins()
        {
            var game = new LoomGame(TwoByTwo(slots: 2));
            game.TapColumn(1);

            var result = game.TapColumn(0);

            Assert.Equal(new[] { (1, 1), (0, 0), (1, 0) }, result.Knitted.Select(s => (s.X, s.Y)));
            Assert.Equal(GameStatus.Won, result.Status);
            Assert.All(game.Slots, s => Assert.Null(s));
            Assert.Equal(-1, game.NeededColor);
        }

        [Fact]
        public void Full_loom_without_needed_color_jams()
        {
            var game = new LoomGame(TwoByTwo(slots: 1));

            var result = game.TapColumn(0);

            Assert.True(result.Accepted);
            Assert.Empty(result.Knitted);
            Assert.Equal(GameStatus.Jammed, game.Status);
            Assert.False(game.TapColumn(1).Accepted);
        }

        [Fact]
        public void Extra_slot_rescues_a_jam()
        {
            var game = new LoomGame(TwoByTwo(slots: 1));
            game.TapColumn(0);

            Assert.True(game.AddSlot());
            Assert.Equal(GameStatus.Playing, game.Status);

            var result = game.TapColumn(1);
            Assert.Equal(GameStatus.Won, result.Status);
        }

        [Fact]
        public void Undo_restores_field_loom_and_progress()
        {
            var game = new LoomGame(TwoByTwo(slots: 2));
            var spoolId = game.Column(1)[0].Id;
            game.TapColumn(1);

            Assert.True(game.Undo());

            Assert.Equal(0, game.KnittedCount);
            Assert.All(game.Slots, s => Assert.Null(s));
            var restored = Assert.Single(game.Column(1));
            Assert.Equal(spoolId, restored.Id);
            Assert.Equal(2, restored.Loops);
            Assert.False(game.CanUndo);
        }

        [Fact]
        public void Undo_keeps_a_bought_slot()
        {
            var game = new LoomGame(TwoByTwo(slots: 1));
            game.TapColumn(0);
            game.AddSlot();

            game.Undo();

            Assert.Equal(GameStatus.Playing, game.Status);
            Assert.Equal(2, game.Slots.Count);
            Assert.Single(game.Column(0));
        }

        [Fact]
        public void Scissors_pull_a_buried_spool()
        {
            var level = TwoByTwo(slots: 2);
            level.spools = new[]
            {
                new SpoolData { column = 0, color = 0, loops = 2 },
                new SpoolData { column = 0, color = 1, loops = 2 },
            };
            var game = new LoomGame(level);

            var result = game.PullSpool(column: 0, depth: 1);

            Assert.True(result.Accepted);
            Assert.Equal(1, result.Spool!.Color);
            Assert.Single(result.Knitted);
            Assert.Equal(0, game.Column(0)[0].Color);
        }

        [Fact]
        public void Tap_on_empty_column_is_rejected_without_history()
        {
            var game = new LoomGame(TwoByTwo(slots: 2));
            game.TapColumn(1);

            var result = game.TapColumn(1);

            Assert.False(result.Accepted);
            Assert.True(game.Undo());
            Assert.False(game.CanUndo);
        }

        [Fact]
        public void Invalid_level_is_rejected()
        {
            var level = TwoByTwo(slots: 2);
            level.spools[0].loops = 3;

            var error = Assert.Throws<System.ArgumentException>(() => new LoomGame(level));
            Assert.Contains("цвет 0", error.Message);
        }
    }
}
