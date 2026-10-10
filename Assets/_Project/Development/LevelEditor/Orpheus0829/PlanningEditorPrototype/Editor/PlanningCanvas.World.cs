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
            StatusChanged?.Invoke("在空白格绘制通道；选择已有通道后可继续扩展。");
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
                BeginDocumentBatch();
                PaintWorldCell(cell, mapTool == PlanningMapTool.Connector);
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
            if ((mapTool != PlanningMapTool.Paint && mapTool != PlanningMapTool.Connector) || cell == lastPaintCell) return;
            // Manhattan interpolation prevents holes when the mouse crosses several cells per event.
            var path = new List<PlanningCell>();
            AppendConnectorPath(path, lastPaintCell, cell);
            foreach (var point in path) PaintWorldCell(new Vector2Int(point.x, point.y), mapTool == PlanningMapTool.Connector);
            lastPaintCell = cell;
        }

        private void CompleteWorldDrag()
        {
            Vector2Int start = CellAt(dragStart), end = CellAt(dragCurrent);
            var area = new RectInt(Mathf.Min(start.x, end.x), Mathf.Min(start.y, end.y), Mathf.Abs(start.x - end.x) + 1, Mathf.Abs(start.y - end.y) + 1);
            if (mapTool == PlanningMapTool.Paint || mapTool == PlanningMapTool.Connector)
            {
                HandleWorldPointerMove(dragCurrent);
                EndDocumentBatch(); return;
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
                BeginDocumentBatch();
                foreach (var point in BuildAreaCells(start, end)) PaintWorldCell(point, false);
                EndDocumentBatch();
            }
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
            if (!AreCellsConnected(owner.cells))
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
                if (!AreCellsConnected(owner.cells))
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
                foreach (var cell in room.cells)
                {
                    Rect rect = CellRect(cell.x, cell.y);
                    FillRect(painter, rect, new Color(.2f, .8f, .78f, selected ? .8f : .55f), 1f);
                    StrokeRect(painter, rect, selected ? Color.white : new Color(.12f, .4f, .4f), selected ? 2f : 1f);
                }
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
                canvas.AddAssemblyBox(new Vector2Int(64, 32));
                Require(connector.boxes.Count == 1 && connector.boxes[0].x == 0, "空通道装配绘制");
                canvas.AddAssemblyBox(new Vector2Int(64, 32));
                Require(connector.boxes.Count == 1, "重复绘制不产生重叠副本");
                canvas.EraseAssemblyBox(new Vector2Int(64, 32));
                Require(connector.boxes.Count == 0, "通道装配擦除");
                canvas.AddAssemblyBox(new Vector2Int(48, 32));
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
    }
}
