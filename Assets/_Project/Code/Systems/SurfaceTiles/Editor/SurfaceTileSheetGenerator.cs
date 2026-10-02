using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Project.SurfaceTiles.Editor
{
    internal readonly struct SurfaceTileSheetGenerateResult
    {
        internal SurfaceTileSheetGenerateResult(
            SurfaceTilePalette palette,
            string texturePath,
            string palettePath,
            int tileCount)
        {
            Palette = palette;
            TexturePath = texturePath;
            PalettePath = palettePath;
            TileCount = tileCount;
        }

        internal SurfaceTilePalette Palette { get; }
        internal string TexturePath { get; }
        internal string PalettePath { get; }
        internal int TileCount { get; }
    }

    internal static class SurfaceTileSheetGenerator
    {
        private const string SurfaceShaderName =
            "2026TapTap/Surface Tiles/Unlit Cutout";
        private const int MaximumAtlasSize = 8192;

        internal static bool TryLoadOriginal(
            Texture2D sourceAsset,
            out Texture2D texture,
            out string error)
        {
            texture = null;
            error = string.Empty;
            if (sourceAsset == null)
            {
                error = "请先选择一张源图片。";
                return false;
            }

            string assetPath = AssetDatabase.GetAssetPath(sourceAsset);
            if (string.IsNullOrWhiteSpace(assetPath) ||
                !File.Exists(Path.GetFullPath(assetPath)))
            {
                error = "源图片必须位于当前 Unity 项目的 Assets 目录内。";
                return false;
            }

            byte[] bytes = File.ReadAllBytes(Path.GetFullPath(assetPath));
            texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false)
            {
                name = sourceAsset.name + "_ImportPreview",
                filterMode = FilterMode.Point,
                hideFlags = HideFlags.HideAndDontSave
            };
            if (!texture.LoadImage(bytes, false))
            {
                UnityEngine.Object.DestroyImmediate(texture);
                texture = null;
                error = "无法读取源图片。请使用 PNG、JPG 或 Unity 支持的图片格式。";
                return false;
            }

            return true;
        }

        internal static bool Generate(
            SurfaceTileSheetImportRecipe recipe,
            out SurfaceTileSheetGenerateResult result,
            out string error)
        {
            result = default;
            error = string.Empty;
            if (recipe == null || recipe.SourceTexture == null)
            {
                error = "导入方案没有指定源图片。";
                return false;
            }

            List<SurfaceTileSourceRegion> regions = recipe.Regions
                .Where(item => item != null && item.Enabled &&
                               item.Rect.width > 0 && item.Rect.height > 0)
                .ToList();
            if (regions.Count == 0)
            {
                error = "至少需要一个启用的瓦片选区。";
                return false;
            }

            if (!TryLoadOriginal(recipe.SourceTexture, out Texture2D source, out error))
            {
                return false;
            }

            Texture2D atlas = null;
            try
            {
                int columns = Mathf.CeilToInt(Mathf.Sqrt(regions.Count));
                int rows = Mathf.CeilToInt((float)regions.Count / columns);
                int atlasWidth = columns * recipe.OutputWidth;
                int atlasHeight = rows * recipe.OutputHeight;
                if (atlasWidth > MaximumAtlasSize || atlasHeight > MaximumAtlasSize)
                {
                    error = $"输出图集 {atlasWidth}×{atlasHeight} 超过 " +
                            $"{MaximumAtlasSize} 上限。请减小瓦片尺寸或分成多个库。";
                    return false;
                }

                Color32[] sourcePixels = source.GetPixels32();
                Color32[] atlasPixels = new Color32[atlasWidth * atlasHeight];
                List<string> names = CreateUniqueNames(regions);
                List<RectInt> contentRects = new List<RectInt>(regions.Count);
                List<Vector2> sizesInCells = new List<Vector2>(regions.Count);
                for (int index = 0; index < regions.Count; index++)
                {
                    RectInt rect = ClampRect(
                        regions[index].Rect,
                        source.width,
                        source.height);
                    if (recipe.TrimTransparentPixels)
                    {
                        rect = Trim(rect, sourcePixels, source.width);
                    }

                    if (rect.width <= 0 || rect.height <= 0)
                    {
                        error = $"选区“{names[index]}”不包含可见像素。";
                        return false;
                    }

                    int cellX = index % columns * recipe.OutputWidth;
                    int cellY = index / columns * recipe.OutputHeight;
                    RectInt contentRect = CopyToCell(
                        sourcePixels,
                        source.width,
                        rect,
                        atlasPixels,
                        atlasWidth,
                        cellX,
                        cellY,
                        recipe.OutputWidth,
                        recipe.OutputHeight,
                        recipe.TransparentPadding,
                        recipe.AllowUpscale,
                        recipe.Anchor);
                    contentRects.Add(contentRect);
                    sizesInCells.Add(new Vector2(
                        rect.width / (float)recipe.SourcePixelsPerCell,
                        rect.height / (float)recipe.SourcePixelsPerCell));
                }

                atlas = new Texture2D(
                    atlasWidth,
                    atlasHeight,
                    TextureFormat.RGBA32,
                    false,
                    false);
                atlas.SetPixels32(atlasPixels);
                atlas.Apply(false, false);

                string folder = EnsureFolder(recipe.OutputFolder);
                string prefix = Sanitize(recipe.OutputName);
                string texturePath = folder + "/" + prefix + "_Atlas.png";
                string palettePath = folder + "/" + prefix + "_Palette.asset";
                string materialPath = folder + "/" + prefix + "_Preview.mat";
                File.WriteAllBytes(
                    Path.GetFullPath(texturePath),
                    atlas.EncodeToPNG());
                AssetDatabase.ImportAsset(
                    texturePath,
                    ImportAssetOptions.ForceSynchronousImport);
                ConfigureAtlasImporter(texturePath, atlasWidth, atlasHeight);
                Texture2D importedAtlas =
                    AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);

                SurfaceTilePalette palette =
                    AssetDatabase.LoadAssetAtPath<SurfaceTilePalette>(palettePath);
                if (palette == null)
                {
                    palette = ScriptableObject.CreateInstance<SurfaceTilePalette>();
                    palette.name = prefix + "_Palette";
                    AssetDatabase.CreateAsset(palette, palettePath);
                }

                Sprite[] previousSprites = AssetDatabase
                    .LoadAllAssetsAtPath(palettePath)
                    .OfType<Sprite>()
                    .ToArray();
                List<Sprite> sprites = new List<Sprite>(regions.Count);
                for (int index = 0; index < regions.Count; index++)
                {
                    int cellX = index % columns * recipe.OutputWidth;
                    int cellY = index / columns * recipe.OutputHeight;
                    Sprite sprite = Sprite.Create(
                        importedAtlas,
                        new Rect(
                            cellX,
                            cellY,
                            recipe.OutputWidth,
                            recipe.OutputHeight),
                        new Vector2(0.5f, 0.5f),
                        recipe.OutputWidth,
                        0,
                        SpriteMeshType.FullRect);
                    sprite.name = names[index];
                    AssetDatabase.AddObjectToAsset(sprite, palette);
                    sprites.Add(sprite);
                }

                palette.ReplaceTilesPreservingIds(sprites);
                palette.SetBakePixelsPerCell(new Vector2Int(
                    recipe.OutputWidth,
                    recipe.OutputHeight));
                for (int index = 0; index < names.Count; index++)
                {
                    int cellX = index % columns * recipe.OutputWidth;
                    int cellY = index / columns * recipe.OutputHeight;
                    RectInt content = contentRects[index];
                    palette.ConfigureTileLayout(
                        names[index],
                        sizesInCells[index],
                        new Rect(
                            (content.x - cellX) / (float)recipe.OutputWidth,
                            (content.y - cellY) / (float)recipe.OutputHeight,
                            content.width / (float)recipe.OutputWidth,
                            content.height / (float)recipe.OutputHeight));
                }
                for (int index = 0; index < previousSprites.Length; index++)
                {
                    if (previousSprites[index] != null)
                    {
                        UnityEngine.Object.DestroyImmediate(
                            previousSprites[index],
                            true);
                    }
                }

                Shader shader = Shader.Find(SurfaceShaderName);
                if (shader == null)
                {
                    error = $"找不到 {SurfaceShaderName}，请等待 Unity 编译完成后重试。";
                    return false;
                }

                Material material = AssetDatabase.LoadAssetAtPath<Material>(
                    materialPath);
                if (material == null)
                {
                    material = new Material(shader)
                    {
                        name = prefix + "_Preview"
                    };
                    AssetDatabase.CreateAsset(material, materialPath);
                }
                else
                {
                    material.shader = shader;
                }

                material.SetTexture("_BaseMap", importedAtlas);
                material.SetColor("_BaseColor", Color.white);
                material.SetFloat("_Cutoff", 0.1f);
                palette.SetPreviewMaterial(material);
                EditorUtility.SetDirty(material);
                EditorUtility.SetDirty(palette);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(
                    palettePath,
                    ImportAssetOptions.ForceSynchronousImport);
                result = new SurfaceTileSheetGenerateResult(
                    palette,
                    texturePath,
                    palettePath,
                    regions.Count);
                return true;
            }
            finally
            {
                if (atlas != null)
                {
                    UnityEngine.Object.DestroyImmediate(atlas);
                }

                UnityEngine.Object.DestroyImmediate(source);
            }
        }

        internal static List<RectInt> DetectRegions(
            Texture2D source,
            byte alphaThreshold,
            int minimumOpaquePixels,
            int mergeGap,
            int margin,
            int componentMinimumOpaquePixels = -1)
        {
            List<ComponentBounds> components = new List<ComponentBounds>();
            if (source == null)
            {
                return new List<RectInt>();
            }

            Color32[] pixels = source.GetPixels32();
            int width = source.width;
            int height = source.height;
            byte[] visited = new byte[pixels.Length];
            int[] queue = new int[pixels.Length];
            for (int start = 0; start < pixels.Length; start++)
            {
                if (visited[start] != 0 || pixels[start].a <= alphaThreshold)
                {
                    continue;
                }

                int head = 0;
                int tail = 0;
                queue[tail++] = start;
                visited[start] = 1;
                int minX = start % width;
                int maxX = minX;
                int minY = start / width;
                int maxY = minY;
                int count = 0;
                while (head < tail)
                {
                    int current = queue[head++];
                    int x = current % width;
                    int y = current / width;
                    count++;
                    minX = Mathf.Min(minX, x);
                    maxX = Mathf.Max(maxX, x);
                    minY = Mathf.Min(minY, y);
                    maxY = Mathf.Max(maxY, y);
                    Visit(x - 1, y, width, height, pixels, visited, queue, ref tail, alphaThreshold);
                    Visit(x + 1, y, width, height, pixels, visited, queue, ref tail, alphaThreshold);
                    Visit(x, y - 1, width, height, pixels, visited, queue, ref tail, alphaThreshold);
                    Visit(x, y + 1, width, height, pixels, visited, queue, ref tail, alphaThreshold);
                }

                int componentMinimum = componentMinimumOpaquePixels >= 0
                    ? componentMinimumOpaquePixels
                    : minimumOpaquePixels;
                if (count >= Mathf.Max(1, componentMinimum))
                {
                    components.Add(new ComponentBounds(
                        new RectInt(
                            minX,
                            minY,
                            maxX - minX + 1,
                            maxY - minY + 1),
                        count));
                }
            }

            MergeComponents(components, Mathf.Max(0, mergeGap));
            return components
                .Where(item => item.OpaquePixels >=
                               Mathf.Max(1, minimumOpaquePixels))
                .Select(item => Expand(
                    item.Rect,
                    Mathf.Max(0, margin),
                    width,
                    height))
                .OrderByDescending(item => item.y)
                .ThenBy(item => item.x)
                .ToList();
        }

        internal static List<RectInt> DetectCompleteRegionsWithOptionalPieces(
            Texture2D source,
            byte alphaThreshold,
            int minimumCompleteOpaquePixels,
            int minimumPieceOpaquePixels,
            int mergeGap,
            int margin,
            bool includeSeparatePieces)
        {
            List<RectInt> complete = DetectRegions(
                source,
                alphaThreshold,
                minimumCompleteOpaquePixels,
                mergeGap,
                margin,
                minimumPieceOpaquePixels);
            if (!includeSeparatePieces)
            {
                return complete;
            }

            List<RectInt> pieces = DetectRegions(
                source,
                alphaThreshold,
                minimumPieceOpaquePixels,
                0,
                margin,
                minimumPieceOpaquePixels);
            foreach (RectInt piece in pieces)
            {
                if (!complete.Contains(piece))
                {
                    complete.Add(piece);
                }
            }

            return complete;
        }

        private static void Visit(
            int x,
            int y,
            int width,
            int height,
            Color32[] pixels,
            byte[] visited,
            int[] queue,
            ref int tail,
            byte alphaThreshold)
        {
            if (x < 0 || y < 0 || x >= width || y >= height)
            {
                return;
            }

            int index = y * width + x;
            if (visited[index] != 0 || pixels[index].a <= alphaThreshold)
            {
                return;
            }

            visited[index] = 1;
            queue[tail++] = index;
        }

        private static void MergeComponents(
            List<ComponentBounds> components,
            int gap)
        {
            bool changed;
            do
            {
                changed = false;
                for (int left = 0; left < components.Count && !changed; left++)
                {
                    RectInt expanded = ExpandUnclamped(components[left].Rect, gap);
                    for (int right = left + 1; right < components.Count; right++)
                    {
                        if (!expanded.Overlaps(components[right].Rect))
                        {
                            continue;
                        }

                        components[left] = new ComponentBounds(
                            Union(components[left].Rect, components[right].Rect),
                            components[left].OpaquePixels +
                            components[right].OpaquePixels);
                        components.RemoveAt(right);
                        changed = true;
                        break;
                    }
                }
            } while (changed);
        }

        private static RectInt CopyToCell(
            Color32[] source,
            int sourceWidth,
            RectInt rect,
            Color32[] target,
            int targetWidth,
            int cellX,
            int cellY,
            int cellWidth,
            int cellHeight,
            int padding,
            bool allowUpscale,
            SurfaceTileOutputAnchor anchor)
        {
            int availableWidth = Mathf.Max(1, cellWidth - padding * 2);
            int availableHeight = Mathf.Max(1, cellHeight - padding * 2);
            float scale = Mathf.Min(
                (float)availableWidth / rect.width,
                (float)availableHeight / rect.height);
            if (!allowUpscale)
            {
                scale = Mathf.Min(1f, scale);
            }

            int drawWidth = Mathf.Max(1, Mathf.RoundToInt(rect.width * scale));
            int drawHeight = Mathf.Max(1, Mathf.RoundToInt(rect.height * scale));
            int offsetX = cellX + (cellWidth - drawWidth) / 2;
            int offsetY = anchor == SurfaceTileOutputAnchor.BottomCenter
                ? cellY + padding
                : cellY + (cellHeight - drawHeight) / 2;
            for (int y = 0; y < drawHeight; y++)
            {
                int sourceY = rect.y + Mathf.Min(
                    rect.height - 1,
                    y * rect.height / drawHeight);
                int targetY = offsetY + y;
                for (int x = 0; x < drawWidth; x++)
                {
                    int sourceX = rect.x + Mathf.Min(
                        rect.width - 1,
                        x * rect.width / drawWidth);
                    target[targetY * targetWidth + offsetX + x] =
                        source[sourceY * sourceWidth + sourceX];
                }
            }

            return new RectInt(offsetX, offsetY, drawWidth, drawHeight);
        }

        private static RectInt Trim(
            RectInt rect,
            Color32[] pixels,
            int sourceWidth)
        {
            int minX = rect.xMax;
            int minY = rect.yMax;
            int maxX = rect.xMin - 1;
            int maxY = rect.yMin - 1;
            for (int y = rect.yMin; y < rect.yMax; y++)
            {
                for (int x = rect.xMin; x < rect.xMax; x++)
                {
                    if (pixels[y * sourceWidth + x].a == 0)
                    {
                        continue;
                    }

                    minX = Mathf.Min(minX, x);
                    minY = Mathf.Min(minY, y);
                    maxX = Mathf.Max(maxX, x);
                    maxY = Mathf.Max(maxY, y);
                }
            }

            return maxX < minX || maxY < minY
                ? new RectInt(rect.x, rect.y, 0, 0)
                : new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }

        private static RectInt ClampRect(RectInt rect, int width, int height)
        {
            int xMin = Mathf.Clamp(rect.xMin, 0, width);
            int yMin = Mathf.Clamp(rect.yMin, 0, height);
            int xMax = Mathf.Clamp(rect.xMax, 0, width);
            int yMax = Mathf.Clamp(rect.yMax, 0, height);
            return new RectInt(
                xMin,
                yMin,
                Mathf.Max(0, xMax - xMin),
                Mathf.Max(0, yMax - yMin));
        }

        private static RectInt Expand(
            RectInt rect,
            int amount,
            int width,
            int height)
        {
            return ClampRect(ExpandUnclamped(rect, amount), width, height);
        }

        private static RectInt ExpandUnclamped(RectInt rect, int amount)
        {
            return new RectInt(
                rect.x - amount,
                rect.y - amount,
                rect.width + amount * 2,
                rect.height + amount * 2);
        }

        private static RectInt Union(RectInt left, RectInt right)
        {
            int xMin = Mathf.Min(left.xMin, right.xMin);
            int yMin = Mathf.Min(left.yMin, right.yMin);
            int xMax = Mathf.Max(left.xMax, right.xMax);
            int yMax = Mathf.Max(left.yMax, right.yMax);
            return new RectInt(xMin, yMin, xMax - xMin, yMax - yMin);
        }

        private static List<string> CreateUniqueNames(
            IReadOnlyList<SurfaceTileSourceRegion> regions)
        {
            HashSet<string> used = new HashSet<string>(StringComparer.Ordinal);
            List<string> names = new List<string>(regions.Count);
            for (int index = 0; index < regions.Count; index++)
            {
                string baseName = Sanitize(regions[index].DisplayName);
                if (string.IsNullOrWhiteSpace(baseName) || baseName == "SurfaceTiles")
                {
                    baseName = "Tile_" + (index + 1).ToString("D3");
                }

                string candidate = baseName;
                int suffix = 2;
                while (!used.Add(candidate))
                {
                    candidate = baseName + "_" + suffix++;
                }

                names.Add(candidate);
            }

            return names;
        }

        private static void ConfigureAtlasImporter(
            string path,
            int width,
            int height)
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
            importer.maxTextureSize = Mathf.NextPowerOfTwo(Mathf.Max(width, height));
            importer.SaveAndReimport();
        }

        private static string EnsureFolder(string requested)
        {
            string path = string.IsNullOrWhiteSpace(requested)
                ? "Assets/_Project/Art/Generated/TilePalettes"
                : requested.Replace('\\', '/').TrimEnd('/');
            if (!path.StartsWith("Assets/", StringComparison.Ordinal) &&
                path != "Assets")
            {
                path = "Assets/_Project/Art/Generated/TilePalettes";
            }

            string[] parts = path.Split('/');
            string current = parts[0];
            for (int index = 1; index < parts.Length; index++)
            {
                string next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[index]);
                }

                current = next;
            }

            return current;
        }

        private static string Sanitize(string value)
        {
            value = string.IsNullOrWhiteSpace(value) ? "SurfaceTiles" : value.Trim();
            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(invalid, '_');
            }

            return value;
        }

        private readonly struct ComponentBounds
        {
            internal ComponentBounds(RectInt rect, int opaquePixels)
            {
                Rect = rect;
                OpaquePixels = opaquePixels;
            }

            internal RectInt Rect { get; }
            internal int OpaquePixels { get; }
        }
    }
}
