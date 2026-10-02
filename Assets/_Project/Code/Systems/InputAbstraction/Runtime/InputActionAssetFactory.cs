using UnityEngine;
using UnityEngine.InputSystem;

namespace Project.InputAbstraction
{
    public static class InputActionAssetFactory
    {
        public static InputActionAsset CreateDefaultGameplayAsset()
        {
            InputActionAsset resource =
                Resources.Load<InputActionAsset>(
                    "DefaultGameplayInput");
            if (resource != null)
            {
                return Object.Instantiate(resource);
            }

            return InputActionCatalog.CreateDefaultAsset();
        }
    }
}
