#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KnittedPicture.Game
{
    /// <summary>
    /// Визуальные эффекты: нитка-клубочек летит от клубка к петле, искры, «пуф» пустого клубка,
    /// всплывающий текст. Маленькие кружки берутся из пула — их сотни в секунду.
    /// </summary>
    public static class Fx
    {
        private static readonly Stack<Image> Pool = new Stack<Image>();

        /// <summary>Сколько мировых единиц в одной точке макета у этого слоя.</summary>
        private static float Unit(RectTransform layer) => layer.lossyScale.x;

        /// <summary>Хвост нитки: сколько точек тянется за клубочком и на какую долю пути каждая отстаёт.</summary>
        private const int TailLength = 5;
        private const float TailLag = 0.06f;

        /// <summary>Чередуем сторону дуги: соседние нитки расходятся веером, а не летят одна в одну.</summary>
        private static int _side = 1;

        /// <summary>
        /// Нитка от клубка к петле: клубочек выпрыгивает из клубка, летит по дуге (то левее, то правее),
        /// покачиваясь, тянет за собой тающий хвост нитки и смазку скорости; у петли ныряет в неё,
        /// и по ткани расходится колечко.
        /// </summary>
        public static void Bead(RectTransform layer, Vector3 from, Vector3 to, Color color, float size, float duration, Action? arrived)
        {
            if (Tweens.Calm)
            {
                // Тот же момент посадки, одна спокойная метка вместо полёта с хвостом.
                var mark = Dot(layer, Palette.WithAlpha(color, 0.25f), size, Art.Ring);
                mark.rectTransform.position = to;
                Tweens.Run(mark, duration, t => mark.color = Palette.WithAlpha(color, 0.25f + 0.35f * t),
                    null, () =>
                    {
                        Release(mark);
                        arrived?.Invoke();
                    });
                return;
            }

            var unit = Unit(layer);
            _side = -_side;
            var swing = _side * (26f + UnityEngine.Random.value * 34f) * unit;
            var rise = (60f + UnityEngine.Random.value * 30f) * unit;
            var c1 = from + new Vector3(swing, rise, 0f);
            var c2 = to + new Vector3(-swing * 0.35f, rise * 0.75f, 0f);
            var wobble = (UnityEngine.Random.value < 0.5f ? -1f : 1f) * (14f + UnityEngine.Random.value * 10f);

            // Хвост: точки цвета нитки, всё мельче и прозрачнее; появляются, как только клубочек отлетел.
            var tail = new Image[TailLength];
            for (var i = 0; i < TailLength; i++)
            {
                var k = (i + 1f) / (TailLength + 1f);
                tail[i] = Dot(layer, Palette.WithAlpha(color, 0.55f * (1f - k)), size * Mathf.Lerp(0.62f, 0.22f, k));
                tail[i].rectTransform.position = from;
                tail[i].gameObject.SetActive(false);
            }

            // Смазка скорости: вытянутый мазок под клубочком по направлению полёта.
            var streak = Dot(layer, Palette.WithAlpha(Color.Lerp(color, Color.white, 0.35f), 0f), size);
            var head = Dot(layer, Color.white, size * 1.15f, Art2.YarnBall(color));
            head.rectTransform.position = from;
            head.rectTransform.localScale = Vector3.one * 0.4f;

            Tweens.Run(head, duration, t =>
            {
                var e = Ease.InOutCubic(t);
                var p = Cubic(from, c1, c2, to, e);
                head.rectTransform.position = p;

                // Выпрыгнул из клубка с перелётом, в полёте чуть дышит, у петли сжимается и ныряет.
                var scale = t < 0.18f ? Mathf.Lerp(0.4f, 1.12f, Ease.OutCubic(t / 0.18f))
                    : t < 0.8f ? 1.12f - 0.08f * Ease.Bump((t - 0.18f) / 0.62f)
                    : Mathf.Lerp(1.04f, 0.55f, (t - 0.8f) / 0.2f);
                head.rectTransform.localScale = Vector3.one * scale;
                head.rectTransform.localRotation = Quaternion.Euler(0f, 0f, wobble * Mathf.Sin(t * Mathf.PI * 2f));

                // Скорость по времени: в середине пути мазок длиннее и ярче, у клубка и у петли его нет.
                var ahead = Cubic(from, c1, c2, to, Ease.InOutCubic(Mathf.Min(1f, t + 0.03f)))
                    - Cubic(from, c1, c2, to, Ease.InOutCubic(Mathf.Max(0f, t - 0.03f)));
                var speed = ahead.magnitude / unit;
                streak.rectTransform.position = p - ahead * 0.35f;
                streak.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(ahead.y, ahead.x) * Mathf.Rad2Deg);
                streak.rectTransform.localScale = new Vector3(1f + Mathf.Min(1.6f, speed / 30f), 0.5f, 1f) * scale;
                streak.color = Palette.WithAlpha(streak.color, Mathf.Min(0.45f, speed / 60f));

                for (var i = 0; i < TailLength; i++)
                {
                    var back = e - (i + 1) * TailLag;
                    tail[i].gameObject.SetActive(back > 0f && t < 0.97f);
                    if (back > 0f)
                    {
                        tail[i].rectTransform.position = Cubic(from, c1, c2, to, back);
                    }
                }
            }, null, () =>
            {
                Release(head);
                Release(streak);
                foreach (var dot in tail)
                {
                    Release(dot);
                }

                Ripple(layer, to, color, size);
                arrived?.Invoke();
            });
        }

        /// <summary>Колечко по ткани там, куда села петля.</summary>
        private static void Ripple(RectTransform layer, Vector3 at, Color color, float size)
        {
            var ring = Dot(layer, Palette.WithAlpha(Color.Lerp(color, Color.white, 0.4f), 0.8f), size * 1.1f, Art.Ring);
            ring.rectTransform.position = at;
            var tint = ring.color;
            Tweens.Run(ring, 0.24f, t =>
            {
                ring.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.5f, 2.1f, t);
                ring.color = Palette.WithAlpha(tint, 0.8f * (1f - t));
            }, Ease.OutCubic, () => Release(ring));
        }

        /// <summary>Искры: несколько точек разлетаются и гаснут.</summary>
        public static void Sparkle(RectTransform layer, Vector3 at, Color color, int count = 3, float radius = 18f)
        {
            if (Tweens.Calm)
            {
                if (count > 0)
                {
                    var mark = Dot(layer, color, 10f, Art2.Sparkle);
                    mark.rectTransform.position = at;
                    Tweens.Run(mark, 0.3f, t => mark.color = Palette.WithAlpha(color, 1f - t), null, () => Release(mark));
                }
                return;
            }

            var unit = Unit(layer);
            for (var i = 0; i < count; i++)
            {
                var angle = (i / (float)count + UnityEngine.Random.value * 0.3f) * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0);
                // Искра-звёздочка: вспыхивает, разлетается с поворотом и гаснет.
                var dot = Dot(layer, Color.Lerp(color, Color.white, 0.2f), 10f, Art2.Sparkle);
                var reach = radius * (0.7f + UnityEngine.Random.value * 0.6f) * unit;
                var spin = (UnityEngine.Random.value < 0.5f ? -1f : 1f) * (60f + UnityEngine.Random.value * 60f);
                var tint = dot.color;
                Tweens.Run(dot, 0.36f, t =>
                {
                    dot.rectTransform.position = at + dir * reach * t;
                    dot.color = Palette.WithAlpha(tint, 1f - t * t);
                    dot.rectTransform.localScale = Vector3.one * (t < 0.2f ? Mathf.Lerp(0.3f, 1.25f, t / 0.2f) : Mathf.Lerp(1.25f, 0.2f, (t - 0.2f) / 0.8f));
                    dot.rectTransform.localRotation = Quaternion.Euler(0f, 0f, spin * t);
                }, Ease.OutCubic, () => Release(dot));
            }
        }

        /// <summary>
        /// Яркий хлопок пряжи: два цветных кольца, звёзды и закрученные волокна.
        /// Все элементы из общего пула; спокойный режим оставляет одну гаснущую звезду.
        /// </summary>
        public static void YarnBurst(RectTransform layer, Vector3 at, Color primary, Color accent, float radius)
        {
            if (Tweens.Calm)
            {
                Sparkle(layer, at, primary, 1);
                return;
            }

            var unit = Unit(layer);
            for (var ringIndex = 0; ringIndex < 2; ringIndex++)
            {
                var tint = ringIndex == 0 ? primary : accent;
                var ring = Dot(layer, tint, radius * 1.5f, Art.Ring);
                ring.rectTransform.position = at;
                ring.color = Palette.WithAlpha(tint, 0f);
                Tweens.Run(ring, 0.46f, t =>
                {
                    ring.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.2f, 1.5f, Ease.OutCubic(t));
                    ring.color = Palette.WithAlpha(tint, 0.55f * (1f - t) * (1f - t));
                }, null, () => Release(ring), ringIndex * 0.07f);
            }

            const int count = 10;
            for (var i = 0; i < count; i++)
            {
                var star = i % 5 == 0;
                var tint = i % 2 == 0 ? primary : accent;
                var angle = (i + UnityEngine.Random.value * 0.35f) / count * Mathf.PI * 2f;
                var direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                var tangent = new Vector3(-direction.y, direction.x, 0f);
                var reach = radius * (0.65f + UnityEngine.Random.value * 0.5f) * unit;
                var curl = (i % 2 == 0 ? 1f : -1f) * radius * 0.22f * unit;
                var size = star ? 14f : 6f;
                var particle = Dot(layer, tint, size, star ? Art2.Sparkle : Art.Circle);
                particle.rectTransform.position = at;
                Tweens.Run(particle, 0.6f, t =>
                {
                    var travel = Ease.OutCubic(t);
                    particle.rectTransform.position = at + direction * reach * travel +
                        tangent * curl * Mathf.Sin(t * Mathf.PI) + new Vector3(0f, -radius * unit * 0.3f * t * t, 0f);
                    particle.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg + t * 140f);
                    var scale = (0.5f + 0.75f * Ease.Bump(t)) * (1f - 0.65f * t);
                    particle.rectTransform.localScale = new Vector3(star ? scale : scale * 2.6f, scale, 1f);
                    particle.color = Palette.WithAlpha(tint, Mathf.Clamp01((1f - t) * 2f));
                }, null, () => Release(particle));
            }
        }

        /// <summary>Развязанные кончики или распускающийся клубок: нити расходятся дугами и утончаются.</summary>
        public static void LooseThreads(RectTransform layer, Vector3 at, Color color, float size, bool unravel = false)
        {
            if (Tweens.Calm)
            {
                Sparkle(layer, at, color, 1);
                return;
            }
            var unit = Unit(layer);
            var count = unravel ? 6 : 2;
            for (var i = 0; i < count; i++)
            {
                var side = i % 2 == 0 ? -1f : 1f;
                var reach = size * unit * (0.45f + i * 0.08f);
                var thread = Dot(layer, color, size * 0.22f);
                thread.rectTransform.position = at;
                Tweens.Run(thread, 0.48f, t =>
                {
                    thread.rectTransform.position = at + new Vector3(side * reach * Ease.OutCubic(t),
                        reach * (Mathf.Sin(t * Mathf.PI) * 0.5f - (unravel ? t * t : 0f)), 0f);
                    thread.rectTransform.localRotation = Quaternion.Euler(0f, 0f, side * (30f + 150f * t));
                    thread.rectTransform.localScale = new Vector3(2.8f * (1f - t * 0.6f), 0.22f * (1f - t), 1f);
                    thread.color = Palette.WithAlpha(color, 1f - t);
                }, null, () => Release(thread));
            }
        }

        /// <summary>Ледяная оболочка раскалывается на отдельные грани, оставляя клубок видимым.</summary>
        public static void IceShatter(RectTransform layer, Vector3 at, Color color, float size)
        {
            if (Tweens.Calm)
            {
                Sparkle(layer, at, color, 1);
                return;
            }
            var unit = Unit(layer);
            for (var i = 0; i < 8; i++)
            {
                var angle = i * Mathf.PI / 4f;
                var direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                var shard = Dot(layer, i % 2 == 0 ? color : Color.white, size * 0.16f, Art.PlainCell);
                var tint = shard.color;
                shard.rectTransform.position = at + direction * size * 0.3f * unit;
                var start = shard.rectTransform.position;
                Tweens.Run(shard, 0.42f, t =>
                {
                    shard.rectTransform.position = start + direction * size * 0.55f * unit * Ease.OutCubic(t) -
                        new Vector3(0f, size * unit * t * t * 0.6f, 0f);
                    shard.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg + 100f * t);
                    shard.rectTransform.localScale = new Vector3(0.6f, 1.3f, 1f) * (1f - t * 0.65f);
                    shard.color = Palette.WithAlpha(tint, 1f - t);
                }, null, () => Release(shard));
            }
        }

        /// <summary>Нить между летящими партнёрами следует за обоими клубками, не перехватывая касания.</summary>
        public static void LinkedFlight(RectTransform layer, RectTransform first, RectTransform second, Color color, float duration)
        {
            if (Tweens.Calm)
            {
                return;
            }
            var thread = Dot(layer, color, 1f);
            thread.color = Color.clear;
            Tweens.Run(thread, duration, t =>
            {
                if (first == null || second == null)
                {
                    thread.color = Color.clear;
                    return;
                }
                var a = Ui.WorldCenter(first);
                var b = Ui.WorldCenter(second);
                var delta = b - a;
                thread.rectTransform.position = (a + b) * 0.5f;
                thread.rectTransform.sizeDelta = new Vector2(delta.magnitude / Mathf.Max(0.0001f, Unit(layer)), 2.5f);
                thread.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
                thread.color = Palette.WithAlpha(color, 0.75f * Mathf.Sin(t * Mathf.PI));
            }, null, () => Release(thread));
        }

        /// <summary>Пустой клубок: расходящееся кольцо и пух.</summary>
        public static void Poof(RectTransform layer, Vector3 at, Color color, float size)
        {
            // Кольцо берётся из общего пула кружков и возвращается в него: пустых клубков на уровне десятки.
            var image = Dot(layer, color, size, Art.Ring);
            var rt = image.rectTransform;
            rt.position = at;
            Tweens.Run(image, 0.35f, t =>
            {
                rt.localScale = Tweens.Calm ? Vector3.one : Vector3.one * (0.6f + 0.9f * t);
                image.color = Palette.WithAlpha(color, 1f - t);
            }, Ease.OutCubic, () => Release(image));
            if (!Tweens.Calm)
            {
                Sparkle(layer, at, color, 7, size * 0.9f);
            }
        }

        /// <summary>
        /// Полёт монетки в счётчик: сначала «выстреливает» из источника в точку burst, раздуваясь, потом летит
        /// по дуге (то левее, то правее) с разгоном, вращаясь, и у счётчика чуть сжимается. Цель спрашивается
        /// каждый кадр: под окном экран мог перестроиться. До старта (delay) монетка невидима.
        /// </summary>
        public static void CoinFlight(RectTransform coin, Vector3 from, Vector3 burst, Func<Vector3> target, float delay, Action arrived)
        {
            var unit = coin.parent is RectTransform layer ? Unit(layer) : 1f;
            _side = -_side;
            var bend = _side * (40f + UnityEngine.Random.value * 40f) * unit;
            var spin = _side * (160f + UnityEngine.Random.value * 120f);
            coin.position = from;
            coin.localScale = Vector3.zero;
            Tweens.Run(coin, 0.78f, t =>
            {
                Vector3 p;
                float scale;
                if (t < 0.24f)
                {
                    var k = Ease.OutCubic(t / 0.24f);
                    p = Vector3.LerpUnclamped(from, burst, k);
                    scale = Mathf.Lerp(0.2f, 1.18f, k);
                }
                else
                {
                    var k = Ease.InOutCubic((t - 0.24f) / 0.76f);
                    var end = target();
                    var line = end - burst;
                    var normal = new Vector3(-line.y, line.x, 0f).normalized;
                    var mid = (burst + end) / 2f + normal * bend;
                    var u = 1f - k;
                    p = u * u * burst + 2f * u * k * mid + k * k * end;
                    scale = Mathf.Lerp(1.18f, 0.72f, k * k);
                }

                coin.position = p;
                coin.localScale = Vector3.one * scale;
                coin.localRotation = Quaternion.Euler(0f, 0f, spin * Ease.OutCubic(t));
            }, null, arrived, delay);
        }

        /// <summary>
        /// Пух при посадке клубка: несколько мягких комочков цвета пряжи разлетаются в стороны по полу,
        /// чуть подпрыгивая, и тают.
        /// </summary>
        public static void Dust(RectTransform layer, Vector3 at, Color color)
        {
            if (Tweens.Calm)
            {
                return;
            }

            var unit = Unit(layer);
            var tint = Color.Lerp(color, Color.white, 0.55f);
            for (var i = 0; i < 6; i++)
            {
                var side = i % 2 == 0 ? -1f : 1f;
                var reach = (14f + UnityEngine.Random.value * 16f) * unit * side;
                var hop = (5f + UnityEngine.Random.value * 7f) * unit;
                var dot = Dot(layer, Palette.WithAlpha(tint, 0.8f), 5f + UnityEngine.Random.value * 4f);
                dot.rectTransform.position = at;
                Tweens.Run(dot, 0.38f, t =>
                {
                    dot.rectTransform.position = at + new Vector3(reach * Ease.OutCubic(t), hop * Ease.Bump(t), 0f);
                    dot.rectTransform.localScale = Vector3.one * (1f - 0.6f * t);
                    dot.color = Palette.WithAlpha(tint, 0.8f * (1f - t));
                }, null, () => Release(dot));
            }
        }

        /// <summary>Готовая всплывающая надпись: корень с тенью-текстом, текстом и прозрачностью.</summary>
        private sealed class Floater
        {
            public RectTransform Root = null!;
            public CanvasGroup Group = null!;
            public Text Label = null!;
            public Text Shadow = null!;
        }

        /// <summary>
        /// Пул по размеру и цвету: числа и тексты обновляются на готовой паре надписей.
        /// Цвет остаётся частью ключа, поскольку от него зависит тиснение, создаваемое Ui.Label.
        /// </summary>
        private static readonly Dictionary<(float Size, Color Color), Stack<Floater>> FloatPool =
            new Dictionary<(float Size, Color Color), Stack<Floater>>();
        private const int FloatPoolStyles = 32;

        /// <summary>Сколько одинаковых надписей держим про запас.</summary>
        private const int FloatPoolDepth = 12;

        /// <summary>Всплывающая надпись: подпрыгивает и уплывает вверх. Берётся из пула и возвращается в него.</summary>
        public static void FloatText(RectTransform layer, Vector3 at, string text, Color color, float size = 22f)
        {
            var key = (size, color);
            Floater? floater = null;
            if (FloatPool.TryGetValue(key, out var spare))
            {
                // Надписи со слоя, который уже уничтожен (ушли с экрана), пропадают вместе с ним: пропускаем.
                while (spare.Count > 0 && floater == null)
                {
                    var candidate = spare.Pop();
                    if (candidate.Root != null)
                    {
                        floater = candidate;
                    }
                }
            }

            if (floater == null)
            {
                var made = Ui.Node(layer, "FloatText");
                made.sizeDelta = new Vector2(240, 40);
                var shadow = Ui.Label(made, text, size, Palette.WithAlpha(Palette.Ink, 0.25f));
                Ui.Place(shadow.rectTransform, 1, 2, 240, 40);
                var label = Ui.Label(made, text, size, color);
                Ui.Place(label.rectTransform, 0, 0, 240, 40);
                var madeGroup = made.gameObject.AddComponent<CanvasGroup>();
                madeGroup.blocksRaycasts = false;
                floater = new Floater { Root = made, Group = madeGroup, Label = label, Shadow = shadow };
            }
            else
            {
                floater.Root.SetParent(layer, false);
                floater.Root.SetAsLastSibling();
                floater.Root.gameObject.SetActive(true);
            }

            var root = floater.Root;
            var group = floater.Group;
            floater.Label.text = text;
            floater.Shadow.text = text;
            root.position = at;
            root.localScale = Vector3.one;
            group.alpha = 1f;
            var unit = Unit(layer);
            Tweens.Run(root, 0.9f, t =>
            {
                root.position = Tweens.Calm ? at : at + new Vector3(0, 46f * unit * Ease.OutCubic(t), 0);
                root.localScale = Tweens.Calm ? Vector3.one : Vector3.one * (t < 0.2f ? Mathf.Lerp(0.5f, 1.15f, t / 0.2f) : Mathf.Lerp(1.15f, 1f, (t - 0.2f) / 0.8f));
                group.alpha = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
            }, null, () =>
            {
                if (root == null)
                {
                    return;
                }

                root.gameObject.SetActive(false);
                if (!FloatPool.TryGetValue(key, out var stack))
                {
                    if (FloatPool.Count >= FloatPoolStyles)
                    {
                        UnityEngine.Object.Destroy(root.gameObject);
                        return;
                    }
                    stack = new Stack<Floater>();
                    FloatPool[key] = stack;
                }

                if (stack.Count < FloatPoolDepth)
                {
                    stack.Push(floater);
                }
                else
                {
                    UnityEngine.Object.Destroy(root.gameObject);
                }
            });
        }

        private static Vector3 Cubic(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
        {
            var u = 1f - t;
            return u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d;
        }

        private static Image Dot(RectTransform layer, Color color, float size, Sprite? sprite = null)
        {
            Image? dot = null;
            while (Pool.Count > 0 && dot == null)
            {
                dot = Pool.Pop();
            }

            if (dot == null)
            {
                dot = Ui.Image(layer, "Dot", Art.Circle, color);
            }
            else
            {
                dot.rectTransform.SetParent(layer, false);
                dot.gameObject.SetActive(true);
            }

            dot.sprite = sprite ?? Art.Circle;
            dot.color = color;
            dot.rectTransform.sizeDelta = new Vector2(size, size);
            dot.rectTransform.localRotation = Quaternion.identity;
            dot.rectTransform.localScale = Vector3.one;
            dot.rectTransform.SetAsLastSibling();
            return dot;
        }

        private static void Release(Image dot)
        {
            if (dot == null)
            {
                return;
            }

            dot.gameObject.SetActive(false);
            Pool.Push(dot);
        }
    }
}
