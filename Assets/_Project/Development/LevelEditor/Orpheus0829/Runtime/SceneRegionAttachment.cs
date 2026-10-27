using UnityEngine;

namespace Project.LevelEditor
{
    /// <summary>Explicit opt-in for non-grid decorations that should follow a room assembly move.</summary>
    [DisallowMultipleComponent, AddComponentMenu("2026TapTap/关卡/区域附属物")]
    public sealed class SceneRegionAttachment : MonoBehaviour
    {
        [SerializeField] private string regionId;
        public string RegionId => regionId;
        public void SetRegion(string value) => regionId = value ?? string.Empty;
    }
}
