using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Project.Player.Editor
{
    public static class PlayerSpriteAssetSetup
    {
        private const string Root = "Assets/_Project/Resources/PlayerVisuals";
        private static readonly string[] Sheets = {
            "Image_765749900939815", "Image_765753151589293", "Image_765756350049135",
            "Image_765759505209551", "Image_765762585688091", "Image_765765554419184", "Image_765768840986943"
        };
        public static void Build()
        {
            if (!AssetDatabase.IsValidFolder(Root)) AssetDatabase.CreateFolder("Assets/_Project/Resources", "PlayerVisuals");
            var clips = new PlayerSpriteAnimationSet.Clip[Sheets.Length];
            float[] durations = { .08f, .08f, .06f, .08f, .10f, .12f, .10f };
            for (int i = 0; i < Sheets.Length; i++)
            {
                string path = "Assets/_Project/Resources/26TAPTAP/玩家/" + Sheets[i] + ".png";
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.GetSourceTextureWidthAndHeight(out int width, out int height);
                if (height != 24 || width % 24 != 0) throw new InvalidOperationException("Invalid player sheet: " + path);
                int count = width / 24;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = count == 1 ? SpriteImportMode.Single : SpriteImportMode.Multiple;
                importer.spritePixelsPerUnit = 24f;
                importer.spritePivot = new Vector2(.5f, .5f);
                var data = new SpriteMetaData[count];
                for (int f = 0; f < count; f++) data[f] = new SpriteMetaData {
                    name = ((PlayerSpriteState)i) + "_" + f.ToString("D2"), rect = new Rect(f * 24, 0, 24, 24),
                    alignment = (int)SpriteAlignment.Center, pivot = new Vector2(.5f, .5f)
                };
                #pragma warning disable CS0618
                if (count > 1) importer.spritesheet = data;
                #pragma warning restore CS0618
                importer.SaveAndReimport();
                var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s => s.name).ToArray();
                if (sprites.Length != count) throw new InvalidOperationException("Sprite count mismatch: " + path);
                clips[i] = new PlayerSpriteAnimationSet.Clip {
                    state = (PlayerSpriteState)i, frames = sprites, secondsPerFrame = durations[i],
                    loop = i == 0 || i == 1 || i == 4 || i == 5
                };
            }
            var set = AssetDatabase.LoadAssetAtPath<PlayerSpriteAnimationSet>(Root + "/PlayerSprites.asset");
            if (set == null) { set = ScriptableObject.CreateInstance<PlayerSpriteAnimationSet>(); AssetDatabase.CreateAsset(set, Root + "/PlayerSprites.asset"); }
            set.Configure(clips); EditorUtility.SetDirty(set);
            var material = AssetDatabase.LoadAssetAtPath<Material>(Root + "/PlayerSprite.mat");
            if (material == null)
            {
                var shader = Shader.Find("Project/Player/Pixel Sprite");
                if (shader == null) throw new InvalidOperationException("Player sprite shader missing");
                material = new Material(shader); AssetDatabase.CreateAsset(material, Root + "/PlayerSprite.mat");
            }
            BindPrefab(StandardPlayerAsset.Path, set, material);
            AssetDatabase.SaveAssets();
        }
        private static void BindPrefab(string path, PlayerSpriteAnimationSet set, Material material)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var controller = root.GetComponent<PlayerController>();
                if (controller == null) throw new InvalidOperationException("PlayerController missing: " + path);
                var presentation = root.GetComponent<PlayerSpritePresentation>();
                if (presentation == null) presentation = root.AddComponent<PlayerSpritePresentation>();
                Transform child = root.transform.Find("Player Visual");
                if (child == null) { child = new GameObject("Player Visual").transform; child.SetParent(root.transform, false); }
                child.gameObject.layer = root.layer;
                var renderer = child.GetComponent<SpriteRenderer>();
                if (renderer == null) renderer = child.gameObject.AddComponent<SpriteRenderer>();
                renderer.sprite = set.GetClip(PlayerSpriteState.Idle).frames[0];
                renderer.sharedMaterial = material; renderer.sortingOrder = 10;
                child.localPosition = root.GetComponent<CapsuleCollider>().center;
                child.localScale = Vector3.one;
                var so = new SerializedObject(presentation);
                child.localPosition += Vector3.up * so.FindProperty("verticalOffsetCells").floatValue;
                so.FindProperty("animationSet").objectReferenceValue = set;
                so.FindProperty("visual").objectReferenceValue = renderer;
                so.FindProperty("spriteMaterial").objectReferenceValue = material;
                so.ApplyModifiedPropertiesWithoutUndo();
                var mesh = root.GetComponent<MeshRenderer>();
                if (mesh == null && root.transform.Find("Visual") != null)
                    mesh = root.transform.Find("Visual").GetComponent<MeshRenderer>();
                if (mesh != null) mesh.enabled = false;
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
