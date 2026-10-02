using UnityEngine.InputSystem;

namespace Project.InputRebinding
{
    public enum InputBindingTrigger
    {
        Press = 0,
        Hold = 1,
        Tap = 2
    }

    public enum InputBindingDevice
    {
        Other = 0,
        Keyboard = 1,
        Mouse = 2,
        Gamepad = 3,
        Touch = 4
    }

    public readonly struct InputBindingInfo
    {
        public InputBindingInfo(
            InputAction action,
            int bindingIndex,
            string displayName,
            string controlPath,
            InputBindingDevice device,
            InputBindingTrigger trigger,
            bool isButton,
            bool isComposite,
            bool isPartOfComposite)
        {
            Action = action;
            BindingIndex = bindingIndex;
            DisplayName = displayName;
            ControlPath = controlPath;
            Device = device;
            Trigger = trigger;
            IsButton = isButton;
            IsComposite = isComposite;
            IsPartOfComposite = isPartOfComposite;
        }

        public InputAction Action { get; }
        public int BindingIndex { get; }
        public string DisplayName { get; }
        public string ControlPath { get; }
        public InputBindingDevice Device { get; }
        public InputBindingTrigger Trigger { get; }
        public bool IsButton { get; }
        public bool IsComposite { get; }
        public bool IsPartOfComposite { get; }
    }
}
