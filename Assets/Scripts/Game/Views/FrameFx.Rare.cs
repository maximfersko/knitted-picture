#nullable enable

using UnityEngine;
using UnityEngine.UI;

namespace KnittedPicture.Game
{
    /// <summary>
    /// Живые редкие рамки: Косичка, Пуговки, Цветочки, Сердечки, Звёздная ночь и 13 рамок коллекций. Лестница
    /// редкости читается по силе эффекта: у редкой — одна тематическая живая идея (бабочки, следы лапок, пузырьки,
    /// дымок из труб…) и маленький акцент раз в несколько секунд; без свечения позади, ударных волн и больших залпов —
    /// они остаются эпическим и легендарным.
    /// </summary>
    public static partial class FrameFx
    {
        private sealed partial class Fx
        {
            /// <summary>Косичка: по вязаной косе бежит мягкий отблеск, с рамки слетают пушинки ворса.</summary>
            public void Cable()
            {
                Streak(1, 6f, Mid, Palette.Hex("#E4FFEE"), 1.2f, 4);
                Fluffs(Palette.Hex("#F2FFF6"), 7);
                SparkleAt(Corners(), Palette.Hex("#F4FFF8"), 3.4f, 1.3f);
            }

            /// <summary>
            /// Пуговки: блик бежит по пуговицам по кругу — каждая на миг вспыхивает и под ней загорается тёплый свет;
            /// раз в 6 с — быстрая волна: пуговицы подмигивают одна за другой.
            /// </summary>
            public void Buttons(Color shine)
            {
                var points = new[]
                {
                    new Vector2(C - Mid, C - Mid), new Vector2(C, C - Mid), new Vector2(C + Mid, C - Mid), new Vector2(C + Mid, C),
                    new Vector2(C + Mid, C + Mid), new Vector2(C, C + Mid), new Vector2(C - Mid, C + Mid), new Vector2(C - Mid, C),
                };
                for (var i = 0; i < points.Length; i++)
                {
                    var corner = i % 2 == 0;
                    var glow = Image(Art.Glow, Color.clear, Band * (corner ? 2.6f : 1.9f));
                    var star = Image(Art2.Sparkle, Color.clear, Band * (corner ? 1.6f : 1.2f));
                    Move(glow, points[i]);
                    Move(star, points[i] + new Vector2(-Band * 0.25f, -Band * 0.25f));
                    var index = i;
                    _steps.Add(time =>
                    {
                        var chase = Spike(Phase(time, 4f, -index * 0.5f), 0.03f, 0.12f);
                        var wave = Spike(Phase(time, 6f, -index * 0.06f), 0.01f, 0.05f);
                        var f = Mathf.Max(chase, wave);
                        glow.color = Palette.WithAlpha(shine, 0.55f * f);
                        star.color = Palette.WithAlpha(Color.white, f);
                        star.rectTransform.localScale = Vector3.one * (0.2f + 0.9f * f);
                        star.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 60f * f);
                    });
                }
            }

            /// <summary>Цветочки: с рамки слетают лепестки и переворачиваются; серединки цветов на углах искрятся, по очереди «распускаются» вспышкой.</summary>
            public void Blossoms()
            {
                var pinks = new[] { Color.white, Palette.Hex("#FFC2DD"), Palette.Hex("#FF8FC4") };
                Particles(Count(6), 7f, Art2.FrameMote(1), (int i, int cycle, float age, float time, ref Spark s) =>
                {
                    var side = R(i, cycle, 1) < 0.5f ? -1f : 1f;
                    var spin = time * (1.5f + R(i, cycle, 2)) + R(i, cycle, 3) * 6f;
                    s.Pos = new Vector2(C + side * (Mid + (R(i, cycle, 4) - 0.4f) * Band * 1.4f) + Mathf.Sin(spin) * Band * 0.8f, -_size * 0.05f + age * _size * 1.08f);
                    s.Size = Band * (0.7f + 0.3f * R(i, cycle, 5));
                    s.ScaleX = Mathf.Cos(spin * 1.3f);
                    s.Rot = Mathf.Sin(spin) * 50f;
                    s.Color = Palette.WithAlpha(pinks[i % pinks.Length], Fade(age, 0.08f, 0.18f));
                });
                var corners = Corners();
                for (var i = 0; i < 4; i++)
                {
                    Flash(corners[i], 6f, -i * 1.5f, Palette.Hex("#FFE7F2"), Band * 3.2f, 0.75f);
                }

                SparkleAt(corners, Palette.Hex("#FFF6B0"), 2.8f, 1.3f);
            }

