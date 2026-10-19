using Project.CameraModes;
using Project.Items;
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
        private bool suppressDefeatPresentation;
        private PlayableDirector standalonePlayerThinkingDirector;

        public PlayableDirector Director => director;
        public TimelineAsset DefeatTimeline => defeatTimeline;
        public TimelineAsset PlayerThinkingTimeline =>
            playerThinkingTimeline;
        public bool IsPlaying =>
            director != null &&
            director.state == PlayState.Playing;
        public bool IsDefeatPresentationSuppressed =>
            suppressDefeatPresentation;

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
            if (suppressDefeatPresentation)
            {
                return;
            }
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

        public void SetDefeatPresentationSuppressed(
            bool value)
        {
            suppressDefeatPresentation = value;
            if (value)
            {
                defeatPresentationActive = false;
            }
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
            CollectibleProgressSnapshot progress = CollectibleProgressService.GetWorldProgress();
            PlayerThinkingTimelinePlayable.SetCollectibleProgress(progress.Collected, progress.Total);
            director?.Stop();
            PlayStandalonePlayerThinking();
            switchingToPlayerThinking = false;
        }

        public bool PlayPlayerThinkingAfterDefeat()
        {
            if (playerThinkingTimeline == null)
            {
                Debug.LogWarning(
                    "[BossDefeatTimeline] 未配置玩家沉思 Timeline，无法进入结局。",
                    this);
                return false;
            }

            defeatPresentationActive = false;
            switchingToPlayerThinking = true;
            CollectibleProgressSnapshot progress =
                CollectibleProgressService.GetWorldProgress();
            PlayerThinkingTimelinePlayable.SetCollectibleProgress(
                progress.Collected,
                progress.Total);
            bool started = PlayStandalonePlayerThinking();
            switchingToPlayerThinking = false;
            return started;
        }

        private bool PlayStandalonePlayerThinking()
        {
            if (standalonePlayerThinkingDirector != null)
            {
                standalonePlayerThinkingDirector.Stop();
                if (standalonePlayerThinkingDirector.gameObject != null)
                {
                    Destroy(
                        standalonePlayerThinkingDirector.gameObject);
                }
                standalonePlayerThinkingDirector = null;
            }

            GameObject directorObject = new GameObject(
                "Boss Post Death Director");
            standalonePlayerThinkingDirector =
                directorObject.AddComponent<PlayableDirector>();
            standalonePlayerThinkingDirector.playableAsset =
                playerThinkingTimeline;
            standalonePlayerThinkingDirector.time = 0d;
            BossPostDeathDirectorLifetime lifetime =
                directorObject.AddComponent<
                    BossPostDeathDirectorLifetime>();
            lifetime.Bind(standalonePlayerThinkingDirector);
            standalonePlayerThinkingDirector.Play();
            return standalonePlayerThinkingDirector.state ==
                   PlayState.Playing;
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
