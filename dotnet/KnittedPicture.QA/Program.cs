using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using KnittedPicture.ArtPreview;
using KnittedPicture.Core;

namespace KnittedPicture.QA
{
    /// <summary>
    /// Глубокий QA-бот.
    ///
    ///   dotnet run -c Release --project dotnet/KnittedPicture.QA -- [ключи]
    ///
    ///   --minutes M      бюджет времени на партии (по умолчанию 3); партии идут по кругу уровней и личностей
    ///   --quick          быстрый прогон для CI: 40 секунд, меньше мутантов
    ///   --deep           дорогие проверки на каждом действии (медленнее, тщательнее)
    ///   --seed N         зерно (1): тот же ключ — те же партии
    ///   --out DIR        папка отчёта (qa-out): report.html, report.json, findings/*/film.png и finding.json
    ///   --level ID       только этот уровень (и его мутанты)
    ///   --mutants N      пограничных уровней-мутантов (60)
    ///   --actions N      максимум действий в партии (400)
    ///   --replay FILE    повторить находку (finding.json): печать каждого шага и кадры replay.png
    ///
    /// Код выхода 1, если есть падения или ошибки правил (не «защита», не «внимание»).
    /// </summary>
    public static class Program
    {
        private static readonly JsonSerializerOptions Json = new JsonSerializerOptions
        {
            IncludeFields = true, WriteIndented = true, ReadCommentHandling = JsonCommentHandling.Skip,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        public static int Main(string[] args)
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            Console.OutputEncoding = Encoding.UTF8;
            var opt = Options.Parse(args);
            if (opt.Replay != null)
            {
                return Replay(opt.Replay);
            }

            var root = FindRoot();
            var shipped = Directory.GetFiles(Path.Combine(root, "Assets", "Resources", "Levels"), "level_*.json")
                .OrderBy(f => f)
                .Select(f => JsonSerializer.Deserialize<LevelData>(File.ReadAllText(f), Json)!)
                .Where(l => opt.Level == null || l.id == opt.Level)
                .ToList();

            var findings = new Findings();
            var run = new RunSummary { Seed = opt.Seed, Mode = opt.Deep ? "глубокий" : opt.Quick ? "быстрый" : "обычный", Levels = shipped.Count };
            List<LevelData> edge;
            try
            {
                edge = EdgeLevels.Build(shipped.Count > 0 ? shipped : Load(root), opt.Seed, opt.Mutants, out var rejected);
                run.EdgeRejected = rejected;
            }
            catch (Exception e)
            {
                findings.Add(new Violation("validator-crash", Severity.Crash, "валидатор уровней упал", e.ToString()), new LevelData { name = "?" }, "-", opt.Seed, new List<QaAction>(), 0);
                edge = new List<LevelData>();
            }

            run.EdgeLevels = edge.Count;
            var levels = shipped.Concat(edge).ToList();
            Console.WriteLine($"QA: {shipped.Count} уровней + {edge.Count} пограничных, {Persona.All(new Novelty()).Count} личностей, бюджет {opt.Minutes:0.#} мин, зерно {opt.Seed}");

            // Решаемость без бустеров (для решателя: на решаемом уровне без моли он обязан выиграть).
            var solvable = new Dictionary<int, bool>();
            Parallel.ForEach(levels, l =>
            {
                var ok = l.timeLimit == 0 && LevelSolver.Solve(l, 60_000) != null;
                lock (solvable)
                {
                    solvable[l.id] = ok;
                }
            });

            var coverage = new Coverage();
            var clock = Stopwatch.StartNew();
            var budget = TimeSpan.FromMinutes(opt.Minutes);
            var depth = opt.Deep ? Depth.Full : new Depth();
            long episodes = 0, actions = 0;
            var personaStats = new Dictionary<string, (long, long)>();
            var slowest = new List<(string, double, string, double)>();

            // Круги: в каждом круге каждый уровень играет каждая личность по одной партии; кругов — сколько влезет в бюджет.
            var round = 0;
            while (clock.Elapsed < budget || round == 0)
            {
                Parallel.ForEach(levels, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, level =>
                {
                    // Настоящие уровни первого круга играются всегда; остальное — пока есть бюджет.
                    if (clock.Elapsed >= budget && (round > 0 || level.id >= 5000))
                    {
                        return;
                    }

                    var novelty = new Novelty();
                    var localCoverage = new Coverage();
                    var stats = new Dictionary<string, (long, long)>();
                    long localActions = 0, localEpisodes = 0;
                    var slow = (Step: 0.0, What: string.Empty, Hint: 0.0);
                    var personas = Persona.All(novelty);
                    for (var pi = 0; pi < personas.Count; pi++)
                    {
                        var persona = personas[pi];
                        var seed = unchecked(opt.Seed * 1_000_003 + level.id * 7919 + round * 104_729 + pi * 9973);
                        EpisodeResult r;
                        try
                        {
                            r = Episode.Play(level, persona, seed, opt.Actions, depth, localCoverage, novelty);
                        }
                        catch (Exception e)
                        {
                            findings.Add(new Violation("qa-crash", Severity.Warning, "сам QA-бот упал (ошибка тестировщика)", e.ToString()), level, persona.Name, seed, new List<QaAction>(), 0);
                            continue;
                        }

                        localEpisodes++;
                        localActions += r.Actions.Count;
                        var (eps, wins) = stats.TryGetValue(persona.Name, out var st) ? st : (0, 0);
                        stats[persona.Name] = (eps + 1, wins + (r.Status == GameStatus.Won ? 1 : 0));
                        foreach (var h in r.Hits)
                        {
                            findings.Add(h.Violation, level, persona.Name, seed, r.Actions, h.Index);
                        }

                        // Решатель на решаемом уровне без моли обязан выиграть: затор по подсказкам — ошибка,
                        // а подсказка, которая молчит (перебор упёрся в предел на огромном уровне), — повод присмотреться.
                        // Бюджет действий кончился посреди игры (огромная картинка) — это не находка.
                        var follower = persona as Follower;
                        if (follower != null && solvable.TryGetValue(level.id, out var ok) && ok && r.Hits.Count == 0
                            && (r.Status == GameStatus.Jammed || r.Status == GameStatus.Playing && follower.GaveUp))
                        {
                            var jam = r.Status == GameStatus.Jammed;
                            findings.Add(new Violation(jam ? "hint-dead-end" : "hint-gives-up", jam ? Severity.Bug : Severity.Warning,
                                jam ? "подсказка завела в тупик на решаемом уровне" : "подсказка не находит пути на решаемом уровне",
                                $"уровень решаем, а партия по подсказкам кончилась {r.Status} после {r.Actions.Count} действий"),
                                level, persona.Name, seed, r.Actions, r.Actions.Count - 1);
                        }

                        // Детерминизм: каждая пятая партия повторяется и обязана пройти тем же путём.
                        if ((seed & 7) == 0 && r.Hits.Count == 0)
                        {
                            var again = Episode.Replay(level, r.Actions, seed, new Depth { Expensive = 0, Solver = 0 });
                            var at = Diverge(r.Snapshots, again.Snapshots);
                            if (at >= 0)
                            {
                                findings.Add(new Violation("nondeterminism", Severity.Bug, "одна и та же партия пошла по-разному",
                                    $"расхождение после действия {at}:\nпервый раз {r.Snapshots[at]}\nповтор     {(at < again.Snapshots.Count ? again.Snapshots[at] : "—")}"),
                                    level, persona.Name, seed, r.Actions, at);
                            }
                        }

                        if (r.SlowestStepMs > slow.Step || r.SlowestHintMs > slow.Hint)
                        {
                            slow = (Math.Max(slow.Step, r.SlowestStepMs), r.SlowestStepMs >= slow.Step ? r.SlowestWhat : slow.What, Math.Max(slow.Hint, r.SlowestHintMs));
                        }
                    }

                    lock (coverage)
                    {
                        coverage.Merge(localCoverage);
                        episodes += localEpisodes;
                        actions += localActions;
                        foreach (var kv in stats)
                        {
                            var (e0, w0) = personaStats.TryGetValue(kv.Key, out var p) ? p : (0, 0);
                            personaStats[kv.Key] = (e0 + kv.Value.Item1, w0 + kv.Value.Item2);
                        }

                        var name = level.id >= 5000 ? $"{level.id} «{level.name}»" : level.id.ToString();
                        var index = slowest.FindIndex(s => s.Item1 == name);
                        var entry = (name, slow.Step, slow.What, slow.Hint);
                        if (index < 0)
                        {
                            slowest.Add(entry);
                        }
                        else if (slow.Step > slowest[index].Item2 || slow.Hint > slowest[index].Item4)
                        {
                            slowest[index] = (name, Math.Max(slow.Step, slowest[index].Item2), slow.What, Math.Max(slow.Hint, slowest[index].Item4));
                        }
                    }
                });

                round++;
                Console.WriteLine($"  круг {round}: {episodes:N0} партий, {actions:N0} действий, находок {findings.All.Count()} ({clock.Elapsed.TotalSeconds:0} с)");
                if (opt.Rounds > 0 && round >= opt.Rounds)
                {
                    break;
                }
            }

            // Мета: дни, часы, награды, сохранения.
            var meta = new MetaFuzz(Math.Max(1, Load(root).Count));
            Console.WriteLine("Мета: прыжки часов, награды в любом порядке, испорченные сохранения…");
            meta.Run(findings, opt.Quick ? 30 : 120, 400, opt.Seed);
            Console.WriteLine($"  мета: {meta.Ops:N0} действий");

            // Подсказка ищется в фоне (кадры не ждут) и не дольше 1,5 с: тревожно, только если она упёрлась в этот предел.
            foreach (var s in slowest.Where(s => s.Item2 > 25 || s.Item4 > 1500))
            {
                findings.Add(new Violation("slow:" + s.Item1, Severity.Warning, $"медленно на уровне {s.Item1}",
                    $"самое долгое действие {s.Item2:0.0} мс ({s.Item3}), подсказка {s.Item4:0} мс; на слабом телефоне в 5–10 раз дольше"),
                    levels.FirstOrDefault(l => s.Item1.StartsWith(l.id.ToString(), StringComparison.Ordinal)) ?? levels[0], "-", 0, new List<QaAction>(), 0);
            }

            run.Episodes = episodes;
            run.Actions = actions;
            foreach (var kv in personaStats)
            {
                run.Personas[kv.Key] = kv.Value;
            }

            run.Slowest.AddRange(slowest);

            // Ужатие и «скриншоты».
            var outDir = Path.GetFullPath(opt.Out);
            if (Directory.Exists(Path.Combine(outDir, "findings")))
            {
                Directory.Delete(Path.Combine(outDir, "findings"), true);
            }

            Directory.CreateDirectory(Path.Combine(outDir, "findings"));
            var number = 0;
            foreach (var f in findings.All.ToList())
            {
                number++;
                if (f.Actions.Count == 0)
                {
                    f.Detail = f.First.Detail;
                    continue;
                }

                Console.WriteLine($"  ужимаю «{f.Kind}»: {f.Actions.Count} действий…");
                if (f.Kind is not ("hint-dead-end" or "hint-gives-up" or "nondeterminism"))
                {
                    f.Minimal = Shrinker.Shrink(f.Level, f.Actions, f.Kind, f.Seed, opt.Quick ? 400 : 2500, out var tests);
                    f.ShrinkTests = tests;
                }

                var frames = new List<Frame>();
                var replay = Episode.Replay(f.Level, f.Minimal, f.Seed, Episode.ExpensiveKinds.Contains(f.Kind) ? Depth.Full : new Depth { Expensive = 0, Solver = 0 }, frames);
                var hit = replay.Hits.FirstOrDefault(h => h.Violation.Kind == f.Kind)?.Violation ?? f.First;
                f.Detail = hit.Detail;
                var last = frames[^1];
                last.MarkColumn = hit.MarkColumn;
                last.MarkSlot = hit.MarkSlot;
                last.MarkCell = hit.MarkCell;
                f.Folder = $"findings/{number:00}-{Safe(f.Kind)}";
                var dir = Path.Combine(outDir, f.Folder);
                Directory.CreateDirectory(dir);
                Png.Save(BoardPainter.Film(frames), Path.Combine(dir, "film.png"));
                Png.Save(BoardPainter.Render(last), Path.Combine(dir, "state.png"));
                File.WriteAllText(Path.Combine(dir, "finding.json"), JsonSerializer.Serialize(new Saved
                {
                    kind = f.Kind, title = f.First.Title, severity = f.Severity.ToString(), seed = f.Seed, persona = f.Persona,
                    level = f.Level, actions = f.Minimal.Select(a => a.ToString()).ToArray(), original = f.Actions.Select(a => a.ToString()).ToArray(),
                }, Json));
            }

            run.Seconds = clock.Elapsed.TotalSeconds;
            Report.Write(outDir, run, findings, coverage);
            Print(findings, coverage, outDir);
            return findings.Count(Severity.Crash) + findings.Count(Severity.Bug) > 0 ? 1 : 0;
        }

