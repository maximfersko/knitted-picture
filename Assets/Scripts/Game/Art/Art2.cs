#nullable enable

using System.Collections.Generic;
using System.Threading.Tasks;
using KnittedPicture.Core;
using UnityEngine;

namespace KnittedPicture.Game
{
    /// <summary>
    /// Растры 2.0 → спрайты Unity. Кэш по ключу: петля каждого цвета рисуется один раз за запуск.
    /// Текстуры с mip-уровнями: мелкое (петли, клубки) не рябит при уменьшении.
    /// </summary>
    public static class Art2
    {
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
        private static readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>();

        /// <summary>Текстуры клубков по порядку последнего обращения (в конце — свежие): их выгружает Trim.</summary>
        private static readonly List<string> Balls = new List<string>();

        /// <summary>Сколько текстур клубков держать сверх тех, что сейчас на экране (каждая 640 px, ~2 МБ).</summary>
        private const int KeepBalls = 12;

        public static Sprite Get(string key, System.Func<Raster> draw, Vector2? pivot = null, Vector4? border = null) =>
            Sub(key, key, () => Texture(key, draw, TextureWrapMode.Clamp), null, pivot, border);

        /// <summary>
        /// Спрайт из части текстуры textureKey (rect — в пикселях; null — вся). Несколько спрайтов делят одну
        /// текстуру: клубок поля и клубок-значок — одна картинка в памяти.
        /// </summary>
        private static Sprite Sub(string key, string textureKey, System.Func<Texture2D> make, Rect? rect, Vector2? pivot = null,
            Vector4? border = null)
        {
            if (textureKey.StartsWith("ball", System.StringComparison.Ordinal))
            {
                Balls.Remove(textureKey);
                Balls.Add(textureKey);
            }

            if (Cache.TryGetValue(key, out var sprite) && sprite != null)
            {
                return sprite;
            }

            var texture = make();
            sprite = Sprite.Create(texture, rect ?? new Rect(0, 0, texture.width, texture.height), pivot ?? new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, border ?? Vector4.zero);
            Cache[key] = sprite;
            return sprite;
        }

        /// <summary>
        /// Выгрузить текстуры клубков, которых нет ни на одной картинке сцены, кроме KeepBalls последних:
        /// цветов в игре больше сотни, и без этого память росла бы от уровня к уровню.
        /// </summary>
        public static void Trim()
        {
            if (Balls.Count <= KeepBalls)
            {
                return;
            }

            var used = new HashSet<Texture>();
            foreach (var image in Object.FindObjectsByType<UnityEngine.UI.Image>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (image.sprite != null)
                {
                    used.Add(image.sprite.texture);
                }
            }

            for (var i = Balls.Count - KeepBalls - 1; i >= 0; i--)
            {
                var key = Balls[i];
                if (!Textures.TryGetValue(key, out var texture) || texture == null)
                {
                    Balls.RemoveAt(i);
                    continue;
                }

                if (used.Contains(texture))
                {
                    continue;
                }

                var stale = new List<string>();
                foreach (var pair in Cache)
                {
                    if (pair.Value == null || pair.Value.texture == texture)
                    {
                        stale.Add(pair.Key);
                    }
                }

                foreach (var name in stale)
                {
                    if (Cache[name] != null)
                    {
                        Object.Destroy(Cache[name]);
                    }

                    Cache.Remove(name);
                }

                Object.Destroy(texture);
                Textures.Remove(key);
                Balls.RemoveAt(i);
            }
        }

        /// <summary>Повторяемая текстура (бумага, верёвка) для RawImage и мешей.</summary>
        public static Texture2D Tile(string key, System.Func<Raster> draw) => Texture(key, draw, TextureWrapMode.Repeat);

        private static Texture2D Texture(string key, System.Func<Raster> draw, TextureWrapMode wrap)
        {
            if (Textures.TryGetValue(key, out var texture) && texture != null)
            {
                return texture;
            }

            var raster = draw();
            texture = new Texture2D(raster.Width, raster.Height, TextureFormat.RGBA32, true)
            {
                wrapMode = wrap,
                filterMode = FilterMode.Trilinear,
            };
            texture.SetPixelData(raster.ToBytes(), 0);
            texture.Apply(true, true);
            Textures[key] = texture;
            return texture;
        }

