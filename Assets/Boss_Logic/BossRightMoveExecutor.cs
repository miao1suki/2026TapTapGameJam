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

        private BossController controller;
        private float readyTime;
        private float activeRightMoveHeightY;
        private bool hasActiveRightMoveHeight;
        private bool applyingMotion;

        public BossController Controller => controller;
        public bool IsMoving => applyingMotion;
        public float RightMoveHeightY =>
            hasActiveRightMoveHeight
                ? activeRightMoveHeightY
                : controller != null
                    ? controller.transform.position.y
                    : 0f;
        public float InvulnerableMoveSpeedMultiplier =>
            invulnerableMoveSpeedMultiplier;

        private void Awake()
        {
            controller = GetComponent<BossController>();
        }

        private void OnEnable()
        {
            if (controller == null)
            {
                controller = GetComponent<BossController>();
            }

            controller.StateChanged -= OnBossStateChanged;
            controller.StateChanged += OnBossStateChanged;
            readyTime = Time.time + Mathf.Max(0f, startDelaySeconds);
        }

        private void FixedUpdate()
        {
            if (controller == null || controller.IsDead)
            {
                StopOwnedMotion();
                return;
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

            controller.SetExternalMove(
                controller.IsInvulnerable
                    ? invulnerableMoveSpeedMultiplier
                    : 1f);
            if (!hasActiveRightMoveHeight)
            {
                activeRightMoveHeightY =
                    controller.transform.position.y;
                hasActiveRightMoveHeight = true;
            }

            controller.SetExternalHeight(activeRightMoveHeightY);
            controller.SetPassThroughTerrain(true);
            applyingMotion = true;
        }

        private void OnDisable()
        {
            if (controller != null)
            {
                controller.StateChanged -= OnBossStateChanged;
            }

            StopOwnedMotion();
        }

        private void OnBossStateChanged(
            BossStateId previous,
            BossStateId next)
        {
            if (next == BossStateId.Death)
            {
                StopOwnedMotion();
            }
        }

        public void SetMoving(bool value)
        {
            moving = value;
            if (!value)
            {
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
        }
    }
}
