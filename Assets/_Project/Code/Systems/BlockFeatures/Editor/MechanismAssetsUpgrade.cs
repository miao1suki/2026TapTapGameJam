using System;
using System.Linq;
using Project.ColorBlocks;
using Project.ContactCubes;
using Project.LevelEditor;
using Project.Mechanisms;
using Project.Rooms;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Project.BlockFeatures.Editor
{
    public static class MechanismAssetsUpgrade
    {
        private const string Colors = "Assets/_Project/Content/ColorBlocks/Prefabs/";
        private const string Cubes = "Assets/_Project/Content/ContactCubes/";
        public static void Run()
        {
            if (!Application.dataPath.Contains("FlowRetirementProbe")) throw new InvalidOperationException("仅允许隔离资源升级。");
            foreach (CubeColor color in Enum.GetValues(typeof(CubeColor))) UpgradeCube(Cubes + color + "_ContactCube.prefab", color, false);
            UpgradeCube(Colors + "Red_Battery.prefab", CubeColor.Red, true);
            var platform = PrefabUtility.LoadPrefabContents(Colors + "Red_Platform.prefab");
            platform.GetComponent<MovingPlatformFeature>().BindBase(null);
            SetColor(platform, "red", platform.GetComponent<Renderer>(), AssetDatabase.LoadAssetAtPath<Material>(Cubes + "Red_ContactCube.mat"));
            PrefabUtility.SaveAsPrefabAsset(platform, Colors + "Red_Platform.prefab"); PrefabUtility.UnloadPrefabContents(platform);
            UpgradeCranks();

            MakeProp("OneWayPlatform", "单向平台", true);
            MakeProp("HiddenBlock", "隐藏方块", false);
            var palette = AssetDatabase.LoadAssetAtPath<LevelEditorPalette>("Assets/_Project/Development/LevelEditor/Orpheus0829/LevelEditorPalette.asset");
            var old = AssetDatabase.LoadAssetAtPath<GameObject>(Colors + "Red_Battery.prefab");
            foreach (var entry in palette.PropEntries) if (entry.Prefab == old)
            {
                entry.Configure("红色接触方块", AssetDatabase.LoadAssetAtPath<GameObject>(Cubes + "Red_ContactCube.prefab"));
                entry.ClearComponentValueOverrides();
            }
            EditorUtility.SetDirty(palette);
            var playerPath = "Assets/_Project/Content/Player/RuntimePlayer.prefab";
            var player = PrefabUtility.LoadPrefabContents(playerPath);
            player.GetComponent<CapsuleCollider>().height = 1;
            var playerSettings = new SerializedObject(player.GetComponent<Project.Player.PlayerController>());
            playerSettings.FindProperty("jumpRiseCells").floatValue = 4; playerSettings.ApplyModifiedPropertiesWithoutUndo();
            const string meshPath = "Assets/_Project/Content/Player/OneCellCapsule.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (mesh == null)
            {
                mesh = Object.Instantiate(player.GetComponent<MeshFilter>().sharedMesh); mesh.name = "OneCellCapsule";
                var vertices = mesh.vertices; for (int i = 0; i < vertices.Length; i++) vertices[i].y *= .5f;
                mesh.vertices = vertices; mesh.RecalculateBounds(); mesh.RecalculateNormals(); AssetDatabase.CreateAsset(mesh, meshPath);
            }
            player.GetComponent<MeshFilter>().sharedMesh = mesh;
            PrefabUtility.SaveAsPrefabAsset(player,playerPath); PrefabUtility.UnloadPrefabContents(player);
            AssetDatabase.SaveAssets(); EditorApplication.Exit(0);
        }
        public static void RunCranks()
        {
            if (!Application.dataPath.Contains("FlowRetirementProbe")) throw new InvalidOperationException("仅允许隔离资源升级。");
            UpgradeCranks(); AssetDatabase.SaveAssets(); EditorApplication.Exit(0);
        }
        private static void UpgradeCranks()
        {
            var crank = PrefabUtility.LoadPrefabContents(Colors + "Red_Crank.prefab");
            var child = crank.transform.Find("升降平台");
            if (child != null) Object.DestroyImmediate(child.gameObject);
            var config = new SerializedObject(crank.GetComponent<CrankPlatformFeature>());
            config.FindProperty("initialPlatformCreated").boolValue = false; config.ApplyModifiedPropertiesWithoutUndo();
            SetColor(crank,"red",crank.GetComponent<Renderer>(),AssetDatabase.LoadAssetAtPath<Material>(Cubes + "Red_ContactCube.mat"));
            PrefabUtility.SaveAsPrefabAsset(crank,Colors + "Red_Crank.prefab"); PrefabUtility.UnloadPrefabContents(crank);

            var lift = PrefabUtility.LoadPrefabContents(Colors + "Red_Platform.prefab"); lift.name = "Red_CrankLift";
            var material = lift.GetComponent<Renderer>().sharedMaterial;
            Object.DestroyImmediate(lift.GetComponent<Renderer>()); Object.DestroyImmediate(lift.GetComponent<MeshFilter>());
            lift.transform.localScale = Vector3.one;
            lift.GetComponent<BoxCollider>().size = new Vector3(1,.25f,1);
            var model = GameObject.CreatePrimitive(PrimitiveType.Cube); model.name = "平台模型";
            Object.DestroyImmediate(model.GetComponent<Collider>());
            model.transform.SetParent(lift.transform,false); model.transform.localScale = new Vector3(1,.25f,1);
            model.GetComponent<Renderer>().sharedMaterial = material;
            var feature = lift.GetComponent<MovingPlatformFeature>(); feature.BindCrank(null);
            var settings = new SerializedObject(feature); settings.FindProperty("route").enumValueIndex = 1;
            settings.FindProperty("travelBlocks").intValue = 4; settings.FindProperty("reverseOnBlock").boolValue = false;
            settings.ApplyModifiedPropertiesWithoutUndo();
            SetColor(lift,"red",model.GetComponent<Renderer>(),AssetDatabase.LoadAssetAtPath<Material>(Cubes + "Red_ContactCube.mat"));
            var prefab = PrefabUtility.SaveAsPrefabAsset(lift,CrankPlatformAuthoring.PlatformPath); PrefabUtility.UnloadPrefabContents(lift);
            var palette = AssetDatabase.LoadAssetAtPath<LevelEditorPalette>("Assets/_Project/Development/LevelEditor/Orpheus0829/LevelEditorPalette.asset");
            foreach (var entry in palette.PropEntries) if (entry.Prefab == AssetDatabase.LoadAssetAtPath<GameObject>(Colors + "Red_Crank.prefab")) entry.Configure("曲柄",entry.Prefab);
            if (!palette.PropEntries.Any(entry => entry.EntryId == "mechanism-crank-lift"))
            {
                var entry = new LevelEditorPropEntry(); entry.Configure("曲柄升降平台",prefab);
                typeof(LevelEditorPropEntry).GetField("entryId",System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(entry,"mechanism-crank-lift");
                palette.AddProp(entry);
            }
            EditorUtility.SetDirty(palette);
        }
        private static void UpgradeCube(string path, CubeColor color, bool legacy)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            if (legacy && root.GetComponent<ContactCube>() == null)
            {
                var old = root.GetComponent<EnergyBlockFeature>(); if (old != null) Object.DestroyImmediate(old);
                var oldCollider = root.GetComponent<Collider>(); if (oldCollider != null) Object.DestroyImmediate(oldCollider);
                var oldRenderer = root.GetComponent<Renderer>(); if (oldRenderer != null) Object.DestroyImmediate(oldRenderer);
                var filter = root.GetComponent<MeshFilter>(); if (filter != null) Object.DestroyImmediate(filter);
                var rb = root.GetComponent<Rigidbody>(); if (rb == null) rb = root.AddComponent<Rigidbody>(); rb.useGravity = true;
                rb.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;
                rb.interpolation = RigidbodyInterpolation.Interpolate; rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                var body = new GameObject("实体"); body.transform.SetParent(root.transform,false); var shape = body.AddComponent<BoxCollider>();
                var model = GameObject.CreatePrimitive(PrimitiveType.Cube); model.name = "模型"; model.transform.SetParent(body.transform,false); Object.DestroyImmediate(model.GetComponent<Collider>());
                model.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Cubes + "Red_ContactCube.mat");
                var cube = root.AddComponent<ContactCube>(); var config = new SerializedObject(cube);
                config.FindProperty("color").enumValueIndex = 0; config.FindProperty("shape").objectReferenceValue = shape; config.FindProperty("model").objectReferenceValue = model.transform; config.ApplyModifiedPropertiesWithoutUndo();
                root.AddComponent<ContactCubeMotion>();
                if (root.GetComponent<RoomResetObject>() == null) root.AddComponent<RoomResetObject>();
            }
            var renderer = root.GetComponentInChildren<Renderer>();
            SetColor(root, color == CubeColor.Red ? "red" : color == CubeColor.Blue ? "blue" : "green",renderer,AssetDatabase.LoadAssetAtPath<Material>(Cubes + color + "_ContactCube.mat"));
            PrefabUtility.SaveAsPrefabAsset(root,path); PrefabUtility.UnloadPrefabContents(root);
        }
        private static void SetColor(GameObject root,string id,Renderer renderer,Material real)
        {
            var color = root.GetComponent<ColorObject>(); if (color == null) color = root.AddComponent<ColorObject>();
            var config = new SerializedObject(color);
            config.FindProperty("baseColorTypeId").stringValue = id;
            config.FindProperty("targetRenderer").objectReferenceValue = renderer;
            config.FindProperty("unlockedMaterial").objectReferenceValue = real;
            config.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void MakeProp(string name,string title,bool oneWay)
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Cube); root.name = name;
            if (oneWay)
            {
                root.transform.localScale = new Vector3(1,.2f,1); root.AddComponent<OneWayPlatform>();
            }
            else Object.DestroyImmediate(root.GetComponent<Collider>());
            root.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<ColorCatalog>("Assets/_Project/Resources/ColorBlocks/ColorCatalog.asset").NeutralMaterial;
            root.AddComponent<RoomResetObject>();
            string path = "Assets/_Project/Content/Mechanisms/" + name + ".prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root,path); Object.DestroyImmediate(root);
            var palette = AssetDatabase.LoadAssetAtPath<LevelEditorPalette>("Assets/_Project/Development/LevelEditor/Orpheus0829/LevelEditorPalette.asset");
            if (!palette.PropEntries.Any(p => p.Prefab == prefab)) { var entry = new LevelEditorPropEntry(); entry.Configure(title,prefab); palette.AddProp(entry); EditorUtility.SetDirty(palette); }
        }
    }
}
