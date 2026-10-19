using System.Collections.Generic;
using Project.BlockFeatures;
using Project.CameraModes;
using Project.Player;
using Project.WordNote;
using TMPro;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Project.BossLogic
{
    public sealed class BossAppearCinematicPlayable :
        PlayableAsset
    {
        [SerializeField] private BossController bossPrefab;
        [SerializeField] private TMP_FontAsset font;
        [SerializeField, TextArea]
        private string warningLine =
            "别急着走啊，你以为我能让你如愿?";
        [SerializeField, TextArea]
        private string questionLine = "？";
        [SerializeField, TextArea]
        private string alarmLine = "不好！";
        [SerializeField, Min(0f)]
        private float introFreezeSeconds = 1f;
        [SerializeField, Min(0f)]
        private float warningDisplaySeconds = 1.8f;
        [SerializeField, Min(0f)]
        private float questionDisplaySeconds = 1f;
        [SerializeField, Min(0f)]
        private float alarmDisplaySeconds = 1f;
        [SerializeField, Min(1f)]
        private float messageFontSize = 72f;
        [SerializeField, Min(1f)]
        private float typewriterCharactersPerSecond = 12f;
        [SerializeField, Min(0f)]
        private float glitchTransitionSeconds = .48f;
        [SerializeField, Range(0f, 1f)]
        private float glitchMinAlpha = .35f;
        [SerializeField, Range(0f, 1f)]
        private float glitchMaxAlpha = 1f;
        [SerializeField, Min(0f)]
        private float glitchHorizontalJitter = 18f;
        [SerializeField, Min(0f)]
        private float glitchVerticalJitter = 4f;
        [SerializeField, Range(0f, 1f)]
        private float glitchColorSeparation = .85f;
        [SerializeField]
        [Tooltip("appear 初始镜头相对玩家位置的固定偏移。")]
        private Vector3 playerCameraOffset =
            new Vector3(0f, 0f, -10f);
        [SerializeField]
        [Tooltip("相对玩家当前位置生成 Boss：X-7、Y+13。")]
        private Vector3 bossSpawnOffsetFromPlayer =
            new Vector3(-7f, 13f, 0f);
        [SerializeField, Min(0f)]
        private float fallAccelerationBlocksPerSecondSquared = 24f;
        [SerializeField, Min(0f)]
        private float fallMaximumSpeedBlocksPerSecond = 20f;
        [SerializeField, Min(0f)]
        private float landingHoldSeconds = 1f;
        [SerializeField, Min(0f)]
        private float cameraReleaseSeconds = .25f;

        public override Playable CreatePlayable(
            PlayableGraph graph,
            GameObject owner)
        {
            PlayableDirector resolvedDirector =
                BossTimelinePerformanceFrame.ResolveDirector(
                    graph,
                    owner);
            BossAppearCinematicBehaviour behaviour =
                new BossAppearCinematicBehaviour
                {
                    bossPrefab = bossPrefab,
                    font = font,
                    warningLine = warningLine,
                    questionLine = questionLine,
                    alarmLine = alarmLine,
                    introFreezeSeconds =
                        introFreezeSeconds,
                    warningDisplaySeconds =
                        warningDisplaySeconds,
                    questionDisplaySeconds =
                        questionDisplaySeconds,
                    alarmDisplaySeconds =
                        alarmDisplaySeconds,
                    messageFontSize = messageFontSize,
                    typewriterCharactersPerSecond =
                        typewriterCharactersPerSecond,
                    glitchTransitionSeconds =
                        glitchTransitionSeconds,
                    glitchMinAlpha = glitchMinAlpha,
                    glitchMaxAlpha = glitchMaxAlpha,
                    glitchHorizontalJitter =
                        glitchHorizontalJitter,
                    glitchVerticalJitter =
                        glitchVerticalJitter,
                    glitchColorSeparation =
                        glitchColorSeparation,
                    playerCameraOffset =
                        playerCameraOffset,
                    bossSpawnOffsetFromPlayer =
                        bossSpawnOffsetFromPlayer,
                    fallAccelerationBlocksPerSecondSquared =
                        fallAccelerationBlocksPerSecondSquared,
                    fallMaximumSpeedBlocksPerSecond =
                        fallMaximumSpeedBlocksPerSecond,
                    landingHoldSeconds =
                        landingHoldSeconds,
                    cameraReleaseSeconds =
                        cameraReleaseSeconds,
                    director = resolvedDirector
                };
            return ScriptPlayable<BossAppearCinematicBehaviour>
                .Create(graph, behaviour);
        }
    }

    public sealed class BossAppearCinematicBehaviour :
        PlayableBehaviour,
        ICameraControlSource,
        ICameraLensOverrideSource
    {
        private enum Phase
        {
            WaitIntro,
            WaitWarning,
            WaitQuestion,
            WaitLanding,
            HoldOnBoss,
            WaitAlarm,
            Complete
        }

        public BossController bossPrefab;
        public TMP_FontAsset font;
        public string warningLine;
        public string questionLine;
        public string alarmLine;
        public float introFreezeSeconds = 1f;
        public float warningDisplaySeconds = 1.8f;
        public float questionDisplaySeconds = 1f;
        public float alarmDisplaySeconds = 1f;
        public float messageFontSize = 72f;
        public float typewriterCharactersPerSecond = 12f;
        public float glitchTransitionSeconds = .48f;
        public float glitchMinAlpha = .35f;
        public float glitchMaxAlpha = 1f;
        public float glitchHorizontalJitter = 18f;
        public float glitchVerticalJitter = 4f;
        public float glitchColorSeparation = .85f;
        public Vector3 playerCameraOffset =
            new Vector3(0f, 0f, -10f);
        public Vector3 bossSpawnOffsetFromPlayer =
            new Vector3(-7f, 13f, 0f);
        public float fallAccelerationBlocksPerSecondSquared = 24f;
        public float fallMaximumSpeedBlocksPerSecond = 20f;
        public float landingHoldSeconds = 1f;
        public float cameraReleaseSeconds = .25f;
        public PlayableDirector director;

        private BossTimelinePerformanceFrame frame;
        private PlayerController player;
        private CameraControlManager cameraManager;
        private TimelineCamRig rig;
        private CameraControlHandle controlHandle;
        private CameraViewModeRequestHandle viewModeHandle;
        private BossAppearCameraGuard cameraGuard;
        private CameraState shotState;
        private float requestedOrthographicSize;
        private float requestedFieldOfView;
        private WordNoteHandle message;
        private BossController boss;
        private Vector3 originPosition;
        private Quaternion originRotation;
        private float baseOrthographicSize;
        private float baseFieldOfView;
        private float phaseStartTime;
        private float landingTime;
        private float fallStartY;
        private float fallDistance;
        private bool initialized;
        private bool failed;
        private bool completed;
        private Phase phase = Phase.WaitIntro;

        public string CameraControlName =>
            "Boss Appear Timeline";

        public override void OnBehaviourPlay(
            Playable playable,
            FrameData info)
        {
            Cleanup();
            initialized = false;
            failed = false;
            completed = false;
            phase = Phase.WaitIntro;
            phaseStartTime = 0f;
            landingTime = 0f;
        }

        public override void ProcessFrame(
            Playable playable,
            FrameData info,
            object playerData)
        {
            if (failed || completed)
            {
                return;
            }
            if (!initialized && !Initialize())
            {
                failed = true;
                director?.Stop();
                return;
            }
            EnsureCameraLease();

            double duration = playable.GetDuration();
            if (duration > 0d &&
                playable.GetTime() >= duration - .0001d)
            {
                FailAndStop("Timeline 在出场流程完成前结束。");
                return;
            }

            float time = (float)playable.GetTime();
            switch (phase)
            {
                case Phase.WaitIntro:
                    ApplyCamera(originPosition);
                    if (time >= introFreezeSeconds)
                    {
                        if (!ShowWarning())
                        {
                            FailAndStop(
                                "无法创建故障字幕。");
                            return;
                        }
                        phase = Phase.WaitWarning;
                    }
                    break;

                case Phase.WaitWarning:
                    ApplyCamera(originPosition);
                    if (MessageFinished())
                    {
                        if (!ShowQuestion())
                        {
                            FailAndStop(
                                "无法创建问号字幕。");
                            return;
                        }
                        phase = Phase.WaitQuestion;
                    }
                    break;

                case Phase.WaitQuestion:
                    ApplyCamera(originPosition);
                    if (MessageFinished())
                    {
                        if (!SpawnBoss())
                        {
                            FailAndStop(
                                "Boss 生成失败。");
                            return;
                        }
                        phase = Phase.WaitLanding;
                    }
                    break;

                case Phase.WaitLanding:
                    UpdateWaitLanding(time);
                    break;

                case Phase.HoldOnBoss:
                    ApplyCamera(originPosition);
                    if (time - landingTime >=
                        landingHoldSeconds)
                    {
                        phaseStartTime = time;
                        if (!ShowAlarm())
                        {
                            FailAndStop(
                                "无法创建警告字幕。");
                            return;
                        }
                        phase = Phase.WaitAlarm;
                    }
                    break;

                case Phase.WaitAlarm:
                    ApplyCamera(originPosition);
                    if (MessageFinished())
                    {
                        FinishSequence();
                    }
                    break;
            }
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

        private bool Initialize()
        {
            if (bossPrefab == null)
            {
                Debug.LogError(
                    "[BossAppear] 未配置 Boss 预制体。");
                return false;
            }
            if (font == null)
            {
                Debug.LogError(
                    "[BossAppear] 未配置 Cubic_11 SDF 字体。");
                return false;
            }
            if (WordNoteUI.Instance == null)
            {
                Debug.LogError(
                    "[BossAppear] 场景中缺少 WordNote_UI。");
                return false;
            }
            if (!BossTimelinePerformanceFrame.TryResolve(
                    director,
                    nameof(BossAppearCinematicPlayable),
                    out frame))
            {
                return false;
            }

            GameObject playerObject = FindTagged("Player");
            player = playerObject != null
                ? playerObject.GetComponent<PlayerController>()
                : null;
            if (player == null)
            {
                Debug.LogError(
                    "[BossAppear] 找不到标准玩家。");
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
                    "[BossAppear] 场景中缺少 CameraControlManager。");
                return false;
            }

            rig = cameraManager.GetComponent<
                      TimelineCamRig>() ??
                  cameraManager.gameObject.AddComponent<
                      TimelineCamRig>();
            StopCompetingCameraDirectors();
            if (rig.IsTimelineControlling ||
                rig.isPlayingAnim)
            {
                rig.ReleaseTimelineControl(false);
            }
            viewModeHandle = rig.RequestViewMode(
                CameraViewMode.Side2D,
                CameraTransition.Immediate);
            CameraState state = cameraManager.CurrentState;
            baseOrthographicSize = Mathf.Max(
                .01f,
                state.orthographicSize);
            baseFieldOfView = state.fieldOfView;
            originPosition =
                player.transform.position +
                frame.ToWorldOffset(playerCameraOffset);
            originRotation = Quaternion.Euler(
                0f,
                rig.PlanarYaw,
                0f);
            shotState = state;
            shotState.position = originPosition;
            shotState.rotation = originRotation;
            requestedOrthographicSize =
                baseOrthographicSize;
            requestedFieldOfView =
                baseFieldOfView;
            controlHandle = cameraManager.RequestControl(
                this,
                CameraControlPriorities.Debug,
                CameraInterruptionPolicy.BlockAll,
                CameraTransition.Immediate);
            Camera outputCamera =
                cameraManager.OutputCamera ??
                Camera.main;
            if (outputCamera != null)
            {
                GameObject guardObject =
                    new GameObject(
                        "Boss Appear Camera Guard");
                cameraGuard =
                    guardObject.AddComponent<
                        BossAppearCameraGuard>();
                cameraGuard.Bind(
                    outputCamera,
                    originPosition,
                    originRotation,
                    baseOrthographicSize,
                    baseFieldOfView);
            }
            ApplyCamera(originPosition);

            initialized = true;
            return true;
        }

        private void StopCompetingCameraDirectors()
        {
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
                    candidate == director ||
                    candidate.state != PlayState.Playing ||
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

        private void UpdateWaitLanding(float time)
        {
            ApplyCamera(originPosition);
            if (boss == null || boss.IsDead)
            {
                FailAndStop(
                    "Boss 在下落阶段丢失。");
                return;
            }

            float elapsed = Mathf.Max(
                0f,
                time - phaseStartTime);
            float cellSize = Mathf.Max(
                .01f,
                GridCellSizeUtility.Resolve(player));
            float acceleration =
                Mathf.Max(
                    0f,
                    fallAccelerationBlocksPerSecondSquared) *
                cellSize;
            float maxSpeed =
                Mathf.Max(
                    0f,
                    fallMaximumSpeedBlocksPerSecond) *
                cellSize;
            float fallProgress;
            if (acceleration <= 0f)
            {
                fallProgress = elapsed * maxSpeed;
            }
            else
            {
                float speedUpTime =
                    maxSpeed > 0f
                        ? maxSpeed / acceleration
                        : float.PositiveInfinity;
                fallProgress = elapsed <= speedUpTime
                    ? .5f *
                      acceleration *
                      elapsed *
                      elapsed
                    : .5f *
                      acceleration *
                      speedUpTime *
                      speedUpTime +
                      maxSpeed *
                      (elapsed - speedUpTime);
            }

            float nextY = Mathf.Max(
                player.transform.position.y,
                fallStartY - fallProgress);
            Vector3 position = boss.transform.position;
            position.y = nextY;
            boss.SetPresentationFallPosition(position);
            if (fallStartY - nextY <
                fallDistance - .0001f)
            {
                return;
            }

            position.y =
                player.transform.position.y;
            boss.SetPresentationFallPosition(position);
            landingTime = time;
            phase = Phase.HoldOnBoss;
        }

        private bool SpawnBoss()
        {
            Vector3 spawnPosition =
                player.transform.position +
                frame.ToWorldOffset(
                    bossSpawnOffsetFromPlayer);
            boss = Object.Instantiate(
                bossPrefab,
                spawnPosition,
                bossPrefab.transform.rotation);
            boss.name = bossPrefab.name;
            boss.SetTrackingEnabled(false);
            boss.BeginPresentationFall();
            fallStartY = boss.transform.position.y;
            fallDistance = Mathf.Max(
                0f,
                fallStartY -
                player.transform.position.y);
            phaseStartTime =
                (float)director.time;
            return boss != null;
        }

        private bool ShowWarning()
        {
            message = WordNoteUI.Instance.RequestTimeline(
                warningLine,
                BuildGlitchStyle());
            return message != null;
        }

        private bool ShowQuestion()
        {
            message = WordNoteUI.Instance.RequestTimeline(
                questionLine,
                BuildTypewriterStyle(
                    questionDisplaySeconds));
            return message != null;
        }

        private bool ShowAlarm()
        {
            message = WordNoteUI.Instance.RequestTimeline(
                alarmLine,
                BuildTypewriterStyle(
                    alarmDisplaySeconds));
            return message != null;
        }

        private WordNoteStyle BuildGlitchStyle()
        {
            var style = BuildBaseStyle(
                warningDisplaySeconds);
            style.enterAnimation = WordNoteAnimation.Glitch;
            style.exitAnimation = WordNoteAnimation.Glitch;
            style.enterMotion = BuildGlitchMotion();
            style.exitMotion = BuildGlitchMotion();
            return style;
        }

        private WordNoteStyle BuildTypewriterStyle(
            float displaySeconds)
        {
            var style = BuildBaseStyle(displaySeconds);
            style.enterAnimation =
                WordNoteAnimation.Typewriter;
            style.enterMotion =
                new WordNoteMotionSettings
                {
                    useCombinedChannels = true,
                    useFade = false,
                    useTypewriter = true,
                    typewriterCharactersPerSecond =
                        Mathf.Max(
                            1f,
                            typewriterCharactersPerSecond),
                    duration = .01f
                };
            style.useExitAnimation = false;
            style.exitAnimation =
                WordNoteAnimation.None;
            return style;
        }

        private WordNoteStyle BuildBaseStyle(
            float displaySeconds)
        {
            return new WordNoteStyle
            {
                font = font,
                fontSize = Mathf.Max(
                    1f,
                    messageFontSize),
                bold = true,
                richText = true,
                fontColor = WordNoteColor.White,
                useOutline = true,
                outlineColor = WordNoteColor.Black,
                textAlignment =
                    WordNoteTextAlignment.Center,
                position = WordNotePosition.Bottom,
                participateInStackLayout = false,
                displayMode =
                    WordNoteDisplayMode.Timed,
                displayDuration =
                    Mathf.Max(0f, displaySeconds),
                useEnterAnimation = true,
                useExitAnimation = true
            };
        }

        private WordNoteMotionSettings BuildGlitchMotion()
        {
            return new WordNoteMotionSettings
            {
                useCombinedChannels = true,
                useFade = false,
                useGlitch = true,
                glitchMinAlpha = glitchMinAlpha,
                glitchMaxAlpha = glitchMaxAlpha,
                glitchHorizontalJitter =
                    glitchHorizontalJitter,
                glitchVerticalJitter =
                    glitchVerticalJitter,
                glitchColorSeparation =
                    glitchColorSeparation,
                duration = Mathf.Max(
                    0f,
                    glitchTransitionSeconds),
                startAlpha = 1f,
                endAlpha = 1f,
                startScale = Vector3.one,
                endScale = Vector3.one
            };
        }

        private bool MessageFinished()
        {
            if (message == null)
            {
                return true;
            }
            if (message.State !=
                WordNoteMessageState.Finished)
            {
                return false;
            }

            message = null;
            return true;
        }

        private void ApplyCamera(Vector3 position)
        {
            if (rig == null || cameraManager == null)
            {
                return;
            }
            EnsureCameraLease();
            shotState.position = position;
            shotState.rotation = Quaternion.Euler(
                0f,
                rig.PlanarYaw,
                0f);
            requestedOrthographicSize =
                baseOrthographicSize;
            requestedFieldOfView =
                baseFieldOfView;
            cameraGuard?.SetShot(
                position,
                shotState.rotation,
                requestedOrthographicSize,
                requestedFieldOfView);
        }

        private void EnsureCameraLease()
        {
            if (cameraManager == null ||
                controlHandle.HasControl)
            {
                return;
            }
            controlHandle = cameraManager.RequestControl(
                this,
                CameraControlPriorities.Debug,
                CameraInterruptionPolicy.BlockAll,
                CameraTransition.Immediate);
        }

        private void FinishSequence()
        {
            if (completed)
            {
                return;
            }
            completed = true;
            ReleaseCamera();

            if (boss != null)
            {
                boss.EndPresentationFall(
                    boss.transform.position);
                boss.SetTrackingEnabled(true);
                boss.ActivateHealthSystem();
                BossCameraSize cameraSize =
                    boss.GetComponent<BossCameraSize>();
                cameraSize?.RequestEnable();
            }

            director?.Stop();
        }

        private void FailAndStop(string reason)
        {
            if (!failed)
            {
                failed = true;
                Debug.LogError(
                    "[BossAppear] " + reason,
                    director);
            }
            director?.Stop();
        }

        private void ReleaseCamera()
        {
            if (viewModeHandle.IsValid)
            {
                if (rig != null)
                {
                    rig.ReleaseViewMode(
                        viewModeHandle);
                }
                else
                {
                    viewModeHandle.Release(true);
                }
            }
            viewModeHandle = default;

            if (controlHandle.IsValid)
            {
                controlHandle.Release(
                    CameraTransition.Ease(
                        Mathf.Max(
                            0f,
                            cameraReleaseSeconds)));
            }
            controlHandle = default;
            if (cameraGuard != null)
            {
                Object.Destroy(cameraGuard.gameObject);
                cameraGuard = null;
            }
            rig = null;
        }

        public bool TryGetCameraState(
            in CameraControlContext context,
            out CameraState state)
        {
            state = shotState;
            return true;
        }

        public bool TryGetLensOverride(
            out float orthographicSize,
            out float fieldOfView)
        {
            orthographicSize =
                requestedOrthographicSize;
            fieldOfView = requestedFieldOfView;
            return true;
        }

        private void Cleanup()
        {
            if (message != null)
            {
                message.ForceDisappear();
                message.Release(false);
                message = null;
            }

            ReleaseCamera();
            if (!completed &&
                boss != null &&
                boss.gameObject != null)
            {
                if (Application.isPlaying)
                {
                    Object.Destroy(boss.gameObject);
                }
                else
                {
                    Object.DestroyImmediate(
                        boss.gameObject);
                }
            }
            boss = null;
            player = null;
            cameraManager = null;
            frame = default;
            initialized = false;
            failed = false;
            phase = Phase.WaitIntro;
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
    }
}