        /// <summary>Растр в текстуру без кэша (картинки уровней кэширует MiniPicture).</summary>
        public static Texture2D Texture(Raster raster)
        {
            var texture = new Texture2D(raster.Width, raster.Height, TextureFormat.RGBA32, true)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
            };
            texture.SetPixelData(raster.ToBytes(), 0);
            texture.Apply(true, true);
            return texture;
        }

        public static Sprite Coin => Get("coin2", MetaRaster.Coin);

        /// <summary>Бирка на ниточке для числа (петли клубка, цена и запас бустера). Белая — красится; 72×40, тело — правые 58.</summary>
        public static Sprite Tag => Get("tag", KnitRaster.Tag);

        /// <summary>Значок цвета номер color (по кругу, если цветов больше, чем фигур).</summary>
        public static Sprite ColorMark(int color)
        {
            var kind = ((color % KnitRaster.ColorMarkCount) + KnitRaster.ColorMarkCount) % KnitRaster.ColorMarkCount;
            return Get("colorMark" + kind, () => KnitRaster.ColorMark(kind));
        }

        /// <summary>Лунка под значок бустера.</summary>
        public static Sprite IconWell => Get("iconWell", KnitRaster.IconWell);

        /// <summary>Доля ширины бирки слева, занятая хвостиком и дырочкой: текст — правее.</summary>
        public const float TagTextFrom = 0.27f;

        public static Sprite Sparkle => Get("sparkle", KnitRaster.Sparkle);

        public static Sprite Gift => Get("gift2", MetaRaster.Gift);

        public static Sprite Hoop => Get("hoop2", MetaRaster.Hoop);

        public static string Key(Color c) => ColorUtility.ToHtmlStringRGBA(c);

        public static Rgba ToRgba(Color c) => new Rgba(c.r, c.g, c.b, c.a);

        // ---------- Полотно ----------

        /// <summary>Лицевая петля цвета пряжи. Опора — нижний кончик «галочки».</summary>
        public static Sprite Stitch(Color c) =>
            Get("stitch" + Key(c), () => KnitRaster.Stitch(ToRgba(c)), new Vector2(0.5f, KnitRaster.StitchTip));

        public static Sprite Loop(Color c) => Get("loop" + Key(c), () => KnitRaster.Loop(ToRgba(c)));

        /// <summary>Спица: 9-slice, левый край — заострённый кончик, правый — под набалдашник.</summary>
        public static Sprite Needle => Get("needle", KnitRaster.Needle, null, new Vector4(30, 0, 30, 0));

        public static Sprite Knob => Get("knob", KnitRaster.Knob);

        public static Sprite WoodFrame => Get("woodFrame", KnitRaster.WoodFrame, null, new Vector4(96, 96, 96, 96));

        public static Texture2D Paper => Tile("paper", KnitRaster.PaperTile);

        public static Texture2D Rope => Tile("rope", KnitRaster.RopeTile);

        /// <summary>Пунктирная рамка-строчка под размер в точках (рисуется в 3× плотности).</summary>
        public static Sprite DashedFrame(float size, float radius)
        {
            var px = Mathf.Max(16, Mathf.RoundToInt(size * Art.Density));
            var r = radius * Art.Density;
            return Get("dashed" + px + "_" + Mathf.RoundToInt(r), () => KnitRaster.DashedFrame(px, 1.6f * Art.Density, r, 3.2f * Art.Density, 2.4f * Art.Density));
        }

        /// <summary>
        /// Строчка по прямоугольнику w×h точек (кнопки, выноски, карточки): пунктир под точный размер.
        /// Размер округляется до 2 точек, чтобы кэш не разрастался.
        /// </summary>
        public static Sprite Stitched(float w, float h, float radius)
        {
            var pw = Mathf.Max(16, Mathf.RoundToInt(w / 2f) * 2 * (int)Art.Density);
            var ph = Mathf.Max(16, Mathf.RoundToInt(h / 2f) * 2 * (int)Art.Density);
            var r = radius * Art.Density;
            return Get("stitched" + pw + "x" + ph + "_" + Mathf.RoundToInt(r),
                () => KnitRaster.DashedFrame(pw, ph, 1.5f * Art.Density, r, 4.5f * Art.Density, 3f * Art.Density));
        }

