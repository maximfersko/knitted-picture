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
        private Vector2 _start;

        /// <summary>Сдвиг пальца от начала жеста в точках макета: по нему решают, жест горизонтальный или нет.</summary>
        public Vector2 Total { get; private set; }

        public event Action? Began;

        /// <summary>Сдвиг пальца по X в точках макета с прошлого кадра.</summary>
        public event Action<float>? Moved;

        /// <summary>Палец отпущен: скорость по X в точках макета в секунду.</summary>
        public event Action<float>? Released;

        /// <summary>
        /// Палец отпущен после явного горизонтального жеста: -1 — влево, +1 — вправо, 0 — не свайп
        /// (коротко или скорее вертикально). Расстояния — в точках макета.
        /// </summary>
        public event Action<int>? Swiped;

        public void OnBeginDrag(PointerEventData eventData)
        {
            _velocity = 0f;
            _start = eventData.position;
            Total = Vector2.zero;
            Began?.Invoke();
        }

        public void OnDrag(PointerEventData eventData)
        {
            // Холст поверх экрана: мировые единицы — пиксели, делим на масштаб, получаем точки макета.
            var scale = Mathf.Max(0.0001f, transform.lossyScale.x);
            var dx = eventData.delta.x / scale;
            Total += eventData.delta / scale;
            var dt = Mathf.Max(0.001f, Time.unscaledDeltaTime);
            _velocity = Mathf.Lerp(_velocity, dx / dt, 0.4f);
            Moved?.Invoke(dx);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            Released?.Invoke(_velocity);
            var scale = Mathf.Max(0.0001f, transform.lossyScale.x);
            var delta = (eventData.position - _start) / scale;
            var horizontal = Mathf.Abs(delta.x) > 1.4f * Mathf.Abs(delta.y);
            var far = Mathf.Abs(delta.x) > 70f || Mathf.Abs(_velocity) > 600f;
            Swiped?.Invoke(horizontal && far ? (delta.x < 0 ? -1 : 1) : 0);
        }
    }
}
