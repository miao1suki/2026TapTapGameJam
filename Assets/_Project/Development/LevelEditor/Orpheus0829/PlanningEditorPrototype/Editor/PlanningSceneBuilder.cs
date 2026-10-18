using System.Collections.Generic;
using Project;
using Project.BlockFeatures;
using Project.LevelEditor;
using Project.LevelEditor.Editor;
using Project.SurfaceTiles;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PlanningEditorPrototype
{
    internal static class PlanningSceneBuilder
    {
        private static float CellSize =>
            Mathf.Max(.05f, LevelEditorState.CellSize);
        private const string RoomRootPrefix = "__PlanningRoom_";
        private const string MapRootName = "__PlanningMapGenerated";
        private const string StagingRootName = "__PlanningMapStaging";
        private static string ContainerName(PlanningRoom room) => room.name + " [" + room.id + "]";

        private static Transform FindOwnedContainer(Transform root, PlanningRoom room)
        {
            if (root == null || room == null) return null;
            var exact = FindDirectChild(root, ContainerName(room));
            if (exact != null) return exact;
            string suffix = " [" + room.id + "]";
            for (int i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child.name.EndsWith(suffix, System.StringComparison.Ordinal)) return child;
                var blocks = child.GetComponentsInChildren<LevelEditorPlacedBlock>(true);
                foreach (var block in blocks)
                    if (!string.IsNullOrEmpty(block.PlanningBoxId) && room.boxes.Exists(b => b.id == block.PlanningBoxId)) return child;
            }
            return FindDirectChild(root, room.name);
        }
        internal static bool SuppressSceneUndo { get; set; }
        private static void RegisterSceneCreation(Object target,string name)
        {
            if(!SuppressSceneUndo) Undo.RegisterCreatedObjectUndo(target,name);
        }
        private static void DestroySceneObject(Object target)
        {
            if(SuppressSceneUndo) Object.DestroyImmediate(target);
            else Undo.DestroyObjectImmediate(target);
        }

        internal static bool HasGeneratedRoot(PlanningRoom room)
        {
            if (room == null)
            {
                return false;
            }

            GameObject root = FindGeneratedRoot(MapRootName);
            return root != null &&
                   FindOwnedContainer(root.transform, room) != null;
        }

        internal static bool HasGeneratedRoot(PlanningDocument document)
        {
            return FindGeneratedRoot(MapRootName) != null;
        }

        internal static bool HasAnyGeneratedRoot()
        {
            return FindGeneratedRoot(MapRootName) != null ||
                   FindGeneratedRoomRoot() != null;
        }

        internal static bool TryBuildRoom(
            PlanningRoom room,
            out string message)
        {
            if (room == null)
            {
                message = "没有可生成的房间。";
                return false;
            }

            GameObject existingRoot = FindGeneratedRoot(MapRootName);
            if (existingRoot != null &&
                HasPlacedBlocks(existingRoot))
            {
                message = "当前已有整套生成场景。" +
                          "请先撤销或删除整套场景，再单独生成房间。";
                return false;
            }

            var rooms = new List<PlanningRoom> { room };
            return TryBuild(
                room.name,
                MapRootName,
                rooms,
                new List<PlanningBox>(),
                1,
                1,
                false,
                false,
                out message);
        }

        private static bool HasPlacedBlocks(GameObject root)
        {
            return root != null &&
                   root.GetComponentsInChildren<
                       LevelEditorPlacedBlock>(true).Length > 0;
        }

        internal static bool TryBuildDocument(
            PlanningDocument document,
            out string message)
        {
            if (document == null || document.rooms.Count == 0)
            {
                message = "世界图里还没有房间。";
                return false;
            }

            PlanningSharedSaveUtility.VerifySceneCopy(SceneManager.GetActiveScene(), document);
            bool success = TryBuild(
                document.name,
                MapRootName,
                document.rooms,
                document.assemblyPatches,
                document.worldBlockCellWidth,
                document.worldBlockCellHeight,
                true,
                false,
                out message);
            if (success) StorePlanningDocument(document);
            return success;
        }

        internal static bool TryApplyDocument(
            PlanningDocument document,
            out string message)
            => TryApplyDocument(document, out message, out _);

        internal static bool TryApplyDocument(PlanningDocument document, out string message, out string persistentJson)
        {
            persistentJson = null;
            if (document == null)
            {
                message = "世界图里还没有房间。";
                return false;
            }

            document.RefreshConnectorPaths();
            PlanningSharedSaveUtility.VerifySceneCopy(SceneManager.GetActiveScene(), document);
            bool success = TryBuild(
                document.name,
                StagingRootName,
                document.rooms,
                document.assemblyPatches,
                document.worldBlockCellWidth,
                document.worldBlockCellHeight,
                true,
                true,
                out message);
            if (success) persistentJson = StorePlanningDocument(document);
            return success;
        }

        private static string StorePlanningDocument(PlanningDocument document)
        {
            var manager = FindGeneratedRoot(MapRootName)?.GetComponent<Project.Rooms.RoomLifecycleManager>();
            if (manager == null) return null;
            string persistentJson = document.ToPersistentJson(true);
            manager.Configure(new List<Project.Rooms.RoomRegion>(manager.Regions), CellSize, persistentJson);
            EditorUtility.SetDirty(manager);
            PlanningSharedSaveUtility.SaveSceneCopy(manager.gameObject.scene, document, persistentJson);
            return persistentJson;
        }

        internal static GameObject FindRoomContainer(PlanningRoom room)
        {
            if (room == null)
            {
                return null;
            }

            GameObject root = FindGeneratedRoot(MapRootName);
            Transform child = FindOwnedContainer(
                root != null ? root.transform : null,
                room);
            return child != null ? child.gameObject : null;
        }

        internal static GameObject EnsureRoomContainer(
            PlanningRoom room,
            bool focus)
        {
            if (room == null)
            {
                return null;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                return null;
            }

            GameObject root = FindGeneratedRoot(MapRootName);
            if (root == null)
            {
                root = new GameObject(MapRootName);
                RegisterSceneCreation(
                    root,
                    "创建规划关卡根节点");
                SceneManager.MoveGameObjectToScene(root, scene);
                root.transform.position = Vector3.zero;
            }

            Transform child = FindOwnedContainer(root.transform, room);
            GameObject container;
            if (child != null)
            {
                container = child.gameObject;
            }
            else
            {
                container = new GameObject(ContainerName(room));
                container.transform.SetParent(root.transform, false);
                RegisterSceneCreation(
                    container,
                    room.isConnector
                        ? "创建通道场景容器"
                        : "创建房间场景容器");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (focus)
            {
                Selection.activeGameObject = container;
                SceneView view = SceneView.lastActiveSceneView;
                view?.FrameSelected();
                view?.Focus();
            }

            return container;
        }

        private static bool TryBuild(
            string title,
            string rootName,
            IReadOnlyList<PlanningRoom> rooms,
            IReadOnlyList<PlanningBox> assemblyPatches,
            int worldBlockCellWidth,
            int worldBlockCellHeight,
            bool connectRooms,
            bool incremental,
            out string message)
        {
            message = string.Empty;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                message = "请先退出播放模式，再生成关卡。";
                return false;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                message = "当前没有可用场景。";
                return false;
            }

            if (FindGeneratedRoot(StagingRootName) != null)
            {
                message = "场景中存在未完成的规划临时根节点。" +
                          "请先检查并处理 __PlanningMapStaging，" +
                          "工具不会自动删除它。";
                return false;
            }

            LevelEditorPalette palette =
                LevelEditorPaletteService.GetOrCreate();
            if (palette == null || palette.Entries.Count == 0)
            {
                message = "关卡编辑器还没有可用方块栏目。";
                return false;
            }

            List<RoomLayout> layouts = BuildLayouts(
                rooms,
                worldBlockCellWidth,
                worldBlockCellHeight);
            if (layouts.Count == 0 && !incremental)
            {
                message = "没有可生成的房间详情。";
                return false;
            }

            GameObject existingGeneratedRoot =
                FindGeneratedRoot(MapRootName);

            var existingCells = new HashSet<Vector2Int>();
            var existingWaterCells = new HashSet<Vector2Int>();
            var existingWaterLayers = new Dictionary<string, HashSet<Vector2Int>>();
            var existingLavaCells = new HashSet<Vector2Int>();
            IReadOnlyList<LevelEditorPlacedBlock> existingBlocks =
                ProjectDiscovery.FindAll<LevelEditorPlacedBlock>(true);
            for (int index = 0; index < existingBlocks.Count; index++)
            {
                LevelEditorPlacedBlock block = existingBlocks[index];
                Transform sceneRoot = block.transform.root;
                if (sceneRoot != null &&
                    (sceneRoot.name == MapRootName ||
                     sceneRoot.name.StartsWith(RoomRootPrefix)))
                {
                    continue;
                }

                var waterFeature = block.GetComponent<WaterVisualFeature>();
                bool isLava = block.GetComponent<LavaHazardFeature>() != null;
                var externalLayer = waterFeature != null ? PlanningWaterLayerUtility.Layer(existingWaterLayers,
                    waterFeature.GetType().FullName) : existingCells;
                for(int x=block.Cell.x;x<block.Cell.x+block.SizeCells.x;x++)
                    for(int y=block.Cell.y;y<block.Cell.y+block.SizeCells.y;y++)
                        if (block.ContainsCell(new Vector2Int(x,y)))
                        {
                            var cell = new Vector2Int(x,-y-1);
                            externalLayer.Add(cell);
                            if (waterFeature != null) existingWaterCells.Add(cell);
                            if (isLava) existingLavaCells.Add(cell);
                        }
            }

            // Validate before staging or replacing any author object. A water/lava conflict
            // must not silently drop part of an irregular water volume during generation.
            var waterValidation = new List<(PlanningBox box, Vector2Int origin)>();
            foreach (var layout in layouts)
                foreach (var box in layout.Room.boxes)
                    waterValidation.Add((box, new Vector2Int(layout.OffsetX, layout.OffsetY)));
            foreach (var box in assemblyPatches) waterValidation.Add((box, Vector2Int.zero));
            if (connectRooms)
                foreach (var connector in rooms)
                {
                    if (connector == null || !connector.isConnector) continue;
                    var path = PlanningLayoutUtility.GetConnectorAssemblyPath(rooms, connector,
                        worldBlockCellWidth, worldBlockCellHeight);
                    if (path.Count == 0) continue;
                    foreach (var box in connector.boxes) waterValidation.Add((box, path[0]));
                }
            if (!PlanningWaterLayerUtility.TryValidate(waterValidation, palette, out message)) return false;
            foreach (var item in waterValidation)
            {
                var opposite = PlanningWaterLayerUtility.IsWater(item.box, palette) ? existingLavaCells :
                    PlanningWaterLayerUtility.IsLava(item.box, palette) ? existingWaterCells : null;
                if (opposite == null || opposite.Count == 0) continue;
                foreach (var local in PlanningShapeUtility.Cells(item.box))
                    if (opposite.Contains(local + item.origin))
                    {
                        message = "规划中的水与场景已有岩浆重叠（或相反）；未修改现有场景，请先调整位置。";
                        return false;
                    }
            }

            var occupied = new HashSet<Vector2Int>();
            var occupiedWater = new Dictionary<string, HashSet<Vector2Int>>();
            int placedCount = 0;
            int skippedCount = 0;
            int connectorCount = 0;
            int patchCount = 0;

            int undoGroup = Undo.GetCurrentGroup();
            if(!SuppressSceneUndo) Undo.SetCurrentGroupName("生成规划关卡：" + title);

            GameObject root = new GameObject(StagingRootName);
            SceneManager.MoveGameObjectToScene(root, scene);
            root.transform.position = Vector3.zero;

            for (int layoutIndex = 0;
                 layoutIndex < layouts.Count;
                 layoutIndex++)
            {
                RoomLayout layout = layouts[layoutIndex];
                GameObject roomRoot = new GameObject(ContainerName(layout.Room));
                roomRoot.transform.SetParent(root.transform, false);

                for (int boxIndex = 0;
                     boxIndex < layout.Room.boxes.Count;
                     boxIndex++)
                {
                    PlanningBox box = layout.Room.boxes[boxIndex];
                    if (box.propEntryId ==
                        PlanningBox.BossRespawnMarkerEntryId)
                    {
                        continue;
                    }

                    string waterKind = PlanningWaterLayerUtility.WaterKind(box, palette);
                    var layer = waterKind != null ? PlanningWaterLayerUtility.Layer(occupiedWater, waterKind) : occupied;
                    var external = waterKind != null ? PlanningWaterLayerUtility.Layer(existingWaterLayers, waterKind) : existingCells;
                    if (!CanPlaceMerged(box, new Vector2Int(layout.OffsetX,layout.OffsetY),layer,external))
                    { skippedCount += box.width*box.height; continue; }
                    for (int y = box.y;
                         y < box.y + box.height;
                         y++)
                    {
                        for (int x = box.x;
                             x < box.x + box.width;
                             x++)
                        {
                            if (!PlanningShapeUtility.Contains(box, new Vector2Int(x,y))) continue;
                            int sceneY = layout.OffsetY + y;
                            var cell = new Vector2Int(
                                layout.OffsetX + x,
                                sceneY);
                            if (!layer.Add(cell) ||
                                external.Contains(cell))
                            {
                                skippedCount++;
                                continue;
                            }

                            LevelEditorPlacedBlock placed =
                                PlacePlanningBox(
                                    palette,
                                    box,
                                    cell,
                                    roomRoot.transform,
                                    new Vector2Int(x,y));
                            if (placed == null)
                            {
                                if ((box.isMerged || box.singleInstance || PlanningDoorUtility.IsDoor(box)) && !IsShapeAnchor(box, new Vector2Int(x,y))) continue;
                                skippedCount++;
                                continue;
                            }

                            placed.gameObject.name =
                                BuildObjectName(box, cell);
                            placedCount++;
                        }
                    }
                }

                if (assemblyPatches.Count > 0)
                {
                    GameObject patchRoot = new GameObject("装配图补丁");
                    patchRoot.transform.SetParent(root.transform, false);
                    for (int boxIndex = 0;
                         boxIndex < assemblyPatches.Count;
                         boxIndex++)
                    {
                        PlanningBox box = assemblyPatches[boxIndex];
                        if (box.propEntryId ==
                            PlanningBox.BossRespawnMarkerEntryId)
                        {
                            continue;
                        }

                        string patchWaterKind = PlanningWaterLayerUtility.WaterKind(box, palette);
                        var patchLayer = patchWaterKind != null ? PlanningWaterLayerUtility.Layer(occupiedWater, patchWaterKind) : occupied;
                        var patchExternal = patchWaterKind != null ? PlanningWaterLayerUtility.Layer(existingWaterLayers, patchWaterKind) : existingCells;
                        if (!CanPlaceMerged(box,Vector2Int.zero,patchLayer,patchExternal)) continue;
                        for (int y = box.y;
                             y < box.y + box.height;
                             y++)
                        {
                            for (int x = box.x;
                                 x < box.x + box.width;
                                 x++)
                            {
                                if (TryPlacePlanningBox(
                                        palette,
                                        box,
                                        new Vector2Int(x, y),
                                        patchRoot.transform,
                                        patchLayer,
                                        patchExternal,
                                        new Vector2Int(x,y)))
                                {
                                    patchCount++;
                                }
                            }
                        }
                    }
                }
            }

            if (connectRooms)
            {
                GetLayoutStride(
                    rooms,
                    worldBlockCellWidth,
                    worldBlockCellHeight,
                    out int strideX,
                    out int strideY);
                for (int connectorIndex = 0;
                     connectorIndex < rooms.Count;
                     connectorIndex++)
                {
                    PlanningRoom connector = rooms[connectorIndex];
                    if (connector == null ||
                        !connector.isConnector)
                    {
                        continue;
                    }

                    if (connector.boxes.Count == 0)
                    {
                        continue;
                    }

                    GameObject connectorRoot = new GameObject(
                        ContainerName(connector));
                    connectorRoot.transform.SetParent(
                        root.transform,
                        false);
                    List<Vector2Int> assemblyPath =
                        PlanningLayoutUtility.GetConnectorAssemblyPath(
                            rooms,
                            connector,
                            worldBlockCellWidth,
                            worldBlockCellHeight);
                    if (assemblyPath.Count == 0)
                    {
                        continue;
                    }

                    Vector2Int origin = assemblyPath[0];
                    for (int boxIndex = 0;
                         boxIndex < connector.boxes.Count;
                         boxIndex++)
                    {
                        PlanningBox box =
                            connector.boxes[boxIndex];
                        string connectorWaterKind = PlanningWaterLayerUtility.WaterKind(box, palette);
                        var connectorLayer = connectorWaterKind != null ? PlanningWaterLayerUtility.Layer(occupiedWater, connectorWaterKind) : occupied;
                        var connectorExternal = connectorWaterKind != null ? PlanningWaterLayerUtility.Layer(existingWaterLayers, connectorWaterKind) : existingCells;
                        if (!CanPlaceMerged(box,origin,connectorLayer,connectorExternal)) continue;
                        for (int y = box.y;
                             y < box.y + box.height;
                             y++)
                        {
                            for (int x = box.x;
                                 x < box.x + box.width;
                                 x++)
                            {
                                var cell = new Vector2Int(
                                    origin.x + x,
                                    origin.y + y);
                                if (TryPlacePlanningBox(
                                        palette,
                                        box,
                                        cell,
                                        connectorRoot.transform,
                                        connectorLayer,
                                        connectorExternal,
                                        new Vector2Int(x,y)))
                                {
                                    connectorCount++;
                                }
                            }
                        }
                    }
                }
            }

            if (placedCount == 0 && !incremental)
            {
                Object.DestroyImmediate(root);
                if(!SuppressSceneUndo) Undo.CollapseUndoOperations(undoGroup);
                message = "没有生成方块，请检查栏目方块是否可用。";
                return false;
            }

            int vineChainCount = ConvertVineChains(root.transform);
            if (incremental)
            {
                ReconcileGeneratedRoots(
                    existingGeneratedRoot,
                    root,
                    out int added,
                    out int removed,
                    out int preserved);
                PersistVineLinks(FindGeneratedRoot(MapRootName)?.transform);
                PlanningDoorUtility.BindSceneDoors(FindGeneratedRoot(MapRootName)?.transform, rooms, assemblyPatches);
                PlanningMechanismUtility.BindScene(FindGeneratedRoot(MapRootName)?.transform, rooms, assemblyPatches);
                ConfigureRoomLifecycle(FindGeneratedRoot(MapRootName), rooms, worldBlockCellWidth, worldBlockCellHeight);
                if(!SuppressSceneUndo) Undo.CollapseUndoOperations(undoGroup);
                EditorSceneManager.MarkSceneDirty(scene);
                SceneView.RepaintAll();
                message = $"已应用到场景：新增 {added}，删除 {removed}，" +
                          $"保留 {preserved} 个原有物体。";
                if (skippedCount > 0)
                {
                    message += $" 跳过 {skippedCount} 个冲突格。";
                }

                return true;
            }

            DestroyGeneratedRoots();
            root.name = rootName;
            PersistVineLinks(root.transform);
            PlanningDoorUtility.BindSceneDoors(root.transform, rooms, assemblyPatches);
            PlanningMechanismUtility.BindScene(root.transform, rooms, assemblyPatches);
            ConfigureRoomLifecycle(root, rooms, worldBlockCellWidth, worldBlockCellHeight);
            RegisterSceneCreation(root, "完成规划场景重建");

            if(!SuppressSceneUndo) Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = root;
            SceneView.RepaintAll();
            message = connectRooms
                ? $"已生成 {layouts.Count} 个房间、" +
                  $"{placedCount} 个详情方块，" +
                  $"{connectorCount} 个通道方块，" +
                  $"{patchCount} 个装配补丁。"
                : $"已在场景生成 {placedCount} 个方块。";
            if (skippedCount > 0)
            {
                message += $" 跳过 {skippedCount} 个冲突格。";
            }
            if (vineChainCount > 0)
            {
                message += $" 已整理 {vineChainCount} 条藤蔓。";
            }

            return true;
        }

        private static void ConfigureRoomLifecycle(GameObject root, IReadOnlyList<PlanningRoom> rooms, int strideX, int strideY)
        {
            if (root == null) return;
            var regions = new List<Project.Rooms.RoomRegion>();
            foreach (var room in rooms)
            {
                var region = new Project.Rooms.RoomRegion { id = room.id, label = room.name, corridor = room.isConnector };
                foreach (var cell in room.cells)
                    for (int x = 0; x < strideX; x++) for (int y = 0; y < strideY; y++)
                        region.cells.Add(new Vector2Int(cell.x * strideX + x, cell.y * strideY + y));
                regions.Add(region);
                Transform container = FindOwnedContainer(root.transform, room);
                if (container == null) continue;
                foreach (var placed in container.GetComponentsInChildren<LevelEditorPlacedBlock>(true))
                {
                    var member = placed.GetComponent<Project.Rooms.RoomResetObject>() ?? placed.gameObject.AddComponent<Project.Rooms.RoomResetObject>();
                    member.SetRegion(room.id);
                    EditorUtility.SetDirty(member);
                    var anchor = placed.GetComponent<Project.BlockFeatures.RespawnAnchorFeature>();
                    if (anchor != null)
                    {
                        anchor.EnsureRuntimeAnchorId();
                        anchor.SetRegion(room.id);
                        PrefabUtility.RecordPrefabInstancePropertyModifications(anchor);
                        EditorUtility.SetDirty(anchor);
                    }
                }
            }
            var manager = root.GetComponent<Project.Rooms.RoomLifecycleManager>() ?? root.AddComponent<Project.Rooms.RoomLifecycleManager>();
            manager.Configure(regions, CellSize, manager.PlanningJson);
            EditorUtility.SetDirty(manager);
        }

        private static void ReconcileGeneratedRoots(
            GameObject existingRoot,
            GameObject stagedRoot,
            out int added,
            out int removed,
            out int preserved)
        {
            added = 0;
            removed = 0;
            preserved = 0;
            if (existingRoot == null)
            {
                added = stagedRoot.GetComponentsInChildren<
                    LevelEditorPlacedBlock>(true).Length;
                stagedRoot.name = MapRootName;
                RegisterSceneCreation(
                    stagedRoot,
                    "应用规划场景");
                return;
            }

            var desiredContainers = new HashSet<string>();
            var stagedContainers = new List<Transform>();
            for (int index = 0; index < stagedRoot.transform.childCount; index++)
            {
                stagedContainers.Add(stagedRoot.transform.GetChild(index));
            }

            foreach (Transform staged in stagedContainers)
            {
                if (staged.GetComponentsInChildren<
                        LevelEditorPlacedBlock>(true).Length == 0)
                {
                    continue;
                }

                desiredContainers.Add(staged.name);
                Transform existing = FindDirectChild(
                    existingRoot.transform,
                    staged.name);
                if (existing == null)
                {
                    var desiredBlocks = staged.GetComponentsInChildren<LevelEditorPlacedBlock>(true);
                    var ids = new HashSet<string>();
                    foreach (var block in desiredBlocks) if (!string.IsNullOrEmpty(block.PlanningBoxId)) ids.Add(block.PlanningBoxId);
                    string suffix = staged.name.LastIndexOf(" [", System.StringComparison.Ordinal) is int start && start >= 0 ? staged.name.Substring(start) : string.Empty;
                    for (int i = 0; i < existingRoot.transform.childCount && existing == null; i++)
                    {
                        var candidate = existingRoot.transform.GetChild(i);
                        if (suffix.Length > 0 && candidate.name.EndsWith(suffix, System.StringComparison.Ordinal)) existing = candidate;
                        else foreach (var block in candidate.GetComponentsInChildren<LevelEditorPlacedBlock>(true))
                            if (ids.Contains(block.PlanningBoxId)) { existing = candidate; break; }
                    }
                }
                if (existing == null)
                {
                    added += staged.GetComponentsInChildren<
                        LevelEditorPlacedBlock>(true).Length;
                    staged.SetParent(existingRoot.transform, true);
                    RegisterSceneCreation(
                        staged.gameObject,
                        "应用规划房间");
                    continue;
                }

                existing.name = staged.name;
                ReconcileContainer(
                    existing,
                    staged,
                    ref added,
                    ref removed,
                    ref preserved);
            }

            var oldContainers = new List<Transform>();
            for (int index = 0;
                 index < existingRoot.transform.childCount;
                 index++)
            {
                oldContainers.Add(existingRoot.transform.GetChild(index));
            }

            foreach (Transform old in oldContainers)
            {
                if (desiredContainers.Contains(old.name))
                {
                    continue;
                }

                LevelEditorPlacedBlock[] blocks =
                    old.GetComponentsInChildren<LevelEditorPlacedBlock>(true);
                int generatedChildCount = 0;
                foreach (LevelEditorPlacedBlock block in blocks)
                {
                    if (block != null && block.transform.parent == old)
                    {
                        generatedChildCount++;
                    }
                }

                if (generatedChildCount == old.childCount)
                {
                    removed += blocks.Length;
                    DestroySceneObject(old.gameObject);
                    continue;
                }

                foreach (LevelEditorPlacedBlock block in blocks)
                {
                    if (block != null && block.transform.parent == old)
                    {
                        removed += CountPlacedBlocks(block.gameObject);
                        DestroySceneObject(block.gameObject);
                    }
                }
            }

            Object.DestroyImmediate(stagedRoot);
        }

        private static void ReconcileContainer(
            Transform existing,
            Transform staged,
            ref int added,
            ref int removed,
            ref int preserved)
        {
            var oldByCell = new Dictionary<(string id, Vector2Int cell), Queue<LevelEditorPlacedBlock>>();
            LevelEditorPlacedBlock[] oldBlocks =
                existing.GetComponentsInChildren<LevelEditorPlacedBlock>(true);
            foreach (LevelEditorPlacedBlock block in oldBlocks)
            {
                if (block.transform.parent == existing)
                {
                    var key = (block.PlanningBoxId ?? string.Empty, block.Cell);
                    if (!oldByCell.TryGetValue(key, out var bucket)) oldByCell[key] = bucket = new Queue<LevelEditorPlacedBlock>();
                    bucket.Enqueue(block);
                }
            }

            LevelEditorPlacedBlock[] desiredBlocks =
                staged.GetComponentsInChildren<LevelEditorPlacedBlock>(true);
            foreach (LevelEditorPlacedBlock desired in desiredBlocks)
            {
                if (desired == null || desired.transform.parent != staged)
                {
                    continue;
                }

                var desiredKey = (desired.PlanningBoxId ?? string.Empty, desired.Cell);
                if (oldByCell.TryGetValue(desiredKey, out var candidates) && candidates.Count > 0)
                {
                    LevelEditorPlacedBlock old = candidates.Dequeue();
                    if (SameGeneratedBlock(old, desired))
                    {
                        var oldParts=old.GetComponentsInChildren<LevelEditorPlacedBlock>(true);
                        var desiredParts=desired.GetComponentsInChildren<LevelEditorPlacedBlock>(true);
                        for(int i=0;i<oldParts.Length;i++)
                            oldParts[i].SetPlanningSource(desiredParts[i].PlanningBoxId,desiredParts[i].PlanningSignature,desiredParts[i].PlanningLocalCell);
                        preserved += CountPlacedBlocks(old.gameObject);
                        Object.DestroyImmediate(desired.gameObject);
                        continue;
                    }

                    removed += CountPlacedBlocks(old.gameObject);
                    DestroySceneObject(old.gameObject);
                }

                added += CountPlacedBlocks(desired.gameObject);
                desired.transform.SetParent(existing, true);
                RegisterSceneCreation(
                    desired.gameObject,
                    "应用规划物体");
            }

            foreach (var bucket in oldByCell.Values)
            foreach (LevelEditorPlacedBlock stale in bucket)
            {
                if (stale != null)
                {
                    removed += CountPlacedBlocks(stale.gameObject);
                    DestroySceneObject(stale.gameObject);
                }
            }
        }

        private static bool SameGeneratedBlock(
            LevelEditorPlacedBlock old,
            LevelEditorPlacedBlock desired)
        {
            if (old == null || desired == null ||
                old.EntryName != desired.EntryName ||
                old.IsProp != desired.IsProp ||
                old.CellWorldSize != desired.CellWorldSize ||
                old.SizeCells != desired.SizeCells ||
                old.PlanningSignature != desired.PlanningSignature ||
                PrefabUtility.GetCorrespondingObjectFromSource(
                    old.gameObject) !=
                PrefabUtility.GetCorrespondingObjectFromSource(
                    desired.gameObject))
            {
                return false;
            }

            LevelEditorPlacedBlock[] oldChildren =
                old.GetComponentsInChildren<LevelEditorPlacedBlock>(true);
            LevelEditorPlacedBlock[] desiredChildren =
                desired.GetComponentsInChildren<LevelEditorPlacedBlock>(true);
            if (oldChildren.Length != desiredChildren.Length)
            {
                return false;
            }

            for (int index = 0; index < oldChildren.Length; index++)
            {
                if (oldChildren[index].Cell != desiredChildren[index].Cell ||
                    oldChildren[index].PlanningSignature != desiredChildren[index].PlanningSignature ||
                    oldChildren[index].EntryName !=
                    desiredChildren[index].EntryName)
                {
                    return false;
                }
            }

            return true;
        }

        private static int CountPlacedBlocks(GameObject root)
        {
            return root.GetComponentsInChildren<
                LevelEditorPlacedBlock>(true).Length;
        }

        private static void PersistVineLinks(Transform generatedRoot)
        {
            if (generatedRoot == null) return;
            foreach (ClimbableVineFeature vine in generatedRoot.GetComponentsInChildren<ClimbableVineFeature>(true))
            {
                vine.RestoreInitialGrowthLinks();
                PrefabUtility.RecordPrefabInstancePropertyModifications(vine);
                EditorUtility.SetDirty(vine);
            }
        }

        private static int ConvertVineChains(Transform generatedRoot)
        {
            int chainCount = 0;
            for (int rootIndex = 0;
                 rootIndex < generatedRoot.childCount;
                 rootIndex++)
            {
                Transform container = generatedRoot.GetChild(rootIndex);
                var vines = new Dictionary<Vector2Int, LevelEditorPlacedBlock>();
                LevelEditorPlacedBlock[] blocks =
                    container.GetComponentsInChildren<LevelEditorPlacedBlock>(true);
                for (int index = 0; index < blocks.Length; index++)
                {
                    LevelEditorPlacedBlock block = blocks[index];
                    if (block.transform.parent == container &&
                        block.IsProp &&
                        block.SizeCells == Vector2Int.one &&
                        block.GetComponent<ClimbableVineFeature>() is
                            ClimbableVineFeature feature &&
                        !(feature is LadderSonFeature) &&
                        PrefabUtility.GetCorrespondingObjectFromSource(
                            block.gameObject) != null &&
                        feature.GrowthSegmentPrefab != null &&
                        feature.GrowthSegmentPrefab
                            .GetComponent<LadderSonFeature>() != null)
                    {
                        vines[block.Cell] = block;
                    }
                }

                foreach (KeyValuePair<Vector2Int, LevelEditorPlacedBlock> pair
                         in vines)
                {
                    if (pair.Value == null)
                    {
                        continue;
                    }

                    Vector2Int cell = pair.Key;
                    if (vines.TryGetValue(
                            cell + Vector2Int.down,
                            out LevelEditorPlacedBlock lower) &&
                        CanJoinVine(lower, pair.Value))
                    {
                        continue;
                    }

                    LevelEditorPlacedBlock bottom = pair.Value;
                    ClimbableVineFeature mother =
                        bottom.GetComponent<ClimbableVineFeature>();
                    var upperBlocks = new List<LevelEditorPlacedBlock>();
                    for (Vector2Int next = cell + Vector2Int.up;
                         vines.TryGetValue(next, out LevelEditorPlacedBlock upper);
                         next += Vector2Int.up)
                    {
                        if (!CanJoinVine(bottom, upper))
                        {
                            break;
                        }

                        upperBlocks.Add(upper);
                    }

                    if (upperBlocks.Count == 0)
                    {
                        continue;
                    }

                    var segments = new List<GameObject>(upperBlocks.Count);
                    for (int index = 0; index < upperBlocks.Count; index++)
                    {
                        LevelEditorPlacedBlock upper = upperBlocks[index];
                        GameObject segment = PrefabUtility.InstantiatePrefab(
                            mother.GrowthSegmentPrefab) as GameObject;
                        if (segment == null)
                        {
                            break;
                        }

                        segment.transform.SetParent(bottom.transform, true);
                        segment.transform.position = upper.transform.position;
                        segment.transform.rotation = upper.transform.rotation;
                        segment.transform.localScale = upper.transform.localScale;
                        segment.name = $"藤蔓节段 [{upper.Cell.x},{upper.Cell.y}]";
                        LevelEditorPlacedBlock placed =
                            segment.GetComponent<LevelEditorPlacedBlock>() ??
                            segment.AddComponent<LevelEditorPlacedBlock>();
                        placed.Configure(
                            upper.Cell,
                            upper.EntryName,
                            upper.EntryColor,
                            false,
                            true,
                            upper.CellWorldSize);
                        placed.SetPlanningSource(upper.PlanningBoxId,upper.PlanningSignature,upper.PlanningLocalCell);
                        segments.Add(segment);
                        Object.DestroyImmediate(upper.gameObject);
                    }

                    mother.ConfigureInitialGrowth(segments);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(mother);
                    foreach (GameObject segment in segments)
                    {
                        var child = segment.GetComponent<LadderSonFeature>();
                        PrefabUtility.RecordPrefabInstancePropertyModifications(child);
                        EditorUtility.SetDirty(child);
                    }
                    EditorUtility.SetDirty(mother);
                    chainCount++;
                }
            }

            return chainCount;
        }

        private static bool CanJoinVine(
            LevelEditorPlacedBlock lower,
            LevelEditorPlacedBlock upper)
        {
            return lower != null &&
                   upper != null &&
                   lower.CellWorldSize == upper.CellWorldSize &&
                   PrefabUtility.GetCorrespondingObjectFromSource(
                       lower.gameObject) ==
                   PrefabUtility.GetCorrespondingObjectFromSource(
                       upper.gameObject);
        }

        private static void GetLayoutStride(
            IReadOnlyList<PlanningRoom> rooms,
            int worldBlockCellWidth,
            int worldBlockCellHeight,
            out int strideX,
            out int strideY)
        {
            PlanningLayoutUtility.GetStride(
                rooms,
                worldBlockCellWidth,
                worldBlockCellHeight,
                out strideX,
                out strideY);
        }

        private static List<RoomLayout> BuildLayouts(
            IReadOnlyList<PlanningRoom> rooms,
            int worldBlockCellWidth,
            int worldBlockCellHeight)
        {
            var layouts = new List<RoomLayout>();
            GetLayoutStride(
                rooms,
                worldBlockCellWidth,
                worldBlockCellHeight,
                out int strideX,
                out int strideY);
            for (int index = 0; index < rooms.Count; index++)
            {
                PlanningRoom room = rooms[index];
                if (room == null ||
                    room.isConnector ||
                    room.boxes.Count == 0)
                {
                    continue;
                }

                PlanningLayoutUtility.RoomLayoutInfo layout =
                    PlanningLayoutUtility.GetRoomLayout(
                        room,
                        strideX,
                        strideY);
                layouts.Add(new RoomLayout(
                    room,
                    layout.OffsetX,
                    layout.OffsetY,
                    layout.MaxPlanY));
            }

            return layouts;
        }

        private static bool TryPlaceConnectorCell(
            LevelEditorBlockEntry entry,
            Vector2Int cell,
            Transform parent,
            HashSet<Vector2Int> occupied,
            HashSet<Vector2Int> existingCells)
        {
            if (entry == null ||
                !occupied.Add(cell) ||
                existingCells.Contains(cell))
            {
                return false;
            }

            LevelEditorPlacedBlock placed =
                PlaceBlock(entry, cell, parent);
            if (placed == null)
            {
                return false;
            }

            placed.gameObject.name =
                $"通道块 [{cell.x},{cell.y}]";
            return true;
        }

        private static LevelEditorPlacedBlock PlaceBlock(
            LevelEditorBlockEntry entry,
            Vector2Int cell,
            Transform parent)
        {
            if (entry == null)
            {
                return null;
            }

            GameObject source = entry.UsesPrefabDirectly
                ? entry.Prefab
                : entry.SourcePrefab;
            GameObject instance = source != null
                ? PrefabUtility.InstantiatePrefab(source) as GameObject
                : null;
            if (instance == null && entry.UsesPrefabDirectly)
            {
                return null;
            }

            if (instance == null)
            {
                instance = GameObject.CreatePrimitive(PrimitiveType.Cube);
                instance.transform.localScale =
                    Vector3.one * CellSize;
            }

            Vector2Int sceneCell = new Vector2Int(
                cell.x,
                -cell.y - 1);
            instance.transform.SetParent(parent, true);
            instance.transform.position = new Vector3(
                (sceneCell.x + .5f) * CellSize,
                (sceneCell.y + .5f) * CellSize,
                0f);

            if (!entry.UsesPrefabDirectly)
            {
                SurfaceTileBlock block =
                    instance.GetComponent<SurfaceTileBlock>();
                if (block != null)
                {
                    block.RegenerateBlockId();
                    EditorUtility.SetDirty(block);
                }
            }

            LevelEditorPlacedBlock placed =
                instance.GetComponent<LevelEditorPlacedBlock>() ??
                instance.AddComponent<LevelEditorPlacedBlock>();
            placed.Configure(
                sceneCell,
                entry.DisplayName,
                entry.Color,
                !entry.UsesPrefabDirectly,
                false,
                CellSize);
            if (!entry.UsesPrefabDirectly)
            {
                LevelEditorDecorationService.ApplyToPlacedBlock(
                    instance,
                    entry,
                    false);
            }

            return placed;
        }

        private static LevelEditorBlockEntry FindEntry(
            LevelEditorPalette palette,
            PlanningBox box)
        {
            if (!string.IsNullOrEmpty(box.paletteEntryId))
            {
                for (int index = 0;
                     index < palette.Entries.Count;
                     index++)
                {
                    LevelEditorBlockEntry entry = palette.Entries[index];
                    if (entry != null &&
                        entry.EntryId == box.paletteEntryId)
                    {
                        return entry;
                    }
                }
            }

            if (!string.IsNullOrEmpty(box.paletteEntryName))
            {
                for (int index = 0;
                     index < palette.Entries.Count;
                     index++)
                {
                    LevelEditorBlockEntry entry = palette.Entries[index];
                    if (entry != null &&
                        entry.DisplayName == box.paletteEntryName)
                    {
                        return entry;
                    }
                }
            }

            return FindEntry(palette, box.type);
        }

        private static LevelEditorPropEntry FindPropEntry(
            LevelEditorPalette palette,
            PlanningBox box)
        {
            if (box.propEntryId == PlanningDoorUtility.ButtonEntryId) return PlanningDoorUtility.GetButtonEntry();
            for (int index = 0;
                 index < palette.PropEntries.Count;
                 index++)
            {
                LevelEditorPropEntry entry =
                    palette.PropEntries[index];
                if (entry == null)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(box.propEntryId) &&
                    entry.EntryId == box.propEntryId)
                {
                    return entry;
                }

                if (string.IsNullOrEmpty(box.propEntryId) &&
                    !string.IsNullOrEmpty(box.propEntryName) &&
                    entry.DisplayName == box.propEntryName)
                {
                    return entry;
                }
            }

            return null;
        }

        private static bool TryPlacePlanningBox(
            LevelEditorPalette palette,
            PlanningBox box,
            Vector2Int cell,
            Transform parent,
            HashSet<Vector2Int> occupied,
            HashSet<Vector2Int> existingCells,
            Vector2Int localCell)
        {
            if (box != null &&
                box.propEntryId ==
                PlanningBox.BossRespawnMarkerEntryId)
            {
                return true;
            }

            if (!PlanningShapeUtility.Contains(box, localCell)) return false;
            if (!occupied.Add(cell) || existingCells.Contains(cell))
            {
                return false;
            }

            if (PlacePlanningBox(palette, box, cell, parent, localCell) == null)
            {
                return false;
            }

            return true;
        }

        private static LevelEditorPlacedBlock PlacePlanningBox(
            LevelEditorPalette palette,
            PlanningBox box,
            Vector2Int cell,
            Transform parent,
            Vector2Int localCell)
        {
            if ((box.isMerged || box.singleInstance || PlanningDoorUtility.IsDoor(box)) && !IsShapeAnchor(box, localCell)) return null;
            if (box.isMerged)
            {
                cell -= localCell - new Vector2Int(box.x, box.y);
                localCell = new Vector2Int(box.x, box.y);
            }
            LevelEditorPlacedBlock placed;
            IReadOnlyList<LevelEditorComponentValueOverride> defaults;
            if (!string.IsNullOrEmpty(box.propEntryId) ||
                !string.IsNullOrEmpty(box.propEntryName))
            {
                placed = PlacePropBlock(palette, box, cell, parent);
                defaults = FindPropEntry(palette,box)?.ComponentValueOverrides;
            }
            else
            {
                LevelEditorBlockEntry entry = FindEntry(palette, box);
                placed = entry != null ? PlaceBlock(entry, cell, parent) : null;
                defaults = entry?.ComponentValueOverrides;
            }
            if (placed == null) return null;
            bool fittedLadder = PlanningDoorUtility.IsLadderEntry(box.propEntryId);
            bool fittedPlatform = PlanningDoorUtility.IsPlatformEntry(box.propEntryId);
            if (fittedLadder)
            {
                // Use authored dimensions, including old two-cell non-merged saves.
                // A centered unit trigger avoids multiplying the legacy height twice.
                var ladderCollider = placed.GetComponent<BoxCollider>();
                if (ladderCollider != null)
                {
                    var size = ladderCollider.size; size.y = 1f; ladderCollider.size = size;
                    var center = ladderCollider.center; center.y = 0f; ladderCollider.center = center;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(ladderCollider);
                }
                placed.transform.localScale *= CellSize;
            }
            if (fittedPlatform) placed.transform.localScale *= CellSize;
            if (!fittedLadder && !fittedPlatform && (PlanningDoorUtility.HasFixedFootprint(box.propEntryId) || box.propEntryId == PlanningDoorUtility.ButtonEntryId))
            {
                placed.transform.localScale *= CellSize;
                if (PlanningDoorUtility.HasFixedFootprint(box.propEntryId))
                {
                    var size = PlanningDoorUtility.Footprint(box.propEntryId);
                    placed.transform.position -= Vector3.up * (size.y - 1) * CellSize;
                    placed.Configure(new Vector2Int(cell.x,-cell.y-size.y),placed.EntryName,placed.EntryColor,false,true,CellSize);
                    placed.SetSizeCells(size);
                }
            }
            var values=box.hasComponentOverrides ? box.componentOverrides : defaults;
            LevelEditorComponentOverrideUtility.ApplyOverrides(placed.gameObject,values,false);
            if (fittedPlatform && placed.TryGetComponent<Project.BlockFeatures.MovingPlatformFeature>(out var platformBody) &&
                platformBody.BodyOrientation == Project.BlockFeatures.MovingPlatformBodyOrientation.Vertical &&
                placed.TryGetComponent<BoxCollider>(out var platformCollider))
            {
                // Rotate only the physical strip, not the moving root / world-space
                // route. Old Auto data and its authored collision remain untouched.
                Vector3 shape = platformCollider.size, pivot = platformCollider.center;
                platformCollider.size = new Vector3(shape.y, shape.x, shape.z);
                platformCollider.center = new Vector3(-pivot.y, pivot.x, pivot.z);
                PrefabUtility.RecordPrefabInstancePropertyModifications(platformCollider);
            }
            if(box.isMerged || fittedLadder || fittedPlatform && box.singleInstance)
            {
                placed.transform.localScale=Vector3.Scale(placed.transform.localScale,new Vector3(box.width,box.height,1));
                placed.transform.position+=new Vector3((box.width-1)*CellSize*.5f,-(box.height-1)*CellSize*.5f,0);
                var bottomCell=new Vector2Int(cell.x,-cell.y-box.height);
                placed.Configure(bottomCell,placed.EntryName,placed.EntryColor,!placed.IsProp && !FindEntry(palette,box).UsesPrefabDirectly,placed.IsProp,CellSize);
                placed.SetSizeCells(new Vector2Int(box.width,box.height));
            }
            // Some single-instance props own their real geometry. Preserve the
            // logical footprint without multiplying it into the root scale.
            if (box.singleInstance && !box.isMerged && !fittedLadder && !fittedPlatform &&
                !PlanningDoorUtility.HasFixedFootprint(box.propEntryId))
                placed.SetSizeCells(new Vector2Int(box.width, box.height));
            if (box.isMerged && PlanningShapeUtility.IsIrregular(box))
            {
                var coverage = new List<RectInt>();
                foreach (RectInt rect in PlanningShapeUtility.Rectangles(box))
                {
                    var part = rect; part.position -= new Vector2Int(box.x, box.y); coverage.Add(part);
                }
                var shape = placed.gameObject.AddComponent<LevelEditorMergedShape>();
                shape.Configure(new Vector2Int(box.width, box.height), coverage);
            }
            // The signature tracks content, not selection or transient scene references.
            PlanningBox signatureBox = box.PersistentClone();
            placed.SetPlanningSource(box.id,JsonUtility.ToJson(signatureBox)+JsonUtility.ToJson(new OverrideSignature { values = LevelEditorComponentOverrideUtility.PersistentValues(values) }),localCell);
            Project.BlockFeatures.Editor.ArtworkScenePreview.QueueObject(placed.gameObject);
            return placed;
        }

        [System.Serializable] private sealed class OverrideSignature { public List<LevelEditorComponentValueOverride> values; }

        private static bool IsShapeAnchor(PlanningBox box, Vector2Int cell) =>
            PlanningShapeUtility.Rectangles(box).Count > 0 && PlanningShapeUtility.Rectangles(box)[0].position == cell;

        private static bool CanPlaceMerged(PlanningBox box,Vector2Int offset,HashSet<Vector2Int> occupied,HashSet<Vector2Int> external)
        {
            if(!box.isMerged && !box.singleInstance && !PlanningDoorUtility.IsDoor(box)) return true;
            foreach (Vector2Int cell in PlanningShapeUtility.Cells(box))
                if(occupied.Contains(cell+offset) || external.Contains(cell+offset)) return false;
            return true;
        }

        private static LevelEditorPlacedBlock PlacePropBlock(
            LevelEditorPalette palette,
            PlanningBox box,
            Vector2Int cell,
            Transform parent)
        {
            LevelEditorPropEntry prop = FindPropEntry(palette, box);
            if (prop == null || prop.Prefab == null)
            {
                return null;
            }

            GameObject instance =
                PrefabUtility.InstantiatePrefab(prop.Prefab) as GameObject;
            if (instance == null)
            {
                return null;
            }

            LevelEditorComponentOverrideUtility.ApplyOverrides(
                instance,
                box.hasComponentOverrides ? box.componentOverrides : prop.ComponentValueOverrides,
                false);
            Vector2Int sceneCell = new Vector2Int(
                cell.x,
                -cell.y - 1);
            instance.transform.SetParent(parent, true);
            instance.transform.position = new Vector3(
                (sceneCell.x + .5f) * CellSize,
                (sceneCell.y + .5f) * CellSize,
                0f);
            LevelEditorPlacedBlock placed =
                instance.GetComponent<LevelEditorPlacedBlock>() ??
                instance.AddComponent<LevelEditorPlacedBlock>();
            placed.Configure(
                sceneCell,
                prop.DisplayName,
                Color.white,
                false,
                true,
                CellSize);
            return placed;
        }

        private static LevelEditorBlockEntry FindEntry(
            LevelEditorPalette palette,
            PlanningDetailType type)
        {
            string preferredName = PreferredEntryName(type);
            for (int index = 0; index < palette.Entries.Count; index++)
            {
                LevelEditorBlockEntry entry = palette.Entries[index];
                if (entry != null &&
                    entry.DisplayName == preferredName)
                {
                    return entry;
                }
            }

            return palette.Entries[0];
        }

        private static string PreferredEntryName(
            PlanningDetailType type)
        {
            switch (type)
            {
                case PlanningDetailType.Solid:
                    return "黑方块";
                case PlanningDetailType.Platform:
                    return "白方块";
                case PlanningDetailType.Hazard:
                    return "红方块";
                case PlanningDetailType.Water:
                    return "蓝方块";
                default:
                    return "绿方块";
            }
        }

        private static int GetMaxPlanY(PlanningRoom room)
        {
            int maxY = 1;
            for (int index = 0; index < room.boxes.Count; index++)
            {
                PlanningBox box = room.boxes[index];
                maxY = Mathf.Max(maxY, box.y + box.height);
            }

            return maxY;
        }

        private static string BuildObjectName(
            PlanningBox box,
            Vector2Int cell)
        {
            string label = string.IsNullOrWhiteSpace(box.label)
                ? box.type.ToString()
                : box.label;
            return $"{label} [{cell.x},{cell.y}]";
        }

        private static GameObject FindGeneratedRoot(string rootName)
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                return null;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                if (roots[index].name == rootName)
                {
                    return roots[index];
                }
            }

            return null;
        }

        private static Transform FindDirectChild(
            Transform parent,
            string childName)
        {
            if (parent == null || string.IsNullOrEmpty(childName))
            {
                return null;
            }

            for (int index = 0; index < parent.childCount; index++)
            {
                Transform child = parent.GetChild(index);
                if (child.name == childName)
                {
                    return child;
                }
            }

            return null;
        }

        private static GameObject FindGeneratedRoomRoot()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                return null;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                if (roots[index].name.StartsWith(RoomRootPrefix))
                {
                    return roots[index];
                }
            }

            return null;
        }

        private static void DestroyGeneratedRoots()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                return;
            }

            var targets = new List<GameObject>();
            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                GameObject root = roots[index];
                if (root.name == MapRootName ||
                    root.name.StartsWith(RoomRootPrefix))
                {
                    targets.Add(root);
                }
            }

            for (int index = 0; index < targets.Count; index++)
            {
                DestroySceneObject(targets[index]);
            }
        }

        private sealed class RoomLayout
        {
            internal readonly PlanningRoom Room;
            internal readonly int OffsetX;
            internal readonly int OffsetY;
            internal readonly int MaxPlanY;

            internal RoomLayout(
                PlanningRoom room,
                int offsetX,
                int offsetY,
                int maxPlanY)
            {
                Room = room;
                OffsetX = offsetX;
                OffsetY = offsetY;
                MaxPlanY = maxPlanY;
            }
        }
    }
}
