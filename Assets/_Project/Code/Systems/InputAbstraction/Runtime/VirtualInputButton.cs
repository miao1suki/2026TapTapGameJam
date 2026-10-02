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
            if (eventData.pointerId == activePointerId)
            {
                Release();
            }
        }

        private void OnDisable()
        {
            Release();
            VirtualInputState.ReleaseOwner(GetInstanceID());
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
