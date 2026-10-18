using UnityEngine;

namespace Project.GreenHook
{
    // Runtime-only companion: camera projection is final by this point.
    [AddComponentMenu(""), DisallowMultipleComponent, DefaultExecutionOrder(1100)]
    public sealed class GreenHookVisualDriver : MonoBehaviour
    {
        private GreenHookAbility ability;
        private void Awake() => ability = GetComponent<GreenHookAbility>();
        private void LateUpdate()
        {
            if (ability != null && ability.isActiveAndEnabled) ability.RenderFrame();
        }
    }
}
