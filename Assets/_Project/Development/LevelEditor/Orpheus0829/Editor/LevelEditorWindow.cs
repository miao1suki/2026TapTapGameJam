using System.Collections.Generic;
using System.Text;
using Project.BlockFeatures.Editor;
using Project.ColorBlocks;
using Project.SurfaceTiles;
using Project.SurfaceTiles.Editor;
using Unity.Profiling;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Project.LevelEditor.Editor
{
    internal sealed class LevelEditorWindow : EditorWindow
    {
        private static readonly ProfilerMarker TickMarker = new ProfilerMarker("LevelEditor.Tick");
        private LevelEditorPanel panel;
        private double nextRefreshTime;

        internal static void Open()
        {
            LevelEditorWindow window = GetWindow<LevelEditorWindow>();
            window.titleContent = new GUIContent("方块/道具放置器");
            window.minSize = new Vector2(380f, 520f);
            window.Show();
            window.Focus();
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            LevelEditorTypography.Apply(root);
            root.style.paddingLeft = 10f;
            root.style.paddingRight = 10f;
            root.style.paddingTop = 8f;
            root.style.paddingBottom = 8f;
            panel = new LevelEditorPanel(root);
            root.SetEnabled(!EditorApplication.isPlayingOrWillChangePlaymode);
        }

        private void OnEnable()
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                SuspendAuthoring();
            }
            else
            {
                rootVisualElement.SetEnabled(true);
            }
        }

        private void OnDisable()
        {
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            if (!LevelEditorState.EditMode)
            {
                return;
            }

            LevelEditorState.EditMode = false;
            SurfaceTileEditorBridge.SetPainting(false);
            LevelEditorViewLock.Exit();
            SceneView.RepaintAll();
        }

        private void Tick()
        {
            // This window is an authoring tool, not a runtime inspector. Hidden
            // tabs still receive EditorApplication.update during Play Mode.
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            using var tick = TickMarker.Auto();
            double now = EditorApplication.timeSinceStartup;
            if (panel == null || now < nextRefreshTime)
            {
                return;
            }

            long tickStart =
                Stopwatch.GetTimestamp();
            nextRefreshTime =
                now +
                (EditorAuthoringPerformanceState
                    .ReducedRefreshEnabled
                    ? hasFocus
                        ? .35d
                        : .75d
                    : .1d);
            panel.Refresh();
            panel.RefreshPendingPreviews();
            if (LevelEditorPlayerService.EnforceEditPlane())
            {
                SceneView.RepaintAll();
            }
            EditorAuthoringPerformanceState
                .RecordLevelEditorTick(
                    EditorAuthoringPerformanceState
                        .ElapsedMilliseconds(
                            tickStart));
        }

        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode ||
                state == PlayModeStateChange.EnteredPlayMode)
            {
                SuspendAuthoring();
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                rootVisualElement.SetEnabled(true);
                nextRefreshTime = 0d;
            }
        }

        private void SuspendAuthoring()
        {
            rootVisualElement.SetEnabled(false);
            if (LevelEditorState.EditMode)
            {
                LevelEditorState.EditMode = false;
                SurfaceTileEditorBridge.SetPainting(false);
                LevelEditorViewLock.Exit();
                SceneView.RepaintAll();
            }
        }
    }

    internal sealed class LevelEditorPanel
    {
        private static readonly Dictionary<int, Texture2D> PrefabPreviewCache =
            new Dictionary<int, Texture2D>();
        private static readonly Dictionary<string, Texture2D> ColorPreviewCache =
            new Dictionary<string, Texture2D>();
        private static readonly Dictionary<string, Texture2D>
            DecorationPreviewCache =
                new Dictionary<string, Texture2D>();

        private readonly VisualElement panel;
        private readonly VisualElement body;
        private readonly bool includeContextSections;
        private readonly Dictionary<Image, GameObject> pendingPropPreviews =
            new Dictionary<Image, GameObject>();
        private readonly Button editModeButton;
        private readonly FloatField gridSizeField;
        private readonly Button selectButton;
        private readonly Button mergeButton;
        private readonly Button mergeModeButton;
        private readonly Button parametersButton;
        private readonly VisualElement mergeActions;
        internal System.Action MergePlanningSelection;
        internal System.Action UnmergePlanningSelection;
        internal System.Action EditPlanningDefaults;
        private readonly Label mergeStatus;
        private readonly Button paintButton;
        private readonly Button eraseButton;
        private readonly Button playerToolButton;
        private readonly VisualElement paletteList;
        private readonly Label paletteScaleLabel;
        private readonly VisualElement propList;
        private ScrollView paletteScroll;
        private ScrollView propScroll;
        private ObjectField playerField;
        private Toggle playerPlaneLockToggle;
        private Label playerStatusLabel;
        private int lastPaletteCount = -1;
        private int lastSelectedIndex = -2;
        private int lastPaletteRevision = -1;
        private int lastPropCount = -1;
        private int lastPropIndex = -2;
        private int lastPropRevision = -1;
        private bool refreshing;

        internal LevelEditorPanel(
            VisualElement root,
            bool includeContextSections = true)
        {
            this.includeContextSections = includeContextSections;
            LevelEditorState.Palette =
                LevelEditorPaletteService.GetOrCreate();
            LevelEditorState.CellSize = 1f;
            LevelEditorState.Tool = LevelEditorTool.Select;
            LevelEditorState.SelectedPropIndex = -1;

            panel = new VisualElement { pickingMode = PickingMode.Position };
            panel.style.width = Length.Percent(100f);
            panel.style.flexGrow = 1f;
            float panelPadding = includeContextSections ? 12f : 2f;
            panel.style.paddingLeft = panelPadding;
            panel.style.paddingRight = panelPadding;
            panel.style.paddingTop = includeContextSections ? 10f : 4f;
            panel.style.paddingBottom = includeContextSections ? 10f : 4f;
            panel.style.backgroundColor = includeContextSections
                ? EditorGUIUtility.isProSkin
                    ? new Color(0.07f, 0.085f, 0.11f, 0.97f)
                    : new Color(0.95f, 0.96f, 0.98f, 0.98f)
                : Color.clear;
            if (includeContextSections)
            {
                Round(panel, 8f);
            }

            root.Add(panel);

            VisualElement header = Row();
            header.style.alignItems = Align.Center;
            if (!includeContextSections)
            {
                header.style.display = DisplayStyle.None;
            }

            VisualElement titles = new VisualElement();
            titles.style.flexGrow = 1f;
            Label title = new Label("方块/道具放置器");
            title.style.fontSize = 16f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            titles.Add(title);
            Label subtitle = new Label("2D 网格 · 方块栏目 · 栏目贴画");
            subtitle.style.fontSize = 11f;
            subtitle.style.opacity = .65f;
            titles.Add(subtitle);
            header.Add(titles);

            body = new ScrollView(ScrollViewMode.Vertical);
            body.style.flexGrow = 1f;
            Button collapse = SmallButton("▾", null);
            collapse.clicked += () =>
            {
                bool show = body.style.display.value == DisplayStyle.None;
                body.style.display = show
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
                collapse.text = show ? "▾" : "▸";
            };
            header.Add(collapse);
            panel.Add(header);
            panel.Add(body);

            editModeButton = new Button(() =>
            {
                SetEditMode(!LevelEditorState.EditMode);
            })
            {
                text = "进入编辑模式"
            };
            editModeButton.style.height = 34f;
            editModeButton.style.fontSize = 14f;
            editModeButton.style.unityFontStyleAndWeight = FontStyle.Bold;
            editModeButton.style.marginTop = 4f;
            editModeButton.style.marginBottom = 6f;
            body.Add(editModeButton);

            body.Add(new Button(() =>
            {
                SceneLevelEditorOverlay.Open();
            })
            {
                text = "场景美术",
                tooltip = "打开场景关卡编辑器，并在同一正交 SceneView 中切换到美术模式。"
            });

            VisualElement tools = Row();
            selectButton = ActionButton("选择", () =>
                SetTool(LevelEditorTool.Select));
            paintButton = ActionButton("绘制", () =>
                SetTool(LevelEditorTool.Paint));
            eraseButton = ActionButton("擦除", () =>
                SetTool(LevelEditorTool.Erase));
            playerToolButton = ActionButton("玩家", () =>
                SetTool(LevelEditorTool.Player));
            tools.Add(selectButton);
            tools.Add(paintButton);
            tools.Add(eraseButton);
            tools.Add(playerToolButton);
            body.Add(tools);

            VisualElement extraTools = Row();
            mergeModeButton = ActionButton("合并", () => SetTool(LevelEditorTool.Merge));
            parametersButton = ActionButton("参数", () => SetTool(LevelEditorTool.Parameters));
            extraTools.Add(mergeModeButton);
            extraTools.Add(parametersButton);
            extraTools.style.display=includeContextSections ? DisplayStyle.None : DisplayStyle.Flex;
            body.Add(extraTools);

            VisualElement mergeRow = Row();
            mergeActions = mergeRow;
            mergeButton = ActionButton("合并选中项", () =>
            {
                if (MergePlanningSelection != null)
                {
                    MergePlanningSelection();
                    return;
                }
                if (LevelEditorBlockFactory.MergeSelected(out string result))
                    SceneView.RepaintAll();
                mergeStatus.text = result;
            });
            mergeRow.Add(mergeButton);
            mergeButton.tooltip="同种物体组成完整矩形后合并为一个实例。整体使用最先放置物体的参数；解除合并恢复各自原始参数。";
            if (!includeContextSections)
                mergeRow.Add(ActionButton("解除合并", () => UnmergePlanningSelection?.Invoke()));
            body.Add(mergeRow);
            mergeStatus = new Label("合并：单击或框选增减选区，同类矩形可合并。参数：点物体编辑，点栏目设置默认值。");
            mergeStatus.style.whiteSpace = WhiteSpace.Normal;
            mergeStatus.style.opacity = .7f;
            body.Add(mergeStatus);

            gridSizeField = new FloatField("格子世界尺寸")
            {
                value = LevelEditorState.CellSize,
                isDelayed = true
            };
            gridSizeField.tooltip =
                "一个格子对应的 Unity 世界单位，默认 1。" +
                "不是世界图区块尺寸。";
            gridSizeField.RegisterValueChangedCallback(evt =>
            {
                if (!refreshing)
                {
                    LevelEditorState.CellSize =
                        Mathf.Clamp(evt.newValue, .1f, 8f);
                    SceneView.RepaintAll();
                }
            });
            body.Add(gridSizeField);

            if (includeContextSections)
            {
                Foldout playerSection = new Foldout
                {
                    text = "玩家参考",
                    value = true
                };
                playerField = new ObjectField("场景玩家")
                {
                    objectType = typeof(GameObject),
                    allowSceneObjects = true
                };
                playerField.RegisterValueChangedCallback(evt =>
                {
                    if (!refreshing)
                    {
                        SetPlayer(evt.newValue as GameObject);
                    }
                });
                playerSection.Add(playerField);

                VisualElement playerActions = Row();
                playerActions.Add(ActionButton(
                    "使用当前选择",
                    UseSelectedAsPlayer));
                playerSection.Add(playerActions);

                playerPlaneLockToggle = new Toggle("限制到 XY 平面")
                {
                    value = LevelEditorState.LockPlayerToPlane
                };
                playerPlaneLockToggle.RegisterValueChangedCallback(evt =>
                {
                    if (!refreshing)
                    {
                        LevelEditorState.LockPlayerToPlane = evt.newValue;
                        if (evt.newValue)
                        {
                            LevelEditorPlayerService.EnforceEditPlane();
                            SceneView.RepaintAll();
                        }
                    }
                });
                playerSection.Add(playerPlaneLockToggle);

                playerStatusLabel = new Label();
                playerStatusLabel.style.whiteSpace = WhiteSpace.Normal;
                playerStatusLabel.style.opacity = .8f;
                playerSection.Add(playerStatusLabel);
                body.Add(playerSection);

            }

            VisualElement sharedScaleRow = Row();
            Label sharedScaleTitle = new Label("栏目缩放");
            sharedScaleTitle.style.flexGrow = 1f;
            sharedScaleTitle.style.unityTextAlign = TextAnchor.MiddleLeft;
            sharedScaleTitle.style.fontSize = 11f;
            sharedScaleTitle.style.opacity = .75f;
            sharedScaleRow.Add(sharedScaleTitle);
            Button sharedMinus = SmallButton("−", () =>
                AdjustPaletteScale(-.1f));
            sharedMinus.style.width = 30f;
            sharedMinus.style.height = 24f;
            sharedMinus.style.marginLeft = 3f;
            sharedMinus.style.marginRight = 2f;
            sharedScaleRow.Add(sharedMinus);
            paletteScaleLabel = new Label();
            paletteScaleLabel.style.width = 42f;
            paletteScaleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            sharedScaleRow.Add(paletteScaleLabel);
            Button sharedPlus = SmallButton("+", () =>
                AdjustPaletteScale(.1f));
            sharedPlus.style.width = 30f;
            sharedPlus.style.height = 24f;
            sharedPlus.style.marginLeft = 2f;
            sharedPlus.style.marginRight = 3f;
            sharedScaleRow.Add(sharedPlus);
            sharedScaleRow.style.marginTop = 2f;
            sharedScaleRow.style.marginBottom = 5f;
            body.Add(sharedScaleRow);

            Foldout paletteSection = new Foldout
            {
                text = "方块栏目",
                value = true
            };
            paletteList = new VisualElement();
            paletteList.style.width = Length.Percent(100f);
            paletteList.style.flexDirection = FlexDirection.Column;
            paletteList.style.paddingTop = 4f;
            paletteList.style.paddingBottom = 4f;

            paletteScroll = new ScrollView();
            paletteScroll.name = "block-palette-scroll";
            paletteScroll.style.height = StyleKeyword.Auto;
            paletteScroll.style.maxHeight = includeContextSections ? 280f : 420f;
            paletteScroll.style.flexShrink = 0f;
            paletteList.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                paletteScroll.style.height = Mathf.Min(evt.newRect.height,
                    includeContextSections ? 280f : 420f);
            });
            paletteScroll.horizontalScrollerVisibility =
                ScrollerVisibility.Hidden;
            paletteScroll.Add(paletteList);
            paletteSection.Add(paletteScroll);
            body.Add(paletteSection);

            Foldout propSection = new Foldout
            {
                name = "prop-palette-section",
                text = "道具栏目",
                value = true
            };
            propList = new VisualElement();
            propList.style.width = Length.Percent(100f);
            propList.style.flexDirection = FlexDirection.Column;
            propScroll = new ScrollView();
            propScroll.style.height = 220f;
            propScroll.horizontalScrollerVisibility =
                ScrollerVisibility.Hidden;
            propScroll.Add(propList);
            propSection.Add(propScroll);
            body.Add(propSection);
        }

        internal void Refresh()
        {
            refreshing = true;
            editModeButton.SetEnabled(
                !EditorApplication.isPlayingOrWillChangePlaymode);
            editModeButton.text = LevelEditorState.EditMode
                ? "退出编辑模式（恢复 3D）"
                : "进入编辑模式";
            editModeButton.style.backgroundColor =
                LevelEditorState.EditMode
                    ? new Color(.72f, .33f, .12f, 1f)
                    : new Color(.16f, .43f, .82f, 1f);
            editModeButton.style.color = Color.white;
            gridSizeField.SetValueWithoutNotify(LevelEditorState.CellSize);
            GameObject player = LevelEditorState.Player;
            playerField?.SetValueWithoutNotify(player);
            playerPlaneLockToggle?.SetValueWithoutNotify(
                LevelEditorState.LockPlayerToPlane);
            playerPlaneLockToggle?.SetEnabled(player != null);
            playerToolButton.SetEnabled(player != null);
            if (playerStatusLabel != null)
            {
                playerStatusLabel.text = player == null
                    ? "未引用场景玩家。"
                    : $"已引用：{player.name} · " +
                      (LevelEditorState.LockPlayerToPlane
                          ? "编辑时锁定 Z=0"
                          : "未锁定编辑平面");
            }

            selectButton.EnableInClassList(
                "selected-tool",
                LevelEditorState.Tool == LevelEditorTool.Select);
            paintButton.EnableInClassList(
                "selected-tool",
                LevelEditorState.Tool == LevelEditorTool.Paint);
            eraseButton.EnableInClassList(
                "selected-tool",
                LevelEditorState.Tool == LevelEditorTool.Erase);
            StyleToolButton(
                selectButton,
                LevelEditorState.Tool == LevelEditorTool.Select);
            StyleToolButton(
                paintButton,
                LevelEditorState.Tool == LevelEditorTool.Paint);
            StyleToolButton(
                eraseButton,
                LevelEditorState.Tool == LevelEditorTool.Erase);
            StyleToolButton(
                playerToolButton,
                LevelEditorState.Tool == LevelEditorTool.Player);
            StyleToolButton(mergeModeButton, LevelEditorState.Tool == LevelEditorTool.Merge);
            StyleToolButton(parametersButton, LevelEditorState.Tool == LevelEditorTool.Parameters);
            mergeActions.style.display = includeContextSections || LevelEditorState.Tool == LevelEditorTool.Merge ? DisplayStyle.Flex : DisplayStyle.None;
            mergeButton.SetEnabled(MergePlanningSelection != null || LevelEditorBlockFactory.SelectedBlockCount >= 2);
            RefreshPalette();
            RefreshProps();
            paletteScaleLabel.text =
                $"{Mathf.RoundToInt(LevelEditorState.PaletteScale * 100f)}%";
            refreshing = false;
        }

        internal void RefreshPendingPreviews()
        {
            if (pendingPropPreviews.Count == 0)
            {
                return;
            }

            var completed = new List<Image>();
            foreach (KeyValuePair<Image, GameObject> pair in
                     pendingPropPreviews)
            {
                if (pair.Value == null || pair.Key.panel == null)
                {
                    completed.Add(pair.Key);
                    continue;
                }

                Texture2D preview = AssetPreview.GetAssetPreview(pair.Value);
                if (preview == null)
                {
                    continue;
                }

                pair.Key.image = preview;
                completed.Add(pair.Key);
            }

            foreach (Image image in completed)
            {
                pendingPropPreviews.Remove(image);
            }
        }

        private static void StyleToolButton(
            Button button,
            bool selected)
        {
            button.style.backgroundColor = selected
                ? new Color(.16f, .43f, .82f, 1f)
                : new Color(.12f, .15f, .19f, 1f);
            button.style.color = selected
                ? Color.white
                : new Color(.78f, .82f, .9f);
            button.style.borderBottomWidth = selected ? 3f : 0f;
            button.style.borderBottomColor =
                new Color(.45f, .78f, 1f);
        }

        internal GameObject Player => LevelEditorState.Player;

        internal void FocusPlayer()
        {
            GameObject player = LevelEditorState.Player;
            if (player == null || !player.scene.IsValid())
            {
                return;
            }

            LevelEditorState.LockPlayerToPlane = true;
            LevelEditorPlayerService.SnapPlayerToPlane();
            SetTool(LevelEditorTool.Player);
            Selection.activeGameObject = player;
            SceneView view = SceneView.lastActiveSceneView;
            view?.FrameSelected();
            view?.Focus();
        }

        private void SetEditMode(bool enabled)
        {
            LevelEditorState.EditMode = enabled;
            if (enabled)
            {
                FrameBeforeEditMode();
                LevelEditorViewLock.Enter(SceneView.lastActiveSceneView);
                LevelEditorState.Tool = LevelEditorTool.Select;
            }
            else
            {
                SurfaceTileEditorBridge.SetPainting(false);
                LevelEditorViewLock.Exit();
            }

            SceneView.RepaintAll();
        }

        private static void FrameBeforeEditMode()
        {
            SceneView view = SceneView.lastActiveSceneView;
            if (view == null)
            {
                return;
            }

            GameObject target = LevelEditorState.Player;
            if (target == null || !target.scene.IsValid())
            {
                return;
            }

            Selection.activeGameObject = target;
            view.FrameSelected();
            view.Repaint();
        }

        private void SetTool(LevelEditorTool tool)
        {
            GameObject player = LevelEditorState.Player;
            if (tool == LevelEditorTool.Player && player == null)
            {
                return;
            }

            LevelEditorState.Tool = tool;
            if (tool == LevelEditorTool.Player)
            {
                Tools.current = Tool.Move;
                Selection.activeGameObject = player;
                SceneView.lastActiveSceneView?.Focus();
            }

            SceneView.RepaintAll();
        }

        internal void SetPlayer(GameObject value)
        {
            if (value != null && !value.scene.IsValid())
            {
                value = null;
            }

            LevelEditorState.Player = value;
            playerField?.SetValueWithoutNotify(value);
            if (value == null &&
                LevelEditorState.Tool == LevelEditorTool.Player)
            {
                LevelEditorState.Tool = LevelEditorTool.Select;
            }

            if (value != null)
            {
                LevelEditorState.LockPlayerToPlane = true;
                if (LevelEditorState.EditMode)
                {
                    Selection.activeGameObject = value;
                }

                LevelEditorPlayerService.SnapPlayerToPlane();
            }

            SceneView.RepaintAll();
        }

        private void UseSelectedAsPlayer()
        {
            SetPlayer(Selection.activeGameObject);
        }

        internal void SetGenerationParent(GameObject value)
        {
            if (value != null && !value.scene.IsValid())
            {
                value = null;
            }

            LevelEditorState.GenerationParent = value;
            SceneView.RepaintAll();
        }

        private void RefreshPalette()
        {
            LevelEditorPalette palette = LevelEditorState.Palette;
            int count = palette != null ? palette.Entries.Count : 0;
            if (count == lastPaletteCount &&
                LevelEditorState.SelectedEntryIndex == lastSelectedIndex &&
                LevelEditorState.PaletteRevision == lastPaletteRevision)
            {
                return;
            }

            if (lastPaletteRevision != LevelEditorState.PaletteRevision)
            {
                DecorationPreviewCache.Clear();
                PrefabPreviewCache.Clear();
                ColorPreviewCache.Clear();
                LevelEditorDecorationService.ClearFrontPreviewCache();
            }

            lastPaletteCount = count;
            lastSelectedIndex = LevelEditorState.SelectedEntryIndex;
            lastPaletteRevision = LevelEditorState.PaletteRevision;
            paletteList.Clear();
            if (palette == null)
            {
                paletteList.Add(new Label("没有方块栏目。"));
                return;
            }

            float scale = Mathf.Clamp(
                LevelEditorState.PaletteScale,
                .5f,
                1.5f);
            float cardWidth = Mathf.Max(40f, 44f * scale);
            float cardHeight = Mathf.Max(64f, 68f * scale);
            float previewSize = Mathf.Max(28f, 98f * scale / 3f);
            if (paletteScroll != null)
            {
                paletteScroll.style.maxHeight = includeContextSections ? 280f : 420f;
            }

            if (LevelEditorState.SelectedEntryIndex >= 0 && LevelEditorState.SelectedEntryIndex < count)
            {
                int selected = LevelEditorState.SelectedEntryIndex;
                var actions = Row();
                actions.name = "block-selection-actions";
                var title = new Label(palette.Entries[selected].DisplayName);
                title.style.flexGrow = 1f;
                title.style.minWidth = 0f;
                title.style.overflow = Overflow.Hidden;
                title.tooltip = title.text;
                actions.Add(title);
                actions.Add(SmallButton("编辑", () => LevelEditorEntryWindow.OpenForEdit(selected)));
                actions.Add(SmallButton("删除", () => RemovePaletteEntry(selected)));
                paletteList.Add(actions);
            }

            string[] groupNames = { "红色方块", "蓝色方块", "绿色方块", "其他有色方块", "无色方块" };
            string[] groupKeys = { "red", "blue", "green", "other", "neutral" };
            var headings = new Foldout[groupNames.Length];
            var grids = new VisualElement[groupNames.Length];
            var groupCounts = new int[groupNames.Length];
            for (int groupIndex = 0; groupIndex < groupNames.Length; groupIndex++)
            {
                string stateKey = "LevelEditor.BlockGroup." + Application.dataPath + "." + groupKeys[groupIndex];
                var heading = new Foldout {
                    name = "block-group-" + groupKeys[groupIndex],
                    text = groupNames[groupIndex],
                    value = SessionState.GetBool(stateKey, true) };
                heading.RegisterValueChangedCallback(evt => SessionState.SetBool(stateKey, evt.newValue));
                var grid = new VisualElement { name = "block-grid-" + groupKeys[groupIndex] };
                grid.style.flexDirection = FlexDirection.Row;
                grid.style.flexWrap = Wrap.Wrap;
                grid.style.paddingTop = 4f;
                heading.Add(grid);
                paletteList.Add(heading);
                headings[groupIndex] = heading;
                grids[groupIndex] = grid;
            }

            for (int index = 0; index < palette.Entries.Count; index++)
            {
                int captured = index;
                LevelEditorBlockEntry entry = palette.Entries[index];
                int colorGroup = GetBlockColorGroup(entry);
                groupCounts[colorGroup]++;
                VisualElement cell = new VisualElement();
                cell.name = "block-card-" + index;
                cell.tooltip = entry.DisplayName;
                cell.style.width = cardWidth;
                cell.style.minWidth = cardWidth;
                cell.style.flexGrow = 0f;
                cell.style.flexShrink = 0f;
                cell.style.height = cardHeight;
                cell.style.marginRight = 4f;
                cell.style.marginBottom = 4f;

                Button card = new Button(() =>
                {
                    LevelEditorState.SelectedEntryIndex = captured;
                    LevelEditorState.SelectedPropIndex = -1;
                    if (LevelEditorState.Tool == LevelEditorTool.Parameters)
                        EditPlanningDefaults?.Invoke();
                    else LevelEditorState.Tool = LevelEditorTool.Paint;
                    SceneView.RepaintAll();
                })
                {
                    text = string.Empty
                };
                card.style.width = Length.Percent(100f);
                card.style.height = Length.Percent(100f);
                card.style.marginLeft = 0f;
                card.style.marginRight = 0f;
                card.style.marginTop = 0f;
                card.style.marginBottom = 0f;
                card.style.flexDirection = FlexDirection.Column;
                card.style.alignItems = Align.Center;
                card.style.justifyContent = Justify.FlexStart;
                card.style.paddingLeft = 0f;
                card.style.paddingRight = 0f;
                card.style.paddingTop = 0f;
                card.style.paddingBottom = 0f;
                card.style.backgroundColor = EditorGUIUtility.isProSkin
                    ? new Color(0.12f, 0.14f, 0.18f, 1f)
                    : new Color(0.88f, 0.89f, 0.92f, 1f);
                card.style.borderLeftWidth = 2f;
                card.style.borderRightWidth = 2f;
                card.style.borderTopWidth = 2f;
                card.style.borderBottomWidth = 2f;
                card.style.borderLeftColor =
                    card.style.borderRightColor =
                    card.style.borderTopColor =
                    card.style.borderBottomColor =
                    index == LevelEditorState.SelectedEntryIndex
                        ? new Color(.25f, .75f, 1f, 1f)
                        : new Color(.18f, .2f, .24f, 1f);

                Image preview = new Image
                {
                    image = ResolvePreview(entry),
                    scaleMode = ScaleMode.ScaleToFit,
                    pickingMode = PickingMode.Ignore
                };
                preview.style.width = previewSize;
                preview.style.height = previewSize;
                preview.style.marginTop = 3f;
                preview.style.flexShrink = 0f;
                card.Add(preview);

                Label name = new Label(entry.DisplayName);
                name.style.width = cardWidth - 4f;
                name.style.fontSize =
                    Mathf.Clamp(10f * scale, 10f, 12f);
                name.style.whiteSpace = WhiteSpace.Normal;
                name.style.maxHeight = 28f;
                name.style.overflow = Overflow.Hidden;
                name.style.unityTextAlign = TextAnchor.MiddleCenter;
                name.pickingMode = PickingMode.Ignore;
                name.style.marginTop = 2f;
                card.Add(name);
                cell.Add(card);

                if (entry.SourcePrefab != null &&
                    AssetPreview.IsLoadingAssetPreview(
                        entry.SourcePrefab.GetInstanceID()))
                {
                    EditorApplication.delayCall += () =>
                    {
                        lastPaletteCount = -1;
                        SceneView.RepaintAll();
                    };
                }

                grids[colorGroup].Add(cell);
            }

            for (int groupIndex = 0; groupIndex < headings.Length; groupIndex++)
            {
                headings[groupIndex].text = groupNames[groupIndex] + "（" + groupCounts[groupIndex] + "）";
                headings[groupIndex].style.display = groupCounts[groupIndex] == 0
                    ? DisplayStyle.None : DisplayStyle.Flex;
            }
            var add = CreateAddCard(
                cardWidth,
                cardHeight,
                LevelEditorEntryWindow.OpenForAdd,
                "新增方块",
                new Color(.3f, .66f, 1f, 1f));
            ConfigureCompactAddCard(add, "block-add-card", cardWidth);
            paletteList.Add(add);
        }

        private static VisualElement CreateAddCard(
            float width,
            float height,
            System.Action action,
            string captionText,
            Color borderColor)
        {
            VisualElement cell = new VisualElement();
            cell.style.width = width;
            cell.style.minWidth = width;
            cell.style.flexGrow = 1f;
            cell.style.height = height;
            cell.style.marginRight = 6f;
            cell.style.marginBottom = 6f;

            Button button = new Button(action)
            {
                text = string.Empty
            };
            button.style.width = Length.Percent(100f);
            button.style.height = Length.Percent(100f);
            button.style.flexDirection = FlexDirection.Column;
            button.style.alignItems = Align.Center;
            button.style.justifyContent = Justify.Center;
            button.style.backgroundColor = EditorGUIUtility.isProSkin
                ? new Color(.1f, .14f, .2f, 1f)
                : new Color(.85f, .9f, .98f, 1f);
            button.style.borderLeftWidth = 2f;
            button.style.borderRightWidth = 2f;
            button.style.borderTopWidth = 2f;
            button.style.borderBottomWidth = 2f;
            button.style.borderLeftColor =
                button.style.borderRightColor =
                button.style.borderTopColor =
                button.style.borderBottomColor =
                borderColor;

            Label plus = new Label("+");
            plus.style.fontSize = 34f;
            plus.style.unityFontStyleAndWeight = FontStyle.Bold;
            plus.style.unityTextAlign = TextAnchor.MiddleCenter;
            plus.pickingMode = PickingMode.Ignore;
            button.Add(plus);

            Label caption = new Label(captionText);
            caption.style.fontSize = 12f;
            caption.style.unityTextAlign = TextAnchor.MiddleCenter;
            caption.pickingMode = PickingMode.Ignore;
            button.Add(caption);
            cell.Add(button);
            return cell;
        }

        private void RefreshProps()
        {
            LevelEditorPalette palette = LevelEditorState.Palette;
            int count = palette != null ? palette.PropEntries.Count : 0;
            if (count == lastPropCount &&
                LevelEditorState.SelectedPropIndex == lastPropIndex &&
                LevelEditorState.PaletteRevision == lastPropRevision)
            {
                return;
            }

            lastPropCount = count;
            lastPropIndex = LevelEditorState.SelectedPropIndex;
            lastPropRevision = LevelEditorState.PaletteRevision;
            pendingPropPreviews.Clear();
            propList.Clear();
            float scale = Mathf.Clamp(
                LevelEditorState.PaletteScale,
                .5f,
                1.5f);
            float cardWidth = Mathf.Max(40f, 44f * scale);
            float cardHeight = Mathf.Max(64f, 68f * scale);
            float previewSize = Mathf.Max(28f, 98f * scale / 3f);
            if (propScroll != null)
            {
                propScroll.style.height = includeContextSections
                    ? 280f
                    : 420f;
            }

            if (palette != null && LevelEditorState.SelectedPropIndex >= 0 &&
                LevelEditorState.SelectedPropIndex < count)
            {
                int selected = LevelEditorState.SelectedPropIndex;
                var actions = Row();
                actions.name = "prop-selection-actions";
                var title = new Label(palette.PropEntries[selected].DisplayName);
                title.style.flexGrow = 1f;
                title.style.minWidth = 0f;
                title.style.overflow = Overflow.Hidden;
                title.tooltip = title.text;
                actions.Add(title);
                actions.Add(SmallButton("编辑", () => LevelEditorPropEntryWindow.OpenForEdit(selected)));
                actions.Add(SmallButton("删除", () => RemovePropEntry(selected)));
                propList.Add(actions);
            }

            string[] groupNames = { "红色道具", "蓝色道具", "绿色道具", "其他有色道具", "无色道具" };
            string[] groupKeys = { "red", "blue", "green", "other", "neutral" };
            var headings = new Foldout[groupNames.Length];
            var grids = new VisualElement[groupNames.Length];
            var groupCounts = new int[groupNames.Length];
            for (int groupIndex = 0; groupIndex < groupNames.Length; groupIndex++)
            {
                string stateKey = "LevelEditor.PropGroup." + Application.dataPath + "." + groupKeys[groupIndex];
                var heading = new Foldout {
                    name = "prop-group-" + groupKeys[groupIndex],
                    text = groupNames[groupIndex],
                    value = SessionState.GetBool(stateKey, true) };
                heading.RegisterValueChangedCallback(evt => SessionState.SetBool(stateKey, evt.newValue));
                var grid = new VisualElement { name = "prop-grid-" + groupKeys[groupIndex] };
                grid.style.flexDirection = FlexDirection.Row;
                grid.style.flexWrap = Wrap.Wrap;
                grid.style.paddingTop = 4f;
                heading.Add(grid);
                propList.Add(heading);
                headings[groupIndex] = heading;
                grids[groupIndex] = grid;
            }

            if (palette != null && count > 0)
            {
                for (int index = 0; index < count; index++)
                {
                    int captured = index;
                    LevelEditorPropEntry entry = palette.PropEntries[index];
                    // Retain saved entry IDs/indices, but show identical migrated defaults only once.
                    bool duplicate = false;
                    if (entry.ComponentValueOverrides.Count == 0)
                        for (int earlier = 0; earlier < index; earlier++)
                        {
                            var previous = palette.PropEntries[earlier];
                            if (previous.Prefab == entry.Prefab && previous.DisplayName == entry.DisplayName &&
                                previous.ComponentValueOverrides.Count == 0) { duplicate = true; break; }
                        }
                    if (duplicate) continue;
                    int colorGroup = GetPropColorGroup(entry);
                    groupCounts[colorGroup]++;
                    VisualElement cell = new VisualElement();
                    cell.name = "prop-card-" + index;
                    cell.tooltip = entry.DisplayName;
                    cell.style.width = cardWidth;
                    cell.style.minWidth = cardWidth;
                    cell.style.flexGrow = 0f;
                    cell.style.flexShrink = 0f;
                    cell.style.height = cardHeight;
                    cell.style.marginRight = 4f;
                    cell.style.marginBottom = 4f;

                    Button card = new Button(() =>
                    {
                        LevelEditorState.SelectedPropIndex = captured;
                        LevelEditorState.SelectedEntryIndex = -1;
                        if (LevelEditorState.Tool == LevelEditorTool.Parameters)
                            EditPlanningDefaults?.Invoke();
                        else LevelEditorState.Tool = LevelEditorTool.Paint;
                        SceneView.RepaintAll();
                    })
                    {
                        text = string.Empty
                    };
                card.style.width = Length.Percent(100f);
                card.style.height = Length.Percent(100f);
                card.style.marginLeft = 0f;
                card.style.marginRight = 0f;
                card.style.marginTop = 0f;
                card.style.marginBottom = 0f;
                card.style.flexDirection = FlexDirection.Column;
                card.style.alignItems = Align.Center;
                card.style.justifyContent = Justify.FlexStart;
                card.style.paddingLeft = 0f;
                card.style.paddingRight = 0f;
                card.style.paddingTop = 0f;
                card.style.paddingBottom = 0f;
                card.style.backgroundColor = EditorGUIUtility.isProSkin
                    ? new Color(0.12f, 0.14f, 0.18f, 1f)
                    : new Color(0.88f, 0.89f, 0.92f, 1f);
                Color border = captured == LevelEditorState.SelectedPropIndex
                    ? new Color(.95f, .62f, .2f)
                    : new Color(.18f, .2f, .24f);
                card.style.borderLeftWidth = 2f;
                card.style.borderRightWidth = 2f;
                card.style.borderTopWidth = 2f;
                card.style.borderBottomWidth = 2f;
                card.style.borderLeftColor = border;
                card.style.borderRightColor = border;
                card.style.borderTopColor = border;
                card.style.borderBottomColor = border;

                bool hasArtwork = ObjectArtworkPlanningPreview.TryGet(entry.Prefab, entry.ComponentValueOverrides,out var artwork);
                Texture2D preview = hasArtwork ? artwork.Texture : entry.Prefab != null
                    ? AssetPreview.GetAssetPreview(entry.Prefab)
                    : null;
                bool awaitingPreview = !hasArtwork && preview == null &&
                    entry.Prefab != null;
                if (!hasArtwork && preview == null && entry.Prefab != null)
                {
                    preview = AssetPreview.GetMiniThumbnail(entry.Prefab);
                }

                Image image = new Image
                {
                    image = preview,
                    scaleMode = hasArtwork ? ScaleMode.StretchToFill : ScaleMode.ScaleToFit,
                    pickingMode = PickingMode.Ignore
                };
                var previewFrame=new VisualElement { pickingMode=PickingMode.Ignore };
                previewFrame.style.width=previewSize; previewFrame.style.height=previewSize; previewFrame.style.marginTop=3f;
                previewFrame.style.flexShrink=0f;
                image.style.position=Position.Absolute;
                float artWidth=previewSize,artHeight=previewSize;
                if (hasArtwork)
                {
                    image.uv=artwork.UV;
                    bool vertical=Mathf.Abs(Mathf.DeltaAngle(artwork.RotationDegrees,0))%180>45;
                    Vector2 display=vertical ? new Vector2(artwork.Cells.y,artwork.Cells.x) : artwork.Cells;
                    float fit=previewSize/Mathf.Max(.001f,Mathf.Max(display.x,display.y));
                    artWidth=artwork.Cells.x*fit; artHeight=artwork.Cells.y*fit;
                    image.style.rotate=new Rotate(new Angle(-artwork.RotationDegrees,AngleUnit.Degree));
                    image.style.scale=new Scale(new Vector3(artwork.FlipX ? -1 : 1,1,1));
                }
                image.style.width=artWidth; image.style.height=artHeight;
                image.style.left=(previewSize-artWidth)*.5f; image.style.top=(previewSize-artHeight)*.5f;
                if (awaitingPreview)
                {
                    pendingPropPreviews[image] = entry.Prefab;
                }
                previewFrame.Add(image); card.Add(previewFrame);
                Label name = new Label(entry.DisplayName);
                name.style.width = cardWidth - 4f;
                name.style.fontSize =
                    Mathf.Clamp(10f * scale, 10f, 12f);
                name.style.whiteSpace = WhiteSpace.Normal;
                name.style.maxHeight = 28f;
                name.style.overflow = Overflow.Hidden;
                name.style.unityTextAlign = TextAnchor.MiddleCenter;
                name.pickingMode = PickingMode.Ignore;
                name.style.marginTop = 2f;
                card.Add(name);
                cell.Add(card);

                    grids[colorGroup].Add(cell);
                }
            }

            for (int groupIndex = 0; groupIndex < headings.Length; groupIndex++)
            {
                headings[groupIndex].text = groupNames[groupIndex] + "（" + groupCounts[groupIndex] + "）";
                headings[groupIndex].style.display = groupCounts[groupIndex] == 0
                    ? DisplayStyle.None : DisplayStyle.Flex;
            }
            var add = CreatePropAddCard(cardWidth, cardHeight);
            ConfigureCompactAddCard(add, "prop-add-card", cardWidth);
            propList.Add(add);
        }

        internal static int GetPropColorGroup(LevelEditorPropEntry entry)
        {
            if (entry?.Prefab == null) return 4;
            var colors = new HashSet<string>();
            foreach (var color in entry.Prefab.GetComponentsInChildren<ColorObject>(true))
                AddColor(ResolveColor(color, "baseColorTypeId", color.BaseColorTypeId));
            foreach (var key in entry.Prefab.GetComponentsInChildren<ColorKeyPickup>(true))
                AddColor(ResolveColor(key, "colorTypeId", key.ColorTypeId));
            if (colors.Count == 0)
            {
                string layer = LayerMask.LayerToName(entry.Prefab.layer).ToLowerInvariant();
                if (layer == "red" || layer == "blue" || layer == "green") colors.Add(layer);
            }
            if (colors.Count == 0) return 4;
            if (colors.Count > 1) return 3;
            foreach (string color in colors)
                return color == "red" ? 0 : color == "blue" ? 1 : color == "green" ? 2 : 3;
            return 4;

            void AddColor(string color)
            {
                if (!string.IsNullOrWhiteSpace(color)) colors.Add(color.Trim().ToLowerInvariant());
            }

            string ResolveColor(Component component, string property, string fallback)
            {
                string path = AnimationUtility.CalculateTransformPath(component.transform, entry.Prefab.transform);
                int index = System.Array.IndexOf(component.GetComponents(component.GetType()), component);
                string result = fallback;
                foreach (var value in entry.ComponentValueOverrides)
                    if (value != null && value.ComponentPath == path &&
                        value.ComponentTypeName == component.GetType().FullName &&
                        value.ComponentIndex == index && value.PropertyPath == property)
                        result = value.Value;
                return result;
            }
        }

        private static VisualElement CreatePropAddCard(
            float width,
            float height)
        {
            return CreateAddCard(
                width,
                height,
                LevelEditorPropEntryWindow.OpenForAdd,
                "新增道具",
                new Color(.95f, .62f, .2f, 1f));
        }

        internal static int GetBlockColorGroup(LevelEditorBlockEntry entry)
        {
            if (entry == null || entry.Color.a <= .01f) return 4;
            Color.RGBToHSV(entry.Color, out float hue, out float saturation, out float value);
            if (saturation < .15f || value < .05f) return 4;
            if (hue < 1f / 12f || hue >= 11f / 12f) return 0;
            if (hue >= 5f / 12f && hue < 3f / 4f) return 1;
            if (hue >= 1f / 4f && hue < 5f / 12f) return 2;
            return 3;
        }

        private static void ConfigureCompactAddCard(VisualElement add, string name, float cardWidth)
        {
            add.name = name;
            add.style.flexGrow = 0f;
            add.style.flexShrink = 0f;
            foreach (var label in add.Query<Label>().ToList())
            {
                label.style.fontSize = label.text == "+" ? 22f : 10f;
                label.style.width = cardWidth - 4f;
                label.style.whiteSpace = WhiteSpace.Normal;
            }
        }

        private void RemovePropEntry(int index)
        {
            LevelEditorPalette palette = LevelEditorState.Palette;
            if (palette == null ||
                index < 0 ||
                index >= palette.PropEntries.Count)
            {
                return;
            }

            LevelEditorPropEntry entry = palette.PropEntries[index];
            if (!EditorUtility.DisplayDialog(
                    "删除道具栏目",
                    $"确定删除“{entry.DisplayName}”？",
                    "删除",
                    "取消"))
            {
                return;
            }

            LevelEditorPaletteService.RemoveProp(palette, index);
            LevelEditorState.SelectedPropIndex = Mathf.Max(0, index - 1);
            lastPropCount = -1;
            SceneView.RepaintAll();
        }

        private static Texture2D ResolvePreview(LevelEditorBlockEntry entry)
        {
            string key = GetDecorationPreviewKey(entry);
            if (DecorationPreviewCache.TryGetValue(
                    key,
                    out Texture2D cachedFront) &&
                cachedFront != null)
            {
                return cachedFront;
            }

            Texture2D front = LevelEditorDecorationService
                .GetEntryFrontPreview(entry, 128);
            if (front != null)
            {
                DecorationPreviewCache[key] = front;
                return front;
            }

            GameObject sourcePrefab = entry.SourcePrefab;
            if (sourcePrefab == null)
            {
                return GetColorPreview(entry.Color);
            }

            int instanceId = sourcePrefab.GetInstanceID();
            if (PrefabPreviewCache.TryGetValue(
                    instanceId,
                    out Texture2D cached) &&
                cached != null)
            {
                return cached;
            }

            // Geometry without authored artwork is represented by its flat palette
            // color; a lit 3D prefab thumbnail misrepresents the front-facing level.
            Texture2D preview = GetColorPreview(entry.Color);

            if (preview != null)
            {
                PrefabPreviewCache[instanceId] = preview;
            }

            return preview;
        }

        private static string GetDecorationPreviewKey(
            LevelEditorBlockEntry entry)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append(entry.Mode);
            builder.Append(':');
            builder.Append(ColorUtility.ToHtmlStringRGBA(entry.Color));
            builder.Append(':');
            builder.Append(entry.SourcePrefab != null
                ? entry.SourcePrefab.GetInstanceID()
                : 0);
            builder.Append(':');
            builder.Append(entry.SurfaceTilePalette != null
                ? entry.SurfaceTilePalette.GetInstanceID()
                : 0);
            builder.Append(':');
            builder.Append(entry.SurfaceTileCellSize);
            builder.Append(':');
            builder.Append(entry.SurfaceTileTransparentBase ? 1 : 0);
            for (int index = 0;
                 index < entry.SurfaceTilePlacements.Count;
                 index++)
            {
                SurfaceTilePlacement placement =
                    entry.SurfaceTilePlacements[index];
                if (placement == null)
                {
                    continue;
                }

                builder.Append('|');
                builder.Append((int)placement.Face);
                builder.Append(',');
                builder.Append(placement.Cell.x);
                builder.Append(',');
                builder.Append(placement.Cell.y);
                builder.Append(',');
                builder.Append(placement.TileId);
                builder.Append(',');
                builder.Append(placement.QuarterTurns);
                builder.Append(',');
                builder.Append(placement.FlipX ? 1 : 0);
                builder.Append(',');
                builder.Append(placement.FlipY ? 1 : 0);
                builder.Append(',');
                builder.Append((int)placement.Anchor);
                builder.Append(',');
                builder.Append(placement.Layer);
                builder.Append(',');
                builder.Append(placement.OffsetCells.x);
                builder.Append(',');
                builder.Append(placement.OffsetCells.y);
            }

            return builder.ToString();
        }

        private static Texture2D GetColorPreview(Color color)
        {
            string key = ColorUtility.ToHtmlStringRGBA(color);
            if (ColorPreviewCache.TryGetValue(key, out Texture2D cached) &&
                cached != null)
            {
                return cached;
            }

            const int size = 64;
            Texture2D texture = new Texture2D(
                size,
                size,
                TextureFormat.RGBA32,
                false)
            {
                name = "LevelEditorBlock_" + key,
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Color[] pixels = new Color[size * size];
            for (int index = 0; index < pixels.Length; index++)
            {
                pixels[index] = color;
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            ColorPreviewCache[key] = texture;
            return texture;
        }

        private void AdjustPaletteScale(float delta)
        {
            float next = Mathf.Clamp(
                LevelEditorState.PaletteScale + delta,
                .5f,
                1.5f);
            if (Mathf.Approximately(next, LevelEditorState.PaletteScale))
            {
                return;
            }

            LevelEditorState.PaletteScale = next;
            lastPaletteCount = -1;
            lastPropCount = -1;
            RefreshPalette();
            RefreshProps();
            paletteScaleLabel.text =
                $"{Mathf.RoundToInt(next * 100f)}%";
        }

        private void RemovePaletteEntry(int index)
        {
            LevelEditorPalette palette = LevelEditorState.Palette;
            if (palette == null || index < 0 ||
                index >= palette.Entries.Count)
            {
                return;
            }

            LevelEditorBlockEntry entry = palette.Entries[index];
            string displayName = entry != null
                ? entry.DisplayName
                : "这个栏目";
            if (!EditorUtility.DisplayDialog(
                    "删除栏目",
                    $"确定删除“{displayName}”栏目？",
                    "删除",
                    "取消"))
            {
                return;
            }

            LevelEditorPaletteService.RemoveEntry(palette, index);
            LevelEditorState.SelectedEntryIndex = Mathf.Max(
                0,
                index - 1);
            lastPaletteCount = -1;
            SceneView.RepaintAll();
        }

        private static VisualElement Row()
        {
            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            return row;
        }

        private static Button ActionButton(
            string text,
            System.Action action)
        {
            Button button = new Button(action) { text = text };
            button.style.flexGrow = 1f;
            button.style.marginRight = 2f;
            button.style.height = 28f;
            button.style.fontSize = 13f;
            return button;
        }

        private static Button SmallButton(
            string text,
            System.Action action)
        {
            Button button = new Button(action) { text = text };
            button.style.width = 28f;
            button.style.height = 24f;
            button.style.fontSize = 13f;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.paddingLeft = 0f;
            button.style.paddingRight = 0f;
            return button;
        }

        private static void Round(VisualElement element, float radius)
        {
            element.style.borderTopLeftRadius = radius;
            element.style.borderTopRightRadius = radius;
            element.style.borderBottomLeftRadius = radius;
            element.style.borderBottomRightRadius = radius;
        }
    }
}
