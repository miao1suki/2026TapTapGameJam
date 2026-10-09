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
        private const int RoomGap = 6;
        private const string RoomRootPrefix = "__PlanningRoom_";
        private const string MapRootName = "__PlanningMapGenerated";
        private const string StagingRootName = "__PlanningMapStaging";
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
                   FindDirectChild(root.transform, room.name) != null;
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
                16,
                16,
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

            return TryBuild(
                document.name,
                MapRootName,
                document.rooms,
                document.assemblyPatches,
                document.worldBlockCellWidth,
                document.worldBlockCellHeight,
                true,
                false,
                out message);
        }

        internal static bool TryApplyDocument(
            PlanningDocument document,
            out string message)
        {
            if (document == null || document.rooms.Count == 0)
            {
                message = "世界图里还没有房间。";
                return false;
            }

            var names = new HashSet<string>();
            foreach (PlanningRoom room in document.rooms)
            {
                if (room != null && !names.Add(room.name))
                {
                    message = $"存在同名房间“{room.name}”，" +
                              "请先改为唯一名称再应用到场景。";
                    return false;
                }
            }

            return TryBuild(
                document.name,
                StagingRootName,
                document.rooms,
                document.assemblyPatches,
                document.worldBlockCellWidth,
                document.worldBlockCellHeight,
                true,
                true,
                out message);
        }

        internal static GameObject FindRoomContainer(PlanningRoom room)
        {
            if (room == null)
            {
                return null;
            }

            GameObject root = FindGeneratedRoot(MapRootName);
            Transform child = FindDirectChild(
                root != null ? root.transform : null,
                room.name);
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

            Transform child = FindDirectChild(root.transform, room.name);
            GameObject container;
            if (child != null)
            {
                container = child.gameObject;
            }
            else
            {
                container = new GameObject(room.name);
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

                for(int x=block.Cell.x;x<block.Cell.x+block.SizeCells.x;x++)
                    for(int y=block.Cell.y;y<block.Cell.y+block.SizeCells.y;y++)
                        existingCells.Add(new Vector2Int(x,-y-1));
            }

            var occupied = new HashSet<Vector2Int>();
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
                GameObject roomRoot = new GameObject(layout.Room.name);
                roomRoot.transform.SetParent(root.transform, false);

                for (int boxIndex = 0;
                     boxIndex < layout.Room.boxes.Count;
                     boxIndex++)
                {
                    PlanningBox box = layout.Room.boxes[boxIndex];
                    if (!CanPlaceMerged(box, new Vector2Int(layout.OffsetX,layout.OffsetY),occupied,existingCells))
                    { skippedCount += box.width*box.height; continue; }
                    for (int y = box.y;
                         y < box.y + box.height;
                         y++)
                    {
                        for (int x = box.x;
                             x < box.x + box.width;
                             x++)
                        {
                            int sceneY = layout.OffsetY + y;
                            var cell = new Vector2Int(
                                layout.OffsetX + x,
                                sceneY);
                            if (!occupied.Add(cell) ||
                                existingCells.Contains(cell))
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
                                if ((box.isMerged || box.singleInstance || PlanningDoorUtility.IsDoor(box)) && (x != box.x || y != box.y)) continue;
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
                        if (!CanPlaceMerged(box,Vector2Int.zero,occupied,existingCells)) continue;
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
                                        occupied,
                                        existingCells,
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
                        connector.name);
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
                        if (!CanPlaceMerged(box,origin,occupied,existingCells)) continue;
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
                                        occupied,
                                        existingCells,
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
                PlanningDoorUtility.BindSceneDoors(FindGeneratedRoot(MapRootName)?.transform, rooms, assemblyPatches);
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
            PlanningDoorUtility.BindSceneDoors(root.transform, rooms, assemblyPatches);
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
                    added += staged.GetComponentsInChildren<
                        LevelEditorPlacedBlock>(true).Length;
                    staged.SetParent(existingRoot.transform, true);
                    RegisterSceneCreation(
                        staged.gameObject,
                        "应用规划房间");
                    continue;
                }

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
            var oldByCell = new Dictionary<Vector2Int, LevelEditorPlacedBlock>();
            LevelEditorPlacedBlock[] oldBlocks =
                existing.GetComponentsInChildren<LevelEditorPlacedBlock>(true);
            foreach (LevelEditorPlacedBlock block in oldBlocks)
            {
                if (block.transform.parent == existing)
                {
                    oldByCell[block.Cell] = block;
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

                if (oldByCell.TryGetValue(
                        desired.Cell,
                        out LevelEditorPlacedBlock old))
                {
                    oldByCell.Remove(desired.Cell);
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

            foreach (LevelEditorPlacedBlock stale in oldByCell.Values)
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
            if ((box.isMerged || box.singleInstance || PlanningDoorUtility.IsDoor(box)) && localCell != new Vector2Int(box.x,box.y)) return null;
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
            if (PlanningDoorUtility.IsDoor(box) || box.propEntryId == PlanningDoorUtility.ButtonEntryId)
            {
                placed.transform.localScale *= CellSize;
                if (PlanningDoorUtility.IsDoor(box))
                {
                    placed.transform.position -= Vector3.up * CellSize;
                    placed.Configure(new Vector2Int(cell.x,-cell.y-2),placed.EntryName,placed.EntryColor,false,true,CellSize);
                    placed.SetSizeCells(new Vector2Int(1,2));
                }
            }
            var values=box.hasComponentOverrides ? box.componentOverrides : defaults;
            LevelEditorComponentOverrideUtility.ApplyOverrides(placed.gameObject,values,false);
            if(box.isMerged)
            {
                placed.transform.localScale=Vector3.Scale(placed.transform.localScale,new Vector3(box.width,box.height,1));
                placed.transform.position+=new Vector3((box.width-1)*CellSize*.5f,-(box.height-1)*CellSize*.5f,0);
                var bottomCell=new Vector2Int(cell.x,-cell.y-box.height);
                placed.Configure(bottomCell,placed.EntryName,placed.EntryColor,!placed.IsProp && !FindEntry(palette,box).UsesPrefabDirectly,placed.IsProp,CellSize);
                placed.SetSizeCells(new Vector2Int(box.width,box.height));
            }
            // The signature tracks content, not selection or transient scene references.
            placed.SetPlanningSource(box.id,JsonUtility.ToJson(box)+JsonUtility.ToJson(new OverrideSignature { values = values == null ? new List<LevelEditorComponentValueOverride>() : new List<LevelEditorComponentValueOverride>(values) }),localCell);
            return placed;
        }

        [System.Serializable] private sealed class OverrideSignature { public List<LevelEditorComponentValueOverride> values; }

        private static bool CanPlaceMerged(PlanningBox box,Vector2Int offset,HashSet<Vector2Int> occupied,HashSet<Vector2Int> external)
        {
            if(!box.isMerged && !box.singleInstance && !PlanningDoorUtility.IsDoor(box)) return true;
            for(int x=box.x;x<box.x+box.width;x++) for(int y=box.y;y<box.y+box.height;y++)
                if(occupied.Contains(new Vector2Int(x,y)+offset) || external.Contains(new Vector2Int(x,y)+offset)) return false;
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
