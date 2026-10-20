using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Project.GridArt.Editor
{
    public static class GridArtShallowGrassAudit
    {
        public static void Run()
        {
            if (!Application.dataPath.Replace('\\', '/').Contains("/Library/GrassConnections/Assets"))
                throw new InvalidOperationException("浅草审计只能在隔离GrassConnections工程执行，不保存作者场景。");
            int assertions = 0;
            void Check(bool ok, string detail)
            {
                assertions++;
                if (!ok) throw new InvalidOperationException(detail);
            }
            try
            {
                var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                source.LoadImage(File.ReadAllBytes(GridArtShallowGrassLibrary.SourcePath));
                GridArtShallowGrassLibrary.ValidateSource(source);
                var palette = GridArtShallowGrassLibrary.BuildSelections();
                Check(palette.brushes.Count == 54, "three colours x (15 nonempty combinations + 3 original selections)");
                var atlas = palette.brushes[0].sprite.texture;
                for (int color = 0; color < 3; color++)
                {
                    for (int variant = 0; variant < 3; variant++)
                    for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++)
                    {
                        Color32 expected = source.GetPixel(5 + color * 32 + x, source.height - 1 - (32 + variant * 32 + 15 - y));
                        Color32 actual = atlas.GetPixel((16 + variant) * 16 + x, color * 16 + y);
                        Check(expected.Equals(actual), "original RGBA is preserved exactly");
                    }
                    for (int mask = 1; mask < 16; mask++)
                    for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++)
                    {
                        bool keep = (mask & 1) != 0 && y >= 14 || (mask & 2) != 0 && x >= 14 ||
                            (mask & 4) != 0 && y < 2 || (mask & 8) != 0 && x < 2;
                        Color32 expected = keep ? (Color32)source.GetPixel(5 + color * 32 + x, source.height - 1 - (32 + 15 - y)) : new Color32();
                        Color32 actual = atlas.GetPixel(mask * 16 + x, color * 16 + y);
                        Check(expected.Equals(actual), "combination retains exact native source edge pixels");
                    }
                }
                var shared = GridArtLibraryService.Shared();
                var before = shared.brushes.Select(b => JsonUtility.ToJson(b)).ToArray();
                var oldSprites = shared.brushes.Select(b => b.sprite).ToArray();
                int alreadyPresent = shared.brushes.Count(b => b.sourceKey != null && b.sourceKey.StartsWith(GridArtShallowGrassLibrary.Prefix, StringComparison.Ordinal));
                int added = GridArtShallowGrassLibrary.Import(shared);
                Check(added == 54 - alreadyPresent, "all selections appended once");
                Check(before.SequenceEqual(shared.brushes.Take(before.Length).Select(b => JsonUtility.ToJson(b))), "all existing brush fields and indices retained");
                Check(oldSprites.SequenceEqual(shared.brushes.Take(before.Length).Select(b => b.sprite)), "old sprite references preserved");
                Check(GridArtShallowGrassLibrary.Import(shared) == 0, "repeat synchronization is idempotent");
                foreach (var brush in shared.brushes.Where(b => b.sourceKey != null && b.sourceKey.StartsWith(GridArtShallowGrassLibrary.Prefix, StringComparison.Ordinal)))
                {
                    Check(brush.sprite.rect.size == Vector2.one * 16 && brush.sprite.pixelsPerUnit == 16 &&
                        brush.DisplaySize == Vector2.one && brush.Size == Vector2Int.one, "native16 / one cell / full frame");
                    Check(brush.colorId == "red" || brush.colorId == "green" || brush.colorId == "blue", "HSV colour attribution");
                }
                var importer = (TextureImporter)AssetImporter.GetAtPath(GridArtShallowGrassLibrary.SourcePath);
                Check(!importer.mipmapEnabled && importer.filterMode == FilterMode.Point && importer.npotScale == TextureImporterNPOTScale.None &&
                    importer.textureCompression == TextureImporterCompression.Uncompressed && importer.maxTextureSize == 128, "native source importer");
                AssetDatabase.SaveAssetIfDirty(shared);
                File.WriteAllBytes("Library/ShallowGrassAtlas.png", atlas.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(source);
                string report = "PASS " + assertions + " assertions: native source, 54 selections, 45 edge combinations, source RGBA, append-only stable indices, idempotence and colour attribution; no scene saved.";
                File.WriteAllText("Library/ShallowGrassAudit.txt", report);
                Debug.Log(report);
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                File.WriteAllText("Library/ShallowGrassAudit.txt", "FAIL " + assertions + "\n" + error);
                Debug.LogException(error);
                EditorApplication.Exit(1);
            }
        }
    }
}
