using System;
using System.IO;
using Project.ObjectArtwork;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Project.WorldVisibility.Editor
{
    public static class VisibilitySetup
    {
        public const string FountainPrefab = "Assets/_Project/Content/Art/Fountain.prefab";
        const string ResourceFolder = "Assets/_Project/Resources/26TAPTAP/喷泉动画";
        [MenuItem("Tools/2026TapTap/光照/在当前场景添加二维光照")]
        public static void AddToCurrentScene()
        {
            if (Application.isPlaying) return;
            var camera = Camera.main;
            if (!camera) { EditorUtility.DisplayDialog("无法添加光照","当前场景没有主相机。","确定"); return; }
            var system = camera.GetComponent<WorldVisibility2D>();
            if (!system) system = Undo.AddComponent<WorldVisibility2D>(camera.gameObject);
            Undo.RecordObject(system,"配置二维光照"); system.targetCamera = camera;
            var player = GameObject.FindGameObjectWithTag("Player"); if (player) system.viewer = player.transform;
            system.previewInEditor = true; EditorUtility.SetDirty(system); Selection.activeObject = system;
        }
        [MenuItem("Tools/2026TapTap/光照/添加场景光源")]
        static void AddLight()
        {
            var root = new GameObject("二维场景光源"); Undo.RegisterCreatedObjectUndo(root,"添加光源");
            if (SceneView.lastActiveSceneView) root.transform.position = new Vector3(SceneView.lastActiveSceneView.pivot.x,SceneView.lastActiveSceneView.pivot.y,0);
            root.AddComponent<VisibilityLight2D>(); Selection.activeGameObject = root;
        }
        [MenuItem("Tools/2026TapTap/美术/定位喷泉预制体")]
        static void SelectFountain() => Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(FountainPrefab);
        /// <summary>Explicit asset setup, no scene save or layout rebuild.</summary>
        public static void BuildAssets()
        {
            foreach (string path in new[] { "Assets/Settings/PC_Renderer.asset", "Assets/Settings/Mobile_Renderer.asset" })
            {
                var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
                WorldVisibilityRendererFeature feature = null;
                foreach (var item in renderer.rendererFeatures) if (item is WorldVisibilityRendererFeature found) feature = found;
                if (!feature)
                {
                    feature = ScriptableObject.CreateInstance<WorldVisibilityRendererFeature>(); feature.name = "二维遮挡光照";
                    AssetDatabase.AddObjectToAsset(feature,renderer); renderer.rendererFeatures.Add(feature);
                }
                var serialized = new SerializedObject(feature);
                serialized.FindProperty("visibilityShader").objectReferenceValue = Shader.Find("Hidden/2026TapTap/WorldVisibility2D");
                serialized.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(feature); EditorUtility.SetDirty(renderer);
            }
            var frames = new Sprite[8];
            for (int i = 0; i < frames.Length; i++)
            {
                string path = ResourceFolder + $"/penquan_{i+1:00}.png";
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = GridArtworkMetrics.PixelsPerCell; importer.spritePivot = new Vector2(.5f,0);
                var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.BottomCenter; settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings); importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
                importer.npotScale = TextureImporterNPOTScale.None; importer.wrapMode = TextureWrapMode.Clamp;
                importer.alphaIsTransparency = true; importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 128; importer.isReadable = true;
                foreach (string platform in new[] { "Standalone","Android" }) importer.ClearPlatformTextureSettings(platform);
                importer.SaveAndReimport();
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (texture.width != 82 || texture.height != 66 || GridArtworkMetrics.ExactExportScale(texture.GetPixels32(),82,66) != 1)
                    throw new InvalidOperationException("喷泉帧尺寸或导出倍率变化，需重新核对美术标准："+path);
                importer.isReadable = false; importer.SaveAndReimport(); frames[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
            string materialPath = "Assets/_Project/Content/Art/Fountain.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (!material) { material = new Material(Shader.Find("Project/Objects/Pixel Sprite Reveal")); AssetDatabase.CreateAsset(material,materialPath); }
            var temporary = new GameObject("喷泉（仅表现）");
            try
            {
                temporary.AddComponent<SpriteRenderer>().sortingOrder = -20;
                temporary.AddComponent<FountainArtwork>().EditorConfigure(frames,material);
                PrefabUtility.SaveAsPrefabAsset(temporary,FountainPrefab);
            }
            finally { UnityEngine.Object.DestroyImmediate(temporary); }
            AssetDatabase.SaveAssets();
        }
    }
}
