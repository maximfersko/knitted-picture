using System.Linq;
using Xunit;

namespace KnittedPicture.Core.Tests
{
    /// <summary>Канва, радужные, толстые и ледяные клубки, лишняя пряжа, тапы и комбо за темп.</summary>
    public class CanvasAndSpoolsTests
    {
        // 3×3: ткань цвета 0 вокруг, рисунок — средний столбец цветов 1 (низ) и 2 (верх).
        private static LevelData Framed(params SpoolData[] spools) => new LevelData
        {
            id = 1,
            width = 3,
            height = 3,
            colors = new[] { "#FFFFFF", "#FF0000", "#0000FF" },
            picture = new[] { "000", "020", "010" },
            canvas = new[] { "111", "101", "101" },
            slots = 3,
            spools = spools,
        };

        // 4×2: нижний ряд цвета 0, верхний цвета 1.
        private static LevelData Strip(int slots, params SpoolData[] spools) => new LevelData
        {
            id = 2,
            width = 4,
            height = 2,
            colors = new[] { "#000000", "#FFFFFF" },
            picture = new[] { "1111", "0000" },
            slots = slots,
            spools = spools,
        };

        [Fact]
        public void Canvas_cells_start_knitted_and_only_the_motif_is_knitted()
        {
            var game = new LoomGame(Framed(
                new SpoolData { column = 0, color = 1, loops = 1 },
                new SpoolData { column = 1, color = 2, loops = 1 }));

            Assert.Equal(2, game.TotalStitches);
            Assert.True(game.IsFabric(0, 0));
            Assert.True(game.IsKnitted(0, 2));
            Assert.False(game.IsKnitted(1, 2));
            Assert.Equal(2, game.FrontY(1));
            Assert.Equal(-1, game.FrontY(0));
            Assert.Equal(new[] { 1 }, game.FrontColors().ToArray());

            game.TapColumn(0);
            game.TapColumn(1);
            game.Settle();

            Assert.Equal(GameStatus.Won, game.Status);
        }

        [Fact]
        public void Front_is_the_lowest_motif_cell_so_columns_front_at_different_rows()
        {
            var level = new LevelData
            {
                width = 2,
                height = 3,
                colors = new[] { "#FFFFFF", "#FF0000" },
                picture = new[] { "11", "10", "00" },
                canvas = new[] { "00", "01", "11" },
                slots = 2,
                spools = new[] { new SpoolData { column = 0, color = 1, loops = 3 } },
            };
            var game = new LoomGame(level);

            Assert.Equal(1, game.FrontY(0));
            Assert.Equal(0, game.FrontY(1));

            game.TapColumn(0);
            var stitches = Enumerable.Range(0, LoomGame.WarmupSteps + 3).SelectMany(_ => game.Step().Stitches).ToList();

            // Самый низкий фронт первым: (0,1), потом (0,0) и (1,0) — при равенстве левый.
            Assert.Equal(new[] { (0, 1), (0, 0), (1, 0) }, stitches.Select(s => (s.X, s.Y)).ToArray());
        }

        [Fact]
        public void Surplus_yarn_leaves_the_loom_when_its_color_is_done()
        {
            var game = new LoomGame(Strip(2,
                new SpoolData { column = 0, color = 0, loops = 3 },
                new SpoolData { column = 0, color = 0, loops = 3 },
                new SpoolData { column = 1, color = 1, loops = 4 }));

            game.TapColumn(0);
            game.TapColumn(0);
            var completions = Enumerable.Range(0, 12).SelectMany(_ => game.Step().Completed).ToList();

            Assert.Equal(4, game.KnittedCount);
            Assert.Equal(2, completions.Sum(c => c.Leftover));
            Assert.Equal(2, game.FreeSlots);
            Assert.Equal(GameStatus.Playing, game.Status);
        }

        [Fact]
        public void Rainbow_spool_knits_whatever_is_on_the_front()
        {
            var game = new LoomGame(Strip(1, new SpoolData { column = 0, color = 0, loops = 8, rainbow = true }));

            game.TapColumn(0);
            game.Settle();

            Assert.Equal(GameStatus.Won, game.Status);
        }

        [Fact]
        public void Thick_spool_takes_two_slots_and_knits_two_loops_a_step()
        {
            var game = new LoomGame(Strip(3,
                new SpoolData { column = 0, color = 0, loops = 4, thick = true },
                new SpoolData { column = 1, color = 1, loops = 4 }));

            var result = game.TapColumn(0);

            Assert.Equal(0, result.Placed[0].Slot);
            Assert.Equal(1, result.Placed[0].TailSlot);
            Assert.True(game.IsTail(1));
            Assert.Equal(1, game.FreeSlots);

            for (var i = 0; i < LoomGame.WarmupSteps; i++)
            {
                game.Step();
            }

            Assert.Equal(2, game.Step().Stitches.Count);
            var done = game.Step().Completed.Single();
            Assert.Equal(1, done.TailSlot);
            Assert.Equal(3, game.FreeSlots);
        }

