using System.Collections.Generic;
using UnityEngine;

namespace PlanningEditorPrototype
{
    // World-cell ownership is authoritative. Connectivity names are derived data.
    internal static class PlanningWorldUtility
    {
        internal static RectInt Bounds(PlanningRoom room)
        {
            if (room.cells.Count == 0) return new RectInt(0, 0, 1, 1);
            int x = int.MaxValue, y = int.MaxValue, right = int.MinValue, bottom = int.MinValue;
            foreach (var cell in room.cells)
            { x = Mathf.Min(x, cell.x); y = Mathf.Min(y, cell.y); right = Mathf.Max(right, cell.x + 1); bottom = Mathf.Max(bottom, cell.y + 1); }
            return new RectInt(x, y, right - x, bottom - y);
        }

        internal static PlanningRoom OwnerAt(PlanningDocument doc, Vector2Int cell) =>
            doc.FindRoomAt(cell.x, cell.y) ?? doc.FindConnectorAt(cell.x, cell.y);

        internal static bool Contains(PlanningRoom room, int x, int y) => room.cells.Exists(c => c.x == x && c.y == y);

        internal static void Reanchor(PlanningDocument doc, PlanningRoom room, Vector2Int oldOrigin)
        {
            Vector2Int origin = Bounds(room).position;
            Vector2Int delta = new Vector2Int((oldOrigin.x - origin.x) * doc.worldBlockCellWidth,
                (oldOrigin.y - origin.y) * doc.worldBlockCellHeight);
            foreach (var box in room.boxes) { box.x += delta.x; box.y += delta.y; }
            if (doc.playerStartRoomId == room.id) doc.playerStartLocal += (Vector2)delta;
        }

        internal static bool TryMove(PlanningDocument doc, HashSet<string> owners, HashSet<string> regions,
            Vector2Int delta)
        {
            if (delta == Vector2Int.zero) return true;
            var occupied = new HashSet<Vector2Int>();
            foreach (var room in doc.rooms)
                if (!owners.Contains(room.id)) foreach (var cell in room.cells) occupied.Add(new Vector2Int(cell.x, cell.y));
            foreach (var room in doc.rooms)
                if (owners.Contains(room.id)) foreach (var cell in room.cells)
                    if (!occupied.Add(new Vector2Int(cell.x, cell.y) + delta)) return false;
            foreach (var room in doc.rooms)
                if (owners.Contains(room.id)) foreach (var cell in room.cells) { cell.x += delta.x; cell.y += delta.y; }
            foreach (var door in doc.doors) if (owners.Contains(door.roomId)) { door.x += delta.x; door.y += delta.y; }
            foreach (var key in doc.keys) if (owners.Contains(key.roomId)) { key.x += delta.x; key.y += delta.y; }
            foreach (var region in doc.regions) if (regions.Contains(region.id)) { region.x += delta.x; region.y += delta.y; }
            RefreshConnectors(doc);
            return true;
        }

        internal static List<string> DeleteOwners(PlanningDocument doc, HashSet<string> owners)
        {
            var boxes = new List<string>();
            foreach (var room in doc.rooms) if (owners.Contains(room.id)) foreach (var box in room.boxes) boxes.Add(box.id);
            doc.rooms.RemoveAll(room => owners.Contains(room.id));
            doc.doors.RemoveAll(door => owners.Contains(door.roomId));
            var removedKeys = new HashSet<string>();
            foreach (var key in doc.keys) if (owners.Contains(key.roomId)) removedKeys.Add(key.id);
            doc.keys.RemoveAll(key => removedKeys.Contains(key.id));
            foreach (var item in doc.locks) item.keyIds.RemoveAll(id => removedKeys.Contains(id));
            if (owners.Contains(doc.playerStartRoomId)) doc.playerStartRoomId = null;
            RefreshConnectors(doc);
            return boxes;
        }

        internal static bool TryAssemblyOwner(PlanningDocument doc, Vector2Int cell, out PlanningRoom owner, out Vector2Int local)
        {
            int sx = Mathf.Max(1, doc.worldBlockCellWidth), sy = Mathf.Max(1, doc.worldBlockCellHeight);
            var world = new Vector2Int(Mathf.FloorToInt((float)cell.x / sx), Mathf.FloorToInt((float)cell.y / sy));
            owner = OwnerAt(doc, world);
            local = owner == null ? default : cell - new Vector2Int(Bounds(owner).x * sx, Bounds(owner).y * sy);
            return owner != null;
        }

