using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Project.ColorBlocks;
using Project.BlockFeatures;
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
        private static GameObject target, second, floor, playerStop, targetStop;
        private static Camera camera;
        private static GreenHookAbility hook;
        private static HookInput input;
        private static int stage, variant, checks;
        private static float next;
        private static double deadline;
        private static Vector3 start, playerStart;
        private static readonly string[] Paths = { "ContactCubes/Red_ContactCube.prefab",
            "ContactCubes/Blue_ContactCube.prefab", "ColorBlocks/Prefabs/Red_Foundation.prefab" };

        public static void Run()
        {
            if (!Application.dataPath.Contains("FlowRetirementProbe")) throw new Exception("Isolated verification only");
            if (!Environment.GetCommandLineArgs().Contains("-noaudio")) throw new Exception("Verification requires -noaudio");
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
                else if (stage == 7)
                {
                    Check(!hook.IsHookActive && !player.IsControlLocked, "real loop swing can release");
                    Cleanup(); variant = 0; SetupBlockedPull(); stage = 8; next = Time.time + .15f;
                }
                else if (stage == 8)
                {
                    player.GetComponent<PlayerColorWheel>().TrySelectColor("green");
                    input.Pointer = camera.WorldToScreenPoint(target.transform.position); input.ClickFrame = Time.frameCount + 2;
                    stage = 9; next = Time.time + 1.2f;
                }
                else if (stage == 9)
                {
                    Check(hook.IsPulling && hook.IsPullingPlayer && player.CanDriveGreenHook && player.ControlLockDepth == 1,
                        "real loop both blocked keep rope " + variant);
                    Check((player.Motor.position - playerStart).sqrMagnitude < .001f &&
                        (target.GetComponent<Rigidbody>().position - start).sqrMagnitude < .001f,
                        "real loop both blocked do not penetrate " + variant);
                    Object.DestroyImmediate(playerStop); stage = 10; next = Time.time + .12f;
                }
                else if (stage == 10)
                {
                    Check(hook.IsPullingPlayer && player.Motor.position.x > playerStart.x + .2f &&
                        (target.GetComponent<Rigidbody>().position - start).sqrMagnitude < .001f,
                        "real loop resumes player pull " + variant);
                    playerStart = player.Motor.position;
                    Object.DestroyImmediate(targetStop); stage = 11; next = Time.time + .12f;
                }
                else if (stage == 11)
                {
                    Check(hook.IsPulling && !hook.IsPullingPlayer && target.GetComponent<Rigidbody>().position.x < start.x - .2f &&
                        (player.Motor.position - playerStart).sqrMagnitude < .001f,
                        "real loop restores target priority " + variant);
                    target.GetComponent<Rigidbody>().position = player.Motor.position + Vector3.right * 6f;
                    Physics.SyncTransforms(); stage = 12; next = Time.time + .1f;
                }
                else if (stage == 12)
                {
                    Check(hook.IsHookActive, "real loop six-cell separation keeps rope " + variant);
                    target.GetComponent<Rigidbody>().position = player.Motor.position + Vector3.right * 20f;
                    Physics.SyncTransforms(); stage = 13; next = Time.time + .1f;
                }
                else if (stage == 13)
                {
                    Check(hook.IsHookActive, "real loop transient overshoot keeps rope " + variant);
                    stage = 14; next = Time.time + .4f;
                }
                else if (stage == 14)
                {
                    Check(!hook.IsHookActive && !player.IsControlLocked && !player.IsGreenHookActive,
                        "real loop out-of-range releases " + variant);
                    Cleanup(); variant++;
                    if (variant < Paths.Length) { SetupBlockedPull(); stage = 8; next = Time.time + .15f; }
                    else { SetupPuzzlePassage(); stage = 15; next = Time.time + .15f; }
                }
                else if (stage == 15)
                {
                    player.GetComponent<PlayerColorWheel>().TrySelectColor("green");
                    input.Pointer = camera.WorldToScreenPoint(target.transform.position);
                    input.ClickFrame = Time.frameCount + 2;
                    stage = 16; next = Time.time + 1.2f;
                }
                else if (stage == 16)
                {
                    Check(hook.IsPulling && hook.IsPullingPlayer && player.CanDriveGreenHook,
                        "puzzle blocked red block pulls player across spike gap");
                    Check(player.Motor.position.x > 3.15f && player.Motor.position.x < 3.45f &&
                        player.Motor.position.y > 3.9f && !player.IsDead,
                        "puzzle reaches narrow shaft instead of dropping halfway onto spikes");
                    Check(Mathf.Abs(target.GetComponent<Rigidbody>().position.x - 4.8f) < .05f,
                        "puzzle red block remains physically blocked by pillar");
                    stage = 17; next = Time.time + .8f;
                }
                else if (stage == 17)
                {
                    Check(hook.IsHookActive && !player.IsDead && player.Motor.position.y > 3.9f,
                        "puzzle both blocked keeps player held until explicit release");
                    input.ReleaseFrame = Time.frameCount + 2;
                    stage = 18; next = Time.time + .8f;
                }
                else
                {
                    Check(!hook.IsHookActive && !player.IsControlLocked && !player.IsDead &&
                        player.IsSwimming && player.Motor.position.y < 3.2f,
                        "puzzle manual release enters underground water passage safely");
                    var signal = floor.GetComponentInChildren<MechanismBaseFeature>();
                    Check(target.GetComponent<ContactCube>().IsEnergyAvailable && signal.IsSignalActive,
                        "puzzle released red block still supplies later mechanism");
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

        private static void SetupBlockedPull()
        {
            SetupPull();
            playerStop = GameObject.CreatePrimitive(PrimitiveType.Cube);
            playerStop.transform.position = player.transform.position + Vector3.right * .7f;
            playerStop.transform.localScale = new Vector3(.4f, 4f, 2f);
            targetStop = GameObject.CreatePrimitive(PrimitiveType.Cube);
            targetStop.transform.position = target.transform.position - Vector3.right * .7f;
            targetStop.transform.localScale = new Vector3(.4f, 4f, 2f);
            playerStart = player.Motor.position; start = target.GetComponent<Rigidbody>().position;
            Physics.SyncTransforms();
        }

        private static void SetupPuzzlePassage()
        {
            input.Device = InputDeviceMode.KeyboardMouse;
            player = Instantiate("Player/Player.prefab", new Vector3(0, 4, 0)).GetComponent<PlayerController>();
            typeof(PlayerController).GetField("invulnerableUntil", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(player, 0f);
            hook = player.GetComponent<GreenHookAbility>();
            target = Instantiate("ContactCubes/Red_ContactCube.prefab", new Vector3(4.8f, 4, 0));
            floor = new GameObject("HookPuzzlePassage");
            PuzzleSolid("Departure", new Vector3(0, 2.5f, 0), new Vector3(1, 2, 2));
            PuzzleSolid("BlockedPillar", new Vector3(4.05f, 3.5f, 0), new Vector3(.5f, 7, 2));
            PuzzleSolid("RedSupport", new Vector3(4.8f, 1.5f, 0), new Vector3(1, 4, 2));
            PuzzleSolid("SpikeFloor", new Vector3(1.75f, .5f, 0), new Vector3(2, 1, 2));
            PuzzleSolid("WaterFloor", new Vector3(3.3f, -1, 0), new Vector3(1.1f, 1, 2));
            foreach (float x in new[] { 1.25f, 2.25f })
            {
                var spike = Instantiate("WorldItems/Spike_上.prefab", new Vector3(x, 1.5f, 0));
                spike.transform.SetParent(floor.transform, true);
            }
            second = Instantiate("ColorBlocks/Prefabs/Blue_WaterSource.prefab", new Vector3(3.3f, 1, 0));
            second.transform.localScale = new Vector3(1, 3, 1);
            var foundation = Instantiate("ColorBlocks/Prefabs/Red_Foundation.prefab", new Vector3(5.8f, 4, 0));
            foundation.transform.SetParent(floor.transform, true);
            var signal = foundation.GetComponent<MechanismBaseFeature>();
            typeof(MechanismBaseFeature).GetField("useGravity", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(signal, false);
            camera.transform.position = new Vector3(2, 3, -10);
            Physics.SyncTransforms();
        }

        private static void PuzzleSolid(string name, Vector3 position, Vector3 size)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube); obj.name = name;
            obj.transform.SetParent(floor.transform, false); obj.transform.position = position; obj.transform.localScale = size;
        }

        private static void Cleanup()
        {
            hook?.CancelHook();
            if (player != null) Object.DestroyImmediate(player.gameObject);
            if (target != null) Object.DestroyImmediate(target);
            if (second != null) Object.DestroyImmediate(second);
            if (floor != null) Object.DestroyImmediate(floor);
            if (playerStop != null) Object.DestroyImmediate(playerStop);
            if (targetStop != null) Object.DestroyImmediate(targetStop);
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
