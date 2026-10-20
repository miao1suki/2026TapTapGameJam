using UnityEngine;

namespace Project.InputAbstraction
{
    /// <summary>Screen.safeArea excludes cutouts, but not the TapTap mini-game capsule.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(RectTransform))]
    public sealed class MobileOverlaySafeArea : MonoBehaviour
    {
        public const float ReservedWidth = 248f;
        public const float ReservedHeight = 156f;
        public const float CloseButtonSize = 124f;

        public static Rect ReservedRect(Rect safeRect) =>
            new Rect(safeRect.xMax - ReservedWidth, safeRect.yMax - ReservedHeight,
                ReservedWidth, ReservedHeight);

        public static void ProtectCloseButton(Component button)
        {
            if (button == null) return;
            var guard = button.GetComponent<MobileOverlaySafeArea>();
            if (guard == null) guard = button.gameObject.AddComponent<MobileOverlaySafeArea>();
            guard.Refresh();
        }

        private void OnEnable() => Refresh();
        // Close buttons can be restyled after OnEnable; re-check only this tiny
        // rect, with no scene search, allocations or layout rebuild each frame.
        private void LateUpdate() => Refresh();

        public void Refresh()
        {
            if (!Application.isPlaying || GameInput.PlatformMode != InputPlatformMode.Mobile) return;
            var rect = (RectTransform)transform;
            var parent = rect.parent as RectTransform;
            var canvas = rect.GetComponentInParent<Canvas>();
            if (parent == null || canvas == null || Screen.width <= 0 || Screen.height <= 0) return;
            if (rect.rect.width < CloseButtonSize)
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, CloseButtonSize);
            if (rect.rect.height < CloseButtonSize)
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, CloseButtonSize);
            // Express the reserved zone in a common 720-high UI unit system, so
            // pause, menu and map canvases with different scalers agree.
            float unit = Screen.height / 720f;
            Rect safe = Screen.safeArea;
            Rect reserved = new Rect(safe.xMax - ReservedWidth * unit, safe.yMax - ReservedHeight * unit,
                ReservedWidth * unit, ReservedHeight * unit);
            Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 center = RectTransformUtility.WorldToScreenPoint(uiCamera, rect.TransformPoint(rect.rect.center));
            float scale = canvas.rootCanvas.scaleFactor;
            Vector2 half = rect.rect.size * scale * .5f;
            Rect hit = new Rect(center - half, half * 2);
            Vector2 desired = center;
            desired.x = Mathf.Clamp(desired.x, safe.xMin + half.x + 14 * unit, safe.xMax - half.x - 14 * unit);
            desired.y = Mathf.Clamp(desired.y, safe.yMin + half.y + 14 * unit, safe.yMax - half.y - 14 * unit);
            hit.position = desired - half;
            if (hit.Overlaps(reserved)) desired.x = reserved.xMin - half.x - 14 * unit;
            if ((desired - center).sqrMagnitude < .01f) return;
            if (!RectTransformUtility.ScreenPointToWorldPointInRectangle(parent, desired, uiCamera, out var world)) return;
            rect.position += world - rect.TransformPoint(rect.rect.center);
        }
    }
}
