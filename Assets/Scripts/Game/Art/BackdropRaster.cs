#nullable enable
using System;

namespace KnittedPicture.Game
{
    /// <summary>Устойчивые параметры тихого фона; зависят от содержания рисунка, в том числе узора дня.</summary>
    public readonly struct BackdropStyle
    {
        public readonly int Family;
        public readonly float PhaseX, PhaseY, TileSize;
        public readonly Rgba Ground, Ink;

        public BackdropStyle(int id, int collection, Rgba baseGround, string[] colors, string[] picture, string[] canvas)
        {
            uint hash = 2166136261;
            void Add(string value) { foreach (char c in value) { hash = (hash ^ c) * 16777619; } hash = (hash ^ 255) * 16777619; }
            Add(id.ToString());
            foreach (var value in colors) { Add(value); }
            foreach (var value in picture) { Add(value); }
            foreach (var value in canvas) { Add(value); }
            Family = (collection % 13 + 13) % 13;
            PhaseX = (hash & 255) / 256f;
            PhaseY = ((hash >> 8) & 255) / 256f;
            TileSize = 112f + ((hash >> 16) & 15);
            var average = baseGround;
            if (colors.Length > 0)
            {
                float r = 0, g = 0, b = 0;
                foreach (var c in colors) { var p = Rgba.Hex(c); r += p.R; g += p.G; b += p.B; }
                average = new Rgba(r / colors.Length, g / colors.Length, b / colors.Length);
            }
            Ground = Rgba.Lerp(Rgba.Lerp(baseGround, average, 0.12f), Rgba.White, 0.55f);
            Ink = Rgba.Lerp(baseGround, average, 0.35f).Shade(-0.18f);
        }
    }

    /// <summary>Повторяемые мотивы коллекций: одна общая текстура на семейство.</summary>
    public static class BackdropRaster
    {
        public static Raster Tile(int family)
        {
            const int size = 128;
            var r = new Raster(size, size);
            r.Paint((x, y) =>
            {
                float u = x - 64, v = y - 64;
                float line;
                switch (family)
                {
                    case 1: case 6: // листья
                        float Leaf(float px, float py) => MathF.Abs(MathF.Max(MathF.Sqrt(px * px + (py - 15f) * (py - 15f)), MathF.Sqrt(px * px + (py + 15f) * (py + 15f))) - 26f);
                        line = MathF.Min(Leaf((u + 17f + v + 9f) * 0.7071f, (v + 9f - u - 17f) * 0.7071f), Leaf((u - 17f - v + 11f) * 0.7071f, (v - 11f + u - 17f) * 0.7071f));
                        break;
                    case 4: case 11: // волны
                        line = MathF.Min(MathF.Abs(v - 15f * MathF.Sin(u * MathF.PI / 64f)), MathF.Abs(v - 48f - 15f * MathF.Sin(u * MathF.PI / 64f)));
                        break;
                    case 5: case 12: // звёзды
                        line = MathF.Min(MathF.Max(MathF.Abs(u), MathF.Abs(v) * 0.18f), MathF.Max(MathF.Abs(v), MathF.Abs(u) * 0.18f));
                        line = MathF.Abs(line - 2f);
                        if (MathF.Abs(u) > 25 || MathF.Abs(v) > 25) { line = 100; }
                        break;
                    case 2: case 3: case 7: // мягкие лепестки
                        line = MathF.Abs(MathF.Sqrt(u*u + v*v) - (23f + 6f * MathF.Cos(5f * MathF.Atan2(v, u))));
                        break;
                    default: // петли пряжи
                        line = MathF.Min(MathF.Abs(MathF.Sqrt((u + 18)*(u + 18) + v*v*1.7f) - 20), MathF.Abs(MathF.Sqrt((u - 18)*(u - 18) + v*v*1.7f) - 20));
                        break;
                }
                return Rgba.White.WithAlpha(0.18f * Draw.Cover(line - 1.2f));
            });
            return r;
        }
    }
}
