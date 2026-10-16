#nullable enable

using System;
using System.Collections.Generic;
using KnittedPicture.Core;
using UnityEngine;
using UnityEngine.UI;

namespace KnittedPicture.Game
{
    /// <summary>
    /// Живые рамки альбома: эпические и легендарные двигаются. По практике коллекционных игр ценность видна
    /// с первого взгляда: эпическая — один сильный тематический эффект, легендарная — подробный рисунок, мягкое
    /// свечение вокруг, бегущий по багету свет и искры. Эффекты лежат на багете и вокруг, картинку не закрывают.
    /// Вся рамка двигается одним циклом (один твин на рамку, а не на каждую частицу); в сетке альбома — облегчённо
    /// (вдвое меньше частиц, без свечения вокруг). «Меньше движения» замораживает всё в спокойной позе.
    /// </summary>
    public static class FrameFx
    {
        /// <summary>Есть ли у рамки живой эффект.</summary>
        public static bool Animated(int frameId) => AlbumFrames.Get(frameId).Rarity >= Rarity.Epic;

        /// <summary>
        /// Добавить эффекты рамки frameId к плитке root (size × size, рамка на всю плитку). Вызывать после того,
        /// как положены рамка и картинка: эффекты ложатся сверху, свечение — под рамку.
        /// </summary>
        public static void Attach(RectTransform root, int frameId, float size, bool lite)
        {
            if (!Animated(frameId))
            {
                return;
            }

            var fx = new Builder(root, size, lite);
            switch (frameId)
            {
                case 9:
                    // Золотой багет: блик бежит по золоту, рубины на розетках вспыхивают.
                    fx.Sweep(1, Palette.Hex("#FFF4C2"), 5.5f, 5);
                    fx.Twinkle(fx.CornerPoints(), Palette.Hex("#FFE7F0"), 2.6f);
                    break;
                case 10:
                    // Снежинки: снег идёт вдоль боковых планок.
                    fx.Fall(Art2.FrameMote(0), new[] { Color.white, Palette.Hex("#E3F4FF") }, 8, 1f, 0.08f, 60f);
                    break;
                case 11:
                    // Первоцветы: лепестки слетают и кружатся.
                    fx.Fall(Art2.FrameMote(1), new[] { Color.white }, 6, 1f, 0.07f, 140f);
                    break;
                case 12:
                    // Подсолнухи: тёплая пыльца плавает над багетом, подсолнухи на углах светятся.
                    fx.CornerGlow(Palette.Hex("#FFD54A"), 0.55f, 2.4f);
                    fx.Wander(8, Palette.Hex("#FFE07A"), false, 0.5f);
                    break;
                case 13:
                    // Листопад: кленовые листья трёх осенних цветов, кружатся.
                    fx.Fall(Art2.FrameMote(2), new[] { Palette.Hex("#E8742A"), Palette.Hex("#D9452B"), Palette.Hex("#F2B632") }, 6, 1f, 0.085f, 160f);
                    break;
                case 27:
                    // Светлячки: огоньки летают над лозой и мерцают, банки-фонарики дышат светом.
                    fx.CornerGlow(Palette.Hex("#F4FF9A"), 0.5f, 3f);
                    fx.Wander(9, Palette.Hex("#E9FF8A"), true, 0.75f);
                    break;
                case 28:
                    // Северное сияние: завеса переливается зелёным и фиолетовым, кристаллы искрятся.
                    fx.Aurora();
                    fx.Twinkle(fx.CornerPoints(), Palette.Hex("#E9FFFF"), 2.2f);
                    break;
                case 29:
                    // Камин: снизу дышит огонь, вверх летят угольки, на полке горят свечи.
                    fx.Hearth();
                    break;
                case 30:
                    // Жар-птица: золотое свечение вокруг, два огонька бегут по хохломе, перья горят, вверх искры.
                    fx.Halo(Palette.Hex("#FF9A2E"), false);
                    fx.CornerGlow(Palette.Hex("#FF5A2A"), 0.6f, 2.2f);
                    fx.Sweep(2, Palette.Hex("#FFE38A"), 4.2f, 7);
                    fx.Rise(Art2.FrameMote(3), Palette.Hex("#FFD27A"), 6);
                    break;
                default:
                    // Волшебный клубок: радужное свечение, по шнуру бежит живая нить света, клубки искрятся.
                    fx.Halo(Color.white, true);
                    fx.Comet();
                    fx.Twinkle(fx.CornerPoints(), Color.white, 3f);
                    break;
            }

            fx.Start();
        }

