using System;
using UnityEngine;

namespace Project.InputAbstraction
{
    internal interface IInputActivitySource
    {
        double LastInputTime { get; }
    }

    internal sealed class CompositeInputSource : IInputSource
    {
        private const float ValueEpsilon = 0.0001f;

        private readonly IInputSource hardware;
        private readonly IInputSource virtualInput;

        internal CompositeInputSource(IInputSource hardware, IInputSource virtualInput)
        {
            this.hardware = hardware ?? throw new ArgumentNullException(nameof(hardware));
            this.virtualInput = virtualInput ?? throw new ArgumentNullException(nameof(virtualInput));
        }

        public InputDeviceMode ActiveDeviceMode
        {
            get
            {
                double hardwareTime = GetLastInputTime(hardware);
                double virtualTime = GetLastInputTime(virtualInput);
                return virtualTime > 0d && virtualTime >= hardwareTime
                    ? virtualInput.ActiveDeviceMode
                    : hardware.ActiveDeviceMode;
            }
        }

        public bool IsActionPressed(InputActionId action) =>
            hardware.IsActionPressed(action) || virtualInput.IsActionPressed(action);

        public bool WasActionPressedThisFrame(InputActionId action) =>
            hardware.WasActionPressedThisFrame(action) ||
            virtualInput.WasActionPressedThisFrame(action);

        public bool WasActionTriggeredThisFrame(InputActionId action) =>
            hardware.WasActionTriggeredThisFrame(action) ||
            virtualInput.WasActionTriggeredThisFrame(action);

        public bool WasActionReleasedThisFrame(InputActionId action) =>
            hardware.WasActionReleasedThisFrame(action) ||
            virtualInput.WasActionReleasedThisFrame(action);

        public InputActionTrigger GetActionTrigger(InputActionId action)
        {
            InputActionTrigger hardwareTrigger =
                hardware.GetActionTrigger(action);
            return hardwareTrigger != InputActionTrigger.Press
                ? hardwareTrigger
                : virtualInput.GetActionTrigger(action);
        }

        public float ReadAxis(InputActionId action)
        {
            float virtualValue = virtualInput.ReadAxis(action);
            return Mathf.Abs(virtualValue) > ValueEpsilon
                ? virtualValue
                : hardware.ReadAxis(action);
        }

        public Vector2 ReadVector2(InputActionId action)
        {
            Vector2 virtualValue = virtualInput.ReadVector2(action);
            return virtualValue.sqrMagnitude > ValueEpsilon * ValueEpsilon
                ? virtualValue
                : hardware.ReadVector2(action);
        }

        public Vector2 PointerPosition => hardware.PointerPosition;
        public Vector2 PointerDelta => hardware.PointerDelta;
        public bool HasKeyboard => hardware.HasKeyboard;
        public bool HasMouse => hardware.HasMouse;
        public bool HasGamepad => hardware.HasGamepad || virtualInput.HasGamepad;
        public bool HasTouch => hardware.HasTouch || virtualInput.HasTouch;

        private static double GetLastInputTime(IInputSource source)
        {
            return source is IInputActivitySource activity ? activity.LastInputTime : 0d;
        }
    }
}
