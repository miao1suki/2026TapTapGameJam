using System;
using System.IO;
using System.Linq;
using Project.ColorBlocks;
using Project.ContactCubes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Project.Player.Editor
{
    /// <summary>Exercises actual feature Update, fixed physics and expiring surface leases.</summary>
    public static class ClimbCornerLiveVerification
    {
        private const string Key = "ClimbCornerLiveVerification";
        private static GameObject source, floor;
        private static PlayerController player;
        private static Bounds bounds;
        private static int variant, stage, checks;
        private static bool sawProtection;
        private static float next;
        private static double deadline;
        public static void Run()
        {
            if (!Application.dataPath.Contains("FlowRetirementProbe") || !Environment.GetCommandLineArgs().Contains("-noaudio"))
                throw new Exception("Isolated -noaudio verification only");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            SessionState.SetBool(Key, true); Resume(); EditorApplication.EnterPlaymode();
        }
        [InitializeOnLoadMethod] private static void Resume()
        {
            if (!SessionState.GetBool(Key, false)) return;
            deadline = EditorApplication.timeSinceStartup + 100;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
        }
        private static void Check(bool pass, string label)
        { if (!pass) throw new Exception(label); checks++; Debug.Log("CORNER LIVE PASS " + label); }
        private static GameObject Spawn(string path, Vector3 position) =>
            Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Content/" + path), position, Quaternion.identity);
        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup > deadline) { Finish(false, "Timeout"); return; }
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            try
            {
                if (stage == 0)
                {
                    ColorRuntimeService.Instance.SetAllUnlockedForCurrentSession(true);
                    ColorRuntimeService.Instance.UnlockAllAbilitiesForCurrentSession();
                    Setup(); return;
                }
                sawProtection |= player.IsClimbCornerProtected;
                if (stage == 1)
                {
                    if (sawProtection && !player.IsClimbCornerProtected && player.Motor.position.y > bounds.max.y + .35f)
                    {
                        Check(true, "actual loop wraps upward " + variant);
                        player.SetMoveInput(Vector2.zero); stage = 2; next = Time.time + .5f;
                    }
                    else if (Time.time > next) throw new Exception("Actual upward corner timed out " + variant + " at " + player.Motor.position);
                }
                else if (stage == 2 && Time.time >= next)
                {
                    Check(player.Motor.position.y > bounds.max.y + .35f && player.Motor.position.y < bounds.max.y + .7f,
                        "settles on real top after surface lease expiry " + variant + " at " + player.Motor.position);
                    Check(!player.IsDead, "climb handoff does not fall or kill player " + variant);
                    Cleanup(); variant++;
                    if (variant == 8) Finish(true, "PASS " + checks); else Setup();
                }
            }
            catch (Exception error) { Debug.LogException(error); Finish(false, error.ToString()); }
        }
        private static void Setup()
        {
            int kind = variant / 2, side = variant % 2 == 0 ? -1 : 1;
            Vector3 origin = new Vector3(100, 20, 0);
            source = Spawn(kind == 3 ? "ColorBlocks/Prefabs/Green_Ladder.prefab" : "ContactCubes/Green_ContactCube.prefab", origin);
            if (kind != 3)
            {
                var cube = source.GetComponent<ContactCube>();
                var serialized = new SerializedObject(cube);
                serialized.FindProperty("initialLength").intValue = kind == 0 ? 1 : 4;
                serialized.ApplyModifiedPropertiesWithoutUndo(); cube.ResetForRoom();
                if (kind == 2) source.transform.rotation = Quaternion.Euler(0, 0, 90);
            }
            Physics.SyncTransforms(); bounds = source.GetComponentInChildren<BoxCollider>().bounds;
            floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Authored support";
            floor.transform.position = new Vector3(bounds.center.x, bounds.min.y - .5f, 0);
            floor.transform.localScale = new Vector3(bounds.size.x, 1, 1);
            float edge = side < 0 ? bounds.min.x : bounds.max.x;
            player = Spawn("Player/Player.prefab", new Vector3(edge + side * .52f, bounds.max.y - .2f, 0)).GetComponent<PlayerController>();
            player.GetComponent<PlayerInputDriver>().enabled = false;
            player.SetMoveInput(Vector2.up); Physics.SyncTransforms();
            sawProtection = false; stage = 1; next = Time.time + 4f;
        }
        private static void Cleanup()
        {
            if (player != null) Object.DestroyImmediate(player.gameObject);
            if (source != null) Object.DestroyImmediate(source);
            if (floor != null) Object.DestroyImmediate(floor);
        }
        private static void Finish(bool pass, string result)
        {
            Cleanup(); File.WriteAllText(Path.Combine(Application.dataPath, "../Library/ClimbCornerLiveVerification.txt"), result);
            SessionState.SetBool(Key, false); EditorApplication.update -= Tick; EditorApplication.Exit(pass ? 0 : 1);
        }
    }
}