        /// <summary>Пунктирное кольцо (подсветка причины затора) диаметром size точек.</summary>
        public static Sprite DashedRing(float size)
        {
            var px = Mathf.Max(16, Mathf.RoundToInt(size / 2f) * 2 * (int)Art.Density);
            return Get("dashedRing" + px, () => KnitRaster.DashedFrame(px, 3f * Art.Density, px / 2f, 6f * Art.Density, 4f * Art.Density));
        }

        // ---------- Клубки ----------

        /// <summary>Фон под клубками на поле: задние ряды смешиваются с ним в пастель.</summary>
        public static readonly Color Ground = new Color(0.969f, 0.945f, 0.918f);

        /// <summary>Клубок цвета пряжи. deep — задний ряд поля: пастельный, сливается с фоном.</summary>
        public static Sprite Ball(Color c, bool deep = false, bool thick = false)
        {
            var color = deep ? Color.Lerp(c, new Color(0.91f, 0.89f, 0.92f), 0.5f) : c;
            return BallSprite(color, thick ? BallRaster.Kind.Thick : BallRaster.Kind.Plain, false);
        }

        /// <summary>
        /// Детальный клубок в пропорциях старого спрайта (шар почти во весь квадрат, завиток внутри):
        /// подменяет окрашиваемый Art.YarnBall там, где клубок — значок или украшение.
        /// </summary>
        public static Sprite YarnBall(Color c) => BallSprite(c, BallRaster.Kind.Plain, true);

        public static Sprite YarnBallRainbow => BallSprite(Color.white, BallRaster.Kind.Rainbow, true);

        private static string BallKey(Color c, BallRaster.Kind kind) => "ball" + Key(c) + kind;

        private static Sprite BallSprite(Color c, BallRaster.Kind kind, bool tight)
        {
            var textureKey = BallKey(c, kind);
            return Sub((tight ? "tight" : string.Empty) + textureKey, textureKey, () => BallTexture(textureKey, ToRgba(c), kind),
                tight ? Tight : (Rect?)null);
        }

        /// <summary>Квадрат вокруг шара (вместе с завитком) внутри текстуры клубка.</summary>
        private static Rect Tight
        {
            get
            {
                const int q = BallRaster.Quality;
                const int half = 74 * q;
                return new Rect(BallRaster.Cx * q - half, BallRaster.Cy * q - half, half * 2, half * 2);
            }
        }

        public static Sprite RainbowBall => BallSprite(Color.white, BallRaster.Kind.Rainbow, false);

        /// <summary>Чужая пряжа цвета ткани: клубок, намотанный нитями канвы.</summary>
        public static Sprite CanvasBall(Color c) => BallSprite(c, BallRaster.Kind.Canvas, false);

        // ---------- Клубки заранее, в фоне ----------

        /// <summary>Клубок, который сейчас красится в фоновом потоке.</summary>
        private sealed class Job
        {
            public Job(string key, byte[] buffer, Task task)
            {
                Key = key;
                Buffer = buffer;
                Task = task;
            }

            public string Key { get; }

            public byte[] Buffer { get; }

            public Task Task { get; }
        }

        private static readonly Queue<(string Key, Rgba Color, BallRaster.Kind Kind)> Waiting = new Queue<(string, Rgba, BallRaster.Kind)>();
        private static Job? _job;

        /// <summary>Буферы байтов клубка (1,6 МБ): один у главного потока, один у фонового — переиспользуются, куча не растёт.</summary>
        private static byte[]? _mainBuffer;

        private static byte[]? _spareBuffer;

        /// <summary>
        /// Подготовить клубки уровня заранее: пока игрок на главной или на экране победы, фоновый поток красит
        /// их по одному, а Pump кладёт готовые в текстуры по одной за кадр. Уровень потом открывается без паузы.
        /// </summary>
        public static void Prefetch(LevelData level)
        {
            Enqueue(Palette.Hidden, BallRaster.Kind.Plain);
            foreach (var hex in level.colors)
            {
                Enqueue(Palette.Vivid(Palette.Hex(hex)), BallRaster.Kind.Plain);
            }
        }

        private static void Enqueue(Color c, BallRaster.Kind kind)
        {
            var key = BallKey(c, kind);
            if (HasTexture(key) || (_job != null && _job.Key == key))
            {
                return;
            }

            foreach (var waiting in Waiting)
            {
                if (waiting.Key == key)
                {
                    return;
                }
            }

            Waiting.Enqueue((key, ToRgba(c), kind));
        }

