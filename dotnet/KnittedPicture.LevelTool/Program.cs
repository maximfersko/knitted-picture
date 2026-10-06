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
        private const int MaxMeasured = 40;

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
            Console.WriteLine("id  название           размер петли цв клуб кол сл  тапы/цель  бот  наугад фронт выбор1 роковые  сид   механики");
            foreach (var path in Directory.GetFiles(source, "level_*.json").OrderBy(p => p))
            {
                var design = JsonSerializer.Deserialize<LevelDesign>(File.ReadAllText(path), ReadOptions)!;
                var built = Build(design);
                if (built == null)
                {
                    Console.WriteLine($"{design.id,2}  {design.name}: не нашлось решаемого поля");
                    failed = true;
                    continue;
                }

                var level = built.Level;
                var file = Path.Combine(output, $"level_{design.id:000}.json");
                File.WriteAllText(file, JsonSerializer.Serialize(level, WriteOptions) + "\n");

                var mechanics = string.Join(" ", new[]
                {
                    design.fabric.Length > 0 ? "канва" : null,
                    design.band > 0 ? $"натяжение {design.band}" : null,
                    design.mystery ? "загадка" : null,
                    design.gold > 0 ? $"золото×{level.gold.Length}" : null,
                    design.extra > 0 ? $"лишние×{design.extra}" : null,
                    design.junk > 0 ? $"чужие×{design.junk}" : null,
                    design.hidden > 0 ? $"скрытые×{design.hidden}" : null,
                    design.knots > 0 ? $"узелки×{design.knots}" : null,
                    design.links > 0 ? $"связки×{design.links}" : null,
                    design.locks > 0 ? $"замки×{design.locks}" : null,
                    level.spools.Any(sp => sp.rainbow) ? $"радужные×{level.spools.Count(sp => sp.rainbow)}" : null,
                    level.spools.Any(sp => sp.thick) ? $"толстые×{level.spools.Count(sp => sp.thick)}" : null,
                    level.spools.Any(sp => sp.ice > 0) ? $"лёд×{level.spools.Count(sp => sp.ice > 0)}" : null,
                    design.timeLimit > 0 ? $"моль {design.timeLimit}с" : null,
                    design.hard ? "сложный" : null,
                }.Where(m => m != null));
                var mark = built.InRange ? " " : "!";
                var taps = level.par > 0 ? $"{built.Solution,3}/{level.par,-3}" : $"{built.Solution,3}    ";
                var stitches = PictureGrid.From(level).Total;
                Console.WriteLine(
                    $"{design.id,2}  {design.name,-18} {level.width,2}×{level.height,-2} {stitches,5} {level.colors.Length,2} {level.spools.Length,4} {design.columns,3} {level.slots,2}  {taps,9}  {built.WinRate,4:P0}{mark} {built.RandomRate,5:P0} {built.Metrics.FrontColors,5:0.0} {built.Metrics.Forced,5:P0} {built.Metrics.Danger,6:P0}  {built.Seed,5}   {mechanics}");
            }

            return failed ? 1 : 0;
        }

        private sealed class Built
        {
            public LevelData Level = null!;
            public int Solution;
            public double WinRate;
            public double RandomRate;
            public int Seed;
            public bool InRange;
            public Metrics Metrics = null!;
        }

        /// <summary>
        /// Перебирает сиды: уровень должен решаться, бот — выигрывать в нужной доле партий,
        /// а решения — попадать в коридоры (не слишком много очевидных ходов, в меру роковых).
        /// </summary>
        private static Built? Build(LevelDesign design)
        {
            Built? best = null;
            var bestDistance = double.MaxValue;
            var measured = 0;

            for (var seed = design.seed; seed < design.seed + MaxSeeds && measured < MaxMeasured; seed++)
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
                var random = Bot.RandomWinRate(level, BotRuns, seed);
                var winDistance = (rate < design.minWin ? design.minWin - rate : rate > design.maxWin ? rate - design.maxWin : 0)
                    + Math.Max(0, random - design.maxRandom);

                // Метрики дорогие — считаем их, только когда бот уже в коридоре или лучшего пока нет.
                if (winDistance > 0 && best != null)
                {
                    continue;
                }

                measured++;
                var metrics = Metrics.Measure(level, solution);
                var distance = winDistance * 4
                    + Math.Max(0, metrics.Forced - design.maxForced)
                    + Math.Max(0, design.minDanger - metrics.Danger)
                    + Math.Max(0, metrics.Danger - design.maxDanger);
                if (distance < bestDistance)
                {
                    best = new Built { Level = level, Solution = solution.Count, WinRate = rate, RandomRate = random, Seed = seed, Metrics = metrics };
                    bestDistance = distance;
                }

                if (distance == 0)
                {
                    break;
                }
            }

            if (best == null)
            {
                return null;
            }

            best.InRange = bestDistance == 0;
            best.Level.par = Par(best.Level, design);
            return best;
        }

        /// <summary>
        /// Цель по тапам имеет смысл, только если тапы могут разниться — есть лишняя пряжа.
        /// Иначе каждый клубок нужен и число тапов одно и то же.
        /// </summary>
        private static int Par(LevelData level, LevelDesign design)
        {
            var loops = level.spools.Sum(sp => sp.loops);
            if (loops == PictureGrid.From(level).Total)
            {
                return 0;
            }

            var fewest = LevelSolver.FewestTaps(level);
            return fewest < 0 ? 0 : fewest + design.parSlack;
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
