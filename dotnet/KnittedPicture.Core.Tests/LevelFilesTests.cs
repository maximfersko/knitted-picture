using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace KnittedPicture.Core.Tests
{
    /// <summary>Каждый уровень из Assets/Resources/Levels корректен и проходится без бустеров.</summary>
    public class LevelFilesTests
    {
        private static readonly JsonSerializerOptions Json = new JsonSerializerOptions { IncludeFields = true };

        public static IEnumerable<object[]> LevelFiles() =>
            Directory.GetFiles(LevelsDirectory(), "level_*.json")
                .OrderBy(path => path)
                .Select(path => new object[] { Path.GetFileName(path) });

        [Theory]
        [MemberData(nameof(LevelFiles))]
        public void Level_is_valid_and_solvable(string fileName)
        {
            var level = Load(fileName);

            Assert.Empty(LevelValidator.Validate(level));
            Assert.NotNull(LevelSolver.Solve(level));
        }

        [Fact]
        public void Level_ids_match_file_order()
        {
            var ids = LevelFiles().Select(row => Load((string)row[0]).id).ToList();

            Assert.NotEmpty(ids);
            Assert.Equal(Enumerable.Range(1, ids.Count), ids);
        }

        [Fact]
        public void Solver_reports_unsolvable_level()
        {
            var level = new LevelData
            {
                width = 2,
                height = 2,
                colors = new[] { "#000000", "#FFFFFF" },
                picture = new[] { "01", "10" },
                slots = 1,
                spools = new[]
                {
                    new SpoolData { column = 0, color = 0, loops = 2 },
                    new SpoolData { column = 1, color = 1, loops = 2 },
                },
            };

            Assert.Null(LevelSolver.Solve(level));
        }

        private static LevelData Load(string fileName) =>
            JsonSerializer.Deserialize<LevelData>(File.ReadAllText(Path.Combine(LevelsDirectory(), fileName)), Json)!;

        private static string LevelsDirectory()
        {
            var dir = new DirectoryInfo(System.AppContext.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Assets")))
            {
                dir = dir.Parent;
            }

            return Path.Combine(dir!.FullName, "Assets", "Resources", "Levels");
        }
    }
}
