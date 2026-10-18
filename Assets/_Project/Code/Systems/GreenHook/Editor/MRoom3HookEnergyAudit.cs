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
    /// <summary>Actual MRoom3 power contact and filled lava pit; real input/physics, no scene save.</summary>
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
        private static Vector3 platformStart, heldCubePosition, freeWalkStart;
        private static ContactCube pitCube;
        private static LavaHazardFeature pitLava;
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
                    // Match the video: player left of the closed vertical gate,
                    // not overlapping that gate one cell left of the base.
                    Vector3 start = new Vector3(foundation.transform.position.x - 2.5f, cube.transform.position.y, 0);
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
                    Check(hook.IsPullPausedForPower, "new power contact pauses continued pulling without unhooking");
                    heldCubePosition = cube.transform.position;
                    stage = 3; next = Time.time + 2f;
                }
                else if (stage == 3)
                {
                    Check(foundation.IsSignalActive && hook.IsPullPausedForPower && hook.IsHookActive,
                        "actual authored gate remains powered after two further seconds without manual release");
                    Check(Vector3.Distance(heldCubePosition, cube.transform.position) < .04f,
                        "red cube is not pulled back off the socket as the gate rises");
                    freeWalkStart = player.transform.position;
                    input.Move = Vector2.left;
                    stage = 4; next = Time.time + .18f;
                }
                else if (stage == 4)
                {
                    Check(player.transform.position.x < freeWalkStart.x - .05f && player.ControlLockDepth == 1,
                        "real input allows ordinary walking while powered rope remains attached");
                    input.Move = Vector2.zero;
                    heldCubePosition = cube.transform.position;
                    input.Pointer = camera.WorldToScreenPoint(cube.transform.position);
                    input.ClickFrame = Time.frameCount + 2;
                    stage = 5; next = Time.time + .3f;
                }
                else if (stage == 5)
                {
                    Check(hook.IsHookActive && !hook.IsPullPausedForPower &&
                        Vector3.Distance(heldCubePosition, cube.transform.position) > .05f,
                        "real second click resumes pulling the same connected red cube");
                    input.ReleaseFrame = Time.frameCount + 2;
                    stage = 6; next = Time.time + .2f;
                }
                else if (stage == 6)
                {
                    Check(!hook.IsHookActive && !player.IsControlLocked, "ordinary jump/Space still releases rope");
                    Check(!hook.IsPullPausedForPower, "manual release clears power-pause state");
                    pitCube = Object.FindObjectsByType<ContactCube>(FindObjectsSortMode.None)
                        .Single(c => c.GetComponent<LevelEditorPlacedBlock>().PlanningBoxId == "mroom3_room_6txw6s17q6_0");
                    pitLava = Object.FindObjectsByType<LavaHazardFeature>(FindObjectsSortMode.None)
                        .Single(c => c.GetComponent<LevelEditorPlacedBlock>().PlanningBoxId == "mroom3_room_xp50gt1f2f_0");
                    Vector3 start = new Vector3(pitLava.transform.position.x - 2, pitCube.transform.position.y, 0);
                    player.transform.position = start; player.Motor.position = start; player.Motor.linearVelocity = Vector3.zero;
                    camera.transform.position = start + new Vector3(3, 0, -10);
                    stage = 7; next = Time.time + .2f;
                }
                else if (stage == 7)
                {
                    Check(!player.IsDead && !player.IsInvulnerable, "actual lava fixture has no death/invulnerability masking");
                    input.Pointer = camera.WorldToScreenPoint(pitCube.transform.position);
                    input.ClickFrame = Time.frameCount + 2;
                    stage = 8; next = Time.time + 1.6f;
                }
                else if (stage == 8)
                {
                    Check(hook.IsPulling && !hook.IsPullPausedForPower, "ordinary pit pull has no spurious base pause");
                    Check(Mathf.Abs(pitCube.transform.position.x - pitLava.transform.position.x) < .08f &&
                        pitCube.transform.position.y < pitLava.transform.position.y + 1.35f,
                        "real input lets authored red cube fall into the one-cell lava pit: cube=" +
                        pitCube.transform.position + " body=" + pitCube.GetComponent<Rigidbody>().position +
                        " lava=" + pitLava.transform.position + " player=" + player.Motor.position);
                    // Exercise the same old-target release collision transition
                    // as the video's target switch, without a second pull moving
                    // the fixture actor away from the infill during landing.
                    // Only the actor is positioned; neither cube nor settling is forced.
                    input.ReleaseFrame = Time.frameCount + 2;
                    Vector3 landing = new Vector3(pitLava.transform.position.x, pitCube.transform.position.y + 2.4f, 0);
                    player.transform.position = landing; player.Motor.position = landing;
                    player.Motor.linearVelocity = Vector3.down;
                    stage = 9; next = Time.time + 1.5f;
                }
                else
                {
                    Check(!player.IsDead && !player.IsInvulnerable, "landing on authored infill survives without immunity");
                    Check(player.IsGrounded && Mathf.Abs(pitCube.transform.position.y - pitLava.transform.position.y) < .06f &&
                        Mathf.Abs(player.Motor.position.y - pitCube.transform.position.y - 1) < .12f,
                        "player is physically supported above the filled lava pit, not passing through it: player=" +
                        player.Motor.position + " cube=" + pitCube.transform.position + " grounded=" + player.IsGrounded);
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
            public Vector2 Pointer, Move;
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
            public Vector2 ReadVector2(InputActionId action) => action == InputActionId.Move ? Move : Vector2.zero;
        }
    }
}
