using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using KnittedPicture.Core;

namespace KnittedPicture.Playtest
{
    /// <summary>
    /// Баланс экономики по типам игроков. Каждый тип живёт N дней на настоящем ProgressService (подарки, сундуки,
    /// задания, узор дня, достижения, наборы), а уровни «проигрывает» с вероятностью по сложности. На проигрыше
    /// решает по характеру: спасается бустером (из запаса или за монеты), смотрит видео или начинает заново.
    /// Итог — таблица по дням: монеты на руках, сколько заработано и потрачено, запас бустеров, на сколько
    /// проигрышей хватало денег на спасение, когда пройдены все уровни. По ней видно, не копятся ли монеты горой
    /// и не кончаются ли раньше, чем бустеры становятся нужны.
    /// </summary>
    public sealed class EconomySim
    {
        /// <summary>Характер игрока.</summary>
        public sealed class Archetype
        {
            public Archetype(string name, int minLevels, int maxLevels, double skipDay, double rescueOnFail, double preBuy, double watchAds,
                double skill, double decor)
            {
                Decor = decor;
                Name = name;
                MinLevels = minLevels;
                MaxLevels = maxLevels;
                SkipDay = skipDay;
                RescueOnFail = rescueOnFail;
                PreBuy = preBuy;
                WatchAds = watchAds;
                Skill = skill;
            }

            public string Name { get; }

            public int MinLevels { get; }

            public int MaxLevels { get; }

            /// <summary>Доля пропущенных дней.</summary>
            public double SkipDay { get; }

            /// <summary>На проигрыше готов спастись бустером, если по карману.</summary>
            public double RescueOnFail { get; }

            /// <summary>Берёт бустер до затора «на всякий случай» (если по карману).</summary>
            public double PreBuy { get; }

            /// <summary>Смотрит видео: удвоить награду, спастись бесплатно.</summary>
            public double WatchAds { get; }

            /// <summary>Множитель шанса проиграть: меньше — сильнее игрок.</summary>
            public double Skill { get; }

            /// <summary>Покупает рамки альбома, когда монет хватает с запасом на спасение (шанс в день).</summary>
            public double Decor { get; }
        }

        public static readonly Archetype[] Types =
        {
            new Archetype("Случайный", 1, 2, 0.35, 0.3, 0.0, 0.1, 1.2, 0.3),
            new Archetype("Обычный", 2, 4, 0.15, 0.6, 0.05, 0.3, 1.0, 0.5),
            new Archetype("Увлечённый", 4, 8, 0.05, 0.8, 0.1, 0.5, 0.8, 0.6),
            new Archetype("Транжира", 3, 5, 0.1, 1.0, 0.35, 0.2, 1.0, 0.9),
            new Archetype("Копилка", 3, 5, 0.1, 0.0, 0.0, 0.0, 1.0, 0.15),
        };

        private static readonly int[] Checkpoints = { 1, 3, 7, 14, 30, 60 };

        private readonly IReadOnlyList<LevelData> _levels;

        /// <summary>
        /// Учитывать «Не сдаёмся»: с 4-й попытки нового уровня один раз бесплатные ножницы (если открыты) и подсказка хода.
        /// Допущения: ножницы без траты запаса и звезды работают как половина шанса провала (игрок срежет ими затор),
        /// подсказка хода даёт ещё −10% к шансу проигрыша; без открытых ножниц (уровни до 8-го) только подсказка.
        /// </summary>
        public bool StuckHelp { get; set; } = true;

        private const double StuckScissorsFactor = 0.5;
        private const double StuckHintFactor = 0.9;

        public EconomySim(IReadOnlyList<LevelData> levels)
        {
            _levels = levels;
        }

        private sealed class Stats
        {
            public readonly Dictionary<int, List<int>> Coins = new Dictionary<int, List<int>>();
            public readonly Dictionary<int, List<int>> Stock = new Dictionary<int, List<int>>();
            public readonly Dictionary<int, List<int>> Level = new Dictionary<int, List<int>>();
            public long Earned;
            public readonly Dictionary<string, long> Sources = new Dictionary<string, long>();
            public readonly Dictionary<string, long> StockSources = new Dictionary<string, long>();
            public long Spent;
            public int Fails;
            public int FailsAffordable;
            public int FailsRescued;
            public int BoostersUsed;
            public int StuckHelps;
            public int StuckScissors;
            public int AttemptsToWin;
            public int Wins;
            public long DecorSpent;
            public readonly Dictionary<int, List<int>> Frames = new Dictionary<int, List<int>>();
            public readonly List<int> AllDoneDay = new List<int>();
            public int Players;
        }

