using Project.ColorBlocks;
using UnityEngine;

namespace Project.Levels.ILorR
{
    /// <summary>R1's explicit starting progress; never installed in other scenes.</summary>
    [DefaultExecutionOrder(-900), DisallowMultipleComponent]
    [AddComponentMenu("2026TapTap/关卡/R1 开局能力")]
    public sealed class R1SceneSetup : MonoBehaviour
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
            service.UnlockAbility("red");
        }
    }
}
