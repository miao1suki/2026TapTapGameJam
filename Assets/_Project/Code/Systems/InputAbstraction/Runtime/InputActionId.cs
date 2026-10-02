namespace Project.InputAbstraction
{
    /// <summary>
    /// Stable gameplay actions. Callers use these ids instead of referring to
    /// Unity Input System action names or device controls.
    /// </summary>
    public enum InputActionId
    {
        Move = 0,
        Look = 1,
        Navigate = 2,
        Jump = 3,
        Interact = 4,
        Cancel = 5,
        Submit = 6,
        Pause = 7,
        Crouch = 8,
        Sprint = 9,
        Attack = 10,
        CameraModeSwitch = 11,
        PointerPrimary = 12,
        PointerSecondary = 13,
    }

    public enum InputActionTrigger
    {
        Press = 0,
        Hold = 1,
        Tap = 2,
    }

    public enum InputDeviceMode
    {
        Auto = 0,
        KeyboardMouse = 1,
        Gamepad = 2,
        Touch = 3,
    }
}
