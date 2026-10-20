using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Project.Audio;
using Project.Player;
using Project.Items;
using Project.BlockFeatures;
using Project.LevelEditor;
using Project.ObjectArtwork;
using Project.Rooms;
using Project.WorldVisibility;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;

namespace Project.ColorBlocks
{
    [DisallowMultipleComponent, DefaultExecutionOrder(1200)]
    [RequireComponent(typeof(Collider))]
    public sealed class ColorKeyPickup : DropItemBase
    {
        private const float DefaultCameraOrthographicSize = 15f;

        public static event Action<ColorKeyPickup> Collected;
        public bool IsCollectionVisualComplete { get; private set; }
        public bool IsPersistentlyConsumed { get; private set; }
        [SerializeField] private string colorTypeId = "red";
        [SerializeField] private PlayableDirector cameraDirector;
        [SerializeField] private bool hideOnCollect = true;
        [SerializeField, Min(.01f)] private float revealDuration = 1.5f;
        [SerializeField, Min(.01f)] private float cameraOrthographicSize =
            DefaultCameraOrthographicSize;
        [SerializeField, HideInInspector] private float orthographicZoomOut = 1.5f;
        [SerializeField, Min(0f)] private float cameraTrackDuration = .5f;
        [SerializeField, Min(.01f)] private float cameraMoveDuration = 3f;
        [SerializeField, Min(0f)] private float cameraHoldDuration = 2f;
        [SerializeField, Min(0f)] private float cameraReturnDuration = .85f;
        [SerializeField, Min(0f)] private float visibilityFadeOutDuration = .2f;
        [SerializeField, Min(0f)] private float visibilityFadeInDuration = .35f;
        private PlayerController collectingPlayer;
        private TimelineCamRig activeRig;
        private float originalOrthographicSize;
        private float originalFieldOfView;
        private float nextPlayerSearchTime;
        private IDisposable controlLockHandle;
        private IDisposable visibilitySuppression;
        private IGridCellSizeProvider hoverGrid;
        private MonoBehaviour hoverGridOwner;
        public ColorKeyUnlockResult UnlockResult { get; private set; }

        public string ColorTypeId => colorTypeId;
        protected override bool UsesIdleHover => true;
        protected override float IdleHoverPixelWorldSize => GridArtworkMetrics.PixelWorldSize(
            hoverGridOwner != null ? hoverGrid.CellWorldSize : GridCellSizeUtility.FallbackCellWorldSize);

        private void OnTransformParentChanged() => CacheHoverGrid();
        private void CacheHoverGrid()
        {
            hoverGrid=null; hoverGridOwner=null;
            foreach (var component in GetComponentsInParent<MonoBehaviour>(true))
                if (component is IGridCellSizeProvider provider && provider.CellWorldSize>0f &&
                    !float.IsNaN(provider.CellWorldSize))
                { hoverGrid=provider; hoverGridOwner=component; break; }
        }

        private void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
            cameraDirector = GetComponent<PlayableDirector>();
        }

