using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Project.ColorBlocks;
using Project.InputAbstraction;
using Project.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Unity.Profiling;
using Project.BlockFeatures;
using Project.ContactCubes;
using Project.Items;
using Project.Rooms;
using System.Collections.Generic;
using System.Reflection;
using Object = UnityEngine.Object;

namespace Project.GreenHook.Editor
{
    /// <summary>Silent, isolated cursor hot-path and aim/energy regression.</summary>
    public static class GreenHookAimAudit
    {
        private const string Flag = "GreenHookAimAudit";
        private static double deadline;
        private static int checks;
        private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;
        private static void Check(bool value, string message)
        {
            if (!value) throw new Exception(message);
            checks++; UnityEngine.Debug.Log("AIM PASS " + message);
        }
        private static void Set(object obj, string field, object value) => obj.GetType().GetField(field, Hidden).SetValue(obj, value);
        private static object Get(object obj, string field) => obj.GetType().GetField(field, Hidden).GetValue(obj);
        private static void Call(object obj, string method, params object[] args) => obj.GetType().GetMethod(method, Hidden).Invoke(obj, args);
        private static GameObject Spawn(string path, Vector3 pos) => Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/Content/" + path + ".prefab"), pos, Quaternion.identity);
        public static void RunBaseline() => Start(true);
        public static void Run() => Start(false);
        private static void Start(bool baseline)
        {
            if (!Application.dataPath.Contains("/Library/FlowRetirementProbe/Assets") ||
                !Environment.GetCommandLineArgs().Contains("-noaudio")) throw new Exception("Silent isolated project only");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            SessionState.SetBool(Flag, true); SessionState.SetBool(Flag + "Baseline", baseline);
            Resume(); EditorApplication.EnterPlaymode();
        }
        [InitializeOnLoadMethod] private static void Resume()
        {
            if (!SessionState.GetBool(Flag, false)) return;
            deadline = EditorApplication.timeSinceStartup + 120;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
        }
        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup > deadline) { Finish("Timeout", false); return; }
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.time < .3f) return;
            EditorApplication.update -= Tick;
            try
            {
                checks = 0;
                string result = Benchmark();
                UnityEngine.Debug.Log(result);
                if (!SessionState.GetBool(Flag + "Baseline", false)) { RunCases(); result += "\nPASS " + checks + " aim/energy/gravity checks"; }
                Finish(result, true);
            }
            catch (Exception error) { UnityEngine.Debug.LogException(error); Finish(error.ToString(), false); }
        }
        private static string Benchmark()
        {
            ColorRuntimeService.Instance.SetAllUnlockedForCurrentSession(true);
            ColorRuntimeService.Instance.UnlockAllAbilitiesForCurrentSession();
            var player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Content/Player/Player.prefab")).GetComponent<PlayerController>();
            player.GetComponent<PlayerInputDriver>().enabled = false;
            player.Motor.constraints = RigidbodyConstraints.FreezeAll;
            var camera = new GameObject("AimCamera", typeof(Camera)).GetComponent<Camera>();
            camera.tag = "MainCamera"; camera.orthographic = true; camera.orthographicSize = 7;
            camera.transform.position = new Vector3(0, 0, -10);
            new GameObject("UIEventSystem", typeof(EventSystem));
            var canvas = new GameObject("BenchmarkUI", typeof(Canvas), typeof(GraphicRaycaster)).GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 2;
            for (int i = 0; i < 64; i++)
            {
                var image = new GameObject("Button", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                image.transform.SetParent(canvas.transform, false);
                image.rectTransform.anchorMin = image.rectTransform.anchorMax = Vector2.zero;
                image.rectTransform.anchoredPosition = new Vector2(10 + i * 2, 10);
                image.rectTransform.sizeDelta = new Vector2(8, 8);
            }
            Canvas.ForceUpdateCanvases();
            camera.Render();
            var input = new AimInput(); GameInput.UseExternalSource(input);
            var hook = player.GetComponent<GreenHookAbility>();
            var positions = new Vector2[64];
            for (int i = 0; i < positions.Length; i++) positions[i] = camera.WorldToScreenPoint(
                new Vector3(Mathf.Cos(i * Mathf.PI / 32) * 4, Mathf.Sin(i * Mathf.PI / 32) * 4, 0));
            for (int i = 0; i < 256; i++) { input.Pointer = positions[i % 64]; hook.Tick(input.Pointer, true); }
            var lineField = typeof(GreenHookAbility).GetField("line", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (!(lineField.GetValue(hook) is LineRenderer activeLine) || !activeLine.enabled) throw new Exception("Benchmark must exercise visible green line");
            var watch = new Stopwatch(); watch.Start();
            for (int i = 0; i < 6000; i++) { input.Pointer = positions[i % 64]; hook.Tick(input.Pointer, true); }
            watch.Stop();
            string result = $"6000 rotating green Tick: {watch.Elapsed.TotalMilliseconds:F3} ms; locked={player.IsControlLocked}, dead={player.IsDead}, timeScale={Time.timeScale}, ui={EventSystem.current != null}";
            for (int i = 0; i < 32; i++) { input.Pointer = positions[i % 64]; GameInput.IsPointerOverUi(); }
            using var allocRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc", 32768,
                ProfilerRecorderOptions.CollectOnlyOnCurrentThread | ProfilerRecorderOptions.WrapAroundWhenCapacityReached);
            watch.Restart();
            for (int i = 0; i < 6000; i++) { input.Pointer = positions[i % 64]; GameInput.IsPointerOverUi(); }
            watch.Stop();
            allocRecorder.Stop();
            result += $"\n6000 UI queries: {watch.Elapsed.TotalMilliseconds:F3} ms";
            long markerValue = 0;
            int samples = allocRecorder.Count;
            for (int i = 0; i < samples; i++) markerValue += allocRecorder.GetSample(i).Value;
            result += $"\nUI GC.Alloc profiler: valid={allocRecorder.Valid}, events={samples}, cumulativeMarkerValue={markerValue} (not a byte counter)";
            if (!SessionState.GetBool(Flag + "Baseline", false))
                Check(allocRecorder.Valid && samples <= 16, "warm UI detection does not allocate per query (6000 calls)");
            long before = GC.GetAllocatedBytesForCurrentThread();
            var allocationProbe = new byte[4096]; GC.KeepAlive(allocationProbe);
            result += $"\nAllocation counter self-check: {GC.GetAllocatedBytesForCurrentThread() - before}";
            GameInput.ClearExternalSource(input);
            Object.DestroyImmediate(player.gameObject); Object.DestroyImmediate(camera.gameObject);
            Object.DestroyImmediate(canvas.gameObject); Object.DestroyImmediate(EventSystem.current.gameObject);
            return result;
        }
        private static void RunCases()
        {
            var simulation = Physics.simulationMode; Physics.simulationMode = SimulationMode.Script;
            var camera = new GameObject("ToleranceCamera").AddComponent<Camera>();
            camera.tag = "MainCamera"; camera.orthographic = true; camera.orthographicSize = 8;
            camera.transform.position = Vector3.back * 10;
            var player = Spawn("Player/Player", Vector3.zero).GetComponent<PlayerController>();
            player.GetComponent<PlayerInputDriver>().enabled = false;
            player.Motor.constraints = RigidbodyConstraints.FreezeAll;
            var hook = player.GetComponent<GreenHookAbility>();
            var input = new AimInput(); GameInput.UseExternalSource(input);
            GreenHookLaunchResult Click(Vector3 point)
            {
                Physics.SyncTransforms(); input.Pointer = camera.WorldToScreenPoint(point);
                return hook.TryLaunch(input.Pointer, true);
            }
            try
            {
                var cube = Spawn("ContactCubes/Red_ContactCube", new Vector3(3, 0, 0));
                Check(Click(new Vector3(3, 1.05f, 0)) == GreenHookLaunchResult.Success && hook.IsPulling,
                    "near-miss above cube uses 0.65-cell tolerance and attaches immediately");
                Check(player.ControlLockDepth == 1 && cube.GetComponent<ContactCube>().IsEnergyAvailable,
                    "hook has one control lock and does not suppress red energy");
                input.Pointer = camera.WorldToScreenPoint(new Vector3(2, 2, 0)); hook.Tick(input.Pointer, true);
                var ring = (LineRenderer)Get(hook, "aimRing"); int ringId = ring.GetInstanceID();
                var anchor = (Vector3)Get(hook, "localAnchor");
                Check(ring.enabled && ring.positionCount == 48, "retarget tolerance ring visible while attached");
                Check(((Vector2)cube.transform.TransformPoint(anchor) - new Vector2(3, .5f)).magnitude < .01f,
                    "near miss anchors on real collider, not empty air");
                hook.CancelHook();
                Check(Click(new Vector3(3, 1.3f, 0)) == GreenHookLaunchResult.Rejected && !hook.IsHookActive,
                    "outside visible tolerance cannot attach");
                cube.transform.position = new Vector3(5.8f, 0, 0); cube.GetComponent<Rigidbody>().position = cube.transform.position;
                Check(Click(new Vector3(5.25f, 0, 0)) == GreenHookLaunchResult.Success,
                    "edge circle reaches a real surface beyond original five-cell range");
                hook.CancelHook(); cube.transform.position = new Vector3(6.2f, 0, 0); cube.GetComponent<Rigidbody>().position = cube.transform.position;
                Check(Click(new Vector3(5.5f, 0, 0)) == GreenHookLaunchResult.OutOfRange && !hook.IsHookActive,
                    "launch slack remains bounded to half a cell");
                Check(Click(new Vector3(20, 0, 0)) == GreenHookLaunchResult.OutOfRange, "distant empty click is out of range");
                cube.transform.position = new Vector3(3, 0, 0); cube.GetComponent<Rigidbody>().position = cube.transform.position;
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.transform.position = new Vector3(3, 0, -1);
                Check(Click(cube.transform.position) == GreenHookLaunchResult.Success,
                    "green rope may target through walls without making bodies pass through them");
                hook.CancelHook(); Object.DestroyImmediate(wall);
                input.Pointer = camera.WorldToScreenPoint(new Vector3(2, 1, 0)); hook.Tick(input.Pointer, true);
                var line = (LineRenderer)Get(hook, "line"); Vector3 oldTip = line.GetPosition(line.positionCount - 1);
                input.Pointer = camera.WorldToScreenPoint(new Vector3(-2, 1, 0)); hook.Tick(input.Pointer, true);
                Vector3 eased = line.GetPosition(line.positionCount - 1);
                Check(eased.x < oldTip.x && eased.x > -2f, "idle tip eases toward cursor rather than snapping");
                Vector3 center = Vector3.zero;
                for (int i = 0; i < ring.positionCount; i++) center += ring.GetPosition(i) / ring.positionCount;
                Check(((Vector2)center - new Vector2(-2, 1)).magnitude < .002f,
                    "click circle stays on raw pointer while rope tip eases");
                Check(Mathf.Abs(Vector2.Distance(ring.GetPosition(0), center) - hook.AimToleranceWorld) < .002f,
                    "visible circle matches actual hit radius");
                Set(hook, "idleFollowSeconds", 0f);
                for (int i = 0; i < 512; i++)
                {
                    var position = new Vector3(Mathf.Cos(i * .7f) * 4, Mathf.Sin(i * .7f) * 4, 0);
                    input.Pointer = camera.WorldToScreenPoint(position); hook.Tick(input.Pointer, true);
                }
                Check(((LineRenderer)Get(hook, "aimRing")).GetInstanceID() == ringId && player.GetComponentsInChildren<LineRenderer>(true).Count(l => l.name == "__GreenAimTolerance") == 1,
                    "rapid rotation reuses exactly one tolerance renderer");
                player.transform.position += Vector3.right; player.Motor.position = player.transform.position;
                input.Pointer = camera.WorldToScreenPoint(new Vector3(2, 1, 0)); hook.Tick(input.Pointer, true);
                Check(Vector2.Distance(line.GetPosition(0), player.GetComponent<TaSource.LinkEffect.InteractionLinkEffect>().SourceWorldPosition) < .001f,
                    "rope origin follows moving player");
                hook.Tick(input.Pointer, false); Check(!ring.enabled && !line.enabled, "changing selected ability hides aim visuals");
                hook.Tick(input.Pointer, true); hook.enabled = false;
                Check(!ring.enabled && !line.enabled, "disabling idle ability also hides circle"); hook.enabled = true;
                Time.timeScale = 0f; hook.Tick(input.Pointer, true);
                Check(!ring.enabled && !line.enabled, "pause hides aim visuals"); Time.timeScale = 1;
                var regionRoot = new GameObject("ToleranceRoomBoundary"); var manager = regionRoot.AddComponent<RoomLifecycleManager>();
                var cellsA = new List<Vector2Int>(); var cellsB = new List<Vector2Int>();
                for (int x = -6; x < 9; x++) for (int y = -5; y < 5; y++) (x < 4 ? cellsA : cellsB).Add(new Vector2Int(x, y));
                manager.Configure(new List<RoomRegion> { new RoomRegion { id = "a", cells = cellsA }, new RoomRegion { id = "b", cells = cellsB } }, 1f, "");
                cube.transform.position = new Vector3(4.8f, 0, 0); cube.GetComponent<Rigidbody>().position = cube.transform.position;
                Check(Click(new Vector3(4.5f, 0, 0)) == GreenHookLaunchResult.Rejected && !hook.IsHookActive,
                    "generous tolerance never crosses room boundary");
                cube.GetComponentInChildren<BoxCollider>().size = new Vector3(3, 1, 1);
                cube.GetComponent<RoomResetObject>().Bind(manager, "b");
                Check(Click(new Vector3(3.7f, 0, 0)) == GreenHookLaunchResult.Rejected && !hook.IsHookActive,
                    "neighbor-room oversized collider cannot reach into player room for targeting");
                Object.DestroyImmediate(regionRoot); Object.DestroyImmediate(cube);
                VerifyUiBlocking(camera, hook, input);
            }
            finally
            {
                hook.CancelHook(); GameInput.ClearExternalSource(input);
                Object.DestroyImmediate(player.gameObject); Object.DestroyImmediate(camera.gameObject);
                Time.timeScale = 1f; Physics.simulationMode = simulation;
            }
            VerifyEnergyWhileAttached();
            VerifyCollectibleGravity();
        }
        private static void VerifyUiBlocking(Camera camera, GreenHookAbility hook, AimInput input)
        {
            var events = new GameObject("BlockingEvents", typeof(EventSystem));
            var canvas = new GameObject("BlockingCanvas", typeof(Canvas), typeof(GraphicRaycaster)).GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 2;
            var image = new GameObject("BlockingImage", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(canvas.transform, false);
            image.rectTransform.anchorMin = Vector2.zero; image.rectTransform.anchorMax = Vector2.one;
            image.rectTransform.offsetMin = image.rectTransform.offsetMax = Vector2.zero;
            Canvas.ForceUpdateCanvases(); camera.Render(); input.Pointer = camera.WorldToScreenPoint(new Vector3(3, 0, 0));
            Check(image.depth >= 0, "UI blocking fixture has been actually rendered");
            Check(hook.TryLaunch(input.Pointer, true) == GreenHookLaunchResult.Ignored, "UI click never launches green hook");
            hook.Tick(input.Pointer, true); Check(!((LineRenderer)Get(hook, "aimRing")).enabled, "UI hides tolerance circle");
            Object.DestroyImmediate(canvas.gameObject); Object.DestroyImmediate(events);
        }
        private static void VerifyEnergyWhileAttached()
        {
            var simulation = Physics.simulationMode; Physics.simulationMode = SimulationMode.Script;
            var player = Spawn("Player/Player", new Vector3(30, 1, 0)).GetComponent<PlayerController>();
            player.GetComponent<PlayerInputDriver>().enabled = false;
            var red = Spawn("ContactCubes/Red_ContactCube", new Vector3(35, 1, 0));
            var foundation = Spawn("ColorBlocks/Prefabs/Red_Foundation", new Vector3(33, 1, 0));
            var feature = foundation.GetComponent<MechanismBaseFeature>(); Set(feature, "useGravity", false); Call(feature, "FixedUpdate");
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.transform.position = new Vector3(33, 0, 0); floor.transform.localScale = new Vector3(15, 1, 2);
            var hook = player.GetComponent<GreenHookAbility>(); var cube = red.GetComponent<ContactCube>();
            try
            {
                Physics.SyncTransforms(); Call(hook, "BeginHook", red.GetComponent<ContactCubeMotion>(), red.transform.position); Call(hook, "BeginPulling");
                bool poweredWhileAttached = false;
                for (int i = 0; i < 80; i++)
                {
                    Call(hook, "FixedUpdate"); Call(player, "FixedUpdate"); Physics.Simulate(Time.fixedDeltaTime);
                    Call(feature, "OnTick", Time.fixedDeltaTime);
                    poweredWhileAttached |= feature.IsSignalActive && hook.IsPulling;
                }
                Check(poweredWhileAttached && hook.IsHookActive && cube.IsHookSuspended && cube.IsEnergyAvailable,
                    "red pulled onto base powers it before manual release");
                Check(!feature.UsesGravity && !foundation.GetComponent<Rigidbody>().useGravity,
                    "no-gravity base preference survives energized pull");
                cube.SetCarrySuspended(true); Call(feature, "OnTick", Time.fixedDeltaTime);
                Check(!cube.IsEnergyAvailable && !feature.IsSignalActive, "carried red still cannot supply energy");
                cube.SetCarrySuspended(false); Call(feature, "OnTick", Time.fixedDeltaTime);
                Check(cube.IsEnergyAvailable && feature.IsSignalActive && hook.IsHookActive, "supply restored without breaking rope");
                red.GetComponentInChildren<BoxCollider>().enabled = false; Call(feature, "OnTick", Time.fixedDeltaTime);
                Check(!cube.IsEnergyAvailable && !feature.IsSignalActive, "disabled solid cannot supply energy");
                red.GetComponentInChildren<BoxCollider>().enabled = true;
            }
            finally
            {
                hook.CancelHook(); Object.DestroyImmediate(player.gameObject); Object.DestroyImmediate(red);
                Object.DestroyImmediate(foundation); Object.DestroyImmediate(floor); Physics.simulationMode = simulation;
            }
        }
        private static void VerifyCollectibleGravity()
        {
            var simulation = Physics.simulationMode; Physics.simulationMode = SimulationMode.Script;
            var item = Spawn("WorldItems/Collectible", new Vector3(60, 20, 0)).GetComponent<CollectiblePickup>();
            var body = item.GetComponent<Rigidbody>();
            try
            {
                Check(!body.useGravity && !body.isKinematic && item.CanBePushed, "collectible floats but stays dynamic and pushable");
                body.useGravity = true; item.ResetForRoom();
                Check(!body.useGravity, "old serialized gravity flag is upgraded on reset");
                float y = body.position.y; body.linearVelocity = Vector3.right * 2f;
                for (int i = 0; i < 50; i++) Physics.Simulate(Time.fixedDeltaTime);
                Check(Mathf.Abs(body.position.y - y) < .001f && body.position.x > 61,
                    "floating collectible can move horizontally without falling");
                item.gameObject.SetActive(false); item.gameObject.SetActive(true);
                Check(!body.useGravity && item.CanBePushed, "pooled reenable preserves no-gravity pickup contract");
            }
            finally { Object.DestroyImmediate(item.gameObject); Physics.simulationMode = simulation; }
        }
        private static void Finish(string result, bool success)
        {
            EditorApplication.update -= Tick; SessionState.EraseBool(Flag);
            File.WriteAllText("Library/GreenHookAim" + (SessionState.GetBool(Flag + "Baseline", false) ? "Baseline" : "") + ".txt", result);
            UnityEngine.Debug.Log(result); EditorApplication.Exit(success ? 0 : 1);
        }
        private sealed class AimInput : IInputSource
        {
            public Vector2 Pointer;
            public Vector2 PointerPosition => Pointer;
            public Vector2 PointerDelta => Vector2.zero;
            public InputDeviceMode ActiveDeviceMode => InputDeviceMode.KeyboardMouse;
            public bool HasKeyboard => true;
            public bool HasMouse => true;
            public bool HasGamepad => false;
            public bool HasTouch => false;
            public bool IsActionPressed(InputActionId action) => false;
            public bool WasActionPressedThisFrame(InputActionId action) => false;
            public bool WasActionTriggeredThisFrame(InputActionId action) => false;
            public bool WasActionReleasedThisFrame(InputActionId action) => false;
            public InputActionTrigger GetActionTrigger(InputActionId action) => InputActionTrigger.Press;
            public float ReadAxis(InputActionId action) => 0;
            public Vector2 ReadVector2(InputActionId action) => Vector2.zero;
        }
    }
}
