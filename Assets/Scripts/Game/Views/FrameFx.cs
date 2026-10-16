#nullable enable

using System;
using System.Collections.Generic;
using KnittedPicture.Core;
using UnityEngine;
using UnityEngine.UI;

namespace KnittedPicture.Game
{
    /// <summary>
    /// Живые рамки альбома (эпические и легендарные). Эффекты собраны как VFX в больших играх, только на uGUI:
    /// <list type="bullet">
    /// <item>слои глубины: свечение и ударные волны позади рамки, частицы на багете, яркие ядра и блики поверх;</item>
    /// <item>частицы с жизнью: у каждой своя случайная судьба на каждый цикл (скорость, гравитация, сопротивление,
    /// завихрение), цвет по рампе за жизнь (белое ядро → жёлтый → оранжевый → тёмно-красный), размер по кривой;</item>
    /// <item>ритм «idle + акцент»: ровная жизнь и раз в несколько секунд кульминация — вспышка, волна, взрыв искр,
    /// чтобы рамка «дышала» сюжетом, а не крутилась по кругу;</item>
    /// <item>псевдо-3D: лепестки и листья переворачиваются (сжатие по X по косинусу), нить волшебного клубка
    /// ныряет под вязку и выходит (тусклее и тоньше «под», ярче и толще «над»).</item>
    /// </list>
    /// Эффекты лежат на багете и вокруг, картинку почти не задевают. Вся рамка — один цикл (один твин), все частицы
    /// заранее созданы и только двигаются. В сетке альбома — облегчённо (около трети частиц, без свечения позади).
    /// «Меньше движения» замораживает всё в спокойной позе.
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

            var fx = new Fx(root, size, lite);
            switch (frameId)
            {
                case 9: fx.GoldBaguette(); break;
                case 10: fx.Snow(); break;
                case 11: fx.Petals(); break;
                case 12: fx.Sunflowers(); break;
                case 13: fx.Leaves(); break;
                case 27: fx.Fireflies(); break;
                case 28: fx.NorthernLights(); break;
                case 29: fx.Fireside(); break;
                case 30: fx.Firebird(); break;
                default: fx.MagicYarn(); break;
            }

