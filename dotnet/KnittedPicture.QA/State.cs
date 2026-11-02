using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using KnittedPicture.Core;

namespace KnittedPicture.QA
{
    /// <summary>Снимки партии: строкой (сравнивать) и кадром (рисовать «скриншот»).</summary>
    public static class State
    {
        /// <summary>
        /// Поле и станок без тапов и статуса: так сравнивают отмену хода (купленные слоты отмена не забирает,
        /// поэтому пустые слоты в конце не считаем).
        /// </summary>
        public static string Board(LoomGame g)
        {
            var sb = new StringBuilder();
            for (var x = 0; x < g.Width; x++)
            {
                sb.Append(g.ColumnHeight(x)).Append('.');
            }

            sb.Append('|');
            for (var c = 0; c < g.ColumnCount; c++)
            {
                foreach (var s in g.Column(c))
                {
                    sb.Append(Spool(s)).Append(',');
                }

                sb.Append(';');
            }

            sb.Append('|');
            var slots = string.Concat(g.Slots.Select(s => (s == null ? "_" : $"{s.Id}:{s.Loops}:{s.Warmup}:{s.UnravelSteps}") + ","));
            while (slots.EndsWith("_,"))
            {
                slots = slots.Substring(0, slots.Length - 2);
            }

            return sb.Append(slots).ToString();
        }

        /// <summary>Всё наблюдаемое: доска, все слоты, тапы, версия, статус, время. Отклонённый ход не должен менять ни символа.</summary>
        public static string Full(LoomGame g) =>
            Board(g) + $"|n{g.Slots.Count}|t{g.Taps}|v{g.Version}|{g.Status}|{g.TimeLeft.ToString("R", CultureInfo.InvariantCulture)}|{g.TimerStarted}";

        private static string Spool(Spool s) =>
            $"{s.Id}:{s.Loops}:{s.Knots}:{(s.Locked ? "L" : "")}{s.Ice}:{s.Link}:{(s.IsColorVisible ? "v" : "h")}";

        /// <summary>Сколько пряжи осталось в клубках (каждый клубок один раз, где бы он ни был).</summary>
        public static int Yarn(LoomGame g) => Spools(g).Sum(s => s.Loops);

        public static IEnumerable<Spool> Spools(LoomGame g)
        {
            var seen = new HashSet<int>();
            for (var c = 0; c < g.ColumnCount; c++)
            {
                foreach (var s in g.Column(c))
                {
                    if (seen.Add(s.Id))
                    {
                        yield return s;
                    }
                }
            }

            foreach (var s in g.Slots)
            {
                if (s != null && seen.Add(s.Id))
                {
                    yield return s;
                }
            }
        }

        public static Frame Capture(LoomGame g, LevelData level, string label) => new Frame(g, level, label);
    }

    /// <summary>Клубок для кадра.</summary>
    public sealed class SpoolView
    {
        public int Id;
        public int Color;
        public int Loops;
        public int Knots;
        public bool Locked;
        public int Ice;
        public int Link;
        public bool Visible;
        public bool Rainbow;
        public bool Thick;
        public bool Key;
        public int Warmup;
        public bool Work;

        public static SpoolView Of(Spool s, LoomGame g) => new SpoolView
        {
            Id = s.Id,
            Color = s.Color,
            Loops = s.Loops,
            Knots = s.Knots,
            Locked = s.Locked,
            Ice = s.Ice,
            Link = s.Link,
            Visible = s.IsColorVisible,
            Rainbow = s.Rainbow,
            Thick = s.Thick,
            Key = s.IsKey,
            Warmup = s.Warmup,
            Work = g.HasWork(s),
        };
    }

    /// <summary>Кадр партии: всё, что нужно, чтобы нарисовать картинку, станок и поле без самой партии.</summary>
    public sealed class Frame
    {
        public Frame(LoomGame g, LevelData level, string label)
        {
            Label = label;
            Width = g.Width;
            Height = g.Height;
            Colors = level.colors;
            Status = g.Status;
            Taps = g.Taps;
            TimeLimit = g.TimeLimit;
            TimeLeft = g.TimeLeft;
            Knitted = g.KnittedCount;
            Total = g.TotalStitches;
            Cells = new int[Width * Height];
            Done = new bool[Width * Height];
            Fabric = new bool[Width * Height];
            Gold = new bool[Width * Height];
            Front = new int[Width];
            Active = new int[Width];
            for (var x = 0; x < Width; x++)
            {
                Front[x] = g.FrontY(x);
                Active[x] = g.ActiveFrontY(x);
                for (var y = 0; y < Height; y++)
                {
                    var i = y * Width + x;
                    Cells[i] = g.ColorAt(x, y);
                    Done[i] = g.IsKnitted(x, y);
                    Fabric[i] = g.IsFabric(x, y);
                    Gold[i] = g.IsGold(x, y);
                }
            }

            Columns = Enumerable.Range(0, g.ColumnCount).Select(c => g.Column(c).Select(s => SpoolView.Of(s, g)).ToList()).ToList();
            Slots = new List<SpoolView?>();
            var tails = new HashSet<int>();
            for (var i = 0; i < g.Slots.Count; i++)
            {
                var s = g.Slots[i];
                Slots.Add(s == null ? null : SpoolView.Of(s, g));
                if (s != null && g.IsTail(i))
                {
                    tails.Add(i);
                }
            }

            Tails = tails;
        }

        public string Label { get; }

        public int Width { get; }

        public int Height { get; }

        public string[] Colors { get; }

        public GameStatus Status { get; }

        public int Taps { get; }

        public float TimeLimit { get; }

        public float TimeLeft { get; }

        public int Knitted { get; }

        public int Total { get; }

        public int[] Cells { get; }

        public bool[] Done { get; }

        public bool[] Fabric { get; }

        public bool[] Gold { get; }

        public int[] Front { get; }

        public int[] Active { get; }

        public List<List<SpoolView>> Columns { get; }

        public List<SpoolView?> Slots { get; }

        /// <summary>Слоты, занятые второй половиной толстого клубка.</summary>
        public HashSet<int> Tails { get; }

        /// <summary>Что подсветить красным: колонка поля, слот станка, клетка картинки.</summary>
        public int MarkColumn = -1;

        public int MarkSlot = -1;

        public int MarkCell = -1;
    }
}
