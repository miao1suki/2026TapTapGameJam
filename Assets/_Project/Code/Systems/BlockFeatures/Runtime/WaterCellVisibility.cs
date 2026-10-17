using System.Collections.Generic;
using Project.LevelEditor;
using UnityEngine;

namespace Project.BlockFeatures
{
    // A grid cache, not a scene scan per target/frame. Walls remain gameplay objects;
    // only water presentation and its interaction preview are masked below them.
    public static class WaterCellVisibility
    {
        private static int revision = -1;
        private static readonly HashSet<(int scene, float size, int x, int y)> walls = new HashSet<(int, float, int, int)>();
        public static bool Covered(Component owner, Vector3 point, float cellSize)
        {
            if (revision != LevelEditorPlacedBlock.ActiveRevision)
            {
                walls.Clear();
                foreach (var block in LevelEditorPlacedBlock.ActiveInstances)
                {
                    if (block == null || block.IsProp) continue;
                    var collider = block.GetComponent<Collider>();
                    if (collider == null || !collider.enabled || collider.isTrigger || block.GetComponent<WaterVisualFeature>() != null) continue;
                    for (int y = block.Cell.y; y < block.Cell.y + block.SizeCells.y; y++)
                        for (int x = block.Cell.x; x < block.Cell.x + block.SizeCells.x; x++)
                            if (block.ContainsCell(new Vector2Int(x,y))) walls.Add((block.gameObject.scene.handle, block.CellWorldSize, x, y));
                }
                revision = LevelEditorPlacedBlock.ActiveRevision;
            }
            return walls.Contains((owner.gameObject.scene.handle, cellSize,
                Mathf.FloorToInt(point.x / cellSize), Mathf.FloorToInt(point.y / cellSize)));
        }

        public static bool TryVisiblePoint(WaterVisualFeature owner, Collider volume, Vector3 origin, float radius, out Vector3 point)
        {
            point = default;
            float cell = GridCellSizeUtility.Resolve(owner);
            Bounds bounds = volume.bounds;
            float best = radius * radius;
            bool found = false;
            int x0 = Mathf.FloorToInt(Mathf.Max(bounds.min.x, origin.x - radius) / cell);
            int y0 = Mathf.FloorToInt(Mathf.Max(bounds.min.y, origin.y - radius) / cell);
            int x1 = Mathf.CeilToInt(Mathf.Min(bounds.max.x, origin.x + radius) / cell);
            int y1 = Mathf.CeilToInt(Mathf.Min(bounds.max.y, origin.y + radius) / cell);
            for (int y = y0; y < y1; y++) for (int x = x0; x < x1; x++)
            {
                var center = new Vector3((x + .5f) * cell, (y + .5f) * cell, bounds.center.z);
                if (Covered(owner, center, cell)) continue;
                var candidate = volume.ClosestPoint(new Vector3(
                    Mathf.Clamp(origin.x, x * cell + .002f * cell, (x + 1) * cell - .002f * cell),
                    Mathf.Clamp(origin.y, y * cell + .002f * cell, (y + 1) * cell - .002f * cell), origin.z));
                float distance = (candidate - origin).sqrMagnitude;
                if (distance > best) continue;
                found = true; best = distance; point = candidate;
            }
            return found;
        }
    }
}
