using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Project.InputAbstraction
{
    // Movement fingers and world-target fingers have independent ownership.
    internal static class TouchGameplayPointer
    {
        private static readonly List<RaycastResult> hits = new List<RaycastResult>(16);
        private static int frame = -1, pointer = -1;
        private static Vector2 position;
        private static bool pressed, triggered, released;
        internal static Vector2 Position { get { Refresh(); return position; } }
        internal static bool Pressed { get { Refresh(); return pressed; } }
        internal static bool Triggered { get { Refresh(); return triggered; } }
        internal static bool Released { get { Refresh(); return released; } }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        { frame = pointer = -1; pressed = triggered = released = false; position = Vector2.zero; }

        private static void Refresh()
        {
            if (frame == Time.frameCount) return;
            frame = Time.frameCount; triggered = released = false;
            var screen = Touchscreen.current;
            if (screen == null) { pressed = false; pointer = -1; return; }
            foreach (var touch in screen.touches)
            {
                int id = touch.touchId.ReadValue();
                var phase = touch.phase.ReadValue();
                if (phase == UnityEngine.InputSystem.TouchPhase.Began && pointer < 0 && !IsHud(touch.position.ReadValue()))
                { pointer = id; position = touch.position.ReadValue(); pressed = triggered = true; }
                if (id == pointer)
                {
                    position = touch.position.ReadValue();
                    if (phase == UnityEngine.InputSystem.TouchPhase.Ended || phase == UnityEngine.InputSystem.TouchPhase.Canceled)
                    { pressed = false; released = true; pointer = -1; }
                }
            }
            if (pointer < 0) return;
            bool found = false;
            foreach (var touch in screen.touches)
                if (touch.touchId.ReadValue() == pointer && touch.press.isPressed) found = true;
            if (!found) { pointer = -1; released = pressed; pressed = false; }
        }
        private static bool IsHud(Vector2 point)
        {
            var system = EventSystem.current;
            if (system == null) return false;
            hits.Clear(); system.RaycastAll(new PointerEventData(system) { position = point }, hits);
            foreach (var hit in hits)
                if (hit.gameObject.GetComponentInParent<VirtualInputButton>() != null ||
                    hit.gameObject.GetComponentInParent<VirtualDirectionButton>() != null ||
                    hit.gameObject.GetComponentInParent<UnityEngine.UI.Button>() != null) return true;
            return false;
        }
    }
}
