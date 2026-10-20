using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Project.BlockFeatures;
using Project.CameraModes;
using Project.GameFlow;
using Project.InputAbstraction;
using Project.Items;
using Project.ObjectArtwork;
using Project.Player;
using Project.Saving;
using Project.WordNote;
using TapTap.TA.RgbGlitch;
using TMPro;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.UI;

namespace Project.Ending
{
    [Serializable]
    public sealed class FountainEndingSettings
    {
        [Header("角色与喷泉")]
        public string playerTag = "Player";
        [Min(.01f)] public float approachSpeedBlocksPerSecond = 2.5f;
        [Min(0f)] public float stopDistanceBlocks = 2f;

        [Header("相机")]
        [Min(0f)] public float cameraReleaseSeconds = .25f;

        [Header("调色盘")]
        public GameObject palettePrefab;
        [Min(1)] public int paletteCount = 4;
        public Vector3 paletteBodyLocalOffset;
        [Min(0f)] public float paletteBaseHeightBlocks = 1.15f;
        [Min(0f)] public float paletteSlotSpacingBlocks = .35f;
        [Min(0f)] public float paletteSlotArcHeightBlocks = .3f;
        [Min(0f)] public float paletteRiseSeconds = .32f;
        [Min(0f)] public float paletteRevealStaggerSeconds = .16f;
        [Min(0f)] public float paletteFormationHoldSeconds = .3f;
        [Min(0f)] public float paletteThrowSeconds = .7f;
        [Min(0f)] public float paletteEnterSeconds = .45f;
        [Min(0f)] public float paletteFountainTopInsetBlocks = .15f;
        [Min(0f)] public float paletteInsideDepthBlocks = .8f;
        [Range(.01f, 1f)] public float paletteInsideScale = .2f;

        [Header("字幕")]
        public TMP_FontAsset font;
        [Min(1f)] public float fontSize = 48f;
        [Min(0f)] public float paletteHoldBeforeDialogueSeconds = 1.5f;
        [Min(0f)] public float messageDisplaySeconds = 2f;
        [Min(0f)] public float messageGapSeconds = .5f;
        [Min(1f)] public float typewriterCharactersPerSecond = 15f;
        [Min(0f)] public float glitchTransitionSeconds = .35f;
        [Range(0f, 1f)] public float glitchMinAlpha = .35f;
        [Range(0f, 1f)] public float glitchMaxAlpha = 1f;
        [Min(0f)] public float glitchHorizontalJitter = 14f;
        [Min(0f)] public float glitchVerticalJitter = 3f;
        [Range(0f, 1f)] public float glitchColorSeparation = .85f;
        [TextArea] public string endingQuestion = "真的结束了?";
        [TextArea] public string endingAnswer = "是的，结束了，";

        [Header("结束")]
        public Color blackScreenColor = Color.black;
        [Min(0)] public int blackScreenSortingOrder = 32000;
        [Min(0f)] public float blackScreenHoldSeconds;
        [Min(0f)] public float menuGlitchHoldSeconds = .35f;
        [Min(0f)] public float menuGlitchFadeSeconds = .35f;
        [Min(.1f)] public float menuReturnTimeoutSeconds = 5f;
    }

    public sealed class FountainEndingTimelinePlayable :
        PlayableAsset
    {
        [SerializeField]
        private FountainEndingSettings settings =
            new FountainEndingSettings();

        public override Playable CreatePlayable(
            PlayableGraph graph,
            GameObject owner)
        {
            PlayableDirector resolvedDirector =
                EndingTimelineFrame.ResolveDirector(
                    graph,
                    owner);
            var behaviour =
                new FountainEndingTimelineBehaviour
                {
                    settings = settings,
                    director = resolvedDirector
                };
            return ScriptPlayable<
                    FountainEndingTimelineBehaviour>
                .Create(graph, behaviour);
        }
    }

    public sealed class FountainEndingTimelineBehaviour :
        PlayableBehaviour
    {
        public FountainEndingSettings settings;
        public PlayableDirector director;

        private FountainEndingRunner runner;

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
                "Fountain Ending Runner");
            runner =
                root.AddComponent<FountainEndingRunner>();
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

