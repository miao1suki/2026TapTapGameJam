using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Project.GridArt.Editor
{
    public sealed partial class GridArtWindow : EditorWindow
    {
        [SerializeField] private GridArtMap map;
        [SerializeField] private int layerIndex;
        [SerializeField] private int brushIndex;
        [SerializeField] private int brushQuarterTurns;
        internal int BrushQuarterTurns => Brush != null && !Brush.autoTerrain ? GridArtRotation.Normalize(brushQuarterTurns) : 0;
        internal Vector2Int BrushFootprint => Brush != null ? GridArtRotation.Size(Brush.Size,BrushQuarterTurns) : Vector2Int.one;
        internal Vector2 BrushDisplaySize => Brush != null ? GridArtRotation.Size(Brush.DisplaySize,BrushQuarterTurns) : Vector2.one;
        private ArtCanvas canvas;
        private VisualElement sidebar;
        private ObjectField mapField;
        private Label status;
        private bool erase, rectangle = true;
        private readonly List<Rect> guides = new List<Rect>();
        private bool showGuides = true;
        private bool showLevelReference = true;
        private float levelReferenceOpacity = .35f;
        private string selectedReferenceRoom = "";
        private string boundSceneKey;
        private int boundSceneHandle;
        private string SceneKey
        {
            get
            {
                var scene = SceneManager.GetActiveScene();
                string id = AssetDatabase.AssetPathToGUID(scene.path);
                if (string.IsNullOrEmpty(id)) id = "unsaved_" + scene.handle;
                return "2026TapTap.GridArt." + Hash128.Compute(Application.dataPath) + "." + id;
            }
        }
        public GridArtMap Map => map;
        public GridArtLayer Layer => map != null && layerIndex >= 0 && layerIndex < map.layers.Count ? map.layers[layerIndex] : null;
        public GridArtBrush Brush => map != null && map.palette != null ? map.palette.Get(brushIndex) : null;
        internal bool CanEditMap => map != null && (string.IsNullOrEmpty(map.ownerSceneGuid) || map.ownerSceneGuid == AssetDatabase.AssetPathToGUID(SceneManager.GetActiveScene().path));
        private int LayerOrder(int index) => map.layers[index].sortingOrder == int.MinValue ? index - 100 : map.layers[index].sortingOrder;
        private IEnumerable<int> OrderedLayers => Enumerable.Range(0,map.layers.Count)
            .OrderBy(index => map.layers[index].depth > 0f ? 0 : 1)
            .ThenBy(LayerOrder).ThenByDescending(index => map.layers[index].depth);

        [MenuItem("Tools/2026TapTap/关卡美术/网格美术编辑器")]
        public static void Open() => GetWindow<GridArtWindow>("网格美术");
        public static void OpenForGrid(float size, IEnumerable<Rect> reference = null)
        {
            var window = GetWindow<GridArtWindow>("网格美术");
            window.guides.Clear(); if (reference != null) window.guides.AddRange(reference);
            window.canvas?.Invalidate();
            window.RequestSource();
            if (window.map == null) window.CreateMap(Mathf.Max(.01f, size));
            else if (!Mathf.Approximately(window.map.cellSize, size))
                window.ShowNotification(new GUIContent("美术图格尺寸不同，请在网格设置中核对。"));
        }

        private void OnEnable()
        {
            Undo.undoRedoPerformed += OnArtUndo;
            GridArtLevelSource.Changed += RequestSource;
            EditorApplication.hierarchyChanged += RequestSource;
            EditorApplication.update += AutomationTick;
            AssemblyReloadEvents.beforeAssemblyReload += FlushBeforeReload;
            EditorSceneManager.sceneSaving += ArtSceneSaving;
            EditorSceneManager.activeSceneChangedInEditMode += SceneChanged;
            EditorSceneManager.sceneOpened += SceneOpened;
            EditorApplication.playModeStateChanged += PlayChanged;
        }
        private void OnDisable()
        {
            StopScenePainting();
            canvas?.CancelStroke(); FlushPending(true);
            Undo.undoRedoPerformed -= OnArtUndo;
            GridArtLevelSource.Changed -= RequestSource;
            EditorApplication.hierarchyChanged -= RequestSource;
            EditorApplication.update -= AutomationTick;
            AssemblyReloadEvents.beforeAssemblyReload -= FlushBeforeReload;
            EditorSceneManager.sceneSaving -= ArtSceneSaving;
            EditorSceneManager.activeSceneChangedInEditMode -= SceneChanged;
            EditorSceneManager.sceneOpened -= SceneOpened;
            EditorApplication.playModeStateChanged -= PlayChanged;
            canvas?.RememberView();
        }
        private void FlushBeforeReload() { StopScenePainting(); canvas?.CancelStroke(); FlushPending(true); }
        private void ArtSceneSaving(Scene scene, string path) { if (map != null && scene == SceneManager.GetActiveScene()) { if (sourcePending) RefreshLevelSource(); FlushPending(false); } }
        private void SceneChanged(Scene before, Scene after) => RebindSceneIfNeeded();
        private void SceneOpened(Scene scene, OpenSceneMode mode) => RebindSceneIfNeeded();
        private void RebindSceneIfNeeded()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || boundSceneKey == SceneKey && boundSceneHandle == scene.handle) return;
            // OpenScene and the active-scene notification are not a single UI update.
            // Make rebinding idempotent, and also call it before reading a new source.
            StopScenePainting(); canvas?.CancelStroke(); FlushDataForPreviousScene();
            canvas?.RememberView(); RememberSelection();
            BindRemembered(); BindAutomation(); canvas?.RestoreView();
            RefreshNavigation(); Refresh();
        }
        private void PlayChanged(PlayModeStateChange state) { if (state == PlayModeStateChange.ExitingEditMode) { StopScenePainting(); canvas?.CancelStroke(); if(sourcePending) RefreshLevelSource(); FlushPending(false); } }
        private void OnFocus()
        {
            RebindSceneIfNeeded();
            if (map?.palette != null && AssetDatabase.GetAssetPath(map.palette) == GridArtLibraryService.SharedPath)
                GridArtLibraryService.Synchronize(map.palette);
            RequestSource();
            if (map != null) foreach (var layer in map.layers) layer.InvalidateLookup();
            if (map != null && (EditorUtility.IsDirty(map) || map.palette != null && EditorUtility.IsDirty(map.palette))) QueuePersistence();
            Refresh();
        }
        private void BindRemembered()
        {
            string guid = EditorPrefs.GetString(SceneKey, "");
            map = AssetDatabase.LoadAssetAtPath<GridArtMap>(AssetDatabase.GUIDToAssetPath(guid));
            if (map == null) foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                var output = root.GetComponentInChildren<GridArtScene>(true);
                if (output != null && output.map != null) { map = output.map; break; }
            }
            if (map == null && !string.IsNullOrEmpty(SceneManager.GetActiveScene().path))
            {
                string sceneGuid = AssetDatabase.AssetPathToGUID(SceneManager.GetActiveScene().path);
                foreach (var candidateGuid in AssetDatabase.FindAssets("t:GridArtMap"))
                {
                    var candidate = AssetDatabase.LoadAssetAtPath<GridArtMap>(AssetDatabase.GUIDToAssetPath(candidateGuid));
                    if (candidate.ownerSceneGuid == sceneGuid) { map = candidate; break; }
                }
            }
            RestoreSelection();
        }
        private string SelectionKey => "2026TapTap.GridArt.Selection." + Hash128.Compute(Application.dataPath) + "." + AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(map));
        private void RememberSelection()
        { if (map != null) { EditorPrefs.SetInt(SelectionKey+".Layer",layerIndex); EditorPrefs.SetInt(SelectionKey+".Brush",brushIndex); EditorPrefs.SetInt(SelectionKey+".Rotation",BrushQuarterTurns); } }
        private void RestoreSelection()
        {
            if (map == null) { layerIndex=brushIndex=0; return; }
            int initial=map.layers.FindIndex(l=>l.label=="墙地表面");
            layerIndex=Mathf.Clamp(EditorPrefs.GetInt(SelectionKey+".Layer",Mathf.Max(0,initial)),0,Mathf.Max(0,map.layers.Count-1));
            brushIndex=EditorPrefs.GetInt(SelectionKey+".Brush",0);
            brushQuarterTurns=GridArtRotation.Normalize(EditorPrefs.GetInt(SelectionKey+".Rotation",0));
        }
        public void CreateGUI()
        {
            BuildWorkspace();
        }
        public void SetMap(GridArtMap value)
        {
            StopScenePainting(); canvas?.CancelStroke(); FlushPending(true); EndPreview();
            canvas?.RememberView(); RememberSelection(); map = value; RestoreSelection();
            EditorPrefs.SetString(SceneKey, value != null ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(value)) : "");
            BindAutomation(); mapField?.SetValueWithoutNotify(value); canvas?.RestoreView(); Refresh();
        }
        private void CreateMap(float size)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (PrefabStageUtility.GetCurrentPrefabStage() != null) { ShowNotification(new GUIContent("请退出预制体模式，在关卡场景创建美术图。")); return; }
            var scene = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(scene.path)) { ShowNotification(new GUIContent("请先保存关卡场景，再创建本关美术图。")); return; }
            string path = AssetDatabase.GenerateUniqueAssetPath(System.IO.Path.ChangeExtension(scene.path, null) + "_Art.asset");
            var data = CreateInstance<GridArtMap>(); data.cellSize = size;
            data.ownerSceneGuid = AssetDatabase.AssetPathToGUID(scene.path);
            data.palette = GridArtLibraryService.Shared(); GridArtLibraryService.Synchronize(data.palette);
            data.layers.Add(new GridArtLayer { label = "远景背景", depth = 5, parallax = new Vector2(.3f, .3f) });
            data.layers.Add(new GridArtLayer { label = "近景背景", depth = 2, parallax = new Vector2(.7f, .7f) });
            data.layers.Add(new GridArtLayer { label = "墙地表面", depth = -.51f, sortingOrder = -100 });
            data.layers.Add(new GridArtLayer { label = "场景装饰", depth = -.55f, sortingOrder = -80 });
            AssetDatabase.CreateAsset(data, path); SetMap(data); layerIndex = 2; Refresh();
        }
        private void Apply()
        {
            try
            {
                GridArtBuilder.Apply(map, SceneManager.GetActiveScene(), false, autoSave);
                diskSignature = DiskSignature(map); buildPending = false;
                if (autoSave) savePending = false;
                UpdateSaveStatus();
            }
            catch (Exception e) { saveError = e.Message; status.text = e.Message; }
        }
        private void Change(string title, Action operation, bool refresh = true)
        {
            if (map == null || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!CanEditMap) { status.text = "此图属于其他场景，请先创建独立副本。"; return; }
            Undo.RecordObject(map, title); operation(); map.revision++; EditorUtility.SetDirty(map);
            ArtChanged(refresh);
        }
        public void Refresh()
        {
            RefreshWorkspace();
        }
        private void MoveLayer(int direction)
        {
            var sorted = OrderedLayers.ToList(); int place = sorted.IndexOf(layerIndex), next = place + direction;
            if (next < 0 || next >= sorted.Count) return;
            Change("调整美术层顺序", () =>
            {
                if (LayerOrder(sorted[place]) == LayerOrder(sorted[next]))
                    for (int i=0;i<sorted.Count;i++) map.layers[sorted[i]].sortingOrder = -200+i*10;
                int a = LayerOrder(layerIndex), b = LayerOrder(sorted[next]);
                map.layers[layerIndex].sortingOrder = b; map.layers[sorted[next]].sortingOrder = a;
            });
        }

        private sealed class ArtCanvas : VisualElement
        {
            private readonly GridArtWindow owner;
            private Vector2 pan = new Vector2(240, 320), panStart, pointerStart;
            private float zoom = 32;
            private Vector2Int start, last;
            private Vector2 pointer;
            private int gesture = -1, pointerId, strokeGroup;
            private bool dirty, canceled, eraseStroke;
            private bool effectGesture;
            private bool needsInitialFrame;
            private Rect? initialFrameBounds;
            private string restoredViewKey;
            public bool IsStroking => effectGesture || gesture >= 0 && gesture != 2;
            public void CancelStroke()
            {
                if(effectGesture)
                {
                    effectGesture = false; owner.EffectCancelGesture();
                    if(this.HasPointerCapture(pointerId)) this.ReleasePointer(pointerId);
                    RepaintView(); return;
                }
                if (!IsStroking) return;
                gesture = -1; dirty = false; Undo.RevertAllDownToGroup(strokeGroup);
                if (this.HasPointerCapture(pointerId)) this.ReleasePointer(pointerId);
                Invalidate();
            }
            private List<(GridArtQuad, Color)> quads;
            private readonly Dictionary<GridArtLayer, List<GridArtQuad>> layerQuads = new Dictionary<GridArtLayer, List<GridArtQuad>>();
            private readonly VisualElement backgroundArtwork, artwork, overlay;
            private int backgroundQuadCount;
            private readonly GridArtLevelReference reference;
            private readonly List<GridArtPreviewBatch> batches = new List<GridArtPreviewBatch>();
            private Vector2Int lastHover;
            public ArtCanvas(GridArtWindow window)
            {
                owner = window; focusable = true; style.overflow = Overflow.Hidden; style.backgroundColor = new Color(.09f,.11f,.14f);
                backgroundArtwork = CreateArtworkContainer("background-artwork-cache");
                Add(backgroundArtwork);
                reference = new GridArtLevelReference { name = "art-level-reference", pickingMode = PickingMode.Ignore };
                reference.StretchToParentSize(); Add(reference);
                artwork = CreateArtworkContainer("artwork-cache");
                Add(artwork);
                overlay = new VisualElement { name = "art-overlay", pickingMode = PickingMode.Ignore };
                overlay.StretchToParentSize(); overlay.generateVisualContent += Draw; Add(overlay);
                RegisterCallback<GeometryChangedEvent>(e =>
                {
                    // A source refresh can change the reference toolbar height after
                    // initial framing. Keep that initial fit centered until navigation,
                    // rather than persisting an offset from the previous layout.
                    if (initialFrameBounds.HasValue && !IsStroking && e.oldRect.size != e.newRect.size)
                        Frame(initialFrameBounds.Value, true);
                    else TryInitialFrame();
                    RepaintView();
                });
                RegisterCallback<PointerDownEvent>(Down); RegisterCallback<PointerMoveEvent>(Move);
                RegisterCallback<PointerUpEvent>(Up); RegisterCallback<PointerCaptureOutEvent>(e =>
                {
                    if(effectGesture) { effectGesture=false; owner.EffectCancelGesture(); RepaintView(); }
                    if (gesture >= 0 && gesture != 2 && dirty && owner.map != null)
                    {
                        owner.map.revision++; EditorUtility.SetDirty(owner.map); Undo.CollapseUndoOperations(strokeGroup); owner.ArtChanged();
                    }
                    gesture = -1;
                });
                RegisterCallback<DetachFromPanelEvent>(evt => { if(effectGesture) CancelStroke(); });
                RegisterCallback<FocusOutEvent>(evt => { if(effectGesture) CancelStroke(); });
                RegisterCallback<WheelEvent>(e => { if(IsStroking) return; needsInitialFrame = false; var before = e.localMousePosition; float old = zoom; zoom = Mathf.Clamp(zoom * Mathf.Exp(-e.delta.y * .04f), .75f, 192); pan = before + (pan - before) * (zoom / old); e.StopPropagation(); RepaintView(); RememberView(); });
                RegisterCallback<KeyDownEvent>(e =>
                {
                    if (e.keyCode == KeyCode.Escape) { CancelStroke(); e.StopPropagation(); RepaintView(); }
                    if ((e.ctrlKey || e.commandKey) && e.keyCode == KeyCode.Z) { if (e.shiftKey) Undo.PerformRedo(); else Undo.PerformUndo(); e.StopPropagation(); }
                });
                RegisterCallback<AttachToPanelEvent>(e => RestoreView());
            }
            private static VisualElement CreateArtworkContainer(string name)
            {
                var container = new VisualElement { name = name, pickingMode = PickingMode.Ignore, usageHints = UsageHints.DynamicTransform };
                container.style.position = Position.Absolute;
                container.style.width = container.style.height = 0;
                container.style.transformOrigin = new TransformOrigin(0, 0);
                return container;
            }
            private string Key => owner.SceneKey + ".View." + (owner.map != null ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(owner.map)) : "reference-only");
            public void RememberView()
            {
                initialFrameBounds = null;
                PersistView();
            }
            private void PersistView()
            {
                // SceneChanged arrives after Unity changed its active scene. Save under
                // the key captured when this view was restored, never the new scene's key.
                if (needsInitialFrame || string.IsNullOrEmpty(restoredViewKey)) return;
                EditorPrefs.SetFloat(restoredViewKey + ".Zoom", zoom); EditorPrefs.SetFloat(restoredViewKey + ".X", pan.x); EditorPrefs.SetFloat(restoredViewKey + ".Y", pan.y);
            }
            public void RestoreView()
            {
                initialFrameBounds = null;
                restoredViewKey = Key; needsInitialFrame = !EditorPrefs.HasKey(Key + ".Zoom");
                zoom = EditorPrefs.GetFloat(Key + ".Zoom", 32); pan = new Vector2(EditorPrefs.GetFloat(Key + ".X", 240), EditorPrefs.GetFloat(Key + ".Y", 320));
                UpdateReference(); TryInitialFrame(); RepaintView();
            }
            public void UpdateReference()
            {
                reference.Configure(owner.levelSource);
                TryInitialFrame(); RepaintView();
            }
            private void TryInitialFrame()
            {
                if (!needsInitialFrame || owner.levelSource == null || contentRect.width < 100 || contentRect.height < 100 || IsStroking) return;
                if (!FrameRoom(owner.selectedReferenceRoom) && !FrameLevel()) FrameAll();
            }
            public bool FrameRoom(string id)
            {
                if (owner.levelSource == null || string.IsNullOrEmpty(id)) return false;
                var room = owner.levelSource.rooms.FirstOrDefault(item => item.id == id);
                return room != null && FrameWorld(room.boundsWorld);
            }
            public bool FrameLevel() => owner.levelSource != null && owner.levelSource.hasWorldBounds && FrameWorld(owner.levelSource.worldBounds);
            private bool FrameWorld(Rect world)
            {
                float size = Mathf.Max(.01f, owner.map != null ? owner.map.cellSize : owner.levelSource?.cellSize ?? 1f);
                return Frame(new Rect(world.position / size, world.size / size));
            }
            private bool Frame(Rect bounds, bool preserveInitialFit = false)
            {
                if (IsStroking || contentRect.width < 100 || contentRect.height < 100 || bounds.width <= 0 || bounds.height <= 0) return false;
                bool initial = preserveInitialFit || needsInitialFrame;
                zoom = Mathf.Clamp(Mathf.Min((contentRect.width - 48) / Mathf.Max(1,bounds.width), (contentRect.height - 48) / Mathf.Max(1,bounds.height)),.75f,64);
                pan = contentRect.center + new Vector2(-bounds.center.x,bounds.center.y)*zoom;
                needsInitialFrame = false; initialFrameBounds = initial ? bounds : null;
                PersistView(); RepaintView(); return true;
            }
            public void FrameAll()
            {
                bool found = false; Rect bounds = default;
                if(owner.map != null) foreach (var layer in owner.map.layers) foreach (var q in GridArtBuilder.BuildQuads(layer, owner.map.palette))
                {
                    bounds = found ? Rect.MinMaxRect(Mathf.Min(bounds.xMin,q.bounds.xMin),Mathf.Min(bounds.yMin,q.bounds.yMin),Mathf.Max(bounds.xMax,q.bounds.xMax),Mathf.Max(bounds.yMax,q.bounds.yMax)) : q.bounds;
                    found = true;
                }
                if (!found) foreach (var guide in owner.guides)
                { bounds = found ? Rect.MinMaxRect(Mathf.Min(bounds.xMin, guide.xMin), Mathf.Min(bounds.yMin, guide.yMin), Mathf.Max(bounds.xMax, guide.xMax), Mathf.Max(bounds.yMax, guide.yMax)) : guide; found = true; }
                if (found) Frame(bounds); else FrameLevel();
            }
            public void Invalidate() { quads = null; layerQuads.Clear(); RepaintView(); }
            private Vector2Int Cell(Vector2 p) => new Vector2Int(Mathf.FloorToInt((p.x - pan.x) / zoom), Mathf.FloorToInt((pan.y - p.y) / zoom));
            private Rect Screen(Rect r) => new Rect(pan.x + r.x * zoom, pan.y - r.yMax * zoom, r.width * zoom, r.height * zoom);
            private static RectInt Selection(Vector2Int a, Vector2Int b) => new RectInt(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Abs(a.x - b.x) + 1, Mathf.Abs(a.y - b.y) + 1);
            private void Down(PointerDownEvent e)
            {
                if (gesture >= 0 || effectGesture) return;
                if (e.button != 2 && owner.previewTravel.sqrMagnitude > .0001f) { owner.status.text = "视差预览中 · 点击复位后继续绘制"; e.StopPropagation(); return; }
                Focus(); pointer = e.localPosition; start = last = Cell(pointer); pointerStart = pointer; panStart = pan;
                if (e.button == 2) needsInitialFrame = false;
                if (e.button != 2 && owner.EffectToolActive)
                {
                    if(owner.EffectPointerDown(start,e.button))
                    {
                        pointerId=e.pointerId; effectGesture=true; this.CapturePointer(pointerId);
                        e.StopPropagation(); RepaintView();
                    }
                    return;
                }
                if (e.button != 2 && (!owner.CanEditMap || owner.Layer == null || owner.Layer.locked || owner.Layer.followLevel || !owner.Layer.visible || owner.map.palette == null || owner.Brush == null || EditorApplication.isPlayingOrWillChangePlaymode)) return;
                gesture = e.button; pointerId = e.pointerId; dirty = canceled = false; eraseStroke = owner.erase || e.button == 1;
                this.CapturePointer(e.pointerId); e.StopPropagation();
                if (gesture != 2) { Undo.IncrementCurrentGroup(); strokeGroup = Undo.GetCurrentGroup(); Undo.RegisterCompleteObjectUndo(owner.map, "绘制网格美术"); if (!owner.rectangle) Paint(start); }
                RepaintView();
            }
            private void Move(PointerMoveEvent e)
            {
                pointer = e.localPosition;
                if(effectGesture) { owner.EffectPointerMove(Cell(pointer)); e.StopPropagation(); RepaintView(); return; }
                if (gesture == 2) pan = panStart + pointer - pointerStart;
                else if (gesture >= 0 && !owner.rectangle)
                {
                    var now = Cell(pointer); int steps = Mathf.Max(Mathf.Abs(now.x - last.x), Mathf.Abs(now.y - last.y));
                    if (steps <= 4096) for (int i = 1; i <= steps; i++) Paint(Vector2Int.RoundToInt(Vector2.Lerp(last, now, (float)i / steps)));
                    last = now;
                }
                if (gesture >= 0 || Cell(pointer) != lastHover) RepaintView();
                lastHover = Cell(pointer);
                if (gesture >= 0) e.StopPropagation();
            }
            private void Up(PointerUpEvent e)
            {
                if(effectGesture)
                {
                    effectGesture=false; owner.EffectPointerUp(Cell(e.localPosition));
                    if(this.HasPointerCapture(pointerId)) this.ReleasePointer(pointerId);
                    e.StopPropagation(); RepaintView(); return;
                }
                if (gesture < 0) return;
                if (gesture != 2 && owner.rectangle && !canceled)
                {
                    var rect = Selection(start, Cell(e.localPosition));
                    if ((long)rect.width * rect.height <= 4096)
                    {
                        Vector2Int step = eraseStroke || owner.Brush.autoTerrain ? Vector2Int.one : owner.BrushFootprint;
                        for (int y = rect.yMin; y < rect.yMax; y += step.y)
                            for (int x = rect.xMin; x < rect.xMax; x += step.x) Paint(new Vector2Int(x,y));
                    }
                    else owner.ShowNotification(new GUIContent("一次框选最多 4096 格。"));
                }
                gesture = -1; this.ReleasePointer(e.pointerId); RememberView();
                if (dirty) { owner.map.revision++; EditorUtility.SetDirty(owner.map); Undo.CollapseUndoOperations(strokeGroup); owner.ArtChanged(); }
                e.StopPropagation(); RepaintView();
            }
            private void Paint(Vector2Int p)
            {
                if (owner.Layer == null) return;
                bool changed = owner.PaintSceneCell(p,eraseStroke);
                dirty |= changed; if (changed) { quads = null; layerQuads.Remove(owner.Layer); }
            }
            public void RepaintView()
            {
                if (quads == null)
                {
                    quads = new List<(GridArtQuad, Color)>();
                    backgroundQuadCount = 0;
                    if (owner.map != null && owner.map.palette != null)
                        foreach (int index in owner.OrderedLayers)
                        {
                            var layer = owner.map.layers[index];
                            if (!layerQuads.TryGetValue(layer,out var cached)) layerQuads[layer]=cached=GridArtBuilder.BuildQuads(layer,owner.map.palette);
                            if (layer.visible) foreach (var q in cached)
                            {
                                Vector2 offset = GridArtScene.LayerOffset(owner.previewTravel, layer, 1f, Project.ObjectArtwork.GridArtworkMetrics.PixelsPerCell);
                                quads.Add((new GridArtQuad(new Rect(q.bounds.position + offset, q.bounds.size), q.uv, q.texture, q.normal, q.colorId, q.quarterTurns), layer.tint));
                                if (layer.depth > 0f) backgroundQuadCount++;
                            }
                        }
                    backgroundArtwork.Clear(); artwork.Clear(); batches.Clear();
                    for (int begin = 0; begin < quads.Count;)
                    {
                        int end = begin + 1;
                        // Do not merge across the level-reference plane even when
                        // both layers use the same texture (e.g. solid wall brushes).
                        int limit = begin < backgroundQuadCount ? backgroundQuadCount : quads.Count;
                        while (end < limit && end - begin < 256 && quads[end].Item1.texture == quads[begin].Item1.texture) end++;
                        var batch = new GridArtPreviewBatch(quads, begin, end - begin);
                        batches.Add(batch); (begin < backgroundQuadCount ? backgroundArtwork : artwork).Add(batch); begin = end;
                    }
                }
                artwork.style.translate = new Translate(pan.x, pan.y);
                artwork.style.scale = new Scale(Vector3.one * (zoom / GridArtPreviewBatch.Units));
                backgroundArtwork.style.translate = artwork.style.translate;
                backgroundArtwork.style.scale = artwork.style.scale;
                float size = Mathf.Max(.01f, owner.map != null ? owner.map.cellSize : owner.levelSource?.cellSize ?? 1f);
                reference.SetView(pan, zoom / size, contentRect);
                reference.SetReferenceVisibility(owner.showLevelReference, owner.levelReferenceOpacity);
                foreach (var batch in batches) batch.SetVisible(Screen(batch.Bounds).Overlaps(contentRect));
                overlay.MarkDirtyRepaint();
            }
            private void Draw(MeshGenerationContext context)
            {
                var painter = context.painter2D;
                GridViewportDrawing.Draw(context, contentRect, pan, zoom, new Color(.36f, .42f, .5f, .5f));
                if (owner.showGuides)
                {
                    painter.strokeColor = new Color(1,.8f,.25f,.65f); painter.lineWidth = 2; painter.BeginPath();
                    foreach (var guide in owner.guides)
                    {
                        Rect r = Screen(guide); if (!r.Overlaps(contentRect)) continue;
                        painter.MoveTo(r.min); painter.LineTo(new Vector2(r.xMax,r.yMin)); painter.LineTo(r.max); painter.LineTo(new Vector2(r.xMin,r.yMax)); painter.ClosePath();
                    }
                    painter.Stroke();
                }
                owner.DrawEffectOverlay(painter,Screen,contentRect);
                if(owner.EffectToolActive) return;
                if (owner.Layer == null) return;
                RectInt selected = Selection(start, Cell(pointer));
                Rect area = gesture >= 0 && gesture != 2 && owner.rectangle ? new Rect(selected.position, selected.size) : new Rect(Cell(pointer), owner.Brush != null && !owner.Brush.autoTerrain ? owner.BrushDisplaySize : Vector2.one);
                Rect box = Screen(area); painter.strokeColor = eraseStroke && gesture >= 0 || owner.erase ? new Color(1,.3f,.25f) : new Color(.25f,.8f,1);
                painter.lineWidth = 2; painter.BeginPath(); painter.MoveTo(box.min); painter.LineTo(new Vector2(box.xMax,box.yMin)); painter.LineTo(box.max); painter.LineTo(new Vector2(box.xMin,box.yMax)); painter.ClosePath(); painter.Stroke();
            }
        }
    }
}