            /// <summary>
            /// Сердечки: сердце наверху бьётся «тук-тук» (двойной пульс света), от него поднимаются сердечки и покачиваются;
            /// раз в 5 с — сердце выпускает веер маленьких сердечек.
            /// </summary>
            public void Hearts()
            {
                var top = new Vector2(C, C - Mid);
                var beat = Image(Art.Glow, Color.clear, Band * 3.6f);
                Move(beat, top);
                beat.transform.SetAsFirstSibling();
                _steps.Add(time =>
                {
                    var p = Phase(time, 1.6f, 0f);
                    var f = Mathf.Max(Spike(p, 0.03f, 0.12f), 0.7f * Spike(Mathf.Max(0f, p - 0.18f), 0.03f, 0.12f));
                    beat.color = Palette.WithAlpha(Palette.Hex("#FF5A7A"), 0.65f * f);
                    beat.rectTransform.localScale = Vector3.one * (0.8f + 0.4f * f);
                });
                var reds = new[] { Palette.Hex("#FF5A7A"), Palette.Hex("#FF9CB0"), Palette.Hex("#E8333A") };
                Particles(Count(6), 3f, Art2.FrameMote(4), (int i, int cycle, float age, float time, ref Spark s) =>
                {
                    var x = C + (R(i, cycle, 1) - 0.5f) * Mid * 1.6f;
                    s.Pos = new Vector2(x + Mathf.Sin(age * 6f + i) * Band * 0.6f, C - Mid - age * _size * 0.28f);
                    s.Size = Band * (0.8f + 0.4f * R(i, cycle, 2)) * (0.6f + 0.4f * Ease.Bump(age));
                    s.Rot = Mathf.Sin(age * 5f + i) * 15f;
                    s.Color = Palette.WithAlpha(reds[i % reds.Length], Fade(age, 0.1f, 0.4f));
                });
                Burst(Count(7), 5f, 0f, 1.4f, Art2.FrameMote(4), (int i, int cycle, float age, float time, ref Spark s) =>
                {
                    var angle = Mathf.Lerp(-160f, -20f, (i + 0.5f) / Count(7)) * Mathf.Deg2Rad;
                    var t = Drag(age * 1.4f, 2.5f);
                    s.Pos = top + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * _size * 0.45f * t;
                    s.Size = Band * 0.7f * (1f - 0.4f * age);
                    s.Color = Palette.WithAlpha(reds[(i + 1) % reds.Length], Fade(age, 0.03f, 0.5f));
                });
            }

            /// <summary>Звёздная ночь: звёзды вспыхивают в случайных местах багета, угловые звёзды искрятся, раз в 6 с — падающая звезда.</summary>
            public void StarryNight()
            {
                TwinkleField(Palette.Hex("#FFF2B0"), 9);
                SparkleAt(Corners(), Palette.Hex("#FFF6C8"), 2.6f, 1.8f);
                ShootingStar(6f);
            }

