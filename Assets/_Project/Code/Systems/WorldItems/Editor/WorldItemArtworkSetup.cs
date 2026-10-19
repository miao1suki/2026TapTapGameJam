using System;
using System.Linq;
using Project.BlockFeatures.Editor;
using Project.LevelEditor;
using Project.ObjectArtwork;
using UnityEditor;
using UnityEngine;

namespace Project.WorldItems.Editor
{
    /// <summary>Explicit import only; does not mutate any open scene or existing placement.</summary>
    public static class WorldItemArtworkSetup
    {
        public const string CollectibleSource = "Assets/_Project/Resources/26TAPTAP/物体道具/E55B9ECE3C45615E2EC3C8F2DF707CCB.png";
        private const string PalettePath = "Assets/_Project/Development/LevelEditor/Orpheus0829/LevelEditorPalette.asset";

        [MenuItem("Tools/2026TapTap/关卡美术/接入收集品与尖刺四款")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            WorldObjectArtworkSetup.BuildSupplementArtwork();
            var importer = (TextureImporter)AssetImporter.GetAtPath(CollectibleSource);
            if (importer == null) throw new InvalidOperationException("缺少成就收集品源图。");
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = GridArtworkMetrics.PixelsPerCell;
            importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None; importer.maxTextureSize = 128;
            importer.alphaIsTransparency = true;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect; settings.spriteGenerateFallbackPhysicsShape = false;
            importer.SetTextureSettings(settings);
            foreach (var target in new[] { "Standalone", "Android", "iPhone" })
            {
                var platform = importer.GetPlatformTextureSettings(target); platform.overridden = false;
                importer.SetPlatformTextureSettings(platform);
            }
            // Artwork is a 32px frame at top-left (0,16), not a 96px sprite,
            // nor a 16px squeeze of its 18px-wide hand-painted silhouette.
            #pragma warning disable CS0618
            var slices = importer.spritesheet.Where(slice => slice.name != "Achievement_Collectible").ToList();
            slices.Add(new SpriteMetaData { name = "Achievement_Collectible", rect = new Rect(0, 0, 32, 32),
                alignment = (int)SpriteAlignment.BottomCenter, pivot = new Vector2(.5f, 0f) });
            importer.spritesheet = slices.ToArray();
            #pragma warning restore CS0618
            importer.SaveAndReimport();
            Sprite sprite = AssetDatabase.LoadAllAssetsAtPath(CollectibleSource).OfType<Sprite>().First(item => item.name == "Achievement_Collectible");
            var library = ObjectArtworkLibrary.Load();
            if (library == null) throw new InvalidOperationException("缺少物体美术库。");
            var entries = library.mechanisms.ToList();
            var entry = entries.FirstOrDefault(item => item.kind == MechanismArtworkKind.Collectible);
            if (entry == null) { entry = new MechanismArtworkEntry { kind = MechanismArtworkKind.Collectible }; entries.Add(entry); }
            entry.frames = new[] { sprite }; library.mechanisms = entries.ToArray();
            library.neutralMeshMaterial = BuildSpikeMaterial();
            EditorUtility.SetDirty(library); AssetDatabase.SaveAssetIfDirty(library);
            BuildPalette(); ObjectArtworkPlanningPreview.ClearCache(); WorldObjectArtworkSetup.RefreshOpenScenes();
        }

        private static Material BuildSpikeMaterial()
        {
            const string path = "Assets/_Project/Resources/ObjectArtwork/Spike Cutout.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var source = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Content/WorldItems/Neutral.mat");
                if (source == null) throw new InvalidOperationException("缺少无色尖刺网格材质。");
                material = new Material(source) { name = "Spike Cutout" };
                AssetDatabase.CreateAsset(material, path);
            }
            // Batched MeshRenderers cannot use the SpriteRenderer uniform path.
            // Keep native transparent pixels cut out rather than drawing opaque
            // full-cell rectangles, without mutating the shared block material.
            material.SetFloat("_AlphaClip", 1f); material.SetFloat("_Cutoff", .5f);
            material.EnableKeyword("_ALPHATEST_ON"); material.SetFloat("_Cull", 0f);
            material.SetOverrideTag("RenderType", "TransparentCutout"); material.renderQueue = 2450;
            EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material);
            return material;
        }

        private static void BuildPalette()
        {
            var palette = AssetDatabase.LoadAssetAtPath<LevelEditorPalette>(PalettePath);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Content/WorldItems/Spike_上.prefab");
            if (palette == null || prefab == null) throw new InvalidOperationException("缺少关卡道具栏目或上向尖刺预制体。");
            for (int size = 0; size < 2; size++) for (int blood = 0; blood < 2; blood++)
            {
                string id = "world-spike-" + (size == 0 ? "small" : "large") + (blood == 0 ? "" : "-bloody");
                string label = (size == 0 ? "小尖刺" : "大尖刺") + (blood == 0 ? "·普通" : "·带血");
                var entry = palette.PropEntries.FirstOrDefault(item => item.EntryId == id);
                if (entry == null)
                {
                    entry = new LevelEditorPropEntry(); entry.Configure(label, prefab); palette.AddProp(entry);
                    var serialized = new SerializedObject(palette); var props = serialized.FindProperty("propEntries");
                    props.GetArrayElementAtIndex(props.arraySize - 1).FindPropertyRelative("entryId").stringValue = id;
                    serialized.ApplyModifiedPropertiesWithoutUndo(); entry = palette.PropEntries.First(item => item.EntryId == id);
                }
                entry.Configure(label, prefab);
                var values = entry.ComponentValueOverrides.Where(value => value.ComponentTypeName != typeof(NeutralSpike).FullName ||
                    value.PropertyPath != "size" && value.PropertyPath != "initiallyBloody" && value.PropertyPath != "facing").ToList();
                void Add(string property, string type, string value)
                {
                    var item = new LevelEditorComponentValueOverride();
                    item.Configure("", typeof(NeutralSpike).FullName, 0, property, type, value, null); values.Add(item);
                }
                Add("size", "Enum", size.ToString()); Add("initiallyBloody", "Boolean", blood == 1 ? "1" : "0"); Add("facing", "Enum", "1");
                entry.ReplaceComponentValueOverrides(values);
            }
            EditorUtility.SetDirty(palette); AssetDatabase.SaveAssetIfDirty(palette);
            Project.LevelEditor.Editor.LevelEditorState.MarkPaletteChanged();
        }
    }
}
