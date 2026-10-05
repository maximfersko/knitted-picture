#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KnittedPicture.Game
{
    /// <summary>
    /// Визуальные эффекты: бусина нитки летит от клубка к петле, искры, «пуф» пустого клубка,
    /// всплывающий текст. Маленькие кружки берутся из пула — их сотни в секунду.
    /// </summary>
    public static class Fx
    {
        private static readonly Stack<Image> Pool = new Stack<Image>();

        /// <summary>Сколько мировых единиц в одной точке макета у этого слоя.</summary>
        private static float Unit(RectTransform layer) => layer.lossyScale.x;

        /// <summary>Нитка-бусина по дуге от клубка к петле, с коротким хвостом.</summary>
        public static void Bead(RectTransform layer, Vector3 from, Vector3 to, Color color, float size, float duration, Action? arrived)
        {
            var unit = Unit(layer);
            var lift = new Vector3(0, 70f * unit, 0);
            var control = (from + to) / 2f + lift;
            var head = Dot(layer, color, size);
            Tweens.Run(head, duration, t =>
            {
                head.rectTransform.position = Bezier(from, control, to, t);
                head.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, 0.6f, t);
            }, Ease.InOutCubic, () =>
            {
                Release(head);
                arrived?.Invoke();
            });

            for (var i = 1; i <= 2; i++)
            {
                var tail = Dot(layer, Palette.WithAlpha(color, 0.45f / i), size * (1f - 0.2f * i));
                var lag = 0.035f * i;
                tail.rectTransform.position = from;
                Tweens.Run(tail, duration, t => tail.rectTransform.position = Bezier(from, control, to, t), Ease.InOutCubic,
                    () => Release(tail), lag);
            }
        }

        /// <summary>Искры: несколько точек разлетаются и гаснут.</summary>
        public static void Sparkle(RectTransform layer, Vector3 at, Color color, int count = 3, float radius = 18f)
        {
            var unit = Unit(layer);
            for (var i = 0; i < count; i++)
            {
                var angle = (i / (float)count + UnityEngine.Random.value * 0.3f) * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0);
                var dot = Dot(layer, Color.Lerp(color, Color.white, 0.5f), 5f);
                var reach = radius * (0.7f + UnityEngine.Random.value * 0.6f) * unit;
                Tweens.Run(dot, 0.28f, t =>
                {
                    dot.rectTransform.position = at + dir * reach * t;
                    dot.color = Palette.WithAlpha(dot.color, 1f - t);
                    dot.rectTransform.localScale = Vector3.one * (1f - 0.6f * t);
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

        private static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, float t)
        {
            var u = 1f - t;
            return u * u * a + 2f * u * t * b + t * t * c;
        }

        private static Image Dot(RectTransform layer, Color color, float size)
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

            dot.color = color;
            dot.rectTransform.sizeDelta = new Vector2(size, size);
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
