using Project.Planar;
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
        private static Vector3 placementPosition;
        private static int playerCell;

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
                    var flow = Project.GameFlow.GameFlowController.Instance;
                    if (flow == null || !flow.IsInitialized || flow.IsTransitioning) return;
                    // Isolate placement from automatic save restore / the new-run story.
                    var session = Project.Saving.RunSessionCoordinator.Instance;
                    if (session != null) { session.StopAllCoroutines(); session.enabled = false; }
                    Check(flow.EnterGameplayImmediatelyForDevelopment() && !Project.GameplayPresentationGate.IsSuppressed,
                        "formal menu releases the real gameplay gate before carry input");
                    player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
                    Check(player != null, "Gaming uses its authored standard player");
                    player.GetComponent<PlayerInputDriver>().enabled = false;
                    ColorRuntimeService.Instance.Unlock("red");
                    var rooms = UnityEngine.Object.FindFirstObjectByType<Project.Rooms.RoomLifecycleManager>();
                    string targetRegion = rooms.FindRegion(new Vector3(16.5f, -28.5f, 0));
                    item = UnityEngine.Object.FindObjectsByType<ContactCube>(FindObjectsSortMode.None)
                        .Where(c => c.Color == CubeColor.Red &&
                            c.GetComponent<Project.Rooms.RoomResetObject>()?.RegionId == targetRegion)
                        .OrderBy(c => c.transform.position.x).First().GetComponent<CarryableFeature>();
                    authoredPosition = item.transform.position;
                    item.GetComponent<Rigidbody2D>().position = new Vector3(16.5f, -28.5f, 0);
                    item.transform.position = item.GetComponent<Rigidbody2D>().position;
                    player.Motor.position = new Vector3(17.48f, -28.5f, 0);
                    player.transform.position = player.Motor.WorldPosition();
                    player.Motor.linearVelocity = Vector3.zero;
                    // The fixture relocates across the map; that is not a hundred-cell fall.
                    var hidden = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                    typeof(PlayerController).GetField("highestAirPosition", hidden).SetValue(player, player.transform.position.y);
                    typeof(PlayerController).GetField("currentFallDistance", hidden).SetValue(player, 0f);
                    rooms.UpdatePlayerRegion(player);
                    PlanarPhysics.SyncTransforms();
                    player.SetMoveInput(Vector2.left); player.SetMoveInput(Vector2.zero);
                    Check(player.UpdateCarryInput(true, false, false, false),
                        $"real pickup command accepted (locked={player.IsControlLocked}, dead={player.IsDead}, " +
                        $"carryable={item.CanCarryNow}, player={player.CarryCenterWorldPosition}, item={item.transform.position})");
                    started = Time.time; phase = 1;
                }
                else if (phase == 1 && Time.time - started > .65f)
                {
                    Check(item.IsCarryActive && player.IsCarrying,
                        $"real red contact cube picked up through player command (locked={player.IsControlLocked}, " +
                        $"dead={player.IsDead}, carryable={item.CanCarryNow}, player={player.transform.position}, item={item.transform.position})");
                    player.SetMoveInput(Vector2.left); player.SetMoveInput(Vector2.zero);
                    playerCell = Mathf.FloorToInt(player.CarryCenterWorldPosition.x / player.GridCellWorldSize);
                    Check(player.TryGetCarryPlacementPreview(out Bounds preview, out bool valid) && valid &&
                        Mathf.Abs(preview.center.x - (playerCell - .5f) * player.GridCellWorldSize) < .01f,
                        $"Gaming left preview uses the actual settled body cell (valid={valid}, preview={preview.center})");
                    placementPosition = preview.center;
                    Vector3 before = player.Motor.WorldPosition();
                    player.UpdateCarryInput(false, true, false, false);
                    Check(item.IsCarryDropAnimating && player.Motor.WorldPosition() == before,
                        "real command starts drop without teleporting player");
                    started = Time.time; phase = 2;
                }
                else if (phase == 2 && Time.time - started > .65f)
                {
                    Check(!item.IsCarryActive && !item.IsCarryDropAnimating &&
                        item.GetComponentsInChildren<Collider2D>().Any(c => c.enabled && !c.isTrigger),
                        "Gaming drop restores contact cube physics after separation");
                    Bounds body = player.GetComponent<CapsuleCollider2D>().bounds;
                    Check(Mathf.Abs(item.transform.position.x - placementPosition.x) < .02f &&
                        body.min.x >= playerCell * player.GridCellWorldSize - .004f &&
                        body.max.x <= (playerCell + 1) * player.GridCellWorldSize + .004f,
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
