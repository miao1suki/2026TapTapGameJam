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
            AddButtonAction(map, InputActionId.Interact, "<Keyboard>/f", "<Gamepad>/buttonWest");
            AddButtonAction(map, InputActionId.DropLeft, "<Keyboard>/q", "<Gamepad>/leftTrigger");
            AddButtonAction(map, InputActionId.DropRight, "<Keyboard>/e", "<Gamepad>/rightTrigger");
            AddButtonAction(
                map,
                InputActionId.Respawn,
                "<Keyboard>/r",
                "<Gamepad>/rightStickPress");
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
                "<Gamepad>/select");
            AddButtonAction(map, InputActionId.PointerPrimary, "<Mouse>/leftButton", "<Touchscreen>/primaryTouch/press");
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

            EnsureInteractBindings(map);
            EnsureDropBindings(map);
            EnsureRespawnAction(map);
            InputAction pointer = map.FindAction(GetName(InputActionId.PointerPrimary), false);
            if (pointer != null)
            {
                bool hasTouch = false;
                foreach (var binding in pointer.bindings)
                    if (binding.path == "<Touchscreen>/primaryTouch/press") hasTouch = true;
                if (!hasTouch) pointer.AddBinding("<Touchscreen>/primaryTouch/press");
            }

            if (mapWasEnabled)
            {
                map.Enable();
            }
        }

        private static void EnsureInteractBindings(
            InputActionMap map)
        {
            InputAction interact = map.FindAction(
                GetName(InputActionId.Interact),
                false);
            if (interact == null)
            {
                interact = map.AddAction(
                    GetName(InputActionId.Interact),
                    InputActionType.Button);
            }

            EnsureBinding(
                interact,
                "<Keyboard>/f");
            EnsureBinding(
                interact,
                "<Gamepad>/buttonWest");

            InputAction colorWheel = map.FindAction(
                GetName(InputActionId.CameraModeSwitch),
                false);
            if (colorWheel != null)
            {
                EnsureBinding(
                    colorWheel,
                    "<Keyboard>/tab");
            }
        }

        private static void EnsureDropBindings(
            InputActionMap map)
        {
            InputAction dropLeft = map.FindAction(
                GetName(InputActionId.DropLeft),
                false);
            if (dropLeft == null)
            {
                dropLeft = map.AddAction(
                    GetName(InputActionId.DropLeft),
                    InputActionType.Button);
            }

            EnsureBinding(dropLeft, "<Keyboard>/q");
            EnsureBinding(dropLeft, "<Gamepad>/leftTrigger");

            InputAction dropRight = map.FindAction(
                GetName(InputActionId.DropRight),
                false);
            if (dropRight == null)
            {
                dropRight = map.AddAction(
                    GetName(InputActionId.DropRight),
                    InputActionType.Button);
            }

            EnsureBinding(dropRight, "<Keyboard>/e");
            EnsureBinding(dropRight, "<Gamepad>/rightTrigger");
        }

        private static void EnsureRespawnAction(
            InputActionMap map)
        {
            InputAction respawn = map.FindAction(
                GetName(InputActionId.Respawn),
                false);
            if (respawn == null)
            {
                respawn = map.AddAction(
                    GetName(InputActionId.Respawn),
                    InputActionType.Button);
            }

            EnsureBinding(
                respawn,
                "<Keyboard>/r");
            EnsureBinding(
                respawn,
                "<Gamepad>/rightStickPress");
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
