using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Project.BlockFeatures;
using Project.ColorBlocks;
using Project.Items;
using Project.LevelEditor;
using Project.Player;
using Project.Rooms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Project.BossLogic.Editor
{
    /// <summary>Contract tests; existing collectible audits cover real Trigger/flight physics.</summary>
    public static class CollectibleBossProgressAudit
    {
        private const string Flag = "CollectibleBossProgressAudit.Running";
        private const string Fixture = "CollectibleBossProgressAudit.Fixture";
        private static double started;
        private static int checks;
        private static readonly List<GameObject> Objects = new List<GameObject>();
        public static void Run()
        {
            if (!Application.dataPath.Replace('\\', '/').Contains("/Library/FlowRetirementProbe/Assets") ||
                Array.IndexOf(Environment.GetCommandLineArgs(), "-noaudio") < 0)
                throw new InvalidOperationException("Use the silent isolated probe project only.");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            string path = "Assets/__CollectibleBossProgressAudit_" + Guid.NewGuid().ToString("N") + ".unity";
            if (!EditorSceneManager.SaveScene(scene, path)) throw new InvalidOperationException("Cannot save isolated fixture.");
            SessionState.SetString(Fixture, path);
            SessionState.SetBool(Flag, true);
            Resume();
            EditorApplication.EnterPlaymode();
        }
        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!SessionState.GetBool(Flag, false)) return;
            started = EditorApplication.timeSinceStartup;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }
        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup - started > 120d) { Finish(false, "Timeout"); return; }
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.time < .3f) return;
            EditorApplication.update -= Tick;
            bool passed = false;
            string report;
            try
            {
                checks = 0;
                AudioListener.pause = true;
                AudioListener.volume = 0f;
                Verify();
                passed = true;
                report = "PASS " + checks + " collectible progress/Boss gate/ending/travel contract checks";
            }
            catch (Exception error) { report = "FAIL " + checks + "\n" + error; Debug.LogException(error); }
            finally
            {
                foreach (GameObject obj in Objects) if (obj != null) Object.DestroyImmediate(obj);
                Objects.Clear();
            }
            Finish(passed, report);
        }
        private static GameObject Root(string name)
        {
            var value = new GameObject(name); Objects.Add(value); return value;
        }
        private static void Check(bool value, string label)
        {
            checks++;
            if (!value) throw new InvalidOperationException("COLLECTIBLE BOSS: " + label);
        }
        private static void Verify()
        {
            Scene scene = SceneManager.GetActiveScene();
            RoomLifecycleManager manager = Root("Progress audit rooms").AddComponent<RoomLifecycleManager>();
            var a = new RoomRegion { id = "progress_a" };
            var b = new RoomRegion { id = "progress_b" };
            for (int x = 100; x < 110; x++) a.cells.Add(new Vector2Int(x, -21));
            for (int x = 110; x < 120; x++) b.cells.Add(new Vector2Int(x, -21));
            manager.Configure(new List<RoomRegion> { a, b }, 1f, string.Empty);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Content/WorldItems/Collectible.prefab");
            Check(prefab != null, "ordinary collectible prefab exists");
            GameObject parent = Root("Authored items"); parent.SetActive(false);
            var items = new List<CollectiblePickup>();
            for (int index = 0; index < 20; index++)
            {
                GameObject instance = Object.Instantiate(prefab, parent.transform);
                instance.transform.position = new Vector3(
                    index < 10 ? 100.5f + index : 110.5f + index - 10,
                    20.5f,
                    0f);
                var placed = instance.GetComponent<LevelEditorPlacedBlock>() ?? instance.AddComponent<LevelEditorPlacedBlock>();
                placed.SetPlanningSource("progress_reward", "audit", new Vector2Int(index, 0));
                var item = instance.GetComponent<CollectiblePickup>(); items.Add(item);
                var member = instance.GetComponent<RoomResetObject>() ?? instance.AddComponent<RoomResetObject>();
                manager.Register(member);
                if (index == 9) instance.SetActive(false);
            }
            parent.SetActive(true);
            CollectibleProgressService.RefreshSceneCatalog(scene);
            var progress = CollectibleProgressService.GetSceneProgress(scene);
            Check(progress.Total == 20 && progress.Collected == 0 && progress.Remaining == 20, "inactive authored item remains in denominator");
            Check(CollectibleProgressService.GetRegionProgress(scene, a.id).Total == 10 &&
                CollectibleProgressService.GetRegionProgress(scene, b.id).Total == 10, "room totals use author identity");
            GameObject playerObject = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Content/Player/Player.prefab"));
            Objects.Add(playerObject);
            PlayerController player = playerObject.GetComponent<PlayerController>();
            player.GetComponent<PlayerInputDriver>().enabled = false;
            player.transform.position = new Vector3(100.5f, 20.5f, 0f);
            player.Motor.position = player.transform.position;
            player.Motor.useGravity = false;
            GameObject gateObject = Root("Boss entrance gate"); gateObject.AddComponent<BoxCollider>().isTrigger = true;
            var gate = gateObject.AddComponent<BossCollectibleEntryGate>();
            new SerializedObject(gate).Update();
            SerializedObject gateData = new SerializedObject(gate);
            Check(gateData.FindProperty("gateEnabled").boolValue, "Boss entry gate defaults to enabled");
            gateData.FindProperty("gateEnabled").boolValue = false; gateData.ApplyModifiedPropertiesWithoutUndo();
            Check(gate.CanPlayTimeline(playerObject), "disabled Boss entry gate bypasses the progress threshold");
            gateData.FindProperty("gateEnabled").boolValue = true; gateData.ApplyModifiedPropertiesWithoutUndo();
            Check(gateData.FindProperty("returnDestinationId").stringValue == "Start", "Boss entry rejection defaults to formal Start anchor");
            gateData.FindProperty("showHint").boolValue = false; gateData.ApplyModifiedPropertiesWithoutUndo();
            gateData.FindProperty("returnDestinationId").stringValue = string.Empty; gateData.ApplyModifiedPropertiesWithoutUndo();
            bool warned = false;
            int loadedScenes = SceneManager.sceneCount;
            Application.LogCallback warning = (message, trace, type) =>
            { if (type == LogType.Warning && message.StartsWith("[BossEntry]", StringComparison.Ordinal)) warned = true; };
            Application.logMessageReceived += warning;
            try
            {
                Check(!gate.CanPlayTimeline(playerObject), "zero collected cannot enter Boss");
                ContactTimelineTrigger contact = gate.GetComponent<ContactTimelineTrigger>();
                contact.OnTriggerEnter(player.GetComponent<Collider>());
                Check(!contact.HasTriggered && contact.ContactCount == 0, "rejected real contact does not consume Once or retain contact");
            }
            finally { Application.logMessageReceived -= warning; }
            Check(warned && SceneManager.sceneCount == loadedScenes,
                "independent Boss without same-scene Start warns instead of loading another scene");
            GameObject startObject = Root("Authored Start anchor");
            startObject.transform.position = new Vector3(101.5f, 20.5f, 0f);
            TeleportAnchorFeature startAnchor = startObject.AddComponent<TeleportAnchorFeature>();
            startAnchor.SetTeleportId("Start");
            RespawnAnchorFeature oldBossPoint = Root("Old Boss checkpoint").AddComponent<RespawnAnchorFeature>();
            oldBossPoint.transform.position = new Vector3(108.5f, 20.5f, 0f);
            oldBossPoint.SetBossArenaRespawnPoint(true); oldBossPoint.Activate();
            PlayerRespawnPointRegistry.Bind(player, oldBossPoint);
            Check(PlayerRespawnPointRegistry.TryFindPreferred(player, player.transform.position, out var beforeReturn) &&
                ReferenceEquals(beforeReturn, oldBossPoint), "fixture begins bound to an activated Boss checkpoint");
            GameObject returnGateObject = Root("Boss gate with same-scene Start");
            returnGateObject.AddComponent<BoxCollider>().isTrigger = true;
            var returnGate = returnGateObject.AddComponent<BossCollectibleEntryGate>();
            var returnGateData = new SerializedObject(returnGate);
            returnGateData.FindProperty("showHint").boolValue = false; returnGateData.ApplyModifiedPropertiesWithoutUndo();
            int relocated = 0;
            Action cameraRelocated = () => relocated++;
            player.CameraFollowRelocated += cameraRelocated;
            try
            {
                using (player.AcquireControlLock(returnGate))
                {
                    Check(!returnGate.CanPlayTimeline(playerObject) && player.IsControlLocked &&
                        Vector3.Distance(player.transform.position, startObject.transform.position) < .001f,
                        "insufficient gate returns exactly to Start without stealing an existing control lock");
                }
            }
            finally { player.CameraFollowRelocated -= cameraRelocated; }
            Check(!player.IsControlLocked && relocated == 1, "story return requests camera relocation and its caller lock releases normally");
            Check(PlayerRespawnPointRegistry.TryFindPreferred(player, oldBossPoint.RespawnPosition, out var returnedPoint) &&
                Vector3.Distance(returnedPoint.RespawnPosition, startObject.transform.position) < .001f &&
                !(returnedPoint is RespawnAnchorFeature bossPoint && bossPoint.IsBossArenaRespawnPoint),
                "Start story return replaces old Boss binding with a non-Boss session respawn fallback");
            ContactTimelineTrigger returnContact = returnGate.GetComponent<ContactTimelineTrigger>();
            returnContact.OnTriggerEnter(player.GetComponent<Collider>());
            Check(!returnContact.HasTriggered && returnContact.ContactCount == 0,
                "a rejected Start return still leaves Once unconsumed");
            for (int i = 0; i < 6; i++) items[i].RestoreCollectedState();
            var colors = ColorRuntimeService.Instance;
            colors.RestoreRunProgress(new[] { "red", "green", "blue" }, Array.Empty<string>());
            Check(!gate.CanEnter && gate.CollectedColorKeyCount == 3,
                "30 percent and only the three scene-color keys still reject Boss entry");
            colors.UnlockAbility("red"); colors.UnlockAbility("green");
            Check(!gate.CanEnter && gate.CollectedColorKeyCount == 5, "five keys still reject Boss entry");
            colors.UnlockAbility("blue");
            Check(gate.CanPlayTimeline(playerObject), "exactly 30 percent and all six keys enter");
            Check(!PlayerThinkingTimelinePlayable.HasSufficientCollectibles(1, 20), "5 percent uses failure ending");
            Check(new CollectibleProgressSnapshot(1, 20).MeetsRatio(.05f) &&
                !new CollectibleProgressSnapshot(0, 20).MeetsRatio(.05f), "5 percent uses the same exact integer rule");
            Check(new CollectibleProgressSnapshot(2, 35).MeetsRatio(.05f) &&
                !new CollectibleProgressSnapshot(1, 35).MeetsRatio(.05f), "5 percent of 35 requires two collectibles");
            Check(EnterBossTimelinePlayable.HasSufficientCollectibles(6, 20) &&
                !EnterBossTimelinePlayable.HasSufficientCollectibles(5, 20),
                "enter_boss accepts exactly 30 percent and rejects below it");
            Check(PlayerThinkingTimelinePlayable.HasSufficientCollectibles(12, 20), "exactly 60 percent uses successful ending");
            Check(!PlayerThinkingTimelinePlayable.HasSufficientCollectibles(11, 20), "below 60 percent uses failure ending");
            Check(PlayerThinkingTimelinePlayable.HasSufficientCollectibles(21, 35) &&
                !PlayerThinkingTimelinePlayable.HasSufficientCollectibles(20, 35), "60 percent of 35 requires 21 collectibles");
            Check(PlayerThinkingTimelinePlayable.HasSufficientCollectibles(60, 100) &&
                !PlayerThinkingTimelinePlayable.HasSufficientCollectibles(59, 100), "60 of 100 succeeds while 59 percent still fails");
            Check(!PlayerThinkingTimelinePlayable.HasSufficientCollectibles(2, 20, .1000001f),
                "an explicitly higher seven-digit threshold is not rounded down to 10 percent");
            Check(!PlayerThinkingTimelinePlayable.HasSufficientCollectibles(0, 0), "missing denominator no longer defaults success");
            manager.ResetRegion(a.id); manager.ResetRegion(b.id);
            progress = CollectibleProgressService.GetSceneProgress(scene);
            Check(progress.Collected == 6 && progress.Remaining == 14, "R-style region restoration does not undo collected progress");
            Check(CollectibleProgressService.GetRegionProgress(scene, a.id).Remaining == 4 &&
                CollectibleProgressService.GetRegionProgress(scene, b.id).Remaining == 10, "room completion and remainder are accurate");
            var saved = new List<string>(); CollectibleProgressService.CopyCollectedStableIds(scene, saved);
            Check(saved.Count == 6 && new HashSet<string>(saved).Count == 6, "future save export includes unique stable IDs only");
            int callbacks = 0;
            Action<CollectiblePickup, GameObject> collected = (item, actor) => callbacks++;
            CollectiblePickup.Collected += collected;
            try { CollectibleProgressService.RestoreCollectedStableIds(scene, saved); CollectibleProgressService.RestoreCollectedStableIds(scene, saved); }
            finally { CollectiblePickup.Collected -= collected; }
            Check(callbacks == 0 && CollectibleProgressService.GetSceneProgress(scene).Collected == 6, "save restore is silent and idempotent");
            GameObject cosmetics = Root("Ending cosmetic clones"); cosmetics.SetActive(false);
            for (int index = 0; index < 6; index++)
            {
                GameObject instance = Object.Instantiate(prefab, cosmetics.transform);
                var item = instance.GetComponent<CollectiblePickup>();
                item.SetExcludedFromProgress(true); item.enabled = false;
            }
            cosmetics.SetActive(true);
            CollectibleProgressService.RefreshSceneCatalog(scene);
            Check(CollectibleProgressService.GetSceneProgress(scene).Total == 20, "six temporary ending props do not affect denominator");
            Scene emptyBossScene = SceneManager.CreateScene("Progress audit empty Boss scene");
            Check(CollectibleProgressService.GetSceneProgress(emptyBossScene).Total == 0 &&
                CollectibleProgressService.GetWorldProgress().Total == 20 && CollectibleProgressService.GetWorldProgress().Collected == 6,
                "a separate empty Boss scene inherits world progress rather than replacing it with zero");
            GameObject bossObject = Root("Boss progress fixture"); bossObject.transform.position = new Vector3(106.5f, 20.5f, 0f);
            bossObject.tag = "Boss";
            bossObject.AddComponent<BoxCollider>();
            BossController boss = bossObject.AddComponent<BossController>();
            boss.Body.useGravity = false;
            Check(BossTravelProgress.HasLivingBoss(scene) && !BossTravelProgress.HasLivingBoss(emptyBossScene),
                "living Boss state is scoped to the owning scene");
            Check(BossTravelProgress.GetState(scene).BossRegionId == a.id, "Boss room captured from initial position");
            player.SetSprintInput(true);
            Check(boss != null && player.IsSprintLocked && !player.SprintRequested,
                "living Boss acquires the player sprint lock");
            Physics.SyncTransforms();
            GameObject normalLava = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/_Project/Content/ColorBlocks/Prefabs/Red_Lava.prefab"),
                new Vector3(106.25f, 20.5f, 0f),
                Quaternion.identity); Objects.Add(normalLava);
            GameObject gravityLava = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/_Project/Content/ColorBlocks/Prefabs/Red_GravityLava.prefab"),
                new Vector3(106.25f, 20.5f, 0f),
                Quaternion.identity); Objects.Add(gravityLava);
            GameObject outsideLava = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/_Project/Content/ColorBlocks/Prefabs/Red_Lava.prefab"),
                new Vector3(107.5f, 20.5f, 0f),
                Quaternion.identity); Objects.Add(outsideLava);
            Physics.SyncTransforms();
            boss.PrepareForBossRespawn();
            Check(!normalLava.activeSelf && !gravityLava.activeSelf,
                "Boss respawn sweep clears normal and gravity lava inside left-to-center X range");
            Check(outsideLava.activeSelf,
                "Boss respawn sweep preserves lava outside left-to-center X range");
            VerifyRoomScopedRespawn(player, boss, oldBossPoint, startAnchor);
            boss.transform.position = new Vector3(100.5f, 20.5f, 0f); boss.Body.position = boss.transform.position;
            boss.ActivateHealthSystem();
            BossTravelState state = BossTravelProgress.GetState(scene);
            Check(state.HasEnteredBoss && state.IsBossFightActive && !state.HasDefeatedBoss && state.BossRegionId == a.id,
                "entering Boss fight gates map and chase does not change excluded room");
            boss.Kill(); state = BossTravelProgress.GetState(scene);
            Check(state.HasEnteredBoss && state.HasDefeatedBoss && !state.IsBossFightActive, "real Boss death unlock state is retained");
            Check(!BossTravelProgress.HasLivingBoss(scene), "real Boss death clears the scene living state");
            player.SetSprintInput(true);
            Check(!player.IsSprintLocked && player.SprintRequested,
                "Boss death releases the player sprint lock");
            BossTravelState otherSceneState = BossTravelProgress.GetState(emptyBossScene);
            Check(otherSceneState.HasEnteredBoss && otherSceneState.HasDefeatedBoss && !otherSceneState.IsBossFightActive &&
                otherSceneState.BossRegionId == null && !BossTravelProgress.IsBossScene(scene) && !BossTravelProgress.IsBossScene(emptyBossScene),
                "global Boss unlock flags survive scene boundaries without leaking local excluded room IDs");
            BossTravelProgress.SetDedicatedBossScene(emptyBossScene, true);
            Check(BossTravelProgress.IsBossScene(emptyBossScene) && !BossTravelProgress.IsBossScene(scene),
                "explicit dedicated arena does not prohibit unrelated rooms in a mixed map");
            BossTravelProgress.SetDedicatedBossScene(emptyBossScene, false);
            bossObject.SetActive(false); bossObject.SetActive(true);
            Check(BossTravelProgress.GetState(scene).BossRegionId == a.id, "re-enabled chasing Boss does not redefine room");
            Check(!BossTravelProgress.HasLivingBoss(scene), "re-enabled defeated Boss is not treated as alive");
        }
        private static void VerifyRoomScopedRespawn(PlayerController player, BossController boss,
            RespawnAnchorFeature oldBossPoint, TeleportAnchorFeature startAnchor)
        {
            Check(ReferenceEquals(BossRespawnStateRegistry.FindAvailableInScene(player.gameObject.scene), boss),
                "respawn isolation fixture has a real registered Boss receiver, not an early no-Boss exit");
            const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
            FieldInfo deathField = typeof(PlayerController).GetField("deathPosition", PrivateInstance);
            MethodInfo fallback = typeof(PlayerController).GetMethod("TryApplyBossArenaRespawnFallback", PrivateInstance);
            Check(deathField != null && fallback != null, "room-scoped respawn fallback test hooks exist");
            object previousDeath = deathField.GetValue(player);
            bool oldPointEnabled = oldBossPoint.enabled;
            var dormantBossPoint = Root("Unactivated Boss fallback point").AddComponent<RespawnAnchorFeature>();
            dormantBossPoint.transform.position = new Vector3(107.5f, 20.5f, 0f);
            dormantBossPoint.SetBossArenaRespawnPoint(true);
            PlayerRespawnMode previousMode = player.RespawnMode;
            float previousLoadingSeconds = player.RespawnLoadingSeconds;
            try
            {
                // The activated checkpoint would deliberately suppress the dormant fallback.
                // Disable it so the same-room positive control exercises the actual selection path.
                oldBossPoint.enabled = false;
                Check(!dormantBossPoint.IsRespawnPointAvailable, "Boss fallback fixture has not been activated");
                Vector3 ordinaryPosition = new Vector3(111.5f, 20.5f, 0f);
                deathField.SetValue(player, ordinaryPosition);
                object[] ordinaryWithoutPoint = { null, ordinaryPosition };
                fallback.Invoke(player, ordinaryWithoutPoint);
                Check(ordinaryWithoutPoint[0] == null &&
                    Vector3.Distance((Vector3)ordinaryWithoutPoint[1], ordinaryPosition) < .001f,
                    "an ordinary Gaming room without a selected point is not hijacked by another room's Boss fallback");
                var ordinaryPoint = new AuditRespawnPoint(ordinaryPosition);
                object[] ordinaryWithPoint = { ordinaryPoint, ordinaryPosition };
                fallback.Invoke(player, ordinaryWithPoint);
                Check(ReferenceEquals(ordinaryWithPoint[0], ordinaryPoint) &&
                    Vector3.Distance((Vector3)ordinaryWithPoint[1], ordinaryPosition) < .001f,
                    "an ordinary Gaming room keeps its selected non-Boss checkpoint");
                Vector3 unknownPosition = new Vector3(500.5f, 20.5f, 0f);
                deathField.SetValue(player, unknownPosition);
                object[] unknownRoom = { null, unknownPosition };
                fallback.Invoke(player, unknownRoom);
                Check(unknownRoom[0] == null && Vector3.Distance((Vector3)unknownRoom[1], unknownPosition) < .001f,
                    "a position outside known rooms cannot fall back to an unrelated Boss room");
                deathField.SetValue(player, dormantBossPoint.transform.position);
                object[] bossRoom = { null, dormantBossPoint.transform.position };
                fallback.Invoke(player, bossRoom);
                Check(ReferenceEquals(bossRoom[0], dormantBossPoint) &&
                    Vector3.Distance((Vector3)bossRoom[1], dormantBossPoint.RespawnPosition) < .001f,
                    "positive control: the same Boss room still selects its unactivated Boss fallback");
                Check(PlayerRespawnPointRegistry.TryFindPreferred(player, ordinaryPosition, out var storyPoint) &&
                    !(storyPoint is RespawnAnchorFeature) &&
                    Vector3.Distance(storyPoint.RespawnPosition, startAnchor.transform.position) < .001f,
                    "Start return still owns the player's session-only story respawn point");
                object[] storyInBossRoom = { storyPoint, storyPoint.RespawnPosition };
                fallback.Invoke(player, storyInBossRoom);
                Check(ReferenceEquals(storyInBossRoom[0], storyPoint) &&
                    Vector3.Distance((Vector3)storyInBossRoom[1], startAnchor.transform.position) < .001f,
                    "Start story binding is not replaced even when the prior death position was in the Boss room");

                // Exercise the public R path as well as the private selection branch. A real
                // floor prevents the normal safe-ground search from falling back to prefab origin.
                Check(player.TryTeleportToStoryAnchor(startAnchor), "Start story return is available before the R regression");
                Physics.SyncTransforms();
                GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube); Objects.Add(floor);
                floor.name = "Ground below authored Start";
                floor.transform.localScale = new Vector3(3f, 1f, 2f);
                floor.transform.position = new Vector3(startAnchor.transform.position.x,
                    player.GetComponent<Collider>().bounds.min.y - .5f, startAnchor.transform.position.z);
                Physics.SyncTransforms();
                int respawnCount = 0;
                Action onRespawn = () => respawnCount++;
                Vector3 bossPosition = boss.transform.position;
                player.RespawnCompleted += onRespawn;
                try
                {
                    player.ConfigureRespawn(PlayerRespawnMode.Standard);
                    player.RequestRespawn();
                }
                finally { player.RespawnCompleted -= onRespawn; }
                Check(respawnCount == 1 && Vector3.Distance(player.transform.position, startAnchor.transform.position) < .05f,
                    "actual R after Start return respawns at Start, not the dormant Boss checkpoint");
                Check(PlayerRespawnPointRegistry.TryFindPreferred(player, dormantBossPoint.RespawnPosition, out var afterR) &&
                    !(afterR is RespawnAnchorFeature) &&
                    Vector3.Distance(afterR.RespawnPosition, startAnchor.transform.position) < .001f &&
                    Vector3.Distance(boss.transform.position, bossPosition) < .001f,
                    "R keeps the non-Boss Start binding and does not apply Boss checkpoint relocation");
            }
            finally
            {
                dormantBossPoint.enabled = false;
                oldBossPoint.enabled = oldPointEnabled;
                deathField.SetValue(player, previousDeath);
                player.ConfigureRespawn(previousMode, previousLoadingSeconds);
            }
        }
        private sealed class AuditRespawnPoint : IPlayerRespawnPoint
        {
            public AuditRespawnPoint(Vector3 position) { RespawnPosition = position; }
            public bool IsRespawnPointAvailable => true;
            public Vector3 RespawnPosition { get; }
            public int RespawnPriority => -1;
            public float RespawnRadiusBlocks => 0f;
        }
        private static void Finish(bool passed, string report)
        {
            EditorApplication.update -= Tick; SessionState.EraseBool(Flag);
            string path = SessionState.GetString(Fixture, string.Empty); SessionState.EraseString(Fixture);
            if (path.StartsWith("Assets/__CollectibleBossProgressAudit_", StringComparison.Ordinal)) AssetDatabase.DeleteAsset(path);
            File.WriteAllText("Library/CollectibleBossProgressAudit.txt", report); Debug.Log(report);
            EditorApplication.Exit(passed ? 0 : 1);
        }
    }
}
