using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Project.LevelEditor;
using Project.LevelEditor.Editor;
using UnityEditor;
using UnityEngine;

namespace PlanningEditorPrototype
{
    internal static class PlanningFragmentUtility
    {
        internal static IEnumerable<PlanningBox> Boxes(PlanningDocument doc) => doc.rooms.SelectMany(r => r.boxes).Concat(doc.assemblyPatches);
        private static IEnumerable<Vector2Int> Cells(RectInt rect)
        { for (int y = rect.y; y < rect.yMax; y++) for (int x = rect.x; x < rect.xMax; x++) yield return new Vector2Int(x, y); }
        internal static bool TryBounds(
            PlanningDocument doc,
            out RectInt bounds)
        {
            bounds = default;
            if (doc == null)
            {
                return false;
            }

            var cells = doc.rooms.SelectMany(r => r.cells).ToList();
            if (cells.Count == 0)
            {
                return false;
            }

            int x = cells.Min(c => c.x), y = cells.Min(c => c.y);
            bounds = new RectInt(
                x,
                y,
                cells.Max(c => c.x) - x + 1,
                cells.Max(c => c.y) - y + 1);
            return true;
        }

        internal static RectInt Bounds(PlanningDocument doc)
        {
            if (!TryBounds(doc, out RectInt bounds))
            {
                throw new InvalidDataException(
                    "存档没有可导入的房间或通道。");
            }

            return bounds;
        }

        internal static void ShiftBox(PlanningBox box, Vector2Int delta)
        {
            box.x += delta.x; box.y += delta.y;
            for (int i = 0; i < box.mergeParts.Count; i++)
            {
                var part = PlanningBox.FromPersistentJson(box.mergeParts[i]);
                if (part == null) throw new InvalidDataException("合并物体的原始数据损坏。");
                ShiftBox(part, delta); box.mergeParts[i] = JsonUtility.ToJson(part);
            }
        }

