using UnityEngine;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Project.InputAbstraction
{
    /// <summary>
    /// Static convenience facade for gameplay code. It never exposes
    /// InputAction, Keyboard, Gamepad or other device-specific types.
    /// </summary>
    public static class GameInput
    {
        private static readonly List<RaycastResult> uiHits = new List<RaycastResult>(32);
        private static EventSystem pointerEventSystem;
        private static PointerEventData pointerEvent;
        private static readonly Dictionary<InputActionId, int> consumedFrames = new Dictionary<InputActionId, int>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetFrameConsumption() => consumedFrames.Clear();
        public static void ConsumeActionThisFrame(InputActionId action) => consumedFrames[action] = Time.frameCount;
        private static bool IsConsumed(InputActionId action) => consumedFrames.TryGetValue(action, out int frame) && frame == Time.frameCount;
        public static IInputSource Source
        {
            get
            {
                InputService service = InputService.EnsureInstance();
                service.EnsureRuntimeInputEnabled();
                return service.ActiveSource;
            }
        }
        public static InputPlatformMode PlatformMode =>
            InputService.EnsureInstance().ResolvedPlatformMode;
        public static InputDeviceMode ActiveDeviceMode => Source.ActiveDeviceMode;
        public static Vector2 PointerPosition => Source.PointerPosition;
        public static Vector2 PointerDelta => Source.PointerDelta;
        public static bool HasKeyboard => Source.HasKeyboard;
        public static bool HasMouse => Source.HasMouse;
        public static bool HasGamepad => Source.HasGamepad;
        public static bool HasTouch => Source.HasTouch;
        public static bool IsTouchJumpRepeating => VirtualInputState.RepeatJumpHeld;

        public static bool IsPointerOverUi()
        {
            EventSystem system = EventSystem.current;
            if (system == null) return false;
            Vector2 position = PointerPosition;
            if (pointerEventSystem != system)
            {
                pointerEventSystem = system;
                pointerEvent = new PointerEventData(system);
            }
            pointerEvent.Reset();
            pointerEvent.position = position;
            // We only ask whether UGUI blocks gameplay. RaycastAll also invokes
            // scene PhysicsRaycasters and sorts hits which this query discards.
            var raycasters = RaycasterManager.GetRaycasters();
            for (int i = 0; i < raycasters.Count; i++)
            {
                if (!(raycasters[i] is GraphicRaycaster graphic) || !graphic.isActiveAndEnabled) continue;
                uiHits.Clear();
                graphic.Raycast(pointerEvent, uiHits);
                if (uiHits.Count == 0) continue;
                uiHits.Clear();
                return true;
            }
            uiHits.Clear();
            return false;
        }

        public static bool IsPressed(InputActionId action) =>
            Source.IsActionPressed(action);

        public static bool WasPressedThisFrame(InputActionId action) =>
            !IsConsumed(action) && Source.WasActionPressedThisFrame(action);

        public static bool WasTriggeredThisFrame(InputActionId action) =>
            !IsConsumed(action) && Source.WasActionTriggeredThisFrame(action);

        public static bool WasReleasedThisFrame(InputActionId action) =>
            !IsConsumed(action) && Source.WasActionReleasedThisFrame(action);

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
