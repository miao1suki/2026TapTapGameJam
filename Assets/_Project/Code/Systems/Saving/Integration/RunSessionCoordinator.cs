using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Project.BlockFeatures;
using Project.BossLogic;
using Project.ColorBlocks;
using Project.GameFlow;
using Project.Items;
using Project.Player;
using Project.Rooms;
using Project.Saving;
using Project.StartMenu;
using Project.StoryStart;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using TapTap.TA.RgbGlitch;

namespace Project.Saving
{
    [DefaultExecutionOrder(-19000)]
    [DisallowMultipleComponent]
    public sealed class RunSessionCoordinator :
        MonoBehaviour
    {
        private const string StoryStartResourcePath =
            "story_start";
        private const float DefaultLoadedRunGlitchFadeSeconds =
            1.2f;
        private const float NewRunEntryCoverDuration =
            1.2f;
        private const float NewRunCoveredMinimumSeconds =
            2f;
        private static RunSessionCoordinator instance;

        private GameFlowController flow;
        private RunSaveData runData;
        private Vector3 runStartPlayerPosition;
        private bool runActive;
        private bool loading;
        private bool suppressAutoSave;
        private bool completing;
        private IDisposable restoreControlLock;
        private GameplayEntryCircleCover newRunEntryCover;
        private float newRunEntryCoveredAt;

        public static RunSessionCoordinator Instance => instance;
        public bool HasActiveRun => runActive;

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (instance != null)
            {
                return;
            }

            GameObject root =
                new GameObject("[Run Session Coordinator]");
            instance =
                root.AddComponent<RunSessionCoordinator>();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            CollectiblePickup.Collected -=
                HandleCollectibleCollected;
            CollectiblePickup.Collected +=
                HandleCollectibleCollected;
            ColorKeyPickup.Collected -=
                HandleColorKeyCollected;
            ColorKeyPickup.Collected +=
                HandleColorKeyCollected;
            ColorRuntimeService.ProgressChanged -=
                HandleColorProgressChanged;
            ColorRuntimeService.ProgressChanged +=
                HandleColorProgressChanged;
            BossTravelProgress.Changed -=
                HandleBossProgressChanged;
            BossTravelProgress.Changed +=
                HandleBossProgressChanged;
            PlayerRespawnPointRegistry.BindingChanged -=
                HandleRespawnBindingChanged;
            PlayerRespawnPointRegistry.BindingChanged +=
                HandleRespawnBindingChanged;
            StartMenuFlowBridge.BeforeStartTransition -=
                HandleBeforeStartTransition;
            StartMenuFlowBridge.BeforeStartTransition +=
                HandleBeforeStartTransition;
            SavePanelController.DeleteCurrentSaveRequested -=
                HandleDeleteCurrentSaveRequested;
            SavePanelController.DeleteCurrentSaveRequested +=
                HandleDeleteCurrentSaveRequested;
            StartCoroutine(ConnectToFlow());
        }

        private void OnDisable()
        {
            CollectiblePickup.Collected -=
                HandleCollectibleCollected;
            ColorKeyPickup.Collected -=
                HandleColorKeyCollected;
            ColorRuntimeService.ProgressChanged -=
                HandleColorProgressChanged;
            BossTravelProgress.Changed -=
                HandleBossProgressChanged;
            PlayerRespawnPointRegistry.BindingChanged -=
                HandleRespawnBindingChanged;
            StartMenuFlowBridge.BeforeStartTransition -=
                HandleBeforeStartTransition;
            SavePanelController.DeleteCurrentSaveRequested -=
                HandleDeleteCurrentSaveRequested;
            ReleaseNewRunEntryCover();
            if (flow != null)
            {
                flow.ActiveSceneChanged -=
                    HandleActiveSceneChanged;
                flow = null;
            }
        }

        private IEnumerator ConnectToFlow()
        {
            while (isActiveAndEnabled && flow == null)
            {
                flow = GameFlowController.Instance;
                if (flow != null)
                {
                    flow.ActiveSceneChanged -=
                        HandleActiveSceneChanged;
                    flow.ActiveSceneChanged +=
                        HandleActiveSceneChanged;
                    if (flow.ActiveSceneId.HasValue)
                    {
                        HandleActiveSceneChanged(
                            flow.ActiveSceneId.Value);
                    }
                    yield break;
                }
                yield return null;
            }
        }

