using System.Collections.Generic;
using System.IO;
using Project.ColorBlocks.Editor;
using Project.LevelEditor;
using Project.LevelEditor.Editor;
using Project.SurfaceTiles.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace PlanningEditorPrototype
{
    public sealed class PlanningEditorWindow : EditorWindow
    {
        private enum PlanningWorkspaceMode
        {
            Map,
            Colors,
            InteractionGraph
        }

        private const string PreferenceKey =
            "2026TapTap.PlanningEditorPrototype.Document.SampleV11";
        private const string CurrentSavePreferenceKey =
            "2026TapTap.PlanningEditorPrototype.CurrentSavePath";
        private const string CanvasBackgroundPreferenceKey =
            "2026TapTap.PlanningEditorPrototype.CanvasBackground";

        private PlanningDocument document;
        private PlanningCanvas canvas;
        private TextField mapNameField;
        private ColorField canvasBackgroundField;
        private IntegerField worldBlockWidthField;
        private IntegerField worldBlockHeightField;
        private Button worldBlockUnlockButton;
        private Button worldBlockApplyButton;
        private ObjectField playerContextField;
        private ObjectField roomParentField;
        private Label contextStatusLabel;
        private VisualElement inspector;
        private VisualElement roomList;
        private VisualElement connectorList;
        private Label statusLabel;
        private VisualElement saveOverlay;
        private VisualElement saveSlotList;
        private TextField saveNameField;
        private Label saveOverlayStatus;
        private string currentSavePath;
        private readonly Dictionary<PlanningMapTool, Button> mapToolButtons =
            new Dictionary<PlanningMapTool, Button>();
        private readonly Dictionary<PlanningWorkspaceMode, Button> workspaceButtons =
            new Dictionary<PlanningWorkspaceMode, Button>();
        private readonly Dictionary<PlanningCanvasMode, Button> modeButtons =
            new Dictionary<PlanningCanvasMode, Button>();
        private VisualElement mapWorkspace;
        private VisualElement colorsWorkspace;
        private VisualElement graphWorkspace;
        private VisualElement topArea;
        private VisualElement leftRail;
        private VisualElement centerStage;
        private VisualElement rightPanel;
        private VisualElement worldMapTools;
        private Foldout blockEditorFoldout;
        private LevelEditorPanel embeddedLevelEditor;
        private bool refreshingLevelContext;
        private double nextLevelEditorRefreshTime;
        private float leftRailWidth = 200f;
        private float rightPanelWidth = 220f;
        private float topAreaHeight = 110f;
        private Color canvasBackgroundColor =
            new Color(.11f, .13f, .17f);
        private readonly Stack<string> planningUndo =
            new Stack<string>();
        private readonly Stack<string> planningRedo =
            new Stack<string>();
        private string planningSnapshot;
        private bool restoringPlanningDocument;
        private bool worldBlockSettingsUnlocked;
        private PlanningWorkspaceMode workspaceMode;
        private PlanningCanvasMode mode;

        [MenuItem("Tools/2026TapTap/关卡规划原型/打开融合编辑器")]
        private static void Open()
        {
            PlanningEditorWindow window =
                GetWindow<PlanningEditorWindow>();
            window.titleContent = new GUIContent("关卡规划融合原型");
            window.minSize = new Vector2(980f, 620f);
            window.Show();
            window.Focus();
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.style.flexGrow = 1f;
            root.style.backgroundColor = new Color(.07f, .08f, .1f);
            canvasBackgroundColor = LoadCanvasBackgroundColor();
            root.RegisterCallback<GeometryChangedEvent>(_ =>
                ClampPaneSizes());

            root.Add(BuildTopBar());
            topArea = new VisualElement();
            topArea.style.height = topAreaHeight;
            topArea.style.minHeight = 72f;
            topArea.style.maxHeight = 240f;
            topArea.style.flexShrink = 0f;
            topArea.Add(BuildWorkspaceTabs());
            topArea.Add(BuildLevelContextBar());
            root.Add(topArea);
            root.Add(BuildHorizontalSplitter(
                () => topAreaHeight,
                SetTopAreaHeight,
                70f,
                240f));
            VisualElement workspaceHost = new VisualElement();
            workspaceHost.style.flexGrow = 1f;
            root.Add(workspaceHost);

            VisualElement main = new VisualElement();
            main.style.flexDirection = FlexDirection.Row;
            main.style.flexGrow = 1f;
            main.style.overflow = Overflow.Hidden;
            mapWorkspace = main;
            workspaceHost.Add(main);

            leftRail = BuildLeftRail();
            main.Add(leftRail);
            main.Add(BuildVerticalSplitter(
                false,
                () => leftRailWidth,
                SetLeftRailWidth,
                110f,
                420f));
            canvas = new PlanningCanvas();
            canvas.DocumentChanged += OnCanvasDocumentChanged;
            canvas.SelectionChanged += OnCanvasSelectionChanged;
            canvas.SetBackgroundColor(canvasBackgroundColor);
            LevelEditorPalette sharedPalette =
                LevelEditorPaletteService.GetOrCreate();
            canvas.SetPalette(sharedPalette);
            if (sharedPalette != null &&
                sharedPalette.Entries.Count > 0 &&
                sharedPalette.Entries[0] != null)
            {
                LevelEditorBlockEntry defaultEntry =
                    sharedPalette.Entries[0];
                canvas.SetDetailEntry(
                    defaultEntry.EntryId,
                    defaultEntry.DisplayName);
            }

            centerStage = new VisualElement();
            centerStage.style.flexGrow = 1f;
            centerStage.style.minWidth = 160f;
            centerStage.style.overflow = Overflow.Hidden;
            centerStage.style.backgroundColor = new Color(.055f, .065f, .085f);
            canvas.style.overflow = Overflow.Hidden;
            centerStage.Add(canvas);
            main.Add(centerStage);
            main.Add(BuildVerticalSplitter(
                true,
                () => rightPanelWidth,
                SetRightPanelWidth,
                180f,
                620f));
            rightPanel = BuildInspector();
            main.Add(rightPanel);
            colorsWorkspace = BuildPlaceholderWorkspace(
                "颜色工作台",
                "队友颜色模块的入口：颜色类型、颜色钥匙、材质、事件和解锁预览。",
                new[]
                {
                    "红色 · 解锁与烧灼",
                    "蓝色 · 交互与解锁",
                    "绿色 · 交互与解锁",
                    "新增颜色位",
                    "初始化颜色资源"
                });
            AddWorkspaceAction(
                colorsWorkspace,
                "打开颜色工作台",
                ColorWorkbenchWindow.Open);
            AddWorkspaceAction(
                colorsWorkspace,
                "初始化颜色资源",
                ColorProjectSetup.InitializeFromMenu);
            graphWorkspace = BuildPlaceholderWorkspace(
                "交互图",
                "颜色、方块功能和环境效果之间的节点、连线与规则会在这里统一编辑。",
                new[]
                {
                    "颜色节点 → 方块能力",
                    "方块能力 → 环境效果",
                    "环境效果 → 新通路",
                    "条件 / 延迟 / 连锁",
                    "添加交互节点"
                });
            AddWorkspaceAction(
                graphWorkspace,
                "打开颜色交互图",
                ColorWorkbenchWindow.Open);
            workspaceHost.Add(colorsWorkspace);
            workspaceHost.Add(graphWorkspace);

            statusLabel = new Label();
            statusLabel.style.height = 22f;
            statusLabel.style.paddingLeft = 8f;
            statusLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            statusLabel.style.color = new Color(.62f, .68f, .8f);
            statusLabel.style.backgroundColor = new Color(.09f, .1f, .13f);
            root.Add(statusLabel);
            root.Add(BuildSaveOverlay());
            canvas.ValidationFailed += message =>
            {
                statusLabel.text = message;
            };
            canvas.StatusChanged += message =>
            {
                statusLabel.text = message;
            };

            LoadDocument();
            SetWorkspace(PlanningWorkspaceMode.Map);
            SetMode(PlanningCanvasMode.World);
            RefreshAll();
        }

        private void OnEnable()
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Tick;
            if (embeddedLevelEditor == null)
            {
                return;
            }

            LevelEditorState.EditMode = false;
            SurfaceTileEditorBridge.SetPainting(false);
            LevelEditorViewLock.Exit();
        }

        private void Tick()
        {
            if (embeddedLevelEditor == null)
            {
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            if (now < nextLevelEditorRefreshTime)
            {
                return;
            }

            nextLevelEditorRefreshTime = now + .1d;
            embeddedLevelEditor.Refresh();
            SyncPlanningCanvasPalette();
            RefreshLevelContextStatus();
            if (LevelEditorPlayerService.EnforceEditPlane())
            {
                SceneView.RepaintAll();
            }
        }

        private VisualElement BuildWorkspaceTabs()
        {
            VisualElement tabs = Row();
            tabs.style.height = 32f;
            tabs.style.paddingLeft = 6f;
            tabs.style.paddingRight = 6f;
            tabs.style.alignItems = Align.Center;
            tabs.style.backgroundColor = new Color(.08f, .1f, .13f);
            tabs.style.borderBottomWidth = 1f;
            tabs.style.borderBottomColor = new Color(.2f, .23f, .3f);
            AddWorkspaceButton(tabs, PlanningWorkspaceMode.Map, "地图规划");
            AddWorkspaceButton(tabs, PlanningWorkspaceMode.Colors, "颜色工作台");
            AddWorkspaceButton(
                tabs,
                PlanningWorkspaceMode.InteractionGraph,
                "交互图");
            return tabs;
        }

        private VisualElement BuildLevelContextBar()
        {
            VisualElement bar = Row();
            bar.style.minHeight = 38f;
            bar.style.flexGrow = 1f;
            bar.style.flexWrap = Wrap.Wrap;
            bar.style.paddingLeft = 8f;
            bar.style.paddingRight = 8f;
            bar.style.paddingTop = 3f;
            bar.style.paddingBottom = 3f;
            bar.style.alignItems = Align.Center;
            bar.style.alignContent = Align.Center;
            bar.style.backgroundColor = new Color(.075f, .09f, .115f);
            bar.style.borderBottomWidth = 1f;
            bar.style.borderBottomColor = new Color(.18f, .21f, .27f);

            playerContextField = new ObjectField("玩家")
            {
                objectType = typeof(GameObject),
                allowSceneObjects = true
            };
            playerContextField.style.width = 260f;
            playerContextField.style.marginRight = 4f;
            playerContextField.RegisterValueChangedCallback(evt =>
            {
                if (!refreshingLevelContext)
                {
                    embeddedLevelEditor?.SetPlayer(
                        evt.newValue as GameObject);
                    RefreshLevelContextStatus();
                }
            });
            bar.Add(playerContextField);

            Button useSelectedPlayer = TopButton(
                "设为玩家",
                () =>
                {
                    embeddedLevelEditor?.SetPlayer(
                        Selection.activeGameObject);
                    RefreshLevelContext();
                });
            bar.Add(useSelectedPlayer);

            Button focusPlayer = TopButton(
                "聚焦玩家",
                () => embeddedLevelEditor?.FocusPlayer());
            bar.Add(focusPlayer);
            bar.Add(ContextSeparator());

            roomParentField = new ObjectField("房间父物体")
            {
                objectType = typeof(GameObject),
                allowSceneObjects = true
            };
            roomParentField.style.width = 260f;
            roomParentField.style.marginRight = 4f;
            roomParentField.RegisterValueChangedCallback(evt =>
            {
                if (!refreshingLevelContext)
                {
                    SetCurrentRoomParent(
                        evt.newValue as GameObject);
                }
            });
            bar.Add(roomParentField);

            Button useSelectedParent = TopButton(
                "设为房间父物体",
                () => SetCurrentRoomParent(
                    Selection.activeGameObject));
            bar.Add(useSelectedParent);

            bar.Add(TopButton(
                "创建 / 定位房间容器",
                () => EnsureCurrentRoomContainer(true)));

            contextStatusLabel = new Label();
            contextStatusLabel.style.flexGrow = 1f;
            contextStatusLabel.style.opacity = .7f;
            contextStatusLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            bar.Add(contextStatusLabel);
            return bar;
        }

        private static VisualElement BuildVerticalSplitter(
            bool invert,
            System.Func<float> getSize,
            System.Action<float> setSize,
            float minimum,
            float maximum)
        {
            VisualElement handle = new VisualElement
            {
                pickingMode = PickingMode.Position
            };
            handle.tooltip = "拖动调整宽度";
            handle.style.width = 12f;
            handle.style.flexShrink = 0f;
            handle.style.alignItems = Align.Center;
            handle.style.justifyContent = Justify.Center;
            handle.style.backgroundColor = new Color(.16f, .19f, .24f);
            Label grip = new Label("⋮")
            {
                pickingMode = PickingMode.Ignore
            };
            grip.style.fontSize = 13f;
            grip.style.opacity = .8f;
            handle.Add(grip);

            int pointerId = -1;
            float startPointer = 0f;
            float startSize = 0f;
            handle.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0)
                {
                    return;
                }

                pointerId = evt.pointerId;
                startPointer = evt.position.x;
                startSize = getSize();
                handle.style.backgroundColor =
                    new Color(.28f, .62f, .9f);
                PointerCaptureHelper.CapturePointer(handle, pointerId);
                evt.StopPropagation();
            });
            handle.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (evt.pointerId != pointerId)
                {
                    return;
                }

                float delta = evt.position.x - startPointer;
                float next = startSize + (invert ? -delta : delta);
                setSize(Mathf.Clamp(next, minimum, maximum));
                evt.StopPropagation();
            });
            handle.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (evt.pointerId != pointerId)
                {
                    return;
                }

                PointerCaptureHelper.ReleasePointer(handle, pointerId);
                pointerId = -1;
                handle.style.backgroundColor =
                    new Color(.16f, .19f, .24f);
                evt.StopPropagation();
            });
            handle.RegisterCallback<PointerCancelEvent>(_ =>
            {
                if (pointerId < 0)
                {
                    return;
                }

                PointerCaptureHelper.ReleasePointer(handle, pointerId);
                pointerId = -1;
                handle.style.backgroundColor =
                    new Color(.16f, .19f, .24f);
            });
            handle.RegisterCallback<PointerEnterEvent>(_ =>
            {
                if (pointerId < 0)
                {
                    handle.style.backgroundColor =
                        new Color(.22f, .28f, .36f);
                }
            });
            handle.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                if (pointerId < 0)
                {
                    handle.style.backgroundColor =
                        new Color(.16f, .19f, .24f);
                }
            });
            return handle;
        }

        private static VisualElement BuildHorizontalSplitter(
            System.Func<float> getSize,
            System.Action<float> setSize,
            float minimum,
            float maximum)
        {
            VisualElement handle = new VisualElement
            {
                pickingMode = PickingMode.Position
            };
            handle.tooltip = "拖动调整高度";
            handle.style.height = 12f;
            handle.style.flexShrink = 0f;
            handle.style.alignItems = Align.Center;
            handle.style.justifyContent = Justify.Center;
            handle.style.backgroundColor = new Color(.16f, .19f, .24f);
            Label grip = new Label("⋯")
            {
                pickingMode = PickingMode.Ignore
            };
            grip.style.fontSize = 12f;
            grip.style.opacity = .8f;
            handle.Add(grip);

            int pointerId = -1;
            float startPointer = 0f;
            float startSize = 0f;
            handle.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0)
                {
                    return;
                }

                pointerId = evt.pointerId;
                startPointer = evt.position.y;
                startSize = getSize();
                handle.style.backgroundColor =
                    new Color(.28f, .62f, .9f);
                PointerCaptureHelper.CapturePointer(handle, pointerId);
                evt.StopPropagation();
            });
            handle.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (evt.pointerId != pointerId)
                {
                    return;
                }

                float delta = evt.position.y - startPointer;
                setSize(Mathf.Clamp(
                    startSize + delta,
                    minimum,
                    maximum));
                evt.StopPropagation();
            });
            handle.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (evt.pointerId != pointerId)
                {
                    return;
                }

                PointerCaptureHelper.ReleasePointer(handle, pointerId);
                pointerId = -1;
                handle.style.backgroundColor =
                    new Color(.16f, .19f, .24f);
                evt.StopPropagation();
            });
            handle.RegisterCallback<PointerCancelEvent>(_ =>
            {
                if (pointerId < 0)
                {
                    return;
                }

                PointerCaptureHelper.ReleasePointer(handle, pointerId);
                pointerId = -1;
                handle.style.backgroundColor =
                    new Color(.16f, .19f, .24f);
            });
            return handle;
        }

        private void SetTopAreaHeight(float value)
        {
            if (Mathf.Approximately(topAreaHeight, value))
            {
                return;
            }

            topAreaHeight = value;
            if (topArea != null)
            {
                topArea.style.height = value;
            }
        }

        private void SetLeftRailWidth(float value)
        {
            if (Mathf.Approximately(leftRailWidth, value))
            {
                return;
            }

            leftRailWidth = value;
            if (leftRail != null)
            {
                leftRail.style.width = value;
            }
        }

        private void SetRightPanelWidth(float value)
        {
            if (Mathf.Approximately(rightPanelWidth, value))
            {
                return;
            }

            rightPanelWidth = value;
            if (rightPanel != null)
            {
                rightPanel.style.width = value;
            }
        }

        private void ClampPaneSizes()
        {
            if (leftRail == null ||
                rightPanel == null ||
                topArea == null)
            {
                return;
            }

            float width = rootVisualElement.resolvedStyle.width;
            float height = rootVisualElement.resolvedStyle.height;
            if (float.IsNaN(width) ||
                float.IsNaN(height) ||
                width <= 0f ||
                height <= 0f)
            {
                return;
            }

            float maxTop = Mathf.Clamp(
                height - 42f - 22f - 220f,
                70f,
                240f);
            SetTopAreaHeight(Mathf.Clamp(
                topAreaHeight,
                70f,
                maxTop));

            float available = Mathf.Max(
                330f,
                width - 30f - 160f);
            float leftMax = Mathf.Min(
                420f,
                available - 180f);
            float left = Mathf.Clamp(
                leftRailWidth,
                110f,
                Mathf.Max(110f, leftMax));
            float rightMax = Mathf.Min(
                620f,
                available - left);
            float right = Mathf.Clamp(
                rightPanelWidth,
                180f,
                Mathf.Max(180f, rightMax));
            if (left + right > available)
            {
                left = Mathf.Max(110f, available - right);
            }

            SetLeftRailWidth(left);
            SetRightPanelWidth(right);
        }

        private VisualElement BuildPlaceholderWorkspace(
            string title,
            string description,
            string[] items)
        {
            VisualElement host = new VisualElement();
            host.style.flexGrow = 1f;
            host.style.paddingLeft = 18f;
            host.style.paddingRight = 18f;
            host.style.paddingTop = 16f;
            host.style.backgroundColor = new Color(.07f, .08f, .1f);

            Label heading = new Label(title);
            heading.style.fontSize = 20f;
            heading.style.unityFontStyleAndWeight = FontStyle.Bold;
            host.Add(heading);

            Label explanation = new Label(description);
            explanation.style.whiteSpace = WhiteSpace.Normal;
            explanation.style.color = new Color(.65f, .72f, .82f);
            explanation.style.marginTop = 6f;
            explanation.style.marginBottom = 14f;
            host.Add(explanation);

            VisualElement list = new VisualElement();
            list.style.flexDirection = FlexDirection.Row;
            list.style.flexWrap = Wrap.Wrap;
            for (int index = 0; index < items.Length; index++)
            {
                Label item = new Label(items[index]);
                item.style.width = 170f;
                item.style.height = 54f;
                item.style.marginRight = 8f;
                item.style.marginBottom = 8f;
                item.style.paddingLeft = 10f;
                item.style.paddingTop = 8f;
                item.style.backgroundColor = new Color(.12f, .15f, .2f);
                item.style.borderLeftWidth = 3f;
                item.style.borderLeftColor = new Color(.28f, .62f, .9f);
                item.style.unityTextAlign = TextAnchor.MiddleLeft;
                list.Add(item);
            }

            host.Add(list);
            return host;
        }

        private static void AddWorkspaceAction(
            VisualElement host,
            string label,
            System.Action action)
        {
            Button button = TopButton(label, action);
            button.style.width = 180f;
            button.style.marginTop = 12f;
            host.Add(button);
        }

        private void AddWorkspaceButton(
            VisualElement parent,
            PlanningWorkspaceMode mode,
            string text)
        {
            Button button = new Button(() => SetWorkspace(mode))
            {
                text = text
            };
            button.style.flexGrow = 1f;
            button.style.minWidth = 0f;
            button.style.height = 24f;
            button.style.marginLeft = 2f;
            button.style.marginRight = 2f;
            button.style.unityTextAlign = TextAnchor.MiddleCenter;
            parent.Add(button);
            workspaceButtons[mode] = button;
        }

        private void SetWorkspace(PlanningWorkspaceMode value)
        {
            workspaceMode = value;
            if (mapWorkspace != null)
            {
                mapWorkspace.style.display = value == PlanningWorkspaceMode.Map
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            }

            colorsWorkspace.style.display =
                value == PlanningWorkspaceMode.Colors
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            graphWorkspace.style.display =
                value == PlanningWorkspaceMode.InteractionGraph
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            foreach (var pair in workspaceButtons)
            {
                pair.Value.style.backgroundColor =
                    pair.Key == value
                        ? new Color(.18f, .34f, .52f)
                        : new Color(.12f, .15f, .19f);
            }
        }

        private VisualElement BuildTopBar()
        {
            VisualElement bar = Row();
            bar.style.height = 42f;
            bar.style.paddingLeft = 8f;
            bar.style.paddingRight = 8f;
            bar.style.alignItems = Align.Center;
            bar.style.backgroundColor = new Color(.1f, .12f, .15f);
            bar.style.borderBottomWidth = 1f;
            bar.style.borderBottomColor = new Color(.2f, .23f, .3f);

            Label brand = new Label("▚  规划融合原型");
            brand.style.unityFontStyleAndWeight = FontStyle.Bold;
            brand.style.fontSize = 14f;
            brand.style.marginRight = 8f;
            bar.Add(brand);

            mapNameField = new TextField();
            mapNameField.style.width = 220f;
            mapNameField.style.height = 28f;
            mapNameField.RegisterValueChangedCallback(evt =>
            {
                if (document != null)
                {
                    document.name = evt.newValue;
                    SaveDocument();
                }
            });
            bar.Add(mapNameField);

            bar.Add(Spacer());
            bar.Add(TopButton("生成整套场景", GenerateMapToScene));
            bar.Add(TopButton("同步到装配图", SyncSceneToAssembly));
            bar.Add(TopButton("修复同步", RepairAndSyncCurrentMap));
            bar.Add(TopButton("撤回", UndoPlanning));
            bar.Add(TopButton("重做", RedoPlanning));
            Button saves = TopButton("新建 / 存档", ToggleSaveOverlay);
            saves.tooltip =
                "新建空白地图、读取、保存、重命名和导入导出 JSON。";
            bar.Add(saves);
            return bar;
        }

        private VisualElement BuildSaveOverlay()
        {
            saveOverlay = new VisualElement
            {
                pickingMode = PickingMode.Position
            };
            saveOverlay.style.position = Position.Absolute;
            saveOverlay.style.top = 52f;
            saveOverlay.style.right = 18f;
            saveOverlay.style.bottom = 26f;
            saveOverlay.style.width = 640f;
            saveOverlay.style.minWidth = 480f;
            saveOverlay.style.paddingLeft = 12f;
            saveOverlay.style.paddingRight = 12f;
            saveOverlay.style.paddingTop = 10f;
            saveOverlay.style.paddingBottom = 10f;
            saveOverlay.style.backgroundColor =
                new Color(.075f, .09f, .12f, .995f);
            saveOverlay.style.borderLeftWidth = 1f;
            saveOverlay.style.borderRightWidth = 1f;
            saveOverlay.style.borderTopWidth = 1f;
            saveOverlay.style.borderBottomWidth = 1f;
            saveOverlay.style.borderLeftColor =
                saveOverlay.style.borderRightColor =
                saveOverlay.style.borderTopColor =
                saveOverlay.style.borderBottomColor =
                new Color(.27f, .34f, .44f);
            saveOverlay.style.display = DisplayStyle.None;
            Round(saveOverlay, 6f);
            saveOverlay.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode != KeyCode.Escape ||
                    saveOverlay.style.display.value ==
                    DisplayStyle.None)
                {
                    return;
                }

                ToggleSaveOverlay();
                evt.StopPropagation();
            });

            VisualElement header = Row();
            header.style.alignItems = Align.Center;
            Label title = new Label("地图存档");
            title.style.fontSize = 17f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.flexGrow = 1f;
            header.Add(title);
            header.Add(SaveSlotButton(
                "关闭",
                ToggleSaveOverlay));
            saveOverlay.Add(header);

            Label hint = new Label(
                "存档保存在项目的 Library/PlanningEditorSaves 文件夹。" +
                "每个存档都可以单独读取、保存、重命名和导出。");
            hint.style.fontSize = 11f;
            hint.style.opacity = .68f;
            hint.style.whiteSpace = WhiteSpace.Normal;
            hint.style.marginBottom = 8f;
            saveOverlay.Add(hint);

            VisualElement createRow = Row();
            createRow.style.alignItems = Align.Center;
            createRow.style.flexWrap = Wrap.Wrap;
            createRow.style.marginBottom = 4f;
            saveNameField = new TextField("新存档名")
            {
                isDelayed = true
            };
            saveNameField.style.flexGrow = 1f;
            saveNameField.style.minWidth = 140f;
            saveNameField.style.marginRight = 4f;
            createRow.Add(saveNameField);
            createRow.Add(TopButton(
                "新建空存档",
                CreateBlankSave));
            createRow.Add(TopButton(
                "保存当前为新存档",
                SaveCurrentAsNewSlot));
            createRow.Add(TopButton(
                "导入 JSON 为新存档",
                ImportJsonAsNewSave));
            saveOverlay.Add(createRow);
            saveOverlay.Add(Separator());

            ScrollView slotScroll =
                new ScrollView(ScrollViewMode.Vertical);
            slotScroll.horizontalScrollerVisibility =
                ScrollerVisibility.Hidden;
            slotScroll.style.flexGrow = 1f;
            slotScroll.style.minHeight = 180f;
            saveSlotList = new VisualElement();
            saveSlotList.style.width = Length.Percent(100f);
            slotScroll.Add(saveSlotList);
            saveOverlay.Add(slotScroll);

            saveOverlayStatus = new Label();
            saveOverlayStatus.style.fontSize = 11f;
            saveOverlayStatus.style.opacity = .76f;
            saveOverlayStatus.style.whiteSpace = WhiteSpace.Normal;
            saveOverlayStatus.style.marginTop = 6f;
            saveOverlayStatus.text =
                $"存档目录：{PlanningSaveStore.SaveFolder}";
            saveOverlay.Add(saveOverlayStatus);
            return saveOverlay;
        }

        private VisualElement BuildLeftRail()
        {
            ScrollView rail = new ScrollView(ScrollViewMode.Vertical);
            rail.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            rail.style.width = leftRailWidth;
            rail.style.minWidth = 110f;
            rail.style.maxWidth = 420f;
            rail.style.flexGrow = 1f;
            rail.style.flexShrink = 0f;
            rail.style.backgroundColor = new Color(.09f, .105f, .135f);
            rail.style.borderRightWidth = 1f;
            rail.style.borderRightColor = new Color(.2f, .23f, .3f);

            Foldout worldBlockFoldout = new Foldout
            {
                text = "世界图区块",
                value = false
            };
            worldBlockFoldout.style.marginLeft = 6f;
            worldBlockFoldout.style.marginRight = 6f;
            worldBlockFoldout.style.marginBottom = 4f;
            worldBlockFoldout.style.borderLeftWidth = 3f;
            worldBlockFoldout.style.borderLeftColor =
                new Color(1f, .62f, .38f);
            worldBlockFoldout.RegisterValueChangedCallback(evt =>
            {
                if (!evt.newValue)
                {
                    LockWorldBlockSettings();
                }
            });

            worldBlockWidthField = new IntegerField("区块宽")
            {
                value = document?.worldBlockCellWidth ?? 16,
                isDelayed = true
            };
            worldBlockHeightField = new IntegerField("区块高")
            {
                value = document?.worldBlockCellHeight ?? 16,
                isDelayed = true
            };
            worldBlockWidthField.SetEnabled(false);
            worldBlockHeightField.SetEnabled(false);
            worldBlockFoldout.Add(worldBlockWidthField);
            worldBlockFoldout.Add(worldBlockHeightField);

            Label worldBlockWarning = new Label(
                "警告：修改后地图比例、边界和生成结果会重算。" +
                "必须先解锁，再确认应用。");
            worldBlockWarning.style.whiteSpace = WhiteSpace.Normal;
            worldBlockWarning.style.color = new Color(1f, .62f, .38f);
            worldBlockWarning.style.fontSize = 11f;
            worldBlockWarning.style.marginLeft = 4f;
            worldBlockWarning.style.marginRight = 4f;
            worldBlockWarning.style.marginTop = 4f;
            worldBlockFoldout.Add(worldBlockWarning);

            VisualElement worldBlockActions = Row();
            worldBlockUnlockButton = TopButton(
                "解锁修改",
                ToggleWorldBlockUnlock);
            worldBlockApplyButton = TopButton(
                "应用并重算",
                ApplyWorldBlockSettings);
            worldBlockApplyButton.SetEnabled(false);
            worldBlockActions.Add(worldBlockUnlockButton);
            worldBlockActions.Add(worldBlockApplyButton);
            worldBlockActions.Add(TopButton(
                "取消",
                LockWorldBlockSettings));
            worldBlockFoldout.Add(worldBlockActions);
            rail.Add(worldBlockFoldout);

            rail.Add(SectionTitle("世界图内容"));
            canvasBackgroundField = new ColorField("画布背景")
            {
                value = canvasBackgroundColor
            };
            canvasBackgroundField.style.marginLeft = 6f;
            canvasBackgroundField.style.marginRight = 6f;
            canvasBackgroundField.style.marginBottom = 4f;
            canvasBackgroundField.RegisterValueChangedCallback(evt =>
            {
                canvasBackgroundColor = evt.newValue;
                SaveCanvasBackgroundColor();
                canvas?.SetBackgroundColor(canvasBackgroundColor);
            });
            rail.Add(canvasBackgroundField);
            rail.Add(SmallRowButton(
                "恢复默认背景",
                ResetCanvasBackground));

            Foldout worldToolsFoldout = new Foldout
            {
                text = "世界图工具",
                value = true
            };
            worldToolsFoldout.style.marginLeft = 6f;
            worldToolsFoldout.style.marginRight = 6f;
            worldToolsFoldout.style.marginBottom = 4f;
            worldMapTools = worldToolsFoldout;
            VisualElement tools = new VisualElement();
            tools.style.flexDirection = FlexDirection.Column;
            tools.style.width = Length.Percent(100f);
            tools.style.flexShrink = 0f;
            tools.Add(ToolGroupTitle("搭建"));
            VisualElement mapToolRowA = ToolRow();
            AddMapToolButton(mapToolRowA, PlanningMapTool.Paint, "房间区块");
            AddMapToolButton(mapToolRowA, PlanningMapTool.Connector, "通道区块");
            tools.Add(mapToolRowA);
            tools.Add(ToolGroupTitle("编辑"));
            VisualElement mapToolRowB = ToolRow();
            AddMapToolButton(mapToolRowB, PlanningMapTool.Erase, "擦除");
            tools.Add(mapToolRowB);
            tools.Add(ToolGroupTitle("导航"));
            VisualElement mapToolRowC = ToolRow();
            AddMapToolButton(mapToolRowC, PlanningMapTool.Select, "选择");
            AddMapToolButton(mapToolRowC, PlanningMapTool.Pan, "拖动");
            tools.Add(mapToolRowC);
            worldToolsFoldout.Add(tools);
            rail.Add(worldToolsFoldout);

            rail.Add(SectionTitle("房间列表"));
            roomList = ScrollList(190f);
            rail.Add(roomList);
            rail.Add(SmallRowButton("+ 新增房间", AddRoom));

            rail.Add(SectionTitle("连接通道"));
            connectorList = ScrollList(130f);
            rail.Add(connectorList);

            return rail;
        }

        private VisualElement BuildInspector()
        {
            ScrollView panel = new ScrollView(ScrollViewMode.Vertical);
            panel.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            panel.style.width = rightPanelWidth;
            panel.style.minWidth = 180f;
            panel.style.maxWidth = 620f;
            panel.style.flexGrow = 1f;
            panel.style.flexShrink = 0f;
            panel.style.backgroundColor = new Color(.09f, .105f, .135f);
            panel.style.borderLeftWidth = 1f;
            panel.style.borderLeftColor = new Color(.2f, .23f, .3f);
            panel.Add(SectionTitle("当前选中"));

            VisualElement modeRow = Row();
            modeRow.style.paddingLeft = 6f;
            modeRow.style.paddingRight = 6f;
            modeRow.style.paddingTop = 6f;
            modeRow.style.paddingBottom = 6f;
            Button world = TopButton("世界图", () =>
                SetMode(PlanningCanvasMode.World));
            Button detail = TopButton("房间详情", () =>
                SetMode(PlanningCanvasMode.Detail));
            Button assembly = TopButton("装配图", () =>
                SetMode(PlanningCanvasMode.Assembly));
            modeButtons[PlanningCanvasMode.World] = world;
            modeButtons[PlanningCanvasMode.Detail] = detail;
            modeButtons[PlanningCanvasMode.Assembly] = assembly;
            world.style.flexGrow = 1f;
            detail.style.flexGrow = 1f;
            assembly.style.flexGrow = 1f;
            modeRow.Add(world);
            modeRow.Add(detail);
            modeRow.Add(assembly);
            panel.Add(modeRow);

            blockEditorFoldout = new Foldout
            {
                text = "方块 / 道具放置器",
                value = false
            };
            blockEditorFoldout.style.marginLeft = 6f;
            blockEditorFoldout.style.marginRight = 6f;
            blockEditorFoldout.style.marginBottom = 4f;
            blockEditorFoldout.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue)
                {
                    EnsureCurrentRoomContainer(false);
                }
            });
            VisualElement editorHost = new VisualElement();
            editorHost.style.height = 380f;
            editorHost.style.minHeight = 300f;
            embeddedLevelEditor = new LevelEditorPanel(editorHost, false);
            blockEditorFoldout.Add(editorHost);
            panel.Add(blockEditorFoldout);

            inspector = new VisualElement();
            inspector.style.paddingLeft = 8f;
            inspector.style.paddingRight = 8f;
            inspector.style.paddingTop = 4f;
            panel.Add(inspector);
            return panel;
        }

        private void LoadDocument()
        {
            string json = EditorPrefs.GetString(PreferenceKey, string.Empty);
            currentSavePath = EditorPrefs.GetString(
                CurrentSavePreferenceKey,
                string.Empty);
            if (!string.IsNullOrEmpty(currentSavePath) &&
                !File.Exists(currentSavePath))
            {
                currentSavePath = string.Empty;
                EditorPrefs.DeleteKey(CurrentSavePreferenceKey);
            }

            if (string.IsNullOrWhiteSpace(json))
            {
                document = PlanningDocument.CreateDefault();
            }
            else
            {
                document = JsonUtility.FromJson<PlanningDocument>(json);
                document ??= PlanningDocument.CreateDefault();
            }

            document.Normalize();
            canvas.SetDocument(document);
            ResetPlanningHistory();
        }

        private static Color LoadCanvasBackgroundColor()
        {
            string value = EditorPrefs.GetString(
                CanvasBackgroundPreferenceKey,
                string.Empty);
            return !string.IsNullOrEmpty(value) &&
                   ColorUtility.TryParseHtmlString(
                       value,
                       out Color color)
                ? color
                : new Color(.11f, .13f, .17f);
        }

        private void SaveCanvasBackgroundColor()
        {
            EditorPrefs.SetString(
                CanvasBackgroundPreferenceKey,
                ColorUtility.ToHtmlStringRGBA(
                    canvasBackgroundColor));
        }

        private void ResetCanvasBackground()
        {
            canvasBackgroundColor = new Color(.11f, .13f, .17f);
            canvasBackgroundField?.SetValueWithoutNotify(
                canvasBackgroundColor);
            canvas?.SetBackgroundColor(canvasBackgroundColor);
            SaveCanvasBackgroundColor();
        }

        private void SaveDocument()
        {
            if (document == null)
            {
                return;
            }

            EditorPrefs.SetString(
                PreferenceKey,
                JsonUtility.ToJson(document, true));
            if (!string.IsNullOrEmpty(currentSavePath) &&
                File.Exists(currentSavePath))
            {
                PlanningSaveStore.Save(
                    currentSavePath,
                    document);
            }
        }

        private void ToggleSaveOverlay()
        {
            if (saveOverlay == null)
            {
                return;
            }

            bool show =
                saveOverlay.style.display.value == DisplayStyle.None;
            saveOverlay.style.display = show
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            if (show)
            {
                RefreshSaveSlots();
                saveOverlayStatus.text =
                    $"存档目录：{PlanningSaveStore.SaveFolder}";
            }
        }

        private void LoadSampleDocument()
        {
            currentSavePath = string.Empty;
            EditorPrefs.DeleteKey(CurrentSavePreferenceKey);
            document = PlanningDocument.CreateDefault();
            canvas.SetDocument(document);
            SaveDocument();
            ResetPlanningHistory();
            RefreshAll();
            saveOverlay.style.display = DisplayStyle.None;
            statusLabel.text = "已读取内置示例地图。";
        }

        private void RefreshSaveSlots()
        {
            if (saveSlotList == null)
            {
                return;
            }

            saveSlotList.Clear();
            saveSlotList.Add(CreateDefaultSaveRow());
            List<PlanningSaveInfo> saves = PlanningSaveStore.LoadAll();
            if (saves.Count == 0)
            {
                Label empty = new Label(
                    "还没有自定义存档。填写名字后新建空存档，" +
                    "或导入一个 JSON 文件。");
                empty.style.whiteSpace = WhiteSpace.Normal;
                empty.style.opacity = .65f;
                empty.style.paddingLeft = 6f;
                empty.style.paddingTop = 12f;
                saveSlotList.Add(empty);
                return;
            }

            for (int index = 0; index < saves.Count; index++)
            {
                saveSlotList.Add(CreateSaveSlotRow(saves[index]));
            }
        }

        private VisualElement CreateDefaultSaveRow()
        {
            VisualElement row = Row();
            row.style.alignItems = Align.Center;
            row.style.minHeight = 38f;
            row.style.paddingLeft = 6f;
            row.style.paddingRight = 6f;
            row.style.paddingTop = 4f;
            row.style.paddingBottom = 4f;
            row.style.marginBottom = 6f;
            row.style.backgroundColor =
                new Color(.1f, .14f, .2f);
            row.style.borderLeftWidth = 3f;
            row.style.borderLeftColor =
                new Color(.95f, .62f, .2f);
            Round(row, 4f);

            Label name = new Label("示例地图");
            name.style.flexGrow = 1f;
            name.style.unityTextAlign = TextAnchor.MiddleLeft;
            name.style.unityFontStyleAndWeight = FontStyle.Bold;
            row.Add(name);

            Label badge = new Label("默认 · 只读");
            badge.style.fontSize = 10f;
            badge.style.paddingLeft = 6f;
            badge.style.paddingRight = 6f;
            badge.style.paddingTop = 2f;
            badge.style.paddingBottom = 2f;
            badge.style.marginRight = 4f;
            badge.style.backgroundColor =
                new Color(.25f, .2f, .12f, .9f);
            badge.style.color = new Color(1f, .78f, .42f);
            Round(badge, 3f);
            row.Add(badge);

            row.Add(SaveSlotButton(
                "读取",
                LoadSampleDocument));
            row.Add(SaveSlotButton(
                "导出",
                ExportSampleMap));
            return row;
        }

        private VisualElement CreateSaveSlotRow(PlanningSaveInfo save)
        {
            VisualElement row = Row();
            row.style.alignItems = Align.Center;
            row.style.minHeight = 38f;
            row.style.paddingLeft = 6f;
            row.style.paddingRight = 6f;
            row.style.paddingTop = 4f;
            row.style.paddingBottom = 4f;
            row.style.marginBottom = 4f;
            row.style.backgroundColor = new Color(.1f, .12f, .16f);
            row.style.borderLeftWidth = 3f;
            row.style.borderLeftColor =
                string.Equals(
                    save.Path,
                    currentSavePath,
                    System.StringComparison.OrdinalIgnoreCase)
                    ? new Color(.28f, .62f, .9f)
                    : new Color(.23f, .27f, .34f);
            Round(row, 4f);

            VisualElement nameHost = new VisualElement();
            nameHost.style.flexGrow = 1f;
            nameHost.style.minWidth = 120f;
            nameHost.style.flexDirection = FlexDirection.Row;
            nameHost.style.alignItems = Align.Center;
            Label name = new Label(save.Name);
            name.style.flexGrow = 1f;
            name.style.unityTextAlign = TextAnchor.MiddleLeft;
            name.tooltip = save.Path;
            nameHost.Add(name);

            TextField renameField = new TextField
            {
                value = save.Name,
                isDelayed = true
            };
            renameField.style.display = DisplayStyle.None;
            renameField.style.flexGrow = 1f;
            renameField.style.minWidth = 120f;
            nameHost.Add(renameField);
            row.Add(nameHost);

            bool renaming = false;
            void BeginRename()
            {
                renaming = true;
                name.style.display = DisplayStyle.None;
                renameField.style.display = DisplayStyle.Flex;
                renameField.value = save.Name;
                renameField.schedule.Execute(() =>
                {
                    renameField.Focus();
                    renameField.SelectAll();
                }).StartingIn(0);
            }

            void CancelRename()
            {
                renaming = false;
                renameField.style.display = DisplayStyle.None;
                name.style.display = DisplayStyle.Flex;
            }

            void CommitRename()
            {
                if (!renaming)
                {
                    return;
                }

                renaming = false;
                try
                {
                    string oldPath = save.Path;
                    string renamedPath = PlanningSaveStore.Rename(
                        oldPath,
                        renameField.value);
                    if (string.Equals(
                            currentSavePath,
                            oldPath,
                            System.StringComparison.OrdinalIgnoreCase))
                    {
                        currentSavePath = renamedPath;
                        document.name =
                            Path.GetFileNameWithoutExtension(renamedPath);
                        mapNameField?.SetValueWithoutNotify(document.name);
                        SaveDocument();
                    }

                    saveOverlayStatus.text =
                        $"已重命名为“{Path.GetFileNameWithoutExtension(renamedPath)}”。";
                    RefreshSaveSlots();
                }
                catch (System.Exception exception)
                {
                    CancelRename();
                    ShowSavePanelError("重命名失败", exception);
                }
            }

            renameField.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return ||
                    evt.keyCode == KeyCode.KeypadEnter)
                {
                    CommitRename();
                    evt.StopPropagation();
                }
                else if (evt.keyCode == KeyCode.Escape)
                {
                    CancelRename();
                    evt.StopPropagation();
                }
            });
            renameField.RegisterCallback<FocusOutEvent>(_ =>
                CommitRename());

            row.Add(SaveSlotButton(
                "读取",
                () => LoadSaveSlot(save)));
            row.Add(SaveSlotButton(
                "保存",
                () => SaveCurrentToSlot(save)));
            row.Add(SaveSlotButton(
                "重命名",
                BeginRename));
            row.Add(SaveSlotButton(
                "导出",
                () => ExportSaveSlot(save)));
            row.Add(SaveSlotButton(
                "删除",
                () => DeleteSaveSlot(save),
                true));
            return row;
        }

        private void CreateBlankSave()
        {
            try
            {
                var blank = new PlanningDocument
                {
                    name = saveNameField != null
                        ? saveNameField.value
                        : string.Empty
                };
                blank.Normalize();
                currentSavePath = PlanningSaveStore.Create(blank);
                document = blank;
                canvas.SetDocument(document);
                SaveDocument();
                ResetPlanningHistory();
                RefreshAll();
                saveNameField?.SetValueWithoutNotify(string.Empty);
                saveOverlay.style.display = DisplayStyle.None;
                statusLabel.text =
                    $"已新建地图存档“{document.name}”。";
            }
            catch (System.Exception exception)
            {
                ShowSavePanelError("新建存档失败", exception);
            }
        }

        private void SaveCurrentAsNewSlot()
        {
            if (document == null)
            {
                return;
            }

            try
            {
                PlanningDocument copy =
                    JsonUtility.FromJson<PlanningDocument>(
                        JsonUtility.ToJson(document));
                copy.name = saveNameField != null &&
                            !string.IsNullOrWhiteSpace(
                                saveNameField.value)
                    ? saveNameField.value
                    : document.name;
                currentSavePath = PlanningSaveStore.Create(copy);
                document = copy;
                canvas.SetDocument(document);
                SaveDocument();
                ResetPlanningHistory();
                RefreshAll();
                saveNameField?.SetValueWithoutNotify(string.Empty);
                saveOverlay.style.display = DisplayStyle.None;
                statusLabel.text =
                    $"当前地图已另存为“{document.name}”。";
            }
            catch (System.Exception exception)
            {
                ShowSavePanelError("另存为失败", exception);
            }
        }

        private void ImportJsonAsNewSave()
        {
            string path = EditorUtility.OpenFilePanel(
                "导入 JSON 为新存档",
                Application.dataPath,
                "json");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            try
            {
                PlanningDocument imported = PlanningSaveStore.Read(path);
                if (saveNameField != null &&
                    !string.IsNullOrWhiteSpace(saveNameField.value))
                {
                    imported.name = saveNameField.value;
                }

                currentSavePath = PlanningSaveStore.Create(imported);
                document = imported;
                canvas.SetDocument(document);
                SaveDocument();
                ResetPlanningHistory();
                RefreshAll();
                saveNameField?.SetValueWithoutNotify(string.Empty);
                saveOverlay.style.display = DisplayStyle.None;
                statusLabel.text =
                    $"已导入并保存为“{document.name}”。";
            }
            catch (System.Exception exception)
            {
                ShowSavePanelError("导入失败", exception);
            }
        }

        private void LoadSaveSlot(PlanningSaveInfo save)
        {
            try
            {
                SaveDocument();
                document = PlanningSaveStore.Read(save.Path);
                currentSavePath = save.Path;
                canvas.SetDocument(document);
                SaveDocument();
                ResetPlanningHistory();
                RefreshAll();
                saveOverlay.style.display = DisplayStyle.None;
                statusLabel.text =
                    $"已读取地图存档“{document.name}”。";
            }
            catch (System.Exception exception)
            {
                ShowSavePanelError("读取失败", exception);
            }
        }

        private void SaveCurrentToSlot(PlanningSaveInfo save)
        {
            if (document == null ||
                !EditorUtility.DisplayDialog(
                    "保存地图存档",
                    $"用当前地图覆盖“{save.Name}”？",
                    "保存",
                    "取消"))
            {
                return;
            }

            try
            {
                document.name = save.Name;
                currentSavePath = save.Path;
                SaveDocument();
                mapNameField?.SetValueWithoutNotify(document.name);
                saveOverlayStatus.text =
                    $"已保存“{document.name}”。";
                RefreshSaveSlots();
            }
            catch (System.Exception exception)
            {
                ShowSavePanelError("保存失败", exception);
            }
        }

        private void ExportSaveSlot(PlanningSaveInfo save)
        {
            try
            {
                ExportDocumentToJson(
                    PlanningSaveStore.Read(save.Path));
            }
            catch (System.Exception exception)
            {
                ShowSavePanelError("导出失败", exception);
            }
        }

        private void ExportSampleMap()
        {
            ExportDocumentToJson(
                PlanningDocument.CreateDefault());
        }

        private void ExportDocumentToJson(
            PlanningDocument exported)
        {
            try
            {
                string path = EditorUtility.SaveFilePanel(
                    "导出地图存档",
                    Application.dataPath,
                    PlanningSaveStore.SanitizeFileName(
                        exported.name),
                    "json");
                if (string.IsNullOrEmpty(path))
                {
                    return;
                }

                exported.name =
                    Path.GetFileNameWithoutExtension(path);
                File.WriteAllText(
                    path,
                    JsonUtility.ToJson(exported, true));
                saveOverlayStatus.text = "已导出：" + path;
            }
            catch (System.Exception exception)
            {
                ShowSavePanelError("导出失败", exception);
            }
        }

        private void DeleteSaveSlot(PlanningSaveInfo save)
        {
            if (!EditorUtility.DisplayDialog(
                    "删除地图存档",
                    $"确定删除“{save.Name}”？\n" +
                    "当前画布内容不会一起删除。",
                    "删除",
                    "取消"))
            {
                return;
            }

            try
            {
                PlanningSaveStore.Delete(save.Path);
                if (string.Equals(
                        currentSavePath,
                        save.Path,
                        System.StringComparison.OrdinalIgnoreCase))
                {
                    currentSavePath = string.Empty;
                    EditorPrefs.DeleteKey(
                        CurrentSavePreferenceKey);
                }

                saveOverlayStatus.text =
                    $"已删除存档“{save.Name}”。";
                RefreshSaveSlots();
            }
            catch (System.Exception exception)
            {
                ShowSavePanelError("删除失败", exception);
            }
        }

        private static void ShowSavePanelError(
            string title,
            System.Exception exception)
        {
            EditorUtility.DisplayDialog(
                title,
                exception.Message,
                "确定");
        }

        private void SetMode(PlanningCanvasMode value)
        {
            mode = value;
            canvas?.SetMode(value);
            if (value == PlanningCanvasMode.Detail &&
                canvas != null &&
                !string.IsNullOrEmpty(canvas.SelectedRoomId))
            {
                canvas.FocusRoom(canvas.SelectedRoomId);
            }
            else if (value == PlanningCanvasMode.World)
            {
                canvas?.FocusWorld();
            }
            else if (value == PlanningCanvasMode.Assembly)
            {
                canvas?.FocusAssembly();
            }

            canvas?.schedule.Execute(FocusCurrentMode).StartingIn(0);

            RefreshModeVisibility();
            RefreshAll();
        }

        private void FocusCurrentMode()
        {
            if (canvas == null)
            {
                return;
            }

            if (mode == PlanningCanvasMode.World)
            {
                canvas.FocusWorld();
            }
            else if (mode == PlanningCanvasMode.Assembly)
            {
                canvas.FocusAssembly();
            }
            else if (!string.IsNullOrEmpty(canvas.SelectedRoomId))
            {
                canvas.FocusRoom(canvas.SelectedRoomId);
            }
        }

        private void RefreshModeVisibility()
        {
            if (worldMapTools != null)
            {
                worldMapTools.style.display =
                    mode == PlanningCanvasMode.World
                        ? DisplayStyle.Flex
                        : DisplayStyle.None;
            }

            if (blockEditorFoldout != null)
            {
                blockEditorFoldout.style.display =
                    mode == PlanningCanvasMode.World
                        ? DisplayStyle.None
                        : DisplayStyle.Flex;
            }

        }

        private void GenerateRoomToScene(PlanningRoom room)
        {
            if (room == null || room.boxes.Count == 0)
            {
                statusLabel.text = "这个房间还没有详情方块。";
                return;
            }

            if (PlanningSceneBuilder.HasGeneratedRoot(room) &&
                !EditorUtility.DisplayDialog(
                    "重新生成房间",
                    "当前场景里已经有这个房间的生成结果。" +
                    "重新生成会替换它，是否继续？",
                    "重新生成",
                    "取消"))
            {
                return;
            }

            if (PlanningSceneBuilder.TryBuildRoom(
                    room,
                    out string message))
            {
                statusLabel.text = message;
                return;
            }

            EditorUtility.DisplayDialog(
                "生成失败",
                message,
                "确定");
        }

        private void GenerateMapToScene()
        {
            if (document == null || document.rooms.Count == 0)
            {
                statusLabel.text = "世界图里还没有房间。";
                return;
            }

            if (PlanningSceneBuilder.HasAnyGeneratedRoot() &&
                !EditorUtility.DisplayDialog(
                    "重新生成整套场景",
                    "当前场景里已经有规划生成结果。" +
                    "重新生成会替换它，是否继续？",
                    "重新生成",
                    "取消"))
            {
                return;
            }

            if (PlanningSceneBuilder.TryBuildDocument(
                    document,
                    out string message))
            {
                statusLabel.text = message;
                return;
            }

            EditorUtility.DisplayDialog(
                "生成失败",
                message,
                "确定");
        }

        private void SyncSceneToAssembly()
        {
            if (canvas == null ||
                !canvas.SyncAssemblyFromScene())
            {
                statusLabel.text =
                    "没有可同步的场景房间容器或方块。";
                return;
            }

            int unassigned = canvas.LastUnassignedSceneBlockCount;
            statusLabel.text = unassigned > 0
                ? $"已同步；另有 {unassigned} 个场景方块未归属房间容器。"
                : "已从场景同步到装配图。";
        }

        private void RepairAndSyncCurrentMap()
        {
            if (document == null)
            {
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "修复同步",
                    "将清理失效通道，按端口重算所有通道，" +
                    "并以 Scene 房间容器覆盖对应房间的规划方块。" +
                    "确定继续？",
                    "确认修复",
                    "取消"))
            {
                return;
            }

            int removedConnectors = document.rooms.RemoveAll(
                item => item.isConnector &&
                        (document.FindRoom(item.fromRoomId) == null ||
                         document.FindRoom(item.toRoomId) == null));
            document.Normalize();
            document.RefreshConnectorPaths();
            bool synced = canvas != null &&
                          canvas.SyncAssemblyFromScene();
            SaveDocument();
            RefreshAll();
            int unassigned = canvas != null
                ? canvas.LastUnassignedSceneBlockCount
                : 0;
            statusLabel.text = synced
                ? $"修复同步完成，移除 {removedConnectors} 条失效通道；" +
                  $"未归属场景方块 {unassigned} 个。"
                : $"通道已重算，移除 {removedConnectors} 条失效通道；" +
                  $"未归属场景方块 {unassigned} 个。";
        }

        private void AddRoom()
        {
            canvas?.BeginRoomPlacement();
        }

        private void ResetPlanningHistory()
        {
            planningUndo.Clear();
            planningRedo.Clear();
            planningSnapshot = document != null
                ? JsonUtility.ToJson(document, false)
                : string.Empty;
        }

        private void RecordPlanningChange()
        {
            if (restoringPlanningDocument)
            {
                return;
            }

            if (!string.IsNullOrEmpty(planningSnapshot))
            {
                planningUndo.Push(planningSnapshot);
            }

            planningSnapshot = JsonUtility.ToJson(document, false);
            planningRedo.Clear();
        }

        private void UndoPlanning()
        {
            if (planningUndo.Count == 0)
            {
                statusLabel.text = "没有可撤回的规划操作。";
                return;
            }

            planningRedo.Push(planningSnapshot);
            string snapshot = planningUndo.Pop();
            RestorePlanningSnapshot(snapshot);
        }

        private void RedoPlanning()
        {
            if (planningRedo.Count == 0)
            {
                statusLabel.text = "没有可重做的规划操作。";
                return;
            }

            planningUndo.Push(planningSnapshot);
            string snapshot = planningRedo.Pop();
            RestorePlanningSnapshot(snapshot);
        }

        private void RestorePlanningSnapshot(string snapshot)
        {
            if (string.IsNullOrEmpty(snapshot))
            {
                return;
            }

            restoringPlanningDocument = true;
            document = JsonUtility.FromJson<PlanningDocument>(snapshot);
            document ??= PlanningDocument.CreateDefault();
            document.Normalize();
            canvas.SetDocument(document);
            planningSnapshot = snapshot;
            SaveDocument();
            restoringPlanningDocument = false;
            RefreshAll();
        }

        private void OnCanvasDocumentChanged()
        {
            document.RefreshConnectorPaths();
            RecordPlanningChange();
            SaveDocument();
            RefreshAll();
        }

        private void OnCanvasSelectionChanged()
        {
            RefreshRoomList();
            RefreshConnectorList();
            RefreshInspector();
        }

        private void RefreshAll()
        {
            if (canvas == null)
            {
                return;
            }

            mapNameField?.SetValueWithoutNotify(document.name);
            if (!worldBlockSettingsUnlocked)
            {
                worldBlockWidthField?.SetValueWithoutNotify(
                    document.worldBlockCellWidth);
                worldBlockHeightField?.SetValueWithoutNotify(
                    document.worldBlockCellHeight);
            }
            RefreshRoomList();
            RefreshConnectorList();
            RefreshInspector();
            RefreshToolStyles();
            if (statusLabel != null)
            {
                statusLabel.text =
                    $"{document.rooms.Count} 房间 · " +
                    $"{document.doors.Count} 门 · " +
                    $"{document.keys.Count} 钥匙 · " +
                    $"缩放 {Mathf.RoundToInt(canvas.Zoom * 100f)}%";
            }

            canvas.MarkDirtyRepaint();
        }

        private void ToggleWorldBlockUnlock()
        {
            if (document == null)
            {
                return;
            }

            worldBlockSettingsUnlocked = !worldBlockSettingsUnlocked;
            worldBlockWidthField?.SetEnabled(
                worldBlockSettingsUnlocked);
            worldBlockHeightField?.SetEnabled(
                worldBlockSettingsUnlocked);
            worldBlockUnlockButton.text =
                worldBlockSettingsUnlocked ? "锁定" : "解锁修改";
            worldBlockApplyButton?.SetEnabled(
                worldBlockSettingsUnlocked);
            statusLabel.text = worldBlockSettingsUnlocked
                ? "已解锁世界图区块尺寸，请谨慎修改。"
                : "世界图区块尺寸已锁定。";
        }

        private void LockWorldBlockSettings()
        {
            worldBlockSettingsUnlocked = false;
            if (document != null)
            {
                worldBlockWidthField?.SetValueWithoutNotify(
                    document.worldBlockCellWidth);
                worldBlockHeightField?.SetValueWithoutNotify(
                    document.worldBlockCellHeight);
            }

            worldBlockWidthField?.SetEnabled(false);
            worldBlockHeightField?.SetEnabled(false);
            worldBlockApplyButton?.SetEnabled(false);
            if (worldBlockUnlockButton != null)
            {
                worldBlockUnlockButton.text = "解锁修改";
            }
        }

        private void ApplyWorldBlockSettings()
        {
            if (document == null)
            {
                return;
            }

            if (!worldBlockSettingsUnlocked)
            {
                statusLabel.text = "请先解锁世界图区块尺寸。";
                return;
            }

            int width = Mathf.Clamp(
                worldBlockWidthField?.value ?? 16,
                1,
                64);
            int height = Mathf.Clamp(
                worldBlockHeightField?.value ?? 16,
                1,
                64);
            if (width == document.worldBlockCellWidth &&
                height == document.worldBlockCellHeight)
            {
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "重算世界图区块尺寸",
                    "修改区块尺寸会导致地图比例、房间边界、" +
                    "通道边界和生成结果整体重算。" +
                    "可能产生重大错误，确定继续？",
                    "确认重算",
                    "取消"))
            {
                worldBlockWidthField?.SetValueWithoutNotify(
                    document.worldBlockCellWidth);
                worldBlockHeightField?.SetValueWithoutNotify(
                    document.worldBlockCellHeight);
                return;
            }

            document.worldBlockCellWidth = width;
            document.worldBlockCellHeight = height;
            RecordPlanningChange();
            SaveDocument();
            RefreshAll();
            statusLabel.text =
                $"世界图区块已重算：{width} × {height}";
            LockWorldBlockSettings();
        }

        private void RefreshLevelContext()
        {
            if (playerContextField == null || roomParentField == null)
            {
                return;
            }

            refreshingLevelContext = true;
            GameObject player = embeddedLevelEditor?.Player;
            playerContextField.SetValueWithoutNotify(player);

            PlanningRoom room = document?.FindRoom(canvas?.SelectedRoomId);
            GameObject roomParent = ResolveRoomParent(room);
            roomParentField.SetValueWithoutNotify(roomParent);
            embeddedLevelEditor?.SetGenerationParent(roomParent);
            refreshingLevelContext = false;
            RefreshLevelContextStatus();
        }

        private void SyncPlanningCanvasPalette()
        {
            if (canvas == null)
            {
                return;
            }

            LevelEditorPalette palette =
                LevelEditorState.Palette ??
                LevelEditorPaletteService.GetOrCreate();
            canvas.SetPalette(palette);
            if (palette == null)
            {
                return;
            }

            int propIndex = LevelEditorState.SelectedPropIndex;
            if (propIndex >= 0 &&
                propIndex < palette.PropEntries.Count)
            {
                LevelEditorPropEntry prop =
                    palette.PropEntries[propIndex];
                if (prop != null)
                {
                    canvas.SetDetailProp(
                        prop.EntryId,
                        prop.DisplayName);
                    return;
                }
            }

            if (palette.Entries.Count == 0)
            {
                return;
            }

            int index = Mathf.Clamp(
                LevelEditorState.SelectedEntryIndex,
                0,
                palette.Entries.Count - 1);
            LevelEditorBlockEntry entry = palette.Entries[index];
            if (entry != null)
            {
                canvas.SetDetailEntry(
                    entry.EntryId,
                    entry.DisplayName);
            }
        }

        private void RefreshLevelContextStatus()
        {
            if (contextStatusLabel == null)
            {
                return;
            }

            GameObject player = embeddedLevelEditor?.Player;
            PlanningRoom room = document?.FindRoom(canvas?.SelectedRoomId);
            GameObject roomParent = ResolveRoomParent(room);
            string playerText = player != null
                ? "玩家：" + player.name
                : "玩家：未选择";
            string parentText = roomParent != null
                ? "房间父物体：" + roomParent.name
                : "房间父物体：未选择";
            contextStatusLabel.text = playerText + "  ·  " + parentText;
        }

        private void SetCurrentRoomParent(GameObject value)
        {
            PlanningRoom room = document?.FindRoom(canvas?.SelectedRoomId);
            if (room == null)
            {
                return;
            }

            if (value != null && !value.scene.IsValid())
            {
                value = null;
            }

            room.roomParentReference = value != null
                ? GlobalObjectId.GetGlobalObjectIdSlow(value).ToString()
                : string.Empty;
            embeddedLevelEditor?.SetGenerationParent(value);
            SaveDocument();
            RefreshLevelContext();
        }

        private GameObject EnsureCurrentRoomContainer(bool focus)
        {
            PlanningRoom room = document?.FindRoom(canvas?.SelectedRoomId);
            if (room == null)
            {
                statusLabel.text = "请先在世界图选择一个房间。";
                return null;
            }

            GameObject parent = ResolveRoomParent(room);
            if (parent == null)
            {
                Scene scene = SceneManager.GetActiveScene();
                if (!scene.IsValid())
                {
                    statusLabel.text = "当前没有可用场景。";
                    return null;
                }

                parent = new GameObject(
                    $"房间容器_{room.name}_{room.id}");
                Undo.RegisterCreatedObjectUndo(
                    parent,
                    "创建房间场景容器");
                SceneManager.MoveGameObjectToScene(parent, scene);
                parent.transform.position = Vector3.zero;
                room.roomParentReference =
                    GlobalObjectId.GetGlobalObjectIdSlow(parent).ToString();
                EditorSceneManager.MarkSceneDirty(scene);
                SaveDocument();
            }

            LevelEditorState.GenerationParent = parent;
            embeddedLevelEditor?.SetGenerationParent(parent);
            RefreshLevelContext();

            if (focus)
            {
                Selection.activeGameObject = parent;
                SceneView view = SceneView.lastActiveSceneView;
                view?.FrameSelected();
                view?.Focus();
            }

            return parent;
        }

        private static GameObject ResolveRoomParent(PlanningRoom room)
        {
            if (room == null ||
                string.IsNullOrEmpty(room.roomParentReference) ||
                !GlobalObjectId.TryParse(
                    room.roomParentReference,
                    out GlobalObjectId globalId))
            {
                return null;
            }

            return GlobalObjectId
                .GlobalObjectIdentifierToObjectSlow(globalId) as GameObject;
        }

        private void RefreshRoomList()
        {
            roomList.Clear();
            for (int index = 0; index < document.rooms.Count; index++)
            {
                PlanningRoom room = document.rooms[index];
                if (room.isConnector)
                {
                    continue;
                }

                Button button = new Button(() =>
                {
                    canvas.SelectRoom(room.id, true);
                    RefreshInspector();
                })
                {
                    text = room.name
                };
                button.style.unityTextAlign = TextAnchor.MiddleLeft;
                button.style.marginLeft = 6f;
                button.style.marginRight = 6f;
                button.style.marginBottom = 2f;
                button.style.height = 24f;
                bool selected = room.id == canvas.SelectedRoomId;
                button.style.backgroundColor = selected
                    ? new Color(.18f, .34f, .52f)
                    : new Color(.12f, .15f, .19f);
                button.style.borderLeftWidth = selected ? 3f : 0f;
                button.style.borderLeftColor =
                    new Color(.45f, .78f, 1f);
                button.style.unityFontStyleAndWeight = selected
                    ? FontStyle.Bold
                    : FontStyle.Normal;
                roomList.Add(button);
            }
        }

        private void RefreshConnectorList()
        {
            connectorList.Clear();
            int count = 0;
            for (int index = 0; index < document.rooms.Count; index++)
            {
                PlanningRoom connector = document.rooms[index];
                if (!connector.isConnector)
                {
                    continue;
                }

                count++;
                PlanningRoom from = document.FindRoom(
                    connector.fromRoomId);
                PlanningRoom to = document.FindRoom(
                    connector.toRoomId);
                string route = from != null && to != null
                    ? $"{from.name} → {to.name}"
                    : connector.name;
                connector.name = route;
                Button button = new Button(() =>
                {
                    canvas.SelectRoom(connector.id, true);
                    RefreshInspector();
                })
                {
                    text = route
                };
                button.style.unityTextAlign = TextAnchor.MiddleLeft;
                button.style.marginLeft = 6f;
                button.style.marginRight = 6f;
                button.style.marginBottom = 2f;
                button.style.height = 24f;
                bool selected = connector.id == canvas.SelectedRoomId;
                button.style.backgroundColor = selected
                    ? new Color(.16f, .43f, .42f)
                    : new Color(.12f, .15f, .19f);
                button.style.borderLeftWidth = selected ? 3f : 0f;
                button.style.borderLeftColor =
                    new Color(.3f, .9f, .82f);
                button.style.unityFontStyleAndWeight = selected
                    ? FontStyle.Bold
                    : FontStyle.Normal;
                connectorList.Add(button);
            }

            if (count == 0)
            {
                Label empty = new Label("使用“通道”工具从房间拖到房间");
                empty.style.opacity = .5f;
                empty.style.marginLeft = 6f;
                empty.style.whiteSpace = WhiteSpace.Normal;
                connectorList.Add(empty);
            }
        }

        private void RefreshInspector()
        {
            if (inspector == null)
            {
                return;
            }

            inspector.Clear();
            RefreshLevelContext();
            if (mode == PlanningCanvasMode.Detail &&
                canvas.SelectedRoomId == null)
            {
                inspector.Add(new Label("请先在世界图选择一个房间。"));
                return;
            }

            PlanningRoom room = document.FindRoom(canvas.SelectedRoomId);
            if (room == null)
            {
                inspector.Add(new Label("未选择房间。"));
                return;
            }

            if (!room.isConnector)
            {
                TextField nameField = new TextField("房间名")
                {
                    value = room.name,
                    isDelayed = true
                };
                nameField.RegisterValueChangedCallback(evt =>
                {
                    room.name = evt.newValue;
                    OnCanvasDocumentChanged();
                });
                inspector.Add(nameField);
            }
            else
            {
                PlanningRoom from = document.FindRoom(room.fromRoomId);
                PlanningRoom to = document.FindRoom(room.toRoomId);
                TextField routeName = new TextField("通道名")
                {
                    value =
                        (from != null ? from.name : "未知房间") +
                        " → " +
                        (to != null ? to.name : "未知房间"),
                    isReadOnly = true
                };
                inspector.Add(routeName);

                IntegerField widthField = new IntegerField("通道宽度")
                {
                    value = room.connectorWidth,
                    isDelayed = true
                };
                widthField.RegisterValueChangedCallback(evt =>
                {
                    room.connectorWidth = Mathf.Clamp(
                        evt.newValue,
                        1,
                        12);
                    OnCanvasDocumentChanged();
                });
                inspector.Add(widthField);
            }

            inspector.Add(new Label(
                $"世界图占格：{room.cells.Count}  " +
                $"详情方块：{room.boxes.Count}"));

            inspector.Add(TopButton(
                mode == PlanningCanvasMode.Detail
                    ? room.isConnector
                        ? "重新聚焦通道布局"
                        : "重新聚焦房间布局"
                    : room.isConnector
                        ? "进入通道详情"
                        : "进入房间详情",
                () =>
            {
                canvas.SelectRoom(room.id);
                SetMode(PlanningCanvasMode.Detail);
            }));

            Foldout generationFoldout = new Foldout
            {
                text = "生成到场景",
                value = false
            };
            Button generateButton = TopButton(
                room.isConnector
                    ? "生成当前通道到场景"
                    : "生成当前房间到场景",
                () => GenerateRoomToScene(room));
            generateButton.SetEnabled(room.boxes.Count > 0);
            generationFoldout.Add(generateButton);
            inspector.Add(generationFoldout);

            Foldout roomActionsFoldout = new Foldout
            {
                text = room.isConnector
                    ? "更多通道操作"
                    : "更多房间操作",
                value = true
            };
            roomActionsFoldout.Add(TopButton(
                room.isConnector ? "聚焦通道" : "聚焦房间",
                () =>
                canvas.FocusRoom(room.id)));
            roomActionsFoldout.Add(TopButton(
                room.isConnector ? "删除通道" : "删除房间",
                () =>
            {
                if (!EditorUtility.DisplayDialog(
                        room.isConnector
                            ? "删除通道"
                            : "删除房间",
                        room.isConnector
                            ? "确定删除这个连接通道？"
                            : "确定删除这个房间？",
                        "删除",
                        "取消"))
                {
                    return;
                }

                document.rooms.Remove(room);
                document.rooms.RemoveAll(
                    item => item.isConnector &&
                            (item.fromRoomId == room.id ||
                             item.toRoomId == room.id));
                document.doors.RemoveAll(
                    door => door.roomId == room.id);
                document.keys.RemoveAll(
                    key => key.roomId == room.id);
                PlanningRoom next = document.rooms.Find(
                    item => !item.isConnector);
                canvas.SelectRoom(
                    next != null ? next.id : null);
                OnCanvasDocumentChanged();
            }));
            inspector.Add(roomActionsFoldout);
        }

        private void RefreshToolStyles()
        {
            foreach (var pair in mapToolButtons)
            {
                pair.Value.style.backgroundColor =
                    pair.Key == canvas.MapTool
                        ? new Color(.18f, .34f, .52f)
                        : new Color(.12f, .15f, .19f);
            }

            foreach (var pair in modeButtons)
            {
                bool selected = pair.Key == mode;
                pair.Value.style.backgroundColor = selected
                    ? new Color(.16f, .43f, .82f)
                    : new Color(.12f, .15f, .19f);
                pair.Value.style.color = selected
                    ? Color.white
                    : new Color(.78f, .82f, .9f);
                pair.Value.style.borderBottomWidth =
                    selected ? 3f : 0f;
                pair.Value.style.borderBottomColor =
                    new Color(.45f, .78f, 1f);
                pair.Value.style.unityFontStyleAndWeight = selected
                    ? FontStyle.Bold
                    : FontStyle.Normal;
            }

        }

        private void AddMapToolButton(
            VisualElement parent,
            PlanningMapTool tool,
            string text)
        {
            Button button = ToolButton(
                text,
                () =>
                {
                    canvas.SetMapTool(tool);
                    RefreshToolStyles();
                });
            parent.Add(button);
            mapToolButtons[tool] = button;
        }

        private static VisualElement ScrollList(float height)
        {
            ScrollView scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.height = height;
            return scroll;
        }

        private static VisualElement SectionTitle(string text)
        {
            Label label = new Label(text);
            label.style.fontSize = 11f;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.opacity = .8f;
            label.style.paddingLeft = 7f;
            label.style.paddingTop = 8f;
            label.style.paddingBottom = 4f;
            return label;
        }

        private static VisualElement ToolGroupTitle(string text)
        {
            Label label = new Label(text);
            label.style.fontSize = 10f;
            label.style.opacity = .58f;
            label.style.marginLeft = 2f;
            label.style.marginTop = 4f;
            label.style.marginBottom = 2f;
            return label;
        }

        private static VisualElement Separator()
        {
            VisualElement line = new VisualElement();
            line.style.height = 1f;
            line.style.marginLeft = 6f;
            line.style.marginRight = 6f;
            line.style.marginTop = 8f;
            line.style.marginBottom = 4f;
            line.style.backgroundColor = new Color(.28f, .32f, .4f);
            return line;
        }

        private static VisualElement ContextSeparator()
        {
            VisualElement line = new VisualElement();
            line.style.width = 1f;
            line.style.height = 24f;
            line.style.marginLeft = 6f;
            line.style.marginRight = 6f;
            line.style.backgroundColor = new Color(.24f, .28f, .36f);
            return line;
        }

        private static Button TopButton(
            string text,
            System.Action action)
        {
            Button button = new Button(action)
            {
                text = text
            };
            button.style.height = 28f;
            button.style.marginLeft = 2f;
            button.style.marginRight = 2f;
            button.style.marginTop = 2f;
            button.style.marginBottom = 2f;
            button.style.paddingLeft = 8f;
            button.style.paddingRight = 8f;
            button.style.unityTextAlign = TextAnchor.MiddleCenter;
            return button;
        }

        private static Button SaveSlotButton(
            string text,
            System.Action action,
            bool danger = false)
        {
            Button button = new Button(action)
            {
                text = text
            };
            button.style.height = 24f;
            button.style.marginLeft = 2f;
            button.style.marginRight = 2f;
            button.style.paddingLeft = 8f;
            button.style.paddingRight = 8f;
            button.style.fontSize = 11f;
            button.style.unityTextAlign = TextAnchor.MiddleCenter;
            if (danger)
            {
                button.style.backgroundColor =
                    new Color(.62f, .18f, .16f);
                button.style.color = Color.white;
            }

            return button;
        }

        private static Button SmallRowButton(
            string text,
            System.Action action)
        {
            Button button = new Button(action)
            {
                text = text
            };
            button.style.height = 24f;
            button.style.marginLeft = 6f;
            button.style.marginRight = 6f;
            button.style.marginTop = 3f;
            return button;
        }

        private static Button ToolButton(
            string text,
            System.Action action)
        {
            Button button = new Button(action)
            {
                text = text
            };
            button.style.flexGrow = 1f;
            button.style.flexBasis = 0f;
            button.style.flexShrink = 1f;
            button.style.minWidth = 0f;
            button.style.maxWidth = Length.Percent(100f);
            button.style.height = 28f;
            button.style.marginLeft = 2f;
            button.style.marginRight = 2f;
            button.style.marginTop = 2f;
            button.style.marginBottom = 2f;
            button.style.fontSize = 11f;
            button.style.unityTextAlign = TextAnchor.MiddleCenter;
            return button;
        }

        private static VisualElement ToolRow()
        {
            VisualElement row = Row();
            row.style.width = Length.Percent(100f);
            row.style.flexShrink = 0f;
            row.style.alignItems = Align.Center;
            return row;
        }

        private static VisualElement Row()
        {
            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            return row;
        }

        private static VisualElement Spacer()
        {
            VisualElement spacer = new VisualElement();
            spacer.style.flexGrow = 1f;
            return spacer;
        }

        private static void Round(
            VisualElement element,
            float radius)
        {
            element.style.borderTopLeftRadius = radius;
            element.style.borderTopRightRadius = radius;
            element.style.borderBottomLeftRadius = radius;
            element.style.borderBottomRightRadius = radius;
        }

        private static Color ParseColor(string value, Color fallback)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   ColorUtility.TryParseHtmlString(value, out Color color)
                ? color
                : fallback;
        }
    }
}
