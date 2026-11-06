#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Project.BlockFeatures;
using Project.CameraModes;
using Project.ColorBlocks;
using Project.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

/// <summary>Automatic native-2D steps, real player/vine assets, and the formal ending entrance.</summary>
public static class VineEndingRegressionAudit
{
    private const string Key = "VineEndingRegressionAudit";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly List<string> results = new();
    private static int phase, failures;
    private static float since, initialY;
    private static double started;
    private static PlayerController rider, blocked, jumper, top, bottom;
    private static ClimbableVineFeature vine, ceilingVine;
    private static bool intermediate;
    private static Camera camera;
    private static CameraControlManager manager;
    private static bool ending;

    public static void Run() => Begin(false);
    public static void RunEnding() => Begin(true);
    private static void Begin(bool realEnding)
    {
        if (!Application.dataPath.Replace('\\', '/').Contains("/Library/FlowRetirementProbe/Assets") ||
            !Environment.GetCommandLineArgs().Contains("-noaudio"))
            throw new InvalidOperationException("Use the silent isolated probe only.");
        if (realEnding) EditorSceneManager.OpenScene("Assets/_Project/Scenes/Playing/Gaming.unity");
        else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetInt(Key, realEnding ? 2 : 1); Resume(); EditorApplication.EnterPlaymode();
    }
    [InitializeOnLoadMethod] private static void Resume()
    {
        if (SessionState.GetInt(Key, 0) == 0) return;
        phase = failures = 0; results.Clear(); ending = SessionState.GetInt(Key, 0) == 2;
        started = EditorApplication.timeSinceStartup;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
    }
    private static void Set(object owner, string field, object value)
    {
        for (Type t = owner.GetType(); t != null; t = t.BaseType)
        {
            var f = t.GetField(field, Private);
            if (f != null) { f.SetValue(owner, value); return; }
        }
        throw new MissingFieldException(field);
    }
    private static T Spawn<T>(string path, Vector3 position) where T : Component =>
        Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Content/" + path),
            position, Quaternion.identity).GetComponent<T>();
    private static PlayerController Player(float x, float y)
    {
        var p = Spawn<PlayerController>("Player/Player.prefab", new Vector3(x, y, 0));
        p.GetComponent<PlayerInputDriver>().enabled = false;
        return p;
    }
    private static ClimbableVineFeature Vine(float x)
    {
        var v = Spawn<ClimbableVineFeature>("ColorBlocks/Prefabs/Green_Ladder.prefab", new Vector3(x, 0, 0));
        Set(v, "maxGrowthBlocks", 2); Set(v, "growInstantly", false);
        return v;
    }
    private static Transform Box(string name, Vector3 at, Vector2 size)
    {
        var go = new GameObject(name); go.transform.position = at;
        go.AddComponent<BoxCollider2D>().size = size; return go.transform;
    }
    private static void Check(bool ok, string label)
    {
        string row = (ok ? "PASS " : "FAIL ") + label;
        results.Add(row); Debug.Log("VINE_ENDING " + row); if (!ok) failures++;
    }
    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup - started > 110) { Finish(new Exception("Timed out")); return; }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.time < .3f) return;
        try { if (ending) EndingTick(); else VineTick(); }
        catch (Exception e) { Finish(e); }
    }
    private static void VineTick()
    {
        if (phase == 0)
        {
            ColorRuntimeService.Instance.SetAllUnlockedForCurrentSession(true);
            new GameObject("Silent listener").AddComponent<AudioListener>();
            vine = Vine(0); rider = Player(0, 1.05f);
            ceilingVine = Vine(8); blocked = Player(8, 1.05f);
            Box("Head ceiling", new Vector3(8, 2.25f, 0), new Vector2(2, 1));
            Vine(16); jumper = Player(16, 1.05f);
            var surface = Box("Real horizontal vine support", new Vector3(24, 0, 0), new Vector2(5, 1));
            top = Player(24, 1.05f); bottom = Player(24, -1.015f);
            Physics2D.SyncTransforms(); phase = 1; since = Time.time;
        }
        else if (phase == 1 && Time.time - since > .5f)
        {
            initialY = rider.Motor.position.y;
            Check(rider.IsGrounded && jumper.IsGrounded, "real player stands on vine collider");
            vine.GrowTo(2); ceilingVine.GrowTo(1);
            jumper.RequestJump();
            var source = GameObject.Find("Real horizontal vine support").transform;
            top.SetHorizontalVineSurface(source, top.Motor.position.y, 2f);
            bottom.SetHorizontalVineSurface(source, bottom.Motor.position.y, 2f);
            top.RequestJump(); bottom.RequestJump();
            phase = 2; since = Time.time;
        }
        else if (phase == 2)
        {
            intermediate |= rider.Motor.position.y > initialY + .05f && rider.Motor.position.y < initialY + 1.9f;
            if (Time.time - since < .06f) return;
            Check(jumper.VerticalVelocity > 0f, "vertical vine top jumps on first press");
            Check(top.VerticalVelocity > 0f, "horizontal vine top jumps on first press");
            Check(bottom.VerticalVelocity < 0f && !bottom.IsHorizontalClimbing, "underside jump still releases downward");
            phase = 3;
        }
        else if (phase == 3)
        {
            intermediate |= rider.Motor.position.y > initialY + .05f && rider.Motor.position.y < initialY + 1.9f;
            if (Time.time - since < 1.5f) return;
            Check(vine.CurrentGrowthBlocks == 2, "direct rider allows two continuous growth cells");
            Check(intermediate && Mathf.Abs(rider.Motor.position.y - initialY - 2) < .12f,
                "direct rider is lifted smoothly by exactly two cells");
            Check(ceilingVine.CurrentGrowthBlocks == 0 && ceilingVine.IsGrowthBlocked,
                "head clearance blocks growth before crushing player");
            Check(!rider.IsDead && !blocked.IsDead, "growth preserves both players");
            Finish(null);
        }
    }
    private static void EndingTick()
    {
        var flow = Project.GameFlow.GameFlowController.Instance;
        if (phase == 0)
        {
            if (flow == null || !flow.IsInitialized || flow.IsTransitioning) return;
            var session = Project.Saving.RunSessionCoordinator.Instance;
            if (session != null) { session.StopAllCoroutines(); session.enabled = false; }
            flow.EnterGameplayImmediatelyForDevelopment();
            rider = Object.FindFirstObjectByType<PlayerController>();
            rider.GetComponent<PlayerInputDriver>().enabled = false;
            var placed = Object.FindObjectsByType<ClimbableVineFeature>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Check(placed.Length > 100, "formal scene resolves all authored vine instances");
            Check(placed.All(v => Mathf.Abs((float)typeof(ClimbableVineFeature).GetField("growthInterval", Private).GetValue(v) - .09f) < .0001f),
                "all formal placed vines resolve the new .09 second interval");
            Check(placed.All(v => Mathf.Abs((float)typeof(ClimbableVineFeature).GetField("visualGrowthDuration", Private).GetValue(v) - 1.15f) < .0001f),
                "all formal placed vines resolve the new 1.15 second visual duration");
            camera = Camera.main; manager = camera.GetComponent<CameraControlManager>();
            phase = 1; since = Time.time;
        }
        else if (phase == 1 && Time.time - since > .5f)
        {
            // This long-lived authored shot is entered on the pre-Boss route.
            var down = Object.FindObjectsByType<ContactTimelineTrigger>(FindObjectsSortMode.None)
                .First(t => t.Timeline != null && t.Timeline.name == "camera_down");
            down.DebugTrigger();
            phase = 4; since = Time.time;
        }
        else if (phase == 4 && Time.time - since > .5f)
        {
            Sample("pre-Boss shot");
            Check(manager.CurrentViewOffset.y < -1f, "authored pre-Boss downward shot is active");
            Check(TeleportAnchorFeature.TryGetAnchor("End", out var anchor), "formal End anchor exists");
            Check(rider.TryTeleportToStoryAnchor(anchor), "formal story teleport succeeds");
            Sample("teleport");
            Vector3 atEntry = camera.WorldToViewportPoint(rider.transform.position);
            Check(atEntry.y >= .2f && atEntry.y <= .6f, "covered story teleport has correct framing immediately");
            phase = 2; since = Time.time;
        }
        else if (phase == 2 && Time.time - since > .5f)
        {
            Sample("half-second"); phase = 3;
        }
        else if (phase == 3 && Time.time - since > 1.5f)
        {
            Sample("settled");
            Vector3 viewport = camera.WorldToViewportPoint(rider.transform.position);
            Check(viewport.y >= .2f && viewport.y <= .6f, "ending entrance keeps player within intended lower central frame");
            foreach (var d in Object.FindObjectsByType<PlayableDirector>(FindObjectsSortMode.None))
                if (d.state == PlayState.Playing) results.Add("Playing director " + d.name + " / " + d.playableAsset?.name);
            Finish(null);
        }
    }
    private static void Sample(string when)
    {
        string row = $"CAMERA {when}: player={rider.transform.position} viewport={camera.WorldToViewportPoint(rider.transform.position)} " +
            $"camera={camera.transform.position} size={camera.orthographicSize} source={manager.ActiveControlName} " +
            $"offset={manager.CurrentViewOffset} focus={manager.FocusPoint}";
        results.Add(row); Debug.Log(row);
    }
    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick; SessionState.EraseInt(Key);
        if (error != null) { failures++; results.Add(error.ToString()); Debug.LogException(error); }
        string report = $"{(failures == 0 ? "PASS" : "FAIL")} failures={failures}\n" + string.Join("\n", results);
        File.WriteAllText("Library/VineEnding" + (ending ? "Camera" : "Physics") + ".txt", report);
        Debug.Log(report); EditorApplication.Exit(failures == 0 ? 0 : 1);
    }
}
#endif
