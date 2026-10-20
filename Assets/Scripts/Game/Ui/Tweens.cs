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

        /// <summary>Мягкий перелёт: как OutBack, но на пару процентов — для сдвигов, где большой перелёт заметен.</summary>
        public static float OutSoftBack(float t)
        {
            const float c1 = 0.9f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        public static float OutQuint(float t) => 1f - Mathf.Pow(1f - t, 5f);

        public static float InCubic(float t) => t * t * t;

        /// <summary>
        /// Пружина: быстро к цели, перелёт и пара затухающих покачиваний. Ровно 1 в конце.
        /// Для отпускания кнопок, выезда окон, переключателей — движение «живое», но не резиновое.
        /// </summary>
        public static float Spring(float t)
        {
            if (t >= 1f)
            {
                return 1f;
            }

            return 1f - Mathf.Exp(-6.5f * t) * Mathf.Cos(t * Mathf.PI * 3.2f) * (1f - t);
        }

        /// <summary>Туда и обратно: 0 → 1 → 0, для «пульса».</summary>
        public static float Bump(float t) => Mathf.Sin(t * Mathf.PI);

        /// <summary>Сердцебиение для повторяющейся анимации: два коротких удара подряд (второй слабее) и долгая пауза.</summary>
        public static float Heartbeat(float t)
        {
            static float Beat(float x, float start, float width) =>
                x >= start && x < start + width ? Mathf.Sin((x - start) / width * Mathf.PI) : 0f;

            return Beat(t, 0f, 0.09f) + 0.7f * Beat(t, 0.12f, 0.09f);
        }

        /// <summary>Толчок с затуханием: резкий пик в начале, потом пара всё более мелких отскоков, в конце 0.</summary>
        public static float Jolt(float t) => Mathf.Sin(t * Mathf.PI * 3f) * (1f - t) * (1f - t);
    }

    /// <summary>
    /// Пружина для того, что ведётся каждый кадр (карусели): слегка недодемпфированная — доезжает с мягким
    /// перелётом в пару процентов и сохраняет скорость броска пальцем. Шаг дробится, чтобы на просадке кадров
    /// пружина не «взрывалась».
    /// </summary>
    public static class Springs
    {
        public static void Step(ref float position, ref float velocity, float target, float dt, float stiffness = 190f, float damping = 21f)
        {
            if (Tweens.Calm)
            {
                // «Меньше движения»: критическое затухание — доезжает без перелёта.
                damping = Mathf.Max(damping, 2f * Mathf.Sqrt(stiffness));
            }

            var steps = Mathf.Max(1, Mathf.CeilToInt(dt * 120f));
            var h = dt / steps;
            for (var i = 0; i < steps; i++)
            {
                velocity += ((target - position) * stiffness - velocity * damping) * h;
                position += velocity * h;
            }
        }
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
            public int Channel;
        }

        private static Tweens? _instance;

        /// <summary>
        /// «Меньше движения» (настройки удобства): бесконечные украшения стоят на месте, элементы появляются
        /// без всплытия и пружин, толчки и встряски слабее. Важное (полёт нитки, клубок в слот) остаётся.
        /// </summary>
        public static bool Calm { get; set; }
        private readonly List<Tween> _active = new List<Tween>();
        private readonly List<Tween> _adding = new List<Tween>();

        /// <summary>Отработавшие твины: на поле сотни нитей в секунду, без пула каждая — мусор для сборщика.</summary>
        private readonly Stack<Tween> _pool = new Stack<Tween>();

        public static void Install(GameObject host)
        {
            _instance = host.AddComponent<Tweens>();
        }

        /// <summary>Планировщик твинов запущен: без него (проверочные сборки) отложенные действия идут сразу.</summary>
        public static bool Installed => _instance != null;

        public static void Run(UnityEngine.Object owner, float duration, Action<float> step, Func<float, float>? ease = null,
            Action? done = null, float delay = 0f, bool loop = false, int channel = 0)
        {
            if (_instance == null)
            {
                step(1f);
                done?.Invoke();
                return;
            }

            if (loop && Calm)
            {
                // Повторяющееся украшение замирает в начальной позе, а не качается.
                step(0f);
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
            tween.Channel = channel;
            _instance._adding.Add(tween);
        }

        /// <summary>
        /// Остановить твины владельца на канале (0 — все неповторяющиеся). Так новое движение не борется
        /// со старым: окно закрывают, пока оно ещё выезжает, — выезд останавливается, а не дописывает масштаб поверх.
        /// Done остановленных не вызывается.
        /// </summary>
        public static void Kill(UnityEngine.Object owner, int channel = 0)
        {
            if (_instance == null)
            {
                return;
            }

            bool Match(Tween tween) => tween.Owner == owner && (channel == 0 ? !tween.Loop : tween.Channel == channel);
            for (var i = _instance._active.Count - 1; i >= 0; i--)
            {
                if (Match(_instance._active[i]))
                {
                    _instance.RemoveAt(i);
                }
            }

            for (var i = _instance._adding.Count - 1; i >= 0; i--)
            {
                if (Match(_instance._adding[i]))
                {
                    _instance._adding.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// Сдвиг «поверх»: каждый кадр к позиции добавляется только изменение смещения. Если элемент тем временем
        /// переставила раскладка или другая анимация, смещение отсчитывается уже от новой позиции, и в конце
        /// элемент стоит там, куда его поставили они, а не на запомненном на старте месте.
        /// </summary>
        public static void Offset(RectTransform rt, Vector2 from, Vector2 to, float duration, Func<float, float>? ease = null,
            Action? done = null, float delay = 0f, int channel = 0)
        {
            var applied = Vector2.zero;
            var written = Vector2.zero;
            var started = false;
            Run(rt, duration, t =>
            {
                var position = rt.anchoredPosition;
                if (!started || position != written)
                {
                    // Начало или позицию поставили заново без нашего смещения — считаем от неё.
                    started = true;
                    applied = Vector2.zero;
                }

                var next = Vector2.LerpUnclamped(from, to, t);
                position += next - applied;
                rt.anchoredPosition = position;
                applied = next;
                written = position;
            }, ease ?? KnittedPicture.Game.Ease.OutCubic, done, delay, false, channel);
        }

        /// <summary>
        /// Появление: элемент проявляется и всплывает на rise точек к своему месту. Прозрачность — через свою
        /// временную CanvasGroup (чужую, которой управляет экран, не трогаем), после конца её убираем.
        /// До старта элемент прозрачен и стоит на месте — раскладка, которая двигает его в это время, не сбивается.
        /// </summary>
        public static void Appear(RectTransform rt, float delay, float rise = 16f, float duration = 0.34f)
        {
            if (rt.GetComponent<CanvasGroup>() != null)
            {
                // Прозрачностью управляет экран: элемент виден сразу, поэтому всплывает без задержки, а не стоит и потом прыгает.
                delay = 0f;
            }
            else
            {
                var group = rt.gameObject.AddComponent<CanvasGroup>();
                group.alpha = 0f;
                Run(group, duration * 0.7f, t => group.alpha = t, KnittedPicture.Game.Ease.OutCubic, () => Destroy(group), delay);
            }

            if (rise != 0f && !Calm)
            {
                Offset(rt, new Vector2(0, -rise), Vector2.zero, duration, KnittedPicture.Game.Ease.OutSoftBack, null, delay);
            }
        }

        /// <summary>Число «набегает» от from до to: счётчики монет и наград.</summary>
        public static void Count(UnityEngine.UI.Text label, int from, int to, float duration, float delay = 0f, string format = "{0}")
        {
            label.text = string.Format(format, from);
            Run(label, duration, t => label.text = string.Format(format, Mathf.RoundToInt(Mathf.Lerp(from, to, t))),
                KnittedPicture.Game.Ease.OutCubic, null, delay);
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

        /// <summary>
        /// Короткий «прыжок» размера: подтверждение действия. Упруго, как желе: раздувается, по ширине
        /// чуть больше, чем по высоте, и пару раз затухающе отскакивает.
        /// </summary>
        public static void Punch(Transform target, float amount = 0.12f, float duration = 0.22f)
        {
            var length = Mathf.Max(duration * 1.6f, 0.28f);
            if (Calm)
            {
                amount *= 0.35f;
            }

            Run(target, length, t =>
            {
                var j = amount * KnittedPicture.Game.Ease.Jolt(t);
                target.localScale = new Vector3(1f + j * 1.1f, 1f + j * 0.8f, 1f);
            }, null, () => target.localScale = Vector3.one);
        }

        /// <summary>Покачать по горизонтали: «нельзя» или затор.</summary>
        public static void Shake(RectTransform rt, float amount = 8f, float duration = 0.35f)
        {
            var origin = rt.anchoredPosition;
            var tilt = rt.localRotation;
            var lean = Calm ? 0f : 0.25f;
            if (Calm)
            {
                amount *= 0.35f;
            }

            Run(rt, duration, t =>
            {
                // Затухание квадратом: первый рывок сильный, хвост быстро стихает; лёгкий наклон в такт — «мотает головой».
                var wave = Mathf.Sin(t * Mathf.PI * 6f) * (1f - t) * (1f - t);
                rt.anchoredPosition = origin + new Vector2(wave * amount, 0);
                rt.localRotation = tilt * Quaternion.Euler(0, 0, -wave * amount * lean);
            }, null, () =>
            {
                rt.anchoredPosition = origin;
                rt.localRotation = tilt;
            });
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

            // Шаг не больше 1/30 с: после долгого кадра (экран строился) анимация продолжается с того же места,
            // а не перескакивает вперёд — переход не теряет начало и не дёргается.
            var dt = Mathf.Min(UnityEngine.Time.unscaledDeltaTime, 1f / 30f);
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
