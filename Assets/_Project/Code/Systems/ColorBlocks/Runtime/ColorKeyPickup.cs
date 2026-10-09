using System;
using System.Collections;
using Project.Player;
using Project.Items;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Project.ColorBlocks
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class ColorKeyPickup : DropItemBase
    {
        [SerializeField] private string colorTypeId = "red";
        [SerializeField] private PlayableDirector cameraDirector;
        [SerializeField] private bool hideOnCollect = true;
        [SerializeField, Range(0.5f, 1f)] private float revealAtTimelineProgress = .98f;
        [SerializeField, Min(.01f)] private float revealDuration = 1.5f;
        [SerializeField, Min(1f)] private float orthographicZoomOut = 1.6f;
        [SerializeField, Min(0f)] private float cameraReturnDuration = .85f;
        private PlayerController collectingPlayer;
        private TimelineCamRig activeRig;
        private float originalOrthographicSize;
        private float originalFieldOfView;
        private float nextPlayerSearchTime;
        private IDisposable controlLockHandle;

        public string ColorTypeId => colorTypeId;

        private void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
            cameraDirector = GetComponent<PlayableDirector>();
        }

        protected override void OnDisable()
        {
            ReleasePlayerControlLock();
            base.OnDisable();
            ColorRuntimeService manager = ColorRuntimeService.Existing;
            if (Application.isPlaying && CollectionRequested && manager != null &&
                !manager.IsUnlocked(colorTypeId))
                manager.Unlock(colorTypeId);
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

            if (ColorRuntimeService.Instance.IsUnlocked(colorTypeId))
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
        /// 颜色钥匙的直接收集入口。钥匙只负责解锁颜色，不经过交互图。
        /// </summary>
        public void OnInteractionPlayerEntered(GameObject actor)
        {
            var player = actor != null
                ? actor.GetComponentInParent<PlayerController>()
                : null;
            if (CollectionRequested || IsAttracting || player == null) return;
            if (ColorRuntimeService.Instance.IsUnlocked(colorTypeId)) return;

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
            ReleasePlayerControlLock();
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

            bool unlocked = ColorRuntimeService.Instance.Unlock(colorTypeId);
            if (unlocked)
            {
                HSVColorFadeManager.Instance.SetColorFaded(
                    colorTypeId,
                    false,
                    revealDuration);
                Debug.Log(
                    $"[ColorBlocks] 钥匙触发入口已解锁 {colorTypeId}：材质恢复/水体显现已开始。",
                    this);
            }
            else
            {
                Debug.LogWarning(
                    $"[ColorBlocks] 钥匙触发入口执行，但 {colorTypeId} 未完成 Unlock。",
                    this);
            }
        }

        protected override void OnCollectionFinished()
        {
            ReleasePlayerControlLock();
        }

        private IEnumerator CollectWithCamera(Action onComplete)
        {
            // 解锁已在触发入口同步完成。镜头 Timeline 不能阻塞颜色恢复；
            // 这里只等待渐显收尾。
            bool unlocked = ColorRuntimeService.Instance.IsUnlocked(colorTypeId);
            bool hasCameraShot = PlayCameraCutscene(collectingPlayer);
            if (hasCameraShot)
            {
                double duration = cameraDirector.playableAsset.duration;
                double revealTime = Mathf.Max(0f,
                    (float)duration * revealAtTimelineProgress - .03f);
                float timeout = Time.unscaledTime + (float)duration + 1f;
                while (cameraDirector != null &&
                       cameraDirector.state == PlayState.Playing &&
                       cameraDirector.time < revealTime &&
                       Time.unscaledTime < timeout)
                {
                    UpdateCameraLens((float)(cameraDirector.time / duration));
                    yield return null;
                }
                if (cameraDirector != null && cameraDirector.state == PlayState.Playing)
                {
                    UpdateCameraLens(1f);
                    cameraDirector.Pause();
                }
            }

            float revealTimeout = Time.unscaledTime + HSVColorFadeManager.Instance.GetEffectiveDuration(revealDuration) + 1f;
            while (unlocked &&
                   HSVColorFadeManager.Instance.GetSaturation(colorTypeId) < .995f &&
                   Time.unscaledTime < revealTimeout)
                yield return null;

            if (hasCameraShot && cameraDirector != null)
                cameraDirector.Stop(); // The Timeline rig returns via CameraControlManager.
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
            if (rig.isPlayingAnim)
            {
                Debug.LogWarning("[ColorBlocks] 相机演出正在播放，颜色钥匙跳过重叠运镜。", this);
                return false;
            }
            rig.target = player.transform;
            bool hasCameraTrack = false;
            if (cameraDirector.playableAsset is TimelineAsset timeline)
                foreach (var track in timeline.GetOutputTracks())
                    if (track is CameraTimelineTrack)
                    {
                        cameraDirector.SetGenericBinding(track, rig);
                        hasCameraTrack = true;
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
            cameraDirector.Play();
            return true;
        }

        private void UpdateCameraLens(float progress)
        {
            if (activeRig == null) return;
            float eased = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress));
            activeRig.SetShotLens(
                Mathf.Lerp(originalOrthographicSize,
                    originalOrthographicSize * orthographicZoomOut, eased),
                originalFieldOfView);
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

#if UNITY_EDITOR
        public void EditorConfigure(string typeId, PlayableDirector director)
        {
            colorTypeId = typeId;
            cameraDirector = director;
        }
#endif
    }
}
