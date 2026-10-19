using System;
using System.Collections.Generic;
using System.Reflection;
using Project.Editor;
using Project.BlockFeatures;
using Project.LevelEditor;
using Project.Subtitles;
using Project.Subtitles.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Project.LevelEditor.Editor
{
    internal sealed class LevelEditorPropComponentEditorWindow :
        EditorWindow
    {
        private GameObject prefab;
        private GameObject preview;
        private VisualElement componentList;
        private Label statusLabel;
        private bool hasEditSession;
        private bool sessionFinished;
        private string initialDraftJson;
        private int initialPlatformWidth;
        [Serializable]
        private sealed class DraftState
        {
            public List<LevelEditorComponentValueOverride> values;
        }
        private int draftPlatformWidth;
        private Func<int, string> savePlatformWidth;
        private Action contextAction;
        private string contextActionLabel;
        private Action secondaryAction;
        private string secondaryActionLabel;
        private Action thirdAction;
        private string thirdActionLabel;
        private Action fourthAction;
        private string fourthActionLabel;
        private Action<bool> bossRespawnToggleCallback;
        private Action<List<LevelEditorComponentValueOverride>>
            onSaved;
        private readonly List<LevelEditorComponentValueOverride>
            draftOverrides =
                new List<LevelEditorComponentValueOverride>();
        private readonly List<SerializedObject> serializedObjects =
            new List<SerializedObject>();

        internal static void Open(
            GameObject sourcePrefab,
            IReadOnlyList<LevelEditorComponentValueOverride> values,
            Action<List<LevelEditorComponentValueOverride>> saveCallback,
            string contextTitle = "物体参数",
            Action extraAction = null,
            string extraActionLabel = null,
            Action secondary = null,
            string secondaryLabel = null,
            Action third = null,
            string thirdLabel = null,
            Action fourth = null,
            string fourthLabel = null,
            Action<bool> bossRespawnToggle = null,
            int platformWidth = 0,
            Func<int, string> applyPlatformWidth = null)
        {
            if (sourcePrefab == null)
            {
                return;
            }

            LevelEditorPropComponentEditorWindow window =
                GetWindow<LevelEditorPropComponentEditorWindow>();
            if (!window.TrySaveDraft(true))
            {
                window.Focus();
                return;
            }
            window.prefab = sourcePrefab;
            window.draftPlatformWidth = platformWidth;
            window.savePlatformWidth = applyPlatformWidth;
            window.onSaved = saveCallback;
            window.contextAction = extraAction;
            window.contextActionLabel = extraActionLabel;
            window.secondaryAction = secondary;
            window.secondaryActionLabel = secondaryLabel;
            window.thirdAction = third;
            window.thirdActionLabel = thirdLabel;
            window.fourthAction = fourth;
            window.fourthActionLabel = fourthLabel;
            window.bossRespawnToggleCallback =
                bossRespawnToggle;
            window.draftOverrides.Clear();
            if (values != null)
            {
                for (int index = 0; index < values.Count; index++)
                {
                    if (values[index] != null)
                    {
                        window.draftOverrides.Add(
                            values[index].Clone());
                    }
                }
            }

            window.titleContent = new GUIContent(contextTitle);
            window.hasEditSession = true;
            window.sessionFinished = false;
            window.initialPlatformWidth = platformWidth;
            window.initialDraftJson = window.DraftJson();
            window.minSize = new Vector2(430f, 560f);
            window.Show();
            window.Focus();
            window.RebuildPreview();
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            LevelEditorTypography.Apply(root);
            root.style.paddingLeft = 10f;
            root.style.paddingRight = 10f;
            root.style.paddingTop = 8f;
            root.style.paddingBottom = 8f;

            Label title = new Label("道具组件数值");
            title.style.fontSize = 16f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            root.Add(title);

            Label workflow = new Label(
                "只编辑已有组件数值，不新增、不删除、不搜索。" +
                "关闭窗口自动保存；放弃修改则不保存。覆盖值优先于预制体默认值。");
            workflow.style.whiteSpace = WhiteSpace.Normal;
            workflow.style.fontSize = 11f;
            workflow.style.opacity = .74f;
            workflow.style.marginBottom = 6f;
            root.Add(workflow);

            ScrollView body = new ScrollView(
                ScrollViewMode.Vertical);
            body.style.flexGrow = 1f;
            componentList = new VisualElement();
            body.Add(componentList);
            root.Add(body);

            Button resetAll = new Button(ResetAllOverrides)
            {
                text = "全部恢复预制体默认值"
            };
            resetAll.style.height = 28f;
            resetAll.style.marginTop = 5f;
            root.Add(resetAll);

            statusLabel = new Label();
            statusLabel.style.whiteSpace = WhiteSpace.Normal;
            statusLabel.style.opacity = .82f;
            statusLabel.style.marginTop = 5f;
            root.Add(statusLabel);

            VisualElement actions = new VisualElement();
            actions.style.flexDirection = FlexDirection.Row;
            actions.style.marginTop = 7f;
            Button save = new Button(SaveAndClose)
            {
                text = "保存组件数值"
            };
            save.style.flexGrow = 1f;
            save.style.height = 32f;
            save.style.unityFontStyleAndWeight = FontStyle.Bold;
            save.style.backgroundColor =
                new Color(.16f, .43f, .82f, 1f);
            save.style.color = Color.white;
            actions.Add(save);
            Button cancel = new Button(DiscardAndClose)
            {
                text = "放弃修改"
            };
            cancel.style.width = 90f;
            cancel.style.height = 32f;
            actions.Add(cancel);
            root.Add(actions);

            RebuildPreview();
        }

        private void OnDisable()
        {
            try
            {
                if (!TrySaveDraft(true)) ReopenInvalidDraft();
            }
            finally { DisposePreview(); }
        }

        internal static bool SaveOpenSession()
        {
            foreach (var window in Resources.FindObjectsOfTypeAll<LevelEditorPropComponentEditorWindow>())
                if (!window.TrySaveDraft(true)) { window.Focus(); return false; }
            return true;
        }

        private void RebuildPreview()
        {
            DisposePreview();
            if (componentList == null)
            {
                return;
            }

            componentList.Clear();
            if (contextAction != null)
                componentList.Add(new Button(() => contextAction?.Invoke()) { text = contextActionLabel ?? "添加关联物体" });
            if (secondaryAction != null)
                componentList.Add(new Button(() => secondaryAction?.Invoke()) { text = secondaryActionLabel ?? "解除关联" });
            if (thirdAction != null)
                componentList.Add(new Button(() => thirdAction?.Invoke()) { text = thirdActionLabel ?? "解除关联" });
            if (fourthAction != null)
                componentList.Add(new Button(() => fourthAction?.Invoke()) { text = fourthActionLabel ?? "解除关联" });
            if (prefab == null)
            {
                componentList.Add(
                    new Label("没有可编辑的道具预制体。"));
                return;
            }

            preview = PrefabUtility.InstantiatePrefab(prefab)
                as GameObject;
            if (preview == null)
            {
                componentList.Add(
                    new Label("无法实例化道具预制体。"));
                return;
            }

            if (PrefabUtility.IsPartOfPrefabInstance(preview))
            {
                PrefabUtility.UnpackPrefabInstance(
                    preview,
                    PrefabUnpackMode.Completely,
                    InteractionMode.AutomatedAction);
            }

            preview.name = "__LevelEditorPropOverridePreview";
            preview.transform.position =
                new Vector3(10000f, 0f, 0f);
            SetHideFlags(preview, HideFlags.DontSaveInEditor);
            Selection.activeGameObject = preview;
            LevelEditorComponentOverrideUtility.ApplyOverrides(
                preview,
                draftOverrides,
                false);

            BuildContactCubeFixedPosition(componentList);

            if (savePlatformWidth != null)
            {
                var width = new IntegerField("平台宽度（格）") { value = draftPlatformWidth, name = "platform-width", isDelayed = true };
                width.tooltip = "从左侧固定位置向右调整，保存后更新规划占格，应用到场景后更新实体；1～4096格。";
                width.RegisterValueChangedCallback(evt =>
                {
                    draftPlatformWidth = Mathf.Max(1, evt.newValue);
                    width.SetValueWithoutNotify(draftPlatformWidth);
                    statusLabel.text = "平台宽度已修改，尚未保存。";
                });
                componentList.Add(width);
            }
            List<Component> components = new List<Component>(
                preview.GetComponentsInChildren<Component>(true));
            components.RemoveAll(
                component => component == null ||
                             component is Transform ||
                             component is BossRespawnMarker);
            components.Sort((left, right) =>
            {
                bool leftPlatform = Project.BlockFeatures.Editor.PlatformParameterUI.Applies(left);
                bool rightPlatform = Project.BlockFeatures.Editor.PlatformParameterUI.Applies(right);
                if (leftPlatform != rightPlatform) return leftPlatform ? -1 : 1;
                bool leftDefault =
                    PropComponentPresentation.IsDefaultComponent(left);
                bool rightDefault =
                    PropComponentPresentation.IsDefaultComponent(right);
                if (leftDefault != rightDefault)
                {
                    return leftDefault ? 1 : -1;
                }

                return string.CompareOrdinal(
                    left.GetType().Name,
                    right.GetType().Name);
            });

            for (int index = 0; index < components.Count; index++)
            {
                componentList.Add(
                    CreateComponentSection(components[index]));
            }

            statusLabel.text = draftOverrides.Count == 0
                ? "当前没有数值覆盖，全部使用预制体默认值。"
                : $"当前有 {draftOverrides.Count} 项数值覆盖。";
        }

        private VisualElement CreateComponentSection(
            Component component)
        {
            bool isDefault =
                PropComponentPresentation.IsDefaultComponent(component);
            string title =
                PropComponentPresentation.GetComponentDisplayName(component);
            var section = new Foldout
            {
                text = isDefault ? $"{title}（默认组件）" : title,
                value = !isDefault && (prefab.GetComponent<MovingPlatformFeature>() == null && prefab.GetComponent<CrankPlatformFeature>() == null || Project.BlockFeatures.Editor.PlatformParameterUI.Applies(component))
            };
            section.style.marginTop = 5f;

            var actions = new VisualElement();
            actions.style.flexDirection = FlexDirection.Row;
            actions.style.justifyContent = Justify.FlexEnd;
            actions.style.marginBottom = 4f;
            Button reset = new Button(() =>
                ResetComponentOverrides(component))
            {
                text = "恢复此组件默认值"
            };
            reset.style.height = 22f;
            actions.Add(reset);
            section.Add(actions);

            if (component is SubtitleTrigger)
            {
                BuildSubtitleTriggerFields(section, component);
                return section;
            }

            var serializedObject = new SerializedObject(component);
            serializedObjects.Add(serializedObject);
            serializedObject.Update();
            bool platform = Project.BlockFeatures.Editor.PlatformParameterUI.Applies(component);
            var coreFields = platform
                ? Project.BlockFeatures.Editor.PlatformParameterUI.Build(section, serializedObject, property =>
                {
                    CaptureOverride(component, property);
                    statusLabel.text = "组件数值已修改，尚未保存。";
                }) : new HashSet<string>();
            if (component is Rigidbody)
            {
                BuildRigidbodyConstraints(section, component, serializedObject);
            }
            SerializedProperty iterator =
                serializedObject.GetIterator();
            bool hasVisibleField = false;
            bool enterChildren = true;
            var groups = new Dictionary<string, Foldout>();
            var groupBindings =
                new Dictionary<string, List<GroupFieldBinding>>();
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (!CanEditProperty(iterator))
                {
                    continue;
                }
                if (coreFields.Contains(iterator.name)) continue;

                hasVisibleField = true;
                SerializedProperty property = iterator.Copy();
                string label =
                    PropComponentPresentation.GetPropertyLabel(
                        component,
                        property);
                string tooltip =
                    PropComponentPresentation.GetPropertyTooltip(
                        component,
                        property);
                VisualElement fieldParent = section;
                BlockParameterAttribute parameter = PropComponentPresentation.GetParameter(component, property);
                if (parameter != null && !string.IsNullOrWhiteSpace(parameter.Group))
                {
                    if (!groups.TryGetValue(parameter.Group, out Foldout group))
                    {
                        group = ProjectInspectorUtility.CreateFoldout(parameter.Group, !platform);
                        groups.Add(parameter.Group, group);
                        section.Add(group);
                    }
                    fieldParent = group;
                }
                if (property.propertyType ==
                        SerializedPropertyType.String &&
                    property.name.EndsWith(
                        "Tag",
                        StringComparison.OrdinalIgnoreCase))
                {
                    var tagField = new TagField(label)
                    {
                        value = property.stringValue,
                        tooltip = tooltip
                    };
                    tagField.RegisterValueChangedCallback(evt =>
                    {
                        property.stringValue = evt.newValue;
                        serializedObject.ApplyModifiedProperties();
                        CaptureOverride(component, property);
                        statusLabel.text =
                            "组件数值已修改，尚未保存。";
                    });
                    fieldParent.Add(tagField);
                    continue;
                }

                if (component is RespawnAnchorFeature &&
                    property.name ==
                    "isBossArenaRespawnPoint" &&
                    bossRespawnToggleCallback != null)
                {
                    var bossToggle = new Toggle(label)
                    {
                        value = property.boolValue,
                        tooltip = tooltip
                    };
                    bossToggle.RegisterValueChangedCallback(evt =>
                    {
                        property.boolValue = evt.newValue;
                        serializedObject.ApplyModifiedProperties();
                        CaptureOverride(component, property);
                        bossRespawnToggleCallback(
                            evt.newValue);
                        statusLabel.text = evt.newValue
                            ? "已生成 Boss 复活标记，尚未保存组件数值。"
                            : "已删除 Boss 复活标记，尚未保存组件数值。";
                    });
                    fieldParent.Add(bossToggle);
                    if (!string.IsNullOrWhiteSpace(tooltip))
                    {
                        Label help = new Label(tooltip);
                        help.style.whiteSpace =
                            WhiteSpace.Normal;
                        help.style.fontSize = 11f;
                        help.style.opacity = .7f;
                        help.style.marginLeft = 4f;
                        help.style.marginBottom = 6f;
                        fieldParent.Add(help);
                    }

                    continue;
                }

                var field = new PropertyField(property.Copy(), label);
                field.BindProperty(property);
                field.label = label;
                if (!string.IsNullOrWhiteSpace(tooltip))
                {
                    field.tooltip = tooltip;
                }

                field.RegisterCallback<SerializedPropertyChangeEvent>(
                    _ =>
                    {
                        serializedObject.ApplyModifiedProperties();
                        CaptureOverride(component, property);
                        statusLabel.text =
                            "组件数值已修改，尚未保存。";
                    });
                fieldParent.Add(field);
                Label description = null;
                if (!string.IsNullOrWhiteSpace(tooltip))
                {
                    description = new Label(tooltip);
                    description.style.whiteSpace = WhiteSpace.Normal;
                    description.style.fontSize = 11f;
                    description.style.opacity = .7f;
                    description.style.marginLeft = 4f;
                    description.style.marginBottom = 6f;
                    fieldParent.Add(description);
                }
                if (!string.IsNullOrWhiteSpace(parameter?.VisibleWhenField))
                {
                    SerializedProperty condition = serializedObject.FindProperty(parameter.VisibleWhenField);
                    if (condition != null)
                    {
                        Label help = description;
                        void RefreshVisibility(SerializedProperty changed)
                        {
                            int value = changed.propertyType == SerializedPropertyType.Boolean
                                ? changed.boolValue ? 1 : 0 : changed.intValue;
                            DisplayStyle display = parameter.MatchesVisibility(value) ? DisplayStyle.Flex : DisplayStyle.None;
                            field.style.display = display;
                            if (help != null) help.style.display = display;
                        }
                        RefreshVisibility(condition);
                        field.TrackPropertyValue(condition, RefreshVisibility);
                    }
                }

                if (parameter != null &&
                    !string.IsNullOrWhiteSpace(parameter.Group))
                {
                    if (!groupBindings.TryGetValue(
                            parameter.Group,
                            out List<GroupFieldBinding> bindings))
                    {
                        bindings = new List<GroupFieldBinding>();
                        groupBindings.Add(
                            parameter.Group,
                            bindings);
                    }

                    bindings.Add(new GroupFieldBinding(
                        field,
                        description,
                        parameter));
                }
            }

            void RefreshGroupVisibility()
            {
                foreach (KeyValuePair<string, Foldout> pair in groups)
                {
                    if (!groupBindings.TryGetValue(
                            pair.Key,
                            out List<GroupFieldBinding> bindings))
                    {
                        pair.Value.style.display =
                            DisplayStyle.Flex;
                        continue;
                    }

                    bool visible = false;
                    for (int index = 0;
                         index < bindings.Count;
                         index++)
                    {
                        if (IsParameterVisible(
                                serializedObject,
                                bindings[index].Parameter))
                        {
                            visible = true;
                            break;
                        }
                    }

                    pair.Value.style.display = visible
                        ? DisplayStyle.Flex
                        : DisplayStyle.None;
                }
            }

            RefreshGroupVisibility();
            section.schedule.Execute(RefreshGroupVisibility)
                .Every(100);

            if (!hasVisibleField)
            {
                Label empty = new Label("没有可覆盖的数值字段。");
                empty.style.opacity = .65f;
                empty.style.marginTop = 3f;
                section.Add(empty);
            }

            return section;
        }

        private static bool IsParameterVisible(
            SerializedObject serializedObject,
            BlockParameterAttribute parameter)
        {
            if (parameter == null ||
                string.IsNullOrWhiteSpace(
                    parameter.VisibleWhenField))
            {
                return true;
            }

            SerializedProperty condition =
                serializedObject.FindProperty(
                    parameter.VisibleWhenField);
            if (condition == null)
            {
                return true;
            }

            int value = condition.propertyType ==
                        SerializedPropertyType.Boolean
                ? condition.boolValue ? 1 : 0
                : condition.intValue;
            return parameter.MatchesVisibility(value);
        }

        private void BuildSubtitleTriggerFields(
            Foldout section,
            Component component)
        {
            var serializedObject = new SerializedObject(component);
            serializedObjects.Add(serializedObject);
            serializedObject.Update();

            SubtitleInspectorFields.AddSignAppearance(section, serializedObject, property =>
            {
                CaptureOverride(component, property);
                statusLabel.text = "告示牌外观已修改，尚未保存。";
            });

            Foldout content = ProjectInspectorUtility.CreateFoldout(
                "字幕内容",
                true);
            AddOverrideField(
                content,
                serializedObject,
                component,
                "cue.layer",
                "显示层级");
            AddOverrideField(
                content,
                serializedObject,
                component,
                "cue.text",
                "字幕文字");
            AddOverrideField(
                content,
                serializedObject,
                component,
                "cue.useManagerDefaults",
                "使用管理器默认样式");
            section.Add(content);

            Foldout style = ProjectInspectorUtility.CreateFoldout(
                "字幕样式",
                true);
            SubtitleInspectorFields.AddStyle(
                style,
                serializedObject,
                "cue.style");
            RegisterNestedOverrideFields(
                style,
                serializedObject,
                component);
            section.Add(style);

            Foldout trigger = ProjectInspectorUtility.CreateFoldout(
                "触发设置",
                true);
            SerializedProperty playOnce =
                serializedObject.FindProperty("playOnce");
            Toggle allowRepeat = new Toggle("允许重复激发")
            {
                value = playOnce != null && !playOnce.boolValue,
                tooltip =
                    "开启后，玩家每次重新进入触发区域都会再次显示字幕。"
            };
            allowRepeat.RegisterValueChangedCallback(evt =>
            {
                if (playOnce == null)
                {
                    return;
                }

                playOnce.boolValue = !evt.newValue;
                serializedObject.ApplyModifiedProperties();
                CaptureOverride(component, playOnce);
                statusLabel.text =
                    "组件数值已修改，尚未保存。";
            });
            trigger.Add(allowRepeat);
            AddOverrideField(
                trigger,
                serializedObject,
                component,
                "hideVisualOnAwake",
                "进入游戏时隐藏占位外观",
                "隐藏旧占位网格；告示牌外观与触发范围独立控制。");
            AddOverrideField(
                trigger,
                serializedObject,
                component,
                "destroyAfterPlay",
                "播放后销毁物体");
            trigger.Add(ProjectInspectorUtility.CreateHelp(
                "“允许重复激发”关闭时，该触发物只会显示一次。" +
                "开启后可在玩家重新进入触发区域时再次显示。"));
            section.Add(trigger);

            Button preview = new Button(() =>
                ((SubtitleTrigger)component).ShowDebug())
            {
                text = "预览字幕"
            };
            preview.style.height = 26f;
            preview.style.marginTop = 4f;
            preview.schedule.Execute(() =>
                preview.SetEnabled(Application.isPlaying)).Every(100);
            section.Add(preview);

            style.schedule.Execute(() =>
            {
                SerializedProperty useDefaults =
                    serializedObject.FindProperty(
                        "cue.useManagerDefaults");
                style.style.display = useDefaults != null &&
                                      useDefaults.boolValue
                    ? DisplayStyle.None
                    : DisplayStyle.Flex;
            }).Every(100);

            allowRepeat.schedule.Execute(() =>
            {
                if (playOnce != null)
                {
                    allowRepeat.SetValueWithoutNotify(
                        !playOnce.boolValue);
                }
            }).Every(100);

            ProjectInspectorUtility.Bind(section, serializedObject);
        }

        private void AddOverrideField(
            VisualElement parent,
            SerializedObject serializedObject,
            Component component,
            string path,
            string label,
            string tooltip = null)
        {
            PropertyField field =
                ProjectInspectorUtility.CreateProperty(
                    serializedObject,
                    path,
                    label,
                    tooltip);
            field.RegisterCallback<SerializedPropertyChangeEvent>(
                _ =>
                {
                    serializedObject.ApplyModifiedProperties();
                    SerializedProperty property =
                        serializedObject.FindProperty(path);
                    if (property != null)
                    {
                        CaptureOverride(component, property);
                    }

                    statusLabel.text =
                        "组件数值已修改，尚未保存。";
                });
            parent.Add(field);
        }

        private void RegisterNestedOverrideFields(
            VisualElement root,
            SerializedObject serializedObject,
            Component component)
        {
            root.Query<PropertyField>().ForEach(field =>
            {
                field.RegisterCallback<SerializedPropertyChangeEvent>(
                    _ =>
                    {
                        serializedObject.ApplyModifiedProperties();
                        string path = field.userData as string;
                        if (string.IsNullOrEmpty(path))
                        {
                            path = field.bindingPath;
                        }

                        SerializedProperty property =
                            string.IsNullOrEmpty(path)
                                ? null
                                : serializedObject.FindProperty(path);
                        if (property != null)
                        {
                            CaptureOverride(component, property);
                        }

                        statusLabel.text =
                            "组件数值已修改，尚未保存。";
                    });
            });
        }

        private void BuildContactCubeFixedPosition(VisualElement parent)
        {
            var cube = preview.GetComponent<Project.ContactCubes.ContactCube>();
            if (cube == null || !cube.TryGetComponent<Rigidbody>(out var body)) return;

            var serializedBody = new SerializedObject(body);
            serializedObjects.Add(serializedBody);
            var constraints = serializedBody.FindProperty("m_Constraints");
            if (constraints == null) return;

            var fixedPosition = new Toggle("固定位置（锁定 X / Y / Z）")
            {
                name = "contact-cube-fixed-position",
                tooltip = "勾选锁定三个位置轴；取消解除三个位置轴锁定。旋转约束保持不变，保存组件数值后生效。"
            };
            fixedPosition.style.marginBottom = 6f;
            void Refresh()
            {
                if (body == null) return;
                serializedBody.Update();
                fixedPosition.SetValueWithoutNotify(
                    (constraints.intValue & (int)RigidbodyConstraints.FreezePosition) ==
                    (int)RigidbodyConstraints.FreezePosition);
            }
            fixedPosition.RegisterValueChangedCallback(evt =>
            {
                if (body == null) return;
                serializedBody.Update();
                int axes = (int)RigidbodyConstraints.FreezePosition;
                constraints.intValue = evt.newValue
                    ? constraints.intValue | axes
                    : constraints.intValue & ~axes;
                serializedBody.ApplyModifiedProperties();
                CaptureOverride(body, constraints);
                statusLabel.text = "固定位置已修改，尚未保存。";
            });
            fixedPosition.TrackSerializedObjectValue(serializedBody, _ => Refresh());
            Refresh();
            parent.Add(fixedPosition);
        }

        private void BuildRigidbodyConstraints(
            VisualElement parent, Component component, SerializedObject serializedObject)
        {
            // Unity hides this native field from NextVisible.
            SerializedProperty constraints = serializedObject.FindProperty("m_Constraints");
            if (constraints == null) return;
            var group = new Foldout { text = "位置与旋转锁定", value = true };
            group.tooltip = "勾选后禁止刚体沿对应轴移动或旋转。";
            parent.Add(group);
            AddRow("锁定位置", "position", new[] {
                RigidbodyConstraints.FreezePositionX,
                RigidbodyConstraints.FreezePositionY,
                RigidbodyConstraints.FreezePositionZ });
            AddRow("锁定旋转", "rotation", new[] {
                RigidbodyConstraints.FreezeRotationX,
                RigidbodyConstraints.FreezeRotationY,
                RigidbodyConstraints.FreezeRotationZ });

            void AddRow(string label, string key, RigidbodyConstraints[] flags)
            {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                var caption = new Label(label);
                caption.style.width = 90f;
                row.Add(caption);
                group.Add(row);
                for (int index = 0; index < flags.Length; index++)
                {
                    RigidbodyConstraints flag = flags[index];
                    string axis = new[] { "X", "Y", "Z" }[index];
                    var toggle = new Toggle(axis) {
                        name = "rigidbody-freeze-" + key + "-" + axis.ToLowerInvariant(),
                        tooltip = label + " " + axis + " 轴" };
                    toggle.style.minWidth = 55f;
                    toggle.style.width = 65f;
                    toggle.labelElement.style.minWidth = 0f;
                    toggle.labelElement.style.width = 15f;
                    toggle.SetValueWithoutNotify((constraints.intValue & (int)flag) != 0);
                    toggle.TrackSerializedObjectValue(serializedObject, _ =>
                    {
                        if (component == null) return;
                        serializedObject.Update();
                        toggle.SetValueWithoutNotify((constraints.intValue & (int)flag) != 0);
                    });
                    toggle.RegisterValueChangedCallback(evt => {
                        serializedObject.Update();
                        int value = constraints.intValue;
                        constraints.intValue = evt.newValue ? value | (int)flag : value & ~(int)flag;
                        serializedObject.ApplyModifiedProperties();
                        CaptureOverride(component, constraints);
                    });
                    row.Add(toggle);
                }
            }
        }

        private static bool CanEditProperty(
            SerializedProperty property)
        {
            if (property.serializedObject.targetObject is Rigidbody &&
                property.propertyPath == "m_Constraints") return false;
            if (property.propertyPath == "m_Script" ||
                property.propertyPath == "m_ObjectHideFlags" ||
                property.propertyPath == "m_CorrespondingSourceObject" ||
                property.propertyPath == "m_PrefabInstance" ||
                property.propertyPath == "m_PrefabAsset" ||
                property.propertyPath == "m_GameObject" ||
                property.propertyPath == "m_Father" ||
                property.propertyPath == "m_Children")
            {
                return false;
            }

            return LevelEditorComponentOverrideUtility.IsSupported(
                property);
        }

        private void CaptureOverride(
            Component component,
            SerializedProperty property)
        {
            for (int index = draftOverrides.Count - 1;
                 index >= 0;
                 index--)
            {
                if (LevelEditorComponentOverrideUtility.IsSameField(
                        draftOverrides[index],
                        component,
                        property))
                {
                    draftOverrides.RemoveAt(index);
                }
            }

            if (LevelEditorComponentOverrideUtility.TryCapture(
                    component,
                    property,
                    out LevelEditorComponentValueOverride value))
            {
                draftOverrides.Add(value);
            }

            statusLabel.text =
                $"当前有 {draftOverrides.Count} 项数值覆盖，尚未保存。";
        }

        private void ResetComponentOverrides(Component component)
        {
            for (int index = draftOverrides.Count - 1;
                 index >= 0;
                 index--)
            {
                if (LevelEditorComponentOverrideUtility
                    .IsSameComponent(draftOverrides[index], component))
                {
                    draftOverrides.RemoveAt(index);
                }
            }

            RebuildPreview();
        }

        private void ResetAllOverrides()
        {
            draftOverrides.Clear();
            RebuildPreview();
        }

        private void SaveAndClose()
        {
            if (TrySaveDraft(false)) Close();
        }

        private string DraftJson() => JsonUtility.ToJson(new DraftState { values = draftOverrides });

        private bool TrySaveDraft(bool onlyIfChanged)
        {
            if (!hasEditSession || sessionFinished) return true;
            // Delayed number/text fields commit on losing focus, before preview disposal.
            rootVisualElement.focusController?.focusedElement?.Blur();
            if (onlyIfChanged && initialPlatformWidth == draftPlatformWidth && initialDraftJson == DraftJson())
            {
                sessionFinished = true;
                return true;
            }
            string error = savePlatformWidth?.Invoke(draftPlatformWidth);
            if (!string.IsNullOrEmpty(error))
            {
                statusLabel.text = error;
                return false;
            }
            // Reusing this window must not clear the list already stored by its owner.
            onSaved?.Invoke(draftOverrides.ConvertAll(value => value.Clone()));
            sessionFinished = true;
            return true;
        }

        private void DiscardAndClose()
        {
            sessionFinished = true;
            Close();
        }

        private void ReopenInvalidDraft()
        {
            var source = prefab;
            var values = draftOverrides.ConvertAll(value => value.Clone());
            string error = statusLabel?.text;
            string title = titleContent.text;
            EditorApplication.delayCall += () =>
            {
                if (source == null || EditorApplication.isPlayingOrWillChangePlaymode) return;
                Open(source, values, onSaved, title, contextAction, contextActionLabel,
                    secondaryAction, secondaryActionLabel, thirdAction, thirdActionLabel,
                    fourthAction, fourthActionLabel, bossRespawnToggleCallback,
                    draftPlatformWidth, savePlatformWidth);
                var window = GetWindow<LevelEditorPropComponentEditorWindow>();
                window.statusLabel.text = error;
                // The restored values are still an unsaved draft.
                window.initialDraftJson = null;
            };
        }

        private void DisposePreview()
        {
            for (int index = 0;
                 index < serializedObjects.Count;
                 index++)
            {
                serializedObjects[index]?.Dispose();
            }
            serializedObjects.Clear();
            if (preview != null)
            {
                DestroyImmediate(preview);
                preview = null;
            }
        }

        private static void SetHideFlags(
            GameObject root,
            HideFlags flags)
        {
            Transform[] transforms =
                root.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < transforms.Length; index++)
            {
                transforms[index].gameObject.hideFlags = flags;
                Component[] components =
                    transforms[index].GetComponents<Component>();
                for (int componentIndex = 0;
                     componentIndex < components.Length;
                     componentIndex++)
                {
                    if (components[componentIndex] != null)
                    {
                        components[componentIndex].hideFlags = flags;
                    }
                }
            }
        }

        private sealed class GroupFieldBinding
        {
            internal GroupFieldBinding(
                PropertyField field,
                Label help,
                BlockParameterAttribute parameter)
            {
                Field = field;
                Help = help;
                Parameter = parameter;
            }

            internal PropertyField Field { get; }
            internal Label Help { get; }
            internal BlockParameterAttribute Parameter { get; }
        }

        private static class PropComponentPresentation
        {
            private static readonly Dictionary<string, string> Labels =
                new Dictionary<string, string>
                {
                    { "Transform.m_LocalPosition", "本地位置" },
                    { "Transform.m_LocalRotation", "本地旋转" },
                    { "Transform.m_LocalScale", "本地缩放" },
                    { "BoxCollider.m_Size", "碰撞体尺寸" },
                    { "BoxCollider.m_Center", "碰撞体中心" },
                    { "BoxCollider.m_IsTrigger", "作为触发器" },
                    { "SphereCollider.m_Radius", "碰撞体半径" },
                    { "SphereCollider.m_IsTrigger", "作为触发器" },
                    { "ClimbableVineFeature.allowGrowthFromColor", "允许颜色触发生长" },
                    { "ClimbableVineFeature.initialSegments", "预置藤蔓节段" },
                    { "BlockAbilityHost.debugLog", "输出调试日志" },
                    { "SubtitleTrigger.cue.layer", "字幕层级" },
                    { "SubtitleTrigger.cue.text", "字幕文字" },
                    { "SubtitleTrigger.cue.useManagerDefaults", "使用管理器默认样式" },
                    { "SubtitleTrigger.cue.style.font", "字体文件" },
                    { "SubtitleTrigger.cue.style.fontSize", "字号" },
                    { "SubtitleTrigger.cue.style.color", "文字颜色" },
                    { "SubtitleTrigger.cue.style.anchor", "屏幕位置" },
                    { "SubtitleTrigger.cue.style.normalizedX", "自定义 X" },
                    { "SubtitleTrigger.cue.style.normalizedY", "自定义 Y" },
                    { "SubtitleTrigger.cue.style.offset", "位置偏移" },
                    { "SubtitleTrigger.cue.style.holdDuration", "停留时间" },
                    { "SubtitleTrigger.cue.style.inAnimation", "进入动画" },
                    { "SubtitleTrigger.cue.style.inDuration", "进入时长" },
                    { "SubtitleTrigger.cue.style.inCurve", "进入曲线" },
                    { "SubtitleTrigger.cue.style.outAnimation", "退出动画" },
                    { "SubtitleTrigger.cue.style.outDuration", "退出时长" },
                    { "SubtitleTrigger.cue.style.outCurve", "退出曲线" },
                    { "SubtitleTrigger.cue.style.motionDistance", "移动距离" },
                    { "SubtitleTrigger.cue.style.scaleFrom", "起始缩放" },
                    { "SubtitleTrigger.playOnce", "只触发一次" },
                    { "SubtitleTrigger.hideVisualOnAwake", "隐藏物体外观" },
                    { "SubtitleTrigger.destroyAfterPlay", "播放后销毁" },
                    { "DisplayBlockFeature.text", "显示文字" },
                    { "DisplayBlockFeature.font", "字体文件" },
                    { "DisplayBlockFeature.fontSizeRatio", "字号比例（相对方块高度）" },
                    { "DisplayBlockFeature.color", "文字颜色" },
                    { "DisplayBlockFeature.anchor", "相对方块位置" },
                    { "DisplayBlockFeature.textAlignment", "文字对齐" },
                    { "DisplayBlockFeature.offset", "位置偏移" },
                    { "DisplayBlockFeature.layerOrder", "玩家图层关系" },
                    { "DisplayBlockFeature.playerTag", "玩家标签" },
                    { "DisplayBlockFeature.showOnAwake", "进入游戏时显示" },
                    { "DisplayBlockFeature.hideColliderOnAwake", "隐藏碰撞体积" },
                    { "DisplayBlockFeature.hideVisualOnAwake", "隐藏方块外观" },
                };

            private static readonly Dictionary<Type, string>
                ComponentNames = new Dictionary<Type, string>
                {
                    { typeof(Project.Mechanisms.DoorController), "按钮解锁门" },
                    { typeof(Project.Mechanisms.DoorButton), "门按钮" },
                    { typeof(Project.Mechanisms.OneWayPlatform), "单向平台" },
                    { typeof(Transform), "变换" },
                    { typeof(BoxCollider), "盒子碰撞体" },
                    { typeof(SphereCollider), "球体碰撞体" },
                    { typeof(CapsuleCollider), "胶囊碰撞体" },
                    { typeof(MeshFilter), "网格过滤器" },
                    { typeof(MeshRenderer), "网格渲染器" },
                    { typeof(SkinnedMeshRenderer), "蒙皮网格渲染器" },
                    { typeof(AudioSource), "音频源" },
                    { typeof(Camera), "相机" },
                    { typeof(Light), "灯光" },
                };

            public static bool IsDefaultComponent(Component component)
            {
                return component is Transform ||
                       component is Collider ||
                       component is Renderer ||
                       component is MeshFilter;
            }

            public static string GetComponentDisplayName(
                Component component)
            {
                if (component == null)
                {
                    return "未知组件";
                }

                Type type = component.GetType();
                if (component is BlockFeature feature)
                    return feature.Metadata.DisplayName;
                if (ComponentNames.TryGetValue(type, out string name))
                {
                    return name;
                }

                if (type.FullName?.Contains(
                        "SubtitleTrigger") == true)
                {
                    return "字幕触发道具";
                }

                if (type.FullName?.Contains(
                        "DisplayBlockFeature") == true)
                {
                    return "文字展示方块";
                }

                switch (type.Name)
                {
                    case "BlockRuntime": return "物体运行配置";
                    case "BlockAbilityHost": return "固定颜色功能控制";
                    case "ColorObject": return "颜色物体";
                    case "ColorKeyPickup": return "颜色钥匙";
                    case "LevelEditorPlacedBlock": return "网格来源";
                }

                return ObjectNames.NicifyVariableName(type.Name);
            }

            public static string GetPropertyLabel(
                Component component,
                SerializedProperty property)
            {
                BlockParameterAttribute parameter = GetParameter(component, property);
                if (!string.IsNullOrWhiteSpace(parameter?.Label)) return parameter.Label;
                var inspectorName = FindField(component.GetType(), property.name)?.GetCustomAttribute<InspectorNameAttribute>();
                if (inspectorName != null) return inspectorName.displayName;
                string key =
                    $"{component.GetType().Name}.{property.propertyPath}";
                return Labels.TryGetValue(key, out string label)
                    ? label
                    : property.displayName;
            }

            private static FieldInfo FindField(Type type, string name)
            {
                while (type != null)
                {
                    FieldInfo field = type.GetField(name, BindingFlags.Instance |
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                    if (field != null) return field;
                    type = type.BaseType;
                }
                return null;
            }

            public static BlockParameterAttribute GetParameter(Component component, SerializedProperty property) =>
                FindField(component.GetType(), property.name)?.GetCustomAttribute<BlockParameterAttribute>();

            public static string GetPropertyTooltip(
                Component component,
                SerializedProperty property)
            {
                BlockParameterAttribute parameter = GetParameter(component, property);
                if (!string.IsNullOrWhiteSpace(parameter?.Tooltip)) return parameter.Tooltip;
                FieldInfo field = FindField(component.GetType(), property.name);
                TooltipAttribute tooltip =
                    field?.GetCustomAttribute<TooltipAttribute>();
                return tooltip?.tooltip ?? property.tooltip;
            }
        }
    }
}
