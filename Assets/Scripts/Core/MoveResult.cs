#nullable enable

using System;
using System.Collections.Generic;

namespace KnittedPicture.Core
{
    public enum GameStatus
    {
        Playing,
        Won,

        /// <summary>Ходов нет: слоты заняты, а нужного цвета на станке нет.</summary>
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
    }

    /// <summary>Одна связанная петля: где, каким цветом и из какого слота.</summary>
    public readonly struct Stitch
    {
        public Stitch(int x, int y, int color, int slot)
        {
            X = x;
            Y = y;
            Color = color;
            Slot = slot;
        }

        /// <summary>Колонка картинки, 0 — слева.</summary>
        public int X { get; }

        /// <summary>Ряд картинки, 0 — сверху.</summary>
        public int Y { get; }

        public int Color { get; }

        public int Slot { get; }
    }

    /// <summary>Клубок переехал с поля на станок.</summary>
    public readonly struct Placement
    {
        public Placement(Spool spool, int column, int depth, int slot)
        {
            Spool = spool;
            Column = column;
            Depth = depth;
            Slot = slot;
        }

        public Spool Spool { get; }

        public int Column { get; }

        public int Depth { get; }

        public int Slot { get; }
    }

    /// <summary>Что случилось после действия игрока: всё нужное виду для анимации.</summary>
    public sealed class MoveResult
    {
        private static readonly IReadOnlyList<Placement> NoPlacements = Array.Empty<Placement>();
        private static readonly IReadOnlyList<Stitch> NoStitches = Array.Empty<Stitch>();
        private static readonly IReadOnlyList<int> NoIds = Array.Empty<int>();

        private MoveResult(
            MoveKind kind,
            RejectReason reason,
            IReadOnlyList<Placement> placed,
            IReadOnlyList<Stitch> knitted,
            Spool? knotted,
            IReadOnlyList<int> unlocked,
            IReadOnlyList<int> revealed,
            GameStatus status)
        {
            Kind = kind;
            Reason = reason;
            Placed = placed;
            Knitted = knitted;
            Knotted = knotted;
            Unlocked = unlocked;
            Revealed = revealed;
            Status = status;
        }

        internal static MoveResult Rejected(RejectReason reason, GameStatus status) =>
            new MoveResult(MoveKind.Rejected, reason, NoPlacements, NoStitches, null, NoIds, NoIds, status);

        internal static MoveResult Knot(Spool spool, GameStatus status) =>
            new MoveResult(MoveKind.KnotUntied, RejectReason.None, NoPlacements, NoStitches, spool, NoIds, NoIds, status);

        internal static MoveResult Place(
            IReadOnlyList<Placement> placed,
            IReadOnlyList<Stitch> knitted,
            IReadOnlyList<int> unlocked,
            IReadOnlyList<int> revealed,
            GameStatus status) =>
            new MoveResult(MoveKind.Placed, RejectReason.None, placed, knitted, null, unlocked, revealed, status);

        public MoveKind Kind { get; }

        public bool Accepted => Kind != MoveKind.Rejected;

        public RejectReason Reason { get; }

        /// <summary>Клубки, уехавшие на станок: один или пара связанных.</summary>
        public IReadOnlyList<Placement> Placed { get; }

        /// <summary>Петли, связанные сразу после хода, по порядку.</summary>
        public IReadOnlyList<Stitch> Knitted { get; }

        /// <summary>Клубок, у которого развязался узелок.</summary>
        public Spool? Knotted { get; }

        /// <summary>Клубки, с которых сняли замок.</summary>
        public IReadOnlyList<int> Unlocked { get; }

        /// <summary>Скрытые клубки, показавшие цвет.</summary>
        public IReadOnlyList<int> Revealed { get; }

        public GameStatus Status { get; }
    }
}
