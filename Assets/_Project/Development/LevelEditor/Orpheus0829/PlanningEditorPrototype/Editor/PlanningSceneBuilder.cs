using System.Collections.Generic;
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
        private const float CellSize = 1f;
        private const int RoomGap = 6;
        private const string RoomRootPrefix = "__PlanningRoom_";
        private const string MapRootName = "__PlanningMapGenerated";

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
                Undo.RegisterCreatedObjectUndo(
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
                Undo.RegisterCreatedObjectUndo(
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

            LevelEditorPalette palette =
                LevelEditorPaletteService.GetOrCreate();
            if (palette == null || palette.Entries.Count == 0)
            {
                message = "关卡编辑器还没有可用方块栏目。";
                return false;
            }

            DestroyGeneratedRoots();

            List<RoomLayout> layouts = BuildLayouts(
                rooms,
                worldBlockCellWidth,
                worldBlockCellHeight);
            if (layouts.Count == 0)
            {
                message = "没有可生成的房间详情。";
                return false;
            }

            var existingCells = new HashSet<Vector2Int>();
            LevelEditorPlacedBlock[] existingBlocks =
                Object.FindObjectsByType<LevelEditorPlacedBlock>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            for (int index = 0; index < existingBlocks.Length; index++)
            {
                existingCells.Add(existingBlocks[index].Cell);
            }

            var occupied = new HashSet<Vector2Int>();
            int placedCount = 0;
            int skippedCount = 0;
            int connectorCount = 0;
            int patchCount = 0;

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("生成规划关卡：" + title);

            GameObject root = new GameObject(rootName);
            Undo.RegisterCreatedObjectUndo(root, "生成规划关卡");
            SceneManager.MoveGameObjectToScene(root, scene);
            root.transform.position = Vector3.zero;

            for (int layoutIndex = 0;
                 layoutIndex < layouts.Count;
                 layoutIndex++)
            {
                RoomLayout layout = layouts[layoutIndex];
                GameObject roomRoot = new GameObject(layout.Room.name);
                roomRoot.transform.SetParent(root.transform, false);
                Undo.RegisterCreatedObjectUndo(
                    roomRoot,
                    "生成规划房间");

                for (int boxIndex = 0;
                     boxIndex < layout.Room.boxes.Count;
                     boxIndex++)
                {
                    PlanningBox box = layout.Room.boxes[boxIndex];
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
                                    roomRoot.transform);
                            if (placed == null)
                            {
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
                    Undo.RegisterCreatedObjectUndo(
                        patchRoot,
                        "生成装配图补丁");
                    for (int boxIndex = 0;
                         boxIndex < assemblyPatches.Count;
                         boxIndex++)
                    {
                        PlanningBox box = assemblyPatches[boxIndex];
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
                                        existingCells))
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
                    Undo.RegisterCreatedObjectUndo(
                        connectorRoot,
                        "生成连接通道");
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
                                        existingCells))
                                {
                                    connectorCount++;
                                }
                            }
                        }
                    }
                }
            }

            if (placedCount == 0)
            {
                Undo.DestroyObjectImmediate(root);
                Undo.CollapseUndoOperations(undoGroup);
                message = "没有生成方块，请检查栏目方块是否可用。";
                return false;
            }

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = root;
            SceneView view = SceneView.lastActiveSceneView;
            if (view != null)
            {
                view.FrameSelected();
            }

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

            return true;
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
                Undo.AddComponent<LevelEditorPlacedBlock>(instance);
            placed.Configure(
                sceneCell,
                entry.DisplayName,
                entry.Color,
                !entry.UsesPrefabDirectly);
            if (!entry.UsesPrefabDirectly)
            {
                LevelEditorDecorationService.ApplyToPlacedBlock(
                    instance,
                    entry);
            }

            Undo.RegisterCreatedObjectUndo(instance, "生成规划方块");
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
            HashSet<Vector2Int> existingCells)
        {
            if (!occupied.Add(cell) || existingCells.Contains(cell))
            {
                return false;
            }

            if (PlacePlanningBox(palette, box, cell, parent) == null)
            {
                return false;
            }

            return true;
        }

        private static LevelEditorPlacedBlock PlacePlanningBox(
            LevelEditorPalette palette,
            PlanningBox box,
            Vector2Int cell,
            Transform parent)
        {
            if (!string.IsNullOrEmpty(box.propEntryId) ||
                !string.IsNullOrEmpty(box.propEntryName))
            {
                return PlacePropBlock(palette, box, cell, parent);
            }

            LevelEditorBlockEntry entry = FindEntry(palette, box);
            return entry != null
                ? PlaceBlock(entry, cell, parent)
                : null;
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
                Undo.AddComponent<LevelEditorPlacedBlock>(instance);
            placed.Configure(
                sceneCell,
                prop.DisplayName,
                Color.white,
                false,
                true);
            Undo.RegisterCreatedObjectUndo(instance, "生成规划道具");
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
                Undo.DestroyObjectImmediate(targets[index]);
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
