#nullable enable
using System;
namespace KnittedPicture.Game
{
    /// <summary>Размер и положение непересекающихся зон событий главного экрана.</summary>
    public static class HomeLayoutRaster
    {
        public static float EventWidth(float width, int count) => Math.Min(96f, (width - 32f) / count);
        public static float EventCenter(float width, int count, int index)
        {
            var step = EventWidth(width, count);
            return width / 2f + (index - (count - 1) / 2f) * step;
        }
    }
}
