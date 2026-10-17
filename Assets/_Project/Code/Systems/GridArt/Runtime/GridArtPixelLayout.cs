using Project.ObjectArtwork;
using UnityEngine;

namespace Project.GridArt
{
    /// <summary>Native source pixels govern art size; logical occupancy is independent.</summary>
    public static class GridArtPixelLayout
    {
        public static Vector2 SourcePixels(Sprite sprite, Rect crop)
        {
            if (sprite == null) return Vector2.zero;
            if (crop.width <= 0 || crop.height <= 0) crop = new Rect(0, 0, 1, 1);
            return Vector2.Scale(sprite.rect.size, new Vector2(
                Mathf.Clamp(crop.width, 0, 1 - Mathf.Clamp01(crop.x)),
                Mathf.Clamp(crop.height, 0, 1 - Mathf.Clamp01(crop.y))));
        }

        public static int SourceScale(Sprite sprite, int configured = 1) => Mathf.Max(Mathf.Max(1, configured), GridArtworkMetrics.SourcePixelScale(sprite));
        public static Vector2 NativeSize(Sprite sprite, Rect crop, int sourcePixelScale = 1) => GridArtworkMetrics.PixelsToCells(SourcePixels(sprite, crop) / SourceScale(sprite, sourcePixelScale));

        public static bool IsNative(GridArtBrush brush)
        {
            if (brush == null) return true;
            if (brush.autoTerrain) return TerrainIssue(brush) == null;
            return brush.sprite == null || (brush.DisplaySize - NativeSize(brush.sprite, brush.contentRect, brush.sourcePixelScale)).sqrMagnitude < .000001f;
        }

        public static string TerrainIssue(GridArtBrush brush)
        {
            if (brush?.pieces == null) return null;
            foreach (var sprite in brush.pieces)
                if (sprite != null && !IsRuleSprite(sprite, brush.sourcePixelScale))
                    return $"边角素材“{sprite.name}”为 {sprite.rect.width:0}×{sprite.rect.height:0} 像素。自动边角需16×16原像素；大图请先按原尺寸切片。";
            return null;
        }

        public static bool IsRuleSprite(Sprite sprite, int sourcePixelScale = 1) => sprite == null ||
            sprite.rect.size == Vector2.one || sprite.rect.size / SourceScale(sprite, sourcePixelScale) == Vector2.one * GridArtworkMetrics.PixelsPerCell;

        public static bool RestoreNative(GridArtBrush brush, out string error)
        {
            error = brush == null ? "请选择素材。" : brush.autoTerrain ? TerrainIssue(brush) : null;
            if (error != null || brush == null) return false;
            if (brush.autoTerrain) { brush.size = Vector2Int.one; brush.visualSize = Vector2.one; return true; }
            if (brush.sprite == null) { error = "请先指定图片。"; return false; }
            Vector2 pixels = SourcePixels(brush.sprite, brush.contentRect) / SourceScale(brush.sprite, brush.sourcePixelScale);
            brush.visualSize = GridArtworkMetrics.PixelsToCells(pixels);
            brush.size = GridArtworkMetrics.PlacementFootprint(pixels);
            return true;
        }
    }
}
