using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Project.SurfaceTiles.Editor
{
    internal sealed class SurfaceTileSheetImporterWindow : EditorWindow
    {
        private enum CanvasMode
        {
            Draw = 0,
            Remove = 1
        }

        private Texture2D sourceAsset;
        private Texture2D sourcePreview;
        private SurfaceTileSheetImportRecipe recipe;
        private float zoom = 0.35f;
        private CanvasMode canvasMode;
        private int selectedIndex = -1;
        private bool dragging;
        private Vector2 dragStart;
        private Vector2 dragCurrent;
        private int minimumOpaquePixels = 64;
        private int mergeGap = 18;
        private int detectionMargin = 2;
        private bool includeSeparatePieces;
        private int separatePieceMinimumPixels = 4;
        private string status = "选择源图片后，在图片上拖框即可创建瓦片。";
        private VisualElement tileCanvas, settingsPanel, regionPanel;
        private Label statusLabel;

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.paddingLeft = rootVisualElement.style.paddingRight = 8;
            var sourceField = new ObjectField("源图片") { objectType = typeof(Texture2D), allowSceneObjects = false, value = sourceAsset };
            sourceField.RegisterValueChangedCallback(e => { SetSource(e.newValue as Texture2D); RefreshToolkit(); });
            rootVisualElement.Add(sourceField);
            var toolbar = new VisualElement(); toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.Add(new Button(() => { AutoDetect(); RefreshToolkit(); }) { text = "识别完整图块" });
            var pieces = new Toggle("追加独立碎片") { value = includeSeparatePieces };
            pieces.RegisterValueChangedCallback(e => includeSeparatePieces = e.newValue); toolbar.Add(pieces);
            toolbar.Add(new Button(() => { canvasMode = canvasMode == CanvasMode.Draw ? CanvasMode.Remove : CanvasMode.Draw; status = canvasMode == CanvasMode.Draw ? "拖框新增；点击选区选中；右键取消选区。" : "点击选区取消；Ctrl+Z 撤销。"; RefreshToolkit(); }) { text = "绘制 / 取消" });
            toolbar.Add(new Button(() => { if (selectedIndex >= 0) RemoveRegion(selectedIndex); RefreshToolkit(); }) { text = "取消选中" });
            toolbar.Add(new Button(() => { if (recipe != null) Generate(); RefreshToolkit(); }) { text = "生成瓦片库" });
            rootVisualElement.Add(toolbar);
            var slider = new Slider("缩放", .05f, 4) { value = zoom };
            slider.RegisterValueChangedCallback(e => { zoom = e.newValue; RefreshCanvas(); }); rootVisualElement.Add(slider);
            var row = new VisualElement(); row.style.flexDirection = FlexDirection.Row; row.style.flexGrow = 1;
            var scroll = new ScrollView(ScrollViewMode.VerticalAndHorizontal); scroll.style.flexGrow = 1;
            tileCanvas = new VisualElement(); tileCanvas.style.position = Position.Relative; tileCanvas.focusable = true;
            tileCanvas.RegisterCallback<PointerDownEvent>(e =>
            {
                if (sourcePreview == null || recipe == null) return;
                tileCanvas.Focus(); Vector2 point = e.localPosition;
                Rect imageRect = new Rect(0, 0, sourcePreview.width * zoom, sourcePreview.height * zoom);
                int index = FindRegionAt(point, imageRect);
                if (index >= 0) { selectedIndex = index; if (e.button == 1 || canvasMode == CanvasMode.Remove) RemoveRegion(index); RefreshToolkit(); e.StopPropagation(); return; }
                if (e.button != 0 || canvasMode == CanvasMode.Remove) return;
                dragging = true; dragStart = dragCurrent = point; tileCanvas.CapturePointer(e.pointerId); RefreshCanvas(); e.StopPropagation();
            });
            tileCanvas.RegisterCallback<PointerMoveEvent>(e => { if (dragging) { dragCurrent = e.localPosition; RefreshCanvas(); e.StopPropagation(); } });
            tileCanvas.RegisterCallback<PointerUpEvent>(e =>
            {
                if (!dragging) return;
                dragCurrent = e.localPosition; dragging = false; tileCanvas.ReleasePointer(e.pointerId);
                Rect area = Rect.MinMaxRect(Mathf.Min(dragStart.x, dragCurrent.x), Mathf.Min(dragStart.y, dragCurrent.y), Mathf.Max(dragStart.x, dragCurrent.x), Mathf.Max(dragStart.y, dragCurrent.y));
                if (area.width >= 2 && area.height >= 2) AddRegion(DisplayToSource(area, new Rect(0, 0, sourcePreview.width * zoom, sourcePreview.height * zoom)));
                RefreshToolkit(); e.StopPropagation();
            });
            tileCanvas.RegisterCallback<KeyDownEvent>(e => { if (e.keyCode == KeyCode.Delete && selectedIndex >= 0) { RemoveRegion(selectedIndex); RefreshToolkit(); e.StopPropagation(); } });
            tileCanvas.RegisterCallback<PointerCaptureOutEvent>(e => { dragging = false; });
            scroll.Add(tileCanvas); row.Add(scroll);
            var sidebar = new ScrollView(); sidebar.style.width = 300; sidebar.style.flexShrink = 0;
            var detection = new Foldout { text = "识别参数", value = false };
            detection.Add(NumberSetting("完整块最少像素", minimumOpaquePixels, value => minimumOpaquePixels = Mathf.Max(1, value)));
            detection.Add(NumberSetting("碎片最少像素", separatePieceMinimumPixels, value => separatePieceMinimumPixels = Mathf.Max(1, value)));
            detection.Add(NumberSetting("合并间距", mergeGap, value => mergeGap = Mathf.Max(0, value)));
            detection.Add(NumberSetting("边缘留白", detectionMargin, value => detectionMargin = Mathf.Max(0, value)));
            sidebar.Add(detection);
            var output = new Foldout { text = "输出设置", value = true }; settingsPanel = new VisualElement(); output.Add(settingsPanel); sidebar.Add(output);
            var regions = new Foldout { text = "瓦片选区", value = true }; regionPanel = new VisualElement(); regions.Add(regionPanel); sidebar.Add(regions);
            row.Add(sidebar); rootVisualElement.Add(row);
            statusLabel = new Label(); statusLabel.style.whiteSpace = WhiteSpace.Normal; rootVisualElement.Add(statusLabel);
            RefreshToolkit();
        }
        private static IntegerField NumberSetting(string label, int value, Action<int> changed)
        {
            var field = new IntegerField(label) { value = value };
            field.RegisterValueChangedCallback(e => changed(e.newValue));
            return field;
        }
        private void RefreshToolkit()
        {
            if (tileCanvas == null) return;
            settingsPanel.Clear(); regionPanel.Clear(); statusLabel.text = status;
            if (recipe != null)
            {
                var serialized = new SerializedObject(recipe);
                foreach (string property in new[] { "outputFolder", "outputName", "outputWidth", "outputHeight", "sourcePixelsPerCell", "transparentPadding", "trimTransparentPixels", "allowUpscale", "anchor" })
                    settingsPanel.Add(new PropertyField(serialized.FindProperty(property)));
                settingsPanel.Bind(serialized);
                for (int i = 0; i < recipe.Regions.Count; i++)
                {
                    int index = i; var region = recipe.Regions[i];
                    var line = new VisualElement(); line.style.flexDirection = FlexDirection.Row;
                    var enabled = new Toggle { value = region.Enabled };
                    enabled.RegisterValueChangedCallback(e => { Undo.RecordObject(recipe, "启用瓦片"); region.Enabled = e.newValue; SaveRecipe(); RefreshCanvas(); }); line.Add(enabled);
                    var select = new Button(() => { selectedIndex = index; RefreshToolkit(); }) { text = region.DisplayName }; select.style.flexGrow = 1; line.Add(select);
                    line.Add(new Button(() => { RemoveRegion(index); RefreshToolkit(); }) { text = "×" }); regionPanel.Add(line);
                    if (index == selectedIndex)
                    {
                        var name = new TextField("名称") { value = region.DisplayName };
                        name.RegisterValueChangedCallback(e => { Undo.RecordObject(recipe, "重命名瓦片"); region.DisplayName = e.newValue; SaveRecipe(); }); regionPanel.Add(name);
                    }
                }
            }
            RefreshCanvas();
        }
        private void RefreshCanvas()
        {
            if (tileCanvas == null) return;
            tileCanvas.Clear();
            if (sourcePreview == null) { tileCanvas.style.width = 500; tileCanvas.style.height = 400; return; }
            float width = sourcePreview.width * zoom, height = sourcePreview.height * zoom;
            tileCanvas.style.width = width; tileCanvas.style.height = height;
            var picture = new Image { image = sourcePreview, scaleMode = ScaleMode.StretchToFill, pickingMode = PickingMode.Ignore };
            picture.style.position = Position.Absolute; picture.style.width = width; picture.style.height = height; tileCanvas.Add(picture);
            if (recipe != null) for (int i = 0; i < recipe.Regions.Count; i++)
                AddOutline(SourceToDisplay(recipe.Regions[i].Rect, new Rect(0, 0, width, height)), i == selectedIndex ? Color.yellow : recipe.Regions[i].Enabled ? Color.green : Color.gray);
            if (dragging) AddOutline(Rect.MinMaxRect(Mathf.Min(dragStart.x, dragCurrent.x), Mathf.Min(dragStart.y, dragCurrent.y), Mathf.Max(dragStart.x, dragCurrent.x), Mathf.Max(dragStart.y, dragCurrent.y)), Color.cyan);
        }
        private void AddOutline(Rect rect, Color color)
        {
            var outline = new VisualElement { pickingMode = PickingMode.Ignore };
            outline.style.position = Position.Absolute; outline.style.left = rect.x; outline.style.top = rect.y;
            outline.style.width = rect.width; outline.style.height = rect.height;
            outline.style.borderLeftWidth = outline.style.borderRightWidth = outline.style.borderTopWidth = outline.style.borderBottomWidth = 2;
            outline.style.borderLeftColor = outline.style.borderRightColor = outline.style.borderTopColor = outline.style.borderBottomColor = color;
            tileCanvas.Add(outline);
        }

        [MenuItem("Tools/2026TapTap/方块贴画/导入不规则瓦片图")]
        internal static void OpenWindow()
        {
            SurfaceTileSheetImporterWindow window =
                GetWindow<SurfaceTileSheetImporterWindow>("不规则瓦片导入");
            window.minSize = new Vector2(1100f, 700f);
            window.Show();
            window.TryUseProjectSelection();
            window.RefreshToolkit();
        }

        private void OnEnable()
        {
            Undo.undoRedoPerformed += RefreshToolkit;
            TryUseProjectSelection();
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= RefreshToolkit;
            DestroyPreview();
        }

        private void SetSource(Texture2D texture)
        {
            DestroyPreview();
            sourceAsset = texture;
            recipe = null;
            selectedIndex = -1;
            if (texture == null)
            {
                status = "请选择源图片。";
                return;
            }

            if (!SurfaceTileSheetGenerator.TryLoadOriginal(
                    texture,
                    out sourcePreview,
                    out string error))
            {
                status = error;
                return;
            }

            string sourcePath = AssetDatabase.GetAssetPath(texture);
            string recipePath = Path.GetDirectoryName(sourcePath)
                ?.Replace('\\', '/') + "/" +
                Path.GetFileNameWithoutExtension(sourcePath) +
                "_SurfaceTileImport.asset";
            recipe = AssetDatabase.LoadAssetAtPath<SurfaceTileSheetImportRecipe>(
                recipePath);
            if (recipe == null)
            {
                recipe = CreateInstance<SurfaceTileSheetImportRecipe>();
                recipe.ConfigureSource(texture);
                AssetDatabase.CreateAsset(recipe, recipePath);
                AssetDatabase.SaveAssets();
            }
            else if (recipe.SourceTexture != texture)
            {
                Undo.RecordObject(recipe, "更新瓦片源图片");
                recipe.ConfigureSource(texture);
                SaveRecipe();
            }

            status = $"原图 {sourcePreview.width}×{sourcePreview.height} · " +
                     "拖框可把连续平台或整栋建筑作为一个瓦片。";
            zoom = .35f;
        }

        private void TryUseProjectSelection()
        {
            Texture2D texture = ResolveTexture(Selection.activeObject);
            if (sourceAsset == null && texture != null)
            {
                SetSource(texture);
            }
        }

        private void AddRegion(RectInt rect)
        {
            rect = ClampToSource(rect);
            if (rect.width <= 0 || rect.height <= 0)
            {
                return;
            }

            Undo.RecordObject(recipe, "添加瓦片选区");
            string name = "Tile_" + (recipe.Regions.Count + 1).ToString("D3");
            recipe.Regions.Add(new SurfaceTileSourceRegion(name, rect));
            selectedIndex = recipe.Regions.Count - 1;
            SaveRecipe();
        }

        private void AutoDetect()
        {
            if (sourcePreview == null || recipe == null)
            {
                return;
            }

            try
            {
                EditorUtility.DisplayProgressBar(
                    "识别不规则瓦片",
                    "正在读取透明区域……",
                    0.45f);
                List<RectInt> detected =
                    SurfaceTileSheetGenerator.DetectCompleteRegionsWithOptionalPieces(
                    sourcePreview,
                    0,
                    minimumOpaquePixels,
                    separatePieceMinimumPixels,
                    mergeGap,
                    detectionMargin,
                    includeSeparatePieces);
                if (detected.Count == 0)
                {
                    status = "没有识别到满足条件的透明块。";
                    return;
                }

                bool replace = recipe.Regions.Count == 0 ||
                               EditorUtility.DisplayDialog(
                                   "自动识别完成",
                                   $"识别到 {detected.Count} 个候选区域。\n" +
                                   "替换现有选区，还是追加？",
                                   "替换",
                                   "追加");
                Undo.RecordObject(recipe, "自动识别瓦片选区");
                if (replace)
                {
                    recipe.Regions.Clear();
                }

                int start = recipe.Regions.Count;
                for (int index = 0; index < detected.Count; index++)
                {
                    recipe.Regions.Add(new SurfaceTileSourceRegion(
                        "Tile_" + (start + index + 1).ToString("D3"),
                        detected[index]));
                }

                selectedIndex = recipe.Regions.Count > 0 ? 0 : -1;
                SaveRecipe();
                status = includeSeparatePieces
                    ? $"已生成完整图块并追加独立碎片，共 {detected.Count} 个候选区域。"
                    : $"已识别 {detected.Count} 个完整图块。概念图中的文字可能被识别，请在右侧删除或禁用。";
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private void Generate()
        {
            SaveRecipe();
            if (!SurfaceTileSheetGenerator.Generate(
                    recipe,
                    out SurfaceTileSheetGenerateResult result,
                    out string error))
            {
                status = error;
                EditorUtility.DisplayDialog("生成失败", error, "确定");
                return;
            }

            status = $"已生成 {result.TileCount} 个瓦片：{result.PalettePath}";
            Selection.activeObject = result.Palette;
            EditorGUIUtility.PingObject(result.Palette);
            EditorUtility.DisplayDialog(
                "瓦片库已生成",
                $"共 {result.TileCount} 个瓦片。\n\n" +
                $"图集：{result.TexturePath}\n" +
                $"瓦片库：{result.PalettePath}\n\n" +
                "现在可回到 Scene 贴画面板，把该瓦片库指定给方块。",
                "确定");
        }

        private static Texture2D ResolveTexture(UnityEngine.Object selected)
        {
            if (selected is Texture2D texture)
            {
                return texture;
            }

            if (selected is Sprite sprite)
            {
                string path = AssetDatabase.GetAssetPath(sprite);
                return AssetDatabase.LoadAssetAtPath<Texture2D>(path) ??
                       sprite.texture;
            }

            return null;
        }

        private Rect SourceToDisplay(RectInt source, Rect imageRect)
        {
            float x = imageRect.x + source.x / (float)sourcePreview.width * imageRect.width;
            float y = imageRect.y +
                      (sourcePreview.height - source.yMax) /
                      (float)sourcePreview.height * imageRect.height;
            return new Rect(
                x,
                y,
                source.width / (float)sourcePreview.width * imageRect.width,
                source.height / (float)sourcePreview.height * imageRect.height);
        }

        private RectInt DisplayToSource(Rect display, Rect imageRect)
        {
            int xMin = Mathf.FloorToInt(
                (display.xMin - imageRect.x) / imageRect.width * sourcePreview.width);
            int xMax = Mathf.CeilToInt(
                (display.xMax - imageRect.x) / imageRect.width * sourcePreview.width);
            int yMin = Mathf.FloorToInt(
                (imageRect.yMax - display.yMax) / imageRect.height * sourcePreview.height);
            int yMax = Mathf.CeilToInt(
                (imageRect.yMax - display.yMin) / imageRect.height * sourcePreview.height);
            return ClampToSource(new RectInt(
                xMin,
                yMin,
                xMax - xMin,
                yMax - yMin));
        }

        private RectInt ClampToSource(RectInt rect)
        {
            if (sourcePreview == null)
            {
                return rect;
            }

            int xMin = Mathf.Clamp(rect.xMin, 0, sourcePreview.width);
            int yMin = Mathf.Clamp(rect.yMin, 0, sourcePreview.height);
            int xMax = Mathf.Clamp(rect.xMax, 0, sourcePreview.width);
            int yMax = Mathf.Clamp(rect.yMax, 0, sourcePreview.height);
            return new RectInt(
                xMin,
                yMin,
                Mathf.Max(0, xMax - xMin),
                Mathf.Max(0, yMax - yMin));
        }

        private int FindRegionAt(Vector2 point, Rect imageRect)
        {
            for (int index = recipe.Regions.Count - 1; index >= 0; index--)
            {
                if (SourceToDisplay(recipe.Regions[index].Rect, imageRect)
                    .Contains(point))
                {
                    return index;
                }
            }

            return -1;
        }

        private int CountEnabledRegions()
        {
            if (recipe == null)
            {
                return 0;
            }

            int count = 0;
            for (int index = 0; index < recipe.Regions.Count; index++)
            {
                if (recipe.Regions[index] != null &&
                    recipe.Regions[index].Enabled)
                {
                    count++;
                }
            }

            return count;
        }

        private void RemoveRegion(int index)
        {
            if (recipe == null || index < 0 || index >= recipe.Regions.Count)
            {
                return;
            }

            Undo.RecordObject(recipe, "删除瓦片选区");
            recipe.Regions.RemoveAt(index);
            if (selectedIndex == index)
            {
                selectedIndex = -1;
            }
            else if (selectedIndex > index)
            {
                selectedIndex--;
            }

            status = "已取消一个瓦片选区；可按 Ctrl+Z 撤销。";
            SaveRecipe();
        }

        private void SaveRecipe()
        {
            if (recipe == null)
            {
                return;
            }

            EditorUtility.SetDirty(recipe);
            AssetDatabase.SaveAssetIfDirty(recipe);
            Repaint();
        }

        private void DestroyPreview()
        {
            if (sourcePreview != null)
            {
                DestroyImmediate(sourcePreview);
                sourcePreview = null;
            }
        }
    }
}