        /// <summary>Сборщик эффектов одной рамки: элементы добавляют шаги в общий цикл.</summary>
        private sealed class Builder
        {
            private const float Period = 240f;

            private readonly RectTransform _root;
            private readonly RectTransform _layer;
            private readonly float _size;
            private readonly bool _lite;
            private readonly List<Action<float>> _steps = new List<Action<float>>();

            public Builder(RectTransform root, float size, bool lite)
            {
                _root = root;
                _size = size;
                _lite = lite;
                _layer = Ui.Place(Ui.Node(root, "FrameFx"), 0, 0, size, size);
            }

            private float C => _size / 2f;

            /// <summary>Середина багета от центра (в точках плитки).</summary>
            private float Mid => _size * 30.25f / FrameRaster.Size;

            private float Band => _size * 7.5f / FrameRaster.Size;

            private float Outer => _size * 33f / FrameRaster.Size;

            private int Count(int n) => _lite ? Mathf.Max(2, n / 2) : n;

            public void Start()
            {
                var steps = _steps.ToArray();
                Tweens.Run(_layer, Period, t =>
                {
                    var time = t * Period;
                    foreach (var step in steps)
                    {
                        step(time);
                    }
                }, null, null, 0f, true);
            }

            /// <summary>Точка на квадрате с полустороной half по часовой от верхнего левого угла; p — доля пути.</summary>
            private Vector2 Path(float p, float half)
            {
                p -= Mathf.Floor(p);
                var side = (int)(p * 4f);
                var f = p * 4f - side;
                return side switch
                {
                    0 => new Vector2(C - half + 2f * half * f, C - half),
                    1 => new Vector2(C + half, C - half + 2f * half * f),
                    2 => new Vector2(C + half - 2f * half * f, C + half),
                    _ => new Vector2(C - half, C + half - 2f * half * f),
                };
            }

            public Vector2[] CornerPoints() => new[]
            {
                new Vector2(C - Mid, C - Mid), new Vector2(C + Mid, C - Mid), new Vector2(C + Mid, C + Mid), new Vector2(C - Mid, C + Mid),
            };

            private Image Add(Sprite sprite, Color color, float size, RectTransform? parent = null)
            {
                var image = Ui.Image(parent ?? _layer, "Fx", sprite, color);
                Ui.PlaceCenter(image.rectTransform, C, C, size, size);
                return image;
            }

            private static void Move(Image image, Vector2 at) => image.rectTransform.anchoredPosition = new Vector2(at.x, -at.y);

            private static float Hash(int i, int salt) => Draw.Hash(i * 7 + salt, salt * 13 + 5);

            /// <summary>Блик: head светлых пятен бегут по багету, за каждым — гаснущий хвост.</summary>
            public void Sweep(int heads, Color color, float period, int tail)
            {
                tail = _lite ? Mathf.Min(tail, 3) : tail;
                var parts = new List<(Image Image, int Head, int K)>();
                for (var h = 0; h < heads; h++)
                {
                    for (var k = tail; k >= 0; k--)
                    {
                        var s = Band * (k == 0 ? 2.4f : 1.9f - 1.2f * k / tail);
                        var image = Add(Art.Glow, Palette.WithAlpha(color, k == 0 ? 0.95f : 0.75f * (1f - (float)k / (tail + 1))), s);
                        parts.Add((image, h, k));
                    }

                    var spark = Add(Art2.Sparkle, Color.white, Band * 1.5f);
                    parts.Add((spark, h, -1));
                }

                _steps.Add(time =>
                {
                    foreach (var (image, head, k) in parts)
                    {
                        var p = time / period + (float)head / heads - Mathf.Max(0, k) * 0.011f;
                        Move(image, Path(p, Mid));
                        if (k < 0)
                        {
                            image.rectTransform.localRotation = Quaternion.Euler(0f, 0f, time * 90f);
                        }
                    }
                });
            }

            /// <summary>Искорки в точках: по очереди вспыхивают и гаснут.</summary>
            public void Twinkle(Vector2[] points, Color color, float period)
            {
                var images = new Image[points.Length];
                for (var i = 0; i < points.Length; i++)
                {
                    images[i] = Add(Art2.Sparkle, color, Band * 1.7f);
                    Move(images[i], points[i] + new Vector2(-Band * 0.25f, -Band * 0.25f));
                }

                _steps.Add(time =>
                {
                    for (var i = 0; i < images.Length; i++)
                    {
                        var t = (time / period + i * 0.37f) % 1f;
                        var flash = t < 0.3f ? Ease.Bump(t / 0.3f) : 0f;
                        images[i].rectTransform.localScale = Vector3.one * (0.15f + 0.95f * flash);
                        images[i].color = Palette.WithAlpha(color, flash);
                    }
                });
            }

