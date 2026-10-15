using UnityEngine;

namespace Project.BlockFeatures
{
    [CreateAssetMenu(menuName = "2026TapTap/水体表现配置")]
    public sealed class WaterPresentationSettings : ScriptableObject
    {
        [InspectorName("水面、水中、水下图片")] public Sprite[] tiles;
        [InspectorName("水面、水中、水下材质")] public Material[] tileMaterials;
        [InspectorName("透明扰动材质")] public Material filterMaterial;
        [InspectorName("定向气泡预制体")] public GameObject bubblePrefab;
        [InspectorName("气泡渐显材质")] public Material bubbleMaterial;
        public static WaterPresentationSettings Load() => Resources.Load<WaterPresentationSettings>("ColorBlocks/WaterPresentation");
        public bool IsValid => tiles != null && tiles.Length == 3 && tileMaterials != null && tileMaterials.Length == 3 &&
            tiles[0] && tiles[1] && tiles[2] && tileMaterials[0] && tileMaterials[1] && tileMaterials[2] && filterMaterial;
    }
}
