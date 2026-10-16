using Project.InputAbstraction;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

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
        public bool LayoutEditing { get; set; }
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
            Direction("上", Vector2.up, new Vector2(245, 365));
            Direction("左", Vector2.left, new Vector2(115, 210));
            Direction("右", Vector2.right, new Vector2(270, 210));
            Direction("下", Vector2.down, new Vector2(400, 130));
            Action("左跳跃", InputActionId.Jump, new Vector2(400, 300), Vector2.zero);
            Action("跳跃", InputActionId.Jump, new Vector2(-120, 155));
            Action("跑", InputActionId.Sprint, new Vector2(-265, 155));
            Action("交互", InputActionId.Interact, new Vector2(-265, 325));
            Action("放左", InputActionId.DropLeft, new Vector2(-410, 155));
            Action("放右", InputActionId.DropRight, new Vector2(-410, 325));
            Action("选色", InputActionId.CameraModeSwitch, new Vector2(-120, 325));
            Action("重生", InputActionId.Respawn, new Vector2(-85, -70), Vector2.one);
            var pause = CreateButton("暂停", Vector2.one, new Vector2(-210, -70));
            pause.GetComponent<Button>().onClick.AddListener(TogglePause);
            ApplySavedLayout();
        }
        private void Awake()
        {
            var inputModule = EventSystem.current?.currentInputModule as InputSystemUIInputModule;
            if (inputModule != null) inputModule.pointerBehavior = UIPointerBehavior.AllPointersAsIs;
            // Upgrade serialized legacy HUDs without rewriting the designer's scene or prefab.
            if (Application.isPlaying && controls != null)
            {
                controls.gameObject.SetActive(false);
                Destroy(controls.gameObject);
                controls = null;
            }
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
        private void Update()
        {
            RefreshVisibility();
            if (controls == null || !controls.gameObject.activeInHierarchy || LayoutEditing) return;
            var player = MobileGameplayContext.Current;
            bool alive = player != null && !player.IsDead;
            bool carrying = alive && player.IsCarrying;
            Show("上", alive && (player.IsSwimming || player.IsClimbing || player.IsHorizontalClimbing || player.IsGreenHookActive));
            Show("下", alive && (player.IsSwimming || player.IsClimbing || player.IsHorizontalClimbing || player.IsGreenHookActive));
            Show("交互", alive && !carrying && (player.CarryQueueCount > 0 || player.HasContextInteraction || player.IsGreenHookActive));
            Show("放左", carrying); Show("放右", carrying);
            Show("重生", player != null && player.IsDead);
            Show("选色", alive && !carrying && !player.IsGreenHookActive);
        }
        private void Show(string label, bool show)
        {
            Transform item = controls.Find(label);
            // Never revoke a held press because its contextual condition changed mid-gesture.
            var held = item != null ? item.GetComponent<VirtualInputButton>() : null;
            if (item != null && item.gameObject.activeSelf != show && (held == null || !held.IsPressed))
                item.gameObject.SetActive(show);
        }
        private void RefreshVisibility()
        {
            bool show = LayoutEditing || (previewOnDesktop || GameInput.PlatformMode == InputPlatformMode.Mobile) &&
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
            rect.sizeDelta = new Vector2(124, 124);
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
            text.font = font; text.text = label == "左跳跃" ? "跳跃" : label; text.fontSize = 29;
            text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
            return obj;
        }

        public void ApplySavedLayout()
        {
            Canvas.ForceUpdateCanvases();
            foreach (RectTransform rect in controls)
                MobileControlLayout.Apply(rect, controls);
        }

        public static void GetDefaultLayout(string label, out Vector2 anchor, out Vector2 position)
        {
            anchor = new Vector2(1, 0);
            position = label switch
            {
                "上" => new Vector2(245, 365), "左" => new Vector2(115, 210),
                "右" => new Vector2(270, 210), "下" => new Vector2(400, 130),
                "左跳跃" => new Vector2(400, 300), "跳跃" => new Vector2(-120, 155),
                "跑" => new Vector2(-265, 155), "交互" => new Vector2(-265, 325),
                "放左" => new Vector2(-410, 155), "放右" => new Vector2(-410, 325),
                "选色" => new Vector2(-120, 325), "重生" => new Vector2(-85, -70),
                "暂停" => new Vector2(-210, -70), _ => Vector2.zero
            };
            if (label is "上" or "下" or "左" or "右" or "左跳跃") anchor = Vector2.zero;
            if (label is "重生" or "暂停") anchor = Vector2.one;
        }
    }
}
