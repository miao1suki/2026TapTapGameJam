using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Project.GridArt.Editor
{
    public static class GridArtBorderAudit
    {
        public static void Run()
        {
            if (!Application.dataPath.Replace('\\', '/').EndsWith("/Library/FlowRetirementProbe/Assets", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Only run in FlowRetirementProbe.");
            int checks = 0;
            void Check(bool ok, string detail) { checks++; if (!ok) throw new InvalidOperationException(detail); }
            try
            {
                var palette = GridArtLibraryService.Shared();
                var keys = palette.brushes.Select(b => b.sourceKey).ToArray();
                var sizes = palette.brushes.Select(b => b.DisplaySize).ToArray();
                var originals = palette.brushes.Select(b => b.sprite).ToArray();
                GridArtBorderLibrary.Ensure(palette);
                Check(keys.SequenceEqual(palette.brushes.Select(b => b.sourceKey)), "brush slots/keys unchanged");
                Check(sizes.SequenceEqual(palette.brushes.Select(b => b.DisplaySize)), "native brush sizes unchanged");
                Check(originals.SequenceEqual(palette.brushes.Select(b => b.sprite)), "ordinary source sprites unchanged");
                int index = palette.brushes.FindIndex(b => b.sourceKey != null && b.sourceKey.EndsWith("#frame") && b.autoTerrain);
                Check(index >= 0, "original white-border slot retained");
                var brush = palette.brushes[index];
                Check(brush.HasInnerCorners && GridArtPixelLayout.IsNative(brush), "all 13 rule slices are native 16px");
                var atlas = brush.pieces[0].texture;
                for (int slot = 0; slot < 13; slot++)
                {
                    var sprite = brush.pieces[slot];
                    Check(sprite.texture == atlas && sprite.rect.size == Vector2.one * 16, "one native atlas slice " + slot);
                    int white = 0;
                    for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++)
                    {
                        var color = atlas.GetPixel(slot * 16 + x, y);
                        if (color.r < .5f) continue;
                        white++;
                        Check(x < 4 || x >= 12 || y < 4 || y >= 12, "white border lies at cell boundary, not middle: " + slot);
                    }
                    Check(slot == 4 ? white == 0 : white > 0, "edges/outer/inner corners have real pixels " + slot);
                }
                var layer = new GridArtLayer();
                layer.Set(Vector2Int.zero, index); layer.Set(Vector2Int.right, index); layer.Set(Vector2Int.up, index);
                var quads = GridArtBuilder.BuildQuads(layer, palette);
                Rect inner = brush.pieces[12].rect;
                Check(Mathf.Abs(quads[3].uv.xMin - (inner.x + 8) / atlas.width) < .00001f, "L-shape selects upper-right concave corner");
                layer.Set(Vector2Int.one, index);
                quads = GridArtBuilder.BuildQuads(layer, palette);
                Check(Mathf.Abs(quads[3].uv.xMin - (brush.pieces[4].rect.x + 8) / atlas.width) < .00001f, "filled diagonal removes internal seam");
                Check(!GridArtBorderLibrary.Ensure(palette), "second repair is idempotent");
                AssetDatabase.SaveAssetIfDirty(palette);
                File.WriteAllBytes("Library/GridArtBorderAtlas.png", atlas.EncodeToPNG());
                WritePreview(palette, index);
                string result = "PASS " + checks + ": white border aligned; 13 rules; original slots/size/art unchanged";
                File.WriteAllText("Library/GridArtBorderAudit.txt", result); Debug.Log(result); EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                string result = "FAIL " + checks + "\n" + error;
                File.WriteAllText("Library/GridArtBorderAudit.txt", result); Debug.LogError(result); EditorApplication.Exit(1);
            }
        }

        private static void WritePreview(GridArtPalette palette, int brush)
        {
            // Left to right: isolated cell, straight strip, concave L, enclosed hole.
            // All silhouettes use the real builder; the capture is 16 native pixels/cell.
            var layer = new GridArtLayer();
            layer.Set(Vector2Int.zero, brush);
            for (int x = 2; x < 6; x++) layer.Set(new Vector2Int(x, 0), brush);
            layer.Set(new Vector2Int(8, 0), brush);
            layer.Set(new Vector2Int(9, 0), brush);
            layer.Set(new Vector2Int(8, 1), brush);
            for (int y = 0; y < 3; y++) for (int x = 12; x < 15; x++)
                if (x != 13 || y != 1) layer.Set(new Vector2Int(x, y), brush);
            const int width = 256, height = 80, scale = 3;
            var native = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color32[width * height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(44, 51, 62, 255);
            foreach (var q in GridArtBuilder.BuildQuads(layer, palette))
            {
                int left = Mathf.RoundToInt(q.bounds.xMin * 16) + 8;
                int bottom = Mathf.RoundToInt(q.bounds.yMin * 16) + 8;
                int w = Mathf.RoundToInt(q.bounds.width * 16), h = Mathf.RoundToInt(q.bounds.height * 16);
                for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
                {
                    float u = (x + .5f) / w, v = (y + .5f) / h;
                    Vector2 uv = Vector2.Lerp(Vector2.Lerp(q.UV(0), q.UV(3), u), Vector2.Lerp(q.UV(1), q.UV(2), u), v);
                    Color32 color = q.texture.GetPixel(Mathf.FloorToInt(uv.x * q.texture.width), Mathf.FloorToInt(uv.y * q.texture.height));
                    pixels[(bottom + y) * width + left + x] = color;
                }
            }
            native.SetPixels32(pixels); native.Apply(false, false);
            File.WriteAllBytes("Library/GridArtBorderPreviewNative.png", native.EncodeToPNG());
            // Integer-only presentation enlargement; no filtering/interpolation.
            var enlarged = new Texture2D(width * scale, height * scale, TextureFormat.RGBA32, false);
            var display = new Color32[width * height * scale * scale];
            for (int y = 0; y < height * scale; y++) for (int x = 0; x < width * scale; x++)
                display[y * width * scale + x] = pixels[(y / scale) * width + x / scale];
            enlarged.SetPixels32(display); enlarged.Apply(false, false);
            File.WriteAllBytes("Library/GridArtBorderPreview.png", enlarged.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(enlarged); UnityEngine.Object.DestroyImmediate(native);
        }
    }
}
