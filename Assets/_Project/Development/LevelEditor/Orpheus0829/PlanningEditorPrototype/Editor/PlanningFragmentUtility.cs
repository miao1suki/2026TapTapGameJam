using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
                    if (!area.Value.Overlaps(world)) { room.boxes.Remove(box); continue; }
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
                if (area.HasValue ? !area.Value.Overlaps(rect) : !Cells(rect).Any(keptCells.Contains))
                    result.assemblyPatches.Remove(patch);
                else if (area.HasValue && !Contains(area.Value, rect))
                    ClipBox(result, patch, rect, area.Value);
                else if (selected != null && Cells(rect).Any(cell => !keptCells.Contains(cell)))
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
            ValidatePlacement(target, source, anchor);
            var incoming = PlanningDocument.FromPersistentJson(source.ToPersistentJson());
            var result = PlanningDocument.FromPersistentJson(target.ToPersistentJson());
            var origin = Bounds(incoming).position;
            var delta = anchor - origin;
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
            Vector2Int shift = anchor - Bounds(source).position;
            var occupied = new HashSet<Vector2Int>(target.rooms.SelectMany(r => r.cells).Select(c => new Vector2Int(c.x, c.y)));
            foreach (var cell in source.rooms.SelectMany(r => r.cells))
                if (!occupied.Add(new Vector2Int(cell.x, cell.y) + shift))
                    throw new InvalidDataException("导入位置与现有房间或通道重叠，请调整导入坐标。");
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
