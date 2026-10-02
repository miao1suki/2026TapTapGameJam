using UnityEngine;

namespace Project.InputRebinding
{
    [DefaultExecutionOrder(-9000)]
    [DisallowMultipleComponent]
    public sealed class InputBindingBootstrap : MonoBehaviour
    {
        [SerializeField]
        private bool loadOnAwake = true;

        public InputBindingService Service { get; private set; }

        private void Awake()
        {
            Service = InputBindingService.CreateFromInputService();
            if (!loadOnAwake)
            {
                Service.ResetAll();
            }
        }
    }
}
