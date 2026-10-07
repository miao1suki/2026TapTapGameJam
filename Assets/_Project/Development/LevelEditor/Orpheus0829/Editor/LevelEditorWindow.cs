using System.Collections.Generic;
using System.Text;
using Project.ColorBlocks;
using Project.SurfaceTiles;
using Project.SurfaceTiles.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Project.LevelEditor.Editor
{
    internal sealed class LevelEditorWindow : EditorWindow
    {
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
            root.style.paddingLeft = 10f;
            root.style.paddingRight = 10f;
            root.style.paddingTop = 8f;
            root.style.paddingBottom = 8f;
            panel = new LevelEditorPanel(root);
            LevelEditorBlockFactory.MigratePlacedBlocks();
        }

        private void OnEnable()
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Tick;
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
            double now = EditorApplication.timeSinceStartup;
            if (panel == null || now < nextRefreshTime)
            {
                return;
            }

            nextRefreshTime = now + .1d;
            if (EditorApplication.isPlayingOrWillChangePlaymode &&
                LevelEditorState.EditMode)
            {
                LevelEditorState.EditMode = false;
                SurfaceTileEditorBridge.SetPainting(false);
                LevelEditorViewLock.Exit();
                SceneView.RepaintAll();
            }

            panel.Refresh();
            if (LevelEditorPlayerService.EnforceEditPlane())
            {
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
        private readonly Button editModeButton;
        private readonly FloatField gridSizeField;
        private readonly Button selectButton;
        private readonly Button mergeButton;
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

            VisualElement mergeRow = Row();
            mergeButton = ActionButton("合并选中方块", () =>
            {
                if (LevelEditorBlockFactory.MergeSelected(out string result))
                    SceneView.RepaintAll();
                mergeStatus.text = result;
            });
            mergeRow.Add(mergeButton);
            body.Add(mergeRow);
            mergeStatus = new Label("选择工具：单击选中，Ctrl 单击增减，拖动框选。 ");
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
            paletteList.style.flexDirection = FlexDirection.Row;
            paletteList.style.flexWrap = Wrap.Wrap;
            paletteList.style.paddingTop = 4f;
            paletteList.style.paddingBottom = 4f;

            paletteScroll = new ScrollView();
            paletteScroll.style.height = 240f;
            paletteScroll.horizontalScrollerVisibility =
                ScrollerVisibility.Hidden;
            paletteScroll.Add(paletteList);
            paletteSection.Add(paletteScroll);
            body.Add(paletteSection);

            Foldout propSection = new Foldout
            {
                text = "道具栏目",
                value = true
            };
            propList = new VisualElement();
            propList.style.width = Length.Percent(100f);
            propList.style.flexDirection = FlexDirection.Row;
            propList.style.flexWrap = Wrap.Wrap;
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
            mergeButton.SetEnabled(LevelEditorBlockFactory.SelectedBlockCount >= 2);
            RefreshPalette();
            RefreshProps();
            paletteScaleLabel.text =
                $"{Mathf.RoundToInt(LevelEditorState.PaletteScale * 100f)}%";
            refreshing = false;
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
                LevelEditorBlockFactory.MigratePlacedBlocks();
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
            float cardWidth = 124f * scale;
            float cardHeight = 148f * scale;
            if (paletteScroll != null)
            {
                paletteScroll.style.height = Mathf.Clamp(
                    cardHeight + 24f,
                    104f,
                    240f);
            }

            for (int index = 0; index < palette.Entries.Count; index++)
            {
                int captured = index;
                LevelEditorBlockEntry entry = palette.Entries[index];
                VisualElement cell = new VisualElement();
                cell.style.width = cardWidth;
                cell.style.height = cardHeight;
                cell.style.marginRight = 6f;
                cell.style.marginBottom = 6f;

                Button card = new Button(() =>
                {
                    LevelEditorState.SelectedEntryIndex = captured;
                    LevelEditorState.SelectedPropIndex = -1;
                    LevelEditorState.Tool = LevelEditorTool.Paint;
                    SceneView.RepaintAll();
                })
                {
                    text = string.Empty
                };
                card.style.width = Length.Percent(100f);
                card.style.height = Length.Percent(100f);
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
                preview.style.width = 98f * scale;
                preview.style.height = 98f * scale;
                preview.style.marginTop = 6f * scale;
                card.Add(preview);

                Label name = new Label(entry.DisplayName);
                name.style.width = 116f * scale;
                name.style.fontSize =
                    Mathf.Clamp(12f * scale, 10f, 16f);
                name.style.whiteSpace = WhiteSpace.Normal;
                name.style.unityTextAlign = TextAnchor.MiddleCenter;
                name.pickingMode = PickingMode.Ignore;
                name.style.marginTop = 4f * scale;
                card.Add(name);
                cell.Add(card);

                if (index == LevelEditorState.SelectedEntryIndex)
                {
                    cell.Add(CreatePaletteCardActions(
                        scale,
                        () => LevelEditorEntryWindow.OpenForEdit(
                            captured),
                        () => RemovePaletteEntry(captured)));
                }

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

                paletteList.Add(cell);
            }

            paletteList.Add(CreateAddCard(
                cardWidth,
                cardHeight,
                LevelEditorEntryWindow.OpenForAdd,
                "新增方块",
                new Color(.3f, .66f, 1f, 1f)));
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
            propList.Clear();
            float scale = Mathf.Clamp(
                LevelEditorState.PaletteScale,
                .5f,
                1.5f);
            float cardWidth = 124f * scale;
            float cardHeight = 148f * scale;
            if (propScroll != null)
            {
                propScroll.style.height = Mathf.Clamp(
                    cardHeight + 24f,
                    104f,
                    240f);
            }

            if (palette != null && count > 0)
            {
                for (int index = 0; index < count; index++)
                {
                    int captured = index;
                    LevelEditorPropEntry entry = palette.PropEntries[index];
                    VisualElement cell = new VisualElement();
                    cell.style.width = cardWidth;
                    cell.style.height = cardHeight;
                    cell.style.marginRight = 6f;
                    cell.style.marginBottom = 6f;

                    Button card = new Button(() =>
                    {
                        LevelEditorState.SelectedPropIndex = captured;
                        LevelEditorState.SelectedEntryIndex = -1;
                        LevelEditorState.Tool = LevelEditorTool.Paint;
                        SceneView.RepaintAll();
                    })
                    {
                        text = string.Empty
                    };
                card.style.width = Length.Percent(100f);
                card.style.height = Length.Percent(100f);
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

                Texture2D preview = entry.Prefab != null
                    ? AssetPreview.GetAssetPreview(entry.Prefab)
                    : null;
                if (preview == null && entry.Prefab != null)
                {
                    preview = AssetPreview.GetMiniThumbnail(entry.Prefab);
                }

                Image image = new Image
                {
                    image = preview,
                    scaleMode = ScaleMode.ScaleToFit,
                    pickingMode = PickingMode.Ignore
                };
                image.style.width = 98f * scale;
                image.style.height = 98f * scale;
                image.style.marginTop = 6f * scale;
                card.Add(image);
                Label name = new Label(entry.DisplayName);
                name.style.width = 116f * scale;
                name.style.fontSize =
                    Mathf.Clamp(12f * scale, 10f, 16f);
                name.style.whiteSpace = WhiteSpace.Normal;
                name.style.unityTextAlign = TextAnchor.MiddleCenter;
                name.pickingMode = PickingMode.Ignore;
                name.style.marginTop = 4f * scale;
                card.Add(name);
                cell.Add(card);

                if (captured == LevelEditorState.SelectedPropIndex)
                {
                    cell.Add(CreatePaletteCardActions(
                        scale,
                        () => LevelEditorPropEntryWindow.OpenForEdit(
                            captured),
                        () => RemovePropEntry(captured)));
                }

                    propList.Add(cell);
                }
            }

            propList.Add(CreatePropAddCard(cardWidth, cardHeight));
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

        private static VisualElement CreatePaletteCardActions(
            float scale,
            System.Action editAction,
            System.Action deleteAction)
        {
            float previewHorizontalInset =
                (124f - 98f) * .5f * scale;
            float actionHeight = Mathf.Clamp(
                22f * scale,
                18f,
                28f);
            float actionFontSize = Mathf.Clamp(
                12f * scale,
                10f,
                14f);
            float actionInset = Mathf.Clamp(
                2f * scale,
                1.5f,
                3f);
            float actionGap = Mathf.Clamp(
                4f * scale,
                3f,
                6f);
            float previewWidth = 98f * scale;
            const int actionCount = 2;
            float actionWidth = Mathf.Clamp(
                (previewWidth - actionGap * (actionCount - 1) - 4f) /
                    actionCount,
                20f,
                48f);

            VisualElement actions = Row();
            actions.style.position = Position.Absolute;
            actions.style.left = previewHorizontalInset;
            actions.style.width = previewWidth;
            actions.style.top = 6f * scale + actionInset;
            actions.style.height = actionHeight + actionInset * 2f;
            actions.style.justifyContent = Justify.Center;
            actions.style.alignItems = Align.Center;
            actions.style.backgroundColor =
                new Color(.03f, .04f, .055f, .9f);
            Round(actions, 4f);

            Button edit = new Button(editAction)
            {
                text = "编辑"
            };
            edit.style.width = actionWidth;
            edit.style.height = actionHeight;
            edit.style.fontSize = actionFontSize;
            edit.style.unityFontStyleAndWeight = FontStyle.Bold;
            edit.style.paddingLeft = 0f;
            edit.style.paddingRight = 0f;
            edit.style.backgroundColor =
                new Color(.16f, .43f, .82f);
            edit.style.color = Color.white;
            edit.style.marginRight = actionGap;
            actions.Add(edit);

            Button delete = new Button(deleteAction)
            {
                text = "删除"
            };
            delete.style.width = actionWidth;
            delete.style.height = actionHeight;
            delete.style.fontSize = actionFontSize;
            delete.style.unityFontStyleAndWeight = FontStyle.Bold;
            delete.style.paddingLeft = 0f;
            delete.style.paddingRight = 0f;
            delete.style.backgroundColor =
                new Color(.62f, .18f, .16f);
            delete.style.color = Color.white;
            actions.Add(delete);
            return actions;
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

            Texture2D preview = AssetPreview.GetAssetPreview(sourcePrefab);
            if (preview == null)
            {
                preview = AssetPreview.GetMiniThumbnail(sourcePrefab);
            }

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