        public string Run(int players, int days, int seed)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Экономика: {players} игроков каждого типа × {days} дней");
            sb.AppendLine();
            sb.AppendLine("| Тип | День | Монеты (медиана) | Запас бустеров | Уровень | Рамок куплено |");
            sb.AppendLine("|---|---|---|---|---|---|");
            var summary = new StringBuilder();
            var sources = new StringBuilder();
            sources.AppendLine("| Тип | Откуда монеты | Откуда бустеры (штук на игрока) |");
            sources.AppendLine("|---|---|---|");
            summary.AppendLine("| Тип | Заработано в день | Потрачено в день | Из них на рамки | Бустеров за уровень | Бустеров потрачено на игрока | «Не сдаёмся»: помощей / ножниц на игрока | Попыток на новый уровень | Проигрышей со спасением по карману | Спасся | Все уровни к дню |");
            summary.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|");
            foreach (var type in Types)
            {
                var stats = new Stats();
                for (var p = 0; p < players; p++)
                {
                    Live(type, days, new Random(seed * 7919 + p * 31 + type.Name.Length), stats);
                }

                foreach (var day in Checkpoints.Where(d => d <= days))
                {
                    sb.AppendLine($"| {type.Name} | {day} | {Median(stats.Coins[day])} | {Median(stats.Stock[day])} | {Median(stats.Level[day])} | {Median(stats.Frames[day])} |");
                }

                var playedDays = (double)players * days;
                var levelsPlayed = Math.Max(1, stats.Level.Count > 0 ? stats.Level[Checkpoints.Where(d => d <= days).Last()].Sum() : 1);
                var allDone = stats.AllDoneDay.Count == 0 ? "не прошли" : $"{Median(stats.AllDoneDay)} (у {stats.AllDoneDay.Count * 100 / players}%)";
                sources.AppendLine($"| {type.Name} | " + string.Join(", ", stats.Sources.OrderByDescending(kv => kv.Value)
                    .Select(kv => $"{kv.Key} {kv.Value * 100 / Math.Max(1, stats.Earned)}%")) + " | " +
                    string.Join(", ", stats.StockSources.Where(kv => kv.Value > 0).OrderByDescending(kv => kv.Value)
                        .Select(kv => $"{kv.Key} {kv.Value / (double)players:0.0}")) + " |");
                summary.AppendLine($"| {type.Name} | {stats.Earned / playedDays:0} | {stats.Spent / playedDays:0} | {stats.DecorSpent / playedDays:0} | {(double)stats.BoostersUsed / levelsPlayed:0.00} | {(double)stats.BoostersUsed / players:0.0} | " +
                    $"{(double)stats.StuckHelps / players:0.00} / {(double)stats.StuckScissors / players:0.00} | {(double)stats.AttemptsToWin / Math.Max(1, stats.Wins):0.00} | " +
                    $"{Percent(stats.FailsAffordable, stats.Fails)} | {Percent(stats.FailsRescued, stats.Fails)} | {allDone} |");
            }

            sb.AppendLine();
            sb.Append(summary);
            sb.AppendLine();
            sb.Append(sources);
            return sb.ToString();
        }

