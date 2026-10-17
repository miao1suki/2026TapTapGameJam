using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Project.BlockFeatures;
using Project.ColorBlocks;
using Project.LevelEditor;
using Project.Rooms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

public static class PlantObstacleBurnAudit
{
    private const string Flag = "PlantObstacleBurnAudit.Running";
    private static readonly List<GameObject> Objects = new List<GameObject>();
    private static int checks;
    private static double started;

    public static void Run()
    {
        if (!Application.dataPath.Replace('\\', '/').Contains("/Library/FlowRetirementProbe/Assets"))
            throw new InvalidOperationException("Only run in the isolated validation project.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Flag, true);
        Resume();
        EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod]
    private static void Resume()
    {
        if (!SessionState.GetBool(Flag, false)) return;
        started = EditorApplication.timeSinceStartup;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup - started > 90) { Finish(false, "Timed out"); return; }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.time < .15f) return;
        EditorApplication.update -= Tick;
        SimulationMode original = Physics.simulationMode;
        try
        {
            Physics.simulationMode = SimulationMode.Script;
            ColorRuntimeService.Instance.SetAllUnlockedForCurrentSession(true);
            CheckConnections(); Clear();
            CheckPreciseGeometry(); Clear();
            CheckRegionsAndReset(); Clear();
            CheckLongChain(); Clear();
            Finish(true, "PLANT BURN PASS " + checks + " assertions");
        }
        catch (Exception error) { Finish(false, error.ToString()); }
        finally { Physics.simulationMode = original; }
    }

    private static PlantObstacleFeature Plant(Vector3 position)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/Content/ColorBlocks/Prefabs/Green_PlantObstacle.prefab");
        GameObject item = Object.Instantiate(prefab, position, Quaternion.identity);
        Objects.Add(item);
        var plant = item.GetComponent<PlantObstacleFeature>();
        Check(plant.IsIntact, "spawned real obstacle is attached and intact");
        return plant;
    }

    private static void CheckConnections()
    {
        var a = Plant(Vector3.zero);
        var b = Plant(Vector3.right);
        var c = Plant(Vector3.right + Vector3.up);
        var diagonal = Plant(new Vector3(2, 2, 0));
        var separated = Plant(Vector3.left * 2);
        int notifications = 0;
        foreach (var plant in new[] { a, b, c }) plant.IntactChanged += (_, intact) =>
        {
            if (intact) return;
            Check(!a.IsIntact && !b.IsIntact && !c.IsIntact, "listeners observe an atomically burned group");
            notifications++;
        };
        Check(!a.ApplyColor("blue", null) && a.IsIntact, "blue does not burn obstacles");
        Check(a.ApplyColor("red", null), "existing red interaction starts connected burn");
        Check(new[] { a, b, c }.All(plant => !plant.IsIntact && !plant.ShowsArtwork), "four-way chain burns entirely");
        Check(diagonal.IsIntact && separated.IsIntact, "diagonal and separated obstacles stay intact");
        Check(notifications == 3 && !b.BreakObstacle(), "one notification per obstacle; repeat burn is rejected");
        foreach (var plant in new[] { a, b, c })
        {
            Check(plant.GetComponentsInChildren<Collider>(true).All(volume => !volume.enabled), "burn disables real collision geometry");
            plant.ResetForRoom();
            Check(plant.IsIntact && plant.ShowsArtwork && plant.GetComponent<BoxCollider>().enabled, "room reset restores collision and artwork");
        }
        Check(a.BreakObstacle() && !c.IsIntact, "reset group can be burned again");
        Clear();
        a = Plant(Vector3.zero); b = Plant(Vector3.one - Vector3.forward);
        Check(a.BreakObstacle() && b.IsIntact, "two diagonal corners in 2x2 are disconnected");
        Clear();
        a = Plant(Vector3.zero); b = Plant(Vector3.right); c = Plant(Vector3.right * 2);
        b.gameObject.SetActive(false);
        Check(a.BreakObstacle() && c.IsIntact, "inactive object cannot bridge a burn");
        Clear();
        a = Plant(Vector3.zero); b = Plant(Vector3.right * 1.01f);
        Check(a.BreakObstacle() && b.IsIntact, "visible small gap is not contact");
        Clear();
        a = Plant(Vector3.zero); b = Plant(Vector3.right * 1.0005f);
        Check(a.BreakObstacle() && !b.IsIntact, "grid floating-point noise does not split neighbors");
        Clear();
        a = Plant(Vector3.zero); b = Plant(Vector3.right + Vector3.forward * 2);
        Check(a.BreakObstacle() && b.IsIntact, "different depth planes do not connect");
    }

    private static void CheckPreciseGeometry()
    {
        var ring = Plant(Vector3.zero);
        ring.transform.localScale = new Vector3(5, 5, 1);
        ring.gameObject.AddComponent<LevelEditorMergedShape>().Configure(new Vector2Int(5, 5), new[]
        {
            new RectInt(0, 0, 5, 1), new RectInt(0, 4, 5, 1),
            new RectInt(0, 1, 1, 3), new RectInt(4, 1, 1, 3)
        });
        var center = Plant(Vector3.zero);
        var edge = Plant(Vector3.right * 3);
        Check(ring.BreakObstacle() && !edge.IsIntact && center.IsIntact,
            "merged ring burns its real edge neighbor without filling the hole");
        Check(ring.GetComponents<BoxCollider>().All(volume => !volume.enabled),
            "colliders added after attachment are all disabled");
        ring.ResetForRoom();
        Check(ring.GetComponents<BoxCollider>().All(volume => volume.enabled), "reset restores all merged volumes");
        Clear();
        var a = Plant(Vector3.zero); a.transform.localScale = Vector3.one * .5f;
        var b = Plant(Vector3.right * .5f); b.transform.localScale = Vector3.one * .5f;
        Check(a.BreakObstacle() && !b.IsIntact, "non-unit world scale still connects actual faces");
    }

    private static void CheckRegionsAndReset()
    {
        var owner = new GameObject("Burn region audit"); Objects.Add(owner);
        var manager = owner.AddComponent<RoomLifecycleManager>();
        manager.Configure(new List<RoomRegion>
        {
            new RoomRegion { id = "a", cells = new List<Vector2Int> { new Vector2Int(0, -1), new Vector2Int(1, -1) } },
            new RoomRegion { id = "b", cells = new List<Vector2Int> { new Vector2Int(2, -1) } }
        }, 1f, "");
        var a = Plant(new Vector3(.5f, .5f));
        var b = Plant(new Vector3(1.5f, .5f));
        var otherRoom = Plant(new Vector3(2.5f, .5f));
        foreach (var plant in new[] { a, b, otherRoom })
            manager.Register(plant.gameObject.AddComponent<RoomResetObject>());
        Check(a.BreakObstacle() && !b.IsIntact && otherRoom.IsIntact, "burn stops at room ownership boundary");
        manager.ResetRegion("a");
        Check(a.IsIntact && b.IsIntact && otherRoom.IsIntact, "existing region reset restores the whole burned group");
        Check(ColorRuntimeService.Instance.IsUnlocked("green"), "burn and room reset preserve unlocked color");
    }

    private static void CheckLongChain()
    {
        var plants = new List<PlantObstacleFeature>();
        for (int i = 0; i < 160; i++) plants.Add(Plant(new Vector3(i, 100, 0)));
        var watch = Stopwatch.StartNew();
        plants[80].BreakObstacle(); watch.Stop();
        Check(plants.All(plant => !plant.IsIntact), "long chain has no recursive depth limit");
        Debug.Log("PLANT BURN event CPU for 160 objects: " + watch.Elapsed.TotalMilliseconds.ToString("F3") + " ms");
    }

    private static void Clear()
    {
        foreach (GameObject item in Objects) if (item != null) Object.DestroyImmediate(item);
        Objects.Clear();
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException("PLANT BURN FAIL: " + message);
        checks++; Debug.Log("PLANT BURN PASS: " + message);
    }

    private static void Finish(bool passed, string message)
    {
        SessionState.SetBool(Flag, false); EditorApplication.update -= Tick;
        Debug.Log(message);
        if (EditorApplication.isPlaying)
        {
            EditorApplication.ExitPlaymode();
            EditorApplication.delayCall += () => EditorApplication.Exit(passed ? 0 : 1);
        }
        else EditorApplication.Exit(passed ? 0 : 1);
    }
}
