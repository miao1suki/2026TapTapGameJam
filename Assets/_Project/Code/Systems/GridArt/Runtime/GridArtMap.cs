using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.GridArt
{
    [Serializable]
    public sealed class GridArtCell
    {
        public Vector2Int position;
        public int brush;
        public GridArtCell(Vector2Int value, int index) { position = value; brush = index; }
    }

    [Serializable]
    public sealed class GridArtLayer
    {
        public string id = Guid.NewGuid().ToString("N");
        public string label = "美术层";
        public bool visible = true;
        public bool locked;
        public float depth = 2f;
        public Vector2 parallax = Vector2.one;
        public Color tint = Color.white;
        public bool lighting3D;
        public int sortingOrder = int.MinValue;
        public bool followLevel;
        public string sourceGroup = "";
        public int sourceBrush;
        public List<GridArtCell> cells = new List<GridArtCell>();
        [NonSerialized] private Dictionary<Vector2Int, int> lookup;
        [NonSerialized] private int lookupCount = -1;
        [NonSerialized] private GridArtPalette erasePalette;
        [NonSerialized] private List<(Vector2Int position, Vector2Int size)> largeCells;
        public void InvalidateLookup() { lookup = null; lookupCount = -1; largeCells = null; }
        private void EnsureLookup()
        {
            if (lookup != null && lookupCount == cells.Count) return;
            lookup = new Dictionary<Vector2Int, int>(cells.Count);
            for (int i = 0; i < cells.Count; i++) lookup[cells[i].position] = i;
            lookupCount = cells.Count;
        }
        public int Find(Vector2Int p)
        {
            EnsureLookup();
            if (!lookup.TryGetValue(p, out int i)) return -1;
            if (i < cells.Count && cells[i].position == p) return i;
            InvalidateLookup(); EnsureLookup(); return lookup.TryGetValue(p, out i) ? i : -1;
        }
        public bool Set(Vector2Int p, int brush)
        {
            int i = Find(p);
            if (i >= 0 && cells[i].brush == brush) return false;
            largeCells = null;
            if (i >= 0) cells[i].brush = brush;
            else { lookup[p] = cells.Count; cells.Add(new GridArtCell(p, brush)); lookupCount = cells.Count; }
            return true;
        }
        public bool Erase(Vector2Int p, GridArtPalette palette)
        {
            EnsureLookup();
            if (largeCells == null || erasePalette != palette)
            {
                largeCells = new List<(Vector2Int,Vector2Int)>(); erasePalette = palette;
                foreach (var c in cells)
                {
                    var b = palette != null ? palette.Get(c.brush) : null;
                    if (b != null && !b.autoTerrain && b.Size != Vector2Int.one) largeCells.Add((c.position,b.Size));
                }
            }
            bool Remove(Vector2Int position)
            {
                if (!lookup.TryGetValue(position,out int index)) return false;
                int last = cells.Count-1; var value = cells[last];
                cells[index]=value; lookup[value.position]=index; cells.RemoveAt(last); lookup.Remove(position); lookupCount=cells.Count; return true;
            }
            bool changed = Remove(p);
            for (int i=largeCells.Count-1;i>=0;i--)
                if (new RectInt(largeCells[i].position,largeCells[i].size).Contains(p))
                { changed |= Remove(largeCells[i].position); largeCells.RemoveAt(i); }
            return changed;
        }
    }

    public sealed class GridArtMap : ScriptableObject
    {
        public GridArtPalette palette;
        [HideInInspector] public string ownerSceneGuid;
        [Min(.01f)] public float cellSize = 1f;
        public List<GridArtLayer> layers = new List<GridArtLayer>();
        [HideInInspector] public int revision;
        [HideInInspector] public int bakedRevision = -1;
        [HideInInspector] public string bakedPaletteHash;
        public Vector2Int WorldToCell(Vector3 world, Vector3 origin) => new Vector2Int(
            Mathf.FloorToInt((world.x - origin.x) / Mathf.Max(.01f, cellSize)),
            Mathf.FloorToInt((world.y - origin.y) / Mathf.Max(.01f, cellSize)));
    }

    public enum GridArtCorner { Fill, HorizontalEdge, VerticalEdge, OuterCorner, InnerCorner }

    public static class GridArtRules
    {
        public static GridArtCorner Resolve(bool horizontal, bool vertical, bool diagonal)
        {
            if (!horizontal && !vertical) return GridArtCorner.OuterCorner;
            if (!horizontal) return GridArtCorner.VerticalEdge;
            if (!vertical) return GridArtCorner.HorizontalEdge;
            return diagonal ? GridArtCorner.Fill : GridArtCorner.InnerCorner;
        }

        // q: bottom-left, bottom-right, top-left, top-right.
        public static int Slot(GridArtCorner kind, int q)
        {
            int x = q % 2 == 0 ? 0 : 2;
            int y = q < 2 ? 2 : 0;
            switch (kind)
            {
                case GridArtCorner.HorizontalEdge: return y * 3 + 1;
                case GridArtCorner.VerticalEdge: return 3 + x;
                case GridArtCorner.OuterCorner: return y * 3 + x;
                case GridArtCorner.InnerCorner: return 9 + q;
                default: return 4;
            }
        }

        public static bool Same(Dictionary<Vector2Int, int> cells, Vector2Int p, int brush)
            => cells.TryGetValue(p, out int value) && value == brush;
    }
}
