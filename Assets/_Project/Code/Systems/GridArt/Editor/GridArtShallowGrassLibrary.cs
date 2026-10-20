using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Project.ObjectArtwork;
using UnityEditor;
using UnityEngine;

namespace Project.GridArt.Editor
{
    /// <summary>Append-only native-pixel combinations, kept outside the editable shared palette.</summary>
    public static class GridArtShallowGrassLibrary
    {
        public const string SourcePath = "Assets/_Project/Resources/26TAPTAP/场景背景/1c7a785acdf1fc73e58c896f2a52c234.png";
        public const string PalettePath = "Assets/_Project/Content/Art/ShallowGrassConnections.asset";
        public const string Prefix = "shallow-grass-v1:";
        // Clockwise from the top. A set bit means that grass is present on that edge.
        public const int Up = 1, Right = 2, Down = 4, Left = 8;
        private static readonly string[] Colors = { "red", "green", "blue" };
        private static readonly string[] ColorNames = { "红色", "绿色", "蓝色" };

        public static int Import(GridArtPalette destination)
        {
            if (destination == null || !AssetDatabase.Contains(destination) || EditorApplication.isPlayingOrWillChangePlaymode)
                return 0;
            var source = AssetDatabase.LoadAssetAtPath<Texture2D>(SourcePath);
            if (source == null) return 0;
            var selections = AssetDatabase.LoadAssetAtPath<GridArtPalette>(PalettePath);
            if (selections == null) selections = BuildSelections();
            int added = 0;
            foreach (var brush in selections.brushes)
            {
                // Never edit a pre-existing brush: maps reference list indices and artists may rename it.
                if (destination.brushes.Any(b => b.sourceKey == brush.sourceKey)) continue;
                destination.brushes.Add(new GridArtBrush {
                    sourceKey = brush.sourceKey, label = brush.label, category = brush.category,
                    sprite = brush.sprite, size = Vector2Int.one, visualSize = Vector2.one,
                    sourcePixelScale = 1, colorId = brush.colorId
                });
                added++;
            }
            if (added > 0) EditorUtility.SetDirty(destination);
            return added;
        }

        public static GridArtPalette BuildSelections()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GridArtPalette>(PalettePath);
            if (existing != null) return existing;
            var importer = AssetImporter.GetAtPath(SourcePath) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("未找到浅草连接原图。");
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 128;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureType = TextureImporterType.Default;
            importer.spritePixelsPerUnit = GridArtworkMetrics.PixelsPerCell;
            importer.alphaIsTransparency = false;
            importer.ClearPlatformTextureSettings("Standalone");
            importer.ClearPlatformTextureSettings("Android");
            importer.SaveAndReimport();
            var decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!decoded.LoadImage(File.ReadAllBytes(SourcePath)))
                    throw new InvalidOperationException("无法读取浅草连接原图。");
                ValidateSource(decoded);
                var palette = ScriptableObject.CreateInstance<GridArtPalette>();
                palette.name = "浅草·三色各向连接";
                GridArtBuilder.EnsureFolder("Assets/_Project/Content/Art");
                AssetDatabase.CreateAsset(palette, PalettePath);
                var atlas = BuildAtlas(decoded);
                atlas.name = "ShallowGrass_Native16_Combinations";
                AssetDatabase.AddObjectToAsset(atlas, palette);
                for (int color = 0; color < 3; color++)
                {
                    // Keep all original selections verbatim, including their asymmetric edge motifs.
                    for (int variant = 0; variant < 3; variant++)
                        Add(color, "original:" + variant, "原图 " + (variant + 1),
                            new Rect((16 + variant) * 16, color * 16, 16, 16), "浅草·原绘连接");
                    for (int mask = 1; mask < 16; mask++)
                        Add(color, "edges:" + mask, EdgeName(mask), new Rect(mask * 16, color * 16, 16, 16), "浅草·全部方向组合");
                }
                void Add(int color, string key, string label, Rect rect, string category)
                {
                    var sprite = Sprite.Create(atlas, rect, Vector2.one * .5f, GridArtworkMetrics.PixelsPerCell, 0, SpriteMeshType.FullRect);
                    sprite.name = "ShallowGrass_" + Colors[color] + "_" + key.Replace(':', '_');
                    AssetDatabase.AddObjectToAsset(sprite, palette);
                    palette.brushes.Add(new GridArtBrush { sourceKey = Prefix + Colors[color] + ":" + key,
                        label = ColorNames[color] + "浅草·" + label, category = category, sprite = sprite,
                        size = Vector2Int.one, visualSize = Vector2.one, sourcePixelScale = 1, colorId = Colors[color] });
                }
                EditorUtility.SetDirty(palette);
                AssetDatabase.SaveAssetIfDirty(palette);
                return palette;
            }
            finally { UnityEngine.Object.DestroyImmediate(decoded); }
        }

        public static void ValidateSource(Texture2D source)
        {
            if (source.width != 89 || source.height != 113 ||
                GridArtworkMetrics.ExactExportScale(source.GetPixels32(), source.width, source.height) != 1)
                throw new InvalidOperationException("浅草原图必须是已核对的89×113原生像素版；不能自动缩放或猜切片。");
        }

        public static Texture2D BuildAtlas(Texture2D source)
        {
            ValidateSource(source);
            const int width = 19 * 16, height = 3 * 16;
            var result = new Texture2D(width, height, TextureFormat.RGBA32, false)
                { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[width * height];
            for (int color = 0; color < 3; color++)
            {
                int originX = 5 + color * 32;
                // Preserve every original 16x16 frame without cropping to the visible silhouette.
                for (int original = 0; original < 3; original++)
                for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++)
                    pixels[(color * 16 + y) * width + (16 + original) * 16 + x] =
                        source.GetPixel(originX + x, source.height - 1 - (32 + original * 32 + 15 - y));
                for (int mask = 1; mask < 16; mask++)
                for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++)
                {
                    // Exact native RGBA copies: no interpolation, rescaling, mirroring or arbitrary rotation.
                    // The full frame has a two-pixel perimeter. Removing an edge leaves the original
                    // neighbouring edge's end cap intact; corners do not become semi-transparent holes.
                    bool keep = (mask & Up) != 0 && y >= 14 || (mask & Right) != 0 && x >= 14 ||
                        (mask & Down) != 0 && y < 2 || (mask & Left) != 0 && x < 2;
                    if (keep) pixels[(color * 16 + y) * width + mask * 16 + x] =
                        source.GetPixel(originX + x, source.height - 1 - (32 + 15 - y));
                }
            }
            result.SetPixels32(pixels); result.Apply(false, false);
            return result;
        }

        private static string EdgeName(int mask)
        {
            var names = new List<string>();
            if ((mask & Up) != 0) names.Add("上");
            if ((mask & Right) != 0) names.Add("右");
            if ((mask & Down) != 0) names.Add("下");
            if ((mask & Left) != 0) names.Add("左");
            return string.Join("＋", names);
        }

        [MenuItem("Tools/2026TapTap/关卡美术/汇入浅草各向连接")]
        public static void ImportShared()
        {
            var palette = GridArtLibraryService.Shared();
            Undo.RecordObject(palette, "汇入浅草各向连接");
            int count = Import(palette);
            AssetDatabase.SaveAssetIfDirty(palette);
            EditorGUIUtility.PingObject(palette);
            Debug.Log($"浅草连接已汇入，新增{count}项；三色各15种方向组合及3种原绘切片。", palette);
        }
    }
}
