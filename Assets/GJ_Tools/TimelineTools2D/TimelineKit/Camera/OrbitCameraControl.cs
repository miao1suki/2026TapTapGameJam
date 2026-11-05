#if ENABLE_INPUT_SYSTEM
using UnityEngine;
using UnityEngine.InputSystem;

[AddComponentMenu("TimelineKit/平面镜头拖动缩放")]
[RequireComponent(typeof(TimelineCamRig))]
public class OrbitCameraControl : MonoBehaviour
{
    [Header("平面镜头控制")]
    [Tooltip("按住右键拖动画面")]
    public bool requireMouseButton = true;
    [Tooltip("水平拖动速度，世界单位/像素")]
    public float lookSpeedX = .01f;
    [Tooltip("垂直拖动速度，世界单位/像素")]
    public float lookSpeedY = .01f;
    public bool invertY;
    public float scrollSpeed = .01f;
    public Vector2 viewSizeRange = new Vector2(2f, 25f);

    private TimelineCamRig _rig;

    private void Awake()
    {
        _rig = GetComponent<TimelineCamRig>();
    }

    private void Update()
    {
        if (_rig == null)
        {
            return;
        }

        _rig.EndManualControl();
        if (!_rig.CanManualDrive)
        {
            return;
        }

        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            return;
        }
        if (!requireMouseButton || mouse.rightButton.isPressed)
        {
            Vector2 delta = mouse.delta.ReadValue();
            if (delta.sqrMagnitude > 0.0001f)
            {
                if (!_rig.BeginManualControl())
                {
                    return;
                }
                _rig.horizontalOffset -= delta.x * lookSpeedX;
                float dir = invertY ? -1f : 1f;
                _rig.height -= delta.y * lookSpeedY * dir;
            }
        }
        float scroll = mouse.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) > 0.0001f)
        {
            if (!_rig.BeginManualControl())
            {
                return;
            }
            _rig.viewSize = Mathf.Clamp(_rig.viewSize - scroll * scrollSpeed, viewSizeRange.x, viewSizeRange.y);
        }
    }
}
#endif
