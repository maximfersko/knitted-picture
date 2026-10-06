// Заглушки uGUI (пакет com.unity.ugui) для проверки компиляции вне Unity.
// Повторяют только те типы и члены, которыми пользуется игра, с теми же именами и сигнатурами.
#nullable disable

namespace UnityEngine.EventSystems
{
    public class UIBehaviour : MonoBehaviour
    {
    }

    public class EventSystem : UIBehaviour
    {
        public static EventSystem current { get; set; }
    }

    public abstract class BaseInputModule : UIBehaviour
    {
    }

    public class StandaloneInputModule : BaseInputModule
    {
    }

    public abstract class BaseRaycaster : UIBehaviour
    {
    }

    public class BaseEventData
    {
    }

    public class PointerEventData : BaseEventData
    {
        public Vector2 position { get; set; }

        public Vector2 delta { get; set; }
    }

    public interface IEventSystemHandler
    {
    }

    public interface IPointerDownHandler : IEventSystemHandler
    {
        void OnPointerDown(PointerEventData eventData);
    }

    public interface IPointerUpHandler : IEventSystemHandler
    {
        void OnPointerUp(PointerEventData eventData);
    }

    public interface IPointerExitHandler : IEventSystemHandler
    {
        void OnPointerExit(PointerEventData eventData);
    }

    public interface IBeginDragHandler : IEventSystemHandler
    {
        void OnBeginDrag(PointerEventData eventData);
    }

    public interface IDragHandler : IEventSystemHandler
    {
        void OnDrag(PointerEventData eventData);
    }

    public interface IEndDragHandler : IEventSystemHandler
    {
        void OnEndDrag(PointerEventData eventData);
    }

    public interface IPointerClickHandler : IEventSystemHandler
    {
        void OnPointerClick(PointerEventData eventData);
    }
}

namespace UnityEngine.UI
{
    using UnityEngine.EventSystems;

    public abstract class Graphic : UIBehaviour
    {
        public virtual Color color { get; set; }

        public virtual bool raycastTarget { get; set; }

        public RectTransform rectTransform => (RectTransform)transform;

        public virtual Material material { get; set; }
    }

    public abstract class MaskableGraphic : Graphic
    {
    }

    public class Image : MaskableGraphic
    {
        public enum Type
        {
            Simple,
            Sliced,
            Tiled,
            Filled,
        }

        public Sprite sprite { get; set; }

        public Type type { get; set; }

        public bool preserveAspect { get; set; }

        public float fillAmount { get; set; }

        public float pixelsPerUnitMultiplier { get; set; }
    }

    public class RawImage : MaskableGraphic
    {
        public Texture texture { get; set; }

        public Rect uvRect { get; set; }
    }

    public class Text : MaskableGraphic
    {
        public string text { get; set; }

        public Font font { get; set; }

        public int fontSize { get; set; }

        public FontStyle fontStyle { get; set; }

        public TextAnchor alignment { get; set; }

        public HorizontalWrapMode horizontalOverflow { get; set; }

        public VerticalWrapMode verticalOverflow { get; set; }

        public float lineSpacing { get; set; }

        public bool supportRichText { get; set; }

        public float preferredWidth => 0f;
    }

    public class CanvasScaler : UIBehaviour
    {
        public enum ScaleMode
        {
            ConstantPixelSize,
            ScaleWithScreenSize,
            ConstantPhysicalSize,
        }

        public ScaleMode uiScaleMode { get; set; }

        public float scaleFactor { get; set; }

        public float referencePixelsPerUnit { get; set; }
    }

    public class GraphicRaycaster : BaseRaycaster
    {
    }

    public class ScrollRect : UIBehaviour
    {
        public enum MovementType
        {
            Unrestricted,
            Elastic,
            Clamped,
        }

        public RectTransform content { get; set; }

        public RectTransform viewport { get; set; }

        public bool horizontal { get; set; }

        public bool vertical { get; set; }

        public MovementType movementType { get; set; }

        public bool inertia { get; set; }

        public float decelerationRate { get; set; }

        public float scrollSensitivity { get; set; }

        public float verticalNormalizedPosition { get; set; }
    }

    public class RectMask2D : UIBehaviour
    {
    }
}
