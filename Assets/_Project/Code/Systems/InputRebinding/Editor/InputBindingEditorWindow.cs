using System;
using System.Collections.Generic;
using Project.InputAbstraction;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UIElements;

namespace Project.InputRebinding.Editor
{
    public sealed class InputBindingEditorWindow : EditorWindow
    {
        private sealed class KeyboardCaptureSession
        {
            public InputActionId ActionId;
            public Action<string> Preview;
            public Action<string> Completed;
            public Action Canceled;
            public bool NotifyOnCancel;
        }

        private static readonly string[] TriggerOptions =
        {
            "点击",
            "长按"
        };

        private static readonly string[] PolicyOptions =
        {
            "仅点击",
            "仅长按",
            "点击/长按可切换",
            "点击+长按"
        };

        private InputBindingService service;
        private VisualElement body;
        private ScrollView actionScroll;
        private Label status;
        private Button undoButton;
        private Button redoButton;
        private InputActionRebindingExtensions.RebindingOperation
            activeRebind;
        private InputAction keyboardCaptureAction;
        private InputActionRebindingExtensions.RebindingOperation
            keyboardCaptureOperation;
        private KeyboardCaptureSession keyboardCaptureSession;
        private InputActionId pendingKeyboardRebindAction;
        private int pendingKeyboardRebindBindingIndex = -1;
        private bool dragging;
        private bool refreshQueued;
        private Vector2 lastPointer;
        private Vector2 savedScrollOffset;
        private readonly HashSet<InputActionId> expandedActions =
            new HashSet<InputActionId>();

        [MenuItem("Tools/2026TapTap/Input/按键映射 %#k")]
        public static void Open()
        {
            InputBindingEditorWindow window =
                GetWindow<InputBindingEditorWindow>();
            window.titleContent = new GUIContent("按键映射");
            window.minSize = new Vector2(620f, 640f);
        }

        private void OnEnable()
        {
            EditorApplication.playModeStateChanged +=
                OnPlayModeStateChanged;
            InputSystem.onEvent += OnInputSystemEvent;
            ResetService();
            rootVisualElement.RegisterCallback<KeyDownEvent>(
                OnKeyDown,
                TrickleDown.TrickleDown);
            rootVisualElement.schedule.Execute(
                () => rootVisualElement.Focus());
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -=
                OnPlayModeStateChanged;
            InputSystem.onEvent -= OnInputSystemEvent;
            rootVisualElement.UnregisterCallback<KeyDownEvent>(
                OnKeyDown,
                TrickleDown.TrickleDown);
            ReleaseService();
        }

        private void OnPlayModeStateChanged(
            PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode ||
                state == PlayModeStateChange.ExitingPlayMode)
            {
                ReleaseService();
                rootVisualElement.Clear();
                return;
            }

            if (state == PlayModeStateChange.EnteredPlayMode ||
                state == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.delayCall += ResetService;
            }
        }

        private void Update()
        {
            if (pendingKeyboardRebindBindingIndex < 0 ||
                Keyboard.current == null)
            {
                return;
            }

            for (int index = 0;
                 index < Keyboard.current.allKeys.Count;
                 index++)
            {
                KeyControl key = Keyboard.current.allKeys[index];
                if (!key.wasPressedThisFrame)
                {
                    continue;
                }

                CompletePendingKeyboardRebind(
                    ToBindingPath(key.path));
                return;
            }
        }

        private void ResetService()
        {
            if (this == null)
            {
                return;
            }

            ReleaseService();
            service = InputBindingService.CreateForEditor();
            service.Changed += QueueRefresh;
            Build();
        }

        private void ReleaseService()
        {
            CancelPendingKeyboardRebind();
            CancelKeyboardCapture(false);
            if (activeRebind != null)
            {
                InputActionRebindingExtensions.RebindingOperation
                    operation = activeRebind;
                activeRebind = null;
                operation.Cancel();
            }

            if (service == null)
            {
                return;
            }

            service.Changed -= QueueRefresh;
            service.Dispose();
            service = null;
        }

