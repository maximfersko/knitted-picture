#nullable enable

using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace KnittedPicture.Game
{
    /// <summary>
    /// Перетаскивание по горизонтали для карусели: сдвиг в точках макета и скорость при отпускании.
    /// Само ничего не двигает — решает тот, кто слушает.
    /// </summary>
    public sealed class Pager : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private float _velocity;

        public event Action? Began;

        /// <summary>Сдвиг пальца по X в точках макета с прошлого кадра.</summary>
        public event Action<float>? Moved;

        /// <summary>Палец отпущен: скорость по X в точках макета в секунду.</summary>
        public event Action<float>? Released;

        public void OnBeginDrag(PointerEventData eventData)
        {
            _velocity = 0f;
            Began?.Invoke();
        }

        public void OnDrag(PointerEventData eventData)
        {
            // Холст поверх экрана: мировые единицы — пиксели, делим на масштаб, получаем точки макета.
            var scale = Mathf.Max(0.0001f, transform.lossyScale.x);
            var dx = eventData.delta.x / scale;
            var dt = Mathf.Max(0.001f, Time.unscaledDeltaTime);
            _velocity = Mathf.Lerp(_velocity, dx / dt, 0.4f);
            Moved?.Invoke(dx);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            Released?.Invoke(_velocity);
        }
    }
}
