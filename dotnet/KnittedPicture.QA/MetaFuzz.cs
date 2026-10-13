using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using KnittedPicture.Core;

namespace KnittedPicture.QA
{
    /// <summary>Действие с прогрессом игрока: заходы по дням (часы прыгают), награды, уровни, бустеры, кривые вызовы.</summary>
    public readonly struct MetaOp
    {
        public MetaOp(string kind, int a = 0, int b = 0)
        {
            Kind = kind;
            A = a;
            B = b;
        }

        public string Kind { get; }

        public int A { get; }

        public int B { get; }

        /// <summary>Кривой вызов, недоступный из интерфейса.</summary>
        public bool IsRobustness => Kind.StartsWith("bad-", StringComparison.Ordinal);

        public override string ToString() => B != 0 ? $"{Kind} {A} {B}" : A != 0 ? $"{Kind} {A}" : Kind;
    }

    /// <summary>
    /// Фаззер меты. Часы прыгают как у живого игрока: тот же день много раз, назад на день (перевели часы),
    /// назад на месяц, вперёд на год и обратно. Награды забираются в любом порядке, в том числе дважды.
    /// После каждого действия: деньги и запасы не в минусе, звёзды и пройденное не убывают, одна награда
    /// не выдаётся дважды за один и тот же день/предмет, сохранение и загрузка ничего не меняют.
    /// Отдельно: испорченные сохранения (старые версии, мусор) не должны ронять игру.
    /// </summary>
    public sealed class MetaFuzz
    {
        private static readonly JsonSerializerOptions Json = new JsonSerializerOptions { IncludeFields = true };
        private static readonly LevelData MetaLevel = new LevelData { id = -1, name = "мета" };
        private readonly int _levelCount;

        public MetaFuzz(int levelCount)
        {
            _levelCount = levelCount;
        }

        public long Ops { get; private set; }

        public void Run(Findings findings, int players, int opsPerPlayer, int seed)
        {
            for (var player = 0; player < players; player++)
            {
                var rng = new Random(unchecked(seed * 7_919 + player * 104_729));
                var ops = Generate(rng, opsPerPlayer);
                var hits = Play(ops, out var count);
                Ops += count;
                foreach (var (v, index) in hits)
                {
                    var minimal = Shrink(ops.Take(index + 1).ToList(), v.Kind);
                    var detail = v.Detail + "\n\nШаги (ужато ddmin):\n" + string.Join("\n", minimal.Select((o, i) => $"{i + 1}. {o}"));
                    findings.Add(new Violation(v.Kind, v.Severity, v.Title, detail), MetaLevel, "мета-игрок " + player, seed, new List<QaAction>(), 0);
                }
            }

            Corrupted(findings, seed);
        }

        private List<MetaOp> Generate(Random rng, int count)
        {
            var ops = new List<MetaOp>();
            var day = 20_000;
            for (var i = 0; i < count; i++)
            {
                var roll = rng.NextDouble();
                if (roll < 0.12)
                {
                    // Часы: обычно завтра, иногда тот же день, назад, на месяц назад, на год вперёд.
                    day += rng.Next(10) switch
                    {
                        < 5 => 1,
                        5 => 0,
                        6 => -1,
                        7 => -30,
                        8 => 365,
                        _ => rng.Next(2, 6),
                    };
                    ops.Add(new MetaOp("day", day));
                    continue;
                }

                ops.Add(rng.Next(17) switch
                {
                    0 or 1 or 2 => new MetaOp("win", rng.Next(1, _levelCount + 1), rng.Next(1, 4)),
                    3 => new MetaOp("lose", rng.Next(1, _levelCount + 1)),
                    4 => new MetaOp("daily"),
                    5 => new MetaOp("pattern"),
                    6 => new MetaOp("missions"),
                    7 => new MetaOp("chest", rng.Next(0, 3), rng.Next(0, 3)),
                    8 => new MetaOp("achievement", rng.Next(Achievements.All.Length)),
                    9 => new MetaOp("set", rng.Next(YarnCatalog.Sets.Length)),
                    10 => new MetaOp("collection", rng.Next(Collections.All.Length)),
                    11 => new MetaOp("buy", rng.Next(4)),
                    12 => new MetaOp("yarn", rng.Next(YarnCatalog.Count)),
                    13 => new MetaOp("reload"),
                    14 => new MetaOp("record", rng.Next(1, _levelCount + 1), rng.Next(5, 80)),
                    15 => new MetaOp("mission-progress", rng.Next(1, 4), rng.Next(0, 300)),
                    _ => rng.Next(7) switch
                    {
                        0 => new MetaOp("bad-collection", rng.Next(2) == 0 ? -1 : 999),
                        1 => new MetaOp("bad-yarn", rng.Next(2) == 0 ? -5 : 9_999),
                        2 => new MetaOp("bad-chest", -1, 99),
                        3 => new MetaOp("bad-win", rng.Next(2) == 0 ? 0 : _levelCount + 7, 3),
                        4 => new MetaOp("bad-stock", rng.Next(4), -5),
                        5 => new MetaOp("bad-coins", -1_000),
                        _ => new MetaOp("bad-stars", rng.Next(1, _levelCount + 1), 9),
                    },
                });
            }

            return ops;
        }

