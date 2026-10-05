#nullable enable

using System.Collections.Generic;
using KnittedPicture.Core;
using UnityEngine;

namespace KnittedPicture.Game
{
    public enum Icon
    {
        Pause,
        Scissors,
        Undo,
        PlusSlot,
        Lock,
        Key,
        Video,
        Home,
        Restart,
        SoundOn,
        SoundOff,
        Album,
        Back,
        Close,
        Moth,
        Knot,
        Link,
        Clock,
        Check,
        Settings,
        Star,
    }

    /// <summary>
    /// Вся графика игры рисуется кодом при запуске: скруглённые плашки, клубки,
    /// вязаные петли и иконки. Внешних картинок проект не требует.
    /// </summary>
    public static class Art
    {
        /// <summary>Текстуры рисуются в 3 раза крупнее, чтобы углы были чёткими на ретине.</summary>
        public const float Density = 3f;

        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        /// <summary>Скруглённый прямоугольник для Image.Type.Sliced с pixelsPerUnitMultiplier = Density.</summary>
        public static Sprite RoundRect(float radius) =>
            Cached("rr" + radius, () => SlicedBox(radius, 0f));

        /// <summary>Контур скруглённого прямоугольника (рамка пустого слота).</summary>
        public static Sprite RoundRectOutline(float radius, float stroke) =>
            Cached("ro" + radius + "_" + stroke, () => SlicedBox(radius, stroke));

        public static Sprite Circle => Cached("circle", () => Round(192, (d, r) => Mathf.Clamp01(r - d + 0.5f)));

        public static Sprite Ring => Cached("ring", () => Round(192, (d, r) => Mathf.Clamp01(5.5f - Mathf.Abs(d - (r - 7f)) + 0.5f)));

        /// <summary>Клубок: тело чуть темнее, нитки — цвет как есть, низ в тени, тонкий обод.</summary>
        public static Sprite YarnBall => Cached("yarn", BuildYarnBall);

        /// <summary>Вязаная петля: «галочка» светлее основы — картинка выглядит связанной.</summary>
        public static Sprite StitchCell => Cached("stitch", () => BuildCell(true));

        public static Sprite PlainCell => Cached("cell", () => BuildCell(false));

        public static Sprite Icon(Icon icon) => Cached("icon" + icon, () => BuildIcon(icon));

        private static Texture2D? _knitPattern;

        /// <summary>Едва заметная вязаная фактура для фона: плитка 48×48, повторяется.</summary>
        public static Texture2D KnitPattern
        {
            get
            {
                if (_knitPattern != null)
                {
                    return _knitPattern;
                }

                const int size = 48;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    wrapMode = TextureWrapMode.Repeat,
                    filterMode = FilterMode.Bilinear,
                };
                var pixels = new Color32[size * size];
                for (var py = 0; py < size; py++)
                {
                    for (var px = 0; px < size; px++)
                    {
                        var x = (px + 0.5f) % 24f;
                        var y = (size - py - 0.5f) % 24f;
                        var left = DistanceToSegment(x, y, 4f, 5f, 12f, 19f);
                        var right = DistanceToSegment(x, y, 20f, 5f, 12f, 19f);
                        var alpha = Mathf.Clamp01(2.2f - Mathf.Min(left, right) + 0.5f) * 0.05f;
                        pixels[py * size + px] = new Color32(43, 35, 80, (byte)(alpha * 255));
                    }
                }

                texture.SetPixels32(pixels);
                texture.Apply();
                _knitPattern = texture;
                return texture;
            }
        }

        /// <summary>Картинка уровня в маленькую текстуру: для альбома и экрана победы.</summary>
        public static Texture2D PictureTexture(LevelData level, int knitted = int.MaxValue)
        {
            var texture = new Texture2D(level.width, level.height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            var colors = new Color[level.colors.Length];
            for (var i = 0; i < colors.Length; i++)
            {
                colors[i] = Palette.Hex(level.colors[i]);
            }

            for (var y = 0; y < level.height; y++)
            {
                for (var x = 0; x < level.width; x++)
                {
                    var color = colors[level.picture[y][x] - '0'];
                    var stitch = (level.height - 1 - y) * level.width + x;
                    if (stitch >= knitted)
                    {
                        color = Color.Lerp(Color.white, color, 0.22f);
                    }

                    texture.SetPixel(x, level.height - 1 - y, color);
                }
            }

            texture.Apply();
            return texture;
        }

        private static Sprite Cached(string key, System.Func<Sprite> build)
        {
            if (!Cache.TryGetValue(key, out var sprite) || sprite == null)
            {
                sprite = build();
                Cache[key] = sprite;
            }

            return sprite;
        }

        private static Sprite SlicedBox(float radius, float stroke)
        {
            var r = Mathf.Max(2, Mathf.CeilToInt(radius * Density));
            var size = r * 2 + 4;
            var s = stroke * Density;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            var half = size / 2f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var qx = Mathf.Abs(x + 0.5f - half) - half + r;
                    var qy = Mathf.Abs(y + 0.5f - half) - half + r;
                    var d = Mathf.Sqrt(Mathf.Max(qx, 0) * Mathf.Max(qx, 0) + Mathf.Max(qy, 0) * Mathf.Max(qy, 0))
                            + Mathf.Min(Mathf.Max(qx, qy), 0) - r;
                    var alpha = s > 0 ? Mathf.Clamp01(s / 2f - Mathf.Abs(d + s / 2f) + 0.5f) : Mathf.Clamp01(0.5f - d);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            var border = r + 1;
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                new Vector4(border, border, border, border));
        }

