using System;
using System.Collections.Generic;
using Project.InputAbstraction;
using Project.InputRebinding;
using Project.Settings;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Project.StartMenu
{
    [DisallowMultipleComponent]
    public sealed class KeyBindingPanelController : MonoBehaviour
    {
        private enum DeviceFilter
        {
            KeyboardMouse,
            Gamepad,
            Touch,
        }

        [SerializeField] private Text statusText;
        [SerializeField] private Button undoButton;
        [SerializeField] private Button redoButton;
        [SerializeField] private Button resetAllButton;
        [SerializeField] private Transform rowList;
        [SerializeField] private Button keyboardMouseTab;
        [SerializeField] private Button gamepadTab;
        [SerializeField] private Button touchTab;
        private InputBindingService service;
        private InputActionRebindingExtensions.RebindingOperation activeRebind;
        private InputSystemUIInputModule uiInputModule;
        private bool uiInputWasEnabled;
        private DeviceFilter deviceFilter = DeviceFilter.KeyboardMouse;
        private bool structureBuilt;
        private bool buttonsBound;
        private bool lastGamepadConnected;

        public void BuildStructure()
        {
            if (structureBuilt || rowList != null)
            {
                return;
            }

            structureBuilt = true;
            Image root = gameObject.GetComponent<Image>();
            if (root == null)
            {
                root = gameObject.AddComponent<Image>();
            }

            root.color = new Color(.04f, .06f, .08f, .94f);

            Text title = StartMenuUiFactory.CreateText(
                "Title",
                transform,
                "按键映射",
                24,
                TextAnchor.MiddleLeft,
                new Color(.92f, .98f, 1f, 1f));
            StartMenuUiFactory.SetTopLeft(
                title.rectTransform,
                new Vector2(22f, -16f),
                new Vector2(220f, 34f));

            RectTransform tabs = StartMenuUiFactory.CreateRect(
                "Device Tabs",
                transform);
            HorizontalLayoutGroup tabLayout =
                tabs.gameObject.AddComponent<HorizontalLayoutGroup>();
            tabLayout.spacing = 6f;
            tabLayout.childControlWidth = true;
            tabLayout.childControlHeight = true;
            tabLayout.childForceExpandWidth = false;
            tabLayout.childForceExpandHeight = false;
            StartMenuUiFactory.SetTopLeft(
                tabs,
                new Vector2(22f, -54f),
                new Vector2(470f, 34f));

            keyboardMouseTab = CreateTab(tabs, "键盘&鼠标");
            gamepadTab = CreateTab(tabs, "控制器");
            touchTab = CreateTab(tabs, "触屏");

            statusText = StartMenuUiFactory.CreateText(
                "Status",
                transform,
                "正在读取当前绑定。",
                13,
                TextAnchor.MiddleLeft,
                new Color(.64f, .76f, .84f, 1f));
            StartMenuUiFactory.SetTopLeft(
                statusText.rectTransform,
                new Vector2(22f, -92f),
                new Vector2(850f, 22f));

            RectTransform toolbar = StartMenuUiFactory.CreateRect(
                "Toolbar",
                transform);
            HorizontalLayoutGroup toolbarLayout =
                toolbar.gameObject.AddComponent<HorizontalLayoutGroup>();
            toolbarLayout.spacing = 6f;
            toolbarLayout.childControlWidth = true;
            toolbarLayout.childControlHeight = true;
            toolbarLayout.childForceExpandWidth = false;
            toolbarLayout.childForceExpandHeight = false;
            StartMenuUiFactory.SetTopLeft(
                toolbar,
                new Vector2(22f, -120f),
                new Vector2(520f, 30f));

            undoButton = CreateToolbarButton(toolbar, "撤销");
            redoButton = CreateToolbarButton(toolbar, "重做");
            resetAllButton = CreateToolbarButton(toolbar, "重置全部");

            CreateHeaderRow();
            ScrollRect scroll = StartMenuUiFactory.CreateVerticalScroll(
                "Binding Rows",
                transform,
                new Color(.025f, .04f, .055f, .74f),
                out RectTransform content);
            RectTransform scrollRect = scroll.GetComponent<RectTransform>();
            StartMenuUiFactory.SetTopLeft(
                scrollRect,
                new Vector2(22f, -190f),
                new Vector2(850f, 250f));
            rowList = content;
        }

        private void Awake()
        {
            BuildStructure();
            BindButtons();
        }

        private void OnEnable()
        {
            if (Application.isPlaying)
            {
                EnsureService();
                Refresh();
            }
        }

        private void OnDisable()
        {
            CancelRebind();
            RestoreUiInput();
            if (service != null)
            {
                service.Changed -= QueueRefresh;
            }
        }

        private void OnDestroy()
        {
            if (service != null)
            {
                service.Changed -= QueueRefresh;
                service.Dispose();
                service = null;
            }
        }

        private void EnsureService()
        {
            if (service != null)
            {
                return;
            }

            service = InputBindingService.CreateFromInputService(
                RuntimeSettingsPolicy.ApplyChangesToRuntime);
            service.Changed += QueueRefresh;
        }

        private void Refresh()
        {
            if (!Application.isPlaying || rowList == null)
            {
                return;
            }

            EnsureService();
            EnsureValidDeviceFilter();
            ClearChildren(rowList);
            int shown = 0;
            foreach (InputActionId actionId in
                     InputActionBindingPolicy.GetVisibleActions())
            {
                if (!TryFindBinding(actionId, out InputBindingInfo binding))
                {
                    continue;
                }

                CreateActionRow(actionId, binding);
                shown++;
            }

            if (shown == 0)
            {
                Text empty = StartMenuUiFactory.CreateText(
                    "Empty",
                    rowList,
                    "当前设备没有可显示的绑定。",
                    15,
                    TextAnchor.MiddleCenter,
                    new Color(.68f, .78f, .86f, .86f));
                empty.gameObject.AddComponent<LayoutElement>()
                    .preferredHeight = 48f;
            }

            if (undoButton != null)
            {
                undoButton.interactable = service.CanUndo;
            }

            if (redoButton != null)
            {
                redoButton.interactable = service.CanRedo;
            }

            UpdateTabVisuals();
            lastGamepadConnected = Gamepad.current != null;
            statusText.text = service.IsPreviewOnly
                ? "预览模式：可以测试改键，不会修改正式映射；移动固定只读。"
                : "移动固定为默认键位；其他操作点击按键框后开始接听。";
        }

        private bool TryFindBinding(
            InputActionId actionId,
            out InputBindingInfo binding)
        {
            IReadOnlyList<InputBindingInfo> bindings =
                service.GetBindings(actionId);
            InputBindingInfo? compositeFallback = null;
            for (int index = 0; index < bindings.Count; index++)
            {
                InputBindingInfo candidate = bindings[index];
                if (!MatchesDevice(candidate.Device))
                {
                    continue;
                }

                if (!candidate.IsComposite &&
                    !candidate.IsPartOfComposite)
                {
                    binding = candidate;
                    return true;
                }

                compositeFallback ??= candidate;
            }

            if (compositeFallback.HasValue)
            {
                binding = compositeFallback.Value;
                return true;
            }

            binding = default;
            return false;
        }

        private void CreateActionRow(
            InputActionId actionId,
            InputBindingInfo binding)
        {
            Image row = StartMenuUiFactory.CreateImage(
                "Action Row",
                rowList,
                new Color(.085f, .13f, .17f, .94f));
            HorizontalLayoutGroup layout =
                row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 5, 5);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 40f;

            Text name = StartMenuUiFactory.CreateText(
                "Action Name",
                row.transform,
                InputBindingService.GetActionDisplayName(actionId),
                16,
                TextAnchor.MiddleLeft,
                new Color(.9f, .97f, 1f, 1f));
            LayoutElement nameElement =
                name.gameObject.AddComponent<LayoutElement>();
            nameElement.preferredWidth = 220f;
            nameElement.preferredHeight = 30f;

            bool canModify = service.CanModifyBinding(actionId);
            bool readOnlyMove = !canModify &&
                                actionId == InputActionId.Move;
            bool editable =
                canModify &&
                !binding.IsComposite &&
                !binding.IsPartOfComposite;
            string keyLabel = readOnlyMove
                ? MoveDisplayLabel()
                : !canModify
                    ? binding.DisplayName + "（只读）"
                    : editable
                        ? binding.DisplayName
                        : binding.DisplayName + "（组合）";
            Button key = StartMenuUiFactory.CreateButton(
                "Key",
                row.transform,
                keyLabel,
                new Vector2(340f, 30f),
                null,
                new Color(.11f, .2f, .28f, .98f));
            LayoutElement keyElement =
                key.gameObject.AddComponent<LayoutElement>();
            keyElement.preferredWidth = 340f;
            keyElement.preferredHeight = 30f;
            if (editable)
            {
                key.onClick.AddListener(
                    () => StartRebind(actionId, binding, key));
            }
            else
            {
                key.interactable = false;
            }

            bool switchable =
                editable &&
                binding.IsButton &&
                InputActionInteractionPolicy.CanConfigureTrigger(actionId);
            Button trigger = StartMenuUiFactory.CreateButton(
                "Trigger",
                row.transform,
                switchable
                    ? binding.Trigger == InputBindingTrigger.Hold
                        ? "长按"
                        : "点击"
                    : "/",
                new Vector2(116f, 30f),
                null,
                switchable
                    ? new Color(.32f, .42f, .64f, .98f)
                    : new Color(.16f, .2f, .24f, .94f));
            LayoutElement triggerElement =
                trigger.gameObject.AddComponent<LayoutElement>();
            triggerElement.preferredWidth = 116f;
            triggerElement.preferredHeight = 30f;
            trigger.interactable = switchable;
            if (switchable)
            {
                trigger.onClick.AddListener(() =>
                {
                    service.SetTrigger(
                        actionId,
                        binding.BindingIndex,
                        binding.Trigger == InputBindingTrigger.Hold
                            ? InputBindingTrigger.Press
                            : InputBindingTrigger.Hold);
                    Refresh();
                });
            }
        }

        private void StartRebind(
            InputActionId actionId,
            InputBindingInfo binding,
            Button keyButton)
        {
            CancelRebind();
            DisableUiInputForRebind();
            keyButton.interactable = false;
            keyButton.GetComponentInChildren<Text>().text =
                "正在接听，请按下目标按键";
            statusText.text =
                $"正在接听：{InputBindingService.GetActionDisplayName(actionId)}";
            activeRebind = service.StartRebind(
                actionId,
                binding.BindingIndex,
                () =>
                {
                    activeRebind = null;
                    RestoreUiInput();
                    Refresh();
                },
                () =>
                {
                    activeRebind = null;
                    RestoreUiInput();
                    statusText.text = "已取消接听。";
                    Refresh();
                });
            if (activeRebind == null)
            {
                RestoreUiInput();
                statusText.text = "当前绑定无法开始接听。";
                Refresh();
            }
        }

        private void CancelRebind()
        {
            if (activeRebind == null)
            {
                return;
            }

            InputActionRebindingExtensions.RebindingOperation operation =
                activeRebind;
            activeRebind = null;
            operation.Cancel();
            RestoreUiInput();
        }

        private void DisableUiInputForRebind()
        {
            if (EventSystem.current == null || uiInputModule != null)
            {
                return;
            }

            uiInputModule =
                EventSystem.current.currentInputModule
                    as InputSystemUIInputModule;
            if (uiInputModule != null)
            {
                uiInputWasEnabled = uiInputModule.enabled;
                uiInputModule.enabled = false;
            }
        }

        private void RestoreUiInput()
        {
            if (uiInputModule == null)
            {
                return;
            }

            uiInputModule.enabled = uiInputWasEnabled;
            uiInputModule = null;
        }

        private bool MatchesDevice(InputBindingDevice device)
        {
            switch (deviceFilter)
            {
                case DeviceFilter.Gamepad:
                    return device == InputBindingDevice.Gamepad;
                case DeviceFilter.Touch:
                    return device == InputBindingDevice.Touch;
                default:
                    return device == InputBindingDevice.Keyboard ||
                           device == InputBindingDevice.Mouse;
            }
        }

        private string MoveDisplayLabel()
        {
            switch (deviceFilter)
            {
                case DeviceFilter.Gamepad:
                    return "左摇杆 / 十字键";
                case DeviceFilter.Touch:
                    return "/";
                default:
                    return "WASD（A/D 左右，W/S 上下）";
            }
        }

        private void BindButtons()
        {
            if (buttonsBound)
            {
                return;
            }

            undoButton?.onClick.AddListener(Undo);
            redoButton?.onClick.AddListener(Redo);
            resetAllButton?.onClick.AddListener(ResetAll);
            keyboardMouseTab?.onClick.AddListener(
                () => SetDeviceFilter(DeviceFilter.KeyboardMouse));
            gamepadTab?.onClick.AddListener(
                () => SetDeviceFilter(DeviceFilter.Gamepad));
            touchTab?.onClick.AddListener(
                () => SetDeviceFilter(DeviceFilter.Touch));
            buttonsBound = true;
        }

        private void SetDeviceFilter(DeviceFilter filter)
        {
            if (!IsDeviceAvailable(filter))
            {
                return;
            }

            deviceFilter = filter;
            Refresh();
        }

        private void UpdateTabVisuals()
        {
            keyboardMouseTab?.gameObject.SetActive(
                IsDeviceAvailable(DeviceFilter.KeyboardMouse));
            gamepadTab?.gameObject.SetActive(
                IsDeviceAvailable(DeviceFilter.Gamepad));
            touchTab?.gameObject.SetActive(
                IsDeviceAvailable(DeviceFilter.Touch));
            SetTabColor(
                keyboardMouseTab,
                deviceFilter == DeviceFilter.KeyboardMouse);
            SetTabColor(
                gamepadTab,
                deviceFilter == DeviceFilter.Gamepad);
            SetTabColor(
                touchTab,
                deviceFilter == DeviceFilter.Touch);
        }

        private void Update()
        {
            bool gamepadConnected = Gamepad.current != null;
            if (gamepadConnected != lastGamepadConnected &&
                Application.isPlaying)
            {
                Refresh();
            }
        }

        private void EnsureValidDeviceFilter()
        {
            if (IsDeviceAvailable(deviceFilter))
            {
                return;
            }

            if (IsDeviceAvailable(DeviceFilter.KeyboardMouse))
            {
                deviceFilter = DeviceFilter.KeyboardMouse;
            }
            else if (IsDeviceAvailable(DeviceFilter.Touch))
            {
                deviceFilter = DeviceFilter.Touch;
            }
            else if (IsDeviceAvailable(DeviceFilter.Gamepad))
            {
                deviceFilter = DeviceFilter.Gamepad;
            }
        }

        private bool IsDeviceAvailable(DeviceFilter filter)
        {
            if (filter == DeviceFilter.Gamepad)
            {
                return Gamepad.current != null;
            }

#if UNITY_EDITOR
            return filter == DeviceFilter.KeyboardMouse ||
                   Touchscreen.current != null;
#else
            InputPlatformMode platform = InputPlatformResolver.Current;
            if (filter == DeviceFilter.Touch)
            {
                return platform == InputPlatformMode.Mobile;
            }

            return platform == InputPlatformMode.Desktop;
#endif
        }

        private void Undo()
        {
            service.Undo();
            Refresh();
        }

        private void Redo()
        {
            service.Redo();
            Refresh();
        }

        private void ResetAll()
        {
            service.ResetAll();
            Refresh();
        }

        private void CreateHeaderRow()
        {
            RectTransform header = StartMenuUiFactory.CreateRect(
                "Header",
                transform);
            HorizontalLayoutGroup layout =
                header.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 0, 0);
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            StartMenuUiFactory.SetTopLeft(
                header,
                new Vector2(22f, -158f),
                new Vector2(850f, 26f));

            AddHeaderLabel(header, "操作", 220f);
            AddHeaderLabel(header, "按键", 340f);
            AddHeaderLabel(header, "点击 / 长按", 116f);
        }

        private Button CreateTab(Transform parent, string label)
        {
            Button button = StartMenuUiFactory.CreateButton(
                label,
                parent,
                label,
                new Vector2(130f, 32f),
                null,
                new Color(.16f, .24f, .32f, .98f));
            LayoutElement element =
                button.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = 130f;
            element.preferredHeight = 32f;
            return button;
        }

        private Button CreateToolbarButton(Transform parent, string label)
        {
            Button button = StartMenuUiFactory.CreateButton(
                label,
                parent,
                label,
                new Vector2(90f, 28f),
                null,
                new Color(.18f, .3f, .4f, .98f));
            LayoutElement element =
                button.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = 90f;
            element.preferredHeight = 28f;
            return button;
        }

        private static void AddHeaderLabel(
            Transform parent,
            string text,
            float width)
        {
            Text label = StartMenuUiFactory.CreateText(
                "Header",
                parent,
                text,
                12,
                TextAnchor.MiddleLeft,
                new Color(.58f, .7f, .78f, .9f));
            LayoutElement element =
                label.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = width;
            element.preferredHeight = 24f;
        }

        private static void SetTabColor(Button button, bool selected)
        {
            if (button == null)
            {
                return;
            }

            Image image = button.GetComponent<Image>();
            if (image != null)
            {
                image.color = selected
                    ? new Color(.22f, .55f, .82f, .98f)
                    : new Color(.16f, .24f, .32f, .98f);
            }
        }

        private void QueueRefresh()
        {
            if (Application.isPlaying && isActiveAndEnabled)
            {
                Refresh();
            }
        }

        private static void ClearChildren(Transform parent)
        {
            for (int index = parent.childCount - 1; index >= 0; index--)
            {
                GameObject child = parent.GetChild(index).gameObject;
                if (Application.isPlaying)
                {
                    Destroy(child);
                }
                else
                {
                    DestroyImmediate(child);
                }
            }
        }
    }
}
