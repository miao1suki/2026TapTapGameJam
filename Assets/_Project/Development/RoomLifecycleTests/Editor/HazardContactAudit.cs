using System;
using System.IO;
using System.Reflection;
using Project.BlockFeatures;
using Project.ColorBlocks;
using Project.LevelEditor;
using Project.Player;
using Project.Rooms;
using Project.WorldItems;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Actual physics callbacks, never reflective calls to hazard contact handlers.
public static class HazardContactAudit
{
    private const string Flag = "HazardContactAudit.Running";
    private static int checks;
    private static double started;
    private static void Check(bool success, string message)
    {
        if (!success) throw new InvalidOperationException("HAZARD CONTACT: " + message);
        checks++; Debug.Log("HAZARD PASS: " + message);
    }
    public static void Run()
    {
        if (!Application.dataPath.Replace('\\', '/').Contains("/Library/FlowRetirementProbe/Assets"))
            throw new InvalidOperationException("仅在隔离工程验证，不修改作者场景。");
        if (!Array.Exists(Environment.GetCommandLineArgs(), argument =>
                string.Equals(argument, "-noaudio", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("危险物测试会连续触发死亡，必须使用 -noaudio 静音运行。");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Flag, true); Resume(); EditorApplication.EnterPlaymode();
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
        if (EditorApplication.timeSinceStartup - started > 90) { Finish(false, "Timeout"); return; }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.time < .15f) return;
        EditorApplication.update -= Tick;
        var previous = Physics.simulationMode;
        try
        {
            // This audit intentionally creates many deaths; never play their audio to the author.
            _ = Project.Audio.AudioManager.Instance;
            AudioListener.volume = 0f;
            AudioListener.pause = true;
            Physics.simulationMode = SimulationMode.Script; checks = 0;
            ColorRuntimeService.Instance.ResetProgress();
            foreach (string direction in new[] { "上", "下", "左", "右" }) VerifySpike(direction);
            VerifyProtectionAndReset(); VerifyRootTrigger();
            foreach (string direction in new[] { "上", "下", "左", "右" })
            { VerifyMerged(direction, false); VerifyMerged(direction, true); }
            VerifyLava(); VerifyRespawnSafety();
            Finish(true, "PASS " + checks);
        }
        catch (Exception error) { Finish(false, error.ToString()); }
        finally { Physics.simulationMode = previous; }
    }
    private static GameObject Spawn(string path, Vector3 position) => Object.Instantiate(
        AssetDatabase.LoadAssetAtPath<GameObject>(path), position, Quaternion.identity);
    private static PlayerController Player(Vector3 position)
    {
        var player = Spawn("Assets/_Project/Content/Player/Player.prefab", position).GetComponent<PlayerController>();
        player.GetComponent<PlayerInputDriver>().enabled = false;
        player.Motor.useGravity = false; player.Motor.constraints = RigidbodyConstraints.FreezeAll;
        Set(player, "invulnerableUntil", 0f);
        return player;
    }
    private static void Set(object target, string name, object value) => target.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static void Step(PlayerController player)
    {
        player.Motor.WakeUp(); Physics.SyncTransforms();
        for (int i = 0; i < 3; i++) Physics.Simulate(.02f);
    }
    private static void VerifySpike(string direction)
    {
        Vector3 origin = new Vector3(100, 20, 0);
        var spike = Spawn("Assets/_Project/Content/WorldItems/Spike_" + direction + ".prefab", origin);
        var player = Player(origin + Vector3.left * 3); Step(player);
        Check(!player.IsDead, "outside spike remains safe " + direction);
        var shape = spike.GetComponentInChildren<MeshCollider>();
        Check(shape != null && shape.convex && shape.isTrigger && spike.GetComponent<NeutralSpike>() != null,
            "real authored directional trigger " + direction);
        player.Motor.position = origin; player.transform.position = origin; Physics.SyncTransforms();
        var playerShape = player.GetComponent<Collider>();
        Check(Physics.ComputePenetration(shape, shape.transform.position, shape.transform.rotation,
            playerShape, playerShape.transform.position, playerShape.transform.rotation, out _, out _), "physical overlap verified " + direction);
        Step(player);
        Check(player.IsDead, "real child trigger enters unified death " + direction);
        Object.DestroyImmediate(player.gameObject); Object.DestroyImmediate(spike);
    }
    private static void Move(PlayerController player, Vector3 position)
    {
        player.Motor.position = position; player.transform.position = position; Step(player);
    }
    private static void VerifyProtectionAndReset()
    {
        Vector3 origin = new Vector3(200, 20, 0);
        var spike = Spawn("Assets/_Project/Content/WorldItems/Spike_上.prefab", origin).GetComponent<NeutralSpike>();
        var player = Player(origin + Vector3.left * 3);
        Set(player, "invulnerableUntil", Time.time + 100);
        Move(player, origin);
        Check(!player.IsDead, "real child trigger respects revival protection");
        Set(player, "invulnerableUntil", 0f); Step(player);
        Check(player.IsDead, "remaining inside kills when protection expires, without re-entry");
        player.RequestRespawn();
        Check(!player.IsDead, "spike death can use normal R respawn");
        Set(player, "invulnerableUntil", 0f); spike.enabled = false; Move(player, origin);
        Check(!player.IsDead, "disabled owner cannot hurt through a live child relay");
        spike.enabled = true; Step(player);
        Check(player.IsDead, "reenabling owner while overlapping resumes danger");
        int relays = spike.GetComponentsInChildren<SpikeContactRelay>(true).Length;
        spike.ResetForRoom(); spike.ResetForRoom();
        Check(spike.GetComponentsInChildren<SpikeContactRelay>(true).Length == relays, "reset does not duplicate relays");
        Object.DestroyImmediate(player.gameObject); Object.DestroyImmediate(spike.gameObject);
    }
    private static void VerifyRootTrigger()
    {
        Vector3 origin = new Vector3(220, 20, 0);
        var root = new GameObject("Root spike"); root.transform.position = origin;
        root.AddComponent<BoxCollider>().isTrigger = true; root.AddComponent<NeutralSpike>();
        var player = Player(origin + Vector3.left * 3); Step(player); Move(player, origin);
        Check(player.IsDead, "root-collider spike remains supported");
        Object.DestroyImmediate(player.gameObject); Object.DestroyImmediate(root);
    }
    private static void VerifyMerged(string direction, bool irregular)
    {
        Vector3 origin = new Vector3(250, 20, 0);
        var staging = new GameObject("Inactive staging"); staging.SetActive(false);
        var spike = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/Content/WorldItems/Spike_" + direction + ".prefab"), origin, Quaternion.identity, staging.transform);
        Vector2Int size = irregular ? new Vector2Int(6, 3) : new Vector2Int(48, 1);
        spike.transform.localScale = new Vector3(size.x, size.y, 1);
        var placed = spike.AddComponent<LevelEditorPlacedBlock>();
        placed.Configure(Vector2Int.zero, "尖刺", Color.white, false, true, 1f); placed.SetSizeCells(size);
        if (irregular)
            spike.AddComponent<LevelEditorMergedShape>().Configure(size, new[] {
                new RectInt(0,0,1,1), new RectInt(5,0,1,1), new RectInt(2,2,1,1) });
        var reset = spike.AddComponent<RoomResetObject>();
        spike.transform.SetParent(null, true); Object.DestroyImmediate(staging);
        var volumes = spike.GetComponentsInChildren<MeshCollider>();
        int active = 0; foreach (var volume in volumes) if (volume.enabled) active++;
        Check(active == (irregular ? 3 : 48), "one precise prism per occupied merged cell " + direction + "/" + irregular);
        int[] cells = irregular ? new[] { 0, 5, 14 } : new[] { 0, 24, 47 };
        foreach (int cell in cells)
        {
            Vector3 point = origin + new Vector3(cell % size.x + .5f - size.x * .5f,
                size.y * .5f - .5f - cell / size.x, 0);
            var player = Player(point + Vector3.back * 3); Step(player); Move(player, point);
            Check(player.IsDead, "merged cell actually kills " + direction + "/" + irregular + "/" + cell);
            Object.DestroyImmediate(player.gameObject);
        }
        if (irregular)
        {
            var player = Player(origin + new Vector3(-.5f, 1, -3)); Step(player);
            Move(player, origin + new Vector3(-.5f, 1, 0));
            Check(!player.IsDead, "merged hole remains safe " + direction);
            Object.DestroyImmediate(player.gameObject);
        }
        reset.RestorePose(); reset.RestoreState();
        active = 0; foreach (var volume in volumes) if (volume.enabled) active++;
        Check(active == (irregular ? 3 : 48), "room reset never restores giant stretched trigger " + direction + "/" + irregular);
        Object.DestroyImmediate(spike);
    }
    private static void VerifyLava()
    {
        Vector3 origin = new Vector3(300, 20, 0);
        ColorRuntimeService.Instance.ResetProgress();
        var lava = Spawn("Assets/_Project/Content/ColorBlocks/Prefabs/Red_Lava.prefab", origin);
        var player = Player(origin + Vector3.left * 3); Step(player); Move(player, origin);
        Check(!player.IsDead, "locked lava remains harmless");
        var managerObject = new GameObject("Hazard room"); var manager = managerObject.AddComponent<RoomLifecycleManager>();
        var member = lava.AddComponent<RoomResetObject>(); member.Bind(manager, "hazards");
        ColorRuntimeService.Instance.Unlock("red");
        Set(player, "invulnerableUntil", Time.time + 100); Step(player);
        Check(!player.IsDead, "unlocked lava respects revival protection");
        Set(player, "invulnerableUntil", 0f); Step(player);
        Check(player.IsDead && player.GetComponent<PlayerHealth>().IsDead, "real lava trigger reaches health and unified death");
        Object.DestroyImmediate(player.gameObject);
        player = Player(origin + Vector3.left * 3);
        var feature = lava.GetComponent<LavaHazardFeature>();
        ColorRuntimeService.Instance.Unlock("blue");
        Check(feature.ApplyColor("blue", player.gameObject) && !lava.activeSelf, "blue cooling disables authored lava trigger");
        Move(player, origin); Check(!player.IsDead, "cooled lava cannot hurt");
        member.RestorePose(); member.RestoreState(); Step(player);
        Check(lava.activeSelf && ColorRuntimeService.Instance.IsUnlocked("red") && player.IsDead,
            "room reset restores lava danger without relocking color");
        Object.DestroyImmediate(player.gameObject); Object.DestroyImmediate(lava); Object.DestroyImmediate(managerObject);
    }
    private static void VerifyRespawnSafety()
    {
        Vector3 origin = new Vector3(350, 20, 0);
        var group = new GameObject("Shared planning root");
        var spike = Spawn("Assets/_Project/Content/WorldItems/Spike_上.prefab", origin);
        var player = Player(origin + Vector3.left * 3);
        spike.transform.SetParent(group.transform, true); player.transform.SetParent(group.transform, true);
        Physics.SyncTransforms();
        var safe = typeof(PlayerController).GetMethod("IsRespawnPositionSafe", BindingFlags.Instance | BindingFlags.NonPublic);
        Check(!(bool)safe.Invoke(player, new object[] { origin }), "spikes are excluded from respawn even under shared map root");
        spike.GetComponentInChildren<Collider>().enabled = false; Physics.SyncTransforms();
        Check((bool)safe.Invoke(player, new object[] { origin }), "disabled trigger does not block respawn");
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.transform.position = origin + new Vector3(3, -1, 0); floor.transform.SetParent(group.transform, true);
        Physics.SyncTransforms();
        var grounded = typeof(PlayerController).GetMethod("TryGetGroundedRespawnPosition", BindingFlags.Instance | BindingFlags.NonPublic);
        object[] arguments = { origin + new Vector3(3, .1f, 0), Vector3.zero };
        Check((bool)grounded.Invoke(player, arguments), "shared-root floor is valid ground rather than mistaken for player");
        Object.DestroyImmediate(group);
    }
    private static void Finish(bool passed, string message)
    {
        SessionState.SetBool(Flag, false); EditorApplication.update -= Tick;
        File.WriteAllText("Library/HazardContactResult.txt", message); Debug.Log(message);
        if (EditorApplication.isPlaying)
        { EditorApplication.ExitPlaymode(); EditorApplication.delayCall += () => EditorApplication.Exit(passed ? 0 : 1); }
        else EditorApplication.Exit(passed ? 0 : 1);
    }
}