        /// <summary>Сыграть действия; вернуть первые срабатывания каждой проверки (с номером действия).</summary>
        private List<(Violation V, int Index)> Play(IReadOnlyList<MetaOp> ops, out int done)
        {
            var hits = new List<(Violation, int)>();
            var p = new ProgressService(new PlayerProgress(), _levelCount);
            var today = 20_000;
            // Ключ награды → сколько раз выдана и сколько было переводов часов назад к моменту первой выдачи.
            var granted = new Dictionary<string, (int Count, int Back)>();
            var back = 0;
            var tainted = false;
            done = 0;
            for (var i = 0; i < ops.Count; i++)
            {
                var op = ops[i];
                var before = Snapshot(p);
                var found = new List<Violation>();
                void Fail(string kind, Severity s, string title, string detail)
                {
                    if ((tainted || op.IsRobustness) && s is Severity.Bug or Severity.Crash)
                    {
                        kind += "~после-кривого-вызова";
                        s = Severity.Robustness;
                        title += " (после кривого вызова)";
                    }

                    found.Add(new Violation("meta:" + kind, s, title, detail));
                }

                // Награда за день или предмет — ровно один раз. Повтор после перевода часов назад — «фарм часами»
                // (внимание: защита от него — решение дизайна), без перевода — ошибка.
                void Once(string key, bool success, string title)
                {
                    if (!success)
                    {
                        return;
                    }

                    var (count, firstBack) = granted.TryGetValue(key, out var g) ? g : (0, back);
                    granted[key] = (count + 1, firstBack);
                    if (count == 0)
                    {
                        return;
                    }

                    var kind = key.Split(':')[0];
                    if (back > firstBack)
                    {
                        Fail("clock-farm:" + kind, Severity.Warning, title + " после перевода часов назад",
                            $"ключ {key}: награда выдана {count + 1} раз; между выдачами часы переводили назад");
                    }
                    else
                    {
                        Fail("double:" + kind, Severity.Bug, title, $"ключ {key}, выдано {count + 1} раз");
                    }
                }

                var rng = new Random(i * 31 + 7);
                try
                {
                    switch (op.Kind)
                    {
                        case "day":
                            back += op.A < today ? 1 : 0;
                            today = op.A;
                            break;
                        case "win":
                            if (p.IsAvailable(op.A))
                            {
                                p.CompleteLevel(op.A, false, op.B);
                                p.RecordResult(op.A, 20, op.B == 3);
                            }

                            break;
                        case "lose":
                            if (!p.IsCompleted(op.A))
                            {
                                p.BreakStreak();
                            }

                            break;
                        case "daily":
                            Once("daily:" + today, p.ClaimDaily(today, rng) != null, "подарок дня выдан дважды за один день");
                            break;

                        case "pattern":
                            Once("pattern:" + today, p.CompletePattern(today, rng) != null, "награда за узор дня выдана дважды за день");
                            break;
                        case "missions":
                            Once("missions:" + today, p.ClaimMissions(today, rng) != null, "награда за задания выдана дважды за день");
                            break;
                        case "mission-progress":
                            p.RecordMissions(today, op.A, op.A == 3, 1, 1, 5, op.B);
                            break;
                        case "chest":
                        {
                            var number = p.ChestsOpened;
                            var contents = p.PeekChest();
                            var ok = p.ClaimChest(op.A % Math.Max(1, contents.Boosters.Length), op.B % Math.Max(1, contents.Yarns.Length)) != null;
                            Once("chest:" + number, ok, "один и тот же сундук открыт дважды");
                            break;
                        }

                        case "achievement":
                            Once("achievement:" + op.A, p.ClaimAchievement(Achievements.All[op.A], rng) != null, "достижение выдано дважды");
                            break;
                        case "set":
                            Once("set:" + op.A, p.ClaimSet(YarnCatalog.Sets[op.A], rng) != null, "набор пряжи выдан дважды");
                            break;
                        case "collection":
                            Once("collection:" + op.A, p.ClaimCollection(op.A, rng) != null, "награда коллекции выдана дважды");
                            break;
                        case "buy":
                        {
                            var type = (BoosterType)op.A;
                            var coins = p.Coins;
                            var stock = p.Stock(type);
                            var pay = p.Pay(type);
                            if (pay == Payment.NotEnough && (stock > 0 || coins >= Economy.Price(type)))
                            {
                                Fail("buy-refused", Severity.Bug, "бустер по карману, а не продаётся", $"{type}: монет {coins}, запас {stock}");
                            }

                            if (pay == Payment.FromStock && p.Stock(type) != stock - 1 || pay == Payment.WithCoins && p.Coins != coins - Economy.Price(type))
                            {
                                Fail("buy-charge", Severity.Bug, "бустер списан неправильно", $"{type}: {pay}, монет {coins}→{p.Coins}, запас {stock}→{p.Stock(type)}");
                            }

                            break;
                        }

                        case "yarn":
                            if (p.HasYarn(op.A))
                            {
                                p.SetActiveYarn(op.A);
                            }

                            break;
                        case "record":
                            p.RecordResult(op.A, op.B, op.B < 30);
                            break;
                        case "reload":
                        {
                            var text = JsonSerializer.Serialize(p.Data, Json);
                            var copy = new ProgressService(JsonSerializer.Deserialize<PlayerProgress>(text, Json)!, _levelCount);
                            if (JsonSerializer.Serialize(copy.Data, Json) != text || Snapshot(copy) != Snapshot(p))
                            {
                                Fail("reload", Severity.Bug, "сохранение и загрузка изменили прогресс", $"до  {Snapshot(p)}\nпосле {Snapshot(copy)}");
                            }

                            p = copy;
                            break;
                        }

                        case "bad-collection":
                            p.ClaimCollection(op.A, rng);
                            break;
                        case "bad-yarn":
                            p.SetActiveYarn(op.A);
                            p.AddYarn(op.A);
                            break;
                        case "bad-chest":
                            p.ClaimChest(op.A, op.B);
                            break;
                        case "bad-win":
                            p.CompleteLevel(op.A, false, op.B);
                            break;
                        case "bad-stock":
                            p.AddStock((BoosterType)op.A, op.B);
                            break;
                        case "bad-coins":
                            p.AddCoins(op.A);
                            break;
                        case "bad-stars":
                            p.CompleteLevel(op.A, false, op.B);
                            break;
                    }

                    Invariants(p, before, today, Fail);
                }
                catch (Exception e)
                {
                    Fail("throw:" + op.Kind.Split(' ')[0] + ":" + e.GetType().Name, op.IsRobustness ? Severity.Robustness : Severity.Crash,
                        op.IsRobustness ? $"прогресс падает на кривом вызове «{op.Kind}»" : $"прогресс упал на «{op.Kind}»",
                        $"{e.GetType().Name}: {e.Message}\n{string.Join("\n", (e.StackTrace ?? "").Split('\n').Take(5).Select(l => l.Trim()))}");
                }

                done++;
                tainted |= op.IsRobustness;
                foreach (var v in found)
                {
                    if (hits.All(h => h.Item1.Kind != v.Kind))
                    {
                        hits.Add((v, i));
                    }
                }

                if (found.Any(v => v.Kind.StartsWith("meta:throw", StringComparison.Ordinal)))
                {
                    break;
                }
            }

            return hits;
        }

