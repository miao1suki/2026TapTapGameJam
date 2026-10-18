using System;
using System.IO;
using System.Linq;
using Project.BlockFeatures;
using Project.ColorBlocks;
using Project.ContactCubes;
using Project.LevelEditor;
using Project.Mechanisms;
using Project.Player;
using Project.Rooms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Silent, explicit Play verification in the dedicated R1 worktree.</summary>
public static class R1GameplayVerification
{
    private const string Flag = "ILorR.R1.GameplayAudit";
    private static double deadline;
    private static int stage, checks;
    private static float stageTime, lastProgress, beforeSwitch;
    private static PlayerController player;
    private static CrankPlatformFeature right, lower, left;
    private static MovingPlatformFeature platform;
    private static DoorController door;
    private static RigidbodyConstraints oldConstraints;
    private static IDisposable playerLock;
    private static Vector3 horizontalStart;
    private static bool IsIntegrationProbe => Application.dataPath.Replace('\\', '/')
        .Contains("/Library/R1R2Integration-FlowRetirementProbe-");

    public static void Run()
    {
        if ((!Application.dataPath.Replace('\\', '/').EndsWith("/2026TapTapGameJam-R1/Assets") && !IsIntegrationProbe) ||
            !Environment.GetCommandLineArgs().Contains("-noaudio")) throw new Exception("Silent R1 worktree only");
        EditorSceneManager.OpenScene(R1LevelBuilder.ScenePath);
        var authoredPlayer = Object.FindFirstObjectByType<PlayerController>();
        if (authoredPlayer == null) throw new Exception("R1 authored player missing");
        // Planner adjustments belong to the Scene; never rebuild from the older planning spawn.
        SessionState.SetFloat(Flag + ".SpawnX", authoredPlayer.transform.position.x);
        SessionState.SetFloat(Flag + ".SpawnY", authoredPlayer.transform.position.y);
        SessionState.SetFloat(Flag + ".SpawnZ", authoredPlayer.transform.position.z);
        SessionState.SetBool(Flag, true); Resume(); EditorApplication.EnterPlaymode();
    }
    [InitializeOnLoadMethod] private static void Resume()
    {
        if (!SessionState.GetBool(Flag, false)) return;
        stage = checks = 0; deadline = EditorApplication.timeSinceStartup + 180;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
    }
    private static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new Exception(message);
    }
    private static void MoveTo(Vector3 point)
    {
        player.Motor.position = point; player.transform.position = point; Physics.SyncTransforms();
    }
    private static void Next() { stage++; stageTime = Time.time; }
    private static CrankPlatformFeature Crank(string id) => Object.FindObjectsByType<CrankPlatformFeature>(FindObjectsSortMode.None)
        .Single(c => c.GetComponent<LevelEditorPlacedBlock>().PlanningBoxId == id);
    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Play verification timed out");
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.time < 1f) return;
            float elapsed = Time.time - stageTime;
            if (stage == 0)
            {
                var service = ColorRuntimeService.Instance;
                Check(new[] { "red", "green", "blue" }.All(service.IsUnlocked), "all three scene colors unlocked");
                Check(service.IsAbilityUnlocked("red") && !service.IsAbilityUnlocked("green") && !service.IsAbilityUnlocked("blue"), "red-only player ability");
                player = Object.FindFirstObjectByType<PlayerController>();
                var authoredSpawn = new Vector3(SessionState.GetFloat(Flag + ".SpawnX", float.NaN),
                    SessionState.GetFloat(Flag + ".SpawnY", float.NaN), SessionState.GetFloat(Flag + ".SpawnZ", float.NaN));
                Check(!player.IsDead && Vector3.Distance(player.transform.position, authoredSpawn) < .2f, "standard player stands safely at the saved authored spawn");
                right = Crank("r1_crank_right"); lower = Crank("r1_crank_lower"); left = Crank("r1_crank_left");
                platform = Object.FindObjectsByType<MovingPlatformFeature>(FindObjectsSortMode.None).Single(p => p.CrankController == right);
                door = Object.FindFirstObjectByType<DoorController>();
                Check(door.State == DoorState.Locked && door.Buttons.All(b => !b.IsPressed), "door initially locked");
                Check(!right.BindSharedController(right) && !right.BindSharedController(lower), "self and cyclic sharing rejected");
                var bar = Object.FindObjectsByType<ContactCube>(FindObjectsSortMode.None).Single(c => c.GetComponent<LevelEditorPlacedBlock>().PlanningBoxId == "r1_green_horizontal");
                Check(bar.Length == 13 && bar.GetComponent<ContactCubeMotion>().IsAuthoredFixed, "fixed bar starts at 13 cells");
                var bounds = bar.GetComponentInChildren<BoxCollider>().bounds;
                Check(Mathf.Abs(bounds.min.x - 18) < .01f && Mathf.Abs(bounds.max.x - 31) < .01f && Mathf.Abs(bounds.min.y + 25) < .01f, "bar occupies exact X30:AJ30 without geometry scaling");
                if (!IsIntegrationProbe) CaptureOverview();
                playerLock = player.AcquireControlLock(typeof(R1GameplayVerification));
                oldConstraints = player.Motor.constraints; player.Motor.constraints = RigidbodyConstraints.FreezeAll;
                MoveTo(lower.transform.position + Vector3.right);
                Check(lower.TryInteract(player.gameObject), "secondary handle accepts nearby player");
                lastProgress = right.Progress; Next();
            }
            else if (stage == 1)
            {
                lower.HoldInteract(player.gameObject);
                Check(right.Progress + .001f >= lastProgress && Mathf.Abs(right.Progress - lower.Progress) < .0001f, "secondary advances same monotonic progress");
                lastProgress = right.Progress;
                if (elapsed < .7f) return;
                Check(right.Progress > .15f && right.Progress < .5f, "secondary increases progress once, not twice per frame");
                Check(Mathf.Abs(platform.transform.position.y - (-30.5f + 14 * right.Progress)) < .35f, "right platform follows real shared progress");
                beforeSwitch = right.Progress; lower.EndInteract(player.gameObject);
                MoveTo(right.transform.position + Vector3.left);
                Check(right.TryInteract(player.gameObject), "primary handle accepts player after switching");
                Check(Mathf.Abs(right.Progress - beforeSwitch) < .001f, "switching handles does not reset progress");
                Next();
            }
            else if (stage == 2)
            {
                right.HoldInteract(player.gameObject);
                Check(right.Progress + .001f >= lastProgress && Mathf.Abs(right.Progress - lower.Progress) < .0001f, "primary continues shared progress");
                lastProgress = right.Progress;
                if (elapsed < .5f) return;
                Check(right.Progress > beforeSwitch + .1f, "primary continues from secondary position");
                right.EndInteract(player.gameObject); beforeSwitch = right.Progress; Next();
            }
            else if (stage == 3)
            {
                if (elapsed < .9f) return;
                Check(right.Progress < beforeSwitch - .03f && Mathf.Abs(right.Progress - lower.Progress) < .0001f, "one shared rebound after both released");
                // Existing single-handle path is still independent.
                MoveTo(left.transform.position + Vector3.left);
                Check(left.TryInteract(player.gameObject), "left single crank still accepts input");
                Next();
            }
            else if (stage == 4)
            {
                left.HoldInteract(player.gameObject);
                if (elapsed < .4f) return;
                Check(left.Progress > .08f && left.SharedController == null, "unshared crank retains original behavior");
                left.EndInteract(player.gameObject);
                Check(door.Buttons[0].TryPress(player.gameObject), "first latched button pressed");
                Next();
            }
            else if (stage == 5)
            {
                if (elapsed < 1f) return;
                Check(door.State == DoorState.Locked && door.Buttons[0].IsPressed && !door.Buttons[1].IsPressed, "one button cannot open exit");
                Check(door.Buttons[1].TryPress(player.gameObject), "second latched button pressed");
                Next();
            }
            else if (stage == 6)
            {
                if (elapsed < 1.2f) return;
                Check(door.State == DoorState.Unlocked && door.Buttons.All(b => b.IsPressed), "both buttons open exit");
                MoveTo(new Vector3(5.5f, -34.5f, 0));
                RoomLifecycleManager.ResetCurrentRooms(); Next();
            }
            else if (stage == 7)
            {
                if (elapsed < .5f) return;
                Check(door.State == DoorState.Unlocked && door.Buttons.All(b => b.IsPressed), "room reset preserves latched door progress");
                Check(right.Progress < .001f && lower.Progress < .001f && left.Progress < .001f, "room reset restores both crank groups");
                var service = ColorRuntimeService.Instance;
                Check(service.IsUnlocked("blue") && service.IsAbilityUnlocked("red") && !service.IsAbilityUnlocked("green"), "reset preserves starting abilities");
                playerLock?.Dispose(); playerLock = null;
                player.Motor.constraints = oldConstraints;
                player.GetComponent<PlayerInputDriver>().enabled = false;
                MoveTo(new Vector3(24.5f, -25.515f, 0)); player.Motor.linearVelocity = Vector3.zero;
                Next();
            }
            else if (stage == 8)
            {
                if (elapsed < .3f) return;
                Check(player.IsHorizontalClimbing, "player attaches to underside of fixed green bar");
                horizontalStart = player.transform.position; Next();
            }
            else if (stage == 9)
            {
                player.SetMoveInput(Vector2.right);
                if (elapsed < .5f) return;
                Check(player.transform.position.x > horizontalStart.x + .4f && Mathf.Abs(player.transform.position.y - horizontalStart.y) < .15f, "horizontal traversal maintains underside height");
                player.SetMoveInput(Vector2.zero); player.RequestJump(); Next();
            }
            else if (stage == 10)
            {
                if (elapsed < .12f) return;
                Check(!player.IsHorizontalClimbing, "jump detaches from horizontal bar");
                var bar = Object.FindObjectsByType<ContactCube>(FindObjectsSortMode.None).Single(c => c.GetComponent<LevelEditorPlacedBlock>().PlanningBoxId == "r1_green_horizontal");
                Check(Vector3.Distance(bar.transform.position, new Vector3(18.5f, -24.5f, 0)) < .001f, "fixed bar never falls or drifts");
                Finish(true, $"PASS {checks} assertions in actual Play; saved authored spawn, starting colors, shared handles/switch/rebound, independent left crank, all-buttons door, room reset, exact fixed 13-cell bar, real underside attach/traverse/jump release. Door activation uses TryPress API, not a full player route completion. No render acceptance claimed; integration skips offscreen overview.");
            }
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }
    private static void CaptureOverview()
    {
        var root = new GameObject("R1 verification overview");
        var camera = root.AddComponent<Camera>();
        camera.orthographic = true; camera.orthographicSize = 22;
        camera.transform.position = new Vector3(29, -20.5f, -80);
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.12f, .15f, .19f);
        var target = new RenderTexture(1740, 1320, 24); target.Create(); camera.targetTexture = target;
        camera.Render();
        var previous = RenderTexture.active; RenderTexture.active = target;
        var pixels = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); pixels.Apply();
        File.WriteAllBytes("Library/R1Overview.png", pixels.EncodeToPNG());
        RenderTexture.active = previous; camera.targetTexture = null;
        Object.DestroyImmediate(pixels); Object.DestroyImmediate(root); target.Release(); Object.DestroyImmediate(target);
    }
    private static void Finish(bool success, string report)
    {
        EditorApplication.update -= Tick; SessionState.EraseBool(Flag);
        if (lower != null && player != null) lower.EndInteract(player.gameObject);
        if (right != null && player != null) right.EndInteract(player.gameObject);
        if (left != null && player != null) left.EndInteract(player.gameObject);
        playerLock?.Dispose(); playerLock = null;
        if (player != null) player.Motor.constraints = oldConstraints;
        File.WriteAllText("Library/R1GameplayVerification.txt", report);
        if (success) Debug.Log(report); else Debug.LogError(report);
        EditorApplication.Exit(success ? 0 : 1);
    }
}