        [Fact]
        public void Thick_spool_needs_two_free_slots()
        {
            var game = new LoomGame(Strip(2,
                new SpoolData { column = 0, color = 1, loops = 4 },
                new SpoolData { column = 1, color = 0, loops = 4, thick = true }));

            game.TapColumn(0);

            Assert.Equal(RejectReason.NoFreeSlot, game.TapColumn(1).Reason);
        }

        [Fact]
        public void Thick_spool_survives_undo_as_one_spool()
        {
            var game = new LoomGame(Strip(3,
                new SpoolData { column = 0, color = 0, loops = 4, thick = true },
                new SpoolData { column = 1, color = 1, loops = 4 }));
            game.TapColumn(0);
            game.TapColumn(1);

            game.Undo();

            Assert.Same(game.Slots[0], game.Slots[1]);
            game.TapColumn(1);
            game.Settle();
            Assert.Equal(GameStatus.Won, game.Status);
        }

        [Fact]
        public void Ice_spool_thaws_after_loops_of_its_color()
        {
            var game = new LoomGame(Strip(2,
                new SpoolData { column = 0, color = 0, loops = 4 },
                new SpoolData { column = 1, color = 1, loops = 4, ice = 3 }));

            Assert.Equal(RejectReason.Frozen, game.TapColumn(1).Reason);

            game.TapColumn(0);
            var thawed = Enumerable.Range(0, 10).SelectMany(_ => game.Step().Thawed).ToList();

            Assert.Empty(thawed);
            Assert.Equal(GameStatus.Jammed, game.Status);
        }

        [Fact]
        public void Ice_counts_down_with_stitches_of_the_same_color()
        {
            var game = new LoomGame(Strip(2,
                new SpoolData { column = 0, color = 1, loops = 2 },
                new SpoolData { column = 0, color = 0, loops = 4 },
                new SpoolData { column = 1, color = 1, loops = 2, ice = 2 }));

            game.TapColumn(0);
            game.TapColumn(0);
            var thawed = Enumerable.Range(0, 12).SelectMany(_ => game.Step().Thawed).ToList();

            Assert.Single(thawed);
            Assert.True(game.CanTap(1));
            game.TapColumn(1);
            game.Settle();
            Assert.Equal(GameStatus.Won, game.Status);
        }

        [Fact]
        public void Scissors_cut_the_ice()
        {
            var game = new LoomGame(Strip(2,
                new SpoolData { column = 0, color = 0, loops = 4 },
                new SpoolData { column = 1, color = 1, loops = 4, ice = 3 }));

            Assert.True(game.PullSpool(1, 0).Accepted);
        }

        [Fact]
        public void Taps_count_every_accepted_action_and_undo_rolls_them_back()
        {
            var game = new LoomGame(Strip(2,
                new SpoolData { column = 0, color = 0, loops = 4, knots = 1 },
                new SpoolData { column = 1, color = 1, loops = 4 }));

            game.TapColumn(0);
            game.TapColumn(0);
            game.TapColumn(1);
            Assert.Equal(3, game.Taps);

            game.Undo();
            Assert.Equal(2, game.Taps);
        }

        [Fact]
        public void Fewest_taps_skips_spools_that_are_not_needed()
        {
            var level = Strip(2,
                new SpoolData { column = 0, color = 0, loops = 4 },
                new SpoolData { column = 1, color = 0, loops = 2 },
                new SpoolData { column = 1, color = 1, loops = 4 },
                new SpoolData { column = 2, color = 1, loops = 4 });

            Assert.Equal(2, LevelSolver.FewestTaps(level));
        }

        [Fact]
        public void Validator_allows_surplus_but_not_shortage_and_checks_canvas()
        {
            var surplus = Strip(2,
                new SpoolData { column = 0, color = 0, loops = 6 },
                new SpoolData { column = 1, color = 1, loops = 4 });
            Assert.Empty(LevelValidator.Validate(surplus));

            var shortage = Strip(2,
                new SpoolData { column = 0, color = 0, loops = 3 },
                new SpoolData { column = 1, color = 1, loops = 4 });
            Assert.NotEmpty(LevelValidator.Validate(shortage));

            var rainbowCovers = Strip(2,
                new SpoolData { column = 0, color = 0, loops = 3 },
                new SpoolData { column = 0, color = 0, loops = 1, rainbow = true },
                new SpoolData { column = 1, color = 1, loops = 4 });
            Assert.Empty(LevelValidator.Validate(rainbowCovers));

            var badCanvas = Framed(new SpoolData { column = 0, color = 1, loops = 1 }, new SpoolData { column = 1, color = 2, loops = 1 });
            badCanvas.canvas = new[] { "11", "101", "101" };
            Assert.NotEmpty(LevelValidator.Validate(badCanvas));

            var goldOnFabric = Framed(new SpoolData { column = 0, color = 1, loops = 1 }, new SpoolData { column = 1, color = 2, loops = 1 });
            goldOnFabric.gold = new[] { 0 };
            Assert.NotEmpty(LevelValidator.Validate(goldOnFabric));

            var neverThaws = Strip(2,
                new SpoolData { column = 0, color = 0, loops = 4 },
                new SpoolData { column = 1, color = 1, loops = 4, ice = 5 });
            Assert.NotEmpty(LevelValidator.Validate(neverThaws));
        }

