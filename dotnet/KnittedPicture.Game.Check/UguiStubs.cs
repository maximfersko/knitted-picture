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

        public void RaycastAll(PointerEventData eventData, System.Collections.Generic.List<RaycastResult> raycastResults)
        {
        }
    }

    public struct RaycastResult
    {
        public GameObject gameObject { get; set; }

        public BaseRaycaster module;

        public float distance;

        public bool isValid => true;
    }

    public static class ExecuteEvents
    {
        public delegate void EventFunction<T1>(T1 handler, BaseEventData eventData);

        public static EventFunction<IPointerDownHandler> pointerDownHandler => null;

        public static EventFunction<IPointerUpHandler> pointerUpHandler => null;

        public static EventFunction<IPointerClickHandler> pointerClickHandler => null;

        public static EventFunction<IBeginDragHandler> beginDragHandler => null;

        public static EventFunction<IDragHandler> dragHandler => null;

        public static EventFunction<IEndDragHandler> endDragHandler => null;

        public static bool Execute<T>(GameObject target, BaseEventData eventData, EventFunction<T> functor)
            where T : IEventSystemHandler => false;

        public static GameObject ExecuteHierarchy<T>(GameObject root, BaseEventData eventData, EventFunction<T> callbackFunction)
            where T : IEventSystemHandler => null;

        public static GameObject GetEventHandler<T>(GameObject root)
            where T : IEventSystemHandler => null;
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
        public BaseEventData()
        {
        }

        public BaseEventData(EventSystem eventSystem)
        {
        }
    }

    public class PointerEventData : BaseEventData
    {
        public enum InputButton
        {
            Left,
            Right,
            Middle,
        }

        public PointerEventData()
        {
        }

        public PointerEventData(EventSystem eventSystem)
            : base(eventSystem)
        {
        }

        public Vector2 position { get; set; }

        public Vector2 delta { get; set; }

        public Vector2 pressPosition { get; set; }

        public InputButton button { get; set; }

        public int clickCount { get; set; }

        public float clickTime { get; set; }

        public bool eligibleForClick { get; set; }

        public bool dragging { get; set; }

        public bool useDragThreshold { get; set; }

        public GameObject pointerPress { get; set; }

        public GameObject rawPointerPress { get; set; }

        public GameObject pointerDrag { get; set; }

        public RaycastResult pointerCurrentRaycast { get; set; }

        public RaycastResult pointerPressRaycast { get; set; }
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

        public virtual Texture mainTexture => null;

        public virtual void SetVerticesDirty()
        {
        }

        protected virtual void OnPopulateMesh(VertexHelper vh)
        {
        }
    }

    public class VertexHelper
    {
        public int currentVertCount => 0;

        public void Clear()
        {
        }

        public void AddVert(Vector3 position, Color32 color, Vector4 uv0)
        {
        }

        public void AddTriangle(int idx0, int idx1, int idx2)
        {
        }
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
        public bool fillCenter { get; set; } = true;
    }

    public abstract class BaseMeshEffect : UIBehaviour
    {
    }

    public class Shadow : BaseMeshEffect
    {
        public Color effectColor { get; set; }

        public Vector2 effectDistance { get; set; }

        public bool useGraphicAlpha { get; set; }
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

        public float preferredHeight => 0f;

        public TextGenerator cachedTextGenerator => null;

        public bool resizeTextForBestFit { get; set; }

        public int resizeTextMinSize { get; set; }

        public int resizeTextMaxSize { get; set; }
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

#if UNITY_ANDROID
namespace UnityEngine
{
    // В справочных сборках из NuGet нет Handheld (он есть только в сборке под телефон).
    public static class Handheld
    {
        public static void Vibrate()
        {
        }
    }
}
#endif
