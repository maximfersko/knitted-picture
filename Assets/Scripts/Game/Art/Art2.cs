#nullable enable

using System.Collections.Generic;
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

        public static Sprite Get(string key, System.Func<Raster> draw, Vector2? pivot = null, Vector4? border = null)
        {
            if (Cache.TryGetValue(key, out var sprite) && sprite != null)
            {
                return sprite;
            }

            var texture = Texture(key, draw, TextureWrapMode.Clamp);
            sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), pivot ?? new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, border ?? Vector4.zero);
            Cache[key] = sprite;
            return sprite;
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
            texture.Apply(true);
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
            texture.Apply(true);
            return texture;
        }

        public static Sprite Coin => Get("coin2", MetaRaster.Coin);

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
            var kind = thick ? BallRaster.Kind.Thick : BallRaster.Kind.Plain;
            return Get("ball" + Key(color) + kind, () => BallRaster.Ball(ToRgba(color), kind));
        }

        /// <summary>
        /// Детальный клубок в пропорциях старого спрайта (шар почти во весь квадрат, завиток внутри):
        /// подменяет окрашиваемый Art.YarnBall там, где клубок — значок или украшение.
        /// </summary>
        public static Sprite YarnBall(Color c) => Get("ballTight" + Key(c), () => Tight(BallRaster.Ball(ToRgba(c))));

        public static Sprite YarnBallRainbow => Get("ballTightRainbow", () => Tight(BallRaster.Ball(Rgba.White, BallRaster.Kind.Rainbow)));

        private static Raster Tight(Raster ball)
        {
            const int half = 74;
            return ball.Crop((int)BallRaster.Cx - half, (int)BallRaster.Cy - half, half * 2, half * 2);
        }

        public static Sprite RainbowBall => Get("ballRainbow", () => BallRaster.Ball(Rgba.White, BallRaster.Kind.Rainbow));

        /// <summary>Чужая пряжа цвета ткани: клубок, намотанный нитями канвы.</summary>
        public static Sprite CanvasBall(Color c) => Get("ballCanvas" + Key(c), () => BallRaster.Ball(ToRgba(c), BallRaster.Kind.Canvas));

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
