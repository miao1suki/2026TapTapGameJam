using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.GridArt
{
    [Serializable]
    public sealed class GridArtBrush
    {
        public string label = "新笔刷";
        public string sourceKey;
        public string category = "装饰";
        public bool autoTerrain;
        public Sprite sprite;
        public Vector2Int size = Vector2Int.one;
        public Vector2 visualSize;
        public Rect contentRect = new Rect(0, 0, 1, 1);
        public int sourcePixelScale = 1;
        public string colorId = "";
        public Texture2D normalMap;
        // 0..8: top-left to bottom-right. 9..12: inner corners BL,BR,TL,TR.
        public Sprite[] pieces = new Sprite[13];
        public Vector2Int Size => new Vector2Int(Mathf.Max(1, size.x), Mathf.Max(1, size.y));
        public Vector2 DisplaySize => visualSize.x > 0 && visualSize.y > 0 ? visualSize : (Vector2)Size;
        public Sprite GetPiece(int slot) => pieces != null && slot < pieces.Length && pieces[slot] != null
            ? pieces[slot] : pieces != null && pieces.Length > 4 ? pieces[4] : null;
        public bool HasInnerCorners => pieces != null && pieces.Length >= 13 &&
            pieces[9] != null && pieces[10] != null && pieces[11] != null && pieces[12] != null;
    }

    public sealed class GridArtPalette : ScriptableObject
    {
        public List<GridArtBrush> brushes = new List<GridArtBrush>();
        public GridArtBrush Get(int index) => index >= 0 && index < brushes.Count ? brushes[index] : null;
    }
}
