using System;
using System.IO;
using System.Reflection;
using Project.ColorBlocks;
using Project.ContactCubes;
using Project.InputAbstraction;
using Project.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Project.GreenHook.Editor
{
    /// <summary>Runs the actual Update/FixedUpdate/input order, without manually driving physics.</summary>
    public static class GreenHookLiveVerification
    {
        private const string SessionKey = "GreenHookLiveVerification";
        private static PlayerController player;
        private static GameObject target, second, floor;
        private static Camera camera;
        private static GreenHookAbility hook;
        private static HookInput input;
        private static int stage, variant, checks;
        private static float next;
        private static double deadline;
        private static Vector3 start;
        private static readonly string[] Paths = { "ContactCubes/Red_ContactCube.prefab",
            "ContactCubes/Blue_ContactCube.prefab", "ColorBlocks/Prefabs/Red_Foundation.prefab" };

        public static void Run()
        {
            if (!Application.dataPath.Contains("FlowRetirementProbe")) throw new Exception("Isolated verification only");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            SessionState.SetBool(SessionKey, true); Resume(); EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!SessionState.GetBool(SessionKey, false)) return;
            deadline = EditorApplication.timeSinceStartup + 120;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
        }

        private static GameObject Instantiate(string path, Vector3 position) =>
            Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Content/" + path), position, Quaternion.identity);
        private static void Check(bool success, string label)
        {
            if (!success) throw new Exception(label);
            checks++; Debug.Log("HOOK LIVE PASS " + label);
        }

        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup > deadline) { Finish(false, "Timed out"); return; }
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.time < next) return;
            try
            {
                if (stage == 0)
                {
                    ColorRuntimeService.Instance.SetAllUnlockedForCurrentSession(true);
                    ColorRuntimeService.Instance.UnlockAllAbilitiesForCurrentSession();
                    camera = new GameObject("HookLiveCamera").AddComponent<Camera>();
                    camera.gameObject.AddComponent<AudioListener>();
                    camera.tag = "MainCamera"; camera.orthographic = true; camera.orthographicSize = 7;
                    camera.transform.position = new Vector3(1, 1, -10);
                    input = new HookInput(); GameInput.UseExternalSource(input);
                    SetupPull(); stage = 1; next = Time.time + .15f;
                }
                else if (stage == 1)
                {
                    start = target.GetComponent<Rigidbody>().position;
                    player.GetComponent<PlayerColorWheel>().TrySelectColor("green");
                    input.Pointer = camera.WorldToScreenPoint(start); input.ClickFrame = Time.frameCount + 2;
                    stage = 2; next = Time.time + .8f;
                }
                else if (stage == 2)
                {
                    Check(hook.IsPulling && player.CanDriveGreenHook, "real loop attached " + variant);
                    Check(target.GetComponent<Rigidbody>().position.x < start.x - .7f, "real loop target moves " + variant);
                    Check(player.ControlLockDepth == 1, "real loop owns one control handle " + variant);
                    input.ReleaseFrame = Time.frameCount + 2; stage = 3; next = Time.time + .1f;
                }
                else if (stage == 3)
                {
                    Check(!hook.IsHookActive && !player.IsControlLocked, "real loop interact releases " + variant);
                    Cleanup(); variant++;
                    if (variant < Paths.Length) { SetupPull(); stage = 1; next = Time.time + .15f; }
                    else { SetupSwing(); stage = 4; next = Time.time + .15f; }
                }
                else if (stage == 4)
                {
                    player.GetComponent<PlayerColorWheel>().TrySelectColor("green");
                    input.Move = Vector2.right;
                    input.Pointer = camera.WorldToScreenPoint(target.transform.position); input.ClickFrame = Time.frameCount + 2;
                    stage = 5; next = Time.time + .6f;
                }
                else if (stage == 5)
                {
                    Check(hook.IsSwinging && player.Motor.linearVelocity.sqrMagnitude > .1f, "real loop swing has momentum");
                    input.Pointer = camera.WorldToScreenPoint(second.transform.position); input.ClickFrame = Time.frameCount + 2;
                    stage = 6; next = Time.time + .1f;
                }
                else if (stage == 6)
                {
                    Transform actual = (Transform)typeof(GreenHookAbility).GetField("targetTransform", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(hook);
                    Check(actual == second.transform && hook.IsSwinging, "real loop left click changes anchor");
                    Check(player.ControlLockDepth == 1 && player.Motor.linearVelocity.sqrMagnitude > .1f,
                        "real loop retarget keeps movement and single lock");
                    input.ReleaseFrame = Time.frameCount + 2; stage = 7; next = Time.time + .1f;
                }
                else
                {
                    Check(!hook.IsHookActive && !player.IsControlLocked, "real loop swing can release");
                    Finish(true, "PASS " + checks);
                }
            }
            catch (Exception error) { Debug.LogException(error); Finish(false, error.ToString()); }
        }

        private static void SetupPull()
        {
            input.Device = variant == 0 ? InputDeviceMode.KeyboardMouse : variant == 1 ? InputDeviceMode.Gamepad : InputDeviceMode.Touch;
            player = Instantiate("Player/Player.prefab", new Vector3(0, .5f, 0)).GetComponent<PlayerController>();
            hook = player.GetComponent<GreenHookAbility>();
            target = Instantiate(Paths[variant], new Vector3(3, .5f, 0));
            floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = new Vector3(1, -.5f, 0); floor.transform.localScale = new Vector3(16, 1, 2);
        }

        private static void SetupSwing()
        {
            input.Device = InputDeviceMode.KeyboardMouse;
            player = Instantiate("Player/Player.prefab", new Vector3(0, .5f, 0)).GetComponent<PlayerController>();
            hook = player.GetComponent<GreenHookAbility>();
            target = Instantiate("ContactCubes/Green_ContactCube.prefab", new Vector3(0, 4, 0));
            second = Instantiate("ContactCubes/Green_ContactCube.prefab", new Vector3(2, 4, 0));
            floor = new GameObject("HookLiveSupports");
            foreach (var position in new[] { new Vector3(0, 3.25f, 0), new Vector3(2, 3.25f, 0), new Vector3(1, -.5f, 0) })
            {
                var support = GameObject.CreatePrimitive(PrimitiveType.Cube);
                support.transform.SetParent(floor.transform); support.transform.position = position;
                support.transform.localScale = position.y < 0 ? new Vector3(16, 1, 2) : new Vector3(1, .5f, 1);
            }
        }

        private static void Cleanup()
        {
            hook?.CancelHook();
            if (player != null) Object.DestroyImmediate(player.gameObject);
            if (target != null) Object.DestroyImmediate(target);
            if (second != null) Object.DestroyImmediate(second);
            if (floor != null) Object.DestroyImmediate(floor);
            input.Move = Vector2.zero;
        }

        private static void Finish(bool success, string result)
        {
            GameInput.ClearExternalSource(input); Cleanup();
            File.WriteAllText(Path.Combine(Application.dataPath, "../Library/GreenHookLiveVerification.txt"), result);
            SessionState.SetBool(SessionKey, false); EditorApplication.update -= Tick; Time.timeScale = 1;
            EditorApplication.Exit(success ? 0 : 1);
        }

        private sealed class HookInput : IInputSource
        {
            public InputDeviceMode Device;
            public Vector2 Pointer, Move;
            public int ClickFrame = -1, ReleaseFrame = -1;
            public InputDeviceMode ActiveDeviceMode => Device;
            public bool IsActionPressed(InputActionId action) => false;
            public bool WasActionPressedThisFrame(InputActionId action) => WasActionTriggeredThisFrame(action);
            public bool WasActionTriggeredThisFrame(InputActionId action) => action == InputActionId.PointerPrimary && Time.frameCount == ClickFrame ||
                action == InputActionId.Interact && Time.frameCount == ReleaseFrame;
            public bool WasActionReleasedThisFrame(InputActionId action) => false;
            public InputActionTrigger GetActionTrigger(InputActionId action) => InputActionTrigger.Press;
            public float ReadAxis(InputActionId action) => 0;
            public Vector2 ReadVector2(InputActionId action) => action == InputActionId.Move ? Move : Vector2.zero;
            public Vector2 PointerPosition => Pointer;
            public Vector2 PointerDelta => Vector2.zero;
            public bool HasKeyboard => Device == InputDeviceMode.KeyboardMouse;
            public bool HasMouse => HasKeyboard;
            public bool HasGamepad => Device == InputDeviceMode.Gamepad;
            public bool HasTouch => Device == InputDeviceMode.Touch;
        }
    }
}
