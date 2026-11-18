using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Project.Rooms;
using Project.GridArt;
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
        public static void RunCase(string name)
        {
            void Check(bool value, string reason) { if (!value) throw new InvalidOperationException(reason); }
            RoomRegion Region(string id, params Vector2Int[] cells) => new RoomRegion { id = id, label = id, cells = cells.ToList() };
            if (name == "custom-mechanism-preview")
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
