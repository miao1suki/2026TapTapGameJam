using System;
using System.Collections.Generic;
using System.Reflection;
using Project.Editor;
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
            Action<List<LevelEditorComponentValueOverride>> saveCallback)
        {
            if (sourcePrefab == null)
            {
                return;
            }

            LevelEditorPropComponentEditorWindow window =
                GetWindow<LevelEditorPropComponentEditorWindow>();
            window.prefab = sourcePrefab;
            window.onSaved = saveCallback;
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

            window.titleContent = new GUIContent("道具组件数值");
            window.minSize = new Vector2(430f, 560f);
            window.Show();
            window.Focus();
            window.RebuildPreview();
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
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
                "保存后覆盖值优先于预制体默认值。");
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
            Button cancel = new Button(Close)
            {
                text = "取消"
            };
            cancel.style.width = 70f;
            cancel.style.height = 32f;
            actions.Add(cancel);
            root.Add(actions);

            RebuildPreview();
        }

        private void OnDisable()
        {
            DisposePreview();
        }

        private void RebuildPreview()
        {
            DisposePreview();
            if (componentList == null)
            {
                return;
            }

            componentList.Clear();
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
                draftOverrides);

            List<Component> components = new List<Component>(
                preview.GetComponents<Component>());
            components.RemoveAll(
                component => component == null || component is Transform);
            components.Sort((left, right) =>
            {
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
                value = !isDefault
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
            SerializedProperty iterator =
                serializedObject.GetIterator();
            bool hasVisibleField = false;
            while (iterator.NextVisible(true))
            {
                if (!CanEditProperty(iterator))
                {
                    continue;
                }

                hasVisibleField = true;
                SerializedProperty property = iterator.Copy();
                var field = new PropertyField(property.Copy());
                field.BindProperty(property);
                field.label =
                    PropComponentPresentation.GetPropertyLabel(
                        component,
                        property);
                string tooltip =
                    PropComponentPresentation.GetPropertyTooltip(
                        component,
                        property);
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
                section.Add(field);
            }

            if (!hasVisibleField)
            {
                Label empty = new Label("没有可覆盖的数值字段。");
                empty.style.opacity = .65f;
                empty.style.marginTop = 3f;
                section.Add(empty);
            }

            return section;
        }

        private void BuildSubtitleTriggerFields(
            Foldout section,
            Component component)
        {
            var serializedObject = new SerializedObject(component);
            serializedObjects.Add(serializedObject);
            serializedObject.Update();

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
                "进入游戏时隐藏物体外观",
                "默认关闭所有 Renderer，但保留 Collider 用于触发。");
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

        private static bool CanEditProperty(
            SerializedProperty property)
        {
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
            onSaved?.Invoke(draftOverrides);
            Close();
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
                };

            private static readonly Dictionary<Type, string>
                ComponentNames = new Dictionary<Type, string>
                {
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
                if (ComponentNames.TryGetValue(type, out string name))
                {
                    return name;
                }

                if (type.FullName?.Contains(
                        "SubtitleTrigger") == true)
                {
                    return "字幕触发道具";
                }

                return ObjectNames.NicifyVariableName(type.Name);
            }

            public static string GetPropertyLabel(
                Component component,
                SerializedProperty property)
            {
                string key =
                    $"{component.GetType().Name}.{property.propertyPath}";
                return Labels.TryGetValue(key, out string label)
                    ? label
                    : ObjectNames.NicifyVariableName(property.name);
            }

            public static string GetPropertyTooltip(
                Component component,
                SerializedProperty property)
            {
                FieldInfo field = component.GetType().GetField(
                    property.name,
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic);
                TooltipAttribute tooltip =
                    field?.GetCustomAttribute<TooltipAttribute>();
                return tooltip?.tooltip;
            }
        }
    }
}
