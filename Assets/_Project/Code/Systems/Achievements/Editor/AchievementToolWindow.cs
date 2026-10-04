using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Project.Achievements.Editor
{
    public sealed class AchievementToolWindow : EditorWindow
    {
        private const string DefaultContentFolder =
            "Assets/_Project/Content/Data/Global/Achievements";
        private const string DefaultCatalogPath =
            "Assets/_Project/Code/Systems/Achievements/Runtime/Resources/AchievementCatalog.asset";

        private string contentFolder = DefaultContentFolder;
        private string catalogPath = DefaultCatalogPath;
        private TextField folderField;
        private TextField saveDirectoryField;
        private TextField saveFileNameField;
        private Label saveDirectoryHint;
        private VisualElement catalogSettingsRow;
        private VisualElement listContainer;
        private VisualElement detailContainer;
        private readonly List<AchievementSO> achievements =
            new List<AchievementSO>();
        private AchievementSO selected;
        private SerializedObject selectedObject;
        private AchievementCatalogSO catalog;

        private void OnEnable()
        {
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
        }

        public static void Open()
        {
            AchievementToolWindow window =
                GetWindow<AchievementToolWindow>(
                    "成就工具");
            window.minSize = new Vector2(900f, 560f);
        }

        public static void OpenWith(
            AchievementSO achievement)
        {
            Open();
            AchievementToolWindow window =
                GetWindow<AchievementToolWindow>();
            window.selected = achievement;
            if (achievement != null)
            {
                window.contentFolder =
                    Path.GetDirectoryName(
                            AssetDatabase.GetAssetPath(
                                achievement))
                        ?.Replace('\\', '/') ??
                    window.contentFolder;
            }

            window.RefreshAll();
        }

        private void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.style.flexGrow = 1f;
            root.style.backgroundColor =
                EditorGUIUtility.isProSkin
                    ? new Color(0.055f, 0.067f, 0.078f, 1f)
                    : new Color(0.91f, 0.925f, 0.94f, 1f);
            root.style.paddingLeft = 8f;
            root.style.paddingRight = 8f;
            root.style.paddingTop = 8f;
            root.style.paddingBottom = 8f;

            VisualElement shell = new VisualElement();
            shell.style.flexGrow = 1f;
            shell.style.paddingLeft = 10f;
            shell.style.paddingRight = 10f;
            shell.style.paddingTop = 8f;
            shell.style.paddingBottom = 8f;
            shell.style.backgroundColor =
                EditorGUIUtility.isProSkin
                    ? new Color(0.075f, 0.09f, 0.11f, 0.97f)
                    : new Color(0.95f, 0.96f, 0.98f, 0.98f);
            shell.style.borderTopLeftRadius = 8f;
            shell.style.borderTopRightRadius = 8f;
            shell.style.borderBottomLeftRadius = 8f;
            shell.style.borderBottomRightRadius = 8f;
            root.Add(shell);

            VisualElement header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            VisualElement titleGroup = new VisualElement();
            titleGroup.style.flexGrow = 1f;
            Label title = new Label("成就工具");
            title.style.fontSize = 14f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleGroup.Add(title);
            Label subtitle = new Label(
                "成就资料 · 条件配置 · 应用到游戏逻辑中");
            subtitle.style.fontSize = 9f;
            subtitle.style.opacity = 0.65f;
            titleGroup.Add(subtitle);
            header.Add(titleGroup);
            VisualElement content = new VisualElement();
            content.style.flexGrow = 1f;
            Button collapse = new Button
            {
                text = "▾"
            };
            collapse.style.width = 25f;
            collapse.style.height = 22f;
            collapse.style.marginLeft = 3f;
            AchievementEditorStyles.Apply(
                collapse,
                false);
            collapse.clicked += () =>
            {
                bool hidden =
                    content.style.display.value ==
                    DisplayStyle.None;
                content.style.display = hidden
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
                collapse.text = hidden ? "▾" : "▸";
            };
            header.Add(collapse);
            shell.Add(header);
            shell.Add(content);

            VisualElement toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Column;
            toolbar.style.marginBottom = 6f;
            toolbar.style.paddingLeft = 6f;
            toolbar.style.paddingRight = 6f;
            toolbar.style.paddingTop = 5f;
            toolbar.style.paddingBottom = 5f;
            toolbar.style.backgroundColor =
                EditorGUIUtility.isProSkin
                    ? new Color(0.09f, 0.11f, 0.13f, 1f)
                    : new Color(0.93f, 0.945f, 0.96f, 1f);
            toolbar.style.borderTopLeftRadius = 6f;
            toolbar.style.borderTopRightRadius = 6f;
            toolbar.style.borderBottomLeftRadius = 6f;
            toolbar.style.borderBottomRightRadius = 6f;
            VisualElement directoryRow = new VisualElement();
            directoryRow.style.flexDirection = FlexDirection.Row;
            directoryRow.style.flexWrap = Wrap.Wrap;
            directoryRow.style.marginBottom = 4f;
            VisualElement actionRow = new VisualElement();
            actionRow.style.flexDirection = FlexDirection.Row;
            actionRow.style.flexWrap = Wrap.Wrap;
            directoryRow.Add(new Label("成就目录"));
            folderField = new TextField
            {
                value = contentFolder
            };
            folderField.style.flexGrow = 1f;
            directoryRow.Add(folderField);
            directoryRow.Add(new Button(
                () =>
                {
                    string selectedFolder =
                        EditorUtility.OpenFolderPanel(
                            "选择成就数据盒目录",
                            contentFolder,
                            string.Empty);
                    if (!string.IsNullOrWhiteSpace(
                            selectedFolder))
                    {
                        contentFolder =
                            ToAssetPath(selectedFolder);
                        folderField.value = contentFolder;
                        RefreshAll();
                    }
                })
            {
                text = "浏览"
            });
            directoryRow.Add(new Button(RefreshAll)
            {
                text = "扫描"
            });
            directoryRow.Add(new Button(CreateAchievement)
            {
                text = "新建"
            });
            actionRow.Add(new Button(Validate)
            {
                text = "校验"
            });
            actionRow.Add(new Button(RenumberConditions)
            {
                text = "整理条件编号"
            });
            actionRow.Add(new Button(UpdateCatalog)
            {
                text = "应用到游戏逻辑中"
            });
            actionRow.Add(new Button(ResetSave)
            {
                text = "重置存档"
            });
            actionRow.Add(new Button(Undo.PerformUndo)
            {
                text = "撤回"
            });
            actionRow.Add(new Button(Undo.PerformRedo)
            {
                text = "重做"
            });
            actionRow.Add(new Button(SaveSelected)
            {
                text = "保存"
            });
            toolbar.Add(directoryRow);
            toolbar.Add(actionRow);
            StyleToolbar(
                toolbar);
            content.Add(toolbar);

            catalogSettingsRow = new VisualElement();
            catalogSettingsRow.style.flexDirection =
                FlexDirection.Row;
            catalogSettingsRow.style.marginBottom = 6f;
            catalogSettingsRow.Add(new Label("存档目录"));
            saveDirectoryField = new TextField();
            saveDirectoryField.style.flexGrow = 1f;
            saveDirectoryField.RegisterValueChangedCallback(
                _ => UpdateCatalogSettings());
            catalogSettingsRow.Add(saveDirectoryField);
            catalogSettingsRow.Add(new Label("文件名"));
            saveFileNameField = new TextField();
            saveFileNameField.style.width = 180f;
            saveFileNameField.RegisterValueChangedCallback(
                _ => UpdateCatalogSettings());
            catalogSettingsRow.Add(saveFileNameField);
            content.Add(catalogSettingsRow);

            saveDirectoryHint = new Label();
            saveDirectoryHint.style.fontSize = 9f;
            saveDirectoryHint.style.opacity = 0.7f;
            saveDirectoryHint.style.whiteSpace =
                WhiteSpace.Normal;
            saveDirectoryHint.style.marginBottom = 6f;
            content.Add(saveDirectoryHint);

            VisualElement body = new VisualElement();
            body.style.flexDirection = FlexDirection.Row;
            body.style.flexGrow = 1f;
            content.Add(body);

            ScrollView listScroll = new ScrollView();
            listScroll.style.width = 230f;
            listScroll.style.marginRight = 8f;
            listContainer = new VisualElement();
            listScroll.Add(listContainer);
            body.Add(listScroll);

            ScrollView detailScroll = new ScrollView();
            detailScroll.style.flexGrow = 1f;
            detailContainer = new VisualElement();
            detailScroll.Add(detailContainer);
            body.Add(detailScroll);

            RefreshAll();
        }

        private void RefreshAll()
        {
            if (folderField != null)
            {
                contentFolder = folderField.value;
            }

            FindAchievements();
            RefreshList();
            LoadCatalog();
            RefreshDetail();
        }

        private void FindAchievements()
        {
            achievements.Clear();
            if (!AssetDatabase.IsValidFolder(
                    contentFolder))
            {
                return;
            }

            string[] guids = AssetDatabase.FindAssets(
                "t:AchievementSO",
                new[] { contentFolder });
            for (int index = 0;
                 index < guids.Length;
                 index++)
            {
                AchievementSO achievement =
                    AssetDatabase.LoadAssetAtPath<AchievementSO>(
                        AssetDatabase.GUIDToAssetPath(
                            guids[index]));
                if (achievement != null)
                {
                    achievements.Add(achievement);
                }
            }

            achievements.Sort(
                (left, right) =>
                    left.AchievementId.CompareTo(
                        right.AchievementId));
        }

        private void RefreshList()
        {
            if (listContainer == null)
            {
                return;
            }

            listContainer.Clear();
            for (int index = 0;
                 index < achievements.Count;
                 index++)
            {
                AchievementSO achievement =
                    achievements[index];
                Button button = new Button(
                    () =>
                    {
                        selected = achievement;
                        RefreshDetail();
                    })
                {
                    text =
                        achievement.AchievementId +
                        "  " +
                        achievement.DisplayName
                };
                button.style.unityTextAlign =
                    TextAnchor.MiddleLeft;
                button.style.marginBottom = 2f;
                listContainer.Add(button);
            }

            AchievementEditorStyles.ApplyAll(
                listContainer);
        }

        private void RefreshDetail()
        {
            if (detailContainer == null)
            {
                return;
            }

            detailContainer.Clear();
            if (selected == null)
            {
                detailContainer.Add(
                    new Label("选择左侧成就进行编辑。"));
                return;
            }

            selectedObject =
                new SerializedObject(selected);
            selectedObject.Update();

            Label guide = new Label(
                "1. 填写成就名字和简介  2. 添加条件  " +
                "3. 打开具体条件逻辑图连接条件  4. 保存到成就  5. 应用到游戏逻辑中");
            guide.style.whiteSpace = WhiteSpace.Normal;
            guide.style.marginBottom = 6f;
            detailContainer.Add(guide);

            VisualElement actionRow = new VisualElement();
            actionRow.style.flexDirection = FlexDirection.Row;
            actionRow.Add(new Button(
                () => AchievementGraphWindow.OpenWith(selected))
            {
                text = "打开成就具体条件逻辑图"
            });
            actionRow.Add(new Button(DeleteSelected)
            {
                text = "删除成就"
            });
            detailContainer.Add(actionRow);
            AddSectionTitle("成就信息");
            AddIntegerProperty(
                "achievementId",
                "成就编号");
            AddStringProperty(
                "displayName",
                "成就名字");
            AddStringProperty(
                "description",
                "成就简介",
                true);
            AddSectionTitle("发布信息");
            AddBooleanProperty(
                "hidden",
                "隐藏成就");
            AddStringProperty(
                "platformAchievementId",
                "平台成就 ID");

            AddSectionTitle("达成条件");
            detailContainer.Add(new Button(AddCondition)
            {
                text = "＋ 添加条件"
            });
            DrawConditions();

            Label graphHint = new Label(
                "条件关系和完成条件请在成就具体条件逻辑图中连线编辑。");
            graphHint.style.whiteSpace = WhiteSpace.Normal;
            graphHint.style.marginTop = 8f;
            graphHint.style.opacity = 0.7f;
            detailContainer.Add(graphHint);
            AchievementEditorStyles.ApplyAll(
                detailContainer,
                "打开成就具体条件逻辑图",
                "＋ 添加条件");
        }

        private void AddIntegerProperty(
            string propertyName,
            string label)
        {
            SerializedProperty property =
                selectedObject.FindProperty(propertyName);
            if (property == null)
            {
                return;
            }

            IntegerField field = new IntegerField(label)
            {
                value = property.intValue
            };
            field.RegisterValueChangedCallback(evt =>
            {
                property.intValue = evt.newValue;
                SaveSelected();
            });
            detailContainer.Add(field);
        }

        private void AddStringProperty(
            string propertyName,
            string label,
            bool multiline = false)
        {
            SerializedProperty property =
                selectedObject.FindProperty(propertyName);
            if (property == null)
            {
                return;
            }

            TextField field = new TextField(label)
            {
                value = property.stringValue,
                multiline = multiline
            };
            if (multiline)
            {
                field.style.minHeight = 70f;
                field.style.whiteSpace =
                    WhiteSpace.Normal;
                TextElement textElement =
                    field.Q<TextElement>();
                if (textElement != null)
                {
                    textElement.style.whiteSpace =
                        WhiteSpace.Normal;
                }
            }

            field.RegisterValueChangedCallback(evt =>
            {
                property.stringValue = evt.newValue;
                SaveSelected();
            });
            detailContainer.Add(field);
        }

        private void AddBooleanProperty(
            string propertyName,
            string label)
        {
            SerializedProperty property =
                selectedObject.FindProperty(propertyName);
            if (property == null)
            {
                return;
            }

            Toggle field = new Toggle(label)
            {
                value = property.boolValue
            };
            field.RegisterValueChangedCallback(evt =>
            {
                property.boolValue = evt.newValue;
                SaveSelected();
            });
            detailContainer.Add(field);
        }

        private void AddSectionTitle(
            string title)
        {
            Label label = new Label(title);
            label.style.unityFontStyleAndWeight =
                FontStyle.Bold;
            label.style.marginTop = 8f;
            detailContainer.Add(label);
        }

        private void AddCondition()
        {
            SerializedProperty conditions =
                selectedObject.FindProperty("conditions");
            conditions.InsertArrayElementAtIndex(
                conditions.arraySize);
            SerializedProperty condition =
                conditions.GetArrayElementAtIndex(
                    conditions.arraySize - 1);
            condition.FindPropertyRelative("conditionId")
                .stringValue =
                (conditions.arraySize).ToString("0000");
            condition.FindPropertyRelative("mode")
                .enumValueIndex = 0;
            condition.FindPropertyRelative("targetCount")
                .intValue = 1;
            condition.FindPropertyRelative("targetProgress")
                .floatValue = 100f;
            condition.FindPropertyRelative("textPrefix")
                .stringValue = "新条件";
            condition.FindPropertyRelative("textSuffix")
                .stringValue = string.Empty;
            SaveSelected();
            RefreshDetail();
        }

        private void DrawConditions()
        {
            SerializedProperty conditions =
                selectedObject.FindProperty("conditions");
            for (int index = 0;
                 index < conditions.arraySize;
                 index++)
            {
                DrawCondition(
                    conditions,
                    index);
            }
        }

        private void DrawCondition(
            SerializedProperty conditions,
            int index)
        {
            SerializedProperty condition =
                conditions.GetArrayElementAtIndex(index);
            VisualElement card = new VisualElement();
            card.style.marginTop = 5f;
            card.style.paddingLeft = 7f;
            card.style.paddingRight = 7f;
            card.style.paddingTop = 6f;
            card.style.paddingBottom = 6f;
            card.style.backgroundColor =
                EditorGUIUtility.isProSkin
                    ? new Color(0.13f, 0.155f, 0.175f, 1f)
                    : new Color(0.895f, 0.91f, 0.925f, 1f);
            card.style.borderLeftWidth = 3f;
            card.style.borderLeftColor =
                new Color(0.18f, 0.55f, 0.95f, 1f);
            card.style.borderTopLeftRadius = 6f;
            card.style.borderTopRightRadius = 6f;
            card.style.borderBottomLeftRadius = 6f;
            card.style.borderBottomRightRadius = 6f;

            SerializedProperty id =
                condition.FindPropertyRelative("conditionId");
            SerializedProperty prefix =
                condition.FindPropertyRelative("textPrefix");
            SerializedProperty suffix =
                condition.FindPropertyRelative("textSuffix");
            SerializedProperty mode =
                condition.FindPropertyRelative("mode");
            SerializedProperty targetCount =
                condition.FindPropertyRelative("targetCount");
            SerializedProperty targetProgress =
                condition.FindPropertyRelative("targetProgress");

            VisualElement header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.Add(new Label("条件 " + id.stringValue));
            Label previewLabel = new Label(
                "  " + BuildConditionPreview(
                    prefix.stringValue,
                    suffix.stringValue,
                    mode.enumValueIndex,
                    targetCount.intValue,
                    targetProgress.floatValue));
            header.Add(previewLabel);
            Button deleteButton = new Button(() =>
            {
                conditions.DeleteArrayElementAtIndex(index);
                SaveSelected();
                RefreshDetail();
            })
            {
                text = "删除"
            };
            deleteButton.style.alignSelf =
                Align.Center;
            header.Add(deleteButton);
            card.Add(header);

            TextField prefixField = new TextField(
                "数值前面的文字")
            {
                value = prefix.stringValue
            };
            prefixField.tooltip =
                "显示在进度数值前面的文字，例如“击杀”。";
            prefixField.RegisterValueChangedCallback(evt =>
            {
                prefix.stringValue = evt.newValue;
                SaveSelected();
                previewLabel.text =
                    "  " + BuildConditionPreview(
                        prefix.stringValue,
                        suffix.stringValue,
                        mode.enumValueIndex,
                        targetCount.intValue,
                        targetProgress.floatValue);
            });
            card.Add(prefixField);

            TextField suffixField = new TextField(
                "数值后面的文字")
            {
                value = suffix.stringValue
            };
            suffixField.tooltip =
                "显示在进度数值后面的文字，例如“个怪物”。";
            suffixField.RegisterValueChangedCallback(evt =>
            {
                suffix.stringValue = evt.newValue;
                SaveSelected();
                previewLabel.text =
                    "  " + BuildConditionPreview(
                        prefix.stringValue,
                        suffix.stringValue,
                        mode.enumValueIndex,
                        targetCount.intValue,
                        targetProgress.floatValue);
            });
            card.Add(suffixField);

            List<string> modes = new List<string>
            {
                "触发一次",
                "累计数量",
                "完成百分比"
            };
            PopupField<string> modeField =
                new PopupField<string>(
                    modes,
                    Mathf.Clamp(
                        mode.enumValueIndex,
                        0,
                        modes.Count - 1));
            modeField.label = "达成方式";
            modeField.tooltip =
                "触发一次：发生一次就完成。\n" +
                "累计数量：每次增加指定数量，达到目标后完成。\n" +
                "完成百分比：每次增加指定百分比，达到 100% 后完成。";
            modeField.RegisterValueChangedCallback(evt =>
            {
                mode.enumValueIndex =
                    modes.IndexOf(evt.newValue);
                SaveSelected();
                RefreshDetail();
            });
            card.Add(modeField);

            if (mode.enumValueIndex == 1)
            {
                IntegerField countField = new IntegerField(
                    "目标数量")
                {
                    value = targetCount.intValue
                };
                countField.RegisterValueChangedCallback(evt =>
                {
                    targetCount.intValue =
                        Mathf.Max(1, evt.newValue);
                    SaveSelected();
                    previewLabel.text =
                        "  " + BuildConditionPreview(
                            prefix.stringValue,
                            suffix.stringValue,
                            mode.enumValueIndex,
                            targetCount.intValue,
                            targetProgress.floatValue);
                });
                card.Add(countField);
            }
            else if (mode.enumValueIndex == 2)
            {
                FloatField progressField = new FloatField(
                    "目标进度")
                {
                    value = targetProgress.floatValue
                };
                progressField.RegisterValueChangedCallback(evt =>
                {
                    targetProgress.floatValue =
                        Mathf.Clamp(evt.newValue, 0f, 100f);
                    SaveSelected();
                    previewLabel.text =
                        "  " + BuildConditionPreview(
                            prefix.stringValue,
                            suffix.stringValue,
                            mode.enumValueIndex,
                            targetCount.intValue,
                            targetProgress.floatValue);
                });
                card.Add(progressField);
            }

            if (IsLockedCondition(id.stringValue))
            {
                Label lockLabel = new Label(
                    "失败条件：请在成就具体条件逻辑图中编辑");
                lockLabel.style.fontSize = 10f;
                lockLabel.style.opacity = 0.75f;
                card.Add(lockLabel);
            }

            detailContainer.Add(card);
        }

        private static string BuildConditionPreview(
            string prefix,
            string suffix,
            int mode,
            int targetCount,
            float targetProgress)
        {
            switch (mode)
            {
                case 1:
                    return prefix + " 0/" + targetCount + suffix;
                case 2:
                    return prefix +
                           " 0/" +
                           targetProgress.ToString("0.#") +
                           "%" +
                           suffix;
                default:
                    return prefix + suffix;
            }
        }

        private bool IsLockedCondition(string conditionId)
        {
            SerializedProperty locks =
                selectedObject.FindProperty(
                    "lockConditionIds");
            for (int index = 0;
                 index < locks.arraySize;
                 index++)
            {
                if (locks.GetArrayElementAtIndex(index)
                        .stringValue == conditionId)
                {
                    return true;
                }
            }

            return false;
        }

        private void SaveSelected()
        {
            if (selectedObject == null)
            {
                return;
            }

            if (selected != null)
            {
                Undo.RecordObject(
                    selected,
                    "修改成就");
            }

            selectedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(selected);
            AssetDatabase.SaveAssets();
        }

        private void DeleteSelected()
        {
            if (selected == null)
            {
                return;
            }

            string assetPath =
                AssetDatabase.GetAssetPath(selected);
            if (!EditorUtility.DisplayDialog(
                    "删除成就",
                    "确定删除数据盒：\n" +
                    selected.DisplayName +
                    "？",
                    "删除",
                    "取消"))
            {
                return;
            }

            AssetDatabase.DeleteAsset(assetPath);
            selected = null;
            RefreshAll();
            UpdateCatalog();
        }

        private void CreateAchievement()
        {
            EnsureFolder(contentFolder);
            List<AchievementSO> all =
                LoadAllAchievements();
            int nextId = 1001;
            for (int index = 0;
                 index < all.Count;
                 index++)
            {
                nextId = Mathf.Max(
                    nextId,
                    all[index].AchievementId + 1);
            }

            AchievementSO achievement =
                CreateInstance<AchievementSO>();
            string assetPath = AssetDatabase
                .GenerateUniqueAssetPath(
                    contentFolder +
                    "/Achievement_" +
                    nextId +
                    ".asset");
            AssetDatabase.CreateAsset(
                achievement,
                assetPath);
            Undo.RegisterCreatedObjectUndo(
                achievement,
                "新建成就");
            SerializedObject serialized =
                new SerializedObject(achievement);
            serialized.FindProperty("achievementId")
                .intValue = nextId;
            serialized.FindProperty("displayName")
                .stringValue = "新成就 " + nextId;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            selected = achievement;
            RefreshAll();
        }

        private void Validate()
        {
            List<AchievementSO> all =
                LoadAllAchievements();
            HashSet<int> ids = new HashSet<int>();
            HashSet<string> names =
                new HashSet<string>(
                    StringComparer.Ordinal);
            List<string> errors = new List<string>();
            for (int index = 0;
                 index < all.Count;
                 index++)
            {
                AchievementSO achievement =
                    all[index];
                if (!ids.Add(achievement.AchievementId))
                {
                    errors.Add(
                        "重复成就编号：" +
                        achievement.AchievementId);
                }

                if (string.IsNullOrWhiteSpace(
                        achievement.DisplayName) ||
                    !names.Add(achievement.DisplayName))
                {
                    errors.Add(
                        "重复或空成就名字：" +
                        achievement.DisplayName);
                }

                HashSet<string> conditionIds =
                    new HashSet<string>(
                        StringComparer.Ordinal);
                for (int conditionIndex = 0;
                     conditionIndex <
                     achievement.Conditions.Count;
                     conditionIndex++)
                {
                    AchievementConditionDefinition condition =
                        achievement.Conditions[conditionIndex];
                    if (!conditionIds.Add(
                            condition.ConditionId))
                    {
                        errors.Add(
                            achievement.DisplayName +
                            " 条件编号重复：" +
                            condition.ConditionId);
                    }
                }
            }

            if (errors.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "成就校验",
                    "未发现编号、名称或条件编号冲突。",
                    "确定");
                return;
            }

            Debug.LogWarning(
                "[AchievementTool]\n" +
                string.Join("\n", errors));
            EditorUtility.DisplayDialog(
                "成就校验失败",
                "发现问题，请查看 Console。",
                "确定");
        }

        private void RenumberConditions()
        {
            Dictionary<int, Dictionary<string, string>> idMaps =
                new Dictionary<int, Dictionary<string, string>>();
            Dictionary<string, Dictionary<string, string>> nameMaps =
                new Dictionary<string, Dictionary<string, string>>(
                    StringComparer.Ordinal);
            for (int index = 0;
                 index < achievements.Count;
                 index++)
            {
                AchievementSO achievement =
                    achievements[index];
                SerializedObject serialized =
                    new SerializedObject(achievement);
                SerializedProperty conditions =
                    serialized.FindProperty("conditions");
                Dictionary<string, string> map =
                    new Dictionary<string, string>(
                        StringComparer.Ordinal);
                for (int conditionIndex = 0;
                     conditionIndex < conditions.arraySize;
                     conditionIndex++)
                {
                    SerializedProperty condition =
                        conditions.GetArrayElementAtIndex(
                            conditionIndex);
                    SerializedProperty id =
                        condition.FindPropertyRelative(
                            "conditionId");
                    string newId =
                        (conditionIndex + 1)
                        .ToString("0000");
                    map[id.stringValue] = newId;
                    id.stringValue = newId;
                }

                idMaps[achievement.AchievementId] = map;
                nameMaps[achievement.DisplayName] = map;
                ApplyIdMap(
                    serialized.FindProperty(
                        "lockConditionIds"),
                    map);
                ApplyNodeIdMap(
                    serialized.FindProperty("logicNodes"),
                    map);
                SerializedProperty root =
                    serialized.FindProperty("rootNodeId");
                if (map.TryGetValue(
                        root.stringValue,
                        out string newRoot))
                {
                    root.stringValue = newRoot;
                }

                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(achievement);
            }

            AssetDatabase.SaveAssets();
            SyncDetectorReferences(idMaps, nameMaps);
            UpdateCatalog();
        }

        private static void SyncDetectorReferences(
            Dictionary<int, Dictionary<string, string>> idMaps,
            Dictionary<string, Dictionary<string, string>> nameMaps)
        {
            HashSet<int> visited =
                new HashSet<int>();
            List<AchievementDetector> detectors =
                new List<AchievementDetector>();
            AchievementDetector[] loaded =
                Resources.FindObjectsOfTypeAll<AchievementDetector>();
            for (int index = 0;
                 index < loaded.Length;
                 index++)
            {
                if (loaded[index] != null &&
                    visited.Add(loaded[index].GetInstanceID()))
                {
                    detectors.Add(loaded[index]);
                }
            }

            string[] prefabGuids =
                AssetDatabase.FindAssets("t:Prefab");
            for (int index = 0;
                 index < prefabGuids.Length;
                 index++)
            {
                GameObject prefab =
                    AssetDatabase.LoadAssetAtPath<GameObject>(
                        AssetDatabase.GUIDToAssetPath(
                            prefabGuids[index]));
                if (prefab == null)
                {
                    continue;
                }

                AchievementDetector[] prefabDetectors =
                    prefab.GetComponentsInChildren<
                        AchievementDetector>(true);
                for (int detectorIndex = 0;
                     detectorIndex < prefabDetectors.Length;
                     detectorIndex++)
                {
                    AchievementDetector detector =
                        prefabDetectors[detectorIndex];
                    if (detector != null &&
                        visited.Add(
                            detector.GetInstanceID()))
                    {
                        detectors.Add(detector);
                    }
                }
            }

            for (int index = 0;
                 index < detectors.Count;
                 index++)
            {
                AchievementDetector detector = detectors[index];
                SerializedObject serialized =
                    new SerializedObject(detector);
                SerializedProperty bindings =
                    serialized.FindProperty("bindings");
                bool changed = false;
                for (int bindingIndex = 0;
                     bindingIndex < bindings.arraySize;
                     bindingIndex++)
                {
                    SerializedProperty binding =
                        bindings.GetArrayElementAtIndex(
                            bindingIndex);
                    SerializedProperty mode =
                        binding.FindPropertyRelative(
                            "lookupMode");
                    Dictionary<string, string> map = null;
                    if (mode.enumValueIndex == 0)
                    {
                        int achievementId =
                            binding.FindPropertyRelative(
                                "achievementId").intValue;
                        idMaps.TryGetValue(
                            achievementId,
                            out map);
                    }
                    else
                    {
                        string achievementName =
                            binding.FindPropertyRelative(
                                "achievementName").stringValue;
                        nameMaps.TryGetValue(
                            achievementName,
                            out map);
                    }

                    if (map == null)
                    {
                        continue;
                    }

                    SerializedProperty conditionId =
                        binding.FindPropertyRelative(
                            "conditionId");
                    if (map.TryGetValue(
                            conditionId.stringValue,
                            out string newId))
                    {
                        conditionId.stringValue = newId;
                        changed = true;
                    }
                }

                if (!changed)
                {
                    continue;
                }

                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(detector);
                if (detector.gameObject.scene.IsValid())
                {
                    EditorSceneManager.MarkSceneDirty(
                        detector.gameObject.scene);
                }
            }

            AssetDatabase.SaveAssets();
        }

        private static void ApplyIdMap(
            SerializedProperty list,
            Dictionary<string, string> map)
        {
            if (list == null)
            {
                return;
            }

            for (int index = 0;
                 index < list.arraySize;
                 index++)
            {
                SerializedProperty item =
                    list.GetArrayElementAtIndex(index);
                if (map.TryGetValue(
                        item.stringValue,
                        out string newId))
                {
                    item.stringValue = newId;
                }
            }
        }

        private static void ApplyNodeIdMap(
            SerializedProperty nodes,
            Dictionary<string, string> map)
        {
            if (nodes == null)
            {
                return;
            }

            for (int index = 0;
                 index < nodes.arraySize;
                 index++)
            {
                SerializedProperty node =
                    nodes.GetArrayElementAtIndex(index);
                ApplyIdMap(
                    node.FindPropertyRelative(
                        "inputNodeIds"),
                    map);
            }
        }

        private void LoadCatalog()
        {
            catalog =
                AssetDatabase.LoadAssetAtPath<AchievementCatalogSO>(
                    catalogPath);
            if (catalog == null)
            {
                return;
            }

            if (saveDirectoryField == null ||
                saveFileNameField == null)
            {
                return;
            }

            saveDirectoryField.SetValueWithoutNotify(
                catalog.SaveDirectoryOverride);
            saveFileNameField.SetValueWithoutNotify(
                catalog.SaveFileName);
            UpdateSaveDirectoryHint();
        }

        private void ResetSave()
        {
            if (catalog == null)
            {
                LoadCatalog();
            }

            if (catalog == null)
            {
                EditorUtility.DisplayDialog(
                    "重置成就存档",
                    "没有找到成就目录，无法确定存档地址。",
                    "确定");
                return;
            }

            string filePath =
                catalog.ResolveSaveFilePath();
            if (!EditorUtility.DisplayDialog(
                    "重置成就存档",
                    "确定删除以下成就存档？\n\n" +
                    filePath +
                    "\n\n此操作无法撤销。",
                    "重置",
                    "取消"))
            {
                return;
            }

            try
            {
                AchievementSaveService saveService =
                    new AchievementSaveService(catalog);
                saveService.Clear();

                if (Application.isPlaying)
                {
                    AchievementManager manager =
                        UnityEngine.Object
                            .FindFirstObjectByType<
                                AchievementManager>();
                    manager?.Reload();
                }

                Debug.Log(
                    "[成就工具] 已重置成就存档：\n" +
                    filePath);
                EditorUtility.DisplayDialog(
                    "重置成就存档",
                    "成就存档已重置。",
                    "确定");
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[成就工具] 重置成就存档失败：\n" +
                    exception);
                EditorUtility.DisplayDialog(
                    "重置成就存档失败",
                    "请查看 Console。",
                    "确定");
            }
        }

        private void UpdateCatalogSettings()
        {
            if (catalog == null)
            {
                return;
            }

            Undo.RecordObject(
                catalog,
                "修改成就发布设置");
            SerializedObject serialized =
                new SerializedObject(catalog);
            serialized.FindProperty("saveDirectoryOverride")
                .stringValue =
                saveDirectoryField.value ?? string.Empty;
            serialized.FindProperty("saveFileName")
                .stringValue =
                saveFileNameField.value ?? "achievements.json";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            UpdateSaveDirectoryHint();
        }

        private void UpdateSaveDirectoryHint()
        {
            if (saveDirectoryHint == null ||
                catalog == null)
            {
                return;
            }

            string defaultDirectory =
                Path.Combine(
                    Application.persistentDataPath,
                    "Achievements");
            saveDirectoryHint.text =
                string.IsNullOrWhiteSpace(
                    catalog.SaveDirectoryOverride)
                    ? "目录留空时使用默认：" +
                      defaultDirectory
                    : "当前使用自定义目录，实际存档：" +
                      catalog.ResolveSaveFilePath();
        }

        private void UpdateCatalog()
        {
            EnsureFolder(
                Path.GetDirectoryName(catalogPath)
                    ?.Replace('\\', '/'));
            catalog =
                AssetDatabase.LoadAssetAtPath<AchievementCatalogSO>(
                    catalogPath);
            if (catalog == null)
            {
                catalog =
                    CreateInstance<AchievementCatalogSO>();
                AssetDatabase.CreateAsset(
                    catalog,
                    catalogPath);
            }

            Undo.RecordObject(
                catalog,
                "发布成就目录");
            List<AchievementSO> all =
                LoadAllAchievements();
            SerializedObject serialized =
                new SerializedObject(catalog);
            SerializedProperty list =
                serialized.FindProperty("achievements");
            list.arraySize = all.Count;
            for (int index = 0;
                 index < all.Count;
                 index++)
            {
                list.GetArrayElementAtIndex(index)
                    .objectReferenceValue = all[index];
            }

            serialized.FindProperty("catalogVersion")
                .stringValue = ComputeCatalogVersion(all);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private void OnUndoRedo()
        {
            if (this == null)
            {
                return;
            }

            RefreshAll();
        }

        private static List<AchievementSO> LoadAllAchievements()
        {
            string[] guids = AssetDatabase.FindAssets(
                "t:AchievementSO");
            List<AchievementSO> result =
                new List<AchievementSO>();
            for (int index = 0;
                 index < guids.Length;
                 index++)
            {
                AchievementSO achievement =
                    AssetDatabase.LoadAssetAtPath<AchievementSO>(
                        AssetDatabase.GUIDToAssetPath(
                            guids[index]));
                if (achievement != null)
                {
                    result.Add(achievement);
                }
            }

            return result;
        }

        private static string ComputeCatalogVersion(
            List<AchievementSO> all)
        {
            StringBuilder builder = new StringBuilder();
            all.Sort(
                (left, right) =>
                    left.AchievementId.CompareTo(
                        right.AchievementId));
            for (int index = 0;
                 index < all.Count;
                 index++)
            {
                AchievementSO achievement = all[index];
                builder.Append(achievement.AchievementId)
                    .Append('|')
                    .Append(achievement.DisplayName)
                    .Append('|')
                    .Append(achievement.RootNodeId)
                    .Append('|');
                for (int conditionIndex = 0;
                     conditionIndex <
                     achievement.Conditions.Count;
                     conditionIndex++)
                {
                    AchievementConditionDefinition condition =
                        achievement.Conditions[conditionIndex];
                    builder.Append(
                            condition.ConditionId)
                        .Append(':')
                        .Append((int)condition.Mode)
                        .Append(':')
                        .Append(condition.TargetCount)
                        .Append(':')
                        .Append(condition.TargetProgress)
                        .Append(';');
                }

                builder.Append('\n');
            }

            return Hash128.Compute(
                    builder.ToString())
                .ToString();
        }

        private static string ToAssetPath(
            string absolutePath)
        {
            string dataPath =
                Application.dataPath.Replace('\\', '/');
            string normalized =
                absolutePath.Replace('\\', '/');
            if (normalized.StartsWith(
                    dataPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                return "Assets" +
                       normalized.Substring(
                           dataPath.Length);
            }

            return normalized;
        }

        private static void EnsureFolder(
            string folder)
        {
            if (string.IsNullOrWhiteSpace(folder) ||
                AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            string[] parts = folder.Split('/');
            string current = parts[0];
            for (int index = 1;
                 index < parts.Length;
                 index++)
            {
                string next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(
                        current,
                        parts[index]);
                }

                current = next;
            }
        }

        private static void StyleToolbar(
            VisualElement row,
            params string[] primaryTexts)
        {
            AchievementEditorStyles.ApplyAll(
                row,
                primaryTexts);
        }
    }
}
