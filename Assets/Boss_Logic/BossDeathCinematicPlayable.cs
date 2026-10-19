using System;
using System.Collections.Generic;
using Project.BlockFeatures;
using Project.CameraModes;
using Project.ColorBlocks;
using Project.Items;
using Project.Pickups;
using Project.Player;
using Project.Rooms;
using Project.WordNote;
using TMPro;
using UnityEngine;
using UnityEngine.Playables;

namespace Project.BossLogic
{
    public enum BossDeathCinematicMode
    {
        Natural,
        Execution
    }

    public sealed class BossDeathCinematicPlayable :
        PlayableAsset
    {
        [SerializeField]
        private BossDeathCinematicMode mode;
        [SerializeField] private string bossTag = "Boss";
        [SerializeField] private string playerTag = "Player";
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private GameObject redKeyPrefab;
        [SerializeField] private GameObject blueKeyPrefab;
        [SerializeField] private GameObject greenKeyPrefab;
        [SerializeField, Min(1)] private int copiesPerColor = 2;
        [SerializeField, TextArea]
        private string firstBossLine = "咳咳……";
        [SerializeField, TextArea]
        private string secondBossLine =
            "嘁，你以为我没留后手吗！";
        [SerializeField, TextArea]
        private string playerLine =
            "身体好难受……使不上劲";
        [SerializeField, TextArea]
        private string finalBossLine =
            "哈哈哈哈……我不好过你也别想好过！";
        [SerializeField, Min(1f)]
        private float messageFontSize = 72f;
        [SerializeField, Min(1f)]
        private float typewriterCharactersPerSecond = 12f;
        [SerializeField, Min(0f)]
        private float messageDisplaySeconds = 2f;
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
        private Vector3 bossCameraOffset =
            new Vector3(0f, 2f, -10f);
        [SerializeField]
        private Vector3 playerCameraOffset =
            new Vector3(0f, 0f, -10f);
        [SerializeField, Min(0f)]
        private float naturalCameraMoveSeconds = 1f;
        [SerializeField, Min(0f)]
        private float executionCameraMoveSeconds = .55f;
        [SerializeField, Min(0f)]
        private float cameraReturnSeconds = 1f;
        [SerializeField, Min(0f)]
        private float cameraReleaseSeconds = .25f;
        [SerializeField, Min(0f)]
        private float bossFlickerInterval = .16f;
        [SerializeField, Min(0f)]
        private float bossFlickerLeadSeconds = 1f;
        [SerializeField, Min(0f)]
        private float executionStartDelaySeconds = .5f;
        [SerializeField]
        private float executionBossHeightOffsetBlocks = -3f;
        [SerializeField, Min(0f)]
        private float executionMoveSpeedBlocksPerSecond = 10f;
        [SerializeField, Min(.01f)]
        private float playerFlickerInterval = .09f;
        [SerializeField]
        private Vector3 keyBodyOffset =
            new Vector3(0f, .4f, 0f);
        [SerializeField, Min(0f)]
        private float keyColumnBaseHeight = 1.45f;
        [SerializeField, Min(0f)]
        private float keyColumnSpacing = .42f;
        [SerializeField, Min(0f)]
        private float keyEmergeStagger = .18f;
        [SerializeField, Min(.01f)]
        private float keyEmergeDuration = .38f;
        [SerializeField, Min(0f)]
        private float keyHoverBobHeight = .04f;
        [SerializeField, Min(0f)]
        private float keyHoverBobSpeed = 3.2f;
        [SerializeField, Min(0f)]
        private float keyPlayerLineDelaySeconds = 1f;
        [SerializeField, Min(0f)]
        private float keyAbsorbDelaySeconds = .5f;
        [SerializeField, Min(0f)]
        private float keyAbsorbStagger = .12f;
        [SerializeField, Min(.01f)]
        private float keyAbsorbDuration = .45f;
        [SerializeField]
        private Vector3 bossKeyAbsorbOffset =
            new Vector3(0f, .45f, 0f);
        [SerializeField, Min(0f)]
        private float deathAnimationFallbackSeconds = 2f;

        public override Playable CreatePlayable(
            PlayableGraph graph,
            GameObject owner)
        {
            PlayableDirector resolvedDirector =
                BossTimelinePerformanceFrame.ResolveDirector(
                    graph,
                    owner);
            BossDeathCinematicBehaviour behaviour =
                new BossDeathCinematicBehaviour
                {
                    mode = mode,
                    bossTag = bossTag,
                    playerTag = playerTag,
                    font = font,
                    redKeyPrefab = redKeyPrefab,
                    blueKeyPrefab = blueKeyPrefab,
                    greenKeyPrefab = greenKeyPrefab,
                    copiesPerColor = copiesPerColor,
                    firstBossLine = firstBossLine,
                    secondBossLine = secondBossLine,
                    playerLine = playerLine,
                    finalBossLine = finalBossLine,
                    messageFontSize = messageFontSize,
                    typewriterCharactersPerSecond =
                        typewriterCharactersPerSecond,
                    messageDisplaySeconds =
                        messageDisplaySeconds,
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
                    bossCameraOffset = bossCameraOffset,
                    playerCameraOffset = playerCameraOffset,
                    naturalCameraMoveSeconds =
                        naturalCameraMoveSeconds,
                    executionCameraMoveSeconds =
                        executionCameraMoveSeconds,
                    cameraReturnSeconds =
                        cameraReturnSeconds,
                    cameraReleaseSeconds =
                        cameraReleaseSeconds,
                    bossFlickerInterval =
                        bossFlickerInterval,
                    bossFlickerLeadSeconds =
                        bossFlickerLeadSeconds,
                    executionStartDelaySeconds =
                        executionStartDelaySeconds,
                    executionBossHeightOffsetBlocks =
                        executionBossHeightOffsetBlocks,
                    executionMoveSpeedBlocksPerSecond =
                        executionMoveSpeedBlocksPerSecond,
                    playerFlickerInterval =
                        playerFlickerInterval,
                    keyBodyOffset = keyBodyOffset,
                    keyColumnBaseHeight =
                        keyColumnBaseHeight,
                    keyColumnSpacing = keyColumnSpacing,
                    keyEmergeStagger = keyEmergeStagger,
                    keyEmergeDuration = keyEmergeDuration,
                    keyHoverBobHeight =
                        keyHoverBobHeight,
                    keyHoverBobSpeed = keyHoverBobSpeed,
                    keyPlayerLineDelaySeconds =
                        keyPlayerLineDelaySeconds,
                    keyAbsorbDelaySeconds =
                        keyAbsorbDelaySeconds,
                    keyAbsorbStagger = keyAbsorbStagger,
                    keyAbsorbDuration = keyAbsorbDuration,
                    bossKeyAbsorbOffset =
                        bossKeyAbsorbOffset,
                    deathAnimationFallbackSeconds =
                        deathAnimationFallbackSeconds,
                    director = resolvedDirector
                };
            return ScriptPlayable<BossDeathCinematicBehaviour>
                .Create(graph, behaviour);
        }
    }

