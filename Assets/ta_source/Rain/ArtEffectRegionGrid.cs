using Project.BlockFeatures;
using UnityEngine;

namespace Project.ArtEffects
{
    /// <summary>Authoring metadata for an independent art root; never a gameplay room or color object.</summary>
    [DisallowMultipleComponent]
    public sealed class ArtEffectRegionGrid : MonoBehaviour, IGridCellSizeProvider
    {
        [SerializeField, Min(.01f), InspectorName("每格世界尺寸")] private float cellWorldSize = 1f;
        public float CellWorldSize => float.IsFinite(cellWorldSize) ? Mathf.Max(.01f, cellWorldSize) : 1f;
        public void Configure(float size) { cellWorldSize = float.IsFinite(size) ? Mathf.Max(.01f, size) : 1f; }
    }
}
