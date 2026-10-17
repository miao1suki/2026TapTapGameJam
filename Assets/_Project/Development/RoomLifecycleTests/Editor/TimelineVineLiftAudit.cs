using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Project.BlockFeatures;
using Project.ColorBlocks;
using Project.ContactCubes;
using Project.Player;
using Project.Rooms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Timeline;
using Object = UnityEngine.Object;

// Real automatic fixed-step/coroutine checks in a disposable, silent project only.
public static class TimelineVineLiftAudit
{
    private const string Flag = "TimelineVineLiftAudit.Running";
    private sealed class Load
    {
        public ClimbableVineFeature Vine;
        public Rigidbody Body;
        public float InitialY;
        public bool Blocked, Intermediate;
        public int Rise = 1;
    }
    private static readonly List<Load> loads = new();
    private static double started;
    private static float phaseTime;
    private static int phase, checks;
    private static Load reset, hook, carry, dynamicCeiling;
    private static Load liveBlue, burn, disabled;
    private static Rigidbody stackTop;
    private static PlayerController rider;
    private static float riderStart;
    private static RoomLifecycleManager manager;
    private static ContactTimelineTrigger trigger;
    private static Collider lateCeiling;
    private static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Set(object target, string field, object value)
    {
        Type type = target.GetType();
        while (type != null)
        {
            var info = type.GetField(field, Hidden);
            if (info != null) { info.SetValue(target, value); return; }
            type = type.BaseType;
        }
        throw new MissingFieldException(field);
    }
    private static void Check(bool value, string label)
    {
        if (!value) throw new InvalidOperationException("VINE LIFT: " + label);
        checks++; Debug.Log("VINE LIFT PASS: " + label);
    }
    private static GameObject Spawn(string path, Vector3 position) => Object.Instantiate(
        AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Content/" + path), position, Quaternion.identity);
    private static GameObject Cube(string name, Vector3 position)
    {
        var item = GameObject.CreatePrimitive(PrimitiveType.Cube); item.name = name; item.transform.position = position; return item;
    }
    private static Load Case(float x, string path = null, bool blocked = false)
    {
        var root = Spawn("ColorBlocks/Prefabs/Green_Ladder.prefab", new Vector3(x, 0, 0)).GetComponent<ClimbableVineFeature>();
        Set(root, "maxGrowthBlocks", 3); Set(root, "growthInterval", .6f); Set(root, "growInstantly", true);
        var item = path == null ? Cube("Gravity load", new Vector3(x, 1, 0)) : Spawn(path, new Vector3(x, 1, 0));
        var body = item.GetComponent<Rigidbody>();
        if (body == null) body = item.AddComponent<Rigidbody>();
        // Isolate load-controller physics from red burning / blue auto-growth;
        // separate unmuted cases below verify that these reactions still happen.
        var reaction = item.GetComponent<ContactCube>();
        if (reaction != null) reaction.SetHookSuspended(true);
        body.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionZ;
        var load = new Load { Vine = root, Body = body, InitialY = 1, Blocked = blocked };
        loads.Add(load); return load;
    }
    public static void Run()
    {
        if (!Application.dataPath.Replace('\\', '/').Contains("/Library/FlowRetirementProbe/Assets") ||
            Array.IndexOf(Environment.GetCommandLineArgs(), "-noaudio") < 0)
            throw new InvalidOperationException("Only use the silent isolated probe project.");
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
        if (EditorApplication.timeSinceStartup - started > 100) { Finish(new Exception("Timed out")); return; }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.time < .15f) return;
        try
        {
            if (phase == 0)
            {
                ColorRuntimeService.Instance.SetAllUnlockedForCurrentSession(true);
                new GameObject("Silent audit listener").AddComponent<AudioListener>();
                Case(0); Case(8, "ContactCubes/Red_ContactCube.prefab");
                Case(16, "ContactCubes/Green_ContactCube.prefab"); Case(24, "ContactCubes/Blue_ContactCube.prefab");
                var stack = Case(32, "ContactCubes/Red_ContactCube.prefab");
                stackTop = Cube("Stack upper gravity box", new Vector3(32, 2, 0)).AddComponent<Rigidbody>();
                stackTop.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionZ;
                var fixedBody = Case(40, blocked: true); fixedBody.Body.useGravity = false; fixedBody.Body.constraints = RigidbodyConstraints.FreezeAll;
                var ceiling = Case(48, blocked: true); Cube("Ceiling", new Vector3(48, 2.5f, 0));
                Case(56, "ColorBlocks/Prefabs/Red_Foundation.prefab");
                var noGravityBase = Case(64, "ColorBlocks/Prefabs/Red_Foundation.prefab", true);
                Set(noGravityBase.Body.GetComponent<MechanismBaseFeature>(), "useGravity", false);
                reset = Case(72, "ContactCubes/Red_ContactCube.prefab");
                hook = Case(80, "ContactCubes/Red_ContactCube.prefab");
                carry = Case(88, "ContactCubes/Blue_ContactCube.prefab");
                dynamicCeiling = Case(96);
                lateCeiling = Cube("Late ceiling", new Vector3(96, 2.6f, 0)).GetComponent<Collider>(); lateCeiling.enabled = false;
                var ridden = Case(104, "ContactCubes/Red_ContactCube.prefab");
                rider = Spawn("Player/Player.prefab", new Vector3(104, 2, 0)).GetComponent<PlayerController>();
                rider.GetComponent<PlayerInputDriver>().enabled = false;
                liveBlue = Case(120, "ContactCubes/Blue_ContactCube.prefab"); loads.Remove(liveBlue);
                Set(liveBlue.Vine,"maxGrowthBlocks",1); liveBlue.Body.GetComponent<ContactCube>().SetHookSuspended(false);
                burn = Case(128);
                Case(144,"ContactCubes/Green_ContactCube.prefab",true);
                disabled = Case(160,"ContactCubes/Green_ContactCube.prefab");
                Case(176,"ContactCubes/Green_ContactCube.prefab",true);
                var blockedRider = Spawn("Player/Player.prefab",new Vector3(176,2,0)).GetComponent<PlayerController>();
                blockedRider.GetComponent<PlayerInputDriver>().enabled = false;
                Cube("Rider head ceiling",new Vector3(176,3.5f,0));
                Case(184).Rise = 2;
                manager = new GameObject("Reset verification").AddComponent<RoomLifecycleManager>();
                var region = new RoomRegion { id = "test", label = "test" };
                foreach (Vector2Int cell in new RectInt(70, -8, 5, 13).allPositionsWithin) region.cells.Add(cell);
                var edge = new RoomRegion { id = "edge", label = "edge", cells = new List<Vector2Int> { new(144,-1), new(144,-2) } };
                manager.Configure(new List<RoomRegion> { region, edge }, 1, "");
                Set(manager, "autoRespawnOnDeath", false);
                trigger = Spawn("ColorBlocks/Prefabs/ContactTimeline_Block.prefab", new Vector3(152, 0, 0)).GetComponent<ContactTimelineTrigger>();
                var timeline = ScriptableObject.CreateInstance<TimelineAsset>(); timeline.durationMode = TimelineAsset.DurationMode.FixedLength; timeline.fixedDuration = 1;
                Set(trigger, "timeline", timeline); trigger.Director.playableAsset = timeline;
                phase = 1; phaseTime = Time.time;
            }
            else if (phase == 1 && Time.time - phaseTime > .45f)
            {
                foreach (Load load in loads)
                {
                    Check(load.Vine.isActiveAndEnabled, "unlocked vine attached at " + load.Vine.transform.position.x);
                    load.InitialY = load.Body.position.y; load.Vine.GrowTo(load.Rise);
                }
                riderStart = rider.transform.position.y;
                Check(trigger.ManagedCollider.enabled && trigger.ManagedCollider.isTrigger, "invisible Timeline collider remains an enabled trigger");
                Check(!trigger.GetComponent<Renderer>().enabled && trigger.GetComponentsInChildren<SpriteRenderer>(true).Length == 0, "Timeline has neither visible cube nor game photo renderer");
                trigger.OnTriggerEnter(rider.GetComponent<Collider>());
                Check(trigger.HasTriggered, "hidden Timeline still accepts actual standard-player collider");
                trigger.enabled = false;
                Check(!trigger.GetComponent<Renderer>().enabled && !rider.IsControlLocked, "disable does not reveal trigger or leave a control lock");
                trigger.enabled = true; trigger.ResetTrigger();
                phase = 2; phaseTime = Time.time;
            }
            else if (phase == 2)
            {
                foreach (Load load in loads)
                    if (load.Body.position.y > load.InitialY + .02f && load.Body.position.y < load.InitialY + .95f) load.Intermediate = true;
                if (Time.time - phaseTime > .2f)
                {
                    Check(VineGrowthLift.IsLifting(hook.Body), "hook test is inside a real incremental lift");
                    hook.Body.GetComponent<ContactCubeMotion>().BeginGreenHookPull();
                    Check(!VineGrowthLift.IsLifting(hook.Body) && !hook.Body.useGravity && !hook.Body.isKinematic, "green hook owns body without lift overwriting its physics");
                    carry.Body.GetComponent<ContactCubeMotion>().SuspendMotion(true);
                    Check(!VineGrowthLift.IsLifting(carry.Body) && carry.Body.isKinematic, "carry suspension takes over without dual body writers");
                    Check(VineGrowthLift.IsLifting(reset.Body), "room reset occurs during an active lift");
                    manager.ResetRegion("test");
                    Check(!VineGrowthLift.IsLifting(reset.Body) && reset.Vine.CurrentGrowthBlocks == 0 && Mathf.Abs(reset.Body.position.y - 1) < .05f && reset.Body.useGravity,
                        "room reset atomically restores authored body and vine growth");
                    lateCeiling.enabled = true; Physics.SyncTransforms();
                    burn.Vine.BeginBurn();
                    Check(!VineGrowthLift.IsLifting(burn.Body) && burn.Body.useGravity, "burning immediately cancels an active load lease");
                    disabled.Vine.gameObject.SetActive(false);
                    Check(!VineGrowthLift.IsLifting(disabled.Body) && disabled.Body.useGravity, "disabling the vine immediately restores its load physics");
                    phase = 3; phaseTime = Time.time;
                }
            }
            else if (phase == 3 && Time.time - phaseTime > 2.4f)
            {
                foreach (Load load in loads)
                {
                    if (load == reset || load == hook || load == carry || load == dynamicCeiling || load == burn || load == disabled) continue;
                    float dy = load.Body.position.y - load.InitialY;
                    string label = "x=" + load.Vine.transform.position.x + " delta=" + dy;
                    if (load.Blocked)
                        Check(load.Vine.CurrentGrowthBlocks == 0 && load.Vine.IsGrowthBlocked && Mathf.Abs(dy) < .08f, "non-gravity or ceiling obstruction stops growth: " + label);
                    else
                    {
                        Check(load.Vine.CurrentGrowthBlocks == load.Rise && Mathf.Abs(dy - load.Rise) < .1f, "gravity load rises authored cell count " + load.Rise + ": " + label);
                        Check(load.Intermediate, "lift passes intermediate physical positions instead of teleporting: " + label);
                        Check(!VineGrowthLift.IsLifting(load.Body) && load.Body.useGravity && !load.Body.isKinematic, "body gravity and control ownership restored: " + label);
                    }
                    Check(Mathf.Abs(load.Body.position.x - load.Vine.transform.position.x) < .05f, "no unintended sideways push: " + label);
                }
                Check(dynamicCeiling.Vine.CurrentGrowthBlocks == 0 && dynamicCeiling.Vine.IsGrowthBlocked && !VineGrowthLift.IsLifting(dynamicCeiling.Body) && dynamicCeiling.Body.useGravity,
                    "new obstruction during growth aborts safely and restores physics");
                Check(Mathf.Abs(stackTop.position.y-3) < .1f && stackTop.useGravity && !VineGrowthLift.IsLifting(stackTop), "entire stacked load rises one cell and releases physics");
                Check(liveBlue.Vine.CurrentGrowthBlocks == 1 && Mathf.Abs(liveBlue.Body.position.y-2) < .1f, "unmuted blue contact triggers growth and is lifted by real gameplay");
                Check(Mathf.Abs(rider.transform.position.y - riderStart - 1) < .18f && !rider.IsControlLocked && !rider.IsDead,
                    "standard player riding a lifted box moves one cell and remains controllable: dy=" + (rider.transform.position.y-riderStart));
                Check(!Physics.GetIgnoreCollision(loads.Find(load=>Mathf.Abs(load.Vine.transform.position.x-104)<.1f).Body.GetComponentInChildren<Collider>(), rider.GetComponent<Collider>()), "lift keeps player-to-box collision enabled");
                loads[1].Body.GetComponent<ContactCube>().SetHookSuspended(false);
                phase = 4; phaseTime = Time.time;
            }
            else if (phase == 4 && Time.time-phaseTime > .25f)
            {
                Check(loads[1].Vine.IsBurning || loads[1].Vine.IsBurned, "unmuted red contact still burns the vine instead of losing its color reaction");
                rider.RequestJump(); phase = 5; phaseTime = Time.time;
            }
            else if (phase == 5 && Time.time-phaseTime > .1f)
            {
                Check(rider.VerticalVelocity > 0, "standard player can actually jump after riding the growing load");
                Finish(null);
            }
        }
        catch (Exception error) { Finish(error); }
    }
    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick; SessionState.EraseBool(Flag);
        string result = error == null ? "PASS " + checks + " real physics assertions" : "FAIL " + error;
        File.WriteAllText("Library/TimelineVineLift.txt", result);
        if (error == null) Debug.Log(result); else Debug.LogException(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