            /// <summary>Сад: над цветами порхают бабочки — машут крыльями, летят ленивыми петлями; цветы искрятся.</summary>
            public void Butterflies()
            {
                var colors = new[] { Palette.Hex("#FF9A3C"), Palette.Hex("#5EB8FF"), Palette.Hex("#FFD54A"), Palette.Hex("#FF7FB6") };
                Particles(_lite ? 2 : 4, Period, Art2.FrameMote(5), (int i, int cycle, float age, float time, ref Spark s) =>
                {
                    var at = FlyAt(i, time, 0.02f);
                    var next = FlyAt(i, time + 0.05f, 0.02f);
                    var dir = next - at;
                    s.Pos = at;
                    s.Size = Band * 1.8f;
                    s.ScaleX = 0.2f + 0.8f * Mathf.Abs(Mathf.Cos(time * (11f + i)));
                    s.Rot = Mathf.Atan2(-dir.y, dir.x) * Mathf.Rad2Deg - 90f;
                    s.Color = colors[i % colors.Length];
                }, false, 0f);
                SparkleAt(MotifPoints(), Palette.Hex("#FFF6B0"), 4f, 1f);
            }

            /// <summary>
            /// Зверята: по рамке кто-то идёт — появляются светлые следы лапок (как по муке), левая-правая, по часовой; свежий след «впечатывается»
            /// с лёгким пружинящим нажимом, старые бледнеют и исчезают.
            /// </summary>
            public void PawPrints()
            {
                var steps = _lite ? 5 : 9;
                Particles(steps, Period, Art2.FrameMote(6), (int i, int cycle, float age, float time, ref Spark s) =>
                {
                    var walk = Mathf.Floor(time * 2.2f);
                    var stepIndex = walk - i;
                    var p = stepIndex * 0.018f;
                    var at = Path(p, Mid);
                    var inward = (new Vector2(C, C) - at).normalized;
                    var foot = ((int)stepIndex & 1) == 0 ? 1f : -1f;
                    s.Pos = at + inward * foot * Band * 0.28f;
                    var fresh = time * 2.2f - walk;
                    var press = i == 0 ? 1f + 0.35f * (1f - Ease.OutCubic(Mathf.Clamp01(fresh * 3f))) : 1f;
                    s.Size = Band * 0.95f * press;
                    s.Rot = TangentAngle(p) - 90f;
                    s.Color = Palette.WithAlpha(Palette.Hex("#FFF4E2"), 0.95f * (1f - (float)i / steps) * (i == 0 ? Mathf.Clamp01(fresh * 4f) : 1f));
                }, false, 0f);
            }

            /// <summary>Вкусняшки: по бокам сыплется цветная посыпка; раз в 5 с — вишенки на углах подпрыгивают светом, сверху залп посыпки.</summary>
            public void Sprinkles()
            {
                var candy = new[] { Palette.Hex("#FF5A7A"), Palette.Hex("#5EB8FF"), Palette.Hex("#FFD54A"), Palette.Hex("#7ED957"), Color.white };
                Particles(Count(12), 4f, Art.Glow, (int i, int cycle, float age, float time, ref Spark s) =>
                {
                    var side = R(i, cycle, 1) < 0.5f ? -1f : 1f;
                    s.Pos = new Vector2(C + side * (Mid + (R(i, cycle, 2) - 0.4f) * Band * 1.6f), -_size * 0.05f + age * _size * 1.08f);
                    s.Size = Band * 0.75f;
                    s.ScaleX = 0.3f;
                    s.Rot = time * 200f * (R(i, cycle, 3) - 0.5f) + R(i, cycle, 4) * 180f;
                    s.Color = Palette.WithAlpha(candy[(i + cycle) % candy.Length], Fade(age, 0.06f, 0.15f));
                });
                var corners = Corners();
                for (var i = 0; i < 4; i++)
                {
                    Flash(corners[i], 5f, -i * 0.08f, Palette.Hex("#FF8090"), Band * 3f, 0.6f);
                }

                var top = new Vector2(C, C - Mid);
                Burst(Count(12), 5f, 0f, 1.6f, Art.Glow, (int i, int cycle, float age, float time, ref Spark s) =>
                {
                    var angle = Mathf.Lerp(-170f, -10f, R(i, cycle, 1)) * Mathf.Deg2Rad;
                    var t = age * 1.6f;
                    s.Pos = top + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * _size * (0.3f + 0.2f * R(i, cycle, 2)) * t + new Vector2(0f, 0.5f * _size * t * t);
                    s.Size = Band * 0.7f;
                    s.ScaleX = 0.3f;
                    s.Rot = age * 600f * (R(i, cycle, 3) - 0.5f);
                    s.Color = Palette.WithAlpha(candy[i % candy.Length], Fade(age, 0.02f, 0.4f));
                });
            }

