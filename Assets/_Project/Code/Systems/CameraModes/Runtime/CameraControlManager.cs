using System;
using System.Collections.Generic;
using Project.InputAbstraction;
using UnityEngine;

namespace Project.CameraModes
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1000)]
    public sealed class CameraControlManager : MonoBehaviour
    {
        private sealed class ControlRequest
        {
            public int id;
            public long order;
            public int priority;
            public CameraInterruptionPolicy interruptionPolicy;
            public ICameraControlSource source;
        }

        [Header("Output (only this component writes Camera data)")]
        [SerializeField]
        [Tooltip("实际被写入的 Unity Camera。其他运行时脚本不应直接修改它。")]
        private Camera outputCamera;

        [Header("Shared focus input")]
        [SerializeField]
        [Tooltip("简单场景可直接绑定；复杂跟随系统应调用 SetFocusPoint 输入最终跟随点。")]
        private Transform focusTarget;

        [SerializeField]
        private Vector3 fallbackFocusPoint;

        [SerializeField]
        [Tooltip("暂停游戏时间时是否仍推进 Manager 的镜头过渡。")]
        private bool useUnscaledTime = true;

        private readonly List<ControlRequest> requests = new List<ControlRequest>();
        private int nextId = 1;
        private long nextOrder = 1;
        private ControlRequest activeRequest;
        private CameraState currentState;
        private CameraState transitionStartState;
        private Matrix4x4 transitionStartProjection;
        private CameraTransition activeTransition;
        private float transitionElapsed;
        private bool initialized;
        private bool isTransitioning;
        private bool focusPointWasSetExternally;
        private ICameraViewModeAuthority viewModeAuthority;

        public event Action<string, string> ActiveControlChanged;
        public event Action<CameraControlHandle> TransitionCompleted;

        public Camera OutputCamera => outputCamera;
        public Transform FocusTarget => focusTarget;
        public Vector3 FocusPoint => ResolveFocusPoint();
        public string ActiveControlName => activeRequest?.source?.CameraControlName;
        public int ActivePriority => activeRequest?.priority ?? int.MinValue;
        public bool IsTransitioning => isTransitioning;
        public float NormalizedTransitionTime => !isTransitioning || activeTransition.duration <= 0f
            ? 1f
            : Mathf.Clamp01(transitionElapsed / activeTransition.duration);
        public CameraState CurrentState => currentState;

        private void Reset()
        {
            outputCamera = GetComponent<Camera>();
        }

        private void Awake()
        {
            EnsureInitialized();
        }

        private void LateUpdate()
        {
            float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            Tick(deltaTime);
        }

        private void OnDisable()
        {
            if (outputCamera != null)
            {
                ApplyStableState(currentState);
            }
        }

        private void OnValidate()
        {
            if (outputCamera == null)
            {
                outputCamera = GetComponent<Camera>();
            }
        }

        public void ConfigureOutput(Camera cameraToControl)
        {
            outputCamera = cameraToControl;
            initialized = false;
            EnsureInitialized();
        }

        public void SetViewModeAuthority(
            ICameraViewModeAuthority authority)
        {
            viewModeAuthority = authority;
        }

        public void ApplyViewModeAuthorityImmediately()
        {
            EnsureInitialized();
            if (outputCamera == null)
            {
                return;
            }

            CameraState state = currentState;
            ApplyViewModeProjection(ref state);
            currentState = state;
            ApplyStableState(currentState);
        }

        public void SetFocusPoint(Vector3 worldPoint, bool snap = false)
        {
            fallbackFocusPoint = worldPoint;
            focusPointWasSetExternally = true;
            if (snap)
            {
                ApplyActiveSourceImmediately();
            }
        }

        public void SetFocusTarget(Transform target, bool snap = false)
        {
            focusTarget = target;
            focusPointWasSetExternally = false;
            if (target != null)
            {
                fallbackFocusPoint = target.position;
            }

            if (snap)
            {
                ApplyActiveSourceImmediately();
            }
        }

        public void ClearFocusTarget(bool keepCurrentFocus = true)
        {
            if (keepCurrentFocus)
            {
                fallbackFocusPoint = ResolveFocusPoint();
            }

            focusTarget = null;
            focusPointWasSetExternally = true;
        }

        public CameraControlHandle RequestControl(
            ICameraControlSource source,
            int priority = CameraControlPriorities.Gameplay,
            CameraInterruptionPolicy interruptionPolicy = CameraInterruptionPolicy.AllowHigherPriority,
            CameraTransition transition = default)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            EnsureInitialized();
            ControlRequest request = new ControlRequest
            {
                id = nextId++,
                order = nextOrder++,
                priority = priority,
                interruptionPolicy = interruptionPolicy,
                source = source,
            };
            requests.Add(request);
            CameraControlHandle handle = new CameraControlHandle(this, request.id);
            ReevaluateActiveRequest(transition, false);
            return handle;
        }

        public bool Retarget(CameraControlHandle handle, CameraTransition transition)
        {
            ControlRequest request = FindRequest(handle);
            if (request == null)
            {
                return false;
            }

            request.order = nextOrder++;
            if (request != activeRequest)
            {
                ReevaluateActiveRequest(transition, false);
                return request == activeRequest;
            }

            BeginTransition(transition);
            return true;
        }

        public bool ReleaseControl(CameraControlHandle handle, CameraTransition transition = default)
        {
            ControlRequest request = FindRequest(handle);
            if (request == null)
            {
                return false;
            }

            bool wasActive = request == activeRequest;
            requests.Remove(request);
            if (wasActive)
            {
                ReevaluateActiveRequest(transition, true);
            }

            return true;
        }

        public bool ForceTakeControl(CameraControlHandle handle, CameraTransition transition = default)
        {
            ControlRequest request = FindRequest(handle);
            if (request == null)
            {
                return false;
            }

            request.order = nextOrder++;
            SwitchActiveRequest(request, transition);
            return true;
        }

        public bool SetInterruptionPolicy(
            CameraControlHandle handle,
            CameraInterruptionPolicy interruptionPolicy,
            CameraTransition transition = default)
        {
            ControlRequest request = FindRequest(handle);
            if (request == null)
            {
                return false;
            }

            request.interruptionPolicy = interruptionPolicy;
            if (request == activeRequest &&
                interruptionPolicy == CameraInterruptionPolicy.AllowHigherPriority)
            {
                ReevaluateActiveRequest(transition, false);
            }

            return true;
        }

        public bool SetPriority(
            CameraControlHandle handle,
            int priority,
            CameraTransition transition = default)
        {
            ControlRequest request = FindRequest(handle);
            if (request == null)
            {
                return false;
            }

            request.priority = priority;
            request.order = nextOrder++;
            ReevaluateActiveRequest(transition, false);
            return true;
        }

        public bool HasControl(CameraControlHandle handle)
        {
            ControlRequest request = FindRequest(handle);
            return request != null && request == activeRequest;
        }

        public bool IsHandleValid(CameraControlHandle handle)
        {
            return FindRequest(handle) != null;
        }

        public float GetTransitionProgress(CameraControlHandle handle)
        {
            return HasControl(handle) ? NormalizedTransitionTime : 0f;
        }

        public void CompleteTransition(CameraControlHandle handle)
        {
            if (!HasControl(handle))
            {
                return;
            }

            ApplyActiveSourceImmediately();
        }

        internal void Tick(float deltaTime)
        {
            EnsureInitialized();
            if (outputCamera == null)
            {
                return;
            }

            if (activeRequest == null)
            {
                ApplyViewModeAuthorityImmediately();
                return;
            }

            CameraControlContext context = new CameraControlContext(
                ResolveFocusPoint(),
                Mathf.Max(0f, deltaTime));
            if (!activeRequest.source.TryGetCameraState(context, out CameraState targetState))
            {
                return;
            }

            ApplyViewModeProjection(ref targetState);
            SanitizeState(ref targetState);
            if (!isTransitioning)
            {
                currentState = targetState;
                ApplyStableState(currentState);
                return;
            }

            transitionElapsed += Mathf.Max(0f, deltaTime);
            float normalizedTime = activeTransition.duration <= 0f
                ? 1f
                : Mathf.Clamp01(transitionElapsed / activeTransition.duration);
            float easedTime = activeTransition.easing == null
                ? normalizedTime
                : Mathf.Clamp01(activeTransition.easing.Evaluate(normalizedTime));
            currentState = LerpState(transitionStartState, targetState, easedTime);

            if (normalizedTime >= 1f)
            {
                currentState = targetState;
                isTransitioning = false;
                ApplyStableState(currentState);
                TransitionCompleted?.Invoke(new CameraControlHandle(this, activeRequest.id));
                GameplaySignalHub.Emit(
                    AchievementSignalIds.CameraTransitionCompleted,
                    gameObject);
            }
            else
            {
                ApplyTransitionState(targetState, currentState, easedTime);
            }
        }

        private void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }

            if (outputCamera == null)
            {
                outputCamera = GetComponent<Camera>();
            }

            if (outputCamera != null)
            {
                currentState = CameraState.FromCamera(outputCamera);
            }

            ResolveViewModeAuthority();
            initialized = true;
        }

        private void ResolveViewModeAuthority()
        {
            viewModeAuthority = null;
            MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour is ICameraViewModeAuthority authority)
                {
                    viewModeAuthority = authority;
                    break;
                }
            }
        }

        private Vector3 ResolveFocusPoint()
        {
            if (!focusPointWasSetExternally && focusTarget != null)
            {
                fallbackFocusPoint = focusTarget.position;
            }

            return fallbackFocusPoint;
        }

        private ControlRequest FindRequest(CameraControlHandle handle)
        {
            if (handle.Manager != this || handle.Id == 0)
            {
                return null;
            }

            return requests.Find(item => item.id == handle.Id);
        }

        private void ReevaluateActiveRequest(CameraTransition transition, bool currentWasReleased)
        {
            ControlRequest candidate = SelectBestRequest();
            if (candidate == activeRequest)
            {
                return;
            }

            if (!currentWasReleased && activeRequest != null &&
                activeRequest.interruptionPolicy == CameraInterruptionPolicy.BlockAll)
            {
                return;
            }

            if (!currentWasReleased && activeRequest != null && candidate != null &&
                candidate.priority <= activeRequest.priority)
            {
                return;
            }

            SwitchActiveRequest(candidate, transition);
        }

        private ControlRequest SelectBestRequest()
        {
            ControlRequest best = null;
            foreach (ControlRequest request in requests)
            {
                if (best == null || request.priority > best.priority ||
                    (request.priority == best.priority && request.order > best.order))
                {
                    best = request;
                }
            }

            return best;
        }

        private void SwitchActiveRequest(ControlRequest next, CameraTransition transition)
        {
            string previousName = activeRequest?.source?.CameraControlName;
            activeRequest = next;
            string nextName = activeRequest?.source?.CameraControlName;
            ActiveControlChanged?.Invoke(previousName, nextName);
            if (activeRequest != null)
            {
                BeginTransition(transition);
            }
            else
            {
                isTransitioning = false;
            }
        }

        private void BeginTransition(CameraTransition transition)
        {
            EnsureInitialized();
            transition.duration = Mathf.Max(0f, transition.duration);
            activeTransition = transition;
            transitionStartState = currentState;
            if (outputCamera != null)
            {
                transitionStartProjection = outputCamera.projectionMatrix;
            }
            transitionElapsed = 0f;
            isTransitioning = activeRequest != null && transition.duration > 0f;
            if (!isTransitioning)
            {
                ApplyActiveSourceImmediately();
            }
        }

        private void ApplyActiveSourceImmediately()
        {
            EnsureInitialized();
            if (outputCamera == null || activeRequest == null)
            {
                isTransitioning = false;
                return;
            }

            CameraControlContext context = new CameraControlContext(ResolveFocusPoint(), 0f);
            if (!activeRequest.source.TryGetCameraState(context, out CameraState state))
            {
                return;
            }

            ApplyViewModeProjection(ref state);
            SanitizeState(ref state);
            currentState = state;
            transitionStartState = state;
            transitionStartProjection = BuildProjection(state);
            transitionElapsed = activeTransition.duration;
            isTransitioning = false;
            ApplyStableState(currentState);
            TransitionCompleted?.Invoke(new CameraControlHandle(this, activeRequest.id));
            GameplaySignalHub.Emit(
                AchievementSignalIds.CameraTransitionCompleted,
                gameObject);
        }

        private static void SanitizeState(ref CameraState state)
        {
            state.orthographicSize = Mathf.Max(0.01f, state.orthographicSize);
            state.fieldOfView = Mathf.Clamp(state.fieldOfView, 1f, 179f);
            if (state.rotation.x == 0f && state.rotation.y == 0f &&
                state.rotation.z == 0f && state.rotation.w == 0f)
            {
                state.rotation = Quaternion.identity;
            }
        }

        private void ApplyViewModeProjection(ref CameraState state)
        {
            if (viewModeAuthority == null ||
                !viewModeAuthority.TryGetProjectionState(
                    out CameraState projectionState))
            {
                return;
            }

            state.projection = projectionState.projection;
            state.orthographicSize = projectionState.orthographicSize;
            state.fieldOfView = projectionState.fieldOfView;
        }

        private static CameraState LerpState(CameraState from, CameraState to, float amount)
        {
            return new CameraState
            {
                position = Vector3.LerpUnclamped(from.position, to.position, amount),
                rotation = Quaternion.SlerpUnclamped(from.rotation, to.rotation, amount),
                projection = amount < 0.5f ? from.projection : to.projection,
                orthographicSize = Mathf.LerpUnclamped(
                    from.orthographicSize,
                    to.orthographicSize,
                    amount),
                fieldOfView = Mathf.LerpUnclamped(from.fieldOfView, to.fieldOfView, amount),
            };
        }

        private void ApplyStableState(CameraState state)
        {
            if (outputCamera == null)
            {
                return;
            }

            outputCamera.transform.SetPositionAndRotation(state.position, state.rotation);
            outputCamera.ResetProjectionMatrix();
            outputCamera.orthographic = state.projection == CameraProjectionMode.Orthographic;
            outputCamera.orthographicSize = state.orthographicSize;
            outputCamera.fieldOfView = state.fieldOfView;
            outputCamera.ResetProjectionMatrix();
        }

        private void ApplyTransitionState(
            CameraState to,
            CameraState blended,
            float amount)
        {
            outputCamera.transform.SetPositionAndRotation(blended.position, blended.rotation);
            Matrix4x4 toProjection = BuildProjection(to);
            outputCamera.orthographic = false;
            outputCamera.projectionMatrix = LerpMatrix(
                transitionStartProjection,
                toProjection,
                amount);
            outputCamera.orthographicSize = blended.orthographicSize;
            outputCamera.fieldOfView = blended.fieldOfView;
        }

        private Matrix4x4 BuildProjection(CameraState state)
        {
            float aspect = outputCamera.aspect > 0f ? outputCamera.aspect : 16f / 9f;
            if (state.projection == CameraProjectionMode.Orthographic)
            {
                float halfWidth = state.orthographicSize * aspect;
                return Matrix4x4.Ortho(
                    -halfWidth,
                    halfWidth,
                    -state.orthographicSize,
                    state.orthographicSize,
                    outputCamera.nearClipPlane,
                    outputCamera.farClipPlane);
            }

            return Matrix4x4.Perspective(
                state.fieldOfView,
                aspect,
                outputCamera.nearClipPlane,
                outputCamera.farClipPlane);
        }

        private static Matrix4x4 LerpMatrix(Matrix4x4 from, Matrix4x4 to, float amount)
        {
            Matrix4x4 result = default;
            for (int index = 0; index < 16; index++)
            {
                result[index] = Mathf.LerpUnclamped(from[index], to[index], amount);
            }

            return result;
        }
    }
}
