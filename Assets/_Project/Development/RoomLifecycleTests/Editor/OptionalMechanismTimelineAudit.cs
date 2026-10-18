using System;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Project.BlockFeatures;
using Project.CameraModes;
using Project.ColorBlocks;
using Project.LevelEditor;
using Project.LevelEditor.Editor;
using Project.Mechanisms;
using Project.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

/// <summary>Opt-in camera shots: old authored data, true button edges, cleanup and platform motion.</summary>
public static class OptionalMechanismTimelineAudit
{
    private const string Flag = "OptionalMechanismTimelineAudit.Running";
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int phase, checks;
    private static double started;
    private static float ready;
    private static PlayerController player;
    private static DoorController door;
    private static DoorButton first, second;
    private static MovingPlatformFeature platform;
    private static Vector3 platformOrigin;
    private static int savedDepth;
    private static int waitingPhase = -1;
    private static double conditionDeadline;

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException("OPTIONAL TIMELINE: " + message);
        checks++; Debug.Log("OPTIONAL TIMELINE PASS: " + message);
    }
    private static void Set(object target, string field, object value) =>
        target.GetType().GetField(field, Hidden).SetValue(target, value);
    private static object Call(object target, string name, params object[] args) =>
        target.GetType().GetMethod(name, Hidden).Invoke(target, args);
    private static GameObject Spawn(string path, Vector3 position) => Object.Instantiate(
        AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Content/" + path), position, Quaternion.identity);

    private static bool Await(bool condition, string description)
    {
        if (condition) return true;
        if (waitingPhase != phase)
        {
            waitingPhase = phase;
            conditionDeadline = EditorApplication.timeSinceStartup + 5d;
        }
        if (EditorApplication.timeSinceStartup >= conditionDeadline)
            throw new InvalidOperationException("OPTIONAL TIMELINE timed out waiting for " + description);
        // Editor update can observe a large Time.time jump before the next
        // Coroutine continuation. Observe the real Director lifecycle instead.
        phase--;
        return false;
    }

    public static void Run()
    {
        if (!Application.dataPath.Replace('\\', '/').Contains("/Library/FlowRetirementProbe/Assets") ||
            !Array.Exists(Environment.GetCommandLineArgs(), argument => argument == "-noaudio"))
            throw new InvalidOperationException("Silent isolated validation project only");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        SessionState.SetBool(Flag, true); Resume(); EditorApplication.EnterPlaymode();
    }
    [InitializeOnLoadMethod]
    private static void Resume()
    {
        if (!SessionState.GetBool(Flag, false)) return;
        phase = checks = 0; waitingPhase = -1; started = EditorApplication.timeSinceStartup;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
    }
    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup - started > 80) { Finish(false, "Timeout"); return; }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.time < .15f || Time.time < ready) return;
        try
        {
            switch (phase++)
            {
                case 0:
                    VerifyAuthoredDefaults();
                    ColorRuntimeService.Instance.SetAllUnlockedForCurrentSession(true);
                    player = Spawn("Player/Player.prefab", new Vector3(-30f, 20f, 0f)).GetComponent<PlayerController>();
                    player.GetComponent<PlayerInputDriver>().enabled = false;
                    player.Motor.constraints = RigidbodyConstraints.FreezeAll;
                    savedDepth = player.ControlLockDepth;
                    var cameraRoot = new GameObject("Optional shot test camera");
                    var camera = cameraRoot.AddComponent<Camera>(); camera.orthographic = true;
                    cameraRoot.AddComponent<AudioListener>(); AudioListener.volume = 0f; AudioListener.pause = true;
                    camera.transform.position = new Vector3(0f, 0f, -10f);
                    cameraRoot.AddComponent<CameraControlManager>().ConfigureOutput(camera);
                    cameraRoot.AddComponent<TimelineCamRig>().target = player.transform;
                    door = Spawn("Mechanisms/Door.prefab", Vector3.zero).GetComponent<DoorController>();
                    first = Spawn("Mechanisms/DoorButton.prefab", new Vector3(10f, 20f, 0f)).GetComponent<DoorButton>();
                    second = Spawn("Mechanisms/DoorButton.prefab", new Vector3(12f, 20f, 0f)).GetComponent<DoorButton>();
                    door.SetButtons(new[] { first, second });
                    Check(door.TimelineTrigger == DoorTimelineTrigger.Disabled, "new / legacy door defaults to disabled");
                    Check(first.TryPress(player.gameObject) && second.TryPress(player.gameObject), "real presses open default door");
                    Call(door, "AdvanceTimers", 2f);
                    Check(door.State == DoorState.Unlocked && !door.IsTimelinePlaying, "default door opens without camera shot");
                    Check(door.GetComponents<PlayableDirector>().Length == 0 && player.ControlLockDepth == savedDepth,
                        "disabled default allocates no Director and acquires no player lock");
                    VerifyPersistence();
                    door.ResetDoor();
                    Set(door, "timelineTrigger", DoorTimelineTrigger.ButtonPressed);
                    Set(door, "cameraMoveDuration", .1f); Set(door, "cameraHoldDuration", .8f); Set(door, "cameraReturnDuration", .05f);
                    Check(first.TryPress(player.gameObject), "partial first button can request optional shot");
                    ready = Time.time + .16f;
                    break;
                case 1:
                    if (!Await(door.GetComponents<PlayableDirector>().Length > 0, "button shot Director to start")) return;
                    Check(door.IsTimelinePlaying && door.State == DoorState.Locked, "button mode plays before every button is complete");
                    Check(player.ControlLockDepth == savedDepth + 1,
                        "explicitly enabled shot may own exactly one player lock (actual " + player.ControlLockDepth + ", baseline " + savedDepth + ")");
                    Check(!first.TryPress(player.gameObject), "already pressed button cannot produce another edge");
                    door.NotifyButtonPressed(player.gameObject);
                    Check((int)typeof(DoorController).GetField("queuedButtonShots", Hidden).GetValue(door) == 0,
                        "re-evaluation and duplicate press do not enqueue another shot");
                    first.ResetButton();
                    Check(first.TryPress(player.gameObject), "release / re-press is a new valid edge");
                    Check((int)typeof(DoorController).GetField("queuedButtonShots", Hidden).GetValue(door) == 1,
                        "second true press queues one shot rather than overlapping directors");
                    door.enabled = false;
                    Check(!door.IsTimelinePlaying && player.ControlLockDepth == savedDepth, "disable cancels queued shots and releases only its lock");
                    ready = Time.time + .12f;
                    break;
                case 2:
                    if (!Await(door.GetComponents<PlayableDirector>().Length == 0, "disabled door Director to retire")) return;
                    Check(door.GetComponents<PlayableDirector>().Length == 0, "disable retires owned Director");
                    door.ResetDoor(); Set(door, "timelineTrigger", DoorTimelineTrigger.DoorOpening); door.enabled = true;
                    Check(first.TryPress(player.gameObject), "opening mode accepts first pressure");
                    Check(!door.IsTimelinePlaying, "opening mode does not play for partial progress");
                    Check(second.TryPress(player.gameObject), "opening mode accepts final pressure");
                    ready = Time.time + .16f;
                    break;
                case 3:
                    if (!Await(door.GetComponents<PlayableDirector>().Length > 0, "opening shot Director to start")) return;
                    Check(door.IsTimelinePlaying && player.ControlLockDepth == savedDepth + 1, "opening mode plays one opt-in shot on all-button completion");
                    door.ResetDoor();
                    Check(!door.IsTimelinePlaying && player.ControlLockDepth == savedDepth, "reset cancels opening shot without a stranded lock");
                    platform = Spawn("ColorBlocks/Prefabs/Red_Platform.prefab", new Vector3(40f, 20f, 0f)).GetComponent<MovingPlatformFeature>();
                    Check(platform.TimelineTrigger == MovingPlatformTimelineTrigger.Disabled, "legacy and new platform default to disabled");
                    Set(platform, "driveMode", MovingPlatformDriveMode.Automatic); Set(platform, "travelDuration", .4f);
                    Set(platform, "travelBlocks", 1); Set(platform, "holdAtEnds", .1f); Set(platform, "reverseOnBlock", false);
                    platform.ResetForRoom(); platformOrigin = platform.transform.position;
                    ready = Time.time + .15f;
                    break;
                case 4:
                    Check(Vector3.Distance(platformOrigin, platform.transform.position) > .001f, "default platform still moves normally");
                    Check(!platform.IsTimelinePlaying && platform.GetComponents<PlayableDirector>().Length == 0 && player.ControlLockDepth == savedDepth,
                        "default platform motion has no Timeline / input freeze");
                    Set(platform, "timelineTrigger", MovingPlatformTimelineTrigger.MovementStarted);
                    Set(platform, "lockPlayerDuringTimeline", false);
                    Set(platform, "cameraMoveDuration", .1f); Set(platform, "cameraHoldDuration", .8f); Set(platform, "cameraReturnDuration", .05f);
                    platform.ResetForRoom(); ready = Time.time + .15f;
                    break;
                case 5:
                    if (!Await(platform.GetComponents<PlayableDirector>().Length > 0, "movement edge Director to start")) return;
                    Check(platform.IsTimelinePlaying && platform.GetComponents<PlayableDirector>().Length == 1,
                        "platform motion edge requests one opt-in shot");
                    Check(player.ControlLockDepth == savedDepth, "optional non-locking platform shot keeps player input free");
                    ready = Time.time + .15f;
                    break;
                case 6:
                    Check(platform.GetComponents<PlayableDirector>().Length == 1, "continued movement does not create one Director per physics step");
                    platform.ResetForRoom();
                    Check(!platform.IsTimelinePlaying, "room reset cancels platform shot");
                    Set(platform, "timelineTrigger", MovingPlatformTimelineTrigger.EndpointReached);
                    Set(platform, "travelDuration", .1f); Set(platform, "holdAtEnds", 5f);
                    platform.ResetForRoom(); ready = Time.time + .22f;
                    break;
                case 7:
                    if (!Await(platform.GetComponents<PlayableDirector>().Length > 0, "endpoint edge Director to start")) return;
                    Check(platform.IsTimelinePlaying && platform.GetComponents<PlayableDirector>().Length == 1,
                        "arrival edge can independently trigger platform shot");
                    Set(platform, "timelineTrigger", MovingPlatformTimelineTrigger.Disabled);
                    Call(platform, "FixedUpdate");
                    Check(!platform.IsTimelinePlaying, "switching platform Timeline off immediately cleans the session");
                    ready = Time.time + .1f;
                    break;
                case 8:
                    var source = Spawn("ColorBlocks/Prefabs/Red_Button.prefab", new Vector3(60f, 20f, 0f)).GetComponent<ButtonLockFeature>();
                    source.SetOn(false);
                    Set(platform, "driveMode", MovingPlatformDriveMode.SignalControlled);
                    Set(platform, "linkedControls", new MonoBehaviour[] { source });
                    Set(platform, "timelineTrigger", MovingPlatformTimelineTrigger.Powered);
                    platform.ResetForRoom(); Call(platform, "FixedUpdate");
                    Check(!platform.IsTimelinePlaying, "unpowered signal mode has no camera shot");
                    source.SetOn(true); ready = Time.time + .16f;
                    break;
                case 9:
                    if (!Await(platform.GetComponents<PlayableDirector>().Length > 0, "power edge Director to start")) return;
                    Check(platform.IsTimelinePlaying && platform.GetComponents<PlayableDirector>().Length == 1,
                        "rising power edge requests exactly one platform shot");
                    for (int index = 0; index < 20; index++) Call(platform, "FixedUpdate");
                    Check(platform.GetComponents<PlayableDirector>().Length == 1, "sustained signal never creates per-step Timeline instances");
                    platform.ResetForRoom();
                    // Existing external control locks remain intact on cancel.
                    var extra = player.AcquireControlLock("audit independent lock");
                    platform.ResetForRoom();
                    Check(player.ControlLockDepth == savedDepth + 1, "mechanism cleanup cannot release unrelated control locks");
                    extra.Dispose();
                    Finish(true, "PASS " + checks + " optional mechanism Timeline assertions");
                    break;
            }
        }
        catch (Exception error) { Finish(false, error.ToString()); }
    }
    private static void VerifyPersistence()
    {
        using var serialized = new SerializedObject(door);
        var property = serialized.FindProperty("timelineTrigger"); property.intValue = 1; serialized.ApplyModifiedPropertiesWithoutUndo();
        Check(LevelEditorComponentOverrideUtility.TryCapture(door, property, out var captured), "planning editor captures door Timeline mode");
        var copy = JsonUtility.FromJson<LevelEditorComponentValueOverride>(JsonUtility.ToJson(captured));
        var restored = Spawn("Mechanisms/Door.prefab", new Vector3(0f, 50f, 0f)).GetComponent<DoorController>();
        LevelEditorComponentOverrideUtility.ApplyOverrides(restored.gameObject, new[] { copy }, false);
        Check(restored.TimelineTrigger == DoorTimelineTrigger.ButtonPressed, "planning JSON round-trip preserves explicit opt-in");
        Object.Destroy(restored.gameObject);
        property.intValue = 0; serialized.ApplyModifiedPropertiesWithoutUndo();
        var platformPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Content/ColorBlocks/Prefabs/Red_Platform.prefab");
        using var platformSerialized = new SerializedObject(platformPrefab.GetComponent<MovingPlatformFeature>());
        Check(platformSerialized.FindProperty("timelineTrigger").intValue == 0, "shared platform prefab remains opt-out without prefab rewrite");
        Check(typeof(DoorController).GetField("timelineTrigger", Hidden).GetCustomAttribute<BlockParameterAttribute>() != null &&
            typeof(MovingPlatformFeature).GetField("timelineTrigger", Hidden).GetCustomAttribute<BlockParameterAttribute>() != null,
            "both options are exposed to existing parameter windows");
    }
    private static void VerifyAuthoredDefaults()
    {
        string doorGuid = AssetDatabase.AssetPathToGUID("Assets/_Project/Code/Systems/Mechanisms/Runtime/DoorController.cs");
        string platformGuid = AssetDatabase.AssetPathToGUID("Assets/_Project/Code/Systems/BlockFeatures/Runtime/Features/MovingPlatformFeature.cs");
        int found = 0;
        foreach (string sceneGuid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(sceneGuid);
            string text = File.ReadAllText(path);
            foreach (Match component in Regex.Matches(text, @"(?ms)^--- !u!114\b.*?(?=^---|\z)"))
            {
                if (!component.Value.Contains("guid: " + doorGuid) && !component.Value.Contains("guid: " + platformGuid)) continue;
                found++;
                var configured = Regex.Match(component.Value, @"(?m)^  timelineTrigger: (\d+)\s*$");
                Check(!configured.Success || configured.Groups[1].Value == "0", "authored default remains opt-out: " + path);
            }
        }
        Check(found > 0, "read-only scan covers existing authored doors / platforms without opening or rewriting scenes");
    }
    private static void Finish(bool success, string message)
    {
        EditorApplication.update -= Tick; SessionState.SetBool(Flag, false);
        File.WriteAllText(Path.Combine(Application.dataPath, "../Library/OptionalMechanismTimelineAudit.txt"), message);
        if (success) Debug.Log(message); else Debug.LogError(message);
        EditorApplication.Exit(success ? 0 : 1);
    }
}
