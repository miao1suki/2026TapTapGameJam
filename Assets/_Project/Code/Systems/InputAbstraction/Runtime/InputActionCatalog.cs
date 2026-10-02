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
            AddButtonAction(map, InputActionId.Cancel, "<Keyboard>/escape", "<Gamepad>/buttonEast");
            AddButtonAction(map, InputActionId.Submit, "<Keyboard>/enter", "<Gamepad>/buttonSouth");
            AddButtonAction(map, InputActionId.Pause, "<Keyboard>/escape", "<Gamepad>/start");
            AddButtonAction(map, InputActionId.Crouch, "<Keyboard>/leftCtrl", "<Gamepad>/leftStickPress");
            AddButtonAction(map, InputActionId.Sprint, "<Keyboard>/leftShift", "<Gamepad>/leftShoulder");
            AddButtonAction(map, InputActionId.Attack, "<Mouse>/leftButton", "<Gamepad>/rightShoulder");
            // Reserved API only: migration does not enable a gameplay view-switch shortcut.
            map.AddAction(InputActionId.CameraModeSwitch.ToString(), InputActionType.Button);
            AddButtonAction(map, InputActionId.PointerPrimary, "<Mouse>/leftButton");
            AddButtonAction(map, InputActionId.PointerSecondary, "<Mouse>/rightButton");
            return asset;
        }

        private static void AddMoveAction(InputActionMap map, InputActionId id)
        {
            InputAction action = map.AddAction(GetName(id), InputActionType.Value, expectedControlLayout: "Vector2");
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
