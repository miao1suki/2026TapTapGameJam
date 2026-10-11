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
