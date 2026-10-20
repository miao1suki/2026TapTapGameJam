using UnityEngine.InputSystem;

namespace Project.InputAbstraction
{
    /// <summary>
    /// Read-only display helper for prompts that must show the current binding.
    /// </summary>
    public static class InputBindingDisplayUtility
    {
        public static string GetPrimaryDisplayString(
            InputActionId actionId)
        {
            InputService service = InputService.Instance;
            InputActionAsset asset = service != null
                ? service.RuntimeActionAsset ??
                  service.ConfiguredActionAsset
                : null;
            if (asset == null)
            {
                return actionId.ToString();
            }

            InputAction action = asset.FindAction(
                actionId.ToString(),
                false);
            if (action == null)
            {
                return actionId.ToString();
            }

            InputDeviceMode deviceMode = GameInput.ActiveDeviceMode;
            string preferredPrefix =
                deviceMode == InputDeviceMode.Gamepad
                    ? "<Gamepad>"
                    : "<Keyboard>";
            string secondaryPrefix =
                deviceMode == InputDeviceMode.Gamepad
                    ? string.Empty
                    : "<Mouse>";
            if (deviceMode != InputDeviceMode.Gamepad)
            {
                for (int index = 0;
                     index < action.bindings.Count;
                     index++)
                {
                    InputBinding binding = action.bindings[index];
                    if (!binding.isComposite)
                    {
                        continue;
                    }

                    string composite = action.GetBindingDisplayString(
                        index,
                        InputBinding.DisplayStringOptions
                            .DontUseShortDisplayNames);
                    if (!string.IsNullOrWhiteSpace(composite))
                    {
                        return composite;
                    }
                }
            }

            for (int pass = 0; pass < 2; pass++)
            {
                string prefix = pass == 0
                    ? preferredPrefix
                    : secondaryPrefix;
                if (string.IsNullOrWhiteSpace(prefix))
                {
                    continue;
                }

                for (int index = 0;
                     index < action.bindings.Count;
                     index++)
                {
                    InputBinding binding = action.bindings[index];
                    if (binding.isComposite ||
                        binding.isPartOfComposite)
                    {
                        continue;
                    }

                    string path = binding.effectivePath;
                    if (string.IsNullOrWhiteSpace(path) ||
                        !path.StartsWith(prefix))
                    {
                        continue;
                    }

                    return action.GetBindingDisplayString(
                        index,
                        InputBinding.DisplayStringOptions
                            .DontUseShortDisplayNames);
                }
            }

            for (int index = 0;
                 index < action.bindings.Count;
                 index++)
            {
                InputBinding binding = action.bindings[index];
                if (binding.isComposite ||
                    binding.isPartOfComposite)
                {
                    continue;
                }

                string path = binding.effectivePath;
                if (!string.IsNullOrWhiteSpace(path) &&
                    (path.StartsWith("<Keyboard>") ||
                     path.StartsWith("<Mouse>")))
                {
                    return action.GetBindingDisplayString(
                        index,
                        InputBinding.DisplayStringOptions
                            .DontUseShortDisplayNames);
                }
            }

            return action.GetBindingDisplayString(
                InputBinding.DisplayStringOptions
                    .DontUseShortDisplayNames);
        }
    }
}