            /// <summary>Тёплый свет на углах (банки, подсолнухи, перья): медленно дышит, каждый угол в своём ритме.</summary>
            public void CornerGlow(Color color, float alpha, float period)
            {
                var points = CornerPoints();
                var images = new Image[points.Length];
                for (var i = 0; i < points.Length; i++)
                {
                    images[i] = Add(Art.Glow, Palette.WithAlpha(color, alpha), Band * 3.2f);
                    Move(images[i], points[i]);
                }

                _steps.Add(time =>
                {
                    for (var i = 0; i < images.Length; i++)
                    {
                        var b = 0.5f + 0.5f * Mathf.Sin((time / period + i * 0.29f) * Mathf.PI * 2f);
                        images[i].color = Palette.WithAlpha(color, alpha * (0.45f + 0.55f * b));
                        images[i].rectTransform.localScale = Vector3.one * (0.85f + 0.2f * b);
                    }
                });
            }

            /// <summary>Огоньки над багетом: каждый кружит возле своей точки и медленно плывёт по кругу; blink — мерцают.</summary>
            public void Wander(int count, Color color, bool blink, float alpha)
            {
                count = Count(count);
                var glows = new Image[count];
                var cores = new Image[count];
                for (var i = 0; i < count; i++)
                {
                    glows[i] = Add(Art.Glow, color, Band * 1.6f);
                    cores[i] = Add(Art.Circle, Color.white, Band * 0.32f);
                }

                _steps.Add(time =>
                {
                    for (var i = 0; i < count; i++)
                    {
                        var ph = Hash(i, 3) * 6.28f;
                        var p = (i + 0.5f) / count + Hash(i, 1) * 0.08f + time * 0.006f;
                        var at = Path(p, Mid) + new Vector2(Mathf.Sin(time * (0.7f + Hash(i, 2)) + ph), Mathf.Cos(time * (0.9f + Hash(i, 4)) + ph * 1.3f)) * Band * 0.45f;
                        var light = blink ? Mathf.Pow(Mathf.Max(0f, Mathf.Sin(time * (1.1f + Hash(i, 5)) + ph)), 2f) : 0.6f + 0.4f * Mathf.Sin(time * 1.3f + ph);
                        light = 0.12f + 0.88f * light;
                        Move(glows[i], at);
                        Move(cores[i], at);
                        glows[i].color = Palette.WithAlpha(color, alpha * light);
                        cores[i].color = Palette.WithAlpha(Color.Lerp(color, Color.white, 0.6f), light);
                        glows[i].rectTransform.localScale = Vector3.one * (0.7f + 0.4f * light);
                    }
                });
            }

            /// <summary>Частицы падают вдоль боковых планок (снег, лепестки, листья): с покачиванием и вращением.</summary>
            public void Fall(Sprite sprite, Color[] colors, int count, float speed, float scale, float spin)
            {
                count = Count(count);
                var images = new Image[count];
                for (var i = 0; i < count; i++)
                {
                    images[i] = Add(sprite, colors[i % colors.Length], _size * scale * (0.8f + 0.5f * Hash(i, 6)));
                }

                _steps.Add(time =>
                {
                    for (var i = 0; i < count; i++)
                    {
                        var side = i % 2 == 0 ? -1f : 1f;
                        var ph = Hash(i, 7);
                        var x = C + side * (Mid + (Hash(i, 8) - 0.35f) * Band * 1.4f);
                        var t = (time * speed * (0.09f + 0.04f * Hash(i, 9)) + ph) % 1f;
                        var y = -_size * 0.06f + t * _size * 1.08f;
                        x += Mathf.Sin(time * 1.4f + ph * 6.28f) * Band * 0.55f;
                        Move(images[i], new Vector2(x, y));
                        var fade = Mathf.Clamp01(t / 0.12f) * Mathf.Clamp01((1f - t) / 0.18f);
                        var c = images[i].color;
                        c.a = fade;
                        images[i].color = c;
                        images[i].rectTransform.localRotation = Quaternion.Euler(0f, 0f, (time * spin * (0.6f + Hash(i, 10))) % 360f);
                    }
                });
            }