        private void Live(Archetype type, int days, Random rng, Stats stats)
        {
            var p = new ProgressService(new PlayerProgress(), _levels.Count);
            const int start = 20000;
            stats.Players++;
            var allDoneRecorded = false;
            for (var day = 1; day <= days; day++)
            {
                var today = start + day;
                if (day > 1 && rng.NextDouble() < type.SkipDay)
                {
                    Record(day, p, stats);
                    continue;
                }

                p.Visit(today);
                Earn(stats, p, () => p.ClaimDaily(today, rng), "подарок дня");
                var sessions = rng.Next(type.MinLevels, type.MaxLevels + 1);
                for (var i = 0; i < sessions; i++)
                {
                    Play(type, p, today, rng, stats);
                }

                if (p.PatternOpen && rng.NextDouble() < 0.5)
                {
                    Earn(stats, p, () => p.CompletePattern(today, rng), "узор дня");
                }

                while (p.CanOpenChest)
                {
                    var contents = p.PeekChest();
                    Earn(stats, p, () => p.ClaimChest(rng.Next(contents.Boosters.Length), rng.Next(contents.Yarns.Length)), "сундук");
                }

                Earn(stats, p, () => p.CanClaimMissions(today) ? p.ClaimMissions(today, rng) : null, "задания");
                // Выставка недели: три самые подходящие к теме картинки в лучшей своей рамке.
                if (p.ExhibitionOpen && !p.ExhibitionDone(today))
                {
                    var theme = Exhibition.ThemeOf(WeeklyMarathon.WeekOf(today));
                    var owned = AlbumFrames.All.Where(f => p.FrameOwned(f.Id)).ToList();
                    var picks = p.Data.completed.Select(n => _levels[n - 1])
                        .Select(l => (l, f: owned.OrderByDescending(f => Exhibition.Score(l, p.StarsOf(l.id), f, theme)).First()))
                        .OrderByDescending(x => Exhibition.Score(x.l, p.StarsOf(x.l.id), x.f, theme)).Take(Exhibition.Pictures).ToList();
                    Earn(stats, p, () => p.SubmitExhibition(today, picks.Select(x => x.l).ToList(), picks.Select(x => x.f.Id).ToList(), rng)?.Granted,
                        "выставка");
                }

                while (p.CanClaimSeason(today))
                {
                    Earn(stats, p, () => p.ClaimSeason(today, rng)?.Granted, "сезон");
                }

                if (p.ComebackPending)
                {
                    Earn(stats, p, () => p.ClaimComeback(rng), "посылка");
                }

                foreach (var a in Achievements.All.Where(p.CanClaimAchievement).ToList())
                {
                    Earn(stats, p, () => p.ClaimAchievement(a, rng), "достижения");
                }

                foreach (var set in YarnCatalog.Sets.Where(p.CanClaimSet).ToList())
                {
                    Earn(stats, p, () => p.ClaimSet(set, rng), "наборы пряжи");
                }

                for (var c = 0; c < Collections.All.Length; c++)
                {
                    if (p.CanClaimCollection(c))
                    {
                        var index = c;
                        Earn(stats, p, () => p.ClaimCollection(index, rng), "коллекции");
                    }
                }

                // Рамки альбома: самая дешёвая некупленная, если после покупки останется на спасение.
                if (rng.NextDouble() < type.Decor)
                {
                    var next = AlbumFrames.All.Where(f => f.Source == FrameSource.Shop).OrderBy(f => f.Price).FirstOrDefault(f => !p.FrameOwned(f.Id));
                    if (next != null && p.Coins >= next.Price + DecorReserve)
                    {
                        var before = p.Coins;
                        if (p.BuyFrame(next.Id))
                        {
                            stats.Spent += before - p.Coins;
                            stats.DecorSpent += before - p.Coins;
                        }
                    }
                }

                if (!allDoneRecorded && p.AllDone)
                {
                    allDoneRecorded = true;
                    stats.AllDoneDay.Add(day);
                }

                Record(day, p, stats);
            }
        }

        private static void Earn(Stats stats, ProgressService p, Func<Granted?> claim, string source)
        {
            var before = p.Coins;
            var stock = TotalStock(p);
            claim();
            Note(stats, source, p.Coins - before, TotalStock(p) - stock);
        }

        private static int TotalStock(ProgressService p) => Enum.GetValues(typeof(BoosterType)).Cast<BoosterType>().Sum(p.Stock);

        private static void Note(Stats stats, string source, int coins, int stock)
        {
            stats.Earned += Math.Max(0, coins);
            stats.Sources[source] = (stats.Sources.TryGetValue(source, out var c) ? c : 0) + Math.Max(0, coins);
            stats.StockSources[source] = (stats.StockSources.TryGetValue(source, out var b) ? b : 0) + Math.Max(0, stock);
        }

