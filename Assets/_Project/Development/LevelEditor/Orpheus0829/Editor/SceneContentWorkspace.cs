using System;
using System.Collections.Generic;
using System.Linq;
using Project.GridArt;
using Project.Rooms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Project.LevelEditor.Editor
{
    internal sealed class SceneContentItem
    {
        internal ScenePaletteItem template;
        internal GridArtScene artScene;
        internal int brushIndex = -1;
        internal string title, kind, key, search;
        internal Sprite sprite;
        internal bool IsArt => artScene != null && brushIndex >= 0;
    }

    /// <summary>Read-only catalog. Entries route to existing authoring transactions, never duplicate author data.</summary>
    internal static class SceneContentCatalog
    {
        internal static List<SceneContentItem> Build(Scene scene)
        {
            var result = new List<SceneContentItem>();
            var palette = AssetDatabase.LoadAssetAtPath<LevelEditorPalette>(ScenePlacementPaletteCatalog.PalettePath);
            foreach (ScenePaletteItem item in ScenePlacementPaletteCatalog.Build(palette))
            {
                string kind = item.isProp ? "机关道具" : "方块";
                Sprite sprite = item.prefab != null ? item.prefab.GetComponentInChildren<SpriteRenderer>(true)?.sprite : null;
                result.Add(new SceneContentItem { template = item, title = item.displayName, kind = kind,
                    key = "object:" + (item.isProp ? "prop:" : "block:") + item.id + ":" + item.index,
                    search = kind + " " + item.SearchText, sprite = sprite });
            }
            if (!scene.IsValid() || !scene.isLoaded) return result;
            foreach (GridArtScene art in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<GridArtScene>(true)))
            {
                if (art.map?.palette == null) continue;
                string mapId = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(art.map));
                for (int i = 0; i < art.map.palette.brushes.Count; i++)
                {
                    GridArtBrush brush = art.map.palette.brushes[i];
                    if (brush == null) continue;
                    result.Add(new SceneContentItem { artScene = art, brushIndex = i, title = brush.label,
                        kind = "美术", key = "art:" + mapId + ":" + art.GetInstanceID() + ":" + i,
                        search = "美术 " + brush.label + " " + brush.category + " " + art.map.name,
                        sprite = brush.sprite != null ? brush.sprite : brush.GetPiece(4) });
                }
            }
            return result;
        }

        internal static bool Matches(SceneContentItem item, string category, string query) =>
            (category == "全部" || item.kind == category) &&
            (string.IsNullOrWhiteSpace(query) || item.search.IndexOf(query.Trim(), StringComparison.OrdinalIgnoreCase) >= 0);
    }

    /// <summary>Dockable content-first front end. Opening, filtering and selecting are zero-write operations.</summary>
    internal sealed class SceneContentWorkspace : EditorWindow
    {
        private const string Owner = "SceneContentWorkspace";
        private static SceneContentWorkspace active;
        internal static bool OwnsAuthoring => active != null && active.painting;
        private List<SceneContentItem> items = new();
        private readonly List<string> roomIds = new();
        private SceneContentItem selected;
        private SceneView view;
        private ListView library;
        private ToolbarSearchField search;
        private DropdownField category, rooms, layers;
        private Label status, details, sceneLabel;
        private Image preview;
        private Button paint;
        private VisualElement inspector;
        private Toggle erase, rectangle;
        private bool painting, refreshing, dirty = true;
        private int boundScene;
        private string selectedKey = "";

        [MenuItem("Tools/2026TapTap/关卡编辑器/场景编辑器 _m")]
        internal static void Open()
        {
            var window = GetWindow<SceneContentWorkspace>();
            window.titleContent = new GUIContent("关卡工作台");
            window.minSize = new Vector2(700, 430);
            window.Show();
        }

        private void OnEnable()
        {
            active = this;
            dirty = true;
            EditorApplication.projectChanged += Invalidate;
            EditorApplication.hierarchyChanged += Invalidate;
            EditorSceneManager.activeSceneChangedInEditMode += SceneChanged;
            Selection.selectionChanged += InspectSelection;
            SceneObjectAuthoring.Changed += Invalidate;
            SceneArtAuthoring.Changed += Invalidate;
            Undo.undoRedoPerformed += Invalidate;
            EditorApplication.playModeStateChanged += PlayChanged;
        }

        private void OnDisable()
        {
            StopPainting();
            EditorApplication.projectChanged -= Invalidate;
            EditorApplication.hierarchyChanged -= Invalidate;
            EditorSceneManager.activeSceneChangedInEditMode -= SceneChanged;
            Selection.selectionChanged -= InspectSelection;
            SceneObjectAuthoring.Changed -= Invalidate;
            SceneArtAuthoring.Changed -= Invalidate;
            Undo.undoRedoPerformed -= Invalidate;
            EditorApplication.playModeStateChanged -= PlayChanged;
            if (active == this) active = null;
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            var header = new Toolbar();
            sceneLabel = new Label { style = { flexGrow = 1 } };
            header.Add(sceneLabel);
            header.Add(new ToolbarButton(() => { StopPainting(); SceneLevelEditorOverlay.Open(); }) { text = "区域与机关连接…" });
            header.Add(new ToolbarButton(Invalidate) { text = "刷新内容" });
            rootVisualElement.Add(header);
            var split = new TwoPaneSplitView(0, 285, TwoPaneSplitViewOrientation.Horizontal);
            split.style.flexGrow = 1;
            var left = new VisualElement { style = { minWidth = 220 } };
            search = new ToolbarSearchField();
            search.RegisterValueChangedCallback(_ => Filter());
            left.Add(search);
            category = new DropdownField("内容", new List<string> { "全部", "方块", "机关道具", "美术" }, 0);
            category.RegisterValueChangedCallback(_ => Filter());
            left.Add(category);
            library = new ListView { fixedItemHeight = 53, selectionType = SelectionType.Single, virtualizationMethod = CollectionVirtualizationMethod.FixedHeight };
            library.style.flexGrow = 1;
            library.makeItem = MakeCard;
            library.bindItem = BindCard;
            library.selectionChanged += selection => { if (!refreshing) Select(selection.OfType<SceneContentItem>().FirstOrDefault()); };
            library.itemsChosen += selection => { Select(selection.OfType<SceneContentItem>().FirstOrDefault()); StartPainting(); };
            left.Add(library);
            split.Add(left);
            var right = new ScrollView { style = { minWidth = 320, paddingLeft = 10, paddingRight = 10 } };
            var heading = new Label("放置内容") { style = { fontSize = 17, marginTop = 10, marginBottom = 8 } };
            right.Add(heading);
            preview = new Image { scaleMode = ScaleMode.ScaleToFit, style = { height = 100, maxWidth = 230 } };
            right.Add(preview);
            details = new Label { style = { whiteSpace = WhiteSpace.Normal, marginBottom = 8 } };
            right.Add(details);
            layers = new DropdownField("美术图层", new List<string>(), 0);
            layers.RegisterValueChangedCallback(_ => { if (painting) Bind(); });
            right.Add(layers);
            erase = new Toggle("擦除模式");
            erase.RegisterValueChangedCallback(_ => { if (painting) Bind(); });
            right.Add(erase);
            rectangle = new Toggle("矩形绘制（美术）");
            rectangle.RegisterValueChangedCallback(_ => { if (painting) Bind(); });
            right.Add(rectangle);
            paint = new Button(StartPainting) { text = "开始在场景中绘制", style = { height = 32, backgroundColor = new Color(.12f, .37f, .61f) } };
            right.Add(paint);
            right.Add(new Button(StopPainting) { text = "结束绘制" });
            var navigation = new Foldout { text = "房间导航", value = true, style = { marginTop = 12 } };
            rooms = new DropdownField("房间", new List<string>(), 0);
            navigation.Add(rooms);
            navigation.Add(new Button(FrameRoom) { text = "在场景中定位" });
            right.Add(navigation);
            var properties = new Foldout { text = "选中物体的实际属性", value = true, style = { marginTop = 12 } };
            properties.Add(new Button(EditSelection) { text = "编辑选中物体参数" });
            inspector = new VisualElement();
            properties.Add(inspector);
            right.Add(properties);
            split.Add(right);
            rootVisualElement.Add(split);
            status = new Label { style = { whiteSpace = WhiteSpace.Normal, paddingLeft = 8, paddingTop = 6, paddingBottom = 6 } };
            rootVisualElement.Add(status);
            rootVisualElement.schedule.Execute(Tick).Every(300);
            Refresh();
        }

        private VisualElement MakeCard()
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, paddingLeft = 6, paddingRight = 6 } };
            row.Add(new Image { name = "icon", scaleMode = ScaleMode.ScaleToFit, style = { width = 42, height = 42, marginRight = 7 } });
            var labels = new VisualElement { style = { flexGrow = 1, overflow = Overflow.Hidden } };
            labels.Add(new Label { name = "title" });
            labels.Add(new Label { name = "kind", style = { fontSize = 10, opacity = .65f } });
            row.Add(labels);
            return row;
        }

        private void BindCard(VisualElement element, int index)
        {
            var item = (SceneContentItem)library.itemsSource[index];
            element.Q<Label>("title").text = item.title;
            element.Q<Label>("kind").text = item.kind;
            var image = element.Q<Image>("icon");
            image.image = null;
            image.sprite = item.sprite;
            if (item.sprite == null && item.template?.prefab != null) image.image = AssetPreview.GetMiniThumbnail(item.template.prefab);
            element.tooltip = item.search;
        }

        private void Select(SceneContentItem item)
        {
            StopPainting();
            selected = item;
            selectedKey = item?.key ?? "";
            UpdateSelection();
        }

        private void UpdateSelection()
        {
            if (details == null) return;
            preview.sprite = selected?.sprite;
            bool art = selected?.IsArt == true;
            layers.style.display = art ? DisplayStyle.Flex : DisplayStyle.None;
            rectangle.style.display = art ? DisplayStyle.Flex : DisplayStyle.None;
            layers.choices = art ? selected.artScene.map.layers.Select(x => x?.label ?? "空图层").ToList() : new List<string>();
            if (layers.choices.Count > 0 && (layers.index < 0 || layers.index >= layers.choices.Count)) layers.index = 0;
            details.text = selected == null ? "在左侧搜索并选择内容。双击卡片开始绘制。" : art
                ? $"{selected.title} · 美术\n只修改所选美术图层，不生成玩法碰撞。"
                : $"{selected.title} · {selected.kind}\n实例化真实内容，参数和引用继续保存在场景中。";
            paint.SetEnabled(selected != null && !EditorApplication.isPlayingOrWillChangePlaymode);
        }

        private void Filter(bool stopPainting = true)
        {
            if (library == null) return;
            if (stopPainting) StopPainting();
            var filtered = items.Where(x => SceneContentCatalog.Matches(x, category.value, search.value)).ToList();
            refreshing = true;
            try
            {
                library.itemsSource = filtered;
                library.Rebuild();
                int index = filtered.FindIndex(x => x.key == selectedKey);
                library.SetSelectionWithoutNotify(index < 0 ? Array.Empty<int>() : new[] { index });
                if (stopPainting && index < 0) { selected = null; selectedKey = ""; UpdateSelection(); }
            }
            finally { refreshing = false; }
        }

        private void Refresh()
        {
            if (library == null) return;
            Scene scene = SceneManager.GetActiveScene();
            boundScene = scene.handle;
            items = SceneContentCatalog.Build(scene);
            selected = items.FirstOrDefault(x => x.key == selectedKey);
            sceneLabel.text = "当前场景：" + (scene.IsValid() ? scene.name : "未打开");
            var manager = SceneRegionAuthoring.FindManager(scene);
            roomIds.Clear();
            var names = new List<string>();
            if (manager != null) foreach (RoomRegion room in manager.Regions)
            {
                if (room == null) continue;
                roomIds.Add(room.id);
                names.Add((string.IsNullOrEmpty(room.label) ? room.id : room.label) + (room.corridor ? " · 通道" : ""));
            }
            rooms.choices = names;
            if (names.Count > 0 && (rooms.index < 0 || rooms.index >= names.Count)) rooms.index = 0;
            Filter(false);
            UpdateSelection();
            InspectSelection();
            dirty = false;
        }

        private void Tick()
        {
            if (SceneManager.GetActiveScene().handle != boundScene) { StopPainting(); dirty = true; }
            if (dirty) Refresh();
            if (painting && (selected == null || !SceneEditSession.IsLocked(view) || EditorApplication.isPlayingOrWillChangePlaymode)) StopPainting();
            if (painting) Bind();
            status.text = painting ? $"正在绘制：{selected?.title}。使用场景视口；中键/Alt 导航，点击结束绘制退出。"
                : $"可用内容 {items.Count} 项。浏览与导航不改场景；方块与美术当前沿用已验证的事务服务。";
        }

        private void StartPainting()
        {
            if (selected == null || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (selected.IsArt && (layers.index < 0 || layers.index >= selected.artScene.map.layers.Count))
            { status.text = "请先选择可绘制的美术图层。"; return; }
            StopPainting();
            view = SceneView.lastActiveSceneView ?? GetWindow<SceneView>();
            active = this;
            painting = true;
            SceneRegionAuthoring.Bind(view, null, "", false);
            SceneEditSession.Enter(view, Owner);
            if (!selected.IsArt) ScenePlacementPaletteCatalog.Select(selected.template);
            Bind();
            view.Show();
            view.Focus();
        }

        private void Bind()
        {
            if (view == null || !painting || selected == null) return;
            bool targetView = SceneView.lastActiveSceneView == view;
            bool art = selected.IsArt;
            LevelEditorState.Tool = erase.value ? LevelEditorTool.Erase : LevelEditorTool.Paint;
            LevelEditorScenePainter.SetOverlayAuthoring(view, targetView && !art);
            GridArtLayer layer = art && layers.index >= 0 && layers.index < selected.artScene.map.layers.Count ? selected.artScene.map.layers[layers.index] : null;
            SceneArtAuthoring.Bind(view, selected.artScene, layer, selected.brushIndex, 0,
                erase.value, rectangle.value, false, "", targetView && art);
        }

        private void StopPainting()
        {
            if (!painting) return;
            painting = false;
            LevelEditorScenePainter.SetOverlayAuthoring(view, false);
            SceneArtAuthoring.Bind(view, null, null, -1, 0, false, false, false, "", false);
            SceneEditSession.Exit(view, Owner);
        }

        private void FrameRoom()
        {
            var manager = SceneRegionAuthoring.FindManager(SceneManager.GetActiveScene());
            if (manager == null || rooms.index < 0 || rooms.index >= roomIds.Count) return;
            var room = manager.Regions.FirstOrDefault(x => x != null && x.id == roomIds[rooms.index]);
            if (room?.cells == null || room.cells.Count == 0) return;
            var grid = SceneGridSpace.For(manager);
            var bounds = new Bounds(grid.RegionCellCenter(room.cells[0]), Vector3.zero);
            foreach (Vector2Int cell in room.cells) bounds.Encapsulate(grid.RegionCellCenter(cell));
            bounds.Expand(manager.CellSize * 2);
            (SceneView.lastActiveSceneView ?? GetWindow<SceneView>()).Frame(bounds, false);
        }

        private void InspectSelection()
        {
            if (inspector == null) return;
            inspector.Clear();
            inspector.Add(new Label(Selection.activeGameObject != null
                ? Selection.activeGameObject.name + "：点击上方按钮编辑真实组件。"
                : "先在场景中选中物体。"));
        }

        private void EditSelection()
        {
            if (inspector == null) return;
            inspector.Clear();
            if (Selection.activeGameObject != null && Selection.activeGameObject.scene == SceneManager.GetActiveScene())
                foreach (Component component in Selection.activeGameObject.GetComponents<Component>())
                {
                    if (component == null) continue;
                    var foldout = new Foldout { text = ObjectNames.NicifyVariableName(component.GetType().Name), value = true };
                    foldout.Add(new InspectorElement(component));
                    inspector.Add(foldout);
                }
        }

        private void Invalidate() => dirty = true;
        private void SceneChanged(Scene oldScene, Scene newScene) { StopPainting(); Invalidate(); }
        private void PlayChanged(PlayModeStateChange change) { StopPainting(); UpdateSelection(); }
    }
}