            /// <summary>Искры поднимаются от верхних углов и гаснут.</summary>
            public void Rise(Sprite sprite, Color color, int count)
            {
                count = Count(count);
                var images = new Image[count];
                for (var i = 0; i < count; i++)
                {
                    images[i] = Add(sprite, color, Band * (1.1f + 0.6f * Hash(i, 11)));
                }

                _steps.Add(time =>
                {
                    for (var i = 0; i < count; i++)
                    {
                        var ph = Hash(i, 12);
                        var t = (time * (0.35f + 0.2f * Hash(i, 13)) + ph) % 1f;
                        var corner = i % 2 == 0 ? C - Mid : C + Mid;
                        var x = corner + (Hash(i, 14) - 0.5f) * Band * 2f + Mathf.Sin(time * 2f + ph * 6.28f) * Band * 0.4f;
                        var y = C - Mid - t * _size * 0.3f;
                        Move(images[i], new Vector2(x, y));
                        images[i].color = Palette.WithAlpha(color, Mathf.Clamp01(t / 0.1f) * (1f - t));
                        images[i].rectTransform.localScale = Vector3.one * (1f - 0.6f * t);
                    }
                });
            }

            /// <summary>
            /// Мягкое свечение вокруг рамки (под ней), ворсистым краем: два слоя крутятся навстречу и дышат.
            /// rainbow — цвет медленно идёт по радуге. В сетке альбома свечения нет.
            /// </summary>
            public void Halo(Color color, bool rainbow)
            {
                if (_lite)
                {
                    return;
                }

                var holder = Ui.PlaceCenter(Ui.Node(_root, "Halo"), C, C, _size * 1.45f, _size * 1.45f);
                holder.SetAsFirstSibling();
                var layers = new Image[2];
                for (var i = 0; i < 2; i++)
                {
                    layers[i] = Ui.Image(holder, "Fluff", Art.Fluff(i), Palette.WithAlpha(color, 0.35f));
                    Ui.Stretch(layers[i].rectTransform);
                }

                _steps.Add(time =>
                {
                    var b = 0.5f + 0.5f * Mathf.Sin(time * 1.6f);
                    var c = rainbow ? Color.HSVToRGB(time * 0.05f % 1f, 0.55f, 1f) : color;
                    for (var i = 0; i < 2; i++)
                    {
                        layers[i].color = Palette.WithAlpha(c, 0.26f + 0.16f * b);
                        layers[i].rectTransform.localRotation = Quaternion.Euler(0f, 0f, (i == 0 ? 12f : -9f) * time % 360f);
                    }

                    holder.localScale = Vector3.one * (0.96f + 0.06f * b);
                });
            }

            /// <summary>Северное сияние: две завесы лучей, зелёная и фиолетовая, переливаются по очереди и чуть плывут.</summary>
            public void Aurora()
            {
                var green = Ui.Image(_layer, "AuroraA", Art2.Aurora(0), Palette.Hex("#6CFFC0"));
                var violet = Ui.Image(_layer, "AuroraB", Art2.Aurora(1), Palette.Hex("#B58CFF"));
                Ui.Place(green.rectTransform, 0, 0, _size, _size);
                Ui.Place(violet.rectTransform, 0, 0, _size, _size);
                green.transform.SetAsFirstSibling();
                violet.transform.SetAsFirstSibling();
                _steps.Add(time =>
                {
                    var w = 0.5f + 0.5f * Mathf.Sin(time * 0.55f);
                    green.color = Palette.WithAlpha(Color.Lerp(Palette.Hex("#6CFFC0"), Palette.Hex("#5FE3FF"), 0.5f + 0.5f * Mathf.Sin(time * 0.31f)), 0.25f + 0.6f * w);
                    violet.color = Palette.WithAlpha(Palette.Hex("#B58CFF"), 0.2f + 0.55f * (1f - w));
                    green.rectTransform.anchoredPosition = new Vector2(Mathf.Sin(time * 0.4f) * Band * 0.35f, 0f);
                    violet.rectTransform.anchoredPosition = new Vector2(-Mathf.Sin(time * 0.33f) * Band * 0.35f, 0f);
                });
            }

