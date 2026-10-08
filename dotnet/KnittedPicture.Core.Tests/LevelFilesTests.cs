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

        /// <summary>
        /// Цвета клубков и рисунка в одном уровне должны легко различаться: игрок сравнивает клубок
        /// с клеткой. Иначе близкие зелёные или коричневые путаются (фидбэк тестировщика), а чужая
        /// пряжа цвета ткани сливается с белым рисунком. Разница — ΔE в Lab (CIE76), не меньше 18.
        /// </summary>
        [Theory]
        [MemberData(nameof(LevelFiles))]
        public void Colors_in_a_level_are_easy_to_tell_apart(string fileName)
        {
            var level = Load(fileName);
            var used = new HashSet<int>(level.spools.Where(s => !s.rainbow).Select(s => s.color));
            for (var y = 0; y < level.height; y++)
            {
                for (var x = 0; x < level.width; x++)
                {
                    var fabric = level.canvas.Length == level.height && level.canvas[y][x] == '1';
                    if (!fabric)
                    {
                        used.Add(level.picture[y][x] - '0');
                    }
                }
            }

            var colors = used.OrderBy(c => c).ToList();
            for (var i = 0; i < colors.Count; i++)
            {
                for (var j = i + 1; j < colors.Count; j++)
                {
                    var distance = DeltaE(level.colors[colors[i]], level.colors[colors[j]]);
                    Assert.True(distance >= 18.0, $"{level.colors[colors[i]]} и {level.colors[colors[j]]}: ΔE {distance:0.0}");
                }
            }
        }

        [Fact]
        public void Level_ids_match_file_order()
        {
            var ids = LevelFiles().Select(row => Load((string)row[0]).id).ToList();

            Assert.NotEmpty(ids);
            Assert.Equal(Enumerable.Range(1, ids.Count), ids);
        }

        [Fact]
        public void Collections_cover_every_level_without_gaps()
        {
            var count = LevelFiles().Count();
            var expected = 1;
            foreach (var collection in Collections.All)
            {
                Assert.Equal(expected, collection.FirstLevel);
                Assert.True(collection.LastLevel >= collection.FirstLevel);
                expected = collection.LastLevel + 1;
            }

            Assert.Equal(count, expected - 1);
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

        private static double DeltaE(string a, string b)
        {
            var p = Lab(a);
            var q = Lab(b);
            return System.Math.Sqrt((p.L - q.L) * (p.L - q.L) + (p.A - q.A) * (p.A - q.A) + (p.B - q.B) * (p.B - q.B));
        }

        private static (double L, double A, double B) Lab(string hex)
        {
            double Channel(int i)
            {
                var c = System.Convert.ToInt32(hex.Substring(i, 2), 16) / 255.0;
                return c <= 0.04045 ? c / 12.92 : System.Math.Pow((c + 0.055) / 1.055, 2.4);
            }

            double F(double t) => t > 0.008856 ? System.Math.Pow(t, 1.0 / 3.0) : 7.787 * t + 16.0 / 116.0;
            var r = Channel(1);
            var g = Channel(3);
            var b = Channel(5);
            var x = F((0.4124 * r + 0.3576 * g + 0.1805 * b) / 0.95047);
            var y = F(0.2126 * r + 0.7152 * g + 0.0722 * b);
            var z = F((0.0193 * r + 0.1192 * g + 0.9505 * b) / 1.08883);
            return (116 * y - 16, 500 * (x - y), 200 * (y - z));
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