        internal static PlanningDocument Extract(PlanningDocument source, ICollection<string> owners, RectInt? area, out int detached)
        {
            var result = PlanningDocument.FromPersistentJson(source.ToPersistentJson());
            var selected = owners == null ? null : new HashSet<string>(owners);
            foreach (var room in result.rooms.ToList())
            {
                if (selected != null && !selected.Contains(room.id)) { result.rooms.Remove(room); continue; }
                if (!area.HasValue) continue;
                var oldOrigin = PlanningWorldUtility.Bounds(room).position;
                room.cells.RemoveAll(c => !area.Value.Contains(new Vector2Int(c.x, c.y)));
                if (room.cells.Count == 0) { result.rooms.Remove(room); continue; }
                foreach (var box in room.boxes.ToList())
                {
                    var world = new RectInt(oldOrigin.x + box.x, oldOrigin.y + box.y, box.width, box.height);
                    var localArea = new RectInt(area.Value.position - oldOrigin, area.Value.size);
                    if (!PlanningShapeUtility.Overlaps(box, localArea)) { room.boxes.Remove(box); continue; }
                    if (!Contains(area.Value, world)) ClipBox(result, box, world, area.Value);
                }
                Vector2Int shift = oldOrigin - PlanningWorldUtility.Bounds(room).position;
                foreach (var box in room.boxes) ShiftBox(box, shift);
                if (room.id == result.playerStartRoomId) result.playerStartLocal += (Vector2)shift;
                room.allowDisconnectedCells = true;
            }
            var retained = new HashSet<string>(result.rooms.Select(r => r.id));
            var keptCells = new HashSet<Vector2Int>(result.rooms.SelectMany(r => r.cells).Select(c => new Vector2Int(c.x, c.y)));
            foreach (var patch in result.assemblyPatches.ToList())
            {
                var rect = new RectInt(patch.x, patch.y, patch.width, patch.height);
                if (area.HasValue ? !PlanningShapeUtility.Overlaps(patch, area.Value) : !PlanningShapeUtility.Cells(patch).Any(keptCells.Contains))
                    result.assemblyPatches.Remove(patch);
                else if (area.HasValue && !Contains(area.Value, rect))
                    ClipBox(result, patch, rect, area.Value);
                else if (selected != null && PlanningShapeUtility.Cells(patch).Any(cell => !keptCells.Contains(cell)))
                    throw new InvalidDataException("装配物体跨越未选中房间，请同时选择相关房间或使用框选导出。");
            }
            result.doors.RemoveAll(d => !retained.Contains(d.roomId) || area.HasValue && !area.Value.Contains(new Vector2Int(d.x, d.y)));
            result.keys.RemoveAll(k => !retained.Contains(k.roomId) || area.HasValue && !area.Value.Contains(new Vector2Int(k.x, k.y)));
            if (!retained.Contains(result.playerStartRoomId)) result.playerStartRoomId = null;
            else if (area.HasValue)
            {
                var room = result.FindRoom(result.playerStartRoomId);
                var position = PlanningWorldUtility.Bounds(room).position + Vector2Int.FloorToInt(result.playerStartLocal);
                if (!area.Value.Contains(position)) result.playerStartRoomId = null;
            }
            result.regions.RemoveAll(r => area.HasValue ? !area.Value.Overlaps(r.Bounds) : !Cells(r.Bounds).Any(keptCells.Contains));
            foreach (var region in result.regions)
                if (area.HasValue)
                {
                    int x = Mathf.Max(region.x, area.Value.x), y = Mathf.Max(region.y, area.Value.y);
                    region.width = Mathf.Min(region.Bounds.xMax, area.Value.xMax) - x;
                    region.height = Mathf.Min(region.Bounds.yMax, area.Value.yMax) - y;
                    region.x = x; region.y = y;
                }
            var keys = new HashSet<string>(result.keys.Select(k => k.id));
            foreach (var item in result.locks) item.keyIds.RemoveAll(id => !keys.Contains(id));
            result.locks.RemoveAll(l => !result.doors.Any(d => d.lockId == l.id));
            var keptBoxes = Boxes(result).ToList();
            var keptIds = new HashSet<string>(keptBoxes.Select(b => b.id));
            int excludedButtons = Boxes(source).Count(b => !string.IsNullOrEmpty(b.doorOwnerId) &&
                keptIds.Contains(b.doorOwnerId) && !keptIds.Contains(b.id));
            detached = DetachExternalLinks(result) + excludedButtons;
            result.RefreshConnectorPaths();
            Bounds(result);
            return result;
        }