    internal sealed class FountainEndingRunner :
        MonoBehaviour
    {
        private FountainEndingSettings settings;
        private PlayableDirector director;
        private EndingTimelineFrame frame;
        private PlayerController player;
        private PlayerSpritePresentation playerVisual;
        private FountainArtwork fountain;
        private Renderer fountainRenderer;
        private CameraControlManager cameraManager;
        private TimelineCamRig cameraRig;
        private CameraViewModeRequestHandle viewModeHandle;
        private IDisposable playerControlLock;
        private WordNoteHandle message;
        private GameObject paletteRoot;
        private GameObject blackScreenRoot;
        private readonly List<Transform> palettes =
            new List<Transform>();
        private readonly List<Vector3> paletteScales =
            new List<Vector3>();
        private Vector3 playerCameraLocalOffset;
        private Vector3 currentCameraPosition;
        private Quaternion cameraRotation;
        private float baseOrthographicSize;
        private float baseFieldOfView;
        private bool running;
        private bool cleaned;
        private bool completingEnding;

        public void Begin(
            FountainEndingSettings valueSettings,
            PlayableDirector valueDirector)
        {
            settings = valueSettings ??
                       new FountainEndingSettings();
            director = valueDirector;
            running = true;
            StartCoroutine(Run());
        }

        public void Abort()
        {
            running = false;
            StopAllCoroutines();
            Cleanup();
        }

        private IEnumerator Run()
        {
            if (!TryInitialize())
            {
                Finish();
                yield break;
            }

            EmitTrueEndingAchievement();
            yield return DrivePlayerToFountain();
            yield return SpawnAndFeedPalettes();
            yield return WaitRealtime(
                settings.paletteHoldBeforeDialogueSeconds);
            yield return PlayDialogue(
                settings.endingQuestion,
                false);
            yield return WaitRealtime(
                settings.messageGapSeconds);
            yield return PlayDialogue(
                settings.endingAnswer,
                true);
            yield return BlackoutAndReturnToMenu();
            Finish();
        }

        private bool TryInitialize()
        {
            GameObject playerObject =
                FindTagged(settings.playerTag);
            player = playerObject != null
                ? playerObject.GetComponent<PlayerController>()
                : null;
            if (player == null)
            {
                Debug.LogError(
                    "[FountainEnding] 场景中没有找到玩家。");
                return false;
            }

            if (!EndingTimelineFrame.TryResolve(
                    director,
                    nameof(FountainEndingTimelinePlayable),
                    out frame))
            {
                return false;
            }

            fountain = FindFountain(
                frame.Anchor.position,
                player.gameObject.scene);
            if (fountain == null)
            {
                Debug.LogError(
                    "[FountainEnding] 场景中没有 FountainArtwork。");
                return false;
            }
            fountainRenderer =
                fountain.GetComponent<Renderer>();
            if (fountainRenderer == null)
            {
                Debug.LogError(
                    "[FountainEnding] 喷泉缺少 Renderer。");
                return false;
            }
            if (WordNoteUI.Instance == null)
            {
                Debug.LogError(
                    "[FountainEnding] 场景中缺少 WordNote_UI。");
                return false;
            }
            if (settings.font == null)
            {
                Debug.LogError(
                    "[FountainEnding] 未配置 Cubic_11 SDF 字体。");
                return false;
            }
            if (settings.palettePrefab == null)
            {
                Debug.LogError(
                    "[FountainEnding] 未配置调色盘预制体。");
                return false;
            }

            cameraManager =
                ProjectDiscovery.FindFirst<
                    CameraControlManager>();
            if (cameraManager == null &&
                Camera.main != null)
            {
                cameraManager =
                    Camera.main.GetComponent<
                        CameraControlManager>();
            }
            if (cameraManager == null)
            {
                Debug.LogError(
                    "[FountainEnding] 场景中缺少 CameraControlManager。");
                return false;
            }

            playerControlLock =
                player.AcquireControlLock(this);
            if (!player.BeginPresentationMotion())
            {
                Debug.LogError(
                    "[FountainEnding] 无法接管玩家 Rigidbody。");
                return false;
            }
            playerVisual =
                player.GetComponent<
                    PlayerSpritePresentation>();

            cameraRig =
                cameraManager.GetComponent<TimelineCamRig>() ??
                cameraManager.gameObject.AddComponent<
                    TimelineCamRig>();
            StopCompetingCameraDirectors();
            if (cameraRig.IsTimelineControlling ||
                cameraRig.isPlayingAnim)
            {
                cameraRig.ReleaseTimelineControl(false);
            }
            viewModeHandle = cameraRig.RequestViewMode(
                CameraViewMode.Side2D,
                CameraTransition.Immediate);
            cameraRig.Acquire(0f);
            cameraRig.SetReturnDurationForCurrentShot(
                Mathf.Max(
                    0f,
                    settings.cameraReleaseSeconds));
            CameraState state = cameraManager.CurrentState;
            currentCameraPosition = state.position;
            cameraRotation = state.rotation;
            baseOrthographicSize = Mathf.Max(
                .01f,
                state.orthographicSize);
            baseFieldOfView = state.fieldOfView;
            playerCameraLocalOffset =
                frame.ToLocalPoint(
                    currentCameraPosition) -
                frame.ToLocalPoint(player.transform.position);
            ApplyCamera(
                currentCameraPosition,
                1f);
            fountain.SetPlaying(true);
            return true;
        }

        private IEnumerator DrivePlayerToFountain()
        {
            Vector3 fountainCenter =
                fountainRenderer.bounds.center;
            float deltaX =
                player.transform.position.x -
                fountainCenter.x;
            float side = Mathf.Abs(deltaX) > .01f
                ? Mathf.Sign(deltaX)
                : player.FacingDirection < 0
                    ? -1f
                    : 1f;
            float cell = Mathf.Max(
                .01f,
                player.GridCellWorldSize);
            Vector3 worldTarget =
                new Vector3(
                    fountainCenter.x +
                    side *
                    Mathf.Max(
                        0f,
                        settings.stopDistanceBlocks) *
                    cell,
                    player.transform.position.y,
                    player.transform.position.z);
            Vector3 localStart =
                frame.ToLocalPoint(
                    player.transform.position);
            Vector3 localEnd =
                frame.ToLocalPoint(worldTarget);
            float distance =
                Vector3.Distance(
                    player.transform.position,
                    worldTarget);
            float duration =
                distance /
                Mathf.Max(
                    .01f,
                    settings
                        .approachSpeedBlocksPerSecond *
                    cell);
            player.FaceWorldPosition(
                fountainCenter);
            playerVisual?
                .SetPresentationAnimationState(
                    PlayerSpriteState.Run);

            Tween tween = DOTween.To(
                    () => 0f,
                    progress =>
                    {
                        Vector3 world =
                            frame.ToWorldPoint(
                                Vector3.Lerp(
                                    localStart,
                                    localEnd,
                                    progress));
                        player.SetPresentationPosition(
                            world);
                        UpdateFollowCamera();
                    },
                    1f,
                    Mathf.Max(.01f, duration))
                .SetEase(Ease.InOutSine)
                .SetUpdate(true)
                .SetTarget(this);
            yield return tween.WaitForCompletion();
            player.SetPresentationPosition(
                worldTarget);
            UpdateFollowCamera();
            playerVisual?
                .SetPresentationAnimationState(
                    PlayerSpriteState.Idle);
        }

        private IEnumerator SpawnAndFeedPalettes()
        {
            int count = Mathf.Max(
                1,
                settings.paletteCount);
            float cell = Mathf.Max(
                .01f,
                player.GridCellWorldSize);
            Vector3 localPlayer =
                frame.ToLocalPoint(
                    player.transform.position);
            paletteRoot =
                new GameObject("Fountain Ending Palettes");
            paletteRoot.SetActive(false);
            var slots = new List<Vector3>(count);
            for (int index = 0;
                 index < count;
                 index++)
            {
                GameObject instance =
                    Instantiate(
                        settings.palettePrefab,
                        paletteRoot.transform);
                instance.name =
                    "Ending Palette " +
                    (index + 1);
                DisablePaletteGameplay(instance);
                Vector3 baseScale =
                    instance.transform.localScale;
                instance.transform.position =
                    frame.ToWorldPoint(
                        localPlayer +
                        settings.paletteBodyLocalOffset);
                instance.transform.localScale =
                    Vector3.zero;
                palettes.Add(instance.transform);
                paletteScales.Add(baseScale);
                float centered =
                    index - (count - 1) * .5f;
                float normalized =
                    centered /
                    Mathf.Max(
                        1f,
                        (count - 1) * .5f);
                slots.Add(
                    frame.ToWorldPoint(
                        localPlayer +
                        new Vector3(
                            centered *
                            settings
                                .paletteSlotSpacingBlocks,
                            settings
                                .paletteBaseHeightBlocks +
                            (1f -
                             normalized *
                             normalized) *
                            settings
                                .paletteSlotArcHeightBlocks,
                            0f)));
            }
            paletteRoot.SetActive(true);

            for (int index = 0;
                 index < palettes.Count;
                 index++)
            {
                Transform item = palettes[index];
                Sequence sequence =
                    DOTween.Sequence()
                        .SetUpdate(true)
                        .SetTarget(item);
                sequence.Join(
                    item.DOMove(
                            slots[index],
                            settings.paletteRiseSeconds)
                        .SetEase(Ease.OutCubic));
                sequence.Join(
                    item.DOScale(
                            paletteScales[index],
                            settings.paletteRiseSeconds)
                        .SetEase(Ease.OutBack));
                yield return sequence.WaitForCompletion();
                yield return WaitRealtime(
                    settings
                        .paletteRevealStaggerSeconds);
            }

            yield return WaitRealtime(
                settings.paletteFormationHoldSeconds);
            Vector3 fountainTop =
                new Vector3(
                    fountainRenderer.bounds.center.x,
                    fountainRenderer.bounds.max.y,
                    fountainRenderer.bounds.center.z) +
                frame.ToWorldOffset(
                    Vector3.up *
                    -settings
                        .paletteFountainTopInsetBlocks *
                    cell);
            Vector3 fountainInside =
                new Vector3(
                    fountainRenderer.bounds.center.x,
                    fountainRenderer.bounds.center.y,
                    fountainRenderer.bounds.center.z) +
                frame.ToWorldOffset(
                    Vector3.down *
                    settings
                        .paletteInsideDepthBlocks *
                    cell);

            for (int index = 0;
                 index < palettes.Count;
                 index++)
            {
                Transform item = palettes[index];
                if (item == null)
                {
                    continue;
                }

                yield return item.DOMove(
                        fountainTop,
                        settings.paletteThrowSeconds)
                    .SetEase(Ease.InOutCubic)
                    .SetUpdate(true)
                    .WaitForCompletion();
                Sequence enter =
                    DOTween.Sequence()
                        .SetUpdate(true)
                        .SetTarget(item);
                enter.Join(
                    item.DOMove(
                            fountainInside,
                            settings.paletteEnterSeconds)
                        .SetEase(Ease.InCubic));
                enter.Join(
                    item.DOScale(
                            paletteScales[index] *
                            settings.paletteInsideScale,
                            settings.paletteEnterSeconds)
                        .SetEase(Ease.InCubic));
                yield return enter.WaitForCompletion();
                Destroy(item.gameObject);
            }
            palettes.Clear();
            paletteScales.Clear();
        }

        private IEnumerator PlayDialogue(
            string text,
            bool glitch)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                yield break;
            }

