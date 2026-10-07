using UnityEngine;

namespace Project.BlockFeatures
{
    /// <summary>
    /// Exposes the world-space size of one level-editor grid cell.
    /// </summary>
    public interface IGridCellSizeProvider
    {
        float CellWorldSize { get; }
    }

    public static class GridCellSizeUtility
    {
        public const float FallbackCellWorldSize = 1f;

        public static float Resolve(Component owner)
        {
            if (owner == null)
            {
                return FallbackCellWorldSize;
            }

            MonoBehaviour[] components =
                owner.GetComponentsInParent<MonoBehaviour>(true);
            for (int index = 0;
                 index < components.Length;
                 index++)
            {
                if (components[index] is
                    IGridCellSizeProvider provider)
                {
                    float size = provider.CellWorldSize;
                    if (size > 0f && !float.IsNaN(size))
                    {
                        return size;
                    }
                }
            }

            return FallbackCellWorldSize;
        }
    }
}