            /// <summary>Море: по бокам поднимаются пузырьки, покачиваются и лопаются искрой; по волнам бежит солнечный отблеск.</summary>
            public void Sea()
            {
                Streak(2, 5f, Mid, Palette.Hex("#E8FBFF"), 1.1f, 4);
                Particles(Count(9), 3.4f, Art2.FrameMote(7), (int i, int cycle, float age, float time, ref Spark s) =>
                {
                    var side = R(i, cycle, 1) < 0.5f ? -1f : 1f;
                    var x = C + side * (Mid + (R(i, cycle, 2) - 0.5f) * Band * 1.2f) + Mathf.Sin(age * 9f + i) * Band * 0.35f;
                    s.Pos = new Vector2(x, C + Mid + Band * 0.4f - age * _size * 0.85f);
                    var pop = age > 0.92f ? 1f + (age - 0.92f) * 8f : 1f;
                    s.Size = Band * (0.45f + 0.5f * R(i, cycle, 3)) * (0.6f + 0.5f * age) * pop;
                    s.Color = Palette.WithAlpha(Color.white, 0.9f * Fade(age, 0.08f, 0.08f));
                });
            }

            /// <summary>Небо: облачка плывут по верхней и нижней планкам с разной скоростью (параллакс), в углу поблёскивает солнце.</summary>
            public void Sky()
            {
                Particles(Count(6), Period, Art.Fluff(0), (int i, int cycle, float age, float time, ref Spark s) =>
                {
                    var bottom = i % 2 == 1;
                    var speed = 0.02f + 0.015f * R(i, 0, 1);
                    var u = (time * speed + R(i, 0, 2)) % 1f;
                    var x = C - Mid - Band * 2f + u * (Mid * 2f + Band * 4f);
                    s.Pos = new Vector2(x, (bottom ? C + Mid : C - Mid) + Mathf.Sin(time * 0.8f + i) * Band * 0.15f);
                    s.Size = Band * (1.8f + 0.8f * R(i, 0, 3));
                    s.ScaleX = 1.4f;
                    s.Rot = time * 4f;
                    s.Color = Palette.WithAlpha(Color.white, 0.85f * Fade(u, 0.12f, 0.12f));
                }, false, 0f);
                var sun = new Vector2(C + Mid, C - Mid);
                var rays = Image(Art.Rays, Color.clear, Band * 4.6f);
                Move(rays, sun);
                rays.transform.SetAsFirstSibling();
                _steps.Add(time =>
                {
                    rays.rectTransform.localRotation = Quaternion.Euler(0f, 0f, time * 15f);
                    rays.color = Palette.WithAlpha(Palette.Hex("#FFE58A"), 0.45f + 0.15f * Mathf.Sin(time * 1.5f));
                });
                SparkleAt(new[] { sun }, Color.white, 3f, 1.8f);
            }

