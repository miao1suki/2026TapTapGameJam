using System;
using System.Linq;
using Project.BlockFeatures;
using Project.ColorBlocks;
using Project.ContactCubes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Project.Player.Editor
{
    /// <summary>Verify the real Gaming scene without saving or modifying authored scene data.</summary>
    public static class GamingCarryPlacementVerification
    {
        private const string Key = "GamingCarryPlacementVerification";
        private static int frames, phase, checks;
        private static float started;
        private static PlayerController player;
        private static CarryableFeature item;
        private static Vector3 authoredPosition;

        public static void Run()
        {
            if (!Application.dataPath.Contains("/Library/FlowRetirementProbe/Assets"))
                throw new Exception("Isolated verification only");
            EditorSceneManager.OpenScene(MobileBuildSetup.GameScene);
            SessionState.SetBool(Key, true);
            Resume(); EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod] private static void Resume()
        {
            if (!SessionState.GetBool(Key, false)) return;
            frames = phase = checks = 0;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
        }

        private static void Check(bool ok, string message)
        {
            if (!ok) throw new Exception(message);
            checks++; Debug.Log("GAMING CARRY PASS " + message);
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || ++frames < 20) return;
            try
            {
                if (phase == 0)
                {
                    player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
                    Check(player != null, "Gaming uses its authored standard player");
                    player.GetComponent<PlayerInputDriver>().enabled = false;
                    ColorRuntimeService.Instance.Unlock("red");
                    item = UnityEngine.Object.FindObjectsByType<ContactCube>(FindObjectsSortMode.None)
                        .Where(c => c.Color == CubeColor.Red)
                        .OrderBy(c => c.transform.position.x).First().GetComponent<CarryableFeature>();
                    authoredPosition = item.transform.position;
                    item.GetComponent<Rigidbody>().position = new Vector3(16.5f, -28.5f, 0);
                    item.transform.position = item.GetComponent<Rigidbody>().position;
                    player.Motor.position = new Vector3(17.48f, -28.5f, 0);
                    player.transform.position = player.Motor.position;
                    player.Motor.linearVelocity = Vector3.zero;
                    Physics.SyncTransforms();
                    player.SetMoveInput(Vector2.left); player.SetMoveInput(Vector2.zero);
                    player.UpdateCarryInput(true, false, false, false);
                    started = Time.time; phase = 1;
                }
                else if (phase == 1 && Time.time - started > .65f)
                {
                    Check(item.IsCarryActive && player.IsCarrying, "real red contact cube picked up through player command");
                    player.SetMoveInput(Vector2.left); player.SetMoveInput(Vector2.zero);
                    Check(player.TryGetCarryPlacementPreview(out Bounds preview, out bool valid) && valid &&
                        Mathf.Abs(preview.center.x - 16.5f) < .01f,
                        "Gaming left preview is valid beside the authored white wall at 18,28");
                    Vector3 before = player.Motor.position;
                    player.UpdateCarryInput(false, true, false, false);
                    Check(item.IsCarryDropAnimating && player.Motor.position == before,
                        "real command starts drop without teleporting player");
                    started = Time.time; phase = 2;
                }
                else if (phase == 2 && Time.time - started > .65f)
                {
                    Check(!item.IsCarryActive && !item.IsCarryDropAnimating &&
                        item.GetComponentsInChildren<Collider>().Any(c => c.enabled && !c.isTrigger),
                        "Gaming drop restores contact cube physics after separation");
                    Check(Mathf.Abs(item.transform.position.x - 16.5f) < .02f &&
                        Mathf.Abs(player.Motor.position.x - 17.5f) < .02f,
                        "real item and interpolated player fit the two grid cells beside the wall");
                    player.RequestRespawn();
                    Check(Vector3.Distance(item.transform.position, authoredPosition) < .02f,
                        "R respawn restores the moved cube to its authored position");
                    Check(ColorRuntimeService.Instance.IsUnlocked("red") &&
                        !ColorRuntimeService.Instance.IsAbilityUnlocked("red"),
                        "R retains scene unlock without granting player ability");
                    Finish(true);
                }
                if (frames > 20000) throw new Exception("Gaming verification timeout");
            }
            catch (Exception e) { Debug.LogException(e); Finish(false); }
        }

        private static void Finish(bool ok)
        {
            SessionState.SetBool(Key, false); EditorApplication.update -= Tick;
            if (ok) Debug.Log("GAMING CARRY VERIFICATION PASSED " + checks);
            EditorApplication.Exit(ok ? 0 : 1);
        }
    }
}
