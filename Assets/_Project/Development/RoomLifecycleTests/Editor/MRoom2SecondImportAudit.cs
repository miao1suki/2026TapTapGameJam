using System;
using System.Collections.Generic;
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

/// <summary>Real PlayMode regression for the named MRoom2 second import.</summary>
public static class MRoom2SecondImportAudit
{
    private const string Flag = "MRoom2SecondImportAudit";
    private static int phase, basisIndex, checks;
    private static float due;
    private static double started;
    private static PlayerController player;
    private static RoomLifecycleManager rooms;
    private static ContactCube red;
    private static MechanismBaseFeature[] bases;
    private static MovingPlatformFeature[] platforms;
    private static readonly Dictionary<MovingPlatformFeature, Vector3> origins = new();
    private static readonly Dictionary<MovingPlatformFeature, float> poweredDistances = new();
    private static DoorController[] doors;
    private static MovingPlatformFeature support, ridden;
    private static float riderOffset;
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
        checks++;
    }
    public static void Run()
    {
        if (!Application.dataPath.Replace('\\', '/').Contains("/Library/FlowRetirementProbe/Assets"))
            throw new InvalidOperationException("Isolated project only");
        EditorSceneManager.OpenScene(MRoom2MapImport.ScenePath);
        SessionState.SetBool(Flag, true); Resume(); EditorApplication.EnterPlaymode();
    }
    [InitializeOnLoadMethod] private static void Resume()
    {
        if (!SessionState.GetBool(Flag, false)) return;
        phase = basisIndex = checks = 0; due = 1; started = EditorApplication.timeSinceStartup;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
    }
    private static void MoveRed(Vector3 position)
    {
        red.transform.position = position; red.GetComponent<Rigidbody>().position = position; Physics.SyncTransforms();
    }
    private static void Wait(int next, float seconds) { phase = next; due = Time.time + seconds; }
    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup - started > 75) { Finish(new Exception("Timeout, phase " + phase)); return; }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.time < due) return;
        try
        {
            if (phase == 0)
            {
                player = Object.FindFirstObjectByType<PlayerController>();
                rooms = Object.FindFirstObjectByType<RoomLifecycleManager>();
                Check(player != null && Camera.main != null && Camera.main.orthographic, "one standard player and 2D camera");
                Check(rooms.CurrentRegionId == "mroom2_play_area", "spawn inside authored region");
                Check(Object.FindObjectsByType<RespawnAnchorFeature>(FindObjectsSortMode.None).Count(a => a.IsCurrent) == 1, "one current checkpoint");
                foreach (string color in new[] { "red", "green", "blue" })
                    Check(!ColorRuntimeService.Instance.IsUnlocked(color) && !ColorRuntimeService.Instance.IsAbilityUnlocked(color), "default locked: " + color);
                player.GetComponent<PlayerInputDriver>().enabled = false; player.Motor.constraints = RigidbodyConstraints.FreezeAll;
                platforms = Object.FindObjectsByType<MovingPlatformFeature>(FindObjectsSortMode.None);
                bases = Object.FindObjectsByType<MechanismBaseFeature>(FindObjectsSortMode.None).OrderBy(b => b.transform.position.x).ToArray();
                Check(platforms.Length == 6 && bases.Length == 3, "six platforms and three bases");
                origins.Clear(); foreach (var p in platforms) { origins[p] = p.transform.position; Check(!p.IsDriveAvailable, "locked platform inactive"); }
                foreach (string color in new[] { "red", "green", "blue" }) ColorRuntimeService.Instance.Unlock(color);
                var cubes = Object.FindObjectsByType<ContactCube>(FindObjectsSortMode.None);
                Check(cubes.Length == 8, "eight named contact cubes");
                foreach (var cube in cubes)
                {
                    cube.GetComponent<ContactCubeMotion>().enabled = false;
                    var body = cube.GetComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false;
                }
                // Move the authored red above the support, not a second red into its occupied column.
                red = cubes.Single(c => c.Color == CubeColor.Red && Vector3.Distance(c.transform.position, new Vector3(-107.5f, 20.5f)) < .1f);
                MoveRed(new Vector3(-121.5f, 33.5f));
                foreach (var b in bases) Check(!b.UsesGravity, "bases remain fixed without gravity");
                foreach (var p in platforms.Where(p => !p.IsCrankControlled))
                    Check(p.SignalMotion == MovingPlatformSignalMotion.PoweredEndpoint && p.LinkedControls.Count == 1, "persisted return mode and base binding");
                MoveRed(bases[0].transform.position + Vector3.right); Wait(1, .5f); return;
            }
            if (phase == 1)
            {
                Check(bases[basisIndex].IsSignalActive, "real red cube powers base " + basisIndex);
                poweredDistances.Clear();
                foreach (var p in platforms.Where(p => p.LinkedControls.Contains(bases[basisIndex])))
                {
                    Vector3 delta = p.transform.position - origins[p];
                    Check(Vector3.Dot(delta, (Vector3)p.Path.Axis * p.Path.FirstEnd) > .001f, "bound platform moves toward configured endpoint");
                    poweredDistances[p] = delta.magnitude;
                }
                Check(poweredDistances.Count == (basisIndex == 0 ? 2 : 1), "shared base drives exactly authored platform set");
                MoveRed(new Vector3(-121.5f, 33.5f)); Wait(2, .8f); return;
            }
            if (phase == 2)
            {
                Check(!bases[basisIndex].IsSignalActive, "removing red cuts power");
                foreach (var pair in poweredDistances)
                    Check(Vector3.Distance(pair.Key.transform.position, origins[pair.Key]) < .015f, "unpowered platform returns to original pose");
                if (++basisIndex < bases.Length)
                { MoveRed(bases[basisIndex].transform.position + Vector3.right); Wait(1, .5f); return; }
                var cranks = Object.FindObjectsByType<CrankPlatformFeature>(FindObjectsSortMode.None);
                foreach (var crank in cranks)
                {
                    Check(crank.Platforms.Count == 1, "independent crank owns one authored platform");
                    var p = crank.Platforms.Single(); crank.SetSignal(true, null);
                    Check(Vector3.Distance(p.transform.position, p.PreviewFirstTarget) < .015f, "crank still uses full progress");
                    crank.SetSignal(false, null);
                    Check(Vector3.Distance(p.transform.position, origins[p]) < .015f, "crank returns to author position");
                }
                doors = Object.FindObjectsByType<DoorController>(FindObjectsSortMode.None);
                Check(doors.Length == 3, "three named doors");
                foreach (var door in doors)
                {
                    using var s = new SerializedObject(door);
                    s.FindProperty("cameraMoveDuration").floatValue = .01f;
                    s.FindProperty("cameraHoldDuration").floatValue = 0;
                    s.FindProperty("cameraReturnDuration").floatValue = .01f;
                    s.FindProperty("unlockDelay").floatValue = .02f; s.ApplyModifiedPropertiesWithoutUndo();
                    foreach (var button in door.Buttons) Check(button.TryPress(player.gameObject), "named button accepts existing pressure API");
                }
                Wait(3, 3); return;
            }
            if (phase == 3)
            {
                foreach (var door in doors) Check(door.State == DoorState.Unlocked, "all named buttons unlock their door");
                rooms.ResetRegion("mroom2_play_area");
                foreach (var door in doors) Check(door.State == DoorState.Unlocked && door.Buttons.All(b => b.IsPressed), "latched door progress survives room reset");
                foreach (var p in platforms) Check(Vector3.Distance(p.transform.position, origins[p]) < .015f, "room reset restores platform placement");
                foreach (string color in new[] { "red", "green", "blue" })
                    Check(ColorRuntimeService.Instance.IsUnlocked(color) && !ColorRuntimeService.Instance.IsAbilityUnlocked(color), "reset retains scene unlock without ability");
                var vines = Object.FindObjectsByType<ClimbableVineFeature>(FindObjectsSortMode.None);
                Check(vines.Count(v => new SerializedObject(v).FindProperty("vineRoot").objectReferenceValue != null) == 1, "reset retains prebuilt vine ownership");
                support = platforms.Single(p => !p.IsCrankControlled && Mathf.Approximately(p.Path.FirstEnd, 1));
                var supportBase = (MechanismBaseFeature)support.LinkedControls.Single();
                red.GetComponent<ContactCubeMotion>().enabled = false;
                red.GetComponent<Rigidbody>().isKinematic = true;
                MoveRed(supportBase.transform.position + Vector3.down);
                Wait(4, .8f); return;
            }
            if (phase == 4)
            {
                Check(Vector3.Distance(support.transform.position, origins[support] + Vector3.up) < .015f,
                    "support rises one cell while red is held against base: platform=" + support.transform.position + " red=" + red.transform.position +
                    " signal=" + ((MechanismBaseFeature)support.LinkedControls.Single()).IsSignalActive);
                red.GetComponent<ContactCubeMotion>().enabled = true;
                red.GetComponent<ContactCubeMotion>().SuspendMotion(false);
                Wait(5, 1); return;
            }
            if (phase == 5)
            {
                var supportBase = (MechanismBaseFeature)support.LinkedControls.Single();
                Check(supportBase.IsSignalActive, "released red remains on support and keeps base powered");
                Check(Mathf.Abs(red.transform.position.y - (supportBase.transform.position.y - 1)) < .08f, "support keeps red touching base without artificial latching");
                red.GetComponent<ContactCubeMotion>().enabled = false; red.GetComponent<Rigidbody>().isKinematic = true;
                MoveRed(new Vector3(-121.5f, 33.5f));
                ridden = platforms.Single(p => !p.IsCrankControlled && p.Path.FirstEnd < -30);
                var top = ridden.GetComponent<Collider>().bounds;
                // The left half runs beneath an authored wall; use the unobstructed rightmost cell.
                var start = new Vector3(top.max.x - .55f, top.max.y + .5f, top.center.z);
                player.transform.position = start; player.Motor.position = start;
                riderOffset = start.x - ridden.transform.position.x;
                player.Motor.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionZ;
                player.Motor.linearVelocity = Vector3.zero; Physics.SyncTransforms();
                Wait(6, .3f); return;
            }
            if (phase == 6)
            {
                Check(player.IsGrounded, "player stands on real imported platform");
                MoveRed(ridden.LinkedControls.Single().transform.position + Vector3.right);
                Wait(7, .8f); return;
            }
            if (phase == 7 || phase == 8)
            {
                Check(Mathf.Abs(player.transform.position.x - ridden.transform.position.x - riderOffset) < .18f,
                    "player follows platform outbound and returning, phase=" + phase + " player=" + player.transform.position + " platform=" + ridden.transform.position);
                Check(Mathf.Abs(player.GetComponent<Collider>().bounds.min.y - ridden.GetComponent<Collider>().bounds.max.y) < .18f,
                    "rider feet remain on platform support");
                if (phase == 7) { MoveRed(new Vector3(-121.5f, 33.5f)); Wait(8, 1); return; }
                Check(Vector3.Distance(ridden.transform.position, origins[ridden]) < .015f, "ridden platform returns home");
                Finish(null);
            }
        }
        catch (Exception error) { Finish(error); }
    }
    private static void Finish(Exception error)
    {
        SessionState.SetBool(Flag, false); EditorApplication.update -= Tick;
        string message = error == null ? "PASS " + checks + " MRoom2 second import runtime assertions" : error.ToString();
        File.WriteAllText("Library/MRoom2SecondRuntimeResult.txt", message); Debug.Log(message); EditorApplication.Exit(error == null ? 0 : 1);
    }
}
