using Project.CameraModes;
using UnityEngine;

namespace Project.BossLogic
{
    [DisallowMultipleComponent]
    public sealed class BossCameraSize : MonoBehaviour
    {
        [Header("视野申请")]
        [SerializeField, Min(1f)]
        [Tooltip("玩法相机未处于 Timeline 控制时使用的正交视野倍率。")]
        private float sizeMultiplier = 2f;

        [SerializeField, Min(0f)]
        [Tooltip("扩大与恢复视野的平滑时间，单位秒。")]
        private float blendDuration = 1f;

        [SerializeField]
        [Tooltip("开启后，组件启用时自动提交视野申请。默认关闭，由 Timeline 或外部 API 开启。")]
        private bool requestOnEnable;

        private BossController boss;
        private CameraControlManager cameraManager;
        private CameraLensRequestHandle requestHandle;
        private bool requestRequested;

        public bool IsRequested => requestRequested;
        public float SizeMultiplier => sizeMultiplier;
        public float BlendDuration => blendDuration;
        public bool HasValidLensRequest =>
            requestHandle.IsValid;

        private void Awake()
        {
            EnsureBoss();
        }

        private void OnEnable()
        {
            EnsureBoss();
            if (requestOnEnable || requestRequested)
            {
                RequestEnable();
            }
        }

        private void OnDisable()
        {
            ReleaseRequest(CameraTransition.Immediate);
        }

        /// <summary>
        /// Requests the configured gameplay-only camera size multiplier.
        /// Repeated calls keep the existing request instead of stacking it.
        /// </summary>
        public void RequestEnable()
        {
            EnsureBoss();
            if (boss != null && boss.IsDead)
            {
                return;
            }

            EnsureCameraManager();
            if (cameraManager == null)
            {
                Debug.LogWarning(
                    "[BossCameraSize] 场景中找不到 CameraControlManager，无法申请视野倍率。",
                    this);
                return;
            }

            CameraTransition transition =
                CameraTransition.Ease(blendDuration);
            if (requestHandle.IsValid)
            {
                requestHandle.Set(
                    sizeMultiplier,
                    transition);
                requestRequested = true;
                return;
            }

            requestHandle =
                cameraManager.RequestLensMultiplier(
                    this,
                    sizeMultiplier,
                    transition);
            requestRequested = requestHandle.IsValid;
        }

        /// <summary>
        /// Releases the request with the configured blend duration.
        /// Boss death presentation invokes this when the camera starts returning
        /// to the player; ordinary player respawns leave the request untouched.
        /// </summary>
        public void RequestDisable()
        {
            requestRequested = false;
            ReleaseRequest(
                CameraTransition.Ease(blendDuration));
        }

        public void SetRequested(bool requested)
        {
            if (requested)
            {
                RequestEnable();
            }
            else
            {
                RequestDisable();
            }
        }

        private void EnsureBoss()
        {
            if (boss == null)
            {
                boss = GetComponent<BossController>();
            }
        }

        private void EnsureCameraManager()
        {
            if (cameraManager != null)
            {
                return;
            }

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
        }

        private void ReleaseRequest(
            CameraTransition transition)
        {
            if (!requestHandle.IsValid)
            {
                requestHandle = default;
                return;
            }

            requestHandle.Release(transition);
            requestHandle = default;
        }
    }
}
