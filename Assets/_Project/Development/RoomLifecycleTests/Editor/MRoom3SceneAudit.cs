using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using PlanningEditorPrototype;
using Project.BlockFeatures;
using Project.ColorBlocks;
using Project.ContactCubes;
using Project.LevelEditor;
using Project.Mechanisms;
using Project.Player;
using Project.Rooms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

/// <summary>Real window updates and real imported-scene gameplay, never the main editor.</summary>
public static class MRoom3SceneAudit
{
    private const string Flag = "MRoom3SceneAudit";
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static PlanningEditorWindow window;
    private static PlanningDocument expected;
    private static double next, deadline;
    private static float due;
    private static int phase, checks, basisIndex;
    private static PlayerController player;
    private static ContactCube red;
    private static MovingPlatformFeature[] platforms;
    private static MechanismBaseFeature[] bases;
    private static DoorController[] doors;
    private static readonly Dictionary<MovingPlatformFeature, Vector3> origins = new();
    private static void Check(bool success, string text)
    {
        if (!success) throw new Exception(text);
        checks++;
    }
    public static void Run()
    {
        if (!Application.dataPath.Replace('\\', '/').Contains("/Library/FlowRetirementProbe/Assets") ||
            !Environment.GetCommandLineArgs().Contains("-noaudio")) throw new Exception("Silent isolated project only");
        EditorSceneManager.OpenScene(MRoom3MapImport.ScenePath);
        expected = PlanningSaveStore.Read(MRoom3MapImport.SavePath);
        foreach (var old in Resources.FindObjectsOfTypeAll<PlanningEditorWindow>()) Object.DestroyImmediate(old);
        window = ScriptableObject.CreateInstance<PlanningEditorWindow>(); window.position = new Rect(40, 40, 1700, 950); window.Show();
        phase = checks = 0; next = EditorApplication.timeSinceStartup + 1; deadline = next + 45;
        EditorApplication.update += WindowTick;
    }
    private static void WindowTick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Window timeout");
            if (EditorApplication.timeSinceStartup < next) return;
            next = EditorApplication.timeSinceStartup + 1;
            Check(window.rootVisualElement.panel != null, "actual window attached");
            var actual = (PlanningDocument)typeof(PlanningEditorWindow).GetField("document", Hidden).GetValue(window);
            Check(actual.ToPersistentJson() == expected.ToPersistentJson(), "auto-sync preserves new map including walls/water");
            MRoom3MapImport.VerifyScene(expected);
            if (phase++ == 1)
                typeof(PlanningEditorWindow).GetMethod("ApplyMapToScene", Hidden).Invoke(window, null);
            if (phase == 3)
            {
                window.Close(); window = ScriptableObject.CreateInstance<PlanningEditorWindow>();
                window.position = new Rect(40, 40, 1700, 950); window.Show();
            }
            if (phase < 5) return;
            window.Close(); window = null; EditorApplication.update -= WindowTick;
            // Return to the exact authored disk scene after the window exercise.
            EditorSceneManager.OpenScene(MRoom3MapImport.ScenePath);
            SessionState.SetBool(Flag, true); Resume(); EditorApplication.EnterPlaymode();
        }
        catch (Exception error) { Finish(error); }
    }
    [InitializeOnLoadMethod] private static void Resume()
    {
        if (!SessionState.GetBool(Flag, false)) return;
        phase = checks = basisIndex = 0; due = 1; deadline = EditorApplication.timeSinceStartup + 100;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
    }
    private static void MoveRed(Vector3 position)
    {
        red.transform.position = position; red.GetComponent<Rigidbody>().position = position; Physics.SyncTransforms();
    }
    private static void Wait(int step, float seconds) { phase = step; due = Time.time + seconds; }
    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup > deadline) { Finish(new Exception("Gameplay timeout, phase " + phase)); return; }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.time < due) return;
        try
        {
            if (phase == 0)
            {
                player = Object.FindFirstObjectByType<PlayerController>();
                var rooms = Object.FindFirstObjectByType<RoomLifecycleManager>();
                Check(player != null && Camera.main != null && Camera.main.orthographic, "standard player and 2D follow camera");
                Check(rooms.CurrentRegionId == MRoom3MapImport.RegionId, "spawn inside new region");
                Check(Object.FindObjectsByType<RespawnAnchorFeature>(FindObjectsSortMode.None).Count(a => a.IsCurrent) == 1, "one active checkpoint");
                foreach (string color in new[] { "red", "green", "blue" })
                    Check(!ColorRuntimeService.Instance.IsUnlocked(color) && !ColorRuntimeService.Instance.IsAbilityUnlocked(color), "default locked " + color);
                player.GetComponent<PlayerInputDriver>().enabled = false; player.Motor.constraints = RigidbodyConstraints.FreezeAll;
                platforms = Object.FindObjectsByType<MovingPlatformFeature>(FindObjectsSortMode.None);
                bases = Object.FindObjectsByType<MechanismBaseFeature>(FindObjectsSortMode.None).OrderBy(b => b.transform.position.x).ToArray();
                Check(platforms.Length == 4 && bases.Length == 3, "four platforms three bases");
                origins.Clear(); foreach (var p in platforms) { origins[p] = p.transform.position; Check(!p.IsDriveAvailable, "locked platform inactive"); }
                foreach (string color in new[] { "red", "green", "blue" })
                {
                    ColorRuntimeService.Instance.Unlock(color);
                    ColorAppearanceManager.Instance.Play(color, true, 0f);
                }
                var cubes = Object.FindObjectsByType<ContactCube>(FindObjectsSortMode.None);
                Check(cubes.Length == 14, "thirteen reds and one blue");
                red = cubes.First(c => c.Color == CubeColor.Red);
                // Freeze test energy sources; do not let the other authored reds
                // incidentally change supply while testing one base at a time.
                foreach (var cube in cubes)
                {
                    cube.GetComponent<ContactCubeMotion>().enabled = false;
                    cube.GetComponent<Rigidbody>().isKinematic = true;
                }
                Physics.SyncTransforms(); Wait(1, 2); return;
            }
            if (phase == 1)
            {
                foreach (var basis in bases) Check(!basis.UsesGravity && !basis.IsSignalActive, "fixed unpowered base");
                Capture("Overview", new Vector3(16, -6, -20), 33, 2560, 1280);
                Capture("Upper", new Vector3(13, 12, -20), 12, 2560, 960);
                Capture("Lower", new Vector3(49, -17, -20), 21, 1280, 1280);
                int index = 0;
                foreach (var cube in Object.FindObjectsByType<ContactCube>(FindObjectsSortMode.None))
                {
                    // Park inside the room, otherwise the real region-boundary
                    // guard correctly restores these test objects to authored poses.
                    cube.transform.position = new Vector3(25.5f + index++ * 2, -30.5f, 0);
                    cube.GetComponent<Rigidbody>().position = cube.transform.position;
                }
                MoveRed(bases[basisIndex].transform.position + (basisIndex == 1 ? Vector3.down : Vector3.up));
                Wait(2, .5f); return;
            }
            if (phase == 2)
            {
                Check(bases[basisIndex].IsSignalActive, "real red charges base " + basisIndex + " base=" + bases[basisIndex].transform.position +
                    " red=" + red.transform.position + " available=" + red.IsEnergyAvailable);
                var controlled = platforms.Where(p => p.LinkedControls.Contains(bases[basisIndex])).ToArray();
                Check(controlled.Length == (basisIndex == 1 ? 2 : 1), "correct shared base consumers");
                foreach (var p in controlled)
                {
                    Check(p.SignalMotion == MovingPlatformSignalMotion.PoweredEndpoint, "charge/return mode persisted");
                    Check(Vector3.Dot(p.transform.position - origins[p], (Vector3)p.Path.Axis * p.Path.FirstEnd) > .001f,
                        "platform actually advances on authored route: " + p.name + " at " + p.transform.position);
                }
                Wait(3, 3.2f); return;
            }
            if (phase == 3)
            {
                foreach (var p in platforms.Where(p => p.LinkedControls.Contains(bases[basisIndex])))
                    Check(Vector3.Distance(p.transform.position, p.PreviewFirstTarget) < .02f, "powered reaches endpoint without wall conflict: " + p.name);
                MoveRed(new Vector3(30.5f, -30.5f, 0)); Wait(4, 3.5f); return;
            }
            if (phase == 4)
            {
                Check(!bases[basisIndex].IsSignalActive, "base discharges");
                foreach (var p in platforms.Where(p => p.LinkedControls.Contains(bases[basisIndex])))
                    Check(Vector3.Distance(p.transform.position, origins[p]) < .02f, "platform returns to authored pose");
                if (++basisIndex < bases.Length) { MoveRed(bases[basisIndex].transform.position + (basisIndex == 1 ? Vector3.down : Vector3.up)); Wait(2, .5f); return; }
                doors = Object.FindObjectsByType<DoorController>(FindObjectsSortMode.None);
                foreach (var door in doors)
                {
                    using var serialized = new SerializedObject(door);
                    serialized.FindProperty("cameraMoveDuration").floatValue = .01f;
                    serialized.FindProperty("cameraHoldDuration").floatValue = 0;
                    serialized.FindProperty("cameraReturnDuration").floatValue = .01f;
                    serialized.FindProperty("unlockDelay").floatValue = .02f; serialized.ApplyModifiedPropertiesWithoutUndo();
                    foreach (var button in door.Buttons) Check(button.TryPress(player.gameObject), "pressure API presses grouped button");
                }
                Wait(5, 3); return;
            }
            if (phase == 5)
            {
                Check(doors.Length == 6 && doors.All(d => d.State == DoorState.Unlocked), "all six doors unlock from their own buttons");
                Object.FindFirstObjectByType<RoomLifecycleManager>().ResetRegion(MRoom3MapImport.RegionId);
                Check(doors.All(d => d.State == DoorState.Unlocked && d.Buttons.All(b => b.IsPressed)), "latched progress survives region reset");
                foreach (var p in platforms) Check(Vector3.Distance(p.transform.position, origins[p]) < .02f, "region reset restores platform");
                foreach (string color in new[] { "red", "green", "blue" })
                    Check(ColorRuntimeService.Instance.IsUnlocked(color) && !ColorRuntimeService.Instance.IsAbilityUnlocked(color), "scene unlock preserved independently " + color);
                Finish(null);
            }
        }
        catch (Exception error) { Finish(error); }
    }
    private static void Capture(string name, Vector3 position, float size, int width, int height)
    {
        var go = new GameObject("MRoom3 verification camera"); var camera = go.AddComponent<Camera>();
        camera.orthographic = true; camera.orthographicSize = size; camera.transform.position = position;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.075f, .09f, .12f);
        camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        var target = new RenderTexture(width, height, 24); var previous = RenderTexture.active;
        camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
        var image = new Texture2D(width, height, TextureFormat.RGBA32, false);
        image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
        File.WriteAllBytes("Library/MRoom3-" + name + ".png", image.EncodeToPNG());
        RenderTexture.active = previous; camera.targetTexture = null;
        Object.Destroy(image); Object.Destroy(target); Object.Destroy(go);
    }
    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick; EditorApplication.update -= WindowTick; SessionState.EraseBool(Flag);
        if (window != null) window.Close();
        string message = error == null ? $"PASS {checks} MRoom3 real gameplay assertions; real window auto-sync/close/reopen passed before Play" : error.ToString();
        File.WriteAllText("Library/MRoom3SceneAudit.txt", message);
        if (error == null) Debug.Log(message); else Debug.LogError(message);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
