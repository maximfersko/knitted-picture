#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace KnittedPicture.Game
{
    /// <summary>
    /// Клубки 2.0 и всё, что на них надевается: шар, намотанный кручёной нитью, с глянцем сверху слева,
    /// цветной тенью снизу справа, ворсом по контуру и завитком нити слева внизу; накладки — лента
    /// связки с бантиком, замок на ремешке, ключик, узелки, ледяной пузырь, бумажная обёртка «?»,
    /// бирка-строчка для числа петель.
    /// </summary>
    public static class BallRaster
    {
        public const int Size = 160;

        /// <summary>Центр и радиус шара в пикселях (шар чуть выше центра: снизу место под завиток).</summary>
        public const float Cx = 84f;

        public const float Cy = 86f;

        public const float Radius = 62f;

        public enum Kind
        {
            Plain,
            Thick,
            Rainbow,

            /// <summary>Пряжа цвета ткани: намотана нитями канвы.</summary>
            Canvas,
        }

        /// <summary>Во сколько раз текстура клубка подробнее макета: 160 точек → 640 пикселей, шар ~500 px.</summary>
        public const int Quality = 4;

        /// <summary>Каналы формы на пиксель (см. Sample): покрытие, светотень, блик, рефлекс, кромка, оттенок радуги, ворс, кончик.</summary>
        private const int Channels = 13;

        private const float ShadeMin = -1f;
        private const float ShadeRange = 1.6f;
        private const float HairMin = -0.45f;
        private const float HairRange = 0.75f;
        private const float TailMin = -0.45f;
        private const float TailRange = 0.75f;
        private const float HazeMax = 0.1f;
        private const float TailShadowMax = 0.6f;

        /// <summary>
        /// Форма клубка каждого вида (без цвета): рисуется один раз за запуск на всех ядрах, а цвет
        /// накладывается простым проходом. Так клубок в 640 px стоит на старте уровня миллисекунды, а не секунды.
        /// </summary>
        private static readonly Dictionary<Kind, byte[]> Shapes = new Dictionary<Kind, byte[]>();

        /// <summary>
        /// Клубок: четыре слоя намотки (основа по всему шару и три ленты поверх, каждая отбрасывает мягкую
        /// тень на нижние). Каждая нить — кручёная трубочка со своим тоном, чуть волнистая, с продольными
        /// волокнами; свет и блик считаются по её собственному изгибу, поэтому у каждой нити свой блик.
        /// Снизу справа густой тот же оттенок и рефлекс по краю. Контур неровный от нитей, по нему тонкие
        /// волоски ворса, поверх шара — несколько выбившихся волосков; слева внизу кончик нити завитком.
        /// Нити мельче пикселя на краю шара плавно сливаются в средний тон — без ряби.
        /// </summary>
        public static Raster Ball(Rgba color, Kind kind = Kind.Plain)
        {
            var shape = ShapeOf(kind);
            const int n = Size * Quality;
            var r = new Raster(n, n);
            var rainbow = kind == Kind.Rainbow;

            // Тона по каналу формы заранее: 256 значений светотени вместо пересчёта в каждом пикселе.
            var body = Lut(color, ShadeMin, ShadeRange);
            var hairs = Lut(color, HairMin, HairRange);
            var tails = Lut(rainbow ? Hue(0.92f) : color, TailMin, TailRange);
            var lighter = color.Shade(0.25f);
            var hazeColor = color.Shade(-0.05f);
            Parallel.For(0, n, py =>
            {
                for (var px = 0; px < n; px++)
                {
                    var i = py * n + px;
                    var o = i * Channels;
                    var result = Rgba.Clear;
                    var cover = shape[o];
                    if (cover > 0)
                    {
                        Rgba c;
                        if (rainbow)
                        {
                            var hue = Hue(shape[o + 5] / 255f);
                            c = hue.Shade(shape[o + 1] / 255f * ShadeRange + ShadeMin);
                            c = Rgba.Lerp(c, Rgba.White, shape[o + 2] / 255f);
                            c = Rgba.Lerp(c, hue.Shade(0.25f), shape[o + 3] / 255f);
                        }
                        else
                        {
                            c = body[shape[o + 1]];
                            if (shape[o + 2] > 0)
                            {
                                c = Rgba.Lerp(c, Rgba.White, shape[o + 2] / 255f);
                            }

                            if (shape[o + 3] > 0)
                            {
                                c = Rgba.Lerp(c, lighter, shape[o + 3] / 255f);
                            }
                        }

                        if (shape[o + 4] > 0)
                        {
                            c = c.Shade(-0.12f * shape[o + 4] / 255f);
                        }

                        result = c.WithAlpha(cover / 255f);
                    }

                    if (shape[o + 6] > 0)
                    {
                        var haze = rainbow ? Hue(shape[o + 9] / 255f).Shade(-0.05f) : hazeColor;
                        result = Rgba.Over(haze.WithAlpha(shape[o + 6] / 255f * HazeMax), result);
                    }

                    if (shape[o + 7] > 0)
                    {
                        var hair = rainbow ? Hue(shape[o + 9] / 255f).Shade(shape[o + 8] / 255f * HairRange + HairMin) : hairs[shape[o + 8]];
                        result = Rgba.Over(hair.WithAlpha(shape[o + 7] / 255f), result);
                    }

                    if (shape[o + 10] > 0 && result.A > 0f)
                    {
                        result = Rgba.Lerp(result, result.Shade(-0.35f).WithAlpha(result.A), shape[o + 10] / 255f * TailShadowMax);
                    }

                    if (shape[o + 11] > 0)
                    {
                        result = Rgba.Over(tails[shape[o + 12]].WithAlpha(shape[o + 11] / 255f), result);
                    }

                    r.Pixels[i] = result;
                }
            });
            return r;
        }

        private static Rgba[] Lut(Rgba color, float min, float range)
        {
            var lut = new Rgba[256];
            for (var i = 0; i < 256; i++)
            {
                lut[i] = color.Shade(i / 255f * range + min);
            }

            return lut;
        }

        /// <summary>Нарисовать форму обычного клубка заранее (игра зовёт в фоне при запуске).</summary>
        public static void Warm() => ShapeOf(Kind.Plain);

        private static byte[] ShapeOf(Kind kind)
        {
            lock (Shapes)
            {
                if (!Shapes.TryGetValue(kind, out var shape))
                {
                    shape = BuildShape(kind);
                    Shapes[kind] = shape;
                }

                return shape;
            }
        }

        private static byte[] BuildShape(Kind kind)
        {
            const int n = Size * Quality;
            var data = new byte[n * n * Channels];
            var context = new Context(kind);
            Parallel.For(0, n, py =>
            {
                // Размер пикселя — свой у каждого потока: ставим на время строки и возвращаем.
                var pixel = Draw.Pixel;
                Draw.Pixel = 1f / Quality;
                try
                {
                    for (var px = 0; px < n; px++)
                    {
                        context.Sample((px + 0.5f) / Quality, (py + 0.5f) / Quality, data, (py * n + px) * Channels);
                    }
                }
                finally
                {
                    Draw.Pixel = pixel;
                }
            });
            return data;
        }

        private static byte Quantize(float v) => (byte)MathF.Round(Draw.Clamp01(v) * 255f);

        /// <summary>Всё, что нужно для формы клубка одного вида: слои намотки, волоски, кончик нити, свет.</summary>
        private sealed class Context
        {
            private readonly Kind _kind;
            private readonly Layer[] _layers;
            private readonly List<Fibre> _fibres;
            private readonly List<Stray> _strays;
            private readonly List<(float X, float Y)> _tail;
            private readonly (float X, float Y, float Z) _light = Normalize(-0.5f, 0.62f, 0.6f);
            private readonly (float X, float Y, float Z) _halfway;

            public Context(Kind kind)
            {
                _kind = kind;
                var strand = kind switch
                {
                    Kind.Thick => 0.19f,
                    Kind.Canvas => 0.15f,
                    _ => 0.11f,
                };
                _layers = new[]
                {
                    new Layer(0.25f, 1f, 0.22f, 9f, strand, 0.3f),
                    new Layer(0.97f, -0.12f, 0.2f, 0.26f, strand, 4.1f),
                    new Layer(0.8f, 0.5f, 0.3f, 0.42f, strand, 1.7f),
                    new Layer(-0.62f, 0.72f, 0.3f, 0.33f, strand, 2.9f),
                };
                _halfway = Normalize(_light.X, _light.Y, _light.Z + 1f);
                _fibres = Fibres();
                _strays = Strays();
                _tail = Tail();
            }

            public void Sample(float x, float y, byte[] data, int o)
            {
                var dx = x - Cx;
                var dy = y - Cy;
                var dist = MathF.Sqrt(dx * dx + dy * dy);
                float cover = 0f, shade = 0f, white = 0f, fresnelMix = 0f, edge = 0f, hue = 0f;
                if (dist < Radius + 3f)
                {
                    // Нормаль сферы; за краем — как на самом краю (нужна для неровного контура).
                    var k = MathF.Min(dist, Radius * 0.999f) / MathF.Max(dist, 0.0001f);
                    var nx = dx * k / Radius;
                    var ny = dy * k / Radius;
                    var nz = MathF.Sqrt(MathF.Max(0.0001f, 1f - nx * nx - ny * ny));

                    // Верхний слой, которому принадлежит точка, и тень от лент над ним.
                    var top = 0;
                    for (var l = _layers.Length - 1; l > 0; l--)
                    {
                        if (MathF.Abs(_layers[l].Dot(nx, ny, nz)) < _layers[l].Band)
                        {
                            top = l;
                            break;
                        }
                    }

                    var occlusion = 1f;
                    for (var l = top + 1; l < _layers.Length; l++)
                    {
                        var gap = MathF.Abs(_layers[l].Dot(nx, ny, nz)) - _layers[l].Band;
                        occlusion *= 1f - 0.4f * (1f - Draw.SmoothStep(0f, 0.16f, gap));
                    }

                    var layer = _layers[top];
                    var tube = layer.Strand(nx, ny, nz, Radius, out var index, out var signed, out var twist, out var across, out var streak);

                    // Нить у края своей ленты чуть уходит вниз — лента читается выпуклой.
                    if (top > 0)
                    {
                        var rim = layer.Band - MathF.Abs(layer.Dot(nx, ny, nz));
                        occlusion *= 0.82f + 0.18f * Draw.SmoothStep(0f, 0.07f, rim);
                    }

                    // Неровный контур: макушки нитей чуть выступают за круг.
                    var d = dist - (Radius + (tube - 0.7f) * 1.4f);
                    cover = Draw.Cover(d);
                    if (cover > 0f)
                    {
                        var jitter = (Draw.Hash((int)index + top * 97, top * 31 + 5) - 0.5f) * 0.1f;
                        hue = index * 0.09f + top * 0.31f;
                        hue -= MathF.Floor(hue);
                        var value = 0.56f + 0.44f * MathF.Pow(tube, 0.7f);
                        value *= 1f + 0.05f * twist + 0.05f * streak;
                        if (_kind == Kind.Canvas)
                        {
                            // Пряжа цвета ткани — плоская тесьма полотняного плетения: нити поперёк ленты.
                            value *= 0.82f + 0.18f * across;
                        }

                        // Нормаль самой нити: поперёк трубочки она наклоняется — у каждой нити свой свет и блик.
                        layer.Across(nx, ny, nz, out var ax, out var ay, out var az);
                        var (mx, my, mz) = Normalize(nx + ax * signed * 0.8f, ny + ay * signed * 0.8f, nz + az * signed * 0.8f);
                        var lambert = nx * _light.X + ny * _light.Y + nz * _light.Z;
                        var micro = mx * _light.X + my * _light.Y + mz * _light.Z - lambert;
                        var wrap = Draw.Clamp01((lambert + 0.35f) / 1.35f);
                        shade = (value - 1f) * 0.85f + (wrap - 0.72f) * 0.8f + micro * 0.3f + jitter + (occlusion - 1f) * 0.9f;

                        // Блик ложится на макушки нитей, широкий мягкий глянец — на всю светлую сторону.
                        var spec = MathF.Pow(Draw.Clamp01(mx * _halfway.X + my * _halfway.Y + mz * _halfway.Z), 26f);
                        var broad = MathF.Pow(Draw.Clamp01(nx * _halfway.X + ny * _halfway.Y + nz * _halfway.Z), 10f);
                        var sheen = MathF.Pow(Draw.Clamp01(lambert), 4f);
                        white = (spec * 0.4f + broad * 0.12f) * (0.35f + 0.65f * tube) * occlusion + sheen * 0.07f;

                        // Рефлекс по теневому краю и лёгкое затемнение у самого контура.
                        fresnelMix = MathF.Pow(1f - nz, 3f) * Draw.Clamp01(0.3f - lambert) * 0.7f;
                        edge = Draw.SmoothStep(-6f, 0f, d);
                    }
                }

                // Ворс: дымка пуха и тонкие изогнутые волоски по контуру, выбившиеся волоски поверх шара.
                float haze = 0f, hairAlpha = 0f, hairShade = 0f, hairHue = 0f;
                var angle = MathF.Atan2(dy, dx);
                if (dist > Radius - 4f && dist < Radius + 10f)
                {
                    haze = Draw.Clamp01(1f - (dist - Radius) / 4f) * Draw.SmoothStep(Radius - 1f, Radius + 0.5f, dist) * 0.08f;
                    hairHue = angle * 0.16f;
                    foreach (var f in _fibres)
                    {
                        if (MathF.Abs(Wrap(angle - f.Angle)) > 0.2f)
                        {
                            continue;
                        }

                        var hair = MathF.Min(Draw.Segment(x, y, f.Ax, f.Ay, f.Bx, f.By), Draw.Segment(x, y, f.Bx, f.By, f.Tx, f.Ty));
                        var hairCover = Draw.Cover(hair - f.Width);
                        if (hairCover <= 0f)
                        {
                            continue;
                        }

                        var tip = Draw.Clamp01(MathF.Sqrt((x - f.Ax) * (x - f.Ax) + (y - f.Ay) * (y - f.Ay)) / f.Length);
                        var lit = Draw.Clamp01(0.5f - 0.5f * MathF.Cos(f.Angle - 2.2f));
                        AddHair(ref hairAlpha, ref hairShade, hairCover * 0.7f * (1f - 0.7f * tip), -0.2f + 0.32f * lit);
                    }
                }

                if (cover > 0.5f)
                {
                    foreach (var s in _strays)
                    {
                        if (MathF.Abs(x - s.Bx) > s.Reach || MathF.Abs(y - s.By) > s.Reach)
                        {
                            continue;
                        }

                        var hair = MathF.Min(Draw.Segment(x, y, s.Ax, s.Ay, s.Bx, s.By), Draw.Segment(x, y, s.Bx, s.By, s.Tx, s.Ty));
                        var hairCover = Draw.Cover(hair - 0.16f);
                        if (hairCover > 0f)
                        {
                            AddHair(ref hairAlpha, ref hairShade, hairCover * 0.55f, 0.18f);
                            hairHue = hue;
                        }
                    }
                }

                // Кончик нити слева внизу: кручёная трубочка завитком, с тенью на клубке.
                float tailShadow = 0f, tailCover = 0f, tailShade = 0f;
                var tailDistance = Polyline(_tail, x, y, 8f, out var along);
                if (tailDistance < 7.5f)
                {
                    const float tailRadius = 4.4f;
                    if (cover > 0f && tailDistance > tailRadius - 0.5f)
                    {
                        tailShadow = (1f - Draw.Clamp01((tailDistance - tailRadius) / 3f)) * 0.6f;
                    }

                    tailCover = Draw.Cover(tailDistance - tailRadius);
                    if (tailCover > 0f)
                    {
                        var profile = MathF.Sqrt(Draw.Clamp01(1f - tailDistance * tailDistance / (tailRadius * tailRadius)));
                        var ply = MathF.Sin((along / 5.5f + tailDistance / tailRadius * 0.45f) * MathF.PI * 2f);
                        var fibre = MathF.Sin((tailDistance / tailRadius * 3f + along * 0.15f) * MathF.PI * 2f);
                        tailShade = -0.3f + 0.42f * profile + 0.07f * ply * profile + 0.025f * fibre * profile;
                    }
                }

                hairHue -= MathF.Floor(hairHue);
                data[o] = Quantize(cover);
                data[o + 1] = Quantize((Draw.Clamp(shade, ShadeMin, ShadeMin + ShadeRange) - ShadeMin) / ShadeRange);
                data[o + 2] = Quantize(white);
                data[o + 3] = Quantize(fresnelMix);
                data[o + 4] = Quantize(edge);
                data[o + 5] = Quantize(hue);
                data[o + 6] = Quantize(haze / HazeMax);
                data[o + 7] = Quantize(hairAlpha);
                data[o + 8] = Quantize((hairShade - HairMin) / HairRange);
                data[o + 9] = Quantize(hairHue);
                data[o + 10] = Quantize(tailShadow / TailShadowMax);
                data[o + 11] = Quantize(tailCover);
                data[o + 12] = Quantize((tailShade - TailMin) / TailRange);
            }

            /// <summary>Волосок поверх уже лежащих: общая прозрачность растёт, тон — средний по весу.</summary>
            private static void AddHair(ref float alpha, ref float shade, float a, float s)
            {
                var total = alpha + a * (1f - alpha);
                if (total > 0f)
                {
                    shade = (shade * alpha * (1f - a) + s * a) / (alpha * (1f - a) + a);
                }

                alpha = total;
            }
        }

        /// <summary>Волосок ворса: от контура наружу с изгибом (два отрезка).</summary>
        private readonly struct Fibre
        {
            public Fibre(float angle, float ax, float ay, float bx, float by, float tx, float ty, float length, float width)
            {
                Angle = angle;
                Ax = ax;
                Ay = ay;
                Bx = bx;
                By = by;
                Tx = tx;
                Ty = ty;
                Length = length;
                Width = width;
            }

            public float Angle { get; }

            public float Ax { get; }

            public float Ay { get; }

            public float Bx { get; }

            public float By { get; }

            public float Tx { get; }

            public float Ty { get; }

            public float Length { get; }

            public float Width { get; }
        }

        /// <summary>Выбившийся волосок поверх намотки: тонкая светлая дужка (два отрезка).</summary>
        private readonly struct Stray
        {
            public Stray(float ax, float ay, float bx, float by, float tx, float ty, float reach)
            {
                Ax = ax;
                Ay = ay;
                Bx = bx;
                By = by;
                Tx = tx;
                Ty = ty;
                Reach = reach;
            }

            public float Ax { get; }

            public float Ay { get; }

            public float Bx { get; }

            public float By { get; }

            public float Tx { get; }

            public float Ty { get; }

            public float Reach { get; }
        }

        private static List<Fibre> Fibres()
        {
            const int count = 64;
            var list = new List<Fibre>();
            for (var i = 0; i < count; i++)
            {
                var angle = (i + Draw.Hash(i, 11) * 0.8f) / count * MathF.PI * 2f - MathF.PI;
                var length = 2.2f + Draw.Hash(i, 23) * 4f;
                var lean = (Draw.Hash(i, 37) < 0.5f ? -1f : 1f) * (0.5f + Draw.Hash(i, 41) * 0.5f);
                var ox = MathF.Cos(angle);
                var oy = MathF.Sin(angle);
                var ax = Cx + ox * (Radius - 1.5f);
                var ay = Cy + oy * (Radius - 1.5f);

                // Наружу и вбок по касательной; кончик загибается сильнее.
                var mx = ox + -oy * lean * 0.6f;
                var my = oy + ox * lean * 0.6f;
                var bx = ax + mx * length * 0.5f;
                var by = ay + my * length * 0.5f;
                var tx = bx + (ox + -oy * lean * 1.3f) * length * 0.5f;
                var ty = by + (oy + ox * lean * 1.3f) * length * 0.5f;
                list.Add(new Fibre(angle, ax, ay, bx, by, tx, ty, length, 0.18f + Draw.Hash(i, 53) * 0.1f));
            }

            return list;
        }

        private static List<Stray> Strays()
        {
            const int count = 9;
            var list = new List<Stray>();
            for (var i = 0; i < count; i++)
            {
                var angle = Draw.Hash(i, 61) * MathF.PI * 2f;
                var reach = MathF.Sqrt(Draw.Hash(i, 67)) * Radius * 0.78f;
                var bx = Cx + MathF.Cos(angle) * reach;
                var by = Cy + MathF.Sin(angle) * reach;
                var dir = Draw.Hash(i, 71) * MathF.PI * 2f;
                var length = 5f + Draw.Hash(i, 73) * 6f;
                var bend = (Draw.Hash(i, 79) - 0.5f) * 1.6f;
                var ax = bx - MathF.Cos(dir) * length * 0.5f;
                var ay = by - MathF.Sin(dir) * length * 0.5f;
                var tx = bx + MathF.Cos(dir + bend) * length * 0.5f;
                var ty = by + MathF.Sin(dir + bend) * length * 0.5f;
                list.Add(new Stray(ax, ay, bx, by, tx, ty, length));
            }

            return list;
        }

        private static float Wrap(float angle)
        {
            while (angle > MathF.PI)
            {
                angle -= MathF.PI * 2f;
            }

            while (angle < -MathF.PI)
            {
                angle += MathF.PI * 2f;
            }

            return angle;
        }

        /// <summary>
        /// Слой намотки: нити идут по параллелям вокруг оси (ax, ay, az), лента — пояс шириной band вокруг
        /// её экватора (band ≥ 1 — весь шар). width — толщина нити в радианах широты, seed — свой рисунок волны.
        /// </summary>
        private sealed class Layer
        {
            private readonly float _ax;
            private readonly float _ay;
            private readonly float _az;
            private readonly float _ux;
            private readonly float _uy;
            private readonly float _uz;
            private readonly float _vx;
            private readonly float _vy;
            private readonly float _vz;
            private readonly float _width;
            private readonly float _seed;

            public Layer(float ax, float ay, float az, float band, float width, float seed)
            {
                (_ax, _ay, _az) = Normalize(ax, ay, az);
                Band = band;
                _width = width;
                _seed = seed;

                // Базис в плоскости экватора — для долготы (кручение и волокна вдоль нити).
                (_ux, _uy, _uz) = Normalize(-_ay, _ax, 0f);
                _vx = _ay * _uz - _az * _uy;
                _vy = _az * _ux - _ax * _uz;
                _vz = _ax * _uy - _ay * _ux;
            }

            public float Band { get; }

            public float Dot(float nx, float ny, float nz) => nx * _ax + ny * _ay + nz * _az;

            /// <summary>Направление поперёк нити по поверхности (к росту широты).</summary>
            public void Across(float nx, float ny, float nz, out float x, out float y, out float z)
            {
                var dot = Dot(nx, ny, nz);
                (x, y, z) = Normalize(_ax - dot * nx + 0.0001f, _ay - dot * ny, _az - dot * nz);
            }

            /// <summary>
            /// Профиль нити в точке: 1 — макушка трубочки, 0 — щель между нитями. index — номер нити,
            /// signed — положение поперёк (−1..1), twist — кручение, across — плетение тесьмы (0..1),
            /// streak — продольные волокна. Где нити мельче пикселя, всё плавно уходит в средний тон.
            /// </summary>
            public float Strand(float nx, float ny, float nz, float radius, out float index, out float signed, out float twist,
                out float across, out float streak)
            {
                var dot = Draw.Clamp(Dot(nx, ny, nz), -0.9999f, 0.9999f);
                var latitude = MathF.Asin(dot);
                var longitude = MathF.Atan2(nx * _vx + ny * _vy + nz * _vz, nx * _ux + ny * _uy + nz * _uz);

                // Нити чуть волнистые: намотаны рукой, а не станком. Целое число волн по кругу — без шва.
                var k = (latitude + _width * 0.35f * MathF.Sin(longitude * 3f + _seed) * MathF.Cos(latitude)) / _width;
                index = MathF.Floor(k);
                var f = k - index;
                signed = 2f * f - 1f;
                var tube = MathF.Sqrt(MathF.Max(0f, 1f - signed * signed));

                // Сколько нитей приходится на пиксель: производная широты по экрану.
                var z = MathF.Max(nz, 0.06f);
                var gx = (_ax - _az * nx / z) / radius;
                var gy = (_ay - _az * ny / z) / radius;
                var perPixel = MathF.Sqrt(gx * gx + gy * gy) / MathF.Sqrt(1f - dot * dot) / _width * Draw.Pixel;
                var blur = Draw.SmoothStep(0.12f, 0.45f, perPixel);
                tube = Draw.Lerp(tube, 0.78f, blur);
                signed *= 1f - blur;

                // Кручение: косые светлые полоски вдоль нити; число витков по кругу целое — без шва.
                var circle = MathF.Cos((index + 0.5f) * _width) * radius * MathF.PI * 2f;
                var turns = MathF.Max(1f, MathF.Round(circle / 6f));
                var fade = (1f - blur) * Draw.SmoothStep(0.12f, 0.4f, nz);
                twist = MathF.Sin(longitude * turns + f * MathF.PI * 1.8f) * tube * fade;
                var weave = MathF.Sin(longitude * MathF.Round(turns * 1.6f)) * MathF.Sin(f * MathF.PI * 4f);
                across = 0.5f + 0.5f * weave * fade;

                // Волокна: тонкие штрихи вдоль нити, у каждой нити свой рисунок; мелкие гаснут раньше.
                var fine = fade * (1f - Draw.SmoothStep(0.12f, 0.45f, perPixel * 5f));
                var h1 = Draw.Hash((int)index, (int)(_seed * 10f)) * MathF.PI * 2f;
                var h2 = Draw.Hash((int)index + 7, (int)(_seed * 10f) + 3) * MathF.PI * 2f;
                var slow1 = MathF.Max(1f, MathF.Round(circle / 9f));
                var slow2 = MathF.Max(1f, MathF.Round(circle / 5f));
                streak = (0.55f * MathF.Sin(f * MathF.PI * 6f + 1.2f * MathF.Sin(longitude * slow1 + h1))
                    + 0.45f * MathF.Sin(f * MathF.PI * 10f + 1.5f * MathF.Sin(longitude * slow2 + h2))) * fine;
                return tube;
            }
        }

        /// <summary>Скрытый цвет: клубок в бумажной обёртке со строчкой по краю (знак «?» рисует игра).</summary>
        public static Raster Hidden()
        {
            var paper = Rgba.Hex("#FBF8F2");
            var stitch = Rgba.Hex("#C9BEF5");
            return Raster.Render(Size, Size, Quality, (x, y) =>
            {
                var d = Draw.Circle(x, y, Cx, Cy, Radius);
                var cover = Draw.Cover(d);
                if (cover <= 0f)
                {
                    return Rgba.Clear;
                }

                var nx = (x - Cx) / Radius;
                var ny = (y - Cy) / Radius;
                var lambert = Draw.Clamp01(-0.5f * nx + 0.55f * ny + 0.65f * MathF.Sqrt(Draw.Clamp01(1f - nx * nx - ny * ny)));
                var c = paper.Shade(-0.12f * (1f - lambert));
                var ring = MathF.Abs(d + 7f);
                var angle = MathF.Atan2(y - Cy, x - Cx);

                // Стежок строчки со сглаженными краями: и поперёк, и по концам.
                var dash = Draw.Clamp01(0.5f + MathF.Sin(angle * 22f) * (Radius - 7f) / 22f / Draw.Pixel);
                c = Rgba.Lerp(c, stitch, 0.9f * dash * Draw.Cover(ring - 1.6f));
                return c.WithAlpha(cover);
            });
        }

        /// <summary>Лента связки: фиолетовая полоса поперёк клубка и бантик снизу. Белая — красится цветом пары.</summary>
        public static Raster Ribbon()
        {
            var r = new Raster(Size, Size);
            r.Paint((x, y) =>
            {
                var curve = Cy - 8f + MathF.Pow((x - Cx) / Radius, 2f) * 10f;
                var inside = Draw.Circle(x, y, Cx, Cy, Radius + 1f) < 0f;
                var band = MathF.Abs(y - curve) - 9f;
                var cover = inside ? Draw.Cover(band) : 0f;
                var shade = 0.82f + 0.18f * Draw.Clamp01((y - curve + 9f) / 18f);

                // Бантик: две петли и узелок под лентой.
                var bow = MathF.Min(Draw.Ellipse(x, y, Cx - 12f, curve - 14f, 12f, 8f, 25f), Draw.Ellipse(x, y, Cx + 12f, curve - 14f, 12f, 8f, -25f));
                var tails = MathF.Min(Draw.Capsule(x, y, Cx - 3f, curve - 10f, Cx - 10f, curve - 30f, 4f, 3f, out _, out _),
                    Draw.Capsule(x, y, Cx + 3f, curve - 10f, Cx + 12f, curve - 28f, 4f, 3f, out _, out _));
                var knot = Draw.Circle(x, y, Cx, curve - 10f, 6f);
                var bowCover = Draw.Cover(MathF.Min(MathF.Min(bow, tails), knot));
                var all = MathF.Max(cover, bowCover);
                if (all <= 0f)
                {
                    return Rgba.Clear;
                }

                var v = bowCover > cover ? (knot < 0f ? 0.78f : 0.9f) : shade;
                return new Rgba(v, v, v, all);
            });
            return r;
        }

        /// <summary>Замок на ремешке: латунный навесной замочек посередине клубка.</summary>
        public static Raster Padlock()
        {
            var r = new Raster(Size, Size);
            var brass = Rgba.Hex("#E2B04A");
            var strap = Rgba.Hex("#8B6FD6");
            r.Paint((x, y) =>
            {
                var result = Rgba.Clear;
                var curve = Cy + 4f + MathF.Pow((x - Cx) / Radius, 2f) * 10f;
                if (Draw.Circle(x, y, Cx, Cy, Radius + 1f) < 0f)
                {
                    var band = Draw.Cover(MathF.Abs(y - curve) - 5f);
                    result = strap.Shade(-0.1f * Draw.Clamp01((curve - y) / 5f)).WithAlpha(band);
                }

                var shackle = MathF.Abs(Draw.Circle(x, y, Cx, Cy - 2f, 11f)) - 3.2f;
                if (y < Cy - 2f)
                {
                    shackle = 99f;
                }

                var body = Draw.RoundBox(x, y, Cx, Cy - 22f, 18f, 15f, 5f);
                var keyhole = MathF.Min(Draw.Circle(x, y, Cx, Cy - 18f, 3.4f), Draw.RoundBox(x, y, Cx, Cy - 26f, 1.6f, 5f, 1f));
                var lockCover = Draw.Cover(MathF.Min(shackle, body));
                if (lockCover > 0f)
                {
                    var lit = Draw.Clamp01((y - (Cy - 37f)) / 30f);
                    var c = body < 0f ? brass.Shade(-0.25f + 0.35f * lit) : Rgba.Hex("#A8742A").Shade(0.2f);
                    if (body < 0f && keyhole < 0f)
                    {
                        c = Rgba.Hex("#5B3A1A");
                    }

                    result = Rgba.Over(c.WithAlpha(lockCover), result);
                }

                return result;
            });
            return r;
        }

        /// <summary>Ключик, заткнутый в намотку справа сверху; латунь с бликом.</summary>
        public static Raster Key()
        {
            var r = new Raster(Size, Size);
            var brass = Rgba.Hex("#E2B04A");
            r.Paint((x, y) =>
            {
                var bow = MathF.Abs(Draw.Circle(x, y, Cx - 2f, Cy + 22f, 15f)) - 6f;
                var shaft = Draw.Capsule(x, y, Cx + 12f, Cy + 30f, Cx + 62f, Cy + 58f, 5f, 5f, out _, out _);
                var teeth = MathF.Min(Draw.RoundBox(x, y, Cx + 50f, Cy + 44f, 4f, 7f, 1.5f), Draw.RoundBox(x, y, Cx + 39f, Cy + 38f, 4f, 6f, 1.5f));
                var d = MathF.Min(MathF.Min(bow, shaft), teeth);
                var cover = Draw.Cover(d);
                if (cover <= 0f)
                {
                    return Rgba.Clear;
                }

                var c = brass.Shade(-0.25f * Draw.SmoothStep(-3f, 0f, d));
                c = Rgba.Lerp(c, Rgba.White, Draw.Clamp01(1f - MathF.Abs(d + 2f)) * 0.35f);
                return c.WithAlpha(cover);
            });
            return r;
        }

        /// <summary>Узелки сверху клубка: сколько count (1–3), сливовые шишечки с петелькой.</summary>
        public static Raster Knots(int count)
        {
            var r = new Raster(Size, Size);
            var plum = Rgba.Hex("#5B3F86");
            var spots = count switch
            {
                1 => new[] { 0f },
                2 => new[] { -20f, 20f },
                _ => new[] { -32f, 0f, 32f },
            };
            r.Paint((x, y) =>
            {
                var result = Rgba.Clear;
                foreach (var sx in spots)
                {
                    var kx = Cx + sx;
                    var ky = Cy + Radius - 10f - MathF.Abs(sx) * 0.3f;
                    var knot = Draw.Ellipse(x, y, kx, ky + 8f, 13f, 10f);
                    var loop = MathF.Abs(Draw.Circle(x, y, kx, ky + 21f, 9f)) - 3.2f;
                    var d = MathF.Min(knot, y > ky + 17f ? loop : 99f);
                    var cover = Draw.Cover(d);
                    if (cover <= 0f)
                    {
                        continue;
                    }

                    var lit = Draw.Clamp01((y - ky) / 14f);
                    result = Rgba.Over(plum.Shade(-0.15f + 0.35f * lit).WithAlpha(cover), result);
                }

                return result;
            });
            return r;
        }

        /// <summary>
        /// Ледяной пузырь: прозрачная голубая сфера чуть больше клубка, светлый обод, блик и трещины —
        /// чем меньше льда осталось (stage 0..3), тем больше трещин.
        /// </summary>
        public static Raster Ice(int stage)
        {
            var r = new Raster(Size, Size);
            var fill = Rgba.Hex("#BFF1F5");
            var rim = Rgba.Hex("#5CC8D8");
            var cracks = new List<(float, float, float, float)>
            {
                (Cx + 18f, Cy + Radius, Cx + 8f, Cy + 30f),
                (Cx + 8f, Cy + 30f, Cx + 20f, Cy + 12f),
                (Cx - 40f, Cy - 30f, Cx - 22f, Cy - 18f),
                (Cx - 22f, Cy - 18f, Cx - 26f, Cy + 2f),
                (Cx + 46f, Cy - 20f, Cx + 28f, Cy - 26f),
                (Cx - 6f, Cy - Radius, Cx - 2f, Cy - 40f),
            };
            var shown = Math.Min(cracks.Count, stage * 2);
            r.Paint((x, y) =>
            {
                var d = Draw.Circle(x, y, Cx, Cy, Radius + 6f);
                var cover = Draw.Cover(d);
                if (cover <= 0f)
                {
                    return Rgba.Clear;
                }

                var edge = Draw.SmoothStep(-10f, 0f, d);
                var c = Rgba.Lerp(fill.WithAlpha(0.32f), rim.WithAlpha(0.9f), edge * edge);
                var shine = Draw.Ellipse(x, y, Cx - 26f, Cy + 32f, 16f, 8f, -35f);
                if (shine < 0f)
                {
                    c = Rgba.Over(Rgba.White.WithAlpha(0.65f * Draw.Clamp01(-shine / 4f)), c);
                }

                for (var i = 0; i < shown; i++)
                {
                    var (ax, ay, bx, by) = cracks[i];
                    var crack = Draw.Segment(x, y, ax, ay, bx, by);
                    if (crack < 1.6f)
                    {
                        c = Rgba.Over(Rgba.White.WithAlpha(0.85f * (1f - crack / 1.6f)), c);
                    }
                }

                return c.WithAlpha(c.A * cover);
            });
            return r;
        }

        /// <summary>Бирка числа петель: белый кружок с пунктирной строчкой по краю.</summary>
        public static Raster Badge()
        {
            const int s = 64;
            var r = new Raster(s, s);
            var stitch = Rgba.Hex("#9488C8");
            r.Paint((x, y) =>
            {
                var d = Draw.Circle(x, y, s / 2f, s / 2f, s / 2f - 2f);
                var cover = Draw.Cover(d);
                if (cover <= 0f)
                {
                    return Rgba.Clear;
                }

                var c = Rgba.White.Shade(-0.04f * Draw.Clamp01((s / 2f - y) / (s / 2f)));
                var ring = MathF.Abs(d + 5f);
                var angle = MathF.Atan2(y - s / 2f, x - s / 2f);
                if (ring < 1.3f && MathF.Sin(angle * 14f) > 0f)
                {
                    c = Rgba.Lerp(c, stitch, 0.85f * (1f - ring / 1.3f));
                }

                return c.WithAlpha(cover);
            });
            return r;
        }

        /// <summary>Мягкая лавандовая тень под клубком (овал).</summary>
        public static Raster Shadow()
        {
            const int w = 96;
            const int h = 32;
            var r = new Raster(w, h);
            var shade = Rgba.Hex("#5B45AE");
            r.Paint((x, y) =>
            {
                var d = Draw.Ellipse(x, y, w / 2f, h / 2f, w / 2f - 2f, h / 2f - 2f);
                var t = Draw.Clamp01(-d / 14f);
                return shade.WithAlpha(0.22f * t * t);
            });
            return r;
        }

        private static Rgba Hue(float h)
        {
            h = h - MathF.Floor(h);
            var r = Draw.Clamp01(MathF.Abs(h * 6f - 3f) - 1f);
            var g = Draw.Clamp01(2f - MathF.Abs(h * 6f - 2f));
            var b = Draw.Clamp01(2f - MathF.Abs(h * 6f - 4f));
            return Rgba.Lerp(new Rgba(r, g, b), Rgba.White, 0.32f);
        }

        private static (float X, float Y, float Z) Normalize(float x, float y, float z)
        {
            var l = MathF.Sqrt(x * x + y * y + z * z);
            return (x / l, y / l, z / l);
        }

        /// <summary>Завиток: выходит из клубка слева внизу и закручивается колечком.</summary>
        private static List<(float X, float Y)> Tail()
        {
            var points = new List<(float X, float Y)>();
            for (var i = 0; i <= 40; i++)
            {
                var t = i / 40f;
                var angle = 3.75f + t * 4.6f;
                var radius = 22f - 12f * t;
                var ox = Cx - 52f + 18f * t;
                var oy = Cy - 50f;
                points.Add((ox + MathF.Cos(angle) * radius * (1f - 0.4f * t), oy + MathF.Sin(angle) * radius * 0.8f));
            }

            return points;
        }

        /// <summary>Расстояние до ломаной и путь вдоль неё до ближайшей точки (along, в точках макета).</summary>
        private static float Polyline(List<(float X, float Y)> line, float x, float y, float limit, out float along)
        {
            var best = float.MaxValue;
            along = 0f;
            var walked = 0f;
            for (var i = 0; i + 1 < line.Count; i++)
            {
                var (ax, ay) = line[i];
                var (bx, by) = line[i + 1];
                var length = MathF.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay));
                if (x >= MathF.Min(ax, bx) - limit && x <= MathF.Max(ax, bx) + limit && y >= MathF.Min(ay, by) - limit && y <= MathF.Max(ay, by) + limit)
                {
                    var d = Draw.Segment(x, y, ax, ay, bx, by);
                    if (d < best)
                    {
                        best = d;
                        var t = length > 0f ? Draw.Clamp01(((x - ax) * (bx - ax) + (y - ay) * (by - ay)) / (length * length)) : 0f;
                        along = walked + t * length;
                    }
                }

                walked += length;
            }

            return best;
        }
    }
}
