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
            Sub(key, key, () => Texture(key, draw, TextureWrapMode.Clamp, border == null), null, pivot, border);

        /// <summary>
        /// Сдвиг выбора mip-уровня к более детальному: при уменьшении спрайт берёт чёткий уровень, а не размытый
        /// соседний (трилинейная фильтрация без сдвига смешивает с вдвое меньшим уровнем и «мылит» мелкие детали).
        /// </summary>
        public const float SharpBias = -0.5f;

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

        /// <summary>
        /// Растр в очереди прогрева: рисуется в фоне; готовый берётся вместо рисования в кадре.
        /// Done завершается, когда фоновый работник его нарисовал.
        /// </summary>
        private sealed class WarmItem
        {
            public WarmItem(string key, System.Func<Raster> draw)
            {
                Key = key;
                Draw = draw;
            }

            public string Key { get; }

            public System.Func<Raster> Draw { get; }

            public TaskCompletionSource<Raster> Done { get; } = new TaskCompletionSource<Raster>();
        }

        private static readonly object WarmLock = new object();

        /// <summary>Очередь прогрева по приоритету: сначала то, что на главной, потом медали и рамки.</summary>
        private static readonly LinkedList<WarmItem> WarmQueue = new LinkedList<WarmItem>();

        /// <summary>Все ещё не забранные растры прогрева — и в очереди, и уже нарисованные.</summary>
        private static readonly Dictionary<string, WarmItem> WarmItems = new Dictionary<string, WarmItem>();

        /// <summary>
        /// Сколько фоновых работников рисуют прогрев. Каждый рисует в один поток (Raster.Sequential): ядра остаются
        /// главному потоку и отрисовке, запуск и первые экраны не дёргаются.
        /// </summary>
        private const int WarmWorkers = 2;

        private static void Prefetch(string key, System.Func<Raster> draw)
        {
            lock (WarmLock)
            {
                if (!WarmItems.ContainsKey(key) && !HasTexture(key))
                {
                    var item = new WarmItem(key, draw);
                    WarmItems[key] = item;
                    WarmQueue.AddLast(item);
                }
            }
        }

        private static void WarmWorker()
        {
            Raster.Sequential = true;
            while (true)
            {
                WarmItem item;
                lock (WarmLock)
                {
                    if (WarmQueue.First == null)
                    {
                        return;
                    }

                    item = WarmQueue.First.Value;
                    WarmQueue.RemoveFirst();
                }

                try
                {
                    item.Done.SetResult(item.Draw());
                }
                catch (System.Exception e)
                {
                    item.Done.SetException(e);
                }
            }
        }

        /// <summary>
        /// Взять растр из прогрева: нарисованный или рисующийся — дождаться (это быстрее, чем начать заново);
        /// ещё не начатый — убрать из очереди и нарисовать сразу самим, а не ждать, пока до него дойдёт очередь.
        /// </summary>
        private static System.Func<Raster>? TakeWarm(string key)
        {
            lock (WarmLock)
            {
                if (!WarmItems.TryGetValue(key, out var item))
                {
                    return null;
                }

                WarmItems.Remove(key);
                if (WarmQueue.Remove(item))
                {
                    return null;
                }

                var task = item.Done.Task;
                return () => task.Status == TaskStatus.Faulted ? item.Draw() : task.Result;
            }
        }

        /// <summary>
        /// Нарисовать заранее (в фоне) то, что открывается в первые минуты, по приоритету: значки главной и вкладок,
        /// монета, подарок, звёзды, камни редкости, выбранная рамка альбома; потом кубки, медали и остальные рамки.
        /// </summary>
        public static void Warm(int selectedFrame = 0)
        {
            Prefetch("house", IconRaster.House);
            Prefetch("albumBook", IconRaster.Album);
            Prefetch("gear", IconRaster.Gear);
            Prefetch("coin2", MetaRaster.Coin);
            Prefetch("gift2", MetaRaster.Gift);
            Prefetch("sparkle", KnitRaster.Sparkle);
            Prefetch("levelStar", IconRaster.LevelStar);
            Prefetch("rewardStar", () => IconRaster.RewardStar(true));
            Prefetch("rewardStarEmpty", () => IconRaster.RewardStar(false));
            for (var tier = 0; tier < 5; tier++)
            {
                var t = tier;
                Prefetch("gem" + t, () => IconRaster.Gem(t));
            }

            var selected = selectedFrame < 0 || selectedFrame >= FrameRaster.Styles ? 0 : selectedFrame;
            Prefetch("frame" + selected, () => FrameRaster.Frame(selected));
            Prefetch("frame0", () => FrameRaster.Frame(0));
            Prefetch("basketBack", IconRaster.BasketBack);
            Prefetch("basketFront", IconRaster.BasketFront);
            Prefetch("trophyGold", () => IconRaster.Trophy(true));
            Prefetch("trophySilver", () => IconRaster.Trophy(false));
            foreach (BadgeArt art in System.Enum.GetValues(typeof(BadgeArt)))
            {
                var a = art;
                Prefetch("medal" + a + "G", () => IconRaster.Badge(a, true));
                Prefetch("medal" + a + "S", () => IconRaster.Badge(a, false));
            }

            for (var style = 0; style < FrameRaster.Styles; style++)
            {
                var st = style;
                Prefetch("frame" + st, () => FrameRaster.Frame(st));
            }

            for (var i = 0; i < WarmWorkers; i++)
            {
                Task.Run(WarmWorker);
            }
        }

        private static Texture2D Texture(string key, System.Func<Raster> draw, TextureWrapMode wrap, bool mips = true)
        {
            if (Textures.TryGetValue(key, out var texture) && texture != null)
            {
                return texture;
            }

            // Прогрев: нарисованный — взять, рисующийся — дождаться, не начатый — нарисовать сейчас самим.
            var warm = TakeWarm(key);
            if (warm != null)
            {
                draw = warm;
            }

            // Плашки «девять частей» (с border) рисуются один к одному и mip-уровни им не нужны: уменьшенные копии
            // смешивают угол с серединой и размывают кромку. Остальным — mip-уровни со сдвигом к чёткому.
            var raster = draw();
            texture = new Texture2D(raster.Width, raster.Height, TextureFormat.RGBA32, mips)
            {
                wrapMode = wrap,
                filterMode = mips ? FilterMode.Trilinear : FilterMode.Bilinear,
                mipMapBias = mips ? SharpBias : 0f,
            };
            texture.SetPixelData(raster.ToBytes(), 0);
            texture.Apply(mips, true);
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
                mipMapBias = SharpBias,
            };
            texture.SetPixelData(raster.ToBytes(), 0);
            texture.Apply(true, true);
            return texture;
        }

        public static Sprite Coin => Get("coin2", MetaRaster.Coin);

        /// <summary>Рамка альбома со своим узором и формой (номер рамки = стиль), квадрат; проём — FrameRaster.Opening.</summary>
        public static Sprite Frame(int style) => Get("frame" + style, () => FrameRaster.Frame(style));

        /// <summary>Частица эффекта живой рамки (FrameRaster.Mote): снежинка, лепесток, лист, искра, сердечко, бабочка, лапка, пузырёк.</summary>
        public static Sprite FrameMote(int kind) => Get("frameMote" + kind, () => FrameRaster.Mote(kind));

        /// <summary>Завеса северного сияния поверх рамки (белая, красится): два рисунка лучей.</summary>
        public static Sprite Aurora(int variant) => Get("aurora" + variant, () => FrameRaster.Aurora(variant));

        /// <summary>Бирка на ниточке для числа (петли клубка, цена и запас бустера). Белая — красится; 72×40, тело — правые 58.</summary>
        public static Sprite Tag => Get("tag", KnitRaster.Tag);

        /// <summary>Кубок достижения: золотой (можно забрать, получено) или серебряный (в процессе).</summary>
        public static Sprite Trophy(bool gold) => Get(gold ? "trophyGold" : "trophySilver", () => IconRaster.Trophy(gold));

        /// <summary>Медаль достижения со своим рисунком: золотая (можно забрать, получено) или серебряная.</summary>
        public static Sprite Medal(BadgeArt art, bool gold) => Get("medal" + art + (gold ? "G" : "S"), () => IconRaster.Badge(art, gold));

        /// <summary>Рисунок медали для достижения: у каждого свой, по смыслу.</summary>
        public static BadgeArt BadgeOf(KnittedPicture.Core.AchievementInfo a)
        {
            switch (a.Id)
            {
                case 0: return BadgeArt.Stitch;
                case 1: return BadgeArt.Picture;
                case 5: return BadgeArt.Stars;
                case 8: return BadgeArt.Balls;
                case 9: return BadgeArt.Album;
                case 10: return BadgeArt.Calendar;
                case 11: return BadgeArt.Crown;
                case 12: return BadgeArt.OpenBook;
                case 13: return BadgeArt.StarBook;
                case 14: return BadgeArt.Books;
                case 15: return BadgeArt.Basket;
                default: return BadgeArt.Stars;
            }
        }

        /// <summary>Камень редкости: дымчатый кварц, изумруд, сапфир, аметист, топаз.</summary>
        public static Sprite Gem(Rarity rarity)
        {
            var tier = (int)rarity;
            return Get("gem" + tier, () => IconRaster.Gem(tier));
        }

        /// <summary>Объёмные значки нижней панели и настроек.</summary>
        public static Sprite House => Get("house", IconRaster.House);

        public static Sprite AlbumBook => Get("albumBook", IconRaster.Album);

        public static Sprite Gear => Get("gear", IconRaster.Gear);

        /// <summary>Звезда награды: золотая выпуклая или пустое гнездо.</summary>
        public static Sprite RewardStar(bool earned) => Get(earned ? "rewardStar" : "rewardStarEmpty", () => IconRaster.RewardStar(earned));

        /// <summary>Звезда-розетка уровня мотка (цифра — подписью поверх).</summary>
        public static Sprite LevelStar => Get("levelStar", IconRaster.LevelStar);

        /// <summary>Корзинка: задняя часть (ручка, нутро) и передняя стенка; клубки кладутся между ними.</summary>
        public static Sprite BasketBack => Get("basketBack", IconRaster.BasketBack);

        public static Sprite BasketFront => Get("basketFront", IconRaster.BasketFront);

        /// <summary>Меланжевый клубок в кадре YarnBall: два цвета вперемешку прожилками.</summary>
        public static Sprite YarnBallMelange(Color main, Color second)
        {
            var key = "melange" + Key(main) + Key(second);
            var tight = Tight;
            return Get(key, () => IconRaster.Melange(ToRgba(main), ToRgba(second), (int)tight.x, (int)tight.y, (int)tight.width));
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
        /// <summary>Непрерывная канва для спокойного тканевого фона партии.</summary>
        public static Texture2D Linen => Tile("linen", KnitRaster.CanvasTile);

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
            var color = deep ? Color.Lerp(c, new Color(0.9f, 0.91f, 0.92f), 0.5f) : c;
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
                mipMapBias = SharpBias,
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
