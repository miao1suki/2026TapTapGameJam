using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Project.BlockFeatures;
using Project.BlockFeatures.Editor;
using Project.LevelEditor;
using Project.SurfaceTiles;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Project.LevelEditor.Editor
{
    internal sealed class LevelEditorComponentEditorWindow : EditorWindow
    {
        private const string PreviewName =
            "__LevelEditorComponentPreview";
        private const string GeneratedFolder =
            "Assets/_Project/Development/LevelEditor/Orpheus0829/" +
            "GeneratedBlocks";
        private static readonly Vector3 PreviewPosition =
            new Vector3(10000f, 0f, 0f);

        private int editingIndex = -1;
        private GameObject preview;
        private GameObject previousSelection;
        private SceneView previousSceneView;
        private Vector3 previousPivot;
        private Quaternion previousRotation;
        private float previousSize;
        private bool previousOrthographic;
        private VisualElement componentList;
        private Label titleLabel;
        private Label statusLabel;
        private bool sceneViewCaptured;
        private readonly Dictionary<int, bool> foldoutStates =
            new Dictionary<int, bool>();
        private readonly List<SerializedObject> componentSerializedObjects =
            new List<SerializedObject>();

        internal static void Open(int entryIndex)
        {
            LevelEditorPalette palette = LevelEditorState.Palette;
            if (palette == null || entryIndex < 0 ||
                entryIndex >= palette.Entries.Count)
            {
                return;
            }

            LevelEditorComponentEditorWindow window =
                GetWindow<LevelEditorComponentEditorWindow>();
            bool alreadyInitialized = window.componentList != null;
            window.editingIndex = entryIndex;
            window.titleContent = new GUIContent("方块组件与调试");
            window.minSize = new Vector2(430f, 560f);
            window.Show();
            window.Focus();
            if (alreadyInitialized)
            {
                window.RebuildPreview();
            }
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.style.paddingLeft = 10f;
            root.style.paddingRight = 10f;
            root.style.paddingTop = 8f;
            root.style.paddingBottom = 8f;

            titleLabel = new Label();
            titleLabel.style.fontSize = 16f;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            root.Add(titleLabel);

            Label workflow = new Label(
                "添加或删除组件 → 调整参数 → 保存组件模板");
            workflow.style.fontSize = 11f;
            workflow.style.opacity = .72f;
            workflow.style.marginBottom = 6f;
            root.Add(workflow);

            ScrollView body = new ScrollView(ScrollViewMode.Vertical);
            body.style.flexGrow = 1f;
            componentList = new VisualElement();
            body.Add(componentList);
            root.Add(body);

            Button addButton = null;
            addButton = new Button(() =>
                ShowAddComponentSearch(addButton.worldBound))
            {
                text = "添加组件"
            };
            addButton.style.height = 30f;
            addButton.style.marginTop = 5f;
            addButton.style.unityFontStyleAndWeight = FontStyle.Bold;
            addButton.style.backgroundColor =
                new Color(.16f, .43f, .82f, 1f);
            addButton.style.color = Color.white;
            root.Add(addButton);

            statusLabel = new Label();
            statusLabel.style.whiteSpace = WhiteSpace.Normal;
            statusLabel.style.opacity = .82f;
            statusLabel.style.marginTop = 5f;
            root.Add(statusLabel);

            VisualElement actions = Row();
            actions.style.marginTop = 7f;
            Button save = new Button(SaveAndClose)
            {
                text = "保存组件模板"
            };
            save.style.flexGrow = 1f;
            save.style.height = 32f;
            save.style.unityFontStyleAndWeight = FontStyle.Bold;
            save.style.backgroundColor =
                new Color(.16f, .43f, .82f, 1f);
            save.style.color = Color.white;
            actions.Add(save);
            Button close = new Button(Close)
            {
                text = "取消"
            };
            close.style.width = 70f;
            close.style.height = 32f;
            actions.Add(close);
            root.Add(actions);

            if (editingIndex >= 0)
            {
                RebuildPreview();
            }
        }

        private void OnDisable()
        {
            DestroyPreview();
            RestoreSceneView();
        }

        private void RebuildPreview()
        {
            LevelEditorBlockEntry entry = GetEntry();
            if (entry == null || entry.UsesPrefabDirectly)
            {
                SetStatus("这个栏目不是“自己编辑”模式，无法编辑组件模板。");
                RebuildComponentList();
                return;
            }

            DestroyPreview();
            foldoutStates.Clear();
            if (!sceneViewCaptured)
            {
                CaptureSceneView();
                sceneViewCaptured = true;
            }
            GameObject source = entry.SourcePrefab;
            if (source != null)
            {
                preview = PrefabUtility.InstantiatePrefab(source)
                    as GameObject;
                if (preview != null &&
                    PrefabUtility.IsPartOfPrefabInstance(preview))
                {
                    PrefabUtility.UnpackPrefabInstance(
                        preview,
                        PrefabUnpackMode.Completely,
                        InteractionMode.AutomatedAction);
                }
            }
            else
            {
                preview = GameObject.CreatePrimitive(PrimitiveType.Cube);
                preview.transform.localScale =
                    Vector3.one * Mathf.Max(
                        .05f,
                        LevelEditorState.CellSize);
            }

            if (preview == null)
            {
                SetStatus("无法创建组件编辑预览。");
                RebuildComponentList();
                return;
            }

            preview.name = PreviewName;
            preview.transform.position = PreviewPosition;
            SetHideFlags(preview, HideFlags.DontSaveInEditor);
            if (preview.GetComponent<BoxCollider>() == null)
            {
                preview.AddComponent<BoxCollider>();
            }

            if (preview.GetComponent<SurfaceTileBlock>() == null)
            {
                preview.AddComponent<SurfaceTileBlock>();
            }

            SetHideFlags(preview, HideFlags.DontSaveInEditor);
            Selection.activeGameObject = preview;
            SceneView.lastActiveSceneView?.FrameSelected();
            SetStatus(entry.CustomTemplate != null
                ? "正在编辑已有组件模板。"
                : "正在基于基础模型创建组件模板。");
            RebuildComponentList();
            SceneView.RepaintAll();
        }

        private void RebuildComponentList()
        {
            if (componentList == null)
            {
                return;
            }

            componentList.Clear();
            ReleaseSerializedObjects();
            LevelEditorBlockEntry entry = GetEntry();
            titleLabel.text = entry != null
                ? $"组件 · {entry.DisplayName}"
                : "组件";
            if (preview == null)
            {
                componentList.Add(new Label("没有可编辑的组件预览。"));
                return;
            }

            Component[] components = preview.GetComponents<Component>();
            for (int index = 0; index < components.Length; index++)
            {
                Component component = components[index];
                if (component == null)
                {
                    continue;
                }

                componentList.Add(CreateComponentSection(component));
            }
        }

        private VisualElement CreateComponentSection(Component component)
        {
            int componentId = component.GetInstanceID();
            bool expanded = foldoutStates.TryGetValue(
                componentId,
                out bool storedExpanded)
                ? storedExpanded
                : !IsProtected(component);
            BlockFeature blockFeature = component as BlockFeature;
            Foldout section = new Foldout
            {
                text = ObjectNames.NicifyVariableName(
                    component.GetType().Name),
                value = expanded
            };
            section.RegisterValueChangedCallback(evt =>
                foldoutStates[componentId] = evt.newValue);
            section.style.marginBottom = 8f;
            section.style.paddingLeft = 8f;
            section.style.paddingRight = 8f;
            section.style.paddingTop = 6f;
            section.style.paddingBottom = 6f;
            section.style.backgroundColor =
                new Color(.13f, .15f, .18f, .96f);
            section.style.borderLeftWidth = 2f;
            section.style.borderRightWidth = 2f;
            section.style.borderTopWidth = 2f;
            section.style.borderBottomWidth = 2f;
            section.style.borderLeftColor =
                section.style.borderRightColor =
                section.style.borderTopColor =
                section.style.borderBottomColor =
                new Color(.24f, .28f, .34f, 1f);

            if (blockFeature != null)
            {
                string displayName = string.IsNullOrWhiteSpace(
                    blockFeature.Metadata.DisplayName)
                    ? component.GetType().Name
                    : blockFeature.Metadata.DisplayName;
                Label displayTitle = new Label(displayName);
                displayTitle.style.fontSize = 15f;
                displayTitle.style.unityFontStyleAndWeight =
                    FontStyle.Bold;
                displayTitle.style.marginBottom = 5f;
                section.Add(displayTitle);
            }

            Button delete = new Button(() =>
                DeleteComponent(component))
            {
                text = "删除"
            };
            delete.style.width = 52f;
            delete.style.height = 22f;
            delete.style.marginLeft = 4f;
            if (IsProtected(component))
            {
                delete.SetEnabled(false);
                delete.tooltip = "默认组件，不可删除。";
            }
            else
            {
                delete.style.backgroundColor =
                    new Color(.62f, .18f, .16f, 1f);
                delete.style.color = Color.white;
            }

            VisualElement actions = Row();
            actions.style.justifyContent = Justify.FlexEnd;
            actions.style.marginBottom = 4f;
            actions.Add(delete);
            section.Add(actions);

            SerializedObject serializedObject =
                new SerializedObject(component);
            componentSerializedObjects.Add(serializedObject);
            serializedObject.Update();
            SerializedProperty iterator = serializedObject.GetIterator();
            bool enterChildren = true;
            bool hasVisibleField = false;
            Foldout parameterSection =
                component is BlockFeature
                    ? new Foldout
                    {
                        text = "参数",
                        value = true
                    }
                    : null;
            List<ParameterFieldBinding> parameterBindings =
                parameterSection != null
                    ? new List<ParameterFieldBinding>()
                    : null;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (iterator.propertyPath == "m_Script")
                {
                    continue;
                }

                hasVisibleField = true;
                PropertyField field = CreateParameterField(
                    component,
                    iterator,
                    out BlockParameterAttribute parameterMetadata);
                if (parameterBindings != null &&
                    parameterMetadata != null)
                {
                    parameterBindings.Add(new ParameterFieldBinding(
                        component,
                        field,
                        parameterMetadata));
                }

                field.RegisterCallback<SerializedPropertyChangeEvent>(
                    _ =>
                    {
                        serializedObject.ApplyModifiedProperties();
                        EditorUtility.SetDirty(component);
                        RefreshParameterVisibility(parameterBindings);
                        SetStatus("组件参数已修改，尚未保存模板。");
                    });
                if (parameterSection != null)
                {
                    parameterSection.Add(field);
                }
                else
                {
                    section.Add(field);
                }
            }

            if (parameterSection != null)
            {
                RefreshParameterVisibility(parameterBindings);
                if (!hasVisibleField)
                {
                    Label empty = new Label("没有可编辑的序列化参数。");
                    empty.style.fontSize = 10f;
                    empty.style.opacity = .65f;
                    empty.style.marginTop = 3f;
                    parameterSection.Add(empty);
                }

                section.Add(parameterSection);
                section.Add(CreateFeatureDebug(blockFeature));
            }
            else if (!hasVisibleField)
            {
                Label empty = new Label("没有可编辑的序列化参数。");
                empty.style.fontSize = 10f;
                empty.style.opacity = .65f;
                empty.style.marginTop = 3f;
                section.Add(empty);
            }

            return section;
        }

        private static PropertyField CreateParameterField(
            Component component,
            SerializedProperty property,
            out BlockParameterAttribute metadata)
        {
            PropertyField field = new PropertyField(property.Copy());
            field.BindProperty(property);
            metadata =
                GetBlockParameter(component.GetType(), property.name);
            if (metadata != null)
            {
                if (!string.IsNullOrWhiteSpace(metadata.Label))
                {
                    field.label = metadata.Label;
                }

                if (!string.IsNullOrWhiteSpace(metadata.Tooltip))
                {
                    field.tooltip = metadata.Tooltip;
                }
            }

            return field;
        }

        private static void RefreshParameterVisibility(
            List<ParameterFieldBinding> bindings)
        {
            if (bindings == null)
            {
                return;
            }

            for (int index = 0; index < bindings.Count; index++)
            {
                ParameterFieldBinding binding = bindings[index];
                bool visible = true;
                if (!string.IsNullOrWhiteSpace(
                        binding.Metadata.VisibleWhenField))
                {
                    FieldInfo condition = binding.Component.GetType()
                        .GetField(
                            binding.Metadata.VisibleWhenField,
                            BindingFlags.Instance |
                            BindingFlags.Public |
                            BindingFlags.NonPublic);
                    if (condition != null)
                    {
                        object raw = condition.GetValue(
                            binding.Component);
                        int value = System.Convert.ToInt32(raw);
                        visible = value ==
                                  binding.Metadata.VisibleWhenValue;
                    }
                }

                binding.Field.style.display = visible
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            }
        }

        private static BlockParameterAttribute GetBlockParameter(
            System.Type componentType,
            string fieldName)
        {
            FieldInfo field = componentType.GetField(
                fieldName,
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic);
            return field != null
                ? field.GetCustomAttribute<BlockParameterAttribute>()
                : null;
        }

        private VisualElement CreateFeatureDebug(
            BlockFeature feature)
        {
            VisualElement box = new VisualElement();
            box.style.marginTop = 3f;
            box.style.marginBottom = 4f;
            box.style.paddingLeft = 6f;
            box.style.paddingRight = 6f;
            box.style.paddingTop = 5f;
            box.style.paddingBottom = 5f;
            box.style.backgroundColor =
                new Color(.1f, .12f, .15f, .9f);

            Label title = new Label("功能调试");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 11f;
            box.Add(title);

            BlockFeatureMetadata metadata = feature.Metadata;
            box.Add(FeatureInfoLabel(
                "阶段",
                $"{DescribePhase(metadata.Phase)} · " +
                $"顺序 {metadata.Order}"));
            box.Add(FeatureInfoLabel(
                "提供",
                DescribeTypes(metadata.Provides)));
            box.Add(FeatureInfoLabel(
                "读取",
                DescribeTypes(metadata.Requires)));
            box.Add(FeatureInfoLabel(
                "写入",
                DescribeChannels(metadata.Writes)));

            List<BlockDebugValue> values =
                new List<BlockDebugValue>();
            feature.CollectDebugValues(values);
            for (int index = 0; index < values.Count; index++)
            {
                BlockDebugValue value = values[index];
                box.Add(FeatureInfoLabel(
                    value.Label,
                    value.Value != null
                        ? value.Value.ToString()
                        : "null"));
            }

            List<BlockDebugAction> debugActions =
                new List<BlockDebugAction>();
            feature.CollectDebugActions(debugActions);
            if (debugActions.Count == 0)
            {
                box.Add(FeatureInfoLabel(
                    "扩展调试",
                    "暂无快捷操作"));
            }
            else
            {
                VisualElement actionRow = Row();
                actionRow.style.marginTop = 3f;
                for (int index = 0;
                     index < debugActions.Count;
                     index++)
                {
                    BlockDebugAction debugAction = debugActions[index];
                    Button button = new Button(() =>
                    {
                        debugAction.Action?.Invoke();
                        RebuildComponentList();
                    })
                    {
                        text = debugAction.Label,
                        tooltip = debugAction.Label
                    };
                    button.SetEnabled(debugAction.Enabled);
                    button.style.flexGrow = 1f;
                    button.style.marginRight = 2f;
                    actionRow.Add(button);
                }

                box.Add(actionRow);
            }

            BlockRuntime runtime = feature.Context != null
                ? feature.Context.Runtime
                : feature.GetComponent<BlockRuntime>();
            if (runtime != null && runtime.DebugEvents.Count > 0)
            {
                Label eventTitle = FeatureInfoLabel(
                    "最近事件",
                    string.Empty);
                eventTitle.text = "最近事件";
                eventTitle.style.marginTop = 3f;
                box.Add(eventTitle);
                int start = Mathf.Max(0, runtime.DebugEvents.Count - 4);
                for (int index = start;
                     index < runtime.DebugEvents.Count;
                     index++)
                {
                    BlockDebugEvent debugEvent =
                        runtime.DebugEvents[index];
                    box.Add(FeatureInfoLabel(
                        DescribeEventKind(debugEvent.Kind),
                        debugEvent.Message));
                }
            }

            return box;
        }

        private static Label FeatureInfoLabel(
            string label,
            string value)
        {
            Label element = new Label($"{label}：{value}");
            element.style.fontSize = 10f;
            element.style.whiteSpace = WhiteSpace.Normal;
            element.style.opacity = .8f;
            return element;
        }

        private static string DescribeTypes(
            IReadOnlyList<System.Type> types)
        {
            if (types == null || types.Count == 0)
            {
                return "无";
            }

            string result = string.Empty;
            for (int index = 0; index < types.Count; index++)
            {
                if (index > 0)
                {
                    result += ", ";
                }

                result += types[index] != null
                    ? types[index].Name
                    : "无效类型";
            }

            return result;
        }

        private static string DescribeChannels(
            IReadOnlyList<BlockChannel> channels)
        {
            if (channels == null || channels.Count == 0)
            {
                return "无";
            }

            string result = string.Empty;
            for (int index = 0; index < channels.Count; index++)
            {
                if (index > 0)
                {
                    result += ", ";
                }

                result += DescribeChannel(channels[index]);
            }

            return result;
        }

        private static string DescribePhase(BlockFeaturePhase phase)
        {
            switch (phase)
            {
                case BlockFeaturePhase.Input:
                    return "输入";
                case BlockFeaturePhase.Intent:
                    return "意图";
                case BlockFeaturePhase.Simulation:
                    return "模拟";
                case BlockFeaturePhase.Reaction:
                    return "反应";
                case BlockFeaturePhase.Link:
                    return "联动";
                case BlockFeaturePhase.Movement:
                    return "移动";
                case BlockFeaturePhase.Presentation:
                    return "表现";
                case BlockFeaturePhase.Cleanup:
                    return "清理";
                default:
                    return phase.ToString();
            }
        }

        private static string DescribeChannel(BlockChannel channel)
        {
            switch (channel)
            {
                case BlockChannel.Custom:
                    return "自定义";
                case BlockChannel.State:
                    return "状态";
                case BlockChannel.Heat:
                    return "热量";
                case BlockChannel.Power:
                    return "动力";
                case BlockChannel.Motion:
                    return "运动";
                case BlockChannel.Color:
                    return "颜色";
                case BlockChannel.Light:
                    return "光照";
                case BlockChannel.Water:
                    return "水";
                case BlockChannel.Growth:
                    return "生长";
                case BlockChannel.Air:
                    return "空气";
                case BlockChannel.Shadow:
                    return "阴影";
                case BlockChannel.Signal:
                    return "信号";
                case BlockChannel.Presentation:
                    return "表现";
                default:
                    return channel.ToString();
            }
        }

        private static string DescribeEventKind(
            BlockDebugEventKind kind)
        {
            switch (kind)
            {
                case BlockDebugEventKind.Signal:
                    return "信号";
                case BlockDebugEventKind.Command:
                    return "命令";
                case BlockDebugEventKind.Link:
                    return "联动";
                case BlockDebugEventKind.Presentation:
                    return "表现";
                case BlockDebugEventKind.Error:
                    return "错误";
                default:
                    return kind.ToString();
            }
        }

        private void ShowAddComponentSearch(Rect anchor)
        {
            if (preview == null)
            {
                SetStatus("没有可编辑的组件预览。");
                return;
            }

            AddComponentWindow.Open(
                this,
                GUIUtility.GUIToScreenRect(anchor));
        }

        private bool CanAdd(Type type)
        {
            if (preview == null || type == null || type.IsAbstract ||
                type.ContainsGenericParameters ||
                !typeof(Component).IsAssignableFrom(type) ||
                type == typeof(Transform) ||
                type == typeof(BlockRuntime) ||
                type == typeof(LevelEditorPlacedBlock))
            {
                return false;
            }

            if (!(type.IsPublic || type.IsNestedPublic) ||
                type.Assembly.GetName().Name.StartsWith(
                    "UnityEditor",
                    StringComparison.Ordinal))
            {
                return false;
            }

            if (type.IsDefined(
                    typeof(DisallowMultipleComponent),
                    true) &&
                preview.GetComponent(type) != null)
            {
                return false;
            }

            if (IsDefaultComponentType(type) &&
                preview.GetComponent(type) != null)
            {
                return false;
            }

            return true;
        }

        private void AddComponent(Type type)
        {
            try
            {
                Undo.AddComponent(preview, type);
                SetStatus($"已添加 {type.Name}，尚未保存模板。");
                RebuildComponentList();
                SceneView.RepaintAll();
            }
            catch (Exception exception)
            {
                SetStatus($"添加组件失败：{exception.Message}");
            }
        }

        private void DeleteComponent(Component component)
        {
            if (component == null || IsProtected(component))
            {
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "删除组件",
                    $"确定从组件模板中删除“{component.GetType().Name}”？",
                    "删除",
                    "取消"))
            {
                return;
            }

            foldoutStates.Remove(component.GetInstanceID());
            Undo.DestroyObjectImmediate(component);
            SetStatus("组件已删除，尚未保存模板。");
            RebuildComponentList();
            SceneView.RepaintAll();
        }

        private static bool IsProtected(Component component)
        {
            return component is Transform ||
                   component is Renderer ||
                   component is Collider ||
                   component is MeshFilter ||
                   component is BlockRuntime ||
                   component is SurfaceTileBlock ||
                   component is LevelEditorPlacedBlock;
        }

        private static bool IsDefaultComponentType(Type type)
        {
            return typeof(Transform).IsAssignableFrom(type) ||
                   typeof(Renderer).IsAssignableFrom(type) ||
                   typeof(Collider).IsAssignableFrom(type) ||
                   typeof(MeshFilter).IsAssignableFrom(type) ||
                   typeof(Rigidbody).IsAssignableFrom(type) ||
                   typeof(BlockRuntime).IsAssignableFrom(type) ||
                   typeof(SurfaceTileBlock).IsAssignableFrom(type) ||
                   typeof(LevelEditorPlacedBlock).IsAssignableFrom(type);
        }

        private void SaveAndClose()
        {
            LevelEditorBlockEntry entry = GetEntry();
            if (entry == null || preview == null ||
                entry.UsesPrefabDirectly)
            {
                SetStatus("无法保存当前组件模板。");
                return;
            }

            BlockFeatureValidationReport validation =
                BlockFeatureValidationUtility.Validate(preview);
            if (validation.HasErrors)
            {
                string summary = validation.GetSummary();
                SetStatus("组件功能校验失败，请先修正配置。");
                EditorUtility.DisplayDialog(
                    "组件模板校验",
                    summary,
                    "返回修改");
                return;
            }

            EnsureGeneratedFolder();
            entry.EnsureEntryId();
            string oldPath = entry.CustomTemplate != null
                ? AssetDatabase.GetAssetPath(entry.CustomTemplate)
                : string.Empty;
            string fileName = SanitizeFileName(entry.DisplayName) +
                              "_" + entry.EntryId + ".prefab";
            string path = GeneratedFolder + "/" + fileName;

            GameObject saveObject = null;
            GameObject asset = null;
            try
            {
                saveObject = Instantiate(preview);
                saveObject.name = entry.DisplayName;
                saveObject.transform.position = Vector3.zero;
                saveObject.transform.rotation = Quaternion.identity;
                SetHideFlags(saveObject, HideFlags.None);
                asset = PrefabUtility.SaveAsPrefabAsset(
                    saveObject,
                    path);
            }
            catch (Exception exception)
            {
                SetStatus($"组件模板保存失败：{exception.Message}");
                return;
            }
            finally
            {
                if (saveObject != null)
                {
                    DestroyImmediate(saveObject);
                }
            }

            if (asset == null)
            {
                SetStatus("组件模板保存失败。");
                return;
            }

            entry.SetCustomTemplate(asset);
            LevelEditorPalette palette = LevelEditorState.Palette;
            EditorUtility.SetDirty(palette);
            AssetDatabase.SaveAssetIfDirty(palette);
            LevelEditorState.MarkPaletteChanged();
            if (!string.IsNullOrEmpty(oldPath) &&
                oldPath != path &&
                oldPath.StartsWith(
                    GeneratedFolder + "/",
                    StringComparison.Ordinal))
            {
                AssetDatabase.DeleteAsset(oldPath);
            }

            SetStatus("组件模板已保存。");
            Close();
            SceneView.RepaintAll();
        }

        private LevelEditorBlockEntry GetEntry()
        {
            LevelEditorPalette palette = LevelEditorState.Palette;
            if (palette == null || editingIndex < 0 ||
                editingIndex >= palette.Entries.Count)
            {
                return null;
            }

            return palette.Entries[editingIndex];
        }

        private static void EnsureGeneratedFolder()
        {
            if (AssetDatabase.IsValidFolder(GeneratedFolder))
            {
                return;
            }

            string parent = Path.GetDirectoryName(GeneratedFolder)
                ?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent) &&
                !AssetDatabase.IsValidFolder(parent))
            {
                Directory.CreateDirectory(parent);
                AssetDatabase.Refresh();
            }

            AssetDatabase.CreateFolder(
                parent,
                Path.GetFileName(GeneratedFolder));
        }

        private static string SanitizeFileName(string value)
        {
            string name = string.IsNullOrWhiteSpace(value)
                ? "Block"
                : value.Trim();
            char[] invalid = Path.GetInvalidFileNameChars();
            for (int index = 0; index < invalid.Length; index++)
            {
                name = name.Replace(invalid[index], '_');
            }

            return name;
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

        private void CaptureSceneView()
        {
            previousSelection = Selection.activeGameObject;
            previousSceneView = SceneView.lastActiveSceneView;
            if (previousSceneView == null)
            {
                return;
            }

            previousPivot = previousSceneView.pivot;
            previousRotation = previousSceneView.rotation;
            previousSize = previousSceneView.size;
            previousOrthographic = previousSceneView.orthographic;
        }

        private void RestoreSceneView()
        {
            if (previousSceneView != null)
            {
                previousSceneView.pivot = previousPivot;
                previousSceneView.rotation = previousRotation;
                previousSceneView.size = previousSize;
                previousSceneView.orthographic = previousOrthographic;
                previousSceneView.Repaint();
            }

            previousSceneView = null;
            sceneViewCaptured = false;
            if (previousSelection != null)
            {
                Selection.activeGameObject = previousSelection;
                previousSelection = null;
            }
        }

        private void DestroyPreview()
        {
            if (componentList != null)
            {
                componentList.Clear();
            }

            ReleaseSerializedObjects();
            if (preview == null)
            {
                GameObject found = GameObject.Find(PreviewName);
                if (found != null)
                {
                    DestroyImmediate(found);
                }

                return;
            }

            if (Selection.activeGameObject == preview ||
                Selection.activeGameObject != null &&
                Selection.activeGameObject.transform.IsChildOf(
                    preview.transform))
            {
                Selection.activeObject = null;
            }

            DestroyImmediate(preview);
            preview = null;
        }

        private void ReleaseSerializedObjects()
        {
            for (int index = 0;
                 index < componentSerializedObjects.Count;
                 index++)
            {
                componentSerializedObjects[index]?.Dispose();
            }

            componentSerializedObjects.Clear();
        }

        private void SetStatus(string message)
        {
            if (statusLabel != null)
            {
                statusLabel.text = message;
            }
        }

        private static VisualElement Row()
        {
            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            return row;
        }

        private sealed class ParameterFieldBinding
        {
            internal ParameterFieldBinding(
                Component component,
                PropertyField field,
                BlockParameterAttribute metadata)
            {
                Component = component;
                Field = field;
                Metadata = metadata;
            }

            internal Component Component { get; }
            internal PropertyField Field { get; }
            internal BlockParameterAttribute Metadata { get; }
        }

        private sealed class AddComponentWindow : EditorWindow
        {
            private LevelEditorComponentEditorWindow owner;
            private TextField searchField;
            private VisualElement results;
            private Type firstResult;

            internal static void Open(
                LevelEditorComponentEditorWindow valueOwner,
                Rect anchor)
            {
                AddComponentWindow window =
                    CreateInstance<AddComponentWindow>();
                window.owner = valueOwner;
                window.titleContent = new GUIContent("添加组件");
                window.ShowAsDropDown(
                    anchor,
                    new Vector2(360f, 420f));
            }

            public void CreateGUI()
            {
                VisualElement root = rootVisualElement;
                root.style.paddingLeft = 8f;
                root.style.paddingRight = 8f;
                root.style.paddingTop = 8f;
                root.style.paddingBottom = 8f;

                searchField = new TextField("搜索")
                {
                    isDelayed = false
                };
                searchField.RegisterValueChangedCallback(
                    _ => RebuildResults());
                searchField.RegisterCallback<KeyDownEvent>(evt =>
                {
                    if (evt.keyCode == KeyCode.Escape)
                    {
                        Close();
                        evt.StopPropagation();
                        return;
                    }

                    if ((evt.keyCode == KeyCode.Return ||
                         evt.keyCode == KeyCode.KeypadEnter) &&
                        firstResult != null)
                    {
                        Add(firstResult);
                        evt.StopPropagation();
                    }
                });
                root.Add(searchField);

                Label hint = new Label("输入名称过滤，回车添加第一项。");
                hint.style.fontSize = 10f;
                hint.style.opacity = .68f;
                hint.style.marginTop = 3f;
                hint.style.marginBottom = 4f;
                root.Add(hint);

                ScrollView scroll = new ScrollView(ScrollViewMode.Vertical);
                scroll.style.flexGrow = 1f;
                results = new VisualElement();
                scroll.Add(results);
                root.Add(scroll);
                RebuildResults();
                searchField.schedule.Execute(() =>
                {
                    searchField.Focus();
                    searchField.SelectAll();
                }).StartingIn(0);
            }

            private void RebuildResults()
            {
                if (results == null)
                {
                    return;
                }

                results.Clear();
                firstResult = null;
                string query = searchField != null
                    ? searchField.value?.Trim() ?? string.Empty
                    : string.Empty;
                List<Type> matches = new List<Type>();
                foreach (Type type in ProjectDiscovery
                             .FindImplementations<Component>())
                {
                    if (owner == null || !owner.CanAdd(type))
                    {
                        continue;
                    }

                    string displayName = DisplayName(type);
                    if (query.Length > 0 &&
                        type.Name.IndexOf(
                            query,
                            StringComparison.OrdinalIgnoreCase) < 0 &&
                        displayName.IndexOf(
                            query,
                            StringComparison.OrdinalIgnoreCase) < 0 &&
                        (type.FullName ?? string.Empty).IndexOf(
                            query,
                            StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }

                    matches.Add(type);
                }

                matches.Sort((left, right) =>
                    string.Compare(
                        DisplayName(left),
                        DisplayName(right),
                        StringComparison.OrdinalIgnoreCase));
                if (matches.Count == 0)
                {
                    results.Add(new Label("没有匹配的组件。"));
                    return;
                }

                firstResult = matches[0];
                for (int index = 0; index < matches.Count; index++)
                {
                    Type type = matches[index];
                    Button item = new Button(() => Add(type))
                    {
                        text = DisplayName(type),
                        tooltip = type.FullName
                    };
                    item.style.height = 26f;
                    item.style.unityTextAlign = TextAnchor.MiddleLeft;
                    item.style.marginBottom = 2f;
                    item.style.backgroundColor =
                        new Color(.16f, .18f, .22f, 1f);
                    item.style.color = Color.white;
                    results.Add(item);
                }
            }

            private void Add(Type type)
            {
                if (owner == null || type == null)
                {
                    return;
                }

                owner.AddComponent(type);
                Close();
            }

            private static string DisplayName(Type type)
            {
                return ObjectNames.NicifyVariableName(type.Name);
            }
        }
    }
}
