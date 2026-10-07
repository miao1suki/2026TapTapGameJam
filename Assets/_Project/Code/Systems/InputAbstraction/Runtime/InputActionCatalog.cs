using UnityEngine;
using UnityEngine.InputSystem;

namespace Project.InputAbstraction
{
    internal static class InputActionCatalog
    {
        internal const string MapName = "Gameplay";

        internal static string GetName(InputActionId action)
        {
            return action.ToString();
        }

        internal static InputActionAsset CreateDefaultAsset()
        {
            InputActionAsset asset = ScriptableObject.CreateInstance<InputActionAsset>();
            asset.name = "2026TapTapInput";
            InputActionMap map = new InputActionMap(MapName);
            asset.AddActionMap(map);
            AddMoveAction(map, InputActionId.Move);
            AddMoveAction(map, InputActionId.Navigate);
            AddLookAction(map, InputActionId.Look);
            AddButtonAction(map, InputActionId.Jump, "<Keyboard>/space", "<Gamepad>/buttonSouth");
            AddButtonAction(map, InputActionId.Interact, "<Keyboard>/e", "<Gamepad>/buttonWest");
            AddButtonAction(map, InputActionId.Carry, "<Mouse>/rightButton", "<Gamepad>/buttonNorth");
            AddButtonAction(map, InputActionId.Cancel, "<Keyboard>/escape", "<Gamepad>/buttonEast");
            AddButtonAction(map, InputActionId.Submit, "<Keyboard>/enter", "<Gamepad>/buttonSouth");
            AddButtonAction(map, InputActionId.Pause, "<Keyboard>/escape", "<Gamepad>/start");
            AddButtonAction(map, InputActionId.Crouch, "<Keyboard>/leftCtrl", "<Gamepad>/leftStickPress");
            AddButtonAction(map, InputActionId.Sprint, "<Keyboard>/leftShift", "<Gamepad>/leftShoulder");
            AddButtonAction(map, InputActionId.Attack, "<Mouse>/leftButton", "<Gamepad>/rightShoulder");
            AddButtonAction(
                map,
                InputActionId.CameraModeSwitch,
                "<Keyboard>/tab",
                "<Keyboard>/f",
                "<Gamepad>/select");
            AddButtonAction(map, InputActionId.PointerPrimary, "<Mouse>/leftButton");
            AddButtonAction(map, InputActionId.PointerSecondary, "<Mouse>/rightButton");
            return asset;
        }

        internal static void EnsureDefaultMoveBindings(
            InputActionAsset asset)
        {
            if (asset == null)
            {
                return;
            }

            InputActionMap map = asset.FindActionMap(
                MapName,
                false);
            InputAction move = map?.FindAction(
                GetName(InputActionId.Move),
                false);
            if (move == null)
            {
                return;
            }

            bool mapWasEnabled = map.enabled;
            if (mapWasEnabled)
            {
                map.Disable();
            }

            move.RemoveAllBindingOverrides();
            while (move.bindings.Count > 0)
            {
                move.ChangeBinding(0).Erase();
            }

            ConfigureMoveAction(move);

            if (mapWasEnabled)
            {
                map.Enable();
            }
        }

        internal static void EnsureRequiredActions(
            InputActionAsset asset)
        {
            EnsureDefaultMoveBindings(asset);
            if (asset == null)
            {
                return;
            }

            InputActionMap map = asset.FindActionMap(
                MapName,
                false);
            if (map == null)
            {
                return;
            }

            bool mapWasEnabled = map.enabled;
            if (mapWasEnabled)
            {
                map.Disable();
            }

            EnsureCarryAction(map);

            if (mapWasEnabled)
            {
                map.Enable();
            }
        }

        private static void EnsureCarryAction(
            InputActionMap map)
        {
            InputAction carry = map.FindAction(
                GetName(InputActionId.Carry),
                false);
            if (carry == null)
            {
                carry = map.AddAction(
                    GetName(InputActionId.Carry),
                    InputActionType.Button);
            }

            EnsureBinding(
                carry,
                "<Mouse>/rightButton");
            EnsureBinding(
                carry,
                "<Gamepad>/buttonNorth");
        }

        private static void EnsureBinding(
            InputAction action,
            string path)
        {
            for (int index = 0;
                 index < action.bindings.Count;
                 index++)
            {
                if (string.Equals(
                        action.bindings[index].effectivePath,
                        path,
                        System.StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            action.AddBinding(path);
        }

        private static void AddMoveAction(InputActionMap map, InputActionId id)
        {
            InputAction action = map.AddAction(GetName(id), InputActionType.Value, expectedControlLayout: "Vector2");
            ConfigureMoveAction(action);
        }

        private static void ConfigureMoveAction(InputAction action)
        {
            action.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            action.AddBinding("<Gamepad>/leftStick");
            action.AddBinding("<Gamepad>/dpad");
        }

        private static void AddLookAction(InputActionMap map, InputActionId id)
        {
            InputAction action = map.AddAction(GetName(id), InputActionType.Value, expectedControlLayout: "Vector2");
            action.AddBinding("<Mouse>/delta");
            action.AddBinding("<Gamepad>/rightStick");
        }

        private static void AddButtonAction(
            InputActionMap map,
            InputActionId id,
            params string[] bindings)
        {
            InputAction action = map.AddAction(GetName(id), InputActionType.Button);
            for (int index = 0; index < bindings.Length; index++)
            {
                action.AddBinding(bindings[index]);
            }
        }
    }
}