        private void Invariants(ProgressService p, string before, int today, Action<string, Severity, string, string> fail)
        {
            var d = p.Data;
            if (p.Coins < 0)
            {
                fail("coins-negative", Severity.Bug, "монеты ушли в минус", $"монет {p.Coins}");
            }

            foreach (BoosterType t in Enum.GetValues(typeof(BoosterType)))
            {
                if (p.Stock(t) < 0)
                {
                    fail("stock-negative", Severity.Bug, "запас бустера ушёл в минус", $"{t}: {p.Stock(t)}");
                }
            }

            if (d.stars.Any(s => s < 0 || s > 3))
            {
                fail("stars-range", Severity.Bug, "звёзд за уровень вне 0..3", string.Join(",", d.stars));
            }

            if (p.TotalStars != d.stars.Where(s => s > 0).Sum(s => Math.Min(3, s)) && p.TotalStars != d.stars.Sum())
            {
                fail("stars-total", Severity.Bug, "сумма звёзд не сходится", $"TotalStars {p.TotalStars}, по уровням {d.stars.Sum()}");
            }

            if (p.CurrentLevel < 1 || p.CurrentLevel > p.LevelCount)
            {
                fail("current-level", Severity.Bug, "текущий уровень вне границ", $"{p.CurrentLevel} из {p.LevelCount}");
            }

            if (p.ChestProgress < 0 || p.ChestsOpened * ProgressService.ChestEvery > p.TotalStars)
            {
                fail("chest-count", Severity.Bug, "сундуков открыто больше, чем накоплено звёзд", $"открыто {p.ChestsOpened}, звёзд {p.TotalStars}");
            }

            if (d.completed.Distinct().Count() != d.completed.Count)
            {
                fail("completed-twice", Severity.Bug, "уровень записан пройденным дважды", string.Join(",", d.completed));
            }

            foreach (var list in new[] { d.achievementsClaimed, d.yarnSetsClaimed, d.collectionsClaimed })
            {
                if (list.Distinct().Count() != list.Count)
                {
                    fail("claimed-twice", Severity.Bug, "награда записана полученной дважды", string.Join(",", list));
                }
            }

            if (p.PendingClaims < 0)
            {
                fail("pending", Severity.Bug, "отрицательное число ждущих наград", p.PendingClaims.ToString());
            }

            var index = p.DailyIndex(today);
            if (index < 0 || index >= DailyGift.Days.Length)
            {
                fail("daily-index", Severity.Bug, "день недели подарков вне 0..6", index.ToString());
            }

            if (p.PatternStreak(today) < 0)
            {
                fail("pattern-streak", Severity.Bug, "отрицательная серия узоров", p.PatternStreak(today).ToString());
            }

            var missions = p.MissionsOpen ? p.Missions(today) : new List<Mission>();
            if (missions.Any(m => m.Target <= 0))
            {
                fail("mission-target", Severity.Bug, "задание дня с целью 0 или меньше", string.Join(", ", missions.Select(m => m.Target)));
            }

            // Ничто не отнимается: пройденное, звёзды, дальность прохождения.
            var was = Parse(before);
            var now = Parse(Snapshot(p));
            if (now.Completed < was.Completed || now.Stars < was.Stars || now.Next < was.Next || now.Chests < was.Chests)
            {
                fail("progress-lost", Severity.Bug, "прогресс уменьшился", $"было {before}\nстало {Snapshot(p)}");
            }
        }

