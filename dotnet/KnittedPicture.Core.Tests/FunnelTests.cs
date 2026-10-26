using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace KnittedPicture.Core.Tests
{
    /// <summary>Воронка первых минут: шаги отправляются один раз, маска живёт в сохранении и переживает сброс прогресса.</summary>
    public class FunnelTests
    {
        private static ProgressService Fresh() => new ProgressService(new PlayerProgress(), levelCount: 15);

        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions { IncludeFields = true };

        [Fact]
        public void Step_is_taken_once_and_steps_are_independent()
        {
            var progress = Fresh();
            Assert.False(progress.FunnelSent(FunnelStep.FirstMove));
            Assert.True(progress.TakeFunnel(FunnelStep.FirstMove));
            Assert.True(progress.FunnelSent(FunnelStep.FirstMove));
            Assert.False(progress.TakeFunnel(FunnelStep.FirstMove));

            Assert.False(progress.FunnelSent(FunnelStep.FirstOpen));
            Assert.True(progress.TakeFunnel(FunnelStep.FirstOpen));
            Assert.True(progress.TakeFunnel(FunnelStep.Level10Win));
            Assert.False(progress.TakeFunnel(FunnelStep.FirstOpen));
        }

        [Fact]
        public void Steps_outside_the_mask_are_refused()
        {
            var progress = Fresh();
            Assert.False(progress.TakeFunnel((FunnelStep)31));
            Assert.False(progress.TakeFunnel((FunnelStep)(-1)));
            Assert.False(progress.FunnelSent((FunnelStep)40));
            Assert.Equal(0, progress.Data.funnel);
        }

        [Fact]
        public void Mask_survives_a_json_round_trip()
        {
            var progress = Fresh();
            progress.TakeFunnel(FunnelStep.FirstOpen);
            progress.TakeFunnel(FunnelStep.Level2Win);
            progress.CountLaunch();

            var json = JsonSerializer.Serialize(progress.Data, Options);
            var loaded = new ProgressService(JsonSerializer.Deserialize<PlayerProgress>(json, Options)!, levelCount: 15);

            Assert.True(loaded.FunnelSent(FunnelStep.FirstOpen));
            Assert.True(loaded.FunnelSent(FunnelStep.Level2Win));
            Assert.False(loaded.FunnelSent(FunnelStep.FirstFail));
            Assert.Equal(1, loaded.Data.launches);
        }

        [Fact]
        public void Reset_keeps_funnel_and_launches_so_firsts_are_not_sent_again()
        {
            var old = new PlayerProgress { funnel = 0b101, launches = 7, coins = 500 };
            var fresh = old.FreshKeepingSettings();
            Assert.Equal(0b101, fresh.funnel);
            Assert.Equal(7, fresh.launches);
            Assert.Equal(new PlayerProgress().coins, fresh.coins);
        }

        [Fact]
        public void Launch_counter_counts_up_and_is_repaired_when_negative()
        {
            var progress = Fresh();
            Assert.Equal(1, progress.CountLaunch());
            Assert.Equal(2, progress.CountLaunch());

            var broken = new ProgressService(new PlayerProgress { launches = -4 }, levelCount: 15);
            Assert.Equal(0, broken.Data.launches);
            Assert.Equal(1, broken.CountLaunch());
        }

        [Fact]
        public void Legacy_save_with_finished_levels_sends_no_funnel_steps()
        {
            var legacy = new ProgressService(new PlayerProgress { completed = new List<int> { 1, 2, 3 } }, levelCount: 15);
            Assert.Equal(0, legacy.Data.launches);
            Assert.True(legacy.MarkLegacyFunnel());
            Assert.Equal(1, legacy.CountLaunch());

            foreach (var step in Enum.GetValues(typeof(FunnelStep)).Cast<FunnelStep>())
            {
                Assert.True(legacy.FunnelSent(step), step.ToString());
                Assert.False(legacy.TakeFunnel(step), step.ToString());
            }
        }

        [Fact]
        public void Legacy_save_with_only_the_intro_seen_is_also_marked()
        {
            var legacy = new ProgressService(new PlayerProgress { introSeen = true }, levelCount: 15);
            Assert.True(legacy.MarkLegacyFunnel());
            Assert.False(legacy.TakeFunnel(FunnelStep.FirstOpen));
        }

        [Fact]
        public void New_player_still_sends_every_funnel_step()
        {
            var fresh = Fresh();
            Assert.False(fresh.MarkLegacyFunnel());
            fresh.CountLaunch();
            Assert.Equal(0, fresh.Data.funnel);
            foreach (var step in Enum.GetValues(typeof(FunnelStep)).Cast<FunnelStep>())
            {
                Assert.True(fresh.TakeFunnel(step), step.ToString());
            }
        }

        [Fact]
        public void Migration_is_skipped_once_launches_are_counted()
        {
            var counted = new ProgressService(new PlayerProgress { launches = 3, introSeen = true, completed = new List<int> { 1 } }, levelCount: 15);
            Assert.False(counted.MarkLegacyFunnel());
            Assert.Equal(0, counted.Data.funnel);
        }

        [Fact]
        public void Event_names_are_unique_prefixed_short_and_fit_the_mask()
        {
            var steps = Enum.GetValues(typeof(FunnelStep)).Cast<FunnelStep>().ToList();
            var names = new HashSet<string>();
            foreach (var step in steps)
            {
                var name = step.EventName();
                Assert.StartsWith("ftue_", name);
                Assert.True(name.Length <= 40, name);
                Assert.True((int)step < 31, name);
                Assert.True(names.Add(name), name);
                Assert.Same(name, step.EventName());
            }

            Assert.Equal("ftue_03_first_move", FunnelStep.FirstMove.EventName());
            Assert.Equal("ftue_01_first_open", FunnelStep.FirstOpen.EventName());
            Assert.Equal("ftue_11_level_10_win", FunnelStep.Level10Win.EventName());
        }

        [Fact]
        public void Pattern_attempt_is_always_one_and_does_not_grow()
        {
            var progress = Fresh();
            Assert.Equal(1, progress.NextAttempt(0));
            Assert.Equal(1, progress.NextAttempt(0));
            Assert.Empty(progress.Data.attempts);
            Assert.Equal(1, progress.NextAttempt(1));
            Assert.Equal(2, progress.NextAttempt(1));
        }
    }
}
