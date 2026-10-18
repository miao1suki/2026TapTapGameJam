using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Project.BlockFeatures;
using Project.ColorBlocks;
using Project.Player;
using Project.InputAbstraction;
using Project.GreenHook;
using TaSource.LinkEffect;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;
using Unity.Profiling;

/// <summary>Read-only preview pool/aim/cursor regression, run only in the silent isolated project.</summary>
public static class InteractionLinkBudgetAudit
{
    private const string Flag = "InteractionLinkBudgetAudit";
    private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;
    private static double deadline;
    private static int checks;
    private static int warmDrawAllocationEvents;
    public static void Run()
    {
        if (!Application.dataPath.Contains("/Library/FlowRetirementProbe/Assets") ||
            !Environment.GetCommandLineArgs().Contains("-noaudio")) throw new Exception("Silent isolated project only");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        SessionState.SetBool(Flag, true); Resume(); EditorApplication.EnterPlaymode();
    }
    [InitializeOnLoadMethod] private static void Resume()
    {
        if (!SessionState.GetBool(Flag, false)) return;
        deadline = EditorApplication.timeSinceStartup + 60;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
    }
    private static void Check(bool success, string message)
    {
        if (!success) throw new Exception(message);
        checks++;
    }
    private static object Get(object instance, string name) => instance.GetType().GetField(name, Hidden).GetValue(instance);
    private static void Set(object instance, string name, object value) => instance.GetType().GetField(name, Hidden).SetValue(instance, value);
    private static void Call(object instance, string name) => instance.GetType().GetMethod(name, Hidden).Invoke(instance, null);
    private static GameObject Spawn(string path, Vector3 pos) => Object.Instantiate(
        AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Content/" + path + ".prefab"), pos, Quaternion.identity);
    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup > deadline) { Finish(new Exception("Timeout")); return; }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.time < .2f) return;
        EditorApplication.update -= Tick;
        try
        {
            checks = 0;
            ColorRuntimeService.Instance.SetAllUnlockedForCurrentSession(true);
            ColorRuntimeService.Instance.UnlockAllAbilitiesForCurrentSession();
            Vector3 origin = new Vector3(0, 40, 0);
            var player = Spawn("Player/Player", origin).GetComponent<PlayerController>();
            player.GetComponent<PlayerInputDriver>().enabled = false;
            player.Motor.constraints = RigidbodyConstraints.FreezeAll;
            var wheel = player.GetComponent<PlayerColorWheel>();
            var effect = player.GetComponent<InteractionLinkEffect>();
            Check(effect.PooledLineCount == 1, "one renderer prewarmed even for legacy prefab pool capacity");
            var waters = Enumerable.Range(1, 4).Select(i => Spawn("ColorBlocks/Prefabs/Blue_WaterSource", origin + Vector3.right * i)).ToArray();
            Check(wheel.TrySelectColor("red"), "select red");
            Physics.SyncTransforms(); Call(effect, "LateUpdate");
            Check(effect.DetectedTargetCount == 4 && effect.VisibleLinkCount == 1, "four waters detected but only nearest one shown");
            var candidates = (IList)Get(effect, "candidates");
            Check(((Component)candidates[0].GetType().GetField("Component").GetValue(candidates[0])).gameObject == waters[0], "nearest water uses actual collider distance");
            for (int i = 0; i < 30; i++)
            {
                player.transform.position = origin + Vector3.right * (i % 4 + 1);
                player.Motor.position = player.transform.position;
                Physics.SyncTransforms(); Call(effect, "ScanTargets"); Call(effect, "DrawLines");
                Check(effect.VisibleLinkCount == 1, "water movement keeps just one nearest preview");
            }
            foreach (var water in waters) Object.DestroyImmediate(water);
            player.transform.position = origin; player.Motor.position = origin;
            var vines = Enumerable.Range(0, 36).Select(i => Spawn("ColorBlocks/Prefabs/Green_Ladder",
                origin + new Vector3(Mathf.Cos(i * Mathf.PI / 18) * 3, Mathf.Sin(i * Mathf.PI / 18) * 3, 0))).ToArray();
            Physics.SyncTransforms(); Call(effect, "ScanTargets"); Call(effect, "DrawLines");
            Check(effect.DetectedTargetCount == 36 && effect.VisibleLinkCount == 1, "dense red draws nearest target only");
            Check(wheel.TrySelectColor("blue"), "select blue");
            Call(effect, "LateUpdate");
            Check(effect.DetectedTargetCount == 36 && effect.VisibleLinkCount == 1, "blue draws nearest target only");
            var originalLines = ((IList)Get(effect, "lines")).Cast<LineRenderer>().Select(l => l.GetInstanceID()).ToArray();
            var draw = (Action)Delegate.CreateDelegate(typeof(Action), effect, effect.GetType().GetMethod("DrawLines", Hidden));
            for (int i = 0; i < 60; i++)
            {
                player.transform.position = origin + new Vector3(Mathf.Cos(i * .2f), Mathf.Sin(i * .2f), 0);
                player.Motor.position = player.transform.position;
                Physics.SyncTransforms(); Call(effect, "ScanTargets"); Call(effect, "DrawLines");
                Check(effect.VisibleLinkCount == 1 && effect.PooledLineCount == 1, "switching nearest reuses one line without overlapping fade");
            }
            var lines = (IList)Get(effect, "lines");
            Check(originalLines.SequenceEqual(lines.Cast<LineRenderer>().Select(l => l.GetInstanceID())), "steady movement reuses renderer identities");
            Check(vines.All(v => v.GetComponent<ClimbableVineFeature>().CurrentGrowthBlocks == 0), "preview never applies blue growth");
            var stateReferences = ((IDictionary)Get(effect, "visualStates")).Values.Cast<object>().ToHashSet();
            foreach (var state in (IEnumerable)Get(effect, "statePool")) stateReferences.Add(state);
            for (int i = 0; i < 30; i++)
            {
                effect.enabled = false; Check(lines.Cast<LineRenderer>().All(l => !l.enabled), "disable returns active renderers without destruction");
                effect.enabled = true; Call(effect, "LateUpdate");
                Check(originalLines.SequenceEqual(lines.Cast<LineRenderer>().Select(l => l.GetInstanceID())), "reenable uses original renderer pool");
                Check(((IDictionary)Get(effect, "visualStates")).Values.Cast<object>().All(stateReferences.Contains), "visual state objects reused across disable/enable");
            }
            for (int i = 0; i < 10; i++) draw();
            using var allocation = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc", 32768,
                ProfilerRecorderOptions.CollectOnlyOnCurrentThread | ProfilerRecorderOptions.WrapAroundWhenCapacityReached);
            for (int i = 0; i < 300; i++) draw();
            allocation.Stop();
            warmDrawAllocationEvents = allocation.Count;
            Check(allocation.Valid && allocation.Count <= 8, "warm 300 nearest draws do not allocate per draw; events=" + allocation.Count);
            Check(wheel.TrySelectColor("green"), "select green"); Call(effect, "LateUpdate");
            Check(effect.VisibleLinkCount == 0, "green never gets duplicate red-blue preview rays");
            var camera = new GameObject("AimCamera").AddComponent<Camera>(); camera.tag = "MainCamera";
            camera.orthographic = true; camera.orthographicSize = 7; camera.transform.position = origin + Vector3.back * 10;
            var hook = player.GetComponent<GreenHookAbility>();
            Set(hook, "idleFollowSeconds", 0f); // Geometry test; eased following has its own aim audit.
            player.transform.position = origin; player.Motor.position = origin;
            var firstPoint = origin + new Vector3(1.5f, .8f, 0);
            hook.Tick(camera.WorldToScreenPoint(firstPoint), true);
            Call(hook, "RenderFrame");
            var greenLine = (LineRenderer)Get(hook, "line");
            Check(Vector2.Distance(greenLine.GetPosition(greenLine.positionCount - 1), firstPoint) < .002f, "green tip tracks near pointer world position");
            var secondPoint = origin + new Vector3(3, 1.6f, 0);
            hook.Tick(camera.WorldToScreenPoint(secondPoint), true);
            Call(hook, "RenderFrame");
            Check(Vector2.Distance(greenLine.GetPosition(greenLine.positionCount - 1), secondPoint) < .002f, "same direction different distance changes green endpoint");
            hook.Tick(camera.WorldToScreenPoint(origin + Vector3.right * 50), true);
            Call(hook, "RenderFrame");
            Check(Vector2.Distance(greenLine.GetPosition(greenLine.positionCount - 1), origin) <= hook.LaunchRangeWorld + .002f, "idle preview clips at launch range including configured slack");
            Check(Vector2.Distance(greenLine.GetPosition(0), effect.SourceWorldPosition) < .002f, "green shares red-blue player origin");
            Check(DesktopCursorPolicy.ResolveLockMode(true, true) == CursorLockMode.Confined, "focused desktop confines without recentering");
            Check(DesktopCursorPolicy.ResolveLockMode(true, false) == CursorLockMode.None, "desktop focus loss releases pointer");
            Check(DesktopCursorPolicy.ResolveLockMode(false, true) == CursorLockMode.None, "editor/mobile stay unlocked");
            Object.DestroyImmediate(camera.gameObject);
            Object.DestroyImmediate(player.gameObject); foreach (var vine in vines) Object.DestroyImmediate(vine);
            Finish(null);
        }
        catch (Exception error) { Finish(error); }
    }
    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick; SessionState.EraseBool(Flag);
        string result = error == null ? $"PASS {checks} pool/aim/cursor assertions\n300 warm nearest-target draws: GC.Alloc events={warmDrawAllocationEvents}" : error.ToString();
        File.WriteAllText("Library/InteractionLinkBudget.txt", result);
        if (error == null) Debug.Log(result); else Debug.LogError(result);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