        internal static PlanningDocument Import(PlanningDocument target, PlanningDocument source, Vector2Int anchor, bool includePlayer, out int detached)
        {
            ValidateStructure(source);
            var incoming = PlanningDocument.FromPersistentJson(source.ToPersistentJson());
            var result = PlanningDocument.FromPersistentJson(target.ToPersistentJson());
            // Legacy 16:1 ownership must migrate before collision checks, not after them.
            ValidatePlacement(result, incoming, anchor);
            ResolveDependencies(incoming, true);
            var origin = Bounds(incoming).position;
            var delta = anchor - origin;
            ValidateObjectPlacement(result, incoming, delta);
            detached = DetachExternalLinks(incoming);
            var ids = new Dictionary<string, string>();
            void Add(string id, string prefix)
            {
                if (string.IsNullOrEmpty(id) || ids.ContainsKey(id)) throw new InvalidDataException("存档包含空或重复的内部ID。");
                ids.Add(id, PlanningDocument.NewId(prefix));
            }
            void IndexBox(PlanningBox box)
            {
                Add(box.id, "box");
                foreach (string json in box.mergeParts) IndexBox(PlanningBox.FromPersistentJson(json));
            }
            foreach (var room in incoming.rooms) { Add(room.id, "room"); foreach (var box in room.boxes) IndexBox(box); }
            foreach (var box in incoming.assemblyPatches) IndexBox(box);
            foreach (var region in incoming.regions) Add(region.id, "region");
            foreach (var door in incoming.doors) Add(door.id, "door");
            foreach (var key in incoming.keys) Add(key.id, "key");
            foreach (var item in incoming.locks) Add(item.id, "lock");
            string Map(string id) => !string.IsNullOrEmpty(id) && ids.TryGetValue(id, out var value) ? value : null;
            void RemapBox(PlanningBox box)
            {
                box.id = Map(box.id); box.doorOwnerId = Map(box.doorOwnerId);
                box.mechanismBaseId = Map(box.mechanismBaseId); box.crankOwnerId = Map(box.crankOwnerId);
                if (string.IsNullOrEmpty(box.mechanismBaseId)) box.mechanismBaseCellOffset = default;
                box.sceneBlock = null;
                for (int i = 0; i < box.mergeParts.Count; i++)
                {
                    var part = PlanningBox.FromPersistentJson(box.mergeParts[i]); RemapBox(part); box.mergeParts[i] = JsonUtility.ToJson(part);
                }
            }
            foreach (var room in incoming.rooms)
            {
                room.id = Map(room.id); room.fromRoomId = Map(room.fromRoomId); room.toRoomId = Map(room.toRoomId);
                room.connectedRoomIds = room.connectedRoomIds.Select(Map).Where(id => id != null).ToList();
                foreach (var cell in room.cells) { cell.x += delta.x; cell.y += delta.y; }
                foreach (var box in room.boxes) RemapBox(box);
                if (!room.isConnector)
                {
                    string name = room.name; int suffix = 2;
                    while (result.rooms.Any(r => r.name == room.name)) room.name = name + " (" + suffix++ + ")";
                }
                result.rooms.Add(room);
            }
            foreach (var box in incoming.assemblyPatches) { RemapBox(box); ShiftBox(box, delta); result.assemblyPatches.Add(box); }
            foreach (var region in incoming.regions) { region.id = Map(region.id); region.x += delta.x; region.y += delta.y; result.regions.Add(region); }
            foreach (var door in incoming.doors) { door.id = Map(door.id); door.roomId = Map(door.roomId); door.lockId = Map(door.lockId); door.x += delta.x; door.y += delta.y; result.doors.Add(door); }
            foreach (var key in incoming.keys) { key.id = Map(key.id); key.roomId = Map(key.roomId); key.x += delta.x; key.y += delta.y; result.keys.Add(key); }
            foreach (var item in incoming.locks) { item.id = Map(item.id); item.keyIds = item.keyIds.Select(Map).Where(id => id != null).ToList(); result.locks.Add(item); }
            if (includePlayer && Map(incoming.playerStartRoomId) != null)
            { result.playerStartRoomId = Map(incoming.playerStartRoomId); result.playerStartLocal = incoming.playerStartLocal; }
            result.Normalize();
            return result;
        }

        private static bool Contains(RectInt outer, RectInt inner) => outer.x <= inner.x && outer.y <= inner.y && outer.xMax >= inner.xMax && outer.yMax >= inner.yMax;
        private static void ClipBox(PlanningDocument document, PlanningBox box, RectInt world, RectInt area)
        {
            if (box.isMerged || box.singleInstance || PlanningDoorUtility.IsDoor(box) || !string.IsNullOrEmpty(box.doorOwnerId))
                throw new InvalidDataException("导出边界切过完整物体，请扩大选区或先解除合并。");
            var original = box.PersistentClone();
            int x = Mathf.Max(world.x, area.x), y = Mathf.Max(world.y, area.y);
            box.x += x - world.x; box.y += y - world.y;
            box.width = Mathf.Min(world.xMax, area.xMax) - x;
            box.height = Mathf.Min(world.yMax, area.yMax) - y;
            PlanningMechanismUtility.RemapCarvedSource(original, new[] { box }, Boxes(document));
        }
        internal static void ValidatePlacement(PlanningDocument target, PlanningDocument source, Vector2Int anchor)
        {
            Vector2Int origin = Bounds(source).position;
            Vector2Int shift = new Vector2Int(checked(anchor.x - origin.x), checked(anchor.y - origin.y));
            var occupied = new HashSet<Vector2Int>(target.rooms.SelectMany(r => r.cells).Select(c => new Vector2Int(c.x, c.y)));
            foreach (var cell in source.rooms.SelectMany(r => r.cells))
                if (!occupied.Add(new Vector2Int(checked(cell.x + shift.x), checked(cell.y + shift.y))))
                    throw new InvalidDataException("导入位置与现有房间或通道重叠，请调整导入坐标。");
        }

