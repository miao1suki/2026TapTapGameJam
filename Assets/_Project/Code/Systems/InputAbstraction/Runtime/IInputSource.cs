using UnityEngine;

namespace Project.InputAbstraction
{
    /// <summary>
    /// Device-agnostic input read API. A future mobile/touch adapter can
    /// implement this interface without changing gameplay callers.
    /// </summary>
    public interface IInputSource
    {
        InputDeviceMode ActiveDeviceMode { get; }
        bool IsActionPressed(InputActionId action);
        bool WasActionPressedThisFrame(InputActionId action);
        bool WasActionTriggeredThisFrame(InputActionId action);
        bool WasActionReleasedThisFrame(InputActionId action);
        InputActionTrigger GetActionTrigger(InputActionId action);
        float ReadAxis(InputActionId action);
        Vector2 ReadVector2(InputActionId action);
        Vector2 PointerPosition { get; }
        Vector2 PointerDelta { get; }
        bool HasKeyboard { get; }
        bool HasMouse { get; }
        bool HasGamepad { get; }
        bool HasTouch { get; }
    }
}
