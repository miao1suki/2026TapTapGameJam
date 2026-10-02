using UnityEngine;

namespace Project.InputAbstraction
{
    /// <summary>
    /// Static convenience facade for gameplay code. It never exposes
    /// InputAction, Keyboard, Gamepad or other device-specific types.
    /// </summary>
    public static class GameInput
    {
        public static IInputSource Source => InputService.EnsureInstance().ActiveSource;
        public static InputPlatformMode PlatformMode =>
            InputService.EnsureInstance().ResolvedPlatformMode;
        public static InputDeviceMode ActiveDeviceMode => Source.ActiveDeviceMode;
        public static Vector2 PointerPosition => Source.PointerPosition;
        public static Vector2 PointerDelta => Source.PointerDelta;
        public static bool HasKeyboard => Source.HasKeyboard;
        public static bool HasMouse => Source.HasMouse;
        public static bool HasGamepad => Source.HasGamepad;
        public static bool HasTouch => Source.HasTouch;

        public static bool IsPressed(InputActionId action) =>
            Source.IsActionPressed(action);

        public static bool WasPressedThisFrame(InputActionId action) =>
            Source.WasActionPressedThisFrame(action);

        public static bool WasTriggeredThisFrame(InputActionId action) =>
            Source.WasActionTriggeredThisFrame(action);

        public static bool WasReleasedThisFrame(InputActionId action) =>
            Source.WasActionReleasedThisFrame(action);

        public static InputActionTrigger GetActionTrigger(InputActionId action) =>
            Source.GetActionTrigger(action);

        public static float Axis(InputActionId action) => Source.ReadAxis(action);

        public static Vector2 ReadVector2(InputActionId action) => Source.ReadVector2(action);

        public static void UseExternalSource(IInputSource source)
        {
            InputService.EnsureInstance().SetExternalSource(source);
        }

        public static void ClearExternalSource(IInputSource source = null)
        {
            InputService.EnsureInstance().ClearExternalSource(source);
        }
    }
}
