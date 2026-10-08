using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Xunit;

namespace KnittedPicture.Core.Tests
{
    /// <summary>
    /// Случайная игра по всем уровням: тапы, ножницы с поля и со станка, отмена, слоты, спасение из затора.
    /// После каждого хода проверяются законы игры: петли не появляются и не пропадают, толстый клубок
    /// занимает ровно два слота, клубок не бывает сразу на поле и на станке, отмена возвращает состояние
    /// точно, отклонённый тап ничего не меняет, «поможет слот / ножницы» не обманывает.
    /// </summary>
    public class RandomPlayTests
    {
        private static readonly JsonSerializerOptions Json = new JsonSerializerOptions { IncludeFields = true };

        public static IEnumerable<object[]> LevelFiles() =>
            Directory.GetFiles(LevelsDirectory(), "*.json").OrderBy(f => f).Select(f => new object[] { Path.GetFileName(f) });

        [Theory]
        [MemberData(nameof(LevelFiles))]
        public void Random_play_keeps_the_rules(string fileName)
        {
            var level = JsonSerializer.Deserialize<LevelData>(File.ReadAllText(Path.Combine(LevelsDirectory(), fileName)), Json)!;
            for (var run = 0; run < 25; run++)
            {
                var rng = new Random(run * 7919 + level.id);
                var game = new LoomGame(level);
                var initial = Loops(game);
                var discarded = 0;
                var history = new Stack<(string snap, int discarded)>();
                for (var step = 0; step < 1500 && game.Status != GameStatus.Won && game.Status != GameStatus.TimeUp; step++)
                {
                    CheckLaws(game, initial, discarded, fileName);
                    if (game.Status == GameStatus.Jammed)
                    {
                        if (game.SlotWouldHelp && rng.Next(2) == 0)
                        {
                            Assert.True(game.AddSlot());
                            Assert.NotEqual(GameStatus.Jammed, game.Status);
                            continue;
                        }

                        if (!game.ScissorsWouldHelp)
                        {
                            Assert.False(game.SlotWouldHelp);
                            break;
                        }

                        var pulled = false;
                        for (var c = 0; c < game.ColumnCount && !pulled; c++)
                        {
                            for (var d = 0; d < game.Column(c).Count && !pulled; d++)
                            {
                                if (game.Column(c)[d].Size <= game.FreeSlots)
                                {
                                    history.Push((Snap(game), discarded));
                                    pulled = game.PullSpool(c, d).Accepted;
                                }
                            }
                        }

                        if (!pulled)
                        {
                            var slot = Enumerable.Range(0, game.Slots.Count).First(i => game.Slots[i] != null);
                            var cut = game.Slots[slot]!;

                            // Нужный клубок возвращается на поле со своими петлями, срезается только чужая и лишняя пряжа.
                            var lost = game.ReturnsToField(cut) ? 0 : cut.Loops;
                            history.Push((Snap(game), discarded));
                            discarded += lost;
                            Assert.True(game.CutFromLoom(slot));
                        }

                        continue;
                    }

                    var action = rng.Next(100);
                    if (action < 75)
                    {
                        var before = Snap(game);
                        history.Push((before, discarded));
                        if (!game.TapColumn(rng.Next(game.ColumnCount)).Accepted)
                        {
                            history.Pop();
                            Assert.Equal(before, Snap(game));
                        }
                    }
                    else if (action < 80 && game.CanUndo)
                    {
                        var (snap, was) = history.Pop();
                        Assert.True(game.Undo());
                        discarded = was;
                        Assert.Equal(snap, Snap(game));
                    }
                    else if (action < 82)
                    {
                        var c = rng.Next(game.ColumnCount);
                        if (game.Column(c).Count > 0)
                        {
                            history.Push((Snap(game), discarded));
                            if (!game.PullSpool(c, rng.Next(game.Column(c).Count)).Accepted)
                            {
                                history.Pop();
                            }
                        }
                    }
                    else if (action < 83)
                    {
                        game.AddSlot();
                    }

                    for (var k = rng.Next(1, 8); k > 0 && game.Status == GameStatus.Playing; k--)
                    {
                        var result = game.Step();
                        discarded += result.Completed.Sum(c => c.Leftover);
                        if (!result.Progress)
                        {
                            break;
                        }
                    }
                }

                if (game.Status == GameStatus.Won)
                {
                    Assert.Equal(game.TotalStitches, game.KnittedCount);
                }
            }
        }

        private static void CheckLaws(LoomGame game, int initial, int discarded, string fileName)
        {
            Assert.True(Loops(game) + game.KnittedCount + discarded == initial, $"{fileName}: петли не сходятся");
            var onLoom = new Dictionary<int, int>();
            foreach (var spool in game.Slots)
            {
                if (spool != null)
                {
                    onLoom[spool.Id] = onLoom.TryGetValue(spool.Id, out var n) ? n + 1 : 1;
                }
            }

            foreach (var spool in game.Slots.Where(s => s != null).Distinct())
            {
                Assert.Equal(spool!.Size, onLoom[spool.Id]);
            }

            for (var c = 0; c < game.ColumnCount; c++)
            {
                Assert.DoesNotContain(game.Column(c), s => onLoom.ContainsKey(s.Id));
            }
        }

        private static int Loops(LoomGame game)
        {
            var sum = 0;
            for (var c = 0; c < game.ColumnCount; c++)
            {
                sum += game.Column(c).Sum(s => s.Loops);
            }

            return sum + game.Slots.Where(s => s != null).Distinct().Sum(s => s!.Loops);
        }

        /// <summary>Снимок без хвоста пустых слотов: купленный слот отмена не забирает.</summary>
        private static string Snap(LoomGame game)
        {
            var sb = new StringBuilder();
            for (var x = 0; x < game.Width; x++)
            {
                sb.Append(game.ColumnHeight(x)).Append('.');
            }

            sb.Append('|');
            for (var c = 0; c < game.ColumnCount; c++)
            {
                foreach (var s in game.Column(c))
                {
                    sb.Append($"{s.Id}:{s.Loops}:{s.Knots}:{s.Locked}:{s.Ice}:{s.Link},");
                }

                sb.Append(';');
            }

            var slots = string.Concat(game.Slots.Select(s => (s == null ? "_" : $"{s.Id}:{s.Loops}:{s.Warmup}") + ","));
            while (slots.EndsWith("_,"))
            {
                slots = slots.Substring(0, slots.Length - 2);
            }

            return sb.Append('|').Append(slots).Append('|').Append(game.Taps).ToString();
        }

        private static string LevelsDirectory()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Assets")))
            {
                dir = dir.Parent;
            }

            return Path.Combine(dir!.FullName, "Assets", "Resources", "Levels");
        }
    }
}
