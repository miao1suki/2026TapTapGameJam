using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
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
    /// <summary>Real one-cell-box/one-cell-gap tests, not a broad unsupported-platform fall.</summary>
    public static class GreenHookNarrowGapAudit
    {
        private const string Flag = "GreenHookNarrowGapAudit";
        private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;
        private static readonly List<GameObject> objects = new List<GameObject>();
        private static readonly StringBuilder diagnostics = new StringBuilder();
        private static int checks;
        public static void Run() => Start(false);
        public static void RunBaseline() => Start(true);
        private static void Start(bool baseline)
        {
            if (!Application.dataPath.Contains("FlowRetirementProbe") ||
                Array.IndexOf(Environment.GetCommandLineArgs(), "-noaudio") < 0)
                throw new InvalidOperationException("Use only the silent isolated probe project.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            SessionState.SetBool(Flag, true);
            SessionState.SetBool(Flag + "Baseline", baseline);
            Resume();
            EditorApplication.EnterPlaymode();
        }
        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!SessionState.GetBool(Flag, false)) return;
            EditorApplication.update -= Wait;
            EditorApplication.update += Wait;
        }
        private static void Wait()
        {
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.time < .6f) return;
            EditorApplication.update -= Wait;
            SimulationMode previous = Physics.simulationMode;
            bool passed = false;
            try
            {
                Physics.simulationMode = SimulationMode.Script;
                ColorRuntimeService.Instance.SetAllUnlockedForCurrentSession(true);
                ColorRuntimeService.Instance.UnlockAllAbilitiesForCurrentSession();
                for (int kind = 0; kind < 4; kind++)
                for (int direction = -1; direction <= 1; direction += 2)
                for (int phase = 0; phase < 3; phase++)
                {
                    VerifyGap(kind, direction, phase, 1f);
                    Cleanup();
                }
                VerifyGap(0, -1, 1, .8f); Cleanup();
                VerifyGap(0, -1, 1, 2f); Cleanup();
                VerifyContinuedPull(); Cleanup();
                VerifyFlatFloor(); Cleanup();
                VerifyStableClearance(); Cleanup();
                VerifyThinWall(); Cleanup();
                VerifyRealSupportClearance(); Cleanup();
                VerifyRetrieval();
                passed = true;
            }
            catch (Exception error) { diagnostics.AppendLine(error.ToString()); Debug.LogException(error); }
            finally { Cleanup(); Physics.simulationMode = previous; }
            string report = (passed ? "PASS " : "FAIL ") + checks + "\n" + diagnostics;
            File.WriteAllText("Library/GreenHookNarrowGapAudit.txt", report);
            Debug.Log(report);
            SessionState.EraseBool(Flag); SessionState.EraseBool(Flag + "Baseline");
            EditorApplication.Exit(passed ? 0 : 1);
        }
        private static void Check(bool condition, string label)
        {
            checks++;
            if (!condition) throw new Exception(label);
        }
        private static object Invoke(object target, string name, params object[] args) =>
            target.GetType().GetMethod(name, Hidden).Invoke(target, args);
        private static void Set(object target, string name, object value) =>
            target.GetType().GetField(name, Hidden).SetValue(target, value);
        private static T Read<T>(object target, string name) =>
            (T)target.GetType().GetField(name, Hidden).GetValue(target);
        private static GameObject Spawn(string path, Vector3 position)
        {
            var result = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Content/" + path), position, Quaternion.identity);
            objects.Add(result); return result;
        }
        private static GameObject Solid(string name, Vector3 position, Vector3 size)
        {
            var result = GameObject.CreatePrimitive(PrimitiveType.Cube);
            result.name = name; result.transform.position = position; result.transform.localScale = size;
            objects.Add(result); return result;
        }
        private static Vector3 Step(GreenHookAbility hook, PlayerController player, GameObject target)
        {
            if (target.TryGetComponent<ContactCubeMotion>(out var cubeMotion)) Invoke(cubeMotion, "FixedUpdate");
            else if (target.TryGetComponent<MechanismBaseFeature>(out var mechanism)) Invoke(mechanism, "FixedUpdate");
            Invoke(hook, "FixedUpdate");
            Vector3 command = target.GetComponent<Rigidbody>().linearVelocity;
            Invoke(player, "FixedUpdate"); Physics.Simulate(Time.fixedDeltaTime);
            return command;
        }
        private static void VerifyGap(int kind, int direction, int phase, float gapWidth)
        {
            Vector3 origin = new Vector3(3000f + kind * 30f, 20f, 0f);
            float halfGap = gapWidth * .5f;
            Solid("Left lip", origin + new Vector3(-(8f + halfGap) * .5f, -.5f, 0f), new Vector3(8f - halfGap, 1f, 2f));
            Solid("Right lip", origin + new Vector3((8f + halfGap) * .5f, -.5f, 0f), new Vector3(8f - halfGap, 1f, 2f));
            Solid("Pit floor", origin + new Vector3(0f, -2.5f, 0f), new Vector3(16f, 1f, 2f));
            float phaseOffset = phase * .047f;
            PlayerController player = Spawn("Player/Player.prefab", origin + new Vector3(direction * 4.5f, .5f, 0f)).GetComponent<PlayerController>();
            player.GetComponent<PlayerInputDriver>().enabled = false;
            player.Motor.interpolation = RigidbodyInterpolation.None;
            string path = kind == 0 ? "ContactCubes/Red_ContactCube.prefab" : kind == 1 ?
                "ContactCubes/Blue_ContactCube.prefab" : "ColorBlocks/Prefabs/Red_Foundation.prefab";
            GameObject target = Spawn(path, origin + new Vector3(-direction * (2.5f + phaseOffset), .5f, 0f));
            if (kind >= 2)
            {
                var mechanism = target.GetComponent<MechanismBaseFeature>();
                Set(mechanism, "useGravity", kind != 3); Invoke(mechanism, "FixedUpdate");
            }
            Rigidbody body = target.GetComponent<Rigidbody>();
            body.interpolation = RigidbodyInterpolation.None;
            BoxCollider shape = target.GetComponentInChildren<BoxCollider>();
            Physics.SyncTransforms();
            Vector3 logicalSize = target.TryGetComponent<ContactCubeMotion>(out var contactMotion)
                ? contactMotion.LogicalContactShapeSize : shape.size;
            Check(Mathf.Abs(Vector3.Scale(logicalSize, shape.transform.lossyScale).x - 1f) < .002f,
                "fixture retains a full one-cell logical box and full-grid artwork scale");
            IGreenHookTarget hookTarget = target.GetComponent<IGreenHookTarget>();
            var hook = player.GetComponent<GreenHookAbility>();
            Set(hook, "pullSpeed", phase == 2 ? 12f : 8f);
            Invoke(hook, "BeginHook", hookTarget, hookTarget.GreenHookTransform.position);
            Invoke(hook, "BeginPulling");
            float minimumY = body.position.y;
            float closestToCenter = float.MaxValue;
            bool crossedOnTop = false;
            for (int tick = 0; tick < 70; tick++)
            {
                Step(hook, player, target);
                Vector3 local = body.position - origin;
                minimumY = Mathf.Min(minimumY, local.y);
                closestToCenter = Mathf.Min(closestToCenter, Mathf.Abs(local.x));
                if (local.x * direction > halfGap + .5f && local.y > .35f) crossedOnTop = true;
                // The release-stability case actively releases once the box is
                // really in the shaft. Keeping the rope attached for 70 steps
                // may legitimately pull the player over it, change pull axis,
                // and lift it back out; that is a separate continuous test.
                if (kind != 3 && gapWidth >= .99f && local.y < -.3f && Mathf.Abs(local.x) < halfGap + .05f)
                    break;
            }
            string label = $"kind={kind} direction={direction} phase={phase} gap={gapWidth:F2}";
            diagnostics.AppendLine(label + $" minY={minimumY:F5} nearestX={closestToCenter:F5} final={body.position - origin} crossedTop={crossedOnTop}" +
                $" physicalWidth={shape.bounds.size.x:F5} offset={shape.contactOffset:F5} velocity={body.linearVelocity} locks={body.constraints}");
            Check(hook.IsHookActive && player.CanDriveGreenHook && player.ControlLockDepth == 1,
                label + " retains single hook ownership instead of detaching at the pit");
            Check(body.useGravity == (kind != 3), label + " preserves explicit gravity policy");
            if (!SessionState.GetBool(Flag + "Baseline", false))
            {
                if (kind == 3 || gapWidth < .99f)
                    Check(minimumY > .45f, label + " no-gravity or genuinely too-narrow passage does not force a fall");
                else
                    Check(minimumY < -.25f && !crossedOnTop, label + " falls into the pit instead of bridging across its two lips");
            }
            hook.CancelHook();
            Check(!player.IsGreenHookActive && !player.IsControlLocked &&
                !target.GetComponent<IGreenHookMotion>().IsGreenHookPullActive,
                label + " release restores both physics owners");
            Vector3 releasedSize = shape.size;
            float releasedX = body.position.x;
            for (int tick = 0; tick < 180; tick++) Step(hook, player, target);
            diagnostics.AppendLine(label + $" released180={body.position - origin} width={shape.bounds.size.x:F5}");
            Check(shape.size == releasedSize, label + " release never expands a physical box inside the shaft");
            if (kind != 3 && gapWidth >= .99f && !SessionState.GetBool(Flag + "Baseline", false))
            {
                Check(Mathf.Abs(body.position.x - releasedX) < .03f &&
                    Mathf.Abs(body.position.y - origin.y + 1.5f) < .1f && body.linearVelocity.sqrMagnitude < .02f,
                    label + " remains stably in the pit for 180 steps after release without re-bridging or launching");
                if (kind == 2)
                {
                    var mechanism = target.GetComponent<MechanismBaseFeature>();
                    Set(mechanism, "useGravity", false);
                    Vector3 beforeNoGravity = body.position;
                    for (int tick = 0; tick < 180; tick++) Step(hook, player, target);
                    Check((body.position - beforeNoGravity).sqrMagnitude < .0001f &&
                        !body.useGravity && body.isKinematic && shape.size == releasedSize,
                        label + " changing a fallen base to no gravity keeps its safe stable shape and pose");
                }
            }
        }
        private static void VerifyFlatFloor()
        {
            Vector3 origin = new Vector3(3200f, 20f, 0f);
            Solid("Continuous floor", origin + Vector3.down * .5f, new Vector3(16f, 1f, 2f));
            PlayerController player = Spawn("Player/Player.prefab", origin + new Vector3(-4f, .5f, 0f)).GetComponent<PlayerController>();
            player.GetComponent<PlayerInputDriver>().enabled = false;
            GameObject target = Spawn("ContactCubes/Red_ContactCube.prefab", origin + new Vector3(3f, .5f, 0f));
            var body = target.GetComponent<Rigidbody>(); var hook = player.GetComponent<GreenHookAbility>();
            Physics.SyncTransforms();
            Invoke(hook, "BeginHook", target.GetComponent<IGreenHookTarget>(), target.transform.position);
            Invoke(hook, "BeginPulling");
            for (int tick = 0; tick < 10; tick++) Step(hook, player, target);
            Check(body.position.x < origin.x + 2f && Mathf.Abs(body.position.y - origin.y - .5f) < .03f,
                "continuous floor keeps normal pull speed without false pit braking");
            Check((body.constraints & RigidbodyConstraints.FreezePositionY) == 0,
                "horizontal hook motion never replaces natural gravity with FreezeY");
        }
        private static void VerifyContinuedPull()
        {
            Vector3 origin = new Vector3(3160f, 20f, 0f);
            Solid("Continuous-pull left lip", origin + new Vector3(-4.25f, -.5f, 0f), new Vector3(7.5f, 1f, 2f));
            Solid("Continuous-pull right lip", origin + new Vector3(4.25f, -.5f, 0f), new Vector3(7.5f, 1f, 2f));
            Solid("Continuous-pull pit floor", origin + new Vector3(0f, -2.5f, 0f), new Vector3(16f, 1f, 2f));
            PlayerController player = Spawn("Player/Player.prefab", origin + new Vector3(-4.5f, .5f, 0f)).GetComponent<PlayerController>();
            player.GetComponent<PlayerInputDriver>().enabled = false;
            player.Motor.interpolation = RigidbodyInterpolation.None;
            GameObject target = Spawn("ContactCubes/Red_ContactCube.prefab", origin + new Vector3(2.594f, .5f, 0f));
            var body = target.GetComponent<Rigidbody>(); var hook = player.GetComponent<GreenHookAbility>();
            body.interpolation = RigidbodyInterpolation.None;
            Set(hook, "pullSpeed", 12f);
            Physics.SyncTransforms();
            Invoke(hook, "BeginHook", target.GetComponent<IGreenHookTarget>(), target.transform.position);
            Invoke(hook, "BeginPulling");
            float minY = .5f;
            bool issuedUpwardPull = false;
            bool unexplainedLaunch = false;
            Vector2 previousAxis = Vector2.zero;
            for (int tick = 0; tick < 180; tick++)
            {
                Vector3 command = Step(hook, player, target);
                Vector2 axis = Read<Vector2>(hook, "pullAxis");
                bool pullingPlayer = Read<bool>(hook, "pullingPlayer");
                if (axis.y < -.5f && !pullingPlayer && command.y > .5f) issuedUpwardPull = true;
                if (body.linearVelocity.y > 1f && !issuedUpwardPull) unexplainedLaunch = true;
                minY = Mathf.Min(minY, body.position.y - origin.y);
                if (tick % 10 == 0 || axis != previousAxis)
                    diagnostics.AppendLine($"continuous tick={tick} player={player.Motor.position - origin} target={body.position - origin}" +
                        $" axis={axis} pullPlayer={pullingPlayer} command={command} solvedVelocity={body.linearVelocity}");
                previousAxis = axis;
            }
            Check(minY < -.25f, "continuous high-speed hook physically drops into the one-cell shaft");
            Check(!unexplainedLaunch, "continuous rope cannot launch the box upward without a real upward hook command");
            Check(hook.IsHookActive && player.CanDriveGreenHook && body.useGravity,
                "continued pull retains rope ownership and natural gravity even if the player moves and the pull axis changes");
        }
        private static void VerifyStableClearance()
        {
            Vector3 origin = new Vector3(3240f, 20f, 0f);
            GameObject target = Spawn("ContactCubes/Red_ContactCube.prefab", origin);
            var motion = target.GetComponent<ContactCubeMotion>();
            var cube = target.GetComponent<ContactCube>();
            var shape = target.GetComponentInChildren<BoxCollider>();
            Vector3 physicalSize = shape.size;
            Check(Mathf.Abs(shape.bounds.size.x - .96f) < .002f &&
                Mathf.Abs(shape.bounds.size.y - 1f) < .002f &&
                Mathf.Abs(motion.LogicalContactShapeSize.x - 1f) < .002f,
                "movable red box has only horizontal .02-cell clearance per side; logical size remains one cell");
            for (int index = 0; index < 5; index++)
            {
                motion.BeginGreenHookPull(); motion.EndGreenHookPull();
                motion.SetCarrySuspended(true); motion.SetCarrySuspended(false);
                cube.ResetForRoom(); motion.ResetForRoom();
                target.SetActive(false); target.SetActive(true);
            }
            Check(shape.size == physicalSize && shape.contactOffset <= .0011f,
                "repeated hook/carry/room-reset/enable cycles do not accumulate collider shrinkage or restore unsafe width");
            GameObject green = Spawn("ContactCubes/Green_ContactCube.prefab", origin + Vector3.right * 3f);
            BoxCollider greenShape = green.GetComponentInChildren<BoxCollider>();
            Check(Mathf.Abs(greenShape.bounds.size.x - 1f) < .002f && greenShape.contactOffset > .0011f,
                "green climbing/growth anchors retain the full original solid box and contact offset");
        }
        private static void VerifyRetrieval()
        {
            Vector3 origin = new Vector3(3360f, 20f, 0f);
            Solid("Retrieval left lip", origin + new Vector3(-4.25f, -.5f, 0f), new Vector3(7.5f, 1f, 2f));
            Solid("Retrieval right lip", origin + new Vector3(4.25f, -.5f, 0f), new Vector3(7.5f, 1f, 2f));
            Solid("Retrieval pit floor", origin + new Vector3(0f, -2.5f, 0f), new Vector3(16f, 1f, 2f));
            GameObject target = Spawn("ContactCubes/Red_ContactCube.prefab", origin + Vector3.down * .5f);
            var motion = target.GetComponent<ContactCubeMotion>();
            var body = target.GetComponent<Rigidbody>();
            var shape = target.GetComponentInChildren<BoxCollider>();
            body.interpolation = RigidbodyInterpolation.None;
            Vector3 stableSize = shape.size;
            Physics.SyncTransforms();
            for (int tick = 0; tick < 180; tick++) { Invoke(motion, "FixedUpdate"); Physics.Simulate(Time.fixedDeltaTime); }
            diagnostics.AppendLine($"retrieval initial={body.position - origin} velocity={body.linearVelocity} settled={motion.IsSettled} locks={body.constraints}");
            Check(Mathf.Abs(body.position.y - origin.y + 1.5f) < .015f,
                "independent retrieval starts with the real box physically resting at the bottom of the one-cell shaft");
            motion.BeginGreenHookPull();
            for (int tick = 0; tick < 24; tick++)
            {
                motion.SetGreenHookVelocity(Vector3.up * 8f);
                Physics.Simulate(Time.fixedDeltaTime);
                if (tick % 5 == 0)
                    diagnostics.AppendLine($"retrieval up tick={tick} target={body.position - origin} velocity={body.linearVelocity} locks={body.constraints}");
            }
            Check(body.position.y > origin.y + 1.5f, "a released fallen box can be physically pulled upward through its one-cell shaft again");
            for (int tick = 0; tick < 15; tick++)
            {
                motion.SetGreenHookVelocity(Vector3.right * 8f);
                Physics.Simulate(Time.fixedDeltaTime);
                if (tick % 5 == 0)
                    diagnostics.AppendLine($"retrieval right tick={tick} target={body.position - origin} velocity={body.linearVelocity} locks={body.constraints}");
            }
            motion.EndGreenHookPull();
            for (int tick = 0; tick < 180; tick++)
            {
                Invoke(motion, "FixedUpdate"); Physics.Simulate(Time.fixedDeltaTime);
                if (tick % 30 == 0 || tick == 179)
                    diagnostics.AppendLine($"retrieval release tick={tick} target={body.position - origin} velocity={body.linearVelocity}" +
                        $" settled={motion.IsSettled} supported={Invoke(motion, "HasSupport")} locks={body.constraints} size={shape.size}");
            }
            // A several-cell real fall may retain a small solver penetration
            // when the resting motion lock engages. This is not shaft bridging
            // or an unsupported gap; keep the real support/shape checks strict.
            Check(body.position.x > origin.x + 1f && Mathf.Abs(body.position.y - origin.y - .5f) < .03f &&
                motion.IsSettled && shape.size == stableSize,
                "after retrieval the stable-clearance box returns to real flat-floor support without changing art/grid size or accumulating skin");
        }
        private static void VerifyThinWall()
        {
            Vector3 origin = new Vector3(3280f, 20f, 0f);
            Solid("Continuous floor", origin + Vector3.down * .5f, new Vector3(16f, 1f, 2f));
            GameObject wall = Solid("Thin real wall", origin + new Vector3(1.25f, 2f, 0f), new Vector3(.05f, 4f, 2f));
            PlayerController player = Spawn("Player/Player.prefab", origin + new Vector3(4.5f, .5f, 0f)).GetComponent<PlayerController>();
            player.GetComponent<PlayerInputDriver>().enabled = false;
            GameObject target = Spawn("ContactCubes/Red_ContactCube.prefab", origin + Vector3.up * .5f);
            var body = target.GetComponent<Rigidbody>(); var hook = player.GetComponent<GreenHookAbility>();
            var shape = target.GetComponentInChildren<BoxCollider>();
            Physics.SyncTransforms();
            Invoke(hook, "BeginHook", target.GetComponent<IGreenHookTarget>(), target.transform.position);
            Invoke(hook, "BeginPulling");
            for (int tick = 0; tick < 40; tick++) Step(hook, player, target);
            Check(shape.bounds.max.x <= wall.GetComponent<Collider>().bounds.min.x + .005f &&
                body.position.x > origin.x + .4f && Mathf.Abs(body.position.y - origin.y - .5f) < .03f,
                "horizontal clearance still stops at a .05-cell wall and preserves flat-floor support");
            Check(hook.IsHookActive, "a blocked target at a thin wall retains its rope");
        }
        private static void VerifyRealSupportClearance()
        {
            Vector3 origin = new Vector3(3320f, 20f, 0f);
            GameObject floor = Solid("Real support", origin + Vector3.down * .5f, new Vector3(8f, 1f, 2f));
            GameObject target = Spawn("ContactCubes/Red_ContactCube.prefab", origin + Vector3.up * .55f);
            var motion = target.GetComponent<ContactCubeMotion>();
            var body = target.GetComponent<Rigidbody>();
            Physics.SyncTransforms();
            Check(!(bool)Invoke(motion, "HasSupport"), ".05-cell air below a cube is not settled support");
            for (int tick = 0; tick < 80; tick++) { Invoke(motion, "FixedUpdate"); Physics.Simulate(Time.fixedDeltaTime); }
            Check(motion.IsSettled && Mathf.Abs(body.position.y - origin.y - .5f) < .015f,
                "natural gravity closes the real support gap before red/blue settle locks engage");
            Collider cubeShape = target.GetComponentInChildren<BoxCollider>();
            Physics.IgnoreCollision(cubeShape, floor.GetComponent<Collider>(), true);
            Check(!(bool)Invoke(motion, "HasSupport"), "an ignored-collision floor cannot hold a contact cube in midair");
            Physics.IgnoreCollision(cubeShape, floor.GetComponent<Collider>(), false);
            origin += Vector3.right * 20f;
            GameObject support = Solid("Base real support", origin + Vector3.down * .5f, new Vector3(8f, 1f, 2f));
            GameObject foundation = Spawn("ColorBlocks/Prefabs/Red_Foundation.prefab", origin + Vector3.up * .55f);
            var mechanism = foundation.GetComponent<MechanismBaseFeature>();
            var baseBody = foundation.GetComponent<Rigidbody>();
            Physics.SyncTransforms();
            Check(!(bool)Invoke(mechanism, "HasSolidSupport"), ".05-cell air below a gravity base is not settled support");
            for (int tick = 0; tick < 80; tick++) { Invoke(mechanism, "FixedUpdate"); Physics.Simulate(Time.fixedDeltaTime); }
            Check(Mathf.Abs(baseBody.position.y - origin.y - .5f) < .015f &&
                (bool)Invoke(mechanism, "HasSolidSupport"),
                "gravity base closes the real floor gap before its support freeze engages");
            Collider baseShape = foundation.GetComponent<Collider>();
            Physics.IgnoreCollision(baseShape, support.GetComponent<Collider>(), true);
            Check(!(bool)Invoke(mechanism, "HasSolidSupport"), "an ignored-collision floor cannot hold a base in midair");
        }
        private static void Cleanup()
        {
            foreach (GameObject obj in objects) if (obj != null) Object.DestroyImmediate(obj);
            objects.Clear();
        }
    }
}
