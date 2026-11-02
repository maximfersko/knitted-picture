#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace KnittedPicture.Core
{
    /// <summary>Короткий буфер одного намерения игрока; время передаёт экран, Unity не требуется.</summary>
    public sealed class ResponsiveInput
    {
        public const float BufferWindow = 0.15f;
        private int _column = -1;
        private int _spoolId;
        private int _version;
        private float _expires;

        public bool Pending => _column >= 0;

        /// <summary>Сброс при другом действии, паузе, смене режима или экрана.</summary>
        public void Clear() => _column = -1;

        /// <summary>Запомнить отказ только если симуляция подтверждает освобождение места за 150 мс.</summary>
        public bool Buffer(LoomGame game, int column, RejectReason reason, float now, float untilStep, float interval)
        {
            Clear();
            if (reason != RejectReason.NoFreeSlot || !game.IsBusy || column < 0 || column >= game.ColumnCount ||
                game.Column(column).Count == 0 || !(interval > 0f) || float.IsInfinity(interval) ||
                float.IsNaN(now) || float.IsInfinity(now) || float.IsNaN(untilStep) || float.IsInfinity(untilStep))
            {
                return false;
            }

            var copy = game.Clone();
            var simulated = 0;
            for (var time = Math.Max(0f, untilStep); time <= BufferWindow && simulated++ < 16; time += interval)
            {
                copy.Step();
                if (copy.CanTap(column))
                {
                    _column = column;
                    _spoolId = game.Column(column)[0].Id;
                    _version = game.Version;
                    _expires = now + BufferWindow;
                    return true;
                }

                if (copy.Status != GameStatus.Playing || !copy.IsBusy)
                {
                    break;
                }
            }

            return false;
        }

        /// <summary>Проверить срок и личность клубка; исполнить ровно один раз, без повторного выбора колонки.</summary>
        public MoveResult? Flush(LoomGame game, float now)
        {
            if (!Pending)
            {
                return null;
            }

            if (float.IsNaN(now) || float.IsInfinity(now) || now > _expires || game.Status != GameStatus.Playing || game.Version != _version ||
                game.Column(_column).Count == 0 || game.Column(_column)[0].Id != _spoolId)
            {
                Clear();
                return null;
            }

            if (!game.CanTap(_column))
            {
                return null;
            }

            var column = _column;
            Clear();
            return game.TapColumn(column);
        }

        /// <summary>
        /// Предпросмотр шага сразу после посадки на копии: учитывает очередь слотов, пару, радугу и натяжение.
        /// У загадки не выдаёт скрытые ряды. Исходная партия и история не меняются.
        /// </summary>
        public static MovePreview Preview(LoomGame game, int column)
        {
            var copy = game.Clone();
            var move = copy.TapColumn(column);
            if (move.Kind != MoveKind.Placed)
            {
                return new MovePreview(Array.Empty<Stitch>(), Array.Empty<int>());
            }

            var ids = new HashSet<int>(move.Placed.Select(p => p.Spool.Id));
            var slots = move.Placed.SelectMany(p => p.TailSlot >= 0 ? new[] { p.Slot, p.TailSlot } : new[] { p.Slot }).ToArray();
            for (var i = 0; i <= LoomGame.WarmupSteps; i++)
            {
                var step = copy.Step();
                var stitches = step.Stitches.Where(s => ids.Contains(s.SpoolId) &&
                    (!game.Mystery || s.Y == game.FrontY(s.X))).ToArray();
                if (stitches.Length > 0)
                {
                    return new MovePreview(stitches, slots);
                }
            }

            return new MovePreview(Array.Empty<Stitch>(), slots);
        }
    }

    /// <summary>Цели первого шага и будущие места выбранного клубка или пары.</summary>
    public sealed class MovePreview
    {
        public MovePreview(IReadOnlyList<Stitch> stitches, IReadOnlyList<int> slots)
        {
            Stitches = stitches;
            Slots = slots;
        }

        public IReadOnlyList<Stitch> Stitches { get; }
        public IReadOnlyList<int> Slots { get; }
    }
}
