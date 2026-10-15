using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using KnittedPicture.Core;

namespace KnittedPicture.Playtest
{
    /// <summary>
    /// «Месяц игры» за секунды: игрок заходит каждый день (иногда пропускает), забирает подарок, проходит
    /// несколько уровней, тратит бустеры, открывает сундуки, получает награды за задания, достижения,
    /// наборы пряжи и коллекции. После каждого действия — проверки: монеты и запасы не уходят в минус,
    /// награду нельзя забрать дважды, сохранение и загрузка ничего не теряют.
    /// </summary>
    public sealed class MetaSim
    {
        private static readonly JsonSerializerOptions Json = new JsonSerializerOptions { IncludeFields = true };
        private readonly Findings _f;
        private readonly IReadOnlyList<LevelData> _levels;

        public MetaSim(Findings findings, IReadOnlyList<LevelData> levels)
        {
            _f = findings;
            _levels = levels;
        }

        public void Run(int players, int days, int seed)
        {
            for (var player = 0; player < players; player++)
            {
                var rng = new Random(seed * 977 + player);
                var progress = new ProgressService(new PlayerProgress(), _levels.Count);
                var start = 20000;
                var repro = $"--meta-player {player} --seed {seed}";
                for (var day = 0; day < days; day++)
                {
                    // Иногда игрок пропускает день или два: подарки должны ставиться на паузу, а не ломаться.
                    if (rng.NextDouble() < 0.15)
                    {
                        continue;
                    }

                    var today = start + day;
                    var ctx = $"игрок {player}, день {day}";
                    Day(progress, today, rng, ctx, repro);
                    progress = SaveLoad(progress, ctx, repro);
                }

                _f.Count("дней игры прожито", days);
                _f.Count("уровней пройдено в мета-симуляции", progress.Data.completed.Count);
            }
        }

        private void Day(ProgressService p, int today, Random rng, string ctx, string repro)
        {
            if (p.CanClaimDaily(today))
            {
                Expect(p.ClaimDaily(today, rng) != null, "подарок дня не выдался, хотя был доступен", ctx, repro);
                Expect(!p.CanClaimDaily(today) && p.ClaimDaily(today, rng) == null, "подарок дня можно забрать дважды за день", ctx, repro);
            }

            var sessions = rng.Next(1, 6);
            for (var i = 0; i < sessions; i++)
            {
                PlayLevel(p, today, rng, ctx, repro);
            }

            if (p.PatternOpen && !p.PatternDone(today) && rng.NextDouble() < 0.6)
            {
                Expect(p.CompletePattern(today, rng) != null || p.PatternDone(today), "узор дня не засчитался", ctx, repro);
                Expect(p.CompletePattern(today, rng) == null, "награду за узор дня можно получить дважды", ctx, repro);
            }

            while (p.CanOpenChest)
            {
                var opened = p.ChestsOpened;
                var contents = p.PeekChest();
                Expect(p.ClaimChest(rng.Next(contents.Boosters.Length), rng.Next(contents.Yarns.Length)) != null, "сундук готов, но не открылся", ctx, repro);
                Expect(p.ChestsOpened == opened + 1, "счётчик сундуков не вырос", ctx, repro);
                _f.Count("сундуков открыто");
            }

            Expect(p.ClaimChest(0, 0) == null, "открылся неготовый сундук", ctx, repro);

            while (p.CanClaimMarathon(today))
            {
                var claimed = p.MarathonClaimed(today);
                Expect(p.ClaimMarathon(today, rng) != null && p.MarathonClaimed(today) == claimed + 1, "ступень марафона не выдалась", ctx, repro);
            }

            Expect(p.ClaimMarathon(today, rng) == null, "марафон выдал неготовую ступень", ctx, repro);

            if (p.CanClaimMissions(today))
            {
                Expect(p.ClaimMissions(today, rng) != null, "награда за задания не выдалась", ctx, repro);
                Expect(p.ClaimMissions(today, rng) == null, "награду за задания можно забрать дважды", ctx, repro);
                _f.Count("наград за задания");
            }

            foreach (var a in Achievements.All.Where(p.CanClaimAchievement).ToList())
            {
                Expect(p.ClaimAchievement(a, rng) != null && p.ClaimAchievement(a, rng) == null, $"достижение «{a.Title}» выдаётся не ровно один раз", ctx, repro);
            }

            foreach (var set in YarnCatalog.Sets.Where(p.CanClaimSet).ToList())
            {
                Expect(p.ClaimSet(set, rng) != null && p.ClaimSet(set, rng) == null, $"набор «{set.Name}» выдаётся не ровно один раз", ctx, repro);
            }

            for (var c = 0; c < Collections.All.Length; c++)
            {
                if (p.CanClaimCollection(c))
                {
                    Expect(p.ClaimCollection(c, rng) != null && p.ClaimCollection(c, rng) == null, $"коллекция {c} выдаётся не ровно один раз", ctx, repro);
                }
            }

            // Иногда игрок меняет моток «в работе» на любой найденный.
            var owned = Enumerable.Range(0, YarnCatalog.Count).Where(p.HasYarn).ToList();
            if (owned.Count > 0 && rng.NextDouble() < 0.2)
            {
                p.SetActiveYarn(owned[rng.Next(owned.Count)]);
            }

            Sane(p, ctx, repro);
        }

