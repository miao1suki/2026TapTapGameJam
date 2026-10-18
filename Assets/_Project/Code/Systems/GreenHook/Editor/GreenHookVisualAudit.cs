using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Project.CameraModes;
using Project.ColorBlocks;
using Project.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Project.GreenHook.Editor
{
    /// <summary>Actual render-frame/camera order and constant-pointer visual stability.</summary>
    public static class GreenHookVisualAudit
    {
        private const string Key = "GreenHookVisualAudit";
        private static double deadline;
        internal static int Checks;
        internal static void Check(bool value, string label)
        {
            if (!value) throw new Exception(label);
            Checks++;
        }
        public static void Run()
        {
            if (!Application.dataPath.Contains("/Library/FlowRetirementProbe/Assets") ||
                !Environment.GetCommandLineArgs().Contains("-noaudio")) throw new Exception("Silent isolated project only");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            SessionState.SetBool(Key, true); Resume(); EditorApplication.EnterPlaymode();
        }
        [InitializeOnLoadMethod] private static void Resume()
        {
            if (!SessionState.GetBool(Key, false)) return;
            deadline = EditorApplication.timeSinceStartup + 150;
            EditorApplication.update -= Setup; EditorApplication.update += Setup;
        }
        private static void Setup()
        {
            if (EditorApplication.timeSinceStartup > deadline) { Finish(new Exception("Timeout")); return; }
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.time < .3f) return;
            EditorApplication.update -= Setup;
            try { Checks = 0; new GameObject("VisualAudit").AddComponent<GreenHookVisualAuditLoop>(); }
            catch (Exception error) { Finish(error); }
        }
        internal static void Finish(Exception error, string timing = "")
        {
            SessionState.EraseBool(Key); EditorApplication.update -= Setup;
            string report = error == null ? $"PASS {Checks} camera/aim/slide/release checks\n{timing}" : error.ToString();
            File.WriteAllText("Library/GreenHookVisualAudit.txt", report);
            if (error == null) Debug.Log(report); else Debug.LogError(report);
            EditorApplication.Exit(error == null ? 0 : 1);
        }
    }

    [DefaultExecutionOrder(-90)]
    public sealed class GreenHookVisualAuditLoop : MonoBehaviour
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        internal PlayerController Player;
        internal GreenHookAbility Hook;
        internal Camera Camera;
        internal Vector2 Pointer;
        internal int Frame;
        internal int FixedFrames;
        internal bool Ready;
        private CameraFollowController cameraFollow;
        private CameraControlManager cameraManager;
        private GreenHookAuditCameraSource cameraSource;
        private CameraControlHandle cameraControl;
        private readonly Vector3 followOffset = new Vector3(.25f, .15f, 0f);
        private Vector3 previousCameraPosition;
        private int movingCameraFrames;
        private float originalStep;
        private Action render;
        private static object Get(object instance, string name) => instance.GetType().GetField(name, Hidden).GetValue(instance);
        private static void Set(object instance, string name, object value) => instance.GetType().GetField(name, Hidden).SetValue(instance, value);
        private static GameObject Spawn(string path, Vector3 position) => Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/Content/" + path + ".prefab"), position, Quaternion.identity);
        private void Awake()
        {
            try { Initialize(); }
            catch (Exception error) { Ready = false; GreenHookVisualAudit.Finish(error); }
        }
        private void Initialize()
        {
            originalStep = Time.fixedDeltaTime; Time.fixedDeltaTime = .1f;
            ColorRuntimeService.Instance.SetAllUnlockedForCurrentSession(true);
            ColorRuntimeService.Instance.UnlockAllAbilitiesForCurrentSession();
            Player = Spawn("Player/Player", Vector3.zero).GetComponent<PlayerController>();
            Player.GetComponent<PlayerInputDriver>().enabled = false;
            Player.Motor.constraints = RigidbodyConstraints.FreezeAll;
            Hook = Player.GetComponent<GreenHookAbility>();
            Camera = new GameObject("VisualCamera").AddComponent<Camera>();
            Camera.tag = "MainCamera"; Camera.orthographic = true; Camera.orthographicSize = 7;
            Camera.transform.position = Vector3.back * 10;
            render = (Action)Delegate.CreateDelegate(typeof(Action), Hook, typeof(GreenHookAbility).GetMethod("RenderFrame", Hidden));
            VerifyShape(); VerifySliding(); VerifyRelease();
            // Exercise the same real LateUpdate chain as gameplay. Only the
            // manager writes Camera; this fixture source consumes the focus
            // submitted by CameraFollowController rather than moving Camera in
            // a synthetic order-1000 LateUpdate.
            cameraManager = Camera.gameObject.AddComponent<CameraControlManager>();
            cameraManager.ConfigureOutput(Camera);
            cameraManager.SetFocusTarget(Player.transform);
            cameraFollow = Camera.gameObject.AddComponent<CameraFollowController>();
            Set(cameraFollow, "damping", 0f);
            Set(cameraFollow, "side2DOffset", followOffset);
            cameraFollow.Configure(cameraManager, null, Player.transform);
            cameraFollow.SetFallbackMode(CameraViewMode.Side2D, true);
            cameraSource = new GreenHookAuditCameraSource(this);
            cameraControl = cameraManager.RequestControl(cameraSource);
            GreenHookVisualAudit.Check(cameraControl.HasControl && cameraFollow.Target == Player.transform &&
                Player.GetComponent<GreenHookVisualDriver>() != null,
                "real follow/manager/render-driver camera chain is active");
            previousCameraPosition = Camera.transform.position;
            var verify = gameObject.AddComponent<GreenHookAuditFrameCheck>(); verify.Owner = this;
            Ready = true;
        }
        private void VerifyShape()
        {
            // Same frame/time, equal length at different orientations: geometry
            // in rope coordinates must not change just because its tip rotated.
            MethodInfo update = typeof(GreenHookAbility).GetMethod("UpdateLine", Hidden);
            Vector3 source = Player.GetComponent<TaSource.LinkEffect.InteractionLinkEffect>().SourceWorldPosition;
            update.Invoke(Hook, new object[] { source + Vector3.right * 4, .5f, 1f, 0f });
            var line = (LineRenderer)Get(Hook, "line"); var deviations = new float[line.positionCount];
            for (int i = 0; i < deviations.Length; i++) deviations[i] = line.GetPosition(i).y - source.y;
            update.Invoke(Hook, new object[] { source + Vector3.up * 4, .5f, 1f, 0f });
            for (int i = 0; i < deviations.Length; i++)
                GreenHookVisualAudit.Check(Mathf.Abs(-line.GetPosition(i).x - deviations[i]) < .00001f,
                    "rapid rotation does not reseed rope silhouette " + i);
            Pointer = Camera.WorldToScreenPoint(source); Hook.Tick(Pointer, true); render();
            GreenHookVisualAudit.Check(((LineRenderer)Get(Hook, "aimRing")).enabled,
                "pointer at player origin keeps circle visible even without a drawable rope");
        }
        private void VerifySliding()
        {
            MethodInfo slide = typeof(GreenHookAbility).Assembly.GetType("Project.GreenHook.GreenHookPhysics")
                .GetMethod("SlideDisplacement", BindingFlags.Static | BindingFlags.NonPublic);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = new Vector3(0, -1, 0); floor.transform.localScale = new Vector3(20, 1, 2);
            Physics.SyncTransforms();
            Vector3 Slide(Vector3 move) => (Vector3)slide.Invoke(null, new object[]
                { Player.GetComponentsInChildren<Collider>(), Player.transform, move, .01f });
            var allowed = Slide(new Vector3(.2f, -.2f, 0));
            GreenHookVisualAudit.Check(allowed.x > .18f && allowed.y > -.025f, "floor preserves horizontal tangent and blocks downward penetration");
            Object.DestroyImmediate(floor);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = Vector3.right; wall.transform.localScale = new Vector3(1, 20, 2);
            Physics.SyncTransforms(); allowed = Slide(new Vector3(.2f, -.2f, 0));
            GreenHookVisualAudit.Check(allowed.y < -.18f && allowed.x < .025f, "wall preserves downward tangent and blocks horizontal penetration");
            Object.DestroyImmediate(wall);
        }
        private void VerifyRelease()
        {
            var anchor = Spawn("ContactCubes/Green_ContactCube", Vector3.up * 3);
            anchor.GetComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezeAll;
            Physics.SyncTransforms();
            GreenHookVisualAudit.Check(Hook.TryLaunch(Camera.WorldToScreenPoint(anchor.transform.position), true) == GreenHookLaunchResult.Success,
                "valid actual click attaches swing for release test");
            Player.Motor.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;
            Player.Motor.linearVelocity = new Vector3(5, 3, 0);
            Hook.DriveInput(Vector2.zero, true);
            Vector2 velocity = Player.Motor.linearVelocity;
            GreenHookVisualAudit.Check(!Hook.IsHookActive && velocity.magnitude > Mathf.Sqrt(34) &&
                Mathf.Abs(Vector2.Dot(velocity.normalized, new Vector2(5, 3).normalized) - 1) < .0001f,
                "manual release preserves actual direction and applies bounded slight boost");
            GreenHookVisualAudit.Check(velocity.magnitude < Mathf.Sqrt(34) + 1.001f, "release boost is bounded");
            Object.DestroyImmediate(anchor); Player.Motor.constraints = RigidbodyConstraints.FreezeAll;
        }
        private void Update()
        {
            if (!Ready) return;
            Frame++;
            Player.transform.position = Player.Motor.position = new Vector3(Mathf.Sin(Frame * .08f), .5f * Mathf.Cos(Frame * .05f), 0);
            // Screen-space pointer is intentionally independent of the camera's
            // later movement. Static for 80 frames, then a fast full-circle sweep.
            Pointer = new Vector2(Screen.width * .5f, Screen.height * .5f) +
                (Frame <= 80 ? new Vector2(90, 45) : new Vector2(Mathf.Cos(Frame * .4f), Mathf.Sin(Frame * .4f)) * 100);
            Hook.Tick(Pointer, true);
            FrameTimingManager.CaptureFrameTimings();
        }
        private void FixedUpdate() { if (Ready) FixedFrames++; }
        internal void VerifyFrame()
        {
            if (!Ready || Frame < 2) return;
            Vector3 expectedFocus = Player.transform.position + followOffset;
            GreenHookVisualAudit.Check(Vector3.Distance(cameraFollow.FocusPoint, expectedFocus) < .0001f &&
                Vector3.Distance(cameraManager.FocusPoint, expectedFocus) < .0001f,
                "real follow submits current-frame focus to manager " + Frame);
            GreenHookVisualAudit.Check(cameraSource.SampleFrame == Time.frameCount && cameraSource.SampleLoopFrame == Frame &&
                Vector3.Distance(cameraSource.SampleFocus, expectedFocus) < .0001f && cameraControl.HasControl,
                "real manager evaluates source with current follow context " + Frame);
            Vector3 expectedCameraPosition = cameraSource.PositionForFocus(expectedFocus);
            GreenHookVisualAudit.Check(Vector3.Distance(Camera.transform.position, expectedCameraPosition) < .0001f &&
                Vector3.Distance(cameraManager.CurrentState.position, expectedCameraPosition) < .0001f,
                "manager alone applies current-frame camera output " + Frame);
            if (Vector3.Distance(Camera.transform.position, previousCameraPosition) > .00001f) movingCameraFrames++;
            previousCameraPosition = Camera.transform.position;
            var ring = (LineRenderer)Get(Hook, "aimRing");
            var line = (LineRenderer)Get(Hook, "line");
            // LineRenderer stores its gradient in Color32 (8-bit channels).
            GreenHookVisualAudit.Check(ring != null && ring.enabled && Mathf.Abs(ring.startColor.a - .7f) <= 1f / 255f,
                $"constant-alpha circle never flashes frame={Frame} enabled={ring != null && ring.enabled} alpha={ring?.startColor.a} locked={Player.IsControlLocked} dead={Player.IsDead} ui={Project.InputAbstraction.GameInput.IsPointerOverUi()} step={Time.timeScale} pointer={Pointer} screen={Screen.width}x{Screen.height}");
            Vector3 center = Vector3.zero;
            for (int i = 0; i < ring.positionCount; i++) center += ring.GetPosition(i) / ring.positionCount;
            GreenHookVisualAudit.Check(Vector2.Distance(Camera.WorldToScreenPoint(center), Pointer) < .05f,
                "circle uses final camera projection in same render frame " + Frame);
            Vector3 origin = Player.GetComponent<TaSource.LinkEffect.InteractionLinkEffect>().SourceWorldPosition;
            GreenHookVisualAudit.Check(Vector2.Distance(Camera.WorldToScreenPoint(line.GetPosition(0)),
                Camera.WorldToScreenPoint(origin)) < .05f,
                "rope source follows current visual player position " + Frame);
            Ray pointerRay = Camera.ScreenPointToRay(Pointer);
            new Plane(Vector3.forward, Player.transform.position).Raycast(pointerRay, out float pointerDepth);
            Vector3 pointerWorld = pointerRay.GetPoint(pointerDepth);
            Vector3 expectedTip = Player.transform.position + Vector3.ClampMagnitude(pointerWorld - Player.transform.position, Hook.LaunchRangeWorld);
            GreenHookVisualAudit.Check(Vector2.Distance(Camera.WorldToScreenPoint(line.GetPosition(line.positionCount - 1)), Camera.WorldToScreenPoint(expectedTip)) < .05f,
                "rope tip has no easing or perspective-depth delay behind pointer " + Frame);
            if (Frame < 180) return;
            GreenHookVisualAudit.Check(movingCameraFrames >= 170,
                "camera-chain projection test includes continuous actual camera movement");
            var timings = new FrameTiming[1]; uint count = FrameTimingManager.GetLatestTimings(1, timings);
            string result = $"180 actual render updates (orthographic then angled perspective); real Follow(500)/Manager(1000)/VisualDriver(1100); " +
                $"moving camera frames={movingCameraFrames}; FixedUpdate={FixedFrames}; frame timing samples={count}";
            if (count > 0) result += $"; last CPU={timings[0].cpuFrameTime:F3}ms GPU={timings[0].gpuFrameTime:F3}ms (editor fixture, not before/after FPS proof)";
            Time.fixedDeltaTime = originalStep; Ready = false;
            GreenHookVisualAudit.Finish(null, result);
        }
    }

    internal sealed class GreenHookAuditCameraSource : ICameraControlSource
    {
        private readonly GreenHookVisualAuditLoop owner;
        internal int SampleFrame { get; private set; }
        internal int SampleLoopFrame { get; private set; }
        internal Vector3 SampleFocus { get; private set; }
        public string CameraControlName => "Green Hook Visual Audit Gameplay Source";
        internal GreenHookAuditCameraSource(GreenHookVisualAuditLoop owner) => this.owner = owner;
        internal Vector3 PositionForFocus(Vector3 focus) => focus + new Vector3(
            Mathf.Sin(owner.Frame * .11f) * .7f, Mathf.Cos(owner.Frame * .09f) * .7f, -10f);
        public bool TryGetCameraState(in CameraControlContext context, out CameraState state)
        {
            SampleFrame = Time.frameCount;
            SampleLoopFrame = owner.Frame;
            SampleFocus = context.FocusPoint;
            state = new CameraState
            {
                position = PositionForFocus(context.FocusPoint),
                rotation = owner.Frame <= 120 ? Quaternion.identity :
                    Quaternion.LookRotation((context.FocusPoint - PositionForFocus(context.FocusPoint)).normalized, Vector3.up),
                projection = owner.Frame <= 120 ? CameraProjectionMode.Orthographic : CameraProjectionMode.Perspective,
                orthographicSize = 7f,
                fieldOfView = 60f
            };
            return true;
        }
    }
    [DefaultExecutionOrder(1200)]
    public sealed class GreenHookAuditFrameCheck : MonoBehaviour
    {
        internal GreenHookVisualAuditLoop Owner;
        private void LateUpdate()
        {
            try { if (Owner != null) Owner.VerifyFrame(); }
            catch (Exception error) { if (Owner != null) Owner.Ready = false; GreenHookVisualAudit.Finish(error); }
        }
    }
}
