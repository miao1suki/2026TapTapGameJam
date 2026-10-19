using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project.GridArt.Editor
{
    public enum GridArtReferenceLayer { Water, Wall, Object, EditorOverlay }

    /// <summary>Read-only presentation geometry in world XY; never a scene renderer or a prefab instance.</summary>
    public readonly struct GridArtLevelReferenceItem
    {
        public readonly string id;
        public readonly GridArtQuad quad;
        public readonly Color tint;
        public readonly GridArtReferenceLayer layer;
        public GridArtLevelReferenceItem(string id, GridArtQuad quad, Color tint, GridArtReferenceLayer layer)
        { this.id = id; this.quad = quad; this.tint = tint; this.layer = layer; }
    }

    public sealed class GridArtLevelRoom
    {
        public string id = "", label = "房间";
        public Rect boundsWorld;
    }

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
        public readonly List<GridArtLevelRoom> rooms = new List<GridArtLevelRoom>();
        public readonly List<GridArtLevelReferenceItem> referenceItems = new List<GridArtLevelReferenceItem>();
        public readonly HashSet<string> referenceOnlyNames = new HashSet<string>();
        public string currentRoomId = "";
        public Rect worldBounds;
        public bool hasWorldBounds;
        public void IncludeBounds(Rect bounds)
        {
            if (bounds.width <= 0 || bounds.height <= 0) return;
            worldBounds = hasWorldBounds ? Rect.MinMaxRect(Mathf.Min(worldBounds.xMin, bounds.xMin),
                Mathf.Min(worldBounds.yMin, bounds.yMin), Mathf.Max(worldBounds.xMax, bounds.xMax),
                Mathf.Max(worldBounds.yMax, bounds.yMax)) : bounds;
            hasWorldBounds = true;
        }
        public void AddReference(string id, GridArtQuad quad, Color tint, GridArtReferenceLayer layer)
        {
            if (quad.texture == null || quad.bounds.width <= 0 || quad.bounds.height <= 0) return;
            referenceItems.Add(new GridArtLevelReferenceItem(id, quad, tint, layer)); IncludeBounds(quad.bounds);
        }
        public void RecalculateBounds()
        {
            hasWorldBounds = false; worldBounds = default;
            foreach (var room in rooms) IncludeBounds(room.boundsWorld);
            foreach (var group in groups) foreach (var bounds in group.bounds) IncludeBounds(bounds);
            foreach (var item in referenceItems) IncludeBounds(item.quad.bounds);
        }
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
            int turns = GridArtRotation.Normalize(layer.sourceQuarterTurns);
            bool same = layer.cells.Count == cells.Count;
            if (same) foreach (var c in layer.cells)
                if (c.brush != layer.sourceBrush || c.quarterTurns != turns || !cells.Contains(c.position)) { same = false; break; }
            if (same) return false;
            layer.cells.Clear();
            // Stable serialization irrespective of scene traversal order.
            var ordered = new List<Vector2Int>(cells);
            ordered.Sort((a,b) => a.y == b.y ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            foreach (var p in ordered) layer.cells.Add(new GridArtCell(p, layer.sourceBrush, turns));
            layer.InvalidateLookup(); return true;
        }
    }
}
