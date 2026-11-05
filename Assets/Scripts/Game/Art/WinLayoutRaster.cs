#nullable enable
using System;
namespace KnittedPicture.Game
{
    /// <summary>Общий вертикальный бюджет пялец и звёзд для игры и нативного превью.</summary>
    public static class WinLayoutRaster
    {
        public static float HintHeight(float height, float cardHeight, bool compact, float bottomSpace = 72f)
        {
            var top = compact ? 52f : 64f;
            var available = height - 144f - 12f - top - 20f - 48f - 40f - 30f - cardHeight - bottomSpace;
            return available < 46f ? 14f : 30f;
        }
        public static float Hoop(float width, float height, float cardHeight, bool compact, float bottomSpace = 72f)
        {
            var hoopY = compact ? 52f : 64f;
            var card = cardHeight + bottomSpace;
            var nextTop = height - 24f - 44f - 6f - 70f;
            var room = (nextTop - 12f - hoopY - 20f - 48f - 40f - HintHeight(height, cardHeight, compact, bottomSpace) - card) / 1.15f;
            return Math.Max(1f, Math.Min(room, Math.Min((width - 40f) / 1.18f, compact ? 196f : 240f)));
        }
        public static float StarsTop(float hoopY, float hoop) => hoopY + hoop * 1.15f + 20f;
    }
}
