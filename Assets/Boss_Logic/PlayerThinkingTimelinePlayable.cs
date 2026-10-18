using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Project.BlockFeatures;
using Project.GameFlow;
using Project.Items;
using Project.Pickups;
using Project.Rooms;
using Project.WordNote;
using TMPro;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Project.BossLogic
{
    public sealed class PlayerThinkingTimelinePlayable :
        PlayableAsset
    {
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private GameObject collectiblePrefab;
        [SerializeField, Min(1f)] private float fontSize = 72f;
        [SerializeField, Min(1f)]
        private float typewriterCharactersPerSecond = 18f;
        [SerializeField, Min(0f)] private float lineHoldSeconds = 1.2f;
        [SerializeField, Min(0f)] private float lineGapSeconds = 1f;
        [SerializeField, Min(0f)]
        private float branchTwoFinalGapSeconds = 2f;
        [SerializeField, Min(0f)]
        private float collectibleRevealStagger = .35f;
        [SerializeField, Min(.05f)]
        private float collectibleRiseDuration = .65f;
        [SerializeField, Min(0f)]
        private float collectiblePrepareSeconds = 2f;
        [SerializeField, Min(0f)]
        private float collectibleBobHeight = .08f;
        [SerializeField, Min(.05f)]
        private float collectibleBobDuration = .8f;
        [SerializeField, Min(0f)]
        private float blackCoverSpeed = .72f;
        [SerializeField, Min(0f)]
        private float blackSoftness;
        [SerializeField, Min(0f)]
        private float fullyBlackHoldSeconds = 1f;
        [SerializeField, Min(0f)]
        private float sufficientThreshold = .6f;
        [SerializeField] private string startDestinationId = "Start";
        [SerializeField] private string endDestinationId = "End";
        [SerializeField]
        private string[] openingLines =
        {
            "刚才那是他吸收了我的所有钥匙？",
            "......",
            "这下难办了......",
            "不过幸好，一路上我收集了一些画板...让我看看他们能否奏效"
        };
        [SerializeField]
        private string[] insufficientLines =
        {
            "......",
            "可恶......",
            "还是达不到吗......",
            "还是回去继续收集画板吧"
        };
        [SerializeField]
        private string[] sufficientLines =
        {
            "终于",
            "要结束了吗......"
        };

        public static void SetCollectibleProgress(
            int current,
            int maximum)
        {
            PlayerThinkingBehaviour.SetCollectibleProgress(
                current,
                maximum);
        }

        public static void ClearCollectibleProgress()
        {
            PlayerThinkingBehaviour.ClearCollectibleProgress();
        }

        internal static PlayableDirector FindDirector(
            PlayableGraph graph,
            GameObject owner)
        {
            PlayableDirector resolved =
                graph.GetResolver() as PlayableDirector;
            if (resolved != null)
            {
                return resolved;
            }
            if (owner != null)
            {
                resolved =
                    owner.GetComponent<PlayableDirector>();
                if (resolved != null)
                {
                    return resolved;
                }
            }

            IReadOnlyList<PlayableDirector> directors =
                ProjectDiscovery.FindAll<PlayableDirector>(
                    true);
            for (int index = 0;
                 index < directors.Count;
                 index++)
            {
                PlayableDirector candidate =
                    directors[index];
                if (candidate == null ||
                    candidate.playableAsset is not
                        TimelineAsset timeline)
                {
                    continue;
                }
                foreach (TrackAsset track in
                         timeline.GetOutputTracks())
                {
                    foreach (TimelineClip clip in
                             track.GetClips())
                    {
                        if (clip.asset is
                            PlayerThinkingTimelinePlayable)
                        {
                            return candidate;
                        }
                    }
                }
            }
            return null;
        }

        public override Playable CreatePlayable(
            PlayableGraph graph,
            GameObject owner)
        {
            PlayableDirector resolvedDirector =
                FindDirector(graph, owner);
            if (resolvedDirector == null && owner != null)
            {
                resolvedDirector =
                    owner.GetComponent<PlayableDirector>();
            }
            PlayerThinkingBehaviour behaviour =
                new PlayerThinkingBehaviour
                {
                    font = font,
                    collectiblePrefab = collectiblePrefab,
                    fontSize = fontSize,
                    typewriterCharactersPerSecond =
                        typewriterCharactersPerSecond,
                    lineHoldSeconds = lineHoldSeconds,
                    lineGapSeconds = lineGapSeconds,
                    branchTwoFinalGapSeconds =
                        branchTwoFinalGapSeconds,
                    collectibleRevealStagger =
                        collectibleRevealStagger,
                    collectibleRiseDuration =
                        collectibleRiseDuration,
                    collectiblePrepareSeconds =
                        collectiblePrepareSeconds,
                    collectibleBobHeight =
                        collectibleBobHeight,
                    collectibleBobDuration =
                        collectibleBobDuration,
                    blackCoverSpeed = blackCoverSpeed,
                    blackSoftness = blackSoftness,
                    fullyBlackHoldSeconds =
                        fullyBlackHoldSeconds,
                    sufficientThreshold =
                        sufficientThreshold,
                    startDestinationId =
                        startDestinationId,
                    endDestinationId = endDestinationId,
                    openingLines = openingLines,
                    insufficientLines =
                        insufficientLines,
                    sufficientLines = sufficientLines,
                    director = resolvedDirector
                };
            return ScriptPlayable<PlayerThinkingBehaviour>
                .Create(graph, behaviour);
        }
    }

    public sealed class PlayerThinkingBehaviour :
        PlayableBehaviour
    {
        private static int currentCollectibleCount;
        private static int maximumCollectibleCount;
        private static bool collectibleProgressConfigured;

        public TMP_FontAsset font;
        public GameObject collectiblePrefab;
        public float fontSize = 72f;
        public float typewriterCharactersPerSecond = 18f;
        public float lineHoldSeconds = 1.2f;
        public float lineGapSeconds = 1f;
        public float branchTwoFinalGapSeconds = 2f;
        public float collectibleRevealStagger = .35f;
        public float collectibleRiseDuration = .65f;
        public float collectiblePrepareSeconds = 2f;
        public float collectibleBobHeight = .08f;
        public float collectibleBobDuration = .8f;
        public float blackCoverSpeed = .72f;
        public float blackSoftness;
        public float fullyBlackHoldSeconds = 1f;
        public float sufficientThreshold = .6f;
        public string startDestinationId = "Start";
        public string endDestinationId = "End";
        public string[] openingLines;
        public string[] insufficientLines;
        public string[] sufficientLines;
        public PlayableDirector director;

        private GameSceneLoader runner;
        private Coroutine routine;
        private WordNoteHandle message;
        private GameObject collectibleRoot;
        private readonly List<Transform> collectibles =
            new List<Transform>();
        private bool running;

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCollectibleProgress()
        {
            currentCollectibleCount = 0;
            maximumCollectibleCount = 0;
            collectibleProgressConfigured = false;
        }

        public static void SetCollectibleProgress(
            int current,
            int maximum)
        {
            currentCollectibleCount =
                Mathf.Max(0, current);
            maximumCollectibleCount =
                Mathf.Max(0, maximum);
            collectibleProgressConfigured = true;
        }

        public static void ClearCollectibleProgress()
        {
            currentCollectibleCount = 0;
            maximumCollectibleCount = 0;
            collectibleProgressConfigured = false;
        }

        public override void OnBehaviourPlay(
            Playable playable,
            FrameData info)
        {
            Cleanup();
            if (!Application.isPlaying)
            {
                return;
            }

            running = true;
            runner = GameSceneLoader.Instance;
            routine = runner.StartCoroutine(
                RunSequence());
        }

        public override void OnBehaviourPause(
            Playable playable,
            FrameData info)
        {
            Cleanup();
        }

        public override void OnGraphStop(
            Playable playable)
        {
            Cleanup();
        }

        public override void OnPlayableDestroy(
            Playable playable)
        {
            Cleanup();
        }

        private IEnumerator RunSequence()
        {
            yield return new WaitForSeconds(.15f);
            yield return PlayLines(
                openingLines,
                lineGapSeconds);
            if (!running)
            {
                yield break;
            }

            if (HasEnoughCollectibles())
            {
                yield return PlaySufficientEnding();
            }
            else
            {
                yield return PlayInsufficientEnding();
            }
            if (!running)
            {
                yield break;
            }

            yield return null;
            director?.Stop();
        }

        private IEnumerator PlayInsufficientEnding()
        {
            yield return PlayLines(
                insufficientLines,
                lineGapSeconds);
            yield return FadeTeleportReveal(
                startDestinationId);
        }

        private IEnumerator PlaySufficientEnding()
        {
            yield return SpawnCollectibles();
            if (collectiblePrepareSeconds > 0f)
            {
                yield return new WaitForSeconds(
                    collectiblePrepareSeconds);
            }
            yield return PlayLines(
                sufficientLines,
                branchTwoFinalGapSeconds,
                true);
            yield return FadeTeleportReveal(
                endDestinationId);
        }

        private IEnumerator PlayLines(
            string[] lines,
            float finalGapSeconds,
            bool finalGapOnly = false)
        {
            if (lines == null)
            {
                yield break;
            }
            for (int index = 0;
                 index < lines.Length;
                 index++)
            {
                if (!running)
                {
                    yield break;
                }
                bool last = index == lines.Length - 1;
                float gap = finalGapOnly && !last
                    ? lineGapSeconds
                    : last
                        ? finalGapSeconds
                        : lineGapSeconds;
                yield return PlayLine(lines[index], gap);
            }
        }

        private IEnumerator PlayLine(
            string text,
            float gapAfter)
        {
            WordNoteUI ui = WordNoteUI.Instance;
            if (ui == null)
            {
                yield return new WaitForSeconds(
                    lineHoldSeconds + gapAfter);
                yield break;
            }

            message = ui.RequestTimeline(
                text,
                BuildStyle());
            while (running &&
                   message != null &&
                   message.State !=
                   WordNoteMessageState.Showing &&
                   message.State !=
                   WordNoteMessageState.Finished)
            {
                yield return null;
            }
            if (lineHoldSeconds > 0f)
            {
                yield return new WaitForSeconds(
                    lineHoldSeconds);
            }
            if (message != null)
            {
                message.ForceDisappear();
                message.Release();
                message = null;
            }
            if (gapAfter > 0f)
            {
                yield return new WaitForSeconds(gapAfter);
            }
        }

        private IEnumerator FadeTeleportReveal(
            string destinationId)
        {
            GameSceneLoader loader = runner != null
                ? runner
                : GameSceneLoader.Instance;
            GameTransitionOverlay overlay =
                loader.TransitionOverlay;
            Transform player = FindPlayer();
            overlay.ResetPresentation();
            yield return overlay.CoverFromCenterAsync(
                player,
                new Vector2(.5f, .5f),
                blackCoverSpeed,
                blackSoftness);
            DestroyCollectibles();
            if (fullyBlackHoldSeconds > 0f)
            {
                yield return new WaitForSecondsRealtime(
                    fullyBlackHoldSeconds);
            }

            if (player == null ||
                !TeleportAnchorFeature.TeleportObject(
                    player.gameObject,
                    destinationId))
            {
                Debug.LogWarning(
                    "[PlayerThinking] 传送失败，目标 ID=" +
                    destinationId);
            }
            yield return null;
            yield return overlay.RevealFromAsync(
                player,
                new Vector2(.5f, .5f),
                blackCoverSpeed,
                blackSoftness);
            overlay.ResetPresentation();
            yield return null;
        }

        private IEnumerator SpawnCollectibles()
        {
            if (collectiblePrefab == null)
            {
                yield break;
            }
            Transform player = FindPlayer();
            if (player == null)
            {
                yield break;
            }

            collectibleRoot =
                new GameObject("PlayerThinking收集品");
            collectibleRoot.transform.SetParent(
                player,
                false);
            collectibleRoot.SetActive(false);
            const int count = 6;
            var slots = new List<Vector3>(count);
            var baseScales =
                new List<Vector3>(count);
            for (int index = 0;
                 index < count;
                 index++)
            {
                GameObject instance =
                    Object.Instantiate(
                        collectiblePrefab,
                        collectibleRoot.transform);
                DisableCollectibleGameplay(instance);
                instance.transform.localPosition =
                    new Vector3(0f, .4f, 0f);
                instance.transform.localScale =
                    Vector3.zero;
                collectibles.Add(instance.transform);
                baseScales.Add(
                    Vector3.one);
                float centered =
                    index - (count - 1) * .5f;
                float normalized =
                    centered /
                    Mathf.Max(1f, (count - 1) * .5f);
                slots.Add(
                    new Vector3(
                        centered * .36f,
                        1.75f +
                        (1f - normalized * normalized) *
                        .14f,
                        0f));
            }
            collectibleRoot.SetActive(true);

            for (int index = 0;
                 index < collectibles.Count;
                 index++)
            {
                Transform item = collectibles[index];
                Sequence sequence = DOTween.Sequence()
                    .SetUpdate(true)
                    .SetTarget(item);
                sequence.Join(item.DOLocalMove(
                        slots[index],
                        collectibleRiseDuration)
                    .SetEase(Ease.OutCubic));
                sequence.Join(item.DOScale(
                        baseScales[index],
                        collectibleRiseDuration)
                    .SetEase(Ease.OutBack));
                yield return sequence.WaitForCompletion();
                if (collectibleRevealStagger > 0f)
                {
                    yield return new WaitForSeconds(
                        collectibleRevealStagger);
                }
            }

            for (int index = 0;
                 index < collectibles.Count;
                 index++)
            {
                Transform item = collectibles[index];
                float baseY = slots[index].y;
                item.DOLocalMoveY(
                        baseY + collectibleBobHeight,
                        collectibleBobDuration)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetEase(Ease.InOutSine)
                    .SetUpdate(true)
                    .SetTarget(item);
                item.DOScale(
                        baseScales[index] * 1.035f,
                        collectibleBobDuration)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetEase(Ease.InOutSine)
                    .SetUpdate(true)
                    .SetTarget(item);
            }
        }

        private WordNoteStyle BuildStyle()
        {
            var style = new WordNoteStyle
            {
                font = font,
                fontSize = fontSize,
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
                    WordNoteAnimation.None
            };
            style.enterMotion.useCombinedChannels = true;
            style.enterMotion.useTypewriter = true;
            style.enterMotion.typewriterCharactersPerSecond =
                typewriterCharactersPerSecond;
            style.enterMotion.useFade = true;
            return style;
        }

        private bool HasEnoughCollectibles()
        {
            if (!collectibleProgressConfigured ||
                maximumCollectibleCount <= 0)
            {
                return true;
            }
            return currentCollectibleCount /
                   (float)maximumCollectibleCount >=
                   sufficientThreshold;
        }

        private static Transform FindPlayer()
        {
            try
            {
                GameObject player =
                    GameObject.FindGameObjectWithTag(
                        "Player");
                return player != null
                    ? player.transform
                    : null;
            }
            catch (UnityException)
            {
                return null;
            }
        }

        private static void DisableCollectibleGameplay(
            GameObject instance)
        {
            Collider[] colliders =
                instance.GetComponentsInChildren<Collider>(
                    true);
            for (int index = 0;
                 index < colliders.Length;
                 index++)
            {
                colliders[index].enabled = false;
            }

            Rigidbody[] bodies =
                instance.GetComponentsInChildren<Rigidbody>(
                    true);
            for (int index = 0;
                 index < bodies.Length;
                 index++)
            {
                bodies[index].detectCollisions = false;
                bodies[index].isKinematic = true;
            }

            CollectiblePickup[] pickups =
                instance.GetComponentsInChildren<
                    CollectiblePickup>(true);
            for (int index = 0;
                 index < pickups.Length;
                 index++)
            {
                pickups[index].enabled = false;
            }

            PickupFlightAnimator[] flights =
                instance.GetComponentsInChildren<
                    PickupFlightAnimator>(true);
            for (int index = 0;
                 index < flights.Length;
                 index++)
            {
                flights[index].enabled = false;
            }

            RoomResetObject[] resets =
                instance.GetComponentsInChildren<
                    RoomResetObject>(true);
            for (int index = 0;
                 index < resets.Length;
                 index++)
            {
                resets[index].enabled = false;
            }
        }

        private void DestroyCollectibles()
        {
            for (int index = 0;
                 index < collectibles.Count;
                 index++)
            {
                Transform item = collectibles[index];
                if (item != null)
                {
                    DOTween.Kill(item);
                }
            }
            collectibles.Clear();
            if (collectibleRoot != null)
            {
                Object.Destroy(collectibleRoot);
                collectibleRoot = null;
            }
        }

        private void Cleanup()
        {
            running = false;
            if (runner != null &&
                routine != null)
            {
                runner.StopCoroutine(routine);
            }
            routine = null;
            runner = null;

            if (message != null)
            {
                message.ForceDisappear();
                message.Release();
                message = null;
            }
            DestroyCollectibles();
            if (GameSceneLoader.Existing != null)
            {
                GameSceneLoader.Existing
                    .TransitionOverlay
                    .ResetPresentation();
            }
        }
    }
}