        internal static void RefreshConnectors(PlanningDocument doc)
        {
            var occupied = new HashSet<Vector2Int>();
            foreach (var room in doc.rooms) if (!room.isConnector) foreach (var cell in room.cells) occupied.Add(new Vector2Int(cell.x, cell.y));
            var names = new Dictionary<string, int>();
            foreach (var connector in doc.rooms)
            {
                if (!connector.isConnector) continue;
                if (!connector.independentCells)
                {
                    // Migrate legacy paths once, retaining explicit contents rather than rebuilding them.
                    var path = PlanningLayoutUtility.GetConnectorAssemblyPath(doc.rooms, connector, doc.worldBlockCellWidth, doc.worldBlockCellHeight);
                    RectInt oldBounds = Bounds(connector);
                    Vector2Int oldAnchor = path.Count > 0 ? path[0] : new Vector2Int(oldBounds.x * doc.worldBlockCellWidth, oldBounds.y * doc.worldBlockCellHeight);
                    if (connector.cells.Count == 0)
                    {
                        var a = doc.FindRoom(connector.fromRoomId); var b = doc.FindRoom(connector.toRoomId);
                        if (a != null && b != null)
                        {
                            var p = Bounds(a).position; var end = Bounds(b).position;
                            connector.cells.Add(new PlanningCell(p.x, p.y));
                            while (p.x != end.x) { p.x += p.x < end.x ? 1 : -1; connector.cells.Add(new PlanningCell(p.x, p.y)); }
                            while (p.y != end.y) { p.y += p.y < end.y ? 1 : -1; connector.cells.Add(new PlanningCell(p.x, p.y)); }
                        }
                    }
                    connector.cells.RemoveAll(c => occupied.Contains(new Vector2Int(c.x, c.y)));
                    connector.independentCells = true;
                    var bounds = Bounds(connector);
                    var newAnchor = new Vector2Int(bounds.x * doc.worldBlockCellWidth, bounds.y * doc.worldBlockCellHeight);
                    foreach (var box in connector.boxes) { box.x += oldAnchor.x - newAnchor.x; box.y += oldAnchor.y - newAnchor.y; }
                }
                var unique = new HashSet<Vector2Int>();
                connector.cells.RemoveAll(c => !unique.Add(new Vector2Int(c.x, c.y)));
                foreach (var cell in connector.cells) occupied.Add(new Vector2Int(cell.x, cell.y));
                connector.connectedRoomIds ??= new List<string>();
                connector.connectedRoomIds.Clear();
                var labels = new List<string>();
                foreach (var room in doc.rooms)
                {
                    if (room.isConnector) continue;
                    bool touches = false;
                    foreach (var cell in connector.cells)
                        if (Contains(room, cell.x - 1, cell.y) || Contains(room, cell.x + 1, cell.y) ||
                            Contains(room, cell.x, cell.y - 1) || Contains(room, cell.x, cell.y + 1)) { touches = true; break; }
                    if (!touches) continue;
                    connector.connectedRoomIds.Add(room.id); labels.Add(room.name);
                }
                connector.fromRoomId = labels.Count > 0 ? connector.connectedRoomIds[0] : null;
                connector.toRoomId = labels.Count > 1 ? connector.connectedRoomIds[1] : null;
                string route = labels.Count == 0 ? "未连接" : labels.Count == 1 ? "未连接 → " + labels[0] : string.Join(" ↔ ", labels);
                names.TryGetValue(route, out int n); names[route] = ++n;
                connector.name = "通道：" + route + (n > 1 ? $"（{n}）" : "");
                RectInt extent = Bounds(connector);
                connector.detailBoundsX = 0; connector.detailBoundsY = 0;
                connector.detailBoundsWidth = extent.width * doc.worldBlockCellWidth;
                connector.detailBoundsHeight = extent.height * doc.worldBlockCellHeight;
            }
        }
    }
}
