using Project.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Interactions
{
    [DisallowMultipleComponent]
    public sealed class PlayerHealthHud : MonoBehaviour
    {
        [SerializeField] private Vector2 anchoredPosition = new Vector2(24f, -24f);
        [SerializeField] private int fontSize = 24;
        [SerializeField] private Color color = new Color(1f, .35f, .35f, 1f);
        private Text label;
        private PlayerHealth health;

        private void Awake()
        {
            health = GetComponent<PlayerHealth>();
            EnsureView();
            if (health != null) health.Changed += Refresh;
            Refresh(health != null ? health.CurrentHealth : 0f,
                health != null ? health.MaxHealth : 0f);
        }

        private void OnDestroy()
        {
            if (health != null) health.Changed -= Refresh;
        }

        private void EnsureView()
        {
            GameObject canvasObject = new GameObject("Player HUD",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 2000;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            GameObject labelObject = new GameObject("Player Health",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            labelObject.transform.SetParent(canvas.transform, false);
            RectTransform rect = labelObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(320f, 48f);
            label = labelObject.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = TextAnchor.UpperLeft;
            label.raycastTarget = false;
        }

        private void Refresh(float current, float max)
        {
            if (label != null)
            {
                label.text = $"生命 {Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
            }
        }
    }
}
