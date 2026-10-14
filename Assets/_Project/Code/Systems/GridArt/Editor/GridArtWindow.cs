using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Project.GridArt.Editor
{
    public sealed class GridArtWindow : EditorWindow
    {
        [SerializeField] private GridArtMap map;
        [SerializeField] private int layerIndex;
        [SerializeField] private int brushIndex;
        private ArtCanvas canvas;
        private VisualElement sidebar;
        private ObjectField mapField;
        private Label status;
        private bool erase, rectangle = true;
        private readonly List<Rect> guides = new List<Rect>();
        private bool showGuides = true;
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

        [MenuItem("Tools/2026TapTap/关卡美术/网格美术编辑器")]
        public static void Open() => GetWindow<GridArtWindow>("网格美术");
        public static void OpenForGrid(float size, IEnumerable<Rect> reference = null)
        {
            var window = GetWindow<GridArtWindow>("网格美术");
            window.guides.Clear(); if (reference != null) window.guides.AddRange(reference);
            window.canvas?.Invalidate();
            if (window.map == null) window.CreateMap(Mathf.Max(.01f, size));
            else if (!Mathf.Approximately(window.map.cellSize, size))
                window.ShowNotification(new GUIContent("美术图格尺寸不同，请在网格设置中核对。"));
        }

        private void OnEnable()
        {
            Undo.undoRedoPerformed += Refresh;
            EditorSceneManager.activeSceneChangedInEditMode += SceneChanged;
        }
        private void OnDisable()
        {
            Undo.undoRedoPerformed -= Refresh;
            EditorSceneManager.activeSceneChangedInEditMode -= SceneChanged;
            canvas?.RememberView();
        }
        private void SceneChanged(Scene before, Scene after) { canvas?.RememberView(); BindRemembered(); canvas?.RestoreView(); Refresh(); }
        private void OnFocus() => Refresh();
        private void BindRemembered()
        {
            string guid = EditorPrefs.GetString(SceneKey, "");
            map = AssetDatabase.LoadAssetAtPath<GridArtMap>(AssetDatabase.GUIDToAssetPath(guid));
        }
        public void CreateGUI()
        {
            minSize = new Vector2(760, 460);
            BindRemembered();
            rootVisualElement.style.flexDirection = FlexDirection.Column;
            var toolbar = new Toolbar();
            toolbar.style.flexWrap = Wrap.Wrap; toolbar.style.height = StyleKeyword.Auto; toolbar.style.minHeight = 28;
            mapField = new ObjectField("美术图") { objectType = typeof(GridArtMap), value = map };
            mapField.style.width = 320;
            mapField.RegisterValueChangedCallback(e => SetMap(e.newValue as GridArtMap)); toolbar.Add(mapField);
            toolbar.Add(new ToolbarButton(() => CreateMap(1)) { text = "新建" });
            toolbar.Add(new ToolbarButton(() => GridArtDemo.Create(this)) { text = "载入素材示例" });
            toolbar.Add(new ToolbarButton(() => EditorApplication.ExecuteMenuItem("Tools/2026TapTap/方块贴画/导入不规则瓦片图")) { text = "切片导入" });
            toolbar.Add(new ToolbarButton(() => canvas?.FrameAll()) { text = "居中全部" });
            toolbar.Add(new ToolbarButton(Apply) { text = "应用到当前场景" });
            toolbar.Add(new ToolbarButton(() => { AssetDatabase.SaveAssets(); if (map != null) EditorGUIUtility.PingObject(map); }) { text = "保存" });
            rootVisualElement.Add(toolbar);
            var content = new VisualElement(); content.style.flexGrow = 1; content.style.flexDirection = FlexDirection.Row;
            canvas = new ArtCanvas(this); canvas.style.flexGrow = 1; content.Add(canvas);
            sidebar = new ScrollView(); sidebar.style.width = 288; sidebar.style.paddingLeft = 12; sidebar.style.paddingRight = 12;
            sidebar.style.backgroundColor = new Color(.14f, .15f, .17f); content.Add(sidebar);
            rootVisualElement.Add(content);
            status = new Label(); status.style.paddingLeft = 12; status.style.paddingTop = 6; status.style.paddingBottom = 6;
            status.style.whiteSpace = WhiteSpace.Normal;
            rootVisualElement.Add(status);
            Refresh();
        }
        public void SetMap(GridArtMap value)
        {
            canvas?.RememberView(); map = value; layerIndex = brushIndex = 0;
            EditorPrefs.SetString(SceneKey, value != null ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(value)) : "");
            mapField?.SetValueWithoutNotify(value); canvas?.RestoreView(); Refresh();
        }
        private void CreateMap(float size)
        {
            string path = EditorUtility.SaveFilePanelInProject("新建美术图", "美术图", "asset", "选择此关卡的美术数据保存位置");
            if (string.IsNullOrEmpty(path)) return;
            var data = CreateInstance<GridArtMap>(); data.cellSize = size;
            data.layers.Add(new GridArtLayer { label = "远景背景", depth = 5, parallax = new Vector2(.3f, .3f) });
            data.layers.Add(new GridArtLayer { label = "近景背景", depth = 2, parallax = new Vector2(.7f, .7f) });
            data.layers.Add(new GridArtLayer { label = "场景装饰", depth = -.51f });
            AssetDatabase.CreateAsset(data, path); SetMap(data);
        }
        private void Apply()
        {
            try
            {
                var output = GridArtBuilder.Apply(map, SceneManager.GetActiveScene());
                Refresh();
                EditorGUIUtility.PingObject(output); status.text = "已应用 · 请保存当前 Scene";
            }
            catch (Exception e) { status.text = e.Message; }
        }
        private void Change(string title, Action operation)
        {
            if (map == null || EditorApplication.isPlayingOrWillChangePlaymode) return;
            Undo.RecordObject(map, title); operation(); map.revision++; EditorUtility.SetDirty(map); Refresh();
        }
        public void Refresh()
        {
            if (sidebar == null) return;
            mapField.SetValueWithoutNotify(map);
            sidebar.Clear(); canvas.Invalidate();
            if (map == null)
            {
                sidebar.Add(new HelpBox("新建美术图，或载入素材示例。", HelpBoxMessageType.Info));
                status.text = "未选择美术图"; return;
            }
            var palette = new ObjectField("瓦片库") { objectType = typeof(GridArtPalette), value = map.palette };
            palette.RegisterValueChangedCallback(e => Change("更换美术库", () => map.palette = e.newValue as GridArtPalette));
            sidebar.Add(palette);
            sidebar.Add(new Button(() =>
            {
                string path = EditorUtility.SaveFilePanelInProject("新建瓦片库", "美术瓦片库", "asset", "选择共享瓦片库位置");
                if (string.IsNullOrEmpty(path)) return;
                var library = CreateInstance<GridArtPalette>(); library.brushes.Add(new GridArtBrush());
                AssetDatabase.CreateAsset(library,path); Change("设置美术库", () => map.palette = library); Selection.activeObject = library;
            }) { text = "新建瓦片库" });
            sidebar.Add(new Button(() => { if (map.palette != null) { Selection.activeObject = map.palette; EditorGUIUtility.PingObject(map.palette); } }) { text = "配置瓦片库" });
            var heading = new Label("图层"); heading.style.fontSize = 16; heading.style.marginTop = 16; sidebar.Add(heading);
            var names = new List<string>(); foreach (var layer in map.layers) names.Add(layer.label);
            if (names.Count > 0)
            {
                layerIndex = Mathf.Clamp(layerIndex, 0, names.Count - 1);
                var layers = new PopupField<string>("当前图层", names, layerIndex);
                layers.RegisterValueChangedCallback(e => { layerIndex = names.IndexOf(e.newValue); Refresh(); }); sidebar.Add(layers);
                var label = new TextField("名称") { value = Layer.label, isDelayed = true };
                label.RegisterValueChangedCallback(e => Change("修改图层名称", () => Layer.label = e.newValue)); sidebar.Add(label);
                var visible = new Toggle("显示") { value = Layer.visible }; visible.RegisterValueChangedCallback(e => Change("显示美术层", () => Layer.visible = e.newValue)); sidebar.Add(visible);
                var locked = new Toggle("锁定") { value = Layer.locked }; locked.RegisterValueChangedCallback(e => Change("锁定美术层", () => Layer.locked = e.newValue)); sidebar.Add(locked);
            }
            var layerActions = new VisualElement(); layerActions.style.flexDirection = FlexDirection.Row;
            layerActions.Add(new Button(() => Change("新增美术层", () => { map.layers.Add(new GridArtLayer { label = "美术层 " + (map.layers.Count + 1) }); layerIndex = map.layers.Count - 1; })) { text = "新增" });
            layerActions.Add(new Button(() => { if (Layer != null && EditorUtility.DisplayDialog("删除图层", "删除“" + Layer.label + "”及其中全部美术？", "删除", "取消")) Change("删除美术层", () => map.layers.RemoveAt(layerIndex)); }) { text = "删除" });
            sidebar.Add(layerActions);
            if (Layer == null) { status.text = "暂无图层 · 点击新增开始绘图"; return; }
            var order = new VisualElement(); order.style.flexDirection = FlexDirection.Row;
            order.Add(new Button(() => MoveLayer(-1)) { text = "后移一层" }); order.Add(new Button(() => MoveLayer(1)) { text = "前移一层" }); sidebar.Add(order);
            var advanced = new Foldout { text = "图层与网格设置", value = EditorPrefs.GetBool(SceneKey + ".Advanced", false) };
            advanced.RegisterValueChangedCallback(e => EditorPrefs.SetBool(SceneKey + ".Advanced", e.newValue));
            var tint = new ColorField("颜色 / 透明度") { value = Layer.tint };
            tint.RegisterValueChangedCallback(e => Change("美术层颜色", () => Layer.tint = e.newValue)); advanced.Add(tint);
            var depth = new FloatField("Z 深度") { value = Layer.depth, isDelayed = true };
            depth.RegisterValueChangedCallback(e => Change("美术层深度", () => Layer.depth = e.newValue)); advanced.Add(depth);
            var factor = new Vector2Field("视差系数") { value = Layer.parallax };
            factor.tooltip = "1：跟随世界；0：固定在镜头中。背景一般使用 0.2～0.8。";
            factor.RegisterValueChangedCallback(e => Change("视差系数", () => Layer.parallax = e.newValue)); advanced.Add(factor);
            var cell = new FloatField("每格世界尺寸") { value = map.cellSize, isDelayed = true };
            cell.RegisterValueChangedCallback(e => Change("美术网格尺寸", () => map.cellSize = Mathf.Max(.01f, e.newValue))); advanced.Add(cell);
            sidebar.Add(advanced);
            var guide = new Toggle("关卡参考轮廓") { value = showGuides };
            guide.RegisterValueChangedCallback(e => { showGuides = e.newValue; canvas.MarkDirtyRepaint(); }); sidebar.Add(guide);
            var paintHeading = new Label("笔刷"); paintHeading.style.fontSize = 16; paintHeading.style.marginTop = 16; sidebar.Add(paintHeading);
            var modes = new VisualElement(); modes.style.flexDirection = FlexDirection.Row;
            modes.Add(new Button(() => { erase = false; Refresh(); }) { text = erase ? "绘制" : "● 绘制" });
            modes.Add(new Button(() => { erase = true; Refresh(); }) { text = erase ? "● 擦除" : "擦除" }); sidebar.Add(modes);
            var box = new Toggle("框选区域") { value = rectangle }; box.RegisterValueChangedCallback(e => rectangle = e.newValue); sidebar.Add(box);
            if (map.palette != null && map.palette.brushes.Count > 0)
            {
                brushIndex = Mathf.Clamp(brushIndex, 0, map.palette.brushes.Count - 1);
                var brushes = new VisualElement(); brushes.style.flexDirection = FlexDirection.Row; brushes.style.flexWrap = Wrap.Wrap;
                for (int i = 0; i < map.palette.brushes.Count; i++)
                {
                    int index = i; var b = map.palette.Get(i);
                    var button = new Button(() => { brushIndex = index; Refresh(); });
                    button.style.width = 120; button.style.height = 96; button.style.marginBottom = 6;
                    if (i == brushIndex) button.style.backgroundColor = new Color(.16f, .34f, .46f);
                    var preview = new Image { sprite = b.autoTerrain ? b.GetPiece(4) : b.sprite, scaleMode = ScaleMode.ScaleToFit };
                    preview.style.height = 56; preview.pickingMode = PickingMode.Ignore; button.Add(preview);
                    var title = new Label(b.label); title.style.unityTextAlign = TextAnchor.MiddleCenter; title.pickingMode = PickingMode.Ignore; button.Add(title); brushes.Add(button);
                }
                sidebar.Add(brushes);
                if (Brush.autoTerrain && !Brush.HasInnerCorners)
                    sidebar.Add(new HelpBox("内拐角素材未齐；缺失位置使用中心图。", HelpBoxMessageType.Warning));
                sidebar.Add(new Label(Brush.autoTerrain ? "自动边缘与拐角" : "占格 " + Brush.Size.x + " × " + Brush.Size.y));
            }
            sidebar.Add(new HelpBox("左键绘制 · 右键擦除\n中键拖动 · 滚轮缩放\nCtrl+Z 撤销 · Esc 取消笔画", HelpBoxMessageType.Info));
            bool applied = false;
            if (map.palette != null)
            {
                string hash = AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(map.palette)).ToString();
                foreach (var output in UnityEngine.Object.FindObjectsByType<GridArtScene>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (output.map == map && output.gameObject.scene == SceneManager.GetActiveScene() && output.IsBuiltFor(map.revision,hash)) applied = true;
            }
            status.text = (applied ? "已应用" : "有未应用修改") + " · " + Layer.cells.Count + " 个绘制点 · " + AssetDatabase.GetAssetPath(map);
        }
        private void MoveLayer(int direction)
        {
            int next = layerIndex + direction;
            if (next < 0 || next >= map.layers.Count) return;
            Change("调整美术层顺序", () => { var l = Layer; map.layers.RemoveAt(layerIndex); map.layers.Insert(next, l); layerIndex = next; });
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
            private List<(GridArtQuad, Color)> quads;
            public ArtCanvas(GridArtWindow window)
            {
                owner = window; focusable = true; style.overflow = Overflow.Hidden;
                generateVisualContent += Draw;
                RegisterCallback<PointerDownEvent>(Down); RegisterCallback<PointerMoveEvent>(Move);
                RegisterCallback<PointerUpEvent>(Up); RegisterCallback<PointerCaptureOutEvent>(e =>
                {
                    if (gesture >= 0 && gesture != 2 && dirty && owner.map != null)
                    {
                        owner.map.revision++; EditorUtility.SetDirty(owner.map); Undo.CollapseUndoOperations(strokeGroup); owner.Refresh();
                    }
                    gesture = -1;
                });
                RegisterCallback<WheelEvent>(e => { var before = e.localMousePosition; float old = zoom; zoom = Mathf.Clamp(zoom * Mathf.Exp(-e.delta.y * .04f), 6, 192); pan = before + (pan - before) * (zoom / old); e.StopPropagation(); MarkDirtyRepaint(); RememberView(); });
                RegisterCallback<KeyDownEvent>(e =>
                {
                    if (e.keyCode == KeyCode.Escape) { canceled = true; gesture = -1; this.ReleasePointer(pointerId); if (dirty) { dirty = false; Undo.PerformUndo(); } e.StopPropagation(); MarkDirtyRepaint(); }
                    if ((e.ctrlKey || e.commandKey) && e.keyCode == KeyCode.Z) { if (e.shiftKey) Undo.PerformRedo(); else Undo.PerformUndo(); e.StopPropagation(); }
                });
                RegisterCallback<AttachToPanelEvent>(e => RestoreView());
            }
            private string Key => "2026TapTap.GridArt.View." + Hash128.Compute(Application.dataPath) + "." + (owner.map != null ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(owner.map)) : "none");
            public void RememberView() { EditorPrefs.SetFloat(Key + ".Zoom", zoom); EditorPrefs.SetFloat(Key + ".X", pan.x); EditorPrefs.SetFloat(Key + ".Y", pan.y); }
            public void RestoreView() { zoom = EditorPrefs.GetFloat(Key + ".Zoom", 32); pan = new Vector2(EditorPrefs.GetFloat(Key + ".X", 240), EditorPrefs.GetFloat(Key + ".Y", 320)); }
            public void FrameAll()
            {
                if (owner.map == null) return;
                bool found = false; Rect bounds = default;
                foreach (var layer in owner.map.layers) foreach (var q in GridArtBuilder.BuildQuads(layer, owner.map.palette))
                {
                    bounds = found ? Rect.MinMaxRect(Mathf.Min(bounds.xMin,q.bounds.xMin),Mathf.Min(bounds.yMin,q.bounds.yMin),Mathf.Max(bounds.xMax,q.bounds.xMax),Mathf.Max(bounds.yMax,q.bounds.yMax)) : q.bounds;
                    found = true;
                }
                if (!found) return;
                zoom = Mathf.Clamp(Mathf.Min((contentRect.width - 48) / Mathf.Max(1,bounds.width), (contentRect.height - 48) / Mathf.Max(1,bounds.height)),6,64);
                pan = contentRect.center + new Vector2(-bounds.center.x,bounds.center.y)*zoom;
                RememberView(); MarkDirtyRepaint();
            }
            public void Invalidate() { quads = null; MarkDirtyRepaint(); }
            private Vector2Int Cell(Vector2 p) => new Vector2Int(Mathf.FloorToInt((p.x - pan.x) / zoom), Mathf.FloorToInt((pan.y - p.y) / zoom));
            private Rect Screen(Rect r) => new Rect(pan.x + r.x * zoom, pan.y - r.yMax * zoom, r.width * zoom, r.height * zoom);
            private static RectInt Selection(Vector2Int a, Vector2Int b) => new RectInt(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Abs(a.x - b.x) + 1, Mathf.Abs(a.y - b.y) + 1);
            private void Down(PointerDownEvent e)
            {
                if (gesture >= 0) return;
                Focus(); pointer = e.localPosition; start = last = Cell(pointer); pointerStart = pointer; panStart = pan;
                if (e.button != 2 && (owner.Layer == null || owner.Layer.locked || !owner.Layer.visible || owner.map.palette == null || owner.Brush == null || EditorApplication.isPlayingOrWillChangePlaymode)) return;
                gesture = e.button; pointerId = e.pointerId; dirty = canceled = false; eraseStroke = owner.erase || e.button == 1;
                this.CapturePointer(e.pointerId); e.StopPropagation();
                if (gesture != 2) { Undo.IncrementCurrentGroup(); strokeGroup = Undo.GetCurrentGroup(); Undo.RegisterCompleteObjectUndo(owner.map, "绘制网格美术"); if (!owner.rectangle) Paint(start); }
                MarkDirtyRepaint();
            }
            private void Move(PointerMoveEvent e)
            {
                pointer = e.localPosition;
                if (gesture == 2) pan = panStart + pointer - pointerStart;
                else if (gesture >= 0 && !owner.rectangle)
                {
                    var now = Cell(pointer); int steps = Mathf.Max(Mathf.Abs(now.x - last.x), Mathf.Abs(now.y - last.y));
                    if (steps <= 4096) for (int i = 1; i <= steps; i++) Paint(Vector2Int.RoundToInt(Vector2.Lerp(last, now, (float)i / steps)));
                    last = now;
                }
                MarkDirtyRepaint();
                if (gesture >= 0) e.StopPropagation();
            }
            private void Up(PointerUpEvent e)
            {
                if (gesture < 0) return;
                if (gesture != 2 && owner.rectangle && !canceled)
                {
                    var rect = Selection(start, Cell(e.localPosition));
                    if ((long)rect.width * rect.height <= 4096)
                    {
                        Vector2Int step = eraseStroke || owner.Brush.autoTerrain ? Vector2Int.one : owner.Brush.Size;
                        for (int y = rect.yMin; y < rect.yMax; y += step.y)
                            for (int x = rect.xMin; x < rect.xMax; x += step.x) Paint(new Vector2Int(x,y));
                    }
                    else owner.ShowNotification(new GUIContent("一次框选最多 4096 格。"));
                }
                gesture = -1; this.ReleasePointer(e.pointerId); RememberView();
                if (dirty) { owner.map.revision++; EditorUtility.SetDirty(owner.map); Undo.CollapseUndoOperations(strokeGroup); owner.Refresh(); }
                e.StopPropagation(); MarkDirtyRepaint();
            }
            private void Paint(Vector2Int p)
            {
                if (owner.Layer == null) return;
                bool changed = eraseStroke ? owner.Layer.Erase(p, owner.map.palette) : owner.Layer.Set(p, owner.brushIndex);
                dirty |= changed; if (changed) quads = null;
            }
            private void Draw(MeshGenerationContext context)
            {
                var painter = context.painter2D;
                painter.fillColor = new Color(.09f, .11f, .14f); painter.BeginPath(); painter.MoveTo(Vector2.zero);
                painter.LineTo(new Vector2(contentRect.width, 0)); painter.LineTo(contentRect.size); painter.LineTo(new Vector2(0, contentRect.height)); painter.ClosePath(); painter.Fill();
                if (quads == null)
                {
                    quads = new List<(GridArtQuad, Color)>();
                    if (owner.map != null && owner.map.palette != null)
                        foreach (var layer in owner.map.layers)
                            if (layer.visible) foreach (var q in GridArtBuilder.BuildQuads(layer, owner.map.palette)) quads.Add((q, layer.tint));
                }
                var visible = new List<(GridArtQuad, Color)>();
                foreach (var item in quads) if (Screen(item.Item1.bounds).Overlaps(contentRect)) visible.Add(item);
                for (int begin = 0; begin < visible.Count;)
                {
                    int end = begin + 1;
                    while (end < visible.Count && end - begin < 4096 && visible[end].Item1.texture == visible[begin].Item1.texture) end++;
                    var mesh = context.Allocate((end-begin)*4,(end-begin)*6,visible[begin].Item1.texture);
                    for (int j = begin; j < end; j++)
                    {
                        var item = visible[j]; Rect r = Screen(item.Item1.bounds); Rect uv = item.Item1.uv;
                        Rect region = mesh.uvRegion;
                        uv = new Rect(region.position + Vector2.Scale(region.size, uv.position), Vector2.Scale(region.size, uv.size));
                        mesh.SetNextVertex(V(r.xMin,r.yMin,uv.xMin,uv.yMax,item.Item2)); mesh.SetNextVertex(V(r.xMax,r.yMin,uv.xMax,uv.yMax,item.Item2));
                        mesh.SetNextVertex(V(r.xMax,r.yMax,uv.xMax,uv.yMin,item.Item2)); mesh.SetNextVertex(V(r.xMin,r.yMax,uv.xMin,uv.yMin,item.Item2));
                        ushort i = (ushort)((j-begin)*4);
                        mesh.SetNextIndex(i); mesh.SetNextIndex((ushort)(i+1)); mesh.SetNextIndex((ushort)(i+2));
                        mesh.SetNextIndex((ushort)(i+2)); mesh.SetNextIndex((ushort)(i+3)); mesh.SetNextIndex(i);
                    }
                    begin = end;
                }
                painter.strokeColor = new Color(.36f, .42f, .5f, .32f); painter.lineWidth = 1; painter.BeginPath();
                for (float x = pan.x % zoom; x < contentRect.width; x += zoom) { painter.MoveTo(new Vector2(x, 0)); painter.LineTo(new Vector2(x, contentRect.height)); }
                for (float y = pan.y % zoom; y < contentRect.height; y += zoom) { painter.MoveTo(new Vector2(0, y)); painter.LineTo(new Vector2(contentRect.width, y)); }
                painter.Stroke();
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
                if (owner.Layer == null) return;
                RectInt selected = Selection(start, Cell(pointer));
                Rect area = gesture >= 0 && gesture != 2 && owner.rectangle ? new Rect(selected.position, selected.size) : new Rect(Cell(pointer), owner.Brush != null && !owner.Brush.autoTerrain ? owner.Brush.Size : Vector2Int.one);
                Rect box = Screen(area); painter.strokeColor = eraseStroke && gesture >= 0 || owner.erase ? new Color(1,.3f,.25f) : new Color(.25f,.8f,1);
                painter.lineWidth = 2; painter.BeginPath(); painter.MoveTo(box.min); painter.LineTo(new Vector2(box.xMax,box.yMin)); painter.LineTo(box.max); painter.LineTo(new Vector2(box.xMin,box.yMax)); painter.ClosePath(); painter.Stroke();
            }
            private static Vertex V(float x, float y, float u, float v, Color color) => new Vertex { position = new Vector3(x,y,Vertex.nearZ), uv = new Vector2(u,v), tint = color };
        }
    }
}
