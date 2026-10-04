using System.Collections.Generic;
using System.Text;
using Project.SurfaceTiles;
using Project.SurfaceTiles.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Project.LevelEditor.Editor
{
    internal static class LevelEditorDecorationService
    {
        private const string PreviewRootName =
            "__LevelEditorDecorationPreview";
        private static readonly Dictionary<string, Texture2D>
            FrontPreviewCache =
                new Dictionary<string, Texture2D>();
        private static GameObject previewRoot;
        private static SceneView previousSceneView;
        private static Vector3 previousPivot;
        private static Quaternion previousRotation;
        private static float previousSize;
        private static bool previousOrthographic;

        internal static void Open(int entryIndex)
        {
            LevelEditorPalette palette = LevelEditorState.Palette;
            if (palette == null || entryIndex < 0 ||
                entryIndex >= palette.Entries.Count)
            {
                return;
            }

            LevelEditorBlockEntry entry = palette.Entries[entryIndex];
            if (LevelEditorState.EditMode)
            {
                LevelEditorState.EditMode = false;
                LevelEditorViewLock.Exit();
            }

            CaptureSceneView();
            SurfaceTileBlock block = CreatePreview(entry);
            if (block == null)
            {
                RestoreSceneView();
                return;
            }

            SurfaceTilePaintEditorWindow.Open(block, () =>
            {
                SaveDecoration(palette, entry, block);
            });
        }

        internal static void ApplyToPlacedBlock(
            GameObject target,
            LevelEditorBlockEntry entry)
        {
            if (target == null || entry == null ||
                entry.UsesPrefabDirectly ||
                !entry.HasDecoration)
            {
                return;
            }

            ApplyDecoration(target, entry);
        }

        internal static Texture2D RenderEntryPreview(
            LevelEditorBlockEntry entry,
            int size)
        {
            if (entry == null ||
                !entry.HasDecoration ||
                entry.SurfaceTilePlacements.Count == 0)
            {
                return null;
            }

            GameObject root = new GameObject(
                "__LevelEditorThumbnail");
            root.hideFlags = HideFlags.HideAndDontSave;
            root.transform.position = new Vector3(100000f, 0f, 0f);
            Mesh previewMesh = null;
            RenderTexture target = null;
            try
            {
                GameObject blockObject = entry.SourcePrefab != null
                    ? PrefabUtility.InstantiatePrefab(entry.SourcePrefab)
                        as GameObject
                    : GameObject.CreatePrimitive(PrimitiveType.Cube);
                if (blockObject == null)
                {
                    return null;
                }

                blockObject.transform.SetParent(root.transform, false);
                blockObject.transform.localPosition = Vector3.zero;
                if (entry.SourcePrefab == null)
                {
                    blockObject.transform.localScale = Vector3.one;
                }

                if (blockObject.GetComponent<BoxCollider>() == null)
                {
                    blockObject.AddComponent<BoxCollider>();
                }

                SurfaceTileBlock block =
                    blockObject.GetComponent<SurfaceTileBlock>() ??
                    blockObject.AddComponent<SurfaceTileBlock>();
                block.EnsureBlockId();
                entry.ApplyDecoration(block);
                block.SetTransparentBase(true);
                previewMesh =
                    SurfaceTileEditorBridge.BuildPreviewMesh(block);
                if (previewMesh == null ||
                    entry.SurfaceTilePalette == null)
                {
                    return null;
                }

                GameObject output = new GameObject("__Preview");
                output.transform.SetParent(blockObject.transform, false);
                MeshFilter filter = output.AddComponent<MeshFilter>();
                filter.sharedMesh = previewMesh;
                MeshRenderer renderer =
                    output.AddComponent<MeshRenderer>();
                renderer.sharedMaterial =
                    entry.SurfaceTilePalette.PreviewMaterial;

                GameObject cameraObject = new GameObject(
                    "__PreviewCamera");
                cameraObject.transform.SetParent(root.transform, false);
                cameraObject.transform.localPosition =
                    new Vector3(0f, 0f, -4f);
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = .52f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.clear;
                camera.cullingMask = ~0;

                target = RenderTexture.GetTemporary(
                    size,
                    size,
                    24,
                    RenderTextureFormat.ARGB32,
                    RenderTextureReadWrite.sRGB);
                RenderTexture previous = RenderTexture.active;
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                Texture2D texture = new Texture2D(
                    size,
                    size,
                    TextureFormat.RGBA32,
                    false,
                    false)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                texture.ReadPixels(
                    new Rect(0f, 0f, size, size),
                    0,
                    0);
                texture.Apply(false, false);
                RenderTexture.active = previous;
                camera.targetTexture = null;
                return texture;
            }
            finally
            {
                if (target != null)
                {
                    RenderTexture.ReleaseTemporary(target);
                }

                Object.DestroyImmediate(root);
                if (previewMesh != null)
                {
                    Object.DestroyImmediate(previewMesh);
                }
            }
        }

        internal static Texture2D RenderEntryFrontPreview(
            LevelEditorBlockEntry entry,
            int size)
        {
            if (entry == null)
            {
                return null;
            }

            if (entry.HasDecoration &&
                entry.SurfaceTilePlacements.Count > 0)
            {
                return RenderEntryPreview(entry, size);
            }

            return RenderSimpleFrontPreview(entry, size);
        }

        internal static Texture2D GetEntryFrontPreview(
            LevelEditorBlockEntry entry,
            int size)
        {
            if (entry == null)
            {
                return null;
            }

            string key = BuildFrontPreviewKey(entry, size);
            if (FrontPreviewCache.TryGetValue(
                    key,
                    out Texture2D cached) &&
                cached != null)
            {
                return cached;
            }

            Texture2D preview = RenderEntryFrontPreview(entry, size);
            if (preview != null)
            {
                FrontPreviewCache[key] = preview;
            }

            return preview;
        }

        private static string BuildFrontPreviewKey(
            LevelEditorBlockEntry entry,
            int size)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append(entry.EntryId ?? entry.DisplayName);
            builder.Append(':');
            builder.Append(size);
            builder.Append(':');
            builder.Append(ColorUtility.ToHtmlStringRGBA(entry.Color));
            builder.Append(':');
            builder.Append(entry.SourcePrefab != null
                ? entry.SourcePrefab.GetInstanceID()
                : 0);
            builder.Append(':');
            builder.Append(entry.SurfaceTilePalette != null
                ? entry.SurfaceTilePalette.GetInstanceID()
                : 0);
            builder.Append(':');
            builder.Append(entry.SurfaceTileCellSize);
            builder.Append(':');
            builder.Append(entry.SurfaceTileTransparentBase ? 1 : 0);
            for (int index = 0;
                 index < entry.SurfaceTilePlacements.Count;
                 index++)
            {
                SurfaceTilePlacement placement =
                    entry.SurfaceTilePlacements[index];
                if (placement == null)
                {
                    continue;
                }

                builder.Append('|');
                builder.Append((int)placement.Face);
                builder.Append(',');
                builder.Append(placement.Cell.x);
                builder.Append(',');
                builder.Append(placement.Cell.y);
                builder.Append(',');
                builder.Append(placement.TileId);
                builder.Append(',');
                builder.Append(placement.QuarterTurns);
                builder.Append(',');
                builder.Append(placement.FlipX ? 1 : 0);
                builder.Append(',');
                builder.Append(placement.FlipY ? 1 : 0);
            }

            return builder.ToString();
        }

        private static Texture2D RenderSimpleFrontPreview(
            LevelEditorBlockEntry entry,
            int size)
        {
            GameObject root = new GameObject(
                "__LevelEditorFrontPreview");
            root.hideFlags = HideFlags.HideAndDontSave;
            root.transform.position = new Vector3(100000f, 0f, 0f);
            RenderTexture target = null;
            Material temporaryMaterial = null;
            try
            {
                GameObject blockObject = entry.SourcePrefab != null
                    ? PrefabUtility.InstantiatePrefab(entry.SourcePrefab)
                        as GameObject
                    : GameObject.CreatePrimitive(PrimitiveType.Cube);
                if (blockObject == null)
                {
                    return null;
                }

                blockObject.transform.SetParent(root.transform, false);
                blockObject.transform.localPosition = Vector3.zero;
                if (entry.SourcePrefab == null)
                {
                    blockObject.transform.localScale =
                        Vector3.one * .94f;
                    Shader shader = Shader.Find(
                        "Universal Render Pipeline/Unlit");
                    if (shader == null)
                    {
                        shader = Shader.Find("Unlit/Color");
                    }

                    if (shader != null)
                    {
                        temporaryMaterial = new Material(shader);
                        temporaryMaterial.color = entry.Color;
                        if (temporaryMaterial.HasProperty("_BaseColor"))
                        {
                            temporaryMaterial.SetColor(
                                "_BaseColor",
                                entry.Color);
                        }

                        if (temporaryMaterial.HasProperty("_Color"))
                        {
                            temporaryMaterial.SetColor(
                                "_Color",
                                entry.Color);
                        }

                        MeshRenderer renderer =
                            blockObject.GetComponent<MeshRenderer>();
                        if (renderer != null)
                        {
                            renderer.sharedMaterial = temporaryMaterial;
                        }
                    }
                }

                GameObject cameraObject = new GameObject(
                    "__FrontPreviewCamera");
                cameraObject.transform.SetParent(root.transform, false);
                cameraObject.transform.localPosition =
                    new Vector3(0f, 0f, -4f);
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = .52f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.clear;
                camera.cullingMask = ~0;

                target = RenderTexture.GetTemporary(
                    size,
                    size,
                    24,
                    RenderTextureFormat.ARGB32,
                    RenderTextureReadWrite.sRGB);
                RenderTexture previous = RenderTexture.active;
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                Texture2D texture = new Texture2D(
                    size,
                    size,
                    TextureFormat.RGBA32,
                    false,
                    false)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                texture.ReadPixels(
                    new Rect(0f, 0f, size, size),
                    0,
                    0);
                texture.Apply(false, false);
                RenderTexture.active = previous;
                camera.targetTexture = null;
                return texture;
            }
            finally
            {
                if (target != null)
                {
                    RenderTexture.ReleaseTemporary(target);
                }

                if (temporaryMaterial != null)
                {
                    Object.DestroyImmediate(temporaryMaterial);
                }

                Object.DestroyImmediate(root);
            }
        }

        private static SurfaceTileBlock CreatePreview(
            LevelEditorBlockEntry entry)
        {
            DestroyPreview();

            previewRoot = new GameObject(PreviewRootName)
            {
                hideFlags = HideFlags.DontSaveInEditor
            };
            previewRoot.transform.position =
                new Vector3(10000f, 0f, 0f);

            GameObject template = entry.SourcePrefab != null
                ? PrefabUtility.InstantiatePrefab(entry.SourcePrefab)
                    as GameObject
                : GameObject.CreatePrimitive(PrimitiveType.Cube);
            if (template == null)
            {
                Object.DestroyImmediate(previewRoot);
                previewRoot = null;
                return null;
            }

            if (entry.SourcePrefab == null)
            {
                template.transform.localScale = Vector3.one;
            }

            template.name = "预览 · " + entry.DisplayName;
            template.transform.SetParent(previewRoot.transform, false);
            template.transform.localPosition = Vector3.zero;
            template.hideFlags = HideFlags.DontSaveInEditor;

            SurfaceTileBlock block =
                SurfaceTileEditorBridge.EnsurePaintable(template);
            if (block == null)
            {
                Object.DestroyImmediate(previewRoot);
                previewRoot = null;
                return null;
            }

            entry.ApplyDecoration(block);
            SurfaceTileEditorBridge.RefreshPreview(block);
            Selection.activeGameObject = template;
            SceneView.lastActiveSceneView?.FrameSelected();
            SceneView.RepaintAll();
            return block;
        }

        private static void SaveDecoration(
            LevelEditorPalette palette,
            LevelEditorBlockEntry entry,
            SurfaceTileBlock block)
        {
            if (block == null)
            {
                DestroyPreview();
                RestoreSceneView();
                return;
            }

            if (palette != null && entry != null)
            {
                entry.CaptureDecoration(block);
                EditorUtility.SetDirty(palette);
                AssetDatabase.SaveAssetIfDirty(palette);
                LevelEditorState.MarkPaletteChanged();
                ApplyToMatchingBlocks(entry);
            }

            DestroyPreview();
            RestoreSceneView();
            SceneView.RepaintAll();
        }

        private static void CaptureSceneView()
        {
            previousSceneView = SceneView.lastActiveSceneView;
            if (previousSceneView == null)
            {
                return;
            }

            previousPivot = previousSceneView.pivot;
            previousRotation = previousSceneView.rotation;
            previousSize = previousSceneView.size;
            previousOrthographic = previousSceneView.orthographic;
        }

        private static void RestoreSceneView()
        {
            if (previousSceneView == null)
            {
                return;
            }

            previousSceneView.pivot = previousPivot;
            previousSceneView.rotation = previousRotation;
            previousSceneView.size = previousSize;
            previousSceneView.orthographic = previousOrthographic;
            previousSceneView.Repaint();
            previousSceneView = null;
        }

        private static void ApplyToMatchingBlocks(
            LevelEditorBlockEntry entry)
        {
            LevelEditorPlacedBlock[] blocks =
                Object.FindObjectsByType<LevelEditorPlacedBlock>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            for (int index = 0; index < blocks.Length; index++)
            {
                LevelEditorPlacedBlock placed = blocks[index];
                if (!entry.UsesPrefabDirectly &&
                    placed.EntryName == entry.DisplayName)
                {
                    ApplyDecoration(placed.gameObject, entry);
                }
            }
        }

        private static void ApplyDecoration(
            GameObject target,
            LevelEditorBlockEntry entry)
        {
            BoxCollider box = target.GetComponent<BoxCollider>();
            if (box == null)
            {
                box = Undo.AddComponent<BoxCollider>(target);
            }

            SurfaceTileBlock block =
                target.GetComponent<SurfaceTileBlock>();
            if (block == null)
            {
                block = Undo.AddComponent<SurfaceTileBlock>(target);
            }

            Undo.RecordObject(block, "应用栏目方块贴画");
            block.EnsureBlockId();
            entry.ApplyDecoration(block);
            SurfaceTileEditorBridge.RefreshPreview(block);
            EditorUtility.SetDirty(block);
            if (target.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(target.scene);
            }
        }

        private static void DestroyPreview()
        {
            if (previewRoot == null)
            {
                previewRoot = GameObject.Find(PreviewRootName);
            }

            if (previewRoot != null)
            {
                GameObject selected = Selection.activeGameObject;
                if (selected != null &&
                    (selected == previewRoot ||
                     selected.transform.IsChildOf(previewRoot.transform)))
                {
                    Selection.activeObject = null;
                    Selection.activeGameObject = null;
                }

                Object.DestroyImmediate(previewRoot);
                ActiveEditorTracker.sharedTracker.ForceRebuild();
            }

            previewRoot = null;
        }
    }
}
