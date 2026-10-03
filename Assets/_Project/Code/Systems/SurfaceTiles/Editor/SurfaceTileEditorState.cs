namespace Project.SurfaceTiles.Editor
{
    internal enum SurfaceTilePaintMode
    {
        Paint = 0,
        Erase = 1,
        Pick = 2
    }

    internal static class SurfaceTileEditorState
    {
        internal static bool Painting { get; set; }
        internal static SurfaceTilePaintMode Mode { get; set; }
        internal static string SelectedTileId { get; set; }
        internal static int QuarterTurns { get; set; }
        internal static bool FlipX { get; set; }
        internal static bool FlipY { get; set; }
        internal static SurfaceTileAnchor Anchor { get; set; } =
            SurfaceTileAnchor.BottomLeft;
        internal static bool Stack { get; set; } = true;
        internal static UnityEngine.Vector2 OffsetCells { get; set; }
        internal static float OffsetNudgeStep { get; set; } = 0.05f;
        internal static SurfaceTileFace HoverFace { get; set; }
        internal static UnityEngine.Vector2Int HoverCell { get; set; }
        internal static SurfaceTileBlock HoverBlock { get; set; }
        internal static bool UseForcedFace { get; set; }
        internal static SurfaceTileFace ForcedFace { get; set; } =
            SurfaceTileFace.Front;
    }
}
