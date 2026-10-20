using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Project.Fireflies;
using Project.GridArt;
using Project.GridArt.Editor;
using Project.LevelEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>One-shot, isolated author-art integration. Never rebuilds level objects.</summary>
public static class TAArtworkIntegration
{
    private const string Gaming = "Assets/_Project/Scenes/Playing/Gaming.unity";
    private const string MapPath = "Assets/_Project/Scenes/Playing/Gaming_Art.asset";
    private static readonly StringBuilder report = new();
    private static int checks;
    public static void Run()
    {
        if (!Application.dataPath.Replace('\\', '/').EndsWith("/Library/FlowRetirementProbe/Assets", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Use the isolated FlowRetirementProbe.");
        int exit = 0;
        try { Integrate(); report.AppendLine("PASS " + checks + " preservation assertions"); }
        catch (Exception e) { exit = 1; report.AppendLine(e.ToString()); Debug.LogException(e); }
        finally { File.WriteAllText("Library/TAArtworkIntegration.txt", report.ToString()); Debug.Log(report.ToString()); EditorApplication.Exit(exit); }
    }
    private static void Check(bool okay, string message)
    {
        if (!okay) throw new InvalidOperationException(message);
        checks++;
    }
    private static T[] All<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
    private static Dictionary<string, string> Snapshot(Scene scene)
    {
        var result = new Dictionary<string, string>();
        foreach (var t in All<Transform>(scene))
        {
            if (t.GetComponentInParent<GridArtScene>(true) != null || t.GetComponentInParent<FireflyField>(true) != null) continue;
            foreach (var c in t.GetComponents<Component>())
                if (c != null && (c.hideFlags & HideFlags.DontSaveInEditor) == 0)
                    result[GlobalObjectId.GetGlobalObjectIdSlow(c).ToString()] = EditorJsonUtility.ToJson(c);
        }
        return result;
    }
    private static void Preserve(Dictionary<string,string> before, Scene scene, string phase)
    {
        var after = Snapshot(scene);
        Check(before.Count == after.Count, phase + " component count unchanged");
        foreach (var pair in before) Check(after.TryGetValue(pair.Key, out string value) && value == pair.Value, phase + " preserves " + pair.Key);
        report.AppendLine(phase + " unchanged non-art components=" + before.Count);
    }
    private static void AddEffect(FireflyField source, Scene target, Vector3 offset)
    {
        Vector3 expected = source.transform.position + offset;
        if (All<FireflyField>(target).Any(f => Vector3.Distance(f.transform.position, expected) < .001f))
        { report.AppendLine("Existing firefly retained at " + expected); return; }
        string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(source.gameObject);
        Check(!string.IsNullOrEmpty(path), "TA firefly retains prefab source");
        var clone = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), target);
        PrefabUtility.SetPropertyModifications(clone, PrefabUtility.GetPropertyModifications(source.gameObject));
        clone.transform.SetPositionAndRotation(expected, source.transform.rotation);
        clone.transform.localScale = source.transform.lossyScale;
        clone.name = "TA 萤火虫区域 · MRoom2";
        Check(clone.GetComponentsInChildren<Collider>(true).Length == 0, "ambient effect adds no gameplay collider");
        var field = clone.GetComponent<FireflyField>();
        Check(field.AreaCells == source.AreaCells && field.LayoutSeed == source.LayoutSeed && field.MotionSeed == source.MotionSeed,
            "TA effect area and motion settings retained");
        report.AppendLine("Firefly added at " + expected + " scale=" + clone.transform.lossyScale + " area=" + field.AreaCells);
    }
    private static void Integrate()
    {
        var jsons = new[] { "Assets/_Project/Scenes/Playing/Gaming.planning.json", "Assets/_Project/Content/LevelMaps/正式关卡.json", "Assets/Scenes/MRoom2.planning.json" };
        var bytes = jsons.ToDictionary(p => p, File.ReadAllBytes);
        var sourceScene = EditorSceneManager.OpenScene("Assets/TAIntegrationReview/TA_MRoom2.unity", OpenSceneMode.Single);
        var sources = All<FireflyField>(sourceScene);
        Check(sources.Length == 1, "source has exactly the newly authored firefly region");
        var scene = EditorSceneManager.OpenScene(Gaming, OpenSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        var before = Snapshot(scene);
        int blocks = All<LevelEditorPlacedBlock>(scene).Length;
        var incoming = AssetDatabase.LoadAssetAtPath<GridArtMap>("Assets/TAIntegrationReview/TA_Gaming_Art.asset");
        var map = AssetDatabase.LoadAssetAtPath<GridArtMap>(MapPath);
        Check(incoming != null && map != null && incoming.layers.Sum(l => l.cells.Count) == 3972, "exact TA 3972-cell source");
        Check(incoming.palette == map.palette, "stable shared palette reference");
        string name = map.name;
        EditorUtility.CopySerialized(incoming, map); map.name = name;
        map.ownerSceneGuid = AssetDatabase.AssetPathToGUID(Gaming);
        foreach (var layer in map.layers) foreach (var cell in layer.cells)
            Check(map.palette.Get(cell.brush) != null, "source brush exists " + cell.brush);
        map.bakedRevision = -1; map.bakedPaletteHash = ""; EditorUtility.SetDirty(map);
        var art = GridArtBuilder.Apply(map, scene, false);
        Check(art != null && All<GridArtScene>(scene).Length == 1, "one formal artwork output");
        foreach (var filter in art.GetComponentsInChildren<MeshFilter>(true))
        {
            var uv = new List<Vector4>(); filter.sharedMesh.GetUVs(1, uv);
            Check(uv.Count == filter.sharedMesh.vertexCount, "new art retains UV1 seam repair");
        }
        AddEffect(sources[0], scene, new Vector3(-68, 69, 0));
        Preserve(before, scene, "Gaming after artwork apply");
        Check(All<LevelEditorPlacedBlock>(scene).Length == blocks, "formal placed block count unchanged: " + blocks);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssetIfDirty(map);
        report.AppendLine("Artwork layers=" + map.layers.Count + " cells=" + map.layers.Sum(l => l.cells.Count));
        var mroom = EditorSceneManager.OpenScene("Assets/Scenes/MRoom2.unity", OpenSceneMode.Additive);
        SceneManager.SetActiveScene(mroom);
        var mroomBefore = Snapshot(mroom);
        AddEffect(sources[0], mroom, Vector3.zero);
        Preserve(mroomBefore, mroom, "MRoom2 ambient-only apply");
        EditorSceneManager.SaveScene(mroom);
        foreach (var pair in bytes) Check(File.ReadAllBytes(pair.Key).SequenceEqual(pair.Value), "planning bytes unchanged " + pair.Key);
        EditorSceneManager.CloseScene(mroom, true);
        EditorSceneManager.CloseScene(sourceScene, true);
        scene = EditorSceneManager.OpenScene(Gaming, OpenSceneMode.Single);
        Preserve(before, scene, "Gaming saved/reopened");
        Check(All<LevelEditorPlacedBlock>(scene).Length == blocks, "saved formal layout unchanged");
        Check(All<GridArtScene>(scene).Single().map.layers.Sum(l => l.cells.Count) == 3972, "saved TA artwork retained");
        IntegrateTrial();
        report.AppendLine("Only map/artwork output and ambient effects modified; code and layout retained.");
    }

    private static void IntegrateTrial()
    {
        const string scenePath = "Assets/_Project/Development/GridArt/TileArtTrial/网格美术测试.unity";
        const string mapPath = "Assets/_Project/Development/GridArt/TileArtTrial/示例美术图.asset";
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        var before = Snapshot(scene);
        var source = AssetDatabase.LoadAssetAtPath<GridArtMap>("Assets/TAIntegrationReview/TA_ExampleArt.asset");
        var target = AssetDatabase.LoadAssetAtPath<GridArtMap>(mapPath);
        Check(source != null && target != null && source.palette == target.palette, "TA example keeps original palette");
        string name = target.name;
        EditorUtility.CopySerialized(source, target); target.name = name;
        target.ownerSceneGuid = AssetDatabase.AssetPathToGUID(scenePath);
        target.bakedRevision = -1; target.bakedPaletteHash = "";
        GridArtBuilder.Apply(target, scene, false);
        Preserve(before, scene, "TA example art-only apply");
        EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssetIfDirty(target);
        report.AppendLine("TA example cells=" + target.layers.Sum(l => l.cells.Count));
    }
}