        private static int Diverge(List<string> a, List<string> b)
        {
            for (var i = 0; i < Math.Max(a.Count, b.Count); i++)
            {
                if (i >= a.Count || i >= b.Count || a[i] != b[i])
                {
                    return Math.Min(i, a.Count - 1);
                }
            }

            return -1;
        }

        private static void Print(Findings findings, Coverage coverage, string outDir)
        {
            Console.WriteLine();
            Console.WriteLine("== Находки ==");
            if (!findings.All.Any())
            {
                Console.WriteLine("нет");
            }

            foreach (var f in findings.All)
            {
                Console.WriteLine($"[{f.Severity}] ×{f.Count} {f.First.Title}  ({f.Kind})");
                if (f.Minimal.Count > 0)
                {
                    Console.WriteLine($"    уровень {f.Level.id}, {f.Persona}, ужато до {f.Minimal.Count}: {string.Join("; ", f.Minimal.Take(12))}{(f.Minimal.Count > 12 ? " …" : "")}");
                }
            }

            var missing = coverage.Missing.ToList();
            Console.WriteLine();
            Console.WriteLine(missing.Count == 0 ? "Покрытие: все ожидаемые ситуации встретились." : "Не встретилось: " + string.Join(", ", missing));
            Console.WriteLine($"\nОтчёт: {Path.Combine(outDir, "report.html")}");
        }

