using Project.CameraModes;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Project.BossLogic
{
    [DisallowMultipleComponent]
    public sealed class BossDefeatTimelinePresenter : MonoBehaviour
    {
        [SerializeField] private PlayableDirector director;
        [SerializeField] private TimelineAsset defeatTimeline;
        [SerializeField] private TimelineAsset playerThinkingTimeline;
        [SerializeField]
        [Tooltip("只接受本物体 BossController 发出的死亡请求。")]
        private bool onlyPlayForThisBoss = true;

        private BossController boss;
        private bool defeatPresentationActive;
        private bool switchingToPlayerThinking;

        public PlayableDirector Director => director;
        public TimelineAsset DefeatTimeline => defeatTimeline;
        public TimelineAsset PlayerThinkingTimeline =>
            playerThinkingTimeline;
        public bool IsPlaying =>
            director != null &&
            director.state == PlayState.Playing;

        private void Awake()
        {
            boss = GetComponent<BossController>();
            EnsureDirector();
        }

        /// <summary>
        /// Bound to BossController's defeat presentation event. The event only
        /// fires from EnterDeath, so external Destroy does not trigger it.
        /// </summary>
        public void PlayDefeatPresentation(
            BossController defeatedBoss)
        {
            if (onlyPlayForThisBoss &&
                boss != null &&
                defeatedBoss != null &&
                defeatedBoss != boss)
            {
                return;
            }

            EnsureDirector();
            if (director == null)
            {
                Debug.LogWarning(
                    "[BossDefeatTimeline] 缺少 PlayableDirector，无法播放死亡演出。",
                    this);
                return;
            }

            if (defeatTimeline != null &&
                director.playableAsset != defeatTimeline)
            {
                director.playableAsset = defeatTimeline;
            }

            if (director.playableAsset == null)
            {
                Debug.LogWarning(
                    "[BossDefeatTimeline] 未配置 Boss 死亡 Timeline。",
                    this);
                return;
            }

            CameraControlManager cameraManager = null;
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                cameraManager =
                    mainCamera.GetComponent<
                        CameraControlManager>();
            }

            cameraManager ??=
                ProjectDiscovery
                    .FindFirst<CameraControlManager>();
            if (cameraManager == null)
            {
                Debug.LogWarning(
                    "[BossDefeatTimeline] 场景中缺少 CameraControlManager，死亡镜头不会播放。",
                    this);
            }
            else if (cameraManager.GetComponent<TimelineCamRig>() == null)
            {
                cameraManager.gameObject.AddComponent<TimelineCamRig>();
            }

            if (director.state == PlayState.Playing)
            {
                director.Stop();
            }

            defeatPresentationActive = true;
            switchingToPlayerThinking = false;
            director.time = 0d;
            director.Play();
        }

        private void Update()
        {
            if (!defeatPresentationActive ||
                switchingToPlayerThinking ||
                director == null ||
                director.playableAsset != defeatTimeline)
            {
                return;
            }

            double duration = director.duration;
            if (duration <= 0d ||
                director.time < duration - .0001d)
            {
                return;
            }

            PlayPlayerThinking();
        }

        private void PlayPlayerThinking()
        {
            if (playerThinkingTimeline == null)
            {
                defeatPresentationActive = false;
                return;
            }

            switchingToPlayerThinking = true;
            defeatPresentationActive = false;
            director.Stop();
            director.playableAsset =
                playerThinkingTimeline;
            director.time = 0d;
            director.Play();
            switchingToPlayerThinking = false;
        }

        public void StopDefeatPresentation()
        {
            defeatPresentationActive = false;
            switchingToPlayerThinking = false;
            if (director != null)
            {
                director.Stop();
                director.time = 0d;
            }
        }

        private void OnDisable()
        {
            StopDefeatPresentation();
        }

        private void EnsureDirector()
        {
            if (director == null)
            {
                director = GetComponent<PlayableDirector>();
            }
        }
    }
}
