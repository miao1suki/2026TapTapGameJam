using System.Collections.Generic;
using System.Text;
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

        [MenuItem("Tools/2026TapTap/关卡编辑器/打开编辑器窗口")]
        private static void Open()
        {
            LevelEditorWindow window = GetWindow<LevelEditorWindow>();
            window.titleContent = new GUIContent("关卡编辑器");
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
        private readonly Button paintButton;
        private readonly Button eraseButton;
        private readonly Button playerToolButton;
        private readonly VisualElement paletteList;
        private readonly Label paletteScaleLabel;
        private readonly ObjectField playerField;
        private readonly Toggle playerPlaneLockToggle;
        private readonly Label playerStatusLabel;
        private readonly ObjectField generationParentField;
        private readonly Label generationParentStatusLabel;
        private readonly Label selectionLabel;
        private readonly Button replaceButton;
        private readonly Button removeButton;
        private int lastPaletteCount = -1;
        private int lastSelectedIndex = -2;
        private int lastPaletteRevision = -1;
        private bool refreshing;

        internal LevelEditorPanel(VisualElement root)
        {
            LevelEditorState.Palette =
                LevelEditorPaletteService.GetOrCreate();
            LevelEditorState.CellSize = 1f;
            LevelEditorState.Tool = LevelEditorTool.Select;

            panel = new VisualElement { pickingMode = PickingMode.Position };
            panel.style.width = Length.Percent(100f);
            panel.style.flexGrow = 1f;
            panel.style.paddingLeft = 12f;
            panel.style.paddingRight = 12f;
            panel.style.paddingTop = 10f;
            panel.style.paddingBottom = 10f;
            panel.style.backgroundColor = EditorGUIUtility.isProSkin
                ? new Color(0.07f, 0.085f, 0.11f, 0.97f)
                : new Color(0.95f, 0.96f, 0.98f, 0.98f);
            Round(panel, 8f);
            root.Add(panel);

            VisualElement header = Row();
            header.style.alignItems = Align.Center;
            VisualElement titles = new VisualElement();
            titles.style.flexGrow = 1f;
            Label title = new Label("关卡编辑器");
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

            gridSizeField = new FloatField("网格尺寸")
            {
                value = LevelEditorState.CellSize,
                isDelayed = true
            };
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

            Foldout generationParentSection = new Foldout
            {
                text = "父物体选取",
                value = false
            };
            generationParentField = new ObjectField("生成父物体")
            {
                objectType = typeof(GameObject),
                allowSceneObjects = true
            };
            generationParentField.RegisterValueChangedCallback(evt =>
            {
                if (!refreshing)
                {
                    SetGenerationParent(
                        evt.newValue as GameObject);
                }
            });
            generationParentSection.Add(generationParentField);

            VisualElement generationParentActions = Row();
            generationParentActions.Add(ActionButton(
                "使用当前选择",
                UseSelectedAsGenerationParent));
            generationParentSection.Add(generationParentActions);

            generationParentStatusLabel = new Label();
            generationParentStatusLabel.style.whiteSpace =
                WhiteSpace.Normal;
            generationParentStatusLabel.style.opacity = .8f;
            generationParentSection.Add(generationParentStatusLabel);
            body.Add(generationParentSection);

            Foldout paletteSection = new Foldout
            {
                text = "方块栏目",
                value = true
            };
            paletteList = new VisualElement();
            paletteList.style.flexDirection = FlexDirection.Row;
            paletteList.style.flexWrap = Wrap.Wrap;
            paletteList.style.paddingTop = 4f;
            paletteList.style.paddingBottom = 4f;

            VisualElement paletteScaleRow = Row();
            Label paletteScaleTitle = new Label("栏目缩放");
            paletteScaleTitle.style.flexGrow = 1f;
            paletteScaleTitle.style.unityTextAlign = TextAnchor.MiddleLeft;
            paletteScaleRow.Add(paletteScaleTitle);
            paletteScaleRow.Add(SmallButton("−", () =>
                AdjustPaletteScale(-.1f)));
            paletteScaleLabel = new Label();
            paletteScaleLabel.style.width = 48f;
            paletteScaleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            paletteScaleRow.Add(paletteScaleLabel);
            paletteScaleRow.Add(SmallButton("+", () =>
                AdjustPaletteScale(.1f)));
            paletteSection.Add(paletteScaleRow);

            ScrollView paletteScroll = new ScrollView();
            paletteScroll.style.height = 310f;
            paletteScroll.Add(paletteList);
            paletteSection.Add(paletteScroll);
            body.Add(paletteSection);

            Foldout selection = new Foldout
            {
                text = "选中方块",
                value = true
            };
            selectionLabel = new Label("未选中");
            selectionLabel.style.whiteSpace = WhiteSpace.Normal;
            selection.Add(selectionLabel);
            VisualElement selectionActions = Row();
            replaceButton = ActionButton("替换为当前", ReplaceSelected);
            removeButton = ActionButton("删除选中", RemoveSelected);
            selectionActions.Add(replaceButton);
            selectionActions.Add(removeButton);
            selection.Add(selectionActions);
            body.Add(selection);
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
            playerField.SetValueWithoutNotify(player);
            playerPlaneLockToggle.SetValueWithoutNotify(
                LevelEditorState.LockPlayerToPlane);
            playerPlaneLockToggle.SetEnabled(player != null);
            playerToolButton.SetEnabled(player != null);
            playerStatusLabel.text = player == null
                ? "未引用场景玩家。"
                : $"已引用：{player.name} · " +
                  (LevelEditorState.LockPlayerToPlane
                      ? "编辑时锁定 Z=0"
                      : "未锁定编辑平面");
            GameObject generationParent =
                LevelEditorState.GenerationParent;
            generationParentField.SetValueWithoutNotify(
                generationParent);
            generationParentStatusLabel.text =
                generationParent == null
                    ? "未指定：新方块放在 __LevelEditorContent。"
                    : $"已指定：新方块放在 {generationParent.name} 下。";
            selectButton.EnableInClassList(
                "selected-tool",
                LevelEditorState.Tool == LevelEditorTool.Select);
            paintButton.EnableInClassList(
                "selected-tool",
                LevelEditorState.Tool == LevelEditorTool.Paint);
            eraseButton.EnableInClassList(
                "selected-tool",
                LevelEditorState.Tool == LevelEditorTool.Erase);
            RefreshPalette();
            RefreshSelection();
            paletteScaleLabel.text =
                $"{Mathf.RoundToInt(LevelEditorState.PaletteScale * 100f)}%";
            refreshing = false;
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

        private void SetPlayer(GameObject value)
        {
            if (value != null && !value.scene.IsValid())
            {
                value = null;
            }

            LevelEditorState.Player = value;
            playerField.SetValueWithoutNotify(value);
            if (value == null &&
                LevelEditorState.Tool == LevelEditorTool.Player)
            {
                LevelEditorState.Tool = LevelEditorTool.Select;
            }

            if (value != null && LevelEditorState.EditMode)
            {
                Selection.activeGameObject = value;
                LevelEditorPlayerService.EnforceEditPlane();
            }

            SceneView.RepaintAll();
        }

        private void UseSelectedAsPlayer()
        {
            SetPlayer(Selection.activeGameObject);
        }

        private void SetGenerationParent(GameObject value)
        {
            if (value != null && !value.scene.IsValid())
            {
                value = null;
            }

            LevelEditorState.GenerationParent = value;
            generationParentField.SetValueWithoutNotify(value);
            generationParentStatusLabel.text = value == null
                ? "未指定：新方块放在 __LevelEditorContent。"
                : $"已指定：新方块放在 {value.name} 下。";
            SceneView.RepaintAll();
        }

        private void UseSelectedAsGenerationParent()
        {
            SetGenerationParent(Selection.activeGameObject);
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
                .7f,
                1.5f);
            float cardWidth = 124f * scale;
            float cardHeight = 148f * scale;
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
                    float actionHeight = Mathf.Clamp(
                        24f * scale,
                        18f,
                        36f);
                    float actionWidth = Mathf.Clamp(
                        44f * scale,
                        32f,
                        66f);
                    float actionFontSize = Mathf.Clamp(
                        12f * scale,
                        10f,
                        16f);
                    float actionInset = Mathf.Clamp(
                        2f * scale,
                        1.5f,
                        3f);
                    float actionGap = Mathf.Clamp(
                        4f * scale,
                        3f,
                        6f);
                    float previewHorizontalInset =
                        (124f - 98f) * .5f * scale;

                    VisualElement cardActions = new VisualElement();
                    cardActions.style.position = Position.Absolute;
                    cardActions.style.left = previewHorizontalInset;
                    cardActions.style.width = 98f * scale;
                    cardActions.style.top =
                        6f * scale + actionInset;
                    cardActions.style.flexDirection = FlexDirection.Row;
                    cardActions.style.justifyContent = Justify.Center;

                    Button editButton = new Button(() =>
                        LevelEditorEntryWindow.OpenForEdit(captured))
                    {
                        text = "编辑"
                    };
                    editButton.style.height = actionHeight;
                    editButton.style.width = actionWidth;
                    editButton.style.fontSize = actionFontSize;
                    editButton.style.unityFontStyleAndWeight =
                        FontStyle.Bold;
                    editButton.style.paddingLeft = 0f;
                    editButton.style.paddingRight = 0f;
                    editButton.style.backgroundColor =
                        new Color(.16f, .43f, .82f, 1f);
                    editButton.style.color = Color.white;
                    editButton.style.marginRight = actionGap;
                    cardActions.Add(editButton);

                    Button deleteButton = new Button(() =>
                        RemovePaletteEntry(captured))
                    {
                        text = "删除"
                    };
                    deleteButton.style.height = actionHeight;
                    deleteButton.style.width = actionWidth;
                    deleteButton.style.fontSize = actionFontSize;
                    deleteButton.style.unityFontStyleAndWeight =
                        FontStyle.Bold;
                    deleteButton.style.paddingLeft = 0f;
                    deleteButton.style.paddingRight = 0f;
                    deleteButton.style.backgroundColor =
                        new Color(.62f, .18f, .16f, 1f);
                    deleteButton.style.color = Color.white;
                    cardActions.Add(deleteButton);
                    cell.Add(cardActions);
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

            paletteList.Add(CreateAddCard(cardWidth, cardHeight));
        }

        private static VisualElement CreateAddCard(
            float width,
            float height)
        {
            VisualElement cell = new VisualElement();
            cell.style.width = width;
            cell.style.height = height;
            cell.style.marginRight = 6f;
            cell.style.marginBottom = 6f;

            Button button = new Button(LevelEditorEntryWindow.OpenForAdd)
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
                new Color(.3f, .66f, 1f, 1f);

            Label plus = new Label("+");
            plus.style.fontSize = 34f;
            plus.style.unityFontStyleAndWeight = FontStyle.Bold;
            plus.style.unityTextAlign = TextAnchor.MiddleCenter;
            plus.pickingMode = PickingMode.Ignore;
            button.Add(plus);

            Label caption = new Label("新增");
            caption.style.fontSize = 12f;
            caption.style.unityTextAlign = TextAnchor.MiddleCenter;
            caption.pickingMode = PickingMode.Ignore;
            button.Add(caption);
            cell.Add(button);
            return cell;
        }

        private static Texture2D ResolvePreview(LevelEditorBlockEntry entry)
        {
            if (!entry.UsesPrefabDirectly &&
                entry.HasDecoration &&
                entry.SurfaceTilePlacements.Count > 0)
            {
                string key = GetDecorationPreviewKey(entry);
                if (DecorationPreviewCache.TryGetValue(
                        key,
                        out Texture2D decorated) &&
                    decorated != null)
                {
                    return decorated;
                }

                decorated = LevelEditorDecorationService
                    .RenderEntryPreview(entry, 128);
                if (decorated != null)
                {
                    DecorationPreviewCache[key] = decorated;
                    return decorated;
                }
            }

            if (!entry.UsesPrefabDirectly &&
                entry.Prefab == null &&
                entry.CustomTemplate != null)
            {
                return GetColorPreview(entry.Color);
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

        private void RefreshSelection()
        {
            LevelEditorPlacedBlock selected =
                LevelEditorBlockFactory.SelectedPlacedBlock();
            selectionLabel.text = selected == null
                ? "未选中已放置方块"
                : $"已选：{selected.name} · 格子 {selected.Cell}";
            replaceButton.SetEnabled(selected != null);
            removeButton.SetEnabled(selected != null);
        }

        private void AdjustPaletteScale(float delta)
        {
            float next = Mathf.Clamp(
                LevelEditorState.PaletteScale + delta,
                .7f,
                1.5f);
            if (Mathf.Approximately(next, LevelEditorState.PaletteScale))
            {
                return;
            }

            LevelEditorState.PaletteScale = next;
            lastPaletteCount = -1;
            RefreshPalette();
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

        private void ReplaceSelected()
        {
            LevelEditorPalette palette = LevelEditorState.Palette;
            int index = LevelEditorState.SelectedEntryIndex;
            if (palette == null || index < 0 ||
                index >= palette.Entries.Count)
            {
                return;
            }

            LevelEditorBlockFactory.ReplaceSelected(
                palette.Entries[index]);
            SceneView.RepaintAll();
        }

        private void RemoveSelected()
        {
            LevelEditorBlockFactory.RemoveSelected();
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
