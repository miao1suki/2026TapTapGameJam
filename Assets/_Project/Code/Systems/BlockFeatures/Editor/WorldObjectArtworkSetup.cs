using System;
using System.IO;
using System.Linq;
using Project.ObjectArtwork;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Project.BlockFeatures.Editor
{
    [InitializeOnLoad]
    public static class WorldObjectArtworkSetup
    {
        private const string Root = "Assets/_Project/Resources/ObjectArtwork";
        private const string Generated = "Assets/_Project/Art/Generated/ObjectArtwork";
        private const string Source = "Assets/_Project/Resources/26TAPTAP/物体道具/";
        static WorldObjectArtworkSetup()
        {
            EditorApplication.delayCall += RefreshOpenScenes;
            EditorSceneManager.sceneOpened += (_, _) => RefreshOpenScenes();
        }
        public static void RefreshOpenScenes()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || ObjectArtworkLibrary.Load() == null) return;
            foreach (var anchor in UnityEngine.Object.FindObjectsByType<RespawnAnchorFeature>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (anchor.gameObject.scene.IsValid() && !EditorUtility.IsPersistent(anchor)) WorldObjectSpritePresentation.Ensure(anchor.gameObject);
            foreach (var plant in UnityEngine.Object.FindObjectsByType<BouncePlantFeature>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (plant.gameObject.scene.IsValid() && !EditorUtility.IsPersistent(plant)) WorldObjectSpritePresentation.Ensure(plant.gameObject);
        }
        public static void Build()
        {
            Folder(Root); Folder(Generated);
            Texture2D states = Read(Source + "Image_765781691944282.png");
            var output = new Texture2D(64, 32, TextureFormat.RGBA32, false);
            output.SetPixels(0, 0, 32, 32, states.GetPixels(2, 0, 32, 32));
            output.SetPixels(32, 0, 32, 32, states.GetPixels(68, 0, 32, 32));
            string statePath = Generated + "/Respawn States.png";
            File.WriteAllBytes(statePath, output.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(states); UnityEngine.Object.DestroyImmediate(output);
            File.Copy(Source + "Image_765776490195273.png", Generated + "/Respawn Current.png", true);
            File.Copy(Source + "弹簧80ms每帧.png", Generated + "/Bounce Plant.png", true);
            AssetDatabase.Refresh();
            var stills = Slice(statePath, 32, 32, "Respawn");
            var current = Slice(Generated + "/Respawn Current.png", 32, 32, "Current");
            var bounce = Slice(Generated + "/Bounce Plant.png", 16, 16, "Bounce");
            var library = AssetDatabase.LoadAssetAtPath<ObjectArtworkLibrary>(Root + "/Objects.asset");
            if (library == null) { library = ScriptableObject.CreateInstance<ObjectArtworkLibrary>(); AssetDatabase.CreateAsset(library, Root + "/Objects.asset"); }
            library.respawnLocked = stills[0]; library.respawnUsed = stills[1]; library.respawnCurrent = current;
            library.plantBounce = bounce; library.plantIdleFrame = 5;
            library.respawnFrameSeconds = .08f; library.plantFrameSeconds = .08f;
            Texture2D spring = Read(Source + "弹簧80ms每帧.png");
            int idleTop = 0;
            for (int y = 0; y < 16; y++) for (int x = 80; x < 96; x++)
                if (spring.GetPixel(x, y).a > .001f) idleTop = Mathf.Max(idleTop, y + 1);
            library.plantIdleTop = idleTop / 16f;
            UnityEngine.Object.DestroyImmediate(spring);
            library.neutralSpriteMaterial = Material(Root + "/Neutral Sprite.mat");
            library.colorSpriteMaterial = Material(Root + "/Plant Sprite.mat");
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssetIfDirty(library);
            BindPrefab("Assets/_Project/Content/ColorBlocks/Prefabs/Respawn_Block.prefab");
            BindPrefab("Assets/_Project/Content/ColorBlocks/Prefabs/Green_JumpPlant.prefab");
            RefreshOpenScenes();
        }
        private static void BindPrefab(string path)
        {
            var owner = PrefabUtility.LoadPrefabContents(path);
            try { WorldObjectSpritePresentation.Ensure(owner); PrefabUtility.SaveAsPrefabAsset(owner, path); }
            finally { PrefabUtility.UnloadPrefabContents(owner); }
        }
        private static Texture2D Read(string path)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(File.ReadAllBytes(path))) throw new InvalidOperationException("无法读取物体图片：" + path);
            return texture;
        }
        private static Material Material(string path)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            var shader = Shader.Find("Project/Objects/Pixel Sprite Reveal");
            if (shader == null) throw new InvalidOperationException("物体精灵 Shader 未导入。");
            material = new Material(shader); AssetDatabase.CreateAsset(material, path);
            return material;
        }
        private static Sprite[] Slice(string path, int width, int height, string prefix)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.GetSourceTextureWidthAndHeight(out int textureWidth, out int textureHeight);
            if (textureHeight != height || textureWidth % width != 0) throw new InvalidOperationException("素材帧尺寸错误：" + path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = height;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings);
            importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp; importer.maxTextureSize = Mathf.NextPowerOfTwo(textureWidth);
            var data = new SpriteMetaData[textureWidth / width];
            for (int index = 0; index < data.Length; index++) data[index] = new SpriteMetaData {
                name = prefix + "_" + index.ToString("D2"), rect = new Rect(index * width, 0, width, height),
                alignment = (int)SpriteAlignment.BottomCenter, pivot = new Vector2(.5f, 0f), border = Vector4.zero
            };
            #pragma warning disable CS0618
            importer.spritesheet = data;
            #pragma warning restore CS0618
            importer.SaveAndReimport();
            var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s => s.name).ToArray();
            if (sprites.Length != data.Length) throw new InvalidOperationException("切片帧数量错误：" + path);
            return sprites;
        }
        private static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            Folder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }

    public sealed class WorldObjectArtworkImports : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] oldPaths)
        {
            if (imported.Any(path => path == "Assets/_Project/Resources/ObjectArtwork/Objects.asset"))
                EditorApplication.delayCall += WorldObjectArtworkSetup.RefreshOpenScenes;
        }
    }
}
