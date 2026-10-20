using System;
using System.Collections.Generic;
using System.IO;
using Project.BlockFeatures;
using Project.Mechanisms;
using Project.ObjectArtwork;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Explicit isolated-project verification; never saves an authored scene.</summary>
public static class OneWayDeliveryAudit
{
    private const string Pending = "OneWayDeliveryAudit.Pending";
    private static int ticks;
    private static double deadline;
    private static readonly List<GameObject> instances = new();
    private static int checks;

    [InitializeOnLoadMethod]
    private static void Resume()
    {
        if (!SessionState.GetBool(Pending, false)) return;
        deadline = EditorApplication.timeSinceStartup + 80;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    public static void Run()
    {
        if (!Application.dataPath.Replace('\\', '/').Contains("/Library/NeutralMechanismProbe/Assets"))
            throw new InvalidOperationException("Audit only runs in NeutralMechanismProbe.");
        try
        {
            if (File.Exists("Library/OneWayDeliveryFailure.txt")) File.Delete("Library/OneWayDeliveryFailure.txt");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            checks = 0;
            Verify(false);
            File.WriteAllText("Library/OneWayDeliveryEdit.txt", $"PASS {checks} actual prefab/editor presentation checks.\n");
            foreach (var item in instances) UnityEngine.Object.DestroyImmediate(item);
            instances.Clear();
            SessionState.SetBool(Pending, true);
            Resume();
            EditorApplication.EnterPlaymode();
        }
        catch (Exception ex) { Fail(ex); }
    }

    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Play audit timed out.");
            if (!EditorApplication.isPlaying || ++ticks < 10) return;
            Verify(true);
            File.WriteAllText("Library/OneWayDeliveryPlay.txt", $"PASS {checks} actual runtime presentation checks; 1/3 cells, neutral/red/green/blue, native pixels, support edge and non-uniform parent cancellation.\n");
            SessionState.SetBool(Pending, false);
            EditorApplication.update -= Tick;
            EditorApplication.Exit(0);
        }
        catch (Exception ex) { Fail(ex); }
    }

    private static void Verify(bool runtime)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Content/Mechanisms/OneWayPlatform.prefab");
        Require(prefab != null, "Missing prefab");
        foreach (OneWayPlatformColor color in Enum.GetValues(typeof(OneWayPlatformColor)))
        foreach (int length in new[] { 1, 3 })
        {
            var item = runtime ? UnityEngine.Object.Instantiate(prefab) : (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instances.Add(item);
            item.transform.position = new Vector3(length * 5, 2, 0);
            item.transform.localScale = new Vector3(length, .2f, 1);
            item.GetComponent<OneWayPlatform>().ConfigureAppearance(color);
            var presentation = MechanismArtworkPresentation.Ensure(item);
            presentation.Refresh(0);
            Physics.SyncTransforms();
            var visual = presentation.Visual;
            Require(presentation.Kind == ObjectArtworkLibrary.OneWayPlatformKind((int)color), "Appearance enum binding changed");
            Require(visual != null && visual.enabled, "Visual missing/disabled");
            Require(visual.sprite.rect.size == new Vector2(16, 16), "Frame is not native 16x16");
            Require(visual.sprite.pixelsPerUnit == 16, "PPU changed");
            Require(visual.drawMode == SpriteDrawMode.Tiled, "Platform stretched rather than tiled");
            Require(Mathf.Abs(visual.transform.lossyScale.x - 1) < .0001f && Mathf.Abs(visual.transform.lossyScale.y - 1) < .0001f, "Non-square art pixels");
            Require(Mathf.Abs(visual.bounds.size.x - length) < .0001f, "Authored footprint changed");
            Require(Mathf.Abs(visual.bounds.size.y - 1) < .0001f, "Native frame height changed");
            Require(Mathf.Abs(visual.bounds.max.y - item.GetComponent<BoxCollider>().bounds.max.y) < .0001f, $"Art support edge does not match collider: art={visual.bounds.max.y}, collider={item.GetComponent<BoxCollider>().bounds.max.y}");
            Require(Mathf.Abs(visual.transform.localScale.y - 5) < .0001f, "Parent Y .2 must be cancelled by child scale 5");
            Require(Mathf.Abs(visual.transform.localPosition.y + 4.5f) < .0001f, "Full frame bottom pivot must be at local Y -4.5");
        }
        foreach (var kind in new[] { MechanismArtworkKind.RedOneWayPlatform, MechanismArtworkKind.GreenOneWayPlatform, MechanismArtworkKind.BlueOneWayPlatform })
        {
            var entry = ObjectArtworkLibrary.Load().Find(kind);
            Require(entry != null && entry.frames.Length == 1, "Color frame missing");
            Require(entry.frames[0].rect.size == new Vector2(16, 16), "Color frame wrong native size");
            Require(entry.supportTopPixels == 16, "Color support edge changed");
        }
    }

    private static void Require(bool value, string message)
    {
        checks++;
        if (!value) throw new InvalidOperationException(message);
    }

    private static void Fail(Exception ex)
    {
        SessionState.SetBool(Pending, false);
        EditorApplication.update -= Tick;
        File.WriteAllText("Library/OneWayDeliveryFailure.txt", ex.ToString());
        Debug.LogException(ex);
        EditorApplication.Exit(1);
    }
}
