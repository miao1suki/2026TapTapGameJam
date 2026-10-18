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
        private VirtualInputButton leftJump, sprint;
        private TextMeshProUGUI sprintLabel;
        private MobileButtonArtwork sprintArtwork;
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
            Direction("上", Vector2.up);
            Direction("左", Vector2.left);
            Direction("右", Vector2.right);
            Direction("下", Vector2.down);
            Action("左跳跃", InputActionId.Jump);
            leftJump = controls.Find("左跳跃").GetComponent<VirtualInputButton>(); leftJump.ConfigureRepeatJump();
            Action("跳跃", InputActionId.Jump);
            Action("跑", InputActionId.Sprint);
            sprint = controls.Find("跑").GetComponent<VirtualInputButton>(); sprint.ConfigureToggle();
            sprintLabel = sprint.GetComponentInChildren<TextMeshProUGUI>();
            sprintArtwork = sprint.GetComponent<MobileButtonArtwork>();
            Action("交互", InputActionId.Interact);
            Action("放左", InputActionId.DropLeft);
            Action("放右", InputActionId.DropRight);
            Action("选色", InputActionId.CameraModeSwitch);
            Direction("右手左", Vector2.left);
            Direction("右手右", Vector2.right);
            Action("重生", InputActionId.Respawn);
            var pause = CreateButton("暂停");
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
            bool rightDirections = alive && (player.ShowAirborneDownControl || player.IsClimbing ||
                player.IsHorizontalClimbing || leftJump != null && leftJump.IsPressed);
            Show("右手左", rightDirections); Show("右手右", rightDirections);
            if (sprintLabel != null)
            {
                string caption = sprint.ToggleOn ? "奔跑" : "行走";
                if (sprintLabel.text != caption) sprintLabel.text = caption;
            }
            if (sprintArtwork != null) sprintArtwork.SetRunning(sprint.ToggleOn);
            Show("上", alive && (player.IsSwimming || player.IsClimbing || player.IsHorizontalClimbing || player.IsGreenHookActive));
            Show("下", alive && (player.IsSwimming || player.IsClimbing || player.IsHorizontalClimbing || player.IsGreenHookActive || player.ShowAirborneDownControl));
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
            var direction = item != null ? item.GetComponent<VirtualDirectionButton>() : null;
            if (item != null && item.gameObject.activeSelf != show && (held == null || !held.IsPressed) &&
                (direction == null || !direction.IsPressed))
                item.gameObject.SetActive(show);
        }
        private void RefreshVisibility()
        {
            bool show = LayoutEditing || !GameplayPresentationGate.IsSuppressed &&
                (previewOnDesktop || GameInput.PlatformMode == InputPlatformMode.Mobile) &&
                Time.timeScale > 0 && (router == null || !router.IsPaused);
            if (visible == show) return;
            visible = show;
            controls.gameObject.SetActive(show);
        }
        public void TogglePause() => router?.TogglePause();
        private void Direction(string label, Vector2 direction)
        {
            CreateButton(label).AddComponent<VirtualDirectionButton>().Configure(direction);
        }
        private void Action(string label, InputActionId action)
        {
            CreateButton(label).AddComponent<VirtualInputButton>().Configure(action);
        }
        private GameObject CreateButton(string label)
        {
            GetDefaultLayout(label, out Vector2 anchor, out Vector2 position);
            var obj = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = (RectTransform)obj.transform;
            rect.SetParent(controls, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.sizeDelta = new Vector2(124, 124);
            rect.anchoredPosition = position;
            var image = obj.GetComponent<Image>();
            image.color = new Color(.07f, .13f, .19f, .68f);
            image.raycastPadding = Vector4.one * -14f;
            var button = obj.GetComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.pressedColor = new Color(.4f, .85f, 1f, 1f);
            colors.highlightedColor = Color.white;
            colors.selectedColor = Color.white;
            colors.fadeDuration = .05f;
            button.colors = colors;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            obj.AddComponent<TouchPressFeedback>();
            var textObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            var textRect = (RectTransform)textObj.transform;
            textRect.SetParent(rect, false);
            textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
            textRect.offsetMin = textRect.offsetMax = Vector2.zero;
            var text = textObj.GetComponent<TextMeshProUGUI>();
            text.font = font; text.text = label switch { "左跳跃" => "跳跃", "右手左" => "左", "右手右" => "右", "跑" => "行走", _ => label }; text.fontSize = 29;
            text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
            if (MobileButtonArtwork.Apply(obj, label)) text.enabled = false;
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
                "右" => new Vector2(270, 210), "下" => new Vector2(450, 120),
                "左跳跃" => new Vector2(400, 300), "跳跃" => new Vector2(-160, 200),
                "跑" => new Vector2(-315, 200), "交互" => new Vector2(-315, 360),
                "放左" => new Vector2(-470, 200), "放右" => new Vector2(-470, 360),
                "选色" => new Vector2(-160, 360), "重生" => new Vector2(-85, -70),
                "右手左" => new Vector2(-315, 520), "右手右" => new Vector2(-160, 520),
                "暂停" => new Vector2(90, -90), _ => Vector2.zero
            };
            if (label is "上" or "下" or "左" or "右" or "左跳跃") anchor = Vector2.zero;
            if (label == "重生") anchor = Vector2.one;
            if (label == "暂停") anchor = Vector2.up;
        }
    }
}