        private void Build()
        {
            if (actionScroll != null)
            {
                savedScrollOffset = actionScroll.scrollOffset;
            }

            VisualElement root = rootVisualElement;
            root.Clear();
            root.focusable = true;
            root.style.flexGrow = 1f;
            root.style.backgroundColor = EditorGUIUtility.isProSkin
                ? new Color(0.055f, 0.067f, 0.078f, 1f)
                : new Color(0.91f, 0.925f, 0.94f, 1f);

            VisualElement shell = new VisualElement();
            shell.style.flexGrow = 1f;
            shell.style.backgroundColor = EditorGUIUtility.isProSkin
                ? new Color(0.075f, 0.09f, 0.105f, 1f)
                : new Color(0.97f, 0.975f, 0.98f, 1f);
            shell.style.borderTopLeftRadius = 7f;
            shell.style.borderTopRightRadius = 7f;
            shell.style.borderBottomLeftRadius = 7f;
            shell.style.borderBottomRightRadius = 7f;
            SetBorder(
                shell,
                EditorGUIUtility.isProSkin
                    ? new Color(0.14f, 0.17f, 0.19f, 1f)
                    : new Color(0.78f, 0.81f, 0.84f, 1f));
            root.Add(shell);

            VisualElement accent = new VisualElement();
            accent.style.height = 3f;
            accent.style.backgroundColor =
                new Color(0.18f, 0.55f, 0.95f, 1f);
            accent.style.borderTopLeftRadius = 7f;
            accent.style.borderTopRightRadius = 7f;
            shell.Add(accent);

            VisualElement header = Row();
            header.style.alignItems = Align.Center;
            header.style.paddingLeft = 12f;
            header.style.paddingRight = 10f;
            header.style.paddingTop = 9f;
            header.style.paddingBottom = 9f;
            header.style.backgroundColor = EditorGUIUtility.isProSkin
                ? new Color(0.09f, 0.11f, 0.13f, 1f)
                : new Color(0.93f, 0.945f, 0.96f, 1f);
            VisualElement titleGroup = new VisualElement();
            titleGroup.style.flexGrow = 1f;
            Label title = new Label("按键映射");
            title.style.fontSize = 17f;
            title.style.color = EditorGUIUtility.isProSkin
                ? new Color(0.94f, 0.97f, 1f, 1f)
                : new Color(0.09f, 0.13f, 0.17f, 1f);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleGroup.Add(title);
            Label subtitle = new Label(
                "键盘  ·  鼠标  ·  手柄  ·  触屏");
            subtitle.style.fontSize = 10f;
            subtitle.style.opacity = 0.58f;
            titleGroup.Add(subtitle);
            header.Add(titleGroup);

            status = new Label("已同步");
            status.style.paddingLeft = 9f;
            status.style.paddingRight = 9f;
            status.style.paddingTop = 4f;
            status.style.paddingBottom = 4f;
            status.style.marginRight = 7f;
            status.style.borderTopLeftRadius = 10f;
            status.style.borderTopRightRadius = 10f;
            status.style.borderBottomLeftRadius = 10f;
            status.style.borderBottomRightRadius = 10f;
            status.style.color = new Color(0.74f, 1f, 0.86f, 1f);
            status.style.backgroundColor =
                new Color(0.12f, 0.48f, 0.32f, 0.52f);
            header.Add(status);

            Button collapse = new Button
            {
                text = "−"
            };
            collapse.clicked += () =>
            {
                bool hidden =
                    body.style.display.value == DisplayStyle.None;
                body.style.display = hidden
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
                status.text = hidden ? "已收起" : "已同步";
                collapse.text = hidden ? "+" : "−";
            };
            collapse.style.width = 28f;
            collapse.style.height = 22f;
            collapse.style.backgroundColor =
                new Color(0.18f, 0.52f, 0.91f, 1f);
            collapse.style.color = Color.white;
            collapse.style.borderTopLeftRadius = 11f;
            collapse.style.borderTopRightRadius = 11f;
            collapse.style.borderBottomLeftRadius = 11f;
            collapse.style.borderBottomRightRadius = 11f;
            header.Add(collapse);
            shell.Add(header);
            MakeDraggable(header);

            body = new VisualElement();
            body.style.flexGrow = 1f;
            body.style.paddingLeft = 10f;
            body.style.paddingRight = 10f;
            body.style.paddingTop = 5f;
            body.style.paddingBottom = 9f;
            shell.Add(body);

            VisualElement toolbar = Row();
            toolbar.style.marginTop = 6f;
            undoButton = CreateButton("撤销", () =>
            {
                service.Undo();
                QueueRefresh();
            }, new Color(0.16f, 0.22f, 0.27f, 1f));
            redoButton = CreateButton("重做", () =>
            {
                service.Redo();
                QueueRefresh();
            }, new Color(0.16f, 0.22f, 0.27f, 1f));
            toolbar.Add(undoButton);
            toolbar.Add(redoButton);
            toolbar.Add(CreateButton("保存", () =>
            {
                service.Save();
                status.text = "已保存";
            }, new Color(0.13f, 0.48f, 0.31f, 1f)));
            toolbar.Add(CreateButton("重置全部", () =>
            {
                if (EditorUtility.DisplayDialog(
                        "重置按键",
                        "确定恢复所有默认键位吗？",
                        "重置",
                        "取消"))
                {
                    service.ResetAll();
                    QueueRefresh();
                }
            }, new Color(0.55f, 0.20f, 0.22f, 1f)));
            toolbar.Add(CreateButton(
                "刷新",
                QueueRefresh,
                new Color(0.19f, 0.31f, 0.42f, 1f)));
            body.Add(toolbar);

            Label hint = new Label(
                "仅蹲下和冲刺可调：点击切换 / 长按持续  ·  交互固定同时支持点击和长按  ·  其他动作固定按下触发");
            hint.style.marginTop = 7f;
            hint.style.fontSize = 10f;
            hint.style.opacity = 0.56f;
            body.Add(hint);

            actionScroll = new ScrollView();
            actionScroll.style.flexGrow = 1f;
            actionScroll.style.marginTop = 6f;
            actionScroll.horizontalScrollerVisibility =
                ScrollerVisibility.Hidden;
            body.Add(actionScroll);

            IReadOnlyList<InputActionId> actionIds =
                GetActionIds();
            for (int index = 0;
                 index < actionIds.Count;
                 index++)
            {
                if (!service.CanShowInBindingUI(actionIds[index]))
                {
                    continue;
                }

                actionScroll.Add(CreateActionSection(
                    actionIds[index]));
            }

            ScrollView currentScroll = actionScroll;
            Vector2 restoreOffset = savedScrollOffset;
            currentScroll.schedule.Execute(
                () => currentScroll.scrollOffset =
                    restoreOffset);

            Label footer = new Label(
                "按 X 隐藏  ·  Ctrl+Shift+K 重新呼出  ·  Ctrl+Z 撤销");
            footer.style.fontSize = 10f;
            footer.style.opacity = 0.46f;
            footer.style.marginTop = 5f;
            body.Add(footer);

            undoButton.SetEnabled(service.CanUndo);
            redoButton.SetEnabled(service.CanRedo);
        }

