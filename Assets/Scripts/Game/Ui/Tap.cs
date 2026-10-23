#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace KnittedPicture.Game
{
    /// <summary>
    /// Нажимаемый элемент: проседает под пальцем, звучит и вызывает действие.
    /// Нажатие — на пружине: быстро вдавливается до масштаба 0.96 по обеим осям, а отпущенный элемент
    /// возвращается критически демпфированно, без перелёта и отскока (кнопки интерфейса, частые действия).
    /// Мягкий «подушечный» отклик (<see cref="Squishy"/>) только у игровых клубков: по высоте просадка сильнее,
    /// а при возврате есть лёгкий перелёт. Проседание накладывается поверх текущего масштаба: «дышащие» кнопки
    /// и клубки тоже отзываются на палец и возвращаются к своей анимации.
    /// </summary>
    public sealed class Tap : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, IPointerClickHandler
    {
        /// <summary>Просадка кнопки интерфейса при полном нажатии: масштаб 0.96 по обеим осям.</summary>
        private const float PressSquash = 0.04f;

        /// <summary>Просадка «подушечного» отклика (клубки поля) по ширине и по высоте при полном нажатии.</summary>
        private const float SquishX = 0.055f;
        private const float SquishY = 0.075f;

        private Vector3 _base = Vector3.one;
        private Vector3 _applied = Vector3.one;
        private bool _pressed;
        private bool _moving;
        private float _depth;
        private float _velocity;

        public event Action? Clicked;

        public bool Interactable { get; set; } = true;

        /// <summary>Звук нажатия: у игровых элементов свой звук, кнопкам — щелчок.</summary>
        public bool ClickSound { get; set; } = true;

        /// <summary>Каким звуком щёлкать: обычный мягкий тап или «спицы» у главных кнопок.</summary>
        public Sound Sound { get; set; } = Sound.Click;

        /// <summary>
        /// Что сказать, если нажали, пока кнопка недоступна (null — молча). Тогда кнопка коротко мотает
        /// «головой», глухо отзывается звуком и толчком, а подсказка всплывает плашкой: тап не проходит в пустоту.
        /// </summary>
        public string? DeniedHint { get; set; }

        /// <summary>
        /// Мягкий «подушечный» отклик: просадка сильнее по высоте и возврат с лёгким перелётом (при «Меньше движения»
        /// перелёта нет). По умолчанию false: пресс 0.96 и возврат без отскока. Включён только у клубков поля.
        /// </summary>
        public bool Squishy { get; set; }

        /// <summary>
        /// Дополнительный множитель масштаба от «толчка» (<see cref="Tweens.Punch"/>). Масштаб элемента с Tap
        /// всегда считается как база × просадка пружины × Extra: толчок не пишет localScale сам и не спорит с пружиной,
        /// поэтому множители не копятся. Пока Extra не единица, Tap пишет масштаб каждый кадр, даже когда пружина в покое.
        /// </summary>
        public Vector3 Extra { get; private set; } = Vector3.one;

        /// <summary>Задать множитель толчка; не единица — разбудить пружину, чтобы она его применяла.</summary>
        public void SetExtra(Vector3 extra)
        {
            Extra = extra;
            if (extra != Vector3.one && isActiveAndEnabled)
            {
                Begin();
            }
        }

        /// <summary>Проседать ли под пальцем. Фоновым областям (затемнение) — нет.</summary>
        public bool PressFeedback { get; set; } = true;

        public Tap OnClick(Action action)
        {
            Clicked += action;
            return this;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!Interactable || !PressFeedback)
            {
                return;
            }

            _pressed = true;
            Begin();
        }

        /// <summary>Пружинящие сейчас (обычно одна-две): их ведёт Tweens после анимаций кадра, а не LateUpdate у каждой кнопки.</summary>
        private static readonly List<Tap> Moving = new List<Tap>();

        /// <summary>Шаг пружин: после анимаций кадра, поэтому проседание ложится поверх их масштаба.</summary>
        public static void FollowPressed()
        {
            var dt = Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
            for (var i = Moving.Count - 1; i >= 0; i--)
            {
                var tap = Moving[i];
                if (tap == null || !tap.Step(dt))
                {
                    Moving.RemoveAt(i);
                }
            }
        }

        private void Begin()
        {
            if (!_moving)
            {
                _moving = true;
                _base = transform.localScale;
                _applied = _base;
                if (!Moving.Contains(this))
                {
                    Moving.Add(this);
                }
            }
        }

        /// <summary>Один шаг пружины. false — успокоилась, масштаб вернулся к своему.</summary>
        private bool Step(float dt)
        {
            if (!_moving)
            {
                return false;
            }

            // Масштаб за кадр поменяла чужая анимация — проседаем уже от нового.
            if (transform.localScale != _applied)
            {
                _base = transform.localScale;
            }

            // Вдавливание — жёсткая пружина почти без перелёта (~80 мс). Отпускание: у кнопок критическое затухание
            // (2·√380 ≈ 39), без отскока; у «подушечных» клубков мягче, с отскоком выше своего размера.
            float stiffness, damping;
            if (_pressed)
            {
                stiffness = 900f;
                damping = 52f;
            }
            else
            {
                stiffness = 380f;
                damping = Squishy && !Tweens.Calm ? 15f : 39f;
            }

            var target = _pressed ? 1f : 0f;
            _velocity += ((target - _depth) * stiffness - _velocity * damping) * dt;
            _depth += _velocity * dt;
            if (!_pressed && Mathf.Abs(_depth) < 0.003f && Mathf.Abs(_velocity) < 0.05f && Extra == Vector3.one)
            {
                Settle();
                return false;
            }

            var squashX = Squishy ? SquishX : PressSquash;
            var squashY = Squishy ? SquishY : PressSquash;
            _applied = Vector3.Scale(Vector3.Scale(_base, new Vector3(1f - squashX * _depth, 1f - squashY * _depth, 1f)), Extra);
            transform.localScale = _applied;
            return true;
        }

        /// <summary>Остановить пружину и вернуть свой масштаб (если его никто не поменял).</summary>
        private void Settle()
        {
            if (_moving && transform.localScale == _applied)
            {
                transform.localScale = _base;
            }

            _moving = false;
            _depth = 0f;
            _velocity = 0f;
        }

        private void OnDisable()
        {
            _pressed = false;
            Settle();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            // Быстрый тап (палец успел подняться раньше, чем кнопка просела): толчок, чтобы отклик был виден и тогда.
            if (_pressed && _depth < 0.6f)
            {
                _velocity = Mathf.Max(_velocity, 22f);
            }

            Release();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            Release();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!Interactable)
            {
                if (DeniedHint != null)
                {
                    Denied();
                }

                return;
            }

            if (ClickSound)
            {
                // Кнопки интерфейса «щёлкают» и в руке: лёгкий тактильный тик вместе со звуком.
                Sfx.Play(Sound);
                Haptics.Play(Haptic.Tap);
            }

            Clicked?.Invoke();
        }

        private void Release()
        {
            _pressed = false;
        }

        private void Denied()
        {
            Ui.Deny(transform as RectTransform, DeniedHint);
        }
    }
}