        private void Play(Archetype type, ProgressService p, int today, Random rng, Stats stats)
        {
            var id = p.AllDone ? rng.Next(1, _levels.Count + 1) : p.CurrentLevel;
            var level = _levels[id - 1];
            var replay = p.IsCompleted(id);
            var boosters = 0;

            // Шанс проиграть: по сложности, моли и номеру уровня; повтор пройденного легче.
            var fail = (level.hard ? 0.45 : 0.18) + (level.timeLimit > 0 ? 0.1 : 0) + Math.Min(0.12, id * 0.002);
            fail *= type.Skill * (replay ? 0.5 : 1.0);

            // «Не сдаёмся»: каждая попытка нового уровня считается, как в игре (App.StartLevel).
            var attempt = p.NextAttempt(id);
            if (StuckHelp && p.TakeStuckHelp(id, attempt))
            {
                stats.StuckHelps++;
                fail *= StuckHintFactor;
                if (p.IsUnlocked(BoosterType.Scissors, id))
                {
                    stats.StuckScissors++;
                    fail *= StuckScissorsFactor;
                }
            }

            if (rng.NextDouble() < type.PreBuy && TryBuy(p, PreBuyType(p, id), stats))
            {
                boosters++;
                fail *= 0.6;
            }

            var rescued = 0;
            var videoRescues = 0;
            while (rng.NextDouble() < fail)
            {
                stats.Fails++;
                var rescue = level.timeLimit > 0 && rng.NextDouble() < 0.5 ? BoosterType.Time : BoosterType.Slot;
                if (!p.IsUnlocked(rescue, id))
                {
                    rescue = BoosterType.Slot;
                }

                var unlocked = p.IsUnlocked(rescue, id);
                if (unlocked && p.CanAffordRescue(rescue, rescued))
                {
                    stats.FailsAffordable++;
                }

                if (unlocked && rng.NextDouble() < type.RescueOnFail && TryRescue(p, rescue, rescued, stats))
                {
                    stats.FailsRescued++;
                    rescued++;
                    fail *= 0.35;
                    continue;
                }

                if (unlocked && videoRescues < Economy.VideoRescuesPerLevel && rng.NextDouble() < type.WatchAds)
                {
                    // Спасение за видео: бесплатно, одно на попытку.
                    videoRescues++;
                    stats.FailsRescued++;
                    rescued++;
                    fail *= 0.35;
                    continue;
                }

                // Начинает заново (на новом уровне серия обрывается) и пробует ещё раз сегодня.
                if (!replay)
                {
                    p.BreakStreak();
                }

                return;
            }

            if (!replay)
            {
                stats.Wins++;
                stats.AttemptsToWin += attempt;
            }

            var stars = Economy.Stars(boosters, rescued);
            var combo = rng.Next(0, 16);
            var coinsBefore = p.Coins;
            var stockBefore = TotalStock(p);
            var reward = p.CompleteLevel(id, level.hard, stars, combo, today);
            Note(stats, replay ? "повтор уровней" : "новые уровни", p.Coins - coinsBefore, TotalStock(p) - stockBefore);
            coinsBefore = p.Coins;
            p.RecordResult(id, rng.Next(10, 60), stars == 3);
            p.RecordLevel(rng.Next(40, 200), rng.Next(0, 3), rng.Next(0, 4), rng.Next(0, 12));
            p.RecordMissions(today, stars, boosters == 0 && rescued == 0, rng.Next(0, 4), rng.Next(0, 3), rng.Next(0, 12), rng.Next(40, 200));
            p.RecordMarathon(today, rng.Next(40, 200));
            if (p.DoublesLeft(today) > 0 && rng.NextDouble() < type.WatchAds)
            {
                // «Удвоить награду за видео» — до Economy.DoublesPerDay раз в день.
                p.AddCoins(reward.Total);
                p.RecordDouble(today);
            }

            Note(stats, "видео ×2", p.Coins - coinsBefore, 0);
        }

        private static BoosterType PreBuyType(ProgressService p, int id) =>
            p.IsUnlocked(BoosterType.Scissors, id) ? BoosterType.Scissors : BoosterType.Slot;

        /// <summary>Сколько монет игрок держит про запас на спасение, прежде чем тратить на рамку.</summary>
        private const int DecorReserve = 900;

        private static bool TryRescue(ProgressService p, BoosterType type, int rescuesBefore, Stats stats)
        {
            var coins = p.Coins;
            if (p.PayRescue(type, rescuesBefore) == Payment.NotEnough)
            {
                return false;
            }

            stats.Spent += coins - p.Coins;
            stats.BoostersUsed++;
            return true;
        }

        private static bool TryBuy(ProgressService p, BoosterType type, Stats stats)
        {
            var coins = p.Coins;
            var payment = p.Pay(type);
            if (payment == Payment.NotEnough)
            {
                return false;
            }

            stats.Spent += coins - p.Coins;
            stats.BoostersUsed++;
            return true;
        }

        private static void Record(int day, ProgressService p, Stats stats)
        {
            if (!Checkpoints.Contains(day))
            {
                return;
            }

            Add(stats.Coins, day, p.Coins);
            Add(stats.Stock, day, Enum.GetValues(typeof(BoosterType)).Cast<BoosterType>().Sum(p.Stock));
            Add(stats.Level, day, p.Data.completed.Count);
            Add(stats.Frames, day, AlbumFrames.All.Count(f => f.Id > 0 && p.FrameOwned(f.Id)));
        }

        private static void Add(Dictionary<int, List<int>> map, int day, int value)
        {
            if (!map.TryGetValue(day, out var list))
            {
                map[day] = list = new List<int>();
            }

            list.Add(value);
        }

        private static int Median(List<int> values)
        {
            var sorted = values.OrderBy(v => v).ToList();
            return sorted.Count == 0 ? 0 : sorted[sorted.Count / 2];
        }

        private static string Percent(int part, int total) => total == 0 ? "—" : $"{part * 100 / total}%";
    }
}
