#nullable enable

using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace KnittedPicture.Game
{
    /// <summary>Горизонтальный свайп по элементу: -1 — влево (следующее), +1 — вправо (предыдущее).</summary>
    public sealed class Swipe : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        /// <summary>Сколько пикселей экрана нужно протащить, чтобы свайп засчитался.</summary>
        private const float Threshold = 60f;

        private Vector2 _start;

        public event Action<int>? Swiped;

        public void OnBeginDrag(PointerEventData eventData)
        {
            _start = eventData.position;
        }

        public void OnDrag(PointerEventData eventData)
        {
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            var delta = eventData.position - _start;
            if (Mathf.Abs(delta.x) >= Threshold && Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
            {
                Swiped?.Invoke(delta.x < 0 ? -1 : 1);
            }
        }
    }
}
