using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Project.BlockFeatures;
using Project.BlockFeatures.Editor;
using Project.LevelEditor;
using Project.ObjectArtwork;
using Project.WorldItems;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Silent, isolated edit-time Scene/asset/preview regression audit.</summary>
public static class SceneArtworkPreviewAudit
{
    private const string TestScene = "Assets/_Project/Development/RoomLifecycleTests/SceneArtworkPreviewAudit.unity";
    private const string Content = "Assets/_Project/Content/";
    private static readonly List<GameObject> owners = new();
    private static GameObject fresh, replacement;
    private static Mesh beforePropertyEdit;
    private static int stage, checks, removedId, beforeRemovalCount;
    private static double started, stageStarted;
    private static void Check(bool value, string label)
    {
        if (!value) throw new InvalidOperationException("SCENE ARTWORK: " + label);
        checks++; Debug.Log("SCENE ARTWORK PASS: " + label);
    }
    private static GameObject Spawn(string path, Vector3 position)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Content + path);
        if (prefab == null) throw new InvalidOperationException("Missing audit prefab " + path);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.transform.position = position; owners.Add(go); return go;
    }
    public static void Run()
    {
        if (!Application.dataPath.Replace('\\', '/').Contains("/Library/FlowRetirementProbe/Assets") ||
            Array.IndexOf(Environment.GetCommandLineArgs(), "-noaudio") < 0)
            throw new InvalidOperationException("Run only in the silent Library/FlowRetirementProbe project.");
        started = stageStarted = EditorApplication.timeSinceStartup; checks = stage = 0; owners.Clear();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        ArtworkScenePreview.Clear();
        fresh = Spawn("WorldItems/Spike_上.prefab", Vector3.zero);
        Check(fresh.GetComponent<MechanismArtworkPresentation>() == null, "real legacy spike has no persisted artwork component");
        EditorApplication.update += Tick;
    }
    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup - started > 90) throw new TimeoutException("Scene artwork update timed out, stage " + stage);
            if (stage == 0)
            {
                if (!ArtworkScenePreview.TryGetFirstMesh(fresh, out var mesh, out var material)) return;
                Check(material.mainTexture == ObjectArtworkLibrary.Load().FirstFrame(MechanismArtworkKind.Spike).texture, "newly instantiated spike immediately uses delivered 2D art without entering Play");
                Check(fresh.GetComponent<MechanismArtworkPresentation>() == null && fresh.transform.childCount == 1, "edit preview adds no component or visual child");
                var model = fresh.GetComponentInChildren<MeshRenderer>();
                Check(model.enabled && model.forceRenderingOff, "legacy 3D model is temporarily muted, authored enabled flag preserved");
                Check((mesh.hideFlags & HideFlags.DontSaveInEditor) != 0, "cached preview mesh cannot enter a saved scene");
                removedId = fresh.GetInstanceID(); UnityEngine.Object.DestroyImmediate(fresh);
                replacement = Spawn("WorldItems/Spike_上.prefab", new Vector3(2, 0, 0));
                stage = 1; stageStarted = EditorApplication.timeSinceStartup; return;
            }
            if (stage == 1)
            {
                if (!ArtworkScenePreview.TryGetFirstMesh(replacement, out _, out _)) return;
                Check(EditorUtility.InstanceIDToObject(removedId) == null && ArtworkScenePreview.CachedOwnerCount == 1,
                    "planner-style replacement removes stale owner and refreshes new prefab");
                ExerciseVariants();
                ArtworkScenePreview.TryGetFirstMesh(replacement, out beforePropertyEdit, out _);
                Transform direction = replacement.GetComponentInChildren<MeshCollider>().transform;
                Undo.RecordObject(direction, "Artwork audit change spike direction");
                direction.localRotation = Quaternion.Euler(0, 0, 90);
                Undo.FlushUndoRecordObjects();
                stage = 2; stageStarted = EditorApplication.timeSinceStartup; return;
            }
            if (stage == 2)
            {
                if (!ArtworkScenePreview.TryGetFirstMesh(replacement, out var edited, out _) || ReferenceEquals(edited, beforePropertyEdit)) return;
                Vector3 edge = edited.vertices[1] - edited.vertices[0];
                Check(Mathf.Abs(edge.y) > Mathf.Abs(edge.x) + .01f,
                    "Undo-aware direction edits invalidate the Scene instance artwork parameters");
                beforePropertyEdit = edited;
                Undo.PerformUndo(); stage = 3; stageStarted = EditorApplication.timeSinceStartup; return;
            }
            if (stage == 3)
            {
                if (!ArtworkScenePreview.TryGetFirstMesh(replacement, out var restored, out _) || ReferenceEquals(restored, beforePropertyEdit)) return;
                Vector3 edge = restored.vertices[1] - restored.vertices[0];
                Check(Mathf.Abs(edge.x) > Mathf.Abs(edge.y) + .01f, "Undo restores the original spike direction without Play or forced refresh");
                stage = 4; stageStarted = EditorApplication.timeSinceStartup; return;
            }
            if (stage == 4)
            {
                if (EditorApplication.timeSinceStartup - stageStarted < .4) return;
                VerifySaveAndCache();
                beforeRemovalCount = ArtworkScenePreview.CachedOwnerCount;
                Undo.DestroyObjectImmediate(replacement.GetComponent<NeutralSpike>());
                stage = 5; stageStarted = EditorApplication.timeSinceStartup; return;
            }
            if (stage == 5)
            {
                if (ArtworkScenePreview.TryGetFirstMesh(replacement, out _, out _)) return;
                Check(ArtworkScenePreview.CachedOwnerCount == beforeRemovalCount - 1,
                    "removing the artwork owner component releases its stale preview while preserving the GameObject");
                Check(!replacement.GetComponentInChildren<MeshRenderer>().forceRenderingOff,
                    "removing the owner component releases its transient renderer visibility lease");
                Undo.PerformUndo(); stage = 6; stageStarted = EditorApplication.timeSinceStartup; return;
            }
            if (stage == 6)
            {
                if (!ArtworkScenePreview.TryGetFirstMesh(replacement, out _, out _)) return;
                Check(replacement.GetComponent<NeutralSpike>() != null && ArtworkScenePreview.CachedOwnerCount == beforeRemovalCount,
                    "Undo component removal restores the artwork preview without entering Play");
                Finish(null);
            }
        }
        catch (Exception error) { Finish(error); }
    }
    private static void ExerciseVariants()
    {
        var directions = new[] { "上", "下", "左", "右" };
        for (int i = 0; i < directions.Length; i++)
        {
            var go = Spawn("WorldItems/Spike_" + directions[i] + ".prefab", new Vector3(4 + i * 2, 0, 0));
            ArtworkScenePreview.RefreshObject(go);
            Check(ArtworkScenePreview.TryGetFirstMesh(go, out var mesh, out _), directions[i] + " spike resolves to the formal artwork");
            Check(mesh.uv.Select(value => value.x).Distinct().Count() == 2, directions[i] + " spike retains a full single-cell UV slice");
            foreach (var model in go.GetComponentsInChildren<MeshRenderer>()) Check(model.forceRenderingOff, directions[i] + " spike hides every old 3D triangle");
        }
        string[] variants = {
            "ColorBlocks/Prefabs/Respawn_Block.prefab", "ColorBlocks/Prefabs/Green_JumpPlant.prefab",
            "ColorBlocks/Prefabs/Red_Foundation.prefab", "ColorBlocks/Prefabs/Green_PlantObstacle.prefab",
            "ContactCubes/Red_ContactCube.prefab", "ContactCubes/Green_ContactCube.prefab", "ContactCubes/Blue_ContactCube.prefab",
            "Mechanisms/Door.prefab", "Mechanisms/DoorButton.prefab", "Mechanisms/LadderSide.prefab", "Mechanisms/LadderSideLeft.prefab",
            "ColorBlocks/Prefabs/Red_Crank.prefab", "ColorBlocks/Prefabs/Red_Lava.prefab"
        };
        for (int i = 0; i < variants.Length; i++)
        {
            var go = Spawn(variants[i], new Vector3((i % 7) * 2, 3 + i / 7 * 3, 0));
            int components = go.GetComponentsInChildren<Component>(true).Length;
            ArtworkScenePreview.RefreshObject(go);
            Check(ArtworkScenePreview.TryGetFirstMesh(go, out _, out _), variants[i] + " has edit-time 2D artwork");
            Check(go.GetComponentsInChildren<Component>(true).Length == components, variants[i] + " does not install gameplay or serialized visual components");
        }
        var respawn = owners.First(go => go != null && go.GetComponent<RespawnAnchorFeature>() != null);
        ArtworkScenePreview.TryGetFirstMesh(respawn, out var respawnMesh, out _);
        Check(Mathf.Abs(respawnMesh.bounds.size.x * respawn.transform.lossyScale.x - 2) < .001f,
            "32-pixel checkpoint remains two cells wide without resizing its trigger");

        var shortPlatform = Spawn("ColorBlocks/Prefabs/Red_Platform.prefab", new Vector3(1, 10, 0));
        var placed = shortPlatform.AddComponent<LevelEditorPlacedBlock>(); placed.SetSizeCells(Vector2Int.one);
        ArtworkScenePreview.RefreshObject(shortPlatform);
        Check(ArtworkScenePreview.TryGetFirstMesh(shortPlatform, out var shortMesh, out _), "legacy one-cell platform has a preview");
        Check(Mathf.Abs(shortMesh.bounds.size.x * shortPlatform.transform.lossyScale.x - 1) < .001f, "legacy platform cannot visually overlap adjacent authored cells");
        Rect crop = PlatformArtworkLayout.SliceUV(ObjectArtworkLibrary.Load().FirstFrame(MechanismArtworkKind.MovingPlatform), 1);
        Check(Mathf.Abs(shortMesh.uv.Min(uv => uv.x) - crop.xMin) < .0001f && Mathf.Abs(shortMesh.uv.Max(uv => uv.x) - crop.xMax) < .0001f,
            "legacy platform crops the middle native pixels rather than compressing three cells");

        var water = Spawn("ColorBlocks/Prefabs/Blue_WaterSource.prefab", new Vector3(6, 10, 0));
        var waterPlaced = water.AddComponent<LevelEditorPlacedBlock>(); waterPlaced.SetSizeCells(new Vector2Int(2, 3));
        water.transform.localScale = new Vector3(2, 3, 1);
        var shape = water.AddComponent<LevelEditorMergedShape>();
        shape.Configure(new Vector2Int(2, 3), new[] { new RectInt(0, 0, 1, 3), new RectInt(1, 0, 1, 1) });
        ArtworkScenePreview.RefreshObject(water);
        Check(ArtworkScenePreview.PartCount(water) == 3, "irregular water batches surface, interior and bottom independently");
        Check(shape.ContainsCell(new Vector2Int(1, 0)) && !shape.ContainsCell(new Vector2Int(1, 1)), "preview preserves authored water holes");
        Check(water.GetComponent<BoxCollider>().enabled, "water preview does not toggle author colliders");
    }
    private static void VerifySaveAndCache()
    {
        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.SaveScene(scene, TestScene);
        string before = File.ReadAllText(TestScene);
        int objects = scene.GetRootGameObjects().Sum(root => root.GetComponentsInChildren<Transform>(true).Length);
        int count = ArtworkScenePreview.CachedOwnerCount;
        ArtworkScenePreview.RefreshOpenScenes();
        Check(!scene.isDirty, "refreshing Scene artwork does not dirty the saved scene");
        Check(scene.GetRootGameObjects().Sum(root => root.GetComponentsInChildren<Transform>(true).Length) == objects,
            "opening/refreshing artwork creates no Scene objects");
        EditorSceneManager.SaveScene(scene, TestScene);
        Check(File.ReadAllText(TestScene) == before, "save bytes are unchanged by the edit-time artwork service");
        Check(!before.Contains("Editor artwork") && !before.Contains("ArtworkScenePreview"), "saved scene contains no preview mesh or Editor controller");
        var oldMesh = owners.Where(go => go != null).Select(go => ArtworkScenePreview.TryGetFirstMesh(go, out var mesh, out _) ? mesh : null).ToArray();
        int builds = ArtworkScenePreview.RebuildCount;
        var cameraObject = new GameObject("Artwork audit camera");
        var camera = cameraObject.AddComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 7;
        camera.transform.position = new Vector3(6, 5, -20); camera.backgroundColor = new Color(.08f, .1f, .14f); camera.clearFlags = CameraClearFlags.SolidColor;
        var target = new RenderTexture(1400, 900, 24); camera.targetTexture = target;
        for (int i = 0; i < 12; i++) { ArtworkScenePreview.DrawForCamera(camera); camera.Render(); }
        Check(builds == ArtworkScenePreview.RebuildCount && count == ArtworkScenePreview.CachedOwnerCount,
            "repeated camera rendering reuses cached meshes without re-reading or rebuilding artwork");
        int index = 0;
        foreach (var go in owners.Where(go => go != null))
        {
            ArtworkScenePreview.TryGetFirstMesh(go, out var current, out _);
            Check(current == oldMesh[index++], "cached mesh identity survives repeated view rendering");
        }
        RenderTexture previous = RenderTexture.active; RenderTexture.active = target;
        var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply();
        File.WriteAllBytes("Library/SceneArtworkPreview.png", image.EncodeToPNG());
        Check(image.GetPixels32().Count(pixel => pixel.r > 120 || pixel.g > 120 || pixel.b > 120) > 100,
            "real editor-camera GPU image contains visible artwork");
        RenderTexture.active = previous; camera.targetTexture = null;
        UnityEngine.Object.DestroyImmediate(image); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(cameraObject);
        ArtworkScenePreview.Clear();
        Check(ArtworkScenePreview.CachedOwnerCount == 0, "reload/Play cleanup releases every cached owner");
        Check(!replacement.GetComponentInChildren<MeshRenderer>().forceRenderingOff && replacement.GetComponentInChildren<MeshRenderer>().enabled,
            "cleanup restores legacy visibility without changing its author enabled flag");
        ArtworkScenePreview.RefreshOpenScenes();
        Check(ArtworkScenePreview.TryGetFirstMesh(replacement, out _, out _), "returning to edit mode can restore the 2D preview");
    }
    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick;
        string result = error == null ? "PASS " + checks + " Scene artwork assertions" : "FAIL " + error;
        File.WriteAllText("Library/SceneArtworkPreviewAudit.txt", result);
        if (error == null) Debug.Log(result); else Debug.LogException(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
