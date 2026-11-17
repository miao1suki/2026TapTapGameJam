using System;
using System.Collections.Generic;
using System.Linq;
using Project.Rooms;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Project.LevelEditor.Editor
{
    // A disposable read model. No Unity objects or author data are written here.
    internal sealed class SceneRoomSnapshot
    {
        internal sealed class Room
        {
            internal string id, label;
            internal bool corridor;
            internal HashSet<Vector2Int> cells;
            internal readonly HashSet<int> neighbours = new();
            internal readonly List<RectInt> runs = new();
            internal RectInt bounds;
            internal int components, overlaps;
            internal string Problems = string.Empty;
            internal void CacheProblems() => Problems = cells.Count == 0 ? "空区域" :
                string.Join(" · ", new[] { components > 1 ? $"断裂 {components} 片" : null,
                    overlaps > 0 ? $"重叠 {overlaps} 格" : null,
                    corridor && neighbours.Count < 2 ? "通道邻接不足" : null }.Where(s => s != null));
        }
        internal readonly List<Room> rooms = new();
        internal readonly Dictionary<Vector2Int, List<int>> owners = new();
        internal RectInt bounds;
        private static readonly Vector2Int[] Directions = { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down };

        internal static SceneRoomSnapshot Build(IReadOnlyList<RoomRegion> regions)
        {
            var result = new SceneRoomSnapshot();
            foreach (RoomRegion region in regions ?? Array.Empty<RoomRegion>())
            {
                if (region == null) continue;
                var room = new Room { id = region.id, label = string.IsNullOrEmpty(region.label) ? region.id : region.label,
                    corridor = region.corridor, cells = new HashSet<Vector2Int>(region.cells ?? new List<Vector2Int>()) };
                int index = result.rooms.Count;
                result.rooms.Add(room);
                foreach (Vector2Int cell in room.cells)
                {
                    if (!result.owners.TryGetValue(cell, out var list)) result.owners.Add(cell, list = new List<int>());
                    list.Add(index);
                }
                if (room.cells.Count == 0) continue;
                room.bounds = Bounds(room.cells);
                var remaining = new HashSet<Vector2Int>(room.cells);
                var queue = new Queue<Vector2Int>();
                while (remaining.Count > 0)
                {
                    room.components++;
                    Vector2Int start = remaining.First(); remaining.Remove(start); queue.Enqueue(start);
                    while (queue.Count > 0)
                    {
                        Vector2Int cell = queue.Dequeue();
                        foreach (Vector2Int direction in Directions)
                            if (remaining.Remove(cell + direction)) queue.Enqueue(cell + direction);
                    }
                }
                // Horizontal runs keep painting proportional to the outline, not total cell area.
                foreach (var row in room.cells.GroupBy(c => c.y))
                {
                    int[] xs = row.Select(c => c.x).OrderBy(x => x).ToArray();
                    int start = xs[0], previous = start;
                    for (int i = 1; i <= xs.Length; i++)
                    {
                        if (i < xs.Length && xs[i] == previous + 1) { previous = xs[i]; continue; }
                        room.runs.Add(new RectInt(start, row.Key, previous - start + 1, 1));
                        if (i < xs.Length) start = previous = xs[i];
                    }
                }
                var merged = new List<RectInt>();
                foreach (var column in room.runs.GroupBy(r => (r.x, r.width)))
                {
                    RectInt active = default; bool first = true;
                    foreach (RectInt run in column.OrderBy(r => r.y))
                    {
                        if (!first && active.yMax == run.y) active.height++;
                        else { if (!first) merged.Add(active); active = run; first = false; }
                    }
                    if (!first) merged.Add(active);
                }
                room.runs.Clear(); room.runs.AddRange(merged);
            }
            if (result.owners.Count > 0) result.bounds = Bounds(result.owners.Keys);
            foreach (var pair in result.owners)
            foreach (int owner in pair.Value)
            {
                if (pair.Value.Count > 1) result.rooms[owner].overlaps++;
                foreach (Vector2Int direction in Directions)
                    if (result.owners.TryGetValue(pair.Key + direction, out var neighbours))
                        foreach (int other in neighbours)
                            if (other != owner) result.rooms[owner].neighbours.Add(other);
            }
            foreach (var room in result.rooms) room.CacheProblems();
            return result;
        }

        private static RectInt Bounds(IEnumerable<Vector2Int> cells)
        {
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            foreach (Vector2Int c in cells) { minX = Math.Min(minX, c.x); minY = Math.Min(minY, c.y); maxX = Math.Max(maxX, c.x); maxY = Math.Max(maxY, c.y); }
            return new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }
    }

    internal sealed class SceneRoomNavigator : VisualElement
    {
        private readonly VisualElement map;
        private readonly ScrollView list;
        private readonly Label status;
        private readonly TextField search;
        private readonly Toggle problemsOnly;
        private readonly Action<string> select;
        private SceneRoomSnapshot snapshot = new();
        private RoomLifecycleManager manager;
        private int revision = -1;
        private string selectedId;
        private Vector2 offset;
        private float scale = 1;
        private bool panning;
        private Vector2 lastPointer;

        internal SceneRoomSnapshot Snapshot => snapshot;
        internal SceneRoomNavigator(Action<string> select)
        {
            this.select = select;
            style.marginTop = 8; style.marginBottom = 8;
            search = new TextField { tooltip = "按房间名称或稳定 ID 搜索", label = "查找" };
            search.RegisterValueChangedCallback(_ => RefreshList()); Add(search);
            problemsOnly = new Toggle("只看问题");
            problemsOnly.RegisterValueChangedCallback(_ => RefreshList()); Add(problemsOnly);
            Add(new Button(Fit) { text = "适配全图", tooltip = "滚轮缩放；中键或右键拖动；左键定位房间。" });
            map = new VisualElement { name = "room-navigation-map" };
            map.style.height = 160; map.style.backgroundColor = Hex("#1E2126");
            map.generateVisualContent += Draw;
            map.RegisterCallback<PointerDownEvent>(OnPointerDown);
            map.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (panning) { offset += (Vector2)evt.localPosition - lastPointer; lastPointer = evt.localPosition; map.MarkDirtyRepaint(); evt.StopPropagation(); return; }
                var hits = Hit(evt.localPosition);
                map.tooltip = hits.Count == 0 ? "点击房间定位；此图只用于导航" :
                    string.Join(" / ", hits.Select(i => $"{snapshot.rooms[i].label} · 邻接 {snapshot.rooms[i].neighbours.Count}"));
            });
            map.RegisterCallback<PointerUpEvent>(evt => { if (panning) { panning = false; map.ReleasePointer(evt.pointerId); evt.StopPropagation(); } });
            map.RegisterCallback<PointerCaptureOutEvent>(_ => panning = false);
            map.RegisterCallback<WheelEvent>(evt =>
            {
                Vector2 point = evt.localMousePosition;
                float next = Mathf.Clamp(scale * Mathf.Exp(-evt.delta.y * .08f), .01f, 64f);
                offset = point - (point - offset) * (next / scale); scale = next;
                map.MarkDirtyRepaint(); evt.StopPropagation();
            });
            map.RegisterCallback<GeometryChangedEvent>(_ => Fit()); Add(map);
            list = new ScrollView(); list.style.maxHeight = 96; Add(list);
            status = new Label { style = { whiteSpace = WhiteSpace.Normal, fontSize = 12, marginTop = 4 } }; Add(status);
        }

        internal void Bind(RoomLifecycleManager target, string selected)
        {
            bool changed = manager != target || revision != (target != null ? target.Revision : -1);
            manager = target;
            bool selectionChanged = selectedId != selected;
            selectedId = selected;
            if (changed)
            {
                revision = target != null ? target.Revision : -1;
                snapshot = SceneRoomSnapshot.Build(target != null ? target.Regions : null);
                Fit();
            }
            if (changed || selectionChanged) RefreshList();
        }

        internal void Fit()
        {
            RectInt b = snapshot.bounds;
            scale = Mathf.Min(Mathf.Max(1, map.contentRect.width - 24) / Mathf.Max(1, b.width),
                Mathf.Max(1, map.contentRect.height - 24) / Mathf.Max(1, b.height));
            offset = new Vector2((map.contentRect.width - b.width * scale) * .5f - b.x * scale,
                (map.contentRect.height - b.height * scale) * .5f - b.y * scale);
            map.MarkDirtyRepaint();
        }

        private bool Matches(SceneRoomSnapshot.Room room) =>
            (!problemsOnly.value || !string.IsNullOrEmpty(room.Problems)) &&
            (string.IsNullOrWhiteSpace(search.value) || (room.label ?? "").IndexOf(search.value, StringComparison.OrdinalIgnoreCase) >= 0 ||
                (room.id ?? "").IndexOf(search.value, StringComparison.OrdinalIgnoreCase) >= 0);

        private void RefreshList()
        {
            list.Clear(); int count = 0;
            foreach (var room in snapshot.rooms)
            {
                if (!Matches(room)) continue;
                count++;
                string problem = room.Problems;
                var button = new Button(() => select(room.id)) { text =
                    $"{(room.id == selectedId ? "●" : room.corridor ? "↔" : "□")} {room.label}" +
                    (string.IsNullOrEmpty(problem) ? $" · 邻接 {room.neighbours.Count}" : $" · ⚠ {problem}"),
                    tooltip = $"{room.id}\n{room.cells.Count} 格；{problem}" };
                button.style.minHeight = 24; button.style.unityTextAlign = TextAnchor.MiddleLeft;
                button.style.color = room.id == selectedId ? Hex("#4DA3FF") : !string.IsNullOrEmpty(problem) ? Hex("#E0A63A") : Hex("#D8DCE2");
                list.Add(button);
            }
            if (count == 0) list.Add(new Label(manager == null ? "未读取场景区域管理器" : snapshot.rooms.Count == 0 ? "暂无房间" : "无匹配房间"));
            var current = snapshot.rooms.FirstOrDefault(r => r.id == selectedId);
            status.text = current == null ? "只读导航 · 点击房间定位 Scene" :
                $"{current.label} · {current.cells.Count} 格\n连接：" +
                (current.neighbours.Count == 0 ? "无四邻接区域" : string.Join("、", current.neighbours.Select(i => snapshot.rooms[i].label))) +
                (string.IsNullOrEmpty(current.Problems) ? "" : $"\n⚠ {current.Problems}（不会自动修复）");
            map.MarkDirtyRepaint();
        }

        private IReadOnlyList<int> Hit(Vector2 point)
        {
            Vector2 local = (point - offset) / Mathf.Max(.0001f, scale);
            return snapshot.owners.TryGetValue(new Vector2Int(Mathf.FloorToInt(local.x), Mathf.FloorToInt(local.y)), out var hits) ? hits : Array.Empty<int>();
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button == 1 || evt.button == 2)
            { panning = true; lastPointer = evt.localPosition; map.CapturePointer(evt.pointerId); evt.StopPropagation(); return; }
            if (evt.button != 0) return;
            var hits = Hit(evt.localPosition);
            if (hits.Count == 1) select(snapshot.rooms[hits[0]].id);
            else if (hits.Count > 1)
            {
                var menu = new GenericDropdownMenu();
                foreach (int index in hits) { var room = snapshot.rooms[index]; menu.AddItem(room.label, room.id == selectedId, () => select(room.id)); }
                menu.DropDown(new Rect(evt.position, Vector2.zero), map, false, false);
            }
            evt.StopPropagation();
        }

        private void Draw(MeshGenerationContext context)
        {
            var painter = context.painter2D;
            foreach (var room in snapshot.rooms)
            {
                Color color = room.id == selectedId ? Hex("#4DA3FF") : room.overlaps > 0 ? Hex("#E5534B") :
                    room.components > 1 ? Hex("#E0A63A") : room.corridor ? Hex("#5FBF8A") : Hex("#7A8594");
                color.a = Matches(room) ? .8f : .15f;
                painter.fillColor = color;
                foreach (RectInt run in room.runs)
                {
                    Vector2 a = offset + new Vector2(run.xMin, run.yMin) * scale;
                    Vector2 b = offset + new Vector2(run.xMax, run.yMax) * scale;
                    painter.BeginPath(); painter.MoveTo(a); painter.LineTo(new Vector2(b.x, a.y));
                    painter.LineTo(b); painter.LineTo(new Vector2(a.x, b.y)); painter.ClosePath(); painter.Fill();
                    if (room.id == selectedId) { painter.strokeColor = Color.white; painter.lineWidth = 1; painter.Stroke(); }
                }
            }
        }

        private static Color Hex(string hex) { ColorUtility.TryParseHtmlString(hex, out Color color); return color; }

        internal static void Frame(RoomLifecycleManager target, string id, SceneView view)
        {
            RoomRegion region = target?.Regions.FirstOrDefault(r => r != null && r.id == id);
            if (view == null || region?.cells == null || region.cells.Count == 0) return;
            SceneGridSpace grid = SceneGridSpace.For(target);
            var bounds = new Bounds(grid.RegionCellCenter(region.cells[0]), Vector3.zero);
            foreach (Vector2Int cell in region.cells) bounds.Encapsulate(grid.RegionCellCenter(cell));
            bounds.Expand(target.CellSize * 2);
            view.Frame(bounds, true);
        }
    }
}
