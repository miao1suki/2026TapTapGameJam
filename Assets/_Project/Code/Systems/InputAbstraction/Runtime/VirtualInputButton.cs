using UnityEngine;
using UnityEngine.EventSystems;

namespace Project.InputAbstraction
{
    [DisallowMultipleComponent]
    public sealed class VirtualInputButton : MonoBehaviour,
        IPointerDownHandler,
        IPointerUpHandler,
        IPointerExitHandler
    {
        [SerializeField]
        private InputActionId action = InputActionId.Jump;

        private int activePointerId = int.MinValue;

        public InputActionId Action => action;
        public bool IsPressed { get; private set; }

        public void Configure(InputActionId inputAction)
        {
            action = inputAction;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (activePointerId != int.MinValue)
            {
                return;
            }

            activePointerId = eventData.pointerId;
            SetPressed(true);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId == activePointerId)
            {
                Release();
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            // Keep ownership until this finger lifts, even when a thumb drifts outside.
            // EventSystem delivers PointerUp to the original press target.
        }

        private void OnDisable()
        {
            Release();
            VirtualInputState.ReleaseOwner(GetInstanceID());
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) Release();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) Release();
        }

        private void Release()
        {
            activePointerId = int.MinValue;
            SetPressed(false);
        }

        private void SetPressed(bool pressed)
        {
            IsPressed = pressed;
            VirtualInputState.SetButton(GetInstanceID(), action, pressed);
        }
    }
}
