using System;
using System.IO;
using System.Linq;
using System.Reflection;
using DG.Tweening;
using Project.BlockFeatures;
using Project.ColorBlocks;
using Project.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Real Play/PhysX/controller head-carry clearance; does not save scenes.</summary>
public static class CarryTunnelClearanceAudit
{
    const string Flag = "CarryTunnelClearanceAudit.Running";
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    static double started;
    static int checks;
    static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
        checks++; Debug.Log("CARRY TUNNEL PASS " + message);
    }
    static void Set(object owner, string field, object value) => owner.GetType().GetField(field, Hidden).SetValue(owner, value);
    static object Get(object owner, string field) => owner.GetType().GetField(field, Hidden).GetValue(owner);
    static void Call(object owner, string method) => owner.GetType().GetMethod(method, Hidden).Invoke(owner, null);
    public static void Run()
    {
        if (!Application.dataPath.Replace('\\', '/').EndsWith("/Library/FlowRetirementProbe/Assets") ||
            !Environment.GetCommandLineArgs().Contains("-noaudio")) throw new InvalidOperationException("Silent isolated probe only");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Flag, true); Resume(); EditorApplication.EnterPlaymode();
    }
    [InitializeOnLoadMethod] static void Resume()
    {
        if (!SessionState.GetBool(Flag, false)) return;
        started = EditorApplication.timeSinceStartup;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
    }
    static void Tick()
    {
        if (EditorApplication.timeSinceStartup - started > 90) { Finish(false, "timeout"); return; }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.time < .2f) return;
        EditorApplication.update -= Tick;
        var mode = Physics.simulationMode;
        try
        {
            Physics.simulationMode = SimulationMode.Script;
            ColorRuntimeService.Instance.SetAllUnlockedForCurrentSession(true);
            ColorRuntimeService.Instance.UnlockAllAbilitiesForCurrentSession();
            foreach (string prefab in new[] { "Red_Foundation", "Universal_Block" })
            {
                Verify(prefab, 2f, 1f, true);
                Verify(prefab, 1f, 1f, false);
                Verify(prefab, 1.5f, 1f, false);
            }
            Verify("Universal_Block", 2f, 2f, false);
            Verify("Universal_Block", 3f, 2f, true);
            Finish(true, "PASS " + checks + " real Play carry-clearance assertions");
        }
        catch (Exception error) { Finish(false, error.ToString()); }
        finally { Physics.simulationMode = mode; }
    }
    static GameObject Solid(string name, Vector3 position, Vector3 size)
    {
        var item = GameObject.CreatePrimitive(PrimitiveType.Cube);
        item.name = name; item.transform.position = position; item.transform.localScale = size; return item;
    }
    static void Verify(string prefab, float clearance, float itemHeight, bool shouldPass)
    {
        var floor = Solid("floor", new Vector3(3, -.5f, 0), new Vector3(16, 1, 4));
        var ceiling = Solid("tunnel ceiling", new Vector3(4, clearance + .5f, 0), new Vector3(4, 1, 4));
        var playerObject = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Content/Player/Player.prefab"), new Vector3(0, .55f, 0), Quaternion.identity);
        var player = playerObject.GetComponent<PlayerController>();
        player.GetComponent<PlayerInputDriver>().enabled = false;
        player.Motor.interpolation = RigidbodyInterpolation.None;
        var owner = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Content/ColorBlocks/Prefabs/" + prefab + ".prefab"), new Vector3(-2, .5f, 0), Quaternion.identity);
        owner.transform.localScale = new Vector3(1, itemHeight, 1);
        var carry = owner.GetComponent<CarryableFeature>();
        Physics.SyncTransforms();
        for (int i = 0; i < 40; i++) Step(player, Vector2.zero);
        Check(carry.BeginCarry(player), prefab + " starts carry");
        Set(player, "selectedCarryTarget", carry);
        var motion = Get(carry, "carryMotion");
        ((Tween)Get(motion, "tween")).Complete();
        carry.UpdateCarryAnchor(player.CarryAnchorWorldPosition); Physics.SyncTransforms();
        Check(owner.GetComponentsInChildren<Collider>().All(c => !c.enabled), prefab + " carried shapes disabled");
        var carried = carry.GetCarryPlacementBounds(owner.transform.position);
        Check(Mathf.Abs(carried.size.y - itemHeight) < .001f, prefab + " actual carried height retained");
        Check(Mathf.Abs(carried.min.y - (player.GetComponent<CapsuleCollider>().bounds.max.y - 1f / 16)) < .001f,
            prefab + " support clearance exactly one effective art pixel");
        float maxTop = 0;
        for (int i = 0; i < 150; i++)
        {
            Step(player, Vector2.right);
            carry.UpdateCarryAnchor(player.CarryAnchorWorldPosition); Physics.SyncTransforms();
            if (player.Motor.position.x > 2.1f && player.Motor.position.x < 5.9f)
                maxTop = Mathf.Max(maxTop, carry.GetCarryPlacementBounds(owner.transform.position).max.y);
        }
        string label = prefab + " itemHeight=" + itemHeight + " tunnel=" + clearance + " x=" + player.Motor.position.x;
        Check(shouldPass ? player.Motor.position.x > 6.3f : player.Motor.position.x < 2f, label);
        if (shouldPass) Check(maxTop <= clearance + .001f, "carried solid never penetrates ceiling: " + label);
        carry.CancelCarry();
        Check(owner.GetComponentsInChildren<Collider>().Any(c => c.enabled && !c.isTrigger), prefab + " cancel restores physical body");
        Object.DestroyImmediate(owner); Object.DestroyImmediate(playerObject); Object.DestroyImmediate(floor); Object.DestroyImmediate(ceiling);
        Physics.SyncTransforms();
    }
    static void Step(PlayerController player, Vector2 input)
    {
        player.SetMoveInput(input); Call(player, "FixedUpdate"); Physics.Simulate(Time.fixedDeltaTime); Physics.SyncTransforms();
    }
    static void Finish(bool success, string message)
    {
        SessionState.SetBool(Flag, false); EditorApplication.update -= Tick;
        File.WriteAllText("Library/CarryTunnelClearanceAudit.txt", message); Debug.Log(message);
        if (EditorApplication.isPlaying) { EditorApplication.ExitPlaymode(); EditorApplication.delayCall += () => EditorApplication.Exit(success ? 0 : 1); }
        else EditorApplication.Exit(success ? 0 : 1);
    }
}