            fx.Start();
        }

        /// <summary>Состояние частицы на кадр: куда, какого размера, повёрнута, какого цвета и насколько «перевёрнута».</summary>
        private struct Spark
        {
            public Vector2 Pos;
            public float Size;
            public float Rot;
            public Color Color;
            public float ScaleX;
        }

        /// <summary>Частица i в цикле cycle (своя случайная судьба), возраст age 0..1, общее время time.</summary>
        private delegate void Eval(int i, int cycle, float age, float time, ref Spark s);

        private sealed class Fx
        {
            /// <summary>Длина общего цикла: всё время эффекта, дальше повтор (незаметно — частицы случайны по циклам).</summary>
            private const float Period = 600f;

            /// <summary>Размер, под который создаются картинки частиц: дальше только масштаб, без перестройки сетки.</summary>
            private const float Unit = 32f;

            private readonly RectTransform _root;
            private readonly RectTransform _layer;
            private readonly float _size;
            private readonly bool _lite;
            private readonly List<Action<float>> _steps = new List<Action<float>>();
            private RectTransform? _back;

            public Fx(RectTransform root, float size, bool lite)
            {
                _root = root;
                _size = size;
                _lite = lite;
                _layer = Ui.Place(Ui.Node(root, "FrameFx"), 0, 0, size, size);
            }

            private float C => _size / 2f;

            /// <summary>Середина багета от центра.</summary>
            private float Mid => _size * 30.25f / FrameRaster.Size;

            private float Band => _size * 7.5f / FrameRaster.Size;

            private float Outer => _size * 33f / FrameRaster.Size;

            /// <summary>Слой позади рамки: свечение и ударные волны.</summary>
            private RectTransform Back
            {
                get
                {
                    if (_back == null)
                    {
                        _back = Ui.Place(Ui.Node(_root, "FrameFxBack"), 0, 0, _size, _size);
                        _back.SetAsFirstSibling();
                    }

                    return _back;
                }
            }

            private int Count(int n) => _lite ? Mathf.Max(2, Mathf.RoundToInt(n * 0.35f)) : n;

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

            // ---------- Рамки ----------

            /// <summary>
            /// Золотой багет: по золоту скользит вытянутый блик с хвостом; рубины на розетках по очереди вспыхивают
            /// крестом. Акцент раз в 6 с: все четыре розетки вспыхивают по кругу, и сверху осыпается золотая пыль.
            /// </summary>
            public void GoldBaguette()
            {
                Streak(1, 5.2f, Mid, Palette.Hex("#FFF6CF"), 1.5f, 6);
                var gold = new[] { Color.white, Palette.Hex("#FFE58A"), Palette.Hex("#E9A62A") };
                Burst(Count(12), 6f, 0f, 2.2f, Art.Glow, (int i, int cycle, float age, float time, ref Spark s) =>
                {
                    var x = C + (R(i, cycle, 1) - 0.5f) * Mid * 2f;
                    var t = age * 2.2f;
                    s.Pos = new Vector2(x + Mathf.Sin(t * 3f + i) * Band * 0.4f, C - Mid - Band * 0.2f + 18f * t * t * (_size / 100f) + R(i, cycle, 2) * Band);
                    s.Size = Band * (0.35f + 0.35f * R(i, cycle, 3)) * (1f - age * 0.5f);
                    s.Color = Palette.WithAlpha(Ramp(age, gold), Twinkle(time, i) * Fade(age, 0.05f, 0.4f));
                });
                RosetteFlashes(Palette.Hex("#FFD9E6"), 6f);
            }

            /// <summary>
            /// Снежинки: снег в три плана — дальний мелкий и медленный, средний, ближний крупный и быстрый, с размытым
            /// ореолом. Ветер плавно меняется; раз в 7 с — порыв: снег закручивает вбок. Кристаллы на углах искрятся.
            /// </summary>
            public void Snow()
            {
                for (var plane = 0; plane < 3; plane++)
                {
                    var depth = plane;
                    var count = Count(new[] { 7, 6, 3 }[plane]);
                    var size = new[] { 0.32f, 0.5f, 0.8f }[plane] * Band;
                    var speed = new[] { 0.09f, 0.13f, 0.2f }[plane];
                    var alpha = new[] { 0.55f, 0.85f, 1f }[plane];
                    Particles(count, 1f / speed, Art2.FrameMote(0), (int i, int cycle, float age, float time, ref Spark s) =>
                    {
                        var gust = Gust(time, 7f);
                        var side = R(i, cycle, 1) < 0.5f ? -1f : 1f;
                        var x = C + side * (Mid + (R(i, cycle, 2) - 0.4f) * Band * 1.8f);
                        x += Mathf.Sin(time * (0.8f + depth * 0.3f) + R(i, cycle, 3) * 6f) * Band * (0.4f + 0.3f * depth);
                        x += gust * Band * (1.2f + depth) * Mathf.Sin(age * Mathf.PI);
                        s.Pos = new Vector2(x, -_size * 0.06f + age * _size * 1.1f);
                        s.Size = size * (0.8f + 0.4f * R(i, cycle, 4));
                        s.Rot = time * (30f + 50f * R(i, cycle, 5)) * side;
                        s.Color = Palette.WithAlpha(depth == 0 ? Palette.Hex("#DDEFFF") : Color.white, alpha * Fade(age, 0.08f, 0.15f));
                    }, depth == 0);
                }

                SparkleAt(Corners(), Palette.Hex("#E8F7FF"), 3f, 1.6f);
            }

            /// <summary>
            /// Первоцветы: лепестки падают по спирали и переворачиваются в воздухе (псевдо-3D), цвет от белого к розовому.
            /// Раз в 6 с — ветерок: стайка лепестков проносится по верхней планке слева направо.
            /// </summary>
            public void Petals()
            {
                var pinks = new[] { Palette.Hex("#FFFFFF"), Palette.Hex("#FFD3E6"), Palette.Hex("#FF9CC6") };
                Particles(Count(8), 7.5f, Art2.FrameMote(1), (int i, int cycle, float age, float time, ref Spark s) =>
                {
                    var side = R(i, cycle, 1) < 0.5f ? -1f : 1f;
                    var spin = time * (1.6f + R(i, cycle, 2)) + R(i, cycle, 3) * 6f;
                    var x = C + side * (Mid + (R(i, cycle, 4) - 0.4f) * Band * 1.6f) + Mathf.Sin(spin) * Band * 0.9f;
                    s.Pos = new Vector2(x, -_size * 0.05f + age * _size * 1.08f);
                    s.Size = Band * (0.75f + 0.4f * R(i, cycle, 5));
                    s.ScaleX = Mathf.Cos(spin * 1.3f);
                    s.Rot = Mathf.Sin(spin) * 50f + side * 30f;
                    s.Color = Palette.WithAlpha(pinks[i % pinks.Length], Fade(age, 0.08f, 0.18f));
                });
                Burst(Count(7), 6f, 1f, 2.4f, Art2.FrameMote(1), (int i, int cycle, float age, float time, ref Spark s) =>
                {
                    var t = Ease.OutCubic(age);
                    var lane = (R(i, cycle, 1) - 0.5f) * Band * 1.6f;
                    s.Pos = new Vector2(C - Mid - Band + t * (Mid * 2f + Band * 2f) + Mathf.Sin(age * 9f + i) * Band * 0.3f,
                        C - Mid + lane + Mathf.Sin(age * 6f + i) * Band * 0.5f);
                    s.Size = Band * (0.6f + 0.5f * R(i, cycle, 2));
                    s.ScaleX = Mathf.Cos(age * 14f + i);
                    s.Rot = age * 400f + i * 40f;
                    s.Color = Palette.WithAlpha(pinks[(i + 1) % pinks.Length], Fade(age, 0.1f, 0.25f));
                });
            }

            /// <summary>
            /// Подсолнухи: за угловыми цветами медленно вращаются солнечные лучи, тёплая пыльца поднимается завитками
            /// и меняет цвет от белого к янтарному. Раз в 5 с — солнечный блик: вспышка в углу и «блики линзы» по диагонали.
            /// </summary>
            public void Sunflowers()
            {
                foreach (var corner in Corners())
                {
                    var rays = Image(Art.Rays, Palette.WithAlpha(Palette.Hex("#FFD54A"), 0.5f), Band * 4.2f, Back);
                    Move(rays, corner);
                    var rt = rays.rectTransform;
                    var phase = corner.x + corner.y;
                    _steps.Add(time =>
                    {
                        rt.localRotation = Quaternion.Euler(0f, 0f, time * 20f + phase);
                        rays.color = Palette.WithAlpha(Palette.Hex("#FFD54A"), 0.35f + 0.2f * Mathf.Sin(time * 1.7f + phase));
                    });
                }

                var amber = new[] { Color.white, Palette.Hex("#FFE58A"), Palette.Hex("#F2A93A") };
                Particles(Count(12), 4.5f, Art.Glow, (int i, int cycle, float age, float time, ref Spark s) =>
                {
                    var start = Path(R(i, cycle, 1), Mid);
                    var curl = new Vector2(Mathf.Sin(age * 7f + R(i, cycle, 2) * 6f), Mathf.Cos(age * 5f + i)) * Band * 0.6f;
                    s.Pos = start + curl + new Vector2(0f, -age * Band * 2.2f);
                    s.Size = Band * (0.35f + 0.4f * Mathf.Sin(age * Mathf.PI));
                    s.Color = Palette.WithAlpha(Ramp(age, amber), Fade(age, 0.15f, 0.3f));
                });
                LensFlare(5f);
            }

            /// <summary>
            /// Листопад: листья падают «маятником» (качаются и скользят из стороны в сторону, как настоящий лист),
            /// переворачиваются в воздухе. Раз в 7 с — порыв: листья сдувает вбок, и по нижней планке катится ещё стайка.
            /// </summary>
            public void Leaves()
            {
                var colors = new[] { Palette.Hex("#E8742A"), Palette.Hex("#D9452B"), Palette.Hex("#F2B632"), Palette.Hex("#B8521E") };
                Particles(Count(8), 8f, Art2.FrameMote(2), (int i, int cycle, float age, float time, ref Spark s) =>
                {
                    var side = R(i, cycle, 1) < 0.5f ? -1f : 1f;
                    var swing = Mathf.Sin(time * (1.3f + R(i, cycle, 2) * 0.6f) + i);
                    var x = C + side * (Mid + (R(i, cycle, 3) - 0.4f) * Band * 1.6f) + swing * Band * 1.1f;
                    x += Gust(time, 7f) * Band * 2.2f * Mathf.Sin(age * Mathf.PI);
                    s.Pos = new Vector2(x, -_size * 0.05f + age * _size * 1.08f + Mathf.Abs(swing) * Band * 0.3f);
                    s.Size = Band * (0.85f + 0.4f * R(i, cycle, 4));
                    s.Rot = swing * 45f + side * 20f;
                    s.ScaleX = Mathf.Cos(time * (0.9f + R(i, cycle, 5)) + i);
                    s.Color = Palette.WithAlpha(colors[(i + cycle) % colors.Length], Fade(age, 0.08f, 0.18f));
                });
                Burst(Count(5), 7f, 0.5f, 2.6f, Art2.FrameMote(2), (int i, int cycle, float age, float time, ref Spark s) =>
                {
                    var t = Ease.OutCubic(age);
                    s.Pos = new Vector2(C - Mid - Band * 1.5f + t * (Mid * 2f + Band * 3f), C + Mid + (R(i, cycle, 1) - 0.5f) * Band - Mathf.Abs(Mathf.Sin(age * 10f + i)) * Band * 0.8f);
                    s.Size = Band * (0.7f + 0.4f * R(i, cycle, 2));
                    s.Rot = age * 720f + i * 60f;
                    s.ScaleX = Mathf.Cos(age * 12f + i);
                    s.Color = Palette.WithAlpha(colors[i % colors.Length], Fade(age, 0.1f, 0.25f));
                });
            }

            /// <summary>
            /// Светлячки: огоньки летают свободно над лозой со светящимся шлейфом (три «отставших» отражения), мигают
            /// вразнобой. Раз в 5 с — синхронная вспышка, как у настоящих светлячков: волна света обегает рамку,
            /// и каждый огонёк вспыхивает, когда она проходит мимо; банки-фонарики отвечают ярким светом,
            /// из них поднимаются искорки.
            /// </summary>
            public void Fireflies()
            {
                CornerGlow(Palette.Hex("#F4FF9A"), 0.45f, 3f, 5f);
                var count = Count(10);
                var trail = _lite ? 1 : 3;
                var green = Palette.Hex("#E9FF8A");
                for (var k = trail; k >= 0; k--)
                {
                    var lag = k * 0.09f;
                    var ghost = k;
                    Particles(count, Period, Art.Glow, (int i, int cycle, float age, float time, ref Spark s) =>
                    {
                        var t = time - lag;
                        s.Pos = FireflyAt(i, t);
                        var wave = SyncWave(i, count, t);
                        var blink = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * (1.1f + R(i, 0, 5)) + R(i, 0, 6) * 6f)), 3f);
                        var light = Mathf.Clamp01(0.1f + 0.75f * blink + wave);
                        s.Size = Band * (ghost == 0 ? 1.7f : 1.1f - 0.2f * ghost) * (0.6f + 0.6f * light);
                        s.Color = Palette.WithAlpha(green, (ghost == 0 ? 0.85f : 0.35f / ghost) * light);
                    }, false, 0f);
                }

                Particles(count, Period, Art.Circle, (int i, int cycle, float age, float time, ref Spark s) =>
                {
                    s.Pos = FireflyAt(i, time);
                    var blink = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(time * (1.1f + R(i, 0, 5)) + R(i, 0, 6) * 6f)), 3f);
                    var light = Mathf.Clamp01(0.15f + 0.85f * blink + SyncWave(i, count, time));
                    s.Size = Band * 0.34f;
                    s.Color = Palette.WithAlpha(Color.Lerp(green, Color.white, 0.7f), light);
                }, false, 0f);

                // Искорки из банок после синхронной вспышки.
                Burst(Count(10), 5f, 0f, 2f, Art.Glow, (int i, int cycle, float age, float time, ref Spark s) =>
                {
                    var corner = Corners()[i % 4];
                    var drift = new Vector2((R(i, cycle, 1) - 0.5f) * Band * 2f, -Band * 2.4f * Ease.OutCubic(age));
                    s.Pos = corner + drift + new Vector2(Mathf.Sin(age * 8f + i) * Band * 0.3f, 0f);
                    s.Size = Band * 0.5f * (1f - age * 0.6f);
                    s.Color = Palette.WithAlpha(Color.Lerp(Color.white, green, age), Fade(age, 0.05f, 0.5f));
                });
            }

            /// <summary>
            /// Северное сияние: две завесы лучей переливаются по рампе зелёный → бирюза → розовый и плывут навстречу,
            /// звёзды вспыхивают в случайных местах. Раз в 4,5 с — падающая звезда: вытянутый след с яркой головой
            /// пересекает верх рамки по диагонали; кристаллы на углах вспыхивают по очереди.
            /// </summary>
            public void NorthernLights()
            {
                var a = Image(Art2.Aurora(0), Color.clear, _size, null, true);
                var b = Image(Art2.Aurora(1), Color.clear, _size, null, true);
                var ramp = new[] { Palette.Hex("#6CFFC0"), Palette.Hex("#5FE3FF"), Palette.Hex("#B58CFF"), Palette.Hex("#FF8CD0"), Palette.Hex("#6CFFC0") };
                _steps.Add(time =>
                {
                    var w = 0.5f + 0.5f * Mathf.Sin(time * 0.6f);
                    a.color = Palette.WithAlpha(Ramp(time * 0.04f % 1f, ramp), 0.3f + 0.6f * w);
                    b.color = Palette.WithAlpha(Ramp((time * 0.04f + 0.4f) % 1f, ramp), 0.25f + 0.6f * (1f - w));
                    a.rectTransform.anchoredPosition = new Vector2(Mathf.Sin(time * 0.45f) * Band * 0.5f, 0f);
                    b.rectTransform.anchoredPosition = new Vector2(-Mathf.Sin(time * 0.37f) * Band * 0.5f, 0f);
                    a.rectTransform.localScale = new Vector3(1f, 1f + 0.04f * Mathf.Sin(time * 0.9f), 1f);
                });

                Particles(Count(10), 2.6f, Art2.Sparkle, (int i, int cycle, float age, float time, ref Spark s) =>
                {
                    s.Pos = Path(R(i, cycle, 1), Mid + (R(i, cycle, 2) - 0.5f) * Band);
                    s.Size = Band * (0.5f + 0.6f * R(i, cycle, 3)) * Ease.Bump(age);
                    s.Rot = 45f * R(i, cycle, 4);
                    s.Color = Palette.WithAlpha(Palette.Hex("#EFFFFF"), Ease.Bump(age));
                });
                ShootingStar(4.5f);
                SparkleAt(Corners(), Palette.Hex("#E9FFFF"), 2.2f, 1.9f);
            }

            /// <summary>
            /// Камин: огонь двумя слоями (глубокий красный и оранжевый) дышит неровно; по нижней планке лижут языки
            /// пламени; угольки поднимаются с завихрением и остывают по рампе белый → жёлтый → оранжевый → тёмно-красный.
            /// Раз в 3,2 с — треск: вспышка и россыпь искр по дугам (с гравитацией) из случайного места огня. Свечи на полке
            /// мерцают своим огоньком.
            /// </summary>
            public void Fireside()
            {
                var deep = Image(Art.Glow, Color.clear, Unit, Back);
                deep.rectTransform.sizeDelta = new Vector2(_size * 1.25f, _size * 0.45f);
                Move(deep, new Vector2(C, C + Mid + Band * 0.6f));
                var warm = Image(Art.Glow, Color.clear, Unit);
                warm.rectTransform.sizeDelta = new Vector2(_size * 0.95f, _size * 0.24f);
                Move(warm, new Vector2(C, C + Mid + Band * 0.4f));
                warm.transform.SetAsFirstSibling();
                _steps.Add(time =>
                {
                    var f = Flicker(time);
                    deep.color = Palette.WithAlpha(Palette.Hex("#E8442A"), 0.35f + 0.25f * f);
                    warm.color = Palette.WithAlpha(Palette.Hex("#FFA040"), 0.45f + 0.35f * f);
                    warm.rectTransform.localScale = new Vector3(1f, 0.85f + 0.3f * f, 1f);
                });

                // Языки пламени по нижней планке.
                var tongues = Count(7);
                Particles(tongues, 0.9f, Art2.FrameMote(3), (int i, int cycle, float age, float time, ref Spark s) =>
                {
                    var x = C - Mid + Mid * 2f * (i + 0.5f) / tongues + (R(i, cycle, 1) - 0.5f) * Band;
                    s.Pos = new Vector2(x, C + Mid + Band * 0.3f - age * Band * 1.6f);
                    s.Size = Band * (1.3f + 0.6f * R(i, cycle, 2)) * Mathf.Sin(age * Mathf.PI);
                    s.ScaleX = 0.7f;
                    s.Rot = (R(i, cycle, 3) - 0.5f) * 30f;
                    s.Color = Palette.WithAlpha(Ramp(age, Palette.Hex("#FFF2B0"), Palette.Hex("#FFB040"), Palette.Hex("#E8442A")), 0.85f * Fade(age, 0.1f, 0.4f));
                });

                var embers = new[] { Color.white, Palette.Hex("#FFE07A"), Palette.Hex("#FF8A2A"), Palette.Hex("#C8281E"), Palette.Hex("#4A1D14") };
                Particles(Count(12), 2.6f, Art.Glow, (int i, int cycle, float age, float time, ref Spark s) =>
                {
                    var side = R(i, cycle, 1) < 0.5f ? -1f : 1f;
                    var x0 = C + side * (Mid + (R(i, cycle, 2) - 0.5f) * Band * 1.2f);
                    var t = age * 2.6f;
                    var curl = Mathf.Sin(t * (2f + R(i, cycle, 3) * 2f) + i) * Band * (0.25f + 0.4f * age);
                    s.Pos = new Vector2(x0 + curl + side * age * Band * 0.6f, C + Mid - t * _size * 0.32f);
                    s.Size = Band * (0.75f - 0.5f * age) * (0.8f + 0.4f * R(i, cycle, 4));
                    s.Color = Palette.WithAlpha(Ramp(age, embers), Fade(age, 0.04f, 0.3f));
                });

                // Треск: вспышка и искры по дугам.
                Flash(new Vector2(C, C + Mid), 3.2f, 0f, Palette.Hex("#FFD27A"), Band * 4f, 0.35f);
                Burst(Count(10), 3.2f, 0f, 1.1f, Art.Glow, (int i, int cycle, float age, float time, ref Spark s) =>
                {
                    // Искры вылетают из камина наружу: веером вниз-в стороны (падают за рамку на «пол») и низкими
                    // дугами вверх над нижней планкой — на картинку не залетают.
                    var origin = new Vector2(C + (R(0, cycle, 9) - 0.5f) * Mid * 1.4f, C + Mid + Band * 0.3f);
                    var up = i % 3 == 0;
                    var angle = (up ? Mathf.Lerp(-140f, -40f, R(i, cycle, 1)) : Mathf.Lerp(15f, 165f, R(i, cycle, 1))) * Mathf.Deg2Rad;
                    var speed = _size * (up ? 0.22f + 0.1f * R(i, cycle, 2) : 0.3f + 0.35f * R(i, cycle, 2));
                    var t = age * 1.1f;
                    var v = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
                    s.Pos = origin + v * t + new Vector2(0f, 0.6f * _size * t * t);
                    s.Size = Band * 0.45f * (1f - age * 0.5f);
                    s.Color = Palette.WithAlpha(Ramp(age, embers), Fade(age, 0.02f, 0.5f));
                });

                // Свечи на полке.
                foreach (var cx in new[] { C - Mid, C + Mid })
                {
                    var at = new Vector2(cx, C - Mid - Band * 0.95f);
                    var halo = Image(Art.Glow, Color.clear, Band * 2.4f);
                    var flame = Image(Art2.FrameMote(3), Color.white, Band * 0.9f);
                    flame.rectTransform.pivot = new Vector2(0.5f, 0.15f);
                    Move(halo, at);
                    Move(flame, at + new Vector2(0f, Band * 0.3f));
                    var seed = cx;
                    _steps.Add(time =>
                    {
                        var f = 0.5f + 0.3f * Mathf.Sin(time * 7.3f + seed) + 0.2f * Mathf.Sin(time * 12.1f + seed * 2f);
                        flame.rectTransform.localScale = new Vector3(0.8f + 0.2f * f, 0.75f + 0.45f * f, 1f);
                        flame.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(time * 3.1f + seed) * 9f);
                        halo.color = Palette.WithAlpha(Palette.Hex("#FFC060"), 0.3f + 0.4f * f);
                    });
                }
            }

            /// <summary>
            /// Жар-птица (легендарная). Idle: тёплое свечение позади, два огненных сгустка бегут по хохломе навстречу
            /// друг другу, за каждым — рваный огненный шлейф (частицы отрываются, поднимаются и остывают по рампе);
            /// перья на углах горят; по всей рамке тлеют искры. Акцент раз в 5 с — сгустки встречаются наверху:
            /// «взлёт жар-птицы» — белая вспышка, веер из перьев-искр, ударная волна по всей рамке и всплеск свечения.
            /// </summary>
            public void Firebird()
            {
                const float beat = 5f;
                Halo(time => Palette.Hex("#FF9A2E"), time => Spike(Phase(time, beat, 0f), 0.04f, 0.5f));
                Shockwave(beat, 0f, Palette.Hex("#FFB040"));
                CornerGlow(Palette.Hex("#FF5A2A"), 0.6f, 2.2f, beat);

                var fire = new[] { Color.white, Palette.Hex("#FFE38A"), Palette.Hex("#FF9A2E"), Palette.Hex("#E8333A"), Palette.Hex("#6B1020") };

                // Два сгустка: стартуют снизу по центру, расходятся и встречаются наверху ровно в момент акцента.
                for (var h = 0; h < 2; h++)
                {
                    var dir = h == 0 ? 1f : -1f;
                    Func<float, float> along = time => 0.625f + dir * 0.5f * (Phase(time, beat, 0f));
                    var tail = _lite ? 4 : 14;
                    Particles(tail, 0.7f, Art.Glow, (int i, int cycle, float age, float time, ref Spark s) =>
                    {
                        var born = time - age * 0.7f;
                        var at = Path(along(born), Mid);
                        var rise = new Vector2((R(i, cycle, 1) - 0.5f) * Band * 0.8f, -age * Band * 1.8f);
                        s.Pos = at + rise;
                        s.Size = Band * (1.5f - 1.1f * age) * (0.8f + 0.4f * R(i, cycle, 2));
                        s.Color = Palette.WithAlpha(Ramp(age, fire), 0.9f * (1f - age));
                    });
                    var core = Image(Art.Glow, Palette.Hex("#FFF6D0"), Band * 2.2f);
                    var star = Image(Art2.Sparkle, Color.white, Band * 1.6f);
                    _steps.Add(time =>
                    {
                        var at = Path(along(time), Mid);
                        Move(core, at);
                        Move(star, at);
                        star.rectTransform.localRotation = Quaternion.Euler(0f, 0f, time * 160f * dir);
                        core.rectTransform.localScale = Vector3.one * (0.9f + 0.25f * Flicker(time * 1.7f));
                    });
                }

                // Взлёт: вспышка наверху и веер перьев-искр.
                var top = new Vector2(C, C - Mid);
                Flash(top, beat, 0f, Color.white, Band * 6f, 0.9f);
                Flash(top, beat, 0f, Palette.Hex("#FFB040"), Band * 10f, 0.55f, true);
                Burst(Count(16), beat, 0f, 1.6f, Art2.FrameMote(3), (int i, int cycle, float age, float time, ref Spark s) =>
                {
                    var angle = Mathf.Lerp(-170f, -10f, (i + R(i, cycle, 1) * 0.6f) / Count(16)) * Mathf.Deg2Rad;
                    var speed = _size * (0.35f + 0.3f * R(i, cycle, 2));
                    var t = Drag(age * 1.6f, 2.2f);
                    var dirV = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    s.Pos = top + dirV * speed * t + new Vector2(0f, -Band * 1.2f * age);
                    s.Size = Band * (1.4f + 0.6f * R(i, cycle, 3)) * (1f - 0.5f * age);
                    s.Rot = Mathf.Atan2(-dirV.y, dirV.x) * Mathf.Rad2Deg - 90f;
                    s.Color = Palette.WithAlpha(Ramp(age, fire), Fade(age, 0.02f, 0.5f));
                });

                // Тлеющие искры по всей рамке.
                Particles(Count(10), 2.2f, Art.Glow, (int i, int cycle, float age, float time, ref Spark s) =>
                {
                    var at = Path(R(i, cycle, 1), Mid + (R(i, cycle, 2) - 0.5f) * Band);
                    s.Pos = at + new Vector2(Mathf.Sin(age * 6f + i) * Band * 0.3f, -age * Band * 1.5f);
                    s.Size = Band * 0.4f * (1f - age * 0.5f);
                    s.Color = Palette.WithAlpha(Ramp(0.2f + age * 0.7f, fire), Fade(age, 0.1f, 0.4f));
                });
            }

            /// <summary>
            /// Волшебный клубок (легендарный). Idle: радужное свечение позади; по шнуру бежит живая нить света и «шьёт»:
            /// ныряет под вязку и выходит (под — тусклее и тоньше, над — ярче и толще), за ней хвост меняет цвет
            /// по радуге и осыпается блёстками; вокруг угловых клубков кружат искорки. Акцент на каждом круге (4,8 с) у
            /// верхнего левого клубка: радужный взрыв мотков-конфетти с гравитацией, радужная ударная волна,
            /// все клубки вспыхивают.
            /// </summary>
            public void MagicYarn()
            {
                const float lap = 4.8f;
                Halo(time => Color.HSVToRGB(time * 0.06f % 1f, 0.55f, 1f), time => Spike(Phase(time, lap, 0f), 0.04f, 0.45f));
                Shockwave(lap, 0f, Color.white, true);

                var line = Outer - Band * 0.3f;
                Vector2 Weave(float p, out float over)
                {
                    var at = Path(p, line);
                    var wave = Mathf.Sin(p * Mathf.PI * 2f * 22f);
                    over = wave;
                    var inward = (new Vector2(C, C) - at).normalized;
                    return at + inward * wave * Band * 0.35f;
                }

                // Хвост нити: сегменты по пути, цвет по радуге, «под» и «над» вязкой.
                var tail = _lite ? 6 : 20;
                Particles(tail, Period, Art.Glow, (int i, int cycle, float age, float time, ref Spark s) =>
                {
                    var p = time / lap - i * 0.0065f;
                    s.Pos = Weave(p, out var over);
                    var k = 1f - (float)i / tail;
                    var depth = 0.55f + 0.45f * over;
                    s.Size = Band * (0.6f + 1.1f * k) * (0.7f + 0.45f * depth);
                    s.Color = Palette.WithAlpha(Color.HSVToRGB((p * 1.5f + 10f) % 1f, 0.6f, 1f), k * (0.35f + 0.65f * depth));
                }, false, 0f);
                var head = Image(Art.Glow, Color.white, Band * 2.2f);
                var star = Image(Art2.Sparkle, Color.white, Band * 2f);
                _steps.Add(time =>
                {
                    var at = Weave(time / lap, out var over);
                    Move(head, at);
                    Move(star, at);
                    star.rectTransform.localRotation = Quaternion.Euler(0f, 0f, time * 200f);
                    star.rectTransform.localScale = Vector3.one * (0.6f + 0.5f * (0.5f + 0.5f * over));
                });

                // Блёстки, которые осыпаются с нити.
                Particles(Count(14), 1.4f, Art2.Sparkle, (int i, int cycle, float age, float time, ref Spark s) =>
                {
                    var born = time - age * 1.4f;
                    var at = Weave(born / lap, out _);
                    s.Pos = at + new Vector2((R(i, cycle, 1) - 0.5f) * Band, age * Band * 1.4f);
                    s.Size = Band * 0.7f * Ease.Bump(Mathf.Clamp01(age * 1.3f));
                    s.Rot = age * 180f;
                    s.Color = Palette.WithAlpha(Color.HSVToRGB((born * 0.3f + R(i, cycle, 2)) % 1f, 0.35f, 1f), 1f - age);
                });

                // Искорки на орбите вокруг угловых клубков.
                var corners = Corners();
                Particles(_lite ? 4 : 8, Period, Art2.Sparkle, (int i, int cycle, float age, float time, ref Spark s) =>
                {
                    var corner = corners[i % 4];
                    var a = time * 2.4f + i * 1.7f;
                    s.Pos = corner + new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.6f) * Band * 0.95f;
                    s.Size = Band * 0.6f * (0.5f + 0.5f * Mathf.Sin(a * 2f));
                    s.Color = Palette.WithAlpha(Color.white, 0.4f + 0.6f * Mathf.Max(0f, Mathf.Sin(a)) + Spike(Phase(time, lap, 0f), 0.02f, 0.3f));
                }, false, 0f);

                // Акцент: конфетти из мотков у верхнего левого клубка.
                var origin = corners[0];
                Flash(origin, lap, 0f, Color.white, Band * 5f, 0.85f);
                Burst(Count(18), lap, 0f, 1.8f, Art.Glow, (int i, int cycle, float age, float time, ref Spark s) =>
                {
                    var angle = Mathf.Lerp(-100f, 10f, R(i, cycle, 1)) * Mathf.Deg2Rad;
                    var speed = _size * (0.35f + 0.45f * R(i, cycle, 2));
                    var t = Drag(age * 1.8f, 1.6f);
                    s.Pos = origin + new Vector2(Mathf.Cos(angle), -Mathf.Sin(angle)) * speed * t * 0.9f + new Vector2(0f, 0.25f * _size * age * age);
                    s.Size = Band * (0.55f + 0.4f * R(i, cycle, 3)) * (1f - 0.4f * age);
                    s.Color = Palette.WithAlpha(Color.HSVToRGB(R(i, cycle, 4), 0.65f, 1f), Fade(age, 0.02f, 0.4f));
                });
            }

            // ---------- Строительные блоки ----------

            /// <summary>
            /// Постоянные частицы: count штук, жизнь life секунд, вразбежку по времени (stagger = 1) или все вместе (0).
            /// На каждый новый цикл частица получает новую случайную судьбу — повторов не видно.
            /// </summary>
            private void Particles(int count, float life, Sprite sprite, Eval eval, bool back = false, float stagger = 1f)
            {
                var images = new Image[count];
                for (var i = 0; i < count; i++)
                {
                    images[i] = Image(sprite, Color.clear, Unit, back ? Back : null);
                }

                _steps.Add(time =>
                {
                    for (var i = 0; i < count; i++)
                    {
                        var local = time + stagger * i * life / count + stagger * R(i, 0, 77) * life * 0.3f;
                        var cycle = Mathf.FloorToInt(local / life);
                        var age = local / life - cycle;
                        Apply(images[i], i, cycle, age, time, eval);
                    }
                });
            }

            /// <summary>
            /// Залп: count частиц, все рождаются в момент акцента (раз в period, со сдвигом offset) и живут duration
            /// секунд; между залпами их не видно.
            /// </summary>
            private void Burst(int count, float period, float offset, float duration, Sprite sprite, Eval eval)
            {
                var images = new Image[count];
                for (var i = 0; i < count; i++)
                {
                    images[i] = Image(sprite, Color.clear, Unit);
                }

                _steps.Add(time =>
                {
                    var since = Phase(time, period, offset) * period;
                    var cycle = Mathf.FloorToInt((time + offset) / period);
                    var age = since / duration;
                    for (var i = 0; i < count; i++)
                    {
                        if (age >= 1f || time < 0.01f)
                        {
                            images[i].color = Color.clear;
                            continue;
                        }

                        Apply(images[i], i, cycle, age, time, eval);
                    }
                });
            }

            private void Apply(Image image, int i, int cycle, float age, float time, Eval eval)
            {
                var s = new Spark { Pos = new Vector2(C, C), Size = 0f, Color = Color.clear, ScaleX = 1f };
                eval(i, cycle, age, time, ref s);
                var rt = image.rectTransform;
                rt.anchoredPosition = new Vector2(s.Pos.x, -s.Pos.y);
                var k = s.Size / Unit;
                rt.localScale = new Vector3(k * s.ScaleX, k, 1f);
                rt.localRotation = Quaternion.Euler(0f, 0f, s.Rot);
                image.color = s.Color;
            }

            /// <summary>Вытянутый блик с хвостом бежит по багету (heads штук), повёрнут по направлению движения.</summary>
            private void Streak(int heads, float period, float half, Color color, float length, int tail)
            {
                tail = _lite ? Mathf.Min(tail, 2) : tail;
                for (var h = 0; h < heads; h++)
                {
                    var offset = (float)h / heads;
                    for (var k = tail; k >= 0; k--)
                    {
                        var image = Image(Art.Glow, Palette.WithAlpha(color, k == 0 ? 1f : 0.7f * (1f - (float)k / (tail + 1))), Unit);
                        var w = Band * length * (k == 0 ? 1.6f : 1.3f - 0.6f * k / tail);
                        image.rectTransform.sizeDelta = new Vector2(w * 1.6f, w * 0.7f);
                        var lag = k * 0.012f;
                        _steps.Add(time =>
                        {
                            var p = time / period + offset - lag;
                            Move(image, Path(p, half));
                            image.rectTransform.localRotation = Quaternion.Euler(0f, 0f, TangentAngle(p));
                        });
                    }

                    var star = Image(Art2.Sparkle, Color.white, Band * 1.6f);
                    _steps.Add(time =>
                    {
                        Move(star, Path(time / period + offset, half));
                        star.rectTransform.localRotation = Quaternion.Euler(0f, 0f, time * 120f);
                    });
                }
            }

            /// <summary>Розетки по очереди вспыхивают крестом-искрой; на акценте все четыре подряд.</summary>
            private void RosetteFlashes(Color color, float beat)
            {
                var corners = Corners();
                for (var i = 0; i < corners.Length; i++)
                {
                    var star = Image(Art2.Sparkle, Color.clear, Band * 2.2f);
                    var glow = Image(Art.Glow, Color.clear, Band * 2.6f, Back);
                    Move(star, corners[i]);
                    Move(glow, corners[i]);
                    var index = i;
                    _steps.Add(time =>
                    {
                        var idle = Spike((time / 2.4f + index * 0.25f) % 1f, 0.05f, 0.2f);
                        var accent = Spike(Phase(time, beat, -index * 0.15f), 0.03f, 0.35f);
                        var f = Mathf.Max(idle, accent);
                        star.color = Palette.WithAlpha(color, f);
                        star.rectTransform.localScale = Vector3.one * (0.2f + 1.1f * f);
                        star.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f * f);
                        glow.color = Palette.WithAlpha(Palette.Hex("#FF6A8A"), 0.6f * f);
                    });
                }
            }

            /// <summary>Искры-кресты в точках: вспыхивают по очереди, каждая в свой момент.</summary>
            private void SparkleAt(Vector2[] points, Color color, float period, float scale)
            {
                for (var i = 0; i < points.Length; i++)
                {
                    var star = Image(Art2.Sparkle, Color.clear, Band * scale);
                    Move(star, points[i] + new Vector2(-Band * 0.25f, -Band * 0.25f));
                    var index = i;
                    _steps.Add(time =>
                    {
                        var f = Spike((time / period + index * 0.37f) % 1f, 0.06f, 0.25f);
                        star.color = Palette.WithAlpha(color, f);
                        star.rectTransform.localScale = Vector3.one * (0.15f + 1f * f);
                        star.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 90f * f);
                    });
                }
            }

            /// <summary>Свет на углах: дышит, каждый угол в своём ритме; на акценте (beat) вспыхивает.</summary>
            private void CornerGlow(Color color, float alpha, float period, float beat)
            {
                var corners = Corners();
                for (var i = 0; i < corners.Length; i++)
                {
                    var glow = Image(Art.Glow, Color.clear, Band * 3.4f);
                    Move(glow, corners[i]);
                    glow.transform.SetAsFirstSibling();
                    var index = i;
                    _steps.Add(time =>
                    {
                        var b = 0.5f + 0.5f * Mathf.Sin((time / period + index * 0.29f) * Mathf.PI * 2f);
                        var accent = Spike(Phase(time, beat, -index * 0.06f), 0.04f, 0.5f);
                        glow.color = Palette.WithAlpha(color, Mathf.Clamp01(alpha * (0.45f + 0.55f * b) + 0.5f * accent));
                        glow.rectTransform.localScale = Vector3.one * (0.85f + 0.2f * b + 0.5f * accent);
                    });
                }
            }

            /// <summary>Мягкое свечение позади рамки ворсистым краем: два слоя крутятся навстречу; boost — всплеск.</summary>
            private void Halo(Func<float, Color> color, Func<float, float> boost)
            {
                if (_lite)
                {
                    return;
                }

                var holder = Ui.PlaceCenter(Ui.Node(Back, "Halo"), C, C, _size * 1.4f, _size * 1.4f);
                var layers = new Image[2];
                for (var i = 0; i < 2; i++)
                {
                    layers[i] = Ui.Image(holder, "Fluff", Art.Fluff(i), Color.clear);
                    Ui.Stretch(layers[i].rectTransform);
                }

                _steps.Add(time =>
                {
                    var b = 0.5f + 0.5f * Mathf.Sin(time * 1.6f);
                    var k = boost(time);
                    var c = color(time);
                    for (var i = 0; i < 2; i++)
                    {
                        layers[i].color = Palette.WithAlpha(c, Mathf.Clamp01(0.26f + 0.14f * b + 0.4f * k));
                        layers[i].rectTransform.localRotation = Quaternion.Euler(0f, 0f, (i == 0 ? 12f : -9f) * time % 360f);
                    }

                    holder.localScale = Vector3.one * (0.95f + 0.06f * b + 0.12f * k);
                });
            }

            /// <summary>Ударная волна на акценте: рваное кольцо ворса расходится от рамки и гаснет.</summary>
            private void Shockwave(float beat, float offset, Color color, bool rainbow = false)
            {
                if (_lite)
                {
                    return;
                }

                var ring = Image(Art.FluffRing, Color.clear, _size * 1.3f, Back);
                _steps.Add(time =>
                {
                    var age = Phase(time, beat, offset) * beat / 0.9f;
                    if (age >= 1f || time < 0.01f)
                    {
                        ring.color = Color.clear;
                        return;
                    }

                    var c = rainbow ? Color.HSVToRGB(time * 0.3f % 1f, 0.5f, 1f) : color;
                    ring.rectTransform.localScale = Vector3.one * (0.8f + 0.45f * Ease.OutCubic(age));
                    ring.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 30f * age);
                    ring.color = Palette.WithAlpha(c, 0.75f * (1f - age));
                });
            }

            /// <summary>Вспышка в точке на акценте: резкая атака, мягкое затухание; behind — позади рамки.</summary>
            private void Flash(Vector2 at, float beat, float offset, Color color, float size, float alpha, bool behind = false)
            {
                var glow = Image(Art.Glow, Color.clear, size, behind ? Back : null);
                Move(glow, at);
                _steps.Add(time =>
                {
                    var f = time < 0.01f ? 0f : Spike(Phase(time, beat, offset), 0.015f, 0.12f);
                    glow.color = Palette.WithAlpha(color, alpha * f);
                    glow.rectTransform.localScale = Vector3.one * (0.6f + 0.6f * f);
                });
            }

            /// <summary>Падающая звезда раз в period: вытянутый светлый след и яркая голова, наискосок через верх рамки.</summary>
            private void ShootingStar(float period)
            {
                var trail = _lite ? 3 : 8;
                var parts = new Image[trail];
                for (var k = trail - 1; k >= 0; k--)
                {
                    parts[k] = Image(Art.Glow, Color.clear, Unit);
                    parts[k].rectTransform.sizeDelta = new Vector2(Band * 2.4f, Band * (0.9f - 0.5f * k / trail));
                }

                var head = Image(Art2.Sparkle, Color.clear, Band * 1.8f);
                var angle = -18f;
                _steps.Add(time =>
                {
                    var cycle = Mathf.FloorToInt(time / period);
                    var age = Phase(time, period, 0f) * period / 0.9f;
                    var visible = age < 1f && time > 0.01f;
                    var from = new Vector2(C - Mid - Band * 2f + R(cycle, 1, 3) * Band * 2f, C - Mid - Band * (1.5f + R(cycle, 1, 4)));
                    var to = new Vector2(C + Mid + Band * 2f, C - Mid + Band * (1.5f + R(cycle, 1, 5) * 2f));
                    var dir = (to - from).normalized;
                    var rot = Mathf.Atan2(-dir.y, dir.x) * Mathf.Rad2Deg;
                    for (var k = 0; k < trail; k++)
                    {
                        var t = Mathf.Clamp01(Ease.OutCubic(age) - k * 0.035f);
                        Move(parts[k], Vector2.Lerp(from, to, t));
                        parts[k].rectTransform.localRotation = Quaternion.Euler(0f, 0f, rot);
                        parts[k].color = visible ? Palette.WithAlpha(Palette.Hex("#DFFFFA"), (1f - (float)k / trail) * Fade(age, 0.05f, 0.4f)) : Color.clear;
                    }

                    Move(head, Vector2.Lerp(from, to, Ease.OutCubic(age)));
                    head.color = visible ? Palette.WithAlpha(Color.white, Fade(age, 0.05f, 0.4f)) : Color.clear;
                    head.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle + time * 200f);
                });
            }

            /// <summary>Солнечный блик: вспышка в правом верхнем углу и «блики линзы» по диагонали к центру.</summary>
            private void LensFlare(float period)
            {
                var corner = new Vector2(C + Mid, C - Mid);
                Flash(corner, period, 0f, Palette.Hex("#FFF2B0"), Band * 6f, 0.9f);
                var spots = new[] { (0.35f, 0.9f, "#FFE58A"), (0.6f, 0.6f, "#FFB8D0"), (0.85f, 1.2f, "#B8F0FF") };
                foreach (var (t, size, hex) in spots)
                {
                    var spot = Image(Art.Glow, Color.clear, Band * size * 2f);
                    Move(spot, Vector2.Lerp(corner, new Vector2(C - Mid * 0.4f, C + Mid * 0.4f), t));
                    var color = Palette.Hex(hex);
                    _steps.Add(time =>
                    {
                        var f = time < 0.01f ? 0f : Spike(Phase(time, period, -t * 0.05f), 0.03f, 0.25f);
                        spot.color = Palette.WithAlpha(color, 0.5f * f);
                    });
                }
            }

            // ---------- Математика ----------

            private Image Image(Sprite sprite, Color color, float size, RectTransform? parent = null, bool full = false)
            {
                var image = Ui.Image(parent ?? _layer, "Fx", sprite, color);
                if (full)
                {
                    Ui.Place(image.rectTransform, 0, 0, _size, _size);
                }
                else
                {
                    Ui.PlaceCenter(image.rectTransform, C, C, size, size);
                }

                return image;
            }

            private static void Move(Image image, Vector2 at) => image.rectTransform.anchoredPosition = new Vector2(at.x, -at.y);

            /// <summary>Повторяемая случайность частицы i в цикле cycle (k — номер параметра).</summary>
            private static float R(int i, int cycle, int k) => Draw.Hash(i * 131 + k * 17 + 3, cycle * 7 + k * 29 + 11);

            /// <summary>Где мы внутри периода акцента: 0 — только что был, дальше растёт до 1.</summary>
            private static float Phase(float time, float period, float offset)
            {
                var p = (time + offset) / period;
                return p - Mathf.Floor(p);
            }

            /// <summary>Вспышка: быстрая атака за attack, затухание за decay (доли периода).</summary>
            private static float Spike(float phase, float attack, float decay)
            {
                if (phase < attack)
                {
                    return phase / attack;
                }

                var t = (phase - attack) / decay;
                return t >= 1f ? 0f : (1f - t) * (1f - t);
            }

            /// <summary>Появление и угасание за жизнь: за долю fadeIn вначале и fadeOut в конце.</summary>
            private static float Fade(float age, float fadeIn, float fadeOut) => Mathf.Clamp01(age / fadeIn) * Mathf.Clamp01((1f - age) / fadeOut);

            /// <summary>Путь с сопротивлением воздуха: быстро вначале, плавно тормозит.</summary>
            private static float Drag(float t, float k) => (1f - Mathf.Exp(-k * t)) / k;

            /// <summary>Неровное пламя: три несоизмеримые волны, а не ровная пульсация.</summary>
            private static float Flicker(float time) => Mathf.Clamp01(0.55f + 0.22f * Mathf.Sin(time * 5.3f) + 0.15f * Mathf.Sin(time * 8.9f + 1.3f) + 0.08f * Mathf.Sin(time * 17.1f));

            /// <summary>Мерцание блёсток: у каждой свой ритм.</summary>
            private static float Twinkle(float time, int i) => 0.6f + 0.4f * Mathf.Sin(time * (9f + i) + i * 1.7f);

            /// <summary>Порыв ветра раз в period: плавно нарастает и стихает (знак — направление).</summary>
            private static float Gust(float time, float period)
            {
                var phase = Phase(time, period, 0f);
                return phase < 0.35f ? Mathf.Sin(phase / 0.35f * Mathf.PI) : 0f;
            }

            private static Color Ramp(float t, params Color[] stops)
            {
                t = Mathf.Clamp01(t) * (stops.Length - 1);
                var i = Mathf.Min(stops.Length - 2, Mathf.FloorToInt(t));
                return Color.Lerp(stops[i], stops[i + 1], t - i);
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

            /// <summary>Угол поворота (для uGUI) по направлению движения вдоль пути.</summary>
            private static float TangentAngle(float p)
            {
                p -= Mathf.Floor(p);
                return (int)(p * 4f) switch
                {
                    0 => 0f,
                    1 => -90f,
                    2 => 180f,
                    _ => 90f,
                };
            }

            private Vector2[] Corners() => new[]
            {
                new Vector2(C - Mid, C - Mid), new Vector2(C + Mid, C - Mid), new Vector2(C + Mid, C + Mid), new Vector2(C - Mid, C + Mid),
            };

            /// <summary>Светлячок i: свободный полёт вокруг своей точки на багете, медленный дрейф по кругу.</summary>
            private Vector2 FireflyAt(int i, float time)
            {
                var ph = R(i, 0, 3) * 6.28f;
                var p = (i + 0.5f) / 10f + R(i, 0, 1) * 0.08f + time * 0.008f;
                var swirl = new Vector2(
                    Mathf.Sin(time * (0.7f + R(i, 0, 2) * 0.5f) + ph) + 0.4f * Mathf.Sin(time * 1.9f + ph * 2f),
                    Mathf.Cos(time * (0.9f + R(i, 0, 4) * 0.5f) + ph * 1.3f) + 0.4f * Mathf.Cos(time * 2.3f + ph));
                return Path(p, Mid) + swirl * Band * 0.55f;
            }

            /// <summary>Синхронная вспышка: волна света обегает рамку за секунду раз в 5 с; огонёк загорается, когда она рядом.</summary>
            private float SyncWave(int i, int count, float time)
            {
                var phase = Phase(time, 5f, 0f) * 5f;
                var p = (i + 0.5f) / 10f + R(i, 0, 1) * 0.08f + time * 0.008f;
                p -= Mathf.Floor(p);
                var d = Mathf.Abs(phase - p * 1.2f);
                return time < 0.01f ? 0f : Mathf.Clamp01(1f - d / 0.18f);
            }
        }
    }
}