        private void HandleActiveSceneChanged(
            GameFlowSceneId sceneId)
        {
            if (sceneId != GameFlowSceneId.Gaming ||
                runActive ||
                loading)
            {
                return;
            }

            StartCoroutine(BeginRun());
        }

        private IEnumerator BeginRun()
        {
            loading = true;
            yield return null;
            PlayerController player = FindPlayer();
            Scene scene = player != null
                ? player.gameObject.scene
                : SceneManager.GetActiveScene();
            if (player == null)
            {
                loading = false;
                yield break;
            }
            CollectibleCounterPresenter.EnsureForScene(scene);

            bool hasValidSave = RunSaveService.TryLoad(
                out RunSaveData loaded,
                out string error);
            if (!hasValidSave &&
                RunSaveService.HasActiveRun)
            {
                RunSaveService.QuarantineCorrupt();
                Debug.LogWarning(
                    "[RunSession] 运行存档无效，已创建新一轮。" +
                    error);
            }

            if (!hasValidSave)
            {
                runData = RunSaveService.CreateNew(
                    scene.path);
                runStartPlayerPosition =
                    player.transform.position;
                runData.initialPlayerPosition =
                    runStartPlayerPosition;
                runData.hasFallbackPlayerPosition = true;
                runData.fallbackPlayerPosition =
                    runStartPlayerPosition;
                runActive = true;
                suppressAutoSave = true;
                ResetRunRuntimeState(scene);
                bool startsCovered =
                    newRunEntryCover != null;
                float coveredHoldSeconds =
                    startsCovered
                        ? Mathf.Max(
                            0f,
                            NewRunCoveredMinimumSeconds -
                            (Time.unscaledTime -
                             newRunEntryCoveredAt))
                        : 0f;
                yield return PlayNewRunStoryStart(
                    player,
                    startsCovered,
                    coveredHoldSeconds);
                suppressAutoSave = false;
                loading = false;
                SaveNow();
                yield break;
            }

            runData = loaded;
            ReleaseNewRunEntryCover();
            runStartPlayerPosition =
                runData.initialPlayerPosition;
            runActive = true;
            suppressAutoSave = true;
            BeginLoadedRunTransition(player);
        }

        private IEnumerator PlayNewRunStoryStart(
            PlayerController player,
            bool startsCovered,
            float coveredHoldSeconds)
        {
            PlayableAsset storyStart =
                Resources.Load<PlayableAsset>(
                    StoryStartResourcePath);
            if (storyStart == null)
            {
                Debug.LogWarning(
                    "[RunSession] 未找到 Resources/" +
                    StoryStartResourcePath +
                    "，新游戏跳过开场演出。");
                ReleaseNewRunEntryCover();
                yield break;
            }

            IPlayerControlLockTarget lockTarget =
                player as IPlayerControlLockTarget;
            using IDisposable controlLock =
                lockTarget?.AcquireControlLock(this);
            GameObject directorObject = new GameObject(
                "New Run Story Start Director");
            PlayableDirector director =
                directorObject.AddComponent<PlayableDirector>();
            if (startsCovered)
            {
                StoryStartEntryHandoff handoff =
                    directorObject.AddComponent<
                        StoryStartEntryHandoff>();
                handoff.startsCovered = true;
                handoff.coveredHoldSeconds =
                    Mathf.Max(
                        0f,
                        coveredHoldSeconds);
            }
            director.playableAsset = storyStart;
            director.time = 0d;
            director.Play();
            yield return null;
            if (startsCovered)
            {
                yield return new WaitForSecondsRealtime(
                    .1f);
                ReleaseNewRunEntryCover();
            }

            float deadline =
                Time.unscaledTime +
                (float)storyStart.duration +
                10f;
            while (director != null &&
                   director.state == PlayState.Playing &&
                   Time.unscaledTime < deadline)
            {
                yield return null;
            }
            if (director != null)
            {
                director.Stop();
            }
            Destroy(directorObject);
        }

