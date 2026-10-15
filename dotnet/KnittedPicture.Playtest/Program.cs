using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using KnittedPicture.Core;
using KnittedPicture.LevelTool;

namespace KnittedPicture.Playtest
{
    /// <summary>
    /// Бот-тестировщик.
    ///
    ///   dotnet run -c Release --project dotnet/KnittedPicture.Playtest -- [ключи]
    ///
    ///   --runs N              хаос-партий на уровень (по умолчанию 200)
    ///   --generated N         ещё N случайных уровней из генератора — ловить ошибки на необычных сочетаниях (0)
    ///   --difficulty-runs N   партий каждого бота для профиля сложности (100; 0 — пропустить)
    ///   --players N --days N  мета-игра: N игроков по N дней (20 × 60)
    ///   --seed N              зерно случайности (1): тот же ключ — те же партии
    ///   --report FILE         отчёт в Markdown
    ///   --economy             только баланс экономики по типам игроков (с --players, --days)
    ///   --repro L:R           повторить одну хаос-партию (уровень L, партия R) с печатью каждого хода
    ///
    /// Код выхода 1, если нарушено хоть одно правило: так проверка на GitHub краснеет.
    /// </summary>
    public static class Program
    {
        private static readonly JsonSerializerOptions Json = new JsonSerializerOptions { IncludeFields = true, ReadCommentHandling = JsonCommentHandling.Skip };

        public static int Main(string[] args)
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            Console.OutputEncoding = Encoding.UTF8;
            var opt = Options.Parse(args);
            var root = FindRoot();
            var levels = Directory.GetFiles(Path.Combine(root, "Assets", "Resources", "Levels"), "level_*.json")
                .OrderBy(f => f)
                .Select(f => JsonSerializer.Deserialize<LevelData>(File.ReadAllText(f), Json)!)
                .ToList();
            var findings = new Findings();
            var rules = new RulesCheck(findings, opt.Seed);

            if (opt.Economy)
            {
                // Только баланс экономики по типам игроков: таблица в консоль (и в отчёт, если задан).
                var table = new EconomySim(levels).Run(opt.Players, opt.Days, opt.Seed);
                Console.WriteLine(table);
                if (opt.Report != null)
                {
                    File.WriteAllText(opt.Report, table);
                }

                return 0;
            }

            if (opt.Repro != null)
            {
                var parts = opt.Repro.Split(':');
                var level = levels.Concat(Generated(root, int.Parse(parts[0]) - 999, opt.Seed, findings)).First(l => l.id == int.Parse(parts[0]));
                Console.WriteLine($"Повтор: уровень {level.id} «{level.name}», партия {parts[1]}, зерно {opt.Seed}");
                rules.Play(level, int.Parse(parts[1]), verbose: true);
                PrintProblems(findings);
                return findings.Total > 0 ? 1 : 0;
            }

            var clock = Stopwatch.StartNew();
            Console.WriteLine($"Правила: {levels.Count} уровней × {opt.Runs} хаос-партий…");
            rules.Run(levels, opt.Runs);
            if (opt.Generated > 0)
            {
                var generated = Generated(root, opt.Generated, opt.Seed, findings);
                Console.WriteLine($"Правила на {generated.Count} сгенерированных уровнях…");
                rules.Run(generated, Math.Max(20, opt.Runs / 4));
            }

            Console.WriteLine($"Мета-игра: {opt.Players} игроков × {opt.Days} дней…");
            new MetaSim(findings, levels).Run(opt.Players, opt.Days, opt.Seed);

            var profiles = new List<LevelProfile>();
            if (opt.DifficultyRuns > 0)
            {
                Console.WriteLine($"Сложность: 4 бота × {opt.DifficultyRuns} партий на уровень…");
                profiles = Difficulty.Run(levels, opt.DifficultyRuns, opt.Seed);
            }

            Console.WriteLine($"Готово за {clock.Elapsed.TotalSeconds:0} с.\n");
            PrintProblems(findings);
            PrintCounters(findings);
            PrintFlags(profiles);
            if (opt.Report != null)
            {
                File.WriteAllText(opt.Report, Report.Markdown(findings, profiles, opt));
                Console.WriteLine($"\nОтчёт: {opt.Report}");
            }

            return findings.Total > 0 ? 1 : 0;
        }