            /// <summary>Лес: сквозь кроны пробиваются лучи (медленно поворачиваются), падают зелёные листочки, порыв раз в 8 с.</summary>
            public void Forest()
            {
                foreach (var corner in new[] { new Vector2(C - Mid, C - Mid), new Vector2(C + Mid, C - Mid) })
                {
                    var rays = Image(Art.Rays, Color.clear, Band * 4.2f);
                    Move(rays, corner);
                    rays.transform.SetAsFirstSibling();
                    var phase = corner.x;
                    _steps.Add(time =>
                    {
                        rays.rectTransform.localRotation = Quaternion.Euler(0f, 0f, time * 10f + phase);
                        rays.color = Palette.WithAlpha(Palette.Hex("#EFFFB0"), 0.3f + 0.15f * Mathf.Sin(time * 1.2f + phase));
                    });
                }

                var greens = new[] { Palette.Hex("#7ED957"), Palette.Hex("#4E9C3A"), Palette.Hex("#B6E36A") };
                Particles(Count(6), 8f, Art2.FrameMote(2), (int i, int cycle, float age, float time, ref Spark s) =>
                {
                    var side = R(i, cycle, 1) < 0.5f ? -1f : 1f;
                    var swing = Mathf.Sin(time * (1.3f + R(i, cycle, 2) * 0.6f) + i);
                    s.Pos = new Vector2(C + side * (Mid + (R(i, cycle, 3) - 0.4f) * Band * 1.4f) + swing * Band + Gust(time, 8f) * Band * 2f * Mathf.Sin(age * Mathf.PI),
                        -_size * 0.05f + age * _size * 1.08f);
                    s.Size = Band * (0.7f + 0.3f * R(i, cycle, 4));
                    s.Rot = swing * 45f;
                    s.ScaleX = Mathf.Cos(time * (0.9f + R(i, cycle, 5)) + i);
                    s.Color = Palette.WithAlpha(greens[(i + cycle) % greens.Length], Fade(age, 0.08f, 0.18f));
                });
            }

            /// <summary>Праздник: звёзды на рамке перемигиваются, раз в 4 с из верхних углов хлопушками вылетает конфетти.</summary>
            public void Party()
            {
                SparkleAt(MotifPoints(), Palette.Hex("#FFF2A0"), 2.2f, 1.1f);
                var colors = new[] { Palette.Hex("#FF5A7A"), Palette.Hex("#5EB8FF"), Palette.Hex("#FFD54A"), Palette.Hex("#7ED957"), Palette.Hex("#B58CFF") };
                for (var side = 0; side < 2; side++)
                {
                    var origin = side == 0 ? new Vector2(C - Mid, C - Mid) : new Vector2(C + Mid, C - Mid);
                    var dir = side == 0 ? 1f : -1f;
                    Flash(origin, 4f, -side * 0.1f, Color.white, Band * 3.4f, 0.8f);
                    Burst(Count(10), 4f, -side * 0.1f, 1.8f, Art.Glow, (int i, int cycle, float age, float time, ref Spark s) =>
                    {
                        var angle = Mathf.Lerp(-80f, 10f, R(i, cycle, 1)) * Mathf.Deg2Rad;
                        var t = Drag(age * 1.8f, 1.8f);
                        s.Pos = origin + new Vector2(Mathf.Cos(angle) * dir, Mathf.Sin(angle)) * _size * (0.35f + 0.3f * R(i, cycle, 2)) * t + new Vector2(0f, 0.3f * _size * age * age);
                        s.Size = Band * 0.8f;
                        s.ScaleX = 0.35f + 0.65f * Mathf.Abs(Mathf.Cos(age * 12f + i));
                        s.Rot = age * 500f * (R(i, cycle, 3) - 0.5f);
                        s.Color = Palette.WithAlpha(colors[(i + side) % colors.Length], Fade(age, 0.02f, 0.35f));
                    });
                }
            }

            /// <summary>Уют: в окошках домиков тёплый свет мерцает, из угловых домиков вьётся дымок — клубы растут, плывут и тают.</summary>
            public void Cosy()
            {
                CornerGlow(Palette.Hex("#FFC060"), 0.5f, 2.6f, 9f);
                var corners = Corners();
                Particles(Count(8), 3.2f, Art.Fluff(1), (int i, int cycle, float age, float time, ref Spark s) =>
                {
                    var corner = corners[i % 2];
                    s.Pos = corner + new Vector2(Band * 0.5f + Mathf.Sin(age * 4f + i) * Band * 0.4f + age * Band * 0.8f, -Band * 0.8f - age * Band * 3.2f);
                    s.Size = Band * (0.6f + 1.4f * age);
                    s.Rot = age * 90f + i * 40f;
                    s.Color = Palette.WithAlpha(Palette.Hex("#E9E4DE"), 0.75f * Fade(age, 0.1f, 0.6f));
                });
            }

