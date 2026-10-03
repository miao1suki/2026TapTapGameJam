using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Project.SurfaceTiles.Editor
{
    public static class SurfaceTileEditorBridge
    {
        public static bool IsPainting => SurfaceTileEditorState.Painting;

        public static SurfaceTileBlock EnsurePaintable(GameObject target)
        {
            if (target == null)
            {
                return null;
            }

            UnityEditor.Selection.activeGameObject = target;
            return SurfaceTileAuthoringService.MakeSelectedObjectPaintable();
        }

        public static void RefreshPreview(SurfaceTileBlock block)
        {
            if (block != null)
            {
                SurfaceTileMeshBuilder.RefreshPreview(block);
            }
        }

        public static Mesh BuildPreviewMesh(SurfaceTileBlock block)
        {
            return block != null
                ? SurfaceTileMeshBuilder.BuildCellMesh(block)
                : null;
        }

        public static void SetPainting(bool painting)
        {
            SurfaceTileEditorState.Painting = painting;
            if (!painting)
            {
                SurfaceTileEditorState.HoverBlock = null;
            }
        }

        public static void SetPaintMode(int mode)
        {
            SurfaceTileEditorState.Mode =
                (SurfaceTilePaintMode)Mathf.Clamp(mode, 0, 2);
        }

        public static void SetSelectedTile(string tileId)
        {
            SurfaceTileEditorState.SelectedTileId = tileId;
        }

        public static void OpenTileImporter()
        {
            SurfaceTileSheetImporterWindow.OpenWindow();
        }

        public static SurfaceTilePalette CreatePaletteFromSelection()
        {
            return SurfaceTileAuthoringService.CreatePaletteFromSelection();
        }

        public static bool GeneratePaletteFromSheet(
            string sourceAssetPath,
            string outputFolder,
            string outputName,
            int minimumCompletePixels,
            int minimumPiecePixels,
            int mergeGap,
            int margin,
            int outputWidth,
            int outputHeight,
            int sourcePixelsPerCell,
            bool includeSeparatePieces,
            out string message)
        {
            message = string.Empty;
            Texture2D source = AssetDatabase.LoadAssetAtPath<Texture2D>(
                sourceAssetPath);
            if (source == null)
            {
                message = $"找不到源图片：{sourceAssetPath}";
                return false;
            }

            if (!SurfaceTileSheetGenerator.TryLoadOriginal(
                    source,
                    out Texture2D preview,
                    out string error))
            {
                message = error;
                return false;
            }

            try
            {
                List<RectInt> detected =
                    SurfaceTileSheetGenerator
                        .DetectCompleteRegionsWithOptionalPieces(
                            preview,
                            0,
                            minimumCompletePixels,
                            minimumPiecePixels,
                            mergeGap,
                            margin,
                            includeSeparatePieces);
                if (detected.Count == 0)
                {
                    message = "没有识别到可用的透明区域。";
                    return false;
                }

                FilterLabelRegions(preview, detected);
                if (detected.Count == 0)
                {
                    message = "没有识别到可用的透明区域。";
                    return false;
                }

                EnsureFolder(outputFolder);
                string recipePath = outputFolder + "/" +
                                    outputName + "_SurfaceTileImport.asset";
                SurfaceTileSheetImportRecipe recipe =
                    AssetDatabase.LoadAssetAtPath<
                        SurfaceTileSheetImportRecipe>(recipePath);
                if (recipe == null)
                {
                    recipe = ScriptableObject.CreateInstance<
                        SurfaceTileSheetImportRecipe>();
                    AssetDatabase.CreateAsset(recipe, recipePath);
                }

                recipe.ConfigureSource(source);
                recipe.ConfigureOutput(
                    outputFolder,
                    outputName,
                    outputWidth,
                    outputHeight,
                    sourcePixelsPerCell,
                    4,
                    true,
                    true,
                    SurfaceTileOutputAnchor.Center);
                recipe.Regions.Clear();
                for (int index = 0; index < detected.Count; index++)
                {
                    recipe.Regions.Add(new SurfaceTileSourceRegion(
                        "Tile_" + (index + 1).ToString("D3"),
                        detected[index]));
                }

                EditorUtility.SetDirty(recipe);
                AssetDatabase.SaveAssetIfDirty(recipe);

                if (!SurfaceTileSheetGenerator.Generate(
                        recipe,
                        out SurfaceTileSheetGenerateResult result,
                        out error))
                {
                    message = error;
                    return false;
                }

                message =
                    $"生成 {result.TileCount} 个瓦片：" +
                    result.PalettePath;
                return true;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(preview);
            }
        }

        private static void EnsureFolder(string folder)
        {
            folder = string.IsNullOrWhiteSpace(folder)
                ? "Assets"
                : folder.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
        }

        private static void FilterLabelRegions(
            Texture2D source,
            List<RectInt> regions)
        {
            Color32[] pixels = source.GetPixels32();
            for (int index = regions.Count - 1; index >= 0; index--)
            {
                if (IsLikelyLabelRegion(
                        pixels,
                        source.width,
                        source.height,
                        regions[index]))
                {
                    regions.RemoveAt(index);
                }
            }
        }

        private static bool IsLikelyLabelRegion(
            Color32[] pixels,
            int width,
            int height,
            RectInt rect)
        {
            int topBandStart = height - 380;
            if (rect.yMin < topBandStart || rect.height > 120)
            {
                return false;
            }

            int opaque = 0;
            int blue = 0;
            int xMax = Mathf.Min(width, rect.xMax);
            int yMax = Mathf.Min(height, rect.yMax);
            for (int y = Mathf.Max(0, rect.yMin); y < yMax; y++)
            {
                int row = y * width;
                for (int x = Mathf.Max(0, rect.xMin); x < xMax; x++)
                {
                    Color32 pixel = pixels[row + x];
                    if (pixel.a < 32)
                    {
                        continue;
                    }

                    opaque++;
                    if (pixel.b > pixel.r * 1.15f &&
                        pixel.b > pixel.g * 1.08f &&
                        pixel.b > 80)
                    {
                        blue++;
                    }
                }
            }

            return opaque > 0 &&
                   (blue / (float)opaque > .35f ||
                    rect.height <= 72);
        }
    }
}