    public sealed class BossDeathCinematicBehaviour :
        PlayableBehaviour
    {
        private sealed class KeyState
        {
            public GameObject gameObject;
            public Transform transform;
            public Vector3 baseScale;
            public Vector3 bodyPosition;
            public Vector3 hoverPosition;
            public bool absorbed;
        }

        private enum Phase
        {
            WaitStart,
            MoveToBoss,
            ExecutionWaitDamage,
            WaitBossFlicker,
            WaitBossLineOne,
            WaitBossLineTwo,
            ReturnToPlayer,
            PlayerFlicker,
            KeysEmerge,
            WaitPlayerLineDelay,
            WaitPlayerLine,
            WaitAbsorbDelay,
            AbsorbKeysAndMoveCamera,
            WaitFinalBossLine,
            WaitDeathAnimation,
            ReturnAfterBossDeath,
            Complete
        }

        public BossDeathCinematicMode mode;
        public string bossTag = "Boss";
        public string playerTag = "Player";
        public TMP_FontAsset font;
        public GameObject redKeyPrefab;
        public GameObject blueKeyPrefab;
        public GameObject greenKeyPrefab;
        public int copiesPerColor = 2;
        public string firstBossLine;
        public string secondBossLine;
        public string playerLine;
        public string finalBossLine;
        public float messageFontSize = 72f;
        public float typewriterCharactersPerSecond = 12f;
        public float messageDisplaySeconds = 2f;
        public float glitchTransitionSeconds = .48f;
        public float glitchMinAlpha = .35f;
        public float glitchMaxAlpha = 1f;
        public float glitchHorizontalJitter = 18f;
        public float glitchVerticalJitter = 4f;
        public float glitchColorSeparation = .85f;
        public Vector3 bossCameraOffset =
            new Vector3(0f, 2f, -10f);
        public Vector3 playerCameraOffset =
            new Vector3(0f, 0f, -10f);
        public float naturalCameraMoveSeconds = 1f;
        public float executionCameraMoveSeconds = .55f;
        public float cameraReturnSeconds = 1f;
        public float cameraReleaseSeconds = .25f;
        public float bossFlickerInterval = .16f;
        public float bossFlickerLeadSeconds = 1f;
        public float executionStartDelaySeconds = .5f;
        public float executionBossHeightOffsetBlocks = -3f;
        public float executionMoveSpeedBlocksPerSecond = 10f;
        public float playerFlickerInterval = .09f;
        public Vector3 keyBodyOffset =
            new Vector3(0f, .4f, 0f);
        public float keyColumnBaseHeight = 1.45f;
        public float keyColumnSpacing = .42f;
        public float keyEmergeStagger = .18f;
        public float keyEmergeDuration = .38f;
        public float keyHoverBobHeight = .04f;
        public float keyHoverBobSpeed = 3.2f;
        public float keyPlayerLineDelaySeconds = 1f;
        public float keyAbsorbDelaySeconds = .5f;
        public float keyAbsorbStagger = .12f;
        public float keyAbsorbDuration = .45f;
        public Vector3 bossKeyAbsorbOffset =
            new Vector3(0f, .45f, 0f);
        public float deathAnimationFallbackSeconds = 2f;
        public PlayableDirector director;

        private BossTimelinePerformanceFrame frame;
        private BossController boss;
        private PlayerController player;
        private BossAnimationDriver bossAnimation;
        private BossRightMoveExecutor bossMover;
        private BossDefeatTimelinePresenter presenter;
        private CameraControlManager cameraManager;
        private TimelineCamRig rig;
        private CameraViewModeRequestHandle viewModeHandle;
        private WordNoteHandle message;
        private readonly List<KeyState> keys =
            new List<KeyState>();
        private GameObject keyRoot;
        private Renderer[] bossRenderers =
            Array.Empty<Renderer>();
        private bool[] bossRendererStates =
            Array.Empty<bool>();
        private Renderer[] playerRenderers =
            Array.Empty<Renderer>();
        private bool[] playerRendererStates =
            Array.Empty<bool>();
        private Vector3 originPosition;
        private Quaternion originRotation;
        private Vector3 cameraMoveStart;
        private float baseOrthographicSize;
        private float baseFieldOfView;
        private float phaseStartTime;
        private float bossFlickerStartTime = -1f;
        private float absorbStartTime;
        private bool initialized;
        private bool failed;
        private bool completed;
        private bool bossDestroyed;
        private bool executionMovementStarted;
        private bool executionDamageDetected;
        private bool bossFlickerActive;
        private bool previousPlayerContactKillEnabled = true;
        private Phase phase = Phase.WaitStart;

        public override void OnBehaviourPlay(
            Playable playable,
            FrameData info)
        {
            Cleanup();
            initialized = false;
            failed = false;
            completed = false;
            bossDestroyed = false;
            executionMovementStarted = false;
            executionDamageDetected = false;
            bossFlickerActive = false;
            bossFlickerStartTime = -1f;
            phaseStartTime = 0f;
            phase = Phase.WaitStart;
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
                FailAndStop("死亡演出初始化失败。");
                return;
            }

            double duration = playable.GetDuration();
            if (duration > 0d &&
                playable.GetTime() >= duration - .0001d &&
                phase != Phase.Complete)
            {
                FailAndStop("死亡 Timeline 在演出完成前结束。");
                return;
            }

            float time = (float)playable.GetTime();
            UpdateBossFlicker(time);

            switch (phase)
            {
                case Phase.WaitStart:
                    BeginIntro(time);
                    break;

                case Phase.MoveToBoss:
                    UpdateMoveToBoss(time);
                    break;

                case Phase.ExecutionWaitDamage:
                    UpdateExecutionWaitDamage(time);
                    break;

                case Phase.WaitBossFlicker:
                    if (time - phaseStartTime >=
                        bossFlickerLeadSeconds)
                    {
                        if (!ShowBossMessage(firstBossLine))
                        {
                            FailAndStop(
                                "无法创建 Boss 故障字幕。");
                            return;
                        }
                        phase = Phase.WaitBossLineOne;
                    }
                    break;

                case Phase.WaitBossLineOne:
                    if (MessageFinished())
                    {
                        if (!ShowBossMessage(secondBossLine))
                        {
                            FailAndStop(
                                "无法创建第二句 Boss 故障字幕。");
                            return;
                        }
                        phase = Phase.WaitBossLineTwo;
                    }
                    break;

                case Phase.WaitBossLineTwo:
                    if (MessageFinished())
                    {
                        phaseStartTime = time;
                        cameraMoveStart = rig.CurrentPosition;
                        phase = Phase.ReturnToPlayer;
                    }
                    break;

                case Phase.ReturnToPlayer:
                    UpdateReturnToPlayer(time);
                    break;

                case Phase.PlayerFlicker:
                    UpdatePlayerFlicker(time);
                    break;

                case Phase.KeysEmerge:
                    UpdateKeysEmerge(time);
                    break;

                case Phase.WaitPlayerLineDelay:
                    UpdateKeyHover(time);
                    if (time - phaseStartTime >=
                        keyPlayerLineDelaySeconds)
                    {
                        if (!ShowPlayerMessage())
                        {
                            FailAndStop(
                                "无法创建玩家打字机字幕。");
                            return;
                        }
                        phase = Phase.WaitPlayerLine;
                    }
                    break;

                case Phase.WaitPlayerLine:
                    UpdateKeyHover(time);
                    if (MessageFinished())
                    {
                        phaseStartTime = time;
                        phase = Phase.WaitAbsorbDelay;
                    }
                    break;

                case Phase.WaitAbsorbDelay:
                    UpdateKeyHover(time);
                    if (time - phaseStartTime >=
                        keyAbsorbDelaySeconds)
                    {
                        absorbStartTime = time;
                        phaseStartTime = time;
                        cameraMoveStart = rig.CurrentPosition;
                        phase = Phase.AbsorbKeysAndMoveCamera;
                    }
                    break;

                case Phase.AbsorbKeysAndMoveCamera:
                    UpdateAbsorbAndCamera(time);
                    break;

                case Phase.WaitFinalBossLine:
                    if (MessageFinished())
                    {
                        BeginDeathAnimation(time);
                    }
                    break;

                case Phase.WaitDeathAnimation:
                    UpdateDeathAnimation(time);
                    break;

                case Phase.ReturnAfterBossDeath:
                    UpdateReturnAfterBossDeath(time);
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
            if (font == null)
            {
                Debug.LogError(
                    "[BossDeath] 未配置 Cubic_11 SDF 字体。");
                return false;
            }
            if (WordNoteUI.Instance == null)
            {
                Debug.LogError(
                    "[BossDeath] 场景中缺少 WordNote_UI。");
                return false;
            }
            if (!BossTimelinePerformanceFrame.TryResolve(
                    director,
                    nameof(BossDeathCinematicPlayable),
                    out frame))
            {
                return false;
            }

            GameObject bossObject = FindTagged(bossTag);
            GameObject playerObject = FindTagged(playerTag);
            boss = bossObject != null
                ? bossObject.GetComponent<BossController>()
                : null;
            player = playerObject != null
                ? playerObject.GetComponent<PlayerController>()
                : null;
            if (boss == null || player == null)
            {
                Debug.LogError(
                    "[BossDeath] 找不到 Boss 或玩家。");
                return false;
            }

            bossAnimation =
                boss.GetComponent<BossAnimationDriver>();
            bossMover =
                boss.GetComponent<BossRightMoveExecutor>();
            presenter =
                boss.GetComponent<
                    BossDefeatTimelinePresenter>();
            previousPlayerContactKillEnabled =
                boss.PlayerContactKillEnabled;
            boss.SetPlayerContactKillEnabled(false);

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
                    "[BossDeath] 场景中缺少 CameraControlManager。");
                return false;
            }

