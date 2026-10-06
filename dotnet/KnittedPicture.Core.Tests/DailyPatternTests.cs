using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace KnittedPicture.Core.Tests
{
    /// <summary>«Узор дня» строится из любой картинки альбома: корректный, проходимый и одинаковый для всех в этот день.</summary>
    public class DailyPatternTests
    {
        private static readonly JsonSerializerOptions Json = new JsonSerializerOptions { IncludeFields = true };

        private static LevelData[] Levels() =>
            Directory.GetFiles(LevelsDirectory(), "*.json").OrderBy(f => f)
                .Select(f => JsonSerializer.Deserialize<LevelData>(File.ReadAllText(f), Json)!).ToArray();

        [Fact]
        public void Every_picture_makes_a_valid_solvable_pattern()
        {
            var levels = Levels();
            for (var i = 0; i < levels.Length; i++)
            {
                var pattern = DailyPattern.Build(levels[i], 20_000 + i);
                Assert.Empty(LevelValidator.Validate(pattern));
                Assert.Equal(DailyPattern.LevelId, pattern.id);
                Assert.NotNull(LevelSolver.Solve(pattern, 300_000));
            }
        }

        [Fact]
        public void Same_day_same_pattern_next_day_another_picture()
        {
            var levels = Levels();
            var day = 20_500;
            var index = DailyPattern.PictureIndex(day, levels.Length);
            var a = DailyPattern.Build(levels[index], day);
            var b = DailyPattern.Build(levels[index], day);
            Assert.Equal(JsonSerializer.Serialize(a, Json), JsonSerializer.Serialize(b, Json));
            Assert.NotEqual(index, DailyPattern.PictureIndex(day + 1, levels.Length));
        }

        private static string LevelsDirectory()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Assets")))
            {
                dir = dir.Parent;
            }

            return Path.Combine(dir!.FullName, "Assets", "Resources", "Levels");
        }
    }
}
