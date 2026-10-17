using UnityEngine;

namespace Project.ObjectArtwork
{
    /// <summary>Shared conversion between authored pixels and gameplay grid cells.</summary>
    public static class GridArtworkMetrics
    {
        public const int PixelsPerCell = 16;
        // This sheet's 4x export has been verified against every RGBA block.
        // Other artwork stays 1x until independently verified or set explicitly.
        public static int SourcePixelScale(Sprite sprite) => sprite == null ? 1 : SourcePixelScale(sprite.texture);
        public static int SourcePixelScale(Texture2D texture) => texture != null && texture.name == "tail" &&
            texture.width == 1408 && texture.height == 704 ? 4 : 1;
        public static Vector2 PixelsToCells(Vector2 pixels, int sourcePixelScale = 1) => pixels / (PixelsPerCell * Mathf.Max(1, sourcePixelScale));
        public static Vector2 PixelsToWorld(Vector2 pixels, float cellWorldSize, int sourcePixelScale = 1) => PixelsToCells(pixels, sourcePixelScale) * Mathf.Max(.001f, cellWorldSize);
        public static float PixelWorldSize(float cellWorldSize) => Mathf.Max(.001f, cellWorldSize) / PixelsPerCell;
        public static Vector2Int PlacementFootprint(Vector2 pixels, int sourcePixelScale = 1)
        {
            Vector2 cells = PixelsToCells(pixels, sourcePixelScale);
            return new Vector2Int(Mathf.Max(1, Mathf.CeilToInt(cells.x - .0001f)), Mathf.Max(1, Mathf.CeilToInt(cells.y - .0001f)));
        }
        public static Vector2 SpriteCells(Sprite sprite, int sourcePixelScale = 1) => sprite == null ? Vector2.zero : PixelsToCells(sprite.rect.size, sourcePixelScale);
        public static Vector3 NaturalSpriteScale(Sprite sprite, Vector3 parentScale, float cellWorldSize, int sourcePixelScale = 1)
        {
            float worldUnits = Mathf.Max(.001f, cellWorldSize) * (sprite == null ? 1f : sprite.pixelsPerUnit / (PixelsPerCell * Mathf.Max(1, sourcePixelScale)));
            return new Vector3(worldUnits / Mathf.Max(.001f, Mathf.Abs(parentScale.x)),
                worldUnits / Mathf.Max(.001f, Mathf.Abs(parentScale.y)), 1f / Mathf.Max(.001f, Mathf.Abs(parentScale.z)));
        }
        // Authoring/import diagnostic only. A verified nearest-neighbor export
        // multiplier is not a licence to infer the artist's cell size by shape.
        public static int ExactExportScale(Color32[] pixels, int width, int height, int maximum = 8)
        {
            if (pixels == null || width <= 0 || height <= 0 || pixels.Length != width * height) return 1;
            for (int scale = Mathf.Min(maximum, Mathf.Min(width, height)); scale >= 2; scale--)
            {
                if (width % scale != 0 || height % scale != 0) continue;
                bool matches = true;
                for (int y = 0; y < height && matches; y++)
                    for (int x = 0; x < width; x++)
                    {
                        Color32 actual = pixels[y * width + x], reference = pixels[(y / scale * scale) * width + x / scale * scale];
                        if (actual.a == 0 && reference.a == 0) continue;
                        if (actual.r == reference.r && actual.g == reference.g && actual.b == reference.b && actual.a == reference.a) continue;
                        matches = false; break;
                    }
                if (matches) return scale;
            }
            return 1;
        }
        public static void ConfigurePixelSampling(MaterialPropertyBlock properties, Sprite sprite, float cellWorldSize)
        {
            properties.SetFloat("_ArtworkPixelWorldSize", PixelWorldSize(cellWorldSize));
            if (sprite == null || sprite.texture == null) return;
            properties.SetVector("_ArtworkPixelTextureSize", new Vector4(sprite.texture.width, sprite.texture.height,
                1f / sprite.texture.width, 1f / sprite.texture.height));
            Rect rect = sprite.textureRect;
            properties.SetVector("_ArtworkPixelUVRect", new Vector4(rect.xMin / sprite.texture.width,
                rect.yMin / sprite.texture.height, rect.xMax / sprite.texture.width, rect.yMax / sprite.texture.height));
        }
    }
}
