using System;
using System.Collections;
using Project.BlockFeatures;
using Project.GameFlow;
using Project.InputAbstraction;
using Project.Items;
using Project.Player;
using Project.WordNote;
using TMPro;
using UnityEngine;
using UnityEngine.Playables;

namespace Project.BossLogic
{
    [Serializable]
    public sealed class EnterBossTimelineSettings
    {
        [Header("收集门槛")]
        [Range(0f, 1f)] public float minimumCollectibleRatio = .3f;

        [Header("字幕")]
        public TMP_FontAsset font;
        [Min(1f)] public float fontSize = 72f;
        [Min(1f)] public float typewriterCharactersPerSecond = 18f;
        [Min(0f)] public float lineHoldSeconds = 1f;
        [TextArea]
        public string insufficientLine =
            "我身上的调色板好像还不足以激活喷泉....";
        [TextArea]
        public string returnLine =
            "还是回去再收集一些吧";

        [Header("返回 Start")]
        public string startDestinationId = "Start";
        public Vector2 blackScreenCenter =
            new Vector2(.5f, .5f);
        [Min(.01f)] public float blackCoverSpeed = .72f;
        [Min(0f)] public float blackSoftness;
        [Min(0f)] public float fullyBlackHoldSeconds = .1f;
    }

    public sealed class EnterBossTimelinePlayable :
        PlayableAsset
    {
        [SerializeField]
        private EnterBossTimelineSettings settings =
            new EnterBossTimelineSettings();

        public static bool HasSufficientCollectibles(
            int current,
            int maximum,
            float threshold = .3f)
        {
            return new CollectibleProgressSnapshot(
                    current,
                    maximum)
                .MeetsRatio(threshold);
        }

        public override Playable CreatePlayable(
            PlayableGraph graph,
            GameObject owner)
        {
            PlayableDirector director =
                ResolveDirector(graph, owner);
            var behaviour =
                new EnterBossTimelineBehaviour
                {
                    settings = settings,
                    director = director
                };
            return ScriptPlayable<
                    EnterBossTimelineBehaviour>
                .Create(graph, behaviour);
        }

        private static PlayableDirector ResolveDirector(
            PlayableGraph graph,
            GameObject owner)
        {
            PlayableDirector director =
                graph.IsValid()
                    ? graph.GetResolver() as
                        PlayableDirector
                    : null;
            if (director == null && owner != null)
            {
                director =
                    owner.GetComponentInParent<
                        PlayableDirector>();
            }
            return director;
        }
    }

    public sealed class EnterBossTimelineBehaviour :
        PlayableBehaviour
    {
        public EnterBossTimelineSettings settings;
        public PlayableDirector director;

        private EnterBossTimelineRunner runner;

        public override void OnBehaviourPlay(
            Playable playable,
            FrameData info)
        {
            CleanupRunner();
            if (!Application.isPlaying)
            {
                return;
            }

            GameObject root = new GameObject(
                "Enter Boss Timeline Runner");
            runner =
                root.AddComponent<
                    EnterBossTimelineRunner>();
            runner.Begin(settings, director);
        }

        public override void OnBehaviourPause(
            Playable playable,
            FrameData info)
        {
            CleanupRunner();
        }

        public override void OnGraphStop(
            Playable playable)
        {
            CleanupRunner();
        }

        public override void OnPlayableDestroy(
            Playable playable)
        {
            CleanupRunner();
        }

        private void CleanupRunner()
        {
            if (runner == null)
            {
                return;
            }

            runner.Abort();
            if (runner.gameObject != null)
            {
                UnityEngine.Object.Destroy(
                    runner.gameObject);
            }
            runner = null;
        }
    }

