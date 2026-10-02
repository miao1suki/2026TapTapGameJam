using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Project.InputAbstraction
{
    internal sealed class UnityInputSource : IInputSource, IInputActivitySource, IDisposable
    {
        private readonly InputActionAsset asset;
        private readonly InputActionMap map;
        private readonly Dictionary<InputActionId, InputAction> actions =
            new Dictionary<InputActionId, InputAction>();
        private readonly bool ownsAsset;

        internal UnityInputSource(
            InputActionAsset configuredAsset,
            bool cloneConfiguredAsset = true)
        {
            if (configuredAsset != null)
            {
                asset = cloneConfiguredAsset
                    ? UnityEngine.Object.Instantiate(configuredAsset)
                    : configuredAsset;
                ownsAsset = true;
            }
            else
            {
                asset = InputActionAssetFactory
                    .CreateDefaultGameplayAsset();
                ownsAsset = true;
            }

            map = asset.FindActionMap(InputActionCatalog.MapName, false);
            if (map == null)
            {
                throw new InvalidOperationException(
                    $"Input asset must contain an action map named '{InputActionCatalog.MapName}'.");
            }

            foreach (InputActionId id in Enum.GetValues(typeof(InputActionId)))
            {
                InputAction action = map.FindAction(InputActionCatalog.GetName(id), false);
                if (action != null)
                {
                    actions.Add(id, action);
                }
            }

            map.Enable();
        }

        public InputDeviceMode ActiveDeviceMode
        {
            get
            {
                double keyboardTime = Keyboard.current?.lastUpdateTime ?? double.MinValue;
                double mouseTime = Mouse.current?.lastUpdateTime ?? double.MinValue;
                double gamepadTime = Gamepad.current?.lastUpdateTime ?? double.MinValue;
                double touchTime = Touchscreen.current?.lastUpdateTime ?? double.MinValue;

                if (Touchscreen.current != null &&
                    touchTime >= keyboardTime &&
                    touchTime >= mouseTime &&
                    touchTime >= gamepadTime)
                {
                    return InputDeviceMode.Touch;
                }

                if (Gamepad.current != null &&
                    gamepadTime >= keyboardTime &&
                    gamepadTime >= mouseTime)
                {
                    return InputDeviceMode.Gamepad;
                }

                if (Keyboard.current != null || Mouse.current != null)
                {
                    return InputDeviceMode.KeyboardMouse;
                }

                return InputDeviceMode.Auto;
            }
        }

        public double LastInputTime => Math.Max(
            Math.Max(
                Keyboard.current?.lastUpdateTime ?? 0d,
                Mouse.current?.lastUpdateTime ?? 0d),
            Math.Max(
                Gamepad.current?.lastUpdateTime ?? 0d,
                Touchscreen.current?.lastUpdateTime ?? 0d));

        public bool IsActionPressed(InputActionId action) =>
            TryGetAction(action, out InputAction value) && value.IsPressed();

        public bool WasActionPressedThisFrame(InputActionId action) =>
            TryGetAction(action, out InputAction value) && value.WasPressedThisFrame();

        public bool WasActionTriggeredThisFrame(InputActionId action) =>
            TryGetAction(action, out InputAction value) && value.WasPerformedThisFrame();

        public bool WasActionReleasedThisFrame(InputActionId action) =>
            TryGetAction(action, out InputAction value) && value.WasReleasedThisFrame();

        public InputActionTrigger GetActionTrigger(InputActionId action)
        {
            if (!InputActionInteractionPolicy.CanConfigureTrigger(
                    action) ||
                !TryGetAction(action, out InputAction value))
            {
                return InputActionTrigger.Press;
            }

            for (int index = 0; index < value.bindings.Count; index++)
            {
                InputBinding binding = value.bindings[index];
                if (binding.isPartOfComposite ||
                    string.IsNullOrWhiteSpace(binding.effectiveInteractions))
                {
                    continue;
                }

                if (binding.effectiveInteractions.Contains("Hold"))
                {
                    return InputActionTrigger.Hold;
                }
            }

            return InputActionTrigger.Press;
        }

        public float ReadAxis(InputActionId action)
        {
            if (!TryGetAction(action, out InputAction value))
            {
                return 0f;
            }

            return value.ReadValue<float>();
        }

        public Vector2 ReadVector2(InputActionId action)
        {
            if (!TryGetAction(action, out InputAction value))
            {
                return Vector2.zero;
            }

            return value.ReadValue<Vector2>();
        }

        public Vector2 PointerPosition => Mouse.current?.position.ReadValue() ?? Vector2.zero;
        public Vector2 PointerDelta => Mouse.current?.delta.ReadValue() ?? Vector2.zero;
        internal InputActionAsset Asset => asset;
        public bool HasKeyboard => Keyboard.current != null;
        public bool HasMouse => Mouse.current != null;
        public bool HasGamepad => Gamepad.current != null;
        public bool HasTouch => Touchscreen.current != null;

        public void Dispose()
        {
            map?.Disable();
            if (ownsAsset && asset != null)
            {
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(asset);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(asset);
                }
            }
        }

        private bool TryGetAction(InputActionId id, out InputAction action)
        {
            return actions.TryGetValue(id, out action);
        }
    }
}
