using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project.GridArt.Editor
{
    // The planning editor registers the adapter; GridArt never depends on Assembly-CSharp-Editor.
    public sealed class GridArtLevelGroup
    {
        public string id = "", label = "整关墙地";
        public readonly List<Rect> bounds = new List<Rect>(); // World XY, not planning Y-down.
    }
    public sealed class GridArtLevelSnapshot
    {
        public string label = "当前场景";
        public float cellSize = 1;
        public readonly List<GridArtLevelGroup> groups = new List<GridArtLevelGroup>();
        public HashSet<Vector2Int> Cells(string group, float size)
        {
            var bounds = new List<Bounds>();
            foreach (var g in groups)
                if (string.IsNullOrEmpty(group) || g.id == group)
                    foreach (var r in g.bounds) bounds.Add(new Bounds(r.center, new Vector3(r.width, r.height, 1)));
            return GridArtGeometry.Cells(bounds, size);
        }
    }
    public static class GridArtLevelSource
    {
        public static Func<Scene, GridArtLevelSnapshot> Read;
        public static event Action Changed;
        public static bool HasListeners => Changed != null;
        public static void NotifyChanged() => Changed?.Invoke();
        public static bool Synchronize(GridArtLayer layer, HashSet<Vector2Int> cells)
        {
            bool same = layer.cells.Count == cells.Count;
            if (same) foreach (var c in layer.cells)
                if (c.brush != layer.sourceBrush || !cells.Contains(c.position)) { same = false; break; }
            if (same) return false;
            layer.cells.Clear();
            // Stable serialization irrespective of scene traversal order.
            var ordered = new List<Vector2Int>(cells);
            ordered.Sort((a,b) => a.y == b.y ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            foreach (var p in ordered) layer.cells.Add(new GridArtCell(p, layer.sourceBrush));
            layer.InvalidateLookup(); return true;
        }
    }
}
