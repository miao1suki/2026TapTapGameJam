using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Project.GridArt;
using Project.GridArt.Editor;
using Project.Mechanisms;
using Project.Rooms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project.LevelEditor.Editor
{
    internal static class SceneEditorReformStage3To6Audit
    {
        private const string RequestPath = "Library/SceneEditorReformStage3To6Audit.request";
        private const string ReportPath = "Library/SceneEditorReformStage3To6Audit.txt";
        private static readonly string[] FormalPaths =
        {
            "Assets/_Project/Scenes/Playing/Gaming.unity",
            "Assets/_Project/Scenes/Playing/Gaming.planning.json",
            "Assets/_Project/Content/LevelMaps/正式关卡.json",
            "Assets/_Project/Scenes/Playing/Gaming_Art.asset",
        };

        [MenuItem("Tools/2026TapTap/关卡编辑器/验证/阶段 3-6 场景全编辑")]
        internal static void RunFromMenu() => Run();

        [InitializeOnLoadMethod]
        private static void RunWhenRequested()
        {
            if (!File.Exists(RequestPath)) return;
            EditorApplication.update -= RunWhenEditorIdle;
            EditorApplication.update += RunWhenEditorIdle;
        }

        private static void RunWhenEditorIdle()
        {
            if (!File.Exists(RequestPath))
            {
                EditorApplication.update -= RunWhenEditorIdle;
                return;
            }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            EditorApplication.update -= RunWhenEditorIdle;
            File.Delete(RequestPath);
            Run();
        }

        private static void Run()
        {
            Dictionary<string, string> hashes = FormalPaths.ToDictionary(path => path, Hash, StringComparer.Ordinal);
            Scene active = SceneManager.GetActiveScene(); bool activeDirty = active.IsValid() && active.isDirty;
            string activePath = active.IsValid() ? active.path : string.Empty;
            Scene preview = EditorSceneManager.NewPreviewScene();
            var report = new StringBuilder(); int checks = 0;
            var undoObjects = new List<UnityEngine.Object>();
            GridArtMap map = null; GridArtRegionOwnership ownership = null;
            LevelEditorPalette sourcePalette = null;
            string regionAssetFolder = null;
            void Check(bool condition, string message)
            {
                checks++; if (!condition) throw new InvalidOperationException(message);
                report.AppendLine("PASS  " + message);
            }
            try
            {
                var managerGo = New("RoomLifecycleManager", preview); var manager = managerGo.AddComponent<RoomLifecycleManager>(); undoObjects.Add(manager);
                manager.ConfigureAuthoredRegions(new[]
                {
                    new RoomRegion { id="room_a", label="房间 A", cells=Enumerable.Range(0, 12).Select(x => new Vector2Int(x,-1)).ToList() },
                    new RoomRegion { id="room_b", label="房间 B", cells=Enumerable.Range(20, 8).Select(x => new Vector2Int(x,-1)).ToList() },
                    new RoomRegion { id="corridor", label="通道", corridor=true, cells=new List<Vector2Int>{new Vector2Int(12,-1),new Vector2Int(13,-1)} },
                }, 1f);

                LevelEditorPlacedBlock terrain0 = Placed(preview, manager, "Terrain0", Vector2Int.zero, false, "墙", "group_seven", "room_a");
                LevelEditorPlacedBlock terrain1 = Placed(preview, manager, "Terrain1", Vector2Int.right, false, "墙", "group_seven", "room_a");
                LevelEditorPlacedBlock water = Placed(preview, manager, "Water", Vector2Int.zero, true, "水体", "water_one", "room_a");
                LevelEditorPlacedBlock prop = Placed(preview, manager, "Prop", new Vector2Int(4,0), true, "机关", "prop_one", "room_a");
                undoObjects.AddRange(new UnityEngine.Object[] { terrain0, terrain1, water, prop });
                SceneObjectIndex index = SceneObjectAuthoring.Read(preview);
                Check(index.At(Vector2Int.zero).Count == 2, "同格多候选完整索引");
                Check(index.At(Vector2Int.zero, SceneObjectChannel.TerrainSolid).Single().block == terrain0, "地形通道独立命中");
                Check(index.At(Vector2Int.zero, SceneObjectChannel.WaterVolume).Single().block == water, "水体通道独立命中");
                Check(index.Group(index.All.First(item => item.block == terrain0)).Count == 2, "重复稳定 ID 聚合为一个逻辑组");
                Check(active.isDirty == activeDirty, "对象索引不改变当前用户 Scene dirty 状态");

                sourcePalette = ScriptableObject.CreateInstance<LevelEditorPalette>();
                var wallSource = new LevelEditorBlockEntry(); wallSource.Configure("墙", Color.gray, null); sourcePalette.Add(wallSource);
                var waterSource = new LevelEditorPropEntry(); waterSource.Configure("水体", null); sourcePalette.AddProp(waterSource);
                var propSource = new LevelEditorPropEntry(); propSource.Configure("机关", null); sourcePalette.AddProp(propSource);
                SceneSourceAudit sourceAudit = SceneObjectSourceCatalog.Audit(preview, sourcePalette);
                Check(sourceAudit.total == 4 && sourceAudit.inferred == 4 && sourceAudit.claimed == 0,
                    "旧 Scene 对象来源只读扫描可恢复方块与道具模板，不自动写入");
                Check(string.IsNullOrEmpty(terrain0.AuthoringPaletteEntryId) && string.IsNullOrEmpty(terrain1.AuthoringPaletteEntryId),
                    "来源扫描保持旧对象零写入");
                SceneObjectRecord sourceRecord = index.All.First(item => item.block == terrain0);
                Check(SceneObjectSourceCatalog.ClaimInferred(sourceRecord, sourcePalette, out _), "明确来源可显式认领整个逻辑组");
                Check(terrain0.AuthoringPaletteEntryId == wallSource.EntryId && terrain1.AuthoringPaletteEntryId == wallSource.EntryId,
                    "同作者组成员记录同一稳定栏目来源");
                Undo.PerformUndo();
                Check(string.IsNullOrEmpty(terrain0.AuthoringPaletteEntryId) && string.IsNullOrEmpty(terrain1.AuthoringPaletteEntryId),
                    "来源认领一次 Undo 完整恢复");
                Undo.PerformRedo();
                Check(terrain0.AuthoringPaletteEntryId == wallSource.EntryId && terrain1.AuthoringPaletteEntryId == wallSource.EntryId,
                    "来源认领 Redo 数据一致");
                Check(SceneObjectSourceCatalog.ClaimAllUnambiguous(preview, sourcePalette, out _) == 2,
                    "全 Scene 只批量认领剩余唯一匹配来源");
                Check(water.AuthoringPaletteEntryId == waterSource.EntryId && prop.AuthoringPaletteEntryId == propSource.EntryId,
                    "方块与道具数据源都按稳定栏目 ID 接轨");
                Undo.PerformUndo();
                Check(string.IsNullOrEmpty(water.AuthoringPaletteEntryId) && string.IsNullOrEmpty(prop.AuthoringPaletteEntryId) &&
                      terrain0.AuthoringPaletteEntryId == wallSource.EntryId,
                    "批量来源认领一次 Undo 且不影响先前事务");
                Undo.PerformRedo();

                Vector3 zBefore = terrain0.transform.position; zBefore.z = 7f; terrain0.transform.position = zBefore;
                SceneObjectRecord terrainRecord = index.All.First(item => item.block == terrain0);
                Check(SceneObjectAuthoring.MoveGroup(terrainRecord, Vector2Int.right, false, out string moveReason), "逻辑组网格移动提交");
                Check(terrain0.Cell == Vector2Int.right && terrain1.Cell == new Vector2Int(2,0) && Mathf.Approximately(terrain0.transform.position.z, 7f), "移动同步 Transform/Cell 并保留 Z 深度");
                Undo.PerformUndo();
                Check(terrain0.Cell == Vector2Int.zero && terrain1.Cell == Vector2Int.right, "对象组移动一步 Undo 完整恢复");
                Undo.PerformRedo();
                Check(terrain0.Cell == Vector2Int.right && terrain1.Cell == new Vector2Int(2,0), "对象组移动 Redo 字段等价");
                Check(active.isDirty == activeDirty, "隔离对象移动不改变当前用户 Scene dirty 状态");

                var doorGo = New("Door", preview); var door = doorGo.AddComponent<DoorController>();
                var buttonBlock = Placed(preview, manager, "Button", new Vector2Int(6,0), true, "按钮", "button_one", "room_a");
                var button = buttonBlock.gameObject.AddComponent<DoorButton>();
                undoObjects.AddRange(new UnityEngine.Object[] { door, button, buttonBlock });
                Check(SceneObjectAuthoring.BindDoor(door, button, out _), "门按钮真实双向 API 绑定");
                Check(door.Buttons.Contains(button) && button.Owner == door, "门按钮双方引用一致");
                SceneObjectRecord buttonRecord = SceneObjectAuthoring.Read(preview).All.First(item => item.block == buttonBlock);
                Check(!SceneObjectAuthoring.DeleteGroup(buttonRecord, false, out string deleteReason) && !string.IsNullOrEmpty(deleteReason), "反向引用默认阻止删除");
                Check(SceneObjectAuthoring.DeleteGroup(buttonRecord, true, out _), "显式解绑与整组删除提交");
                Check(door.Buttons.Count == 0, "删除后没有门的单边残留引用");
                Undo.PerformUndo();
                DoorButton restoredButton = preview.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<DoorButton>(true)).FirstOrDefault();
                Check(restoredButton != null && door.Buttons.Count == 1 && door.Buttons[0] == restoredButton && restoredButton.Owner == door,
                    "解绑删除一步 Undo 恢复对象与双向引用");
                Check(active.isDirty == activeDirty, "隔离引用事务不改变当前用户 Scene dirty 状态");

                map = ScriptableObject.CreateInstance<GridArtMap>(); map.cellSize = 1f;
                var artLayer = new GridArtLayer { id="art_layer", label="装饰", cells=new List<GridArtCell>{new GridArtCell(new Vector2Int(4,0),0)} };
                map.layers.Add(artLayer); ownership = ScriptableObject.CreateInstance<GridArtRegionOwnership>();
                ownership.Set("art_layer", new Vector2Int(4,0), "room_a");
                regionAssetFolder = "Assets/__SceneEditorReformRegion_" + Guid.NewGuid().ToString("N");
                AssetDatabase.CreateFolder("Assets", regionAssetFolder.Substring("Assets/".Length));
                AssetDatabase.CreateAsset(map, regionAssetFolder + "/Map.asset");
                AssetDatabase.CreateAsset(ownership, regionAssetFolder + "/Ownership.asset");
                var artGo = New("GridArtScene", preview); var art = artGo.AddComponent<GridArtScene>(); art.map = map;
                var ownershipLink = artGo.AddComponent<GridArtRegionOwnershipLink>(); ownershipLink.SetOwnership(ownership);
                undoObjects.AddRange(new UnityEngine.Object[] { map, ownership, art });
                SceneRegionMovePreview movePreview = SceneRegionAssembly.Preview(manager, new[] { "room_a" }, new Vector2Int(0,2));
                Check(movePreview.CanCommit && movePreview.artAnchors == 1 && movePreview.objects >= 5, "区域装配预检收集对象组与已认领美术");
                string stableId = prop.AuthoringId; Vector2Int localCell = prop.AuthoringLocalCell;
                Check(SceneRegionAssembly.Move(manager, new[] { "room_a" }, new Vector2Int(0,2), out _), "区域整体移动单事务提交");
                Check(manager.Regions.First(item => item.id == "room_a").cells.Contains(new Vector2Int(4,-3)), "区域格按统一 XY/Region 坐标移动");
                Check(prop.Cell == new Vector2Int(4,2) && prop.AuthoringId == stableId && prop.AuthoringLocalCell == localCell, "区域移动保留稳定身份与作者局部格");
                Check(artLayer.cells.Single().position == new Vector2Int(4,2) && ownership.Get("art_layer", new Vector2Int(4,2)) == "room_a", "只移动显式认领的 GridArt 锚点与归属");
                Undo.PerformUndo();
                Check(prop.Cell == new Vector2Int(4,0) && artLayer.cells.Single().position == new Vector2Int(4,0), "区域整体移动一步 Undo 恢复对象与美术");
                Undo.PerformRedo();
                Check(prop.Cell == new Vector2Int(4,2) && artLayer.cells.Single().position == new Vector2Int(4,2), "区域整体移动 Redo 数据一致");
                Check(active.isDirty == activeDirty, "隔离区域装配不改变当前用户 Scene dirty 状态");

                RunGridArtIncrementalAudit(active, Check, report);
                Check(active.isDirty == activeDirty, "隔离 GridArt 构建不改变当前用户 Scene dirty 状态");

                Check(active.isDirty == activeDirty, "当前用户 Scene dirty 状态未改变");
                foreach (string path in FormalPaths) Check(Hash(path) == hashes[path], "正式资源哈希未变：" + path);
                report.Insert(0, $"Scene Editor Reform Stage 3-6 Audit\nPASS  {checks} checks\n");
                File.WriteAllText(ReportPath, report.ToString()); Debug.Log(report.ToString());
            }
            catch (Exception exception)
            {
                File.WriteAllText(ReportPath, "FAIL after " + checks + " checks\n" + report + exception);
                Debug.LogException(exception); throw;
            }
            finally
            {
                foreach (UnityEngine.Object value in undoObjects) if (value != null) Undo.ClearUndo(value);
                EditorSceneManager.ClosePreviewScene(preview);
                if (map != null && !AssetDatabase.Contains(map)) UnityEngine.Object.DestroyImmediate(map);
                if (ownership != null && !AssetDatabase.Contains(ownership)) UnityEngine.Object.DestroyImmediate(ownership);
                if (sourcePalette != null) UnityEngine.Object.DestroyImmediate(sourcePalette);
                if (!string.IsNullOrEmpty(regionAssetFolder) && AssetDatabase.IsValidFolder(regionAssetFolder))
                    AssetDatabase.DeleteAsset(regionAssetFolder);
                SceneObjectAuthoring.NotifyChanged();
                if (!activeDirty && active.IsValid() && active.isDirty && !string.IsNullOrEmpty(activePath) &&
                    FormalPaths.All(path => Hash(path) == hashes[path]))
                    EditorSceneManager.OpenScene(activePath, OpenSceneMode.Single);
            }
        }

        private static GameObject New(string name, Scene scene)
        {
            var value = new GameObject(name); SceneManager.MoveGameObjectToScene(value, scene); return value;
        }

        private static LevelEditorPlacedBlock Placed(Scene scene, RoomLifecycleManager manager, string name, Vector2Int cell,
            bool prop, string entry, string id, string region)
        {
            GameObject go = New(name, scene); go.transform.position = new Vector3(cell.x + .5f, cell.y + .5f, 0);
            var block = go.AddComponent<LevelEditorPlacedBlock>(); block.Configure(cell, entry, Color.white, false, prop, 1f);
            block.SetPlanningSource(id, "audit", Vector2Int.zero);
            var reset = go.AddComponent<RoomResetObject>(); reset.Bind(manager, region); return block;
        }

        private static void RunGridArtIncrementalAudit(Scene original, Action<bool, string> check, StringBuilder report)
        {
            string folder = "Assets/__SceneEditorReformGridArt_" + Guid.NewGuid().ToString("N");
            Scene testScene = default;
            string generated = null;
            try
            {
                AssetDatabase.CreateFolder("Assets", folder.Substring("Assets/".Length));
                testScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                string scenePath = folder + "/Incremental.unity";
                EditorSceneManager.SaveScene(testScene, scenePath);
                if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);

                var texture = new Texture2D(8, 8) { name = "Tile" };
                AssetDatabase.CreateAsset(texture, folder + "/Tile.asset");
                Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 8, 8), Vector2.one * .5f, 8);
                sprite.name = "Tile"; AssetDatabase.AddObjectToAsset(sprite, texture);
                var palette = ScriptableObject.CreateInstance<GridArtPalette>();
                palette.brushes.Add(new GridArtBrush { label = "测试笔刷", sprite = sprite, size = Vector2Int.one });
                palette.brushes.Add(new GridArtBrush
                {
                    label = "跨块大笔刷", sprite = sprite, size = new Vector2Int(4, 2), visualSize = new Vector2(4, 2),
                });
                var terrainPieces = new Sprite[13];
                for (int i = 0; i < terrainPieces.Length; i++)
                {
                    terrainPieces[i] = Sprite.Create(texture, new Rect(i % 8, i / 8, 1, 1), Vector2.one * .5f, 1);
                    terrainPieces[i].name = "TerrainPiece" + i;
                    AssetDatabase.AddObjectToAsset(terrainPieces[i], texture);
                }
                palette.brushes.Add(new GridArtBrush { label = "跨块自动地形", autoTerrain = true, pieces = terrainPieces });
                AssetDatabase.CreateAsset(palette, folder + "/Palette.asset");
                var testMap = ScriptableObject.CreateInstance<GridArtMap>(); testMap.palette = palette;
                var testLayer = new GridArtLayer { id = "incremental", label = "增量层" }; testMap.layers.Add(testLayer);
                var autoLayer = new GridArtLayer { id = "auto_terrain", label = "自动地形" }; testMap.layers.Add(autoLayer);
                testLayer.Set(Vector2Int.zero, 0); testLayer.Set(new Vector2Int(40, 0), 0); testMap.revision = 1;
                autoLayer.Set(new Vector2Int(31, 0), 2); autoLayer.Set(new Vector2Int(32, 0), 2);
                AssetDatabase.CreateAsset(testMap, folder + "/Map.asset");
                var testOwnership = ScriptableObject.CreateInstance<GridArtRegionOwnership>();
                testOwnership.Set(testLayer.id, Vector2Int.zero, "room_a");
                AssetDatabase.CreateAsset(testOwnership, folder + "/Ownership.asset");
                var root = new GameObject("Incremental GridArt"); SceneManager.MoveGameObjectToScene(root, testScene);
                var output = root.AddComponent<GridArtScene>(); output.map = testMap;
                var ownershipLink = root.AddComponent<GridArtRegionOwnershipLink>(); ownershipLink.SetOwnership(testOwnership);
                output = GridArtBuilder.Apply(testMap, testScene, false, false);
                generated = "Assets/_Project/Art/Generated/GridArt/" + AssetDatabase.AssetPathToGUID(scenePath);
                MeshFilter stable = LayerMeshes(output, testLayer.id).Single(item => item.sharedMesh.bounds.center.x > 32f);
                Mesh stableMesh = stable.sharedMesh; string stablePath = AssetDatabase.GetAssetPath(stableMesh);
                DateTime stableWrite = File.GetLastWriteTimeUtc(stablePath);

                testLayer.Set(Vector2Int.right, 0); int previous = testMap.revision; testMap.revision++;
                var watch = System.Diagnostics.Stopwatch.StartNew();
                GridArtBuilder.ApplyAffected(testMap, testScene, testLayer, new[] { Vector2Int.right }, previous, false);
                watch.Stop();
                MeshFilter near = LayerMeshes(output, testLayer.id).Single(item => item.sharedMesh.bounds.center.x < 32f);
                check(near.sharedMesh.vertexCount == 8, "GridArt 单格笔画只更新命中块内容");
                check(stable != null && stable.sharedMesh == stableMesh && File.GetLastWriteTimeUtc(stablePath) == stableWrite,
                    "GridArt 单格笔画不改未命中的网格块资产");

                testLayer.Set(new Vector2Int(80, 0), 0); previous = testMap.revision; testMap.revision++;
                GridArtBuilder.ApplyAffected(testMap, testScene, testLayer, new[] { new Vector2Int(80, 0) }, previous, false);
                check(LayerMeshes(output, testLayer.id).Length == 3, "GridArt 增量笔画可创建新的 32×32 块");
                testLayer.Erase(new Vector2Int(80, 0), palette); previous = testMap.revision; testMap.revision++;
                GridArtBuilder.ApplyAffected(testMap, testScene, testLayer, new[] { new Vector2Int(80, 0) }, previous, false);
                check(LayerMeshes(output, testLayer.id).Length == 2, "GridArt 增量擦除可移除空块且不重建其他块");

                var largeAnchor = new Vector2Int(31, 10); var largeHit = new Vector2Int(33, 11);
                testLayer.Set(largeAnchor, 1); previous = testMap.revision; testMap.revision++;
                GridArtBuilder.ApplyAffected(testMap, testScene, testLayer, new[] { largeAnchor }, previous, false);
                check(LayerMeshes(output, testLayer.id).Any(item => item.sharedMesh.bounds.max.x > 32f),
                    "GridArt 大笔刷可从锚点块跨越相邻块边界完整显示");
                var covering = new List<Vector2Int>(); testLayer.GetAnchorsCovering(largeHit, palette, covering);
                check(covering.Contains(largeAnchor) && testLayer.Erase(largeHit, palette), "GridArt 大笔刷跨块命中使用锚点缓存擦除");
                previous = testMap.revision; testMap.revision++;
                GridArtBuilder.ApplyAffected(testMap, testScene, testLayer, new[] { largeAnchor, largeHit }, previous, false);
                check(LayerMeshes(output, testLayer.id).Where(item => item.sharedMesh.bounds.center.x < 32f)
                    .All(item => item.sharedMesh.bounds.max.x < 32f), "GridArt 大笔刷跨块擦除不留下派生网格");

                MeshFilter autoLeft = LayerMeshes(output, autoLayer.id).Single(item => item.sharedMesh.bounds.center.x < 32f);
                Vector2[] autoLeftUv = autoLeft.sharedMesh.uv.ToArray();
                check(autoLayer.Erase(new Vector2Int(32, 0), palette), "GridArt 自动地形边界格可擦除");
                previous = testMap.revision; testMap.revision++;
                GridArtBuilder.ApplyAffected(testMap, testScene, autoLayer, new[] { new Vector2Int(32, 0) }, previous, false);
                MeshFilter[] autoAfterErase = LayerMeshes(output, autoLayer.id);
                check(autoAfterErase.Length == 1 && !autoAfterErase[0].sharedMesh.uv.SequenceEqual(autoLeftUv),
                    "GridArt 自动地形跨块擦除会刷新相邻块连接 UV");
                autoLayer.Set(new Vector2Int(32, 0), 2); previous = testMap.revision; testMap.revision++;
                GridArtBuilder.ApplyAffected(testMap, testScene, autoLayer, new[] { new Vector2Int(32, 0) }, previous, false);
                check(LayerMeshes(output, autoLayer.id).Length == 2, "GridArt 自动地形跨块重绘会恢复相邻块");

                Undo.IncrementCurrentGroup(); int ownershipUndo = Undo.GetCurrentGroup();
                Undo.RegisterCompleteObjectUndo(testOwnership, "美术归属 Undo 审计");
                testOwnership.Set(testLayer.id, Vector2Int.right, "room_a");
                Undo.CollapseUndoOperations(ownershipUndo); Undo.PerformUndo();
                check(string.IsNullOrEmpty(testOwnership.Get(testLayer.id, Vector2Int.right)), "稀疏美术归属一步 Undo 恢复");
                Undo.PerformRedo();
                check(testOwnership.Get(testLayer.id, Vector2Int.right) == "room_a", "稀疏美术归属 Redo 恢复");
                EditorUtility.SetDirty(ownershipLink); EditorSceneManager.SaveScene(testScene);
                EditorSceneManager.CloseScene(testScene, true);
                testScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                GridArtRegionOwnershipLink reopenedLink = testScene.GetRootGameObjects()
                    .SelectMany(item => item.GetComponentsInChildren<GridArtRegionOwnershipLink>(true)).Single();
                check(reopenedLink.Ownership == testOwnership && reopenedLink.Ownership.Get(testLayer.id, Vector2Int.right) == "room_a",
                    "稀疏美术归属 Link 保存重开后引用与数据完整");
                report.AppendLine($"PERF  GridArt 单格增量构建 {watch.Elapsed.TotalMilliseconds:0.###} ms · 未命中块 0 写入");
            }
            finally
            {
                if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
                if (testScene.IsValid() && testScene.isLoaded) EditorSceneManager.CloseScene(testScene, true);
                if (!string.IsNullOrEmpty(generated)) AssetDatabase.DeleteAsset(generated);
                if (AssetDatabase.IsValidFolder(folder)) AssetDatabase.DeleteAsset(folder);
            }
        }

        private static MeshFilter[] LayerMeshes(GridArtScene output, string layerId)
        {
            if (output == null) return Array.Empty<MeshFilter>();
            string prefix = "美术层_" + layerId + " · ";
            for (int i = 0; i < output.transform.childCount; i++)
            {
                Transform root = output.transform.GetChild(i);
                if (root.name.StartsWith(prefix, StringComparison.Ordinal)) return root.GetComponentsInChildren<MeshFilter>(true);
            }
            return Array.Empty<MeshFilter>();
        }

        private static string Hash(string path)
        {
            using SHA256 sha = SHA256.Create(); using FileStream stream = File.OpenRead(path);
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty);
        }
    }
}
