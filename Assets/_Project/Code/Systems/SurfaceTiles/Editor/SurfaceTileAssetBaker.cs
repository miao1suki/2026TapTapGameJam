using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Project;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Project.SurfaceTiles.Editor
{
    internal static class SurfaceTileAssetBaker
    {
        private const string GeneratedRoot =
            "Assets/_Project/Generated/SurfaceTiles";
        private const string SurfaceShaderName =
            "2026TapTap/Surface Tiles/Unlit Cutout";
        private const string BakeShaderName =
            "Hidden/2026TapTap/Surface Tile Bake";
        private const int MaximumTextureSize = 8192;

        internal static bool Bake(SurfaceTileBlock block, out string message)
        {
            message = string.Empty;
            if (block == null || block.Palette == null)
            {
                message = "请先选择一个可贴画方块并指定瓦片库。";
                return false;
            }

            if (!block.Palette.UsesSingleTexture())
            {
                message = "当前瓦片库包含多张源图片，请拆成多个瓦片库。";
                return false;
            }

            Vector2Int tilePixels = block.Palette.BakePixelsPerCell;
            BlockLayout layout = BuildLayout(block, tilePixels);
            if (layout.Width <= 0 || layout.Height <= 0 ||
                layout.Width > MaximumTextureSize || layout.Height > MaximumTextureSize)
            {
                message = $"合成贴图尺寸 {layout.Width}×{layout.Height} 超出限制 {MaximumTextureSize}。";
                return false;
            }

            Shader bakeShader = Shader.Find(BakeShaderName);
            Shader surfaceShader = Shader.Find(SurfaceShaderName);
            if (bakeShader == null || surfaceShader == null)
            {
                message = "表面瓦片 Shader 尚未编译完成，请等待 Unity 编译后重试。";
                return false;
            }

            EnsureUniqueId(block);
            string sceneName = string.IsNullOrWhiteSpace(block.gameObject.scene.name)
                ? "UnsavedScene"
                : Sanitize(block.gameObject.scene.name);
            string folder = EnsureFolder(GeneratedRoot, sceneName);
            string prefix = Sanitize(block.name) + "_" + block.BlockId.Substring(0, 8);
            string texturePath = $"{folder}/{prefix}_Tiles.png";
            string materialPath = $"{folder}/{prefix}_Tiles.mat";
            string meshPath = $"{folder}/{prefix}_Tiles.asset";

            Texture2D atlas = RenderAtlas(block, layout, bakeShader);
            if (atlas == null)
            {
                message = "合成贴图失败。";
                return false;
            }

            byte[] png = atlas.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(atlas);
            File.WriteAllBytes(Path.GetFullPath(texturePath), png);
            AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceSynchronousImport);
            ConfigureTextureImporter(texturePath);
            Texture2D importedTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);

            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(surfaceShader)
                {
                    name = prefix + "_Tiles"
                };
                AssetDatabase.CreateAsset(material, materialPath);
            }
            else
            {
                material.shader = surfaceShader;
            }

            material.SetTexture("_BaseMap", importedTexture);
            material.mainTexture = importedTexture;
            material.SetFloat("_Cutoff", 0.1f);
            EditorUtility.SetDirty(material);

            Dictionary<SurfaceTileFace, SurfaceTileBakedFace> bakedFaces =
                layout.BakedFaces();
            Mesh generated = SurfaceTileMeshBuilder.BuildBakedMesh(
                block,
                bakedFaces);
            generated.name = Path.GetFileNameWithoutExtension(meshPath);
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (mesh == null)
            {
                mesh = generated;
                AssetDatabase.CreateAsset(mesh, meshPath);
            }
            else
            {
                EditorUtility.CopySerialized(generated, mesh);
                UnityEngine.Object.DestroyImmediate(generated);
                EditorUtility.SetDirty(mesh);
            }

            SurfaceTileMeshBuilder.EnsureOutput(block);
            Undo.RecordObject(block, "烘焙表面瓦片");
            Undo.RecordObject(block.OutputFilter, "烘焙表面瓦片");
            Undo.RecordObject(block.OutputRenderer, "烘焙表面瓦片");
            block.BindBake(mesh, material);
            EditorUtility.SetDirty(block);
            EditorSceneManager.MarkSceneDirty(block.gameObject.scene);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(importedTexture);
            message =
                $"已保存并在 Project 中高亮合成贴图：\n{texturePath}\n\n" +
                $"磁盘完整位置：\n{Path.GetFullPath(folder)}";
            return true;
        }

        private static Texture2D RenderAtlas(
            SurfaceTileBlock block,
            BlockLayout layout,
            Shader shader)
        {
            RenderTexture target = RenderTexture.GetTemporary(
                layout.Width,
                layout.Height,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB);
            RenderTexture previous = RenderTexture.active;
            Material material = new Material(shader);
            try
            {
                RenderTexture.active = target;
                GL.Clear(true, true, Color.clear);
                GL.PushMatrix();
                GL.LoadPixelMatrix(0f, layout.Width, 0f, layout.Height);
                List<SurfaceTilePlacement> placements = block.Placements
                    .OrderBy(item => item.Layer)
                    .ToList();
                for (int index = 0; index < placements.Count; index++)
                {
                    SurfaceTilePlacement placement = placements[index];
                    if (!block.Palette.TryGet(
                            placement.TileId,
                            out SurfaceTilePalette.Entry entry) ||
                        entry.Sprite == null ||
                        !layout.FaceRects.TryGetValue(
                            placement.Face,
                            out RectInt faceRect) ||
                        !layout.FaceCellBounds.TryGetValue(
                            placement.Face,
                            out Rect faceBounds))
                    {
                        continue;
                    }

                    Vector2Int grid = block.GetGridSize(placement.Face);
                    Rect baseRect = SurfaceTileGeometry.GetPlacementRect(
                        entry,
                        placement.Cell,
                        placement.QuarterTurns,
                        placement.Anchor);
                    if (!SurfaceTileGeometry.PlacementFitsGrid(
                            baseRect,
                            grid))
                    {
                        continue;
                    }

                    Rect placementRect = SurfaceTileGeometry.GetPlacementRect(
                        entry,
                        placement);

                    Sprite sprite = entry.Sprite;
                    material.SetTexture("_MainTex", sprite.texture);
                    material.SetPass(0);
                    Rect spriteUv = SurfaceTileMeshBuilder.ContentUv(
                        sprite,
                        entry.ContentRect);
                    float x0 = faceRect.x +
                               (placementRect.xMin - faceBounds.xMin) *
                               layout.TilePixels.x;
                    float y0 = faceRect.y +
                               (placementRect.yMin - faceBounds.yMin) *
                               layout.TilePixels.y;
                    float x1 = faceRect.x +
                               (placementRect.xMax - faceBounds.xMin) *
                               layout.TilePixels.x;
                    float y1 = faceRect.y +
                               (placementRect.yMax - faceBounds.yMin) *
                               layout.TilePixels.y;
                    Vector2[] logical =
                    {
                        new Vector2(0f, 0f),
                        new Vector2(1f, 0f),
                        new Vector2(1f, 1f),
                        new Vector2(0f, 1f)
                    };
                    Vector2[] output =
                    {
                        new Vector2(x0, y0),
                        new Vector2(x1, y0),
                        new Vector2(x1, y1),
                        new Vector2(x0, y1)
                    };
                    GL.Begin(GL.QUADS);
                    for (int corner = 0; corner < 4; corner++)
                    {
                        Vector2 tileUv = SurfaceTileMeshBuilder.TransformTileUv(
                            logical[corner],
                            placement.QuarterTurns,
                            placement.FlipX,
                            placement.FlipY);
                        GL.TexCoord2(
                            spriteUv.x + tileUv.x * spriteUv.width,
                            spriteUv.y + tileUv.y * spriteUv.height);
                        GL.Vertex3(output[corner].x, output[corner].y, 0f);
                    }
                    GL.End();
                }

                GL.PopMatrix();
                Texture2D texture = new Texture2D(
                    layout.Width,
                    layout.Height,
                    TextureFormat.RGBA32,
                    false,
                    false);
                texture.ReadPixels(new Rect(0f, 0f, layout.Width, layout.Height), 0, 0);
                texture.Apply(false, false);
                return texture;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.DestroyImmediate(material);
            }
        }

        private static BlockLayout BuildLayout(
            SurfaceTileBlock block,
            Vector2Int tilePixels)
        {
            Rect[] bounds = new Rect[6];
            Vector2Int[] pixelSizes = new Vector2Int[6];
            for (int index = 0; index < 6; index++)
            {
                SurfaceTileFace face = (SurfaceTileFace)index;
                bounds[index] = GetFaceCellBounds(block, face, tilePixels);
                pixelSizes[index] = new Vector2Int(
                    Mathf.Max(1, Mathf.RoundToInt(
                        bounds[index].width * tilePixels.x)),
                    Mathf.Max(1, Mathf.RoundToInt(
                        bounds[index].height * tilePixels.y)));
            }

            int firstRowWidth = pixelSizes[0].x + pixelSizes[1].x +
                                pixelSizes[2].x;
            int secondRowWidth = pixelSizes[3].x + pixelSizes[4].x +
                                 pixelSizes[5].x;
            int firstRowHeight = Mathf.Max(
                pixelSizes[0].y,
                Mathf.Max(pixelSizes[1].y, pixelSizes[2].y));
            int secondRowHeight = Mathf.Max(
                pixelSizes[3].y,
                Mathf.Max(pixelSizes[4].y, pixelSizes[5].y));
            BlockLayout layout = new BlockLayout(
                Mathf.Max(firstRowWidth, secondRowWidth),
                firstRowHeight + secondRowHeight,
                tilePixels);
            int x = 0;
            for (int index = 0; index < 6; index++)
            {
                if (index == 3)
                {
                    x = 0;
                }

                int y = index < 3 ? 0 : firstRowHeight;
                SurfaceTileFace face = (SurfaceTileFace)index;
                layout.FaceRects[face] = new RectInt(
                    x,
                    y,
                    pixelSizes[index].x,
                    pixelSizes[index].y);
                layout.FaceCellBounds[face] = bounds[index];
                x += pixelSizes[index].x;
            }

            return layout;
        }

        private static Rect GetFaceCellBounds(
            SurfaceTileBlock block,
            SurfaceTileFace face,
            Vector2Int tilePixels)
        {
            Vector2Int grid = block.GetGridSize(face);
            Rect bounds = new Rect(0f, 0f, grid.x, grid.y);
            IReadOnlyList<SurfaceTilePlacement> placements = block.Placements;
            for (int index = 0; index < placements.Count; index++)
            {
                SurfaceTilePlacement placement = placements[index];
                if (placement.Face != face ||
                    !block.Palette.TryGet(
                        placement.TileId,
                        out SurfaceTilePalette.Entry entry))
                {
                    continue;
                }

                Rect placementRect = SurfaceTileGeometry.GetPlacementRect(
                    entry,
                    placement);
                float minX = Mathf.Min(bounds.xMin, placementRect.xMin);
                float minY = Mathf.Min(bounds.yMin, placementRect.yMin);
                float maxX = Mathf.Max(bounds.xMax, placementRect.xMax);
                float maxY = Mathf.Max(bounds.yMax, placementRect.yMax);
                bounds = Rect.MinMaxRect(minX, minY, maxX, maxY);
            }

            float xStep = 1f / Mathf.Max(1, tilePixels.x);
            float yStep = 1f / Mathf.Max(1, tilePixels.y);
            return Rect.MinMaxRect(
                Mathf.Floor(bounds.xMin / xStep) * xStep,
                Mathf.Floor(bounds.yMin / yStep) * yStep,
                Mathf.Ceil(bounds.xMax / xStep) * xStep,
                Mathf.Ceil(bounds.yMax / yStep) * yStep);
        }

        private static void EnsureUniqueId(SurfaceTileBlock block)
        {
            block.EnsureBlockId();
            IReadOnlyList<SurfaceTileBlock> all =
                ProjectDiscovery.FindAll<SurfaceTileBlock>(true);
            for (int index = 0; index < all.Count; index++)
            {
                if (all[index] != block && all[index].BlockId == block.BlockId)
                {
                    block.RegenerateBlockId();
                    return;
                }
            }
        }

        private static void ConfigureTextureImporter(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport();
        }

        private static string EnsureFolder(string root, string child)
        {
            string[] rootParts = root.Split('/');
            string current = rootParts[0];
            for (int index = 1; index < rootParts.Length; index++)
            {
                string next = current + "/" + rootParts[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, rootParts[index]);
                }
                current = next;
            }

            string childPath = current + "/" + child;
            if (!AssetDatabase.IsValidFolder(childPath))
            {
                AssetDatabase.CreateFolder(current, child);
            }
            return childPath;
        }

        private static string Sanitize(string value)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(invalid, '_');
            }
            return string.IsNullOrWhiteSpace(value) ? "SurfaceTileBlock" : value;
        }

        private sealed class BlockLayout
        {
            internal BlockLayout(int width, int height, Vector2Int tilePixels)
            {
                Width = width;
                Height = height;
                TilePixels = tilePixels;
            }

            internal int Width { get; }
            internal int Height { get; }
            internal Vector2Int TilePixels { get; }
            internal Dictionary<SurfaceTileFace, RectInt> FaceRects { get; } =
                new Dictionary<SurfaceTileFace, RectInt>();
            internal Dictionary<SurfaceTileFace, Rect> FaceCellBounds { get; } =
                new Dictionary<SurfaceTileFace, Rect>();

            internal Dictionary<SurfaceTileFace, SurfaceTileBakedFace> BakedFaces()
            {
                Dictionary<SurfaceTileFace, SurfaceTileBakedFace> result =
                    new Dictionary<SurfaceTileFace, SurfaceTileBakedFace>();
                foreach (KeyValuePair<SurfaceTileFace, RectInt> pair in FaceRects)
                {
                    RectInt rect = pair.Value;
                    Rect uvRect = new Rect(
                        (float)rect.x / Width,
                        (float)rect.y / Height,
                        (float)rect.width / Width,
                        (float)rect.height / Height);
                    result[pair.Key] = new SurfaceTileBakedFace(
                        uvRect,
                        FaceCellBounds[pair.Key]);
                }
                return result;
            }
        }
    }
}
