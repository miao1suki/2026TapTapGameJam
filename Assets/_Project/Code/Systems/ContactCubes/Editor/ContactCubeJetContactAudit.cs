using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Project.ColorBlocks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Project.ContactCubes.Editor
{
    /// <summary>Real PhysX falling/resting pairs, plus contact-shell and lifecycle boundaries.</summary>
    public static class ContactCubeJetContactAudit
    {
        private const string Flag = "ContactCubeJetContactAudit.Running";
        private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;
        private static readonly List<GameObject> objects = new List<GameObject>();
        private static readonly List<ContactCube> cubes = new List<ContactCube>();
        private static readonly StringBuilder diagnostics = new StringBuilder();
        private static int checks;
        private static double started;

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
            started = EditorApplication.timeSinceStartup;
            EditorApplication.update -= Wait;
            EditorApplication.update += Wait;
        }

        private static void Wait()
        {
            if (EditorApplication.timeSinceStartup - started > 120)
            { Finish(false, "Timeout"); return; }
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.time < .6f) return;
            EditorApplication.update -= Wait;
            SimulationMode previous = Physics.simulationMode;
            bool passed = false;
            try
            {
                checks = 0;
                diagnostics.Clear();
                AudioListener.volume = 0f;
                AudioListener.pause = true;
                Physics.simulationMode = SimulationMode.Script;
                ColorRuntimeService.Instance.SetAllUnlockedForCurrentSession(true);
                VerifyFallingPair(.01f, .01f, false); Cleanup();
                VerifyFallingPair(.03f, .03f, false); Cleanup();
                VerifyFallingPair(.005f, .05f, false); Cleanup();
                VerifyFallingPair(.01f, .01f, true); Cleanup();
                VerifyLogicalHorizontalFaces(); Cleanup();
                VerifyContactShellAndWorldIsolation(); Cleanup();
                VerifyLifecycle(); Cleanup();
                VerifyRotatedShapes(); Cleanup();
                passed = true;
            }
            catch (Exception error) { diagnostics.AppendLine(error.ToString()); Debug.LogException(error); }
            finally { Cleanup(); Physics.simulationMode = previous; }
            Finish(passed, (passed ? "PASS " : "FAIL ") + checks + "\n" + diagnostics);
        }

        private static void Finish(bool passed, string report)
        {
            EditorApplication.update -= Wait;
            SessionState.EraseBool(Flag);
            File.WriteAllText("Library/ContactCubeJetContactAudit.txt", report);
            Debug.Log(report);
            EditorApplication.Exit(passed ? 0 : 1);
        }

        private static void Check(bool condition, string label)
        {
            checks++;
            if (!condition) throw new InvalidOperationException("CUBE JET CONTACT: " + label);
        }

        private static void Invoke(object target, string method) =>
            target.GetType().GetMethod(method, Hidden).Invoke(target, null);

        private static ContactCube Spawn(CubeColor color, Vector3 position, bool frozen = false)
        {
            string path = "Assets/_Project/Content/ContactCubes/" + color + "_ContactCube.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) throw new InvalidOperationException("Missing fixture: " + path);
            GameObject result = Object.Instantiate(prefab, position, Quaternion.identity);
            objects.Add(result);
            ContactCube cube = result.GetComponent<ContactCube>();
            cubes.Add(cube);
            Rigidbody body = result.GetComponent<Rigidbody>();
            // Script simulation has no render interpolation pass between steps.
            body.interpolation = RigidbodyInterpolation.None;
            if (frozen)
            {
                result.GetComponent<ContactCubeMotion>().enabled = false;
                body.isKinematic = true;
                body.useGravity = false;
            }
            return cube;
        }

        private static GameObject Solid(string name, Vector3 center, Vector3 size, bool trigger = false)
        {
            GameObject result = GameObject.CreatePrimitive(PrimitiveType.Cube);
            result.name = name;
            result.transform.position = center;
            result.transform.localScale = size;
            result.GetComponent<Collider>().isTrigger = trigger;
            objects.Add(result);
            return result;
        }

        private static BoxCollider Shape(ContactCube cube) => cube.GetComponentInChildren<BoxCollider>();

        private static void Move(ContactCube cube, Vector3 position)
        {
            cube.transform.position = position;
            cube.GetComponent<Rigidbody>().position = position;
            Physics.SyncTransforms();
        }

        private static CubeWaterJet ActiveJet(ContactCube cube)
        {
            CubeWaterJet active = null;
            foreach (CubeWaterJet jet in cube.GetComponentsInChildren<CubeWaterJet>(true))
            {
                if (!jet.isActiveAndEnabled) continue;
                Check(active == null, "one blue/red pair owns only one active jet");
                active = jet;
            }
            return active;
        }

        private static void Step(int count = 1)
        {
            for (int step = 0; step < count; step++)
            {
                foreach (ContactCube cube in cubes)
                    if (cube != null && cube.isActiveAndEnabled && cube.GetComponent<ContactCubeMotion>().enabled)
                        Invoke(cube.GetComponent<ContactCubeMotion>(), "FixedUpdate");
                foreach (ContactCube cube in cubes)
                    if (cube != null && cube.isActiveAndEnabled) Invoke(cube, "FixedUpdate");
                foreach (ContactCube cube in cubes)
                    if (cube != null)
                        foreach (CubeWaterJet jet in cube.GetComponentsInChildren<CubeWaterJet>())
                            if (jet.isActiveAndEnabled) Invoke(jet, "FixedUpdate");
                Physics.Simulate(Time.fixedDeltaTime);
            }
        }

        private static void VerifyFallingPair(float redOffset, float blueOffset, bool redHooked)
        {
            Vector3 origin = new Vector3(2000f, 20f, 0f);
            Solid("Actual floor", origin + Vector3.down * .5f, new Vector3(10f, 1f, 3f));
            ContactCube red = Spawn(CubeColor.Red, origin + Vector3.up * .5f);
            ContactCube blue = Spawn(CubeColor.Blue, origin + Vector3.up * 3.5f);
            Shape(red).contactOffset = redOffset;
            Shape(blue).contactOffset = blueOffset;
            var contact = blue.gameObject.AddComponent<JetAuditCollisionProbe>();
            contact.Expected = red;
            if (redHooked) red.GetComponent<ContactCubeMotion>().BeginGreenHookPull();
            Physics.SyncTransforms();
            bool landed = false;
            for (int step = 0; step < 360; step++)
            {
                Step();
                if (contact.ContactEvents > 0 && blue.GetComponent<ContactCubeMotion>().IsSettled)
                { landed = true; break; }
            }
            string label = $"fall redOffset={redOffset:F3} blueOffset={blueOffset:F3} redHooked={redHooked}";
            Check(landed, label + " actually collides and settles, not a teleported/static-only fixture");
            Step(3);
            CubeWaterJet stable = ActiveJet(blue);
            float gap = Shape(blue).bounds.min.y - Shape(red).bounds.max.y;
            diagnostics.AppendLine(label + $" contacts={contact.ContactEvents} restingGap={gap:F6}");
            Check(gap <= .01f + Shape(red).contactOffset + Shape(blue).contactOffset + .0002f,
                label + " settles within actual pair contact shell, not an old oversized support air-gap");
            Check(stable != null && stable.Direction.y > .999f, label + " emits upward after landing");
            float minimumGap = gap, maximumGap = gap;
            for (int step = 0; step < 180; step++)
            {
                Step();
                gap = Shape(blue).bounds.min.y - Shape(red).bounds.max.y;
                minimumGap = Mathf.Min(minimumGap, gap);
                maximumGap = Mathf.Max(maximumGap, gap);
                Check(ActiveJet(blue) == stable, label + " keeps the same jet through real resting physics step " + step);
            }
            diagnostics.AppendLine(label + $" persistent180 gapRange=[{minimumGap:F6},{maximumGap:F6}]");
            Check(red.IsEnergyAvailable, label + " red passive power remains available");
            if (redHooked) Check(red.IsHookSuspended, "red outgoing hook suspension does not silence blue jet");
            Move(blue, origin + new Vector3(0f, 3f, 0f));
            Invoke(stable, "FixedUpdate");
            Check(!stable.gameObject.activeSelf, label + " real separation retires jet before another push");
            Step();
            Check(ActiveJet(blue) == null, label + " no time-based stale contact lease");
        }

        private static void VerifyLogicalHorizontalFaces()
        {
            Vector3 origin = new Vector3(2050f, 20f, 0f);
            ContactCube blue = Spawn(CubeColor.Blue, origin, true);
            ContactCube red = Spawn(CubeColor.Red, origin + Vector3.left, true);
            Check(Mathf.Abs(Shape(blue).size.x - .96f) < .0001f &&
                Mathf.Abs(Shape(red).size.x - .96f) < .0001f,
                "real red/blue fixtures retain stable one-cell-pit physical clearance");
            Check(Mathf.Abs(blue.GetComponent<ContactCubeMotion>().LogicalContactShapeSize.x - 1f) < .0001f &&
                Mathf.Abs(red.GetComponent<ContactCubeMotion>().LogicalContactShapeSize.x - 1f) < .0001f,
                "logical contact keeps the authored one-cell shape instead of shrinking with physics");
            Physics.SyncTransforms(); Step();
            float physicalGap = Shape(blue).bounds.min.x - Shape(red).bounds.max.x;
            diagnostics.AppendLine($"adjacent authored grid faces physicalGap={physicalGap:F6}");
            Check(physicalGap > .039f && ActiveJet(blue) != null,
                "adjacent one-cell art/logic reacts despite .04 physical-only horizontal clearance");
            CubeWaterJet stable = ActiveJet(blue);
            red.SetHookSuspended(true); Step(4);
            Check(ActiveJet(blue) == stable && red.IsEnergyAvailable,
                "hooked red preserves passive logical contact and electrical supply");
            Move(red, origin + Vector3.left * 1.011f); Step();
            Check(ActiveJet(blue) == stable, "logical gap inside default .01 plus actual pair offsets stays connected");
            Move(red, origin + Vector3.left * 1.014f);
            Invoke(stable, "FixedUpdate");
            Check(!stable.isActiveAndEnabled,
                "logical gap outside .01 plus actual small offsets stops before any stale jet push");
            Step();
            Check(ActiveJet(blue) == null,
                "candidate broadphase padding does not enlarge actual logical contact tolerance");
        }

        private static void VerifyContactShellAndWorldIsolation()
        {
            Vector3 origin = new Vector3(2100f, 20f, 0f);
            ContactCube blue = Spawn(CubeColor.Blue, origin, true);
            ContactCube red = Spawn(CubeColor.Red, origin + Vector3.down * 1.025f, true);
            Shape(blue).contactOffset = Shape(red).contactOffset = .01f;
            var world = Solid("Non-cube color target outside old .01 tolerance",
                origin + Vector3.right * 1.025f, Vector3.one).AddComponent<JetAuditColorTarget>();
            Physics.SyncTransforms();
            Step();
            Check(ActiveJet(blue) != null, ".025 physical microgap is inside .01 tolerance plus both .01 contact offsets");
            Check(world.Applications == 0, "cube contact-shell expansion does not enlarge world color application");
            Move(red, origin + Vector3.down * 1.031f);
            Step();
            Check(ActiveJet(blue) == null, "gap outside pair-specific contact shell clears immediately");
            Move(red, origin + Vector3.down);
            Shape(red).contactOffset = .06f;
            Step();
            // Inject only the callback candidate here to isolate its stale-cache
            // contract. The falling cases above separately prove actual PhysX
            // callback delivery, including asymmetric collider contact offsets.
            var candidates = (HashSet<ContactCube>)typeof(ContactCube).GetField("collisionNeighbours", Hidden).GetValue(blue);
            candidates.Add(red);
            Move(red, origin + Vector3.down * 1.065f);
            Step();
            Check(ActiveJet(blue) != null, "asymmetric stored collision candidate uses both collider offsets");
            Move(red, origin + Vector3.down * 1.081f);
            Step();
            Check(ActiveJet(blue) == null && !candidates.Contains(red),
                "cached collision candidate is discarded beyond actual shell without requiring Exit callback");
        }

        private static void VerifyLifecycle()
        {
            Vector3 origin = new Vector3(2200f, 20f, 0f);
            ContactCube blue = Spawn(CubeColor.Blue, origin, true);
            ContactCube red = Spawn(CubeColor.Red, origin + Vector3.down, true);
            var receiver = Solid("Jet push lifecycle receiver", origin + Vector3.up * 2f,
                Vector3.one * .2f, true).AddComponent<JetAuditPushReceiver>();
            Physics.SyncTransforms();
            Step();
            Check(ActiveJet(blue) != null && receiver.Velocity.y > 0f, "active jet drives receiver");
            red.SetCarrySuspended(true);
            Step();
            Check(ActiveJet(blue) == null && receiver.Velocity == Vector3.zero, "carrying red clears jet and push ownership");
            red.SetCarrySuspended(false); Step();
            Check(ActiveJet(blue) != null, "placing red restores eligible physical reaction");
            blue.SetHookSuspended(true); Step();
            Check(ActiveJet(blue) == null && receiver.Velocity == Vector3.zero,
                "hooking blue still pauses its own outgoing active reaction");
            blue.SetHookSuspended(false); Step();
            Check(ActiveJet(blue) != null, "releasing blue restores contacts without a stale lease");
            blue.SetCarrySuspended(true); Step();
            Check(ActiveJet(blue) == null && receiver.Velocity == Vector3.zero,
                "carrying blue clears its own jet and receiver ownership");
            blue.SetCarrySuspended(false); Step();
            Check(ActiveJet(blue) != null, "placing blue reacquires current physical contact");
            Shape(red).enabled = false; Step();
            Check(ActiveJet(blue) == null, "disabled red entity collider is not a valid neighbor");
            Shape(red).enabled = true; Physics.SyncTransforms(); Step();
            Check(ActiveJet(blue) != null, "enabled shape can react again");
            Shape(red).isTrigger = true; Physics.SyncTransforms(); Step();
            Check(ActiveJet(blue) == null, "trigger-only red shape cannot count as entity contact");
            Shape(red).isTrigger = false; Physics.SyncTransforms(); Step();
            ColorRuntimeService.Instance.SetUnlockedForCurrentSession("red", false); Step();
            Check(ActiveJet(blue) == null && receiver.Velocity == Vector3.zero, "locked red clears water and receiver velocity");
            ColorRuntimeService.Instance.SetUnlockedForCurrentSession("red", true); Step();
            Check(ActiveJet(blue) != null, "unlock reacquires current real contacts");
            ColorRuntimeService.Instance.SetUnlockedForCurrentSession("blue", false); Step();
            Check(ActiveJet(blue) == null && receiver.Velocity == Vector3.zero,
                "locking blue also retires its owned jet immediately");
            ColorRuntimeService.Instance.SetUnlockedForCurrentSession("blue", true); Step();
            Check(ActiveJet(blue) != null, "unlocking blue reacquires physical contact");
            blue.ResetForRoom();
            Check(ActiveJet(blue) == null && receiver.Velocity == Vector3.zero, "room reset immediately clears all owned effects");
            Step();
            Check(ActiveJet(blue) != null, "still touching unlocked pair can react after reset");
            blue.enabled = false;
            Check(ActiveJet(blue) == null && receiver.Velocity == Vector3.zero, "disable clears all outgoing effects immediately");
        }

        private static void VerifyRotatedShapes()
        {
            Vector3 origin = new Vector3(2300f, 20f, 0f);
            Quaternion rotation = Quaternion.Euler(0f, 0f, 32f);
            ContactCube blue = Spawn(CubeColor.Blue, origin, true);
            ContactCube red = Spawn(CubeColor.Red, origin + rotation * Vector3.down * 1.025f, true);
            // Motion deliberately caps its default contact offset to .001 for
            // one-cell openings. This first case explicitly tests a larger
            // authored pair offset; .025 must not pass with the small default.
            Shape(blue).contactOffset = Shape(red).contactOffset = .01f;
            blue.transform.rotation = red.transform.rotation = rotation;
            blue.GetComponent<Rigidbody>().rotation = red.GetComponent<Rigidbody>().rotation = rotation;
            Physics.SyncTransforms(); Step();
            Check(ActiveJet(blue) != null, "physical shell handles oriented entity boxes, not world AABB overlap alone");
            Move(red, origin + rotation * Vector3.down * 1.04f); Step();
            Check(ActiveJet(blue) == null, "rotated AABBs overlapping does not preserve separated oriented boxes");
            Shape(blue).contactOffset = Shape(red).contactOffset = .001f;
            Move(red, origin + rotation * Vector3.down * 1.011f); Step();
            Check(ActiveJet(blue) != null, "rotated default small-offset pair accepts only its actual logical shell");
            Move(red, origin + rotation * Vector3.down * 1.014f); Step();
            Check(ActiveJet(blue) == null, "rotated small-offset pair rejects a logical gap beyond .012 without AABB leakage");
        }

        private static void Cleanup()
        {
            foreach (GameObject obj in objects) if (obj != null) Object.DestroyImmediate(obj);
            objects.Clear(); cubes.Clear();
        }
    }

    public sealed class JetAuditCollisionProbe : MonoBehaviour
    {
        public ContactCube Expected;
        public int ContactEvents;
        private void OnCollisionEnter(Collision collision) => Record(collision);
        private void OnCollisionStay(Collision collision) => Record(collision);
        private void Record(Collision collision)
        {
            if (collision.collider.GetComponentInParent<ContactCube>() == Expected) ContactEvents++;
        }
    }

    public sealed class JetAuditPushReceiver : MonoBehaviour, ICubeJetPushReceiver
    {
        public Vector3 Velocity;
        public void SetCubeJetVelocity(Component source, Vector3 velocity, float deltaTime) => Velocity = velocity;
    }

    public sealed class JetAuditColorTarget : MonoBehaviour, IColorApplicationTarget
    {
        public int Applications;
        public bool CanApplyColor(string colorId, GameObject actor) => true;
        public bool ApplyColor(string colorId, GameObject actor) { Applications++; return true; }
    }
}
