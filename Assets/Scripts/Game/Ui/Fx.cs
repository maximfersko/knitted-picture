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
            var unit = Unit(layer);
            for (var i = 0; i < count; i++)
            {
                var angle = (i / (float)count + UnityEngine.Random.value * 0.3f) * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0);
                // Искра-звёздочка: вспыхивает, разлетается с поворотом и гаснет.
                var dot = Dot(layer, Color.Lerp(color, Color.white, 0.45f), 10f, Art2.Sparkle);
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

        /// <summary>Пустой клубок: расходящееся кольцо и пух.</summary>
        public static void Poof(RectTransform layer, Vector3 at, Color color, float size)
        {
            var image = Ui.Image(layer, "Ring", Art.Ring, color);
            var rt = image.rectTransform;
            UnityEngine.Object.Destroy(image.gameObject, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            rt.position = at;
            Tweens.Run(rt, 0.35f, t =>
            {
                rt.localScale = Vector3.one * (0.6f + 0.9f * t);
                image.color = Palette.WithAlpha(color, 1f - t);
            }, Ease.OutCubic);
            Sparkle(layer, at, color, 7, size * 0.9f);
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

        /// <summary>Всплывающая надпись: подпрыгивает и уплывает вверх.</summary>
        public static void FloatText(RectTransform layer, Vector3 at, string text, Color color, float size = 22f)
        {
            var root = Ui.Node(layer, "FloatText");
            root.sizeDelta = new Vector2(240, 40);
            root.position = at;
            var shadow = Ui.Label(root, text, size, Palette.WithAlpha(Palette.Ink, 0.25f));
            Ui.Place(shadow.rectTransform, 1, 2, 240, 40);
            var label = Ui.Label(root, text, size, color);
            Ui.Place(label.rectTransform, 0, 0, 240, 40);
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            var unit = Unit(layer);
            Tweens.Run(root, 0.9f, t =>
            {
                root.position = at + new Vector3(0, 46f * unit * Ease.OutCubic(t), 0);
                root.localScale = Vector3.one * (t < 0.2f ? Mathf.Lerp(0.5f, 1.15f, t / 0.2f) : Mathf.Lerp(1.15f, 1f, (t - 0.2f) / 0.8f));
                group.alpha = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
            }, null, () => UnityEngine.Object.Destroy(root.gameObject));
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
