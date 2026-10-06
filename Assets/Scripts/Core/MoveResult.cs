#nullable enable

using System;
using System.Collections.Generic;

namespace KnittedPicture.Core
{
    public enum GameStatus
    {
        Playing,
        Won,

        /// <summary>Ходов нет: слоты заняты, а нужных цветов на фронте вязки нет.</summary>
        Jammed,

        /// <summary>Моль добралась до картинки: время вышло.</summary>
        TimeUp,
    }

    public enum MoveKind
    {
        Rejected,

        /// <summary>Клубок (или пара связанных) уехал на станок.</summary>
        Placed,

        /// <summary>Тап развязал узелок, клубок остался на месте.</summary>
        KnotUntied,
    }

    public enum RejectReason
    {
        None,

        /// <summary>Игра уже выиграна, проиграна или ждёт спасения.</summary>
        GameOver,
        EmptyColumn,
        NoFreeSlot,
        Locked,

        /// <summary>Связанный клубок ещё не впереди своей колонки.</summary>
        LinkNotReady,

        /// <summary>Клубок во льду: оттает, когда станок свяжет петли его цвета.</summary>
        Frozen,
    }

    /// <summary>Одна связанная петля: где, каким цветом, из какого слота и каким клубком.</summary>
    public readonly struct Stitch
    {
        public Stitch(int x, int y, int color, int slot, int spoolId)
        {
            X = x;
            Y = y;
            Color = color;
            Slot = slot;
            SpoolId = spoolId;
        }

        /// <summary>Колонка картинки, 0 — слева.</summary>
        public int X { get; }

        /// <summary>Ряд картинки, 0 — сверху.</summary>
        public int Y { get; }

        public int Color { get; }

        public int Slot { get; }

        public int SpoolId { get; }
    }

    /// <summary>Клубок довязал все петли и освободил слот.</summary>
    public readonly struct Completion
    {
        public Completion(int spoolId, int slot, int tailSlot, bool perfect, int leftover)
        {
            SpoolId = spoolId;
            Slot = slot;
            TailSlot = tailSlot;
            Perfect = perfect;
            Leftover = leftover;
        }

        public int SpoolId { get; }

        public int Slot { get; }

        /// <summary>Второй слот толстого клубка, -1 — у обычного.</summary>
        public int TailSlot { get; }

        /// <summary>Клубок работал без простоя от начала до конца.</summary>
        public bool Perfect { get; }

        /// <summary>
        /// Лишняя пряжа: цвет клубка в картинке кончился, и он ушёл со станка недовязанным.
        /// 0 — клубок довязал все петли.
        /// </summary>
        public int Leftover { get; }
    }

    /// <summary>Клубок переехал с поля на станок.</summary>
    public readonly struct Placement
    {
        public Placement(Spool spool, int column, int depth, int slot, int tailSlot)
        {
            Spool = spool;
            Column = column;
            Depth = depth;
            Slot = slot;
            TailSlot = tailSlot;
        }

        public Spool Spool { get; }

        public int Column { get; }

        public int Depth { get; }

        public int Slot { get; }

        /// <summary>Второй слот толстого клубка, -1 — у обычного.</summary>
        public int TailSlot { get; }
    }

    /// <summary>Результат одного шага вязки: каждый клубок на станке сделал не больше одной петли.</summary>
    public sealed class StepResult
    {
        public static readonly StepResult Idle = new StepResult(Array.Empty<Stitch>(), Array.Empty<Completion>(), Array.Empty<int>(), false, GameStatus.Playing);

        internal StepResult(IReadOnlyList<Stitch> stitches, IReadOnlyList<Completion> completed, IReadOnlyList<int> thawed, bool progress, GameStatus status)
        {
            Stitches = stitches;
            Completed = completed;
            Thawed = thawed;
            Progress = progress;
            Status = status;
        }

        public IReadOnlyList<Stitch> Stitches { get; }

        public IReadOnlyList<Completion> Completed { get; }

        /// <summary>Клубки на поле, у которых растаял лёд.</summary>
        public IReadOnlyList<int> Thawed { get; }

        /// <summary>Что-то изменилось: связана петля или клубок «долетел». false — станок стоит.</summary>
        public bool Progress { get; }

        public GameStatus Status { get; }
    }

    /// <summary>Что случилось после действия игрока: всё нужное виду для анимации.</summary>
    public sealed class MoveResult
    {
        private static readonly IReadOnlyList<Placement> NoPlacements = Array.Empty<Placement>();
        private static readonly IReadOnlyList<int> NoIds = Array.Empty<int>();

        private MoveResult(
            MoveKind kind,
            RejectReason reason,
            IReadOnlyList<Placement> placed,
            Spool? knotted,
            IReadOnlyList<int> unlocked,
            IReadOnlyList<int> revealed,
            GameStatus status)
        {
            Kind = kind;
            Reason = reason;
            Placed = placed;
            Knotted = knotted;
            Unlocked = unlocked;
            Revealed = revealed;
            Status = status;
        }

        internal static MoveResult Rejected(RejectReason reason, GameStatus status) =>
            new MoveResult(MoveKind.Rejected, reason, NoPlacements, null, NoIds, NoIds, status);

        internal static MoveResult Knot(Spool spool, GameStatus status) =>
            new MoveResult(MoveKind.KnotUntied, RejectReason.None, NoPlacements, spool, NoIds, NoIds, status);

        internal static MoveResult Place(IReadOnlyList<Placement> placed, IReadOnlyList<int> unlocked, IReadOnlyList<int> revealed, GameStatus status) =>
            new MoveResult(MoveKind.Placed, RejectReason.None, placed, null, unlocked, revealed, status);

        public MoveKind Kind { get; }

        public bool Accepted => Kind != MoveKind.Rejected;

        public RejectReason Reason { get; }

        /// <summary>Клубки, уехавшие на станок: один или пара связанных.</summary>
        public IReadOnlyList<Placement> Placed { get; }

        /// <summary>Клубок, у которого развязался узелок.</summary>
        public Spool? Knotted { get; }

        /// <summary>Клубки, с которых сняли замок.</summary>
        public IReadOnlyList<int> Unlocked { get; }

        /// <summary>Скрытые клубки, показавшие цвет.</summary>
        public IReadOnlyList<int> Revealed { get; }

        public GameStatus Status { get; }
    }
}