        private static string Snapshot(ProgressService p) =>
            $"монет {p.Coins} · уровень {p.CurrentLevel} · пройдено {p.Data.completed.Count} · звёзд {p.TotalStars} · сундуков {p.ChestsOpened} · " +
            $"далее {p.Data.nextLevel} · серия {p.Streak} · мотков {p.YarnKinds}";

        private static (int Completed, int Stars, int Next, int Chests) Parse(string s)
        {
            int N(string label)
            {
                var i = s.IndexOf(label + " ", StringComparison.Ordinal) + label.Length + 1;
                var j = i;
                while (j < s.Length && (char.IsDigit(s[j]) || s[j] == '-'))
                {
                    j++;
                }

                return int.Parse(s.Substring(i, j - i));
            }

            return (N("пройдено"), N("звёзд"), N("далее"), N("сундуков"));
        }

        /// <summary>ddmin по действиям меты.</summary>
        private List<MetaOp> Shrink(List<MetaOp> ops, string kind)
        {
            bool Fails(List<MetaOp> c) => Play(c, out _).Any(h => h.V.Kind == kind);
            if (!Fails(ops))
            {
                return ops;
            }

            var n = 2;
            var budget = 1500;
            while (ops.Count >= 2 && budget-- > 0)
            {
                var chunk = (int)Math.Ceiling(ops.Count / (double)n);
                var reduced = false;
                for (var start = 0; start < ops.Count; start += chunk)
                {
                    var complement = ops.Take(start).Concat(ops.Skip(start + chunk)).ToList();
                    if (complement.Count < ops.Count && Fails(complement))
                    {
                        ops = complement;
                        n = Math.Max(n - 1, 2);
                        reduced = true;
                        break;
                    }
                }

                if (!reduced)
                {
                    if (n >= ops.Count)
                    {
                        break;
                    }

                    n = Math.Min(ops.Count, n * 2);
                }
            }

            return ops;
        }

