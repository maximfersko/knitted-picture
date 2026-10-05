using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using KnittedPicture.Core;

namespace KnittedPicture.LevelTool
{
    /// <summary>
    /// dotnet run --project dotnet/KnittedPicture.LevelTool [-- levels-src Assets/Resources/Levels]
    /// Для каждого исходника перебирает сиды, пока уровень не станет решаемым
    /// и бот не будет выигрывать в нужной доле партий.
    /// </summary>
    public static class Program
    {
        private const int BotRuns = 200;
        private const int MaxSeeds = 400;

        private static readonly JsonSerializerOptions ReadOptions = new JsonSerializerOptions
        {
            IncludeFields = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
        };

        private static readonly JsonSerializerOptions WriteOptions = new JsonSerializerOptions
        {
            IncludeFields = true,
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        };

        public static int Main(string[] args)
        {
            var root = FindRoot();
            var source = args.Length > 0 ? args[0] : Path.Combine(root, "levels-src");
            var output = args.Length > 1 ? args[1] : Path.Combine(root, "Assets", "Resources", "Levels");
            Directory.CreateDirectory(output);

            var failed = false;
            Console.WriteLine("id  название           размер цв клуб кол сл  ходов  бот   сид   механики");
            foreach (var path in Directory.GetFiles(source, "level_*.json").OrderBy(p => p))
            {
                var design = JsonSerializer.Deserialize<LevelDesign>(File.ReadAllText(path), ReadOptions)!;
                var (level, solution, winRate, seed, inRange) = Build(design);
                if (level == null)
                {
                    Console.WriteLine($"{design.id,2}  {design.name}: не нашлось решаемого поля");
                    failed = true;
                    continue;
                }

                var file = Path.Combine(output, $"level_{design.id:000}.json");
                File.WriteAllText(file, JsonSerializer.Serialize(level, WriteOptions) + "\n");

                var mechanics = string.Join(" ", new[]
                {
                    design.hidden > 0 ? $"скрытые×{design.hidden}" : null,
                    design.knots > 0 ? $"узелки×{design.knots}" : null,
                    design.links > 0 ? $"связки×{design.links}" : null,
                    design.locks > 0 ? $"замки×{design.locks}" : null,
                    design.timeLimit > 0 ? $"моль {design.timeLimit}с" : null,
                    design.hard ? "сложный" : null,
                }.Where(s => s != null));
                var mark = inRange ? " " : "!";
                Console.WriteLine(
                    $"{design.id,2}  {design.name,-18} {level.width,2}×{level.height,-2} {level.colors.Length,2} {level.spools.Length,4} {design.columns,3} {level.slots,2} {solution,5}  {winRate,4:P0}{mark} {seed,5}   {mechanics}");
            }

            return failed ? 1 : 0;
        }

        private static (LevelData? level, int solution, double winRate, int seed, bool inRange) Build(LevelDesign design)
        {
            LevelData? best = null;
            var bestSolution = 0;
            var bestRate = 0.0;
            var bestSeed = 0;
            var bestDistance = double.MaxValue;

            for (var seed = design.seed; seed < design.seed + MaxSeeds; seed++)
            {
                var level = LevelGenerator.Generate(design, seed);
                if (LevelValidator.Validate(level).Count > 0)
                {
                    continue;
                }

                var solution = LevelSolver.Solve(level);
                if (solution == null)
                {
                    continue;
                }

                var rate = Bot.WinRate(level, BotRuns, seed);
                var distance = rate < design.minWin ? design.minWin - rate : rate > design.maxWin ? rate - design.maxWin : 0;
                if (distance < bestDistance)
                {
                    best = level;
                    bestSolution = solution.Count;
                    bestRate = rate;
                    bestSeed = seed;
                    bestDistance = distance;
                }

                if (distance == 0)
                {
                    break;
                }
            }

            return (best, bestSolution, bestRate, bestSeed, bestDistance == 0);
        }

        private static string FindRoot()
        {
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Assets")))
            {
                dir = dir.Parent;
            }

            return dir?.FullName ?? Directory.GetCurrentDirectory();
        }
    }
}