        // Run once on file selection/apply, never during pan or drawing previews.
        internal static void ValidateDependencies(PlanningDocument source)
        { ValidateStructure(source); ResolveDependencies(source, false); }

        private static void ResolveDependencies(PlanningDocument source, bool canonicalize)
        {
            var palette = LevelEditorPaletteService.GetOrCreate();
            var resolvedAssets = new HashSet<string>();
            foreach (var box in Boxes(source)) ResolveBox(box);

            void ResolveBox(PlanningBox box)
            {
                string label = box.propEntryName ?? box.paletteEntryName ?? box.label ?? box.id;
                if (box.type == PlanningDetailType.Prop)
                {
                    var entry = box.propEntryId == PlanningDoorUtility.ButtonEntryId
                        ? PlanningDoorUtility.GetButtonEntry()
                        : palette.PropEntries.FirstOrDefault(p => p != null && p.EntryId == box.propEntryId) ??
                          palette.PropEntries.FirstOrDefault(p => p != null && !string.IsNullOrEmpty(box.propEntryName) && p.DisplayName == box.propEntryName);
                    if (entry?.Prefab == null)
                        throw new InvalidDataException("缺少道具资源：" + label + "。请先同步道具栏目及预制体。");
                    if (canonicalize && box.propEntryId != PlanningDoorUtility.ButtonEntryId)
                    { box.propEntryId = entry.EntryId; box.propEntryName = entry.DisplayName; }
                }
                else if (!string.IsNullOrEmpty(box.paletteEntryId) || !string.IsNullOrEmpty(box.paletteEntryName))
                {
                    var entry = palette.Entries.FirstOrDefault(p => p != null && p.EntryId == box.paletteEntryId) ??
                                palette.Entries.FirstOrDefault(p => p != null && !string.IsNullOrEmpty(box.paletteEntryName) && p.DisplayName == box.paletteEntryName);
                    if (entry == null || entry.UsesPrefabDirectly && entry.Prefab == null)
                        throw new InvalidDataException("缺少方块资源：" + label + "。请先同步栏目及预制体。");
                    if (canonicalize) { box.paletteEntryId = entry.EntryId; box.paletteEntryName = entry.DisplayName; }
                }
                if (box.componentOverrides != null)
                    foreach (var value in box.componentOverrides)
                    {
                        if (value == null || value.ValueType != "ObjectReference" || string.IsNullOrEmpty(value.Value) || value.Value == "reference:null") continue;
                        if (!resolvedAssets.Add(value.Value)) continue;
                        if (!GlobalObjectId.TryParse(value.Value, out var assetId) || assetId.identifierType == 2)
                            throw new InvalidDataException("物体“" + label + "”的参数“" + value.PropertyPath + "”包含场景引用，不能跨存档导入。请改用项目资源或编辑器绑定。");
                        if (GlobalObjectId.GlobalObjectIdentifierToObjectSlow(assetId) == null)
                            throw new InvalidDataException("物体“" + label + "”的参数“" + value.PropertyPath + "”缺少引用资源，请先同步资源。");
                    }
                for (int i = 0; i < box.mergeParts.Count; i++)
                {
                    var part = PlanningBox.FromPersistentJson(box.mergeParts[i]);
                    ResolveBox(part);
                    if (canonicalize) box.mergeParts[i] = JsonUtility.ToJson(part);
                }
            }
        }