        /// <summary>Испорченные сохранения: старые версии (пустые списки, нет полей) и мусор. Игра не должна падать.</summary>
        private void Corrupted(Findings findings, int seed)
        {
            var rng = new Random(seed * 13 + 5);
            for (var i = 0; i < 300; i++)
            {
                var data = new PlayerProgress();
                var garbage = i % 3 == 2;
                var changes = new List<string>();
                void Set(string what, Action change)
                {
                    change();
                    changes.Add(what);
                }

                for (var k = rng.Next(1, 6); k > 0; k--)
                {
                    switch (rng.Next(garbage ? 12 : 6))
                    {
                        case 0: Set("nextLevel далеко за концом", () => data.nextLevel = _levelCount + rng.Next(1, 50)); break;
                        case 1: Set("звёзд больше, чем уровней", () => data.stars = Enumerable.Repeat(3, _levelCount + 5).ToList()); break;
                        case 2: Set("мотков больше каталога", () => data.yarns = Enumerable.Repeat(1, YarnCatalog.Count + 3).ToList()); break;
                        case 3: Set("активный моток которого нет", () => data.activeYarn = rng.Next(0, YarnCatalog.Count)); break;
                        case 4: Set("сундуков открыто больше звёзд", () => data.chestsOpened = rng.Next(1, 30)); break;
                        case 5: Set("старый формат заданий", () => { data.missionDay = 20_000; data.missionKinds = new List<int> { 0 }; data.missionTargets.Clear(); }); break;
                        case 6: Set("отрицательные монеты", () => data.coins = -rng.Next(1, 500)); break;
                        case 7: Set("null вместо списков", () => { data.stars = null!; data.completed = null!; data.yarns = null!; }); break;
                        case 8: Set("nextLevel 0 или меньше", () => data.nextLevel = -rng.Next(0, 5)); break;
                        case 9: Set("звезда 9 за уровень", () => data.stars = new List<int> { 9, -1, 3 }); break;
                        case 10: Set("неизвестный тип задания", () => { data.missionDay = 20_000; data.missionKinds = new List<int> { 99 }; data.missionTargets = new List<int> { 1 }; data.missionValues = new List<int> { 0 }; }); break;
                        default: Set("выбранный моток вне каталога", () => data.activeYarn = 9_999); break;
                    }
                }

                try
                {
                    var p = new ProgressService(data, _levelCount);
                    _ = p.CurrentLevel;
                    _ = p.TotalStars;
                    _ = p.PendingClaims;
                    _ = p.ChestProgress;
                    _ = p.ActiveYarn;
                    _ = p.YarnKinds;
                    _ = p.WarmStartFor(p.CurrentLevel);
                    if (p.MissionsOpen)
                    {
                        _ = p.Missions(20_000);
                    }

                    _ = p.DailyIndex(20_000);
                    if (p.CanOpenChest)
                    {
                        _ = p.PeekChest();
                    }

                    p.CompleteLevel(p.CurrentLevel, false, 3);
                }
                catch (Exception e)
                {
                    var what = string.Join(", ", changes);
                    findings.Add(new Violation("meta:save-" + (garbage ? "garbage" : "old") + ":" + e.GetType().Name,
                            garbage ? Severity.Robustness : Severity.Crash,
                            garbage ? "игра падает на испорченном сохранении" : "игра падает на сохранении старого вида",
                            $"сохранение: {what}\n{e.GetType().Name}: {e.Message}\n{string.Join("\n", (e.StackTrace ?? "").Split('\n').Take(5).Select(l => l.Trim()))}"),
                        MetaLevel, "сохранения", seed, new List<QaAction>(), 0);
                }
            }
        }
    }
}
