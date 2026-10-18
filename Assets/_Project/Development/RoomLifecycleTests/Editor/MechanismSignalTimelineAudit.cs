using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Project.BlockFeatures;
using Project.CameraModes;
using Project.ColorBlocks;
using Project.LevelEditor;
using Project.LevelEditor.Editor;
using Project.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

/// <summary>Real energy overlap, PlayableDirectors, shared camera and authored-parameter persistence.</summary>
public static class MechanismSignalTimelineAudit
{
    private const string Flag = "MechanismSignalTimelineAudit.Running";
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static MechanismBaseFeature source;
    private static MovingPlatformFeature first, second;
    private static GameObject energy, cameraRoot;
    private static PlayerController player;
    private static int phase, checks, baseline;
    private static double started, deadline;
    private static int waitingPhase;
    private static Vector3 firstOrigin, secondOrigin;
    private static readonly List<string> order = new List<string>();
    private static readonly HashSet<int> baselineDirectorIds = new HashSet<int>();
    private static int destructionFrame;
    private static string previous;
    private static IDisposable externalLock;

    public static void Run()
    {
        string path = Application.dataPath.Replace('\\', '/');
        if ((!path.Contains("/Library/FlowRetirementProbe/Assets") && !path.Contains("/Library/MechanismSignalProbe/Assets")) ||
            !Array.Exists(Environment.GetCommandLineArgs(), item => item == "-noaudio"))
            throw new InvalidOperationException("Silent isolated validation project only");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        SessionState.SetBool(Flag, true); Resume(); EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod]
    private static void Resume()
    {
        if (!SessionState.GetBool(Flag, false)) return;
        phase = checks = 0; waitingPhase = -1; started = EditorApplication.timeSinceStartup;
        order.Clear(); previous = null;
        baselineDirectorIds.Clear();
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
    }

    private static void Check(bool value, string label)
    {
        if (!value) throw new InvalidOperationException("SIGNAL TIMELINE: " + label);
        checks++; Debug.Log("SIGNAL TIMELINE PASS: " + label);
    }

    private static GameObject Spawn(string relative, Vector3 position) => Object.Instantiate(
        AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Content/" + relative), position, Quaternion.identity);
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Hidden).SetValue(target, value);
    private static void Call(object target, string method) => target.GetType().GetMethod(method, Hidden).Invoke(target, null);
    private static bool Await(bool condition, string label)
    {
        if (condition) { waitingPhase = -1; return true; }
        if (waitingPhase != phase) { waitingPhase = phase; deadline = EditorApplication.timeSinceStartup + 12d; }
        if (EditorApplication.timeSinceStartup > deadline) throw new InvalidOperationException("Waiting for " + label + "; shots=" + string.Join(",", order));
        phase--; return false;
    }

    private static void ChangeEnergy(bool on)
    {
        var position = new Vector3(on ? -1f : -10f, 5f, 0f);
        energy.transform.position = position;
        var body = energy.GetComponent<Rigidbody>();
        if (body != null) { body.position = position; if (!body.isKinematic) body.linearVelocity = Vector3.zero; }
        Physics.SyncTransforms(); Call(source, "ScanForEnergy");
        Check(source.IsSignalActive == on, on ? "real neighboring red cube energizes base" : "moving red cube away removes signal");
    }

    private static bool AllFinished => !source.IsTimelinePlaying && !first.IsTimelinePlaying && !second.IsTimelinePlaying;
    private static void StartRecording() { order.Clear(); previous = null; }
    private static void Observe()
    {
        if (source == null || first == null || second == null) return;
        string active = null;
        int activeCount = 0;
        foreach (var component in new Component[] { source, first, second })
            foreach (var director in component.GetComponents<PlayableDirector>())
                if (director.state == PlayState.Playing)
                { activeCount++; active = component == source ? "base" : component == first ? "first20" : "second10"; }
        if (activeCount > 1) throw new InvalidOperationException("Overlapping mechanism Directors");
        if (player.ControlLockDepth > baseline + 1) throw new InvalidOperationException("Overlapping mechanism player locks");
        if (active != null && active != previous) order.Add(active);
        previous = active;
    }