    internal sealed class EnterBossTimelineRunner :
        MonoBehaviour
    {
        private EnterBossTimelineSettings settings;
        private PlayableDirector director;
        private PlayerController player;
        private IDisposable controlLock;
        private WordNoteHandle message;
        private bool running;
        private bool cleaned;

        public void Begin(
            EnterBossTimelineSettings valueSettings,
            PlayableDirector valueDirector)
        {
            settings = valueSettings ??
                       new EnterBossTimelineSettings();
            director = valueDirector;
            running = true;
            StartCoroutine(Run());
        }

        public void Abort()
        {
            running = false;
            StopAllCoroutines();
            ReleaseState();
        }

        private IEnumerator Run()
        {
            player = FindTaggedPlayer();
            if (player == null)
            {
                Fail("场景中没有找到玩家。");
                yield break;
            }

            CollectibleProgressSnapshot progress =
                CollectibleProgressService
                    .GetWorldProgress();
            Debug.Log(
                "[EnterBoss] 收集进度 " +
                progress.Collected +
                "/" +
                progress.Total +
                "，门槛 " +
                (settings.minimumCollectibleRatio *
                 100f)
                .ToString("0") +
                "%。",
                director);
            if (EnterBossTimelinePlayable
                    .HasSufficientCollectibles(
                        progress.Collected,
                        progress.Total,
                        settings
                            .minimumCollectibleRatio))
            {
                Debug.Log(
                    "[EnterBoss] 收集进度达标，直接放行。",
                    director);
                Finish();
                yield break;
            }

            if (WordNoteUI.Instance == null)
            {
                Fail("场景中缺少 WordNote_UI。");
                yield break;
            }
            if (settings.font == null)
            {
                Fail("未配置 Cubic_11 SDF 字体。");
                yield break;
            }
            GameSceneLoader loader =
                GameSceneLoader.Instance;
            if (loader == null)
            {
                Fail("场景中缺少 GameSceneLoader。");
                yield break;
            }

            controlLock =
                player.AcquireControlLock(this);
            yield return PlayLine(
                settings.insufficientLine);
            if (!running)
            {
                yield break;
            }
            yield return PlayLine(
                settings.returnLine);
            if (!running)
            {
                yield break;
            }
            yield return CoverTeleportReveal(
                loader);
            Finish();
        }

        private IEnumerator PlayLine(string text)
        {
            if (!running ||
                string.IsNullOrWhiteSpace(text))
            {
                yield break;
            }

            message =
                WordNoteUI.Instance.RequestTimeline(
                    text,
                    BuildStyle());
            if (message == null)
            {
                yield break;
            }

            while (running &&
                   message != null &&
                   message.State !=
                   WordNoteMessageState.Showing &&
                   message.State !=
                   WordNoteMessageState.Finished)
            {
                yield return null;
            }
            if (settings.lineHoldSeconds > 0f)
            {
                yield return
                    new WaitForSecondsRealtime(
                        settings
                            .lineHoldSeconds);
            }
            if (message != null)
            {
                message.ForceDisappear();
                message.Release();
                message = null;
            }
        }

        private IEnumerator CoverTeleportReveal(
            GameSceneLoader loader)
        {
            GameTransitionOverlay overlay =
                loader.TransitionOverlay;
            overlay.ResetPresentation();
            yield return overlay.CoverFromCenterAsync(
                settings.blackScreenCenter,
                settings.blackCoverSpeed,
                settings.blackSoftness);

            bool teleported = false;
            if (player != null &&
                TeleportAnchorFeature.TryGetAnchor(
                    settings.startDestinationId,
                    out TeleportAnchorFeature start) &&
                start != null &&
                start.gameObject.scene ==
                player.gameObject.scene)
            {
                teleported =
                    player.TryTeleportToStoryAnchor(
                        start);
            }
            bool returnedHome = teleported &&
                                string.Equals(
                                    settings
                                        .startDestinationId,
                                    "Start",
                                    StringComparison.Ordinal);
            if (!teleported)
            {
                Debug.LogWarning(
                    "[EnterBoss] 无法返回传送锚点 " +
                    settings.startDestinationId +
                    "；本轮进度保持不变。",
                    director);
            }

            if (settings
                    .fullyBlackHoldSeconds > 0f)
            {
                yield return
                    new WaitForSecondsRealtime(
                        settings
                            .fullyBlackHoldSeconds);
            }
            yield return overlay.RevealFromAsync(
                player != null
                    ? player.transform
                    : null,
                settings.blackScreenCenter,
                settings.blackCoverSpeed,
                settings.blackSoftness);
            if (returnedHome && player != null)
            {
                GameplaySignalHub.Emit(
                    AchievementSignalIds
                        .LowCollectibleReturnHome,
                    player.gameObject);
            }
            overlay.ResetPresentation();
            yield return null;
        }

        private WordNoteStyle BuildStyle()
        {
            return new WordNoteStyle
            {
                font = settings.font,
                fontSize = Mathf.Max(
                    1f,
                    settings.fontSize),
                bold = true,
                richText = true,
                fontColor = WordNoteColor.White,
                useOutline = true,
                outlineColor = WordNoteColor.Black,
                textAlignment =
                    WordNoteTextAlignment.Center,
                position = WordNotePosition.Bottom,
                participateInStackLayout = true,
                displayMode =
                    WordNoteDisplayMode.Persistent,
                useEnterAnimation = true,
                enterAnimation =
                    WordNoteAnimation.Typewriter,
                useExitAnimation = false,
                exitAnimation =
                    WordNoteAnimation.None,
                enterMotion =
                    new WordNoteMotionSettings
                    {
                        useCombinedChannels = true,
                        useFade = false,
                        useTypewriter = true,
                        typewriterCharactersPerSecond =
                            Mathf.Max(
                                1f,
                                settings
                                    .typewriterCharactersPerSecond),
                        duration = .01f,
                        startAlpha = 1f,
                        endAlpha = 1f,
                        startScale = Vector3.one,
                        endScale = Vector3.one
                    }
            };
        }

        private void Finish()
        {
            if (cleaned)
            {
                return;
            }
            running = false;
            ReleaseState();
            director?.Stop();
        }

        private void ReleaseState()
        {
            if (cleaned)
            {
                return;
            }
            cleaned = true;
            if (message != null)
            {
                message.ForceDisappear();
                message.Release();
                message = null;
            }
            controlLock?.Dispose();
            controlLock = null;
            if (GameSceneLoader.Existing != null)
            {
                GameSceneLoader.Existing
                    .TransitionOverlay
                    .ResetPresentation();
            }
        }

        private void Fail(string reason)
        {
            Debug.LogError(
                "[EnterBoss] " + reason,
                director);
            Finish();
        }

        private void OnDestroy()
        {
            ReleaseState();
        }

        private static PlayerController
            FindTaggedPlayer()
        {
            try
            {
                GameObject player =
                    GameObject
                        .FindGameObjectWithTag(
                            "Player");
                return player != null
                    ? player.GetComponent<
                        PlayerController>()
                    : null;
            }
            catch (UnityException)
            {
                return null;
            }
        }
    }
}