            /// <summary>Камин: огонь снизу дышит неровно, вверх по бокам летят угольки, у свечей пляшут огоньки.</summary>
            public void Hearth()
            {
                var fire = Add(Art.Glow, Palette.WithAlpha(Palette.Hex("#FF8A2A"), 0.6f), _size * 0.9f);
                fire.rectTransform.sizeDelta = new Vector2(_size * 1.05f, _size * 0.3f);
                Move(fire, new Vector2(C, C + Mid + Band * 0.45f));
                fire.transform.SetAsFirstSibling();

                var flames = new Image[2];
                var halos = new Image[2];
                for (var i = 0; i < 2; i++)
                {
                    var x = i == 0 ? C - Mid : C + Mid;
                    var at = new Vector2(x, C - Mid - Band * 0.95f);
                    halos[i] = Add(Art.Glow, Palette.WithAlpha(Palette.Hex("#FFC060"), 0.55f), Band * 2.2f);
                    flames[i] = Add(Art2.FrameMote(3), Color.white, Band * 0.85f);
                    flames[i].rectTransform.pivot = new Vector2(0.5f, 0.15f);
                    Move(halos[i], at);
                    Move(flames[i], at + new Vector2(0f, Band * 0.3f));
                }

                var embers = Count(7);
                var sparks = new Image[embers];
                for (var i = 0; i < embers; i++)
                {
                    sparks[i] = Add(Art.Glow, Palette.Hex("#FFB040"), Band * 0.95f);
                }

                _steps.Add(time =>
                {
                    // Неровное пламя: сумма двух несоизмеримых волн, а не ровная пульсация.
                    var flicker = 0.55f + 0.25f * Mathf.Sin(time * 5.3f) + 0.2f * Mathf.Sin(time * 8.9f + 1.3f);
                    fire.color = Palette.WithAlpha(Palette.Hex("#FF8A2A"), 0.5f + 0.35f * flicker);
                    fire.rectTransform.localScale = new Vector3(1f, 0.9f + 0.2f * flicker, 1f);
                    for (var i = 0; i < 2; i++)
                    {
                        var f = 0.5f + 0.3f * Mathf.Sin(time * (7f + i) + i) + 0.2f * Mathf.Sin(time * 11.3f + i * 2f);
                        flames[i].rectTransform.localScale = new Vector3(0.85f + 0.15f * f, 0.8f + 0.35f * f, 1f);
                        flames[i].rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(time * 3.1f + i) * 7f);
                        halos[i].color = Palette.WithAlpha(Palette.Hex("#FFC060"), 0.3f + 0.35f * f);
                    }

                    for (var i = 0; i < embers; i++)
                    {
                        var ph = Hash(i, 21);
                        var t = (time * (0.22f + 0.12f * Hash(i, 22)) + ph) % 1f;
                        var side = i % 2 == 0 ? -1f : 1f;
                        var x = C + side * (Mid + (Hash(i, 23) - 0.5f) * Band) + Mathf.Sin(time * 2.3f + ph * 6.28f) * Band * 0.5f;
                        var y = C + Mid - t * _size * 0.75f;
                        Move(sparks[i], new Vector2(x, y));
                        sparks[i].color = Palette.WithAlpha(Color.Lerp(Palette.Hex("#FFD27A"), Palette.Hex("#E8442A"), t), Mathf.Clamp01(t / 0.08f) * (1f - t));
                        sparks[i].rectTransform.localScale = Vector3.one * (1f - 0.5f * t);
                    }
                });
            }

            /// <summary>
            /// Волшебный клубок: по радужному шнуру бежит нить света — яркая голова со звёздочкой и хвост,
            /// каждая точка хвоста цвета шнура под ней.
            /// </summary>
            public void Comet()
            {
                var tail = _lite ? 4 : 10;
                var dots = new Image[tail];
                for (var k = tail - 1; k >= 0; k--)
                {
                    dots[k] = Add(Art.Glow, Color.white, Band * (1.9f - 1.1f * k / tail));
                }

                var head = Add(Art2.Sparkle, Color.white, Band * 1.8f);
                _steps.Add(time =>
                {
                    var p0 = time / 4.8f;
                    for (var k = 0; k < tail; k++)
                    {
                        var p = p0 - k * 0.013f;
                        Move(dots[k], Path(p, Outer - Band * 0.25f));
                        var hue = (p - Mathf.Floor(p) + 0.875f) % 1f;
                        dots[k].color = Palette.WithAlpha(Color.HSVToRGB(hue, 0.55f, 1f), 0.95f * (1f - (float)k / tail));
                    }

                    Move(head, Path(p0, Outer - Band * 0.25f));
                    head.rectTransform.localRotation = Quaternion.Euler(0f, 0f, time * 120f);
                });
            }
        }
    }
}
