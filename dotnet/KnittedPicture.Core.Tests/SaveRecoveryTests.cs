using System;
using System.Text.Json;
using Xunit;

namespace KnittedPicture.Core.Tests
{
    /// <summary>Выбор между основной записью сохранения и резервной копией (SaveStore в Unity берёт решение отсюда).</summary>
    public class SaveRecoveryTests
    {
        private static PlayerProgress? Parse(string json) => JsonSerializer.Deserialize<PlayerProgress>(json, new JsonSerializerOptions { IncludeFields = true });

        private static string Json(int coins) => JsonSerializer.Serialize(new PlayerProgress { coins = coins }, new JsonSerializerOptions { IncludeFields = true });

        [Fact]
        public void Empty_main_record_with_a_readable_backup_is_restored()
        {
            var (outcome, value) = SaveRecovery.Choose(string.Empty, Json(5), Parse);
            Assert.Equal(LoadOutcome.Restored, outcome);
            Assert.Equal(5, value!.coins);
        }

        [Fact]
        public void Both_records_empty_is_a_fresh_start_not_a_loss()
        {
            var (outcome, value) = SaveRecovery.Choose(null, null, Parse);
            Assert.Equal(LoadOutcome.Ok, outcome);
            Assert.Null(value);
            Assert.Equal(LoadOutcome.Ok, SaveRecovery.Choose(string.Empty, "broken", Parse).Outcome);
        }

        [Fact]
        public void Intact_main_record_wins_over_the_backup()
        {
            var (outcome, value) = SaveRecovery.Choose(Json(7), Json(3), Parse);
            Assert.Equal(LoadOutcome.Ok, outcome);
            Assert.Equal(7, value!.coins);
        }

        [Theory]
        [InlineData("{\"coins\": ")]
        [InlineData("not json at all")]
        [InlineData("null")]
        public void Broken_main_record_falls_back_to_the_backup(string broken)
        {
            var (outcome, value) = SaveRecovery.Choose(broken, Json(3), Parse);
            Assert.Equal(LoadOutcome.Restored, outcome);
            Assert.Equal(3, value!.coins);
        }

        [Fact]
        public void Both_records_broken_means_lost_and_a_fresh_start()
        {
            var (outcome, value) = SaveRecovery.Choose("{broken", "also broken", Parse);
            Assert.Equal(LoadOutcome.Lost, outcome);
            Assert.Null(value);
            Assert.Equal(LoadOutcome.Lost, SaveRecovery.Choose("{broken", string.Empty, Parse).Outcome);
        }

        [Fact]
        public void Any_exception_from_the_parser_counts_as_a_broken_record()
        {
            PlayerProgress? Throwing(string json) => throw new InvalidOperationException("любое исключение, не только ArgumentException");
            Assert.Equal(LoadOutcome.Lost, SaveRecovery.Choose("x", "y", Throwing).Outcome);
        }
    }
}
