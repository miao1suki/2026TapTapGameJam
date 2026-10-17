using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using Project.BlockFeatures;
using Project.ColorBlocks;
using Project.ContactCubes;
using Project.Items;
using Project.Player;
using Project.WorldItems;
using Project.WorldItems.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;
using PlanningEditorPrototype;
using Project.LevelEditor;
using Project.LevelEditor.Editor;
using Project.InputAbstraction;
using TaSource.LinkEffect;

namespace Project.GreenHook.Editor
{
    /// <summary>Isolated, real rigidbody regression; never opens or rewrites an author's scene.</summary>
    public static class GreenHookVerification
    {
        private static int checks;
        private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static object Invoke(object target, string method, params object[] args) =>
            target.GetType().GetMethod(method, Private).Invoke(target, args);
        private static void Set(object obj, string field, object value) => obj.GetType().GetField(field, Private).SetValue(obj, value);
        private static void Check(bool success, string label)
        {
            checks++; if (!success) throw new Exception(label); Debug.Log("HOOK PASS " + label);
        }
        public static void Run()
        {
            if (!Application.dataPath.Contains("FlowRetirementProbe")) throw new Exception("Isolated verification only");
            if (!Environment.GetCommandLineArgs().Contains("-noaudio")) throw new Exception("Verification requires -noaudio");
            WorldItemSetup.CreateMissing();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            SessionState.SetBool("GreenHookVerification", true); Resume(); EditorApplication.EnterPlaymode();
        }
        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!SessionState.GetBool("GreenHookVerification", false)) return;
            EditorApplication.update -= Wait; EditorApplication.update += Wait;
        }
        private static void Wait()
        {
            // The grace-time cases backdate a real timestamp by 0.41s; allow the play clock to start first.
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.time < .6f) return;
            EditorApplication.update -= Wait;
            try { RunCases(); Finish(true, "PASS " + checks); }
            catch (Exception error) { Debug.LogException(error); Finish(false, error.ToString()); }
        }
        private static GameObject Instantiate(string path, Vector3 position) =>
            Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path), position, Quaternion.identity);
        private static GameObject Solid(string name, Vector3 position, Vector3 size)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube); obj.name = name; obj.transform.position = position; obj.transform.localScale = size;
            return obj;
        }
        private static PlayerController Player(Vector3 position)
        {
            var player = Instantiate("Assets/_Project/Content/Player/Player.prefab", position).GetComponent<PlayerController>();
            player.GetComponent<PlayerInputDriver>().enabled = false;
            player.Motor.interpolation = RigidbodyInterpolation.None;
            return player;
        }
        private static GreenHookAbility Attach(PlayerController player, IGreenHookTarget target)
        {
            var hook = player.GetComponent<GreenHookAbility>();
            Set(hook, "hookDirection", Vector2.right); Set(hook, "hookLength", 6f);
            Invoke(hook, "BeginHook", target, target.GreenHookTransform.position);
            Invoke(hook, "BeginPulling"); Physics.SyncTransforms(); return hook;
        }
        private static void Step(GreenHookAbility hook, PlayerController player, int count)
        {
            for (int i = 0; i < count; i++)
            {
                Invoke(hook, "FixedUpdate"); Invoke(player, "FixedUpdate"); Physics.Simulate(Time.fixedDeltaTime);
            }
        }
        private static void RunCases()
        {
            var oldSimulation = Physics.simulationMode; Physics.simulationMode = SimulationMode.Script;
            try
            {
                ColorRuntimeService.Instance.SetAllUnlockedForCurrentSession(true);
                ColorRuntimeService.Instance.UnlockAllAbilitiesForCurrentSession();
                VerifyBothBlockedConnection();
                VerifyAttachedRange();
                VerifySwingRangeBoundary();
                VerifyPlantSwingAnchors();
                VerifyPullGravity();
                VerifyPullOwnershipHandoffs();
                for (int variant = 0; variant < 3; variant++)
                {
                    Vector3 origin = new Vector3(variant * 30, 20, 0);
                    var player = Player(origin);
                    var cube = Instantiate("Assets/_Project/Content/ContactCubes/Red_ContactCube.prefab", origin + Vector3.right * 5).GetComponent<ContactCubeMotion>();
                    cube.GetComponent<Rigidbody>().interpolation = variant == 1 ? RigidbodyInterpolation.Interpolate : RigidbodyInterpolation.None;
                    var floor = Solid("Floor", origin + new Vector3(2,-1.1f,0), new Vector3(20,1,2));
                    Physics.SyncTransforms();
                    var hook = Attach(player, cube); Vector3 start = cube.GreenHookPosition;
                    Step(hook, player, 7);
                    Check(cube.GreenHookPosition.x < start.x - .5f && !hook.IsPullingPlayer, "target first " + variant);
                    Check(Mathf.Abs(player.Motor.position.x - origin.x) < .03f, "player held during target motion");
                    var wall = Solid("Wall", new Vector3(cube.GreenHookPosition.x-.7f,origin.y,0), new Vector3(.4f,4,2));
                    Physics.SyncTransforms(); Step(hook,player,7);
                    Check(hook.IsPullingPlayer && player.Motor.position.x > origin.x + .25f, "blocked target switches to player");
                    Object.DestroyImmediate(wall); Physics.SyncTransforms();
                    start = cube.GreenHookPosition; Step(hook,player,4);
                    Check(!hook.IsPullingPlayer && cube.GreenHookPosition.x < start.x-.2f, "target priority re-evaluated");
                    Step(hook,player,650);
                    Check(hook.IsHookActive && player.IsGreenHookActive, "contact and long duration do not detach");
                    hook.DriveInput(Vector2.zero,true);
                    Check(!hook.IsHookActive && !player.IsGreenHookActive && !player.IsControlLocked, "manual release restores control");
                    Object.DestroyImmediate(cube.gameObject); Object.DestroyImmediate(player.gameObject); Object.DestroyImmediate(floor);
                }
                {
                    var player = Player(new Vector3(100,20,0));
                    var cube = Instantiate("Assets/_Project/Content/ContactCubes/Green_ContactCube.prefab", new Vector3(103,22,0)).GetComponent<ContactCubeMotion>();
                    cube.GetComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezeAll;
                    var hook = Attach(player,cube); Vector3 anchorStart = cube.GreenHookPosition;
                    Check(hook.IsSwinging, "green cube is swing anchor");
                    hook.DriveInput(Vector2.right,false); Step(hook,player,650);
                    Check(hook.IsSwinging && player.IsGreenHookActive, "swing never times out");
                    Check(Vector3.Distance(anchorStart,cube.GreenHookPosition)<.001f, "swing does not move anchor");
                    Check(Vector2.Distance(player.Motor.position,anchorStart) <= 3.7f, "rope length constrained");
                    var externalLock=player.AcquireControlLock(new object()); Step(hook,player,1);
                    Check(player.Motor.linearVelocity.sqrMagnitude < .001f && !player.CanDriveGreenHook, "additional control lock pauses hook motion");
                    externalLock.Dispose(); player.UpdateGreenHookInput(Vector2.zero,true);
                    Check(!hook.IsHookActive && !player.IsControlLocked,"player API releases swing");
                    Object.DestroyImmediate(player.gameObject); Object.DestroyImmediate(cube.gameObject);
                }
                VerifyItems();
                VerifyBaseAndPush();
                VerifyRoomBoundary();
                VerifyAimingAndFourAxisPull();
                VerifySwingRetargetInput();
                VerifySwingSafetyAndRapidRetarget();
                VerifyExtendingTargetInvalidation();
            }
            finally { Physics.simulationMode = oldSimulation; }
        }

        private static readonly string[] PullPrefabs =
        {
            "Assets/_Project/Content/ContactCubes/Red_ContactCube.prefab",
            "Assets/_Project/Content/ContactCubes/Blue_ContactCube.prefab",
            "Assets/_Project/Content/ColorBlocks/Prefabs/Red_Foundation.prefab"
        };

        private static void VerifyPullGravity()
        {
            for (int kind = 0; kind < 4; kind++)
            {
                Vector3 origin = new Vector3(1900 + kind * 25, 40, 0);
                var obj = Instantiate(PullPrefabs[Mathf.Min(kind, 2)], origin);
                var feature = obj.GetComponent<MechanismBaseFeature>();
                if (kind == 3)
                {
                    Set(feature, "useGravity", false);
                    Invoke(feature, "FixedUpdate");
                }
                var body = obj.GetComponent<Rigidbody>();
                var motion = obj.GetComponent<IGreenHookMotion>();
                bool originalKinematic = body.isKinematic;
                bool gravity = kind != 3;
                if (!body.isKinematic) body.linearVelocity = Vector3.down * 2;
                motion.BeginGreenHookPull();
                Check(body.useGravity == gravity && !body.isKinematic, "hook gravity respects author setting " + kind);
                Vector3 start = body.position;
                for (int step = 0; step < 20; step++)
                {
                    motion.SetGreenHookVelocity(Vector3.left * 3);
                    Physics.Simulate(Time.fixedDeltaTime);
                }
                Check(body.position.x < start.x - 1f, "horizontal force still pulls target " + kind);
                Check(gravity ? body.position.y < start.y - 1f : Mathf.Abs(body.position.y - start.y) < .001f,
                    "horizontal pull preserves falling or explicit no-gravity " + kind);
                start = body.position;
                for (int step = 0; step < 10; step++)
                {
                    motion.SetGreenHookVelocity(Vector3.zero);
                    Physics.Simulate(Time.fixedDeltaTime);
                }
                Check(Mathf.Abs(body.position.x - start.x) < .001f &&
                    (gravity ? body.position.y < start.y - .5f : Mathf.Abs(body.position.y - start.y) < .001f),
                    "blocked/held target stops lateral force without levitating " + kind);
                motion.SetGreenHookVelocity(Vector3.up * 3);
                Physics.Simulate(Time.fixedDeltaTime);
                Check(Mathf.Abs(body.linearVelocity.x) < .001f && body.linearVelocity.y > 2.5f && body.useGravity == gravity,
                    "vertical command preserves gravity policy " + kind);
                motion.EndGreenHookPull(false, Vector3.zero, 0);
                Check(body.useGravity == gravity && body.isKinematic == originalKinematic,
                    "release returns original motion ownership " + kind);
                Object.DestroyImmediate(obj);
            }
            for (int kind = 0; kind < PullPrefabs.Length; kind++)
            {
                Vector3 origin = new Vector3(2020 + kind * 25, 40, 0);
                var player = Player(origin + Vector3.left * 3);
                var obj = Instantiate(PullPrefabs[kind], origin);
                var body = obj.GetComponent<Rigidbody>();
                var pillar = Solid("DeparturePillar", origin + Vector3.down * 2f, new Vector3(1, 3, 2));
                var bottom = Solid("UndergroundFloor", origin + new Vector3(-2, -6, 0), new Vector3(14, 1, 2));
                Physics.SyncTransforms();
                var hook = Attach(player, obj.GetComponent<IGreenHookTarget>());
                Step(hook, player, 18);
                Check(body.position.x < origin.x - 1f && body.position.y < origin.y - .25f,
                    "live hook target falls after leaving pillar " + kind);
                Check(hook.IsHookActive && player.CanDriveGreenHook && !player.IsDead,
                    "falling target retains connection and single ownership " + kind);
                hook.CancelHook();
                Object.DestroyImmediate(obj); Object.DestroyImmediate(player.gameObject);
                Object.DestroyImmediate(pillar); Object.DestroyImmediate(bottom);

                player = Player(origin + Vector3.left * 3);
                obj = Instantiate(PullPrefabs[kind], origin);
                body = obj.GetComponent<Rigidbody>();
                var sideWall = Solid("FallingSideWall", origin + Vector3.left * .7f, new Vector3(.4f, 8, 2));
                Physics.SyncTransforms();
                hook = Attach(player, obj.GetComponent<IGreenHookTarget>());
                Step(hook, player, 5);
                Check(hook.IsPullingPlayer && body.position.y < origin.y - .04f && body.useGravity,
                    "blocked target still falls naturally while player is pulled " + kind);
                Check(player.Motor.position.x > origin.x - 2.7f && hook.IsHookActive,
                    "falling target does not prevent player fallback or detach " + kind);
                hook.CancelHook();
                Object.DestroyImmediate(obj); Object.DestroyImmediate(player.gameObject); Object.DestroyImmediate(sideWall);
            }
        }

        private static void VerifyPullOwnershipHandoffs()
        {
            for (int kind = 0; kind < PullPrefabs.Length; kind++)
            for (int handoff = 0; handoff < 3; handoff++)
            {
                Vector3 origin = new Vector3(2150 + kind * 50, 30 + handoff * 10, 0);
                var player = Player(origin);
                var obj = Instantiate(PullPrefabs[kind], origin + Vector3.right * 3);
                var motion = obj.GetComponent<IGreenHookMotion>();
                var body = obj.GetComponent<Rigidbody>();
                var hook = Attach(player, obj.GetComponent<IGreenHookTarget>());
                Check(motion.IsGreenHookPullActive && player.ControlLockDepth == 1,
                    "handoff begins with unique target/player ownership " + kind + "/" + handoff);
                if (handoff == 0) ((IRoomColorResettable)motion).ResetForRoom();
                else if (handoff == 1) obj.GetComponent<ICarryMotionParticipant>().SetCarrySuspended(true);
                else ((Behaviour)motion).enabled = false;
                Invoke(hook, "FixedUpdate");
                Check(!motion.IsGreenHookPullActive && !hook.IsHookActive && !player.IsControlLocked && !player.IsGreenHookActive,
                    "target handoff cancels rope and releases only its player handle " + kind + "/" + handoff);
                if (handoff == 1)
                {
                    Check(body.isKinematic && !body.useGravity, "carry keeps exclusive target physics " + kind);
                    obj.GetComponent<ICarryMotionParticipant>().SetCarrySuspended(false);
                }
                Object.DestroyImmediate(player.gameObject); Object.DestroyImmediate(obj);
            }
        }

        private static void VerifyBothBlockedConnection()
        {
            Vector3[] directions = { Vector3.right, Vector3.left, Vector3.up, Vector3.down };
            for (int kind = 0; kind < PullPrefabs.Length; kind++)
            for (int direction = 0; direction < directions.Length; direction++)
            {
                string label = "blocked pair " + kind + "/" + direction;
                Vector3 origin = new Vector3(1100 + kind * 50, 50 + direction * 15, 0);
                Vector3 axis = directions[direction];
                Vector3 wallSize = axis.x != 0 ? new Vector3(.4f, 4f, 2f) : new Vector3(4f, .4f, 2f);
                var player = Player(origin);
                var obj = Instantiate(PullPrefabs[kind], origin + axis * 4f);
                var target = obj.GetComponent<IGreenHookTarget>();
                var body = obj.GetComponent<Rigidbody>();
                var playerWall = Solid("PlayerStop", origin + axis * .7f, wallSize);
                var targetWall = Solid("TargetStop", obj.transform.position - axis * .7f, wallSize);
                // These are collision/fallback tests, not weightlessness tests. A
                // real support keeps the target at its authored height while blocked.
                var support = axis.y > 0f ? null : Solid("TargetSupport", obj.transform.position + Vector3.down * .75f,
                    new Vector3(axis.x != 0f ? 4f : 1f, .5f, 2f));
                Physics.SyncTransforms();
                var hook = Attach(player, target);
                Vector3 targetStart = body.position;
                Step(hook, player, 650);
                Check(hook.IsPulling && hook.IsPullingPlayer && player.CanDriveGreenHook && player.ControlLockDepth == 1,
                    label + " stays connected without timeout");
                Check((body.position - targetStart).sqrMagnitude < .001f &&
                    (player.Motor.position - origin).sqrMagnitude < .001f, label + " neither body penetrates blockers");

                Object.DestroyImmediate(playerWall); Physics.SyncTransforms();
                Step(hook, player, 4);
                Check(hook.IsPullingPlayer && Vector3.Dot(player.Motor.position - origin, axis) > .3f &&
                    (body.position - targetStart).sqrMagnitude < .001f, label + " resumes player pull without relaunch");
                Vector3 playerStart = player.Motor.position;
                Object.DestroyImmediate(targetWall); Physics.SyncTransforms();
                Step(hook, player, 4);
                Check(!hook.IsPullingPlayer && Vector3.Dot(targetStart - body.position, axis) > .3f &&
                    (player.Motor.position - playerStart).sqrMagnitude < .001f, label + " restores target priority");
                var releaseInput = new HookInput { Release = true,
                    ReleaseAction = direction % 2 == 0 ? InputActionId.Jump : InputActionId.Interact };
                GameInput.UseExternalSource(releaseInput);
                try { player.GetComponent<PlayerInputDriver>().Drive(player); }
                finally { GameInput.ClearExternalSource(releaseInput); }
                Check(!hook.IsHookActive && !player.IsControlLocked && !player.IsGreenHookActive,
                    label + " explicit release clears ownership");
                Object.DestroyImmediate(player.gameObject); Object.DestroyImmediate(obj);
                if (support != null) Object.DestroyImmediate(support);
            }
        }

        private static void VerifyAttachedRange()
        {
            // Both render and physics entry points must reject a real out-of-range anchor.
            // A blended visual tip or mouse pointer must never determine attachment lifetime.
            for (int kind = 0; kind < 4; kind++)
            for (int movePlayer = 0; movePlayer < 2; movePlayer++)
            for (int useUpdate = 0; useUpdate < 2; useUpdate++)
            {
                Vector3 origin = new Vector3(1300 + kind * 40, 50 + movePlayer * 20 + useUpdate * 10, 0);
                var player = Player(origin);
                string path = kind == 3 ? "Assets/_Project/Content/ContactCubes/Green_ContactCube.prefab" : PullPrefabs[kind];
                var obj = Instantiate(path, origin + Vector3.right * 3f);
                var body = obj.GetComponent<Rigidbody>();
                if (kind == 3) body.constraints = RigidbodyConstraints.FreezeAll;
                if (kind == 3) player.Motor.linearVelocity = Vector3.up * 2f;
                var hook = Attach(player, obj.GetComponent<IGreenHookTarget>());
                string label = "range " + kind + "/" + movePlayer + "/" + useUpdate;
                // Changing pointer alone does not release an existing connection.
                hook.Tick(new Vector2(-10000, -10000), true);
                Invoke(hook, useUpdate == 0 ? "FixedUpdate" : "Update");
                Check(hook.IsHookActive && player.ControlLockDepth == 1, label + " ignores pointer outside circle");
                if (movePlayer == 0) obj.transform.position = body.position = origin + Vector3.right * 6f;
                else player.transform.position = player.Motor.position = origin - Vector3.right * 3f;
                Physics.SyncTransforms();
                Invoke(hook, useUpdate == 0 ? "FixedUpdate" : "Update");
                Check(hook.IsHookActive && player.ControlLockDepth == 1, label + " six cells retains attached slack");
                if (movePlayer == 0) obj.transform.position = body.position = origin + Vector3.right * 9f;
                else player.transform.position = player.Motor.position = origin - Vector3.right * 6f;
                Physics.SyncTransforms();
                Invoke(hook, useUpdate == 0 ? "FixedUpdate" : "Update");
                Check(hook.IsHookActive, label + " transient overshoot retains connection");
                Set(hook, "outOfRangeSince", Time.time - .2f);
                Invoke(hook, useUpdate == 0 ? "FixedUpdate" : "Update");
                Check(hook.IsHookActive, label + " grace does not expire early");
                // Returning inside the enlarged radius clears the continuous-outside timer.
                if (movePlayer == 0) obj.transform.position = body.position = origin + Vector3.right * 6f;
                else player.transform.position = player.Motor.position = origin - Vector3.right * 3f;
                Physics.SyncTransforms();
                Invoke(hook, useUpdate == 0 ? "FixedUpdate" : "Update");
                Check((float)typeof(GreenHookAbility).GetField("outOfRangeSince", Private).GetValue(hook) < 0f,
                    label + " returning inside resets grace");
                if (movePlayer == 0) obj.transform.position = body.position = origin + Vector3.right * 9f;
                else player.transform.position = player.Motor.position = origin - Vector3.right * 6f;
                Physics.SyncTransforms();
                Invoke(hook, useUpdate == 0 ? "FixedUpdate" : "Update");
                Set(hook, "outOfRangeSince", Time.time - .41f);
                Invoke(hook, useUpdate == 0 ? "FixedUpdate" : "Update");
                Check(!hook.IsHookActive && !player.IsGreenHookActive && !player.IsControlLocked,
                    label + " sustained nine-cell separation releases once");
                if (kind == 3) Check(player.Motor.linearVelocity.y > 1.9f, label + " range release preserves swing momentum");
                Check(!(obj.GetComponent<IGreenHookMotion>() is GreenHookBodyMotion baseMotion) || !baseMotion.IsHookMoving,
                    label + " base physics ownership released");
                Invoke(hook, "FixedUpdate"); Invoke(hook, "Update");
                Check(player.ControlLockDepth == 0, label + " repeated ticks cannot double-release");
                Object.DestroyImmediate(player.gameObject); Object.DestroyImmediate(obj);
            }
        }

        private static void VerifySwingRangeBoundary()
        {
            Vector3 origin = new Vector3(1500, 50, 0);
            var player = Player(origin);
            var obj = Instantiate("Assets/_Project/Content/ContactCubes/Green_ContactCube.prefab", origin + Vector3.up * 5f);
            obj.GetComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezeAll;
            var hook = Attach(player, obj.GetComponent<IGreenHookTarget>());
            hook.DriveInput(Vector2.right, false); Step(hook, player, 650);
            Check(hook.IsSwinging && player.ControlLockDepth == 1, "maximum-length swing does not detach from rounding");
            Check(Vector3.Distance(player.Motor.position, obj.transform.position) <= 5.001f,
                "maximum-length swing stays within range");
            var externalLock = player.AcquireControlLock(new object());
            obj.transform.position = obj.GetComponent<Rigidbody>().position = player.Motor.position + Vector3.right * 9f;
            Physics.SyncTransforms(); Invoke(hook, "FixedUpdate");
            Check(hook.IsHookActive, "external lock does not remove range grace");
            Set(hook, "outOfRangeSince", Time.time - .41f); Invoke(hook, "FixedUpdate");
            Check(!hook.IsHookActive && player.ControlLockDepth == 1 && player.Motor.linearVelocity.sqrMagnitude < .0001f,
                "range cleanup respects external control lock");
            externalLock.Dispose();
            Object.DestroyImmediate(player.gameObject); Object.DestroyImmediate(obj);
        }

        private static void VerifyPlantSwingAnchors()
        {
            string[] paths = { "Green_Ladder.prefab", "Green_JumpPlant.prefab" };
            for (int index = 0; index < paths.Length; index++)
            {
                Vector3 origin = new Vector3(1600 + index * 20, 50, 0);
                var player = Player(origin);
                var obj = Instantiate("Assets/_Project/Content/ColorBlocks/Prefabs/" + paths[index], origin + Vector3.up * 3f);
                var anchor = obj.GetComponent<IGreenHookTarget>();
                Check(anchor != null && anchor.CanBeGreenHooked(player.gameObject) && anchor.GreenHookMotionMode == GreenHookMotionMode.Swing,
                    "plant is an unlocked swing anchor " + index);
                var camera = new GameObject("ImmediateSwingCamera").AddComponent<Camera>();
                camera.tag = "MainCamera"; camera.orthographic = true; camera.orthographicSize = 7f;
                camera.transform.position = origin + new Vector3(0, 1, -10);
                Physics.SyncTransforms();
                var hook = player.GetComponent<GreenHookAbility>();
                Check(hook.TryLaunch(camera.WorldToScreenPoint(obj.transform.position), true) == GreenHookLaunchResult.Success &&
                    hook.IsSwinging && player.CanDriveGreenHook && player.ControlLockDepth == 1,
                    "plant click starts swinging in same call " + index);
                Vector3 anchorStart = obj.transform.position;
                hook.DriveInput(Vector2.right, false); Step(hook, player, 650);
                Check(hook.IsSwinging && player.ControlLockDepth == 1 && (obj.transform.position - anchorStart).sqrMagnitude < .001f,
                    "plant swing persists without moving anchor " + index);
                hook.DriveInput(Vector2.zero, true);
                Check(!player.IsControlLocked && !player.IsGreenHookActive, "plant swing releases " + index);
                Object.DestroyImmediate(player.gameObject); Object.DestroyImmediate(obj); Object.DestroyImmediate(camera.gameObject);
            }
        }

        private static void VerifyAimingAndFourAxisPull()
        {
            var camera = new GameObject("HookTestCamera").AddComponent<Camera>();
            camera.tag = "MainCamera"; camera.orthographic = true; camera.orthographicSize = 7f;
            string[] paths = { "ContactCubes/Red_ContactCube.prefab", "ContactCubes/Blue_ContactCube.prefab",
                "ColorBlocks/Prefabs/Red_Foundation.prefab" };
            for (int kind = 0; kind < paths.Length; kind++)
            {
                Vector3 origin = new Vector3(500 + kind * 20, 30, 0);
                var player = Player(origin + Vector3.up * .5f);
                var obj = Instantiate("Assets/_Project/Content/" + paths[kind], origin + Vector3.right * 3f);
                var hook = player.GetComponent<GreenHookAbility>();
                camera.transform.position = origin + new Vector3(1, 1, -10);
                var floor = Solid("Floor", origin + new Vector3(1, -1, 0), new Vector3(12, 1, 2));
                Physics.SyncTransforms();
                Vector2 pointer = camera.WorldToScreenPoint(obj.transform.position);
                var result = hook.TryLaunch(pointer, true);
                Check(result == GreenHookLaunchResult.Success && hook.IsPulling, "real screen click immediately attaches " + kind);
                Check(player.CanDriveGreenHook && player.ControlLockDepth == 1, "same-frame click owns exactly one motor lock " + kind);
                Vector3 source = player.GetComponent<InteractionLinkEffect>().SourceWorldPosition;
                var resolve = (Vector3)Invoke(hook, "ResolveSourcePoint");
                Check((resolve - source).sqrMagnitude < .00001f, "same source as red and blue " + kind);
                Vector2 direction = (Vector2)typeof(GreenHookAbility).GetField("hookDirection", Private).GetValue(hook);
                Check(Mathf.Abs(direction.x) > .1f && Mathf.Abs(direction.y) > .1f, "visual direction is not snapped " + kind);
                Check(hook.IsPulling && player.ControlLockDepth == 1, "off-axis target actually attaches " + kind);
                Vector3 start = obj.GetComponent<Rigidbody>().position;
                Step(hook, player, 5);
                Vector3 velocity = obj.GetComponent<Rigidbody>().linearVelocity;
                Check(obj.GetComponent<Rigidbody>().position.x < start.x - .3f, "off-axis real launch moves object " + kind);
                Check(Mathf.Abs(velocity.x) < .001f || Mathf.Abs(velocity.y) < .001f, "pull velocity is cardinal " + kind);
                hook.CancelHook(); Check(!player.IsControlLocked, "cancel releases only hook lock " + kind);
                Object.DestroyImmediate(player.gameObject); Object.DestroyImmediate(obj); Object.DestroyImmediate(floor);
            }
            // Real clicks in all quadrants, not a private BeginPulling shortcut.
            for (int index = 0; index < 8; index++)
            {
                Vector3 origin = new Vector3(580 + index * 20, 40, 0);
                float angle = (22.5f + index * 45f) * Mathf.Deg2Rad;
                var player = Player(origin);
                var obj = Instantiate("Assets/_Project/Content/ContactCubes/Red_ContactCube.prefab",
                    origin + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * 3f);
                var hook = player.GetComponent<GreenHookAbility>();
                camera.transform.position = origin + new Vector3(0, 0, -10); Physics.SyncTransforms();
                Check(hook.TryLaunch(camera.WorldToScreenPoint(obj.transform.position), true) == GreenHookLaunchResult.Success,
                    "free angle launch " + index);
                Check(hook.IsPulling && player.CanDriveGreenHook, "free angle reaches clicked collider " + index);
                Step(hook, player, 1);
                Vector3 velocity = obj.GetComponent<Rigidbody>().linearVelocity;
                Vector3 command = velocity - Physics.gravity * Time.fixedDeltaTime;
                Check(velocity.sqrMagnitude > .1f && (Mathf.Abs(command.x) < .001f || Mathf.Abs(command.y) < .001f),
                    "free angle command remains cardinal while gravity is independent " + index);
                hook.CancelHook(); Object.DestroyImmediate(player.gameObject); Object.DestroyImmediate(obj);
            }
            Object.DestroyImmediate(camera.gameObject);
        }

        private static void VerifySwingRetargetInput()
        {
            Vector3 origin = new Vector3(800, 50, 0);
            var player = Player(origin);
            var first = Instantiate("Assets/_Project/Content/ContactCubes/Green_ContactCube.prefab", origin + Vector3.up * 4f)
                .GetComponent<ContactCubeMotion>();
            var second = Instantiate("Assets/_Project/Content/ContactCubes/Green_ContactCube.prefab", origin + new Vector3(2, 3, 0))
                .GetComponent<ContactCubeMotion>();
            first.GetComponent<Rigidbody>().constraints = second.GetComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezeAll;
            var camera = new GameObject("HookTestCamera").AddComponent<Camera>();
            camera.tag = "MainCamera"; camera.orthographic = true; camera.orthographicSize = 7f;
            camera.transform.position = origin + new Vector3(0, 1, -10);
            player.Motor.linearVelocity = Vector3.right * 5f;
            var hook = Attach(player, first);
            Check(Mathf.Abs(player.Motor.linearVelocity.x - 5f) < .01f, "initial swing preserves incoming momentum");
            hook.DriveInput(Vector2.right, false); Step(hook, player, 5);
            Check(player.Motor.linearVelocity.x > 5f, "tangential input pumps swing");
            var wheel = player.GetComponent<PlayerColorWheel>(); Check(wheel.TrySelectColor("green"), "green ability selected");
            var input = new HookInput { Click = true, Pointer = camera.WorldToScreenPoint(second.transform.position) };
            Vector3 before = player.Motor.position;
            GameInput.UseExternalSource(input);
            try
            {
                player.GetComponent<PlayerInputDriver>().Drive(player);
                Transform actual = (Transform)typeof(GreenHookAbility).GetField("targetTransform", Private).GetValue(hook);
                Check(actual == second.transform && hook.IsSwinging, "existing player input retargets without reopening wheel");
                Check(player.ControlLockDepth == 1 && (player.Motor.position - before).sqrMagnitude < .00001f,
                    "retarget neither stacks locks nor teleports player");
                Check(player.Motor.linearVelocity.sqrMagnitude > 1f, "retarget does not zero momentum");
                Check(wheel.IsSelected("green"), "retarget retains selected ability");
                Check(hook.TryLaunch(camera.WorldToScreenPoint(origin + new Vector3(20, 20, 0)), true) == GreenHookLaunchResult.OutOfRange
                    && hook.IsSwinging, "invalid new target preserves current rope");
                var extra = player.AcquireControlLock(new object());
                Check(hook.TryLaunch(input.Pointer, true) == GreenHookLaunchResult.Ignored, "retarget respects external timeline lock");
                Step(hook, player, 1); Check(player.Motor.linearVelocity.sqrMagnitude < .001f, "timeline pauses swing");
                extra.Dispose(); input.Click = false; input.Move = Vector2.right;
                player.GetComponent<PlayerInputDriver>().Drive(player); Step(hook, player, 20);
                Check(player.Motor.linearVelocity.sqrMagnitude > .1f && hook.IsSwinging, "swing resumes after external lock");
                Vector3 velocity = player.Motor.linearVelocity;
                input.Release = true; player.GetComponent<PlayerInputDriver>().Drive(player);
                Check(!hook.IsHookActive && !player.IsControlLocked, "existing interact input detaches");
                Check((player.Motor.linearVelocity - velocity).sqrMagnitude < .001f, "manual release preserves resolved momentum");
            }
            finally { GameInput.ClearExternalSource(input); }
            Object.DestroyImmediate(player.gameObject); Object.DestroyImmediate(first.gameObject);
            Object.DestroyImmediate(second.gameObject); Object.DestroyImmediate(camera.gameObject);
        }

        private static void VerifySwingSafetyAndRapidRetarget()
        {
            Vector3 origin = new Vector3(900, 50, 0);
            var player = Player(origin);
            var first = Instantiate("Assets/_Project/Content/ContactCubes/Green_ContactCube.prefab", origin + Vector3.up * 4f)
                .GetComponent<ContactCubeMotion>();
            var second = Instantiate("Assets/_Project/Content/ContactCubes/Green_ContactCube.prefab", origin + new Vector3(2, 3, 0))
                .GetComponent<ContactCubeMotion>();
            first.GetComponent<Rigidbody>().constraints = second.GetComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezeAll;
            var camera = new GameObject("HookSafetyCamera").AddComponent<Camera>();
            camera.tag = "MainCamera"; camera.orthographic = true; camera.orthographicSize = 7f;
            camera.transform.position = origin + new Vector3(0, 1, -10);
            Physics.SyncTransforms();
            var hook = Attach(player, first);
            Invoke(hook, "UpdatePullingVisual");
            Check(hook.TryLaunch(camera.WorldToScreenPoint(second.transform.position), true) == GreenHookLaunchResult.Success,
                "rapid switch first anchor accepted");
            Invoke(hook, "UpdatePullingVisual");
            var line = (LineRenderer)typeof(GreenHookAbility).GetField("line", Private).GetValue(hook);
            Vector3 visibleTip = line.GetPosition(line.positionCount - 1);
            Check(hook.TryLaunch(camera.WorldToScreenPoint(first.transform.position), true) == GreenHookLaunchResult.Success,
                "rapid switch can interrupt blend");
            Vector3 blendStart = (Vector3)typeof(GreenHookAbility).GetField("retargetVisualStart", Private).GetValue(hook);
            Check(((Vector2)(blendStart - visibleTip)).sqrMagnitude < .00001f && player.ControlLockDepth == 1,
                "interrupted blend starts at visible tip with one handle");
            Check(hook.TryLaunch(camera.WorldToScreenPoint(first.transform.position + Vector3.right * .2f), true) == GreenHookLaunchResult.Success,
                "same object accepts a new local anchor");
            Time.timeScale = 0f;
            try
            {
                Check(hook.TryLaunch(camera.WorldToScreenPoint(second.transform.position), true) == GreenHookLaunchResult.Ignored && hook.IsSwinging,
                    "pause preserves existing anchor");
            }
            finally { Time.timeScale = 1f; }
            var wall = Solid("SwingWall", origin + Vector3.right * 1.5f, new Vector3(.3f, 20f, 2f));
            Physics.SyncTransforms(); hook.DriveInput(Vector2.right, false); Step(hook, player, 300);
            Check(player.Motor.position.x < origin.x + 1.35f && hook.IsSwinging,
                "swing sweep cannot pass through a wall");
            var extra = player.AcquireControlLock(new object());
            first.SuspendMotion(true); Invoke(hook, "Update");
            Check(!hook.IsHookActive && !player.IsGreenHookActive && player.ControlLockDepth == 1,
                "invalid swing target cancels only its own control handle");
            extra.Dispose();
            Object.DestroyImmediate(wall); Object.DestroyImmediate(player.gameObject);
            Object.DestroyImmediate(first.gameObject); Object.DestroyImmediate(second.gameObject); Object.DestroyImmediate(camera.gameObject);
        }

        private static void VerifyExtendingTargetInvalidation()
        {
            Vector3 origin = new Vector3(1000, 50, 0);
            var player = Player(origin);
            var cube = Instantiate("Assets/_Project/Content/ContactCubes/Red_ContactCube.prefab", origin + Vector3.right * 3f)
                .GetComponent<ContactCubeMotion>();
            var hook = player.GetComponent<GreenHookAbility>();
            player.Motor.linearVelocity = Vector3.right * 4f;
            Invoke(hook, "BeginHook", cube, cube.transform.position);
            cube.SuspendMotion(true); Invoke(hook, "Update");
            Check(!hook.IsHookActive && !player.IsControlLocked && !player.IsGreenHookActive,
                "target becoming carried during flight cancels before attachment");
            Check(cube.IsSuspended && cube.GetComponent<Rigidbody>().isKinematic,
                "cancelled flight does not steal carried target physics");
            Check(Mathf.Abs(player.Motor.linearVelocity.x - 4f) < .001f,
                "cancelled flight preserves normal movement");
            Object.DestroyImmediate(player.gameObject); Object.DestroyImmediate(cube.gameObject);
        }

        private sealed class HookInput : IInputSource
        {
            public bool Click, Release;
            public InputActionId ReleaseAction = InputActionId.Interact;
            public Vector2 Pointer, Move;
            public InputDeviceMode ActiveDeviceMode => InputDeviceMode.KeyboardMouse;
            public bool IsActionPressed(InputActionId action) => false;
            public bool WasActionPressedThisFrame(InputActionId action) => WasActionTriggeredThisFrame(action);
            public bool WasActionTriggeredThisFrame(InputActionId action) => action == InputActionId.PointerPrimary && Click ||
                action == ReleaseAction && Release;
            public bool WasActionReleasedThisFrame(InputActionId action) => false;
            public InputActionTrigger GetActionTrigger(InputActionId action) => InputActionTrigger.Press;
            public float ReadAxis(InputActionId action) => 0;
            public Vector2 ReadVector2(InputActionId action) => action == InputActionId.Move ? Move : Vector2.zero;
            public Vector2 PointerPosition => Pointer;
            public Vector2 PointerDelta => Vector2.zero;
            public bool HasKeyboard => true;
            public bool HasMouse => true;
            public bool HasGamepad => false;
            public bool HasTouch => false;
        }
        private static void VerifyBaseAndPush()
        {
            var player = Player(new Vector3(300,20,0));
            var foundation = Instantiate("Assets/_Project/Content/ColorBlocks/Prefabs/Red_Foundation.prefab",new Vector3(305,20,0));
            var feature=foundation.GetComponent<MechanismBaseFeature>();
            Check(feature != null && feature.GreenHookMotionMode == GreenHookMotionMode.PullTarget,"base contract target-first");
            var motion=foundation.GetComponent<GreenHookBodyMotion>();
            var body = foundation.GetComponent<Rigidbody>();
            bool originalKinematic = body.isKinematic, originalGravity = body.useGravity;
            var originalConstraints = body.constraints;
            Check(motion != null, "existing base gains hook motion");
            var hook=Attach(player,feature); Step(hook,player,5);
            Check(foundation.GetComponent<Rigidbody>().position.x < 304.5f && motion.IsHookMoving,"base actually pulled");
            player.UpdateGreenHookInput(Vector2.zero,true);
            Check(!motion.IsHookMoving && body.isKinematic == originalKinematic && body.useGravity == originalGravity &&
                body.constraints == originalConstraints, "base releases physics ownership and restores author gravity settings");
            Object.DestroyImmediate(foundation); Object.DestroyImmediate(player.gameObject);
            for(int index=0;index<4;index++)
            {
                Vector3 start=new Vector3(330+index*15,20,0);
                var floor=Solid("PushFloor",start+new Vector3(2,-1.0f,0),new Vector3(20,1,2));
                GameObject mover=index==3 ? Instantiate("Assets/_Project/Content/ColorBlocks/Prefabs/Red_Foundation.prefab",start) :
                    Instantiate("Assets/_Project/Content/ContactCubes/"+(CubeColor)index+"_ContactCube.prefab",start);
                var item=Instantiate(WorldItemSetup.Folder+"/Collectible.prefab",start+Vector3.right*.9f);
                var move=mover.GetComponent<IGreenHookMotion>(); move.BeginGreenHookPull(); Physics.SyncTransforms();
                for(int step=0;step<30;step++) { move.SetGreenHookVelocity(Vector3.right*3); Physics.Simulate(.02f); }
                Check(item.GetComponent<Rigidbody>().position.x>start.x+1.5f,"physical push pickup "+index);
                move.EndGreenHookPull(false,Vector3.zero,0);
                Object.DestroyImmediate(mover); Object.DestroyImmediate(item); Object.DestroyImmediate(floor);
            }
            var palette=AssetDatabase.LoadAssetAtPath<Project.LevelEditor.LevelEditorPalette>("Assets/_Project/Development/LevelEditor/Orpheus0829/LevelEditorPalette.asset");
            var props=palette.PropEntries.Where(p=>p.Prefab!=null && AssetDatabase.GetAssetPath(p.Prefab).StartsWith(WorldItemSetup.Folder)).ToArray();
            Check(props.Length==5 && props.Select(p=>p.EntryId).Distinct().Count()==5,"five editor props with persistent unique IDs");
        }
        private static void VerifyRoomBoundary()
        {
            var player=Player(new Vector3(400.5f,20.5f,0));
            var cube=Instantiate("Assets/_Project/Content/ContactCubes/Red_ContactCube.prefab",new Vector3(405.5f,20.5f,0)).GetComponent<ContactCubeMotion>();
            var obj=new GameObject("RoomManager"); var manager=obj.AddComponent<Project.Rooms.RoomLifecycleManager>();
            var cells=new List<Vector2Int>();
            for(int x=399;x<410;x++) for(int y=-24;y<-18;y++) cells.Add(new Vector2Int(x,y));
            manager.Configure(new List<Project.Rooms.RoomRegion>{new Project.Rooms.RoomRegion{id="room",cells=cells}},1f,"");
            var hook=Attach(player,cube); Step(hook,player,2); Check(hook.IsHookActive,"hook allowed within room");
            player.Motor.position=new Vector3(430,20,0); Physics.SyncTransforms(); Step(hook,player,1);
            Check(!hook.IsHookActive && !player.IsGreenHookActive,"room exit cancels and releases ownership");
            Object.DestroyImmediate(player.gameObject); Object.DestroyImmediate(cube.gameObject); Object.DestroyImmediate(obj);
        }
        private static void VerifyItems()
        {
            var player = Player(new Vector3(200,20,0));
            var obj = Instantiate(WorldItemSetup.Folder + "/Collectible.prefab", new Vector3(205,20,0));
            var item = obj.GetComponent<CollectiblePickup>(); var body = obj.GetComponent<Rigidbody>();
            Check(item is DropItemBase && item.CanBePushed,"pushable shared pickup lifecycle");
            body.position += Vector3.right; item.GetComponent<Project.Rooms.RoomResetObject>().RestorePose(); item.ResetForRoom();
            Check(Mathf.Abs(body.position.x-205)<.001f && item.CanCollect,"collectible reset returns to author position");
            int collected=0; Action<CollectiblePickup,GameObject> listener=(pickup,actor)=>collected++;
            CollectiblePickup.Collected += listener;
            Invoke(item,"OnTriggerEnter",player.GetComponent<Collider>());
            Check(item.CollectionRequested && !item.CollectionFinished && obj.activeSelf,"collection waits for flight instead of early hide");
            item.ResetForRoom();
            Check(item.CanCollect && collected==0 && !obj.GetComponent<Project.Pickups.PickupFlightAnimator>().IsPlaying,"reset cancels pending pickup and reward");
            CollectiblePickup.Collected -= listener;
            Object.DestroyImmediate(obj);
            foreach (string direction in new[]{"上","下","左","右"})
            {
                var spike=Instantiate(WorldItemSetup.Folder+"/Spike_"+direction+".prefab",new Vector3(210,20,0));
                Check(spike.GetComponentInChildren<MeshCollider>().isTrigger && spike.GetComponent<ColorObject>()==null,"neutral spike trigger "+direction);
                Check(spike.GetComponentInChildren<MeshCollider>().bounds.size.x > 0,"spike oriented geometry "+direction);
                Object.DestroyImmediate(spike);
            }
            var hazard = new GameObject("hazard").AddComponent<NeutralSpike>();
            Set(player,"invulnerableUntil",Time.time+100);
            Invoke(hazard,"OnTriggerEnter",player.GetComponent<Collider>());
            Check(!player.IsDead,"spikes respect revival protection");
            Set(player,"invulnerableUntil",0f);
            Invoke(hazard,"OnTriggerEnter",player.GetComponent<Collider>());
            Check(player.IsDead,"spike touch enters normal death flow");
            Object.DestroyImmediate(player.gameObject); Object.DestroyImmediate(hazard.gameObject);
        }
        private static void Finish(bool success,string result)
        {
            File.WriteAllText(Path.Combine(Application.dataPath,"../Library/GreenHookVerification.txt"),result);
            SessionState.SetBool("GreenHookVerification",false); EditorApplication.Exit(success?0:1);
        }

        public static void RunEditor()
        {
            if (!Application.dataPath.Contains("FlowRetirementProbe")) throw new Exception("Isolated verification only");
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
                WorldItemSetup.CreateMissing();
                var palette=LevelEditorPaletteService.GetOrCreate();
                var ids=palette.PropEntries.Select(entry=>entry.EntryId).ToArray();
                WorldItemSetup.CreateMissing();
                Check(ids.SequenceEqual(palette.PropEntries.Select(entry=>entry.EntryId)),"setup is idempotent and preserves existing IDs");
                var props=palette.PropEntries.Where(entry=>entry.Prefab!=null && AssetDatabase.GetAssetPath(entry.Prefab).StartsWith(WorldItemSetup.Folder)).ToArray();
                var doc=new PlanningDocument {name="WorldItemsProbe"}; var room=new PlanningRoom("Items"); doc.rooms.Add(room);
                for(int i=0;i<5;i++)
                {
                    room.cells.Add(new PlanningCell(i,0));
                    room.boxes.Add(new PlanningBox {id="prop"+i,type=PlanningDetailType.Prop,propEntryId=props[i].EntryId,propEntryName=props[i].DisplayName,x=i,y=0});
                    var editor=UnityEditor.Editor.CreateEditor(props[i].Prefab.GetComponent<CollectiblePickup>() as Component ?? props[i].Prefab.GetComponent<NeutralSpike>());
                    Check(editor.CreateInspectorGUI()!=null,"UI Toolkit inspector "+props[i].DisplayName); Object.DestroyImmediate(editor);
                }
                var water=palette.PropEntries.First(entry=>entry.Prefab!=null && entry.Prefab.GetComponent<WaterVisualFeature>()!=null);
                room.boxes.Add(new PlanningBox{id="water",type=PlanningDetailType.Prop,propEntryId=water.EntryId,propEntryName=water.DisplayName,x=0,y=0,width=5,height=1,isMerged=true});
                PlanningSceneBuilder.SuppressSceneUndo=true;
                Check(PlanningSceneBuilder.TryApplyDocument(doc,out var message),"new props apply in water: "+message);
                var root=GameObject.Find("__PlanningMapGenerated");
                Check(root.GetComponentsInChildren<CollectiblePickup>(true).Length==1 && root.GetComponentsInChildren<NeutralSpike>(true).Length==4,"all five props instantiated");
                var old=root.GetComponentsInChildren<LevelEditorPlacedBlock>(true).ToDictionary(item=>item.PlanningBoxId,item=>item.GetInstanceID());
                Check(PlanningSceneBuilder.TryApplyDocument(doc,out _) && root.GetComponentsInChildren<LevelEditorPlacedBlock>(true).All(item=>old[item.PlanningBoxId]==item.GetInstanceID()),"incremental sync retains prop identities");
                PlanningSaveStore.Save("Library/WorldItemsProbe.planning.json",doc);
                var saved=PlanningSaveStore.Read("Library/WorldItemsProbe.planning.json");
                Check(saved.ToPersistentJson()==doc.ToPersistentJson(),"new props save and reload");
                var fragment=PlanningFragmentUtility.Extract(doc,new[]{room.id},null,out _);
                var imported=PlanningFragmentUtility.Import(new PlanningDocument(),fragment,new Vector2Int(10,0),false,out _);
                Check(imported.rooms[0].boxes.Count==6 && imported.rooms[0].boxes.Any(box=>box.propEntryId==props[0].EntryId),"fragment import retains all props and water");
                PlanningSceneBuilder.SuppressSceneUndo=false;
                Finish(true,"EDITOR PASS "+checks);
            }
            catch(Exception error) { Debug.LogException(error); Finish(false,error.ToString()); }
        }
    }
}
