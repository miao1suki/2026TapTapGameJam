using System;
using System.Collections.Generic;
using Project.InputAbstraction;
using UnityEngine;
using UnityEngine.Events;

namespace Project.CameraModes
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CameraControlManager))]
    [DefaultExecutionOrder(100)]
    public sealed class CameraModeController :
        MonoBehaviour,
        IAchievementSignalProvider,
        ICameraControlSource,
        ICameraViewModeAuthority
    {
        private static readonly string[] AchievementSignals =
        {
            AchievementSignalIds.CameraModeChanged,
            AchievementSignalIds.CameraTransitionStarted,
            AchievementSignalIds.CameraTransitionCompleted,
            AchievementSignalIds.CameraFaceFront,
            AchievementSignalIds.CameraFaceRight,
            AchievementSignalIds.CameraFaceBack,
            AchievementSignalIds.CameraFaceLeft
        };

        private sealed class ModeRequest
        {
            public int id;
            public long order;
            public int priority;
            public CameraViewMode mode;
            public ICameraViewModeRequester requester;
            public bool overrideSide2DYaw;
            public float side2DYawDegrees;
            public CameraTransition transition;
        }

        [Serializable]
        public sealed class Side2DSettings
        {
            [Tooltip("相机沿世界 Z 轴后退的距离。")]
            [Min(0.01f)] public float distance = 10f;

            [Tooltip("正交相机的可视高度参数。")]
            [Min(0.01f)] public float orthographicSize = 5f;

            [Tooltip("相对焦点的竖直观察偏移。不会造成左右偏移。")]
            public float verticalOffset = 1f;

            [Tooltip("相对焦点的纵深观察偏移。")]
            public float depthOffset;

            [Tooltip("2D 平面朝向。0=看向世界 +Z，90=向右转过一个面。")]
            public float yawDegrees;
        }

        [Serializable]
        public sealed class Perspective3DSettings
        {
            [Tooltip("相机到观察中心的直线距离。")]
            [Min(0.01f)] public float distance = 12f;

            [Tooltip("透视相机的俯仰角。可由 RotatePerspective 或 SetPerspectiveAngles 动态调整。")]
            [Range(1f, 80f)] public float pitch = 35f;

            [Tooltip("透视相机垂直视野角。")]
            [Range(1f, 179f)] public float fieldOfView = 50f;

            [Tooltip("相对焦点的竖直观察偏移。不会造成左右偏移。")]
            public float verticalOffset = 1f;

            [Tooltip("相对焦点的纵深观察偏移。")]
            public float depthOffset;

            [Tooltip("3D 环绕视角的水平朝向。")]
            public float yawDegrees;
        }

        [Serializable]
        public sealed class TransitionSettings
        {
            [Tooltip("2D/3D 模式切换时间。设为 0 时立即完成。")]
            [Min(0f)] public float duration = 0.8f;

            [Tooltip("切换缓动曲线。输入和输出均建议保持在 0 到 1。")]
            public AnimationCurve easing = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        }

        [Serializable]
        public sealed class CameraViewModeEvent : UnityEvent<CameraViewMode>
        {
        }

        [Header("Manager")]
        [SerializeField]
        [Tooltip("唯一负责写入实际 Camera 的管理器。")]
        private CameraControlManager cameraManager;

        [SerializeField]
        [Tooltip("普通玩法视角使用 Gameplay 优先级；演出应使用更高优先级。")]
        private int controlPriority = CameraControlPriorities.Gameplay;

        [Header("Modes")]
        [SerializeField] private CameraViewMode initialMode = CameraViewMode.Side2D;
        [SerializeField] private Side2DSettings side2D = new Side2DSettings();
        [SerializeField] private Perspective3DSettings perspective3D = new Perspective3DSettings();

        [Header("Transition")]
        [SerializeField] private TransitionSettings transition = new TransitionSettings();

        [Header("Events")]
        [SerializeField] private CameraViewModeEvent onTransitionStarted = new CameraViewModeEvent();
        [SerializeField] private CameraViewModeEvent onModeChanged = new CameraViewModeEvent();

        private CameraControlHandle controlHandle;
        private readonly List<ModeRequest> modeRequests =
            new List<ModeRequest>();
        private int nextModeRequestId = 1;
        private long nextModeRequestOrder = 1;
        private CameraViewMode currentMode;
        private CameraViewMode targetMode;
        private bool yawTransitionActive;
        private float yawTransitionFrom;
        private float yawTransitionTo;
        private float yawTransitionElapsed;
        private float yawTransitionDuration;
        private AnimationCurve yawTransitionCurve;
        private bool initialized;
        private bool suppressCompletionNotification;

        public event Action<CameraViewMode> TransitionStarted;
        public event Action<CameraViewMode> ModeChanged;

        public string CameraControlName => "2D / 3D Camera Mode";
        public CameraControlManager Manager => cameraManager;
        public Camera ControlledCamera => cameraManager != null ? cameraManager.OutputCamera : null;
        public Transform FollowTarget => cameraManager != null ? cameraManager.FocusTarget : null;
        public CameraViewMode CurrentMode => currentMode;
        public CameraViewMode TargetMode => targetMode;
        public bool HasControl => controlHandle.HasControl;
        public float Perspective3DYaw => perspective3D.yawDegrees;
        public float Perspective3DPitch => perspective3D.pitch;
        public float Side2DYaw => side2D.yawDegrees;
        public float TargetSide2DYaw => yawTransitionActive
            ? yawTransitionTo
            : side2D.yawDegrees;
        public bool IsSide2DYawTransitioning => yawTransitionActive;
        public float ModeTransitionDuration => transition.duration;
        public CameraTransition ModeTransition => BuildTransition();
        public bool IsTransitioning =>
            yawTransitionActive ||
            (HasControl && cameraManager != null && cameraManager.IsTransitioning);
        public float NormalizedTransitionTime
        {
            get
            {
                if (yawTransitionActive)
                {
                    return yawTransitionDuration <= 0f
                        ? 1f
                        : Mathf.Clamp01(
                            yawTransitionElapsed /
                            yawTransitionDuration);
                }
                return cameraManager != null
                    ? cameraManager.GetTransitionProgress(controlHandle)
                    : 0f;
            }
        }

        public IReadOnlyList<string> GetAchievementSignalIds()
        {
            return AchievementSignals;
        }

        private void Reset()
        {
            cameraManager = GetComponent<CameraControlManager>();
        }

        private void Awake()
        {
            EnsureInitialized();
        }

        private void OnEnable()
        {
            EnsureInitialized();
            EnsureRegistered();
        }

        private void OnDisable()
        {
            UnsubscribeFromManager();
            controlHandle.Release(CameraTransition.Immediate);
            controlHandle = default;
            modeRequests.Clear();
        }

        private void Update()
        {
            TickYawTransition(Time.deltaTime);
            if (modeRequests.Count > 0)
            {
                ApplyBestModeRequest(BuildTransition());
            }
        }

        private void OnValidate()
        {
            if (cameraManager == null)
            {
                cameraManager = GetComponent<CameraControlManager>();
            }

            side2D.distance = Mathf.Max(0.01f, side2D.distance);
            side2D.orthographicSize = Mathf.Max(0.01f, side2D.orthographicSize);
            perspective3D.distance = Mathf.Max(0.01f, perspective3D.distance);
            perspective3D.pitch = Mathf.Clamp(perspective3D.pitch, 1f, 80f);
            perspective3D.fieldOfView = Mathf.Clamp(perspective3D.fieldOfView, 1f, 179f);
            perspective3D.yawDegrees =
                Mathf.Repeat(perspective3D.yawDegrees + 180f, 360f) - 180f;
            side2D.yawDegrees = Mathf.Repeat(side2D.yawDegrees + 180f, 360f) - 180f;
            transition.duration = Mathf.Max(0f, transition.duration);
        }

        internal void SetSide2DYaw(float yawDegrees, bool applyImmediate = false)
        {
            side2D.yawDegrees = NormalizeYaw(yawDegrees);
            if (applyImmediate && HasControl)
            {
                cameraManager.Retarget(controlHandle, CameraTransition.Immediate);
            }
        }

        public void RotatePerspective(
            float yawDelta,
            float pitchDelta,
            bool applyImmediate = false)
        {
            perspective3D.yawDegrees = Mathf.Repeat(
                perspective3D.yawDegrees + yawDelta + 180f,
                360f) - 180f;
            perspective3D.pitch = Mathf.Clamp(
                perspective3D.pitch + pitchDelta,
                1f,
                80f);
            if (applyImmediate && HasControl)
            {
                cameraManager.Retarget(controlHandle, CameraTransition.Immediate);
            }
        }

        public void SetPerspectiveAngles(
            float yawDegrees,
            float pitchDegrees,
            bool applyImmediate = false)
        {
            perspective3D.yawDegrees = Mathf.Repeat(
                yawDegrees + 180f,
                360f) - 180f;
            perspective3D.pitch = Mathf.Clamp(pitchDegrees, 1f, 80f);
            if (applyImmediate && HasControl)
            {
                cameraManager.Retarget(controlHandle, CameraTransition.Immediate);
            }
        }

        public void Configure(Camera cameraToControl, Transform target, bool snapToInitialMode = true)
        {
            EnsureInitialized();
            cameraManager.ConfigureOutput(cameraToControl);
            cameraManager.SetFocusTarget(target, false);
            EnsureRegistered();
            if (snapToInitialMode)
            {
                SnapToMode(initialMode, false);
            }
        }

        public void SetFollowTarget(Transform target, bool snapFollowPosition = false)
        {
            EnsureInitialized();
            cameraManager.SetFocusTarget(target, snapFollowPosition);
        }

        public void SetFollowTarget(ICameraFollowTarget targetProvider, bool snapFollowPosition = false)
        {
            SetFollowTarget(targetProvider?.CameraFollowTransform, snapFollowPosition);
        }

        public void SetFocusPoint(Vector3 worldPoint, bool snap = false)
        {
            EnsureInitialized();
            cameraManager.SetFocusPoint(worldPoint, snap);
        }

        public void ClearFollowTarget(bool keepCurrentFocus = true)
        {
            EnsureInitialized();
            cameraManager.ClearFocusTarget(keepCurrentFocus);
        }

        internal void SwitchMode(CameraViewMode mode, bool immediate = false)
        {
            CameraTransition cameraTransition = immediate
                ? CameraTransition.Immediate
                : BuildTransition();
            ApplyMode(mode, cameraTransition);
        }

        internal void ToggleMode(bool immediate = false)
        {
            CameraViewMode nextMode = targetMode == CameraViewMode.Side2D
                ? CameraViewMode.Perspective3D
                : CameraViewMode.Side2D;
            SwitchMode(nextMode, immediate);
        }

        internal void SwitchTo2D(bool immediate = false)
        {
            SwitchMode(CameraViewMode.Side2D, immediate);
        }

        internal void SwitchTo3D(bool immediate = false)
        {
            SwitchMode(CameraViewMode.Perspective3D, immediate);
        }

        public CameraViewModeRequestHandle RequestMode(
            ICameraViewModeRequester requester,
            CameraViewMode mode,
            bool immediate = false)
        {
            return RequestMode(
                requester,
                mode,
                false,
                0f,
                immediate
                    ? CameraTransition.Immediate
                    : BuildTransition());
        }

        public CameraViewModeRequestHandle RequestMode(
            ICameraViewModeRequester requester,
            CameraViewMode mode,
            float side2DYawDegrees,
            bool immediate = false)
        {
            return RequestMode(
                requester,
                mode,
                true,
                side2DYawDegrees,
                immediate
                    ? CameraTransition.Immediate
                    : BuildTransition());
        }

        public CameraViewModeRequestHandle RequestMode(
            ICameraViewModeRequester requester,
            CameraViewMode mode,
            CameraTransition transition)
        {
            return RequestMode(
                requester,
                mode,
                false,
                0f,
                transition);
        }

        public CameraViewModeRequestHandle RequestMode(
            ICameraViewModeRequester requester,
            CameraViewMode mode,
            float side2DYawDegrees,
            CameraTransition transition)
        {
            return RequestMode(
                requester,
                mode,
                true,
                side2DYawDegrees,
                transition);
        }

        private CameraViewModeRequestHandle RequestMode(
            ICameraViewModeRequester requester,
            CameraViewMode mode,
            bool overrideSide2DYaw,
            float side2DYawDegrees,
            CameraTransition transition)
        {
            if (requester == null)
            {
                throw new ArgumentNullException(nameof(requester));
            }

            EnsureInitialized();
            ModeRequest request = new ModeRequest
            {
                id = nextModeRequestId++,
                order = nextModeRequestOrder++,
                priority = requester.CameraModeRequestPriority,
                mode = mode,
                requester = requester,
                overrideSide2DYaw = overrideSide2DYaw,
                side2DYawDegrees = side2DYawDegrees,
                transition = transition,
            };
            modeRequests.Add(request);
            ApplyBestModeRequest(BuildTransition());
            return new CameraViewModeRequestHandle(this, request.id);
        }

        internal bool IsModeRequestValid(
            CameraViewModeRequestHandle handle)
        {
            return handle.Controller == this &&
                   handle.Id != 0 &&
                   modeRequests.Exists(item => item.id == handle.Id);
        }

        internal void ReleaseModeRequest(
            CameraViewModeRequestHandle handle,
            bool immediate)
        {
            if (handle.Controller != this)
            {
                return;
            }

            ModeRequest request = modeRequests.Find(
                item => item.id == handle.Id);
            if (request == null)
            {
                return;
            }

            modeRequests.Remove(request);
            ApplyBestModeRequest(
                immediate
                    ? CameraTransition.Immediate
                    : BuildTransition());
        }

        public bool TryGetProjectionState(out CameraState state)
        {
            EnsureInitialized();
            if (targetMode == CameraViewMode.Side2D)
            {
                state = new CameraState
                {
                    projection = CameraProjectionMode.Orthographic,
                    orthographicSize = side2D.orthographicSize,
                    fieldOfView = perspective3D.fieldOfView,
                };
                return true;
            }

            state = new CameraState
            {
                projection = CameraProjectionMode.Perspective,
                orthographicSize = side2D.orthographicSize,
                fieldOfView = perspective3D.fieldOfView,
            };
            return true;
        }

        internal void SnapToMode(CameraViewMode mode, bool notifyListeners = true)
        {
            EnsureInitialized();
            EnsureRegistered();
            yawTransitionActive = false;
            yawTransitionCurve = null;
            bool changed = currentMode != mode || targetMode != mode || IsTransitioning;
            targetMode = mode;
            suppressCompletionNotification = !notifyListeners;
            try
            {
                cameraManager.Retarget(controlHandle, CameraTransition.Immediate);
            }
            finally
            {
                suppressCompletionNotification = false;
            }
            if (currentMode != mode)
            {
                currentMode = mode;
                if (changed && notifyListeners)
                {
                    onModeChanged.Invoke(mode);
                    ModeChanged?.Invoke(mode);
                    EmitModeChangedSignal(mode);
                }
            }
            cameraManager.ApplyViewModeAuthorityImmediately();
        }

        public void SnapFollowPosition()
        {
            EnsureInitialized();
            cameraManager.CompleteTransition(controlHandle);
        }

        public bool TryGetCameraState(
            in CameraControlContext context,
            out CameraState state)
        {
            if (targetMode == CameraViewMode.Side2D)
            {
                Quaternion yaw = Quaternion.Euler(0f, side2D.yawDegrees, 0f);
                Vector3 center = context.FocusPoint + yaw * new Vector3(
                    0f,
                    side2D.verticalOffset,
                    side2D.depthOffset);
                state = new CameraState
                {
                    position = center + yaw * Vector3.back * side2D.distance,
                    rotation = Quaternion.LookRotation(
                        yaw * Vector3.forward,
                        Vector3.up),
                    projection = CameraProjectionMode.Orthographic,
                    orthographicSize = side2D.orthographicSize,
                    fieldOfView = perspective3D.fieldOfView,
                };
                return true;
            }

            Vector3 perspectiveCenter = context.FocusPoint + new Vector3(
                0f,
                perspective3D.verticalOffset,
                perspective3D.depthOffset);
            Quaternion rotation = Quaternion.Euler(
                perspective3D.pitch,
                perspective3D.yawDegrees,
                0f);
            state = new CameraState
            {
                position = perspectiveCenter - rotation * Vector3.forward * perspective3D.distance,
                rotation = rotation,
                projection = CameraProjectionMode.Perspective,
                orthographicSize = side2D.orthographicSize,
                fieldOfView = perspective3D.fieldOfView,
            };
            return true;
        }

        private void BeginYawTransition(
            float targetYawDegrees,
            CameraTransition cameraTransition)
        {
            float normalizedTarget = NormalizeYaw(targetYawDegrees);
            if (cameraTransition.duration <= 0f)
            {
                yawTransitionActive = false;
                side2D.yawDegrees = normalizedTarget;
                cameraManager.ApplyViewModeAuthorityImmediately();
                EmitFaceSignal();
                return;
            }

            yawTransitionFrom = side2D.yawDegrees;
            yawTransitionTo = normalizedTarget;
            yawTransitionElapsed = 0f;
            yawTransitionDuration = cameraTransition.duration;
            yawTransitionCurve = cameraTransition.easing;
            yawTransitionActive = true;
        }

        private void TickYawTransition(float deltaTime)
        {
            if (!yawTransitionActive)
            {
                return;
            }

            yawTransitionElapsed += Mathf.Max(0f, deltaTime);
            float normalizedTime = yawTransitionDuration <= 0f
                ? 1f
                : Mathf.Clamp01(
                    yawTransitionElapsed /
                    yawTransitionDuration);
            float easedTime = yawTransitionCurve == null
                ? normalizedTime
                : Mathf.Clamp01(
                    yawTransitionCurve.Evaluate(normalizedTime));
            side2D.yawDegrees = Mathf.LerpAngle(
                yawTransitionFrom,
                yawTransitionTo,
                easedTime);

            if (normalizedTime >= 1f)
            {
                side2D.yawDegrees =
                    NormalizeYaw(yawTransitionTo);
                yawTransitionActive = false;
                yawTransitionCurve = null;
                EmitFaceSignal();
            }
        }

        private void ApplyMode(
            CameraViewMode mode,
            CameraTransition cameraTransition)
        {
            EnsureInitialized();
            EnsureRegistered();
            if (targetMode == mode)
            {
                if (cameraTransition.duration <= 0f)
                {
                    cameraManager.Retarget(
                        controlHandle,
                        CameraTransition.Immediate);
                    if (HasControl)
                    {
                        CompleteModeChange();
                    }
                }
                return;
            }

            targetMode = mode;
            onTransitionStarted.Invoke(mode);
            TransitionStarted?.Invoke(mode);
            GameplaySignalHub.Emit(
                AchievementSignalIds.CameraTransitionStarted,
                gameObject);
            cameraManager.Retarget(controlHandle, cameraTransition);
            if (cameraTransition.duration <= 0f)
            {
                if (HasControl)
                {
                    CompleteModeChange();
                }
                else
                {
                    cameraManager.ApplyViewModeAuthorityImmediately();
                }
            }
        }

        private void ApplyBestModeRequest(
            CameraTransition cameraTransition)
        {
            ModeRequest best = null;
            foreach (ModeRequest request in modeRequests)
            {
                if (best == null ||
                    request.priority > best.priority ||
                    (request.priority == best.priority &&
                     request.order > best.order))
                {
                    best = request;
                }
            }

            bool side2DYawChanged = false;
            float targetSide2DYaw = 0f;
            if (best != null &&
                best.mode == CameraViewMode.Side2D &&
                best.overrideSide2DYaw)
            {
                targetSide2DYaw = NormalizeYaw(
                    best.side2DYawDegrees);
                float comparisonYaw = yawTransitionActive
                    ? yawTransitionTo
                    : side2D.yawDegrees;
                side2DYawChanged = !Mathf.Approximately(
                    comparisonYaw,
                    targetSide2DYaw);
            }

            CameraViewMode nextMode =
                best != null ? best.mode : initialMode;
            CameraTransition effectiveTransition =
                best != null ? best.transition : cameraTransition;
            if (side2DYawChanged)
            {
                if (targetMode == nextMode &&
                    nextMode == CameraViewMode.Side2D)
                {
                    BeginYawTransition(
                        targetSide2DYaw,
                        effectiveTransition);
                }
                else
                {
                    side2D.yawDegrees = targetSide2DYaw;
                }
            }

            ApplyMode(
                nextMode,
                effectiveTransition);
        }

        internal void Tick(float deltaTime)
        {
            EnsureInitialized();
            TickYawTransition(deltaTime);
            cameraManager.Tick(deltaTime);
        }

        internal void SettleTransitionForDisable()
        {
            if (cameraManager != null)
            {
                cameraManager.CompleteTransition(controlHandle);
            }
        }

        private void EnsureInitialized()
        {
            if (cameraManager == null)
            {
                cameraManager = GetComponent<CameraControlManager>();
            }

            if (cameraManager == null)
            {
                cameraManager = gameObject.AddComponent<CameraControlManager>();
            }

            cameraManager.SetViewModeAuthority(this);
            if (!initialized)
            {
                currentMode = initialMode;
                targetMode = initialMode;
                initialized = true;
            }
        }

        private void EnsureRegistered()
        {
            if (controlHandle.IsValid || !isActiveAndEnabled)
            {
                return;
            }

            controlHandle = cameraManager.RequestControl(
                this,
                controlPriority,
                CameraInterruptionPolicy.AllowHigherPriority,
                CameraTransition.Immediate);
            SubscribeToManager();
        }

        private void SubscribeToManager()
        {
            cameraManager.TransitionCompleted -= OnManagerTransitionCompleted;
            cameraManager.TransitionCompleted += OnManagerTransitionCompleted;
        }

        private void UnsubscribeFromManager()
        {
            if (cameraManager != null)
            {
                cameraManager.TransitionCompleted -= OnManagerTransitionCompleted;
            }
        }

        private void OnManagerTransitionCompleted(CameraControlHandle handle)
        {
            if (handle == controlHandle)
            {
                CompleteModeChange(!suppressCompletionNotification);
            }
        }

        private void CompleteModeChange(bool notifyListeners = true)
        {
            if (currentMode == targetMode)
            {
                return;
            }

            currentMode = targetMode;
            if (notifyListeners)
            {
                onModeChanged.Invoke(currentMode);
                ModeChanged?.Invoke(currentMode);
                EmitModeChangedSignal(currentMode);
            }
        }

        private void EmitModeChangedSignal(
            CameraViewMode mode)
        {
            GameplaySignalHub.Emit(
                AchievementSignalIds.CameraModeChanged,
                gameObject);
            if (mode == CameraViewMode.Side2D)
            {
                EmitFaceSignal();
            }
        }

        private void EmitFaceSignal()
        {
            int quarterTurns = Mathf.RoundToInt(
                Side2DYaw / 90f);
            quarterTurns =
                ((quarterTurns % 4) + 4) % 4;
            string signalId;
            switch (quarterTurns)
            {
                case 1:
                    signalId =
                        AchievementSignalIds.CameraFaceRight;
                    break;
                case 2:
                    signalId =
                        AchievementSignalIds.CameraFaceBack;
                    break;
                case 3:
                    signalId =
                        AchievementSignalIds.CameraFaceLeft;
                    break;
                default:
                    signalId =
                        AchievementSignalIds.CameraFaceFront;
                    break;
            }

            GameplaySignalHub.Emit(
                signalId,
                gameObject);
        }

        private CameraTransition BuildTransition()
        {
            return new CameraTransition
            {
                duration = transition.duration,
                easing = transition.easing,
            };
        }

        private static float NormalizeYaw(float yawDegrees)
        {
            return Mathf.Repeat(yawDegrees + 180f, 360f) - 180f;
        }
    }
}