        protected override void OnDisable()
        {
            ReleasePlayerControlLock();
            ReleaseVisibilitySuppression();
            base.OnDisable();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (IsPersistentlyConsumed)
            {
                ApplyConsumedVisual();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            var player = other.GetComponentInParent<PlayerController>();
            if (player == null) return;
            if (IsAttracting)
            {
                CompleteAttraction();
                return;
            }

            OnInteractionPlayerEntered(player.gameObject);
        }

        private void Update()
        {
            if (!Application.isPlaying ||
                CollectionRequested ||
                AttractionRequested)
            {
                return;
            }

            if (collectingPlayer == null ||
                !collectingPlayer.isActiveAndEnabled)
            {
                if (Time.unscaledTime < nextPlayerSearchTime)
                {
                    return;
                }

                nextPlayerSearchTime = Time.unscaledTime + .5f;
                collectingPlayer =
                    ProjectDiscovery.FindFirst<PlayerController>();
            }

            if (collectingPlayer == null ||
                !collectingPlayer.IsWithinPickupSenseRange(
                    transform.position))
            {
                return;
            }

            if (!TryBeginAttraction(collectingPlayer.gameObject))
            {
                collectingPlayer = null;
            }
        }

        /// <summary>
        /// 同色钥匙依次解锁场景颜色与玩家能力，不经过交互图。
        /// </summary>
        public void OnInteractionPlayerEntered(GameObject actor)
        {
            var player = actor != null
                ? actor.GetComponentInParent<PlayerController>()
                : null;
            if (CollectionRequested || IsAttracting || player == null) return;

            collectingPlayer = player;
            if (!TryBeginCollection(player.gameObject))
            {
                collectingPlayer = null;
                return;
            }
        }

        protected override void ResetItemState()
        {
            base.ResetItemState();
            CacheHoverGrid();
            IsCollectionVisualComplete = false;
            UnlockResult = ColorKeyUnlockResult.None;
            ReleasePlayerControlLock();
            ReleaseVisibilitySuppression();
            collectingPlayer = null;
            nextPlayerSearchTime = 0f;
            var collider = GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = true;
                collider.isTrigger = true;
            }

            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                renderer.enabled = true;
            }
        }

        protected override void PlayCollectAnimation(
            Action onComplete)
        {
            bool reportedAnimationComplete = false;
            bool reportedCameraComplete = false;

            void CompleteVisual()
            {
                if (reportedAnimationComplete)
                {
                    return;
                }

                reportedAnimationComplete = true;
                IsCollectionVisualComplete = hideOnCollect;
                if (hideOnCollect)
                    foreach (var renderer in GetComponentsInChildren<Renderer>(true))
                        renderer.enabled = false;
                TryFinish();
            }

            void CompleteCamera()
            {
                if (reportedCameraComplete)
                {
                    return;
                }

                reportedCameraComplete = true;
                TryFinish();
            }

            void TryFinish()
            {
                if (reportedAnimationComplete &&
                    reportedCameraComplete)
                {
                    onComplete?.Invoke();
                }
            }

            base.PlayCollectAnimation(CompleteVisual);
            if (collectingPlayer == null)
            {
                CompleteCamera();
                return;
            }

            StartCoroutine(CollectWithCamera(CompleteCamera));
        }

        protected override void OnCollectionStarted()
        {
            AcquirePlayerControlLock(collectingPlayer);
            var collider = GetComponent<Collider>();
            if (collider != null) collider.enabled = false;

            UnlockResult = ColorRuntimeService.Instance.CollectColorKey(colorTypeId);
            if (TryGetStableInstanceId(out _))
            {
                IsPersistentlyConsumed = true;
            }
            if (UnlockResult != ColorKeyUnlockResult.None)
            {
                AudioCuePlayer.Play(AudioCueIds.PaintAcquire, .9f);
            }
            if (UnlockResult == ColorKeyUnlockResult.SceneColor)
            {
                ColorAppearanceManager.Instance.Play(
                    colorTypeId,
                    true,
                    revealDuration);
            }
            Collected?.Invoke(this);
        }

        protected override void OnCollectionFinished()
        {
            ReleasePlayerControlLock();
        }

        private IEnumerator CollectWithCamera(Action onComplete)
        {
            // 解锁已在触发入口同步完成。镜头 Timeline 不能阻塞颜色恢复；
            // 这里只等待渐显收尾。
            bool unlocked = UnlockResult == ColorKeyUnlockResult.SceneColor;
            bool hasCameraShot = PlayCameraCutscene(collectingPlayer);
            if (hasCameraShot)
            {
                double duration = cameraDirector.playableAsset.duration;
                float totalDuration = Mathf.Max(.01f, (float)duration);
                float trackDuration = Mathf.Clamp(
                    cameraTrackDuration,
                    0f,
                    totalDuration - .01f);
                float reverseStart = Mathf.Clamp(
                    trackDuration +
                    cameraMoveDuration +
                    cameraHoldDuration,
                    trackDuration + .01f,
                    totalDuration);
                bool releasedAtReverse = false;
                float timeout = Time.unscaledTime + (float)duration + 1f;
                while (cameraDirector != null &&
                       cameraDirector.state == PlayState.Playing &&
                       cameraDirector.time < duration &&
                       Time.unscaledTime < timeout)
                {
                    float timelineTime = (float)cameraDirector.time;
                    UpdateCameraLens(EvaluateCameraShotProgress(
                        timelineTime,
                        totalDuration));
                    if (!releasedAtReverse &&
                        timelineTime >= reverseStart - .0001f)
                    {
                        ReleasePlayerControlLock();
                        releasedAtReverse = true;
                    }

                    yield return null;
                }

                if (cameraDirector != null)
                {
                    UpdateCameraLens(0f);
                    cameraDirector.Stop();
                    if (!releasedAtReverse)
                    {
                        ReleasePlayerControlLock();
                    }
                }
            }
            ReleaseVisibilitySuppression();

            // The color reveal may finish during the camera return; it must not
            // keep player input locked.
            float revealTimeout = Time.unscaledTime + ColorAppearanceManager.Instance.GetEffectiveDuration(revealDuration) + 1f;
            while (unlocked &&
                   !ColorAppearanceManager.Instance.IsComplete(colorTypeId) &&
                   Time.unscaledTime < revealTimeout)
                yield return null;

            collectingPlayer = null;
            onComplete?.Invoke();
        }

        private bool PlayCameraCutscene(PlayerController player)
        {
            if (cameraDirector == null) cameraDirector = GetComponent<PlayableDirector>();
            if (cameraDirector == null || cameraDirector.playableAsset == null)
            {
                Debug.LogWarning("[ColorBlocks] 颜色钥匙未配置 Timeline；颜色仍会解锁。", this);
                return false;
            }
            var rig = ProjectDiscovery.FindFirst<TimelineCamRig>();
            if (rig == null)
            {
                var manager = ProjectDiscovery.FindFirst<
                    Project.CameraModes.CameraControlManager>();
                if (manager != null) rig = manager.gameObject.AddComponent<TimelineCamRig>();
            }
            if (rig == null)
            {
                Debug.LogWarning("[ColorBlocks] 未找到 TimelineCamRig，演出轨无法接管相机。", this);
                return false;
            }
            if (rig.IsTimelineControlling)
            {
                Debug.LogWarning("[ColorBlocks] 相机演出正在播放，颜色钥匙跳过重叠运镜。", this);
                return false;
            }

            if (rig.isPlayingAnim)
            {
                rig.ReleaseTimelineControl(false);
            }
            rig.target = player.transform;
            bool hasCameraTrack = false;
            if (cameraDirector.playableAsset is TimelineAsset timeline)
            {
                foreach (var track in timeline.GetOutputTracks())
                {
                    if (track is CameraTimelineTrack)
                    {
                        cameraDirector.SetGenericBinding(track, rig);
                        hasCameraTrack = true;
                    }
                }
            }
            if (!hasCameraTrack)
            {
                Debug.LogWarning("[ColorBlocks] 颜色钥匙 Timeline 缺少相机轨。", this);
                return false;
            }
            activeRig = rig;
            var state = rig.Manager.CurrentState;
            originalOrthographicSize = state.orthographicSize;
            originalFieldOfView = state.fieldOfView;
            rig.SetShotLens(originalOrthographicSize, originalFieldOfView);
            rig.SetReturnDurationForCurrentShot(cameraReturnDuration);
            WorldVisibility2D visibility =
                WorldVisibility2D.FindForScene(gameObject.scene);
            if (visibility != null)
            {
                ReleaseVisibilitySuppression();
                visibilitySuppression = visibility.SuppressOcclusion(
                    this,
                    visibilityFadeOutDuration,
                    visibilityFadeInDuration);
            }
            cameraDirector.Play();
            return true;
        }

        private void UpdateCameraLens(float progress)
        {
            if (activeRig == null) return;
            float eased = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress));
            if (cameraOrthographicSize <= .01f)
            {
                cameraOrthographicSize = orthographicZoomOut > .01f
                    ? Mathf.Max(
                        DefaultCameraOrthographicSize,
                        originalOrthographicSize * orthographicZoomOut)
                    : DefaultCameraOrthographicSize;
            }
            activeRig.SetShotLens(
                Mathf.Lerp(originalOrthographicSize,
                    cameraOrthographicSize, eased),
                originalFieldOfView);
        }

        private float EvaluateCameraShotProgress(
            float timelineTime,
            float totalDuration)
        {
            float forwardEnd = Mathf.Clamp(
                cameraTrackDuration + cameraMoveDuration,
                .01f,
                totalDuration);
            float reverseStart = Mathf.Clamp(
                cameraTrackDuration +
                cameraMoveDuration +
                cameraHoldDuration,
                forwardEnd,
                totalDuration);
            float motionStart = Mathf.Clamp(
                cameraTrackDuration,
                0f,
                totalDuration - .0001f);
            if (timelineTime <= forwardEnd)
            {
                return Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.Max(0f, timelineTime - motionStart) /
                    Mathf.Max(
                        .0001f,
                        forwardEnd - motionStart));
            }

            if (timelineTime <= reverseStart)
            {
                return 1f;
            }

            float reverseDuration = Mathf.Max(
                .0001f,
                totalDuration - reverseStart);
            return 1f - Mathf.SmoothStep(
                0f,
                1f,
                (timelineTime - reverseStart) / reverseDuration);
        }

        private void AcquirePlayerControlLock(PlayerController player)
        {
            ReleasePlayerControlLock();
            if (player is not IPlayerControlLockTarget lockTarget)
            {
                return;
            }

            controlLockHandle = lockTarget.AcquireControlLock(this);
        }

        private void ReleasePlayerControlLock()
        {
            controlLockHandle?.Dispose();
            controlLockHandle = null;
        }

        private void ReleaseVisibilitySuppression()
        {
            visibilitySuppression?.Dispose();
            visibilitySuppression = null;
        }

        public bool TryGetStableInstanceId(
            out string instanceId)
        {
            instanceId = null;
            LevelEditorPlacedBlock placed =
                GetComponent<LevelEditorPlacedBlock>();
            string scenePath = gameObject.scene.path;
            if (placed == null ||
                string.IsNullOrWhiteSpace(
                    placed.PlanningBoxId) ||
                string.IsNullOrWhiteSpace(scenePath))
            {
                return false;
            }

            Vector2Int cell = placed.PlanningLocalCell;
            scenePath = scenePath.Replace('\\', '/');
            string box = placed.PlanningBoxId;
            instanceId = string.Format(
                CultureInfo.InvariantCulture,
                "colorkey:{0}:{1}:{2}:{3}:{4}:{5}",
                scenePath.Length,
                scenePath,
                box.Length,
                box,
                cell.x,
                cell.y);
            return true;
        }

        public static void CopyConsumedStableIds(
            Scene scene,
            ICollection<string> destination)
        {
            if (destination == null ||
                !scene.IsValid() ||
                !scene.isLoaded)
            {
                return;
            }

            foreach (GameObject root in
                     scene.GetRootGameObjects())
            {
                foreach (ColorKeyPickup key in
                         root.GetComponentsInChildren<
                             ColorKeyPickup>(true))
                {
                    if (key.IsPersistentlyConsumed &&
                        key.TryGetStableInstanceId(
                            out string id))
                    {
                        destination.Add(id);
                    }
                }
            }
        }

        public static void RestoreConsumedStableIds(
            Scene scene,
            IEnumerable<string> consumedIds)
        {
            if (consumedIds == null ||
                !scene.IsValid() ||
                !scene.isLoaded)
            {
                return;
            }

            var ids = new HashSet<string>(
                consumedIds,
                StringComparer.Ordinal);
            foreach (GameObject root in
                     scene.GetRootGameObjects())
            {
                foreach (ColorKeyPickup key in
                         root.GetComponentsInChildren<
                             ColorKeyPickup>(true))
                {
                    if (key.TryGetStableInstanceId(
                            out string id) &&
                        ids.Contains(id))
                    {
                        key.IsPersistentlyConsumed = true;
                        key.ApplyConsumedVisual();
                    }
                }
            }
        }

        public static void ResetPersistentConsumption(
            Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return;
            }

            foreach (GameObject root in
                     scene.GetRootGameObjects())
            {
                foreach (ColorKeyPickup key in
                         root.GetComponentsInChildren<
                             ColorKeyPickup>(true))
                {
                    key.IsPersistentlyConsumed = false;
                    if (!key.gameObject.activeSelf)
                    {
                        key.gameObject.SetActive(true);
                    }
                    key.ResetForReuse();
                }
            }
        }

        private void ApplyConsumedVisual()
        {
            StopItemAnimations();
            ReleasePlayerControlLock();
            Collider collider = GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }
            foreach (Renderer renderer in
                     GetComponentsInChildren<Renderer>(true))
            {
                renderer.enabled = false;
            }
            if (!RoomResetObject.HideForReaction(gameObject))
            {
                gameObject.SetActive(false);
            }
        }

        protected override void RecycleItem()
        {
            ApplyConsumedVisual();
        }

#if UNITY_EDITOR
        public void EditorConfigure(string typeId, PlayableDirector director)
        {
            colorTypeId = typeId;
            cameraDirector = director;
        }
#endif
    }
}
