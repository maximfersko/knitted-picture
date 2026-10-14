#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace KnittedPicture.Game
{
    /// <summary>
    /// Нажимаемый элемент: проседает под пальцем, звучит и вызывает действие.
    /// Нажатие — на пружине: быстро вдавливается (по высоте чуть сильнее, как подушка под пальцем),
    /// а отпущенный элемент отскакивает с лёгким перелётом и затихает. Проседание накладывается поверх
    /// текущего масштаба: «дышащие» кнопки и клубки тоже отзываются на палец и возвращаются к своей анимации.
    /// </summary>
    public sealed class Tap : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, IPointerClickHandler
    {
        /// <summary>Насколько проседает по ширине и по высоте при полном нажатии.</summary>
        private const float SquashX = 0.055f;
        private const float SquashY = 0.075f;

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

            // Вдавливание — жёсткая пружина почти без перелёта (~80 мс); отпускание — мягче, с отскоком выше своего размера.
            float stiffness, damping;
            if (_pressed)
            {
                stiffness = 900f;
                damping = 52f;
            }
            else
            {
                stiffness = 380f;
                damping = 15f;
            }

            var target = _pressed ? 1f : 0f;
            _velocity += ((target - _depth) * stiffness - _velocity * damping) * dt;
            _depth += _velocity * dt;
            if (!_pressed && Mathf.Abs(_depth) < 0.003f && Mathf.Abs(_velocity) < 0.05f)
            {
                Settle();
                return false;
            }

            _applied = Vector3.Scale(_base, new Vector3(1f - SquashX * _depth, 1f - SquashY * _depth, 1f));
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
                return;
            }

            if (ClickSound)
            {
                Sfx.Play(Sound);
            }

            Clicked?.Invoke();
        }

        private void Release()
        {
            _pressed = false;
        }
    }
}
