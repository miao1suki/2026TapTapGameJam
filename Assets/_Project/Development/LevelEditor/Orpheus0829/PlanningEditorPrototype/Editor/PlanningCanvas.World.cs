using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace PlanningEditorPrototype
{
    public sealed partial class PlanningCanvas
    {
        private readonly HashSet<string> worldSelection = new HashSet<string>();
        private readonly HashSet<string> regionSelection = new HashSet<string>();
        private readonly Dictionary<string, Label> regionLabels = new Dictionary<string, Label>();
        private string selectedRegionId;
        private bool worldMoving, worldAdditive;
        private Vector2Int worldMoveDelta;
        public event Action<IReadOnlyList<string>> WorldItemsRemoved;
        public event Action<IReadOnlyList<PlanningRoom>> WorldOwnersRemoving;
        public IReadOnlyCollection<string> SelectedWorldIds => worldSelection;
        public bool IsWorldSelected(string id) => worldSelection.Contains(id) || (mode != PlanningCanvasMode.World && selectedRoomId == id);
        public bool IsRegionSelected(string id) => regionSelection.Contains(id);
        public PlanningRegion SelectedRegion => document?.regions.Find(r => r.id == selectedRegionId);
        public string RegionAnnotation { get; set; } = "新区域";
        public Color RegionColor { get; set; } = new Color(.45f, .6f, .8f);
        public bool AutomaticRegionColor { get; set; } = true;

        public void BeginConnectorPlacement()
        {
            SelectRoom(null);
            SetMapTool(PlanningMapTool.Connector);
            StatusChanged?.Invoke("单击或拖拽框选空白格创建通道；选中通道后可继续扩展。");
        }

        public void SelectRegion(string id, bool focus = false, bool additive = false)
        {
            if (!additive) { worldSelection.Clear(); regionSelection.Clear(); }
            bool removed = id != null && !regionSelection.Add(id) && additive;
            if (removed) regionSelection.Remove(id);
            selectedRegionId = removed ? null : id; selectedRoomId = null; selectedBoxId = null;
            if (removed) foreach (string value in regionSelection) { selectedRegionId = value; break; }
            if (focus && SelectedRegion != null)
                CenterOn(SelectedRegion.Bounds.center.x, SelectedRegion.Bounds.center.y);
            MarkDirtyRepaint(); SelectionChanged?.Invoke();
        }

        public void UpdateSelectedRegion(string annotation, Color color)
        {
            var region = SelectedRegion;
            if (region == null) return;
            region.annotation = annotation; region.colorHex = "#" + ColorUtility.ToHtmlStringRGB(color);
            NotifyDocumentChanged();
        }

        public void DeleteWorldSelection()
        {
            if (document == null || (worldSelection.Count == 0 && regionSelection.Count == 0)) return;
            if (!EditorUtility.DisplayDialog("删除选中区域", $"删除 {worldSelection.Count} 个房间/通道和 {regionSelection.Count} 个规划区域？\n房间/通道内的规划物体及其场景实例将一并删除。", "删除", "取消")) return;
            WorldOwnersRemoving?.Invoke(document.rooms.FindAll(r => worldSelection.Contains(r.id)));
            var removed = PlanningWorldUtility.DeleteOwners(document, worldSelection);
            document.regions.RemoveAll(r => regionSelection.Contains(r.id));
            worldSelection.Clear(); regionSelection.Clear(); selectedRoomId = null; selectedRegionId = null; selectedBoxId = null;
            WorldItemsRemoved?.Invoke(removed);
            NotifyDocumentChanged(); SelectionChanged?.Invoke();
        }

        private void HandleWorldPointerDown(Vector2 position, bool additive)
        {
            worldAdditive = additive;
            worldMoving = false; worldMoveDelta = Vector2Int.zero;
            Vector2Int cell = CellAt(position);
            if (mapTool == PlanningMapTool.Select)
            {
                var hit = PlanningWorldUtility.OwnerAt(document, cell);
                // Background regions must not consume blank-space room marquee gestures.
                var region = document.regions.FindLast(r => r.Bounds.Contains(cell) && regionSelection.Contains(r.id));
                if (!additive && SelectedRegion != null && SelectedRegion.Bounds.Contains(cell) && regionSelection.Contains(selectedRegionId))
                { worldMoving = true; return; }
                if (hit != null)
                {
                    if (additive || !worldSelection.Contains(hit.id)) SelectRoom(hit.id, false, additive);
                    selectedRegionId = null;
                    worldMoving = worldSelection.Contains(hit.id);
                }
                else if (region != null)
                {
                    if (additive || !regionSelection.Contains(region.id)) SelectRegion(region.id, false, additive);
                    worldMoving = regionSelection.Contains(region.id);
                }
                else if (!additive)
                {
                    worldSelection.Clear(); regionSelection.Clear(); selectedRoomId = selectedRegionId = null;
                    SelectionChanged?.Invoke();
                }
            }
            else if (mapTool == PlanningMapTool.Paint || mapTool == PlanningMapTool.Connector)
            {
                // Commit once on release so a rectangle is one history/sync transaction.
                lastPaintCell = cell;
            }
            else if (mapTool == PlanningMapTool.Door) AddDoor(cell);
            else if (mapTool == PlanningMapTool.Key) AddKey(cell);
        }

        private void HandleWorldPointerMove(Vector2 position)
        {
            Vector2Int cell = CellAt(position);
            if (mapTool == PlanningMapTool.Select && worldMoving)
            {
                worldMoveDelta = cell - CellAt(dragStart);
                return;
            }
            // Paint/connector/erase display a marquee; dragging does not mutate the document.
        }

        private void CompleteWorldDrag()
        {
            Vector2Int start = CellAt(dragStart), end = CellAt(dragCurrent);
            var area = new RectInt(Mathf.Min(start.x, end.x), Mathf.Min(start.y, end.y), Mathf.Abs(start.x - end.x) + 1, Mathf.Abs(start.y - end.y) + 1);
            if (mapTool == PlanningMapTool.Paint || mapTool == PlanningMapTool.Connector)
            {
                PaintWorldArea(area, mapTool == PlanningMapTool.Connector);
                return;
            }
            if (mapTool == PlanningMapTool.Select)
            {
                if (worldMoving)
                {
                    Vector2Int delta = end - start;
                    if (delta != Vector2Int.zero)
                    {
                        if (PlanningWorldUtility.TryMove(document, worldSelection, regionSelection, delta)) NotifyDocumentChanged();
                        else StatusChanged?.Invoke("无法移动：目标位置与未选中的房间或通道重叠。");
                    }
                }
                else if (start != end)
                {
                    if (!worldAdditive) { worldSelection.Clear(); regionSelection.Clear(); }
                    bool hitOwner = false;
                    foreach (var room in document.rooms)
                        if (room.cells.Exists(c => area.Contains(new Vector2Int(c.x, c.y)))) { worldSelection.Add(room.id); hitOwner = true; }
                    if (!hitOwner) foreach (var region in document.regions) if (area.Overlaps(region.Bounds)) regionSelection.Add(region.id);
                    selectedRoomId = null; selectedRegionId = null;
                    foreach (string id in worldSelection) { selectedRoomId = id; break; }
                    if (selectedRoomId == null) foreach (string id in regionSelection) { selectedRegionId = id; break; }
                    SelectionChanged?.Invoke();
                }
                return;
            }
            if (mapTool == PlanningMapTool.Region)
            {
                var color = AutomaticRegionColor ? Color.HSVToRGB((document.regions.Count * .173f + .56f) % 1f, .45f, .85f) : RegionColor;
                var region = new PlanningRegion { x = area.x, y = area.y, width = area.width, height = area.height,
                    annotation = RegionAnnotation, colorHex = "#" + ColorUtility.ToHtmlStringRGB(color) };
                document.regions.Add(region); SelectRegion(region.id);
                NotifyDocumentChanged(); StatusChanged?.Invoke("已创建区域规划。"); return;
            }
            if (mapTool == PlanningMapTool.Erase) { EraseWorldArea(area); return; }
            if (mapTool == PlanningMapTool.Rectangle)
            {
                PaintWorldArea(area, false);
            }
        }

        private void PaintWorldArea(RectInt area, bool connector)
        {
            var owner = isPlacingRoom ? null : document.FindRoom(selectedRoomId);
            if (owner != null && owner.isConnector != connector) owner = null;
            // A click/drag beginning on an existing matching owner extends that owner.
            var first = PlanningWorldUtility.OwnerAt(document, CellAt(dragStart));
            if (!isPlacingRoom && owner == null && first != null && first.isConnector == connector) owner = first;
            var blocked = new HashSet<Vector2Int>();
            foreach (var room in document.rooms)
                if (room != owner) foreach (var cell in room.cells) blocked.Add(new Vector2Int(cell.x, cell.y));
            var cells = owner != null ? CloneCells(owner.cells) : new List<PlanningCell>();
            var existing = new HashSet<Vector2Int>();
            foreach (var cell in cells) existing.Add(new Vector2Int(cell.x, cell.y));
            int before = cells.Count;
            for (int y = area.y; y < area.yMax; y++) for (int x = area.x; x < area.xMax; x++)
            {
                var p = new Vector2Int(x, y);
                if (!blocked.Contains(p) && existing.Add(p)) cells.Add(new PlanningCell(x, y));
            }
            if (cells.Count == before) return;
            if (!(owner?.allowDisconnectedCells ?? false) && !AreCellsConnected(cells))
            {
                StatusChanged?.Invoke("未创建：区域需与选中房间或通道连通，且不能覆盖其他区域。");
                return;
            }
            if (owner == null)
            {
                owner = new PlanningRoom(connector ? "未连接通道" : NextRoomName())
                    { isConnector = connector, independentCells = connector };
                document.rooms.Add(owner);
            }
            Vector2Int oldOrigin = PlanningWorldUtility.Bounds(owner).position;
            bool hadCells = owner.cells.Count > 0;
            owner.cells = cells;
            if (hadCells) PlanningWorldUtility.Reanchor(document, owner, oldOrigin);
            isPlacingRoom = false;
            SelectRoom(owner.id);
            NotifyDocumentChanged();
        }

        private void PaintWorldCell(Vector2Int cell, bool connector)
        {
            var occupied = PlanningWorldUtility.OwnerAt(document, cell);
            var owner = document.FindRoom(selectedRoomId);
            if (owner != null && owner.isConnector != connector) owner = null;
            if (occupied != null)
            {
                if (owner == null && occupied.isConnector == connector) SelectRoom(occupied.id);
                return;
            }
            bool created = owner == null;
            if (created)
            {
                owner = new PlanningRoom(connector ? "未连接通道" : NextRoomName()) { isConnector = connector, independentCells = connector };
                document.rooms.Add(owner); selectedRoomId = owner.id;
            }
            Vector2Int oldOrigin = PlanningWorldUtility.Bounds(owner).position;
            owner.cells.Add(new PlanningCell(cell.x, cell.y));
            if (!owner.allowDisconnectedCells && !AreCellsConnected(owner.cells))
            {
                owner.cells.RemoveAt(owner.cells.Count - 1);
                if (created) document.rooms.Remove(owner);
                StatusChanged?.Invoke("请从选中区域相邻的空白格继续绘制，或先创建新区域。"); return;
            }
            if (!created) PlanningWorldUtility.Reanchor(document, owner, oldOrigin);
            worldSelection.Clear(); regionSelection.Clear(); worldSelection.Add(owner.id); selectedRegionId = null;
            NotifyDocumentChanged();
        }

        private string NextRoomName()
        {
            int number = 1;
            while (document.rooms.Exists(r => !r.isConnector && r.name == "房间 " + number)) number++;
            return "房间 " + number;
        }

        private void EraseWorldArea(RectInt area)
        {
            var delete = new HashSet<string>(); bool changed = false;
            var rooms = new List<PlanningRoom>(document.rooms);
            foreach (var owner in rooms)
            {
                var before = CloneCells(owner.cells);
                var origin = PlanningWorldUtility.Bounds(owner).position;
                if (owner.cells.RemoveAll(c => area.Contains(new Vector2Int(c.x, c.y))) == 0) continue;
                if (owner.cells.Count == 0) { delete.Add(owner.id); changed = true; continue; }
                if (!owner.allowDisconnectedCells && !AreCellsConnected(owner.cells))
                {
                    owner.cells = before;
                    StatusChanged?.Invoke("擦除未执行：剩余区块需要保持连通。"); continue;
                }
                // Remove content in erased world cells, leaving other owners untouched.
                foreach (var old in before)
                {
                    if (!area.Contains(new Vector2Int(old.x, old.y))) continue;
                    var erased = new RectInt((old.x - origin.x) * WorldBlockCellWidth, (old.y - origin.y) * WorldBlockCellHeight, WorldBlockCellWidth, WorldBlockCellHeight);
                    var boxes = new List<PlanningBox>(owner.boxes);
                    foreach (var box in boxes)
                    {
                        if (!new RectInt(box.x, box.y, box.width, box.height).Overlaps(erased)) continue;
                        for (int x = erased.x; x < erased.xMax; x++) for (int y = erased.y; y < erased.yMax; y++)
                        {
                            var current = FindBoxAt(owner, x, y);
                            if (current != null) CarveBox(owner.boxes, current, new Vector2Int(x, y));
                        }
                    }
                }
                PlanningWorldUtility.Reanchor(document, owner, origin);
                changed = true;
            }
            if (delete.Count > 0)
            {
                WorldOwnersRemoving?.Invoke(document.rooms.FindAll(r => delete.Contains(r.id)));
                var removed = PlanningWorldUtility.DeleteOwners(document, delete);
                WorldItemsRemoved?.Invoke(removed);
                worldSelection.ExceptWith(delete);
                if (delete.Contains(selectedRoomId)) selectedRoomId = null;
            }
            if (changed) { NotifyDocumentChanged(); SelectionChanged?.Invoke(); }
        }

        private bool TryFindAssemblyOwner(Vector2Int cell, int strideX, int strideY, out PlanningRoom owner, out Vector2Int local) =>
            PlanningWorldUtility.TryAssemblyOwner(document, cell, out owner, out local);

        private bool CanPlaceLocalRect(PlanningRoom owner, RectInt rect)
        {
            Vector2Int origin = PlanningWorldUtility.Bounds(owner).position;
            for (int x = rect.x; x < rect.xMax; x++) for (int y = rect.y; y < rect.yMax; y++)
                if (!PlanningWorldUtility.Contains(owner, origin.x + Mathf.FloorToInt((float)x / WorldBlockCellWidth),
                    origin.y + Mathf.FloorToInt((float)y / WorldBlockCellHeight))) return false;
            return true;
        }

        private void DrawConnectors(Painter2D painter)
        {
            foreach (var room in document.rooms)
            {
                if (!room.isConnector) continue;
                bool selected = worldSelection.Contains(room.id) || room.id == selectedRoomId;
                DrawWorldOwner(painter, room, new Color(.2f, .8f, .78f, selected ? .8f : .55f), selected);
            }
        }

        private void DrawWorldOwner(Painter2D painter, PlanningRoom room, Color color, bool selected)
        {
            // At overview zoom, draw contiguous row spans instead of thousands of tiny outlines.
            var cells = new HashSet<Vector2Int>();
            foreach (var cell in room.cells) cells.Add(new Vector2Int(cell.x, cell.y));
            foreach (var cell in cells)
            {
                if (cells.Contains(cell + Vector2Int.left)) continue;
                int end = cell.x + 1;
                while (cells.Contains(new Vector2Int(end, cell.y))) end++;
                Rect span = CellRect(cell.x, cell.y, end - cell.x, 1);
                if (span.xMax < 0 || span.yMax < 0 || span.xMin > resolvedStyle.width || span.yMin > resolvedStyle.height) continue;
                FillRect(painter, span, color, 0f);
                if (GridSize < 10f) continue;
                int min = Mathf.Max(cell.x, CellAt(Vector2.zero).x);
                int max = Mathf.Min(end, CellAt(new Vector2(resolvedStyle.width, resolvedStyle.height)).x + 1);
                for (int x = min; x < max; x++)
                    StrokeRect(painter, CellRect(x, cell.y), selected ? Color.white : new Color(0f, 0f, 0f, .45f), 1f);
            }
        }

        private void DrawWorldRegions(Painter2D painter)
        {
            foreach (var region in document.regions)
            {
                Rect rect = CellRect(region.x, region.y, region.width, region.height);
                Color color = ParseColor(region.colorHex, RegionColor); color.a = .22f;
                FillRect(painter, rect, color, 0f);
                color.a = regionSelection.Contains(region.id) ? 1f : .6f;
                StrokeRect(painter, rect, color, regionSelection.Contains(region.id) ? 2f : 1f);
            }
        }

        private void DrawAssemblyOwnership(Painter2D painter)
        {
            foreach (var room in document.rooms)
            {
                Color color = room.isConnector ? new Color(.12f, .7f, .7f, .12f) : new Color(.3f, .5f, .8f, .08f);
                foreach (var cell in room.cells)
                {
                    Rect rect = CellRect(cell.x * WorldBlockCellWidth, cell.y * WorldBlockCellHeight, WorldBlockCellWidth, WorldBlockCellHeight);
                    FillRect(painter, rect, color, 0f);
                    StrokeRect(painter, rect, new Color(color.r, color.g, color.b, .4f), 1f);
                }
            }
        }

        private void DrawDetailOwnership(Painter2D painter, PlanningRoom room)
        {
            Vector2Int origin = PlanningWorldUtility.Bounds(room).position;
            var color = room.isConnector ? new Color(.2f, .8f, .78f, .12f) : new Color(.3f, .55f, .85f, .08f);
            foreach (var cell in room.cells)
            {
                Rect rect = CellRect((cell.x - origin.x) * WorldBlockCellWidth, (cell.y - origin.y) * WorldBlockCellHeight, WorldBlockCellWidth, WorldBlockCellHeight);
                FillRect(painter, rect, color, 0f);
                StrokeRect(painter, rect, new Color(color.r, color.g, color.b, .55f), 1f);
            }
        }

        private void DrawWorldMovePreview(Painter2D painter)
        {
            if (!worldMoving || !isDragging || worldMoveDelta == Vector2Int.zero) return;
            foreach (var room in document.rooms) if (worldSelection.Contains(room.id)) foreach (var cell in room.cells)
                StrokeRect(painter, CellRect(cell.x + worldMoveDelta.x, cell.y + worldMoveDelta.y), new Color(1f, .8f, .3f), 2f);
            foreach (var region in document.regions) if (regionSelection.Contains(region.id))
                StrokeRect(painter, CellRect(region.x + worldMoveDelta.x, region.y + worldMoveDelta.y, region.width, region.height), Color.white, 2f);
        }

        private void RefreshRegionLabels()
        {
            if (document == null) return;
            var live = new HashSet<string>();
            foreach (var region in document.regions)
            {
                live.Add(region.id);
                if (!regionLabels.TryGetValue(region.id, out var label))
                {
                    label = new Label { pickingMode = PickingMode.Ignore };
                    label.style.position = Position.Absolute; label.style.fontSize = 12;
                    label.style.color = Color.white; label.style.backgroundColor = new Color(.08f, .1f, .12f, .8f);
                    label.style.paddingLeft = label.style.paddingRight = 5;
                    regionLabels.Add(region.id, label); Add(label);
                }
                Rect rect = CellRect(region.x, region.y, region.width, region.height);
                label.text = region.annotation;
                label.style.display = mode == PlanningCanvasMode.World ? DisplayStyle.Flex : DisplayStyle.None;
                label.style.left = rect.x; label.style.top = rect.y - 22;
                label.style.maxWidth = Mathf.Max(60, rect.width); label.style.overflow = Overflow.Hidden;
            }
            var stale = new List<string>();
            foreach (var pair in regionLabels) if (!live.Contains(pair.Key)) stale.Add(pair.Key);
            foreach (string id in stale) { regionLabels[id].RemoveFromHierarchy(); regionLabels.Remove(id); }
        }

        [MenuItem("Tools/2026TapTap/关卡编辑器/验证世界图与装配编辑")]
        private static void ValidateWorldAndAssemblyEditing()
        {
            // In-memory only: no scene generation, asset writes, preferences or confirmation dialogs.
            var doc = new PlanningDocument();
            var room = new PlanningRoom("房间 1"); room.cells.Add(new PlanningCell(3, 2));
            var other = new PlanningRoom("房间 2"); other.cells.Add(new PlanningCell(6, 2));
            var connector = new PlanningRoom("通道") { isConnector = true, independentCells = true };
            connector.cells.Add(new PlanningCell(4, 2));
            doc.rooms.Add(room); doc.rooms.Add(other); doc.rooms.Add(connector);
            var canvas = new PlanningCanvas(); canvas.SetDocument(doc); canvas.SetMode(PlanningCanvasMode.World);
            int changes = 0; canvas.DocumentChanged += () => changes++;
            canvas.SelectRoom(connector.id);
            canvas.BeginDocumentBatch();
            canvas.PaintWorldCell(new Vector2Int(5, 2), true);
            canvas.PaintWorldCell(new Vector2Int(6, 2), true);
            canvas.EndDocumentBatch();
            Require(changes == 1 && connector.cells.Count == 2, "单笔事务与不覆盖房间");
            Require(connector.connectedRoomIds.Count == 2, "绘制后更新两端连接");
            canvas.SelectRoom(room.id); canvas.SelectRoom(connector.id, false, true);
            Require(canvas.worldSelection.Count == 2, "房间通道多选");
            canvas.SelectRoom(connector.id, false, true);
            Require(canvas.worldSelection.Count == 1 && canvas.selectedRoomId == room.id, "取消选中");
            var tool = Project.LevelEditor.Editor.LevelEditorState.Tool;
            try
            {
                // An earlier world Pan selection must not swallow assembly Paint/Erase input.
                canvas.SetMapTool(PlanningMapTool.Pan); canvas.SetMode(PlanningCanvasMode.Assembly);
                Project.LevelEditor.Editor.LevelEditorState.Tool = Project.LevelEditor.Editor.LevelEditorTool.Paint;
                canvas.AddAssemblyBox(new Vector2Int(4, 2));
                Require(connector.boxes.Count == 1 && connector.boxes[0].x == 0, "空通道装配绘制");
                canvas.AddAssemblyBox(new Vector2Int(4, 2));
                Require(connector.boxes.Count == 1, "重复绘制不产生重叠副本");
                canvas.EraseAssemblyBox(new Vector2Int(4, 2));
                Require(connector.boxes.Count == 0, "通道装配擦除");
                canvas.AddAssemblyBox(new Vector2Int(3, 2));
                Require(room.boxes.Count == 1 && room.boxes[0].x == 0, "空房间装配绘制");
            }
            finally { Project.LevelEditor.Editor.LevelEditorState.Tool = tool; }
            doc.regions.Add(new PlanningRegion { annotation = "探索区", x = 2, y = 1, width = 6, height = 4 });
            var loaded = JsonUtility.FromJson<PlanningDocument>(JsonUtility.ToJson(doc)); loaded.Normalize();
            Require(loaded.regions.Count == 1 && loaded.regions[0].annotation == "探索区", "区域存档往返");
            Require(loaded.FindRoom(connector.id).independentCells, "通道存档保持独立占格");
            canvas.SetMode(PlanningCanvasMode.World);
            var deleted = new List<string>(); canvas.WorldItemsRemoved += ids => deleted.AddRange(ids);
            string boxId = room.boxes[0].id;
            canvas.EraseWorldArea(new RectInt(3, 2, 1, 1));
            Require(doc.FindRoom(room.id) == null && deleted.Contains(boxId), "最后占格删除所属内容");
            Require(doc.FindRoom(connector.id) != null && doc.FindRoom(other.id) != null, "删除不连带移除相邻通道和房间");
            Require(connector.connectedRoomIds.Count == 1 && connector.name.Contains("未连接"), "删除刷新一端连接名");
            Debug.Log("世界图与装配编辑验证通过：占格、事务、多选、绘制/擦除、存档与删除所有权。");
        }

        [MenuItem("Tools/2026TapTap/关卡编辑器/验证统一网格与旧存档")]
        public static void VerifyUnifiedGrid()
        {
            int checks = 0;
            void Check(bool value, string message) { Require(value, message); checks++; }
            void CheckMigration(string json)
            {
                var old = JsonUtility.FromJson<PlanningDocument>(json);
                old.RefreshConnectorPaths();
                int sx = old.worldBlockCellWidth, sy = old.worldBlockCellHeight;
                var expected = new Dictionary<string, RectInt>();
                foreach (var room in old.rooms) foreach (var box in room.boxes)
                    expected.Add(box.id, PlanningLayoutUtility.GetAssemblyBoxRect(room, box, sx, sy));
                var upgraded = PlanningDocument.FromPersistentJson(json);
                Check(upgraded.worldBlockCellWidth == 1 && upgraded.worldBlockCellHeight == 1, "统一步长");
                Check(upgraded.rooms.Count == old.rooms.Count, "房间数不变");
                foreach (var room in old.rooms)
                {
                    var newRoom = upgraded.FindRoom(room.id);
                    var before = PlanningWorldUtility.Bounds(room); var after = PlanningWorldUtility.Bounds(newRoom);
                    Check(room.cells.Count == 0 ? newRoom.cells.Count == 0 :
                        after == new RectInt(before.x * sx, before.y * sy, before.width * sx, before.height * sy),
                        "边界位置和尺寸：" + room.name + " before=" + before + " stride=" + sx + "/" + sy + " after=" + after);
                    foreach (var box in newRoom.boxes)
                        Check(PlanningLayoutUtility.GetAssemblyBoxRect(newRoom, box, 1, 1) == expected[box.id], "内部物体装配坐标");
                }
                Check(upgraded.playerStartRoomId == old.playerStartRoomId && upgraded.playerStartLocal == old.playerStartLocal, "出生点所属和局部坐标");
                string once = upgraded.ToPersistentJson();
                Check(PlanningDocument.FromPersistentJson(once).ToPersistentJson() == once, "多次读取不重复展开");
            }
            var legacy = new PlanningDocument { worldBlockCellWidth = 16, worldBlockCellHeight = 8 };
            var room = new PlanningRoom("测试房间"); room.cells.Add(new PlanningCell(-2, -1)); room.cells.Add(new PlanningCell(-1, -1));
            room.boxes.Add(new PlanningBox { id = "box_keep", x = 3, y = 2, width = 4, height = 2 });
            legacy.rooms.Add(room); legacy.playerStartRoomId = room.id; legacy.playerStartLocal = new Vector2(4.5f, 2f);
            legacy.regions.Add(new PlanningRegion { x = -3, y = -2, width = 4, height = 3 });
            legacy.doors.Add(new PlanningDoor(room.id, -2, -1, 0));
            legacy.keys.Add(new PlanningKey("测试钥匙", room.id, -1, -1));
            legacy.assemblyPatches.Add(new PlanningBox { id = "patch_keep", x = 45, y = -12, width = 3, height = 2 });
            string original = JsonUtility.ToJson(legacy);
            CheckMigration(original);
            var migrated = PlanningDocument.FromPersistentJson(original);
            Check(migrated.FindRoom(room.id).cells.Count == 256, "非正方形步长与负坐标展开");
            Check(migrated.regions[0].Bounds == new RectInt(-48, -16, 64, 24), "区域规划转换");
            Check(migrated.doors[0].x == -32 && migrated.doors[0].y == -8 && migrated.doors[0].id == legacy.doors[0].id, "世界门标记转换和 ID");
            Check(migrated.keys[0].x == -16 && migrated.keys[0].y == -8 && migrated.keys[0].id == legacy.keys[0].id, "世界钥匙标记转换和 ID");
            Check(migrated.assemblyPatches[0].x == 45 && migrated.assemblyPatches[0].y == -12 && migrated.assemblyPatches[0].width == 3, "全局补丁不缩放");
            string noStride = System.Text.RegularExpressions.Regex.Replace(original, "\"worldBlockCell(?:Width|Height)\":\\d+,", "");
            Check(PlanningDocument.FromPersistentJson(noStride).FindRoom(room.id).cells.Count == 512, "无步长字段的旧格式");
            string defaultPath = "Assets/_Project/Development/LevelEditor/Orpheus0829/PlanningEditorPrototype/Editor/DefaultPlanningMap.json";
            CheckMigration(System.IO.File.ReadAllText(defaultPath));
            if (System.IO.Directory.Exists(PlanningSaveStore.SaveFolder))
                foreach (string path in System.IO.Directory.GetFiles(PlanningSaveStore.SaveFolder, "*.json"))
                {
                    string saved = System.IO.File.ReadAllText(path);
                    CheckMigration(saved);
                    PlanningSaveStore.Read(path);
                    Check(System.IO.File.ReadAllText(path) == saved, "读取不改写原存档");
                }
            string temporary = System.IO.Path.Combine("Library", "UnifiedGridChecks", Guid.NewGuid().ToString("N") + ".json");
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(temporary));
            System.IO.File.WriteAllText(temporary, original);
            var loaded = PlanningSaveStore.Read(temporary);
            Check(System.IO.File.ReadAllText(temporary) == original, "旧文件读取只改内存");
            PlanningSaveStore.Save(temporary, loaded);
            string backup = temporary + ".before-unified-grid.bak";
            Check(System.IO.File.ReadAllText(backup) == original && PlanningSaveStore.Read(temporary).worldBlockCellWidth == 1, "旧文件覆盖前备份");
            PlanningSaveStore.Save(temporary, loaded);
            Check(System.IO.File.ReadAllText(backup) == original, "重复保存不覆盖备份");

            var doc = new PlanningDocument(); var canvas = new PlanningCanvas(); canvas.SetDocument(doc);
            canvas.SetMode(PlanningCanvasMode.World); int events = 0; canvas.DocumentChanged += () => events++;
            canvas.BeginRoomPlacement(); canvas.PaintWorldArea(new RectInt(0, 0, 4, 3), false);
            var placed = doc.rooms[0];
            Check(placed.cells.Count == 12 && events == 1 && !canvas.isPlacingRoom, "框选创建为单步事务");
            canvas.PaintWorldArea(new RectInt(4, 0, 2, 3), false);
            Check(placed.cells.Count == 18 && events == 2, "连续矩形扩展");
            canvas.PaintWorldArea(new RectInt(20, 0, 2, 3), false);
            Check(placed.cells.Count == 18 && events == 2, "断开的区域不破坏原房间");
            canvas.BeginConnectorPlacement(); canvas.PaintWorldArea(new RectInt(6, 0, 2, 3), true);
            var corridor = doc.rooms[1];
            Check(corridor.cells.Count == 6 && corridor.connectedRoomIds.Contains(placed.id), "框选通道与连接检测");
            canvas.EraseWorldArea(new RectInt(7, 0, 1, 3));
            Check(corridor.cells.Count == 3 && events == 4, "框选擦除为单步事务");
            canvas.EraseWorldArea(new RectInt(0, 0, 8, 3));
            Check(doc.rooms.Count == 0, "全区域擦除允许删除最后房间和通道");
            ValidateWorldAndAssemblyEditing();
            System.IO.File.WriteAllText("Library/UnifiedGridVerification.txt", "PASS " + checks);
            Debug.Log("统一网格与旧存档验证通过：" + checks + " 项断言。");
        }
    }
}
