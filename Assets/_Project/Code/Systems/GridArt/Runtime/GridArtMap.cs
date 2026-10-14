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
        public List<GridArtCell> cells = new List<GridArtCell>();
        public int Find(Vector2Int p) => cells.FindIndex(c => c.position == p);
        public bool Set(Vector2Int p, int brush)
        {
            int i = Find(p);
            if (i >= 0 && cells[i].brush == brush) return false;
            if (i >= 0) cells[i].brush = brush;
            else cells.Add(new GridArtCell(p, brush));
            return true;
        }
        public bool Erase(Vector2Int p, GridArtPalette palette)
        {
            return cells.RemoveAll(c =>
            {
                var b = palette != null ? palette.Get(c.brush) : null;
                Vector2Int size = b != null && !b.autoTerrain ? b.Size : Vector2Int.one;
                return new RectInt(c.position, size).Contains(p);
            }) > 0;
        }
    }

    public sealed class GridArtMap : ScriptableObject
    {
        public GridArtPalette palette;
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
