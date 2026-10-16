using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Project.GridArt.Editor
{
    public sealed partial class GridArtWindow
    {
        private VisualElement libraryPanel, inspectorPanel, brushList;
        private string search = "", category = "全部";
        private bool scenePainting;
        private Vector2 previewTravel;
        internal bool ScenePainting => scenePainting;
        internal bool RectangleMode => rectangle;
        internal bool EraseMode => erase;
        internal bool PaintSceneCell(Vector2Int p, bool remove) => remove ? Layer.Erase(p, map.palette) : Layer.Set(p, brushIndex);
        internal void CommitSceneStroke() { ArtChanged(); }
        internal void StopScenePainting() { scenePainting = false; GridArtScenePainter.SetTarget(null); rootVisualElement.Q<Toggle>("scene-paint")?.SetValueWithoutNotify(false); }
        private void EndPreview() { previewTravel = Vector2.zero; rootVisualElement.Q<Slider>("parallax-preview")?.SetValueWithoutNotify(0); canvas?.Invalidate(); }
        private const string StylePath = "Assets/_Project/Code/Systems/GridArt/Editor/GridArtWorkspace.uss";

        private void BuildWorkspace()
        {
            minSize = new Vector2(920, 560); titleContent = new GUIContent("关卡美术");
            BindRemembered();
            BindAutomation();
            var root = rootVisualElement; root.Clear(); root.AddToClassList("art-workspace");
            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(StylePath); if (sheet != null) root.styleSheets.Add(sheet);
            var header = Row(); header.AddToClassList("art-header");
            header.Add(new Label("关卡美术") { name = "art-title" });
            mapField = new ObjectField { objectType = typeof(GridArtMap), allowSceneObjects = false, value = map };
            mapField.style.flexGrow = 1; mapField.RegisterValueChangedCallback(e => SetMap(e.newValue as GridArtMap)); header.Add(mapField);
            var create = ActionButton("新建", () => CreateMap(map != null ? map.cellSize : 1)); create.name = "art-new"; header.Add(create);
            var copy = ActionButton("独立副本", CopyMap); copy.name = "art-copy"; header.Add(copy);
            var apply = ActionButton("刷新场景", Apply); apply.name = "art-apply"; header.Add(apply);
            var save = ActionButton("保存场景", SaveAll); save.name = "art-save"; save.AddToClassList("art-primary"); header.Add(save); root.Add(header);
            var tools = Row(); tools.AddToClassList("art-toolbar");
            var draw = ActionButton("绘制", () => { EndPreview(); erase = false; Refresh(); }); draw.name = "art-draw"; tools.Add(draw);
            var remove = ActionButton("擦除", () => { EndPreview(); erase = true; Refresh(); }); remove.name = "art-erase"; tools.Add(remove);
            var rect = new Toggle("框选") { value = rectangle }; rect.RegisterValueChangedCallback(e => rectangle = e.newValue); tools.Add(rect);
            var scene = new Toggle("场景绘制") { value = scenePainting, name = "scene-paint" };
            scene.tooltip = "在Scene视图当前美术层平面绘制；松开鼠标后应用该笔画。Esc退出。";
            scene.RegisterValueChangedCallback(e =>
            {
                if (e.newValue && (!CanEditMap || Brush == null || Layer == null))
                { scene.SetValueWithoutNotify(false); status.text = "请先选择本关美术图、图层和素材。"; return; }
                if (e.newValue) EndPreview();
                scenePainting = e.newValue; GridArtScenePainter.SetTarget(e.newValue ? this : null);
            }); tools.Add(scene);
            tools.Add(ActionButton("居中", () => canvas?.FrameAll()));
            tools.Add(ActionButton("切片导入", () => EditorApplication.ExecuteMenuItem("Tools/2026TapTap/方块贴画/导入不规则瓦片图")));
            var spacer = new VisualElement(); spacer.style.flexGrow = 1; tools.Add(spacer);
            tools.Add(ActionButton("素材栏", () => TogglePanel(libraryPanel)));
            tools.Add(ActionButton("图层栏", () => TogglePanel(inspectorPanel))); root.Add(tools);
            var persistence = Row(); persistence.AddToClassList("art-toolbar");
            var autosave = new Toggle("自动保存美术") { name = "art-autosave", value = autoSave };
            autosave.tooltip = "停止操作后保存本工具数据，关闭和重编译前补存；不自动保存场景中的其他修改。";
            autosave.RegisterValueChangedCallback(e => { autoSave = e.newValue; EditorPrefs.SetBool(SceneKey+".AutoSave",autoSave); if (autoSave) QueuePersistence(); }); persistence.Add(autosave);
            var autoapply = new Toggle("实时场景预览") { name = "art-autopreview", value = autoPreview };
            autoapply.tooltip = "笔画完成后合并更新变动的网格；不在拖动中保存或重建。";
            autoapply.RegisterValueChangedCallback(e => { autoPreview=e.newValue; EditorPrefs.SetBool(SceneKey+".AutoPreview",autoPreview); if (autoPreview) QueuePersistence(); }); persistence.Add(autoapply); root.Add(persistence);
            var content = Row(); content.style.flexGrow = 1; content.style.minHeight = 0;
            libraryPanel = new VisualElement { name="art-library-panel" }; libraryPanel.AddToClassList("art-library"); content.Add(libraryPanel);
            var center = new VisualElement(); center.style.flexGrow = 1; center.style.minWidth = 180;
            canvas = new ArtCanvas(this); canvas.style.flexGrow = 1; center.Add(canvas);
            var previewRow = Row();
            var preview = new Slider("视差预览", -16, 16) { value = 0, showInputField = true, name = "parallax-preview" }; preview.style.flexGrow = 1;
            preview.tooltip = "模拟镜头水平移动，不修改场景相机或美术数据。";
            preview.RegisterValueChangedCallback(e => { previewTravel = new Vector2(e.newValue, 0); canvas.Invalidate(); });
            previewRow.Add(preview); previewRow.Add(ActionButton("复位", EndPreview)); center.Add(previewRow);
            center.Add(new Label("左键绘制 · 右键擦除 · 中键平移 · 滚轮缩放") { name = "art-canvas-hint" }); content.Add(center);
            inspectorPanel = new ScrollView { name="art-inspector-panel" }; inspectorPanel.AddToClassList("art-inspector"); content.Add(inspectorPanel);
            sidebar = inspectorPanel; root.Add(content);
            status = new Label(); status.AddToClassList("art-status"); root.Add(status);
            Refresh();
        }

        private static VisualElement Row() { var r = new VisualElement(); r.style.flexDirection = FlexDirection.Row; return r; }
        private static Button ActionButton(string text, Action action) => new Button(action) { text = text };
        private void TogglePanel(VisualElement p)
        {
            if (p == null) return;
            bool show=p.resolvedStyle.display==DisplayStyle.None; p.style.display=show?DisplayStyle.Flex:DisplayStyle.None;
            EditorPrefs.SetBool(SceneKey+".Panel."+p.name,show);
        }
        private static Label Heading(string text) { var label = new Label(text); label.AddToClassList("art-section-title"); return label; }
        private Foldout Section(string text, bool initial)
        {
            var fold = new Foldout { text = text, value = EditorPrefs.GetBool(SceneKey + "." + text, initial) };
            fold.RegisterValueChangedCallback(e => { if (e.target == fold) EditorPrefs.SetBool(SceneKey + "." + text, e.newValue); });
            return fold;
        }

        private void RefreshWorkspace()
        {
            if (sidebar == null || libraryPanel == null) return;
            libraryPanel.style.display=EditorPrefs.GetBool(SceneKey+".Panel."+libraryPanel.name,true)?DisplayStyle.Flex:DisplayStyle.None;
            inspectorPanel.style.display=EditorPrefs.GetBool(SceneKey+".Panel."+inspectorPanel.name,true)?DisplayStyle.Flex:DisplayStyle.None;
            bool editing = !EditorApplication.isPlayingOrWillChangePlaymode;
            rootVisualElement.Q<Button>("art-new")?.SetEnabled(editing);
            rootVisualElement.Q<Button>("art-copy")?.SetEnabled(editing && map != null);
            rootVisualElement.Q<Button>("art-apply")?.SetEnabled(editing && CanEditMap && map.palette != null);
            rootVisualElement.Q<Button>("art-save")?.SetEnabled(editing && map != null);
            rootVisualElement.Q<Toggle>("scene-paint")?.SetEnabled(editing && CanEditMap && Brush != null && Layer != null);
            if (scenePainting && (!CanEditMap || Brush == null || Layer == null)) StopScenePainting();
            rootVisualElement.Q<Button>("art-draw")?.EnableInClassList("art-selected", !erase);
            rootVisualElement.Q<Button>("art-erase")?.EnableInClassList("art-selected", erase);
            rootVisualElement.Q<Button>("art-draw")?.SetEnabled(editing && Layer != null && !Layer.followLevel && !Layer.locked);
            rootVisualElement.Q<Button>("art-erase")?.SetEnabled(editing && Layer != null && !Layer.followLevel && !Layer.locked);
            var libraryOffset = libraryPanel.Q<ScrollView>("art-library-scroll")?.scrollOffset ?? Vector2.zero;
            var inspectorOffset = ((ScrollView)inspectorPanel).scrollOffset;
            mapField.SetValueWithoutNotify(map); canvas.Invalidate(); libraryPanel.Clear(); inspectorPanel.Clear();
            libraryPanel.Add(Heading("素材库"));
            var query = new ToolbarSearchField { value = search };
            query.RegisterValueChangedCallback(e => { search = e.newValue; RefreshBrushes(); }); libraryPanel.Add(query);
            var sourceTools = Section("素材来源", false);
            var source = new ObjectField("图片 / 切片") { objectType = typeof(UnityEngine.Object), allowSceneObjects = false };
            var ppc = new IntegerField("每格像素") { value = 64, isDelayed = true }; sourceTools.Add(source); sourceTools.Add(ppc);
            sourceTools.Add(ActionButton("加入当前库", () =>
            {
                if (map?.palette == null) { status.text = "请先新建或选择美术图。"; return; }
                int added = GridArtLibraryService.Import(map.palette, source.value, ppc.value); ArtChanged(true);
                status.text = added > 0 ? "已添加 " + added + " 项素材" : "没有新增切片：请选择已切片图片或Sprite。";
            }));
            sourceTools.Add(ActionButton("汇入所有切片库", () =>
            {
                if (map?.palette == null) return;
                int added = GridArtLibraryService.Synchronize(map.palette); ArtChanged(true); status.text = "已汇入 " + added + " 项新素材";
            }));
            sourceTools.Add(ActionButton("配置当前素材", () => { var settings=rootVisualElement.Q<Foldout>("art-brush-settings"); if(settings!=null) { settings.value=true; ((ScrollView)inspectorPanel).ScrollTo(settings); } }));
            sourceTools.Add(ActionButton("接入共享素材库", () =>
            {
                if (!CanEditMap) { status.text = "请先创建此关卡的独立美术图。"; return; }
                try { GridArtLibraryService.Connect(map, GridArtLibraryService.Shared()); diskSignature=DiskSignature(map); ArtChanged(true); }
                catch (Exception e) { status.text = e.Message; }
            }));
            libraryPanel.Add(sourceTools);
            if (map == null)
            {
                libraryPanel.Add(new HelpBox("新建美术图后选择素材。", HelpBoxMessageType.Info));
                inspectorPanel.Add(Heading("开始布置"));
                inspectorPanel.Add(new HelpBox("创建本关美术图后，选择素材，自动铺绘墙地或直接绘制。", HelpBoxMessageType.Info));
                inspectorPanel.Add(ActionButton("创建本关美术图", () => CreateMap(levelSource?.cellSize ?? 1)));
                inspectorPanel.Add(ActionButton("打开独立示例", () => EditorApplication.ExecuteMenuItem("Tools/2026TapTap/关卡美术/打开美术测试场景")));
                status.text = "未选择美术图 · 玩法碰撞保持独立"; return;
            }
            if (!CanEditMap) inspectorPanel.Add(new HelpBox("此图属于其他场景。创建独立副本后再绘制。", HelpBoxMessageType.Warning));
            var categories = new System.Collections.Generic.List<string> { "全部", "自动地形" };
            if (map.palette != null) foreach (var b in map.palette.brushes)
                if (!string.IsNullOrEmpty(b.category) && !categories.Contains(b.category)) categories.Add(b.category);
            if (!categories.Contains(category)) category = "全部";
            var filter = new PopupField<string>(categories, categories.IndexOf(category));
            filter.RegisterValueChangedCallback(e => { category = e.newValue; RefreshBrushes(); }); libraryPanel.Add(filter);
            var scroll = new ScrollView { name = "art-library-scroll" }; scroll.style.flexGrow = 1;
            brushList = new VisualElement(); brushList.AddToClassList("art-brush-grid"); scroll.Add(brushList); libraryPanel.Add(scroll); RefreshBrushes();
            if (map.palette != null && map.palette.brushes.Count > 0) brushIndex = Mathf.Clamp(brushIndex, 0, map.palette.brushes.Count - 1);
            inspectorPanel.Add(Heading("当前素材"));
            if (Brush != null)
            {
                inspectorPanel.Add(new Image { sprite = Brush.autoTerrain ? Brush.GetPiece(4) : Brush.sprite, scaleMode = ScaleMode.ScaleToFit, name = "art-current-preview" });
                inspectorPanel.Add(new Label(Brush.label));
                inspectorPanel.Add(new Label(Brush.autoTerrain ? "自动连接边缘与拐角" : "尺寸 " + Brush.DisplaySize.x.ToString("0.##") + " × " + Brush.DisplaySize.y.ToString("0.##") + " 格"));
                if (Brush.autoTerrain && !Brush.HasInnerCorners) inspectorPanel.Add(new HelpBox("缺少内拐角素材，暂用中心图。", HelpBoxMessageType.Warning));
                inspectorPanel.Add(BuildBrushSettings());
            }
            var sourcePanel = new VisualElement { name = "art-level-source" }; inspectorPanel.Add(sourcePanel); RefreshSourcePanel();
            inspectorPanel.Add(Heading("美术图层"));
            foreach (int i in OrderedLayers.Reverse())
            {
                int index = i; var layer = map.layers[i]; var row = Row();
                var visible = new Toggle { value = layer.visible }; visible.style.width = 22;
                visible.RegisterValueChangedCallback(e => Change("图层显隐", () => layer.visible = e.newValue)); row.Add(visible);
                var button = ActionButton(layer.label + (layer.locked ? "  · 锁定" : ""), () => { layerIndex = index; if (layer.followLevel) sourceGroup=layer.sourceGroup; Refresh(); });
                button.style.flexGrow = 1; if (index == layerIndex) button.AddToClassList("art-selected"); row.Add(button); inspectorPanel.Add(row);
            }
            var add = Row(); add.Add(ActionButton("+ 墙地", () => AddLayer("墙地表面", -.51f, 1, -100)));
            add.Add(ActionButton("+ 装饰", () => AddLayer("场景装饰", -.55f, 1, -80))); inspectorPanel.Add(add);
            var background = Row(); background.Add(ActionButton("+ 远景", () => AddLayer("远景背景", 8, .25f, -200)));
            background.Add(ActionButton("+ 中景", () => AddLayer("中景背景", 4, .6f, -150)));
            background.Add(ActionButton("+ 前景", () => AddLayer("前景装饰", -1, 1.15f, 100))); inspectorPanel.Add(background);
            if (Layer == null) { status.text = "添加一个美术图层开始绘制。"; return; }
            var config = Section("当前图层设置", true);
            var name = new TextField("名称") { value = Layer.label, isDelayed = true }; name.RegisterValueChangedCallback(e => Change("图层名称", () => Layer.label = e.newValue)); config.Add(name);
            var locked = new Toggle("锁定") { value = Layer.locked }; locked.RegisterValueChangedCallback(e => Change("锁定图层", () => Layer.locked = e.newValue)); config.Add(locked);
            var factor = new Slider("视差速度", 0, 1.5f) { value = Layer.parallax.x, showInputField = true };
            factor.tooltip = "1为普通世界物体；越小越远；超过1为前景。";
            factor.RegisterValueChangedCallback(e => Change("视差速度", () => Layer.parallax = Vector2.one * e.newValue, false)); config.Add(factor);
            var actions = Row(); actions.Add(ActionButton("后移", () => MoveLayer(-1))); actions.Add(ActionButton("前移", () => MoveLayer(1)));
            actions.Add(ActionButton("删除", () => { if (EditorUtility.DisplayDialog("删除图层", "删除“" + Layer.label + "”及其美术内容？", "删除", "取消")) Change("删除图层", () => map.layers.RemoveAt(layerIndex)); })); config.Add(actions);
            inspectorPanel.Add(config);
            var advanced = Section("深度、受光与网格", false);
            var depth = new FloatField("Z深度") { value = Layer.depth, isDelayed = true }; depth.RegisterValueChangedCallback(e => Change("美术深度", () => Layer.depth = e.newValue)); advanced.Add(depth);
            var order = new IntegerField("绘制顺序") { value = Layer.sortingOrder == int.MinValue ? layerIndex - 100 : Layer.sortingOrder, isDelayed = true }; order.RegisterValueChangedCallback(e => Change("绘制顺序", () => Layer.sortingOrder = e.newValue)); advanced.Add(order);
            var tint = new ColorField("颜色 / 透明度") { value = Layer.tint }; tint.RegisterValueChangedCallback(e => Change("美术颜色", () => Layer.tint = e.newValue, false)); advanced.Add(tint);
            var lit = new Toggle("接收3D灯光") { value = Layer.lighting3D }; lit.RegisterValueChangedCallback(e => Change("美术受光", () => Layer.lighting3D = e.newValue)); advanced.Add(lit);
            var cell = new FloatField("每格世界尺寸") { value = map.cellSize, isDelayed = true }; cell.RegisterValueChangedCallback(e => { Change("美术网格", () => map.cellSize = Mathf.Max(.01f, e.newValue)); RequestSource(); }); advanced.Add(cell);
            var palette = new ObjectField("素材库") { objectType = typeof(GridArtPalette), allowSceneObjects = false, value = map.palette };
            palette.RegisterValueChangedCallback(e => { if (map.layers.Any(l => l.cells.Count > 0)) { palette.SetValueWithoutNotify(map.palette); status.text = "已有绘图不能直接换库，请使用独立副本或共享库迁移。"; } else Change("选择素材库", () => map.palette = e.newValue as GridArtPalette); }); advanced.Add(palette); inspectorPanel.Add(advanced);
            var guide = new Toggle("显示关卡轮廓") { value = showGuides }; guide.RegisterValueChangedCallback(e => { showGuides = e.newValue; canvas.RepaintView(); }); inspectorPanel.Add(guide);
            UpdateSaveStatus();
            scroll.schedule.Execute(() => scroll.scrollOffset = libraryOffset);
            inspectorPanel.schedule.Execute(() => ((ScrollView)inspectorPanel).scrollOffset = inspectorOffset);
            RememberSelection();
        }

        private void RefreshBrushes()
        {
            if (brushList == null) return; brushList.Clear(); if (map?.palette == null) return;
            int count = 0;
            for (int i = 0; i < map.palette.brushes.Count; i++)
            {
                var brush = map.palette.Get(i);
                if (category == "自动地形" ? !brush.autoTerrain : category != "全部" && brush.category != category) continue;
                if (!string.IsNullOrEmpty(search) && (brush.label + " " + brush.category).IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0) continue;
                int index = i; var button = ActionButton("", () => { brushIndex = index; Refresh(); }); button.AddToClassList("art-brush");
                if (i == brushIndex) button.AddToClassList("art-selected"); button.tooltip = brush.label + " · " + brush.DisplaySize + " 格";
                var image = new Image { sprite = brush.autoTerrain ? brush.GetPiece(4) : brush.sprite, scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore }; image.style.height = 60; button.Add(image);
                var label = new Label(brush.label) { pickingMode = PickingMode.Ignore }; label.AddToClassList("art-brush-name"); button.Add(label); brushList.Add(button); count++;
            }
            if (count == 0) brushList.Add(new Label("无匹配素材\n展开素材来源添加切片。"));
        }

        private void AddLayer(string name, float depth, float speed, int order) => Change("添加美术图层", () =>
        { map.layers.Add(new GridArtLayer { label = name, depth = depth, parallax = Vector2.one * speed, sortingOrder = order }); layerIndex = map.layers.Count - 1; });
        private void CopyMap()
        {
            if (map == null || EditorApplication.isPlayingOrWillChangePlaymode) return;
            string path = EditorUtility.SaveFilePanelInProject("独立美术图", map.name + "_副本", "asset", "为当前关卡保存独立美术图");
            if (string.IsNullOrEmpty(path) || path == AssetDatabase.GetAssetPath(map)) return;
            var copy = Instantiate(map); copy.ownerSceneGuid = ""; copy.bakedRevision = -1;
            if (!CanEditMap) foreach (var layer in copy.layers) layer.followLevel = false;
            AssetDatabase.CreateAsset(copy, AssetDatabase.GenerateUniqueAssetPath(path));
            foreach (var output in UnityEngine.Object.FindObjectsByType<GridArtScene>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                if (output.map == map && output.gameObject.scene == SceneManager.GetActiveScene())
                { Undo.RecordObject(output,"美术图独立副本"); output.map = copy; EditorUtility.SetDirty(output); EditorSceneManager.MarkSceneDirty(output.gameObject.scene); }
            SetMap(copy);
        }
    }
}
