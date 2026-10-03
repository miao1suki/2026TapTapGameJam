using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.ColorBlocks
{
    [Serializable]
    public sealed class ColorGraphNode
    {
        public string id = Guid.NewGuid().ToString("N");
        public string title = "新节点";
        public Vector2 position;
    }

    [Serializable]
    public sealed class ColorGraphEdge
    {
        public string fromId;
        public string toId;
    }

    [Serializable]
    public sealed class ColorTypeDefinition
    {
        public string id = Guid.NewGuid().ToString("N");
        public string displayName = "新颜色";
        public Color swatch = Color.white;
        [Range(0, 31)] public int unityLayer = -1;
        public Material targetMaterial;
        public string unlockEventId;
        public List<ColorGraphNode> nodes = new List<ColorGraphNode>();
        public List<ColorGraphEdge> edges = new List<ColorGraphEdge>();
    }

    [CreateAssetMenu(menuName = "2026TapTap/颜色/颜色目录", fileName = "ColorCatalog")]
    public sealed class ColorCatalog : ScriptableObject
    {
        [SerializeField] private Material neutralMaterial;
        [SerializeField] private List<ColorTypeDefinition> colors =
            new List<ColorTypeDefinition>();

        public Material NeutralMaterial => neutralMaterial;
        public IReadOnlyList<ColorTypeDefinition> Colors => colors;

        public ColorTypeDefinition Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            return colors.Find(color => color != null && color.id == id);
        }

#if UNITY_EDITOR
        public List<ColorTypeDefinition> EditableColors => colors;
        public void SetNeutralMaterial(Material material) => neutralMaterial = material;
#endif
    }
}
