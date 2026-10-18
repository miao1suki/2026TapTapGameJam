using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Project.ColorBlocks;
using Project.InputAbstraction;
using Project.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

namespace Project.GreenHook.Editor
{
    // Deliberately profiles the authored scene, not thousands of isolated Tick calls.
    public static class GreenHookSceneProfile
    {
        private const string Flag = "GreenHookSceneProfile";
        private static double deadline;
        internal static bool NativeInput => SessionState.GetBool(Flag + ".Native", false);
        public static void RunNative()
        {
            SessionState.SetBool(Flag + ".Native", true);
            Run();
        }
        public static void Run()
        {
            if (!Application.dataPath.Contains("/Library/FlowRetirementProbe/Assets") ||
                !Environment.GetCommandLineArgs().Contains("-noaudio")) throw new Exception("Silent isolated project only");
            EditorSceneManager.OpenScene("Assets/Scenes/MRoom3.unity");
            SessionState.SetBool(Flag, true); Resume(); EditorApplication.EnterPlaymode();
        }
        [InitializeOnLoadMethod] private static void Resume()
        {
            if (!SessionState.GetBool(Flag, false)) return;
            deadline = EditorApplication.timeSinceStartup + 240;
            EditorApplication.update -= Setup; EditorApplication.update += Setup;
        }
        private static void Setup()
        {
            if (EditorApplication.timeSinceStartup > deadline) { Finish("Timeout", false); return; }
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.time < .5f) return;
            EditorApplication.update -= Setup;
            try { new GameObject("SceneProfile").AddComponent<GreenHookSceneProfileLoop>(); }
            catch (Exception e) { Finish(e.ToString(), false); }
        }
        internal static void Finish(string report, bool success)
        {
            ProfilerDriver.enabled = false;
            SessionState.EraseBool(Flag); EditorApplication.update -= Setup;
            SessionState.EraseBool(Flag + ".Native");
            File.WriteAllText("Library/GreenHookSceneProfile.txt", report);
            Debug.Log(report); EditorApplication.Exit(success ? 0 : 1);
        }
    }

    [DefaultExecutionOrder(-200)]
    public sealed class GreenHookSceneProfileLoop : MonoBehaviour, IInputSource
    {
        private int frame, stage;
        private PlayerController player;
        private GreenHookAbility hook;
        private Camera camera;
        private RenderTexture target;
        private Shader originalShader;
        private Material ropeMaterial;
        private double elapsed;
        private readonly List<double> times = new List<double>();
        private readonly StringBuilder report = new StringBuilder();
        private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;
        private Vector2 pointer;
        private Mouse simulatedMouse;
        private int queuedEvents;
        private void Awake()
        {
            Application.targetFrameRate = -1; QualitySettings.vSyncCount = 0;
            ColorRuntimeService.Instance.SetAllUnlockedForCurrentSession(true);
            ColorRuntimeService.Instance.UnlockAllAbilitiesForCurrentSession();
            foreach (var key in Object.FindObjectsByType<ColorKeyPickup>(FindObjectsSortMode.None)) key.gameObject.SetActive(false);
            player = Object.FindFirstObjectByType<PlayerController>(); hook = player.GetComponent<GreenHookAbility>();
            camera = Camera.main;
            target = new RenderTexture(3840, 2160, 24); target.Create(); camera.targetTexture = target;
            typeof(PlayerController).GetField("invulnerableUntil", Hidden).SetValue(player, float.PositiveInfinity);
            player.Motor.constraints = RigidbodyConstraints.FreezeAll;
            // Keep all authored objects and behaviours; only select the existing ability.
            if (GreenHookSceneProfile.NativeInput)
            {
                simulatedMouse = InputSystem.AddDevice<Mouse>();
                simulatedMouse.MakeCurrent();
                InputSystem.onBeforeUpdate += QueueMouseMotion;
            }
            else GameInput.UseExternalSource(this);
            player.GetComponent<PlayerColorWheel>().TrySelectColor("green");
            report.AppendLine($"Authored MRoom3: {Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Length} behaviours, " +
                $"{Object.FindObjectsByType<Collider>(FindObjectsSortMode.None).Length} colliders; GPU {SystemInfo.graphicsDeviceName}; target={target.width}x{target.height}; native={simulatedMouse != null}");
            BeginStage();
        }
        private void BeginStage()
        {
            times.Clear(); frame = 0;
            ProfilerDriver.ClearAllFrames(); ProfilerDriver.enabled = true;
            elapsed = UnityEditor.EditorApplication.timeSinceStartup;
        }
        private void Update()
        {
            try
            {
                frame++;
                Vector3 center = player.transform.position + Vector3.up * .3f;
                float angle = frame * .28f;
                pointer = camera.WorldToScreenPoint(center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * 4);
                if (frame > 90) times.Add(Time.unscaledDeltaTime * 1000d);
                if (frame == 5 && stage == 0)
                {
                    var line = (LineRenderer)typeof(GreenHookAbility).GetField("line", Hidden).GetValue(hook);
                    ropeMaterial = line.sharedMaterial; originalShader = ropeMaterial.shader;
                    var request = UnityEngine.Rendering.AsyncGPUReadback.Request(target, 0);
                    request.WaitForCompletion();
                    if (!request.hasError) File.WriteAllBytes("Library/GreenHookSceneProfile.raw", request.GetData<byte>().ToArray());
                }
                if (frame < 600) return;
                ProfilerDriver.enabled = false;
                var samples = new Dictionary<string, double>();
                var totals = new Dictionary<string, double>();
                int count = 0;
                for (int f = ProfilerDriver.firstFrameIndex + 90; f <= ProfilerDriver.lastFrameIndex; f++)
                {
                    using var data = ProfilerDriver.GetRawFrameDataView(f, 0);
                    if (!data.valid) continue; count++;
                    for (int s = 0; s < data.sampleCount; s++)
                    {
                        string name = data.GetSampleName(s); double ms = data.GetSampleTimeMs(s);
                        if (!samples.TryGetValue(name, out double max) || ms > max) samples[name] = ms;
                        totals.TryGetValue(name, out double total); totals[name] = total + ms;
                    }
                }
                times.Sort();
                string label = stage == 0 ? "green normal" : stage == 1 ? "green simple shader" : stage == 2 ? "ability hidden" :
                    stage == 3 ? "green and editor window" : stage == 4 ? "red and editor window" : "blue and editor window";
                report.AppendLine($"Stage {stage}: {label}; " +
                    $"wall={(EditorApplication.timeSinceStartup - elapsed):F2}s frames={frame}; " +
                    $"dt median={times[times.Count / 2]:F2} p95={times[(int)(times.Count * .95)]:F2} max={times.Last():F2}ms; profiler frames={count}");
                foreach (var pair in totals.OrderByDescending(p => p.Value).Take(65))
                    report.AppendLine($"avg={pair.Value / Math.Max(1, count):F3} max={samples[pair.Key]:F3}ms {pair.Key}");
                if (++stage == 6)
                {
                    report.AppendLine($"Native mouse events queued: {queuedEvents}; source pointer={GameInput.PointerPosition}; last queued={pointer}");
                    GameInput.ClearExternalSource(this); GreenHookSceneProfile.Finish(report.ToString(), true); return;
                }
                if (stage == 1) ropeMaterial.shader = Shader.Find("Sprites/Default");
                if (stage == 2) { ropeMaterial.shader = originalShader; player.GetComponent<PlayerColorWheel>().ClearSelectedColor(); }
                if (stage == 3)
                {
                    player.GetComponent<PlayerColorWheel>().TrySelectColor("green");
                    EditorWindow.GetWindow<PlanningEditorPrototype.PlanningEditorWindow>();
                }
                if (stage == 4) player.GetComponent<PlayerColorWheel>().TrySelectColor("red");
                if (stage == 5) player.GetComponent<PlayerColorWheel>().TrySelectColor("blue");
                BeginStage();
            }
            catch (Exception e) { GreenHookSceneProfile.Finish(report + "\n" + e, false); }
        }
        private void QueueMouseMotion()
        {
            if (InputState.currentUpdateType != InputUpdateType.Dynamic || player == null) return;
            Vector2 previous = simulatedMouse.position.ReadValue();
            // Exercise real Input System event merging, Point actions and EventSystem.
            // A virtual device avoids moving the user's actual desktop cursor.
            for (int i = 0; i < 64; i++)
            {
                float angle = (frame + i / 64f) * .28f;
                Vector3 center = player.transform.position + Vector3.up * .3f;
                pointer = camera.WorldToScreenPoint(center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * 4);
                InputSystem.QueueStateEvent(simulatedMouse, new MouseState { position = pointer, delta = pointer - previous });
                previous = pointer; queuedEvents++;
            }
        }
        private void OnDestroy()
        {
            InputSystem.onBeforeUpdate -= QueueMouseMotion;
            if (simulatedMouse != null && simulatedMouse.added) InputSystem.RemoveDevice(simulatedMouse);
        }
        public Vector2 PointerPosition => pointer;
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
