using System;
using Project.BlockFeatures;
using Project.Player;
using UnityEngine;

namespace Project.BossLogic
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BossController))]
    [AddComponentMenu("2026TapTap/Boss/右移马夫")]
    public sealed class BossRightMoveExecutor : MonoBehaviour
    {
        [Header("右移路线")]
        [SerializeField] private bool moving = true;
        [SerializeField] private bool waitForBossMotionEnabled = true;
        [SerializeField, Min(0f)] private float startDelaySeconds;
        [SerializeField, Range(0f, 1f)]
        private float invulnerableMoveSpeedMultiplier = .7f;

        [Header("玩家复活暂停")]
        [SerializeField] private bool pauseWhilePlayerRespawning = true;
        [SerializeField] private string playerTag = "Player";

        [Header("弱垂直追踪")]
        [SerializeField] private bool enableWeakVerticalTracking = true;
        [SerializeField, Min(0f)]
        private float verticalDeadbandBlocks = 2f;
        [SerializeField, Min(0f)]
        private float verticalTrackSpeedBlocksPerSecond = .18f;

        private BossController controller;
        private PlayerController player;
        private bool playerRespawnPaused;
        private float nextPlayerLookupTime;
        private bool checkpointAssistActive;
        private bool executionSuspended;
        private float checkpointAssistSpeedMultiplier = 1f;
        private float checkpointAssistTargetX;
        private float checkpointAssistTargetY;
        private float checkpointAssistUntil;
        private float checkpointAssistVerticalSpeed;
        private float checkpointAssistTolerance = .25f;
        private float readyTime;
        private float activeRightMoveHeightY;
        private bool hasActiveRightMoveHeight;
        private bool applyingMotion;

        public BossController Controller => controller;
        public bool IsMoving => applyingMotion;
        /// <summary>Presentation boundary only; published after the executor's real motion ownership changes.</summary>
        public event Action<bool> MotionStateChanged;
        public float RightMoveHeightY =>
            hasActiveRightMoveHeight
                ? activeRightMoveHeightY
                : controller != null
                    ? controller.transform.position.y
                    : 0f;
        public float InvulnerableMoveSpeedMultiplier =>
            invulnerableMoveSpeedMultiplier;
        public bool IsPausedForPlayerRespawn =>
            playerRespawnPaused;
        public bool IsWeakVerticalTrackingEnabled =>
            enableWeakVerticalTracking;
        public float VerticalDeadbandBlocks =>
            verticalDeadbandBlocks;
        public float VerticalTrackSpeedBlocksPerSecond =>
            verticalTrackSpeedBlocksPerSecond;
        public bool IsCheckpointAssistActive =>
            checkpointAssistActive;
        public bool IsExecutionSuspended =>
            executionSuspended;

        private void Awake()
        {
            controller = GetComponent<BossController>();
            // Existing authored bosses gain presentation without rewriting scenes or motion settings.
            if (GetComponent<BossRgbFearPresentation>() == null)
                gameObject.AddComponent<BossRgbFearPresentation>();
        }

        private void OnEnable()
        {
            if (controller == null)
            {
                controller = GetComponent<BossController>();
            }

            controller.StateChanged -= OnBossStateChanged;
            controller.StateChanged += OnBossStateChanged;
            controller.RespawnStateApplied -=
                OnBossRespawnStateApplied;
            controller.RespawnStateApplied +=
                OnBossRespawnStateApplied;
            ResolvePlayer();
            BindPlayer();
            readyTime = Time.time + Mathf.Max(0f, startDelaySeconds);
        }

        private void FixedUpdate()
        {
            if (controller == null || controller.IsDead)
            {
                StopOwnedMotion();
                return;
            }

            if (executionSuspended)
            {
                StopOwnedMotion();
                return;
            }

            ResolvePlayerIfNeeded();
            if (pauseWhilePlayerRespawning)
            {
                bool playerUnavailable =
                    player != null &&
                    (player.IsDead ||
                     player.IsRespawning);
                if (playerRespawnPaused ||
                    playerUnavailable)
                {
                    StopOwnedMotion();
                    return;
                }
            }

            bool motionEnabled =
                !waitForBossMotionEnabled ||
                controller.TrackingEnabled;
            if (!moving ||
                !motionEnabled ||
                Time.time < readyTime)
            {
                StopOwnedMotion();
                return;
            }

            if (!hasActiveRightMoveHeight)
            {
                activeRightMoveHeightY =
                    controller.transform.position.y;
                hasActiveRightMoveHeight = true;
            }

            UpdateCheckpointAssist(Time.fixedDeltaTime);
            UpdateWeakVerticalTarget(
                Time.fixedDeltaTime);
            float speedMultiplier =
                controller.IsInvulnerable
                    ? invulnerableMoveSpeedMultiplier
                    : 1f;
            if (checkpointAssistActive &&
                controller.transform.position.x <
                checkpointAssistTargetX -
                checkpointAssistTolerance)
            {
                speedMultiplier *=
                    checkpointAssistSpeedMultiplier;
            }

            controller.SetExternalMove(speedMultiplier);
            controller.SetExternalHeight(activeRightMoveHeightY);
            controller.SetPassThroughTerrain(true);
            if (!applyingMotion)
            {
                applyingMotion = true;
                MotionStateChanged?.Invoke(true);
            }
        }

        private void OnDisable()
        {
            if (controller != null)
            {
                controller.StateChanged -= OnBossStateChanged;
                controller.RespawnStateApplied -=
                    OnBossRespawnStateApplied;
            }

            UnbindPlayer();
            StopOwnedMotion();
        }

        private void OnBossStateChanged(
            BossStateId previous,
            BossStateId next)
        {
            if (next == BossStateId.Death)
            {
                ClearCheckpointAssist();
                StopOwnedMotion();
            }
        }

        private void OnBossRespawnStateApplied(
            float health,
            Vector3 position)
        {
            if (!hasActiveRightMoveHeight &&
                !applyingMotion)
            {
                return;
            }

            activeRightMoveHeightY = position.y;
            hasActiveRightMoveHeight = true;
            ClearCheckpointAssist();
        }

        private void ResolvePlayerIfNeeded()
        {
            if (player != null ||
                Time.unscaledTime < nextPlayerLookupTime)
            {
                return;
            }

            bool wasMissing = player == null;
            ResolvePlayer();
            if (wasMissing && player != null)
            {
                BindPlayer();
            }
        }

        private void ResolvePlayer()
        {
            nextPlayerLookupTime =
                Time.unscaledTime + .25f;
            player =
                ProjectDiscovery.FindFirstTagged<
                    PlayerController>(playerTag);
            if (player == null)
            {
                return;
            }

            playerRespawnPaused =
                player.IsDead ||
                player.IsRespawning;
        }

        private void BindPlayer()
        {
            if (player == null)
            {
                return;
            }

            player.PlayerDied -= OnPlayerDied;
            player.PlayerDied += OnPlayerDied;
            player.PlayerRespawnCompleted -=
                OnPlayerRespawnCompleted;
            player.PlayerRespawnCompleted +=
                OnPlayerRespawnCompleted;
        }

        private void UnbindPlayer()
        {
            if (player == null)
            {
                return;
            }

            player.PlayerDied -= OnPlayerDied;
            player.PlayerRespawnCompleted -=
                OnPlayerRespawnCompleted;
        }

        private void OnPlayerDied()
        {
            if (!pauseWhilePlayerRespawning)
            {
                return;
            }

            playerRespawnPaused = true;
            ClearCheckpointAssist();
            StopOwnedMotion();
        }

        private void OnPlayerRespawnCompleted()
        {
            if (!pauseWhilePlayerRespawning)
            {
                return;
            }

            playerRespawnPaused = false;
            readyTime = Time.time;
        }

        public void BeginCheckpointAssist(
            Vector3 targetPosition,
            float speedMultiplier,
            float maximumDurationSeconds,
            float verticalSpeedBlocksPerSecond,
            float horizontalToleranceBlocks)
        {
            checkpointAssistSpeedMultiplier =
                Mathf.Max(1f, speedMultiplier);
            checkpointAssistTargetX =
                targetPosition.x;
            checkpointAssistTargetY =
                targetPosition.y;
            checkpointAssistUntil =
                Time.time +
                Mathf.Max(.01f, maximumDurationSeconds);
            checkpointAssistVerticalSpeed =
                Mathf.Max(
                    0f,
                    verticalSpeedBlocksPerSecond);
            checkpointAssistTolerance =
                Mathf.Max(0f, horizontalToleranceBlocks);
            checkpointAssistActive = true;
            if (!hasActiveRightMoveHeight)
            {
                activeRightMoveHeightY =
                    controller != null
                        ? controller.transform.position.y
                        : checkpointAssistTargetY;
                hasActiveRightMoveHeight = true;
            }
        }

        private void UpdateCheckpointAssist(
            float deltaTime)
        {
            if (!checkpointAssistActive ||
                controller == null)
            {
                return;
            }

            float cellSize =
                GridCellSizeUtility.Resolve(controller);
            float verticalStep =
                checkpointAssistVerticalSpeed *
                cellSize *
                Mathf.Max(0f, deltaTime);
            activeRightMoveHeightY =
                Mathf.MoveTowards(
                    activeRightMoveHeightY,
                    checkpointAssistTargetY,
                    verticalStep);
            float horizontalRemaining =
                checkpointAssistTargetX -
                controller.transform.position.x;
            bool horizontalReached =
                horizontalRemaining <=
                checkpointAssistTolerance;
            bool verticalReached =
                Mathf.Abs(
                    activeRightMoveHeightY -
                    checkpointAssistTargetY) <=
                checkpointAssistTolerance;
            if (horizontalReached &&
                verticalReached ||
                Time.time >= checkpointAssistUntil)
            {
                ClearCheckpointAssist();
            }
        }

        private void ClearCheckpointAssist()
        {
            checkpointAssistActive = false;
            checkpointAssistSpeedMultiplier = 1f;
            checkpointAssistVerticalSpeed = 0f;
        }

        private void UpdateWeakVerticalTarget(
            float deltaTime)
        {
            if (!enableWeakVerticalTracking ||
                player == null ||
                checkpointAssistActive)
            {
                return;
            }

            float cellSize =
                GridCellSizeUtility.Resolve(controller);
            float deadband =
                Mathf.Max(0f, verticalDeadbandBlocks) *
                cellSize;
            float playerY =
                player.transform.position.y;
            float difference =
                playerY - activeRightMoveHeightY;
            if (Mathf.Abs(difference) <= deadband)
            {
                return;
            }

            float targetY = playerY -
                            Mathf.Sign(difference) *
                            deadband;
            float maxStep =
                Mathf.Max(
                    0f,
                    verticalTrackSpeedBlocksPerSecond) *
                cellSize *
                Mathf.Max(0f, deltaTime);
            activeRightMoveHeightY = Mathf.MoveTowards(
                activeRightMoveHeightY,
                targetY,
                maxStep);
        }

        public void SetMoving(bool value)
        {
            moving = value;
            if (!value)
            {
                StopOwnedMotion();
            }
        }

        public void SetExecutionSuspended(bool suspended)
        {
            executionSuspended = suspended;
            if (suspended)
            {
                ClearCheckpointAssist();
                StopOwnedMotion();
            }
        }

        private void StopOwnedMotion()
        {
            if (!applyingMotion)
            {
                return;
            }

            if (controller != null)
            {
                controller.ClearExternalMotion();
                controller.ClearExternalHeight();
                controller.SetPassThroughTerrain(false);
            }

            applyingMotion = false;
            hasActiveRightMoveHeight = false;
            MotionStateChanged?.Invoke(false);
        }
    }
}
