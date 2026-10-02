using UnityEngine;

namespace Project.CameraModes
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(500)]
    [RequireComponent(typeof(CameraControlManager))]
    public sealed class CameraFollowController :
        MonoBehaviour,
        ICameraViewModeRequester
    {
        [SerializeField]
        [Tooltip("统一管理相机焦点与输出的组件。留空时从当前物体查找。")]
        private CameraControlManager cameraManager;

        [SerializeField]
        [Tooltip("用于读取当前 2D/3D 模式。留空时从当前物体查找。")]
        private CameraModeController modeController;

        [SerializeField]
        [Tooltip("玩法相机的跟随目标。")]
        private Transform target;

        [Header("跟随模式")]
        [SerializeField]
        [HideInInspector]
        [Tooltip("找不到 CameraModeController 时使用的备用模式。")]
        private CameraViewMode fallbackMode = CameraViewMode.Side2D;

        [SerializeField]
        [HideInInspector]
        private CameraViewMode requestedMode = CameraViewMode.Perspective3D;

        [SerializeField]
        [HideInInspector]
        private float requestedSide2DYawDegrees;

        [SerializeField]
        private bool requestOnEnable = true;

        [Header("2D 跟随")]
        [SerializeField]
        [Tooltip("2D 模式下相对目标的焦点偏移。")]
        private Vector3 side2DOffset = new Vector3(0f, 1f, 0f);

        [Header("3D 跟随")]
        [SerializeField]
        [Tooltip("3D 模式下相对目标的焦点偏移。")]
        private Vector3 perspective3DOffset = new Vector3(0f, 1f, 0f);

        [Header("平滑参数")]
        [SerializeField, Min(0f)]
        [Tooltip("焦点跟随的平滑时间，越大越慢。")]
        private float damping = 0.12f;

        [SerializeField]
        [Tooltip("目标在死区内移动时不更新焦点。")]
        private Vector2 deadZone = Vector2.zero;

        [SerializeField]
        [Tooltip("根据目标速度在移动方向上的额外观察提前量。")]
        private Vector2 lookAhead;

        [SerializeField, Min(0f)]
        [Tooltip("目标跳变超过该距离时立即吸附焦点，避免相机长距离追赶。")]
        private float teleportDistance = 8f;

        private Vector3 currentFocus;
        private Vector3 velocity;
        private Vector3 previousTargetPosition;
        private bool hasFocus;
        private CameraViewModeRequestHandle requestedModeHandle;

        public Transform Target => target;
        public Vector3 FocusPoint => currentFocus;
        public CameraViewMode FollowMode => ResolveMode();
        public CameraViewMode RequestedMode => requestedMode;
        public float RequestedSide2DYawDegrees =>
            requestedSide2DYawDegrees;
        public CameraModeController ModeController => modeController;
        public string CameraModeRequesterName =>
            "Camera Follow Controller";
        public int CameraModeRequestPriority =>
            CameraControlPriorities.Gameplay;

        private void Reset()
        {
            cameraManager = GetComponent<CameraControlManager>();
            modeController = GetComponent<CameraModeController>();
        }

        private void Awake()
        {
            ResolveReferences();
            currentFocus = ResolveDesiredFocus();
            previousTargetPosition = target != null ? target.position : Vector3.zero;
            hasFocus = target != null;
        }

        private void OnEnable()
        {
            ResolveReferences();
            if (requestOnEnable && modeController != null)
            {
                if (requestedMode == CameraViewMode.Side2D)
                {
                    SetRequestedMode(
                        requestedMode,
                        requestedSide2DYawDegrees);
                }
                else
                {
                    SetRequestedMode(requestedMode);
                }
            }
        }

        private void OnDisable()
        {
            if (requestedModeHandle.IsValid)
            {
                requestedModeHandle.Release(true);
            }
            requestedModeHandle = default;
        }

        private void LateUpdate()
        {
            if (cameraManager == null || target == null)
            {
                return;
            }

            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f)
            {
                return;
            }

            Vector3 desiredFocus = ResolveDesiredFocus();
            Vector3 targetDelta = target.position - previousTargetPosition;
            previousTargetPosition = target.position;
            desiredFocus += new Vector3(
                targetDelta.x * lookAhead.x,
                targetDelta.y * lookAhead.y,
                0f);

            if (!hasFocus || Vector3.Distance(desiredFocus, currentFocus) > teleportDistance)
            {
                currentFocus = desiredFocus;
                velocity = Vector3.zero;
                hasFocus = true;
            }
            else
            {
                Vector3 delta = desiredFocus - currentFocus;
                if (Mathf.Abs(delta.x) <= deadZone.x)
                {
                    delta.x = 0f;
                }
                if (Mathf.Abs(delta.y) <= deadZone.y)
                {
                    delta.y = 0f;
                }

                desiredFocus = currentFocus + delta;
                currentFocus = damping <= 0f
                    ? desiredFocus
                    : Vector3.SmoothDamp(
                        currentFocus,
                        desiredFocus,
                        ref velocity,
                        damping,
                        Mathf.Infinity,
                        deltaTime);
            }

            cameraManager.SetFocusPoint(currentFocus);
        }

        public void Configure(
            CameraControlManager manager,
            CameraModeController controller,
            Transform followTarget)
        {
            cameraManager = manager;
            modeController = controller;
            SetTarget(followTarget, true);
        }

        public void SetTarget(Transform followTarget, bool snap = false)
        {
            target = followTarget;
            previousTargetPosition = target != null ? target.position : Vector3.zero;
            if (snap && target != null)
            {
                currentFocus = ResolveDesiredFocus();
                velocity = Vector3.zero;
                hasFocus = true;
                cameraManager?.SetFocusPoint(currentFocus, true);
            }
        }

        public void SetFallbackMode(CameraViewMode viewMode, bool snap = false)
        {
            if (modeController != null)
            {
                SetRequestedMode(viewMode, snap);
                return;
            }

            fallbackMode = viewMode;
            if (snap)
            {
                Snap();
            }
        }

        public void SetRequestedMode(
            CameraViewMode mode,
            bool immediate = false)
        {
            SetRequestedMode(
                mode,
                requestedSide2DYawDegrees,
                false,
                immediate);
        }

        public void SetRequestedMode(
            CameraViewMode mode,
            float side2DYawDegrees,
            bool immediate = false)
        {
            requestedSide2DYawDegrees = side2DYawDegrees;
            SetRequestedMode(
                mode,
                side2DYawDegrees,
                true,
                immediate);
        }

        public void AdjustRequestedSide2DYaw(
            float deltaDegrees,
            bool immediate = false)
        {
            requestedSide2DYawDegrees = Mathf.Repeat(
                requestedSide2DYawDegrees + deltaDegrees + 180f,
                360f) - 180f;
            SetRequestedMode(
                CameraViewMode.Side2D,
                requestedSide2DYawDegrees,
                true,
                immediate);
        }

        public void TurnRequestedLeft90(bool immediate = false)
        {
            AdjustRequestedSide2DYaw(-90f, immediate);
        }

        public void TurnRequestedRight90(bool immediate = false)
        {
            AdjustRequestedSide2DYaw(90f, immediate);
        }

        private void SetRequestedMode(
            CameraViewMode mode,
            float side2DYawDegrees,
            bool overrideSide2DYaw,
            bool immediate)
        {
            CameraTransition transition = immediate
                ? CameraTransition.Immediate
                : modeController != null
                    ? modeController.ModeTransition
                    : CameraTransition.Immediate;
            SetRequestedMode(
                mode,
                side2DYawDegrees,
                overrideSide2DYaw,
                transition);
        }

        public void SetRequestedMode(
            CameraViewMode mode,
            CameraTransition transition)
        {
            SetRequestedMode(
                mode,
                requestedSide2DYawDegrees,
                false,
                transition);
        }

        public void SetRequestedMode(
            CameraViewMode mode,
            float side2DYawDegrees,
            CameraTransition transition)
        {
            requestedSide2DYawDegrees = side2DYawDegrees;
            SetRequestedMode(
                mode,
                side2DYawDegrees,
                true,
                transition);
        }

        private void SetRequestedMode(
            CameraViewMode mode,
            float side2DYawDegrees,
            bool overrideSide2DYaw,
            CameraTransition transition)
        {
            requestedMode = mode;
            ResolveReferences();
            if (modeController == null)
            {
                fallbackMode = mode;
                if (transition.duration <= 0f)
                {
                    Snap();
                }
                return;
            }

            CameraViewModeRequestHandle previousHandle =
                requestedModeHandle;
            requestedModeHandle =
                overrideSide2DYaw
                    ? modeController.RequestMode(
                        this,
                        mode,
                        side2DYawDegrees,
                        transition)
                    : modeController.RequestMode(
                        this,
                        mode,
                        transition);
            if (previousHandle.IsValid)
            {
                previousHandle.Release(
                    transition.duration <= 0f);
            }
            if (transition.duration <= 0f)
            {
                Snap();
            }
        }

        public CameraViewModeRequestHandle RequestViewMode(
            CameraViewMode mode,
            bool immediate = false)
        {
            ResolveReferences();
            return modeController != null
                ? modeController.RequestMode(this, mode, immediate)
                : default;
        }

        public CameraViewModeRequestHandle RequestViewMode(
            CameraViewMode mode,
            float side2DYawDegrees,
            bool immediate = false)
        {
            ResolveReferences();
            return modeController != null
                ? modeController.RequestMode(
                    this,
                    mode,
                    side2DYawDegrees,
                    immediate)
                : default;
        }

        public CameraViewModeRequestHandle RequestViewMode(
            CameraViewMode mode,
            CameraTransition transition)
        {
            ResolveReferences();
            return modeController != null
                ? modeController.RequestMode(
                    this,
                    mode,
                    transition)
                : default;
        }

        public CameraViewModeRequestHandle RequestViewMode(
            CameraViewMode mode,
            float side2DYawDegrees,
            CameraTransition transition)
        {
            ResolveReferences();
            return modeController != null
                ? modeController.RequestMode(
                    this,
                    mode,
                    side2DYawDegrees,
                    transition)
                : default;
        }

        public void Snap()
        {
            if (target == null)
            {
                return;
            }

            currentFocus = ResolveDesiredFocus();
            velocity = Vector3.zero;
            hasFocus = true;
            cameraManager?.SetFocusPoint(currentFocus, true);
        }

        private CameraViewMode ResolveMode()
        {
            return modeController != null
                ? modeController.CurrentMode
                : fallbackMode;
        }

        private Vector3 ResolveDesiredFocus()
        {
            if (target == null)
            {
                return currentFocus;
            }

            Vector3 offset = ResolveMode() == CameraViewMode.Side2D
                ? side2DOffset
                : perspective3DOffset;
            return target.position + offset;
        }

        private void ResolveReferences()
        {
            if (cameraManager == null)
            {
                cameraManager = GetComponent<CameraControlManager>();
            }
            if (modeController == null)
            {
                modeController = GetComponent<CameraModeController>();
            }
        }
    }
}
