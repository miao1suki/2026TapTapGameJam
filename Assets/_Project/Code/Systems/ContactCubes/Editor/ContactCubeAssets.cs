using System;
using System.IO;
using System.Linq;
using Project.LevelEditor;
using Project.Rooms;
using UnityEditor;
using UnityEngine;

namespace Project.ContactCubes.Editor
{
    // Explicit isolated authoring entry; never overwrites a developer scene.
    public static class ContactCubeAssets
    {
        public static void Build()
        {
            if (!Application.dataPath.Contains("FlowRetirementProbe")) throw new InvalidOperationException("仅在隔离项目生成初始资源。");
            const string folder = "Assets/_Project/Content/ContactCubes";
            Directory.CreateDirectory(folder); AssetDatabase.Refresh();
            var palette = AssetDatabase.LoadAssetAtPath<LevelEditorPalette>("Assets/_Project/Development/LevelEditor/Orpheus0829/LevelEditorPalette.asset");
            var jet = Material(folder + "/WaterJet.mat", new Color(.15f, .65f, .95f));
            foreach (CubeColor color in Enum.GetValues(typeof(CubeColor)))
            {
                string name = color + "_ContactCube";
                var material = Material(folder + "/" + name + ".mat", color == CubeColor.Red ? new Color(.85f,.08f,.08f) : color == CubeColor.Green ? new Color(.1f,.7f,.15f) : new Color(.1f,.3f,.9f));
                var root = new GameObject(name);
                var rb = root.AddComponent<Rigidbody>();
                rb.useGravity = true;
                rb.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                var body = new GameObject("实体"); body.transform.SetParent(root.transform, false);
                var shape = body.AddComponent<BoxCollider>();
                var visual = GameObject.CreatePrimitive(PrimitiveType.Cube); visual.name = "模型";
                UnityEngine.Object.DestroyImmediate(visual.GetComponent<Collider>());
                visual.transform.SetParent(body.transform, false); visual.GetComponent<Renderer>().sharedMaterial = material;
                var cube = root.AddComponent<ContactCube>();
                var so = new SerializedObject(cube);
                so.FindProperty("color").enumValueIndex = (int)color;
                so.FindProperty("shape").objectReferenceValue = shape;
                so.FindProperty("model").objectReferenceValue = visual.transform;
                so.FindProperty("jetMaterial").objectReferenceValue = jet;
                so.ApplyModifiedPropertiesWithoutUndo();
                root.AddComponent<ContactCubeMotion>();
                root.AddComponent<Project.BlockFeatures.CarryableFeature>();
                var colorObject = root.AddComponent<Project.ColorBlocks.ColorObject>();
                var colorSettings = new SerializedObject(colorObject);
                colorSettings.FindProperty("baseColorTypeId").stringValue = color == CubeColor.Red ? "red" : color == CubeColor.Blue ? "blue" : "green";
                colorSettings.FindProperty("targetRenderer").objectReferenceValue = visual.GetComponent<Renderer>();
                colorSettings.FindProperty("unlockedMaterial").objectReferenceValue = material;
                colorSettings.ApplyModifiedPropertiesWithoutUndo();
                if (color == CubeColor.Green) root.AddComponent<HorizontalCubeVine>();
                root.AddComponent<RoomResetObject>();
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, folder + "/" + name + ".prefab");
                UnityEngine.Object.DestroyImmediate(root);
                if (palette != null && !palette.PropEntries.Any(p => p.Prefab == prefab))
                {
                    var entry = new LevelEditorPropEntry(); entry.Configure((color == CubeColor.Red ? "红" : color == CubeColor.Green ? "绿" : "蓝") + "色接触方块", prefab);
                    entry.EnsureEntryId(); palette.AddProp(entry);
                }
            }
            if (palette != null) EditorUtility.SetDirty(palette);
            AssetDatabase.SaveAssets(); EditorApplication.Exit(0);
        }
        private static Material Material(string path, Color color)
        {
            var value = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (value == null) { value = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(value, path); }
            value.SetColor("_BaseColor", color); EditorUtility.SetDirty(value); return value;
        }
    }
}
