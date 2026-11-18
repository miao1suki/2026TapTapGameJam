using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Project.Rooms;
using Project.GridArt;
using Project.GridArt.Editor;
using Project.Mechanisms;
using Project.ObjectArtwork;
using Project.BlockFeatures;
using Project.BlockFeatures.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Debug = UnityEngine.Debug;

namespace Project.LevelEditor.Editor
{
    public static class SceneWorkbenchAudit
    {
        public sealed class LayoutTestWindow : EditorWindow { }

        public static System.Collections.IEnumerator RunVisualCase(string name)
        {
            void Check(bool value, string reason) { if (!value) throw new InvalidOperationException(reason); }
            Scene active = SceneManager.GetActiveScene();
            bool dirty = active.isDirty;
            int undo = Undo.GetCurrentGroup();
            var previousSelection = Selection.activeObject;
            Scene preview = EditorSceneManager.NewPreviewScene();
            var view = ScriptableObject.CreateInstance<SceneView>();
            var host = ScriptableObject.CreateInstance<LayoutTestWindow>();
            try
            {
                var go = new GameObject("Narrow Inspector sample");
                SceneManager.MoveGameObjectToScene(go, preview);
                go.AddComponent<LevelEditorPlacedBlock>();
                go.AddComponent<DoorController>();
                Selection.activeGameObject = go;
                view.Show();
                // This test hosts its own panel. Hide the SceneView's automatic
                // copy so it cannot retain the same view's session lease.
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
                object canvas = typeof(EditorWindow).GetProperty("overlayCanvas",flags)?.GetValue(view);
                if (canvas?.GetType().GetProperty("overlays",flags)?.GetValue(canvas) is System.Collections.IEnumerable overlays)
                    foreach (object item in overlays)
                        if (item is SceneLevelEditorOverlay overlay) overlay.displayed = false;
                for (int frame=0;frame<4;frame++) yield return null;
                host.position = new Rect(20,20,420,1000); host.Show();
                var panel = new SceneLevelEditorPanel(view);
                host.rootVisualElement.Add(panel);
                for (int frame=0;frame<8;frame++) yield return null;
                if (name == "inspector-vector-layout")
                {
                    // Expand binding details so hidden fields also receive real
                    // layout at each supported panel width.
                    panel.Query<Foldout>().ForEach(fold => fold.value = true);
                    var vectors = panel.Query<Vector3Field>().ToList();
                    Check(vectors.Count > 0,"未创建真实门的向量参数字段");
                    foreach (float width in new[] {330f,380f,420f})
                    {
                        panel.style.width = width;
                        for (int frame=0;frame<4;frame++) yield return null;
                        foreach (var vector in vectors)
                        {
                            foreach (var axis in vector.Query<FloatField>().ToList())
                            {
                                var label = axis.Q<Label>(className:"unity-base-field__label");
                                var input = axis.Q(className:"unity-base-field__input");
                                Check(label != null && label.worldBound.width < 30,$"{width}px轴标签错误继承主字段宽度");
                                Check(input != null && input.worldBound.width >= 30,$"{width}px向量数值输入过窄");
                                Check(input.worldBound.xMax <= vector.worldBound.xMax+1,$"{width}px向量参数超出面板宽度");
                            }
                        }
                    }
                }
                else if (name == "panel-session-handoff")
                {
                    panel.ToggleLock(); Check(SceneEditSession.IsLocked(view),"未开启场景会话");
                    panel.UseWorkbenchTool("select");
                    typeof(SceneLevelEditorPanel).GetMethod("ChooseResourceKind",flags).Invoke(panel,new object[]{true});
                    panel.UseWorkbenchTool("erase");
                    panel.ToggleLock();
                    panel.RemoveFromHierarchy();
                    var replacement = new SceneLevelEditorPanel(view);
                    host.rootVisualElement.Add(replacement);
                    for (int frame=0;frame<4;frame++) yield return null;
                    Check(SceneEditSession.IsLocked(view),"重建Overlay中断落笔会话");
                    Check(SceneObjectAuthoring.ActiveChannel == null,"重建Overlay重置了全部物体通道");
                    Check((SceneEditMode)typeof(SceneLevelEditorPanel).GetField("mode",flags).GetValue(replacement) == SceneEditMode.Art,"重建Overlay丢失美术模式");
                    Check((bool)typeof(SceneLevelEditorPanel).GetField("navigationPaused",flags).GetValue(replacement),"重建Overlay意外恢复落笔");
                    Check(((Toggle)typeof(SceneLevelEditorPanel).GetField("artErase",flags).GetValue(replacement)).value,"重建Overlay丢失美术擦除工具");
                    replacement.RemoveFromHierarchy();
                    for (int frame=0;frame<4;frame++) yield return null;
                    Check(!SceneEditSession.IsLocked(view),"关闭工作台后未释放视角会话");
                }
                else if (name == "toolbar-ui-events")
                {
                    panel.ToggleLock();
                    panel.UseWorkbenchTool("select");
                    var button = panel.Q<Button>("workbench-tool-paint");
                    using (var down = PointerDownEvent.GetPooled(new Event { type=EventType.MouseDown, button=0, mousePosition=button.worldBound.center }))
                    { down.target=button; button.SendEvent(down); }
                    using (var up = PointerUpEvent.GetPooled(new Event { type=EventType.MouseUp, button=0, mousePosition=button.worldBound.center }))
                    { up.target=button; button.SendEvent(up); }
                    for (int frame=0;frame<3;frame++) yield return null;
                    Check(LevelEditorState.Tool==LevelEditorTool.Paint,"工具栏真实Pointer点击未进入画笔");
                    panel.UseWorkbenchTool("select");
                    using (var key = KeyDownEvent.GetPooled(Event.KeyboardEvent("b")))
                    { key.target=button; button.SendEvent(key); }
                    Check(LevelEditorState.Tool==LevelEditorTool.Paint,"焦点在面板按钮时B快捷键未进入画笔");
                    var search = panel.Query<TextField>().First();
                    panel.UseWorkbenchTool("select");
                    using (var key = KeyDownEvent.GetPooled(Event.KeyboardEvent("b")))
                    { key.target=search; search.SendEvent(key); }
                    Check(LevelEditorState.Tool==LevelEditorTool.Select,"输入框内B被误当成画笔快捷键");
                }
                else if (name == "stable-fold-layout")
                {
                    host.rootVisualElement.style.justifyContent = Justify.FlexEnd;
                    for (int frame=0;frame<4;frame++) yield return null;
                    var tool = panel.Q<Button>("workbench-authoring-toggle");
                    float y = tool.worldBound.y, height = panel.worldBound.height;
                    Check(height > 150,"工作台未参与真实布局");
                    var resources = (Foldout)typeof(SceneLevelEditorPanel).GetField("resourceFold",flags).GetValue(panel);
                    var inspector = (Foldout)typeof(SceneLevelEditorPanel).GetField("inspectorFold",flags).GetValue(panel);
                    resources.value = false; inspector.value = false;
                    for (int frame=0;frame<4;frame++) yield return null;
                    Check(Mathf.Abs(tool.worldBound.y-y)<1 && Mathf.Abs(panel.worldBound.height-height)<1,"底部对齐时收起资源/参数移动了主工具");
                    resources.value = true; inspector.value = true;
                    for (int frame=0;frame<4;frame++) yield return null;
                    Check(Mathf.Abs(tool.worldBound.y-y)<1,"重新展开资源移动了主工具");
                    typeof(SceneLevelEditorPanel).GetMethod("ToggleCompact",flags).Invoke(panel,null);
                    for (int frame=0;frame<4;frame++) yield return null;
                    Check(panel.Q<ScrollView>("workbench-body").resolvedStyle.display == DisplayStyle.None && panel.worldBound.height < height,"显式收起未释放画面空间");
                }
                else throw new ArgumentException(name);
                Check(active.isDirty == dirty && Undo.GetCurrentGroup() == undo,"只读布局验收写入作者数据或Undo");
            }
            finally
            {
                Selection.activeObject = previousSelection;
                host.Close();
                SceneEditSession.Exit(view,"SceneLevelEditorOverlay:"+view.GetInstanceID());
                view.Close();
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        public static void RunCase(string name)
        {
            void Check(bool value, string reason) { if (!value) throw new InvalidOperationException(reason); }
            RoomRegion Region(string id, params Vector2Int[] cells) => new RoomRegion { id = id, label = id, cells = cells.ToList() };
            if (name == "preview-tint-zero-write")
            {
                var preview = EditorSceneManager.NewPreviewScene();
                try
                {
                    var go = Project.Planar.PlanarPrimitive.Create(Project.Planar.PlanarPrimitive.Shape.Box);
                    SceneManager.MoveGameObjectToScene(go,preview);
                    var renderer = go.GetComponent<MeshRenderer>();
                    renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Content/WorldItems/Neutral.mat");
                    var block = go.AddComponent<LevelEditorPlacedBlock>();
                    block.Configure(Vector2Int.zero,"预览测试",new Color(.08f,.08f,.08f),true);
                    renderer.SetPropertyBlock(null);
                    string before = EditorJsonUtility.ToJson(block)+EditorJsonUtility.ToJson(renderer);
                    bool dirty=preview.isDirty; int undo=Undo.GetCurrentGroup();
                    Native2DScenePreview.RestoreBlockTints(preview);
                    var props = new MaterialPropertyBlock(); renderer.GetPropertyBlock(props);
                    Check(props.GetColor("_BaseColor")==block.EntryColor,"重载后未恢复临时方块颜色");
                    Check(before==EditorJsonUtility.ToJson(block)+EditorJsonUtility.ToJson(renderer) && preview.isDirty==dirty && Undo.GetCurrentGroup()==undo,"临时预览着色写入作者字段、材质、Scene或Undo");
                }
                finally { EditorSceneManager.ClosePreviewScene(preview); }
            }
            else if (name == "editable-layer-default")
            {
                var map = ScriptableObject.CreateInstance<GridArtMap>();
                var other = ScriptableObject.CreateInstance<GridArtMap>();
                var view = ScriptableObject.CreateInstance<SceneView>();
                var host = ScriptableObject.CreateInstance<LayoutTestWindow>();
                var preview = EditorSceneManager.NewPreviewScene();
                try
                {
                    map.layers.Add(new GridArtLayer {id="background",label="远景",locked=true});
                    map.layers.Add(new GridArtLayer {id="hidden",label="隐藏",visible=false});
                    map.layers.Add(new GridArtLayer {id="terrain",label="地形"});
                    other.layers.Add(new GridArtLayer {id="only",label="只读",locked=true});
                    var go = new GameObject("Layer selection"); SceneManager.MoveGameObjectToScene(go,preview);
                    var art = go.AddComponent<GridArtScene>(); art.map = map;
                    int undo = Undo.GetCurrentGroup(); string before = EditorJsonUtility.ToJson(map);
                    var panel = new SceneLevelEditorPanel(view);
                    var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                    typeof(SceneLevelEditorPanel).GetField("activeArtScene",flags).SetValue(panel,art);
                    panel.RefreshArtLayerChoices(map);
                    host.Show(); host.rootVisualElement.Add(panel);
                    var layers = (DropdownField)typeof(SceneLevelEditorPanel).GetField("artLayer",flags).GetValue(panel);
                    Check(layers.index == 2,"首次选择没有跳过隐藏/锁定层");
                    layers.index = 0; panel.RefreshArtLayerChoices(map);
                    Check(layers.index == 0,"刷新覆盖了作者显式选择的锁定层");
                    map.layers.Reverse(); panel.RefreshArtLayerChoices(map);
                    Check(layers.index == 2,"图层重排未按稳定ID保留选择");
                    map.layers.Reverse();
                    panel.RefreshArtLayerChoices(other);
                    Check(layers.index == 0,"无可编辑层时丢失只读浏览入口");
                    Check(EditorJsonUtility.ToJson(map)==before && Undo.GetCurrentGroup()==undo,"选择图层写入作者Map或Undo");
                }
                finally { host.Close(); UnityEngine.Object.DestroyImmediate(map); UnityEngine.Object.DestroyImmediate(other); UnityEngine.Object.DestroyImmediate(view); EditorSceneManager.ClosePreviewScene(preview); }
            }
            else if (name == "ownership-script-persistence")
            {
                const string folder = "Assets/_Project/Development/LevelEditor/SceneAuthoringFixture";
                const string prefabPath = folder + "/__OwnershipRoundTrip.prefab";
                const string assetPath = folder + "/__OwnershipRoundTrip.asset";
                Check(!File.Exists(prefabPath) && !File.Exists(assetPath),"归属持久化测试路径已存在，拒绝覆盖");
                Scene test = default;
                GameObject reopened = null;
                try
                {
                    var ownership = ScriptableObject.CreateInstance<GridArtRegionOwnership>();
                    ownership.Set("terrain",new Vector2Int(-2,3),"room_a");
                    AssetDatabase.CreateAsset(ownership,assetPath);
                    test = EditorSceneManager.NewPreviewScene();
                    var go = new GameObject("Persistent art ownership");
                    SceneManager.MoveGameObjectToScene(go,test);
                    var link = go.AddComponent<GridArtRegionOwnershipLink>();
                    link.SetOwnership(ownership);
                    Check(!string.IsNullOrEmpty(AssetDatabase.GetAssetPath(MonoScript.FromMonoBehaviour(link))),"归属组件没有可持久引用的脚本资产");
                    PrefabUtility.SaveAsPrefabAsset(go,prefabPath);
                    AssetDatabase.ImportAsset(prefabPath,ImportAssetOptions.ForceSynchronousImport|ImportAssetOptions.ForceUpdate);
                    reopened = PrefabUtility.LoadPrefabContents(prefabPath);
                    link = reopened.GetComponent<GridArtRegionOwnershipLink>();
                    Check(link != null && link.Ownership != null && link.Ownership.Get("terrain",new Vector2Int(-2,3)) == "room_a","保存重开后丢失美术归属引用或负坐标归属");
                }
                finally
                {
                    if (reopened != null) PrefabUtility.UnloadPrefabContents(reopened);
                    if (test.IsValid() && test.isLoaded) EditorSceneManager.ClosePreviewScene(test);
                    AssetDatabase.DeleteAsset(prefabPath);
                    AssetDatabase.DeleteAsset(assetPath);
                }
            }
            else if (name == "custom-mechanism-preview")
            {
                var scene=EditorSceneManager.NewPreviewScene();
                var texture=new Texture2D(16,32);
                var sprite=Sprite.Create(texture,new Rect(0,0,16,32),new Vector2(.5f,0),16);
                var custom=ScriptableObject.CreateInstance<ObjectArtworkLibrary>();
                try {
                    custom.mechanisms=new[]{new MechanismArtworkEntry {kind=MechanismArtworkKind.Door,frames=new[]{sprite}}};
                    var owner=new GameObject("Custom artwork door"); SceneManager.MoveGameObjectToScene(owner,scene);
                    owner.AddComponent<DoorController>(); var presentation=owner.AddComponent<MechanismArtworkPresentation>();
                    var serialized=new SerializedObject(presentation); serialized.FindProperty("artwork").objectReferenceValue=custom; serialized.ApplyModifiedPropertiesWithoutUndo();
                    int undo=Undo.GetCurrentGroup(); string before=EditorJsonUtility.ToJson(presentation); bool dirty=scene.isDirty;
                    Check(ObjectArtworkPlanningPreview.TryGet(owner,out var art)&&art.Sprite==sprite,"实例美术库被共享预览覆盖");
                    var drawer=new SceneResourceDrawer(_=>{},_=>{},_=>{});
                    drawer.Bind(new[]{new ScenePaletteItem {prefab=owner,isProp=true,index=0,displayName="真实门"}},null,false);
                    Check(drawer.Q<Image>()?.sprite==sprite,"资源卡片没有显示真实机关美术");
                    Check(before==EditorJsonUtility.ToJson(presentation)&&undo==Undo.GetCurrentGroup()&&dirty==scene.isDirty,"读取实例美术改变作者状态");
                    var other=new GameObject("Default artwork door"); SceneManager.MoveGameObjectToScene(other,scene); other.AddComponent<DoorController>();
                    Check(ObjectArtworkPlanningPreview.TryGet(other,out var standard)&&standard.Sprite==ObjectArtworkLibrary.Load().FirstFrame(MechanismArtworkKind.Door),"实例美术污染共享默认库");
                } finally { EditorSceneManager.ClosePreviewScene(scene); UnityEngine.Object.DestroyImmediate(custom); UnityEngine.Object.DestroyImmediate(sprite); UnityEngine.Object.DestroyImmediate(texture); }
            }
            else if (name == "hole")
            {
                var region = Region("ring", Enumerable.Range(0, 9).Select(i => new Vector2Int(i % 3, i / 3)).Where(c => c != Vector2Int.one).ToArray());
                var data = SceneRoomSnapshot.Build(new[] { region });
                Check(!data.owners.ContainsKey(Vector2Int.one), "孔洞被错误命中");
                Check(data.rooms[0].runs.Sum(r => r.width * r.height) == 8 && data.rooms[0].components == 1, "形状合并填平孔洞");
            }
            else if (name == "adjacency")
            {
                var data = SceneRoomSnapshot.Build(new[] { Region("a", Vector2Int.zero), Region("diagonal", Vector2Int.one), Region("b", Vector2Int.right) });
                Check(!data.rooms[0].neighbours.Contains(1) && data.rooms[0].neighbours.SetEquals(new[] { 2 }), "对角接触误作四邻接");
                Check(data.rooms[2].neighbours.SetEquals(new[] { 0, 1 }), "双向邻接漏项");
            }
            else if (name == "overlap")
            {
                var data = SceneRoomSnapshot.Build(new[] { Region("a", Vector2Int.zero), Region("b", Vector2Int.zero) });
                Check(data.owners[Vector2Int.zero].SequenceEqual(new[] { 0, 1 }) && data.rooms.All(r => r.overlaps == 1), "重叠丢失候选或计数");
            }
            else if (name == "empty-disconnected")
            {
                var data = SceneRoomSnapshot.Build(new[] { Region("empty"), Region("islands", Vector2Int.zero, new Vector2Int(-4, -7)) });
                Check(data.rooms[0].Problems == "空区域" && data.rooms[1].components == 2 && data.bounds.Contains(new Vector2Int(-4, -7)), "空区域或负坐标断裂错误");
            }
            else if (name == "snapshot-isolation")
            {
                var source = Region("a", Vector2Int.zero);
                var data = SceneRoomSnapshot.Build(new[] { source }); source.cells.Add(Vector2Int.right);
                Check(data.rooms[0].cells.Count == 1, "导航持有可变作者集合");
            }
            else if (name == "staged-cancel" || name == "occupied-erase" || name == "legacy-island")
            {
                Scene active = SceneManager.GetActiveScene(); bool activeDirty = active.isDirty;
                Scene preview = EditorSceneManager.NewPreviewScene();
                try
                {
                    var go = new GameObject("Workbench test"); SceneManager.MoveGameObjectToScene(go, preview);
                    var manager = go.AddComponent<RoomLifecycleManager>();
                    manager.ConfigureAuthoredRegions(new[] { Region("a", Vector2Int.zero, Vector2Int.right, new Vector2Int(2, 0)) }, 1);
                    if (name == "staged-cancel")
                    {
                        int undo = Undo.GetCurrentGroup(); string before = EditorJsonUtility.ToJson(manager); bool dirty = preview.isDirty;
                        SceneRegionAuthoring.PreviewAndCancelForAudit(manager, "a", Vector2Int.up);
                        Check(before == EditorJsonUtility.ToJson(manager) && Undo.GetCurrentGroup() == undo && preview.isDirty == dirty,
                            "取消预览改变作者序列化、dirty 或 Undo");
                    }
                    else if (name == "occupied-erase")
                    {
                        var item = new GameObject("同格水体"); SceneManager.MoveGameObjectToScene(item, preview);
                        var block = item.AddComponent<LevelEditorPlacedBlock>(); block.Configure(SceneGridSpace.RegionToScene(Vector2Int.zero), "水体", Color.blue, false, true, 1);
                        SceneObjectAuthoring.NotifyChanged();
                        int undo = Undo.GetCurrentGroup(); string before = EditorJsonUtility.ToJson(manager);
                        Check(!SceneRegionAuthoring.TryApplySingleCellForAudit(manager, "a", Vector2Int.zero, SceneRegionBrush.Erase, out _), "擦区域未阻止格内对象");
                        Check(before == EditorJsonUtility.ToJson(manager) && undo == Undo.GetCurrentGroup() && block != null, "拒绝擦除产生部分结果");
                    }
                    else
                    {
                        manager.ConfigureAuthoredRegions(new[] { Region("a", Vector2Int.zero, Vector2Int.right, new Vector2Int(7, 0)) }, 1);
                        Check(SceneRegionAuthoring.TryApplySingleCellForAudit(manager, "a", Vector2Int.right, SceneRegionBrush.Erase, out _), "既有分离岛的非增裂编辑被拒绝");
                        Undo.PerformUndo(); Check(manager.Regions[0].cells.Count == 3, "区域事务不能一步撤销");
                        Undo.ClearUndo(manager);
                    }
                    Check(active.isDirty == activeDirty, "预览测试标脏其他场景");
                }
                finally { EditorSceneManager.ClosePreviewScene(preview); }
            }
            else if (name == "art-staged-cancel" || name == "art-staged-undo" || name == "art-owned-undo")
            {
                Scene preview = EditorSceneManager.NewPreviewScene();
                var map = ScriptableObject.CreateInstance<GridArtMap>();
                var ownership = ScriptableObject.CreateInstance<GridArtRegionOwnership>();
                ownership.name = "Persistent ownership identity";
                try
                {
                    var go = new GameObject("Art stroke test"); SceneManager.MoveGameObjectToScene(go, preview);
                    var art = go.AddComponent<GridArtScene>(); art.map = map;
                    go.AddComponent<GridArtRegionOwnershipLink>().SetOwnership(ownership);
                    var layer = new GridArtLayer { id = "test" }; map.layers.Add(layer);
                    int undo = Undo.GetCurrentGroup(); bool dirty = EditorUtility.IsDirty(map); string before = EditorJsonUtility.ToJson(map);
                    SceneArtAuthoring.StrokeForAudit(art, layer, Vector2Int.zero, true);
                    Check(before == EditorJsonUtility.ToJson(map) && undo == Undo.GetCurrentGroup() && dirty == EditorUtility.IsDirty(map), "取消美术笔画写入作者 Map 或 Undo");
                    if (name != "art-staged-cancel")
                    {
                        SceneArtAuthoring.StrokeForAudit(art, layer, Vector2Int.zero, false, name == "art-owned-undo");
                        Check(map.layers[0].cells.Count == 1 && map.revision == 1, "美术提交不完整");
                        if (name == "art-owned-undo") Check(ownership.Get("test", Vector2Int.zero) == "audit_region" && ownership.name == "Persistent ownership identity" && ownership.hideFlags == HideFlags.None, "稀疏归属提交丢失或临时对象标记泄漏");
                        Undo.PerformUndo(); Check(map.layers[0].cells.Count == 0 && map.revision == 0, "美术不能一步撤销");
                        Check(ownership.Entries.Count == 0, "美术 Undo 未同步稀疏归属");
                        Undo.PerformRedo(); Check(map.layers[0].cells.Count == 1 && map.revision == 1, "美术不能重做");
                        if (name == "art-owned-undo") Check(ownership.Entries.Count == 1, "美术 Redo 未恢复稀疏归属");
                    }
                }
                finally { Undo.ClearUndo(map); Undo.ClearUndo(ownership); UnityEngine.Object.DestroyImmediate(map); UnityEngine.Object.DestroyImmediate(ownership); EditorSceneManager.ClosePreviewScene(preview); }
            }
            else if (name == "session-status-refresh")
            {
                var view = ScriptableObject.CreateInstance<SceneView>();
                var panel = new SceneLevelEditorPanel(view);
                var status = panel.Q<UnityEngine.UIElements.Button>("workbench-authoring-toggle");
                var oldChannel = SceneObjectAuthoring.ActiveChannel;
                var oldTool = LevelEditorState.Tool;
                int undo = Undo.GetCurrentGroup(); bool dirty = SceneManager.GetActiveScene().isDirty;
                try
                {
                    Check(status.text.Contains("暂停"), "起始状态不是暂停");
                    panel.ToggleLock(); Check(status.text.Contains("落笔"), "锁定状态被长元组哈希缓存忽略");
                    panel.ToggleLock(); Check(status.text.Contains("暂停"), "暂停状态被缓存忽略");
                    SceneObjectAuthoring.ActiveChannel = SceneObjectChannel.TerrainSolid;
                    panel.UseWorkbenchTool("select");
                    Check(!SceneObjectAuthoring.ActiveChannel.HasValue,"通用点选仍被旧的墙体通道过滤");
                    Check(Undo.GetCurrentGroup() == undo && SceneManager.GetActiveScene().isDirty == dirty, "切换状态写入作者数据或Undo");
                }
                finally { SceneObjectAuthoring.ActiveChannel=oldChannel; LevelEditorState.Tool=oldTool; SceneEditSession.Exit(view,"SceneLevelEditorOverlay:"+view.GetInstanceID()); UnityEngine.Object.DestroyImmediate(view); }
            }
            else if (name == "resource-stroke-cache")
            {
                var palette = ScriptableObject.CreateInstance<GridArtPalette>();
                var map = ScriptableObject.CreateInstance<GridArtMap>();
                try
                {
                    palette.brushes.Add(new GridArtBrush { label = "苔石", category = "自动地形" }); map.palette = palette;
                    var drawer = new SceneResourceDrawer(_ => { }, _ => { }, _ => { });
                    drawer.Bind(Array.Empty<ScenePaletteItem>(), map, true);
                    int builds = drawer.RebuildCount;
                    for (int i = 0; i < 100; i++) { map.revision++; drawer.Bind(Array.Empty<ScenePaletteItem>(), map, true); }
                    Check(drawer.RebuildCount == builds, "连续铺图重建资源卡片");
                    var replacement = ScriptableObject.CreateInstance<GridArtPalette>();
                    try { map.palette = replacement; drawer.Bind(Array.Empty<ScenePaletteItem>(), map, true); Check(drawer.RebuildCount == builds + 1, "切换素材库未刷新卡片"); }
                    finally { UnityEngine.Object.DestroyImmediate(replacement); }
                }
                finally { UnityEngine.Object.DestroyImmediate(map); UnityEngine.Object.DestroyImmediate(palette); }
            }
            else throw new ArgumentException(name);
        }

        public static void RunFromCommandLine()
        {
            try
            {
                foreach (string name in new[] { "hole", "adjacency", "overlap", "empty-disconnected", "snapshot-isolation", "staged-cancel", "occupied-erase", "legacy-island", "art-staged-cancel", "art-staged-undo", "art-owned-undo", "resource-stroke-cache" }) RunCase(name);
                RunPrefabRoundTrip();
                EditorSceneManager.OpenScene("Assets/_Project/Scenes/Playing/Gaming.unity");
                RoomLifecycleManager manager = SceneRegionAuthoring.FindManager(SceneManager.GetActiveScene());
                var samples = new List<double>(); SceneRoomSnapshot snapshot = null;
                for (int i = 0; i < 5; i++) { var watch = Stopwatch.StartNew(); snapshot = SceneRoomSnapshot.Build(manager.Regions); watch.Stop(); samples.Add(watch.Elapsed.TotalMilliseconds); }
                samples.Sort();
                File.WriteAllText("Library/SceneWorkbenchAudit.txt", $"PASS 12 workbench cases + Prefab save/reopen round trip\nUnity {Application.unityVersion}\nCPU {SystemInfo.processorType}; RAM {SystemInfo.systemMemorySize} MB\nRegions {snapshot.rooms.Count}; cells {snapshot.owners.Count}; rectangles {snapshot.rooms.Sum(r => r.runs.Count)}; 5 builds median {samples[2]:0.###} ms / worst {samples[4]:0.###} ms\n");
                EditorApplication.Exit(0);
            }
            catch (Exception ex) { Debug.LogException(ex); EditorApplication.Exit(1); }
        }

        public static void RunPrefabRoundTrip()
        {
            const string path = "Assets/_Project/Development/LevelEditor/SceneAuthoringFixture/__RoundTrip.unity";
            if (File.Exists(path)) throw new InvalidOperationException("Round-trip test path already exists; refusing overwrite.");
            try
            {
                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var manager = new GameObject("Round trip manager").AddComponent<RoomLifecycleManager>();
                manager.ConfigureAuthoredRegions(new[] { new RoomRegion { id = "round_trip", label = "测试", cells = new List<Vector2Int> { new Vector2Int(0,-1), new Vector2Int(0,-2) } } }, 1);
                var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Content/Mechanisms/Door.prefab");
                if (source == null) throw new InvalidOperationException("Door prefab missing");
                var go = (GameObject)PrefabUtility.InstantiatePrefab(source, scene); go.name = "Round trip door"; go.transform.position = new Vector3(.5f,1.5f,0);
                var block = go.GetComponent<LevelEditorPlacedBlock>() ?? go.AddComponent<LevelEditorPlacedBlock>();
                block.Configure(new Vector2Int(0,1),"门",Color.white,false,true,1); block.SetPlanningSource("round_trip_door","",Vector2Int.zero);
                var reset = go.GetComponent<RoomResetObject>() ?? go.AddComponent<RoomResetObject>(); reset.Bind(manager,"round_trip");
                PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
                EditorSceneManager.SaveScene(scene,path); SceneObjectAuthoring.NotifyChanged();
                if (!SceneRegionAssembly.Move(manager,new[]{"round_trip"},new Vector2Int(3,0),out string reason)) throw new InvalidOperationException(reason);
                Undo.FlushUndoRecordObjects();
                Undo.PerformUndo(); if (block.Cell != new Vector2Int(0,1)) throw new InvalidOperationException("Assembly Undo failed");
                Undo.PerformRedo(); EditorSceneManager.SaveScene(scene,path);
                scene = EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
                block = scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<LevelEditorPlacedBlock>(true)).Single();
                if (block.Cell != new Vector2Int(3,1) || Vector3.Distance(block.transform.position,new Vector3(3.5f,1.5f,0)) > .001f || block.AuthoringId != "round_trip_door" || block.GetComponent<RoomResetObject>().RegionId != "round_trip" || !PrefabUtility.IsPartOfPrefabInstance(block.gameObject))
                    throw new InvalidOperationException("Moved Prefab overrides, author identity or room ownership did not survive save/reopen.");
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                AssetDatabase.DeleteAsset(path);
            }
        }

        public static void OpenForReview()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Playing/Gaming.unity");
            SceneLevelEditorOverlay.Open();
            var view = SceneView.lastActiveSceneView;
            view.in2DMode = true;
            var manager = SceneRegionAuthoring.FindManager(SceneManager.GetActiveScene());
            var room = manager.Regions.First(r => r != null && !r.corridor && r.cells.Count > 200);
            SceneRoomNavigator.Frame(manager, room.id, view);
        }

