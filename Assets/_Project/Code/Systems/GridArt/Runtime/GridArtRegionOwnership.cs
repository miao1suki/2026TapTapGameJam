using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.GridArt
{
    [Serializable]
    public sealed class GridArtRegionOwnershipEntry
    {
        public string layerId;
        public Vector2Int anchorCell;
        public string regionId;
    }

    /// <summary>Sparse ownership for authored art. Existing cells are unowned until explicitly claimed.</summary>
    [CreateAssetMenu(fileName = "GridArtRegionOwnership", menuName = "2026TapTap/Grid Art/区域归属")]
    public sealed class GridArtRegionOwnership : ScriptableObject
    {
        [SerializeField] private List<GridArtRegionOwnershipEntry> entries = new();
        public IReadOnlyList<GridArtRegionOwnershipEntry> Entries => entries;

        public string Get(string layerId, Vector2Int cell)
        {
            GridArtRegionOwnershipEntry entry = entries.Find(item => item.layerId == layerId && item.anchorCell == cell);
            return entry?.regionId ?? string.Empty;
        }

        public bool Set(string layerId, Vector2Int cell, string regionId)
        {
            int index = entries.FindIndex(item => item.layerId == layerId && item.anchorCell == cell);
            if (string.IsNullOrEmpty(regionId))
            {
                if (index < 0) return false;
                entries.RemoveAt(index); return true;
            }
            if (index >= 0)
            {
                if (entries[index].regionId == regionId) return false;
                entries[index].regionId = regionId; return true;
            }
            entries.Add(new GridArtRegionOwnershipEntry { layerId = layerId, anchorCell = cell, regionId = regionId });
            return true;
        }

        public int Claim(string layerId, IEnumerable<Vector2Int> cells, string regionId)
        {
            int changed = 0;
            if (cells != null) foreach (Vector2Int cell in cells) if (Set(layerId, cell, regionId)) changed++;
            return changed;
        }
    }

    /// <summary>Added only by an explicit author action, keeping legacy GridArtScene serialization untouched.</summary>
    [AddComponentMenu("")]
    public sealed class GridArtRegionOwnershipLink : MonoBehaviour
    {
        [SerializeField, HideInInspector] private GridArtRegionOwnership ownership;
        public GridArtRegionOwnership Ownership => ownership;
        public void SetOwnership(GridArtRegionOwnership value) => ownership = value;
    }
}