        private IEnumerator HandleBeforeStartTransition()
        {
            if (runActive ||
                loading ||
                newRunEntryCover != null)
            {
                yield break;
            }

            bool hasValidSave = RunSaveService.TryLoad(
                out _,
                out _);
            if (hasValidSave)
            {
                yield break;
            }

            newRunEntryCover =
                GameplayEntryCircleCover.Create(
                    transform);
            yield return newRunEntryCover.CoverAsync(
                NewRunEntryCoverDuration);
            newRunEntryCoveredAt =
                Time.unscaledTime;
            StartCoroutine(
                ReleaseEntryCoverIfTransitionStalls());
        }

        private IEnumerator ReleaseEntryCoverIfTransitionStalls()
        {
            float deadline =
                Time.unscaledTime + 8f;
            while (newRunEntryCover != null &&
                   !runActive &&
                   Time.unscaledTime < deadline)
            {
                yield return null;
            }
            if (newRunEntryCover != null &&
                !runActive)
            {
                Debug.LogWarning(
                    "[RunSession] 新档入场未开始，已释放入场遮罩。");
                ReleaseNewRunEntryCover();
            }
        }

        private void BeginLoadedRunTransition(
            PlayerController player)
        {
            IPlayerControlLockTarget lockTarget =
                player as IPlayerControlLockTarget;
            restoreControlLock =
                lockTarget?.AcquireControlLock(this);
            StartCoroutine(
                PlayLoadedRunGlitchTransition(player));
        }

        private IEnumerator PlayLoadedRunGlitchTransition(
            PlayerController player)
        {
            RgbGlitchEffect.PlayDeath(0f);
            yield return null;

            bool restored = false;
            try
            {
                ApplyLoadedRun(player);
                restored = true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }

            if (!restored)
            {
                RgbGlitchEffect.PlayRespawn(0f);
                HandleLoadedRunTransitionFinished(false);
                yield break;
            }

            // Give the restored scene one rendered frame before revealing it.
            yield return null;
            float fadeSeconds =
                RgbGlitchSceneDriver.Active != null
                    ? RgbGlitchSceneDriver.Active
                        .RespawnFadeSeconds
                    : DefaultLoadedRunGlitchFadeSeconds;
            RgbGlitchEffect.PlayRespawn(fadeSeconds);
            if (fadeSeconds > 0f)
            {
                yield return new WaitForSecondsRealtime(
                    fadeSeconds);
            }

            HandleLoadedRunTransitionFinished(true);
        }

        private void ApplyLoadedRun(
            PlayerController player)
        {
            Scene scene = player.gameObject.scene;
            RestoreRespawnAnchors();
            ColorRuntimeService.Instance.RestoreRunProgress(
                runData.unlockedColorIds,
                runData.unlockedAbilityIds);
            ColorKeyPickup.RestoreConsumedStableIds(
                scene,
                runData.consumedColorKeyIds);
            // Reconcile both sides of the save: authored inactive/suspended
            // snapshots must not keep UNSAVED pickups unavailable. This resets
            // only ordinary collectibles, not keys, achievements or the run.
            CollectibleProgressService.ResetSessionProgress(scene);
            CollectibleProgressService.RestoreCollectedStableIds(
                scene,
                runData.collectedCollectibleIds);
            BossTravelProgress.RestoreRuntimeProgress(
                scene,
                runData.bossEntered,
                runData.bossDefeated);

            RespawnAnchorFeature checkpoint = null;
            if (RespawnAnchorChainRuntime.TryFindAnchor(
                    runData.currentRespawnAnchorId,
                    out RespawnAnchorFeature restored))
            {
                if (restored.IsBossArenaRespawnPoint)
                {
                    if (RespawnAnchorChainRuntime
                        .TryFindBossEntryFallback(
                            restored,
                            out RespawnAnchorFeature bossEntry))
                    {
                        // Loading must recover to the authored entry even when
                        // the player bypassed its activation trigger before the fight.
                        bossEntry.Activate();
                        checkpoint = bossEntry;
                        Debug.Log(
                            "[RunSession] 当前存档为 Boss 重生点，" +
                            "读档改为恢复 Boss 前置出生点。");
                    }
                    else
                    {
                        checkpoint = restored;
                        Debug.LogWarning(
                            "[RunSession] 当前存档为 Boss 重生点，" +
                            "但同场景链表中没有可用的 Boss 前置出生点。");
                    }
                }
                else
                {
                    checkpoint = restored;
                }
            }
            player.TryRestoreLoadedRunPosition(
                checkpoint,
                runData.hasFallbackPlayerPosition,
                runData.fallbackPlayerPosition);
        }