            /// <summary>Друзья: вокруг рамки плавают сердечки; раз в 5 с две искры бегут навстречу и встречаются наверху «дай пять» — вспышка.</summary>
            public void Friends()
            {
                var warm = new[] { Palette.Hex("#FF8A6A"), Palette.Hex("#FFB08A"), Palette.Hex("#FF5A7A") };
                Particles(Count(7), 3.6f, Art2.FrameMote(4), (int i, int cycle, float age, float time, ref Spark s) =>
                {
                    var at = Path(R(i, cycle, 1), Mid + (R(i, cycle, 2) - 0.5f) * Band);
                    s.Pos = at + new Vector2(Mathf.Sin(age * 5f + i) * Band * 0.4f, -age * Band * 1.8f);
                    s.Size = Band * 0.8f * Ease.Bump(Mathf.Clamp01(age * 1.2f));
                    s.Rot = Mathf.Sin(age * 6f + i) * 15f;
                    s.Color = Palette.WithAlpha(warm[i % warm.Length], Fade(age, 0.1f, 0.35f));
                });
                for (var h = 0; h < 2; h++)
                {
                    var dir = h == 0 ? 1f : -1f;
                    var star = Image(Art2.Sparkle, Color.clear, Band * 1.5f);
                    _steps.Add(time =>
                    {
                        var p = Phase(time, 5f, 0f);
                        var run = Mathf.Clamp01(p / 0.4f);
                        Move(star, Path(0.625f + dir * 0.5f * (1f - run), Mid));
                        star.color = Palette.WithAlpha(Color.white, p < 0.4f && time > 0.01f ? 0.9f : 0f);
                        star.rectTransform.localRotation = Quaternion.Euler(0f, 0f, time * 150f * dir);
                    });
                }

                Flash(new Vector2(C, C - Mid), 5f, -2f, Palette.Hex("#FFE0C0"), Band * 4f, 0.85f);
            }

            /// <summary>Мастерская: по металлу пробегает холодный блик; на углах щёлкают спицы — по очереди короткие искры.</summary>
            public void Workshop()
            {
                Streak(1, 4.5f, Mid, Palette.Hex("#F4F8FF"), 1.4f, 5);
                var corners = Corners();
                for (var c = 0; c < 4; c++)
                {
                    var corner = corners[c];
                    Burst(Count(5), 4.8f, -c * 1.2f, 0.5f, Art.Glow, (int i, int cycle, float age, float time, ref Spark s) =>
                    {
                        var angle = R(i, cycle, 1) * Mathf.PI * 2f;
                        s.Pos = corner + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Band * 1.4f * Ease.OutCubic(age);
                        s.Size = Band * 0.4f * (1f - age);
                        s.Color = Palette.WithAlpha(Palette.Hex("#FFE58A"), 1f - age);
                    });
                }

                Fluffs(Palette.Hex("#FFF4E0"), 4);
            }

            /// <summary>Путешествия: огонёк-путник обходит рамку со светящимся следом; на каждом круге компас на углу вспыхивает.</summary>
            public void Journey()
            {
                var lap = 7f;
                var tail = _lite ? 3 : 8;
                Particles(tail, Period, Art.Glow, (int i, int cycle, float age, float time, ref Spark s) =>
                {
                    s.Pos = Path(time / lap - i * 0.012f, Mid);
                    var k = 1f - (float)i / tail;
                    s.Size = Band * (0.5f + 0.9f * k);
                    s.Color = Palette.WithAlpha(Color.Lerp(Palette.Hex("#5FE3D0"), Palette.Hex("#FFD34D"), k), 0.85f * k);
                }, false, 0f);
                var head = Image(Art2.Sparkle, Color.white, Band * 1.4f);
                _steps.Add(time =>
                {
                    Move(head, Path(time / lap, Mid));
                    head.rectTransform.localRotation = Quaternion.Euler(0f, 0f, time * 140f);
                });
                foreach (var corner in Corners())
                {
                    Flash(corner, lap, 0f, Palette.Hex("#FFE58A"), Band * 3f, 0.7f);
                }
            }

