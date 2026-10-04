using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.Rendering.Universal;

namespace Project.ColorBlocks.Editor
{
    internal static class ColorProjectSetup
    {
        internal const string CatalogPath = "Assets/_Project/Resources/ColorBlocks/ColorCatalog.asset";
        private const string MaterialFolder = "Assets/_Project/Content/ColorBlocks/Materials";
        private const string PrefabFolder = "Assets/_Project/Content/ColorBlocks/Prefabs";
        private const string TimelineFolder = "Assets/_Project/Content/ColorBlocks/Timelines";
        private const string ShaderFolder = "Assets/_Project/Code/Systems/ColorBlocks/Shaders/";

        internal static void InitializeFromMenu()
        {
            var catalog = EnsureCatalog();
            InstallRendererFeatures();
            EnsureStarterPrefabs(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log("[ColorBlocks] 颜色目录、材质、层、预制体与 PC/Mobile Renderer Feature 已检查。");
        }

        internal static ColorCatalog EnsureCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ColorCatalog>(CatalogPath);
            if (catalog != null) return catalog;
            EnsureFolder("Assets/_Project/Resources/ColorBlocks");
            EnsureFolder(MaterialFolder);
            catalog = ScriptableObject.CreateInstance<ColorCatalog>();
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderFolder + "OutlinedColorBlock.shader");
            catalog.SetNeutralMaterial(CreateMaterial("Color_Neutral", Color.white, shader));
            AddInitial(catalog, "red", "红", new Color(1f, 0.08f, 0.08f), shader);
            AddInitial(catalog, "green", "绿", new Color(0.08f, 1f, 0.08f), shader);
            AddInitial(catalog, "blue", "蓝", new Color(0.08f, 0.28f, 1f), shader);
            AssetDatabase.CreateAsset(catalog, CatalogPath);
            AssetDatabase.SaveAssets();
            return catalog;
        }

        internal static int FindAvailableLayer(string name)
        {
            for (int i = 8; i < 32; i++)
                if (LayerMask.LayerToName(i) == name) return i;
            for (int i = 8; i < 32; i++)
                if (string.IsNullOrEmpty(LayerMask.LayerToName(i)))
                {
                    var manager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
                    var layers = manager.FindProperty("layers");
                    layers.GetArrayElementAtIndex(i).stringValue = name;
                    manager.ApplyModifiedProperties();
                    return i;
                }
            return -1;
        }

        internal static Material CreateMaterial(string name, Color tint, Shader shader = null)
        {
            EnsureFolder(MaterialFolder);
            var path = MaterialFolder + "/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            if (shader == null) shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderFolder + "OutlinedColorBlock.shader");
            if (shader == null) throw new InvalidOperationException("颜色方块 Shader 缺失。");
            var material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", tint);
            material.SetColor("_OutlineColor", Color.black);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        internal static void InstallRendererFeatures()
        {
            var mask = AssetDatabase.LoadAssetAtPath<Shader>(ShaderFolder + "ColorObjectMask.shader");
            var fade = AssetDatabase.LoadAssetAtPath<Shader>(ShaderFolder + "SelectiveHSV.shader");
            if (mask == null || fade == null) throw new InvalidOperationException("HSV Shader 缺失。");
            foreach (string path in new[] { "Assets/Settings/PC_Renderer.asset", "Assets/Settings/Mobile_Renderer.asset" })
            {
                var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
                if (data == null) throw new InvalidOperationException("找不到 URP Renderer: " + path);
                var existing = data.rendererFeatures.OfType<SelectiveHsvRendererFeature>().FirstOrDefault();
                if (existing != null) continue;
                var feature = ScriptableObject.CreateInstance<SelectiveHsvRendererFeature>();
                feature.name = "Selective HSV Color Fade";
                feature.EditorConfigure(mask, fade);
                AssetDatabase.AddObjectToAsset(feature, data);
                data.rendererFeatures.Add(feature);
                EditorUtility.SetDirty(feature);
                data.SetDirty();
                EditorUtility.SetDirty(data);
            }
        }

        private static void EnsureStarterPrefabs(ColorCatalog catalog)
        {
            EnsureFolder(PrefabFolder);
            EnsureFolder(TimelineFolder);
            foreach (var color in catalog.Colors)
            {
                var blockPath = PrefabFolder + "/ColorBlock_" + color.id + ".prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(blockPath) == null)
                {
                    var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    try
                    {
                        block.name = "ColorBlock_" + color.id;
                        block.layer = color.unityLayer;
                        var renderer = block.GetComponent<Renderer>();
                        renderer.sharedMaterial = catalog.NeutralMaterial;
                        block.AddComponent<ColorBlock>().EditorConfigure(color.id, renderer);
                        PrefabUtility.SaveAsPrefabAsset(block, blockPath);
                    }
                    finally { UnityEngine.Object.DestroyImmediate(block); }
                }

                var pickupPath = PrefabFolder + "/ColorKey_" + color.id + ".prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(pickupPath) != null) continue;
                var timelinePath = TimelineFolder + "/ColorPickup_" + color.id + ".playable";
                var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(timelinePath);
                if (timeline == null)
                {
                    timeline = ScriptableObject.CreateInstance<TimelineAsset>();
                    AssetDatabase.CreateAsset(timeline, timelinePath);
                    var track = timeline.CreateTrack<CameraTimelineTrack>(null, "颜色获取 · 相机");
                    var clip = track.CreateClip<CameraTimelineClip>();
                    clip.duration = 2;
                    clip.displayName = "获得" + color.displayName + "色";
                    if (clip.asset is CameraTimelineClip cameraClip)
                    {
                        cameraClip.cameraMoveMode = CamMoveMode.SmoothLerp;
                        cameraClip.cameraTargetLocalPos = new Vector3(0, 2, -4);
                        cameraClip.lockLookAtPlayer = true;
                    }
                }
                var pickup = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                try
                {
                    pickup.name = "ColorKey_" + color.id;
                    pickup.transform.localScale = Vector3.one * 0.5f;
                    pickup.GetComponent<Collider>().isTrigger = true;
                    pickup.GetComponent<Renderer>().sharedMaterial = catalog.NeutralMaterial;
                    var director = pickup.AddComponent<PlayableDirector>();
                    director.playOnAwake = false;
                    director.playableAsset = timeline;
                    pickup.AddComponent<ColorKeyPickup>().EditorConfigure(color.id, director);
                    PrefabUtility.SaveAsPrefabAsset(pickup, pickupPath);
                }
                finally { UnityEngine.Object.DestroyImmediate(pickup); }
            }
        }

        internal static void EnsureFolder(string path)
        {
            var parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static void AddInitial(ColorCatalog catalog, string id, string display, Color tint, Shader shader)
        {
            catalog.EditableColors.Add(new ColorTypeDefinition
            {
                id = id,
                displayName = display,
                swatch = tint,
                unityLayer = FindAvailableLayer("Color_" + id),
                targetMaterial = CreateMaterial("Color_" + id, tint, shader),
                unlockEventId = "color.unlocked." + id,
                nodes = new System.Collections.Generic.List<ColorGraphNode>
                {
                    new ColorGraphNode { id = id + ".touch", title = "接触一级色", position = new Vector2(20, 120) },
                    new ColorGraphNode { id = id + ".ability", title = "解锁后交互", position = new Vector2(210, 120) }
                }
            });
        }
    }
}