        private static void ValidateStructure(PlanningDocument source)
        {
            if (source == null || source.rooms == null) throw new InvalidDataException("存档没有房间数据。");
            var ids = new HashSet<string>();
            void Id(string id)
            { if (string.IsNullOrEmpty(id) || !ids.Add(id)) throw new InvalidDataException("存档包含空或重复的内部ID。"); }
            void Box(PlanningBox box, int depth)
            {
                if (box == null || box.width <= 0 || box.height <= 0 || box.mergeParts == null || depth > 32)
                    throw new InvalidDataException("存档包含损坏的物体或合并数据。");
                Id(box.id);
                foreach (var json in box.mergeParts)
                {
                    if (string.IsNullOrEmpty(json)) throw new InvalidDataException("合并物体的原始数据损坏。");
                    Box(PlanningBox.FromPersistentJson(json), depth + 1);
                }
            }
            foreach (var room in source.rooms)
            {
                if (room == null || room.cells == null || room.boxes == null || room.cells.Any(c => c == null))
                    throw new InvalidDataException("存档包含损坏的房间数据。");
                Id(room.id);
                foreach (var box in room.boxes) Box(box, 0);
            }
            foreach (var box in source.assemblyPatches ?? Enumerable.Empty<PlanningBox>()) Box(box, 0);
            foreach (var region in source.regions ?? Enumerable.Empty<PlanningRegion>()) Id(region.id);
            foreach (var door in source.doors ?? Enumerable.Empty<PlanningDoor>()) Id(door.id);
            foreach (var key in source.keys ?? Enumerable.Empty<PlanningKey>()) Id(key.id);
            foreach (var item in source.locks ?? Enumerable.Empty<PlanningLock>()) Id(item.id);
        }

        private static void ValidateObjectPlacement(PlanningDocument target, PlanningDocument source, Vector2Int shift)
        {
            var palette = LevelEditorPaletteService.GetOrCreate();
            var foreground = new HashSet<Vector2Int>();
            var water = new HashSet<Vector2Int>();
            IEnumerable<(PlanningBox box, Vector2Int origin)> WorldBoxes(PlanningDocument doc)
            {
                foreach (var room in doc.rooms)
                {
                    Vector2Int origin = PlanningWorldUtility.Bounds(room).position;
                    foreach (var box in room.boxes) yield return (box, origin);
                }
                foreach (var box in doc.assemblyPatches) yield return (box, Vector2Int.zero);
            }
            foreach (var item in WorldBoxes(target))
            {
                var layer = PlanningWaterLayerUtility.IsWater(item.box, palette) ? water : foreground;
                foreach (var cell in PlanningShapeUtility.Cells(item.box)) layer.Add(cell + item.origin);
            }
            foreach (var item in WorldBoxes(source))
            {
                var layer = PlanningWaterLayerUtility.IsWater(item.box, palette) ? water : foreground;
                foreach (var cell in PlanningShapeUtility.Cells(item.box))
                    if (layer.Contains(cell + item.origin + shift))
                        throw new InvalidDataException("导入物体与当前地图同层物体重叠，请调整导入坐标。装配物体可能超出房间边界。");
            }
        }
        private static int DetachExternalLinks(PlanningDocument doc)
        {
            int count = 0;
            var all = Boxes(doc).ToList(); var ids = new HashSet<string>(all.Select(b => b.id));
            foreach (var box in all)
            {
                if (!string.IsNullOrEmpty(box.doorOwnerId) && !ids.Contains(box.doorOwnerId))
                { foreach (var room in doc.rooms) room.boxes.Remove(box); doc.assemblyPatches.Remove(box); count++; continue; }
                if (!string.IsNullOrEmpty(box.mechanismBaseId) && !ids.Contains(box.mechanismBaseId)) { box.mechanismBaseId = null; box.mechanismBaseCellOffset = default; count++; }
                if (!string.IsNullOrEmpty(box.crankOwnerId) && !ids.Contains(box.crankOwnerId)) { box.crankOwnerId = null; count++; }
            }
            return count;
        }
    }
}
