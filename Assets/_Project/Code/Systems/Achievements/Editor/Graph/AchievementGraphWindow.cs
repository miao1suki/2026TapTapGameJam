using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Project.Achievements.Editor
{
    public sealed class AchievementGraphWindow : EditorWindow
    {
        private AchievementSO achievement;
        private AchievementGraphView graphView;
        private ObjectField achievementField;
        private Label status;
        private VisualElement inspectorPanel;
        private AchievementGraphNode selectedNode;
        private SerializedObject achievementObject;
        private readonly List<AchievementGraphSnapshot>
            undoHistory =
                new List<AchievementGraphSnapshot>();
        private readonly List<AchievementGraphSnapshot>
            redoHistory =
                new List<AchievementGraphSnapshot>();
        private AchievementGraphSnapshot currentSnapshot;
        private bool restoringGraph;
        private bool graphDirty;

        private void OnEnable()
        {
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            achievementObject?.Dispose();
            achievementObject = null;
        }

        public static void Open()
        {
            AchievementGraphWindow window =
                GetWindow<AchievementGraphWindow>(
                    "成就具体条件逻辑图");
            window.minSize = new Vector2(900f, 600f);
        }

        public static void OpenWith(
            AchievementSO target)
        {
            AchievementGraphWindow window =
                GetWindow<AchievementGraphWindow>(
                    "成就具体条件逻辑图");
            window.minSize = new Vector2(900f, 600f);
            window.RequestSwitch(target);
        }

        private void RequestSwitch(
            AchievementSO target)
        {
            if (target == achievement)
            {
                achievementField?.SetValueWithoutNotify(
                    achievement);
                return;
            }

            if (graphDirty &&
                graphView != null)
            {
                int choice =
                    EditorUtility.DisplayDialogComplex(
                        "切换成就数据盒",
                        "当前逻辑图还有没保存的修改。",
                        "保存并切换",
                        "取消",
                        "放弃修改");
                if (choice == 1)
                {
                    achievementField?.SetValueWithoutNotify(
                        achievement);
                    return;
                }

                if (choice == 0 &&
                    !SaveGraph())
                {
                    achievementField?.SetValueWithoutNotify(
                        achievement);
                    return;
                }
            }

            achievement = target;
            achievementField?.SetValueWithoutNotify(
                achievement);
            if (graphView == null)
            {
                return;
            }

            Reload();
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
            Label title = new Label("成就具体条件逻辑图");
            title.style.fontSize = 14f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleGroup.Add(title);
            Label subtitle = new Label(
                "条件方块 · 组合关系 · 失败条件");
            subtitle.style.fontSize = 9f;
            subtitle.style.opacity = 0.65f;
            titleGroup.Add(subtitle);
            header.Add(titleGroup);
            VisualElement windowContent = new VisualElement();
            windowContent.style.flexGrow = 1f;
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
                    windowContent.style.display.value ==
                    DisplayStyle.None;
                windowContent.style.display = hidden
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
                collapse.text = hidden ? "▾" : "▸";
            };
            header.Add(collapse);
            shell.Add(header);
            shell.Add(windowContent);

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
            VisualElement primaryRow = new VisualElement();
            primaryRow.style.flexDirection = FlexDirection.Row;
            primaryRow.style.flexWrap = Wrap.Wrap;
            primaryRow.style.marginBottom = 4f;
            VisualElement actionRow = new VisualElement();
            actionRow.style.flexDirection = FlexDirection.Row;
            actionRow.style.flexWrap = Wrap.Wrap;
            achievementField = new ObjectField(
                "当前数据盒")
            {
                objectType = typeof(AchievementSO),
                allowSceneObjects = false,
                value = achievement
            };
            achievementField.style.width = 230f;
            achievementField.RegisterValueChangedCallback(evt =>
                RequestSwitch(
                    evt.newValue as AchievementSO));
            primaryRow.Add(achievementField);
            primaryRow.Add(new Button(Reload)
            {
                text = "重新读取数据盒"
            });
            primaryRow.Add(new Button(() =>
            {
                graphView.AddConditionNode();
            })
            {
                text = "＋ 添加条件"
            });
            primaryRow.Add(new Button(() =>
            {
                graphView.AddFailureNode();
            })
            {
                text = "＋ 失败条件"
            });
            StyleToolbar(
                primaryRow);
            toolbar.Add(primaryRow);
            actionRow.Add(new Button(() =>
            {
                graphView.AddLogicNode(
                    AchievementLogicType.And);
            })
            {
                text = "＋ 全部达成"
            });
            actionRow.Add(new Button(() =>
            {
                graphView.AddLogicNode(
                    AchievementLogicType.Or);
            })
            {
                text = "＋ 任意一个"
            });
            actionRow.Add(new Button(() =>
            {
                graphView.AddLogicNode(
                    AchievementLogicType.Xor);
            })
            {
                text = "＋ 只能一个"
            });
            actionRow.Add(new Button(() =>
            {
                graphView.AddLogicNode(
                    AchievementLogicType.Not);
            })
            {
                text = "＋ 必须未发生"
            });
            actionRow.Add(new Button(Bake)
            {
                text = "保存到成就"
            });
            actionRow.Add(new Button(UndoGraphChange)
            {
                text = "撤回"
            });
            actionRow.Add(new Button(RedoGraphChange)
            {
                text = "重做"
            });
            status = new Label("未加载");
            status.style.marginLeft = 10f;
            status.style.paddingLeft = 9f;
            status.style.paddingRight = 9f;
            status.style.paddingTop = 3f;
            status.style.paddingBottom = 3f;
            status.style.borderTopLeftRadius = 10f;
            status.style.borderTopRightRadius = 10f;
            status.style.borderBottomLeftRadius = 10f;
            status.style.borderBottomRightRadius = 10f;
            status.style.backgroundColor =
                new Color(0.18f, 0.55f, 0.95f, 0.22f);
            status.style.unityTextAlign =
                TextAnchor.MiddleLeft;
            actionRow.Add(status);
            StyleToolbar(
                actionRow);
            toolbar.Add(actionRow);
            windowContent.Add(toolbar);

            Label guide = new Label(
                "先在右侧填写成就名字和简介；再用线把条件连到条件组。条件组下面的一行“接入”会显示来源编号。失败条件要先点“＋ 失败条件”生成方块，再连入对应条件。");
            guide.style.whiteSpace = WhiteSpace.Normal;
            guide.style.marginBottom = 6f;
            guide.style.opacity = 0.7f;
            windowContent.Add(guide);

            VisualElement content = new VisualElement();
            content.style.flexDirection = FlexDirection.Row;
            content.style.flexGrow = 1f;
            windowContent.Add(content);

            graphView = new AchievementGraphView();
            graphView.style.flexGrow = 1f;
            graphView.Changed += MarkGraphChanged;
            content.Add(graphView);

            inspectorPanel = new VisualElement();
            inspectorPanel.style.width = 270f;
            inspectorPanel.style.marginLeft = 8f;
            inspectorPanel.style.paddingLeft = 8f;
            inspectorPanel.style.paddingRight = 8f;
            inspectorPanel.style.paddingTop = 8f;
            inspectorPanel.style.paddingBottom = 8f;
            inspectorPanel.style.overflow = Overflow.Hidden;
            inspectorPanel.style.backgroundColor =
                EditorGUIUtility.isProSkin
                    ? new Color(0.13f, 0.155f, 0.175f, 1f)
                    : new Color(0.895f, 0.91f, 0.925f, 1f);
            inspectorPanel.style.borderLeftWidth = 3f;
            inspectorPanel.style.borderLeftColor =
                new Color(0.18f, 0.55f, 0.95f, 1f);
            inspectorPanel.style.borderTopLeftRadius = 6f;
            inspectorPanel.style.borderTopRightRadius = 6f;
            inspectorPanel.style.borderBottomLeftRadius = 6f;
            inspectorPanel.style.borderBottomRightRadius = 6f;
            content.Add(inspectorPanel);
            Reload();
        }

        private void Update()
        {
            if (graphView == null ||
                inspectorPanel == null)
            {
                return;
            }

            AchievementGraphNode next =
                graphView.selection
                    .OfType<AchievementGraphNode>()
                    .FirstOrDefault();
            if (next == selectedNode)
            {
                return;
            }

            selectedNode = next;
            RebuildInspector();
        }

        private void Reload()
        {
            if (graphView == null)
            {
                return;
            }

            achievementObject?.Dispose();
            achievementObject = achievement == null
                ? null
                : new SerializedObject(achievement);
            achievementObject?.Update();
            achievementField?.SetValueWithoutNotify(
                achievement);
            restoringGraph = true;
            graphView.Load(achievement);
            restoringGraph = false;
            currentSnapshot =
                graphView.CaptureSnapshot();
            undoHistory.Clear();
            redoHistory.Clear();
            selectedNode = null;
            graphDirty = false;
            RebuildInspector();
            status.text = achievement == null
                ? "未选择成就"
                : achievement.DisplayName;
        }

        private void Bake()
        {
            SaveGraph();
        }

        private bool SaveGraph()
        {
            if (graphView == null ||
                graphView.Achievement == null)
            {
                return false;
            }

            if (!AchievementGraphBaker.Bake(graphView))
            {
                return false;
            }

            graphDirty = false;
            status.text =
                "已保存到成就：" +
                graphView.Achievement.DisplayName;
            return true;
        }

        private void RebuildInspector()
        {
            inspectorPanel.Clear();
            AddAchievementInfo();

            Label title = new Label(
                "当前选中的条件");
            title.style.unityFontStyleAndWeight =
                FontStyle.Bold;
            title.style.marginBottom = 6f;
            inspectorPanel.Add(title);

            if (selectedNode is
                AchievementConditionGraphNode conditionNode)
            {
                TextField prefix = new TextField(
                    "数值前面的文字")
                {
                    value = conditionNode.TextPrefix
                };
                prefix.tooltip =
                    "显示在进度数值前面的文字，例如“击杀”。";
                prefix.RegisterValueChangedCallback(evt =>
                {
                    conditionNode.SetConditionSettings(
                        evt.newValue,
                        conditionNode.TextSuffix,
                        conditionNode.Mode,
                        conditionNode.TargetCount,
                        conditionNode.TargetProgress);
                    MarkGraphChanged();
                });
                inspectorPanel.Add(prefix);

                TextField suffix = new TextField(
                    "数值后面的文字")
                {
                    value = conditionNode.TextSuffix
                };
                suffix.tooltip =
                    "显示在进度数值后面的文字，例如“个怪物”。";
                suffix.RegisterValueChangedCallback(evt =>
                {
                    conditionNode.SetConditionSettings(
                        conditionNode.TextPrefix,
                        evt.newValue,
                        conditionNode.Mode,
                        conditionNode.TargetCount,
                        conditionNode.TargetProgress);
                    MarkGraphChanged();
                });
                inspectorPanel.Add(suffix);

                PopupField<string> mode =
                    new PopupField<string>(
                        new System.Collections.Generic.List<string>
                        {
                            "触发一次",
                            "累计数量",
                            "完成百分比"
                        },
                        (int)conditionNode.Mode);
                mode.label = "达成方式";
                mode.tooltip =
                    "触发一次：发生一次就完成。\n" +
                    "累计数量：每次增加指定数量，达到目标后完成。\n" +
                    "完成百分比：每次增加指定百分比，达到 100% 后完成。";
                mode.RegisterValueChangedCallback(evt =>
                {
                    int modeIndex =
                        mode.choices.IndexOf(evt.newValue);
                    conditionNode.SetConditionSettings(
                        conditionNode.TextPrefix,
                        conditionNode.TextSuffix,
                        (AchievementConditionMode)modeIndex,
                        conditionNode.TargetCount,
                        conditionNode.TargetProgress);
                    MarkGraphChanged();
                });
                inspectorPanel.Add(mode);

                IntegerField count = new IntegerField(
                    "目标数量")
                {
                    value = conditionNode.TargetCount
                };
                count.RegisterValueChangedCallback(evt =>
                {
                    conditionNode.SetConditionSettings(
                        conditionNode.TextPrefix,
                        conditionNode.TextSuffix,
                        conditionNode.Mode,
                        evt.newValue,
                        conditionNode.TargetProgress);
                    MarkGraphChanged();
                });
                inspectorPanel.Add(count);

                FloatField progress = new FloatField(
                    "目标进度")
                {
                    value = conditionNode.TargetProgress
                };
                progress.RegisterValueChangedCallback(evt =>
                {
                    conditionNode.SetConditionSettings(
                        conditionNode.TextPrefix,
                        conditionNode.TextSuffix,
                        conditionNode.Mode,
                        conditionNode.TargetCount,
                        evt.newValue);
                    MarkGraphChanged();
                });
                inspectorPanel.Add(progress);

                inspectorPanel.Add(
                    new Label(
                        "失败条件请用工具栏的“＋ 失败条件”方块连接。"));
                return;
            }

            if (selectedNode is
                AchievementLogicGraphNode logicNode)
            {
                PopupField<string> logic =
                    new PopupField<string>(
                        new System.Collections.Generic.List<string>
                        {
                            "全部达成",
                            "任意一个达成",
                            "只能有一个达成",
                            "必须未发生"
                        },
                        (int)logicNode.LogicType);
                logic.label = "这些条件怎么算";
                logic.tooltip =
                    "全部达成：连接进来的条件都必须完成。\n" +
                    "任意一个达成：只要完成其中一个就可以。\n" +
                    "只能有一个达成：这些条件里只能完成一个。\n" +
                    "必须未发生：连接进来的条件必须没有发生。";
                logic.RegisterValueChangedCallback(evt =>
                {
                    int logicIndex =
                        logic.choices.IndexOf(evt.newValue);
                    logicNode.SetLogicType(
                        (AchievementLogicType)logicIndex);
                });
                inspectorPanel.Add(logic);
                inspectorPanel.Add(
                    new Label(
                        "可以把多个条件或条件组连到这里。"));
                return;
            }

            inspectorPanel.Add(
                new Label(
                    "点一个条件或条件组，就可以在这里修改。"));
        }

        private void AddAchievementInfo()
        {
            Label title = new Label(
                "成就名字和简介");
            title.style.unityFontStyleAndWeight =
                FontStyle.Bold;
            title.style.marginBottom = 6f;
            inspectorPanel.Add(title);

            if (achievementObject == null)
            {
                inspectorPanel.Add(
                    new Label("未选择成就。"));
                return;
            }

            achievementObject.Update();
            TextField nameField = new TextField(
                "成就名字")
            {
                value = graphView.Achievement.DisplayName
            };
            nameField.RegisterValueChangedCallback(evt =>
            {
                achievementObject
                    .FindProperty("displayName")
                    .stringValue = evt.newValue;
                SaveAchievementInfo("修改成就名字");
            });
            inspectorPanel.Add(nameField);

            inspectorPanel.Add(
                new Label("成就简介"));
            TextField descriptionField = new TextField
            {
                value = graphView.Achievement.Description,
                multiline = true
            };
            descriptionField.style.minHeight = 70f;
            descriptionField.style.whiteSpace =
                WhiteSpace.Normal;
            descriptionField.style.flexShrink = 1f;
            descriptionField.style.width =
                StyleKeyword.Auto;
            descriptionField.style.maxWidth =
                Length.Percent(100f);
            TextElement descriptionText =
                descriptionField.Q<TextElement>();
            if (descriptionText != null)
            {
                descriptionText.style.whiteSpace =
                    WhiteSpace.Normal;
            }
            descriptionField.RegisterValueChangedCallback(evt =>
            {
                achievementObject
                    .FindProperty("description")
                    .stringValue = evt.newValue;
                SaveAchievementInfo("修改成就简介");
            });
            inspectorPanel.Add(descriptionField);

            Label divider = new Label();
            divider.style.height = 8f;
            inspectorPanel.Add(divider);
        }

        private void SaveAchievementInfo(
            string undoName)
        {
            if (achievementObject == null ||
                graphView == null ||
                graphView.Achievement == null)
            {
                return;
            }

            Undo.RecordObject(
                graphView.Achievement,
                undoName);
            achievementObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(
                graphView.Achievement);
            AssetDatabase.SaveAssets();
            status.text =
                graphView.Achievement.DisplayName +
                " · 已保存";
        }

        private void OnUndoRedo()
        {
            if (this == null ||
                graphView == null)
            {
                return;
            }

            Reload();
        }

        private void UndoGraphChange()
        {
            if (undoHistory.Count == 0)
            {
                Undo.PerformUndo();
                return;
            }

            redoHistory.Add(
                graphView.CaptureSnapshot());
            int index = undoHistory.Count - 1;
            AchievementGraphSnapshot snapshot =
                undoHistory[index];
            undoHistory.RemoveAt(index);
            RestoreGraphSnapshot(snapshot);
        }

        private void RedoGraphChange()
        {
            if (redoHistory.Count == 0)
            {
                Undo.PerformRedo();
                return;
            }

            undoHistory.Add(
                graphView.CaptureSnapshot());
            int index = redoHistory.Count - 1;
            AchievementGraphSnapshot snapshot =
                redoHistory[index];
            redoHistory.RemoveAt(index);
            RestoreGraphSnapshot(snapshot);
        }

        private void RestoreGraphSnapshot(
            AchievementGraphSnapshot snapshot)
        {
            if (graphView == null ||
                snapshot == null)
            {
                return;
            }

            restoringGraph = true;
            graphView.ClearSelection();
            graphView.RestoreSnapshot(snapshot);
            restoringGraph = false;
            selectedNode = null;
            currentSnapshot =
                graphView.CaptureSnapshot();
            graphDirty = true;
            RebuildInspector();
            status.text =
                graphView.Achievement == null
                    ? "未选择成就"
                    : graphView.Achievement.DisplayName +
                      " · 未保存";
        }

        private void MarkGraphChanged()
        {
            if (restoringGraph ||
                graphView == null ||
                graphView.Achievement == null)
            {
                return;
            }

            if (currentSnapshot != null)
            {
                undoHistory.Add(currentSnapshot);
                redoHistory.Clear();
            }

            graphView.RefreshSourceSummaries();
            currentSnapshot =
                graphView.CaptureSnapshot();
            graphDirty = true;
            if (status != null &&
                graphView.Achievement != null)
            {
                status.text =
                    graphView.Achievement.DisplayName +
                    " · 未保存";
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
