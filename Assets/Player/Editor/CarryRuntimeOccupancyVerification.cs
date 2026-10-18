using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Project.BlockFeatures;
using Project.ColorBlocks;
using Project.LevelEditor;
using Project.Rooms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Project.Player.Editor
{
    /// <summary>Real MRoom2 reproduction plus live-occupancy fixtures; never saves author data.</summary>
    public static class CarryRuntimeOccupancyVerification
    {
        private const string Flag = "CarryRuntimeOccupancyVerification";
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly List<GameObject> Fixtures = new List<GameObject>();
        private static PlayerController player;
        private static CarryableFeature carried;
        private static PlantObstacleFeature plant;
        private static Func<Vector3Int, bool> cellFree, gridBlocked, rayBlocked;
        private static SimulationMode savedSimulation;
        private static int phase, checks;
        private static double started;
        private static float phaseTime;

        public static void Run()
        {
            if (!Application.dataPath.Replace('\\', '/').Contains("/Library/FlowRetirementProbe/Assets"))
                throw new InvalidOperationException("Use the isolated verification project only.");
            EditorSceneManager.OpenScene("Assets/Scenes/MRoom2.unity");
            // A designer may add a key at the spawn. Its unrelated startup cutscene
            // must not own the player during this isolated placement verification.
            foreach (var key in Object.FindObjectsByType<ColorKeyPickup>(FindObjectsSortMode.None))
                key.gameObject.SetActive(false);
            SessionState.SetBool(Flag, true);
            Resume();
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod] private static void Resume()
        {
            if (!SessionState.GetBool(Flag, false)) return;
            phase = checks = 0;
            started = EditorApplication.timeSinceStartup;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private static void Check(bool value, string message)
        {
            if (!value) throw new Exception("LIVE OCCUPANCY FAIL: " + message);
            checks++;
            Debug.Log("LIVE OCCUPANCY PASS: " + message);
        }

        private static Func<Vector3Int, bool> Query(string name) =>
            (Func<Vector3Int, bool>)Delegate.CreateDelegate(typeof(Func<Vector3Int, bool>), player,
                typeof(PlayerController).GetMethod(name, Private));

        private static void MovePlayer(Vector3 position)
        {
            player.Motor.position = position;
            player.transform.position = position;
            player.Motor.linearVelocity = Vector3.zero;
            Physics.SyncTransforms();
        }

        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup - started > 90) { Finish(false, "Timed out"); return; }
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.time < .5f) return;
            try
            {
                if (phase == 0)
                {
                    savedSimulation = Physics.simulationMode;
                    Physics.simulationMode = SimulationMode.Script;
                    ColorRuntimeService.Instance.SetAllUnlockedForCurrentSession(true);
                    player = Object.FindFirstObjectByType<PlayerController>();
                    player.GetComponent<PlayerInputDriver>().enabled = false;
                    player.Motor.interpolation = RigidbodyInterpolation.None;
                    cellFree = Query("IsCarryDropCellFree");
                    gridBlocked = Query("IsCarryCellOccupiedByPlacedBlock");
                    rayBlocked = Query("HasCarryBlockerInDirection");
                    var blocks = Object.FindObjectsByType<LevelEditorPlacedBlock>(FindObjectsSortMode.None);
                    carried = blocks.Single(b => b.Cell == new Vector2Int(-95, 18) && b.GetComponent<CarryableFeature>() != null)
                        .GetComponent<CarryableFeature>();
                    plant = blocks.Single(b => b.ContainsCell(new Vector2Int(-94, 12)) && b.GetComponent<PlantObstacleFeature>() != null)
                        .GetComponent<PlantObstacleFeature>();
                    Check(!cellFree(new Vector3Int(-94, 12, 0)), "real intact plant blocks the base's right neighbor");
                    Check(plant.BreakObstacle(player), "burn real authored six-cell plant");
                    Physics.SyncTransforms();
                    Check(!plant.IsIntact && plant.GetComponentsInChildren<Collider>().All(c => !c.enabled), "burn clears physical collision immediately");
                    Check(!gridBlocked(new Vector3Int(-94, 12, 0)) && cellFree(new Vector3Int(-94, 12, 0)), "burned plant no longer reserves an invisible cell");
                    Check(plant.GetComponent<LevelEditorPlacedBlock>().ContainsCell(new Vector2Int(-94, 12)), "authoring footprint is preserved for reset/editor");
                    carried.transform.position = new Vector3(-93.5f, 12.5f, 0);
                    carried.GetComponent<Rigidbody>().position = carried.transform.position;
                    MovePlayer(new Vector3(-92.5f, 12.5f, 0));
                    player.SetMoveInput(Vector2.left);
                    player.SetMoveInput(Vector2.zero);
                    player.UpdateCarryInput(true, false, false, false);
                    phaseTime = Time.time;
                    phase = 1;
                }
                else if (phase == 1 && Time.time - phaseTime > .75f)
                {
                    Check(carried.IsCarryActive && player.IsCarrying, "real F command picks up MRoom2 red cube");
                    Check(player.TryGetCarryPlacementPreview(out Bounds preview, out bool valid) && valid &&
                        Vector3.Distance(preview.center, new Vector3(-93.5f, 12.5f, 0)) < .02f,
                        "real preview accepts burned cell immediately beside base");
                    player.UpdateCarryInput(false, true, false, false);
                    Check(carried.IsCarryDropAnimating, "real Q command starts placement without manual state bypass");
                    phaseTime = Time.time;
                    phase = 2;
                }
                else if (phase == 2 && Time.time - phaseTime > .8f)
                {
                    player.UpdateCarryInput(false, false, false, false);
                    Check(!carried.IsCarryActive && !player.IsCarrying &&
                        Vector3.Distance(carried.transform.position, new Vector3(-93.5f, 12.5f, 0)) < .03f,
                        "drop completes in the previously blocked plant cell");
                    Check(cellFree(new Vector3Int(-95, 18, 0)), "moved red cube releases its original author cell");
                    Check(!cellFree(new Vector3Int(-94, 12, 0)), "moved cube blocks its actual new cell");
                    player.RequestRespawn();
                    Check(plant.IsIntact && !cellFree(new Vector3Int(-94, 12, 0)), "R restores plant and its live occupancy");
                    Check(Vector3.Distance(carried.transform.position, new Vector3(-94.5f, 18.5f, 0)) < .03f,
                        "R restores red cube's original pose without editing metadata");
                    for (int i = 0; i < 8; i++)
                    {
                        Check(plant.BreakObstacle(player) && cellFree(new Vector3Int(-94, 12, 0)), "repeat burn frees cell " + i);
                        player.RequestRespawn();
                        Check(plant.IsIntact && !cellFree(new Vector3Int(-94, 12, 0)), "repeat R blocks restored cell " + i);
                    }
                    CheckFixtures();
                    Finish(true, "LIVE OCCUPANCY VERIFICATION PASSED " + checks);
                }
            }
            catch (Exception error) { Finish(false, error.ToString()); }
        }

        private static LevelEditorPlacedBlock Box(Vector2Int cell, string name, bool trigger = false)
        {
            var item = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Fixtures.Add(item);
            item.name = name;
            item.transform.position = new Vector3(cell.x + .5f, cell.y + .5f, 0);
            item.GetComponent<Collider>().isTrigger = trigger;
            var block = item.AddComponent<LevelEditorPlacedBlock>();
            block.Configure(cell, name, Color.white, false, true);
            Physics.SyncTransforms();
            return block;
        }

        private static void CheckFixtures()
        {
            MovePlayer(new Vector3(201.5f, 100.5f, 0));
            var cell = new Vector3Int(200, 100, 0);
            var solid = Box(new Vector2Int(200, 100), "solid");
            Check(gridBlocked(cell) && !cellFree(cell), "solid author cell stays blocked");
            solid.GetComponent<Collider>().enabled = false;
            Check(!gridBlocked(cell) && cellFree(cell), "opened or hidden solid no longer reserves its footprint");
            solid.GetComponent<Collider>().enabled = true;
            Check(gridBlocked(cell), "re-enabled solid blocks without rebuilding registry");
            solid.transform.position += Vector3.up * 3;
            Physics.SyncTransforms();
            Check(cellFree(cell) && !cellFree(cell + Vector3Int.up * 3), "moving solid releases old cell and blocks new cell");
            Check(solid.Cell == new Vector2Int(200, 100), "movement does not rewrite author cell");
            var trigger = Box(new Vector2Int(200, 100), "water or interaction trigger", true);
            Check(cellFree(cell) && !rayBlocked(Vector3Int.left), "water/Trigger does not block cell or directional ray");
            var thinWall = Box(new Vector2Int(200, 100), "thin solid wall");
            thinWall.transform.position = new Vector3(200.95f, 100.5f, 0);
            thinWall.transform.localScale = new Vector3(.02f, 1, 1);
            Physics.SyncTransforms();
            Check(!cellFree(cell) && rayBlocked(Vector3Int.left), "thin physical wall still blocks alongside water");
            Object.DestroyImmediate(thinWall.gameObject);
            Object.DestroyImmediate(trigger.gameObject);
            Object.DestroyImmediate(solid.gameObject);

            var ring = Box(new Vector2Int(210, 100), "merged ring");
            ring.transform.position = new Vector3(211.5f, 101.5f, 0);
            ring.transform.localScale = new Vector3(3, 3, 1);
            ring.SetSizeCells(new Vector2Int(3, 3));
            ring.gameObject.AddComponent<LevelEditorMergedShape>().Configure(new Vector2Int(3, 3), new[]
            {
                new RectInt(0, 0, 3, 1), new RectInt(0, 2, 3, 1),
                new RectInt(0, 1, 1, 1), new RectInt(2, 1, 1, 1)
            });
            Physics.SyncTransforms();
            Check(cellFree(new Vector3Int(211, 101, 0)), "merged hole stays free");
            Check(!cellFree(new Vector3Int(210, 101, 0)), "merged real edge stays blocked");
            foreach (var shape in ring.GetComponents<Collider>()) shape.enabled = false;
            Check(cellFree(new Vector3Int(210, 101, 0)), "all merged collision volumes can release occupancy");
            var lateChild = new GameObject("late solid child", typeof(BoxCollider));
            lateChild.transform.SetParent(ring.transform, false);
            lateChild.transform.localPosition = new Vector3(-1f / 3f, 0, 0);
            lateChild.transform.localScale = new Vector3(1f / 3f, 1f / 3f, 1);
            Physics.SyncTransforms();
            Check(!cellFree(new Vector3Int(210, 101, 0)), "late-added child collider is not missed by a stale cache");
            lateChild.SetActive(false);
            Check(cellFree(new Vector3Int(210, 101, 0)), "inactive child does not keep a ghost cell");
            var depth = Box(new Vector2Int(220, 100), "different depth");
            depth.transform.position += Vector3.forward * 3;
            Physics.SyncTransforms();
            Check(cellFree(new Vector3Int(220, 100, 0)), "other depth does not reserve the player plane");

            // Measure only the hot query, excluding reflection, logging and setup.
            for (int i = 0; i < 16; i++) gridBlocked(new Vector3Int(210, 101, 0));
            var watch = System.Diagnostics.Stopwatch.StartNew();
            long startAllocation = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++) gridBlocked(new Vector3Int(210, 101, 0));
            long allocation = GC.GetAllocatedBytesForCurrentThread() - startAllocation;
            watch.Stop();
            Check(allocation == 0, "1000 warmed live grid queries allocate zero managed bytes");
            Debug.Log($"LIVE OCCUPANCY CPU: {watch.Elapsed.TotalMilliseconds:F3} ms / 1000 queries; {allocation} managed bytes");
        }

        private static void Finish(bool ok, string message)
        {
            SessionState.SetBool(Flag, false);
            EditorApplication.update -= Tick;
            Physics.simulationMode = savedSimulation;
            foreach (var item in Fixtures) if (item != null) Object.DestroyImmediate(item);
            Fixtures.Clear();
            Debug.Log(message);
            EditorApplication.Exit(ok ? 0 : 1);
        }
    }
}
