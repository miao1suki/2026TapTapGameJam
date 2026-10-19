using System;
using System.IO;
using System.Linq;
using Project.ObjectArtwork;
using UnityEditor;
using UnityEngine;

namespace Project.GridArt.Editor
{
    /// <summary>Reassemble existing native pixels, never scale a decorative inset into a terrain edge.</summary>
    public static class GridArtBorderLibrary
    {
        private const string AtlasName = "WhiteBorder_EdgeAligned_13";
        public static bool Ensure(GridArtPalette palette)
        {
            string key = AssetDatabase.AssetPathToGUID(GridArtProjectLibrary.BackgroundPath) + "#frame";
            var brush = palette.brushes.FirstOrDefault(b => b.sourceKey == key && b.autoTerrain);
            if (brush == null) return false;
            string path = AssetDatabase.GetAssetPath(palette);
            var assets = AssetDatabase.LoadAllAssetsAtPath(path);
            var atlas = assets.OfType<Texture2D>().FirstOrDefault(t => t.name == AtlasName);
            if (atlas == null)
            {
                var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                try
                {
                    if (!source.LoadImage(File.ReadAllBytes(GridArtProjectLibrary.BackgroundPath)))
                        throw new InvalidOperationException("白线边框源图无法读取。");
                    atlas = BuildAtlas(source); atlas.name = AtlasName;
                    AssetDatabase.AddObjectToAsset(atlas, palette);
                }
                finally { UnityEngine.Object.DestroyImmediate(source); }
            }
            bool changed = brush.pieces == null || brush.pieces.Length != 13 || brush.sourcePixelScale != 1;
            if (brush.pieces == null || brush.pieces.Length != 13) Array.Resize(ref brush.pieces, 13);
            for (int slot = 0; slot < 13; slot++)
            {
                string name = AtlasName + "_" + slot;
                var sprite = assets.OfType<Sprite>().FirstOrDefault(s => s.name == name && s.texture == atlas);
                if (sprite == null)
                {
                    sprite = Sprite.Create(atlas, new Rect(slot * 16, 0, 16, 16), Vector2.one * .5f,
                        GridArtworkMetrics.PixelsPerCell, 0, SpriteMeshType.FullRect);
                    sprite.name = name; AssetDatabase.AddObjectToAsset(sprite, palette);
                }
                if (brush.pieces[slot] != sprite) { brush.pieces[slot] = sprite; changed = true; }
            }
            brush.sourcePixelScale = 1;
            if (changed) EditorUtility.SetDirty(palette);
            return changed;
        }

        public static Texture2D BuildAtlas(Texture2D source)
        {
            if (source.width != 1408 || source.height != 704 ||
                GridArtworkMetrics.ExactExportScale(source.GetPixels32(), source.width, source.height) != 4)
                throw new InvalidOperationException("白线边框需使用已验证4倍导出的tail原图。");
            // Each original 16px slice includes a 6px inset. Extract its four-pixel
            // border and extend with the original straight-edge motifs. Inner
            // corners use the opposite exterior elbow, without resampling.
            Color32 Pixel(int x, int top) => source.GetPixel(640 + x * 4, source.height - 1 - (320 + top * 4));
            var atlas = new Texture2D(13 * 16, 16, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[13 * 16 * 16];
            for (int slot = 0; slot < 13; slot++) for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++)
            {
                Color32 c = Pixel(16, 16); // Original solid-black interior.
                if (slot < 9)
                {
                    bool left = slot % 3 == 0, right = slot % 3 == 2;
                    bool top = slot / 3 == 0, bottom = slot / 3 == 2;
                    if (top && y < 4) c = Pixel(32 + x, 6 + y);
                    if (bottom && y >= 12) c = Pixel(32 + x, 70 + y - 12);
                    if (left && x < 4) c = Pixel(6 + x, 32 + y);
                    if (right && x >= 12) c = Pixel(70 + x - 12, 32 + y);
                    if ((left && x < 4 || right && x >= 12) && (top && y < 4 || bottom && y >= 12))
                        c = Pixel((left ? 6 : 70) + x % 12, (top ? 6 : 70) + y % 12);
                }
                else
                {
                    // Slots 9..12: BL, BR, TL, TR (world-space).
                    bool left = (slot - 9) % 2 == 0, top = slot >= 11;
                    if ((left ? x < 4 : x >= 12) && (top ? y < 4 : y >= 12))
                        c = Pixel((left ? 70 : 6) + x % 12, (top ? 70 : 6) + y % 12);
                }
                pixels[(15 - y) * atlas.width + slot * 16 + x] = c;
            }
            atlas.SetPixels32(pixels); atlas.Apply(false, false); return atlas;
        }
    }
}