        private static bool HasTexture(string key) => Textures.TryGetValue(key, out var texture) && texture != null;

        /// <summary>Раз в кадр (App.Update): готовый клубок — в текстуру, следующий — в работу.</summary>
        public static void Pump()
        {
            if (_job != null)
            {
                if (!_job.Task.IsCompleted)
                {
                    return;
                }

                Finish(_job);
                return;
            }

            while (Waiting.Count > 0)
            {
                var (key, color, kind) = Waiting.Dequeue();
                if (HasTexture(key))
                {
                    continue;
                }

                var buffer = _spareBuffer ?? new byte[BallRaster.Pixels * BallRaster.Pixels * 4];
                _spareBuffer = null;
                _job = new Job(key, buffer, Task.Run(() => BallRaster.BallBytes(color, kind, buffer)));
                return;
            }
        }

        private static void Finish(Job job)
        {
            _job = null;
            _spareBuffer = job.Buffer;
            if (job.Task.Status == TaskStatus.RanToCompletion && !HasTexture(job.Key))
            {
                Textures[job.Key] = Upload(job.Buffer);
                Balls.Remove(job.Key);
                Balls.Add(job.Key);
            }
        }

        /// <summary>Текстура клубка: готовая, из фоновой работы (дождаться её) или покрасить сейчас.</summary>
        private static Texture2D BallTexture(string key, Rgba color, BallRaster.Kind kind)
        {
            if (Textures.TryGetValue(key, out var texture) && texture != null)
            {
                return texture;
            }

            if (_job != null && _job.Key == key)
            {
                _job.Task.Wait();
                Finish(_job);
                if (Textures.TryGetValue(key, out texture) && texture != null)
                {
                    return texture;
                }
            }

            _mainBuffer = BallRaster.BallBytes(color, kind, _mainBuffer);
            texture = Upload(_mainBuffer);
            Textures[key] = texture;
            return texture;
        }

        private static Texture2D Upload(byte[] bytes)
        {
            var texture = new Texture2D(BallRaster.Pixels, BallRaster.Pixels, TextureFormat.RGBA32, true)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
            };
            texture.SetPixelData(bytes, 0);
            texture.Apply(true, true);
            return texture;
        }

        public static Sprite HiddenBall => Get("ballHidden", BallRaster.Hidden);

        public static Sprite Ribbon => Get("ribbon", BallRaster.Ribbon);

        public static Sprite Padlock => Get("padlock", BallRaster.Padlock);

        public static Sprite KeyCharm => Get("keyCharm", BallRaster.Key);

        public static Sprite Knots(int count) => Get("knots" + Mathf.Clamp(count, 1, 3), () => BallRaster.Knots(Mathf.Clamp(count, 1, 3)));

        public static Sprite Ice(int stage) => Get("ice" + Mathf.Clamp(stage, 0, 3), () => BallRaster.Ice(Mathf.Clamp(stage, 0, 3)));

        public static Sprite Badge => Get("badge", BallRaster.Badge);

        public static Sprite BallShadow => Get("ballShadow", BallRaster.Shadow);

        /// <summary>Сколько места в спрайте клубка занимает сам шар (диаметр к стороне).</summary>
        public const float BallFill = BallRaster.Radius * 2f / BallRaster.Size;

        // ---------- Станок и бустеры ----------

        public static Sprite LoomBar => Get("loomBar", LoomRaster.Bar, null, new Vector4(96, 96, 96, 96));

        public static Sprite Well => Get("well", LoomRaster.Well);

        public static Sprite WellGlow => Get("wellGlow", LoomRaster.Glow);

        public static Sprite LoomNeedle => Get("loomNeedle", LoomRaster.Needle);

        public static Sprite Booster(BoosterType type) => Get("booster" + type, () => LoomRaster.Booster(type switch
        {
            BoosterType.Scissors => LoomRaster.BoosterArt.Scissors,
            BoosterType.Undo => LoomRaster.BoosterArt.Undo,
            BoosterType.Slot => LoomRaster.BoosterArt.Slot,
            _ => LoomRaster.BoosterArt.Time,
        }));
    }
}
