using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Project.SurfaceTiles.Editor
{
    public sealed class SurfaceTilePaintEditorWindow : EditorWindow
    {
        private SurfaceTileBlock block;
        private Action onClosed;
        private HudElements elements;
        private double nextRefreshTime;

        public static void Open(
            SurfaceTileBlock value,
            Action onClosed = null)
        {
            SurfaceTilePaintEditorWindow window =
                GetWindow<SurfaceTilePaintEditorWindow>(
                    "栏目贴画编辑器");
            window.minSize = new Vector2(560f, 720f);
            window.titleContent = new GUIContent("栏目贴画编辑器");
            window.block = value;
            window.onClosed = onClosed;
            SurfaceTileSceneHud.SetSuppressed(true);
            SurfaceTileEditorState.Painting = false;
            window.Show();
            window.elements?.Refresh();
            if (value != null)
            {
                Selection.activeGameObject = value.gameObject;
                SceneView.lastActiveSceneView?.FrameSelected();
            }

            SceneView.RepaintAll();
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.style.paddingLeft = 8f;
            root.style.paddingRight = 8f;
            root.style.paddingTop = 6f;
            root.style.paddingBottom = 6f;
            elements = new HudElements(root, this);
            elements.Refresh();
        }

        private void OnEnable()
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Tick;
            SurfaceTileSceneHud.SetSuppressed(false);
            SurfaceTileEditorState.Painting = false;
            SurfaceTileEditorState.UseForcedFace = false;
            Action callback = onClosed;
            onClosed = null;
            callback?.Invoke();
            SceneView.RepaintAll();
        }

        private void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            if (elements == null || now < nextRefreshTime)
            {
                return;
            }

            nextRefreshTime = now + .1d;
            elements.Refresh();
        }

        internal SurfaceTileBlock TargetBlock => block;

        internal void SaveAndClose()
        {
            SurfaceTileEditorState.Painting = false;
            Action callback = onClosed;
            onClosed = null;
            callback?.Invoke();
            Close();
        }

        private static void Run(Action action)
        {
            EditorApplication.delayCall += () =>
            {
                action();
                SceneView.RepaintAll();
            };
        }

        private sealed class HudElements
        {
            private readonly SurfaceTilePaintEditorWindow window;
            private readonly VisualElement panel;
            private readonly VisualElement body;
            private readonly Label selectionStatus;
            private readonly ObjectField paletteField;
            private readonly FloatField cellSizeField;
            private readonly Toggle transparentBaseToggle;
            private readonly Button paintToggle;
            private readonly VisualElement tileGrid;
            private readonly Label tileHint;
            private readonly Label tileSizeStatus;
            private readonly Toggle stackToggle;
            private readonly FloatField offsetXField;
            private readonly FloatField offsetYField;
            private readonly FloatField nudgeStepField;
            private readonly Label bakeStatus;
            private SurfaceTilePalette displayedPalette;
            private SurfaceTileBlock currentBlock;
            private readonly Dictionary<string, Button> tileButtons =
                new Dictionary<string, Button>();
            private readonly Dictionary<SurfaceTileAnchor, Button> anchorButtons =
                new Dictionary<SurfaceTileAnchor, Button>();
            private readonly Dictionary<SurfaceTileFace, Button> faceButtons =
                new Dictionary<SurfaceTileFace, Button>();
            private Button autoFaceButton;
            private bool refreshing;

            internal HudElements(
                VisualElement root,
                SurfaceTilePaintEditorWindow owner)
            {
                window = owner;
                panel = new VisualElement { pickingMode = PickingMode.Position };
                panel.style.width = Length.Percent(100f);
                panel.style.flexGrow = 1f;
                panel.style.paddingLeft = 10f;
                panel.style.paddingRight = 10f;
                panel.style.paddingTop = 8f;
                panel.style.paddingBottom = 8f;
                root.Add(panel);

                Label workflow = new Label(
                    "选瓦片 → 开始绘制 → 保存并关闭");
                workflow.style.fontSize = 11f;
                workflow.style.opacity = .75f;
                panel.Add(workflow);

                body = new ScrollView(ScrollViewMode.Vertical);
                body.style.flexGrow = 1f;
                body.style.marginTop = 5f;
                panel.Add(body);

                selectionStatus = Badge("栏目预览方块");
                body.Add(selectionStatus);
                body.Add(Button("聚焦预览方块", FocusCurrentBlock));

                Foldout tileStep = new Foldout
                {
                    text = "1. 选瓦片",
                    value = true
                };
                paletteField = new ObjectField("瓦片库")
                {
                    objectType = typeof(SurfaceTilePalette),
                    allowSceneObjects = false
                };
                paletteField.RegisterValueChangedCallback(evt =>
                {
                    if (!refreshing && CurrentBlock() != null)
                    {
                        ConfigureBlock((SurfaceTilePalette)evt.newValue, null);
                    }
                });
                tileStep.Add(paletteField);
                tileHint = new Label(
                    "先在 Project 里选择切好的 Sprite，再创建瓦片库。");
                tileHint.style.whiteSpace = WhiteSpace.Normal;
                tileStep.Add(tileHint);
                tileGrid = new VisualElement();
                tileGrid.style.flexDirection = FlexDirection.Row;
                tileGrid.style.flexWrap = Wrap.Wrap;
                ScrollView tileScroll = new ScrollView();
                tileScroll.style.height = 240f;
                tileScroll.Add(tileGrid);
                tileStep.Add(tileScroll);
                tileSizeStatus = Badge("选择瓦片后显示实际尺寸");
                tileStep.Add(tileSizeStatus);
                tileStep.Add(Button("导入不规则瓦片图…", () =>
                    SurfaceTileSheetImporterWindow.OpenWindow()));
                body.Add(tileStep);

                Foldout paintStep = new Foldout
                {
                    text = "2. 开始绘制",
                    value = true
                };
                paintToggle = BigButton("开始绘制", TogglePainting);
                paintStep.Add(paintToggle);
                paintStep.Add(Section("绘制面"));
                VisualElement faceRowA = Row();
                autoFaceButton = Button("自动", () => SetForcedFace(null));
                faceRowA.Add(autoFaceButton);
                AddFaceButton(faceRowA, SurfaceTileFace.Front, "正面");
                AddFaceButton(faceRowA, SurfaceTileFace.Right, "右面");
                AddFaceButton(faceRowA, SurfaceTileFace.Back, "后面");
                paintStep.Add(faceRowA);
                VisualElement faceRowB = Row();
                AddFaceButton(faceRowB, SurfaceTileFace.Left, "左面");
                AddFaceButton(faceRowB, SurfaceTileFace.Top, "顶面");
                AddFaceButton(faceRowB, SurfaceTileFace.Bottom, "底面");
                paintStep.Add(faceRowB);
                VisualElement modeRow = Row();
                modeRow.Add(Button("画", () => SurfaceTileEditorState.Mode =
                    SurfaceTilePaintMode.Paint));
                modeRow.Add(Button("擦", () => SurfaceTileEditorState.Mode =
                    SurfaceTilePaintMode.Erase));
                modeRow.Add(Button("吸取", () => SurfaceTileEditorState.Mode =
                    SurfaceTilePaintMode.Pick));
                paintStep.Add(modeRow);
                Label help = new Label(
                    "选瓦片 → 开始绘制 → 在 Scene 左键贴画。\n" +
                    "Shift+左键擦除 · Ctrl+左键吸取 · Esc退出。\n" +
                    "绘制完成点“保存并关闭”写回栏目。");
                help.style.fontSize = 9f;
                help.style.opacity = 0.72f;
                help.style.whiteSpace = WhiteSpace.Normal;
                help.style.marginTop = 5f;
                paintStep.Add(help);
                paintStep.Add(BigButton(
                    "保存并关闭",
                    window.SaveAndClose));
                body.Add(paintStep);

                Foldout advanced = new Foldout
                {
                    text = "高级设置",
                    value = false
                };
                cellSizeField = new FloatField("每格世界尺寸")
                {
                    isDelayed = true
                };
                cellSizeField.RegisterValueChangedCallback(evt =>
                {
                    if (!refreshing && CurrentBlock() != null)
                    {
                        ConfigureBlock(null, Mathf.Max(0.01f, evt.newValue));
                    }
                });
                advanced.Add(cellSizeField);

                transparentBaseToggle = new Toggle(
                    "透明底（隐藏原模型颜色，只显示贴画）");
                transparentBaseToggle.RegisterValueChangedCallback(evt =>
                {
                    if (!refreshing)
                    {
                        SetTransparentBase(evt.newValue);
                    }
                });
                advanced.Add(transparentBaseToggle);

                stackToggle = new Toggle("叠加到已有贴画（保留透明处底图）")
                {
                    value = SurfaceTileEditorState.Stack
                };
                stackToggle.RegisterValueChangedCallback(evt =>
                    SurfaceTileEditorState.Stack = evt.newValue);
                advanced.Add(stackToggle);

                advanced.Add(Button("从 Project 选中切片创建瓦片库", () => Run(() =>
                {
                    SurfaceTileBlock block = CurrentBlock();
                    SurfaceTilePalette palette = SurfaceTileAuthoringService
                        .CreatePaletteFromSelection();
                    if (palette != null && block != null)
                    {
                        ConfigureBlock(palette, null);
                    }
                })));
                advanced.Add(Section("摆放锚点"));
                VisualElement anchorRowA = Row();
                AddAnchorButton(anchorRowA, SurfaceTileAnchor.BottomLeft, "起点铺开");
                AddAnchorButton(anchorRowA, SurfaceTileAnchor.Center, "格内居中");
                AddAnchorButton(anchorRowA, SurfaceTileAnchor.BottomEdge, "贴下边");
                advanced.Add(anchorRowA);
                VisualElement anchorRowB = Row();
                AddAnchorButton(anchorRowB, SurfaceTileAnchor.TopEdge, "贴上边");
                AddAnchorButton(anchorRowB, SurfaceTileAnchor.LeftEdge, "贴左边");
                AddAnchorButton(anchorRowB, SurfaceTileAnchor.RightEdge, "贴右边");
                advanced.Add(anchorRowB);

                advanced.Add(Section("微调位置（格）"));
                Label offsetHint = new Label(
                    "水平 X / 垂直 Y；正 Y 可把草沿表面往上抬。允许略微伸出方块边缘。");
                offsetHint.style.fontSize = 9f;
                offsetHint.style.opacity = 0.72f;
                offsetHint.style.whiteSpace = WhiteSpace.Normal;
                advanced.Add(offsetHint);
                VisualElement offsetRow = Row();
                offsetXField = new FloatField("X") { isDelayed = false };
                offsetXField.style.flexGrow = 1f;
                offsetXField.RegisterValueChangedCallback(evt =>
                {
                    if (!refreshing)
                    {
                        SetOffset(new Vector2(
                            evt.newValue,
                            SurfaceTileEditorState.OffsetCells.y));
                    }
                });
                offsetYField = new FloatField("Y") { isDelayed = false };
                offsetYField.style.flexGrow = 1f;
                offsetYField.RegisterValueChangedCallback(evt =>
                {
                    if (!refreshing)
                    {
                        SetOffset(new Vector2(
                            SurfaceTileEditorState.OffsetCells.x,
                            evt.newValue));
                    }
                });
                offsetRow.Add(offsetXField);
                offsetRow.Add(offsetYField);
                advanced.Add(offsetRow);
                nudgeStepField = new FloatField("按钮步长") { isDelayed = true };
                nudgeStepField.RegisterValueChangedCallback(evt =>
                {
                    if (!refreshing)
                    {
                        SurfaceTileEditorState.OffsetNudgeStep =
                            Mathf.Clamp(Mathf.Abs(evt.newValue), 0.001f, 1f);
                        nudgeStepField.SetValueWithoutNotify(
                            SurfaceTileEditorState.OffsetNudgeStep);
                    }
                });
                advanced.Add(nudgeStepField);
                VisualElement nudgeRow = Row();
                nudgeRow.Add(Button("←", () => NudgeOffset(Vector2.left)));
                nudgeRow.Add(Button("↓", () => NudgeOffset(Vector2.down)));
                nudgeRow.Add(Button("归零", () => SetOffset(Vector2.zero)));
                nudgeRow.Add(Button("↑", () => NudgeOffset(Vector2.up)));
                nudgeRow.Add(Button("→", () => NudgeOffset(Vector2.right)));
                advanced.Add(nudgeRow);
                VisualElement transformRow = Row();
                transformRow.Add(Button("↻ 旋转", () =>
                {
                    SurfaceTileEditorState.QuarterTurns =
                        (SurfaceTileEditorState.QuarterTurns + 1) % 4;
                    UpdateTileSelection();
                    SceneView.RepaintAll();
                }));
                transformRow.Add(Button("↔ 翻转", () =>
                    SurfaceTileEditorState.FlipX = !SurfaceTileEditorState.FlipX));
                transformRow.Add(Button("↕ 翻转", () =>
                    SurfaceTileEditorState.FlipY = !SurfaceTileEditorState.FlipY));
                advanced.Add(transformRow);

                advanced.Add(Section("发布优化"));
                bakeStatus = Badge("尚未合成");
                advanced.Add(bakeStatus);
                advanced.Add(BigButton("合成贴图并持久化保存", () => Run(() =>
                {
                    SurfaceTileBlock block = CurrentBlock();
                    if (block == null)
                    {
                        return;
                    }

                    SurfaceTileAssetBaker.Bake(block, out string message);
                    EditorUtility.DisplayDialog("方块贴画", message, "确定");
                })));
                body.Add(advanced);
            }

            internal void Refresh()
            {
                SurfaceTileBlock selected = SelectedBlock();
                if (selected != null)
                {
                    currentBlock = selected;
                }

                SurfaceTileBlock block = CurrentBlock();
                if (block != null &&
                    !block.BakeUpToDate &&
                    block.BakedMesh != null &&
                    block.OutputFilter != null &&
                    block.OutputFilter.sharedMesh == block.BakedMesh)
                {
                    SurfaceTileMeshBuilder.RefreshPreview(block);
                }

                refreshing = true;
                selectionStatus.text = block == null
                    ? "未选中可贴画方块"
                    : $"已选：{block.name} · {block.Placements.Count} 层贴画";
                selectionStatus.style.backgroundColor = block == null
                    ? new Color(0.35f, 0.2f, 0.12f, 0.75f)
                    : new Color(0.08f, 0.35f, 0.27f, 0.8f);
                paletteField.SetValueWithoutNotify(block != null ? block.Palette : null);
                cellSizeField.SetValueWithoutNotify(block != null ? block.CellSize : 1f);
                transparentBaseToggle.SetValueWithoutNotify(
                    block != null && block.TransparentBase);
                transparentBaseToggle.SetEnabled(
                    block != null && block.SourceRenderer != null);
                stackToggle.SetValueWithoutNotify(SurfaceTileEditorState.Stack);
                offsetXField.SetValueWithoutNotify(
                    SurfaceTileEditorState.OffsetCells.x);
                offsetYField.SetValueWithoutNotify(
                    SurfaceTileEditorState.OffsetCells.y);
                nudgeStepField.SetValueWithoutNotify(
                    SurfaceTileEditorState.OffsetNudgeStep);
                paintToggle.SetEnabled(block != null && block.Palette != null);
                paintToggle.text = SurfaceTileEditorState.Painting
                    ? "■ 退出绘制（Esc）"
                    : "▶ 开始绘制";
                bakeStatus.text = block == null
                    ? "尚未选择方块"
                    : block.BakeUpToDate
                        ? "已合成，可直接用于运行时"
                        : "有未合成改动";

                refreshing = false;

                if (displayedPalette != (block != null ? block.Palette : null))
                {
                    displayedPalette = block != null ? block.Palette : null;
                    RebuildTiles();
                }

                UpdateTileSelection();
                UpdateFaceSelection();
            }

            private void TogglePainting()
            {
                if (SurfaceTileEditorState.Painting)
                {
                    SurfaceTileEditorState.Painting = false;
                    SceneView.RepaintAll();
                    return;
                }

                SurfaceTileBlock block = CurrentBlock();
                if (block == null ||
                    block.Palette == null ||
                    block.Palette.Tiles.Count == 0)
                {
                    selectionStatus.text = "请先指定带瓦片的瓦片库。";
                    selectionStatus.style.backgroundColor =
                        new Color(0.35f, 0.2f, 0.12f, 0.75f);
                    return;
                }

                if (string.IsNullOrEmpty(
                        SurfaceTileEditorState.SelectedTileId) ||
                    !block.Palette.TryGet(
                        SurfaceTileEditorState.SelectedTileId,
                        out _))
                {
                    SurfaceTileEditorState.SelectedTileId =
                        block.Palette.Tiles[0].Id;
                }

                SurfaceTileEditorState.Painting = true;
                SurfaceTileEditorState.Mode = SurfaceTilePaintMode.Paint;
                FocusCurrentBlock();
                SceneView.RepaintAll();
            }

            private void FocusCurrentBlock()
            {
                SurfaceTileBlock block = CurrentBlock();
                if (block == null)
                {
                    return;
                }

                Selection.activeGameObject = block.gameObject;
                SceneView.lastActiveSceneView?.LookAt(
                    block.transform.position,
                    Quaternion.Euler(30f, -45f, 0f),
                    2.5f);
                SceneView.lastActiveSceneView?.Focus();
                SceneView.RepaintAll();
            }

            private void ConfigureBlock(
                SurfaceTilePalette palette,
                float? cellSize)
            {
                SurfaceTileBlock block = CurrentBlock();
                if (block == null)
                {
                    return;
                }

                Undo.RecordObject(block, "配置方块贴画");
                SurfaceTilePalette finalPalette = palette != null
                    ? palette
                    : block.Palette;
                block.Configure(finalPalette, cellSize ?? block.CellSize);
                block.RemoveOutOfBoundsTiles();
                if (finalPalette != null && finalPalette.Tiles.Count > 0 &&
                    !finalPalette.TryGet(SurfaceTileEditorState.SelectedTileId, out _))
                {
                    SurfaceTileEditorState.SelectedTileId = finalPalette.Tiles[0].Id;
                }

                SurfaceTileMeshBuilder.RefreshPreview(block);
                EditorUtility.SetDirty(block);
                EditorSceneManager.MarkSceneDirty(block.gameObject.scene);
                displayedPalette = null;
            }

            private void SetTransparentBase(bool value)
            {
                SurfaceTileBlock block = CurrentBlock();
                if (block == null)
                {
                    return;
                }

                Renderer sourceRenderer = block.SourceRenderer;
                Undo.RecordObject(block, "切换方块贴画透明底");
                if (sourceRenderer != null)
                {
                    Undo.RecordObject(sourceRenderer, "切换方块贴画透明底");
                }

                block.SetTransparentBase(value);
                EditorUtility.SetDirty(block);
                if (sourceRenderer != null)
                {
                    EditorUtility.SetDirty(sourceRenderer);
                }

                EditorSceneManager.MarkSceneDirty(block.gameObject.scene);
                SceneView.RepaintAll();
            }

            private SurfaceTileBlock CurrentBlock()
            {
                return currentBlock != null ? currentBlock : SelectedBlock();
            }

            private SurfaceTileBlock SelectedBlock()
            {
                return window != null ? window.TargetBlock : null;
            }

            private void RebuildTiles()
            {
                tileGrid.Clear();
                tileButtons.Clear();
                tileHint.style.display = displayedPalette == null ||
                                         displayedPalette.Tiles.Count == 0
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
                if (displayedPalette == null)
                {
                    return;
                }

                foreach (SurfaceTilePalette.Entry tile in displayedPalette.Tiles)
                {
                    if (tile?.Sprite == null)
                    {
                        continue;
                    }

                    string tileId = tile.Id;
                    Button button = new Button(() =>
                    {
                        SurfaceTileEditorState.SelectedTileId = tileId;
                        SurfaceTileEditorState.Mode = SurfaceTilePaintMode.Paint;
                    })
                    {
                        tooltip = $"{tile.DisplayName} · " +
                                  $"{FormatSize(tile.SizeInCells.x)}×" +
                                  $"{FormatSize(tile.SizeInCells.y)} 格"
                    };
                    button.style.width = 62f;
                    button.style.height = 62f;
                    button.style.marginLeft = 2f;
                    button.style.marginRight = 2f;
                    button.style.marginTop = 2f;
                    button.style.marginBottom = 2f;
                    button.style.backgroundImage = new StyleBackground(tile.Sprite);
                    tileGrid.Add(button);
                    tileButtons[tileId] = button;
                }
            }

            private void UpdateTileSelection()
            {
                foreach (KeyValuePair<string, Button> pair in tileButtons)
                {
                    Color color = pair.Key == SurfaceTileEditorState.SelectedTileId
                        ? new Color(0.15f, 0.78f, 1f, 1f)
                        : new Color(0f, 0f, 0f, 0.35f);
                    pair.Value.style.borderLeftColor = color;
                    pair.Value.style.borderRightColor = color;
                    pair.Value.style.borderTopColor = color;
                    pair.Value.style.borderBottomColor = color;
                    pair.Value.style.borderLeftWidth = 2f;
                    pair.Value.style.borderRightWidth = 2f;
                    pair.Value.style.borderTopWidth = 2f;
                    pair.Value.style.borderBottomWidth = 2f;
                }

                foreach (KeyValuePair<SurfaceTileAnchor, Button> pair in anchorButtons)
                {
                    pair.Value.style.backgroundColor =
                        pair.Key == SurfaceTileEditorState.Anchor
                            ? new Color(0.12f, 0.52f, 0.82f, 1f)
                            : new Color(0f, 0f, 0f, 0.18f);
                }

                if (displayedPalette != null && displayedPalette.TryGet(
                        SurfaceTileEditorState.SelectedTileId,
                        out SurfaceTilePalette.Entry selected))
                {
                    Vector2 rotated = SurfaceTileGeometry.GetRotatedSize(
                        selected,
                        SurfaceTileEditorState.QuarterTurns);
                    Vector2Int footprint = new Vector2Int(
                        Mathf.CeilToInt(rotated.x - 0.0001f),
                        Mathf.CeilToInt(rotated.y - 0.0001f));
                    tileSizeStatus.text =
                        $"显示尺寸 {FormatSize(rotated.x)}×{FormatSize(rotated.y)} 格" +
                        $" · 占用 {footprint.x}×{footprint.y} 格" +
                        $" · 偏移 {SurfaceTileEditorState.OffsetCells.x:0.##}, " +
                        $"{SurfaceTileEditorState.OffsetCells.y:0.##}";
                }
                else
                {
                    tileSizeStatus.text = "选择瓦片后显示实际尺寸";
                }
            }

            private void AddAnchorButton(
                VisualElement row,
                SurfaceTileAnchor anchor,
                string text)
            {
                Button button = Button(text, () =>
                {
                    SurfaceTileEditorState.Anchor = anchor;
                    UpdateTileSelection();
                    SceneView.RepaintAll();
                });
                row.Add(button);
                anchorButtons[anchor] = button;
            }

            private void AddFaceButton(
                VisualElement row,
                SurfaceTileFace face,
                string text)
            {
                Button button = Button(text, () => SetForcedFace(face));
                row.Add(button);
                faceButtons[face] = button;
            }

            private void SetForcedFace(SurfaceTileFace? face)
            {
                SurfaceTileEditorState.UseForcedFace = face.HasValue;
                if (face.HasValue)
                {
                    SurfaceTileEditorState.ForcedFace = face.Value;
                    FocusFace(face.Value);
                }

                UpdateFaceSelection();
                SceneView.RepaintAll();
            }

            private void FocusFace(SurfaceTileFace face)
            {
                SurfaceTileBlock block = CurrentBlock();
                if (block == null || SceneView.lastActiveSceneView == null)
                {
                    return;
                }

                Vector3 up = Vector3.up;
                Quaternion rotation;
                switch (face)
                {
                    case SurfaceTileFace.Right:
                        rotation = Quaternion.LookRotation(Vector3.left, up);
                        break;
                    case SurfaceTileFace.Back:
                        rotation = Quaternion.LookRotation(Vector3.back, up);
                        break;
                    case SurfaceTileFace.Left:
                        rotation = Quaternion.LookRotation(Vector3.right, up);
                        break;
                    case SurfaceTileFace.Top:
                        rotation = Quaternion.LookRotation(
                            Vector3.down,
                            Vector3.forward);
                        break;
                    case SurfaceTileFace.Bottom:
                        rotation = Quaternion.LookRotation(
                            Vector3.up,
                            Vector3.forward);
                        break;
                    default:
                        rotation = Quaternion.LookRotation(
                            Vector3.forward,
                            up);
                        break;
                }

                SceneView.lastActiveSceneView.LookAt(
                    block.transform.position,
                    rotation,
                    2f);
            }

            private void UpdateFaceSelection()
            {
                Color selectedColor =
                    new Color(0.12f, 0.52f, 0.82f, 1f);
                Color normalColor =
                    new Color(0f, 0f, 0f, 0.18f);
                if (autoFaceButton != null)
                {
                    autoFaceButton.style.backgroundColor =
                        !SurfaceTileEditorState.UseForcedFace
                            ? selectedColor
                            : normalColor;
                }

                foreach (
                    KeyValuePair<SurfaceTileFace, Button> pair
                    in faceButtons)
                {
                    pair.Value.style.backgroundColor =
                        SurfaceTileEditorState.UseForcedFace &&
                        pair.Key == SurfaceTileEditorState.ForcedFace
                            ? selectedColor
                            : normalColor;
                }
            }

            private void NudgeOffset(Vector2 direction)
            {
                SetOffset(
                    SurfaceTileEditorState.OffsetCells +
                    direction * SurfaceTileEditorState.OffsetNudgeStep);
            }

            private void SetOffset(Vector2 value)
            {
                float limit = SurfaceTileBlock.MaximumOffsetCells;
                SurfaceTileEditorState.OffsetCells = new Vector2(
                    Mathf.Clamp(value.x, -limit, limit),
                    Mathf.Clamp(value.y, -limit, limit));
                offsetXField.SetValueWithoutNotify(
                    SurfaceTileEditorState.OffsetCells.x);
                offsetYField.SetValueWithoutNotify(
                    SurfaceTileEditorState.OffsetCells.y);
                UpdateTileSelection();
                SceneView.RepaintAll();
            }

            private static string FormatSize(float value)
            {
                return Mathf.Approximately(value, Mathf.Round(value))
                    ? Mathf.RoundToInt(value).ToString()
                    : value.ToString("0.##");
            }

            private static VisualElement Row()
            {
                VisualElement row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                return row;
            }

            private static Label Section(string text)
            {
                Label label = new Label(text);
                label.style.unityFontStyleAndWeight = FontStyle.Bold;
                label.style.marginTop = 7f;
                return label;
            }

            private static Label Badge(string text)
            {
                Label label = new Label(text);
                label.style.paddingLeft = 7f;
                label.style.paddingRight = 7f;
                label.style.paddingTop = 4f;
                label.style.paddingBottom = 4f;
                label.style.marginTop = 5f;
                label.style.marginBottom = 4f;
                label.style.backgroundColor = new Color(0.16f, 0.2f, 0.26f, 0.85f);
                Round(label, 4f);
                return label;
            }

            private static Button BigButton(string text, Action action)
            {
                Button button = Button(text, action);
                button.style.height = 36f;
                button.style.unityFontStyleAndWeight = FontStyle.Bold;
                button.style.backgroundColor = new Color(0.12f, 0.52f, 0.82f, 1f);
                button.style.color = Color.white;
                return button;
            }

            private static Button Button(string text, Action action)
            {
                Button button = new Button(action) { text = text };
                button.style.flexGrow = 1f;
                button.style.height = 25f;
                button.style.marginLeft = 2f;
                button.style.marginRight = 2f;
                button.style.marginTop = 2f;
                button.style.marginBottom = 2f;
                return button;
            }

            private static Button SmallButton(string text, Action action)
            {
                Button button = action == null ? new Button() : new Button(action);
                button.text = text;
                button.style.width = 25f;
                button.style.height = 22f;
                button.style.marginLeft = 3f;
                return button;
            }

            private static void Round(VisualElement element, float radius)
            {
                element.style.borderTopLeftRadius = radius;
                element.style.borderTopRightRadius = radius;
                element.style.borderBottomLeftRadius = radius;
                element.style.borderBottomRightRadius = radius;
            }

            private static void MakeDraggable(
                VisualElement target,
                VisualElement handle)
            {
                bool dragging = false;
                int pointerId = -1;
                Vector2 last = Vector2.zero;
                handle.RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (evt.button != 0 || evt.target is Button)
                    {
                        return;
                    }

                    Rect layout = target.layout;
                    target.style.left = layout.x;
                    target.style.top = layout.y;
                    target.style.right = StyleKeyword.Auto;
                    dragging = true;
                    pointerId = evt.pointerId;
                    last = evt.position;
                    handle.CapturePointer(pointerId);
                    evt.StopPropagation();
                });
                handle.RegisterCallback<PointerMoveEvent>(evt =>
                {
                    if (!dragging || evt.pointerId != pointerId)
                    {
                        return;
                    }

                    Vector2 current = evt.position;
                    Vector2 delta = current - last;
                    last = current;
                    target.style.left = target.layout.x + delta.x;
                    target.style.top = target.layout.y + delta.y;
                    evt.StopPropagation();
                });
                handle.RegisterCallback<PointerUpEvent>(evt =>
                {
                    if (!dragging || evt.pointerId != pointerId)
                    {
                        return;
                    }

                    dragging = false;
                    handle.ReleasePointer(pointerId);
                    pointerId = -1;
                    evt.StopPropagation();
                });
                handle.RegisterCallback<PointerCaptureOutEvent>(_ =>
                {
                    dragging = false;
                    pointerId = -1;
                });
            }
        }
    }
}