            rig = cameraManager.GetComponent<
                      TimelineCamRig>() ??
                  cameraManager.gameObject.AddComponent<
                      TimelineCamRig>();
            rig.Acquire(0f);
            rig.SetReturnDurationForCurrentShot(
                Mathf.Max(0f, cameraReleaseSeconds));
            originPosition = rig.CurrentPosition;
            originRotation = rig.CurrentRotation;
            CameraState state = cameraManager.CurrentState;
            baseOrthographicSize = Mathf.Max(
                .01f,
                state.orthographicSize);
            baseFieldOfView = state.fieldOfView;
            viewModeHandle = rig.RequestViewMode(
                CameraViewMode.Side2D,
                CameraTransition.Immediate);
            CacheBossRenderers();
            CachePlayerRenderers();

            if (mode == BossDeathCinematicMode.Execution)
            {
                presenter?.SetDefeatPresentationSuppressed(true);
                bossMover?.SetExecutionSuspended(true);
                AlignBossForExecutionStart();
                boss.BeginExecutionSequence(
                    executionMoveSpeedBlocksPerSecond);
                boss.Damaged -= HandleBossDamaged;
                boss.Died -= HandleBossDied;
                boss.Damaged += HandleBossDamaged;
                boss.Died += HandleBossDied;
                phaseStartTime = 0f;
            }
            else
            {
                player
                    .TryTeleportToNearestRespawnForPresentation();
                BeginBossFlicker(0f);
                phaseStartTime = 0f;
                cameraMoveStart = originPosition;
            }

