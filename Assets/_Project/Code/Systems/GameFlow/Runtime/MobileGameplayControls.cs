using Project.InputAbstraction;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Project.GameFlow
{
    [DisallowMultipleComponent]
    public sealed class MobileGameplayControls : MonoBehaviour
    {
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private RectTransform controls;
        [SerializeField] private GameUiRouter router;
        [SerializeField] private bool previewOnDesktop;
        private bool? visible;
        public RectTransform Controls => controls;
        public void Configure(TMP_FontAsset value, GameUiRouter owner)
        {
            font = value;
            router = owner;
        }
        public void BuildView()
        {
            if (controls != null) return;
            controls = new GameObject("Touch Controls", typeof(RectTransform), typeof(ScreenSafeArea)).GetComponent<RectTransform>();
            controls.SetParent(transform, false);
            controls.GetComponent<ScreenSafeArea>().Refresh();
            Direction("上", Vector2.up, new Vector2(170, 295));
            Direction("左", Vector2.left, new Vector2(65, 190));
            Direction("右", Vector2.right, new Vector2(275, 190));
            Direction("下", Vector2.down, new Vector2(170, 85));
            Action("跳跃", InputActionId.Jump, new Vector2(-85, 95));
            Action("冲刺", InputActionId.Sprint, new Vector2(-85, 220));
            Action("交互", InputActionId.Interact, new Vector2(-210, 220));
            Action("放左", InputActionId.DropLeft, new Vector2(-335, 95));
            Action("放右", InputActionId.DropRight, new Vector2(-210, 95));
            Action("选色", InputActionId.CameraModeSwitch, new Vector2(-335, 220));
            Action("重生", InputActionId.Respawn, new Vector2(-85, -70), Vector2.one);
            var pause = CreateButton("暂停", Vector2.one, new Vector2(-210, -70));
            pause.GetComponent<Button>().onClick.AddListener(TogglePause);
        }
        private void Awake()
        {
            BuildView();
            // Persistent UnityEvent is configured in the saved prefab; this also supports runtime creation.
            var pause = controls.Find("暂停")?.GetComponent<Button>();
            if (pause != null && pause.onClick.GetPersistentEventCount() == 0)
            {
                pause.onClick.RemoveListener(TogglePause);
                pause.onClick.AddListener(TogglePause);
            }
            RefreshVisibility();
        }
        private void Update() => RefreshVisibility();
        private void RefreshVisibility()
        {
            bool show = (previewOnDesktop || GameInput.PlatformMode == InputPlatformMode.Mobile) &&
                Time.timeScale > 0 && (router == null || !router.IsPaused);
            if (visible == show) return;
            visible = show;
            controls.gameObject.SetActive(show);
        }
        public void TogglePause() => router?.TogglePause();
        private void Direction(string label, Vector2 direction, Vector2 position)
        {
            CreateButton(label, Vector2.zero, position).AddComponent<VirtualDirectionButton>().Configure(direction);
        }
        private void Action(string label, InputActionId action, Vector2 position, Vector2? anchor = null)
        {
            CreateButton(label, anchor ?? new Vector2(1, 0), position).AddComponent<VirtualInputButton>().Configure(action);
        }
        private GameObject CreateButton(string label, Vector2 anchor, Vector2 position)
        {
            var obj = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = (RectTransform)obj.transform;
            rect.SetParent(controls, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.sizeDelta = new Vector2(104, 104);
            rect.anchoredPosition = position;
            var image = obj.GetComponent<Image>();
            image.color = new Color(.07f, .13f, .19f, .68f);
            var button = obj.GetComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var textObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            var textRect = (RectTransform)textObj.transform;
            textRect.SetParent(rect, false);
            textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
            textRect.offsetMin = textRect.offsetMax = Vector2.zero;
            var text = textObj.GetComponent<TextMeshProUGUI>();
            text.font = font; text.text = label; text.fontSize = 29;
            text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
            return obj;
        }
    }
}
