using System.Collections;
using Project.Player;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Project.ColorBlocks
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class ColorKeyPickup : MonoBehaviour
    {
        [SerializeField] private string colorTypeId = "red";
        [SerializeField] private PlayableDirector cameraDirector;
        [SerializeField] private bool hideOnCollect = true;
        [SerializeField, Range(0.5f, 1f)] private float revealAtTimelineProgress = .98f;
        [SerializeField, Min(.01f)] private float revealDuration = 1.5f;
        [SerializeField, Min(1f)] private float orthographicZoomOut = 1.6f;
        [SerializeField, Min(0f)] private float cameraReturnDuration = .85f;
        private bool collected;
        private TimelineCamRig activeRig;
        private float originalOrthographicSize;
        private float originalFieldOfView;

        public string ColorTypeId => colorTypeId;

        private void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
            cameraDirector = GetComponent<PlayableDirector>();
        }

        private void OnDisable()
        {
            ColorRuntimeService manager = ColorRuntimeService.Existing;
            if (Application.isPlaying && collected && manager != null &&
                !manager.IsUnlocked(colorTypeId))
                manager.Unlock(colorTypeId);
        }

        private void OnTriggerEnter(Collider other)
        {
            var player = other.GetComponentInParent<PlayerController>();
            if (player == null) return;
            OnInteractionPlayerEntered(player.gameObject);
        }

        /// <summary>
        /// 颜色钥匙的收集入口。钥匙预制体同时挂有通用物体交互来源，
        /// 因此物体图可以把“玩家进入”连接到这个方法；保留碰撞回退入口
        /// 以兼容尚未完成交互定义绑定的旧场景。
        /// </summary>
        public void OnInteractionPlayerEntered(GameObject actor)
        {
            var player = actor != null
                ? actor.GetComponentInParent<PlayerController>()
                : null;
            if (collected || player == null) return;
            if (ColorRuntimeService.Instance.IsUnlocked(colorTypeId)) return;
            collected = true;
            var collider = GetComponent<Collider>();
            if (collider != null) collider.enabled = false;
            if (hideOnCollect)
            {
                foreach (var renderer in GetComponentsInChildren<Renderer>()) renderer.enabled = false;
            }
            StartCoroutine(CollectWithCamera(player));
        }

        // 这些入口让钥匙定义可以安全复用物体图的默认接触链；钥匙本身
        // 没有需要在停留/离开或非玩家碰触时追加的行为。
        public void OnInteractionPlayerLeft(GameObject actor) { }
        public void OnInteractionPlayerStay(GameObject actor) { }
        public void OnInteractionObjectTouched(GameObject other) { }
        public void OnInteractionObjectStay(GameObject other) { }

        private IEnumerator CollectWithCamera(PlayerController player)
        {
            bool hasCameraShot = PlayCameraCutscene(player);
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

            bool unlocked = ColorRuntimeService.Instance.Unlock(colorTypeId);
            if (unlocked)
                HSVColorFadeManager.Instance.SetColorFaded(colorTypeId, false, revealDuration);
            float revealTimeout = Time.unscaledTime + revealDuration + 1f;
            while (unlocked &&
                   HSVColorFadeManager.Instance.GetSaturation(colorTypeId) < .995f &&
                   Time.unscaledTime < revealTimeout)
                yield return null;

            if (hasCameraShot && cameraDirector != null)
                cameraDirector.Stop(); // The Timeline rig returns via CameraControlManager.
            Destroy(gameObject);
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

#if UNITY_EDITOR
        public void EditorConfigure(string typeId, PlayableDirector director)
        {
            colorTypeId = typeId;
            cameraDirector = director;
        }
#endif
    }
}