        [MenuItem("Tools/2026TapTap/关卡编辑器/验证/打开混合样板")]
        public static void OpenFixtureForReview()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Development/LevelEditor/SceneAuthoringFixture/Mixed.unity", OpenSceneMode.Single);
            SceneLevelEditorOverlay.Open();
            var view = SceneView.lastActiveSceneView;
            view.in2DMode = true;
            SceneRoomNavigator.Frame(SceneRegionAuthoring.FindManager(SceneManager.GetActiveScene()), "fixture_a", view);
            StartReviewObservations(SceneManager.GetActiveScene().path);
        }

        internal static void StartReviewObservations(string path)
        {
            reviewScenePath = path;
            lastReviewState = null;
            File.WriteAllText("Library/SceneWorkbenchUIAudit.txt", "Explicit fixture UI review; state observations only.\n");
            EditorApplication.update -= ObserveReviewState;
            EditorApplication.update += ObserveReviewState;
        }

        // Explicit review entry only. Creates independent Scene/Map/ownership
        // copies so painting, Undo and save/reopen trials cannot alter the demo.
        public static void OpenInteractionLabForReview()
        {
            const string folder = "Assets/_Project/Development/LevelEditor/SceneWorkbenchInteractionLab";
            string path = folder + "/InteractionLab.unity";
            if (!File.Exists(path))
            {
                if (AssetDatabase.IsValidFolder(folder)) throw new InvalidOperationException("交互样板目录已存在，拒绝覆盖");
                AssetDatabase.CreateFolder("Assets/_Project/Development/LevelEditor","SceneWorkbenchInteractionLab");
                var source = EditorSceneManager.OpenScene(SceneWorkbenchShowcase.ScenePath);
                var art = source.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<GridArtScene>(true)).Single();
                var ownership = SceneArtAuthoring.OwnershipFor(art);
                if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(art.map),folder+"/Map.asset") ||
                    !AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(ownership),folder+"/Ownership.asset"))
                    throw new InvalidOperationException("独立作者资产复制失败，未覆盖源数据");
                art.map = AssetDatabase.LoadAssetAtPath<GridArtMap>(folder+"/Map.asset");
                art.map.ownerSceneGuid = string.Empty;
                EditorUtility.SetDirty(art.map);
                art.GetComponent<GridArtRegionOwnershipLink>().SetOwnership(AssetDatabase.LoadAssetAtPath<GridArtRegionOwnership>(folder+"/Ownership.asset"));
                EditorSceneManager.SaveScene(source,path);
                GridArtBuilder.Apply(art.map,source,false,true);
                EditorSceneManager.SaveScene(source);
            }
            EditorSceneManager.OpenScene(path);
            SceneLevelEditorOverlay.Open();
            var view = SceneView.lastActiveSceneView;
            view.in2DMode = true;
            view.Frame(new Bounds(new Vector3(21,9,0),new Vector3(48,27,1)),true);
            StartReviewObservations(path);
        }

        // Explicit, disposable-lab verification; never called by normal UI.
        public static void RunRoundNineAudit()
        {
            const string output = "Library/SceneWorkbenchRound9";
            const string lab = "Assets/_Project/Development/LevelEditor/SceneWorkbenchInteractionLab/InteractionLab.unity";
            GridArtMap map = null;
            string original = null;
            try
            {
                Directory.CreateDirectory(output);
                foreach (string path in new[] {SceneWorkbenchShowcase.ScenePath,
                    "Assets/_Project/Development/LevelEditor/SceneAuthoringShowcase/MossConnections.unity",
                    "Assets/_Project/Development/LevelEditor/SceneAuthoringFixture/Mixed.unity"})
                {
                    var saved = EditorSceneManager.OpenScene(path);
                    var links = saved.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<GridArtRegionOwnershipLink>(true)).ToArray();
                    if (links.Length != 1 || links[0].Ownership == null) throw new InvalidOperationException("冷开丢失归属："+path);
                }
                Scene scene = EditorSceneManager.OpenScene(lab);
                foreach (var probe in SceneObjectAuthoring.Read(scene).All.Where(r=>r.block.EntryName=="__RoundNineMaterialProbe" && r.block.Cell==new Vector2Int(-13,16)).ToArray())
                    UnityEngine.Object.DestroyImmediate(probe.block.gameObject);
                SceneObjectAuthoring.NotifyChanged();
                if (scene.isDirty) EditorSceneManager.SaveScene(scene);
                var art = scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<GridArtScene>(true)).Single();
                map = art.map;
                if (map.layers.Sum(l=>l.cells.Count)!=388 || SceneObjectAuthoring.Read(scene).All.Count!=368)
                    throw new InvalidOperationException("前台保存后的作者数量未持久化");
                if (map.ownerSceneGuid != AssetDatabase.AssetPathToGUID(lab)) throw new InvalidOperationException("副本Map仍归属源Scene");
                var entry = new LevelEditorBlockEntry(); entry.Configure("__RoundNineMaterialProbe",new Color(.08f,.08f,.08f),null);
                var block = LevelEditorBlockFactory.Place(entry,new Vector2Int(-13,16));
                if (block == null || block.EntryName != "__RoundNineMaterialProbe") throw new InvalidOperationException("材质测试格被占用");
                var material = block.GetComponent<MeshRenderer>().sharedMaterial;
                if (material == null || material.shader == null || !material.shader.isSupported || !AssetDatabase.Contains(material))
                    throw new InvalidOperationException("基础方块没有可持久引用的受支持材质");
                EditorSceneManager.SaveScene(scene);
                scene = EditorSceneManager.OpenScene(lab);
                if (scene.isDirty) throw new InvalidOperationException("恢复临时着色时改写了Scene");
                block = SceneObjectAuthoring.Read(scene).All.Single(r=>r.block.EntryName=="__RoundNineMaterialProbe").block;
                if (block.GetComponent<MeshRenderer>().sharedMaterial != material) throw new InvalidOperationException("保存重开丢失基础方块材质");
                CaptureBlockProbe(block,output+"/black-block-after-reopen.png");
                UnityEngine.Object.DestroyImmediate(block.gameObject);
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                art = scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<GridArtScene>(true)).Single();
                map = art.map; original = EditorJsonUtility.ToJson(map,true);
                File.WriteAllText(output+"/map-before.json",original);
                string authorBefore = ArtworkAuthorSnapshot(map);
                var layer = map.layers.First(l=>l.visible&&!l.locked);
                int revision = map.revision;
                SceneArtAuthoring.StrokeForAudit(art,layer,new Vector2Int(-14,16),false);
                GridArtBuilder.ApplyAffected(map,scene,layer,new[]{new Vector2Int(-14,16)},revision,false);
                string authorAfter = ArtworkAuthorSnapshot(map);
                Undo.PerformUndo();
                File.WriteAllText(output+"/map-after-undo.json",EditorJsonUtility.ToJson(map,true));
                if (ArtworkAuthorSnapshot(map)!=authorBefore) throw new InvalidOperationException("美术撤销未精确恢复作者数据");
                Undo.PerformRedo();
                if (ArtworkAuthorSnapshot(map)!=authorAfter) throw new InvalidOperationException("美术重做未精确恢复作者数据");
                File.WriteAllText(output+"/result.txt","PASS: three saved Scene ownership links; GUI-saved 368 objects/388 art cells; material reference and GPU black tint after save/reopen; exact art author Undo/Redo (derived bake fields excluded).\n");
            }
            catch (Exception e) { Debug.LogException(e); File.WriteAllText(output+"/failure.txt",e.ToString()); EditorApplication.Exit(1); return; }
            finally
            {
                if (map != null && original != null)
                {
                    Undo.ClearUndo(map); EditorJsonUtility.FromJsonOverwrite(original,map);
                    foreach (var layer in map.layers) layer.InvalidateLookup();
                    EditorUtility.SetDirty(map);
                    GridArtBuilder.Apply(map,SceneManager.GetActiveScene(),false,false);
                    AssetDatabase.SaveAssetIfDirty(map); EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
                }
            }
            EditorApplication.Exit(0);
        }

        private static string ArtworkAuthorSnapshot(GridArtMap map)
        {
            var copy = UnityEngine.Object.Instantiate(map);
            try
            {
                copy.name=map.name; copy.hideFlags=HideFlags.None;
                copy.bakedRevision=-1; copy.bakedPaletteHash=null;
                return EditorJsonUtility.ToJson(copy);
            }
            finally { UnityEngine.Object.DestroyImmediate(copy); }
        }

        private static void CaptureBlockProbe(LevelEditorPlacedBlock block,string output)
        {
            var go = new GameObject("Material probe camera");
            SceneManager.MoveGameObjectToScene(go,block.gameObject.scene);
            var camera = go.AddComponent<Camera>(); camera.orthographic=true; camera.orthographicSize=1.5f;
            camera.transform.position=block.transform.position+Vector3.back*10;
            camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.1f,.5f,.8f);
            camera.cullingMask=1<<30; block.gameObject.layer=30;
            var rt=new RenderTexture(256,256,24); rt.Create();
            var png=new Texture2D(256,256,TextureFormat.RGBA32,false);
            var previous=RenderTexture.active;
            try
            {
                camera.targetTexture=rt; camera.Render(); RenderTexture.active=rt;
                png.ReadPixels(new Rect(0,0,256,256),0,0); png.Apply();
                File.WriteAllBytes(output,png.EncodeToPNG());
                Color center=png.GetPixel(128,128),corner=png.GetPixel(8,8);
                if (Mathf.Max(center.r,center.g,center.b)>.45f || Mathf.Max(center.r,center.g,center.b)-Mathf.Min(center.r,center.g,center.b)>.025f || corner.b-corner.r<.2f)
                    throw new InvalidOperationException($"基础方块实际像素不是深灰：{center}，背景{corner}");
            }
            finally { RenderTexture.active=previous; rt.Release(); UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(png); UnityEngine.Object.DestroyImmediate(go); }
        }

        private static string lastReviewState;
        private static string reviewScenePath;
        private static double nextReviewObservation;
        private static void ObserveReviewState()
        {
            if (EditorApplication.timeSinceStartup < nextReviewObservation) return;
            nextReviewObservation = EditorApplication.timeSinceStartup + .5;
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != reviewScenePath) { EditorApplication.update -= ObserveReviewState; return; }
            var art = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<GridArtScene>(true)).FirstOrDefault();
            var map = art != null ? art.map : null;
            string mapHash = map != null ? Hash128.Compute(EditorJsonUtility.ToJson(map)).ToString() : "none";
            string state = $"sceneDirty={scene.isDirty}; objects={scene.GetRootGameObjects().Sum(r => r.GetComponentsInChildren<LevelEditorPlacedBlock>(true).Length)}; mapDirty={map != null && EditorUtility.IsDirty(map)}; revision={map?.revision}; cells={map?.layers.Sum(l => l.cells.Count)}; mapHash={mapHash}";
            if (state == lastReviewState) return;
            lastReviewState = state;
            File.AppendAllText("Library/SceneWorkbenchUIAudit.txt", $"{DateTime.Now:HH:mm:ss.fff} {state}\n");
        }

        [MenuItem("Tools/2026TapTap/关卡编辑器/验证/创建隔离混合样板")]
        public static void CreateMixedFixture()
        {
            const string folder = "Assets/_Project/Development/LevelEditor/SceneAuthoringFixture";
            if (AssetDatabase.IsValidFolder(folder))
                throw new InvalidOperationException("样板目录已存在；请保留作者数据，使用已有 Mixed.unity 或在独立工作树重建。");
            AssetDatabase.CreateFolder("Assets/_Project/Development/LevelEditor", "SceneAuthoringFixture");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SceneManager.SetActiveScene(scene);
            var managerGo = new GameObject("房间管理器"); var manager = managerGo.AddComponent<RoomLifecycleManager>();
            List<Vector2Int> Rect(int x, int y, int w, int h) => Enumerable.Range(0, w * h).Select(i => new Vector2Int(x + i % w, y + i / w)).ToList();
            manager.ConfigureAuthoredRegions(new[]
            {
                new RoomRegion { id="fixture_a", label="起点房间", cells=Rect(0,-12,20,12) },
                new RoomRegion { id="fixture_passage", label="连接通道", corridor=true, cells=Rect(20,-5,6,3) },
                new RoomRegion { id="fixture_b", label="机关房间", cells=Rect(26,-12,18,12) },
                new RoomRegion { id="fixture_empty", label="待绘制房间" },
            }, 1);
            var texture = new Texture2D(16,16); texture.filterMode = FilterMode.Point;
            texture.SetPixels(Enumerable.Range(0,256).Select(i => (i/16 + i%16)%2==0 ? new Color(.25f,.5f,.65f) : new Color(.18f,.35f,.45f)).ToArray()); texture.Apply();
            File.WriteAllBytes(folder + "/TestTile.png", texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture); AssetDatabase.ImportAsset(folder + "/TestTile.png");
            var importer = (TextureImporter)AssetImporter.GetAtPath(folder + "/TestTile.png"); importer.textureType=TextureImporterType.Sprite; importer.spritePixelsPerUnit=16; importer.filterMode=FilterMode.Point; importer.SaveAndReimport();
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(folder + "/TestTile.png");
            var palette = ScriptableObject.CreateInstance<GridArtPalette>(); palette.brushes.Add(new GridArtBrush { label="隔离诊断瓦片", sprite=sprite, colorId="none" }); AssetDatabase.CreateAsset(palette,folder+"/Palette.asset");
            var map = ScriptableObject.CreateInstance<GridArtMap>(); map.palette=palette; map.cellSize=1;
            var artLayer = new GridArtLayer { id="fixture_art", label="测试装饰", depth=1 };
            artLayer.Set(new Vector2Int(4,3),0); artLayer.Set(new Vector2Int(30,3),0); map.layers.Add(artLayer); AssetDatabase.CreateAsset(map,folder+"/Map.asset");
            var ownership = ScriptableObject.CreateInstance<GridArtRegionOwnership>(); ownership.Set(artLayer.id,new Vector2Int(4,3),"fixture_a"); AssetDatabase.CreateAsset(ownership,folder+"/Ownership.asset");
            var artGo=new GameObject("GridArt"); var art=artGo.AddComponent<GridArtScene>(); art.map=map; artGo.AddComponent<GridArtRegionOwnershipLink>().SetOwnership(ownership);
            LevelEditorPlacedBlock Place(string name, Vector2Int cell, bool prop, string id, string region, string prefab=null)
            {
                var source = prefab != null ? AssetDatabase.LoadAssetAtPath<GameObject>(prefab) : null;
                var go = source != null ? (GameObject)PrefabUtility.InstantiatePrefab(source,scene) : new GameObject(name);
                go.name=name; go.transform.position=new Vector3(cell.x+.5f,cell.y+.5f,0);
                if(source==null){go.AddComponent<SpriteRenderer>().sprite=sprite; go.AddComponent<BoxCollider2D>();}
                var block=go.GetComponent<LevelEditorPlacedBlock>() ?? go.AddComponent<LevelEditorPlacedBlock>(); block.Configure(cell,name,Color.white,false,prop,1); block.SetPlanningSource(id,"",Vector2Int.zero);
                (go.GetComponent<RoomResetObject>() ?? go.AddComponent<RoomResetObject>()).Bind(manager,region);
                if(source!=null) PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
                return block;
            }
            for(int i=0;i<20;i++) Place("地形",new Vector2Int(i,0),false,"fixture_wall_"+i,"fixture_a");
            Place("同格水体",new Vector2Int(5,0),true,"fixture_water","fixture_a","Assets/_Project/Content/ColorBlocks/Prefabs/Blue_WaterSource.prefab");
            Place("同格道具",new Vector2Int(5,0),true,"fixture_prop","fixture_a");
            var door=Place("真实门",new Vector2Int(15,1),true,"fixture_door","fixture_a","Assets/_Project/Content/Mechanisms/Door.prefab").GetComponent<DoorController>();
            var button=Place("真实按钮",new Vector2Int(10,1),true,"fixture_button","fixture_a","Assets/_Project/Content/Mechanisms/DoorButton.prefab").GetComponent<DoorButton>();
            if(door!=null && button!=null) { door.SetButtons(new[]{button}); PrefabUtility.RecordPrefabInstancePropertyModifications(door); PrefabUtility.RecordPrefabInstancePropertyModifications(button); }
            Place("完整逻辑组一",new Vector2Int(29,1),false,"fixture_group","fixture_b");
            var second=Place("完整逻辑组二",new Vector2Int(30,1),false,"fixture_group","fixture_b"); second.SetPlanningSource("fixture_group","",Vector2Int.right);
            EditorSceneManager.SaveScene(scene,folder+"/Mixed.unity"); AssetDatabase.SaveAssets();
            // Derived preview is intentionally separate from the editable fixture sources.
            Project.GridArt.Editor.GridArtBuilder.Apply(map,scene,false,false); EditorSceneManager.SaveScene(scene);
            SceneLevelEditorOverlay.Open(); SceneRoomNavigator.Frame(manager,"fixture_a",SceneView.lastActiveSceneView);
        }
    }
}