            initialized = true;
            phase = Phase.WaitStart;
            return true;
        }

        private void AlignBossForExecutionStart()
        {
            if (boss == null || player == null)
            {
                return;
            }
            Vector3 position = boss.WorldPosition;
            float localYOffset =
                frame.ToWorldOffset(
                    new Vector3(
                        0f,
                        executionBossHeightOffsetBlocks,
                        0f))
                .y;
            position.y =
                player.transform.position.y +
                localYOffset;
            boss.TeleportTo(
                position,
                boss.transform.rotation);
        }

        private void BeginIntro(float time)
        {
            if (mode == BossDeathCinematicMode.Natural)
            {
                phaseStartTime = time;
                cameraMoveStart = rig.CurrentPosition;
                phase = Phase.MoveToBoss;
                return;
            }

            if (time >= executionStartDelaySeconds)
            {
                phaseStartTime = time;
                cameraMoveStart = rig.CurrentPosition;
                phase = Phase.MoveToBoss;
            }
        }

        private void UpdateMoveToBoss(float time)
        {
            if (mode == BossDeathCinematicMode.Execution &&
                !executionDamageDetected &&
                !executionMovementStarted &&
                CameraProgress(time) >= .5f)
            {
                executionMovementStarted = true;
                boss.StartExecutionMovement();
            }

            Vector3 target = ResolveBossCameraPosition();
            float progress = CameraProgress(time);
            ApplyCamera(
                Vector3.Lerp(
                    cameraMoveStart,
                    target,
                    Smooth(progress)));

            if (progress < 1f)
            {
                return;
            }

            if (mode == BossDeathCinematicMode.Execution &&
                !executionDamageDetected)
            {
                phase = Phase.ExecutionWaitDamage;
                return;
            }

            phaseStartTime = time;
            phase = Phase.WaitBossFlicker;
        }

        private void UpdateExecutionWaitDamage(float time)
        {
            ApplyCamera(ResolveBossCameraPosition());
            if (!executionDamageDetected && !boss.IsDead)
            {
                return;
            }

            executionDamageDetected = true;
            BeginBossFlicker(time);
            phaseStartTime = time;
            phase = Phase.WaitBossFlicker;
        }

        private void UpdateReturnToPlayer(float time)
        {
            float duration = Mathf.Max(
                .01f,
                cameraReturnSeconds);
            float progress = Mathf.Clamp01(
                (time - phaseStartTime) / duration);
            ApplyCamera(
                Vector3.Lerp(
                    cameraMoveStart,
                    ResolvePlayerCameraPosition(),
                    Smooth(progress)));
            if (progress < 1f)
            {
                return;
            }

            player.FaceWorldPosition(
                boss.WorldPosition);
            BeginPlayerFlicker(time);
        }

        private void BeginPlayerFlicker(float time)
        {
            phaseStartTime = time;
            SetPlayerRenderersVisible(false);
            phase = Phase.PlayerFlicker;
        }

        private void UpdatePlayerFlicker(float time)
        {
            int step = Mathf.FloorToInt(
                (time - phaseStartTime) /
                Mathf.Max(.01f, playerFlickerInterval));
            const int requiredSteps = 4;
            if (step < requiredSteps)
            {
                SetPlayerRenderersVisible(
                    (step & 1) != 0);
                return;
            }

            RestorePlayerRenderers();
            SpawnKeys();
            phaseStartTime = time;
            phase = Phase.KeysEmerge;
        }

        private void SpawnKeys()
        {
            DestroyKeys();
            keyRoot = new GameObject("BossDeathKeyColumn");
            List<GameObject> prefabs = BuildKeyPrefabOrder();
            int count = prefabs.Count;
            for (int index = 0;
                 index < count;
                 index++)
            {
                GameObject prefab = prefabs[index];
                GameObject instance =
                    UnityEngine.Object.Instantiate(
                    prefab,
                    keyRoot.transform);
                instance.name =
                    prefab.name + "_DeathCinematic";
                DisableKeyGameplay(instance);
                ApplyKeySorting(instance);
                Vector3 baseScale =
                    instance.transform.localScale;
                Vector3 body =
                    player.transform.position +
                    frame.ToWorldOffset(keyBodyOffset);
                Vector3 hover =
                    player.transform.position +
                    frame.ToWorldOffset(
                        new Vector3(
                            0f,
                            keyColumnBaseHeight +
                            index * keyColumnSpacing,
                            0f));
                instance.transform.position = body;
                instance.transform.localScale =
                    Vector3.zero;
                keys.Add(new KeyState
                {
                    gameObject = instance,
                    transform = instance.transform,
                    baseScale = baseScale,
                    bodyPosition = body,
                    hoverPosition = hover
                });
            }
        }

        private void UpdateKeysEmerge(float time)
        {
            float elapsed = time - phaseStartTime;
            for (int index = 0;
                 index < keys.Count;
                 index++)
            {
                KeyState key = keys[index];
                float local =
                    elapsed - index * keyEmergeStagger;
                if (local <= 0f)
                {
                    key.transform.position =
                        key.bodyPosition;
                    key.transform.localScale =
                        Vector3.zero;
                    continue;
                }

                float progress = Mathf.Clamp01(
                    local /
                    Mathf.Max(.01f, keyEmergeDuration));
                float eased = Smooth(progress);
                key.transform.position =
                    Vector3.Lerp(
                        key.bodyPosition,
                        key.hoverPosition,
                        eased);
                key.transform.localScale =
                    key.baseScale *
                    Mathf.SmoothStep(0f, 1f, progress);
                key.transform.rotation =
                    Quaternion.Euler(
                        0f,
                        0f,
                        progress * 180f);
            }

            float total =
                Mathf.Max(0, keys.Count - 1) *
                keyEmergeStagger +
                keyEmergeDuration;
            if (elapsed >= total)
            {
                UpdateKeyHover(time);
                phaseStartTime = time;
                phase = Phase.WaitPlayerLineDelay;
            }
        }

        private void UpdateKeyHover(float time)
        {
            for (int index = 0;
                 index < keys.Count;
                 index++)
            {
                KeyState key = keys[index];
                if (key.absorbed ||
                    key.transform == null)
                {
                    continue;
                }

                float bob = Mathf.Sin(
                                time *
                                keyHoverBobSpeed +
                                index) *
                            keyHoverBobHeight;
                key.transform.position =
                    key.hoverPosition +
                    frame.ToWorldOffset(
                        new Vector3(0f, bob, 0f));
                key.transform.localScale =
                    key.baseScale;
                key.transform.rotation =
                    Quaternion.Euler(
                        0f,
                        0f,
                        180f +
                        Mathf.Sin(
                            time *
                            keyHoverBobSpeed +
                            index) *
                        6f);
            }
        }

        private void UpdateAbsorbAndCamera(float time)
        {
            float cameraProgress = Mathf.Clamp01(
                (time - phaseStartTime) /
                Mathf.Max(.01f, cameraReturnSeconds));
            ApplyCamera(
                Vector3.Lerp(
                    cameraMoveStart,
                    ResolveBossCameraPosition(),
                    Smooth(cameraProgress)));

            Vector3 absorbTarget =
                boss.WorldPosition +
                frame.ToWorldOffset(
                    bossKeyAbsorbOffset);
            for (int index = 0;
                 index < keys.Count;
                 index++)
            {
                KeyState key = keys[index];
                if (key.absorbed ||
                    key.transform == null)
                {
                    continue;
                }

                float start =
                    absorbStartTime +
                    index * keyAbsorbStagger;
                float progress = Mathf.Clamp01(
                    (time - start) /
                    Mathf.Max(.01f, keyAbsorbDuration));
                Vector3 hover =
                    key.hoverPosition +
                    frame.ToWorldOffset(
                        new Vector3(
                            0f,
                            Mathf.Sin(
                                time *
                                keyHoverBobSpeed +
                                index) *
                            keyHoverBobHeight,
                            0f));
                Vector3 control =
                    Vector3.Lerp(
                        hover,
                        absorbTarget,
                        .5f) +
                    frame.ToWorldOffset(
                        new Vector3(0f, .5f, 0f));
                key.transform.position =
                    QuadraticBezier(
                        hover,
                        control,
                        absorbTarget,
                        Smooth(progress));
                key.transform.localScale =
                    key.baseScale *
                    (1f - progress);
                if (progress >= 1f)
                {
                    key.absorbed = true;
                    key.gameObject.SetActive(false);
                }
            }

            bool allAbsorbed = true;
            for (int index = 0;
                 index < keys.Count;
                 index++)
            {
                if (!keys[index].absorbed)
                {
                    allAbsorbed = false;
                    break;
                }
            }
            if (!allAbsorbed ||
                cameraProgress < 1f)
            {
                return;
            }

            if (!ShowBossMessage(finalBossLine))
            {
                FailAndStop(
                    "无法创建最终 Boss 故障字幕。");
                return;
            }
            phase = Phase.WaitFinalBossLine;
        }

        private void BeginDeathAnimation(float time)
        {
            StopBossFlicker();
            RestoreBossRenderers();
            bossAnimation?.PlayDeathAnimationNow();
            phaseStartTime = time;
            phase = Phase.WaitDeathAnimation;
        }

        private void UpdateDeathAnimation(float time)
        {
            bool complete;
            if (bossAnimation != null)
            {
                complete =
                    bossAnimation.IsDeathAnimationComplete;
            }
            else
            {
                complete =
                    time - phaseStartTime >=
                    Mathf.Max(
                        0f,
                        deathAnimationFallbackSeconds);
            }
            if (!complete)
            {
                return;
            }

            HideBossPresentation();
            phaseStartTime = time;
            cameraMoveStart = rig.CurrentPosition;
            phase = Phase.ReturnAfterBossDeath;
        }

        private void UpdateReturnAfterBossDeath(float time)
        {
            float progress = Mathf.Clamp01(
                (time - phaseStartTime) /
                Mathf.Max(.01f, cameraReturnSeconds));
            ApplyCamera(
                Vector3.Lerp(
                    cameraMoveStart,
                    ResolvePlayerCameraPosition(),
                    Smooth(progress)));
            if (progress < 1f)
            {
                return;
            }

            FinishSequence();
        }

        private void FinishSequence()
        {
            if (completed)
            {
                return;
            }
            completed = true;
            phase = Phase.Complete;
            presenter?.PlayPlayerThinkingAfterDefeat();
            DestroyBossObject();
            ReleaseCamera();
            RestorePlayerRenderers();
            DestroyKeys();
            message?.ForceDisappear();
            message?.Release(false);
            message = null;
            director?.Stop();
        }

        private void HandleBossDamaged(
            float amount,
            float remaining)
        {
            if (mode != BossDeathCinematicMode.Execution ||
                amount <= 0f)
            {
                return;
            }
            executionDamageDetected = true;
            BeginBossFlicker(
                director != null
                    ? (float)director.time
                    : Time.time);
        }

        private void HandleBossDied()
        {
            if (mode == BossDeathCinematicMode.Execution)
            {
                executionDamageDetected = true;
            }
        }

        private void BeginBossFlicker(float time)
        {
            if (bossFlickerActive)
            {
                return;
            }
            bossFlickerActive = true;
            bossFlickerStartTime = time;
        }

        private void UpdateBossFlicker(float time)
        {
            if (!bossFlickerActive ||
                boss == null ||
                bossRenderers.Length == 0)
            {
                return;
            }

            int phaseIndex = Mathf.FloorToInt(
                (time - bossFlickerStartTime) /
                Mathf.Max(.01f, bossFlickerInterval));
            bool visible = (phaseIndex & 1) == 0;
            for (int index = 0;
                 index < bossRenderers.Length;
                 index++)
            {
                Renderer renderer = bossRenderers[index];
                if (renderer != null)
                {
                    renderer.enabled =
                        bossRendererStates[index] &&
                        visible;
                }
            }
        }

        private void StopBossFlicker()
        {
            bossFlickerActive = false;
            bossFlickerStartTime = -1f;
        }

        private void CacheBossRenderers()
        {
            bossRenderers =
                boss.GetComponentsInChildren<
                    Renderer>(true);
            bossRendererStates =
                new bool[bossRenderers.Length];
            for (int index = 0;
                 index < bossRenderers.Length;
                 index++)
            {
                bossRendererStates[index] =
                    bossRenderers[index] != null &&
                    bossRenderers[index].enabled;
            }
        }

        private void CachePlayerRenderers()
        {
            playerRenderers =
                player.GetComponentsInChildren<
                    Renderer>(true);
            playerRendererStates =
                new bool[playerRenderers.Length];
            for (int index = 0;
                 index < playerRenderers.Length;
                 index++)
            {
                playerRendererStates[index] =
                    playerRenderers[index] != null &&
                    playerRenderers[index].enabled;
            }
        }

        private void SetPlayerRenderersVisible(bool visible)
        {
            for (int index = 0;
                 index < playerRenderers.Length;
                 index++)
            {
                if (playerRenderers[index] != null)
                {
                    playerRenderers[index].enabled =
                        playerRendererStates[index] &&
                        visible;
                }
            }
        }

        private void RestorePlayerRenderers()
        {
            for (int index = 0;
                 index < playerRenderers.Length;
                 index++)
            {
                if (playerRenderers[index] != null)
                {
                    playerRenderers[index].enabled =
                        playerRendererStates[index];
                }
            }
        }

        private void RestoreBossRenderers()
        {
            for (int index = 0;
                 index < bossRenderers.Length;
                 index++)
            {
                if (bossRenderers[index] != null)
                {
                    bossRenderers[index].enabled =
                        bossRendererStates[index];
                }
            }
        }

        private void HideBossPresentation()
        {
            StopBossFlicker();
            Renderer[] renderers =
                boss.GetComponentsInChildren<
                    Renderer>(true);
            for (int index = 0;
                 index < renderers.Length;
                 index++)
            {
                if (renderers[index] != null)
                {
                    renderers[index].enabled = false;
                }
            }
            Collider[] colliders =
                boss.GetComponentsInChildren<
                    Collider>(true);
            for (int index = 0;
                 index < colliders.Length;
                 index++)
            {
                if (colliders[index] != null)
                {
                    colliders[index].enabled = false;
                }
            }
        }

        private bool ShowBossMessage(string text)
        {
            message = WordNoteUI.Instance.RequestTimeline(
                text,
                BuildGlitchStyle());
            return message != null;
        }

        private bool ShowPlayerMessage()
        {
            message = WordNoteUI.Instance.RequestTimeline(
                playerLine,
                BuildTypewriterStyle());
            return message != null;
        }

        private WordNoteStyle BuildBaseStyle()
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
                participateInStackLayout = true,
                displayMode =
                    WordNoteDisplayMode.Timed,
                displayDuration =
                    Mathf.Max(0f, messageDisplaySeconds),
                useEnterAnimation = true,
                useExitAnimation = true
            };
        }

        private WordNoteStyle BuildGlitchStyle()
        {
            WordNoteStyle style = BuildBaseStyle();
            style.enterAnimation =
                WordNoteAnimation.Glitch;
            style.exitAnimation =
                WordNoteAnimation.Glitch;
            style.enterMotion =
                BuildGlitchMotion();
            style.exitMotion =
                BuildGlitchMotion();
            return style;
        }

        private WordNoteStyle BuildTypewriterStyle()
        {
            WordNoteStyle style = BuildBaseStyle();
            style.enterAnimation =
                WordNoteAnimation.Typewriter;
            style.exitAnimation =
                WordNoteAnimation.Typewriter;
            style.enterMotion =
                BuildTypewriterMotion();
            style.exitMotion =
                BuildTypewriterMotion();
            return style;
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

        private WordNoteMotionSettings BuildTypewriterMotion()
        {
            return new WordNoteMotionSettings
            {
                useCombinedChannels = true,
                useFade = false,
                useTypewriter = true,
                typewriterCharactersPerSecond =
                    Mathf.Max(
                        1f,
                        typewriterCharactersPerSecond),
                duration = .01f,
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

        private float CameraProgress(float time)
        {
            float duration = mode ==
                             BossDeathCinematicMode.Execution
                ? executionCameraMoveSeconds
                : naturalCameraMoveSeconds;
            return Mathf.Clamp01(
                (time - phaseStartTime) /
                Mathf.Max(.01f, duration));
        }

        private Vector3 ResolveBossCameraPosition()
        {
            return boss.WorldPosition +
                   frame.ToWorldOffset(
                       bossCameraOffset);
        }

        private Vector3 ResolvePlayerCameraPosition()
        {
            return player.transform.position +
                   frame.ToWorldOffset(
                       playerCameraOffset);
        }

        private void ApplyCamera(Vector3 position)
        {
            if (rig == null)
            {
                return;
            }
            rig.SetShotLens(
                baseOrthographicSize,
                baseFieldOfView);
            rig.SetShotTransform(
                position,
                Quaternion.Euler(
                    0f,
                    rig.PlanarYaw,
                    0f));
        }

        private static float Smooth(float progress)
        {
            return Mathf.SmoothStep(
                0f,
                1f,
                Mathf.Clamp01(progress));
        }

        private static Vector3 QuadraticBezier(
            Vector3 start,
            Vector3 control,
            Vector3 end,
            float progress)
        {
            float inverse = 1f - progress;
            return inverse * inverse * start +
                   2f * inverse * progress * control +
                   progress * progress * end;
        }

        private List<GameObject> BuildKeyPrefabOrder()
        {
            var result = new List<GameObject>();
            int count = Mathf.Max(1, copiesPerColor);
            for (int copy = 0;
                 copy < count;
                 copy++)
            {
                if (redKeyPrefab != null)
                {
                    result.Add(redKeyPrefab);
                }
                if (blueKeyPrefab != null)
                {
                    result.Add(blueKeyPrefab);
                }
                if (greenKeyPrefab != null)
                {
                    result.Add(greenKeyPrefab);
                }
            }
            return result;
        }

        private static void DisableKeyGameplay(
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
                bodies[index].isKinematic = true;
            }
            ColorKeyPickup[] pickups =
                instance.GetComponentsInChildren<
                    ColorKeyPickup>(true);
            for (int index = 0;
                 index < pickups.Length;
                 index++)
            {
                pickups[index].enabled = false;
            }
            DropItemBase[] drops =
                instance.GetComponentsInChildren<
                    DropItemBase>(true);
            for (int index = 0;
                 index < drops.Length;
                 index++)
            {
                drops[index].enabled = false;
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
            PlayableDirector[] directors =
                instance.GetComponentsInChildren<
                    PlayableDirector>(true);
            for (int index = 0;
                 index < directors.Length;
                 index++)
            {
                directors[index].Stop();
                directors[index].enabled = false;
            }
            MechanismArtworkPresentation[] artworks =
                instance.GetComponentsInChildren<
                    MechanismArtworkPresentation>(true);
            for (int index = 0;
                 index < artworks.Length;
                 index++)
            {
                artworks[index].enabled = false;
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

        private static void ApplyKeySorting(
            GameObject instance)
        {
            int sortingLayerId =
                ResolveSortingLayer("BossTop");
            SpriteRenderer[] sprites =
                instance.GetComponentsInChildren<
                    SpriteRenderer>(true);
            for (int index = 0;
                 index < sprites.Length;
                 index++)
            {
                if (sortingLayerId != 0)
                {
                    sprites[index].sortingLayerID =
                        sortingLayerId;
                }
                sprites[index].sortingOrder = 32760;
            }
        }

        private static int ResolveSortingLayer(
            string layerName)
        {
            SortingLayer[] layers =
                SortingLayer.layers;
            for (int index = 0;
                 index < layers.Length;
                 index++)
            {
                if (layers[index].name == layerName)
                {
                    return layers[index].id;
                }
            }
            return 0;
        }

        private void DestroyKeys()
        {
            for (int index = 0;
                 index < keys.Count;
                 index++)
            {
                KeyState key = keys[index];
                if (key.gameObject != null)
                {
                    DestroyObject(key.gameObject);
                }
            }
            keys.Clear();
            if (keyRoot != null)
            {
                DestroyObject(keyRoot);
                keyRoot = null;
            }
        }

        private void DestroyBossObject()
        {
            if (boss == null || bossDestroyed)
            {
                return;
            }
            bossDestroyed = true;
            GameObject target = boss.gameObject;
            DestroyObject(target);
            boss = null;
        }

        private static void DestroyObject(
            UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(target);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
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

            if (rig != null &&
                rig.IsTimelineControlling)
            {
                rig.ReleaseTimelineControl(true);
            }
            rig = null;
            cameraManager = null;
        }

        private void FailAndStop(string reason)
        {
            if (!failed)
            {
                failed = true;
                Debug.LogError(
                    "[BossDeath] " + reason,
                    director);
            }
            director?.Stop();
        }

        private void Cleanup()
        {
            if (boss != null)
            {
                boss.Damaged -= HandleBossDamaged;
                boss.Died -= HandleBossDied;
            }
            StopBossFlicker();
            RestoreBossRenderers();
            RestorePlayerRenderers();
            ReleaseCamera();
            DestroyKeys();

            if (message != null)
            {
                message.ForceDisappear();
                message.Release(false);
                message = null;
            }

            if (!completed && boss != null)
            {
                boss.SetPlayerContactKillEnabled(
                    previousPlayerContactKillEnabled);
                presenter?.SetDefeatPresentationSuppressed(
                    false);
                bossMover?.SetExecutionSuspended(false);
                if (mode ==
                    BossDeathCinematicMode.Execution)
                {
                    boss.EndExecutionSequence();
                }
            }

            boss = null;
            player = null;
            bossAnimation = null;
            bossMover = null;
            presenter = null;
            bossRenderers = Array.Empty<Renderer>();
            bossRendererStates = Array.Empty<bool>();
            playerRenderers = Array.Empty<Renderer>();
            playerRendererStates = Array.Empty<bool>();
            frame = default;
            initialized = false;
            failed = false;
            phase = Phase.WaitStart;
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
