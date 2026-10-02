using UnityEngine;
using UnityEngine.EventSystems;

namespace Project.InputAbstraction
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class VirtualJoystick : MonoBehaviour,
        IPointerDownHandler,
        IDragHandler,
        IPointerUpHandler
    {
        [SerializeField]
        private InputActionId action = InputActionId.Move;

        [SerializeField]
        private RectTransform background;

        [SerializeField]
        private RectTransform handle;

        [SerializeField, Range(0f, 0.95f)]
        private float deadZone = 0.1f;

        [SerializeField, Min(0f)]
        private float movementRadius;

        private int activePointerId = int.MinValue;

        public InputActionId Action => action;
        public Vector2 Value { get; private set; }

        public void Configure(
            RectTransform joystickBackground,
            RectTransform joystickHandle,
            InputActionId inputAction)
        {
            background = joystickBackground;
            handle = joystickHandle;
            action = inputAction;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (activePointerId != int.MinValue)
            {
                return;
            }

            activePointerId = eventData.pointerId;
            UpdateFromPointer(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId == activePointerId)
            {
                UpdateFromPointer(eventData);
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != activePointerId)
            {
                return;
            }

            activePointerId = int.MinValue;
            SetValue(Vector2.zero, Vector2.zero);
        }

        private void Reset()
        {
            background = transform as RectTransform;
        }

        private void OnDisable()
        {
            activePointerId = int.MinValue;
            SetValue(Vector2.zero, Vector2.zero);
            VirtualInputState.ReleaseOwner(GetInstanceID());
        }

        private void UpdateFromPointer(PointerEventData eventData)
        {
            RectTransform target = background != null ? background : transform as RectTransform;
            if (target == null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    target,
                    eventData.position,
                    eventData.pressEventCamera,
                    out Vector2 localPoint))
            {
                return;
            }

            float radius = movementRadius > 0f
                ? movementRadius
                : Mathf.Min(target.rect.width, target.rect.height) * 0.5f;
            if (radius <= Mathf.Epsilon)
            {
                return;
            }

            Vector2 raw = Vector2.ClampMagnitude((localPoint - target.rect.center) / radius, 1f);
            float magnitude = raw.magnitude;
            Vector2 filtered = magnitude <= deadZone
                ? Vector2.zero
                : raw.normalized * Mathf.InverseLerp(deadZone, 1f, magnitude);
            SetValue(filtered, raw * radius);
        }

        private void SetValue(Vector2 inputValue, Vector2 handlePosition)
        {
            Value = inputValue;
            if (handle != null)
            {
                handle.anchoredPosition = handlePosition;
            }

            VirtualInputState.SetVector(GetInstanceID(), action, inputValue);
        }
    }
}
