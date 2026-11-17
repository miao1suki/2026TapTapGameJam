using System;
using System.Collections.Generic;
using System.Linq;
using Project.GridArt;
using Project.BlockFeatures;
using Project.Mechanisms;
using Project.Rooms;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEditor.UIElements;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Project.LevelEditor.Editor
{
    internal enum SceneEditMode
    {
        Region,
        Object,
        Binding,
        Art,
    }

    /// <summary>
    /// A zero-write SceneView session. It owns only editor camera state and never
    /// mutates a Scene, prefab, authoring asset, selection, or Undo history.
    /// </summary>
    [InitializeOnLoad]
    internal static class SceneEditSession
    {
        private sealed class LockedView
        {
            internal SceneView view;
            internal SceneViewState state;
            internal readonly HashSet<string> owners = new HashSet<string>(StringComparer.Ordinal);
        }

        internal readonly struct SceneViewState
        {
            internal readonly bool in2DMode;
            internal readonly bool orthographic;
            internal readonly Quaternion rotation;
            internal readonly Vector3 pivot;
            internal readonly float size;

            internal SceneViewState(SceneView view)
            {
                in2DMode = view.in2DMode;
                orthographic = view.orthographic;
                rotation = view.rotation;
                pivot = view.pivot;
                size = view.size;
            }

            internal void Restore(SceneView view)
            {
                if (view == null) return;
                view.in2DMode = in2DMode;
                view.orthographic = orthographic;
                view.rotation = rotation;
                view.pivot = pivot;
                view.size = size;
                view.Repaint();
            }
        }

        private static readonly Dictionary<int, LockedView> Locked = new Dictionary<int, LockedView>();
        internal static event Action Changed;

        static SceneEditSession()
        {
            SceneView.duringSceneGui -= DuringSceneGui;
            SceneView.duringSceneGui += DuringSceneGui;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= ExitAll;
            AssemblyReloadEvents.beforeAssemblyReload += ExitAll;
            EditorApplication.quitting -= ExitAll;
            EditorApplication.quitting += ExitAll;
        }

        internal static bool IsLocked(SceneView view) =>
            view != null && Locked.ContainsKey(view.GetInstanceID());

        internal static void Enter(SceneView view, string owner)
        {
            if (view == null || string.IsNullOrEmpty(owner) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            int key = view.GetInstanceID();
            if (!Locked.TryGetValue(key, out LockedView locked))
            {
                locked = new LockedView { view = view, state = new SceneViewState(view) };
                Locked.Add(key, locked);
            }
            if (!locked.owners.Add(owner)) return;
            Enforce(view);
            view.Repaint();
            Changed?.Invoke();
        }

        internal static void Exit(SceneView view, string owner)
        {
            if (view == null || string.IsNullOrEmpty(owner)) return;
            int key = view.GetInstanceID();
            if (!Locked.TryGetValue(key, out LockedView locked) || !locked.owners.Remove(owner)) return;
            if (locked.owners.Count > 0)
            {
                Changed?.Invoke();
                return;
            }
            Locked.Remove(key);
            locked.state.Restore(view);
            Changed?.Invoke();
        }

        internal static void Enforce(SceneView view)
        {
            if (!IsLocked(view)) return;
            if (!view.in2DMode) view.in2DMode = true;
            if (!view.orthographic) view.orthographic = true;
            if (Quaternion.Angle(view.rotation, Quaternion.identity) > .001f) view.rotation = Quaternion.identity;
            Vector3 pivot = view.pivot;
            if (!Mathf.Approximately(pivot.z, 0f))
            {
                pivot.z = 0f;
                view.pivot = pivot;
            }
        }

        internal static void ExitAll()
        {
            if (Locked.Count == 0) return;
            LockedView[] snapshot = Locked.Values.ToArray();
            Locked.Clear();
            foreach (LockedView locked in snapshot) locked.state.Restore(locked.view);
            Changed?.Invoke();
        }

        private static void DuringSceneGui(SceneView view) => Enforce(view);

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingEditMode || change == PlayModeStateChange.EnteredPlayMode)
                ExitAll();
        }
    }

    /// <summary>Single source of truth for XY authoring and room Y-down conversion.</summary>
    internal readonly struct SceneGridSpace
    {
        private readonly Transform origin;
        internal readonly float CellSize;

        internal SceneGridSpace(Transform origin, float cellSize)
        {
            this.origin = origin;
            CellSize = Mathf.Max(.01f, cellSize);
        }

        internal static SceneGridSpace World(float cellSize) => new SceneGridSpace(null, cellSize);
        internal static SceneGridSpace For(RoomLifecycleManager manager) =>
            manager != null ? new SceneGridSpace(manager.transform, manager.CellSize) : World(1f);

        internal Vector3 SceneCellCenter(Vector2Int cell, float localDepth = 0f) =>
            ToWorld(new Vector3((cell.x + .5f) * CellSize, (cell.y + .5f) * CellSize, localDepth));

        internal Vector3 RegionCellCenter(Vector2Int cell, float localDepth = 0f) =>
            ToWorld(new Vector3((cell.x + .5f) * CellSize, -(cell.y + .5f) * CellSize, localDepth));

        internal Vector2Int WorldToSceneCell(Vector3 world)
        {
            Vector3 local = ToLocal(world);
            return new Vector2Int(Mathf.FloorToInt(local.x / CellSize), Mathf.FloorToInt(local.y / CellSize));
        }

        internal Vector2Int WorldToRegionCell(Vector3 world)
        {
            Vector3 local = ToLocal(world);
            return new Vector2Int(Mathf.FloorToInt(local.x / CellSize), -Mathf.FloorToInt(local.y / CellSize) - 1);
        }

        internal static Vector2Int SceneToRegion(Vector2Int cell) => new Vector2Int(cell.x, -cell.y - 1);
        internal static Vector2Int RegionToScene(Vector2Int cell) => new Vector2Int(cell.x, -cell.y - 1);

        internal bool Raycast(Ray ray, out Vector3 world)
        {
            Vector3 point = ToWorld(Vector3.zero);
            Vector3 normal = origin != null ? origin.forward : Vector3.forward;
            var plane = new Plane(normal, point);
            if (plane.Raycast(ray, out float distance))
            {
                world = ray.GetPoint(distance);
                return true;
            }
            world = default;
            return false;
        }

        private Vector3 ToWorld(Vector3 local) => origin != null ? origin.TransformPoint(local) : local;
        private Vector3 ToLocal(Vector3 world) => origin != null ? origin.InverseTransformPoint(world) : world;
        internal float LocalDepth(Vector3 world) => ToLocal(world).z;
    }

    internal readonly struct SceneLevelSummary
    {
        internal readonly int managers;
        internal readonly int rooms;
        internal readonly int corridors;
        internal readonly int regionCells;
        internal readonly int placedObjects;
        internal readonly int authorGroups;
        internal readonly int repeatedGroups;
        internal readonly int missingAuthorIds;
        internal readonly int resetMembers;
        internal readonly int artMaps;
        internal readonly float cellSize;

        private SceneLevelSummary(int managers, int rooms, int corridors, int regionCells,
            int placedObjects, int authorGroups, int repeatedGroups, int missingAuthorIds,
            int resetMembers, int artMaps, float cellSize)
        {
            this.managers = managers;
            this.rooms = rooms;
            this.corridors = corridors;
            this.regionCells = regionCells;
            this.placedObjects = placedObjects;
            this.authorGroups = authorGroups;
            this.repeatedGroups = repeatedGroups;
            this.missingAuthorIds = missingAuthorIds;
            this.resetMembers = resetMembers;
            this.artMaps = artMaps;
            this.cellSize = cellSize;
        }

        internal static SceneLevelSummary Read(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return default;
            var managerList = new List<RoomLifecycleManager>();
            var blockList = new List<LevelEditorPlacedBlock>();
            var resetList = new List<RoomResetObject>();
            var artList = new List<GridArtScene>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                managerList.AddRange(root.GetComponentsInChildren<RoomLifecycleManager>(true));
                blockList.AddRange(root.GetComponentsInChildren<LevelEditorPlacedBlock>(true));
                resetList.AddRange(root.GetComponentsInChildren<RoomResetObject>(true));
                artList.AddRange(root.GetComponentsInChildren<GridArtScene>(true));
            }

            int rooms = 0, corridors = 0, regionCells = 0;
            foreach (RoomLifecycleManager manager in managerList)
            foreach (RoomRegion region in manager.Regions)
            {
                if (region == null) continue;
                if (region.corridor) corridors++; else rooms++;
                regionCells += region.cells?.Count ?? 0;
            }

            var groupCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            int missing = 0;
            foreach (LevelEditorPlacedBlock block in blockList)
            {
                if (string.IsNullOrEmpty(block.PlanningBoxId)) { missing++; continue; }
                groupCounts.TryGetValue(block.PlanningBoxId, out int count);
                groupCounts[block.PlanningBoxId] = count + 1;
            }

            return new SceneLevelSummary(managerList.Count, rooms, corridors, regionCells,
                blockList.Count, groupCounts.Count, groupCounts.Values.Count(value => value > 1), missing,
                resetList.Count, artList.Count, managerList.Count > 0 ? managerList[0].CellSize : 1f);
        }
    }

    [Overlay(typeof(SceneView), "场景关卡编辑器", true)]
    public sealed class SceneLevelEditorOverlay : Overlay
    {
        internal static event Action ObjectPaintingRequested;
        internal static event Action<GridArtMap, int> ArtPaintingRequested;
        private static bool objectPaintingPending;
        private static GridArtMap pendingArtMap;
        private static int pendingArtBrush = -1;

        internal static bool HasPendingObjectPainting =>
            objectPaintingPending;

        internal static bool HasPendingArtPainting =>
            pendingArtMap != null && pendingArtBrush >= 0;

        public override VisualElement CreatePanelContent() =>
            new SceneLevelEditorPanel(containerWindow as SceneView);

        [MenuItem("Tools/2026TapTap/关卡编辑器/场景编辑器 _m")]
        public static void Open()
        {
            SceneView view = SceneView.lastActiveSceneView ?? EditorWindow.GetWindow<SceneView>();
            view.Show();
            view.Focus();
            var property = typeof(EditorWindow).GetProperty("overlayCanvas",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            object canvas = property?.GetValue(view);
            var overlaysProperty = canvas?.GetType().GetProperty("overlays",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (overlaysProperty?.GetValue(canvas) is System.Collections.IEnumerable overlays)
            {
                foreach (object item in overlays)
                    if (item is SceneLevelEditorOverlay overlay) { overlay.displayed = true; return; }
            }
            view.ShowNotification(new GUIContent("请在 SceneView 右上角 Overlays 中启用“场景关卡编辑器”"));
        }

        internal static void BeginObjectPainting()
        {
            objectPaintingPending = true;
            Open();
            EditorApplication.delayCall += () =>
            {
                ObjectPaintingRequested?.Invoke();
                SceneView.lastActiveSceneView?.Focus();
            };
        }

        internal static void MarkObjectPaintingHandled()
        {
            objectPaintingPending = false;
        }

        internal static void BeginArtPainting(
            GridArtMap map,
            int brushIndex)
        {
            pendingArtMap = map;
            pendingArtBrush = brushIndex;
            Open();
            EditorApplication.delayCall += () =>
            {
                ArtPaintingRequested?.Invoke(map, brushIndex);
                SceneView.lastActiveSceneView?.Focus();
            };
        }

        internal static void MarkArtPaintingHandled()
        {
            pendingArtMap = null;
            pendingArtBrush = -1;
        }
    }

    internal sealed class SceneLevelEditorPanel : VisualElement
    {
        private readonly SceneView sceneView;
        private readonly string sessionOwner;
        private readonly Button sessionStatus;
        private readonly Label modeStatus;
        private readonly Label summary;
        private readonly Label saveStatus;
        private readonly SceneRoomNavigator roomNavigator;
        private readonly Button lockButton;
        private readonly Dictionary<SceneEditMode, Button> modeButtons = new Dictionary<SceneEditMode, Button>();
        private readonly VisualElement regionPanel;
        private readonly DropdownField regionDropdown;
        private readonly TextField regionName;
        private readonly Toggle corridorToggle;
        private readonly Label regionStatus;
        private readonly TextField assemblyRegionIds;
        private readonly IntegerField assemblyDeltaX;
        private readonly IntegerField assemblyDeltaY;
        private readonly Label assemblyStatus;
        private readonly VisualElement assemblyChoices;
        private readonly HashSet<string> assemblySelection = new(StringComparer.Ordinal);
        private readonly VisualElement objectPanel;
        private DropdownField objectChannel;
        private ObjectField objectSourceAsset;
        private Label objectSourceDetails;
        private Button pingSourceButton;
        private Button paintSourceButton;
        private Button editSourceButton;
        private Image objectPreview;
        private Label objectToolStatus;
        private Label objectStatus;
        private readonly Dictionary<LevelEditorTool, Button> objectToolButtons =
            new Dictionary<LevelEditorTool, Button>();
        private readonly List<Button> selectedObjectButtons =
            new List<Button>();
        private readonly VisualElement bindingPanel;
        private InspectorElement selectedInspector;
        private ObjectField bindingConsumer;
        private ObjectField bindingSource;
        private Label bindingStatus;
        private readonly VisualElement artPanel;
        private DropdownField artLayer;
        private DropdownField artBrush;
        private Image artBrushPreview;
        private Label artBrushDetails;
        private IntegerField artTurns;
        private Toggle artErase;
        private Button artPaintToolButton;
        private Button artEraseToolButton;
        private Label artToolStatus;
        private Toggle artRectangle;
        private Toggle artFollowRegion;
        private Label artStatus;
        private readonly List<string> regionIds = new List<string>();
        private readonly List<ScenePaletteItem> paletteItems = new List<ScenePaletteItem>();
        private ScenePaletteItem selectedPaletteItem;
        private GridArtScene activeArtScene;
        private SceneEditMode mode;
        private string selectedRegionId = string.Empty;
        private int sceneHandle = -1;
        private int paletteRevision = -1;
        private bool summaryDirty = true;
        private bool navigationPaused;
        private readonly ScrollView body;
        private readonly SceneResourceDrawer resources;
        private readonly Foldout resourceFold;
        private readonly Foldout inspectorFold;
        private readonly List<UnityEditor.Editor> instanceEditors = new();
        private readonly Dictionary<string, Button> workTools = new();
        private readonly Dictionary<SceneObjectChannel, Button> workChannels = new();
        private Button artChannelButton;
        private readonly Button sceneSave, artSave;
        private bool compact;
        private bool artContext;
        private VisualElement artLayerBar;
        private Toggle layerVisible, layerLocked;
        private Foldout navigationFold;
        private int sessionVisualHash;
        private bool sessionVisualReady;

        internal SceneLevelEditorPanel(SceneView sceneView)
        {
            this.sceneView = sceneView;
            body = new ScrollView { name = "workbench-body", style = { minHeight = 0, flexShrink = 1 } };
            body.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            sessionOwner = "SceneLevelEditorOverlay:" + (sceneView != null ? sceneView.GetInstanceID().ToString() : "none");
            style.minWidth = 330f;
            style.maxWidth = 420f;
            style.width = 380f;
            style.maxHeight = 760f;
            style.color = SceneWorkbenchTheme.Text;
            style.backgroundColor = SceneWorkbenchTheme.Background;
            style.fontSize = 12;
            style.paddingLeft = 8f;
            style.paddingRight = 8f;
            style.paddingTop = 8f;
            style.paddingBottom = 8f;

            var title = new Label("关卡工作台");
            title.style.fontSize = 13f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            var heading = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            title.style.flexGrow = 1; heading.Add(title);
            heading.Add(new Button(() => { compact = !compact; body.style.display = compact ? DisplayStyle.None : DisplayStyle.Flex; }) { text = "展开 / 收起", tooltip = "最大化 Scene 时可收起资源与参数，只保留工具条。" });
            base.Add(heading);
            var subtitle = new Label("Scene 直接编辑 · 模板仅用于新建");
            subtitle.style.fontSize = 12f;
            subtitle.style.color = new Color(.72f, .75f, .79f);
            subtitle.style.marginBottom = 6f;
            subtitle.style.display = DisplayStyle.None;

            sessionStatus = new Button(ToggleLock) { name = "workbench-authoring-toggle" };
            sessionStatus.style.unityFontStyleAndWeight = FontStyle.Bold;
            sessionStatus.style.marginBottom = 4f;
            sessionStatus.style.paddingLeft = 8;
            sessionStatus.style.paddingTop = 6;
            sessionStatus.style.paddingBottom = 6;
            sessionStatus.style.borderLeftWidth = 4;
            sessionStatus.style.backgroundColor = SceneWorkbenchTheme.Surface;
            var stateRow = new VisualElement { style = { flexDirection = FlexDirection.Row, height = 32, marginTop = 4 } };
            sessionStatus.style.flexGrow = 1; sessionStatus.style.fontSize = 12;
            sessionStatus.tooltip = "点击切换落笔；结束搭建会恢复进入前的 Scene 视角。";
            stateRow.Add(sessionStatus);
            sceneSave = new Button(SaveSceneAuthor) { text = "Scene ✓" };
            artSave = new Button(SaveArtAuthors) { text = "美术 ✓" };
            foreach (var save in new[] { sceneSave, artSave }) { save.style.width = 72; save.style.height = 28; stateRow.Add(save); }
            base.Add(stateRow);

            lockButton = new Button(ToggleLock);
            lockButton.style.height = 34f;
            lockButton.style.unityFontStyleAndWeight = FontStyle.Bold;
            lockButton.style.display = DisplayStyle.None;
            saveStatus = new Label { style = { fontSize = 12, whiteSpace = WhiteSpace.Normal, marginTop = 4, unityFontStyleAndWeight = FontStyle.Bold } };
            saveStatus.style.display = DisplayStyle.None;

            var modes = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 6f } };
            AddModeButton(modes, SceneEditMode.Region, "1 房间");
            AddModeButton(modes, SceneEditMode.Object, "2 物体");
            AddModeButton(modes, SceneEditMode.Binding, "3 参数");
            AddModeButton(modes, SceneEditMode.Art, "4 美术");
            modes.style.display = DisplayStyle.None;
            base.Add(BuildWorkbenchTools());

            roomNavigator = new SceneRoomNavigator(SelectRoomFromNavigator);
            var navigation = new Foldout { text = "房间导航与区域", value = false, viewDataKey = "scene-room-navigation" };
            navigationFold = navigation;
            navigation.RegisterValueChangedCallback(evt => { if (evt.target == navigation && evt.newValue) RefreshNavigator(); });
            navigation.Add(roomNavigator);

            modeStatus = new Label();
            modeStatus.style.whiteSpace = WhiteSpace.Normal;
            modeStatus.style.fontSize = 12f;
            modeStatus.style.color = new Color(.72f, .75f, .79f);
            modeStatus.style.marginTop = 5f;
            modeStatus.style.display = DisplayStyle.None;

            regionPanel = new VisualElement();
            regionPanel.style.marginTop = 6f;
            regionDropdown = new DropdownField("当前房间/通道");
            regionDropdown.RegisterValueChangedCallback(_ =>
            {
                int index = regionDropdown.index;
                selectedRegionId = index >= 0 && index < regionIds.Count ? regionIds[index] : string.Empty;
                RefreshRegionControls();
                RefreshNavigator();
                BindAuthoring();
            });
            regionPanel.Add(regionDropdown);
            var createRow = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            createRow.Add(RegionButton("＋ 房间", () => CreateRegion(false)));
            createRow.Add(RegionButton("＋ 通道", () => CreateRegion(true)));
            regionPanel.Add(createRow);
            regionName = new TextField("名称") { isDelayed = true };
            regionName.RegisterValueChangedCallback(evt =>
            {
                RoomLifecycleManager manager = ActiveManager();
                if (SceneRegionAuthoring.Rename(manager, selectedRegionId, evt.newValue)) summaryDirty = true;
            });
            regionPanel.Add(regionName);
            corridorToggle = new Toggle("这是通道");
            corridorToggle.RegisterValueChangedCallback(evt =>
            {
                RoomLifecycleManager manager = ActiveManager();
                if (SceneRegionAuthoring.SetCorridor(manager, selectedRegionId, evt.newValue)) summaryDirty = true;
            });
            regionPanel.Add(corridorToggle);
            var brushRow = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            brushRow.Add(RegionButton("绘制", () => { SceneRegionAuthoring.Brush = SceneRegionBrush.Paint; RefreshRegionControls(); }));
            brushRow.Add(RegionButton("擦除", () => { SceneRegionAuthoring.Brush = SceneRegionBrush.Erase; RefreshRegionControls(); }));
            regionPanel.Add(brushRow);
            regionStatus = new Label();
            regionStatus.style.whiteSpace = WhiteSpace.Normal;
            regionStatus.style.fontSize = 12f;
            regionStatus.style.color = new Color(.72f, .75f, .79f);
            regionPanel.Add(regionStatus);

            var assembly = new Foldout { text = "区域整体移动（Scene 内装配）", value = false };
            assemblyChoices = new VisualElement();
            assembly.Add(new Label("勾选要一起移动的区域；不选时使用当前区域。"));
            var assemblyScroll = new ScrollView { style = { maxHeight = 144 } };
            assemblyScroll.Add(assemblyChoices); assembly.Add(assemblyScroll);
            assemblyRegionIds = new TextField("区域稳定 ID") { tooltip = "多个 ID 用逗号分隔；默认当前区域。" };
            var idsAdvanced = new Foldout { text = "高级：区域 ID", value = false };
            idsAdvanced.Add(assemblyRegionIds); assembly.Add(idsAdvanced);
            var deltaRow = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            assemblyDeltaX = new IntegerField("X 格") { value = 0 }; assemblyDeltaX.style.flexGrow = 1;
            assemblyDeltaY = new IntegerField("Y 格") { value = 0 }; assemblyDeltaY.style.flexGrow = 1;
            deltaRow.Add(assemblyDeltaX); deltaRow.Add(assemblyDeltaY); assembly.Add(deltaRow);
            var assemblyButtons = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            assemblyButtons.Add(RegionButton("预检", PreviewAssembly));
            assemblyButtons.Add(RegionButton("整体移动", MoveAssembly));
            assembly.Add(assemblyButtons);
            assemblyStatus = new Label { style = { whiteSpace = WhiteSpace.Normal, fontSize = 12, color = new Color(.72f, .75f, .79f) } };
            assembly.Add(assemblyStatus); regionPanel.Add(assembly);
            var regionEdit = new Foldout { text = "编辑房间 / 通道", value = false };
            regionEdit.RegisterValueChangedCallback(evt => { if (evt.target == regionEdit && evt.newValue) SelectMode(SceneEditMode.Region); });
            regionEdit.Add(regionPanel); navigation.Add(regionEdit);

            objectPanel = BuildObjectPanel();
            var objectMore = new Foldout { text = "更多 · 物体操作与模板", value = false }; objectMore.Add(objectPanel);
            bindingPanel = BuildBindingPanel(out selectedInspector, out bindingConsumer, out bindingSource, out bindingStatus);
            artPanel = BuildArtPanel();
            artLayer.RemoveFromHierarchy();
            artLayerBar = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 4 } };
            artLayer.label = "图层"; artLayer.style.flexGrow = 1; artLayer.style.minWidth = 0; artLayerBar.Add(artLayer);
            artLayer.labelElement.style.minWidth = 32; artLayer.labelElement.style.width = 32;
            artLayer.style.flexBasis = 0;
            layerVisible = new Toggle("显示"); layerLocked = new Toggle("锁定");
            foreach (var toggle in new[] { layerVisible, layerLocked }) { toggle.style.width = 58; toggle.style.flexShrink = 0; toggle.labelElement.style.minWidth = 28; toggle.labelElement.style.width = 28; }
            layerVisible.RegisterValueChangedCallback(evt => ChangeArtLayerFlags(evt.newValue, null));
            layerLocked.RegisterValueChangedCallback(evt => ChangeArtLayerFlags(null, evt.newValue));
            artLayerBar.Add(layerVisible); artLayerBar.Add(layerLocked); base.Add(artLayerBar);
            resources = new SceneResourceDrawer(ChooseResourceKind, ChooseObjectCard, ChooseArtCard);
            resourceFold = new Foldout { text = "资源 · 物体 / 美术", value = true, viewDataKey = "workbench-resources" };
            resourceFold.Add(resources); body.Add(resourceFold);
            var artSettings = new Foldout { text = "美术图层与笔刷设置", value = false }; artSettings.Add(artPanel); body.Add(artSettings);
            inspectorFold = new Foldout { text = "实例参数", value = true, viewDataKey = "workbench-inspector" };
            var inspectorScroll = new ScrollView { style = { maxHeight = 240 } }; inspectorScroll.Add(bindingPanel); inspectorFold.Add(inspectorScroll); body.Add(inspectorFold);
            body.Add(navigation); body.Add(objectMore);

            var overview = new Foldout { text = "诊断 · 当前 Scene（只读）", value = false };
            overview.style.marginTop = 6f;
            summary = new Label();
            summary.style.whiteSpace = WhiteSpace.Normal;
            summary.style.fontSize = 12f;
            overview.Add(summary);
            overview.Add(new Button(() => { summaryDirty = true; Refresh(); }) { text = "重新扫描（只读）" });
            body.Add(overview);
            SceneWorkbenchTheme.Section(resourceFold, SceneWorkbenchTheme.Object);
            SceneWorkbenchTheme.Section(inspectorFold, SceneWorkbenchTheme.Object);
            SceneWorkbenchTheme.Section(artSettings, SceneWorkbenchTheme.Art);
            SceneWorkbenchTheme.Section(navigation, SceneWorkbenchTheme.Room);
            SceneWorkbenchTheme.Section(objectMore, SceneWorkbenchTheme.Object);
            SceneWorkbenchTheme.Section(overview, SceneWorkbenchTheme.Muted);

            var notice = new HelpBox("打开、浏览、切模式和选择方块、道具或美术素材均为零写入。只有绘制、擦除、移动、改参、绑定、区域/美术归属与保存等显式作者动作会修改 Scene/资产，并进入 Unity Undo。", HelpBoxMessageType.Info);
            notice.style.marginTop = 6f;
            notice.style.display = DisplayStyle.None;
            base.Add(body);

            RegisterCallback<AttachToPanelEvent>(_ => Subscribe());
            RegisterCallback<DetachFromPanelEvent>(_ => Unsubscribe());
            schedule.Execute(Refresh).Every(200);
            RegisterCallback<GeometryChangedEvent>(_ => { if (sceneView != null) { style.maxHeight = Mathf.Max(220, sceneView.position.height * .85f); body.style.maxHeight = Mathf.Max(100, sceneView.position.height * .85f - 120); } });
            SelectMode(SceneEditMode.Object);
            Refresh();
        }

        private void AddModeButton(VisualElement parent, SceneEditMode value, string text)
        {
            var button = new Button(() => SelectMode(value)) { text = text };
            button.style.flexGrow = 1f;
            button.style.height = 30f;
            button.style.fontSize = 12f;
            button.style.marginLeft = 1f;
            button.style.marginRight = 1f;
            modeButtons.Add(value, button);
            parent.Add(button);
        }

        private void SelectMode(SceneEditMode value)
        {
            mode = value;
            if (mode == SceneEditMode.Object || mode == SceneEditMode.Art) artContext = mode == SceneEditMode.Art;
            foreach (var pair in modeButtons)
            {
                bool active = pair.Key == mode;
                pair.Value.style.unityFontStyleAndWeight = active
                    ? FontStyle.Bold
                    : FontStyle.Normal;
                pair.Value.style.backgroundColor = active
                    ? new Color(.20f, .29f, .39f)
                    : new Color(.20f, .21f, .23f);
                pair.Value.style.borderBottomWidth = active ? 2 : 0;
                pair.Value.style.borderBottomColor = new Color(.30f, .64f, 1f);
            }
            modeStatus.text = mode switch
            {
                SceneEditMode.Region => "先选房间或通道，再用鼠标直接绘制它的范围。",
                SceneEditMode.Object => "选择一种方块或道具进行放置；点已有物体可直接改参数。",
                SceneEditMode.Binding => "编辑当前所选物体的参数，或连接门、按钮和机关。",
                SceneEditMode.Art => "从素材卡片选择美术，再直接在 Scene 里铺设。",
                _ => string.Empty,
            };
            regionPanel.style.display = DisplayStyle.Flex;
            objectPanel.style.display = DisplayStyle.Flex;
            bindingPanel.style.display = DisplayStyle.Flex;
            artPanel.style.display = DisplayStyle.Flex;
            resources?.Bind(paletteItems, activeArtScene?.map, artContext);
            if (artLayerBar != null) artLayerBar.style.display = artContext ? DisplayStyle.Flex : DisplayStyle.None;
            BindAuthoring();
        }

        private VisualElement BuildWorkbenchTools()
        {
            var tools = new VisualElement { name = "workbench-tools", style = { flexDirection = FlexDirection.Row, marginTop = 4 } };
            foreach (var item in new[] { ("paint", "画 B"), ("erase", "擦 E"), ("rectangle", "矩形 R"), ("select", "点选 V") })
            {
                string key = item.Item1;
                var button = new Button(() => UseWorkbenchTool(key)) { text = item.Item2, style = { flexGrow = 1, height = 32, fontSize = 12 } };
                workTools[key] = button; tools.Add(button);
            }
            var group = new VisualElement(); group.Add(tools);
            var channels = new VisualElement { name = "workbench-channels", style = { flexDirection = FlexDirection.Row, marginTop = 4 } };
            foreach (var item in new[] { (SceneObjectChannel.TerrainSolid, "墙"), (SceneObjectChannel.WaterVolume, "水"), (SceneObjectChannel.GameplayProp, "道具") })
            {
                var channel = item.Item1;
                var button = new Button(() => { ChooseResourceKind(false); SceneObjectAuthoring.ActiveChannel = channel; LevelEditorState.Tool = LevelEditorTool.Select; RefreshSession(); })
                { text = item.Item2, tooltip = "选择/擦除只命中该通道；画笔通道由所选模板决定。", style = { flexGrow = 1, height = 26 } };
                workChannels[channel] = button; channels.Add(button);
            }
            artChannelButton = new Button(() => ChooseResourceKind(true)) { text = "美术", style = { flexGrow = 1, height = 26 } };
            channels.Add(artChannelButton);
            group.Add(channels);
            group.Add(new Button(() => { navigationPaused = false; SceneEditSession.Exit(sceneView, sessionOwner); RefreshSession(); })
            { text = "结束搭建 · 恢复原视角", style = { height = 22, fontSize = 11 }, tooltip = "恢复此 SceneView 进入搭建前的视角；不保存或改写作者数据。" });
            return group;
        }

        private void ChooseResourceKind(bool art)
        {
            artContext = art; SelectMode(art ? SceneEditMode.Art : SceneEditMode.Object);
            resources?.Bind(paletteItems, activeArtScene?.map, art);
        }

        private void ChooseObjectCard(ScenePaletteItem item)
        {
            ChooseResourceKind(false); ScenePlacementPaletteCatalog.Select(item);
            selectedPaletteItem = item; ApplyPaletteSelection(); LevelEditorState.Tool = LevelEditorTool.Paint;
            RefreshSession();
        }

        private void ChooseArtCard(int index)
        {
            ChooseResourceKind(true); SceneArtPaletteSelection.Select(activeArtScene?.map, index);
            artBrush.index = index; artErase.SetValueWithoutNotify(false); BindAuthoring(); RefreshSession();
        }

        private void UseWorkbenchTool(string tool)
        {
            if (tool == "select") { SelectMode(SceneEditMode.Object); LevelEditorState.Tool = LevelEditorTool.Select; }
            else if (mode == SceneEditMode.Region)
            {
                if (tool == "rectangle") { regionStatus.text = "区域使用连续四邻接笔画；矩形工具用于物体和美术。"; return; }
                SceneRegionAuthoring.Brush = tool == "erase" ? SceneRegionBrush.Erase : SceneRegionBrush.Paint;
            }
            else if (artContext)
            {
                SelectMode(SceneEditMode.Art); artErase.SetValueWithoutNotify(tool == "erase"); artRectangle.SetValueWithoutNotify(tool == "rectangle");
            }
            else
            {
                SelectMode(SceneEditMode.Object); LevelEditorState.Tool = tool == "erase" ? LevelEditorTool.Erase : LevelEditorTool.Paint;
                LevelEditorScenePainter.RectangleStroke = tool == "rectangle";
            }
            RefreshSession();
        }

        private void OnWorkbenchKeys(SceneView view)
        {
            Event evt = Event.current;
            if (view != sceneView || !SceneEditSession.IsLocked(view) || EditorGUIUtility.editingTextField || GUIUtility.hotControl != 0 ||
                evt == null || evt.type != EventType.KeyDown || evt.alt || evt.control || evt.command || EditorApplication.isPlayingOrWillChangePlaymode) return;
            string tool = evt.keyCode switch { KeyCode.B => "paint", KeyCode.E => "erase", KeyCode.R => "rectangle", KeyCode.V => "select", _ => null };
            if (tool == null) return;
            UseWorkbenchTool(tool); evt.Use();
        }

        private void ToggleLock()
        {
            if (sceneView == null) return;
            if (SceneEditSession.IsLocked(sceneView)) navigationPaused = !navigationPaused;
            else { navigationPaused = false; SceneEditSession.Enter(sceneView, sessionOwner); }
            RefreshSession();
        }

        private void Subscribe()
        {
            SceneWorkbenchInput.Register(sceneView, this);
            SceneView.duringSceneGui -= OnWorkbenchKeys; SceneView.duringSceneGui += OnWorkbenchKeys;
            SceneEditSession.Changed -= RefreshSession;
            SceneEditSession.Changed += RefreshSession;
            SceneRegionAuthoring.Changed -= OnRegionChanged;
            SceneRegionAuthoring.Changed += OnRegionChanged;
            SceneObjectAuthoring.Changed -= OnObjectChanged;
            SceneObjectAuthoring.Changed += OnObjectChanged;
            SceneArtAuthoring.Changed -= OnArtChanged;
            SceneArtAuthoring.Changed += OnArtChanged;
            SceneLevelEditorOverlay.ObjectPaintingRequested -= BeginObjectPainting;
            SceneLevelEditorOverlay.ObjectPaintingRequested += BeginObjectPainting;
            SceneLevelEditorOverlay.ArtPaintingRequested -= BeginArtPainting;
            SceneLevelEditorOverlay.ArtPaintingRequested += BeginArtPainting;
            SceneArtPaletteSelection.Changed -= OnArtBrushSelectionChanged;
            SceneArtPaletteSelection.Changed += OnArtBrushSelectionChanged;
            ScenePlacementPaletteCatalog.SelectionChanged -= OnPaletteSelectionChanged;
            ScenePlacementPaletteCatalog.SelectionChanged += OnPaletteSelectionChanged;
            Selection.selectionChanged -= RefreshSelectionInspector;
            Selection.selectionChanged += RefreshSelectionInspector;
            EditorApplication.hierarchyChanged -= MarkSummaryDirty;
            EditorApplication.hierarchyChanged += MarkSummaryDirty;
            Undo.undoRedoPerformed -= MarkSummaryDirty;
            Undo.undoRedoPerformed += MarkSummaryDirty;
            RefreshSelectionInspector();
            if (SceneLevelEditorOverlay.HasPendingObjectPainting)
            {
                schedule.Execute(BeginObjectPainting);
            }
            if (SceneLevelEditorOverlay.HasPendingArtPainting)
            {
                schedule.Execute(() => BeginArtPainting(
                    activeArtScene?.map,
                    SceneArtPaletteSelection.Current(activeArtScene?.map)));
            }
        }

        private void Unsubscribe()
        {
            SceneWorkbenchInput.Unregister(sceneView, this);
            SceneView.duringSceneGui -= OnWorkbenchKeys;
            foreach (var editor in instanceEditors) if (editor != null) UnityEngine.Object.DestroyImmediate(editor);
            instanceEditors.Clear();
            SceneEditSession.Changed -= RefreshSession;
            SceneRegionAuthoring.Changed -= OnRegionChanged;
            SceneObjectAuthoring.Changed -= OnObjectChanged;
            SceneArtAuthoring.Changed -= OnArtChanged;
            SceneLevelEditorOverlay.ObjectPaintingRequested -= BeginObjectPainting;
            SceneLevelEditorOverlay.ArtPaintingRequested -= BeginArtPainting;
            SceneArtPaletteSelection.Changed -= OnArtBrushSelectionChanged;
            ScenePlacementPaletteCatalog.SelectionChanged -= OnPaletteSelectionChanged;
            Selection.selectionChanged -= RefreshSelectionInspector;
            EditorApplication.hierarchyChanged -= MarkSummaryDirty;
            Undo.undoRedoPerformed -= MarkSummaryDirty;
            SceneEditSession.Exit(sceneView, sessionOwner);
            SceneRegionAuthoring.Bind(sceneView, null, string.Empty, false);
            LevelEditorScenePainter.SetOverlayAuthoring(sceneView, false);
            SceneArtAuthoring.Bind(sceneView, null, null, -1, 0, false, false, false, string.Empty, false);
        }

        private void MarkSummaryDirty() => summaryDirty = true;

        private void BeginObjectPainting()
        {
            if (sceneView != SceneView.lastActiveSceneView)
            {
                return;
            }

            SelectMode(SceneEditMode.Object);
            navigationPaused = false;
            if (!SceneEditSession.IsLocked(sceneView))
            {
                SceneEditSession.Enter(sceneView, sessionOwner);
            }
            selectedPaletteItem =
                ScenePlacementPaletteCatalog.Current(paletteItems) ??
                selectedPaletteItem;
            ApplyPaletteSelection();
            LevelEditorState.Tool = LevelEditorTool.Paint;
            SceneLevelEditorOverlay.MarkObjectPaintingHandled();
            RefreshSession();
        }

        private void BeginArtPainting(GridArtMap map, int brushIndex)
        {
            if (sceneView != SceneView.lastActiveSceneView)
            {
                return;
            }

            RefreshArtChoices();
            if (activeArtScene?.map != map ||
                map?.palette?.Get(brushIndex) == null)
            {
                return;
            }

            navigationPaused = false;
            SelectMode(SceneEditMode.Art);
            if (!SceneEditSession.IsLocked(sceneView))
            {
                SceneEditSession.Enter(sceneView, sessionOwner);
            }
            artBrush.index = brushIndex;
            artErase.SetValueWithoutNotify(false);
            RefreshArtToolState();
            SceneLevelEditorOverlay.MarkArtPaintingHandled();
            RefreshArtBrushPreview();
            BindAuthoring();
            RefreshSession();
            sceneView.Focus();
        }

        private void OnPaletteSelectionChanged()
        {
            selectedPaletteItem =
                ScenePlacementPaletteCatalog.Current(paletteItems);
            ApplyPaletteSelection();
        }

        private void Refresh()
        {
            RefreshSession();
            resources?.Bind(paletteItems, activeArtScene?.map, artContext);
            Scene scene = SceneManager.GetActiveScene();
            if (scene.handle != sceneHandle) { sceneHandle = scene.handle; summaryDirty = true; assemblySelection.Clear(); assemblyRegionIds.SetValueWithoutNotify(string.Empty); selectedRegionId = SessionState.GetString("SceneWorkbench.Region:" + scene.path, string.Empty); }
            if (paletteRevision != LevelEditorState.PaletteRevision)
            {
                paletteRevision = LevelEditorState.PaletteRevision;
                summaryDirty = true;
            }
            if (!summaryDirty) return;
            summaryDirty = false;
            SceneLevelSummary data = SceneLevelSummary.Read(scene);
            summary.text =
                $"区域管理器 {data.managers} · 房间 {data.rooms} · 通道 {data.corridors} · 区域格 {data.regionCells}\n" +
                $"真实对象 {data.placedObjects} · 作者组 {data.authorGroups} · 多实例组 {data.repeatedGroups}\n" +
                $"缺失作者 ID {data.missingAuthorIds} · 重置成员 {data.resetMembers} · GridArt {data.artMaps}\n" +
                $"格尺寸 {data.cellSize:0.###}；XY 是唯一搭建平面，保留对象原有 Z 渲染深度。";
            RefreshRegionChoices();
            RefreshObjectChoices();
            RefreshArtChoices();
            resources?.Bind(paletteItems, activeArtScene?.map, artContext);
        }

        private void RefreshSession()
        {
            Scene current = SceneManager.GetActiveScene();
            GridArtMap map = activeArtScene != null ? activeArtScene.map : null;
            GridArtLayer selectedLayer = map != null && artLayer.index >= 0 && artLayer.index < map.layers.Count ? map.layers[artLayer.index] : null;
            var ownership = SceneArtAuthoring.OwnershipFor(activeArtScene);
            bool locked = SceneEditSession.IsLocked(sceneView);
            bool artDirty = map != null && EditorUtility.IsDirty(map) || ownership != null && EditorUtility.IsDirty(ownership);
            int visualHash = (current.handle, current.isDirty, map, selectedLayer, selectedLayer?.visible, selectedLayer?.locked, locked, navigationPaused,
                artDirty, mode, artContext, LevelEditorState.Tool, LevelEditorScenePainter.RectangleStroke, SceneObjectAuthoring.ActiveChannel,
                artErase.value, artRectangle.value, EditorApplication.isPlayingOrWillChangePlaymode, EditorGUIUtility.isProSkin).GetHashCode();
            if (sessionVisualReady && visualHash == sessionVisualHash) { BindAuthoring(); return; }
            sessionVisualReady = true; sessionVisualHash = visualHash;
            layerVisible?.SetValueWithoutNotify(selectedLayer?.visible ?? false);
            layerLocked?.SetValueWithoutNotify(selectedLayer?.locked ?? false);
            artLayerBar?.SetEnabled(selectedLayer != null && !EditorApplication.isPlayingOrWillChangePlaymode);
            saveStatus.text = $"场景 {(current.isDirty ? "待保存" : "已保存")} · 美术 {(map != null && EditorUtility.IsDirty(map) ? "待保存" : "无更改")} · 归属 {(ownership != null && EditorUtility.IsDirty(ownership) ? "待保存" : "无更改")}";
            sceneSave.text = current.isDirty ? "Scene ●" : "Scene ✓";
            sceneSave.tooltip = current.isDirty ? "场景待保存 · 点击只保存当前 Scene" : "场景已保存";
            artSave.text = artDirty ? "美术 ●" : "美术 ✓";
            artSave.tooltip = saveStatus.text + "\n点击只保存当前 Map 与稀疏归属。";
            sceneSave.style.color = current.isDirty ? SceneWorkbenchTheme.Warning : SceneWorkbenchTheme.Saved;
            artSave.style.color = artDirty ? SceneWorkbenchTheme.Warning : SceneWorkbenchTheme.Saved;
            sceneSave.SetEnabled(!EditorApplication.isPlayingOrWillChangePlaymode);
            artSave.SetEnabled(map != null && !EditorApplication.isPlayingOrWillChangePlaymode);
            sessionStatus.text = locked
                ? navigationPaused ? "■ 暂停 · 点击继续" : "■ 落笔 · 点击暂停"
                : "■ 暂停 · 点击开启";
            Color identity = mode == SceneEditMode.Region ? SceneWorkbenchTheme.Room : artContext ? SceneWorkbenchTheme.Art : SceneWorkbenchTheme.Object;
            Color sessionColor = locked && !navigationPaused ? identity : SceneWorkbenchTheme.Warning;
            sessionStatus.style.color = sessionColor;
            sessionStatus.style.borderLeftColor = sessionColor;
            lockButton.text = locked
                ? navigationPaused ? "继续搭建（启用落笔）" : "结束搭建并恢复原视角"
                : "开始搭建（锁定正交 2D）";
            lockButton.style.backgroundColor = locked && !navigationPaused
                ? new Color(.24f, .25f, .27f)
                : new Color(.12f, .42f, .72f);
            lockButton.SetEnabled(sceneView != null && !EditorApplication.isPlayingOrWillChangePlaymode);
            string activeTool = mode == SceneEditMode.Region ? SceneRegionAuthoring.Brush == SceneRegionBrush.Erase ? "erase" : "paint" :
                artContext && mode == SceneEditMode.Art ? artErase.value ? "erase" : artRectangle.value ? "rectangle" : "paint" :
                LevelEditorState.Tool == LevelEditorTool.Select ? "select" : LevelEditorState.Tool == LevelEditorTool.Erase ? "erase" : LevelEditorScenePainter.RectangleStroke ? "rectangle" : "paint";
            foreach (var item in workTools) SceneWorkbenchTheme.Tool(item.Value, item.Key == activeTool, identity);
            foreach (var item in workChannels) SceneWorkbenchTheme.Tool(item.Value, !artContext && SceneObjectAuthoring.ActiveChannel == item.Key, SceneWorkbenchTheme.Object);
            SceneWorkbenchTheme.Tool(artChannelButton, artContext, SceneWorkbenchTheme.Art);
            SceneWorkbenchTheme.Section(resourceFold, artContext ? SceneWorkbenchTheme.Art : SceneWorkbenchTheme.Object);
            RefreshObjectToolState();
            BindAuthoring();
        }

        private void SaveCurrentAuthors()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || !EditorSceneManager.SaveScene(scene)) return;
            if (activeArtScene != null && activeArtScene.gameObject.scene == scene)
            {
                if (activeArtScene.map != null) AssetDatabase.SaveAssetIfDirty(activeArtScene.map);
                var ownership = SceneArtAuthoring.OwnershipFor(activeArtScene);
                if (ownership != null) AssetDatabase.SaveAssetIfDirty(ownership);
            }
            RefreshSession();
        }

        private void SaveSceneAuthor()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Scene scene = SceneManager.GetActiveScene();
            if (scene.IsValid() && scene.isLoaded) EditorSceneManager.SaveScene(scene);
            RefreshSession();
        }

        private void SaveArtAuthors()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || activeArtScene?.map == null) return;
            AssetDatabase.SaveAssetIfDirty(activeArtScene.map);
            var ownership = SceneArtAuthoring.OwnershipFor(activeArtScene);
            if (ownership != null) AssetDatabase.SaveAssetIfDirty(ownership);
            RefreshSession();
        }

        private void ChangeArtLayerFlags(bool? visible, bool? locked)
        {
            var map = activeArtScene?.map;
            if (map == null || artLayer.index < 0 || artLayer.index >= map.layers.Count || EditorApplication.isPlayingOrWillChangePlaymode) return;
            var layer = map.layers[artLayer.index];
            if (layer == null || (!visible.HasValue || visible.Value == layer.visible) && (!locked.HasValue || locked.Value == layer.locked)) return;
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("修改美术图层状态");
            Undo.RegisterCompleteObjectUndo(map, "修改美术图层状态");
            if (visible.HasValue) layer.visible = visible.Value;
            if (locked.HasValue) layer.locked = locked.Value;
            map.revision++; EditorUtility.SetDirty(map); Undo.CollapseUndoOperations(group);
            if (!string.IsNullOrEmpty(AssetDatabase.GetAssetPath(map)) && !string.IsNullOrEmpty(activeArtScene.gameObject.scene.path))
                Project.GridArt.Editor.GridArtBuilder.Apply(map, activeArtScene.gameObject.scene, false, false);
            BindAuthoring(); RefreshSession();
        }

        private Button RegionButton(string text, Action clicked)
        {
            var button = new Button(clicked) { text = text };
            button.style.flexGrow = 1f;
            button.style.marginLeft = 1f;
            button.style.marginRight = 1f;
            return button;
        }

        private RoomLifecycleManager ActiveManager() => SceneRegionAuthoring.FindManager(SceneManager.GetActiveScene());

        private void CreateRegion(bool corridor)
        {
            RoomLifecycleManager manager = ActiveManager();
            if (manager == null) { regionStatus.text = "当前 Scene 没有 RoomLifecycleManager。"; return; }
            selectedRegionId = SceneRegionAuthoring.CreateRegion(manager, corridor);
            summaryDirty = true;
            RefreshRegionChoices();
            BindAuthoring();
        }

        private void RefreshRegionChoices()
        {
            RoomLifecycleManager manager = ActiveManager();
            regionIds.Clear();
            var labels = new List<string>();
            if (manager != null)
            {
                foreach (RoomRegion region in manager.Regions)
                {
                    if (region == null) continue;
                    regionIds.Add(region.id);
                    labels.Add((string.IsNullOrEmpty(region.label) ? region.id : region.label) + (region.corridor ? " [通道]" : " [房间]"));
                }
            }
            regionDropdown.choices = labels;
            assemblySelection.RemoveWhere(id => !regionIds.Contains(id));
            assemblyChoices.Clear();
            for (int i = 0; i < regionIds.Count; i++)
            {
                string id = regionIds[i];
                var toggle = new Toggle(labels[i]); toggle.SetValueWithoutNotify(assemblySelection.Contains(id));
                toggle.RegisterValueChangedCallback(evt =>
                {
                    if (evt.newValue) assemblySelection.Add(id); else assemblySelection.Remove(id);
                    assemblyRegionIds.SetValueWithoutNotify(string.Join(",", assemblySelection));
                    assemblyStatus.text = "选择已改变，请重新预检。";
                });
                assemblyChoices.Add(toggle);
            }
            int selected = regionIds.IndexOf(selectedRegionId);
            if (selected < 0 && regionIds.Count > 0) { selected = 0; selectedRegionId = regionIds[0]; }
            regionDropdown.index = selected;
            RefreshNavigator();
            RefreshRegionControls();
        }

        private void SelectRoomFromNavigator(string id)
        {
            int index = regionIds.IndexOf(id);
            if (index < 0) return;
            selectedRegionId = id;
            navigationPaused = true;
            SessionState.SetString("SceneWorkbench.Region:" + SceneManager.GetActiveScene().path, id);
            regionDropdown.SetValueWithoutNotify(regionDropdown.choices[index]);
            regionDropdown.index = index;
            RefreshRegionControls();
            BindAuthoring();
            roomNavigator.Bind(ActiveManager(), selectedRegionId);
            SceneEditSession.Enter(sceneView, sessionOwner);
            SceneRoomNavigator.Frame(ActiveManager(), id, sceneView);
            RefreshSession();
            var navigation = roomNavigator.GetFirstAncestorOfType<Foldout>();
            if (navigation != null) navigation.value = false;
        }

        private void RefreshRegionControls()
        {
            RoomLifecycleManager manager = ActiveManager();
            RoomRegion region = manager?.Regions.FirstOrDefault(item => item != null && item.id == selectedRegionId);
            regionName.SetValueWithoutNotify(region?.label ?? string.Empty);
            corridorToggle.SetValueWithoutNotify(region != null && region.corridor);
            regionName.SetEnabled(region != null);
            corridorToggle.SetEnabled(region != null);
            regionStatus.text = (SceneRegionAuthoring.Brush == SceneRegionBrush.Paint ? "当前：绘制 · " : "当前：擦除 · ") + SceneRegionAuthoring.Status;
        }

        private void BindAuthoring()
        {
            bool activeView = !navigationPaused && sceneView != null && SceneView.lastActiveSceneView == sceneView;
            bool authoring = activeView && mode == SceneEditMode.Region && SceneEditSession.IsLocked(sceneView) && !EditorApplication.isPlayingOrWillChangePlaymode;
            SceneRegionAuthoring.Bind(sceneView, ActiveManager(), selectedRegionId, authoring);
            bool objectAuthoring = activeView && mode == SceneEditMode.Object && SceneEditSession.IsLocked(sceneView) && !EditorApplication.isPlayingOrWillChangePlaymode;
            LevelEditorScenePainter.SetOverlayAuthoring(sceneView, objectAuthoring);
            bool artAuthoring = activeView && mode == SceneEditMode.Art && SceneEditSession.IsLocked(sceneView) && !EditorApplication.isPlayingOrWillChangePlaymode;
            GridArtLayer layer = activeArtScene?.map != null && artLayer.index >= 0 && artLayer.index < activeArtScene.map.layers.Count ? activeArtScene.map.layers[artLayer.index] : null;
            SceneArtAuthoring.Bind(sceneView, activeArtScene, layer, artBrush.index, artTurns.value, artErase.value, artRectangle.value,
                artFollowRegion.value, selectedRegionId, artAuthoring);
        }

        private void OnRegionChanged()
        {
            summaryDirty = true;
            RefreshNavigator();
            RefreshRegionControls();
        }

        private void RefreshNavigator()
        {
            if (navigationFold != null && navigationFold.value) roomNavigator?.Bind(ActiveManager(), selectedRegionId);
        }

        private VisualElement BuildObjectPanel()
        {
            var panel = new VisualElement { style = { marginTop = 6 } };
            var chooseTitle = new Label("第 1 步　选择要放的东西");
            chooseTitle.style.fontSize = 13f;
            chooseTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            chooseTitle.style.marginBottom = 4f;
            panel.Add(chooseTitle);

            Button openPicker = new Button(ScenePlacementPaletteWindow.Open)
            {
                name = "open-placement-palette",
                text = "打开方块与道具列表…",
            };
            openPicker.style.height = 38f;
            openPicker.style.marginBottom = 5f;
            openPicker.style.unityFontStyleAndWeight = FontStyle.Bold;
            openPicker.style.backgroundColor = new Color(.16f, .18f, .21f);
            openPicker.style.borderTopWidth = openPicker.style.borderBottomWidth = openPicker.style.borderLeftWidth = openPicker.style.borderRightWidth = 1;
            openPicker.style.borderTopColor = openPicker.style.borderBottomColor = openPicker.style.borderLeftColor = openPicker.style.borderRightColor = new Color(.30f, .64f, 1f);
            panel.Add(openPicker);

            var currentBrush = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    paddingLeft = 6f,
                    paddingRight = 6f,
                    paddingTop = 5f,
                    paddingBottom = 5f,
                    backgroundColor = new Color(.16f, .17f, .19f),
                },
            };
            objectPreview = new Image { scaleMode = ScaleMode.ScaleToFit };
            objectPreview.style.width = 48f;
            objectPreview.style.height = 48f;
            objectPreview.style.marginRight = 8f;
            currentBrush.Add(objectPreview);
            objectSourceDetails = new Label
            {
                style =
                {
                    whiteSpace = WhiteSpace.Normal,
                    fontSize = 12f,
                    flexGrow = 1f,
                },
            };
            currentBrush.Add(objectSourceDetails);
            panel.Add(currentBrush);

            var sourceButtons = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            paintSourceButton = RegionButton("用当前内容开始画", StartPaintingSelectedSource);
            paintSourceButton.style.height = 32f;
            paintSourceButton.style.unityFontStyleAndWeight = FontStyle.Bold;
            paintSourceButton.style.backgroundColor = new Color(.12f, .42f, .72f);
            sourceButtons.Add(paintSourceButton);
            panel.Add(sourceButtons);

            var actionTitle = new Label("第 2 步　在场景中操作");
            actionTitle.style.fontSize = 13f;
            actionTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            actionTitle.style.marginTop = 9f;
            actionTitle.style.marginBottom = 3f;
            panel.Add(actionTitle);
            var tools = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            AddObjectToolButton(tools, LevelEditorTool.Select, "点选并改参数");
            AddObjectToolButton(tools, LevelEditorTool.Paint, "连续放置");
            AddObjectToolButton(tools, LevelEditorTool.Erase, "擦除");
            panel.Add(tools);
            objectToolStatus = new Label
            {
                style =
                {
                    whiteSpace = WhiteSpace.Normal,
                    fontSize = 12f,
                    marginTop = 4f,
                    marginBottom = 4f,
                },
            };
            panel.Add(objectToolStatus);

            var adjust = new Foldout { text = "调整当前选中的物体", value = true };
            var move = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            move.Add(SelectedObjectButton("左移一格", () => MoveSelected(Vector2Int.left)));
            move.Add(SelectedObjectButton("右移一格", () => MoveSelected(Vector2Int.right)));
            move.Add(SelectedObjectButton("下移一格", () => MoveSelected(Vector2Int.down)));
            move.Add(SelectedObjectButton("上移一格", () => MoveSelected(Vector2Int.up)));
            adjust.Add(move);
            var rebind = new Toggle("跨过房间边界时更新归属");
            rebind.tooltip = "关闭时，跨区域移动会被拒绝；开启后会把对象归到目标房间。";
            rebind.RegisterValueChangedCallback(evt => SceneObjectAuthoring.RebindAcrossRegions = evt.newValue);
            adjust.Add(rebind);
            var deletion = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            Button delete = SelectedObjectButton("删除选中物体", () => DeleteSelected(false));
            delete.style.color = new Color(1f, .62f, .62f);
            Button unlinkDelete = SelectedObjectButton("解除连接后删除", () => DeleteSelected(true));
            unlinkDelete.style.color = new Color(1f, .62f, .62f);
            deletion.Add(delete);
            deletion.Add(unlinkDelete);
            adjust.Add(deletion);
            panel.Add(adjust);

            objectStatus = new Label
            {
                style =
                {
                    whiteSpace = WhiteSpace.Normal,
                    fontSize = 12f,
                    color = new Color(.72f, .75f, .79f),
                    marginTop = 4f,
                },
            };
            panel.Add(objectStatus);

            var advanced = new Foldout { text = "高级：通道与模板管理", value = false };
            objectChannel = new DropdownField("擦除目标类型", new List<string> { "地形", "水体", "玩法道具", "标记/触发器", "装饰对象" }, 0);
            objectChannel.tooltip = "通常会随当前画笔自动选择；只有同一格叠放多种对象时才需要手动改。";
            objectChannel.RegisterValueChangedCallback(_ =>
            {
                SceneObjectAuthoring.ActiveChannel = (SceneObjectChannel)Mathf.Clamp(objectChannel.index, 0, 4);
                RefreshObjectStatus();
            });
            advanced.Add(objectChannel);
            objectSourceAsset = new ObjectField("当前模板资产") { objectType = typeof(GameObject), allowSceneObjects = false };
            objectSourceAsset.SetEnabled(false);
            advanced.Add(objectSourceAsset);
            var manageCurrent = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            pingSourceButton = RegionButton("在 Project 中查看", PingSelectedSource);
            editSourceButton = RegionButton("编辑当前模板", EditSelectedSource);
            manageCurrent.Add(pingSourceButton);
            manageCurrent.Add(editSourceButton);
            advanced.Add(manageCurrent);
            var addTemplates = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            addTemplates.Add(RegionButton("新建方块模板", LevelEditorEntryWindow.OpenForAdd));
            addTemplates.Add(RegionButton("添加道具 Prefab", LevelEditorPropEntryWindow.OpenForAdd));
            advanced.Add(addTemplates);
            advanced.Add(RegionButton("刷新方块与道具列表", () => { summaryDirty = true; Refresh(); }));
            advanced.Add(new HelpBox("模板只在生成时提供初始内容。已经放进 Scene 的物体不会被模板回改。", HelpBoxMessageType.Info));
            panel.Add(advanced);
            return panel;
        }

        private void AddObjectToolButton(
            VisualElement parent,
            LevelEditorTool tool,
            string text)
        {
            Button button = RegionButton(text, () => ActivateObjectTool(tool));
            button.style.height = 32f;
            button.style.fontSize = 12f;
            objectToolButtons[tool] = button;
            parent.Add(button);
        }

        private Button SelectedObjectButton(string text, Action clicked)
        {
            Button button = RegionButton(text, clicked);
            selectedObjectButtons.Add(button);
            button.SetEnabled(false);
            return button;
        }

        private VisualElement BuildBindingPanel(out InspectorElement inspector, out ObjectField consumer, out ObjectField source, out Label status)
        {
            var panel = new VisualElement { style = { marginTop = 6 } };
            panel.Add(new Label("真实实例参数") { style = { unityFontStyleAndWeight = FontStyle.Bold } });
            inspector = new InspectorElement(); panel.Add(inspector);
            var fold = new Foldout { text = "机关引用绑定", value = false };
            consumer = new ObjectField("消费者") { objectType = typeof(Component), allowSceneObjects = true };
            source = new ObjectField("来源 / 目标") { objectType = typeof(Component), allowSceneObjects = true };
            fold.Add(consumer); fold.Add(source);
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            row.Add(RegionButton("绑定门按钮", BindDoor)); row.Add(RegionButton("绑定机关", BindMechanism)); fold.Add(row);
            status = new Label { style = { whiteSpace = WhiteSpace.Normal, fontSize = 12, color = new Color(.72f, .75f, .79f) } }; fold.Add(status); panel.Add(fold);
            return panel;
        }

        private VisualElement BuildArtPanel()
        {
            var panel = new VisualElement { style = { marginTop = 6 } };
            Label chooseTitle = new Label("第 1 步　选择美术素材");
            chooseTitle.style.fontSize = 13f;
            chooseTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            chooseTitle.style.marginBottom = 4f;
            panel.Add(chooseTitle);
            Button openCards = new Button(() =>
                SceneArtPaletteWindow.Open(activeArtScene?.map))
            {
                text = "打开美术素材卡片…",
            };
            openCards.style.height = 38f;
            openCards.style.unityFontStyleAndWeight = FontStyle.Bold;
            openCards.style.backgroundColor = new Color(.12f, .42f, .72f);
            panel.Add(openCards);

            VisualElement current = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginTop = 5f,
                    marginBottom = 8f,
                    paddingLeft = 6f,
                    paddingRight = 6f,
                    paddingTop = 5f,
                    paddingBottom = 5f,
                    backgroundColor = new Color(.16f, .17f, .19f),
                },
            };
            artBrushPreview = new Image { scaleMode = ScaleMode.ScaleToFit };
            artBrushPreview.style.width = 52f;
            artBrushPreview.style.height = 52f;
            artBrushPreview.style.marginRight = 8f;
            current.Add(artBrushPreview);
            artBrushDetails = new Label
            {
                style =
                {
                    flexGrow = 1f,
                    whiteSpace = WhiteSpace.Normal,
                    fontSize = 12f,
                },
            };
            current.Add(artBrushDetails);
            panel.Add(current);

            Label actionTitle = new Label("第 2 步　在场景中铺美术");
            actionTitle.style.fontSize = 13f;
            actionTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            actionTitle.style.marginBottom = 4f;
            panel.Add(actionTitle);
            artErase = new Toggle { value = false };
            VisualElement artTools = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row },
            };
            artPaintToolButton = RegionButton(
                "绘制", () => ActivateArtTool(false));
            artEraseToolButton = RegionButton(
                "擦除", () => ActivateArtTool(true));
            artPaintToolButton.name = "art-paint-tool";
            artEraseToolButton.name = "art-erase-tool";
            artTools.Add(artPaintToolButton);
            artTools.Add(artEraseToolButton);
            panel.Add(artTools);
            artToolStatus = new Label
            {
                style =
                {
                    whiteSpace = WhiteSpace.Normal,
                    fontSize = 12f,
                    marginTop = 4f,
                    marginBottom = 5f,
                },
            };
            panel.Add(artToolStatus);
            artLayer = new DropdownField("GridArt 图层"); artLayer.RegisterValueChangedCallback(_ => BindAuthoring()); panel.Add(artLayer);
            artBrush = new DropdownField("笔刷"); artBrush.RegisterValueChangedCallback(_ => { RefreshArtBrushPreview(); BindAuthoring(); });
            artTurns = new IntegerField("旋转 90° 次数") { value = 0 }; artTurns.RegisterValueChangedCallback(_ => BindAuthoring()); panel.Add(artTurns);
            artRectangle = new Toggle("矩形"); artRectangle.RegisterValueChangedCallback(_ => BindAuthoring()); panel.Add(artRectangle);
            artFollowRegion = new Toggle("新笔画跟随当前区域"); artFollowRegion.RegisterValueChangedCallback(_ => BindAuthoring()); panel.Add(artFollowRegion);
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            row.Add(RegionButton("创建稀疏归属资产", CreateArtOwnership)); row.Add(RegionButton("认领当前层区域内美术", ClaimArt)); panel.Add(row);
            artStatus = new Label { style = { whiteSpace = WhiteSpace.Normal, fontSize = 12, color = new Color(.72f, .75f, .79f) } }; panel.Add(artStatus);
            RefreshArtToolState();
            return panel;
        }

        private void ActivateArtTool(bool erase)
        {
            SelectMode(SceneEditMode.Art);
            if (sceneView != null && !SceneEditSession.IsLocked(sceneView))
            {
                SceneEditSession.Enter(sceneView, sessionOwner);
            }
            artErase.SetValueWithoutNotify(erase);
            RefreshArtToolState();
            BindAuthoring();
            sceneView?.Focus();
        }

        private void RefreshArtToolState()
        {
            if (artPaintToolButton == null || artEraseToolButton == null)
            {
                return;
            }

            bool erase = artErase.value;
            SetArtToolStyle(artPaintToolButton, !erase);
            SetArtToolStyle(artEraseToolButton, erase);
            if (artToolStatus != null)
            {
                artToolStatus.text = erase
                    ? "当前：擦除当前 GridArt 图层。左键单击或拖动，Esc 停止。"
                    : "当前：绘制当前素材。左键单击或拖动，Esc 停止。";
            }
        }

        private static void SetArtToolStyle(Button button, bool active)
        {
            button.style.unityFontStyleAndWeight = active
                ? FontStyle.Bold
                : FontStyle.Normal;
            button.style.backgroundColor = active
                ? new Color(.12f, .42f, .72f)
                : new Color(.20f, .21f, .23f);
        }

        private string[] AssemblyIds() => assemblySelection.Count > 0 ? assemblySelection.OrderBy(id => id, StringComparer.Ordinal).ToArray() : (string.IsNullOrWhiteSpace(assemblyRegionIds.value) ? selectedRegionId ?? string.Empty : assemblyRegionIds.value)
            .Split(new[] { ',', '，', ';', '；', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(value => value.Trim()).Where(value => value.Length > 0).Distinct().ToArray();

        private void PreviewAssembly()
        {
            SceneRegionMovePreview preview = SceneRegionAssembly.Preview(ActiveManager(), AssemblyIds(), new Vector2Int(assemblyDeltaX.value, assemblyDeltaY.value));
            assemblyStatus.text = preview.ToString();
        }

        private void MoveAssembly()
        {
            var preview = SceneRegionAssembly.Preview(ActiveManager(), AssemblyIds(), new Vector2Int(assemblyDeltaX.value, assemblyDeltaY.value));
            assemblyStatus.text = preview.ToString();
            if (!preview.CanCommit || !EditorUtility.DisplayDialog("整体移动预览", preview + $"\n位移：X {assemblyDeltaX.value}、Y {assemblyDeltaY.value} 格", "确认整体移动", "取消")) return;
            if (SceneRegionAssembly.Move(ActiveManager(), AssemblyIds(), new Vector2Int(assemblyDeltaX.value, assemblyDeltaY.value), out string result))
                summaryDirty = true;
            assemblyStatus.text = result;
        }

        private void RefreshObjectChoices()
        {
            LevelEditorPalette palette =
                ScenePlacementPaletteCatalog.LoadPalette();
            RoomLifecycleManager manager = ActiveManager();
            if (manager != null) LevelEditorState.CellSize = manager.CellSize;
            paletteItems.Clear();
            paletteItems.AddRange(
                ScenePlacementPaletteCatalog.Build(palette));
            selectedPaletteItem =
                ScenePlacementPaletteCatalog.Current(paletteItems) ??
                paletteItems.FirstOrDefault();
            ApplyPaletteSelection();
            RefreshObjectStatus();
        }

        private void ApplyPaletteSelection()
        {
            ScenePaletteItem item = selectedPaletteItem;
            if (item == null)
            {
                LevelEditorState.SelectedEntryIndex = -1;
                LevelEditorState.SelectedPropIndex = -1;
                objectSourceAsset?.SetValueWithoutNotify(null);
                if (objectSourceDetails != null) objectSourceDetails.text = "没有符合筛选条件的模板。";
                pingSourceButton?.SetEnabled(false);
                paintSourceButton?.SetEnabled(false);
                editSourceButton?.SetEnabled(false);
                if (objectPreview != null)
                {
                    objectPreview.image = null;
                    objectPreview.style.backgroundColor =
                        new Color(.12f, .13f, .15f);
                }
                return;
            }
            pingSourceButton.SetEnabled(true);
            paintSourceButton.SetEnabled(true);
            editSourceButton.SetEnabled(true);
            ScenePlacementPaletteCatalog.Select(item);
            objectChannel.SetValueWithoutNotify(objectChannel.choices[(int)item.channel]);
            objectChannel.index = (int)item.channel;
            objectSourceAsset.SetValueWithoutNotify(item.prefab);

            Texture preview = item.prefab != null
                ? AssetPreview.GetAssetPreview(item.prefab) ??
                  AssetPreview.GetMiniThumbnail(item.prefab)
                : null;
            objectPreview.image = preview;
            objectPreview.style.backgroundColor = preview == null
                ? item.color
                : new Color(.1f, .11f, .13f);

            objectSourceDetails.text =
                $"当前选择\n{item.KindLabel} · {item.displayName}\n" +
                $"{ChannelLabel(item.channel)} · 初始参数 {item.overrideCount} 项";
        }

        private static string ChannelLabel(SceneObjectChannel channel) => channel switch
        {
            SceneObjectChannel.TerrainSolid => "地形",
            SceneObjectChannel.WaterVolume => "水体",
            SceneObjectChannel.GameplayProp => "玩法道具",
            SceneObjectChannel.MarkerTrigger => "标记/触发器",
            SceneObjectChannel.DecorationObject => "装饰对象",
            _ => channel.ToString(),
        };

        private void PingSelectedSource()
        {
            UnityEngine.Object target = selectedPaletteItem?.prefab != null
                ? selectedPaletteItem.prefab
                : selectedPaletteItem?.palette;
            if (target == null) return;
            Selection.activeObject = target;
            EditorGUIUtility.PingObject(target);
        }

        private void StartPaintingSelectedSource()
        {
            selectedPaletteItem =
                ScenePlacementPaletteCatalog.Current(paletteItems) ??
                selectedPaletteItem;
            if (selectedPaletteItem == null)
            {
                objectStatus.text = "请先点击上方蓝色按钮选择方块或道具。";
                return;
            }
            ApplyPaletteSelection();
            SceneLevelEditorOverlay.BeginObjectPainting();
            objectStatus.text =
                $"已选择 {selectedPaletteItem.displayName}。在 Scene 中单击或拖动放置，按 Esc 停止。";
        }

        private void ActivateObjectTool(LevelEditorTool tool)
        {
            SelectMode(SceneEditMode.Object);
            if (sceneView != null &&
                !SceneEditSession.IsLocked(sceneView))
            {
                SceneEditSession.Enter(sceneView, sessionOwner);
            }
            LevelEditorState.Tool = tool;
            RefreshObjectToolState();
            BindAuthoring();
            sceneView?.Focus();
        }

        private void RefreshObjectToolState()
        {
            if (objectToolStatus == null)
            {
                return;
            }

            LevelEditorTool current = LevelEditorState.Tool;
            foreach (KeyValuePair<LevelEditorTool, Button> pair in
                     objectToolButtons)
            {
                bool active = pair.Key == current;
                pair.Value.style.unityFontStyleAndWeight = active
                    ? FontStyle.Bold
                    : FontStyle.Normal;
                pair.Value.style.backgroundColor = active
                    ? new Color(.12f, .42f, .72f)
                    : new Color(.20f, .21f, .23f);
            }

            objectToolStatus.text = current switch
            {
                LevelEditorTool.Select =>
                    "当前：点选并改参数。单击物体会选择整个作者根并打开实例参数。",
                LevelEditorTool.Paint => selectedPaletteItem != null
                    ? $"当前：连续放置“{selectedPaletteItem.displayName}”。单击或拖动，Esc 停止。"
                    : "当前：连续放置。请先选择一种方块或道具。",
                LevelEditorTool.Erase =>
                    $"当前：擦除{(SceneObjectAuthoring.ActiveChannel.HasValue ? ChannelLabel(SceneObjectAuthoring.ActiveChannel.Value) : "当前格物体")}。单击或拖动，Esc 停止。",
                _ => "请选择上方的一种操作。",
            };
        }

        private void EditSelectedSource()
        {
            if (selectedPaletteItem == null)
            {
                objectStatus.text = "请先选择一个方块或道具模板。";
                return;
            }
            if (selectedPaletteItem.isProp) LevelEditorPropEntryWindow.OpenForEdit(selectedPaletteItem.index);
            else LevelEditorEntryWindow.OpenForEdit(selectedPaletteItem.index);
        }

        private void RefreshObjectStatus()
        {
            SceneObjectIndex index = SceneObjectAuthoring.Read(SceneManager.GetActiveScene());
            SceneObjectRecord selected = SelectedRecord();
            if (selected?.block != null)
            {
                int groupCount = index.Group(selected).Count;
                objectStatus.text =
                    $"已选：{selected.block.name} · {ChannelLabel(selected.channel)}" +
                    (groupCount > 1 ? $" · 同组 {groupCount} 个物体" : string.Empty);
                return;
            }

            int[] counts = new int[5];
            foreach (SceneObjectRecord record in index.All)
            {
                counts[(int)record.channel]++;
            }
            objectStatus.text =
                $"尚未选中已有物体。当前场景共有 {index.All.Count} 个可编辑对象。";
        }

        private SceneObjectRecord SelectedRecord()
        {
            LevelEditorPlacedBlock block = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponentInParent<LevelEditorPlacedBlock>() : null;
            return block != null ? SceneObjectAuthoring.Read(block.gameObject.scene).All.FirstOrDefault(item => item.block == block) : null;
        }

        private void MoveSelected(Vector2Int delta)
        {
            SceneObjectRecord record = SelectedRecord();
            if (!SceneObjectAuthoring.MoveGroup(record, delta, SceneObjectAuthoring.RebindAcrossRegions, out string reason)) objectStatus.text = reason;
            else { objectStatus.text = "已整体移动所选逻辑组。"; summaryDirty = true; }
        }

        private void DeleteSelected(bool unlink)
        {
            SceneObjectRecord record = SelectedRecord();
            if (!SceneObjectAuthoring.DeleteGroup(record, unlink, out string reason)) objectStatus.text = reason;
            else { objectStatus.text = unlink ? "已解除反向引用并删除整个逻辑组。" : "已删除整个逻辑组。"; summaryDirty = true; }
        }

        private void RefreshSelectionInspector()
        {
            selectedInspector.Unbind();
            selectedInspector.Clear();
            foreach (var editor in instanceEditors) if (editor != null) UnityEngine.Object.DestroyImmediate(editor);
            instanceEditors.Clear();
            LevelEditorPlacedBlock block =
                LevelEditorBlockFactory.SelectedPlacedBlock();
            if (block != null)
            {
                inspectorFold.text = "实例参数 · " + block.name;
                foreach (var component in block.GetComponents<MonoBehaviour>())
                {
                    if (component == null) continue;
                    var editor = UnityEditor.Editor.CreateEditor(component);
                    if (editor == null) continue;
                    instanceEditors.Add(editor);
                    var fold = new Foldout { text = ObjectNames.NicifyVariableName(component.GetType().Name), value = component is not RoomResetObject };
                    fold.Add(new InspectorElement(editor)); selectedInspector.Add(fold);
                }
            }
            else
            {
                inspectorFold.text = "实例参数";
                selectedInspector.Add(new Label("点选场景中的物体以编辑真实参数。") { style = { whiteSpace = WhiteSpace.Normal, color = new Color(.63f,.65f,.68f) } });
            }
            foreach (Button button in selectedObjectButtons)
            {
                button.SetEnabled(block != null);
            }
            RefreshObjectStatus();
        }

        private void BindDoor()
        {
            DoorController door = (bindingConsumer.value as Component)?.GetComponent<DoorController>();
            DoorButton button = (bindingSource.value as Component)?.GetComponent<DoorButton>();
            SceneObjectAuthoring.BindDoor(door, button, out string reason);
            bindingStatus.text = string.IsNullOrEmpty(reason) ? "门与按钮双向引用已绑定。" : reason;
        }

        private void BindMechanism()
        {
            SceneObjectAuthoring.BindMechanism(bindingConsumer.value as Component, bindingSource.value as Component, out string reason);
            bindingStatus.text = string.IsNullOrEmpty(reason) ? "机关真实 Unity 引用已绑定。" : reason;
        }

        private void RefreshArtChoices()
        {
            activeArtScene = null;
            Scene scene = SceneManager.GetActiveScene();
            if (scene.IsValid()) foreach (GameObject root in scene.GetRootGameObjects())
            {
                activeArtScene = root.GetComponentInChildren<GridArtScene>(true);
                if (activeArtScene != null) break;
            }
            GridArtMap map = activeArtScene?.map;
            artLayer.choices = map != null ? map.layers.Select(item => item?.label ?? "<空图层>").ToList() : new List<string>();
            artLayer.index = artLayer.choices.Count > 0 ? Mathf.Clamp(artLayer.index, 0, artLayer.choices.Count - 1) : -1;
            artBrush.choices = map?.palette != null ? map.palette.brushes.Select((item, i) => $"{i} · {item?.label ?? "<空笔刷>"}").ToList() : new List<string>();
            artBrush.index = artBrush.choices.Count > 0
                ? SceneArtPaletteSelection.Current(map)
                : -1;
            RefreshArtBrushPreview();
            RefreshArtToolState();
            artStatus.text = map == null ? "当前 Scene 没有已绑定的 GridArtMap；不会自动创建。" :
                $"Map {map.name} · 图层 {map.layers.Count} · 美术格 {map.layers.Sum(item => item?.cells?.Count ?? 0)} · {(SceneArtAuthoring.OwnershipFor(activeArtScene) == null ? "旧美术默认未归属" : "已绑定稀疏归属资产")}\n{SceneArtAuthoring.Status}";
            BindAuthoring();
        }

        private void OnArtBrushSelectionChanged(
            GridArtMap map,
            int index)
        {
            if (activeArtScene?.map != map || artBrush == null)
            {
                return;
            }

            artBrush.index = index;
            RefreshArtBrushPreview();
            BindAuthoring();
        }

        private void RefreshArtBrushPreview()
        {
            if (artBrushPreview == null || artBrushDetails == null)
            {
                return;
            }

            GridArtBrush brush = activeArtScene?.map?.palette?.Get(
                artBrush?.index ?? -1);
            if (brush == null)
            {
                artBrushPreview.sprite = null;
                artBrushDetails.text = "尚未选择美术素材";
                return;
            }

            artBrushPreview.sprite = brush.autoTerrain
                ? brush.GetPiece(4) ?? brush.sprite
                : brush.sprite;
            artBrushDetails.text =
                $"当前素材\n{brush.label}\n" +
                $"{(brush.autoTerrain ? "自动地形" : brush.category)} · " +
                $"{brush.DisplaySize.x:0.##} × {brush.DisplaySize.y:0.##} 格";
        }

        private void CreateArtOwnership()
        {
            GridArtRegionOwnership ownership = SceneArtAuthoring.CreateOwnership(activeArtScene);
            artStatus.text = ownership != null ? "已创建并绑定稀疏归属资产；旧 13817 格不会被批量改写。" : "无法创建归属资产，请先绑定已保存的 GridArtMap。";
        }

        private void ClaimArt()
        {
            GridArtLayer layer = activeArtScene?.map != null && artLayer.index >= 0 && artLayer.index < activeArtScene.map.layers.Count ? activeArtScene.map.layers[artLayer.index] : null;
            string preview = SceneArtAuthoring.PreviewClaim(activeArtScene, layer, ActiveManager(), selectedRegionId, out bool canClaim);
            artStatus.text = preview;
            if (!canClaim || !EditorUtility.DisplayDialog("认领美术预览", preview, "确认认领", "取消")) return;
            int count = SceneArtAuthoring.ClaimRegion(activeArtScene, layer, ActiveManager(), selectedRegionId);
            artStatus.text = count > 0 ? $"已显式认领 {count} 个当前层锚点。" : SceneArtAuthoring.Status;
        }

        private void OnObjectChanged() { summaryDirty = true; RefreshObjectStatus(); }
        private void OnArtChanged() { summaryDirty = true; if (artStatus != null) artStatus.text = SceneArtAuthoring.Status; }
    }
}
