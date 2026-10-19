using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Project.BlockFeatures;
using Project.ColorBlocks;
using Project.ContactCubes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class PlatformLoadTransportAudit
{
    private const string Flag = "PlatformLoadTransportAudit.Running";
    private static readonly List<GameObject> objects = new();
    private static int checks;
    private static double started;
    private static MovingPlatformFeature naturalPlatform;
    private static Rigidbody naturalRed, naturalBase;
    private static Vector3 naturalOrigin, naturalRedOffset, naturalBaseOffset;
    private static float naturalStarted, naturalFarthest;
    private static bool naturalBound;
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    public static void Run()
    {
        if (!Application.dataPath.Replace('\\', '/').Contains("/Library/FlowRetirementProbe/Assets") ||
            Array.IndexOf(Environment.GetCommandLineArgs(), "-noaudio") < 0) throw new Exception("Silent isolated probe only");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        SessionState.SetBool(Flag, true); Resume(); EditorApplication.EnterPlaymode();
    }
    [InitializeOnLoadMethod] private static void Resume()
    {
        if (!SessionState.GetBool(Flag, false)) return;
        started = EditorApplication.timeSinceStartup;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
    }
    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup - started > 90) { Finish(false, "Timeout"); return; }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.time < .5f) return;
        EditorApplication.update -= Tick;
        SimulationMode previous = Physics.simulationMode;
        bool passed = false;
        try
        {
            checks = 0; AudioListener.pause = true; Physics.simulationMode = SimulationMode.Script;
            ColorRuntimeService.Instance.SetAllUnlockedForCurrentSession(true);
            foreach (bool foundation in new[] { false, true })
            foreach (bool gravity in new[] { false, true })
            foreach (MovingPlatformPathMode direction in new[] { MovingPlatformPathMode.Right, MovingPlatformPathMode.Up, MovingPlatformPathMode.Down })
            { Verify(foundation, gravity, direction); Cleanup(); }
            VerifyBlocked(); Cleanup(); VerifyStack(); Cleanup(); VerifySelfPoweredLoad(); Cleanup();
            passed = true;
        }
        catch (Exception error) { Debug.LogException(error); Finish(false, error.ToString()); }
        finally { Cleanup(); Physics.simulationMode = previous; }
        if (passed)
        {
            try { StartNatural(); }
            catch (Exception error) { Cleanup(); Finish(false, error.ToString()); }
        }
    }
    private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    private static void Call(object target, string method) => target.GetType().GetMethod(method, Hidden).Invoke(target, null);
    private static GameObject Spawn(string path, Vector3 position)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (source == null) throw new Exception("Missing " + path);
        var result = Object.Instantiate(source, position, Quaternion.identity); objects.Add(result); return result;
    }
    private static MovingPlatformFeature Platform(MovingPlatformPathMode mode)
    {
        var result = Spawn("Assets/_Project/Content/ColorBlocks/Prefabs/Red_Platform.prefab", new Vector3(200, 200, 0)).GetComponent<MovingPlatformFeature>();
        using var s = new SerializedObject(result);
        s.FindProperty("driveMode").intValue = 0;
        s.FindProperty("pathMode").intValue = (int)mode;
        foreach (string distance in new[] { "rightDistance", "upDistance", "downDistance" }) s.FindProperty(distance).floatValue = 3;
        s.FindProperty("travelDuration").floatValue = 2;
        s.FindProperty("holdAtEnds").floatValue = 0;
        s.FindProperty("reverseOnBlock").boolValue = true;
        s.ApplyModifiedPropertiesWithoutUndo(); result.ResetForRoom(); return result;
    }
    private static Rigidbody Load(MovingPlatformFeature platform, bool foundation, bool gravity, float height = 0)
    {
        var result = Spawn(foundation ? "Assets/_Project/Content/ColorBlocks/Prefabs/Red_Foundation.prefab" :
            "Assets/_Project/Content/ContactCubes/Red_ContactCube.prefab", platform.transform.position);
        Rigidbody body = result.GetComponent<Rigidbody>();
        Check(body != null, "load owns body");
        body.interpolation = RigidbodyInterpolation.None;
        if (foundation)
        {
            using var s = new SerializedObject(result.GetComponent<MechanismBaseFeature>());
            s.FindProperty("useGravity").boolValue = gravity; s.ApplyModifiedPropertiesWithoutUndo();
        }
        else result.GetComponent<ContactCubeMotion>().ConfigureGravity(gravity);
        Physics.SyncTransforms();
        Bounds b = result.GetComponentInChildren<Collider>().bounds;
        float top = platform.GetComponent<Collider>().bounds.max.y;
        Teleport(body, body.position + Vector3.up * (top - b.min.y + .002f + height));
        Physics.SyncTransforms(); return body;
    }
    private static void Teleport(Rigidbody body, Vector3 position)
    { body.position = position; body.transform.position = position; }
    private static void Step(MovingPlatformFeature platform)
    {
        Physics.SyncTransforms(); Call(platform, "FixedUpdate");
        foreach (GameObject item in objects)
        {
            if (item == null) continue;
            var cube = item.GetComponent<ContactCubeMotion>(); if (cube != null && cube.enabled) Call(cube, "FixedUpdate");
            var basis = item.GetComponent<MechanismBaseFeature>(); if (basis != null) { Call(basis, "FixedUpdate"); Call(basis, "ScanForEnergy"); }
        }
        Physics.Simulate(Time.fixedDeltaTime); Physics.SyncTransforms();
    }
    private static void Verify(bool foundation, bool gravity, MovingPlatformPathMode direction)
    {
        var platform = Platform(direction); var body = Load(platform, foundation, gravity);
        Vector3 origin = platform.transform.position, relative = body.position - origin;
        float farthest = 0f;
        for (int i = 0; i < 220; i++)
        {
            Step(platform);
            farthest = Mathf.Max(farthest, Vector3.Distance(platform.transform.position, origin));
            Check(Vector3.Distance(body.position - platform.transform.position, relative) < .08f,
                "load follows platform " + foundation + "/" + gravity + "/" + direction + " step=" + i + " rel=" + (body.position - platform.transform.position));
        }
        Check(farthest > 2.9f, "platform reached route endpoint with load " + foundation + "/" + gravity + "/" + direction + " max=" + farthest + " active=" + platform.IsDriveAvailable);
        Check(body.GetComponent<PlatformMount>() == null, "no implicit authored mount added");
        platform.ResetForRoom(); Check(Vector3.Distance(platform.transform.position, origin) < .001f, "platform reset author origin");
    }
    private static void VerifyBlocked()
    {
        var platform = Platform(MovingPlatformPathMode.Up); var body = Load(platform, false, true);
        var ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube); objects.Add(ceiling);
        ceiling.transform.position = body.position + Vector3.up * 1.1f;
        ceiling.transform.localScale = new Vector3(5, 1, 3);
        Physics.SyncTransforms();
        float ceilingBottom = ceiling.GetComponent<Collider>().bounds.min.y;
        for (int i = 0; i < 50; i++)
        {
            Step(platform);
            Check(body.GetComponentInChildren<Collider>().bounds.max.y <= ceilingBottom + .025f, "load never crushed into ceiling");
        }
    }
    private static void VerifyStack()
    {
        var platform = Platform(MovingPlatformPathMode.Right);
        var bottom = Load(platform, false, true);
        var top = Load(platform, true, true, bottom.GetComponentInChildren<Collider>().bounds.size.y);
        Vector3 a = bottom.position - platform.transform.position, b = top.position - platform.transform.position;
        for (int i = 0; i < 100; i++)
        {
            Step(platform);
            Check(Vector3.Distance(bottom.position - platform.transform.position, a) < .08f, "stack bottom follows");
            Check(Vector3.Distance(top.position - platform.transform.position, b) < .08f, "stack top follows");
        }
    }
    private static void VerifySelfPoweredLoad()
    {
        var platform = Platform(MovingPlatformPathMode.Right);
        platform.GetComponent<BoxCollider>().size = new Vector3(3, 1, 1);
        var red = Load(platform, false, true);
        var basis = Load(platform, true, true);
        Teleport(red, red.position + Vector3.left * .5f + Vector3.up * .2f);
        Teleport(basis, basis.position + Vector3.right * .5f + Vector3.up * .2f);
        Physics.SyncTransforms();
        var mechanism = basis.GetComponent<MechanismBaseFeature>();
        // These are ordinary dropped bodies, with no PlatformMount metadata.
        for (int i = 0; i < 65; i++)
        {
            Call(red.GetComponent<ContactCubeMotion>(), "FixedUpdate"); Call(mechanism, "FixedUpdate");
            Physics.Simulate(Time.fixedDeltaTime); Physics.SyncTransforms();
        }
        platform.BindBase(mechanism);
        using (var s = new SerializedObject(platform))
        { s.FindProperty("signalMotion").intValue = (int)MovingPlatformSignalMotion.PoweredEndpoint; s.ApplyModifiedPropertiesWithoutUndo(); }
        platform.ResetForRoom(); Call(mechanism, "ScanForEnergy");
        Check(mechanism.IsSignalActive, "real red cube powers loaded base before motion");
        Vector3 origin = platform.transform.position, a = red.position - origin, b = basis.position - origin;
        for (int i = 0; i < 140; i++)
        {
            Step(platform);
            Check(mechanism.IsSignalActive, "loaded base stays powered through endpoint motion " + i);
            Check(Vector3.Distance(red.position - platform.transform.position, a) < .08f, "self-powered red load follows");
            Check(Vector3.Distance(basis.position - platform.transform.position, b) < .08f, "self-powered base follows");
        }
        Check(platform.transform.position.x > origin.x + 2.9f, "actual base-driven platform reaches endpoint with both loads");
    }
    private static void StartNatural()
    {
        var importer = AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/_Project/Code/Systems/BlockFeatures/Runtime/Features/MovingPlatformFeature.cs");
        Check(MonoImporter.GetExecutionOrder(importer) == -150, "real platform importer executes before cube physics");
        naturalPlatform = Platform(MovingPlatformPathMode.Right);
        naturalPlatform.GetComponent<BoxCollider>().size = new Vector3(3, 1, 1);
        // Empty real signal set keeps the platform still while both thrown loads settle.
        naturalPlatform.BindSignalSources(Array.Empty<MonoBehaviour>());
        naturalRed = Load(naturalPlatform, false, true);
        naturalBase = Load(naturalPlatform, true, true);
        Teleport(naturalRed, naturalRed.position + Vector3.left * .5f + Vector3.up * .2f);
        Teleport(naturalBase, naturalBase.position + Vector3.right * .5f + Vector3.up * .2f);
        using (var s = new SerializedObject(naturalPlatform))
        { s.FindProperty("signalMotion").intValue = (int)MovingPlatformSignalMotion.PoweredEndpoint; s.ApplyModifiedPropertiesWithoutUndo(); }
        Physics.SyncTransforms(); naturalStarted = Time.time; naturalBound = false; naturalFarthest = 0;
        EditorApplication.update += ObserveNatural;
    }
    private static void ObserveNatural()
    {
        try
        {
            if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup - started > 90) throw new Exception("Natural physics timeout");
            if (Time.time - naturalStarted < .8f) return;
            var source = naturalBase.GetComponent<MechanismBaseFeature>();
            if (!naturalBound)
            {
                Check(source.IsSignalActive, "natural thrown red cube powers thrown base after landing");
                naturalPlatform.BindBase(source);
                naturalOrigin = naturalPlatform.transform.position;
                naturalRedOffset = naturalRed.position - naturalOrigin;
                naturalBaseOffset = naturalBase.position - naturalOrigin;
                naturalStarted = Time.time; naturalBound = true; return;
            }
            // No reflected FixedUpdate or Physics.Simulate here: actual Unity execution.
            Check(source.IsSignalActive, "natural execution continuous load power");
            Check(Vector3.Distance(naturalRed.position - naturalPlatform.transform.position, naturalRedOffset) < .08f, "natural red transport stable");
            Check(Vector3.Distance(naturalBase.position - naturalPlatform.transform.position, naturalBaseOffset) < .08f, "natural base transport stable");
            naturalFarthest = Mathf.Max(naturalFarthest, Vector3.Distance(naturalPlatform.transform.position, naturalOrigin));
            if (Time.time - naturalStarted < 3.2f) return;
            Check(naturalFarthest > 2.9f, "natural real signal transports both loads to endpoint");
            Cleanup(); Finish(true, "PASS " + checks + " platform load assertions (real Play and PhysX, including natural FixedUpdate self-powered transport)");
        }
        catch (Exception error) { Cleanup(); Finish(false, error.ToString()); }
    }
    private static void Cleanup() { foreach (GameObject item in objects) if (item != null) Object.DestroyImmediate(item); objects.Clear(); }
    private static void Finish(bool pass, string report)
    {
        SessionState.EraseBool(Flag); EditorApplication.update -= Tick;
        EditorApplication.update -= ObserveNatural;
        File.WriteAllText("Library/PlatformLoadTransportAudit.txt", report); Debug.Log(report); EditorApplication.Exit(pass ? 0 : 1);
    }
}