    private static void ConfigureShot(object component, object trigger)
    {
        Set(component, "timelineTrigger", trigger);
        Set(component, "cameraMoveDuration", .1f); Set(component, "cameraHoldDuration", .7f);
        Set(component, "cameraReturnDuration", .1f); Set(component, "lockPlayerDuringTimeline", true);
    }

    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup - started > 110d) { Finish(false, "Timeout"); return; }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.time < .1f) return;
        try
        {
            Observe();
            switch (phase++)
            {
                case 0:
                    AudioListener.volume = 0f; AudioListener.pause = true;
                    ColorRuntimeService.Instance.SetAllUnlockedForCurrentSession(true);
                    player = Spawn("Player/Player.prefab", new Vector3(-25f, 12f, 0f)).GetComponent<PlayerController>();
                    player.GetComponent<PlayerInputDriver>().enabled = false;
                    player.Motor.constraints = RigidbodyConstraints.FreezeAll;
                    baseline = player.ControlLockDepth;
                    cameraRoot = new GameObject("Signal timeline audit camera");
                    var camera = cameraRoot.AddComponent<Camera>(); camera.orthographic = true;
                    camera.transform.position = new Vector3(0f, 5f, -10f);
                    cameraRoot.AddComponent<CameraControlManager>().ConfigureOutput(camera);
                    cameraRoot.AddComponent<TimelineCamRig>().target = player.transform;
                    // The standard player's action runner owns a legitimate idle Director.
                    foreach (var existing in Object.FindObjectsByType<PlayableDirector>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                        baselineDirectorIds.Add(existing.GetInstanceID());
                    Check(baselineDirectorIds.Contains(player.GetComponent<PlayableDirector>().GetInstanceID()),
                        "fixture preserves standard player action Director as an unrelated resource");
                    var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    floor.transform.position = new Vector3(0f, 4f, 0f); floor.transform.localScale = new Vector3(40f, 1f, 4f);
                    source = Spawn("ColorBlocks/Prefabs/Red_Foundation.prefab", new Vector3(0f, 5f, 0f)).GetComponent<MechanismBaseFeature>();
                    Set(source, "useGravity", false); source.ResetForRoom();
                    energy = Spawn("ContactCubes/Red_ContactCube.prefab", new Vector3(-10f, 5f, 0f));
                    first = Spawn("ColorBlocks/Prefabs/Red_Platform.prefab", new Vector3(25f, 8f, 0f)).GetComponent<MovingPlatformFeature>();
                    second = Spawn("ColorBlocks/Prefabs/Red_Platform.prefab", new Vector3(35f, 8f, 0f)).GetComponent<MovingPlatformFeature>();
                    foreach (var platform in new[] { first, second })
                    {
                        Set(platform, "travelBlocks", 2); Set(platform, "travelDuration", .4f);
                        Set(platform, "reverseOnBlock", false); platform.BindBase(source);
                    }
                    firstOrigin = first.transform.position; secondOrigin = second.transform.position;
                    Check(source.TimelineTrigger == MechanismBaseTimelineTrigger.Disabled &&
                        first.TimelineTrigger == MovingPlatformTimelineTrigger.Disabled, "base and platform authored defaults are opt-out");
                    Check(source.PlatformTimelineMode == MechanismPlatformTimelineMode.AllOrdered, "legacy base defaults to all ordered without enabling shots");
                    Call(source, "ScanForEnergy"); ChangeEnergy(true);
                    Check(AllFinished && source.GetComponents<PlayableDirector>().Length == 0 && player.ControlLockDepth == baseline,
                        "default signal creates no session, Director or player lock");
                    break;
                case 1:
                    if (!Await((first.transform.position - firstOrigin).sqrMagnitude > .001f &&
                        (second.transform.position - secondOrigin).sqrMagnitude > .001f, "default physical motion")) return;
                    Check(AllFinished, "default platforms physically move without camera sessions");
                    ChangeEnergy(false);
                    ConfigureShot(source, MechanismBaseTimelineTrigger.SignalChanged);
                    ConfigureShot(first, MovingPlatformTimelineTrigger.Powered); ConfigureShot(second, MovingPlatformTimelineTrigger.Powered);
                    Set(first, "timelineOrder", 20); Set(second, "timelineOrder", 10);
                    VerifyParameters(); StartRecording(); ChangeEnergy(true);
                    break;
                case 2:
                    if (!Await(AllFinished && order.Count >= 3, "base and two ordered platform shots")) return;
                    Check(string.Join(",", order) == "base,second10,first20", "base precedes platforms, numeric order beats creation order");
                    Check(player.ControlLockDepth == baseline, "completed sequence restores player lock depth");
                    Check((first.transform.position - firstOrigin).sqrMagnitude > .001f ||
                        (second.transform.position - secondOrigin).sqrMagnitude > .001f, "camera sequence does not pause physical platform motion");
                    for (int index = 0; index < 25; index++) Call(source, "ScanForEnergy");
                    Check(AllFinished, "sustained power does not retrigger a sequence");
                    Set(source, "timelineTrigger", MechanismBaseTimelineTrigger.Powered);
                    ChangeEnergy(false); Set(source, "platformTimelineMode", MechanismPlatformTimelineMode.FirstOnly);
                    StartRecording(); ChangeEnergy(true);
                    break;
                case 3:
                    if (!Await(AllFinished && order.Count >= 2, "base and single selected platform")) return;
                    Check(string.Join(",", order) == "base,second10", "FirstOnly selects lowest-order enabled platform and skips others permanently");
                    Set(source, "platformTimelineMode", MechanismPlatformTimelineMode.AllOrdered);
                    ConfigureShot(source, MechanismBaseTimelineTrigger.SignalChanged);
                    ConfigureShot(first, MovingPlatformTimelineTrigger.Unpowered); ConfigureShot(second, MovingPlatformTimelineTrigger.Unpowered);
                    StartRecording(); ChangeEnergy(false);
                    break;
                case 4:
                    if (!Await(AllFinished && order.Count >= 3, "ordered release sequence")) return;
                    Check(string.Join(",", order) == "base,second10,first20", "release edge also sequences base then explicitly enabled platforms");
                    ConfigureShot(first, MovingPlatformTimelineTrigger.Powered); ConfigureShot(second, MovingPlatformTimelineTrigger.Powered);
                    StartRecording(); ChangeEnergy(true);
                    break;
                case 5:
                    if (!Await(order.Count > 0 && order[0] == "base" && source.GetComponents<PlayableDirector>().Length > 0,
                        "base to hold active shot before cancellation")) return;
                    externalLock = player.AcquireControlLock("independent audit lock");
                    source.ResetForRoom(); first.ResetForRoom(); second.ResetForRoom();
                    Check(AllFinished && player.ControlLockDepth == baseline + 1, "room reset cancels active and queued shots, preserving unrelated lock");
                    externalLock.Dispose(); externalLock = null;
                    Call(source, "ScanForEnergy");
                    Check(AllFinished && source.IsSignalActive, "reset rescans existing energy without replaying reset-generated edges");
                    Set(source, "timelineTrigger", MechanismBaseTimelineTrigger.Powered);
                    ChangeEnergy(false); StartRecording(); ChangeEnergy(true);
                    break;
                case 6:
                    if (!Await(source.GetComponents<PlayableDirector>().Length > 0 && order.Count > 0, "new sequence after reset")) return;
                    first.BindBase(null);
                    Check(!first.IsTimelinePlaying && first.LinkedControls.Count == 0, "unbind cancels only that platform queued request");
                    second.gameObject.SetActive(false);
                    Check(!second.IsTimelinePlaying, "platform disable removes its queued shot");
                    source.gameObject.SetActive(false);
                    Check(!source.IsTimelinePlaying && player.ControlLockDepth == baseline, "base disable cancels its active session and releases lock");
                    break;
                case 7:
                    source.gameObject.SetActive(true); second.gameObject.SetActive(true);
                    first.BindBase(source); second.BindBase(source);
                    source.ResetForRoom(); first.ResetForRoom(); second.ResetForRoom();
                    Set(source, "timelineTrigger", MechanismBaseTimelineTrigger.Powered);
                    ChangeEnergy(false); cameraRoot.SetActive(false); ChangeEnergy(true);
                    Check(AllFinished && player.ControlLockDepth == baseline, "missing active camera cannot leave a queued session or player lock");
                    cameraRoot.SetActive(true); ChangeEnergy(false); StartRecording(); ChangeEnergy(true);
                    break;
                case 8:
                    if (!Await(order.Count > 0 && source.GetComponents<PlayableDirector>().Length > 0, "active sequence before destruction")) return;
                    destructionFrame = Time.frameCount;
                    Object.Destroy(source.gameObject); Object.Destroy(first.gameObject); Object.Destroy(second.gameObject);
                    break;
                case 9:
                    if (!Await(Time.frameCount > destructionFrame + 1 && source == null && first == null && second == null,
                        "owned objects and deferred child resources destruction")) return;
                    Check(player.ControlLockDepth == baseline, "destroying source and consumers leaves no player lock");
                    var remainingDirectors = new HashSet<int>();
                    foreach (var remaining in Object.FindObjectsByType<PlayableDirector>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                        remainingDirectors.Add(remaining.GetInstanceID());
                    Check(remainingDirectors.SetEquals(baselineDirectorIds),
                        "destruction retires every owned Director and preserves the exact unrelated Director baseline");
                    Check(!cameraRoot.GetComponent<TimelineCamRig>().isPlayingAnim, "destruction returns Timeline camera control");
                    Finish(true, "PASS " + checks + " mechanism signal Timeline assertions");
                    break;
            }
        }
        catch (Exception error) { Finish(false, error.ToString()); }
    }

    private static void VerifyParameters()
    {
        var clone = Spawn("ColorBlocks/Prefabs/Red_Foundation.prefab", new Vector3(-15f, 5f, 0f)).GetComponent<MechanismBaseFeature>();
        foreach (string field in new[] { "timelineTrigger", "platformTimelineMode", "cameraMoveDuration", "lockPlayerDuringTimeline" })
        {
            Check(source.GetType().GetField(field, Hidden).GetCustomAttribute<BlockParameterAttribute>() != null,
                "base parameter exposed: " + field);
            using var serialized = new SerializedObject(source);
            Check(LevelEditorComponentOverrideUtility.TryCapture(source, serialized.FindProperty(field), out var captured), "capture base parameter " + field);
            var copy = JsonUtility.FromJson<LevelEditorComponentValueOverride>(JsonUtility.ToJson(captured));
            LevelEditorComponentOverrideUtility.ApplyOverrides(clone.gameObject, new[] { copy }, false);
            Check(Equals(source.GetType().GetField(field, Hidden).GetValue(source), source.GetType().GetField(field, Hidden).GetValue(clone)),
                "JSON override restores base parameter " + field);
        }
        var platformClone = Spawn("ColorBlocks/Prefabs/Red_Platform.prefab", new Vector3(-20f, 5f, 0f)).GetComponent<MovingPlatformFeature>();
        using (var serialized = new SerializedObject(first))
        {
            Check(LevelEditorComponentOverrideUtility.TryCapture(first, serialized.FindProperty("timelineOrder"), out var captured), "capture platform sequence order");
            var copy = JsonUtility.FromJson<LevelEditorComponentValueOverride>(JsonUtility.ToJson(captured));
            LevelEditorComponentOverrideUtility.ApplyOverrides(platformClone.gameObject, new[] { copy }, false);
            Check(platformClone.TimelineOrder == 20, "platform sequence order survives planning JSON round-trip");
        }
        Object.Destroy(clone.gameObject); Object.Destroy(platformClone.gameObject);
    }

    private static void Finish(bool success, string message)
    {
        externalLock?.Dispose(); externalLock = null;
        EditorApplication.update -= Tick; SessionState.SetBool(Flag, false);
        File.WriteAllText(Path.Combine(Application.dataPath, "../Library/MechanismSignalTimelineAudit.txt"), message);
        if (success) Debug.Log(message); else Debug.LogError(message);
        EditorApplication.Exit(success ? 0 : 1);
    }
}
