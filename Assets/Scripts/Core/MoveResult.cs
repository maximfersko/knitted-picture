#nullable enable

using System;
using System.Collections.Generic;

namespace KnittedPicture.Core
{
    public enum GameStatus
    {
        Playing,
        Won,

        /// <summary>Все слоты заняты, а нужного цвета на станке нет.</summary>
        Jammed,
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

    /// <summary>Что случилось после действия игрока: всё нужное виду для анимации.</summary>
    public sealed class MoveResult
    {
        private static readonly IReadOnlyList<Stitch> NoStitches = Array.Empty<Stitch>();

        internal MoveResult(bool accepted, Spool? spool, int slot, IReadOnlyList<Stitch> knitted, GameStatus status)
        {
            Accepted = accepted;
            Spool = spool;
            Slot = slot;
            Knitted = knitted;
            Status = status;
        }

        internal static MoveResult Rejected(GameStatus status) => new MoveResult(false, null, -1, NoStitches, status);

        /// <summary>false — ход невозможен (пустая колонка, нет свободного слота, игра окончена).</summary>
        public bool Accepted { get; }

        /// <summary>Клубок, который переехал на станок.</summary>
        public Spool? Spool { get; }

        /// <summary>В какой слот он встал.</summary>
        public int Slot { get; }

        /// <summary>Петли, связанные сразу после хода, по порядку.</summary>
        public IReadOnlyList<Stitch> Knitted { get; }

        public GameStatus Status { get; }
    }
}