        private static Sprite Round(int size, System.Func<float, float, float> alphaAt)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            var c = size / 2f;
            var radius = c - 1f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var d = Mathf.Sqrt((x + 0.5f - c) * (x + 0.5f - c) + (y + 0.5f - c) * (y + 0.5f - c));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alphaAt(d, radius) * 255));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite BuildYarnBall()
        {
            const int size = 192;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            const float c = size / 2f;
            const float radius = c - 2f;
            for (var py = 0; py < size; py++)
            {
                for (var px = 0; px < size; px++)
                {
                    var x = px + 0.5f;
                    var y = size - py - 0.5f;
                    var d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    var alpha = Mathf.Clamp01(radius - d + 0.5f);

                    // Тело клубка темнее к низу.
                    var down = Mathf.Clamp01((y - c) / radius);
                    var value = 0.86f - 0.16f * down * down;

                    // Три витка нитки — дуги, выгнутые вверх.
                    for (var i = -1; i <= 1; i++)
                    {
                        var arcRadius = 300f - i * 46f;
                        var arcDistance = Mathf.Abs(Mathf.Sqrt((x - c) * (x - c) + (y - (c + 300f)) * (y - (c + 300f))) - arcRadius);
                        var line = Mathf.Clamp01(5.5f - arcDistance + 0.5f);
                        value = Mathf.Lerp(value, 1f, line);
                    }

                    // Блик слева сверху.
                    var glare = Mathf.Clamp01(1f - Mathf.Sqrt((x - 62f) * (x - 62f) + (y - 58f) * (y - 58f)) / 26f);
                    value = Mathf.Lerp(value, 1f, glare * 0.5f);

                    // Тонкий обод, чтобы белый клубок не терялся на белом.
                    var rim = Mathf.Clamp01(d - (radius - 6f));
                    value *= 1f - 0.22f * rim;

                    var v = (byte)(Mathf.Clamp01(value) * 255);
                    pixels[py * size + px] = new Color32(v, v, v, (byte)(alpha * 255));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite BuildCell(bool knit)
        {
            const int size = 48;
            const float radius = 11f;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (var py = 0; py < size; py++)
            {
                for (var px = 0; px < size; px++)
                {
                    var x = px + 0.5f;
                    var y = size - py - 0.5f;
                    var qx = Mathf.Abs(x - 24f) - 24f + radius;
                    var qy = Mathf.Abs(y - 24f) - 24f + radius;
                    var d = Mathf.Sqrt(Mathf.Max(qx, 0) * Mathf.Max(qx, 0) + Mathf.Max(qy, 0) * Mathf.Max(qy, 0))
                            + Mathf.Min(Mathf.Max(qx, qy), 0) - radius;
                    var alpha = Mathf.Clamp01(0.5f - d);
                    var value = 1f;
                    if (knit)
                    {
                        value = 0.84f;
                        var left = DistanceToSegment(x, y, 9f, 11f, 24f, 38f);
                        var right = DistanceToSegment(x, y, 39f, 11f, 24f, 38f);
                        var line = Mathf.Clamp01(5f - Mathf.Min(left, right) + 0.5f);
                        value = Mathf.Lerp(value, 1f, line);
                        if (y > 42f)
                        {
                            value *= 0.86f;
                        }
                    }

                    var v = (byte)(value * 255);
                    pixels[py * size + px] = new Color32(v, v, v, (byte)(alpha * 255));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static float DistanceToSegment(float x, float y, float ax, float ay, float bx, float by)
        {
            var dx = bx - ax;
            var dy = by - ay;
            var t = Mathf.Clamp01(((x - ax) * dx + (y - ay) * dy) / (dx * dx + dy * dy));
            var ex = x - (ax + t * dx);
            var ey = y - (ay + t * dy);
            return Mathf.Sqrt(ex * ex + ey * ey);
        }

        private static Sprite BuildIcon(Icon icon)
        {
            var p = new Painter();
            switch (icon)
            {
                case KnittedPicture.Game.Icon.Pause:
                    p.Stroke(2.8f).Line(9, 6, 9, 18).Line(15, 6, 15, 18);
                    break;
                case KnittedPicture.Game.Icon.Scissors:
                    p.Circle(6, 6, 3).Circle(6, 18, 3).Line(20, 4, 8.1f, 15.9f).Line(14.5f, 14.5f, 20, 20).Line(8.1f, 8.1f, 12, 12);
                    break;
                case KnittedPicture.Game.Icon.Undo:
                    p.Line(9, 14, 4, 9).Line(4, 9, 9, 4).Line(4, 9, 14.5f, 9).Arc(14.5f, 14.5f, 5.5f, -90, 90).Line(14.5f, 20, 11, 20);
                    break;
                case KnittedPicture.Game.Icon.PlusSlot:
                    p.Box(12, 12, 9, 9, 4.5f, false).Line(12, 8, 12, 16).Line(8, 12, 16, 12);
                    break;
                case KnittedPicture.Game.Icon.Lock:
                    p.Box(12, 16, 7, 5.5f, 2.5f, true).Line(8, 11, 8, 8).Arc(12, 8, 4, 180, 360).Line(16, 8, 16, 11);
                    break;
                case KnittedPicture.Game.Icon.Key:
                    p.Circle(7.5f, 16.5f, 4).Line(10.5f, 13.5f, 20, 4).Line(16.5f, 7.5f, 19.5f, 10.5f).Line(14, 10, 16, 12);
                    break;
                case KnittedPicture.Game.Icon.Video:
                    p.Box(12, 12, 9, 7, 3.5f, false).Triangle(10, 9, 15.5f, 12, 10, 15);
                    break;
                case KnittedPicture.Game.Icon.Home:
                    p.Line(3, 11, 12, 3.5f).Line(12, 3.5f, 21, 11).Line(6, 9, 6, 20).Line(18, 9, 18, 20).Line(6, 20, 18, 20);
                    break;
                case KnittedPicture.Game.Icon.Restart:
                    p.Arc(12, 12.5f, 7.5f, -50, 220).Line(16.8f, 6.8f, 20, 4.5f).Line(16.8f, 6.8f, 19.5f, 9.8f);
                    break;
                case KnittedPicture.Game.Icon.SoundOn:
                    p.Box(5.5f, 12, 2.5f, 3, 1, true).Triangle(5, 12, 12.5f, 5, 12.5f, 19).Arc(13, 12, 4, -45, 45).Arc(13, 12, 7.5f, -45, 45);
                    break;
                case KnittedPicture.Game.Icon.SoundOff:
                    p.Box(5.5f, 12, 2.5f, 3, 1, true).Triangle(5, 12, 12.5f, 5, 12.5f, 19).Line(16, 9, 21, 15).Line(21, 9, 16, 15);
                    break;
                case KnittedPicture.Game.Icon.Album:
                    p.Box(7.5f, 7.5f, 3.5f, 3.5f, 1.5f, false).Box(16.5f, 7.5f, 3.5f, 3.5f, 1.5f, false)
                        .Box(7.5f, 16.5f, 3.5f, 3.5f, 1.5f, false).Box(16.5f, 16.5f, 3.5f, 3.5f, 1.5f, false);
                    break;
                case KnittedPicture.Game.Icon.Back:
                    p.Stroke(2.6f).Line(15, 5, 8, 12).Line(8, 12, 15, 19);
                    break;
                case KnittedPicture.Game.Icon.Close:
                    p.Stroke(2.6f).Line(6, 6, 18, 18).Line(18, 6, 6, 18);
                    break;
                case KnittedPicture.Game.Icon.Moth:
                    p.Disc(7, 12, 4.8f).Disc(17, 12, 4.8f).Disc(8.5f, 17, 3).Disc(15.5f, 17, 3).Disc(12, 13, 2.4f).Disc(12, 8.5f, 2)
                        .Stroke(1.4f).Line(11, 7, 9, 3).Line(13, 7, 15, 3);
                    break;
                case KnittedPicture.Game.Icon.Knot:
                    p.Triangle(12, 12, 3.5f, 6.5f, 3.5f, 17.5f).Triangle(12, 12, 20.5f, 6.5f, 20.5f, 17.5f).Disc(12, 12, 3);
                    break;
                case KnittedPicture.Game.Icon.Link:
                    p.Stroke(2.6f).Circle(8.5f, 12, 4.5f).Circle(15.5f, 12, 4.5f);
                    break;
                case KnittedPicture.Game.Icon.Clock:
                    p.Circle(12, 12, 9).Line(12, 7, 12, 12).Line(12, 12, 15.5f, 14);
                    break;
                case KnittedPicture.Game.Icon.Check:
                    p.Stroke(3f).Line(5, 12.5f, 10, 17.5f).Line(10, 17.5f, 19, 7);
                    break;
                case KnittedPicture.Game.Icon.Settings:
                    p.Line(20, 7, 11, 7).Line(13, 17, 4, 17).Circle(17, 17, 3).Circle(7, 7, 3);
                    break;
                case KnittedPicture.Game.Icon.Star:
                    for (var i = 0; i < 10; i++)
                    {
                        var a0 = (-90f + i * 36f) * Mathf.Deg2Rad;
                        var a1 = (-90f + (i + 1) * 36f) * Mathf.Deg2Rad;
                        var r0 = i % 2 == 0 ? 10.5f : 4.6f;
                        var r1 = i % 2 == 0 ? 4.6f : 10.5f;
                        p.Triangle(12, 12.6f, 12 + r0 * Mathf.Cos(a0), 12.6f + r0 * Mathf.Sin(a0), 12 + r1 * Mathf.Cos(a1), 12.6f + r1 * Mathf.Sin(a1));
                    }

                    break;
            }

            return p.ToSprite();
        }
    }
}