        private void HandleLoadedRunTransitionFinished(
            bool completed)
        {
            restoreControlLock?.Dispose();
            restoreControlLock = null;
            suppressAutoSave = false;
            loading = false;
            if (completed)
            {
                SaveNow();
            }
        }

        private void RestoreRespawnAnchors()
        {
            RespawnAnchorChainSaveData saveData =
                default;
            try
            {
                saveData =
                    JsonUtility.FromJson<
                        RespawnAnchorChainSaveData>(
                        runData.respawnAnchorSaveJson);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[RunSession] 重生点快照解析失败。\n" +
                    exception);
            }
            if (saveData != null)
            {
                RespawnAnchorChainRuntime.TryRestoreSaveData(
                    saveData,
                    out string error);
                if (!string.IsNullOrWhiteSpace(error))
                {
                    Debug.LogWarning(
                        "[RunSession] 重生点恢复失败：" +
                        error);
                }
            }
        }

        private void ResetRunRuntimeState(Scene scene)
        {
            RespawnAnchorChainRuntime.ResetRuntimeState();
            ColorRuntimeService.Instance.ResetProgress();
            ColorKeyPickup.ResetPersistentConsumption(scene);
            CollectibleProgressService.ResetAllSessionProgress();
            BossTravelProgress.ResetRuntimeProgress();
            PlayerRespawnPointRegistry.ClearBinding(
                FindPlayer());
        }

        private void SaveNow()
        {
            if (!runActive ||
                loading ||
                suppressAutoSave ||
                completing)
            {
                return;
            }

            PlayerController player = FindPlayer();
            if (player == null)
            {
                return;
            }
            Scene scene = player.gameObject.scene;
            runData.scenePath = scene.path;
            runData.fallbackPlayerPosition =
                player.transform.position;
            runData.hasFallbackPlayerPosition = true;
            runData.currentRespawnAnchorId =
                ResolveCurrentAnchorId(player);
            runData.respawnAnchorSaveJson =
                JsonUtility.ToJson(
                    RespawnAnchorChainRuntime.CaptureSaveData(
                        scene));

            runData.collectedCollectibleIds.Clear();
            CollectibleProgressService.CopyCollectedStableIds(
                scene,
                runData.collectedCollectibleIds);

            runData.consumedColorKeyIds.Clear();
            ColorKeyPickup.CopyConsumedStableIds(
                scene,
                runData.consumedColorKeyIds);

            runData.unlockedColorIds.Clear();
            runData.unlockedAbilityIds.Clear();
            ColorRuntimeService.Instance.CopyUnlockedColorsTo(
                runData.unlockedColorIds);
            ColorRuntimeService.Instance.CopyUnlockedAbilitiesTo(
                runData.unlockedAbilityIds);
            Sort(runData.collectedCollectibleIds);
            Sort(runData.consumedColorKeyIds);
            Sort(runData.unlockedColorIds);
            Sort(runData.unlockedAbilityIds);

            BossTravelState boss =
                BossTravelProgress.GetState(scene);
            runData.bossEntered = boss.HasEnteredBoss;
            runData.bossFightActive = false;
            runData.bossDefeated = boss.HasDefeatedBoss;
            RoomLifecycleManager rooms =
                RoomLifecycleManager.ForScene(scene);
            runData.planningRevision = ComputeSceneAuthoringRevision(scene, rooms);
            RunSaveService.Save(runData);
        }

        private static string ComputeSceneAuthoringRevision(Scene scene, RoomLifecycleManager rooms)
        {
            if (!scene.IsValid() || rooms == null) return string.Empty;
            var source = new StringBuilder(4096);
            source.Append(rooms.CellSize.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append('|');
            foreach (RoomRegion region in rooms.Regions.Where(item => item != null).OrderBy(item => item.id, StringComparer.Ordinal))
            {
                source.Append(region.id).Append(':').Append(region.corridor ? '1' : '0').Append(':');
                foreach (Vector2Int cell in (region.cells ?? new List<Vector2Int>()).OrderBy(cell => cell.x).ThenBy(cell => cell.y))
                    source.Append(cell.x).Append(',').Append(cell.y).Append(';');
                source.Append('|');
            }
            var blocks = new List<Project.LevelEditor.LevelEditorPlacedBlock>();
            foreach (GameObject root in scene.GetRootGameObjects()) blocks.AddRange(root.GetComponentsInChildren<Project.LevelEditor.LevelEditorPlacedBlock>(true));
            foreach (Project.LevelEditor.LevelEditorPlacedBlock block in blocks.OrderBy(item => item.AuthoringId, StringComparer.Ordinal)
                         .ThenBy(item => item.AuthoringLocalCell.x).ThenBy(item => item.AuthoringLocalCell.y))
                source.Append(block.AuthoringId).Append('@').Append(block.Cell.x).Append(',').Append(block.Cell.y).Append(':')
                    .Append(block.SizeCells.x).Append(',').Append(block.SizeCells.y).Append('|');
            return Hash128.Compute(source.ToString()).ToString();
        }

        private string ResolveCurrentAnchorId(
            PlayerController player)
        {
            return PlayerRespawnPointRegistry.TryGetBound(
                       player,
                       out IPlayerRespawnPoint point) &&
                   point is RespawnAnchorFeature anchor
                ? anchor.AnchorId
                : string.Empty;
        }

        private void HandleCollectibleCollected(
            CollectiblePickup item,
            GameObject actor)
        {
            SaveNow();
        }

        private void HandleColorKeyCollected(
            ColorKeyPickup key)
        {
            SaveNow();
        }

        private void HandleColorProgressChanged()
        {
            SaveNow();
        }

        private void HandleBossProgressChanged()
        {
            SaveNow();
        }

        private void HandleRespawnBindingChanged(
            PlayerController player,
            IPlayerRespawnPoint point,
            bool current)
        {
            if (current &&
                point is RespawnAnchorFeature)
            {
                SaveNow();
            }
        }

        private void OnApplicationPause(bool pause)
        {
            if (pause)
            {
                SaveNow();
            }
        }

        private void OnApplicationQuit()
        {
            SaveNow();
        }

        public static bool CompleteCurrentRunAndReturnToMenu()
        {
            return instance != null &&
                   instance.CompleteCurrentRun();
        }

        public static bool DeleteCurrentRunAndReturnToMenu()
        {
            return instance != null &&
                   instance.DeleteCurrentRun();
        }

        private bool CompleteCurrentRun()
        {
            return EndCurrentRun(true);
        }

        private bool DeleteCurrentRun()
        {
            return EndCurrentRun(false);
        }

        private bool EndCurrentRun(bool completedTrueEnding)
        {
            if (!runActive || completing)
            {
                return false;
            }

            completing = true;
            if (completedTrueEnding &&
                runData != null)
            {
                runData.trueEndingCompleted = true;
            }
            RunSaveService.Delete();
            PlayerController player = FindPlayer();
            if (player != null)
            {
                Scene scene = player.gameObject.scene;
                suppressAutoSave = true;
                ResetRunRuntimeState(scene);
                player.TryRestoreLoadedRunPosition(
                    null,
                    true,
                    runStartPlayerPosition);
                suppressAutoSave = false;
            }
            runActive = false;
            runData = null;
            completing = false;
            ReleaseNewRunEntryCover();
            GameFlowController.Instance?.ReturnToMainMenu();
            return true;
        }

        private bool HandleDeleteCurrentSaveRequested()
        {
            return DeleteCurrentRun();
        }

        private void ReleaseNewRunEntryCover()
        {
            if (newRunEntryCover == null)
            {
                return;
            }

            newRunEntryCover.Dispose();
            newRunEntryCover = null;
        }

        private static PlayerController FindPlayer()
        {
            return ProjectDiscovery.FindFirst<
                PlayerController>();
        }

        private static void Sort(List<string> values)
        {
            values?.Sort(StringComparer.Ordinal);
        }
    }
}
