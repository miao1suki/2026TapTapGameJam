using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Project.BlockFeatures;
using Project.ColorBlocks;
using Project.GameFlow;
using Project.LevelEditor;
using Project.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Project.Rooms.Editor
{
    /// <summary>Real formal-scene reset timings. Run only in the disposable verification project.</summary>
    public static class RoomTransitionPerformanceAudit
    {
        private const string Key = nameof(RoomTransitionPerformanceAudit);
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly StringBuilder report = new StringBuilder();
        private static readonly MethodInfo WaterTick = typeof(GameplayWaterPresentation).GetMethod("LateUpdate", Hidden);
        private static int frames, regionIndex;
        private static RoomLifecycleManager manager;
        private static PlayerController player;
        private static double started;
        private static int checks;

        public static void RunVerified()
        {
            SessionState.SetBool(Key + ".Verify", true);
            Run();
        }

        public static void Run()
        {
            if (!Application.dataPath.Contains("FlowRetirementProbe"))
                throw new InvalidOperationException("Use the isolated FlowRetirementProbe project only.");
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Playing/Gaming.unity");
            SessionState.SetBool(Key, true);
            Resume();
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!SessionState.GetBool(Key, false)) return;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            started = EditorApplication.timeSinceStartup;
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            try
            {
                if (manager == null)
                {
                    var flow = GameFlowController.Instance;
                    if (flow == null || !flow.IsInitialized || flow.IsTransitioning)
                    {
                        if (EditorApplication.timeSinceStartup - started > 120d)
                            throw new TimeoutException("Formal Gaming flow did not finish menu initialization.");
                        return;
                    }
                    Check(flow.EnterGameplayImmediatelyForDevelopment() && !Project.GameplayPresentationGate.IsSuppressed,
                        "Formal Gaming did not leave its menu presentation gate");
                    manager = Object.FindFirstObjectByType<RoomLifecycleManager>();
                    player = Object.FindFirstObjectByType<PlayerController>();
                    if (manager == null || player == null) throw new Exception("Formal Gaming lifecycle/player missing.");
                    player.GetComponent<PlayerInputDriver>().enabled = false;
                    player.enabled = false;
                    player.Motor.isKinematic = true;
                    typeof(RoomLifecycleManager).GetField("player", Hidden).SetValue(manager, null);
                    ColorRuntimeService.Instance.SetAllUnlockedForCurrentSession(true);
                    report.AppendLine("Formal Gaming: " + manager.Regions.Count + " regions; " +
                        Object.FindObjectsByType<RoomResetObject>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length + " reset members.");
                }
                if (++frames < 100) return; // Prewarm only after the real game/menu flow releases gameplay.
                if (regionIndex >= manager.Regions.Count)
                {
                    VerifyWalkTransitions();
                    VerifyTransientResetAndWallRevision();
                    VerifyRuntimeComponentChanges();
                    VerifyBuoyancyResetDoesNotReenter();
                    report.AppendLine($"PASS {checks} reset and crossing checks.");
                    Finish(0);
                    return;
                }
                RoomRegion region = manager.Regions[regionIndex++];
                var members = Object.FindObjectsByType<RoomResetObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .Where(m => m.Manager == manager && m.RegionId == region.id).ToArray();
                var before = Object.FindObjectsByType<GameplayWaterPresentation>(FindObjectsSortMode.None);
                var ids = new HashSet<int>(before.Select(w => w.GetInstanceID()));
                var watch = Stopwatch.StartNew();
                manager.ResetRegion(region.id);
                double resetMs = watch.Elapsed.TotalMilliseconds;
                var after = Object.FindObjectsByType<GameplayWaterPresentation>(FindObjectsSortMode.None);
                int replacements = after.Count(w => !ids.Contains(w.GetInstanceID()));
                watch.Restart();
                foreach (var water in after) WaterTick.Invoke(water, null);
                double waterMs = watch.Elapsed.TotalMilliseconds;
                if (SessionState.GetBool(Key + ".Verify", false))
                {
                    Check(replacements == 0, region.label + " recreated unchanged water presentation");
                    Check(ColorRuntimeService.Instance.IsUnlocked("blue"), "Reset cleared color progress");
                    foreach (var water in after)
                    {
                        var source = water.GetComponentInParent<WaterVisualFeature>();
                        Check(source != null && source.IsAttached, "Water feature lost attachment");
                    }
                }
                report.AppendLine($"{region.label} [{region.id}]: members={members.Length}, reset={resetMs:F3}ms, " +
                    $"waterRenderRebuild={waterMs:F3}ms, recreatedWater={replacements}/{before.Length}, " +
                    $"blueUnlocked={ColorRuntimeService.Instance.IsUnlocked("blue")}");
            }
            catch (Exception exception)
            {
                report.AppendLine("FAIL " + exception);
                UnityEngine.Debug.LogException(exception);
                Finish(1);
            }
        }

        private static void VerifyWalkTransitions()
        {
            if (!SessionState.GetBool(Key + ".Verify", false)) return;
            typeof(RoomLifecycleManager).GetField("player", Hidden).SetValue(manager, player);
            var regionField = typeof(RoomLifecycleManager).GetField("currentRegionId", Hidden);
            var lookup = new Dictionary<Vector2Int, RoomRegion>();
            foreach (var region in manager.Regions)
                foreach (var cell in region.cells) lookup[cell] = region;
            var tested = new HashSet<string>();
            foreach (var pair in lookup)
                foreach (var direction in new[] { Vector2Int.right, Vector2Int.up })
                {
                    if (!lookup.TryGetValue(pair.Key + direction, out var next) || next.id == pair.Value.id) continue;
                    string key = string.CompareOrdinal(pair.Value.id, next.id) < 0
                        ? pair.Value.id + ":" + next.id : next.id + ":" + pair.Value.id;
                    if (!tested.Add(key)) continue;
                    foreach (bool reverse in new[] { false, true })
                    {
                        RoomRegion from = reverse ? next : pair.Value;
                        RoomRegion to = reverse ? pair.Value : next;
                        Vector2Int targetCell = reverse ? pair.Key : pair.Key + direction;
                        var target = manager.transform.TransformPoint(new Vector3(
                            (targetCell.x + .5f) * manager.CellSize, -(targetCell.y + .5f) * manager.CellSize, 0f));
                        regionField.SetValue(manager, from.id);
                        player.transform.position = target;
                        player.Motor.position = target;
                        int resetEvents = 0;
                        Action<string> onReset = id => { if (id == from.id) resetEvents++; };
                        manager.RegionReset += onReset;
                        var watch = Stopwatch.StartNew();
                        manager.UpdatePlayerRegion(player);
                        watch.Stop();
                        manager.RegionReset -= onReset;
                        Check(manager.CurrentRegionId == to.id && resetEvents == 1, "Adjacent-room crossing did not reset exactly its source");
                        report.AppendLine($"Walk {from.label} -> {to.label}: {watch.Elapsed.TotalMilliseconds:F3}ms");
                    }
                }
            Check(tested.Count > 0, "Formal Gaming contains no adjacent-region boundary");
            typeof(RoomLifecycleManager).GetField("player", Hidden).SetValue(manager, null);
        }

        private static void Check(bool condition, string message)
        {
            checks++;
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void VerifyTransientResetAndWallRevision()
        {
            if (!SessionState.GetBool(Key + ".Verify", false)) return;
            var waters = Object.FindObjectsByType<GameplayWaterPresentation>(FindObjectsSortMode.None);
            var steamRemaining = typeof(WaterVisualFeature).GetField("steamRemainingSeconds", Hidden);
            var steamVfx = typeof(WaterVisualFeature).GetField("steamVfxInstance", Hidden);
            bool steamTested = false;
            foreach (var water in waters)
            {
                if (water.SurfaceCellCount == 0) continue;
                var feature = water.GetComponentInParent<WaterVisualFeature>();
                typeof(WaterVisualFeature).GetField("steamMode", Hidden).SetValue(feature, SteamReactionMode.Continuous);
                if (!feature.ApplyColorAt("red", player.gameObject, water.Volume.center)) continue;
                Check((float)steamRemaining.GetValue(feature) > 0f, "Continuous steam did not start");
                var member = feature.GetComponent<RoomResetObject>();
                manager.ResetRegion(member.RegionId);
                Check((float)steamRemaining.GetValue(feature) == 0f && steamVfx.GetValue(feature) == null,
                    "Room reset retained transient steam/VFX");
                Check(water != null && water.isActiveAndEnabled && feature.IsAttached,
                    "Transient cleanup destroyed the unchanged water presentation");
                // A real disable/hide still retires presentation and restores it on room reset.
                Check(RoomResetObject.HideForReaction(feature.gameObject), "Water hide fixture lacks ownership");
                Check(!water.gameObject.activeInHierarchy, "Real hide did not retire presentation");
                manager.ResetRegion(member.RegionId);
                Check(feature.gameObject.activeInHierarchy && feature.IsAttached &&
                    feature.GetComponentInChildren<GameplayWaterPresentation>() != null,
                    "Hidden authored water did not restore presentation");
                steamTested = true;
                break;
            }
            Check(steamTested, "No exposed formal Gaming water available for steam reset regression");

            var vine = Object.FindObjectsByType<ClimbableVineFeature>(FindObjectsSortMode.None)
                .First(v => v.IsAttached && !(v is LadderSonFeature) && v.CurrentGrowthBlocks < v.MaxGrowthBlocks);
            int originalCount = vine.CurrentGrowthBlocks;
            typeof(ClimbableVineFeature).GetField("growInstantly", Hidden).SetValue(vine, true);
            vine.transform.position = new Vector3(900f, 900f, 0f);
            vine.GrowOne();
            Check(vine.CurrentGrowthBlocks == originalCount + 1, "Vine transient growth fixture did not create a segment");
            manager.ResetRegion(vine.GetComponent<RoomResetObject>().RegionId);
            Check(vine.CurrentGrowthBlocks == originalCount && vine.IsAttached,
                "Vine reset retained generated growth or lost its attachment");
            Check(Mathf.Approximately((float)typeof(ClimbableVineFeature).GetField("visibleCells", Hidden).GetValue(vine),
                (float)typeof(ClimbableVineFeature).GetField("targetCells", Hidden).GetValue(vine)),
                "Vine reset did not immediately restore authored artwork length");
            int solidRevision = LevelEditorPlacedBlock.SolidRevision;
            vine.gameObject.SetActive(false);
            vine.gameObject.SetActive(true);
            Check(LevelEditorPlacedBlock.SolidRevision == solidRevision,
                "Prop/vine lifetime still invalidates all wall-masked water meshes");

            var wallObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wallObject.name = "Wall revision regression fixture";
            wallObject.transform.position = new Vector3(950.5f, 950.5f, 0f);
            var wall = wallObject.AddComponent<LevelEditorPlacedBlock>();
            wall.Configure(new Vector2Int(950, 950), "wall", Color.white, false);
            Physics.SyncTransforms();
            Check(WaterCellVisibility.Covered(wall, wallObject.transform.position, 1f), "New wall was not cached");
            Vector3 previousPosition = wallObject.transform.position;
            wallObject.transform.position += Vector3.right;
            wall.NotifySolidGeometryChanged();
            Physics.SyncTransforms();
            Check(!WaterCellVisibility.Covered(wall, previousPosition, 1f) &&
                WaterCellVisibility.Covered(wall, wallObject.transform.position, 1f),
                "Notified moving wall left stale author-cell occlusion");
            wallObject.SetActive(false);
            Check(!WaterCellVisibility.Covered(wall, wallObject.transform.position, 1f), "Disabled wall remained in water occlusion");
            Object.Destroy(wallObject);
            report.AppendLine("PASS transient steam/VFX cleanup, real hide/restore, generated vine cleanup, native artwork length, prop-only revision isolation and moved/disabled wall cache.");
        }

        private static void VerifyRuntimeComponentChanges()
        {
            if (!SessionState.GetBool(Key + ".Verify", false)) return;
            var fixture = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fixture.name = "Runtime reset component regression fixture";
            fixture.transform.position = new Vector3(970f, 970f, 0f);
            var member = fixture.AddComponent<RoomResetObject>();
            Vector3 original = fixture.transform.position;
            try
            {
                Check(!member.Persistent, "Plain runtime fixture became persistent");
                // Query once before adding: a permanently cached null would miss this.
                var hint = fixture.AddComponent<Project.Mechanisms.DoorHint>();
                Check(member.Persistent, "Runtime-added persistence component was not discovered");
                fixture.transform.position += Vector3.right;
                member.RestorePose();
                Check(fixture.transform.position != original, "Runtime-added persistent object lost its pose");
                Object.DestroyImmediate(hint);
                Check(!member.Persistent, "Destroyed persistence component remained cached");
                member.RestorePose();
                Check(fixture.transform.position == original, "Object did not resume reset after persistence component removal");

                var hide = fixture.AddComponent<Project.TemporaryObjectHideState>();
                hide.Hide();
                Check(!fixture.GetComponent<Collider>().enabled && fixture.GetComponent<Renderer>().forceRenderingOff,
                    "Runtime-added hide lease fixture did not hide");
                member.RestorePose();
                Check(!hide.IsHidden && fixture.GetComponent<Collider>().enabled &&
                    !fixture.GetComponent<Renderer>().forceRenderingOff,
                    "Runtime-added hide lease was missed by reset component lookup");
            }
            finally
            {
                Object.Destroy(fixture);
            }
            report.AppendLine("PASS runtime-added / destroyed persistence components and runtime-added visibility lease discovery.");
        }

        private static void VerifyBuoyancyResetDoesNotReenter()
        {
            if (!SessionState.GetBool(Key + ".Verify", false)) return;
            var fixture = new GameObject("Buoyancy reset re-entry regression fixture");
            fixture.transform.position = new Vector3(980f, 980f, 0f);
            var volume = fixture.AddComponent<BoxCollider>();
            volume.size = new Vector3(3f, 4f, 3f);
            volume.isTrigger = true;
            var color = fixture.AddComponent<ColorObject>();
            color.enabled = false;
            color.Configure("blue", null);
            color.enabled = true;
            var column = fixture.AddComponent<BuoyancyColumnFeature>();
            column.enabled = true;
            fixture.GetComponent<BlockRuntime>().RefreshFeatureSet(true);
            var member = fixture.AddComponent<RoomResetObject>();
            member.SetRegion(manager.Regions[0].id);
            manager.Register(member);

            var body = player.Motor;
            Vector3 previousPosition = body.position;
            bool previousKinematic = body.isKinematic;
            var playerCollider = player.GetComponent<Collider>();
            var lifted = (Dictionary<int, PlayerController>)typeof(BuoyancyColumnFeature)
                .GetField("liftedPlayers", Hidden).GetValue(column);
            var buoyantIds = (HashSet<int>)typeof(BuoyancyColumnFeature)
                .GetField("buoyantPlayerIds", Hidden).GetValue(column);
            var overshootIds = (HashSet<int>)typeof(BuoyancyColumnFeature)
                .GetField("overshootPlayerIds", Hidden).GetValue(column);
            var waterSources = (HashSet<int>)typeof(PlayerController).GetField("waterSources", Hidden).GetValue(player);
            var waterBuoyancies = (Dictionary<int, float>)typeof(PlayerController)
                .GetField("waterBuoyancies", Hidden).GetValue(player);
            try
            {
                body.isKinematic = false;
                Physics.SyncTransforms();
                Check(column.IsAttached && playerCollider != null && volume.isTrigger,
                    "Buoyancy reset fixture did not activate");
                foreach (bool aboveSurface in new[] { false, true })
                {
                    // Fully submerged catches lift/audio re-registration. The same
                    // horizontal column just above the surface catches exit boosts.
                    Vector3 center = volume.bounds.center;
                    if (aboveSurface) center.y = volume.bounds.max.y + playerCollider.bounds.extents.y + .2f;
                    Vector3 centerOffset = playerCollider.bounds.center - player.transform.position;
                    player.transform.position = center - centerOffset;
                    body.position = player.transform.position;
                    Physics.SyncTransforms();
                    body.linearVelocity = Vector3.up * 1.25f;
                    column.OnPlayerEnter(player.gameObject);
                    Check(lifted.Count == 1, "Buoyancy fixture did not track the contact before reset");
                    if (!aboveSurface)
                        Check(waterBuoyancies.ContainsKey(column.GetInstanceID()) && buoyantIds.Count == 1,
                            "Submerged buoyancy fixture did not start lift/audio ownership");
                    manager.ResetRegion(member.RegionId);
                    int id = column.GetInstanceID();
                    Check(lifted.Count == 0 && buoyantIds.Count == 0 && overshootIds.Count == 0,
                        "Buoyancy reset re-registered lift/audio/exit-boost ownership");
                    Check(!waterSources.Contains(id) && !waterBuoyancies.ContainsKey(id),
                        "Buoyancy reset left a player water/lift source behind");
                    Check(!(bool)typeof(BuoyancyColumnFeature).GetField("bubbleAudioPlaying", Hidden).GetValue(column) &&
                        (Project.Audio.AudioManager.Existing == null || Project.Audio.AudioManager.Existing.GetLoop(column) == null),
                        "Buoyancy reset restarted or retained a bubble audio loop");
                    Check(Mathf.Approximately(body.linearVelocity.y, 1.25f),
                        "Buoyancy reset incorrectly applied a physical top-exit boost");
                    Check(column.IsAttached, "Buoyancy cleanup retired the unchanged feature");
                }
            }
            finally
            {
                column.ResetForRoom();
                fixture.SetActive(false);
                Object.Destroy(fixture);
                body.linearVelocity = Vector3.zero;
                player.transform.position = previousPosition;
                body.position = previousPosition;
                body.isKinematic = previousKinematic;
                Physics.SyncTransforms();
            }
            report.AppendLine("PASS submerged and above-surface buoyancy reset: no stale lift, water, overshoot, audio ownership or exit impulse; attachment retained.");
        }

        private static void Finish(int code)
        {
            report.AppendLine($"Completed in {EditorApplication.timeSinceStartup - started:F1}s.");
            File.WriteAllText("Library/RoomTransitionPerformanceAudit.txt", report.ToString());
            UnityEngine.Debug.Log(report.ToString());
            SessionState.SetBool(Key, false);
            SessionState.SetBool(Key + ".Verify", false);
            EditorApplication.update -= Tick;
            EditorApplication.Exit(code);
        }
    }
}
