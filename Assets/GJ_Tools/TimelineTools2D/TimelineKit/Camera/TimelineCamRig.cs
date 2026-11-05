using Project.CameraModes;
using UnityEngine;

[AddComponentMenu("TimelineKit/TimelineCamRig")]
[DisallowMultipleComponent]
[RequireComponent(typeof(CameraControlManager))]
public class TimelineCamRig :
    MonoBehaviour,
    ICameraControlSource,
    ICameraViewModeRequester,
    ICameraLensOverrideSource
{
    [Header("Project Camera")]
    [SerializeField]
    [Tooltip("场景中的统一相机管理器。Timeline 只能通过它修改 Camera。")]
    private CameraControlManager cameraManager;

    [SerializeField]
    [Tooltip("Project 中唯一的 2D 相机权威。")]
    private CameraModeController cameraModeController;

    [SerializeField]
    [Tooltip("Timeline 运镜使用的控制优先级，默认与项目演出优先级一致。")]
    private int controlPriority = CameraControlPriorities.Cutscene;

    [SerializeField, Min(0f)]
    [Tooltip("轨道结束后交还 Project 玩法相机的平滑时间。")]
    private float returnTransitionDuration = 0.25f;

    [Header("跟随目标")]
    [Tooltip("平面相机跟随的目标，也是运镜轨的参照锚点")]
    public Transform target;

    [Header("平面机位")]
    [Tooltip("相对跟随目标的水平偏移")]
    public float horizontalOffset;
    [Tooltip("相对跟随目标的高度")]
    public float height = 1.5f;
    [Min(.01f), Tooltip("正交相机半高")]
    public float viewSize = 5f;

    [Header("状态")]
    [Tooltip("Timeline 相机轨道接管中(自动置位)")]
    public bool isPlayingAnim;

    [HideInInspector]
    public bool userManualAllowed;

    [HideInInspector]
    public bool userDriving;

    private CameraControlHandle _controlHandle;
    private CameraState _normalState;
    private CameraState _shotState;
    private bool _hasNormalState;
    private bool _hasShotState;
    private int _activeTracks;
    private bool _hasLensOverride;
    private float _shotOrthographicSize;

    private float _returnDurationOverride = -1f;

    public string CameraControlName => "Timeline Camera";

    public CameraControlManager Manager
    {
        get
        {
            ResolveManager();
            return cameraManager;
        }
    }

    public Transform AnchorTransform
    {
        get
        {
            if (target != null)
            {
                return target;
            }

            CameraControlManager manager = Manager;
            return manager != null && manager.OutputCamera != null
                ? manager.OutputCamera.transform
                : transform;
        }
    }

    public bool IsTimelineControlling => _controlHandle.IsValid && _controlHandle.HasControl;

    public float ReturnTransitionDuration => returnTransitionDuration;

    public bool SmoothReturnOnRelease { get; private set; } = true;

    public float PlanarYaw =>
        cameraModeController != null
            ? cameraModeController.Side2DYaw
            : 0f;

    public bool CanManualDrive =>
        isPlayingAnim &&
        userManualAllowed &&
        IsTimelineControlling;

    public bool HasNormalState => _hasNormalState;

    public Vector3 NormalPosition => _normalState.position;

    public Quaternion NormalRotation => _normalState.rotation;

    public Vector3 CurrentPosition => ReadCurrentState().position;

    public Quaternion CurrentRotation => ReadCurrentState().rotation;

    public bool IsCurrentProjectionOrthographic =>
        cameraModeController != null
            ? cameraModeController.TargetMode == CameraViewMode.Side2D
            : ReadCurrentState().projection == CameraProjectionMode.Orthographic;

    public string CameraModeRequesterName => "Timeline Camera";

    public int CameraModeRequestPriority => controlPriority;

    private void Reset()
    {
        ResolveManager();
    }

    private void Awake()
    {
        _activeTracks = 0;
        _controlHandle = default;
        isPlayingAnim = false;
        userManualAllowed = false;
        userDriving = false;
        _hasShotState = false;
        _hasNormalState = false;
        _hasLensOverride = false;
        _returnDurationOverride = -1f;
        ResolveManager();
        ResolveModeController();
    }

    private void OnDisable()
    {
        Release(true);
    }

    public Vector3 OrbitPosition(Transform anchor)
    {
        CameraState state = ReadCurrentState();
        return new Vector3(anchor.position.x + horizontalOffset, anchor.position.y + height, state.position.z);
    }

    public Vector3 GetNormalOrbitPosition(Transform anchor)
    {
        if (!_hasNormalState)
        {
            return OrbitPosition(anchor);
        }

        return _normalState.position;
    }

    public void CaptureNormalState()
    {
        if (_hasNormalState)
        {
            return;
        }

        _normalState = ReadCurrentState();
        _hasNormalState = true;
    }

    public void SetReturnMode(bool smoothReturn)
    {
        SmoothReturnOnRelease = smoothReturn;
    }

    public void Acquire(float transitionDuration = 0.25f)
    {
        CameraControlManager manager = Manager;
        if (manager == null)
        {
            Debug.LogWarning("[TimelineCamRig] 缺少 CameraControlManager，Timeline 运镜不生效", this);
            return;
        }

        if (_activeTracks > 0)
        {
            Debug.LogWarning(
                "[TimelineCamRig] 多条 CameraTimelineTrack 正在同时控制同一相机，请确保同一时间只有一条相机轨道生效。",
                this);
        }

        CaptureNormalState();
        if (_activeTracks == 0)
        {
            CameraTransition transition = transitionDuration > 0f
                ? CameraTransition.Ease(transitionDuration)
                : CameraTransition.Immediate;
            _controlHandle = manager.RequestControl(
                this,
                controlPriority,
                CameraInterruptionPolicy.AllowHigherPriority,
                transition);
        }

        _activeTracks++;
        isPlayingAnim = true;
    }

    public void Release(bool restoreProjectCamera)
    {
        if (_activeTracks > 0)
        {
            _activeTracks--;
        }

        if (_activeTracks > 0)
        {
            return;
        }

        isPlayingAnim = false;
        userManualAllowed = false;
        userDriving = false;
        _hasShotState = false;

        if (restoreProjectCamera)
        {
            Transform projectTarget = Manager != null
                ? Manager.FocusTarget
                : null;
            if (projectTarget != null)
            {
                target = projectTarget;
            }
        }

        if (_controlHandle.IsValid)
        {
            float returnDuration = _returnDurationOverride >= 0f
                ? _returnDurationOverride : returnTransitionDuration;
            CameraTransition transition =
                restoreProjectCamera && returnDuration > 0f
                    ? CameraTransition.Ease(returnDuration)
                    : CameraTransition.Immediate;
            _controlHandle.Release(transition);
        }

        _controlHandle = default;
        _hasNormalState = false;
        _hasLensOverride = false;
        _returnDurationOverride = -1f;
    }

    public void ReleaseTimelineControl(bool restoreProjectCamera)
    {
        _activeTracks = 0;
        Release(restoreProjectCamera);
    }

    public void SetReturnDurationForCurrentShot(float seconds)
    {
        _returnDurationOverride = Mathf.Max(0f, seconds);
    }

    public void SetShotLens(float orthographicSize)
    {
        _shotOrthographicSize = Mathf.Max(.01f, orthographicSize);

        _hasLensOverride = true;
    }

    public bool TryGetLensOverride(
        out float orthographicSize)
    {
        orthographicSize = _shotOrthographicSize;

        return _hasLensOverride && isPlayingAnim;
    }

    public void SetShotTransform(
        Vector3 position,
        Quaternion rotation)
    {
        if (!_hasShotState)
        {
            _shotState = ReadCurrentState();
        }

        _shotState.position = position;
        _shotState.rotation = rotation;
        _hasShotState = true;
    }

    public CameraViewModeRequestHandle RequestViewMode(
        CameraViewMode mode,
        float transitionDuration)
    {
        CameraTransition transition = transitionDuration <= 0f
            ? CameraTransition.Immediate
            : CameraTransition.Ease(transitionDuration);
        return RequestViewMode(
            mode,
            false,
            0f,
            transition);
    }

    public CameraViewModeRequestHandle RequestViewMode(
        CameraViewMode mode,
        float side2DYawDegrees,
        float transitionDuration)
    {
        CameraTransition transition = transitionDuration <= 0f
            ? CameraTransition.Immediate
            : CameraTransition.Ease(transitionDuration);
        return RequestViewMode(
            mode,
            true,
            side2DYawDegrees,
            transition);
    }

    public CameraViewModeRequestHandle RequestViewMode(
        CameraViewMode mode,
        CameraTransition transition)
    {
        return RequestViewMode(
            mode,
            false,
            0f,
            transition);
    }

    public CameraViewModeRequestHandle RequestViewMode(
        CameraViewMode mode,
        float side2DYawDegrees,
        CameraTransition transition)
    {
        return RequestViewMode(
            mode,
            true,
            side2DYawDegrees,
            transition);
    }

    private CameraViewModeRequestHandle RequestViewMode(
        CameraViewMode mode,
        bool overrideSide2DYaw,
        float side2DYawDegrees,
        CameraTransition transition)
    {
        ResolveModeController();
        if (cameraModeController == null)
        {
            return default;
        }

        CameraViewModeRequestHandle handle =
            overrideSide2DYaw
                ? cameraModeController.RequestMode(
                    this,
                    mode,
                    side2DYawDegrees,
                    transition)
                : cameraModeController.RequestMode(
                    this,
                    mode,
                    transition);
        if (_controlHandle.IsValid)
        {
            Manager?.Retarget(_controlHandle, transition);
        }
        return handle;
    }

    public void ReleaseViewMode(
        CameraViewModeRequestHandle handle,
        bool immediate = false)
    {
        if (handle.IsValid)
        {
            handle.Release(immediate);
        }
        if (_controlHandle.IsValid)
        {
            CameraTransition transition = immediate
                ? CameraTransition.Immediate
                : CameraTransition.Ease(returnTransitionDuration);
            Manager?.Retarget(_controlHandle, transition);
        }
    }

    public bool BeginManualControl()
    {
        if (!CanManualDrive)
        {
            return false;
        }

        if (!userDriving)
        {
            SyncParamsFromCamera();
            userDriving = true;
        }

        return true;
    }

    public void EndManualControl()
    {
        userDriving = false;
    }

    public void SyncParamsFromCamera()
    {
        SyncParamsFromState(ReadCurrentState());
    }

    public bool TryGetCameraState(
        in CameraControlContext context,
        out CameraState state)
    {
        if (userDriving && CanManualDrive)
        {
            state = BuildManualState(context.FocusPoint);
            return true;
        }

        if (_hasShotState)
        {
            state = _shotState;
            ApplyViewModeProjection(ref state);
            return true;
        }

        state = default;
        return false;
    }

    private void LateUpdate()
    {
        if (!isPlayingAnim || userDriving || _hasShotState)
        {
            return;
        }

        CameraControlManager manager = Manager;
        if (manager == null || target == null)
        {
            return;
        }

        CameraState orbitState = BuildManualState(target.position);
        _shotState = orbitState;
        _hasShotState = true;
    }

    private CameraState BuildManualState(Vector3 lookPoint)
    {
        CameraState state = _hasShotState ? _shotState : ReadCurrentState();
        Vector3 center = target != null ? target.position : lookPoint;
        state.position = new Vector3(center.x + horizontalOffset, center.y + height, state.position.z);
        state.rotation = Quaternion.Euler(0f, 0f, state.rotation.eulerAngles.z);
        state.projection = CameraProjectionMode.Orthographic;
        state.orthographicSize = Mathf.Max(.01f, viewSize);
        return state;
    }

    private void SyncParamsFromState(CameraState state)
    {
        viewSize = state.orthographicSize;
        if (target != null) { height = state.position.y - target.position.y; horizontalOffset = state.position.x - target.position.x; }
    }

    private CameraState ReadCurrentState()
    {
        CameraControlManager manager = Manager;
        if (manager != null)
        {
            return manager.CurrentState;
        }

        Camera camera = GetComponent<Camera>();
        if (camera != null)
        {
            return CameraState.FromCamera(camera);
        }

        return new CameraState
        {
            position = transform.position,
            rotation = transform.rotation,
            projection = CameraProjectionMode.Orthographic,
            orthographicSize = 5f,

        };
    }

    private void ResolveManager()
    {
        if (cameraManager == null)
        {
            cameraManager = GetComponent<CameraControlManager>();
        }
    }

    private void ResolveModeController()
    {
        if (cameraModeController == null)
        {
            cameraModeController = GetComponent<CameraModeController>();
        }
    }

    private void ApplyViewModeProjection(ref CameraState state)
    {
        ResolveModeController();
        if (cameraModeController == null ||
            !cameraModeController.TryGetProjectionState(
                out CameraState projectionState))
        {
            return;
        }

        state.projection = projectionState.projection;
        state.orthographicSize = projectionState.orthographicSize;

    }

    private static Quaternion LookRotation(Vector3 position, Vector3 lookPoint, Quaternion fallback)
        { return Quaternion.Euler(0f, 0f, fallback.eulerAngles.z); }
}