        [Fact]
        public void Foreign_yarn_stays_on_the_loom_until_cut()
        {
            var game = new LoomGame(Framed(
                new SpoolData { column = 0, color = 0, loops = 3 },
                new SpoolData { column = 1, color = 1, loops = 1 },
                new SpoolData { column = 2, color = 2, loops = 1 }));
            var foreign = game.Column(0)[0];
            Assert.True(game.IsForeign(foreign));

            game.TapColumn(0);
            game.Settle();
            Assert.Same(foreign, game.Slots[0]);

            Assert.True(game.CutFromLoom(0));
            Assert.Null(game.Slots[0]);
            game.TapColumn(1);
            game.TapColumn(2);
            game.Settle();
            Assert.Equal(GameStatus.Won, game.Status);
        }

        [Fact]
        public void Foreign_yarn_can_jam_the_loom_and_cutting_it_unjams()
        {
            var level = Framed(
                new SpoolData { column = 0, color = 0, loops = 3 },
                new SpoolData { column = 1, color = 1, loops = 1 },
                new SpoolData { column = 2, color = 2, loops = 1 });
            level.slots = 1;
            var game = new LoomGame(level);

            game.TapColumn(0);
            game.Settle();
            Assert.Equal(GameStatus.Jammed, game.Status);

            Assert.True(game.CutFromLoom(0));
            Assert.Equal(GameStatus.Playing, game.Status);
        }

        [Fact]
        public void Band_keeps_columns_within_rows_of_the_lowest_front()
        {
            // 2×4: левый столбец весь цвета 0, правый — цвета 1. С натяжением 1 левый
            // не может уйти вверх больше чем на ряд, пока правый стоит.
            var level = new LevelData
            {
                width = 2,
                height = 4,
                colors = new[] { "#000000", "#FFFFFF" },
                picture = new[] { "01", "01", "01", "01" },
                band = 1,
                slots = 2,
                spools = new[]
                {
                    new SpoolData { column = 0, color = 0, loops = 4 },
                    new SpoolData { column = 1, color = 1, loops = 4 },
                },
            };
            var game = new LoomGame(level);

            game.TapColumn(0);
            game.Settle();

            Assert.Equal(2, game.ColumnHeight(0));
            Assert.Equal(-1, game.ActiveFrontY(0));
            Assert.Equal(GameStatus.Playing, game.Status);

            // Для пунктира на картинке: левый столбец ждёт, правый держит его, отстающий ряд — нижний.
            Assert.True(game.IsHeld(0));
            Assert.False(game.IsHeld(1));
            Assert.Equal(3, game.LowestFront);

            game.TapColumn(1);
            game.Settle();
            Assert.Equal(GameStatus.Won, game.Status);
        }

        [Fact]
        public void Tempo_combo_survives_a_player_who_thinks_a_second_and_a_half()
        {
            // Станок довязывает клубок за полсекунды, дальше ждёт игрока: серия не должна сгорать за обычное раздумье.
            var combo = new TempoCombo();
            for (var i = 0; i < TempoCombo.RushFrom; i++)
            {
                combo.OnPlaced();
                Assert.False(combo.Update(1.5f, working: false));
            }

            Assert.True(combo.Rush);
        }

        [Fact]
        public void Tempo_combo_grows_while_the_loom_works_and_burns_after_idle()
        {
            var combo = new TempoCombo();
            for (var i = 0; i < TempoCombo.RushFrom; i++)
            {
                combo.OnPlaced();
                Assert.False(combo.Update(0.5f, working: true));
            }

            Assert.True(combo.Rush);
            var coins = Enumerable.Range(0, TempoCombo.RushStitchesPerCoin * 2).Count(_ => combo.OnStitch());
            Assert.Equal(2, coins);
            Assert.Equal(2, combo.RushCoins);

            Assert.False(combo.Update(TempoCombo.Grace * 0.6f, working: false));
            Assert.True(combo.Update(TempoCombo.Grace * 0.6f, working: false));
            Assert.Equal(0, combo.Combo);
            Assert.False(combo.Rush);
            Assert.Equal(TempoCombo.RushFrom, combo.Best);
        }

        [Fact]
        public void Stars_drop_for_boosters_and_for_rescues()
        {
            Assert.Equal(3, Economy.Stars(0, 0));
            Assert.Equal(2, Economy.Stars(1, 0));
            Assert.Equal(1, Economy.Stars(0, 1));
            Assert.Equal(1, Economy.Stars(2, 1));
        }
    }
}
