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
        [SerializeField] private float rightMoveHeightY = -42f;
        [SerializeField] private bool waitForBossMotionEnabled = true;
        [SerializeField, Min(0f)] private float startDelaySeconds;

        private BossController controller;
        private float readyTime;
        private bool applyingMotion;

        public BossController Controller => controller;
        public bool IsMoving => applyingMotion;
        public float RightMoveHeightY => rightMoveHeightY;

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

            controller.SetExternalMove(1f);
            controller.SetExternalHeight(rightMoveHeightY);
            controller.SetPassThroughTerrain(true);
            applyingMotion = true;
        }

        private void OnDisable()
        {
            StopOwnedMotion();
        }

        public void SetMoving(bool value)
        {
            moving = value;
            if (!value)
            {
                StopOwnedMotion();
            }
        }

        public void SetRightMoveHeightY(float value)
        {
            rightMoveHeightY = value;
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
        }
    }
}
