using System;
using System.IO;
using System.Linq;
using Project.BlockFeatures;
using Project.ColorBlocks;
using Project.ContactCubes;
using Project.InputAbstraction;
using Project.LevelEditor;
using Project.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Project.GreenHook.Editor
{
    /// <summary>Actual MRoom3 first base/cube, real input/update/physics, without saving the scene.</summary>
    public static class MRoom3HookEnergyAudit
    {
        private const string Flag = "MRoom3HookEnergyAudit";
        private static double deadline;
        private static int stage, checks;
        private static float next;
        private static PlayerController player;
        private static GreenHookAbility hook;
        private static ContactCube cube;
        private static MechanismBaseFeature foundation;
        private static MovingPlatformFeature platform;
        private static Camera camera;
        private static Vector3 platformStart;
        private static TestInput input;
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
            deadline = EditorApplication.timeSinceStartup + 120;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
        }
        private static void Check(bool value, string message)
        {
            if (!value) throw new Exception(message);
            checks++; Debug.Log("MROOM3 HOOK PASS " + message);
        }
        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup > deadline) { Finish("Timeout", false); return; }
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.time < next || Time.time < .5f) return;
            try
            {
                if (stage == 0)
                {
                    ColorRuntimeService.Instance.SetAllUnlockedForCurrentSession(true);
                    ColorRuntimeService.Instance.UnlockAllAbilitiesForCurrentSession();
                    // Nearby authored keys otherwise attract during this isolated
                    // mechanism test and correctly acquire their cutscene lock.
                    foreach (var key in Object.FindObjectsByType<ColorKeyPickup>(FindObjectsSortMode.None))
                        key.gameObject.SetActive(false);
                    player = Object.FindFirstObjectByType<PlayerController>(); hook = player.GetComponent<GreenHookAbility>();
                    foundation = Object.FindObjectsByType<MechanismBaseFeature>(FindObjectsSortMode.None)
                        .Single(b => b.GetComponent<LevelEditorPlacedBlock>().PlanningBoxId == "mroom3_room_v9h13tx4qb_0");
                    cube = Object.FindObjectsByType<ContactCube>(FindObjectsSortMode.None)
                        .Single(c => c.GetComponent<LevelEditorPlacedBlock>().PlanningBoxId == "mroom3_room_4pq8a262ag_0");
                    platform = Object.FindObjectsByType<MovingPlatformFeature>(FindObjectsSortMode.None)
                        .Single(p => p.GetComponent<LevelEditorPlacedBlock>().PlanningBoxId == "mroom3_room_ohsi3r0eyu_0");
                    Check(!foundation.UsesGravity, "author's first base remains no-gravity");
                    Check(cube.IsUnlocked && cube.IsEnergyAvailable && !foundation.IsSignalActive, "first red and unpowered base ready");
                    Vector3 start = new Vector3(foundation.transform.position.x - 1f, cube.transform.position.y, 0);
                    player.transform.position = start; player.Motor.position = start; player.Motor.linearVelocity = Vector3.zero;
                    // Use an independent test camera rather than fighting the scene camera manager.
                    foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) { cam.enabled = false; cam.tag = "Untagged"; }
                    camera = new GameObject("MRoom3HookTestCamera").AddComponent<Camera>(); camera.tag = "MainCamera";
                    camera.orthographic = true; camera.orthographicSize = 5; camera.transform.position = start + new Vector3(2, 0, -10);
                    input = new TestInput(); input.Pointer = camera.WorldToScreenPoint(cube.transform.position); GameInput.UseExternalSource(input);
                    Check(player.GetComponent<PlayerColorWheel>().TrySelectColor("green"), "green selected through existing wheel API");
                    platformStart = platform.transform.position;
                    stage = 1; next = Time.time + .2f;
                }
                else if (stage == 1)
                {
                    Check(!player.IsControlLocked && !player.IsDead, "fixture player can launch");
                    input.Pointer = camera.WorldToScreenPoint(cube.transform.position); input.ClickFrame = Time.frameCount + 2;
                    stage = 2; next = Time.time + 1.2f;
                }
                else if (stage == 2)
                {
                    Check(input.ClickReads > 0, "actual PlayerInputDriver consumed pointer click");
                    Check(hook.IsPulling && cube.IsHookSuspended && player.CanDriveGreenHook, "real input keeps first red attached");
                    Check(foundation.IsSignalActive && cube.IsEnergyAvailable, "first base powers before pressing Space");
                    Check(Vector3.Distance(platformStart, platform.transform.position) > .05f, "authored linked platform responds while rope remains attached");
                    Check(player.ControlLockDepth == 1, "energy signal does not detach or add control locks");
                    input.ReleaseFrame = Time.frameCount + 2;
                    stage = 3; next = Time.time + .2f;
                }
                else
                {
                    Check(!hook.IsHookActive && !player.IsControlLocked, "ordinary jump/Space still releases rope");
                    Check(foundation.IsSignalActive, "base stays powered after voluntary release");
                    Finish("PASS " + checks + " actual MRoom3 input/physics assertions", true);
                }
            }
            catch (Exception error) { Debug.LogException(error); Finish(error.ToString(), false); }
        }
        private static void Finish(string result, bool success)
        {
            GameInput.ClearExternalSource(input); hook?.CancelHook();
            EditorApplication.update -= Tick; SessionState.EraseBool(Flag);
            File.WriteAllText("Library/MRoom3HookEnergyAudit.txt", result); Debug.Log(result);
            EditorApplication.Exit(success ? 0 : 1);
        }
        private sealed class TestInput : IInputSource
        {
            public Vector2 Pointer;
            public int ClickFrame = -1, ReleaseFrame = -1;
            public int ClickReads;
            public Vector2 PointerPosition => Pointer;
            public Vector2 PointerDelta => Vector2.zero;
            public InputDeviceMode ActiveDeviceMode => InputDeviceMode.KeyboardMouse;
            public bool HasKeyboard => true;
            public bool HasMouse => true;
            public bool HasGamepad => false;
            public bool HasTouch => false;
            public bool IsActionPressed(InputActionId action) => false;
            public bool WasActionPressedThisFrame(InputActionId action) => WasActionTriggeredThisFrame(action);
            public bool WasActionTriggeredThisFrame(InputActionId action)
            {
                bool clicked = action == InputActionId.PointerPrimary && Time.frameCount == ClickFrame;
                if (clicked) ClickReads++;
                return clicked || action == InputActionId.Jump && Time.frameCount == ReleaseFrame;
            }
            public bool WasActionReleasedThisFrame(InputActionId action) => false;
            public InputActionTrigger GetActionTrigger(InputActionId action) => InputActionTrigger.Press;
            public float ReadAxis(InputActionId action) => 0;
            public Vector2 ReadVector2(InputActionId action) => Vector2.zero;
        }
    }
}