        private void PlayLevel(ProgressService p, int today, Random rng, string ctx, string repro)
        {
            // Пройдя всё, игрок перепроходит старые уровни.
            var id = p.AllDone ? rng.Next(1, _levels.Count + 1) : p.CurrentLevel;
            Expect(p.IsAvailable(id), $"уровень {id} недоступен, хотя игра его предлагает", ctx, repro);
            var level = _levels[id - 1];

            // Бустеры: иногда покупает или берёт из запаса открытый бустер.
            foreach (BoosterType type in Enum.GetValues(typeof(BoosterType)))
            {
                if (p.IsUnlocked(type, id) && rng.NextDouble() < 0.15 && p.CanAfford(type))
                {
                    var coins = p.Coins;
                    var stock = p.Stock(type);
                    var payment = p.Pay(type);
                    Expect(payment != Payment.NotEnough, "бустер был по карману, но не купился", ctx, repro);
                    Expect(payment == Payment.FromStock ? p.Stock(type) == stock - 1 : p.Coins == coins - Economy.Price(type), "бустер списан неправильно", ctx, repro);
                    _f.Count("бустеров использовано");
                }
            }

            if (rng.NextDouble() < 0.25)
            {
                // Проигрыш: перезапуск нового уровня обрывает серию побед.
                if (!p.IsCompleted(id))
                {
                    p.BreakStreak();
                }

                _f.Count("проигрышей");
                return;
            }

            var stars = rng.Next(1, 4);
            var coinsBefore = p.Coins;
            var reward = p.CompleteLevel(id, level.hard, stars);
            Expect(p.IsCompleted(id), $"уровень {id} пройден, но не отмечен", ctx, repro);
            Expect(p.Coins >= coinsBefore, "за победу монет стало меньше", ctx, repro);
            Expect(p.StarsOf(id) >= stars, "звёзды за уровень не сохранились", ctx, repro);
            p.RecordResult(id, rng.Next(10, 60), stars == 3);
            p.RecordLevel(rng.Next(40, 200), rng.Next(0, 3), rng.Next(0, 4), rng.Next(0, 12));
            p.RecordMissions(today, stars, stars == 3, rng.Next(0, 4), rng.Next(0, 3), rng.Next(0, 12), rng.Next(40, 200));
            p.RecordMarathon(today, rng.Next(40, 200));
            p.TakeInterstitialSlot(id);
            _f.Count("побед в мета-симуляции");
        }

        private void Sane(ProgressService p, string ctx, string repro)
        {
            Expect(p.Coins >= 0, $"монеты ушли в минус: {p.Coins}", ctx, repro);
            foreach (BoosterType type in Enum.GetValues(typeof(BoosterType)))
            {
                Expect(p.Stock(type) >= 0, $"запас «{type}» ушёл в минус", ctx, repro);
            }

            Expect(p.CurrentLevel >= 1 && p.CurrentLevel <= p.LevelCount, $"текущий уровень вне границ: {p.CurrentLevel}", ctx, repro);
            Expect(p.ChestProgress >= 0, "прогресс сундука отрицательный", ctx, repro);
            Expect(p.Data.completed.Distinct().Count() == p.Data.completed.Count, "уровень записан пройденным дважды", ctx, repro);
        }

        /// <summary>Сохранить и загрузить заново: после этого всё должно совпадать (как перезапуск игры).</summary>
        private ProgressService SaveLoad(ProgressService p, string ctx, string repro)
        {
            var text = JsonSerializer.Serialize(p.Data, Json);
            var copy = new ProgressService(JsonSerializer.Deserialize<PlayerProgress>(text, Json)!, p.LevelCount);
            Expect(JsonSerializer.Serialize(copy.Data, Json) == text, "сохранение и загрузка изменили прогресс", ctx, repro);
            Expect(copy.Coins == p.Coins && copy.CurrentLevel == p.CurrentLevel && copy.ChestProgress == p.ChestProgress,
                "после загрузки другие монеты, уровень или сундук", ctx, repro);
            return copy;
        }

        private void Expect(bool ok, string kind, string ctx, string repro)
        {
            if (!ok)
            {
                _f.Fail(kind, ctx, repro);
            }
        }
    }
}
