using Project.ColorBlocks;
using UnityEngine;

namespace TaSource.GivingColor
{
    /// <summary>Test-only receiver used by the temporary Test-scene targets.</summary>
    [DisallowMultipleComponent]
    public sealed class GivingColorTestReceiver : MonoBehaviour, IColorApplicationTarget
    {
        public bool CanApplyColor(string colorId, GameObject actor)
        {
            return isActiveAndEnabled &&
                   ColorRuntimeService.Existing != null &&
                   ColorRuntimeService.Existing.IsUnlocked(colorId);
        }

        public bool ApplyColor(string colorId, GameObject actor)
        {
            return CanApplyColor(colorId, actor);
        }
    }
}
