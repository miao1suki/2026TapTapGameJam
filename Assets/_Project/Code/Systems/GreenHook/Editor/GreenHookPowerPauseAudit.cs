using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Project.BlockFeatures;
using Project.ColorBlocks;
using Project.ContactCubes;
using Project.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Project.GreenHook.Editor
{
    /// <summary>Source-specific power edges, real pull physics, and retained hook ownership.</summary>
    public static class GreenHookPowerPauseAudit
    {
        private const string Flag = "GreenHookPowerPauseAudit.Running";
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private static double deadline;
        private static int checks;
        private static string currentCase;

        public static void Run()
        {
            if (!Application.dataPath.Replace('\\', '/').Contains("/Library/FlowRetirementProbe/Assets") ||
                Array.IndexOf(Environment.GetCommandLineArgs(), "-noaudio") < 0)
                throw new InvalidOperationException("Use only the silent isolated probe project.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool(Flag, true);
            Resume();
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!SessionState.GetBool(Flag, false)) return;
            deadline = EditorApplication.timeSinceStartup + 120;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup > deadline) { Finish(false, "Timeout: " + currentCase); return; }
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.time < .6f) return;
            EditorApplication.update -= Tick;
            SimulationMode previous = Physics.simulationMode;
            bool passed = false;
            string report;
            try
            {
                checks = 0;
                AudioListener.volume = 0f;
                AudioListener.pause = true;
                Physics.simulationMode = SimulationMode.Script;
                ColorRuntimeService.Instance.SetAllUnlockedForCurrentSession(true);
                ColorRuntimeService.Instance.UnlockAllAbilitiesForCurrentSession();
                currentCase = "video pit socket approach";
                VerifyPitSocketApproach();
                currentCase = "red-to-base retained power";
                VerifyRedPull();
                currentCase = "base-to-red, no-gravity priority";
                VerifyBasePull();
                currentCase = "blue and unrelated source";
                VerifyUnrelatedSource();
                currentCase = "already connected, reconnect, and retarget";
                VerifyClickAndPowerEdges();
                passed = true;
                report = "PASS " + checks + " hook power-pause physics/ownership checks";
            }
            catch (Exception error)
            {
                report = "FAIL " + checks + " [" + currentCase + "]\n" + error;
                Debug.LogException(error);
            }
            finally { Physics.simulationMode = previous; }
            Finish(passed, report);
        }

        private static void Check(bool condition, string label)
        {
            checks++;
            if (!condition) throw new InvalidOperationException("POWER PAUSE: " + label);
        }

        // Reflection only drives the same component physics callbacks used by
        // the existing isolated GreenHookVerification. Power is never injected:
        // OnTick performs the real collider/IEnergySource scan every step.
        private static void Call(object target, string method, params object[] args)
        {
            MethodInfo callback = target.GetType().GetMethod(method, Hidden);
            if (callback == null) throw new MissingMethodException(target.GetType().Name, method);
            callback.Invoke(target, args);
        }
        private static void Set(object target, string name, object value) =>
            target.GetType().GetField(name, Hidden).SetValue(target, value);
        private static object Get(object target, string name) => target.GetType().GetField(name, Hidden).GetValue(target);

        private sealed class Fixture : IDisposable
        {
            private readonly List<GameObject> objects = new List<GameObject>();
            private readonly List<ContactCubeMotion> motions = new List<ContactCubeMotion>();
            private readonly List<MechanismBaseFeature> bases = new List<MechanismBaseFeature>();
            private readonly List<MovingPlatformFeature> platforms = new List<MovingPlatformFeature>();
            public readonly Vector3 Origin;
            public readonly PlayerController Player;
            public readonly GreenHookAbility Hook;
            private readonly Camera camera;

            public Fixture(float x, bool fullFloor = true)
            {
                Origin = new Vector3(x, 20.5f, 0f);
                Player = Spawn("Player/Player", Origin).GetComponent<PlayerController>();
                Player.GetComponent<PlayerInputDriver>().enabled = false;
                Player.Motor.interpolation = RigidbodyInterpolation.None;
                Hook = Player.GetComponent<GreenHookAbility>();
                var cameraObject = new GameObject("Power audit camera", typeof(Camera));
                objects.Add(cameraObject);
                camera = cameraObject.GetComponent<Camera>();
                camera.tag = "MainCamera";
                camera.orthographic = true;
                camera.orthographicSize = 8f;
                camera.transform.position = Origin + new Vector3(2.5f, 1f, -10f);
                GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                objects.Add(floor);
                floor.name = "Power audit physical floor";
                floor.transform.position = Origin + new Vector3(fullFloor ? 2f : 0f, -1f, 0f);
                floor.transform.localScale = new Vector3(fullFloor ? 20f : 1.2f, 1f, 2f);
                Physics.SyncTransforms();
            }

            private GameObject Spawn(string relativePath, Vector3 position)
            {
                string path = "Assets/_Project/Content/" + relativePath + ".prefab";
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) throw new InvalidOperationException("Missing fixture: " + path);
                var instance = Object.Instantiate(prefab, position, Quaternion.identity);
                objects.Add(instance);
                return instance;
            }

            public void Solid(string name, Vector3 offset, Vector3 size)
            {
                GameObject solid = GameObject.CreatePrimitive(PrimitiveType.Cube);
                objects.Add(solid);
                solid.name = name;
                solid.transform.position = Origin + offset;
                solid.transform.localScale = size;
                Physics.SyncTransforms();
            }

            public ContactCube Cube(string color, Vector3 offset, bool fixedSource = false)
            {
                var cube = Spawn("ContactCubes/" + color + "_ContactCube", Origin + offset).GetComponent<ContactCube>();
                var motion = cube.GetComponent<ContactCubeMotion>();
                motions.Add(motion);
                var body = cube.GetComponent<Rigidbody>();
                body.interpolation = RigidbodyInterpolation.None;
                if (fixedSource) { body.useGravity = false; body.isKinematic = true; }
                Physics.SyncTransforms();
                return cube;
            }

            public MechanismBaseFeature Base(Vector3 offset, bool gravity = false)
            {
                var foundation = Spawn("ColorBlocks/Prefabs/Red_Foundation", Origin + offset).GetComponent<MechanismBaseFeature>();
                bases.Add(foundation);
                Set(foundation, "useGravity", gravity);
                Call(foundation, "FixedUpdate");
                foundation.GetComponent<Rigidbody>().interpolation = RigidbodyInterpolation.None;
                Physics.SyncTransforms();
                Scan();
                return foundation;
            }

            public MovingPlatformFeature Platform(MechanismBaseFeature source)
            {
                var platform = Spawn("ColorBlocks/Prefabs/Red_Platform", Origin + new Vector3(2f, 6f, 0f))
                    .GetComponent<MovingPlatformFeature>();
                platforms.Add(platform);
                Set(platform, "route", MovingPlatformRoute.Vertical);
                Set(platform, "travelBlocks", 2);
                Set(platform, "travelDuration", .4f);
                Set(platform, "signalMotion", MovingPlatformSignalMotion.PoweredEndpoint);
                platform.BindBase(source);
                return platform;
            }

            public void Scan()
            {
                Physics.SyncTransforms();
                foreach (var foundation in bases) Call(foundation, "OnTick", Time.fixedDeltaTime);
            }

            public void Step(int count = 1)
            {
                for (int step = 0; step < count; step++)
                {
                    foreach (var motion in motions) Call(motion, "FixedUpdate");
                    foreach (var foundation in bases) Call(foundation, "FixedUpdate");
                    Scan();
                    Call(Hook, "FixedUpdate");
                    Call(Player, "FixedUpdate");
                    foreach (var platform in platforms)
                    {
                        Call(platform, "OnTick", Time.fixedDeltaTime);
                        Call(platform, "FixedUpdate");
                    }
                    Physics.Simulate(Time.fixedDeltaTime);
                    Scan();
                }
            }

            public void Click(IGreenHookTarget target)
            {
                Scan();
                var result = Hook.TryLaunch(camera.WorldToScreenPoint(target.GreenHookTransform.position), true);
                Check(result == GreenHookLaunchResult.Success && Hook.IsPulling,
                    "real screen click attaches/retargets " + target.GreenHookTransform.name + " (" + result + ")");
            }

            public void MovePlayer(Vector3 position)
            {
                Player.SetMoveInput(Vector2.zero);
                Player.Motor.position = position;
                Player.transform.position = position;
                Player.Motor.linearVelocity = Vector3.zero;
                Physics.SyncTransforms();
            }

            public void UntilPaused(int limit = 100)
            {
                for (int step = 0; step < limit && !Hook.IsPullPausedForPower; step++) Step();
                Check(Hook.IsPullPausedForPower, "new physical source contact pauses pulling within " + limit + " steps");
            }

            public void Dispose()
            {
                Hook.CancelHook();
                for (int i = objects.Count - 1; i >= 0; i--) if (objects[i] != null) Object.DestroyImmediate(objects[i]);
                Physics.SyncTransforms();
            }
        }

        private static void AssertOwner(Fixture fixture, IGreenHookMotion motion, string label)
        {
            Check(fixture.Hook.IsHookActive && fixture.Hook.IsPulling && !fixture.Hook.IsPullingPlayer &&
                motion.IsGreenHookPullActive && fixture.Player.ControlLockDepth == 1 && fixture.Player.CanDriveGreenHook &&
                fixture.Player.IsGreenHookLocomotionAllowed, label);
        }

        private static void VerifyRedPull()
        {
            using var fixture = new Fixture(100f);
            var foundation = fixture.Base(Vector3.right * 2.2f);
            var red = fixture.Cube("Red", Vector3.right * 5f);
            var motion = red.GetComponent<ContactCubeMotion>();
            var platform = fixture.Platform(foundation);
            fixture.Scan();
            Check(!foundation.IsSignalActive, "red starts outside the real base detector");
            float start = red.transform.position.x;
            fixture.Click(motion);
            fixture.UntilPaused();
            Check(red.transform.position.x < start - .3f && foundation.IsPoweredBy(red),
                "real pull displacement, not a forced signal, creates the pause edge");
            Vector3 resting = red.transform.position;
            for (int step = 0; step < 220; step++)
            {
                fixture.Step();
                Check(fixture.Hook.IsPullPausedForPower && foundation.IsPoweredBy(red),
                    "red remains the actual supplying contact while attached " + step);
                AssertOwner(fixture, motion, "one hook owner and normal locomotion while power-paused " + step);
            }
            Check(Mathf.Abs(red.transform.position.x - resting.x) < .02f && red.GetComponent<Rigidbody>().useGravity,
                "pause stops lateral pull without disabling target gravity");
            Check(Vector3.Distance(platform.transform.position, platform.PreviewFirstTarget) < .03f && !platform.IsMoving,
                "bound PoweredEndpoint platform reaches and retains its endpoint, not a brief pulse then return");
            float playerX = fixture.Player.Motor.position.x;
            fixture.Player.SetMoveInput(Vector2.left);
            fixture.Step(5);
            fixture.Player.SetMoveInput(Vector2.zero);
            Check(fixture.Player.Motor.position.x < playerX - .1f && foundation.IsSignalActive,
                "ordinary walking is actually responsive while retaining powered hook");
            fixture.MovePlayer(fixture.Player.Motor.position + Vector3.up * 2f);
            float playerY = fixture.Player.Motor.position.y;
            fixture.Step(8);
            Check(fixture.Player.Motor.position.y < playerY - .04f && fixture.Hook.IsPullPausedForPower,
                "ordinary player gravity continues during the pause");
            fixture.Hook.CancelHook();
            Check(!fixture.Hook.IsPullPausedForPower && !fixture.Hook.IsHookActive &&
                !motion.IsGreenHookPullActive && fixture.Player.ControlLockDepth == 0 && foundation.IsPoweredBy(red),
                "manual cancel clears pause/ownership without removing passive red supply");
        }

        private static void VerifyPitSocketApproach()
        {
            for (int direction = -1; direction <= 1; direction += 2)
            for (int phase = 0; phase < 3; phase++)
            {
                using var fixture = new Fixture(400f + phase * 30f + (direction + 1) * 60f, false);
                fixture.Solid("Socket left floor", new Vector3(direction * .75f, -1f, 0f), new Vector3(2.5f, 1f, 2f));
                fixture.Solid("Socket right floor", new Vector3(direction * 5.5f, -1f, 0f), new Vector3(5f, 1f, 2f));
                var foundation = fixture.Base(new Vector3(direction * 2.5f, -1f, 0f));
                var red = fixture.Cube("Red", new Vector3(direction * (4.5f + phase * .047f), 0f, 0f));
                var motion = red.GetComponent<ContactCubeMotion>();
                var platform = fixture.Platform(foundation);
                Set(fixture.Hook, "pullSpeed", phase == 2 ? 12f : 8f);
                fixture.Scan();
                Check(!foundation.IsSignalActive, "video socket begins unpowered");
                fixture.Click(motion);
                float firstSignalOffset = -1f;
                for (int tick = 0; tick < 100 && !fixture.Hook.IsPullPausedForPower; tick++)
                {
                    fixture.Step();
                    if (firstSignalOffset < 0f && foundation.IsPoweredBy(red))
                        firstSignalOffset = Mathf.Abs(red.transform.position.x - foundation.transform.position.x);
                }
                Check(fixture.Hook.IsPullPausedForPower, "video socket eventually reaches a retained power pause");
                float offset = Mathf.Abs(red.transform.position.x - foundation.transform.position.x);
                Debug.Log("SOCKET APPROACH direction=" + direction + " phase=" + phase + " firstSignalOffset=" + firstSignalOffset + " offset=" + offset +
                    " red=" + red.transform.position + " base=" + foundation.transform.position);
                Check(firstSignalOffset > .3f, "fixture really exercises early neighbour-volume power, not an already seated cube");
                Check(offset < .035f, "power pause occurs after red box reaches the socket, not the floor lip: " + offset);
                Check(foundation.IsPoweredBy(red) && platform.LinkedControls.Count != 0,
                    "real source and live platform remain powered during socket seating");
                Vector3 resting = red.transform.position;
                fixture.Step(160);
                Check(fixture.Hook.IsPullPausedForPower && foundation.IsPoweredBy(red) &&
                    Mathf.Abs(red.transform.position.x - resting.x) < .02f,
                    "socket seating stays powered and does not slide away after the platform moves");
                Check(Vector3.Distance(platform.transform.position, platform.PreviewFirstTarget) < .03f,
                    "early signal still lets the bound platform finish its actual travel");
                AssertOwner(fixture, motion, "socket seating keeps rope and ordinary walking ownership");
                fixture.Click(motion);
                fixture.Step();
                Check(!fixture.Hook.IsPullPausedForPower, "deliberate reclick resumes from a seated socket");
            }
        }

        private static void VerifyBasePull()
        {
            // Only the player has a floor: an explicitly no-gravity base must
            // stay in midair before, during, and after the new power pause.
            using var fixture = new Fixture(160f, false);
            var red = fixture.Cube("Red", Vector3.right * 2.2f, true);
            var foundation = fixture.Base(Vector3.right * 5f, false);
            var motion = foundation.GetComponent<GreenHookBodyMotion>();
            float startX = foundation.transform.position.x;
            float startY = foundation.transform.position.y;
            fixture.Click(foundation);
            fixture.UntilPaused();
            Check(foundation.transform.position.x < startX - .3f && foundation.IsPoweredBy(red),
                "pulling the base toward a real source also creates a new pause edge");
            for (int step = 0; step < 220; step++)
            {
                fixture.Step();
                Check(foundation.IsPoweredBy(red) && fixture.Hook.IsPullPausedForPower,
                    "pulled base retains its supplying source " + step);
                AssertOwner(fixture, motion, "base retains single owner while paused " + step);
            }
            Check(Mathf.Abs(foundation.transform.position.y - startY) < .001f && !foundation.UsesGravity &&
                !foundation.GetComponent<Rigidbody>().useGravity,
                "explicit base no-gravity preference wins over hook and pause physics");
            fixture.Hook.CancelHook();
            fixture.Step(4);
            Check(!fixture.Hook.IsPullPausedForPower && fixture.Player.ControlLockDepth == 0 &&
                !motion.IsGreenHookPullActive && foundation.GetComponent<Rigidbody>().isKinematic &&
                !foundation.GetComponent<Rigidbody>().useGravity,
                "cancel restores no-gravity base author ownership rather than enabling falling");
        }

        private static void VerifyUnrelatedSource()
        {
            using var fixture = new Fixture(220f);
            var foundation = fixture.Base(Vector3.right * 2.2f);
            var red = fixture.Cube("Red", Vector3.right * 1.2f, true);
            var blue = fixture.Cube("Blue", Vector3.right * 5f);
            var motion = blue.GetComponent<ContactCubeMotion>();
            fixture.Scan();
            Check(foundation.IsPoweredBy(red) && !foundation.IsPoweredBy(blue),
                "source-specific query rejects blue despite another red energizing the same base");
            fixture.Click(motion);
            float initial = blue.transform.position.x;
            for (int step = 0; step < 45; step++)
            {
                fixture.Step();
                Check(!fixture.Hook.IsPullPausedForPower && fixture.Hook.IsHookActive &&
                    foundation.IsPoweredBy(red) && !foundation.IsPoweredBy(blue),
                    "unrelated red supply never creates a blue-target pause " + step);
            }
            Check(blue.transform.position.x < initial - .3f && fixture.Player.ControlLockDepth == 1,
                "blue still receives real pull motion with one retained hook owner");
        }

        private static void VerifyClickAndPowerEdges()
        {
            using var fixture = new Fixture(280f);
            var foundation = fixture.Base(Vector3.right * 2.2f);
            var red = fixture.Cube("Red", Vector3.right * 3.3f);
            var motion = red.GetComponent<ContactCubeMotion>();
            fixture.MovePlayer(fixture.Origin + Vector3.right * 6f);
            fixture.Scan();
            Check(foundation.IsPoweredBy(red), "red is already connected before this launch");
            fixture.Click(motion);
            Check(!fixture.Hook.IsPullPausedForPower, "launch snapshots existing power instead of immediately pausing");
            float start = red.transform.position.x;
            fixture.Step(18);
            Check(!fixture.Hook.IsPullPausedForPower && !foundation.IsSignalActive && red.transform.position.x > start + .5f,
                "deliberate click on an already connected source can pull it out");
            fixture.MovePlayer(fixture.Origin);
            fixture.Click(motion);
            fixture.UntilPaused();
            Check(foundation.IsPoweredBy(red), "leaving and then reconnecting produces another real pause edge");
            fixture.Click(motion);
            Check(!fixture.Hook.IsPullPausedForPower && fixture.Player.ControlLockDepth == 1 && motion.IsGreenHookPullActive,
                "clicking the same paused object resumes without acquiring a second lock");
            fixture.Step();
            Check(!fixture.Hook.IsPullPausedForPower && foundation.IsPoweredBy(red),
                "the same existing connection does not immediately pause again after resume");
            fixture.MovePlayer(red.transform.position + Vector3.right * 2.6f);
            fixture.Click(motion);
            fixture.Step(18);
            Check(!fixture.Hook.IsPullPausedForPower && !foundation.IsSignalActive,
                "resuming an actual paused object allows it to leave its old socket");
            fixture.MovePlayer(fixture.Origin + Vector3.right);
            fixture.Click(motion);
            fixture.UntilPaused();
            Check(foundation.IsPoweredBy(red),
                "a resumed object pauses again on a later genuine reconnection, not only its first launch");
            var other = fixture.Cube("Red", new Vector3(1f, 3f, 0f));
            var otherMotion = other.GetComponent<ContactCubeMotion>();
            fixture.Click(otherMotion);
            Check(!fixture.Hook.IsPullPausedForPower && !motion.IsGreenHookPullActive && otherMotion.IsGreenHookPullActive &&
                fixture.Player.ControlLockDepth == 1, "changing target clears old pause/contact history and hands off one motion owner");
            fixture.Hook.CancelHook();
            Check(!fixture.Hook.IsHookActive && !fixture.Hook.IsPullPausedForPower && !otherMotion.IsGreenHookPullActive &&
                fixture.Player.ControlLockDepth == 0 && Get(fixture.Hook, "pulledBase") == null &&
                Get(fixture.Hook, "pulledEnergy") == null &&
                ((HashSet<MechanismBaseFeature>)Get(fixture.Hook, "previousPowerContacts")).Count == 0 &&
                ((HashSet<MechanismBaseFeature>)Get(fixture.Hook, "currentPowerContacts")).Count == 0,
                "cancel completely clears per-hook power source and contact caches");
        }

        private static void Finish(bool passed, string report)
        {
            EditorApplication.update -= Tick;
            SessionState.EraseBool(Flag);
            File.WriteAllText("Library/GreenHookPowerPauseAudit.txt", report);
            Debug.Log(report);
            EditorApplication.Exit(passed ? 0 : 1);
        }
    }
}
