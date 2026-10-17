using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using PlanningEditorPrototype;
using Project.BlockFeatures;
using Project.ColorBlocks;
using Project.LevelEditor;
using Project.LevelEditor.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Route geometry, serialized planning preview and real motion regression.</summary>
public static class MovingPlatformRouteAudit
{
    private const string Flag = "MovingPlatformRouteAudit.Running";
    private static int checks;
    private static double started;
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        checks++;
    }
    private static void Near(Vector3 actual, Vector3 expected, string message) =>
        Check(Vector3.Distance(actual, expected) < .002f, message + ": " + actual + " / " + expected);
    private static void Call(object target, string method, params object[] values) =>
        target.GetType().GetMethod(method, Hidden).Invoke(target, values);
    public static void Run()
    {
        if (!Application.dataPath.Replace('\\', '/').Contains("/Library/FlowRetirementProbe/Assets"))
            throw new InvalidOperationException("Isolated validation project only");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        SessionState.SetBool(Flag, true);
        Resume(); EditorApplication.EnterPlaymode();
    }
    [InitializeOnLoadMethod]
    private static void Resume()
    {
        if (!SessionState.GetBool(Flag, false)) return;
        started = EditorApplication.timeSinceStartup;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
    }
    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup - started > 70) { Finish(false, "Timed out"); return; }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.time < .2f) return;
        EditorApplication.update -= Tick;
        try
        {
            checks = 0;
            ColorRuntimeService.Instance.Unlock("red");
            for (int mode = 0; mode <= 6; mode++)
                for (int negative = 0; negative < 2; negative++)
                    Verify((MovingPlatformPathMode)mode, negative == 1);
            VerifyCenterLimits(); VerifySignalAndCrank(); VerifyPlanningPersistence(); VerifyGridAndBlocking();
            VerifyPoweredEndpoint();
            Finish(true, "PASS " + checks + " platform route assertions");
        }
        catch (Exception error) { Finish(false, error.ToString()); }
    }
    private static MovingPlatformFeature Spawn(Vector3 position)
    {
        var item = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/Content/ColorBlocks/Prefabs/Red_Platform.prefab"), position, Quaternion.identity);
        return item.GetComponent<MovingPlatformFeature>();
    }
    private static void Configure(MovingPlatformFeature platform, MovingPlatformPathMode mode, bool negative)
    {
        using var serialized = new SerializedObject(platform);
        serialized.FindProperty("pathMode").intValue = (int)mode;
        serialized.FindProperty("route").intValue = 0;
        serialized.FindProperty("travelBlocks").intValue = 5;
        serialized.FindProperty("reverseRoute").boolValue = negative;
        serialized.FindProperty("leftDistance").floatValue = 3;
        serialized.FindProperty("rightDistance").floatValue = 5;
        serialized.FindProperty("downDistance").floatValue = 2;
        serialized.FindProperty("upDistance").floatValue = 4;
        serialized.FindProperty("centerOffset").floatValue = 1;
        serialized.FindProperty("startNegative").boolValue = negative;
        serialized.FindProperty("driveMode").intValue = 0;
        serialized.FindProperty("reverseOnBlock").boolValue = false;
        serialized.FindProperty("travelDuration").floatValue = 1;
        serialized.FindProperty("holdAtEnds").floatValue = 0;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        platform.ResetForRoom();
    }
    private static void Step(MovingPlatformFeature platform, int count)
    {
        for (int i = 0; i < count; i++) Call(platform, "FixedUpdate");
    }
    private static void Verify(MovingPlatformPathMode mode, bool negative)
    {
        Vector3 origin = new Vector3(100 + (int)mode * 30, 200, 0);
        var platform = Spawn(origin); Configure(platform, mode, negative);
        Check(platform.IsDriveAvailable, "unlocked platform attached");
        var path = platform.Path;
        float min = mode == MovingPlatformPathMode.Legacy ? negative ? -5 : 0
            : mode == MovingPlatformPathMode.Left ? -3 : mode == MovingPlatformPathMode.Down ? -2
            : mode == MovingPlatformPathMode.HorizontalRange ? -2 : mode == MovingPlatformPathMode.VerticalRange ? -1 : 0;
        float max = mode == MovingPlatformPathMode.Legacy ? negative ? 0 : 5
            : mode == MovingPlatformPathMode.Right ? 5 : mode == MovingPlatformPathMode.Up ? 4
            : mode == MovingPlatformPathMode.HorizontalRange ? 6 : mode == MovingPlatformPathMode.VerticalRange ? 5 : 0;
        Check(path.Minimum == min && path.Maximum == max, "independent limits " + mode);
        Near(platform.transform.position, origin, "configuration never teleports author position");
        float visitedMin = 0, visitedMax = 0;
        for (int i = 0; i < 160; i++)
        {
            Step(platform, 1);
            Vector3 delta = platform.transform.position - origin;
            float distance = Vector3.Dot(delta, path.Axis);
            Check(distance >= min - .002f && distance <= max + .002f, "no endpoint overshoot " + mode);
            Near(delta, (Vector3)path.Axis * distance, "straight line without transverse drift");
            visitedMin = Mathf.Min(visitedMin, distance); visitedMax = Mathf.Max(visitedMax, distance);
        }
        Check(Mathf.Abs(visitedMin - min) < .002f && Mathf.Abs(visitedMax - max) < .002f, "visits both route limits " + mode);
        platform.ResetForRoom(); Near(platform.transform.position, origin, "room reset restores author position");
        Step(platform, 1);
        Check(Vector3.Dot(platform.transform.position - origin, path.Axis) * path.FirstEnd > 0f, "reset preserves initial direction");
        Object.DestroyImmediate(platform.gameObject);
    }
    private static void VerifyCenterLimits()
    {
        foreach (var mode in new[] { MovingPlatformPathMode.HorizontalRange, MovingPlatformPathMode.VerticalRange })
            foreach (float center in new[] { -999f, -2.5f, 0, 1.5f, 999f })
            {
                var p = MovingPlatformPath.Resolve(mode, 0, 4, false, 3, 5, 2, 4, center, false);
                Check(p.Minimum <= 0 && p.Maximum >= 0, "center keeps initial platform on segment");
                Check(Mathf.Abs(p.Span - (mode == MovingPlatformPathMode.HorizontalRange ? 8 : 6)) < .001f, "center does not stretch route");
            }
        var zero = MovingPlatformPath.Resolve(MovingPlatformPathMode.HorizontalRange, 0, 4, false, 0, 0, 0, 0, 9, true);
        Check(zero.Span == 0 && zero.FirstEnd == 0, "zero length is stationary");
    }
    private static void VerifySignalAndCrank()
    {
        var p = Spawn(new Vector3(400, 200, 0));
        Configure(p, MovingPlatformPathMode.HorizontalRange, true);
        var source = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/Content/ColorBlocks/Prefabs/Red_Crank.prefab"), new Vector3(440, 200, 0), Quaternion.identity)
            .GetComponent<CrankPlatformFeature>();
        using (var s = new SerializedObject(p))
        {
            s.FindProperty("driveMode").intValue = 1;
            s.FindProperty("linkedControls").arraySize = 1;
            s.FindProperty("linkedControls").GetArrayElementAtIndex(0).objectReferenceValue = source;
            s.ApplyModifiedPropertiesWithoutUndo();
        }
        p.ResetForRoom(); Vector3 origin = p.transform.position;
        Step(p, 5); Near(p.transform.position, origin, "inactive signal stays at placement");
        source.SetSignal(true, null); Step(p, 8);
        Check(p.transform.position.x < origin.x, "range starts negative under signal");
        source.SetSignal(false, null); Vector3 paused = p.transform.position;
        Step(p, 8); Near(p.transform.position, paused, "loss of signal pauses in place");
        source.SetSignal(true, null); Step(p, 4);
        Check(p.transform.position.x < paused.x, "signal resumes without jump");
        p.BindCrank(source); p.ResetForRoom();
        source.SetSignal(false, null); p.SnapToCrankProgress(); Near(p.transform.position, origin, "crank zero keeps placement");
        source.SetSignal(true, null); p.SnapToCrankProgress();
        Near(p.transform.position, origin + Vector3.left * 2, "crank full uses configured first endpoint");
        source.SetSignal(false, null); p.SnapToCrankProgress(); Near(p.transform.position, origin, "crank return keeps author position");
        p.BindCrank(null); Object.DestroyImmediate(source.gameObject); Object.DestroyImmediate(p.gameObject);
    }
    private static void VerifyPlanningPersistence()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Content/ColorBlocks/Prefabs/Red_Platform.prefab").GetComponent<MovingPlatformFeature>();
        List<LevelEditorComponentValueOverride> Values(params (string, string, string)[] items) => items.Select(item =>
        {
            var v = new LevelEditorComponentValueOverride();
            v.Configure("", typeof(MovingPlatformFeature).FullName, 0, item.Item1, item.Item2, item.Item3, null); return v;
        }).ToList();
        var values = Values(("route", "Enum", "0"), ("travelBlocks", "Integer", "35"), ("reverseRoute", "Boolean", "1"));
        var old = PlanningPlatformPathUtility.Resolve(prefab, values);
        Near(old.PreviewTo, Vector3.left * 35, "legacy reverse override points left in planning");
        values.AddRange(Values(("pathMode", "Enum", "6"), ("downDistance", "Float", "2.5"),
            ("upDistance", "Float", "6.5"), ("centerOffset", "Float", "1.5"), ("startNegative", "Boolean", "1")));
        var box = new PlanningBox { hasComponentOverrides = true, componentOverrides = values };
        var loaded = PlanningBox.FromPersistentJson(JsonUtility.ToJson(box.PersistentClone()));
        var preview = PlanningPlatformPathUtility.Resolve(prefab, loaded.componentOverrides);
        Near(preview.PreviewFrom, Vector3.down, "saved range minimum preview");
        Near(preview.PreviewTo, Vector3.up * 8, "saved range maximum preview");
        var p = Spawn(new Vector3(500, 200, 0));
        LevelEditorComponentOverrideUtility.ApplyOverrides(p.gameObject, loaded.componentOverrides, false);
        Check(p.Path.Minimum == preview.Minimum && p.Path.Maximum == preview.Maximum && p.Path.StartsNegative,
            "runtime and serialized preview use identical path");
        foreach (string field in new[] { "pathMode", "leftDistance", "rightDistance", "upDistance", "downDistance", "centerOffset", "startNegative" })
            Check(typeof(MovingPlatformFeature).GetField(field, Hidden).GetCustomAttribute<BlockParameterAttribute>() != null,
                "Chinese parameter metadata " + field);
        Check(typeof(MovingPlatformFeature).GetField("upDistance", Hidden).GetCustomAttribute<BlockParameterAttribute>().MatchesVisibility(6), "range parameter conditional visibility");
        Check(!typeof(MovingPlatformFeature).GetField("upDistance", Hidden).GetCustomAttribute<BlockParameterAttribute>().MatchesVisibility(1), "irrelevant direction hidden");
        var a = new Vector2(-1000000, 200); var b = new Vector2(1000000, 200);
        Check(PlanningPlatformPathUtility.ClipLine(new Rect(0, 0, 800, 600), ref a, ref b), "long crossing route remains visible");
        Near(a, new Vector3(0, 200), "long route clipped at viewport entry");
        Near(b, new Vector3(800, 200), "long route clipped at viewport exit");
        a = new Vector2(-100, -200); b = new Vector2(1000, -200);
        Check(!PlanningPlatformPathUtility.ClipLine(new Rect(0, 0, 800, 600), ref a, ref b), "offscreen route performs no dash loop");
        Object.DestroyImmediate(p.gameObject);
    }
    private static void VerifyPoweredEndpoint()
    {
        var source = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/Content/ColorBlocks/Prefabs/Red_Crank.prefab"), new Vector3(900, 200), Quaternion.identity)
            .GetComponent<CrankPlatformFeature>();
        foreach (var mode in new[] { MovingPlatformPathMode.Right, MovingPlatformPathMode.Left,
            MovingPlatformPathMode.Up, MovingPlatformPathMode.Down, MovingPlatformPathMode.HorizontalRange, MovingPlatformPathMode.VerticalRange })
        {
            Vector3 origin = new Vector3(800, 200, 0);
            var p = Spawn(origin); Configure(p, mode, true);
            var value = new LevelEditorComponentValueOverride();
            value.Configure("", typeof(MovingPlatformFeature).FullName, 0, "signalMotion", "Enum", "1", null);
            var loaded = PlanningBox.FromPersistentJson(JsonUtility.ToJson(new PlanningBox
                { hasComponentOverrides = true, componentOverrides = new List<LevelEditorComponentValueOverride> { value } }.PersistentClone()));
            LevelEditorComponentOverrideUtility.ApplyOverrides(p.gameObject, loaded.componentOverrides, false);
            Check(p.SignalMotion == MovingPlatformSignalMotion.PoweredEndpoint, "signal mode survives planning serialization");
            using (var s = new SerializedObject(p))
            {
                s.FindProperty("driveMode").intValue = 1;
                s.FindProperty("linkedControls").arraySize = 1;
                s.FindProperty("linkedControls").GetArrayElementAtIndex(0).objectReferenceValue = source;
                s.FindProperty("holdAtEnds").floatValue = 10;
                s.ApplyModifiedPropertiesWithoutUndo();
            }
            source.SetSignal(false, null); p.ResetForRoom(); Step(p, 70);
            Near(p.transform.position, origin, "unpowered endpoint mode stays at author position");
            source.SetSignal(true, null); Step(p, 150);
            Vector3 end = origin + (Vector3)p.Path.Axis * p.Path.FirstEnd;
            Near(p.transform.position, end, "powered reaches configured first endpoint: " + mode);
            Check(!p.IsMoving, "endpoint stops without ping-pong");
            Step(p, 100); Near(p.transform.position, end, "holds endpoint indefinitely");
            source.SetSignal(false, null); Step(p, 5);
            Check(p.IsMoving && Vector3.Distance(p.transform.position, end) > .001f, "power loss starts return immediately despite endpoint hold");
            Step(p, 150); Near(p.transform.position, origin, "unpowered returns to author, not opposite range endpoint");
            source.SetSignal(true, null); Step(p, 8); Vector3 mid = p.transform.position;
            source.SetSignal(false, null); Step(p, 1);
            Check(Vector3.Distance(p.transform.position, origin) < Vector3.Distance(mid, origin), "midflight reversal responds without snap");
            Check(Vector3.Distance(p.transform.position, mid) < .5f, "reversal is incremental");
            source.SetSignal(true, null); Step(p, 150); Near(p.transform.position, end, "recharging resumes toward endpoint");
            p.ResetForRoom(); Near(p.transform.position, origin, "room reset restores authored pose while retaining mode and binding");
            Check(p.LinkedControls.Count == 1 && p.SignalMotion == MovingPlatformSignalMotion.PoweredEndpoint, "reset preserves configuration");
            p.BindCrank(source); source.SetSignal(false, null); p.SnapToCrankProgress();
            Near(p.transform.position, origin, "signal mode never overrides crank zero");
            source.SetSignal(true, null); p.SnapToCrankProgress(); Near(p.transform.position, end, "crank progress remains authoritative");
            Object.DestroyImmediate(p.gameObject);
        }
        var blocked = Spawn(new Vector3(800, 200)); Configure(blocked, MovingPlatformPathMode.Right, false);
        using (var s = new SerializedObject(blocked))
        {
            s.FindProperty("driveMode").intValue = 1; s.FindProperty("signalMotion").intValue = 1;
            s.FindProperty("linkedControls").arraySize = 1;
            s.FindProperty("linkedControls").GetArrayElementAtIndex(0).objectReferenceValue = source;
            s.FindProperty("reverseOnBlock").boolValue = true; s.ApplyModifiedPropertiesWithoutUndo();
        }
        blocked.ResetForRoom(); source.SetSignal(true, null);
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.transform.position = blocked.transform.position + Vector3.right * 1.6f;
        for (int i = 0; i < 100; i++) { Physics.SyncTransforms(); Step(blocked, 1); }
        Check(blocked.transform.position.x > 800 && blocked.transform.position.x < 800.7f, "obstructed endpoint mode waits before solid, never reverses");
        Vector3 paused = blocked.transform.position; Step(blocked, 10); Near(blocked.transform.position, paused, "blocked progress does not accumulate");
        Object.DestroyImmediate(wall); Physics.SyncTransforms(); Step(blocked, 150);
        Near(blocked.transform.position, new Vector3(805, 200), "cleared obstacle resumes to endpoint");
        Object.DestroyImmediate(blocked.gameObject); Object.DestroyImmediate(source.gameObject);
        Check(typeof(MovingPlatformFeature).GetField("signalMotion", Hidden).GetCustomAttribute<BlockParameterAttribute>().MatchesVisibility(1),
            "signal settings have Chinese conditional inspector metadata");
    }
    private static void VerifyGridAndBlocking()
    {
        Vector3 origin = new Vector3(600, 200, 0);
        var platform = Spawn(origin); Configure(platform, MovingPlatformPathMode.Up, false);
        platform.gameObject.SetActive(false);
        var placed = platform.GetComponent<LevelEditorPlacedBlock>() ?? platform.gameObject.AddComponent<LevelEditorPlacedBlock>();
        placed.SetCellWorldSize(1.75f);
        platform.transform.localScale = new Vector3(1, 5, 1);
        platform.gameObject.SetActive(true);
        Near(platform.PreviewEnd, origin + Vector3.up * 7, "merged visual scale does not multiply grid route");
        Step(platform, 50); Near(platform.transform.position, origin + Vector3.up * 7, "non-unit grid route reaches correct endpoint");
        platform.ResetForRoom(); Near(platform.transform.position, origin, "grid reset restores authored origin");
        Object.DestroyImmediate(platform.gameObject);
        platform = Spawn(origin); Configure(platform, MovingPlatformPathMode.Right, false);
        using (var s = new SerializedObject(platform)) { s.FindProperty("reverseOnBlock").boolValue = true; s.ApplyModifiedPropertiesWithoutUndo(); }
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.transform.position = origin + Vector3.right * 1.6f;
        float farthest = 0;
        for (int i = 0; i < 100; i++)
        {
            Physics.SyncTransforms(); Step(platform, 1);
            farthest = Mathf.Max(farthest, platform.transform.position.x - origin.x);
            Check(platform.transform.position.x >= origin.x - .002f && platform.transform.position.x < origin.x + .7f,
                "obstacle reversal stays inside route and outside wall");
        }
        Check(farthest > .1f, "blocked platform moved before obstacle reversal");
        Object.DestroyImmediate(wall); Object.DestroyImmediate(platform.gameObject);
        platform = Spawn(origin); Configure(platform, MovingPlatformPathMode.HorizontalRange, false);
        using (var s = new SerializedObject(platform))
        {
            s.FindProperty("leftDistance").floatValue = 0; s.FindProperty("rightDistance").floatValue = 0;
            s.ApplyModifiedPropertiesWithoutUndo();
        }
        platform.ResetForRoom(); Step(platform, 15); Near(platform.transform.position, origin, "zero range has no motion or division error");
        Object.DestroyImmediate(platform.gameObject);
    }
    private static void Finish(bool success, string message)
    {
        SessionState.SetBool(Flag, false); EditorApplication.update -= Tick;
        File.WriteAllText("Library/MovingPlatformRouteResult.txt", message);
        Debug.Log(message); EditorApplication.Exit(success ? 0 : 1);
    }
}
