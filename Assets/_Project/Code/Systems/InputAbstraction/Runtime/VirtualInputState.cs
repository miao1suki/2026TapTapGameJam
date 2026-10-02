using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;

namespace Project.InputAbstraction
{
    internal static class VirtualInputState
    {
        private static readonly Dictionary<InputActionId, Dictionary<int, Vector2>> Vectors =
            new Dictionary<InputActionId, Dictionary<int, Vector2>>();

        private static readonly Dictionary<InputActionId, HashSet<int>> Buttons =
            new Dictionary<InputActionId, HashSet<int>>();

        private static readonly Dictionary<InputActionId, int> PressFrames =
            new Dictionary<InputActionId, int>();

        private static readonly Dictionary<InputActionId, int> ReleaseFrames =
            new Dictionary<InputActionId, int>();

        internal static double LastInputTime { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForPlayMode()
        {
            Reset();
        }

        internal static void SetVector(int ownerId, InputActionId action, Vector2 value)
        {
            value = Vector2.ClampMagnitude(value, 1f);
            if (!Vectors.TryGetValue(action, out Dictionary<int, Vector2> owners))
            {
                owners = new Dictionary<int, Vector2>();
                Vectors.Add(action, owners);
            }

            owners.TryGetValue(ownerId, out Vector2 previous);
            if (value.sqrMagnitude <= 0.000001f)
            {
                owners.Remove(ownerId);
            }
            else
            {
                owners[ownerId] = value;
            }

            if ((previous - value).sqrMagnitude > 0.000001f)
            {
                MarkActivity();
            }
        }

        internal static void SetButton(int ownerId, InputActionId action, bool pressed)
        {
            if (!Buttons.TryGetValue(action, out HashSet<int> owners))
            {
                owners = new HashSet<int>();
                Buttons.Add(action, owners);
            }

            bool wasPressed = owners.Count > 0;
            if (pressed)
            {
                owners.Add(ownerId);
            }
            else
            {
                owners.Remove(ownerId);
            }

            bool isPressed = owners.Count > 0;
            if (wasPressed == isPressed)
            {
                return;
            }

            if (isPressed)
            {
                PressFrames[action] = Time.frameCount;
            }
            else
            {
                ReleaseFrames[action] = Time.frameCount;
            }

            MarkActivity();
        }

        internal static void ReleaseOwner(int ownerId)
        {
            foreach (KeyValuePair<InputActionId, Dictionary<int, Vector2>> entry in Vectors)
            {
                if (entry.Value.Remove(ownerId))
                {
                    MarkActivity();
                }
            }

            foreach (KeyValuePair<InputActionId, HashSet<int>> entry in Buttons)
            {
                bool wasPressed = entry.Value.Count > 0;
                if (!entry.Value.Remove(ownerId))
                {
                    continue;
                }

                if (wasPressed && entry.Value.Count == 0)
                {
                    ReleaseFrames[entry.Key] = Time.frameCount;
                }

                MarkActivity();
            }
        }

        internal static Vector2 ReadVector(InputActionId action)
        {
            if (!Vectors.TryGetValue(action, out Dictionary<int, Vector2> owners))
            {
                return Vector2.zero;
            }

            Vector2 strongest = Vector2.zero;
            foreach (Vector2 value in owners.Values)
            {
                if (value.sqrMagnitude > strongest.sqrMagnitude)
                {
                    strongest = value;
                }
            }

            return strongest;
        }

        internal static bool IsPressed(InputActionId action)
        {
            return Buttons.TryGetValue(action, out HashSet<int> owners) && owners.Count > 0;
        }

        internal static bool WasPressedThisFrame(InputActionId action)
        {
            return PressFrames.TryGetValue(action, out int frame) && frame == Time.frameCount;
        }

        internal static bool WasReleasedThisFrame(InputActionId action)
        {
            return ReleaseFrames.TryGetValue(action, out int frame) && frame == Time.frameCount;
        }

        internal static void Reset()
        {
            Vectors.Clear();
            Buttons.Clear();
            PressFrames.Clear();
            ReleaseFrames.Clear();
            LastInputTime = 0d;
        }

        private static void MarkActivity()
        {
            LastInputTime = InputState.currentTime;
            if (LastInputTime <= 0d)
            {
                LastInputTime = double.Epsilon;
            }
        }
    }

    internal sealed class VirtualInputSource : IInputSource, IInputActivitySource
    {
        public InputDeviceMode ActiveDeviceMode => InputDeviceMode.Touch;
        public double LastInputTime => VirtualInputState.LastInputTime;
        public bool IsActionPressed(InputActionId action) => VirtualInputState.IsPressed(action);
        public bool WasActionPressedThisFrame(InputActionId action) =>
            VirtualInputState.WasPressedThisFrame(action);

        public bool WasActionTriggeredThisFrame(InputActionId action) =>
            VirtualInputState.WasPressedThisFrame(action);

        public bool WasActionReleasedThisFrame(InputActionId action) =>
            VirtualInputState.WasReleasedThisFrame(action);

        public InputActionTrigger GetActionTrigger(InputActionId action) =>
            InputActionTrigger.Press;

        public float ReadAxis(InputActionId action) => VirtualInputState.ReadVector(action).x;
        public Vector2 ReadVector2(InputActionId action) => VirtualInputState.ReadVector(action);
        public Vector2 PointerPosition => Vector2.zero;
        public Vector2 PointerDelta => Vector2.zero;
        public bool HasKeyboard => false;
        public bool HasMouse => false;
        public bool HasGamepad => false;
        public bool HasTouch => true;
    }
}
