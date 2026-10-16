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
        public bool ToggleOn { get; private set; }
        private bool toggleMode, repeatJump;
        public void ConfigureToggle() { toggleMode = true; }
        public void ConfigureRepeatJump() { repeatJump = true; }

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
            IsPressed = true;
            if (toggleMode) ToggleOn = !ToggleOn;
            VirtualInputState.SetButton(GetInstanceID(), action, toggleMode ? ToggleOn : true);
            if (repeatJump) VirtualInputState.SetRepeatingJump(GetInstanceID(), true);
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

        private void OnEnable()
        {
            if (toggleMode && ToggleOn) VirtualInputState.SetButton(GetInstanceID(), action, true);
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) { ToggleOn = false; Release(); VirtualInputState.ReleaseOwner(GetInstanceID()); }
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) { ToggleOn = false; Release(); VirtualInputState.ReleaseOwner(GetInstanceID()); }
        }

        private void Release()
        {
            activePointerId = int.MinValue;
            IsPressed = false;
            if (!toggleMode) VirtualInputState.SetButton(GetInstanceID(), action, false);
            if (repeatJump) VirtualInputState.SetRepeatingJump(GetInstanceID(), false);
        }

    }
}
