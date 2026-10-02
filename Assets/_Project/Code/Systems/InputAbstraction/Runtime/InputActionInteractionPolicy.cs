using UnityEngine;
using UnityEngine.InputSystem;

namespace Project.InputAbstraction
{
    public enum InputActionTriggerPolicy
    {
        ClickOnly = 0,
        HoldOnly = 1,
        Switchable = 2,
    }

    public static class InputActionInteractionPolicy
    {
        private const string PreferencePrefix =
            "2026TapTap.InputBindings.TriggerPolicy.v1.";

        public static bool CanConfigureTrigger(
            InputActionId actionId)
        {
            return GetPolicy(actionId) ==
                   InputActionTriggerPolicy.Switchable;
        }

        public static InputActionTriggerPolicy GetPolicy(
            InputActionId actionId)
        {
            InputActionTriggerPolicy defaultPolicy =
                actionId == InputActionId.Crouch ||
                actionId == InputActionId.Sprint
                    ? InputActionTriggerPolicy.Switchable
                    : InputActionTriggerPolicy.ClickOnly;
            int value = PlayerPrefs.GetInt(
                PreferencePrefix + actionId,
                (int)defaultPolicy);
            return (InputActionTriggerPolicy)Mathf.Clamp(
                value,
                (int)InputActionTriggerPolicy.ClickOnly,
                (int)InputActionTriggerPolicy.Switchable);
        }

        public static void SetPolicy(
            InputActionId actionId,
            InputActionTriggerPolicy policy)
        {
            PlayerPrefs.SetInt(
                PreferencePrefix + actionId,
                (int)policy);
            PlayerPrefs.Save();
        }

        public static void ResetPolicies()
        {
            foreach (InputActionId actionId in
                     System.Enum.GetValues(
                         typeof(InputActionId)))
            {
                PlayerPrefs.DeleteKey(
                    PreferencePrefix + actionId);
            }

            PlayerPrefs.Save();
        }

        public static string CapturePolicies()
        {
            InputActionId[] actionIds =
                (InputActionId[])System.Enum.GetValues(
                    typeof(InputActionId));
            string[] values = new string[actionIds.Length];
            for (int index = 0;
                 index < actionIds.Length;
                 index++)
            {
                values[index] =
                    ((int)GetPolicy(actionIds[index]))
                    .ToString();
            }

            return string.Join(",", values);
        }

        public static void RestorePolicies(string snapshot)
        {
            if (string.IsNullOrWhiteSpace(snapshot))
            {
                return;
            }

            InputActionId[] actionIds =
                (InputActionId[])System.Enum.GetValues(
                    typeof(InputActionId));
            string[] values = snapshot.Split(',');
            int count = Mathf.Min(
                actionIds.Length,
                values.Length);
            for (int index = 0; index < count; index++)
            {
                if (!int.TryParse(
                        values[index],
                        out int value))
                {
                    continue;
                }

                value = Mathf.Clamp(
                    value,
                    (int)InputActionTriggerPolicy.ClickOnly,
                    (int)InputActionTriggerPolicy.Switchable);
                PlayerPrefs.SetInt(
                    PreferencePrefix + actionIds[index],
                    value);
            }

            PlayerPrefs.Save();
        }

        public static void Normalize(InputActionAsset asset)
        {
            if (asset == null)
            {
                return;
            }

            InputActionMap map = asset.FindActionMap(
                InputActionCatalog.MapName,
                false);
            if (map == null)
            {
                return;
            }

            bool mapWasEnabled = map.enabled;
            bool mapIsEnabled = mapWasEnabled;
            for (int actionIndex = 0;
                 actionIndex < map.actions.Count;
                 actionIndex++)
            {
                InputAction action = map.actions[actionIndex];
                if (action.type != InputActionType.Button ||
                    !System.Enum.TryParse(
                        action.name,
                        out InputActionId actionId))
                {
                    continue;
                }

                string interaction =
                    GetInteraction(
                        GetPolicy(actionId),
                        action);
                for (int bindingIndex = 0;
                     bindingIndex < action.bindings.Count;
                     bindingIndex++)
                {
                    InputBinding binding =
                        action.bindings[bindingIndex];
                    if (binding.isPartOfComposite ||
                        (binding.interactions == interaction &&
                         string.IsNullOrWhiteSpace(
                             binding.overrideInteractions)))
                    {
                        continue;
                    }

                    if (mapIsEnabled)
                    {
                        map.Disable();
                        mapIsEnabled = false;
                    }

                    action.RemoveBindingOverride(bindingIndex);
                    binding = action.bindings[bindingIndex];
                    binding.interactions = interaction;
                    binding.overrideInteractions = null;
                    action.ChangeBinding(bindingIndex)
                        .To(binding);
                }
            }

            if (mapWasEnabled)
            {
                map.Enable();
            }
        }

        private static bool HasHoldBinding(
            InputAction action)
        {
            for (int index = 0;
                 index < action.bindings.Count;
                 index++)
            {
                InputBinding binding = action.bindings[index];
                if (!binding.isPartOfComposite &&
                    !string.IsNullOrWhiteSpace(
                        binding.effectiveInteractions) &&
                    binding.effectiveInteractions.Contains("Hold"))
                {
                    return true;
                }
            }

            return false;
        }

        private static string GetInteraction(
            InputActionTriggerPolicy policy,
            InputAction action)
        {
            switch (policy)
            {
                case InputActionTriggerPolicy.HoldOnly:
                    return "Hold";
                case InputActionTriggerPolicy.Switchable:
                    return HasHoldBinding(action)
                        ? "Hold"
                        : string.Empty;
                default:
                    return string.Empty;
            }
        }
    }
}
