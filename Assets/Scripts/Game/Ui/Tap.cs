#nullable enable

using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace KnittedPicture.Game
{
    /// <summary>Нажимаемый элемент: слегка проседает под пальцем, звучит и вызывает действие.</summary>
    public sealed class Tap : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, IPointerClickHandler
    {
        private Vector3 _scale = Vector3.one;
        private bool _pressed;

        public event Action? Clicked;

        public bool Interactable { get; set; } = true;

        /// <summary>Звук нажатия: у игровых элементов свой звук, кнопкам — щелчок.</summary>
        public bool ClickSound { get; set; } = true;

        /// <summary>Проседать ли под пальцем. Фоновым областям (затемнение) — нет.</summary>
        public bool PressFeedback { get; set; } = true;

        public Tap OnClick(Action action)
        {
            Clicked += action;
            return this;
        }

        private void Awake()
        {
            _scale = transform.localScale;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!Interactable || !PressFeedback)
            {
                return;
            }

            _pressed = true;
            transform.localScale = _scale * 0.94f;
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
                Sfx.Play(Sound.Click);
            }

            Clicked?.Invoke();
        }

        private void Release()
        {
            if (_pressed)
            {
                _pressed = false;
                transform.localScale = _scale;
            }
        }
    }
}
