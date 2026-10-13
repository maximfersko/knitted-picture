#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace KnittedPicture.Game
{
    /// <summary>
    /// Нажимаемый элемент: слегка проседает под пальцем, звучит и вызывает действие.
    /// Проседание накладывается поверх текущего масштаба: «дышащие» кнопки и клубки тоже
    /// отзываются на палец, а после отпускания возвращаются к своей анимации, а не к масштабу из Awake.
    /// </summary>
    public sealed class Tap : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, IPointerClickHandler
    {
        private const float Pressed = 0.94f;

        private Vector3 _base = Vector3.one;
        private Vector3 _applied = Vector3.one;
        private bool _pressed;

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
            if (!Held.Contains(this))
            {
                Held.Add(this);
            }

            Press();
        }

        /// <summary>Нажатые сейчас (обычно одна-две): их ведёт Tweens после анимаций кадра, а не LateUpdate у каждой кнопки.</summary>
        private static readonly List<Tap> Held = new List<Tap>();

        /// <summary>Анимация за этот кадр поменяла масштаб нажатого — проседаем уже от нового.</summary>
        public static void FollowPressed()
        {
            for (var i = Held.Count - 1; i >= 0; i--)
            {
                var tap = Held[i];
                if (tap == null || !tap._pressed)
                {
                    Held.RemoveAt(i);
                    continue;
                }

                if (tap.transform.localScale != tap._applied)
                {
                    tap.Press();
                }
            }
        }

        private void OnDisable()
        {
            Release();
        }

        private void Press()
        {
            _base = transform.localScale;
            _applied = _base * Pressed;
            transform.localScale = _applied;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
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
            if (_pressed)
            {
                _pressed = false;
                if (transform.localScale == _applied)
                {
                    transform.localScale = _base;
                }
            }
        }
    }
}
