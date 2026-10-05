using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.ColorBlocks
{
    public sealed class ColorTypeDefinition
    {
        public string id = Guid.NewGuid().ToString("N");
        public string displayName = "新颜色";
        public Color swatch = Color.white;
        [Range(0, 31)] public int unityLayer = -1;
        [Tooltip("方块编辑识别材质；内置钥匙也用它显示对应颜色。")]
        public Material targetMaterial;
        public string unlockEventId;
    }

    [CreateAssetMenu(menuName = "2026TapTap/颜色/颜色目录", fileName = "ColorCatalog")]
    public sealed class ColorCatalog : ScriptableObject
    {
        [SerializeField] private Material neutralMaterial;
        [SerializeField] private GameObject blueWaterPrefab;
        [SerializeField] private List<ColorTypeDefinition> colors =
            new List<ColorTypeDefinition>();

        public Material NeutralMaterial => neutralMaterial;
        public GameObject BlueWaterPrefab => blueWaterPrefab;
        public IReadOnlyList<ColorTypeDefinition> Colors => colors;

        public ColorTypeDefinition Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            return colors.Find(color => color != null && color.id == id);
        }

#if UNITY_EDITOR
        public List<ColorTypeDefinition> EditableColors => colors;
        public void SetNeutralMaterial(Material material) => neutralMaterial = material;
        public void SetBlueWaterPrefab(GameObject prefab) => blueWaterPrefab = prefab;
#endif
    }
}
