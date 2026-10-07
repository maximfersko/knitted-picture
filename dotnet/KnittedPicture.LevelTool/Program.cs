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

        /// <summary>Предел перебора решателя на один вариант: дольше — значит, вариант всё равно плохой.</summary>
        private const int SolverStates = 120_000;

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
            // dotnet run --project dotnet/KnittedPicture.LevelTool [-- [источник] [выход]] [--only 16-20]
            var only = (from: 0, to: int.MaxValue);
            var positional = new List<string>();
            for (var i = 0; i < args.Length; i++)
            {
                if (args[i] == "--only" && i + 1 < args.Length)
                {
                    var parts = args[++i].Split('-');
                    only = (int.Parse(parts[0]), int.Parse(parts[parts.Length - 1]));
                }
                else
                {
                    positional.Add(args[i]);
                }
            }

            var root = FindRoot();
            var source = positional.Count > 0 ? positional[0] : Path.Combine(root, "levels-src");
            var output = positional.Count > 1 ? positional[1] : Path.Combine(root, "Assets", "Resources", "Levels");
            Directory.CreateDirectory(output);

            var designs = Directory.GetFiles(source, "level_*.json")
                .OrderBy(p => p)
                .Select(path => JsonSerializer.Deserialize<LevelDesign>(File.ReadAllText(path), ReadOptions)!)
                .Where(d => d.id >= only.from && d.id <= only.to)
                .ToList();

            // Уровни независимы: считаем параллельно, а печатаем и пишем по порядку — результат тот же.
            var results = new Built?[designs.Count];
            var done = 0;
            System.Threading.Tasks.Parallel.For(0, designs.Count, i =>
            {
                var clock = System.Diagnostics.Stopwatch.StartNew();
                results[i] = Build(designs[i]);
                var n = System.Threading.Interlocked.Increment(ref done);

                // Прогресс — в stderr, чтобы таблица в stdout осталась чистой.
                Console.Error.WriteLine($"[{n}/{designs.Count}] уровень {designs[i].id} «{designs[i].name}» — {clock.Elapsed.TotalSeconds:0} с");
            });

            var failed = false;
            Console.WriteLine("id  название           размер петли цв клуб кол сл тапы  бот  наугад ранний фронт выбор1 роковые  сид   механики");
            for (var i = 0; i < designs.Count; i++)
            {
                var design = designs[i];
                var built = results[i];
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
                    level.band > 0 ? $"натяжение {level.band}" : null,
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
                var stitches = PictureGrid.From(level).Total;
                Console.WriteLine(
                    $"{design.id,2}  {design.name,-18} {level.width,2}×{level.height,-2} {stitches,5} {level.colors.Length,2} {level.spools.Length,4} {design.columns,3} {level.slots,2} {built.Solution,4}  {built.WinRate,4:P0}{mark} {built.RandomRate,5:P0} {built.EarlyJam,6:P0} {built.Metrics.FrontColors,5:0.0} {built.Metrics.Forced,5:P0} {built.Metrics.Danger,6:P0}  {built.Seed,5}   {mechanics}");
            }

            return failed ? 1 : 0;
        }

        private sealed class Built
        {
            public LevelData Level = null!;
            public int Solution;
            public double WinRate;
            public double RandomRate;
            public double EarlyJam;
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

                // Сначала дешёвые боты (доли секунды), а решатель и метрики (секунды) — только для подходящих.
                var (rate, early) = Bot.Profile(level, BotRuns, seed);
                var random = Bot.RandomWinRate(level, BotRuns, seed);
                var winDistance = (rate < design.minWin ? design.minWin - rate : rate > design.maxWin ? rate - design.maxWin : 0)
                    + Math.Max(0, random - design.maxRandom)
                    + Math.Max(0, early - design.maxEarlyJam);
                if (winDistance > 0 && best != null && winDistance * 4 >= bestDistance)
                {
                    continue;
                }

                var solution = LevelSolver.Solve(level, SolverStates);
                if (solution == null)
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
                    best = new Built { Level = level, Solution = solution.Count, WinRate = rate, RandomRate = random, EarlyJam = early, Seed = seed, Metrics = metrics };
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
            return best;
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
