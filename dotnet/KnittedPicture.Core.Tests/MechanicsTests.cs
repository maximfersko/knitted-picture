using System.Linq;
using Xunit;

namespace KnittedPicture.Core.Tests
{
    /// <summary>Особые клубки, ножницы, моль и подсказка.</summary>
    public class MechanicsTests
    {
        // 4×2: нижний ряд цвета 0, верхний цвета 1.
        private static LevelData Strip(params SpoolData[] spools) => new LevelData
        {
            id = 1,
            width = 4,
            height = 2,
            colors = new[] { "#000000", "#FFFFFF" },
            picture = new[] { "1111", "0000" },
            slots = 3,
            spools = spools,
        };

        [Fact]
        public void Scissors_get_out_of_a_jam_that_an_extra_slot_cannot_fix()
        {
            // Лёд на клубке цвета 1 тает только от петель цвета 1, а их вяжет лишь он сам: тупик.
            var game = new LoomGame(Strip(
                new SpoolData { column = 0, color = 0, loops = 4 },
                new SpoolData { column = 1, color = 1, loops = 4, ice = 4 }));
            game.TapColumn(0);
            game.Settle();

            Assert.Equal(GameStatus.Jammed, game.Status);
            Assert.False(game.SlotWouldHelp);
            Assert.True(game.ScissorsWouldHelp);
            Assert.False(game.TapColumn(1).Accepted);

            Assert.True(game.PullSpool(1, 0).Accepted);
            Assert.Equal(GameStatus.Playing, game.Status);
            game.Settle();
            Assert.Equal(GameStatus.Won, game.Status);
        }

        [Fact]
        public void Extra_slot_helps_a_full_loom_jam()
        {
            var game = new LoomGame(new LevelData
            {
                id = 98,
                width = 2,
                height = 4,
                colors = new[] { "#000000", "#FFFFFF" },
                picture = new[] { "10", "01", "10", "01" },
                slots = 1,
                spools = new[]
                {
                    new SpoolData { column = 0, color = 0, loops = 4 },
                    new SpoolData { column = 1, color = 1, loops = 4 },
                },
            });
            game.TapColumn(0);
            game.Settle();

            Assert.Equal(GameStatus.Jammed, game.Status);
            Assert.True(game.SlotWouldHelp);
            Assert.True(game.ScissorsWouldHelp);
        }

        [Fact]
        public void Hidden_spool_reveals_when_it_reaches_the_front()
        {
            var game = new LoomGame(Strip(
                new SpoolData { column = 0, color = 0, loops = 4 },
                new SpoolData { column = 0, color = 1, loops = 4, hidden = true }));
            var hidden = game.Column(0)[1];
            Assert.False(hidden.IsColorVisible);

            var result = game.TapColumn(0);

            Assert.Equal(new[] { hidden.Id }, result.Revealed);
            Assert.True(game.Column(0)[0].IsColorVisible);
        }

        [Fact]
        public void Hidden_spool_at_the_front_is_visible_from_the_start()
        {
            var game = new LoomGame(Strip(
                new SpoolData { column = 0, color = 0, loops = 4, hidden = true },
                new SpoolData { column = 1, color = 1, loops = 4 }));

            Assert.True(game.Column(0)[0].IsColorVisible);
        }

        [Fact]
        public void Knot_takes_an_extra_tap()
        {
            var game = new LoomGame(Strip(
                new SpoolData { column = 0, color = 0, loops = 4, knots = 1 },
                new SpoolData { column = 1, color = 1, loops = 4 }));

            var first = game.TapColumn(0);
            Assert.Equal(MoveKind.KnotUntied, first.Kind);
            Assert.Single(game.Column(0));
            Assert.Equal(0, game.Column(0)[0].Knots);

            Assert.Equal(MoveKind.Placed, game.TapColumn(0).Kind);
            game.Settle();
            Assert.Equal(4, game.KnittedCount);
        }

        [Fact]
        public void Linked_spools_travel_together()
        {
            var game = new LoomGame(Strip(
                new SpoolData { column = 0, color = 0, loops = 4, link = 1 },
                new SpoolData { column = 1, color = 1, loops = 4, link = 1 }));

            var result = game.TapColumn(1);
            game.Settle();

            Assert.Equal(2, result.Placed.Count);
            Assert.Equal(GameStatus.Won, game.Status);
        }

        [Fact]
        public void Linked_spool_waits_for_its_partner_to_reach_the_front()
        {
            var game = new LoomGame(Strip(
                new SpoolData { column = 0, color = 0, loops = 2, link = 1 },
                new SpoolData { column = 1, color = 0, loops = 2 },
                new SpoolData { column = 1, color = 1, loops = 4, link = 1 }));

            Assert.Equal(RejectReason.LinkNotReady, game.TapColumn(0).Reason);
            game.TapColumn(1);
            game.Settle();
            Assert.Equal(MoveKind.Placed, game.TapColumn(0).Kind);
        }

        [Fact]
        public void Linked_pair_needs_two_free_slots_or_the_loom_jams()
        {
            var level = Strip(
                new SpoolData { column = 0, color = 1, loops = 4 },
                new SpoolData { column = 1, color = 0, loops = 2, link = 1 },
                new SpoolData { column = 2, color = 0, loops = 2, link = 1 });
            level.slots = 2;
            var game = new LoomGame(level);

            game.TapColumn(0);
            game.Settle();

            Assert.Equal(1, game.FreeSlots);
            Assert.Equal(GameStatus.Jammed, game.Status);

            game.AddSlot();
            Assert.Equal(GameStatus.Playing, game.Status);
            Assert.Equal(2, game.TapColumn(1).Placed.Count);
            game.Settle();
            Assert.Equal(GameStatus.Won, game.Status);
        }