        private static string Safe(string kind) => new string(kind.Select(ch => char.IsLetterOrDigit(ch) || ch == '-' ? ch : '-').ToArray()).Trim('-');

        /// <summary>Сохранённая находка: всё для повтора без исходного прогона.</summary>
        public sealed class Saved
        {
            public string kind = string.Empty;
            public string title = string.Empty;
            public string severity = string.Empty;
            public int seed;
            public string persona = string.Empty;
            public LevelData level = new LevelData();
            public string[] actions = Array.Empty<string>();
            public string[] original = Array.Empty<string>();
        }

        /// <summary>Повтор находки: каждый шаг с состоянием и сработавшими проверками, все кадры в replay.png.</summary>
        private static int Replay(string path)
        {
            var saved = JsonSerializer.Deserialize<Saved>(File.ReadAllText(path), Json)!;
            var actions = saved.actions.Select(QaAction.Parse).ToList();
            Console.WriteLine($"Повтор «{saved.title}» ({saved.kind}): уровень {saved.level.id} «{saved.level.name}», {actions.Count} действий");
            var frames = new List<Frame>();
            var result = Episode.Replay(saved.level, actions, saved.seed, Depth.Full, frames);
            for (var i = 0; i < result.Actions.Count; i++)
            {
                Console.WriteLine($"{i,4}  {result.Actions[i],-14} {(i < result.Snapshots.Count ? result.Snapshots[i] : "(упало)")}");
                foreach (var h in result.Hits.Where(h => h.Index == i))
                {
                    Console.WriteLine($"      ✗ {h.Violation.Title}: {h.Violation.Detail.Split('\n')[0]}");
                }
            }

            var outPath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path))!, "replay.png");
            Png.Save(BoardPainter.Film(frames, frames.Count), outPath);
            Console.WriteLine($"Кадры: {outPath}");
            return result.Hits.Any(h => h.Violation.Kind == saved.kind) ? 1 : 0;
        }

        private static List<LevelData> Load(string root) =>
            Directory.GetFiles(Path.Combine(root, "Assets", "Resources", "Levels"), "level_*.json")
                .Select(f => JsonSerializer.Deserialize<LevelData>(File.ReadAllText(f), Json)!).ToList();

        private static string FindRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Assets")))
            {
                dir = dir.Parent;
            }

            return dir?.FullName ?? Directory.GetCurrentDirectory();
        }

        private sealed class Options
        {
            public double Minutes = 3;
            public bool Quick;
            public bool Deep;
            public int Seed = 1;
            public string Out = "qa-out";
            public int? Level;
            public int Mutants = 60;
            public int Actions = 400;
            public int Rounds;
            public string? Replay;

            public static Options Parse(string[] args)
            {
                var o = new Options();
                for (var i = 0; i < args.Length; i++)
                {
                    string Next() => args[++i];
                    switch (args[i])
                    {
                        case "--minutes": o.Minutes = double.Parse(Next(), CultureInfo.InvariantCulture); break;
                        case "--quick": o.Quick = true; o.Minutes = 0.67; o.Mutants = 25; break;
                        case "--deep": o.Deep = true; break;
                        case "--seed": o.Seed = int.Parse(Next(), CultureInfo.InvariantCulture); break;
                        case "--out": o.Out = Next(); break;
                        case "--level": o.Level = int.Parse(Next(), CultureInfo.InvariantCulture); break;
                        case "--mutants": o.Mutants = int.Parse(Next(), CultureInfo.InvariantCulture); break;
                        case "--actions": o.Actions = int.Parse(Next(), CultureInfo.InvariantCulture); break;
                        case "--rounds": o.Rounds = int.Parse(Next(), CultureInfo.InvariantCulture); break;
                        case "--replay": o.Replay = Next(); break;
                        default: throw new ArgumentException("неизвестный ключ " + args[i]);
                    }
                }

                return o;
            }
        }
    }
}