        /// <summary>Случайные уровни из генератора: настройки берутся из исходников уровней и перемешиваются.</summary>
        private static List<LevelData> Generated(string root, int count, int seed, Findings findings)
        {
            var result = new List<LevelData>();
            var rng = new Random(seed * 31 + 7);
            var sources = Directory.GetFiles(Path.Combine(root, "levels-src"), "level_*.json").OrderBy(f => f).ToArray();
            for (var tries = 0; result.Count < count && tries < count * 20; tries++)
            {
                var d = JsonSerializer.Deserialize<LevelDesign>(File.ReadAllText(sources[rng.Next(sources.Length)]), Json)!;
                d.slots = rng.Next(3, 7);
                d.columns = rng.Next(3, 7);
                d.band = rng.Next(0, 4);
                d.playBand = 0;
                d.hidden = rng.Next(0, 5);
                d.knots = rng.Next(0, 4);
                d.links = rng.Next(0, 3);
                d.locks = rng.Next(0, 4);
                d.rainbow = rng.Next(0, 3);
                d.thick = rng.Next(0, 3);
                d.ice = rng.Next(0, 3);
                d.junk = rng.Next(0, 3);
                d.extra = rng.Next(0, 4);
                d.swaps = rng.Next(0, 8);
                d.mystery = rng.Next(4) == 0;
                d.timeLimit = rng.Next(3) == 0 ? 60 : 0;
                LevelData level;
                try
                {
                    level = LevelGenerator.Generate(d, rng.Next(100000));
                }
                catch (Exception e)
                {
                    findings.Fail("генератор уровней упал: " + e.GetType().Name, e.Message);
                    continue;
                }

                if (LevelValidator.Validate(level).Count > 0)
                {
                    findings.Count("сгенерированных уровней отброшено как некорректные");
                    continue;
                }

                level.id = 1000 + result.Count;
                result.Add(level);
            }

            return result;
        }

        private static void PrintProblems(Findings f)
        {
            Console.WriteLine("== Нарушения правил ==");
            if (f.Problems.Count == 0)
            {
                Console.WriteLine("нет");
                return;
            }

            foreach (var kv in f.Problems.OrderByDescending(k => k.Value))
            {
                Console.WriteLine($"{kv.Value,6} × {kv.Key}\n         пример: {f.Examples[kv.Key]}");
                if (f.Repro[kv.Key].Length > 0)
                {
                    Console.WriteLine($"         повторить: dotnet run -c Release --project dotnet/KnittedPicture.Playtest -- {f.Repro[kv.Key]}");
                }
            }
        }

        private static void PrintCounters(Findings f)
        {
            Console.WriteLine("\n== Счётчики ==");
            foreach (var kv in f.Counters.OrderBy(k => k.Key))
            {
                Console.WriteLine($"{kv.Value,8} × {kv.Key}");
            }
        }

        private static void PrintFlags(List<LevelProfile> profiles)
        {
            if (profiles.Count == 0)
            {
                return;
            }

            Console.WriteLine("\n== Уровни, выбивающиеся из кривой сложности ==");
            var flagged = profiles.Where(p => p.Flags.Count > 0).ToList();
            if (flagged.Count == 0)
            {
                Console.WriteLine("нет");
            }

            foreach (var p in flagged)
            {
                Console.WriteLine($"  {p.Id,3} «{p.Name}»{(p.Hard ? " (сложный)" : "")}: {string.Join("; ", p.Flags)}" +
                                  $"  [наугад {p.Random:P0}, средний {p.Average:P0}, по подсветке {p.Follower:P0}, знающий {p.Skilled:P0}]");
            }
        }

        private static string FindRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Assets")))
            {
                dir = dir.Parent;
            }

            return dir?.FullName ?? Directory.GetCurrentDirectory();
        }
    }

    public sealed class Options
    {
        public int Runs = 200;
        public int Generated;
        public int DifficultyRuns = 100;
        public int Players = 20;
        public int Days = 60;
        public int Seed = 1;
        public string? Report;
        public string? Repro;

        /// <summary>--economy: только симуляция экономики по типам игроков.</summary>
        public bool Economy;

        public static Options Parse(string[] args)
        {
            var o = new Options();
            for (var i = 0; i < args.Length; i++)
            {
                string Next() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"после {args[i]} нужно значение");
                switch (args[i])
                {
                    case "--runs": o.Runs = int.Parse(Next()); break;
                    case "--generated": o.Generated = int.Parse(Next()); break;
                    case "--difficulty-runs": o.DifficultyRuns = int.Parse(Next()); break;
                    case "--players": o.Players = int.Parse(Next()); break;
                    case "--days": o.Days = int.Parse(Next()); break;
                    case "--seed": o.Seed = int.Parse(Next()); break;
                    case "--report": o.Report = Next(); break;
                    case "--repro": o.Repro = Next(); break;
                    case "--meta-player": Next(); break;
                    case "--economy": o.Economy = true; break;
                    default: throw new ArgumentException($"неизвестный ключ {args[i]}");
                }
            }

            return o;
        }
    }
}
