using UnityEngine;

namespace Project.InputAbstraction
{
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(RectTransform))]
    public sealed class ScreenSafeArea : MonoBehaviour
    {
        private Rect lastArea;
        private Vector2Int lastSize;
        private void OnEnable() => Refresh();
        private void Update()
        {
            if (lastArea != Screen.safeArea || lastSize.x != Screen.width || lastSize.y != Screen.height)
                Refresh();
        }
        public void Refresh()
        {
            lastArea = Screen.safeArea;
            lastSize = new Vector2Int(Screen.width, Screen.height);
            Apply(lastArea, new Vector2(Screen.width, Screen.height));
        }
        public void Apply(Rect area, Vector2 screenSize)
        {
            if (screenSize.x <= 0 || screenSize.y <= 0) return;
            var rect = (RectTransform)transform;
            rect.anchorMin = new Vector2(area.xMin / screenSize.x, area.yMin / screenSize.y);
            rect.anchorMax = new Vector2(area.xMax / screenSize.x, area.yMax / screenSize.y);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