        [Fact]
        public void Key_opens_locks_when_it_reaches_the_loom()
        {
            var game = new LoomGame(Strip(
                new SpoolData { column = 0, color = 1, loops = 4, locked = true },
                new SpoolData { column = 1, color = 0, loops = 4, key = true }));

            Assert.Equal(RejectReason.Locked, game.TapColumn(0).Reason);
            var result = game.TapColumn(1);

            Assert.Single(result.Unlocked);
            Assert.False(game.Column(0)[0].Locked);
            Assert.Equal(MoveKind.Placed, game.TapColumn(0).Kind);
        }

        [Fact]
        public void Scissors_take_the_chosen_spool_alone()
        {
            var game = new LoomGame(Strip(
                new SpoolData { column = 0, color = 0, loops = 4 },
                new SpoolData { column = 0, color = 1, loops = 4, knots = 2, hidden = true }));

            var result = game.PullSpool(column: 0, depth: 1);

            var placed = Assert.Single(result.Placed);
            Assert.Equal(1, placed.Spool.Color);
            Assert.Equal(0, placed.Spool.Knots);
            Assert.True(placed.Spool.IsColorVisible);
            Assert.Single(game.Column(0));
        }

        [Fact]
        public void Scissors_cut_a_link()
        {
            var game = new LoomGame(Strip(
                new SpoolData { column = 0, color = 0, loops = 4, link = 3 },
                new SpoolData { column = 1, color = 1, loops = 4, link = 3 }));

            var result = game.PullSpool(column: 0, depth: 0);

            Assert.Single(result.Placed);
            Assert.Equal(0, game.Column(1)[0].Link);
            Assert.Equal(MoveKind.Placed, game.TapColumn(1).Kind);
        }

        [Fact]
        public void Moth_timer_starts_on_first_action_and_ends_the_game()
        {
            var level = Strip(
                new SpoolData { column = 0, color = 0, loops = 4 },
                new SpoolData { column = 1, color = 1, loops = 4 });
            level.timeLimit = 10;
            var game = new LoomGame(level);

            game.Tick(30);
            Assert.Equal(10f, game.TimeLeft);

            game.TapColumn(0);
            game.Tick(4);
            Assert.Equal(6f, game.TimeLeft);

            game.Tick(10);
            Assert.Equal(GameStatus.TimeUp, game.Status);
            Assert.False(game.CanUndo);
            Assert.Equal(RejectReason.GameOver, game.TapColumn(1).Reason);
            Assert.Empty(game.Step().Stitches);

            game.AddTime(Economy.TimeBonusSeconds);
            Assert.Equal(GameStatus.Playing, game.Status);
            game.TapColumn(1);
            game.Settle();
            Assert.Equal(GameStatus.Won, game.Status);
        }

        [Fact]
        public void Solver_handles_links_knots_and_locks()
        {
            var level = Strip(
                new SpoolData { column = 0, color = 1, loops = 4, locked = true },
                new SpoolData { column = 1, color = 0, loops = 2, knots = 1, key = true },
                new SpoolData { column = 2, color = 0, loops = 2 });

            var path = LevelSolver.Solve(level);

            Assert.NotNull(path);
            var game = new LoomGame(level);
            foreach (var column in path!)
            {
                Assert.True(game.TapColumn(column).Accepted);
                game.Settle();
            }

            Assert.Equal(GameStatus.Won, game.Status);
        }

        [Fact]
        public void Validator_rejects_locks_without_key()
        {
            var level = Strip(
                new SpoolData { column = 0, color = 1, loops = 4, locked = true },
                new SpoolData { column = 1, color = 0, loops = 4 });

            Assert.Contains(LevelValidator.Validate(level), e => e.Contains("ключа"));
        }

        [Fact]
        public void Validator_rejects_key_behind_a_lock()
        {
            var level = Strip(
                new SpoolData { column = 0, color = 1, loops = 4, locked = true },
                new SpoolData { column = 0, color = 0, loops = 4, key = true });

            Assert.Contains(LevelValidator.Validate(level), e => e.Contains("за замком"));
        }

        [Fact]
        public void Validator_rejects_broken_links()
        {
            var level = Strip(
                new SpoolData { column = 0, color = 1, loops = 4, link = 2 },
                new SpoolData { column = 0, color = 0, loops = 4, link = 2 });

            Assert.Contains(LevelValidator.Validate(level), e => e.Contains("одной колонке"));
        }

        [Fact]
        public void Hint_points_to_a_winning_column()
        {
            var level = Strip(
                new SpoolData { column = 0, color = 1, loops = 4 },
                new SpoolData { column = 1, color = 0, loops = 4 });
            level.slots = 1;
            var game = new LoomGame(level);

            Assert.Equal(1, LevelSolver.Hint(game));
            Assert.Equal(0, game.KnittedCount);
            Assert.Equal(new[] { 1, 0 }, LevelSolver.Solve(game)!.ToArray());
        }
    }
}