        private VisualElement CreateActionSection(
            InputActionId actionId)
        {
            bool hasButtonBinding = false;
            Foldout foldout = new Foldout
            {
                text =
                    $"{InputBindingService.GetActionDisplayName(actionId)} " +
                    $"({actionId})",
                value = expandedActions.Contains(actionId)
            };
            foldout.style.marginTop = 5f;
            foldout.style.paddingLeft = 7f;
            foldout.style.paddingRight = 7f;
            foldout.style.paddingTop = 4f;
            foldout.style.paddingBottom = 5f;
            foldout.style.backgroundColor =
                EditorGUIUtility.isProSkin
                    ? new Color(0.095f, 0.115f, 0.13f, 1f)
                    : new Color(0.945f, 0.955f, 0.965f, 1f);
            foldout.style.borderTopLeftRadius = 6f;
            foldout.style.borderTopRightRadius = 6f;
            foldout.style.borderBottomLeftRadius = 6f;
            foldout.style.borderBottomRightRadius = 6f;
            SetBorder(
                foldout,
                EditorGUIUtility.isProSkin
                    ? new Color(0.14f, 0.18f, 0.21f, 1f)
                    : new Color(0.81f, 0.84f, 0.87f, 1f));
            foldout.style.unityFontStyleAndWeight =
                FontStyle.Bold;
            foldout.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue)
                {
                    expandedActions.Add(actionId);
                }
                else
                {
                    expandedActions.Remove(actionId);
                    if (keyboardCaptureSession != null &&
                        keyboardCaptureSession.ActionId == actionId)
                    {
                        CancelKeyboardCaptureForAction(
                            actionId,
                            true);
                    }
                }
            });

            IReadOnlyList<InputBindingInfo> bindings =
                service.GetBindings(actionId);
            bool canModify = service.CanModifyBinding(actionId);
            for (int index = 0;
                 index < bindings.Count;
                 index++)
            {
                if (bindings[index].IsButton)
                {
                    hasButtonBinding = true;
                    break;
                }
            }

            if (hasButtonBinding)
            {
                InputActionTriggerPolicy policy =
                    InputActionInteractionPolicy.GetPolicy(actionId);
                Label triggerSummary = new Label(
                    policy == InputActionTriggerPolicy.Switchable
                        ? $"当前触发方式：{GetTriggerLabel(service.GetActionTrigger(actionId))} · 点击切换，长按持续"
                        : policy == InputActionTriggerPolicy.ClickAndHold
                            ? "触发策略：点击 + 长按（固定，两者都可用）"
                            : $"触发策略：{GetPolicyLabel(policy)}");
                triggerSummary.style.fontSize = 10f;
                triggerSummary.style.marginLeft = 2f;
                triggerSummary.style.marginBottom = 3f;
                triggerSummary.style.opacity = 0.56f;
                foldout.Add(triggerSummary);
            }

            if (!canModify)
            {
                Label readOnly = new Label(
                    "默认只读：移动固定使用 WASD / 左摇杆 / 十字键。");
                readOnly.style.fontSize = 10f;
                readOnly.style.marginLeft = 2f;
                readOnly.style.marginBottom = 3f;
                readOnly.style.opacity = 0.62f;
                foldout.Add(readOnly);
            }

            if (bindings.Count == 0)
            {
                Label empty = new Label("当前映射中没有这个动作。");
                empty.style.opacity = 0.62f;
                foldout.Add(empty);
            }
            else
            {
                for (int index = 0;
                     index < bindings.Count;
                     index++)
                {
                    foldout.Add(CreateBindingRow(
                        actionId,
                        bindings[index]));
                }
            }

            if (canModify)
            {
                foldout.Add(CreateAddBindingRow(
                    actionId,
                    bindings.Count > 0));
            }
            return foldout;
        }

        private VisualElement CreateBindingRow(
            InputActionId actionId,
            InputBindingInfo binding)
        {
            bool canModify = service.CanModifyBinding(actionId);
            VisualElement card = new VisualElement();
            card.style.backgroundColor =
                EditorGUIUtility.isProSkin
                    ? new Color(0.13f, 0.155f, 0.175f, 1f)
                    : new Color(0.895f, 0.91f, 0.925f, 1f);
            card.style.borderTopLeftRadius = 6f;
            card.style.borderTopRightRadius = 6f;
            card.style.borderBottomLeftRadius = 6f;
            card.style.borderBottomRightRadius = 6f;
            card.style.borderLeftWidth = 3f;
            card.style.borderLeftColor =
                GetDeviceAccent(binding.Device);
            card.style.paddingLeft = 8f;
            card.style.paddingRight = 7f;
            card.style.paddingTop = 6f;
            card.style.paddingBottom = 6f;
            card.style.marginTop = 4f;

            VisualElement row = Row();
            row.style.alignItems = Align.Center;
            row.Add(CreateDeviceIcon(binding.Device));
            Label device = new Label(
                GetDeviceLabel(binding.Device));
            device.style.width = 42f;
            device.style.unityFontStyleAndWeight =
                FontStyle.Bold;
            row.Add(device);

            Label display = new Label(binding.DisplayName);
            display.style.flexGrow = 1f;
            display.style.marginLeft = 4f;
            display.style.unityFontStyleAndWeight =
                FontStyle.Bold;
            row.Add(display);

            if (canModify &&
                binding.IsButton &&
                InputActionInteractionPolicy
                    .CanConfigureTrigger(actionId))
            {
                PopupField<string> trigger =
                    new PopupField<string>(
                        new List<string>(TriggerOptions),
                        ToTriggerIndex(binding.Trigger));
                trigger.style.width = 80f;
                trigger.tooltip =
                    "点击：点按切换；长按：按住生效";
                trigger.RegisterValueChangedCallback(evt =>
                {
                    service.SetTrigger(
                        actionId,
                        binding.BindingIndex,
                        FromTriggerLabel(evt.newValue));
                });
                row.Add(trigger);
            }
            else if (binding.IsButton &&
                     InputActionInteractionPolicy.IsClickAndHold(
                         actionId))
            {
                Label both = new Label("点击+长按");
                both.style.width = 80f;
                both.style.unityTextAlign =
                    TextAnchor.MiddleCenter;
                both.style.opacity = .72f;
                row.Add(both);
            }

            Button rebind = CreateButton(
                canModify ? "改键" : "只读",
                () =>
            {
                StartRebind(
                    actionId,
                    binding,
                    display);
            }, canModify
                ? new Color(0.16f, 0.40f, 0.66f, 1f)
                : new Color(0.20f, 0.24f, 0.28f, 1f));
            rebind.style.width = 52f;
            rebind.SetEnabled(canModify);
            row.Add(rebind);

            Button reset = CreateButton("重置", () =>
            {
                service.ResetBinding(
                    actionId,
                    binding.BindingIndex);
                QueueRefresh();
            }, new Color(0.20f, 0.30f, 0.38f, 1f));
            reset.style.width = 52f;
            reset.SetEnabled(canModify);
            row.Add(reset);

            Button remove = CreateButton("删除", () =>
            {
                service.RemoveBinding(
                    actionId,
                    binding.BindingIndex);
                QueueRefresh();
            }, new Color(0.48f, 0.20f, 0.22f, 1f));
            remove.style.width = 52f;
            remove.SetEnabled(canModify);
            row.Add(remove);
            card.Add(row);
            return card;
        }

        private VisualElement CreateAddBindingRow(
            InputActionId actionId,
            bool allowClear)
        {
            VisualElement card = new VisualElement();
            card.style.marginTop = 6f;
            card.style.paddingLeft = 8f;
            card.style.paddingRight = 8f;
            card.style.paddingTop = 7f;
            card.style.paddingBottom = 7f;
            card.style.backgroundColor =
                EditorGUIUtility.isProSkin
                    ? new Color(0.105f, 0.13f, 0.15f, 1f)
                    : new Color(0.925f, 0.94f, 0.95f, 1f);
            card.style.borderTopLeftRadius = 6f;
            card.style.borderTopRightRadius = 6f;
            card.style.borderBottomLeftRadius = 6f;
            card.style.borderBottomRightRadius = 6f;
            SetBorder(
                card,
                EditorGUIUtility.isProSkin
                    ? new Color(0.17f, 0.22f, 0.25f, 1f)
                    : new Color(0.78f, 0.82f, 0.85f, 1f));

            Label heading = new Label("新增绑定");
            heading.style.unityFontStyleAndWeight =
                FontStyle.Bold;
            heading.style.marginBottom = 4f;
            card.Add(heading);

            List<string> deviceOptions = new List<string>
            {
                "键盘",
                "鼠标",
                "手柄",
                "触屏"
            };
            InputBindingDevice recommendedDevice =
                GetRecommendedDevice(actionId);
            int deviceIndex = Mathf.Max(
                0,
                deviceOptions.IndexOf(
                    GetDeviceLabel(recommendedDevice)));
            PopupField<string> device =
                new PopupField<string>(
                    deviceOptions,
                    deviceIndex);
            device.style.height = 23f;
            string recommendedControl =
                GetRecommendedControl(
                    actionId,
                    recommendedDevice);
            InputActionTriggerPolicy selectedPolicy =
                InputActionInteractionPolicy.GetPolicy(actionId);
            string selectedPath =
                GetControlPath(
                    recommendedDevice,
                    recommendedControl);
            Label keyboardValueLabel = null;

            void StartKeyboardCaptureAndAdd()
            {
                if (FromDeviceLabel(device.value) !=
                    InputBindingDevice.Keyboard)
                {
                    return;
                }

                InputActionTriggerPolicy capturePolicy =
                    selectedPolicy;
                if (keyboardValueLabel != null)
                {
                    keyboardValueLabel.text = "监听中";
                }

                status.text = "正在听取键盘…";
                StartKeyboardCapture(
                    actionId,
                    previewName =>
                    {
                        if (keyboardValueLabel != null)
                        {
                            keyboardValueLabel.text =
                                $"识别：{previewName}";
                        }
                    },
                    finalPath =>
                    {
                        service.SetTriggerPolicy(
                            actionId,
                            capturePolicy);
                        int bindingIndex =
                            service.AddBinding(
                                actionId,
                                finalPath,
                                InputBindingDevice.Keyboard,
                                GetDefaultBindingTrigger(
                                    capturePolicy));
                        if (bindingIndex < 0)
                        {
                            if (keyboardValueLabel != null)
                            {
                                keyboardValueLabel.text =
                                    "添加失败";
                            }

                            status.text = "添加失败";
                            QueueRefresh();
                            return;
                        }

                        selectedPath = finalPath;
                        if (keyboardValueLabel != null)
                        {
                            keyboardValueLabel.text =
                                GetKeyboardDisplayName(
                                    finalPath);
                        }

                        status.text = "已添加";
                        QueueRefresh();
                    },
                    () =>
                    {
                        selectedPath = string.Empty;
                        if (keyboardValueLabel != null)
                        {
                            keyboardValueLabel.text = "已取消";
                        }

                        status.text = "已取消";
                        QueueRefresh();
                    });
            }

            VisualElement controlHost = new VisualElement();
            controlHost.style.flexGrow = 1f;
            void RebuildControlHost()
            {
                controlHost.Clear();
                keyboardValueLabel = null;
                InputBindingDevice selectedDevice =
                    FromDeviceLabel(device.value);
                if (selectedDevice ==
                    InputBindingDevice.Keyboard)
                {
                    selectedPath = string.Empty;
                    VisualElement listenRow = Row();
                    listenRow.style.alignItems = Align.Center;
                    Label keyValue = new Label("未听取");
                    keyboardValueLabel = keyValue;
                    keyValue.style.flexGrow = 1f;
                    keyValue.style.height = 23f;
                    keyValue.style.unityTextAlign =
                        TextAnchor.MiddleLeft;
                    keyValue.style.paddingLeft = 7f;
                    keyValue.style.backgroundColor =
                        EditorGUIUtility.isProSkin
                            ? new Color(0.12f, 0.15f, 0.17f, 1f)
                            : new Color(0.90f, 0.92f, 0.94f, 1f);
                    listenRow.Add(keyValue);
                    listenRow.Add(CreateButton(
                        "听取并添加",
                        StartKeyboardCaptureAndAdd,
                        new Color(0.18f, 0.38f, 0.64f, 1f)));
                    controlHost.Add(listenRow);
                    return;
                }

                string recommended =
                    GetRecommendedControl(
                        actionId,
                        selectedDevice);
                List<string> options =
                    GetControlOptions(selectedDevice);
                int selectedIndex = Mathf.Max(
                    0,
                    options.IndexOf(recommended));
                PopupField<string> control =
                    new PopupField<string>(
                        options,
                        selectedIndex);
                control.style.flexGrow = 1f;
                control.style.height = 23f;
                selectedPath = GetControlPath(
                    selectedDevice,
                    control.value);
                control.RegisterValueChangedCallback(evt =>
                {
                    selectedPath = GetControlPath(
                        selectedDevice,
                        evt.newValue);
                });
                controlHost.Add(control);
            }
            device.RegisterValueChangedCallback(
                evt =>
                {
                    CancelKeyboardCaptureForAction(
                        actionId,
                        true);
                    RebuildControlHost();
                });
            RebuildControlHost();

            VisualElement firstRow = Row();
            firstRow.style.alignItems = Align.Center;
            Label deviceLabel = new Label("平台");
            deviceLabel.style.width = 34f;
            firstRow.Add(deviceLabel);
            device.style.width = 88f;
            firstRow.Add(device);
            Label controlLabel = new Label("初始键位");
            controlLabel.style.width = 62f;
            controlLabel.style.marginLeft = 8f;
            firstRow.Add(controlLabel);
            firstRow.Add(controlHost);
            card.Add(firstRow);

            VisualElement secondRow = Row();
            secondRow.style.alignItems = Align.Center;
            secondRow.style.marginTop = 5f;
            Label policyLabel = new Label("触发策略");
            policyLabel.style.width = 62f;
            secondRow.Add(policyLabel);
            PopupField<string> policyField =
                new PopupField<string>(
                    new List<string>(PolicyOptions),
                    (int)selectedPolicy);
            policyField.style.width = 150f;
            policyField.SetEnabled(
                InputActionInteractionPolicy.CanConfigureTrigger(
                    actionId));
            policyField.RegisterValueChangedCallback(evt =>
            {
                selectedPolicy =
                    FromPolicyLabel(evt.newValue);
            });
            secondRow.Add(policyField);

            secondRow.Add(CreateButton("＋ 添加", () =>
            {
                if (FromDeviceLabel(device.value) ==
                    InputBindingDevice.Keyboard)
                {
                    StartKeyboardCaptureAndAdd();
                    return;
                }

                service.SetTriggerPolicy(
                    actionId,
                    selectedPolicy);
                int bindingIndex = service.AddBinding(
                    actionId,
                    selectedPath,
                    FromDeviceLabel(device.value),
                    GetDefaultBindingTrigger(selectedPolicy));
                if (bindingIndex >= 0)
                {
                    status.text = "已添加";
                    QueueRefresh();
                }
            }, new Color(0.12f, 0.44f, 0.63f, 1f)));

            if (allowClear)
            {
                secondRow.Add(CreateButton("清空", () =>
                {
                    if (!EditorUtility.DisplayDialog(
                            "清空绑定",
                            $"清空 {InputBindingService.GetActionDisplayName(actionId)} 的全部键位吗？",
                            "清空",
                            "取消"))
                    {
                        return;
                    }

                    service.ClearBindings(actionId);
                    QueueRefresh();
                }, new Color(0.46f, 0.22f, 0.24f, 1f)));
            }

            card.Add(secondRow);
            return card;
        }

        private void StartRebind(
            InputActionId actionId,
            InputBindingInfo binding,
            Label display)
        {
            if (binding.Device == InputBindingDevice.Keyboard)
            {
                activeRebind?.Cancel();
                activeRebind = null;
                pendingKeyboardRebindAction = actionId;
                pendingKeyboardRebindBindingIndex =
                    binding.BindingIndex;
                display.text = "请按键盘按键…";
                status.text =
                    $"正在接听：{InputBindingService.GetActionDisplayName(actionId)}";
                rootVisualElement.Focus();
                return;
            }

            display.text = "请按键…";
            InputActionRebindingExtensions.RebindingOperation
                operation = service.StartRebind(
                    actionId,
                    binding.BindingIndex,
                    () =>
                    {
                        activeRebind = null;
                        QueueRefresh();
                    },
                    () =>
                    {
                        activeRebind = null;
                        QueueRefresh();
                    });
            if (operation == null)
            {
                Refresh();
                return;
            }

            activeRebind = operation;
        }

        private void CancelPendingKeyboardRebind()
        {
            pendingKeyboardRebindAction = default;
            pendingKeyboardRebindBindingIndex = -1;
        }

        private void CompletePendingKeyboardRebind(
            KeyCode keyCode)
        {
            string path = KeyCodeToControlPath(keyCode);
            if (string.IsNullOrEmpty(path))
            {
                status.text = "暂不支持这个键盘按键。";
                return;
            }

            CompletePendingKeyboardRebind(path);
        }

        private void CompletePendingKeyboardRebind(
            string path)
        {
            bool changed = service.SetBindingPath(
                pendingKeyboardRebindAction,
                pendingKeyboardRebindBindingIndex,
                path);
            CancelPendingKeyboardRebind();
            status.text = changed
                ? $"已改为：{GetKeyboardDisplayName(path)}"
                : "写入绑定失败。";
            QueueRefresh();
        }

        private void OnInputSystemEvent(
            InputEventPtr eventPtr,
            InputDevice device)
        {
            if (pendingKeyboardRebindBindingIndex < 0 ||
                device is not Keyboard keyboard)
            {
                return;
            }

            for (int index = 0;
                 index < keyboard.allKeys.Count;
                 index++)
            {
                KeyControl key = keyboard.allKeys[index];
                if (!key.ReadValueFromEvent(
                        eventPtr,
                        out float value) ||
                    value <= .5f)
                {
                    continue;
                }

                CompletePendingKeyboardRebind(
                    ToBindingPath(key.path));
                return;
            }
        }

        private static string ToBindingPath(string controlPath)
        {
            if (string.IsNullOrWhiteSpace(controlPath))
            {
                return null;
            }

            if (controlPath.StartsWith("/"))
            {
                int slash = controlPath.IndexOf(
                    '/',
                    1);
                if (slash > 1)
                {
                    return "<" +
                           controlPath.Substring(1, slash - 1) +
                           ">" +
                           controlPath.Substring(slash);
                }
            }

            return controlPath;
        }

        private void StartKeyboardCapture(
            InputActionId actionId,
            Action<string> preview,
            Action<string> completed,
            Action canceled)
        {
            CancelKeyboardCapture(true);
            keyboardCaptureSession = new KeyboardCaptureSession
            {
                ActionId = actionId,
                Preview = preview,
                Completed = completed,
                Canceled = canceled,
                NotifyOnCancel = true
            };
            keyboardCaptureAction = new InputAction(
                "KeyboardKeyCapture",
                InputActionType.Button);
            keyboardCaptureAction.AddBinding(
                "<Keyboard>/space");
            InputAction captureAction = keyboardCaptureAction;
            InputActionRebindingExtensions.RebindingOperation
                operation = captureAction
                    .PerformInteractiveRebinding(0)
                    .WithCancelingThrough("<Keyboard>/escape")
                    .WithControlsHavingToMatchPath(
                        "<Keyboard>/*")
                    .WithControlsExcluding("<Mouse>/position")
                    .WithControlsExcluding("<Mouse>/delta")
                    .OnPotentialMatch(result =>
                    {
                        keyboardCaptureSession?.Preview?.Invoke(
                            result.selectedControl?.displayName ??
                            string.Empty);
                        result.Complete();
                    })
                    .OnComplete(result =>
                    {
                        string path =
                            captureAction.bindings[0].effectivePath;
                        FinishKeyboardCapture(
                            path,
                            false,
                            true);
                    })
                    .OnCancel(result =>
                        FinishKeyboardCapture(
                            null,
                            true,
                            keyboardCaptureSession
                                ?.NotifyOnCancel ?? true));
            keyboardCaptureOperation = operation;
            operation.Start();
        }

        private void CancelKeyboardCapture(bool notify)
        {
            if (keyboardCaptureSession != null)
            {
                keyboardCaptureSession.NotifyOnCancel =
                    notify;
            }

            if (keyboardCaptureOperation == null)
            {
                FinishKeyboardCapture(
                    null,
                    true,
                    notify);
                return;
            }

            keyboardCaptureOperation.Cancel();
            if (keyboardCaptureOperation != null)
            {
                FinishKeyboardCapture(
                    null,
                    true,
                    notify);
            }
        }

        private void CancelKeyboardCaptureForAction(
            InputActionId actionId,
            bool notify)
        {
            if (keyboardCaptureSession != null &&
                keyboardCaptureSession.ActionId == actionId)
            {
                CancelKeyboardCapture(notify);
            }
        }

        private void FinishKeyboardCapture(
            string path,
            bool canceled,
            bool notify)
        {
            KeyboardCaptureSession session =
                keyboardCaptureSession;
            InputActionRebindingExtensions.RebindingOperation
                operation = keyboardCaptureOperation;
            InputAction action = keyboardCaptureAction;
            keyboardCaptureSession = null;
            keyboardCaptureOperation = null;
            keyboardCaptureAction = null;
            operation?.Dispose();
            action?.Disable();
            action?.Dispose();

            if (!notify || session == null)
            {
                return;
            }

            if (canceled)
            {
                session.Canceled?.Invoke();
                return;
            }

            session.Completed?.Invoke(path);
        }

        private void Refresh()
        {
            if (this == null ||
                rootVisualElement == null ||
                service == null ||
                service.Asset == null)
            {
                return;
            }

            CancelPendingKeyboardRebind();
            InputService.EnsureRequiredActions(service.Asset);
            Build();
        }

        private void QueueRefresh()
        {
            if (refreshQueued)
            {
                return;
            }

            refreshQueued = true;
            EditorApplication.delayCall += () =>
            {
                refreshQueued = false;
                Refresh();
            };
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (service == null)
            {
                return;
            }

            if (pendingKeyboardRebindBindingIndex >= 0)
            {
                CompletePendingKeyboardRebind(evt.keyCode);
                evt.StopImmediatePropagation();
                return;
            }

            if (evt.target is TextField)
            {
                return;
            }

            bool command =
                (evt.modifiers &
                 (EventModifiers.Control |
                  EventModifiers.Command)) != 0;
            if (command && evt.keyCode == KeyCode.Z)
            {
                if (evt.shiftKey)
                {
                    service.Redo();
                }
                else
                {
                    service.Undo();
                }

                Refresh();
                evt.StopPropagation();
                return;
            }

            if (command && evt.keyCode == KeyCode.Y)
            {
                service.Redo();
                Refresh();
                evt.StopPropagation();
                return;
            }

            if (evt.keyCode == KeyCode.X)
            {
                evt.StopPropagation();
                Close();
            }
        }

        private void MakeDraggable(VisualElement handle)
        {
            handle.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0 ||
                    evt.target is Button)
                {
                    return;
                }

                dragging = true;
                lastPointer = GUIUtility.GUIToScreenPoint(
                    new Vector2(
                        evt.position.x,
                        evt.position.y));
                handle.CapturePointer(evt.pointerId);
                evt.StopPropagation();
            });

            handle.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (!dragging)
                {
                    return;
                }

                Vector2 pointer = GUIUtility.GUIToScreenPoint(
                    new Vector2(
                        evt.position.x,
                        evt.position.y));
                Vector2 delta = pointer - lastPointer;
                lastPointer = pointer;
                position = new Rect(
                    position.x + delta.x,
                    position.y + delta.y,
                    position.width,
                    position.height);
                evt.StopPropagation();
            });

            handle.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (!dragging)
                {
                    return;
                }

                dragging = false;
                handle.ReleasePointer(evt.pointerId);
                evt.StopPropagation();
            });
        }

        private static IReadOnlyList<InputActionId> GetActionIds()
        {
            return InputActionBindingPolicy.GetVisibleActions();
        }

        private static string GetDeviceLabel(
            InputBindingDevice device)
        {
            switch (device)
            {
                case InputBindingDevice.Keyboard:
                    return "键盘";
                case InputBindingDevice.Mouse:
                    return "鼠标";
                case InputBindingDevice.Gamepad:
                    return "手柄";
                case InputBindingDevice.Touch:
                    return "触屏";
                default:
                    return "其他";
            }
        }

        private static Color GetDeviceAccent(
            InputBindingDevice device)
        {
            switch (device)
            {
                case InputBindingDevice.Keyboard:
                    return new Color(0.20f, 0.62f, 0.96f, 1f);
                case InputBindingDevice.Mouse:
                    return new Color(0.17f, 0.76f, 0.58f, 1f);
                case InputBindingDevice.Gamepad:
                    return new Color(0.94f, 0.58f, 0.18f, 1f);
                case InputBindingDevice.Touch:
                    return new Color(0.72f, 0.45f, 0.94f, 1f);
                default:
                    return new Color(0.55f, 0.60f, 0.64f, 1f);
            }
        }

        private static Image CreateDeviceIcon(
            InputBindingDevice device)
        {
            string fileName;
            switch (device)
            {
                case InputBindingDevice.Keyboard:
                    fileName = "Keyboard";
                    break;
                case InputBindingDevice.Mouse:
                    fileName = "Mouse";
                    break;
                case InputBindingDevice.Gamepad:
                    fileName = "Gamepad";
                    break;
                case InputBindingDevice.Touch:
                    fileName = "Touch";
                    break;
                default:
                    fileName = "InputControl";
                    break;
            }

            string prefix = EditorGUIUtility.isProSkin
                ? "d_"
                : string.Empty;
            Texture2D texture =
                AssetDatabase.LoadAssetAtPath<Texture2D>(
                    $"Packages/com.unity.inputsystem/InputSystem/Editor/Icons/{prefix}{fileName}.png");
            if (texture == null && prefix.Length > 0)
            {
                texture =
                    AssetDatabase.LoadAssetAtPath<Texture2D>(
                        $"Packages/com.unity.inputsystem/InputSystem/Editor/Icons/{fileName}.png");
            }

            Image icon = new Image
            {
                image = texture,
                scaleMode = ScaleMode.ScaleToFit,
                tooltip = GetDeviceLabel(device)
            };
            icon.style.width = 22f;
            icon.style.height = 22f;
            icon.style.marginRight = 4f;
            return icon;
        }

        private static int ToTriggerIndex(
            InputBindingTrigger trigger)
        {
            return Mathf.Clamp(
                (int)trigger,
                0,
                TriggerOptions.Length - 1);
        }

        private static InputBindingTrigger FromTriggerLabel(
            string label)
        {
            switch (label)
            {
                case "长按":
                    return InputBindingTrigger.Hold;
                default:
                    return InputBindingTrigger.Press;
            }
        }

        private static InputActionTriggerPolicy FromPolicyLabel(
            string label)
        {
            switch (label)
            {
                case "仅长按":
                    return InputActionTriggerPolicy.HoldOnly;
                case "点击/长按可切换":
                    return InputActionTriggerPolicy.Switchable;
                case "点击+长按":
                    return InputActionTriggerPolicy.ClickAndHold;
                default:
                    return InputActionTriggerPolicy.ClickOnly;
            }
        }

        private static string GetPolicyLabel(
            InputActionTriggerPolicy policy)
        {
            switch (policy)
            {
                case InputActionTriggerPolicy.HoldOnly:
                    return "仅长按";
                case InputActionTriggerPolicy.Switchable:
                    return "点击/长按可切换";
                case InputActionTriggerPolicy.ClickAndHold:
                    return "点击+长按";
                default:
                    return "仅点击";
            }
        }

        private static InputBindingTrigger GetDefaultBindingTrigger(
            InputActionTriggerPolicy policy)
        {
            return policy == InputActionTriggerPolicy.HoldOnly
                ? InputBindingTrigger.Hold
                : InputBindingTrigger.Press;
        }

        private static string GetTriggerLabel(
            InputActionTrigger trigger)
        {
            switch (trigger)
            {
                case InputActionTrigger.Hold:
                    return "长按";
                default:
                    return "点击";
            }
        }

        private static InputBindingDevice GetRecommendedDevice(
            InputActionId actionId)
        {
            switch (actionId)
            {
                case InputActionId.Attack:
                case InputActionId.Carry:
                case InputActionId.PointerPrimary:
                case InputActionId.PointerSecondary:
                    return InputBindingDevice.Mouse;
                case InputActionId.Navigate:
                    return InputBindingDevice.Gamepad;
                default:
                    return InputBindingDevice.Keyboard;
            }
        }

        private static string GetRecommendedControl(
            InputActionId actionId,
            InputBindingDevice device)
        {
            if (device == InputBindingDevice.Mouse)
            {
                switch (actionId)
                {
                    case InputActionId.Look:
                        return "鼠标移动";
                    case InputActionId.Attack:
                    case InputActionId.PointerPrimary:
                        return "鼠标左键";
                    case InputActionId.Carry:
                    case InputActionId.PointerSecondary:
                        return "鼠标右键";
                }
            }

            if (device == InputBindingDevice.Gamepad &&
                actionId == InputActionId.Navigate)
            {
                return "左摇杆";
            }

            switch (actionId)
            {
                case InputActionId.Move:
                    return "W";
                case InputActionId.Jump:
                    return "空格";
                case InputActionId.Interact:
                    return "E";
                case InputActionId.Cancel:
                case InputActionId.Pause:
                    return "Escape";
                case InputActionId.Submit:
                    return "回车";
                case InputActionId.Crouch:
                    return "左 Ctrl";
                case InputActionId.Sprint:
                    return "左 Shift";
                case InputActionId.CameraModeSwitch:
                    return "Tab";
                default:
                    return GetControlOptions(device)[0];
            }
        }

        private static List<string> GetControlOptions(
            InputBindingDevice device)
        {
            switch (device)
            {
                case InputBindingDevice.Mouse:
                    return new List<string>
                    {
                        "鼠标左键",
                        "鼠标右键",
                        "鼠标中键",
                        "鼠标移动"
                    };
                case InputBindingDevice.Gamepad:
                    return new List<string>
                    {
                        "手柄下键",
                        "手柄右键",
                        "手柄左键",
                        "手柄上键",
                        "左肩键",
                        "右肩键",
                        "菜单键",
                        "视图键",
                        "左摇杆按下",
                        "右摇杆按下",
                        "左摇杆",
                        "右摇杆",
                        "十字键上",
                        "十字键下",
                        "十字键左",
                        "十字键右"
                    };
                case InputBindingDevice.Touch:
                    return new List<string>
                    {
                        "主触点轻触",
                        "主触点位置"
                    };
                default:
                    return new List<string>
                    {
                        "空格",
                        "回车",
                        "E",
                        "Q",
                        "F",
                        "Tab",
                        "左 Ctrl",
                        "左 Shift",
                        "Escape",
                        "W",
                        "A",
                        "S",
                        "D",
                        "Z",
                        "X",
                        "C",
                        "R"
                    };
            }
        }

        private static string GetControlPath(
            InputBindingDevice device,
            string label)
        {
            switch (device)
            {
                case InputBindingDevice.Mouse:
                    switch (label)
                    {
                        case "鼠标右键":
                            return "<Mouse>/rightButton";
                        case "鼠标中键":
                            return "<Mouse>/middleButton";
                        case "鼠标移动":
                            return "<Mouse>/delta";
                        default:
                            return "<Mouse>/leftButton";
                    }
                case InputBindingDevice.Gamepad:
                    switch (label)
                    {
                        case "手柄右键":
                            return "<Gamepad>/buttonEast";
                        case "手柄左键":
                            return "<Gamepad>/buttonWest";
                        case "手柄上键":
                            return "<Gamepad>/buttonNorth";
                        case "左肩键":
                            return "<Gamepad>/leftShoulder";
                        case "右肩键":
                            return "<Gamepad>/rightShoulder";
                        case "菜单键":
                            return "<Gamepad>/start";
                        case "视图键":
                            return "<Gamepad>/select";
                        case "左摇杆按下":
                            return "<Gamepad>/leftStickPress";
                        case "右摇杆按下":
                            return "<Gamepad>/rightStickPress";
                        case "左摇杆":
                            return "<Gamepad>/leftStick";
                        case "右摇杆":
                            return "<Gamepad>/rightStick";
                        case "十字键上":
                            return "<Gamepad>/dpad/up";
                        case "十字键下":
                            return "<Gamepad>/dpad/down";
                        case "十字键左":
                            return "<Gamepad>/dpad/left";
                        case "十字键右":
                            return "<Gamepad>/dpad/right";
                        default:
                            return "<Gamepad>/buttonSouth";
                    }
                case InputBindingDevice.Touch:
                    return label == "主触点位置"
                        ? "<Touchscreen>/primaryTouch/position"
                        : "<Touchscreen>/primaryTouch/tap";
                default:
                    switch (label)
                    {
                        case "回车":
                            return "<Keyboard>/enter";
                        case "E":
                            return "<Keyboard>/e";
                        case "Q":
                            return "<Keyboard>/q";
                        case "F":
                            return "<Keyboard>/f";
                        case "Tab":
                            return "<Keyboard>/tab";
                        case "左 Ctrl":
                            return "<Keyboard>/leftCtrl";
                        case "左 Shift":
                            return "<Keyboard>/leftShift";
                        case "Escape":
                            return "<Keyboard>/escape";
                        case "W":
                            return "<Keyboard>/w";
                        case "A":
                            return "<Keyboard>/a";
                        case "S":
                            return "<Keyboard>/s";
                        case "D":
                            return "<Keyboard>/d";
                        case "Z":
                            return "<Keyboard>/z";
                        case "X":
                            return "<Keyboard>/x";
                        case "C":
                            return "<Keyboard>/c";
                        case "R":
                            return "<Keyboard>/r";
                        default:
                            return "<Keyboard>/space";
                    }
            }
        }

        private static string KeyCodeToControlPath(
            KeyCode keyCode)
        {
            if (keyCode >= KeyCode.A && keyCode <= KeyCode.Z)
            {
                return "<Keyboard>/" +
                       char.ToLowerInvariant(
                           (char)('a' + keyCode - KeyCode.A));
            }

            if (keyCode >= KeyCode.Alpha0 &&
                keyCode <= KeyCode.Alpha9)
            {
                return "<Keyboard>/" +
                       (keyCode - KeyCode.Alpha0);
            }

            if (keyCode >= KeyCode.F1 && keyCode <= KeyCode.F12)
            {
                return "<Keyboard>/f" +
                       (keyCode - KeyCode.F1 + 1);
            }

            if (keyCode >= KeyCode.Keypad0 &&
                keyCode <= KeyCode.Keypad9)
            {
                return "<Keyboard>/numpad" +
                       (keyCode - KeyCode.Keypad0);
            }

            switch (keyCode)
            {
                case KeyCode.Space: return "<Keyboard>/space";
                case KeyCode.Return: return "<Keyboard>/enter";
                case KeyCode.KeypadEnter: return "<Keyboard>/numpadEnter";
                case KeyCode.Escape: return "<Keyboard>/escape";
                case KeyCode.Tab: return "<Keyboard>/tab";
                case KeyCode.Backspace: return "<Keyboard>/backspace";
                case KeyCode.Delete: return "<Keyboard>/delete";
                case KeyCode.Insert: return "<Keyboard>/insert";
                case KeyCode.Home: return "<Keyboard>/home";
                case KeyCode.End: return "<Keyboard>/end";
                case KeyCode.PageUp: return "<Keyboard>/pageUp";
                case KeyCode.PageDown: return "<Keyboard>/pageDown";
                case KeyCode.UpArrow: return "<Keyboard>/upArrow";
                case KeyCode.DownArrow: return "<Keyboard>/downArrow";
                case KeyCode.LeftArrow: return "<Keyboard>/leftArrow";
                case KeyCode.RightArrow: return "<Keyboard>/rightArrow";
                case KeyCode.LeftShift: return "<Keyboard>/leftShift";
                case KeyCode.RightShift: return "<Keyboard>/rightShift";
                case KeyCode.LeftControl: return "<Keyboard>/leftCtrl";
                case KeyCode.RightControl: return "<Keyboard>/rightCtrl";
                case KeyCode.LeftAlt: return "<Keyboard>/leftAlt";
                case KeyCode.RightAlt: return "<Keyboard>/rightAlt";
                case KeyCode.Minus: return "<Keyboard>/minus";
                case KeyCode.Equals: return "<Keyboard>/equals";
                case KeyCode.LeftBracket: return "<Keyboard>/leftBracket";
                case KeyCode.RightBracket: return "<Keyboard>/rightBracket";
                case KeyCode.Semicolon: return "<Keyboard>/semicolon";
                case KeyCode.Quote: return "<Keyboard>/quote";
                case KeyCode.BackQuote: return "<Keyboard>/backquote";
                case KeyCode.Comma: return "<Keyboard>/comma";
                case KeyCode.Period: return "<Keyboard>/period";
                case KeyCode.Slash: return "<Keyboard>/slash";
                case KeyCode.Backslash: return "<Keyboard>/backslash";
            }

            return null;
        }

        private static string GetKeyboardDisplayName(
            string path)
        {
            switch (path)
            {
                case "<Keyboard>/leftShift":
                    return "左 Shift";
                case "<Keyboard>/rightShift":
                    return "右 Shift";
                case "<Keyboard>/leftCtrl":
                    return "左 Ctrl";
                case "<Keyboard>/rightCtrl":
                    return "右 Ctrl";
                case "<Keyboard>/leftAlt":
                    return "左 Alt";
                case "<Keyboard>/rightAlt":
                    return "右 Alt";
                case "<Keyboard>/leftMeta":
                    return "左 Win";
                case "<Keyboard>/rightMeta":
                    return "右 Win";
                default:
                    int separator = path.LastIndexOf('/');
                    return separator >= 0 &&
                           separator + 1 < path.Length
                        ? path.Substring(separator + 1)
                        : path;
            }
        }

        private static InputBindingDevice FromDeviceLabel(
            string label)
        {
            switch (label)
            {
                case "鼠标":
                    return InputBindingDevice.Mouse;
                case "手柄":
                    return InputBindingDevice.Gamepad;
                case "触屏":
                    return InputBindingDevice.Touch;
                default:
                    return InputBindingDevice.Keyboard;
            }
        }

        private static VisualElement Row()
        {
            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            return row;
        }

        private static Button CreateButton(
            string text,
            Action action,
            Color? backgroundColor = null)
        {
            Button button = new Button(action)
            {
                text = text
            };
            button.style.height = 24f;
            button.style.marginLeft = 2f;
            button.style.marginRight = 2f;
            button.style.marginTop = 2f;
            button.style.marginBottom = 2f;
            button.style.paddingLeft = 8f;
            button.style.paddingRight = 8f;
            button.style.borderTopLeftRadius = 4f;
            button.style.borderTopRightRadius = 4f;
            button.style.borderBottomLeftRadius = 4f;
            button.style.borderBottomRightRadius = 4f;
            button.style.color = Color.white;
            button.style.backgroundColor =
                backgroundColor ??
                new Color(0.20f, 0.29f, 0.37f, 1f);
            return button;
        }

        private static void SetBorder(
            VisualElement element,
            Color color)
        {
            element.style.borderLeftWidth = 1f;
            element.style.borderRightWidth = 1f;
            element.style.borderTopWidth = 1f;
            element.style.borderBottomWidth = 1f;
            element.style.borderLeftColor = color;
            element.style.borderRightColor = color;
            element.style.borderTopColor = color;
            element.style.borderBottomColor = color;
        }
    }
}
