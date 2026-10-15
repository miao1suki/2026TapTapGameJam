using UnityEngine;
using UnityEngine.EventSystems;

namespace Project.GameFlow
{
    // UI gestures remain separate from gameplay input and virtual joystick state.
    public sealed class EscWheelTouchNavigation : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private EscMenuController controller;
        private Vector2 previous;
        private float accumulated;
        private int pointerId;
        public void Configure(EscMenuController value) => controller = value;
        public void OnBeginDrag(PointerEventData data)
        {
            pointerId = data.pointerId; previous = data.position; accumulated = 0;
        }
        public void OnDrag(PointerEventData data)
        {
            if (data.pointerId != pointerId) return;
            accumulated += data.position.y - previous.y; previous = data.position;
            float threshold = Mathf.Clamp(Screen.height * .06f,32f,128f);
            if (Mathf.Abs(accumulated) < threshold) return;
            controller?.NavigateWheel(accumulated > 0 ? -1 : 1);
            accumulated = 0;
        }
        public void OnEndDrag(PointerEventData data) { accumulated = 0; }
        private void OnDisable() { accumulated = 0; }
    }
}
