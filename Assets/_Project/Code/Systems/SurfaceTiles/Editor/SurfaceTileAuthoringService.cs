using System.Collections.Generic;
using System.IO;
using System.Linq;
using Project;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Project.SurfaceTiles.Editor
{
    internal static class SurfaceTileAuthoringService
    {
        private const string SurfaceShaderName =
            "2026TapTap/Surface Tiles/Unlit Cutout";

        internal static SurfaceTileBlock MakeSelectedObjectPaintable()
        {
            GameObject target = Selection.activeGameObject;
            if (target == null)
            {
                target = GameObject.CreatePrimitive(PrimitiveType.Cube);
                target.name = "SurfaceTileBlock";
                target.transform.position = SceneView.lastActiveSceneView != null
                    ? SceneView.lastActiveSceneView.pivot
                    : Vector3.zero;
                Undo.RegisterCreatedObjectUndo(target, "创建可贴画方块");
            }

            BoxCollider box = target.GetComponent<BoxCollider>();
            if (box == null)
            {
                box = Undo.AddComponent<BoxCollider>(target);
            }

            SurfaceTileBlock block = target.GetComponent<SurfaceTileBlock>();
            if (block == null)
            {
                block = Undo.AddComponent<SurfaceTileBlock>(target);
            }

            block.EnsureBlockId();
            SurfaceTileMeshBuilder.EnsureOutput(block);
            block.EnsureBaseVisibility();
            Selection.activeGameObject = target;
            EditorSceneManager.MarkSceneDirty(target.scene);
            SceneView.lastActiveSceneView?.FrameSelected();
            return block;
        }

        internal static SurfaceTilePalette CreatePaletteFromSelection()
        {
            List<Sprite> sprites = new List<Sprite>();
            foreach (Object selected in Selection.objects)
            {
                if (selected is Sprite sprite)
                {
                    sprites.Add(sprite);
                    continue;
                }

                if (selected is not Texture2D)
                {
                    continue;
                }

                string path = AssetDatabase.GetAssetPath(selected);
                sprites.AddRange(
                    AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>());
            }

            sprites = sprites
                .Where(item => item != null)
                .Distinct()
                .OrderBy(item => item.name)
                .ToList();
            if (sprites.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "没有可用瓦片",
                    "请先在 Project 窗口选择已按 Multiple 模式切片的图片或 Sprite。",
                    "确定");
                return null;
            }

            Texture2D texture = sprites[0].texture;
            if (sprites.Any(item => item.texture != texture))
            {
                EditorUtility.DisplayDialog(
                    "请选择同一张瓦片图",
                    "一个瓦片库目前只接受来自同一张切片图片的 Sprite。",
                    "确定");
                return null;
            }

            string defaultName = Path.GetFileNameWithoutExtension(
                AssetDatabase.GetAssetPath(texture)) + "_SurfaceTilePalette";
            string palettePath = EditorUtility.SaveFilePanelInProject(
                "保存表面瓦片库",
                defaultName,
                "asset",
                "请选择瓦片库保存位置。");
            if (string.IsNullOrWhiteSpace(palettePath))
            {
                return null;
            }

            SurfaceTilePalette palette = ScriptableObject.CreateInstance<SurfaceTilePalette>();
            palette.ReplaceTiles(sprites);
            AssetDatabase.CreateAsset(palette, palettePath);

            Shader shader = Shader.Find(SurfaceShaderName);
            if (shader == null)
            {
                AssetDatabase.DeleteAsset(palettePath);
                EditorUtility.DisplayDialog(
                    "缺少表面瓦片 Shader",
                    $"找不到 {SurfaceShaderName}，请等待 Unity 编译后重试。",
                    "确定");
                return null;
            }

            Material material = new Material(shader)
            {
                name = defaultName + "_Preview",
                mainTexture = texture
            };
            material.SetTexture("_BaseMap", texture);
            string materialPath = Path.ChangeExtension(palettePath, null) + "_Preview.mat";
            AssetDatabase.CreateAsset(material, materialPath);
            palette.SetPreviewMaterial(material);
            EditorUtility.SetDirty(palette);
            AssetDatabase.SaveAssets();
            Selection.activeObject = palette;
            return palette;
        }

        internal static int RepairScene()
        {
            IReadOnlyList<SurfaceTileBlock> blocks =
                ProjectDiscovery.FindAll<SurfaceTileBlock>(true);
            HashSet<string> ids = new HashSet<string>();
            int repaired = 0;
            for (int index = 0; index < blocks.Count; index++)
            {
                SurfaceTileBlock block = blocks[index];
                Undo.RecordObject(block, "修复表面瓦片方块");
                block.EnsureBlockId();
                if (!ids.Add(block.BlockId))
                {
                    block.RegenerateBlockId();
                    ids.Add(block.BlockId);
                    repaired++;
                }

                repaired += block.RemoveOutOfBoundsTiles();
                SurfaceTileMeshBuilder.EnsureOutput(block);
                block.EnsureBaseVisibility();
                if (!block.BakeUpToDate)
                {
                    SurfaceTileMeshBuilder.RefreshPreview(block);
                }
            }

            if (blocks.Count > 0)
            {
                EditorSceneManager.MarkSceneDirty(blocks[0].gameObject.scene);
            }

            return repaired;
        }
    }
}
