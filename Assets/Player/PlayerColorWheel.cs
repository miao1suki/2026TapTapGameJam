using Project.InputAbstraction;
using Project.ColorBlocks;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerInteractionSensor))]
    public sealed class PlayerColorWheel : MonoBehaviour
    {
        [SerializeField] private float displaySeconds = .75f;
        private PlayerInteractionSensor sensor;
        private Text label;
        private float visibleUntil;

        private void Awake()
        {
            sensor = GetComponent<PlayerInteractionSensor>();
        }

        private void Update()
        {
            if (GameInput.WasTriggeredThisFrame(InputActionId.PointerSecondary))
            {
                ColorBlock block = sensor?.CurrentTarget as ColorBlock;
                if (block != null && block.TryOpenColorWheel(gameObject))
                {
                    Show(block.CurrentColorTypeId);
                }
                else
                {
                    Show("无法与当前物体交互");
                }
            }
            if (label != null)
            {
                label.enabled = Time.unscaledTime < visibleUntil;
            }
        }

        private void Show(string colorId)
        {
            EnsureLabel();
            label.text = $"颜色轮盘 · {colorId}";
            label.enabled = true;
            visibleUntil = Time.unscaledTime + displaySeconds;
        }

        private void EnsureLabel()
        {
            if (label != null) return;
            GameObject canvasObject = new GameObject("Color Wheel UI",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 2001;
            canvasObject.GetComponent<CanvasScaler>().uiScaleMode =
                CanvasScaler.ScaleMode.ScaleWithScreenSize;
            GameObject objectRoot = new GameObject("Color Wheel Hint",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            objectRoot.transform.SetParent(canvas.transform, false);
            RectTransform rect = objectRoot.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(.5f, .5f);
            rect.anchorMax = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(560f, 60f);
            rect.anchoredPosition = new Vector2(0f, -180f);
            label = objectRoot.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            label.fontSize = 24;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.raycastTarget = false;
        }
    }
}
