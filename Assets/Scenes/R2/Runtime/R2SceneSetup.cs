using Project.ColorBlocks;
using UnityEngine;

namespace Project.Levels.ILorR
{
    /// <summary>Explicit progress for the standalone R2 test scene, not formal Gaming.</summary>
    [DefaultExecutionOrder(-900), DisallowMultipleComponent]
    [AddComponentMenu("2026TapTap/关卡/R2 开局能力")]
    public sealed class R2SceneSetup : MonoBehaviour
    {
        private void Awake()
        {
            var service = ColorRuntimeService.Instance;
            service.ResetProgress();
            foreach (string color in new[] { "red", "green", "blue" })
            {
                service.Unlock(color);
                ColorAppearanceManager.Instance.Play(color, true, 0f);
            }
            service.UnlockAbility("blue");
        }
    }
}
