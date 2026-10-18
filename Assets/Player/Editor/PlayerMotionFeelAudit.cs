using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Project.BlockFeatures;
using Project.ColorBlocks;
using Project.ContactCubes;
using Project.GreenHook;
using Project.InputAbstraction;
using Project.Mechanisms;
using Unity.Profiling;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Project.Player.Editor
{
    /// <summary>Real PhysX regressions for retained-hook walking and automatic climb ownership.</summary>
    public static class PlayerMotionFeelAudit
    {
        private const string SessionKey = "PlayerMotionFeelAudit";
        private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
        private static readonly List<GameObject> objects = new List<GameObject>();
        private static int checks;

        public static void Run()
        {
            if (!Application.dataPath.Contains("FlowRetirementProbe") ||
                !Environment.GetCommandLineArgs().Contains("-noaudio"))
                throw new InvalidOperationException("Run only in the silent isolated probe project.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            SessionState.SetBool(SessionKey, true);
            Resume();
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!SessionState.GetBool(SessionKey, false)) return;
            EditorApplication.update -= Wait;
            EditorApplication.update += Wait;
        }

        private static void Wait()
        {
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.time < .6f) return;
            EditorApplication.update -= Wait;
            SimulationMode previous = Physics.simulationMode;
            bool passed = false;
            string report;
            try
            {
                Physics.simulationMode = SimulationMode.Script;
                ColorRuntimeService.Instance.SetAllUnlockedForCurrentSession(true);
                ColorRuntimeService.Instance.UnlockAllAbilitiesForCurrentSession();
                VerifyAttachedWalking();
                ClearObjects();
                VerifyClimbOwnershipAndRelease();
                ClearObjects();
                VerifyJumpFromExpiredSurface();
                ClearObjects();
                VerifyClimbSourcesRecoverInPlace();
                ClearObjects();
                VerifyGroundedJumpReleaseInput();
                ClearObjects();
                VerifyClimbQueryGates();
                ClearObjects();
                VerifyPlayerActivation();
                passed = true;
                report = "PASS " + checks;
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                report = "FAIL " + checks + " " + error;
            }
            finally
            {
                ClearObjects();
                Physics.simulationMode = previous;
            }
            File.WriteAllText(Path.Combine(Application.dataPath, "../Library/PlayerMotionFeelAudit.txt"), report);
            Debug.Log("PLAYER MOTION FEEL " + report);
            SessionState.EraseBool(SessionKey);
            EditorApplication.Exit(passed ? 0 : 1);
        }

        private static void Check(bool condition, string label)
        {
            checks++;
            if (!condition) throw new Exception(label);
            Debug.Log("MOTION PASS " + label);
        }

        private static object Invoke(object target, string method, params object[] arguments) =>
            target.GetType().GetMethod(method, Private).Invoke(target, arguments);
        private static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, Private).SetValue(target, value);

        private static GameObject Prefab(string path, Vector3 position)
        {
            GameObject instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path), position, Quaternion.identity);
            objects.Add(instance);
            return instance;
        }

        private static GameObject Solid(string name, Vector3 position, Vector3 size)
        {
            GameObject shape = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shape.name = name;
            shape.transform.position = position;
            shape.transform.localScale = size;
            objects.Add(shape);
            return shape;
        }

        private static PlayerController Player(Vector3 position)
        {
            PlayerController player = Prefab("Assets/_Project/Content/Player/Player.prefab", position).GetComponent<PlayerController>();
            player.GetComponent<PlayerInputDriver>().enabled = false;
            player.Motor.interpolation = RigidbodyInterpolation.None;
            return player;
        }

        private static GreenHookAbility Attach(PlayerController player, IGreenHookTarget target)
        {
            GreenHookAbility hook = player.GetComponent<GreenHookAbility>();
            Invoke(hook, "BeginHook", target, target.GreenHookTransform.position);
            Invoke(hook, "BeginPulling");
            Physics.SyncTransforms();
            return hook;
        }

        private static void Step(GreenHookAbility hook, PlayerController player, int count)
        {
            for (int step = 0; step < count; step++)
            {
                if (hook != null) Invoke(hook, "FixedUpdate");
                Invoke(player, "FixedUpdate");
                Physics.Simulate(Time.fixedDeltaTime);
            }
        }

        private static void VerifyAttachedWalking()
        {
            Solid("Walking ground", new Vector3(0f, -.5f, 0f), new Vector3(40f, 1f, 2f));
            PlayerController player = Player(new Vector3(0f, .5f, 0f));
            ContactCubeMotion cube = Prefab("Assets/_Project/Content/ContactCubes/Red_ContactCube.prefab", new Vector3(4f, .5f, 0f)).GetComponent<ContactCubeMotion>();
            Physics.SyncTransforms();
            GreenHookAbility hook = Attach(player, cube);
            Step(hook, player, 1);
            Check(player.IsGreenHookLocomotionAllowed, "target-first pull delegates ordinary walking without releasing the hook");
            Check(player.ControlLockDepth == 1 && player.CanDriveGreenHook, "delegated walking retains exactly the hook control handle");
            player.UpdateGreenHookInput(Vector2.left, false);
            float start = player.Motor.position.x;
            Step(hook, player, 8);
            Check(player.Motor.position.x < start - .1f, "movement input moves the player while pulling the target");
            Check(hook.IsHookActive && player.IsGreenHookActive, "walking does not automatically detach");
            player.UpdateGreenHookInput(Vector2.zero, false);
            Step(hook, player, 90);
            Check(player.IsGreenHookLocomotionAllowed && hook.IsHookActive, "contact waiting keeps movement available instead of permanent zero velocity");
            start = player.Motor.position.x;
            player.UpdateGreenHookInput(Vector2.left, false);
            Step(hook, player, 8);
            Check(player.Motor.position.x < start - .1f, "walking resumes immediately after contact without a jump release");
            IDisposable higherPriority = player.AcquireControlLock(new object());
            player.SetGreenHookLocomotion(hook, true);
            player.SetMoveInput(Vector2.right);
            Step(hook, player, 1);
            Check(!player.IsGreenHookLocomotionAllowed && !player.CanDriveGreenHook && player.Motor.linearVelocity.sqrMagnitude < .001f,
                "an additional Timeline-style lock still owns the motor");
            higherPriority.Dispose();
            Step(hook, player, 1);
            Check(player.CanDriveGreenHook && hook.IsHookActive, "releasing the extra lock resumes the same connection");
            hook.DriveInput(Vector2.zero, true);
            Check(!player.IsControlLocked && !player.IsGreenHookActive, "manual release disposes the original control handle once");
        }

        private static void VerifyClimbOwnershipAndRelease()
        {
            PlayerController player = Player(new Vector3(30f, 3f, 0f));
            ContactCubeMotion green = Prefab("Assets/_Project/Content/ContactCubes/Green_ContactCube.prefab", new Vector3(33f, 3f, 0f)).GetComponent<ContactCubeMotion>();
            green.GetComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezeAll;
            Component surface = green;
            player.EnterClimb(surface);
            player.SetHorizontalVineSurface(surface, 3f, 2f);
            GreenHookAbility hook = Attach(player, green);
            Check(hook.IsSwinging && !player.IsClimbing && !player.IsHorizontalClimbing, "starting a swing clears previous vertical and horizontal climb ownership");
            Check(player.CurrentStateId != PlayerStateId.Locked, "a sole rope motor is not mistaken for a Timeline animation freeze");
            for (int index = 0; index < 24; index++)
            {
                player.EnterClimb(surface);
                player.SetHorizontalVineSurface(surface, player.Motor.position.y, 2f);
                player.ProtectClimbCorner(surface, green.GetComponentInChildren<Collider>().bounds, 2f);
                Step(hook, player, 1);
            }
            Check(!player.IsClimbing && !player.IsHorizontalClimbing && !player.IsClimbCornerProtected,
                "repeated real-surface callbacks cannot steal the swing motor or renew climb leases");
            Check(hook.IsSwinging && player.CanDriveGreenHook, "surface contact does not detach or add control handles");
            player.QueuePlatformDelta(Vector3.right * 20f);
            Check(((Vector3)typeof(PlayerController).GetField("pendingPlatformDelta", Private).GetValue(player)).sqrMagnitude < .0001f,
                "a rope-controlled player does not accumulate a stale platform teleport");
            player.Motor.linearVelocity = new Vector3(6f, 2f, 0f);
            Vector3 beforeRelease = player.Motor.linearVelocity;
            hook.DriveInput(Vector2.zero, true);
            Vector3 released = player.Motor.linearVelocity;
            Check(released.x >= beforeRelease.x - .001f && released.y >= beforeRelease.y - .001f,
                "manual swing release retains the resolved direction and inertia");
            player.EnterClimb(surface);
            player.SetHorizontalVineSurface(surface, player.Motor.position.y, 2f);
            Check(!player.CanAutomaticallyClimb && !player.IsClimbing && !player.IsHorizontalClimbing && player.GreenHookClimbProtectionRemaining > .9f,
                "release grants one second of automatic-climb immunity");
            player.SetMoveInput(Vector2.zero);
            float releasedX = player.Motor.linearVelocity.x;
            Step(null, player, 1);
            Check(Mathf.Abs(player.Motor.linearVelocity.x - releasedX) < .001f,
                "ordinary deceleration does not erase swing inertia on the first released physics tick");
            player.SetMoveInput(Vector2.left);
            Step(null, player, 1);
            Check(player.Motor.linearVelocity.x < releasedX, "opposite movement still brakes released inertia deliberately");
            Set(player, "greenHookClimbBlockedUntil", Time.time + .05f);
            player.EnterClimb(surface);
            Check(!player.IsClimbing, "automatic climb remains blocked throughout the grace timestamp");
            Set(player, "greenHookClimbBlockedUntil", Time.time - .001f);
            player.EnterClimb(surface);
            Check(player.CanAutomaticallyClimb && player.IsClimbing, "normal surface climbing resumes after grace expiry");
        }

        private static void VerifyJumpFromExpiredSurface()
        {
            Solid("Jump ground", new Vector3(70f, -.5f, 0f), new Vector3(10f, 1f, 2f));
            PlayerController player = Player(new Vector3(70f, .5f, 0f));
            GameObject surface = Solid("Expired horizontal source", new Vector3(75f, 4f, 0f), Vector3.one);
            Physics.SyncTransforms();
            Step(null, player, 2);
            Check(player.IsGrounded, "jump fixture stands on a real solid floor");
            player.SetHorizontalVineSurface(surface.transform, player.Motor.position.y, 2f);
            IDictionary leases = (IDictionary)typeof(PlayerController).GetField("horizontalVines", Private).GetValue(player);
            object lease = leases[surface.transform.GetInstanceID()];
            lease.GetType().GetField("renewed").SetValue(lease, Time.fixedTime - .2f);
            leases[surface.transform.GetInstanceID()] = lease;
            Check(!player.IsHorizontalClimbing, "input state ignores an expired lease before the next cleanup FixedUpdate");
            player.RequestJump();
            player.SetJumpHeld(true);
            Check(player.JumpBufferRemaining > 0f, "an ordinary jump is buffered rather than consumed as stale horizontal release");
            Step(null, player, 1);
            Check(player.Motor.linearVelocity.y > 3f && !player.IsGrounded, "jump starts on the next motor tick without waiting for hold classification");
            Check(player.JumpBufferRemaining <= 0f, "jump buffer is consumed once by the motor");
        }

        private static void VerifyPlayerActivation()
        {
            int notifications = 0;
            Action<PlayerController> observer = player => notifications++;
            PlayerController.PlayerActivated += observer;
            try
            {
                PlayerController player = Player(new Vector3(100f, .5f, 0f));
                Check(notifications == 1 && PlayerController.ActivePlayers.Contains(player), "late-spawned player is registered before the activation callback");
                player.gameObject.SetActive(false);
                player.gameObject.SetActive(true);
                Check(notifications == 2, "reactivated player notifies collision subscribers without frame scans");
            }
            finally { PlayerController.PlayerActivated -= observer; }
        }

        private static void VerifyGroundedJumpReleaseInput()
        {
            ReleaseInput input = new ReleaseInput();
            GameInput.UseExternalSource(input);
            try
            {
                Solid("Release jump ground", new Vector3(160f, -.5f, 0f), new Vector3(20f, 1f, 2f));
                PlayerController player = Player(new Vector3(160f, .5f, 0f));
                ContactCubeMotion red = Prefab("Assets/_Project/Content/ContactCubes/Red_ContactCube.prefab",
                    new Vector3(164f, .5f, 0f)).GetComponent<ContactCubeMotion>();
                GreenHookAbility hook = Attach(player, red);
                Step(hook, player, 30);
                Check(player.IsGrounded && player.IsGreenHookActive, "release-jump input fixture is grounded and remains attached");
                player.GetComponent<PlayerInputDriver>().Drive(player);
                Check(!hook.IsHookActive && !player.IsGreenHookActive && player.JumpBufferRemaining > 0f,
                    "the real input driver detaches and buffers a grounded jump in the same call");
                Step(null, player, 1);
                Check(player.Motor.linearVelocity.y > 3f && !player.IsGrounded,
                    "the first grounded jump press launches without a second press after release");

                ClearObjects();
                player = Player(new Vector3(190f, 5f, 0f));
                ContactCubeMotion green = Prefab("Assets/_Project/Content/ContactCubes/Green_ContactCube.prefab",
                    new Vector3(193f, 5f, 0f)).GetComponent<ContactCubeMotion>();
                hook = Attach(player, green);
                player.Motor.linearVelocity = new Vector3(3f, 2f, 0f);
                player.GetComponent<PlayerInputDriver>().Drive(player);
                Check(!hook.IsHookActive && player.JumpBufferRemaining <= 0f && !player.JumpHeld,
                    "airborne jump input only releases, without injecting an extra air jump or jump hold");
                Check(player.Motor.linearVelocity.x >= 3f && player.Motor.linearVelocity.y >= 2f,
                    "airborne driver release retains the resolved directional velocity");
            }
            finally { GameInput.ClearExternalSource(input); }
        }

        private sealed class ReleaseInput : IInputSource
        {
            public InputDeviceMode ActiveDeviceMode => InputDeviceMode.KeyboardMouse;
            public bool IsActionPressed(InputActionId action) => action == InputActionId.Jump;
            public bool WasActionPressedThisFrame(InputActionId action) => action == InputActionId.Jump;
            public bool WasActionTriggeredThisFrame(InputActionId action) => action == InputActionId.Jump;
            public bool WasActionReleasedThisFrame(InputActionId action) => false;
            public InputActionTrigger GetActionTrigger(InputActionId action) => InputActionTrigger.Press;
            public float ReadAxis(InputActionId action) => 0f;
            public Vector2 ReadVector2(InputActionId action) => Vector2.zero;
            public Vector2 PointerPosition => Vector2.zero;
            public Vector2 PointerDelta => Vector2.zero;
            public bool HasKeyboard => true;
            public bool HasMouse => true;
            public bool HasGamepad => false;
            public bool HasTouch => false;
        }

        private static void VerifyClimbSourcesRecoverInPlace()
        {
            PlayerController player = Player(new Vector3(130f, 5f, 0f));
            ClimbableVineFeature vine = Prefab("Assets/_Project/Content/ColorBlocks/Prefabs/Green_Ladder.prefab",
                new Vector3(131f, 5f, 0f)).GetComponent<ClimbableVineFeature>();
            Physics.SyncTransforms();
            Invoke(vine, "RefreshClimbingPlayers");
            Check(player.IsClimbing, "real vine proximity initially registers climbing");
            GreenHookAbility hook = Attach(player, vine);
            Invoke(vine, "RefreshClimbingPlayers");
            IDictionary cachedPlayers = (IDictionary)typeof(ClimbableVineFeature).GetField("climbingPlayers", Private).GetValue(vine);
            Check(cachedPlayers.Count == 0 && !player.IsClimbing, "vine releases its own contact cache while the rope owns the player");
            hook.DriveInput(Vector2.zero, true);
            Invoke(vine, "RefreshClimbingPlayers");
            Check(cachedPlayers.Count == 0 && !player.IsClimbing, "vine does not cache a rejected registration during release grace");
            Set(player, "greenHookClimbBlockedUntil", Time.time - .001f);
            Invoke(vine, "RefreshClimbingPlayers");
            Check(cachedPlayers.Count == 1 && player.IsClimbing, "vine registers again in place after grace without leaving its range");

            NeutralLadder ladder = Prefab("Assets/_Project/Content/Mechanisms/LadderFront.prefab",
                new Vector3(130f, 5f, 0f)).GetComponent<NeutralLadder>();
            Collider body = player.GetComponent<CapsuleCollider>();
            Set(player, "greenHookClimbBlockedUntil", Time.time + 1f);
            player.ExitClimb(vine);
            Invoke(ladder, "Track", body);
            Check(!player.IsClimbing, "neutral ladder trigger stays inactive throughout rope grace");
            Set(player, "greenHookClimbBlockedUntil", Time.time - .001f);
            Invoke(ladder, "Track", body);
            Check(player.IsClimbing, "neutral ladder stay registers normally after grace");

            GameObject legacyObject = Solid("Legacy colored ladder", new Vector3(140f, 5f, 0f), Vector3.one);
            LadderFeature legacy = legacyObject.AddComponent<LadderFeature>();
            player.ExitClimb(ladder);
            Set(player, "greenHookClimbBlockedUntil", Time.time + 1f);
            Invoke(legacy, "OnPlayerEntered", player);
            Check(!player.IsClimbing, "legacy ladder enter obeys shared rope grace");
            Set(player, "greenHookClimbBlockedUntil", Time.time - .001f);
            Invoke(legacy, "OnPlayerStayed", player);
            Check(player.IsClimbing, "legacy ladder stay retries a blocked enter after grace");
        }

        private static void VerifyClimbQueryGates()
        {
            Check(!PlayerController.HasAutomaticClimbCandidate, "an empty player registry needs no climb overlap queries");
            PlayerController player = Player(new Vector3(230f, 5f, 0f));
            ClimbableVineFeature vine = Prefab("Assets/_Project/Content/ColorBlocks/Prefabs/Green_Ladder.prefab",
                new Vector3(231f, 5f, 0f)).GetComponent<ClimbableVineFeature>();
            Collider sentinel = Solid("Vine overlap sentinel outside every query", new Vector3(250f, 50f, 0f), Vector3.one).GetComponent<Collider>();
            Collider[] vineHits = (Collider[])typeof(ClimbableVineFeature).GetField("proximityBuffer", Private).GetValue(vine);
            IDictionary vinePlayers = (IDictionary)typeof(ClimbableVineFeature).GetField("climbingPlayers", Private).GetValue(vine);
            Physics.SyncTransforms();
            vineHits[0] = sentinel;
            Invoke(vine, "RefreshClimbingPlayers");
            Check(PlayerController.HasAutomaticClimbCandidate && vineHits[0] != sentinel && player.IsClimbing,
                "eligible players perform the real vine overlap and enter climbing");
            int candidates = 0;
            // Warm the property before measuring its concrete HashSet enumerator.
            for (int index = 0; index < 16; index++) if (PlayerController.HasAutomaticClimbCandidate) candidates++;
            // This Unity Mono reports zero even for explicit allocations through
            // GC.GetAllocatedBytesForCurrentThread; calibrate the profiler marker
            // positively before trusting a small/zero event count below.
            var allocationProbe = new byte[16][];
            using (var calibration = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc", 32768,
                ProfilerRecorderOptions.CollectOnlyOnCurrentThread | ProfilerRecorderOptions.WrapAroundWhenCapacityReached))
            {
                for (int index = 0; index < allocationProbe.Length; index++) allocationProbe[index] = new byte[4096];
                GC.KeepAlive(allocationProbe);
                calibration.Stop();
                Check(calibration.Valid && calibration.Count >= allocationProbe.Length,
                    "GC.Alloc positive calibration observes all 16 explicit array allocations; events=" + calibration.Count);
            }
            using (var allocation = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc", 32768,
                ProfilerRecorderOptions.CollectOnlyOnCurrentThread | ProfilerRecorderOptions.WrapAroundWhenCapacityReached))
            {
                for (int index = 0; index < 512; index++) if (PlayerController.HasAutomaticClimbCandidate) candidates++;
                allocation.Stop();
                Check(allocation.Valid && allocation.Count <= 8,
                    "warm registry queries have no per-call GC.Alloc across 512 lookups; events=" + allocation.Count);
            }
            Check(candidates == 528, "all 528 same-frame registry queries see the eligible player without world scans");

            GreenHookAbility hook = Attach(player, vine);
            vineHits[0] = sentinel;
            Invoke(vine, "RefreshClimbingPlayers");
            Check(!PlayerController.HasAutomaticClimbCandidate && vineHits[0] == sentinel && vinePlayers.Count == 0 &&
                !player.IsClimbing && !player.IsClimbCornerProtected,
                "a same-frame hook skips vine overlap but clears all old climb ownership");
            // OnTick must still advance the independent visual and water-contact work.
            Set(vine, "maxGrowthBlocks", 4);
            Set(vine, "nextWaterContactCheck", Time.time - .1f);
            Invoke(vine, "OnTick", .02f);
            Check((float)typeof(ClimbableVineFeature).GetField("nextWaterContactCheck", Private).GetValue(vine) > Time.time + .19f &&
                typeof(ClimbableVineFeature).GetField("sequenceRenderer", Private).GetValue(vine) != null,
                "no-candidate gating does not skip vine sequence rendering or water contact checks");
            hook.DriveInput(Vector2.zero, true);
            vineHits[0] = sentinel;
            Invoke(vine, "RefreshClimbingPlayers");
            Check(!PlayerController.HasAutomaticClimbCandidate && player.GreenHookClimbProtectionRemaining > .9f &&
                vineHits[0] == sentinel && vinePlayers.Count == 0,
                "release grace still skips vine overlap and leaves no rejected player cached");
            Set(player, "greenHookClimbBlockedUntil", Time.time - .001f);
            vineHits[0] = sentinel;
            Invoke(vine, "RefreshClimbingPlayers");
            Check(PlayerController.HasAutomaticClimbCandidate && vineHits[0] != sentinel && vinePlayers.Count == 1 && player.IsClimbing,
                "grace expiry immediately restores the vine query without a stale frame cache");

            PlayerController secondPlayer = Player(new Vector3(230f, 5.2f, 0f));
            hook = Attach(player, vine);
            Physics.SyncTransforms();
            vineHits[0] = sentinel;
            Invoke(vine, "RefreshClimbingPlayers");
            Check(PlayerController.HasAutomaticClimbCandidate && vineHits[0] != sentinel &&
                vinePlayers.Count == 1 && vinePlayers.Contains(secondPlayer.GetInstanceID()) && secondPlayer.IsClimbing && !player.IsClimbing,
                "one hooked player does not suppress a second eligible player's vine query");
            GreenHookAbility secondHook = Attach(secondPlayer, vine);
            vineHits[0] = sentinel;
            Invoke(vine, "RefreshClimbingPlayers");
            Check(!PlayerController.HasAutomaticClimbCandidate && vineHits[0] == sentinel && vinePlayers.Count == 0,
                "all players hooked skips the shared vine query and removes the remaining source lease");
            secondHook.DriveInput(Vector2.zero, true);
            Check(!PlayerController.HasAutomaticClimbCandidate, "a second player's immediate release still observes its own grace");
            Set(secondPlayer, "greenHookClimbBlockedUntil", Time.time - .001f);
            Check(PlayerController.HasAutomaticClimbCandidate, "a second player's grace expiry is visible in the same frame");
            secondPlayer.gameObject.SetActive(false);
            Check(!PlayerController.HasAutomaticClimbCandidate, "disabled players cannot keep useless overlap queries enabled");

            ClearObjects();
            Solid("Green surface support", new Vector3(270f, 4f, 0f), new Vector3(20f, 1f, 2f));
            player = Player(new Vector3(270f, 5f, 0f));
            ContactCubeMotion green = Prefab("Assets/_Project/Content/ContactCubes/Green_ContactCube.prefab",
                new Vector3(271f, 5f, 0f)).GetComponent<ContactCubeMotion>();
            HorizontalCubeVine horizontal = green.GetComponent<HorizontalCubeVine>();
            sentinel = Solid("Horizontal overlap sentinel outside every query", new Vector3(290f, 50f, 0f), Vector3.one).GetComponent<Collider>();
            Collider[] horizontalHits = (Collider[])typeof(HorizontalCubeVine).GetField("hits", Private).GetValue(horizontal);
            HashSet<PlayerController> sidePlayers = (HashSet<PlayerController>)typeof(HorizontalCubeVine).GetField("previousSides", Private).GetValue(horizontal);
            Physics.SyncTransforms();
            Invoke(green, "FixedUpdate");
            Check(green.IsSettled, "horizontal query fixture has a real physically supported green block");
            horizontalHits[0] = sentinel;
            Invoke(horizontal, "FixedUpdate");
            Check(horizontalHits[0] != sentinel && sidePlayers.Contains(player) && player.IsClimbing,
                "eligible green-surface players perform the real overlap and receive a side-climb lease");
            hook = Attach(player, green);
            horizontalHits[0] = sentinel;
            Invoke(horizontal, "FixedUpdate");
            Check(!PlayerController.HasAutomaticClimbCandidate && horizontalHits[0] == sentinel && sidePlayers.Count == 0 &&
                !player.IsClimbing && !player.IsHorizontalClimbing && !player.IsClimbCornerProtected,
                "a hook skips horizontal overlap while fully removing previous vertical and horizontal sources");
            hook.DriveInput(Vector2.zero, true);
            horizontalHits[0] = sentinel;
            Invoke(horizontal, "FixedUpdate");
            Check(!PlayerController.HasAutomaticClimbCandidate && horizontalHits[0] == sentinel && sidePlayers.Count == 0,
                "horizontal overlap stays suppressed for the release protection interval");
            Set(player, "greenHookClimbBlockedUntil", Time.time - .001f);
            horizontalHits[0] = sentinel;
            Invoke(horizontal, "FixedUpdate");
            Check(PlayerController.HasAutomaticClimbCandidate && horizontalHits[0] != sentinel && sidePlayers.Contains(player),
                "horizontal overlap and climbing recover in place immediately after protection expires");
            secondPlayer = Player(new Vector3(272f, 5f, 0f));
            hook = Attach(player, green);
            Physics.SyncTransforms();
            horizontalHits[0] = sentinel;
            Invoke(horizontal, "FixedUpdate");
            Check(PlayerController.HasAutomaticClimbCandidate && horizontalHits[0] != sentinel && sidePlayers.Count == 1 &&
                sidePlayers.Contains(secondPlayer) && secondPlayer.IsClimbing && !player.IsClimbing,
                "a second eligible player keeps green-surface queries enabled without reattaching the hooked player");
            secondHook = Attach(secondPlayer, green);
            horizontalHits[0] = sentinel;
            Invoke(horizontal, "FixedUpdate");
            Check(!PlayerController.HasAutomaticClimbCandidate && horizontalHits[0] == sentinel && sidePlayers.Count == 0,
                "all hooked players suppress horizontal overlap and clear its final side lease");
        }

        private static void ClearObjects()
        {
            foreach (GameObject instance in objects)
                if (instance != null) Object.DestroyImmediate(instance);
            objects.Clear();
        }
    }
}
