#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace KnittedPicture.Game
{
    public static class Ease
    {
        public static float Linear(float t) => t;

        public static float OutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);

        public static float InOutCubic(float t) => t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;

        public static float OutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        /// <summary>Туда и обратно: 0 → 1 → 0, для «пульса».</summary>
        public static float Bump(float t) => Mathf.Sin(t * Mathf.PI);
    }

    /// <summary>
    /// Лёгкий движок анимаций без внешних библиотек. Анимация живёт, пока жив её владелец:
    /// уничтожили объект — анимация тихо закончилась.
    /// </summary>
    public sealed class Tweens : MonoBehaviour
    {
        private sealed class Tween
        {
            public UnityEngine.Object? Owner;
            public float Delay;
            public float Duration;
            public float Time;
            public Action<float>? Step;
            public Func<float, float> Ease = KnittedPicture.Game.Ease.Linear;
            public Action? Done;
            public bool Loop;
        }

        private static Tweens? _instance;
        private readonly List<Tween> _active = new List<Tween>();
        private readonly List<Tween> _adding = new List<Tween>();

        /// <summary>Отработавшие твины: на поле сотни нитей в секунду, без пула каждая — мусор для сборщика.</summary>
        private readonly Stack<Tween> _pool = new Stack<Tween>();

        public static void Install(GameObject host)
        {
            _instance = host.AddComponent<Tweens>();
        }

        public static void Run(UnityEngine.Object owner, float duration, Action<float> step, Func<float, float>? ease = null,
            Action? done = null, float delay = 0f, bool loop = false)
        {
            if (_instance == null)
            {
                step(1f);
                done?.Invoke();
                return;
            }

            if (loop)
            {
                Ui.LoopLayer(owner);
            }

            var tween = _instance._pool.Count > 0 ? _instance._pool.Pop() : new Tween();
            tween.Owner = owner;
            tween.Duration = Mathf.Max(0.0001f, duration);
            tween.Delay = delay;
            tween.Time = 0f;
            tween.Step = step;
            tween.Ease = ease ?? KnittedPicture.Game.Ease.Linear;
            tween.Done = done;
            tween.Loop = loop;
            _instance._adding.Add(tween);
        }

        public static void Delay(UnityEngine.Object owner, float seconds, Action action) =>
            Run(owner, seconds, NoStep, null, action);

        private static readonly Action<float> NoStep = _ => { };

        public static void Move(RectTransform rt, Vector3 fromWorld, Vector3 toWorld, float duration, Func<float, float>? ease = null,
            Action? done = null, float delay = 0f)
        {
            rt.position = fromWorld;
            Run(rt, duration, t => rt.position = Vector3.LerpUnclamped(fromWorld, toWorld, t), ease ?? KnittedPicture.Game.Ease.OutCubic, done, delay);
        }

        public static void Scale(Transform target, float from, float to, float duration, Func<float, float>? ease = null,
            Action? done = null, float delay = 0f)
        {
            target.localScale = Vector3.one * from;
            Run(target, duration, t => target.localScale = Vector3.one * Mathf.LerpUnclamped(from, to, t), ease ?? KnittedPicture.Game.Ease.OutBack, done, delay);
        }

        /// <summary>Короткий «прыжок» размера: подтверждение действия.</summary>
        public static void Punch(Transform target, float amount = 0.12f, float duration = 0.22f)
        {
            Run(target, duration, t => target.localScale = Vector3.one * (1f + amount * KnittedPicture.Game.Ease.Bump(t)));
        }

        /// <summary>Покачать по горизонтали: «нельзя» или затор.</summary>
        public static void Shake(RectTransform rt, float amount = 8f, float duration = 0.35f)
        {
            var origin = rt.anchoredPosition;
            Run(rt, duration, t =>
            {
                var offset = Mathf.Sin(t * Mathf.PI * 6f) * amount * (1f - t);
                rt.anchoredPosition = origin + new Vector2(offset, 0);
            }, null, () => rt.anchoredPosition = origin);
        }

        public static void Fade(CanvasGroup group, float from, float to, float duration, Action? done = null, float delay = 0f)
        {
            group.alpha = from;
            Run(group, duration, t => group.alpha = Mathf.Lerp(from, to, t), KnittedPicture.Game.Ease.OutCubic, done, delay);
        }

        private void Update()
        {
            if (_adding.Count > 0)
            {
                _active.AddRange(_adding);
                _adding.Clear();
            }

            var dt = UnityEngine.Time.unscaledDeltaTime;
            for (var i = _active.Count - 1; i >= 0; i--)
            {
                var tween = _active[i];
                if (tween.Owner == null)
                {
                    RemoveAt(i);
                    continue;
                }

                if (tween.Delay > 0)
                {
                    tween.Delay -= dt;
                    continue;
                }

                tween.Time += dt;
                var t = Mathf.Clamp01(tween.Time / tween.Duration);
                tween.Step?.Invoke(tween.Ease(t));
                if (t < 1f)
                {
                    continue;
                }

                if (tween.Loop)
                {
                    tween.Time = 0;
                    continue;
                }

                var done = tween.Done;
                RemoveAt(i);
                done?.Invoke();
            }
        }

        /// <summary>Убрать твин из списка (порядок сохраняем: два твина одного свойства решают, кто последний) и вернуть в пул.</summary>
        private void RemoveAt(int i)
        {
            var tween = _active[i];
            _active.RemoveAt(i);
            tween.Owner = null;
            tween.Step = null;
            tween.Done = null;
            _pool.Push(tween);
        }

        private void LateUpdate()
        {
            Tap.FollowPressed();
        }
    }
}
