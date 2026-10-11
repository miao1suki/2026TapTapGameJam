namespace Project.InputAbstraction
{
    /// <summary>
    /// Formats prompts for keyboard, gamepad and touch devices.
    /// </summary>
    public static class InputPromptUtility
    {
        public static bool UseActionLabels =>
            GameInput.ActiveDeviceMode == InputDeviceMode.Touch ||
            GameInput.PlatformMode == InputPlatformMode.Mobile;

        public static string FormatKey(InputActionId actionId)
        {
            return $"[{InputBindingDisplayUtility.GetPrimaryDisplayString(actionId)}]";
        }

        public static string FormatAction(
            InputActionId actionId,
            string actionLabel,
            string effect)
        {
            if (UseActionLabels)
            {
                return $"按下{actionLabel}以{effect}";
            }

            return $"按{FormatKey(actionId)}以{effect}";
        }
    }
}
