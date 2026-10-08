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
        Hand,
        Snowflake,
        Bolt,
        Gift,
        Chest,
        Trophy,
        Info,
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

        /// <summary>Клубок: шар, намотанный нитью, с перевязью наискосок и кончиком нити. Красится цветом пряжи.</summary>
        public static Sprite YarnBall => Cached("yarn", () => BuildYarnBall(false));

        /// <summary>Большой подробный клубок для главного экрана: слои намотки, кручёная нить, ворс. Красится цветом пряжи.</summary>
        public static Sprite YarnBallLarge => Cached("yarnLarge", () => BuildDetailedYarnBall(LargeBall.Plain));

        /// <summary>Каждая вторая нитка большого клубка (остальное прозрачно): поверх основного — меланж вторым цветом.</summary>
        public static Sprite YarnBallLargeMelange => Cached("yarnLargeMelange", () => BuildDetailedYarnBall(LargeBall.EveryOther));

        /// <summary>Большой радужный клубок: каждая нитка своего цвета, красится белым.</summary>
        public static Sprite YarnBallLargeRainbow => Cached("yarnLargeRainbow", () => BuildDetailedYarnBall(LargeBall.Rainbow));

        /// <summary>Вязальная спица: остриём вверх, бусина-набалдашник внизу. Цвета запечены, красить белым.</summary>
        public static Sprite KnittingNeedle => Cached("needle", BuildNeedle);

        /// <summary>
        /// Узор ткани поверх клубка (светлая сетка канвы в круге клубка): так выглядит чужая пряжа
        /// цвета ткани — тот же узор, что у готовой ткани на картинке.
        /// </summary>
        public static Sprite FabricOverlay => Cached("fabricOverlay", BuildFabricOverlay);

        /// <summary>Радужный клубок: витки разных цветов, красится белым.</summary>
        public static Sprite RainbowBall => Cached("rainbow", () => BuildYarnBall(true));

        /// <summary>Вязаная петля: «галочка» светлее основы — картинка выглядит связанной.</summary>
        public static Sprite StitchCell => Cached("stitch", () => BuildCell(true));

        public static Sprite PlainCell => Cached("cell", () => BuildCell(false));

        /// <summary>Ткань-основа: та же вязка, но тише — рисунок и фронт заметнее фона.</summary>
        /// <summary>
        /// Клетка ткани-основы: гладкая канва из переплетённых нитей, без петель-«галочек».
        /// Так фон не путается со связанным рисунком: видно, что он готов с самого начала и вязать его не нужно.
        /// </summary>
        public static Sprite FabricCell => Cached("canvas", BuildCanvasCell);

        public static Sprite Icon(Icon icon) => Cached("icon" + icon, () => BuildIcon(icon));

        /// <summary>Мягкое круглое свечение: к краю плавно сходит на нет.</summary>
        public static Sprite Glow => Cached("glow", () => Round(256, (d, r) =>
        {
            var t = Mathf.Clamp01(1f - d / r);
            return t * t;
        }));

        /// <summary>Двенадцать тихих лучей, гаснущих к краю: подложка под открытым сундуком.</summary>
        public static Sprite Rays => Cached("rays", () => BuildRays(256, 12));

        /// <summary>Корпус вязаной шкатулки: петли «ёлочкой», лента посередине, кант сверху.</summary>
        public static Sprite ChestBody(bool big) => Cached("chestBody" + big, () => BuildChest(false, big));

        /// <summary>Крышка шкатулки: купол той же вязки, кант снизу.</summary>
        public static Sprite ChestLid(bool big) => Cached("chestLid" + big, () => BuildChest(true, big));

        /// <summary>Размеры частей сундука в точках макета при ширине корпуса 220.</summary>
        public static readonly Vector2 ChestBodySize = new Vector2(220f, 118f);

        public static readonly Vector2 ChestLidSize = new Vector2(232f, 74f);

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

        /// <summary>
        /// Картинка уровня в маленькую текстуру: для альбома и экрана победы.
        /// Не связанная — ткань как есть, рисунок бледным эскизом; у загадки рисунка не видно вовсе.
        /// </summary>
        public static Texture2D PictureTexture(LevelData level, bool done = true)
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
                    var fabric = level.canvas != null && level.canvas.Length == level.height && level.canvas[y][x] == '1';
                    if (!done && !fabric)
                    {
                        color = level.mystery ? Palette.Track : Color.Lerp(Color.white, color, 0.22f);
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

        /// <summary>
        /// Свечение по краю экрана (для Image.Type.Sliced с pixelsPerUnitMultiplier = Density):
        /// у самого края ярко, к середине на ширину width плавно гаснет, середина прозрачная.
        /// </summary>
        public static Sprite EdgeGlow(float width) => Cached("edge" + width, () =>
        {
            var border = Mathf.Max(2, Mathf.CeilToInt(width * Density));
            var size = border * 2 + 4;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var edge = Mathf.Min(Mathf.Min(x, size - 1 - x), Mathf.Min(y, size - 1 - y));
                    var t = Mathf.Clamp01(1f - edge / (float)border);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(t * t * 255));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                new Vector4(border, border, border, border));
        });

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

        /// <summary>
        /// Клубок рисуется как шар, намотанный нитью: снизу слой витков в одну сторону, поверх — широкая
        /// перевязь витков наискосок с тенью по краю. Каждая нитка круглая в сечении (светлая середина,
        /// тёмная бороздка между нитками), свет сверху слева, по краю мягкий обод. Слева внизу — кончик нити.
        /// Текстура серая и красится цветом пряжи; у радужного каждая нитка своего цвета.
        /// </summary>
        private static Sprite BuildYarnBall(bool rainbow)
        {
            const int size = 192;
            const float c = size / 2f;
            const float radius = c - 8f;
            const float strand = 0.24f;
            const float band = 0.55f;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            var under = new Vector3(0.3f, 0.9f, 0.3f).normalized;
            var over = new Vector3(-0.9f, 0.3f, 0.3f).normalized;
            var light = new Vector3(-0.5f, 0.6f, 0.65f).normalized;
            var half = (light + Vector3.forward).normalized;
            var tail = YarnTail(c, radius);
            const float tailWidth = 5f;

            for (var py = 0; py < size; py++)
            {
                for (var px = 0; px < size; px++)
                {
                    // Координаты от центра, y вверх (нулевая строка текстуры — нижняя).
                    var x = px + 0.5f;
                    var y = py + 0.5f;
                    var nx = (x - c) / radius;
                    var ny = (y - c) / radius;
                    var r2 = nx * nx + ny * ny;
                    var d = Mathf.Sqrt(r2) * radius;
                    var alpha = Mathf.Clamp01(radius - d + 0.5f);
                    var value = 0f;
                    var strandIndex = 0f;
                    if (alpha > 0f)
                    {
                        var n = new Vector3(nx, ny, Mathf.Sqrt(Mathf.Max(0f, 1f - r2)));
                        var tOver = Vector3.Dot(n, over);
                        if (Mathf.Abs(tOver) < band)
                        {
                            // Перевязь сверху; у самого края — тёмная кромка.
                            value = Strand(tOver, strand, out strandIndex) * (1f - 0.25f * SmoothStep(band - 0.06f, band, Mathf.Abs(tOver)));
                            strandIndex += 100f;
                        }
                        else
                        {
                            // Нижний слой в тени перевязи.
                            var shadow = 1f - 0.22f * (1f - SmoothStep(band, band + 0.12f, Mathf.Abs(tOver)));
                            value = Strand(Vector3.Dot(n, under), strand, out strandIndex) * shadow;
                        }

                        var lambert = Mathf.Clamp01(Vector3.Dot(n, light));
                        var shine = Mathf.Pow(Mathf.Clamp01(Vector3.Dot(n, half)), 18f) * 0.25f;
                        value = value * (0.66f + 0.34f * lambert) * 1.12f + shine;
                        value *= 1f - 0.18f * SmoothStep(radius - 7f, radius - 1f, d);
                    }

                    var color = rainbow
                        ? Color.HSVToRGB(Mathf.Repeat(strandIndex * 0.11f, 1f), 0.5f, 1f) * Mathf.Clamp01(value)
                        : Color.white * Mathf.Clamp01(value);

                    // Кончик нити поверх клубка: круглый в сечении, как и нитки.
                    var tailDistance = DistanceToPolyline(tail, x, y, tailWidth + 1f);
                    var tailAlpha = Mathf.Clamp01(tailWidth - tailDistance + 0.5f);
                    if (tailAlpha > 0f)
                    {
                        var profile = Mathf.Sqrt(Mathf.Clamp01(1f - (tailDistance / tailWidth) * (tailDistance / tailWidth)));
                        var tailValue = 0.72f + 0.24f * profile;
                        var tailColor = rainbow ? Color.HSVToRGB(0f, 0.5f, 1f) * tailValue : Color.white * tailValue;
                        var total = tailAlpha + alpha * (1f - tailAlpha);
                        color = (tailColor * tailAlpha + color * alpha * (1f - tailAlpha)) / Mathf.Max(total, 0.0001f);
                        alpha = total;
                    }

                    color.a = alpha;
                    pixels[py * size + px] = color;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>
        /// Большой клубок для главного экрана: четыре слоя намотки крест-накрест (каждый верхний бросает
        /// тень на нижние), нить кручёная — по ней бегут косые витки прядей, у каждой нитки свой оттенок,
        /// по краю ворс и выбившиеся волоски, кончик нити тоже кручёный. Серый, красится цветом пряжи.
        /// </summary>
        private enum LargeBall
        {
            Plain,
            EveryOther,
            Rainbow,
        }

        private static Sprite BuildDetailedYarnBall(LargeBall kind)
        {
            const int size = 320;
            const float c = size / 2f;
            const float radius = c - 22f;

            // Шар чуть выше середины: снизу место для нити, которая ложится на пол.
            const float cy = c + 12f;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            // Слои снизу вверх: ось витков, полуширина пояса (по синусу широты), толщина нитки.
            var axes = new[]
            {
                new Vector3(0.25f, 0.95f, 0.2f).normalized,
                new Vector3(0.95f, -0.15f, 0.35f).normalized,
                new Vector3(-0.55f, -0.8f, 0.3f).normalized,
                new Vector3(-0.9f, 0.35f, 0.3f).normalized,
            };
            var bands = new[] { 2f, 0.5f, 0.42f, 0.4f };
            var widths = new[] { 0.13f, 0.12f, 0.12f, 0.115f };
            var light = new Vector3(-0.45f, 0.6f, 0.66f).normalized;
            var half = (light + Vector3.forward).normalized;
            var tail = DetailTail(c, cy, radius);
            const float tailWidth = 7f;
            var hairs = StrayHairs(c, cy, radius);

            for (var py = 0; py < size; py++)
            {
                for (var px = 0; px < size; px++)
                {
                    var x = px + 0.5f;
                    var y = py + 0.5f;
                    var nx = (x - c) / radius;
                    var ny = (y - cy) / radius;
                    var r2 = nx * nx + ny * ny;
                    var d = Mathf.Sqrt(r2) * radius;

                    // Ворс: край не ровный, а пушистый — высота ворса меняется по углу.
                    var angle = Mathf.Atan2(ny, nx);
                    var fuzz = 0.9f * (Hash(Mathf.Floor((angle + Mathf.PI) * 70f)) - 0.5f) + 0.5f * Mathf.Sin(angle * 23f);
                    var alpha = Mathf.Clamp01((radius + fuzz - d) / 2.2f + 0.5f);
                    var value = 0f;
                    var strandIndex = 0f;
                    if (alpha > 0f)
                    {
                        var rr = Mathf.Min(r2, 1f);
                        var n = new Vector3(Mathf.Min(1f, nx), Mathf.Min(1f, ny), Mathf.Sqrt(Mathf.Max(0f, 1f - rr)));

                        // Верхний слой, под которым лежит точка, и тень от слоёв над ним.
                        var layer = 0;
                        var shade = 1f;
                        for (var i = axes.Length - 1; i >= 0; i--)
                        {
                            var t = Mathf.Abs(Vector3.Dot(n, axes[i]));
                            if (t < bands[i])
                            {
                                layer = i;
                                break;
                            }

                            // Тень от пояса сверху на лежащее ниже.
                            shade *= 1f - 0.3f * (1f - SmoothStep(bands[i], bands[i] + 0.1f, t));
                        }

                        var axis = axes[layer];
                        var tl = Vector3.Dot(n, axis);
                        var k = Mathf.Asin(Mathf.Clamp(tl, -1f, 1f)) / widths[layer];
                        var index = Mathf.Floor(k);
                        var across = k - index;
                        var profile = Mathf.Sqrt(Mathf.Max(0f, Mathf.Sin(Mathf.PI * across)));

                        // Вдоль нитки — долгота вокруг оси; по ней бегут косые витки прядей.
                        var u = Vector3.Cross(axis, Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.forward).normalized;
                        var v = Vector3.Cross(axis, u);
                        var along = Mathf.Atan2(Vector3.Dot(n, v), Vector3.Dot(n, u));
                        var ply = Mathf.Sin((along * 34f + across * 2.4f + index * 1.7f) * Mathf.PI);
                        var twist = 1f - 0.11f * SmoothStep(0.2f, 1f, ply) * profile;

                        var tone = 0.94f + 0.08f * Hash(index * 7.13f + layer * 31.7f);
                        strandIndex = index + layer * 100f;
                        value = (0.62f + 0.38f * profile) * twist * tone * shade;

                        // Кромка пояса: нитки у края чуть темнее, пояс выглядит толще.
                        if (layer > 0)
                        {
                            value *= 1f - 0.18f * SmoothStep(bands[layer] - 0.05f, bands[layer], Mathf.Abs(tl));
                        }

                        // Мелкий ворс по всей поверхности.
                        value *= 0.97f + 0.06f * Hash(px * 0.37f + py * 1.91f);

                        var lambert = Mathf.Clamp01(Vector3.Dot(n, light));
                        var shine = Mathf.Pow(Mathf.Clamp01(Vector3.Dot(n, half)), 14f) * 0.18f * profile;
                        value = value * (0.64f + 0.36f * lambert) * 1.2f + shine;
                        value *= 1f - 0.22f * SmoothStep(radius - 12f, radius, d);
                    }

                    var color = kind == LargeBall.Rainbow
                        ? Color.HSVToRGB(Mathf.Repeat(strandIndex * 0.11f, 1f), 0.5f, 1f) * Mathf.Clamp01(value)
                        : Color.white * Mathf.Clamp01(value);
                    if (kind == LargeBall.EveryOther)
                    {
                        // Меланж: вторым цветом только каждая вторая нитка, ни волосков, ни кончика.
                        alpha *= Mathf.Repeat(strandIndex, 2f) >= 1f ? 1f : 0f;
                        color.a = alpha;
                        pixels[py * size + px] = color;
                        continue;
                    }

                    // Выбившиеся волоски у края.
                    var hair = DistanceToPolyline(hairs, x, y, 2f);
                    var hairAlpha = Mathf.Clamp01(0.8f - hair) * 0.45f;
                    if (hairAlpha > 0f && alpha < 1f)
                    {
                        var total = hairAlpha + alpha * (1f - hairAlpha);
                        color = (Color.white * 0.9f * hairAlpha + color * alpha * (1f - hairAlpha)) / Mathf.Max(total, 0.0001f);
                        alpha = total;
                    }

                    // Кончик нити: кручёный, как и нитки клубка.
                    var tailDistance = DistanceToPolyline(tail, x, y, tailWidth + 1f);
                    var tailAlpha = Mathf.Clamp01(tailWidth - tailDistance + 0.5f);
                    if (tailAlpha > 0f)
                    {
                        var across = tailDistance / tailWidth;
                        var round = Mathf.Sqrt(Mathf.Clamp01(1f - across * across));
                        var twistTail = 1f - 0.08f * SmoothStep(0.3f, 1f, Mathf.Sin((x * 0.55f + y * 0.55f) * 0.9f));
                        var tailValue = (0.66f + 0.3f * round) * twistTail;
                        var tailColor = kind == LargeBall.Rainbow ? Color.HSVToRGB(0f, 0.5f, 1f) : Color.white;
                        var total = tailAlpha + alpha * (1f - tailAlpha);
                        color = (tailColor * tailValue * tailAlpha + color * alpha * (1f - tailAlpha)) / Mathf.Max(total, 0.0001f);
                        alpha = total;
                    }

                    color.a = alpha;
                    pixels[py * size + px] = color;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>
        /// Спица: тонкий круглый бамбуковый стержень (светлая середина, тени по бокам, блик), сверху заострён,
        /// снизу розовая бусина-набалдашник с бликом.
        /// </summary>
        private static Sprite BuildNeedle()
        {
            const int width = 40;
            const int height = 400;
            const float rod = 7f;
            const float knob = 16f;
            const float cx = width / 2f;
            const float knobY = knob + 2f;
            const float tipLength = 46f;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[width * height];
            var rodColor = new Color(0.9f, 0.7f, 0.47f);
            var knobColor = new Color(1f, 0.44f, 0.65f);
            for (var py = 0; py < height; py++)
            {
                for (var px = 0; px < width; px++)
                {
                    var x = px + 0.5f;
                    var y = py + 0.5f;

                    // Стержень сужается к острию наверху.
                    var halfWidth = y > height - tipLength ? rod * Mathf.Sqrt(Mathf.Clamp01((height - 2f - y) / tipLength)) : rod;
                    var u = (x - cx) / Mathf.Max(halfWidth, 0.01f);
                    var rodAlpha = y > knobY && y < height - 1f ? Mathf.Clamp01(halfWidth - Mathf.Abs(x - cx) + 0.5f) : 0f;
                    var shade = 0.72f + 0.28f * Mathf.Sqrt(Mathf.Clamp01(1f - u * u)) + 0.2f * Mathf.Exp(-(u + 0.35f) * (u + 0.35f) * 18f);
                    var color = rodColor * shade;
                    var alpha = rodAlpha;

                    // Бусина: шар с бликом сверху слева.
                    var kd = Mathf.Sqrt((x - cx) * (x - cx) + (y - knobY) * (y - knobY));
                    var knobAlpha = Mathf.Clamp01(knob - kd + 0.5f);
                    if (knobAlpha > 0f)
                    {
                        var nx = (x - cx) / knob;
                        var ny = (y - knobY) / knob;
                        var nz = Mathf.Sqrt(Mathf.Clamp01(1f - nx * nx - ny * ny));
                        var light = Mathf.Clamp01(-0.45f * nx + 0.55f * ny + 0.7f * nz);
                        var spot = Mathf.Pow(light, 24f) * 0.6f;
                        var knobShade = knobColor * (0.62f + 0.42f * light) + Color.white * spot;
                        color = knobShade * knobAlpha + color * alpha * (1f - knobAlpha);
                        alpha = knobAlpha + alpha * (1f - knobAlpha);
                        color /= Mathf.Max(alpha, 0.0001f);
                    }

                    color.a = alpha;
                    pixels[py * width + px] = color;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>Псевдослучайное число 0..1 по координате — одинаковое при каждой сборке.</summary>
        private static float Hash(float n)
        {
            var s = Mathf.Sin(n * 12.9898f) * 43758.547f;
            return s - Mathf.Floor(s);
        }

        /// <summary>Кончик нити большого клубка: спускается слева внизу и ложится на пол.</summary>
        private static List<Vector2> DetailTail(float cx, float cy, float radius)
        {
            var anchors = new[] { (232f, -14f), (236f, 0f), (242f, 10f), (252f, 17f), (264f, 20f), (276f, 21f), (287f, 20f) };
            var points = new List<Vector2>();
            foreach (var (angle, offset) in anchors)
            {
                var a = angle * Mathf.Deg2Rad;
                points.Add(new Vector2(cx + Mathf.Cos(a) * (radius + offset), cy + Mathf.Sin(a) * (radius + offset)));
            }

            return CatmullRom(points);
        }

        /// <summary>Несколько выбившихся волосков: короткие дуги наружу из края клубка.</summary>
        private static List<Vector2> StrayHairs(float cx, float cy, float radius)
        {
            var line = new List<Vector2>();
            for (var i = 0; i < 14; i++)
            {
                var a = (i * 137.5f + 20f) * Mathf.Deg2Rad;
                if (Mathf.Sin(a) < -0.6f && Mathf.Cos(a) < 0.3f)
                {
                    continue; // там кончик нити
                }

                var length = 5f + 5f * Hash(i * 3.3f);
                var bend = (Hash(i * 5.1f) - 0.5f) * 0.6f;
                for (var s = 0; s <= 4; s++)
                {
                    var t = s / 4f;
                    var r = radius - 2f + length * t;
                    var aa = a + bend * t;
                    line.Add(new Vector2(cx + Mathf.Cos(aa) * r, cy + Mathf.Sin(aa) * r));
                }

                // Разрыв: следующий волосок — отдельная ломаная.
                line.Add(new Vector2(float.NaN, float.NaN));
            }

            return line;
        }

        /// <summary>Нитка в сечении: 1 — середина нитки, ниже — бороздка между соседними. Номер нитки — для радуги.</summary>
        private static float Strand(float t, float width, out float index)
        {
            var k = Mathf.Asin(Mathf.Clamp(t, -1f, 1f)) / width;
            index = Mathf.Floor(k);
            var profile = Mathf.Sin(Mathf.PI * (k - index));
            return 0.74f + 0.26f * Mathf.Sqrt(Mathf.Max(0f, profile));
        }

        private static float SmoothStep(float from, float to, float t)
        {
            t = Mathf.Clamp01((t - from) / (to - from));
            return t * t * (3f - 2f * t);
        }

        /// <summary>Кончик нити: выходит из клубка слева внизу и завивается в угол (кривая через опорные точки).</summary>
        private static List<Vector2> YarnTail(float c, float radius)
        {
            var anchors = new[] { (210f, -4f), (225f, 4f), (234f, 16f), (230f, 30f), (218f, 38f) };
            var points = new List<Vector2>();
            foreach (var (angle, offset) in anchors)
            {
                var a = angle * Mathf.Deg2Rad;
                points.Add(new Vector2(c + Mathf.Cos(a) * (radius + offset), c + Mathf.Sin(a) * (radius + offset)));
            }

            return CatmullRom(points);
        }

        /// <summary>Гладкая кривая через опорные точки (Катмулл — Ром), 16 шагов на отрезок.</summary>
        private static List<Vector2> CatmullRom(List<Vector2> points)
        {
            var curve = new List<Vector2>();
            for (var i = 0; i + 1 < points.Count; i++)
            {
                var p0 = points[Mathf.Max(0, i - 1)];
                var p1 = points[i];
                var p2 = points[i + 1];
                var p3 = points[Mathf.Min(points.Count - 1, i + 2)];
                for (var s = 0; s < 16; s++)
                {
                    var t = s / 16f;
                    curve.Add(0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * (t * t) + (3f * p1 - p0 - 3f * p2 + p3) * (t * t * t)));
                }
            }

            curve.Add(points[points.Count - 1]);
            return curve;
        }

        /// <summary>Расстояние от точки до ломаной; дальше limit — просто большое число (быстрый отказ по рамке).</summary>
        private static float DistanceToPolyline(List<Vector2> line, float x, float y, float limit)
        {
            var best = float.MaxValue;
            var p = new Vector2(x, y);
            for (var i = 0; i + 1 < line.Count; i++)
            {
                var a = line[i];
                var b = line[i + 1];

                // NaN — разрыв ломаной (так в одну линию складываются отдельные волоски).
                if (float.IsNaN(a.x) || float.IsNaN(b.x) || x < Mathf.Min(a.x, b.x) - limit || x > Mathf.Max(a.x, b.x) + limit || y < Mathf.Min(a.y, b.y) - limit || y > Mathf.Max(a.y, b.y) + limit)
                {
                    continue;
                }

                var ab = b - a;
                var t = ab.sqrMagnitude > 0.0001f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
                best = Mathf.Min(best, Vector2.Distance(p, a + ab * t));
            }

            return best;
        }

        private static Sprite BuildFabricOverlay()
        {
            const int size = 192;
            const float c = size / 2f;
            const float radius = c - 8f;
            const float tile = 30f;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (var py = 0; py < size; py++)
            {
                for (var px = 0; px < size; px++)
                {
                    var x = px + 0.5f;
                    var y = size - py - 0.5f;
                    var d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    var inside = Mathf.Clamp01(radius - 3f - d + 0.5f);

                    // Та же канва, что у ткани на картинке: светлые нити вдоль и поперёк.
                    var gx = Mathf.Abs(Mathf.Repeat(x, tile / 2f) - tile / 4f);
                    var gy = Mathf.Abs(Mathf.Repeat(y, tile / 2f) - tile / 4f);
                    var line = Mathf.Clamp01(2.2f - Mathf.Min(gx, gy) + 0.5f);
                    pixels[py * size + px] = new Color32(255, 255, 255, (byte)(line * inside * 0.75f * 255));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite BuildRays(int size, int count)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            var c = size / 2f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = x + 0.5f - c;
                    var dy = y + 0.5f - c;
                    var d = Mathf.Sqrt(dx * dx + dy * dy) / c;
                    var wave = 0.5f + 0.5f * Mathf.Cos(Mathf.Atan2(dy, dx) * count);
                    var ray = Mathf.SmoothStep(0.55f, 1f, wave);
                    var fade = Mathf.Clamp01(1f - d) * Mathf.Clamp01(d * 4f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(ray * fade * 255));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>
        /// Часть сундука в текстуру: тёплая карамельная вязка (столбики петель-«галочек»),
        /// лента посередине (у большого — золотая), тёмный кант по стыку крышки и корпуса,
        /// мягкая тень к низу и тонкий обод. Приглушённые цвета — сундук не спорит с картинкой.
        /// </summary>
        private static Sprite BuildChest(bool lid, bool big)
        {
            const float scale = 2f;
            var size = lid ? ChestLidSize : ChestBodySize;
            var tw = Mathf.CeilToInt(size.x * scale);
            var th = Mathf.CeilToInt(size.y * scale);
            var texture = new Texture2D(tw, th, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[tw * th];

            var knit = Palette.Hex("#D8A47C");
            var knitLight = Palette.Hex("#EBC39E");
            var band = big ? Palette.Hex("#E7B64E") : Palette.Hex("#A893DA");
            var bandLight = big ? Palette.Hex("#F6D88C") : Palette.Hex("#C6B7EE");
            var trim = Palette.Hex("#B57A52");
            var edge = Palette.Hex("#9A6544");
            var topRadius = lid ? 34f : 10f;
            var bottomRadius = lid ? 8f : 18f;
            const float bandHalf = 17f;
            const float rim = 9f;

            for (var py = 0; py < th; py++)
            {
                for (var px = 0; px < tw; px++)
                {
                    // Координаты в точках макета, y — вниз.
                    var x = (px + 0.5f) / scale;
                    var y = (th - py - 0.5f) / scale;
                    var radius = y < size.y / 2f ? topRadius : bottomRadius;
                    var qx = Mathf.Abs(x - size.x / 2f) - size.x / 2f + radius;
                    var qy = Mathf.Abs(y - size.y / 2f) - size.y / 2f + radius;
                    var d = Mathf.Sqrt(Mathf.Max(qx, 0) * Mathf.Max(qx, 0) + Mathf.Max(qy, 0) * Mathf.Max(qy, 0))
                            + Mathf.Min(Mathf.Max(qx, qy), 0) - radius;
                    var alpha = Mathf.Clamp01(0.5f - d * scale);
                    if (alpha <= 0f)
                    {
                        continue;
                    }

                    var inBand = Mathf.Abs(x - size.x / 2f) < bandHalf;
                    var baseColor = inBand ? band : knit;
                    var light = inBand ? bandLight : knitLight;

                    // Петли: столбики «галочек» 14×12, лента — со своим шагом, чтобы читалась отдельной полосой.
                    var cellW = inBand ? 11.33f : 14f;
                    var lx = Mathf.Repeat(x - (inBand ? size.x / 2f - bandHalf : 0f), cellW);
                    var ly = Mathf.Repeat(y + (lid ? 3f : 0f), 12f);
                    var left = DistanceToSegment(lx, ly, 1.8f, 1.5f, cellW / 2f, 10.5f);
                    var right = DistanceToSegment(lx, ly, cellW - 1.8f, 1.5f, cellW / 2f, 10.5f);
                    var stitch = Mathf.Clamp01(2.1f - Mathf.Min(left, right));
                    var color = Color.Lerp(baseColor, light, stitch * 0.85f);

                    // Кант на стыке: у крышки снизу, у корпуса сверху.
                    var fromSeam = lid ? size.y - y : y;
                    if (fromSeam < rim)
                    {
                        var bead = Mathf.Clamp01(1.2f - Mathf.Abs(Mathf.Repeat(x, 8f) - 4f) / 2.6f);
                        color = Color.Lerp(trim, Color.Lerp(trim, knitLight, 0.35f), bead * 0.6f);
                        if (fromSeam < 1.5f)
                        {
                            color = edge;
                        }
                    }

                    // Свет сверху, тень снизу, тонкий тёмный обод.
                    var shade = lid ? Mathf.Lerp(1.06f, 0.96f, y / size.y) : Mathf.Lerp(1.02f, 0.84f, (y / size.y) * (y / size.y));
                    color *= shade;
                    color = Color.Lerp(color, edge, Mathf.Clamp01(1f + (d + 2.2f) * 0.9f) * 0.9f);
                    color.a = alpha;
                    pixels[py * tw + px] = color;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, tw, th), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite BuildCell(bool knit, bool soft = false)
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
                        value = soft ? 0.93f : 0.84f;
                        var left = DistanceToSegment(x, y, 9f, 11f, 24f, 38f);
                        var right = DistanceToSegment(x, y, 39f, 11f, 24f, 38f);
                        var line = Mathf.Clamp01(5f - Mathf.Min(left, right) + 0.5f);
                        value = Mathf.Lerp(value, 1f, line);
                        if (y > 42f)
                        {
                            value *= soft ? 0.95f : 0.86f;
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

        private static Sprite BuildCanvasCell()
        {
            const int size = 48;
            const float radius = 11f;
            const float pitch = 8f;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (var py = 0; py < size; py++)
            {
                for (var px = 0; px < size; px++)
                {
                    var x = px + 0.5f;
                    var y = py + 0.5f;
                    var qx = Mathf.Abs(x - 24f) - 24f + radius;
                    var qy = Mathf.Abs(y - 24f) - 24f + radius;
                    var d = Mathf.Sqrt(Mathf.Max(qx, 0) * Mathf.Max(qx, 0) + Mathf.Max(qy, 0) * Mathf.Max(qy, 0))
                            + Mathf.Min(Mathf.Max(qx, qy), 0) - radius;
                    var alpha = Mathf.Clamp01(0.5f - d);

                    // Полотняное переплетение: нити вдоль и поперёк, в клетках между ними — чуть темнее.
                    var u = Mathf.Repeat(x, pitch) / pitch;
                    var v = Mathf.Repeat(y, pitch) / pitch;
                    var warp = Mathf.Sin(Mathf.PI * u);
                    var weft = Mathf.Sin(Mathf.PI * v);
                    var over = (Mathf.Floor(x / pitch) + Mathf.Floor(y / pitch)) % 2f == 0f;
                    var value = 0.9f + 0.08f * (over ? warp : weft);
                    var v8 = (byte)(value * 255);
                    pixels[py * size + px] = new Color32(v8, v8, v8, (byte)(alpha * 255));
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
                    // Домик: крыша, стены и дверь.
                    p.Stroke(2.4f).Line(3, 11.5f, 12, 3.5f).Line(12, 3.5f, 21, 11.5f)
                        .Box(12, 15, 6.5f, 5.5f, 1.6f, false).Box(12, 18, 1.7f, 2.8f, 0.8f, true);
                    break;
                case KnittedPicture.Game.Icon.Restart:
                    // Круговая стрелка по часовой: дуга и залитый наконечник сверху.
                    p.Stroke(2.5f).Arc(12, 12.5f, 7, 0, 262).Triangle(16.4f, 5.5f, 11, 1.8f, 11, 9.2f);
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
                case KnittedPicture.Game.Icon.Hand:
                    // Указательный палец вверх и ладонь.
                    p.Stroke(4.2f).Line(10, 4, 10, 14).Stroke(3.6f).Line(13.6f, 11, 13.6f, 14.5f).Line(16.8f, 12, 16.8f, 15)
                        .Box(13.4f, 17.4f, 5.6f, 4.6f, 3f, true).Stroke(3.4f).Line(5.6f, 13.6f, 8, 17.5f);
                    break;
                case KnittedPicture.Game.Icon.Snowflake:
                    p.Stroke(2.2f).Line(12, 3, 12, 21).Line(4.2f, 7.5f, 19.8f, 16.5f).Line(4.2f, 16.5f, 19.8f, 7.5f)
                        .Stroke(1.8f).Line(9.5f, 4.5f, 12, 7).Line(14.5f, 4.5f, 12, 7).Line(9.5f, 19.5f, 12, 17).Line(14.5f, 19.5f, 12, 17);
                    break;
                case KnittedPicture.Game.Icon.Info:
                    p.Stroke(2.4f).Circle(12, 12, 9).Disc(12, 7.6f, 1.6f).Stroke(2.6f).Line(12, 11, 12, 16.8f);
                    break;
                case KnittedPicture.Game.Icon.Gift:
                    p.Box(12, 15.5f, 7.5f, 5.5f, 1.5f, false).Box(12, 9.5f, 9, 2.5f, 1.2f, false).Line(12, 7, 12, 21)
                        .Circle(9, 5, 2.2f).Circle(15, 5, 2.2f);
                    break;
                case KnittedPicture.Game.Icon.Chest:
                    p.Box(12, 15, 8.5f, 5.5f, 2, false).Arc(12, 10, 8.5f, 180, 360).Line(3.5f, 10, 20.5f, 10).Box(12, 13, 1.8f, 2.2f, 0.8f, true);
                    break;
                case KnittedPicture.Game.Icon.Trophy:
                    p.Line(6, 5, 18, 5).Arc(12, 5, 6, 0, 180).Arc(6, 8, 2.6f, 90, 270).Arc(18, 8, 2.6f, -90, 90)
                        .Line(12, 11, 12, 17).Line(8, 19, 16, 19);
                    break;
                case KnittedPicture.Game.Icon.Bolt:
                    p.Triangle(14, 2, 5, 13.5f, 12.5f, 13.5f).Triangle(10, 22, 19, 10.5f, 11.5f, 10.5f);
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