            /// <summary>Космос: вокруг угловых планет кружат спутники (за планетой — тусклее), звёзды мерцают, раз в 5 с — падающая звезда.</summary>
            public void Space()
            {
                var corners = Corners();
                Particles(_lite ? 4 : 8, Period, Art.Glow, (int i, int cycle, float age, float time, ref Spark s) =>
                {
                    var corner = corners[i % 4];
                    var a = time * (1.6f + 0.4f * (i / 4)) + i * 1.3f;
                    var front = Mathf.Sin(a) > 0f;
                    s.Pos = corner + new Vector2(Mathf.Cos(a) * Band * 1.25f, Mathf.Sin(a) * Band * 0.45f);
                    s.Size = Band * (front ? 0.75f : 0.5f);
                    s.Color = Palette.WithAlpha(i % 2 == 0 ? Palette.Hex("#D9C2FF") : Palette.Hex("#9FE8FF"), front ? 1f : 0.35f);
                }, false, 0f);
                TwinkleField(Palette.Hex("#EDE4FF"), 7);
                ShootingStar(5f);
            }

            // ---------- Помощники редких рамок ----------

            /// <summary>Пушинки ворса: отрываются от багета, медленно плывут вверх и тают.</summary>
            private void Fluffs(Color color, int count)
            {
                Particles(Count(count), 3.6f, Art.Glow, (int i, int cycle, float age, float time, ref Spark s) =>
                {
                    var at = Path(R(i, cycle, 1), Mid + (R(i, cycle, 2) - 0.5f) * Band);
                    s.Pos = at + new Vector2(Mathf.Sin(age * 4f + i) * Band * 0.5f, -age * Band * 1.6f);
                    s.Size = Band * (0.45f + 0.3f * R(i, cycle, 3));
                    s.Color = Palette.WithAlpha(color, 0.8f * Fade(age, 0.15f, 0.4f));
                });
            }

            /// <summary>Звёзды на багете: вспыхивают и гаснут в случайных местах, каждая в свой момент.</summary>
            private void TwinkleField(Color color, int count)
            {
                Particles(Count(count), 2.4f, Art2.Sparkle, (int i, int cycle, float age, float time, ref Spark s) =>
                {
                    s.Pos = Path(R(i, cycle, 1), Mid + (R(i, cycle, 2) - 0.5f) * Band);
                    s.Size = Band * (0.5f + 0.6f * R(i, cycle, 3)) * Ease.Bump(age);
                    s.Rot = 45f * R(i, cycle, 4);
                    s.Color = Palette.WithAlpha(color, Ease.Bump(age));
                });
            }

            /// <summary>Точки мелких мотивов рамок коллекций: по пять на каждой стороне (шаг 9 точек растра).</summary>
            private Vector2[] MotifPoints()
            {
                var k = _size / FrameRaster.Size;
                var points = new Vector2[20];
                for (var j = 0; j < 5; j++)
                {
                    var o = (j - 2) * 9f * k;
                    points[j] = new Vector2(C + o, C - Mid);
                    points[5 + j] = new Vector2(C + Mid, C + o);
                    points[10 + j] = new Vector2(C - o, C + Mid);
                    points[15 + j] = new Vector2(C - Mid, C - o);
                }

                return points;
            }

            /// <summary>Полёт бабочки: медленно по кругу рамки с ленивыми петлями поперёк.</summary>
            private Vector2 FlyAt(int i, float time, float speed)
            {
                var p = i * 0.27f + time * speed;
                var loop = new Vector2(Mathf.Sin(time * (0.9f + 0.2f * i) + i), Mathf.Sin(time * (1.4f + 0.15f * i) + i * 2f)) * Band * 0.9f;
                return Path(p, Mid) + loop;
            }
        }
    }
}