            message = WordNoteUI.Instance.RequestTimeline(
                text,
                BuildDialogueStyle(glitch));
            while (running &&
                   message != null &&
                   message.State !=
                   WordNoteMessageState.Finished)
            {
                yield return null;
            }
            message = null;
        }

        private WordNoteStyle BuildDialogueStyle(
            bool glitch)
        {
            var style = new WordNoteStyle
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
                    WordNoteDisplayMode.Timed,
                displayDuration = Mathf.Max(
                    0f,
                    settings.messageDisplaySeconds),
                useEnterAnimation = true,
                useExitAnimation = false,
                exitAnimation = WordNoteAnimation.None
            };
            if (glitch)
            {
                style.enterAnimation =
                    WordNoteAnimation.Glitch;
                style.enterMotion =
                    BuildGlitchMotion();
            }
            else
            {
                style.enterAnimation =
                    WordNoteAnimation.Typewriter;
                style.enterMotion =
                    BuildTypewriterMotion();
            }
            return style;
        }

        private WordNoteMotionSettings
            BuildTypewriterMotion()
        {
            return new WordNoteMotionSettings
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
            };
        }

        private WordNoteMotionSettings
            BuildGlitchMotion()
        {
            return new WordNoteMotionSettings
            {
                useCombinedChannels = true,
                useFade = false,
                useGlitch = true,
                glitchMinAlpha =
                    settings.glitchMinAlpha,
                glitchMaxAlpha =
                    settings.glitchMaxAlpha,
                glitchHorizontalJitter =
                    settings.glitchHorizontalJitter,
                glitchVerticalJitter =
                    settings.glitchVerticalJitter,
                glitchColorSeparation =
                    settings.glitchColorSeparation,
                duration = Mathf.Max(
                    0f,
                    settings.glitchTransitionSeconds),
                startAlpha = 1f,
                endAlpha = 1f,
                startScale = Vector3.one,
                endScale = Vector3.one
            };
        }

        private IEnumerator BlackoutAndReturnToMenu()
        {
            ShowBlackScreen();
            yield return null;

            ReleaseGameplayForMenu();
            bool completed =
                RunSessionCoordinator
                    .CompleteCurrentRunAndReturnToMenu();
            if (!completed)
            {
                Debug.LogWarning(
                    "[FountainEnding] 当前没有可清档的运行存档，仅返回开始界面。");
                GameFlowController.Instance?
                    .ReturnToMainMenu();
            }

            completingEnding = true;
            GameFlowController flow =
                GameFlowController.Instance;
            float deadline =
                Time.unscaledTime +
                Mathf.Max(
                    .1f,
                    settings.menuReturnTimeoutSeconds);
            while (flow != null &&
                   flow.ActiveSceneId !=
                   GameFlowSceneId.MainMenu &&
                   Time.unscaledTime < deadline)
            {
                yield return null;
            }
            yield return WaitRealtime(
                settings.blackScreenHoldSeconds);

            HideBlackScreenImmediate();
            RgbGlitchEffect.PlayDeath(0f);
            yield return null;
            yield return WaitRealtime(
                settings.menuGlitchHoldSeconds);
            RgbGlitchEffect.PlayRespawn(
                settings.menuGlitchFadeSeconds);
            yield return WaitRealtime(
                settings.menuGlitchFadeSeconds);
        }

        private void EmitTrueEndingAchievement()
        {
            if (player == null)
            {
                return;
            }

            GameplaySignalHub.Emit(
                AchievementSignalIds
                    .TrueEndingReached,
                player.gameObject);
        }

        private void ShowBlackScreen()
        {
            if (blackScreenRoot != null)
            {
                return;
            }

            blackScreenRoot = new GameObject(
                "Fountain Ending Black Screen",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(Image));
            Canvas canvas =
                blackScreenRoot.GetComponent<Canvas>();
            canvas.renderMode =
                RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder =
                settings.blackScreenSortingOrder;
            CanvasScaler scaler =
                blackScreenRoot.GetComponent<
                    CanvasScaler>();
            scaler.uiScaleMode =
                CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution =
                new Vector2(1920f, 1080f);
            scaler.screenMatchMode =
                CanvasScaler.ScreenMatchMode.Expand;
            RectTransform rect =
                blackScreenRoot.GetComponent<
                    RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Image image =
                blackScreenRoot.GetComponent<Image>();
            image.color = settings.blackScreenColor;
            image.raycastTarget = true;
        }

        private void HideBlackScreenImmediate()
        {
            if (blackScreenRoot == null)
            {
                return;
            }
            Destroy(blackScreenRoot);
            blackScreenRoot = null;
        }

        private void ReleaseGameplayForMenu()
        {
            ReleaseCamera();
            playerVisual?
                .ClearPresentationAnimationState();
            playerVisual = null;
            if (player != null)
            {
                player.EndPresentationMotion();
            }
            playerControlLock?.Dispose();
            playerControlLock = null;
        }

        private void UpdateFollowCamera()
        {
            Vector3 localPlayer =
                frame.ToLocalPoint(
                    player.transform.position);
            currentCameraPosition =
                frame.ToWorldPoint(
                    localPlayer +
                    playerCameraLocalOffset);
            ApplyCamera(
                currentCameraPosition,
                1f);
        }

        private void ApplyCamera(
            Vector3 position,
            float lensMultiplier)
        {
            currentCameraPosition = position;
            if (cameraRig == null)
            {
                return;
            }
            cameraRig.SetShotLens(
                baseOrthographicSize *
                Mathf.Max(0f, lensMultiplier),
                baseFieldOfView);
            cameraRig.SetShotTransform(
                position,
                cameraRotation);
        }

        private void StopCompetingCameraDirectors()
        {
            IReadOnlyList<PlayableDirector> directors =
                ProjectDiscovery.FindAll<
                    PlayableDirector>(true);
            for (int index = 0;
                 index < directors.Count;
                 index++)
            {
                PlayableDirector candidate =
                    directors[index];
                if (candidate == null ||
                    candidate == director ||
                    candidate.state !=
                    PlayState.Playing ||
                    candidate.playableAsset is not
                        TimelineAsset timeline)
                {
                    continue;
                }

                bool hasCameraTrack = false;
                foreach (TrackAsset track in
                         timeline.GetOutputTracks())
                {
                    if (track is CameraTimelineTrack)
                    {
                        hasCameraTrack = true;
                        break;
                    }
                }
                if (hasCameraTrack)
                {
                    candidate.Stop();
                }
            }
        }

        private void DisablePaletteGameplay(
            GameObject instance)
        {
            Collider[] colliders =
                instance.GetComponentsInChildren<
                    Collider>(true);
            for (int index = 0;
                 index < colliders.Length;
                 index++)
            {
                colliders[index].enabled = false;
            }
            Rigidbody[] bodies =
                instance.GetComponentsInChildren<
                    Rigidbody>(true);
            for (int index = 0;
                 index < bodies.Length;
                 index++)
            {
                bodies[index].detectCollisions = false;
                bodies[index].useGravity = false;
                bodies[index].isKinematic = true;
            }
            CollectiblePickup[] pickups =
                instance.GetComponentsInChildren<
                    CollectiblePickup>(true);
            for (int index = 0;
                 index < pickups.Length;
                 index++)
            {
                pickups[index]
                    .SetExcludedFromProgress(true);
            }
            MonoBehaviour[] behaviours =
                instance.GetComponentsInChildren<
                    MonoBehaviour>(true);
            for (int index = 0;
                 index < behaviours.Length;
                 index++)
            {
                behaviours[index].enabled = false;
            }
            MechanismArtworkPresentation
                .PrepareCinematicClone(
                    instance,
                    "BossTop",
                    32760);
        }

        private void Finish()
        {
            if (cleaned)
            {
                return;
            }
            running = false;
            Cleanup();
            director?.Stop();
        }

        private void Cleanup()
        {
            if (cleaned)
            {
                return;
            }
            cleaned = true;
            running = false;

            if (message != null)
            {
                message.ForceDisappear();
                message.Release(false);
                message = null;
            }
            ReleaseCamera();
            playerVisual?
                .ClearPresentationAnimationState();
            playerVisual = null;
            if (player != null)
            {
                player.EndPresentationMotion();
            }
            playerControlLock?.Dispose();
            playerControlLock = null;

            if (paletteRoot != null)
            {
                DOTween.Kill(paletteRoot);
                Destroy(paletteRoot);
                paletteRoot = null;
            }
            palettes.Clear();
            paletteScales.Clear();
            if (blackScreenRoot != null)
            {
                Destroy(blackScreenRoot);
                blackScreenRoot = null;
            }
            DOTween.Kill(this);
        }

        private void ReleaseCamera()
        {
            if (viewModeHandle.IsValid)
            {
                if (cameraRig != null)
                {
                    cameraRig.ReleaseViewMode(
                        viewModeHandle);
                }
                else
                {
                    viewModeHandle.Release(true);
                }
            }
            viewModeHandle = default;
            if (cameraRig != null &&
                cameraRig.IsTimelineControlling)
            {
                cameraRig.ReleaseTimelineControl(
                    !completingEnding);
            }
            cameraRig = null;
            cameraManager = null;
        }

        private IEnumerator WaitRealtime(
            float seconds)
        {
            if (seconds > 0f)
            {
                yield return new WaitForSecondsRealtime(
                    seconds);
            }
        }

        private static FountainArtwork FindFountain(
            Vector3 nearPosition,
            UnityEngine.SceneManagement.Scene scene)
        {
            IReadOnlyList<FountainArtwork> fountains =
                ProjectDiscovery.FindAll<
                    FountainArtwork>();
            FountainArtwork closest = null;
            float closestDistance =
                float.PositiveInfinity;
            for (int index = 0;
                 index < fountains.Count;
                 index++)
            {
                FountainArtwork candidate =
                    fountains[index];
                if (candidate == null ||
                    candidate.gameObject.scene != scene)
                {
                    continue;
                }
                float distance =
                    Vector3.SqrMagnitude(
                        candidate.transform.position -
                        nearPosition);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closest = candidate;
                }
            }
            return closest;
        }

        private static GameObject FindTagged(
            string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                return null;
            }
            try
            {
                return GameObject.FindGameObjectWithTag(
                    tag);
            }
            catch (UnityException)
            {
                return null;
            }
        }

        private void OnDestroy()
        {
            Cleanup();
        }
    }

    internal readonly struct EndingTimelineFrame
    {
        private readonly Transform anchor;

        private EndingTimelineFrame(Transform value)
        {
            anchor = value;
        }

        public Transform Anchor => anchor;

        public Vector3 ToWorldPoint(
            Vector3 localPoint)
        {
            return anchor != null
                ? anchor.TransformPoint(localPoint)
                : localPoint;
        }

        public Vector3 ToWorldOffset(
            Vector3 localOffset)
        {
            return anchor != null
                ? anchor.TransformVector(localOffset)
                : localOffset;
        }

        public Vector3 ToLocalPoint(
            Vector3 worldPoint)
        {
            return anchor != null
                ? anchor.InverseTransformPoint(worldPoint)
                : worldPoint;
        }

        public static PlayableDirector ResolveDirector(
            PlayableGraph graph,
            GameObject owner)
        {
            PlayableDirector director = null;
            if (graph.IsValid())
            {
                director =
                    graph.GetResolver() as
                    PlayableDirector;
            }
            if (director == null && owner != null)
            {
                director =
                    owner.GetComponentInParent<
                        PlayableDirector>();
            }
            return director;
        }

        public static bool TryResolve(
            PlayableDirector director,
            string ownerName,
            out EndingTimelineFrame frame)
        {
            frame = default;
            if (director == null)
            {
                Debug.LogError(
                    $"[{ownerName}] 找不到 owning PlayableDirector，演出已停止。");
                return false;
            }

            ContactTimelineTrigger trigger =
                director.GetComponent<
                    ContactTimelineTrigger>();
            if (trigger == null)
            {
                trigger =
                    director.GetComponentInParent<
                        ContactTimelineTrigger>();
            }
            Transform anchor = trigger != null
                ? trigger.transform
                : director.transform;
            if (anchor == null)
            {
                Debug.LogError(
                    $"[{ownerName}] Timeline 没有可用的表演坐标系。",
                    director);
                return false;
            }
            frame = new EndingTimelineFrame(anchor);
            return true;
        }
    }
}
