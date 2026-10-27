using System;
using System.Collections.Generic;
using System.Linq;
using Project.Rooms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Project.LevelEditor.Editor
{
    internal enum SceneRegionBrush
    {
        Paint,
        Erase,
    }

    [InitializeOnLoad]
    internal static class SceneRegionAuthoring
    {
        private static RoomLifecycleManager manager;
        private static string regionId;
        private static bool enabled;
        private static SceneView boundView;
        private static bool stroking;
        private static int undoGroup = -1;
        private static Vector2Int lastCell;
        private static int controlId;
        private static int cachedManagerId;
        private static int cachedRevision = -1;
        private static readonly Dictionary<string, List<RectInt>> CachedRuns = new(StringComparer.Ordinal);
        private static readonly Vector3[] Corners = new Vector3[4];

        internal static SceneRegionBrush Brush { get; set; }
        internal static string Status { get; private set; } = "选择区域后即可绘制。";
        internal static event Action Changed;

        static SceneRegionAuthoring()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.duringSceneGui += OnSceneGUI;
            Undo.undoRedoPerformed -= OnUndoRedo;
            Undo.undoRedoPerformed += OnUndoRedo;
            AssemblyReloadEvents.beforeAssemblyReload -= CancelStroke;
            AssemblyReloadEvents.beforeAssemblyReload += CancelStroke;
        }

        internal static bool Bind(SceneView view, RoomLifecycleManager value, string selectedId, bool valueEnabled)
        {
            selectedId ??= string.Empty;
            if (boundView == view && manager == value && regionId == selectedId && enabled == valueEnabled) return false;
            if (stroking && (boundView != view || manager != value || regionId != selectedId || !valueEnabled)) FinishStroke();
            boundView = view;
            manager = value;
            regionId = selectedId;
            enabled = valueEnabled;
            SceneView.RepaintAll();
            return true;
        }

        internal static bool StableBindIsNoOpForAudit() => !Bind(boundView, manager, regionId, enabled);

        internal static RoomLifecycleManager FindManager(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                RoomLifecycleManager found = root.GetComponentInChildren<RoomLifecycleManager>(true);
                if (found != null) return found;
            }
            return null;
        }

        internal static string CreateRegion(RoomLifecycleManager target, bool corridor)
        {
            if (target == null) return string.Empty;
            string id = "region_" + Guid.NewGuid().ToString("N");
            List<RoomRegion> regions = Clone(target.Regions);
            regions.Add(new RoomRegion
            {
                id = id,
                label = corridor ? "新通道" : "新房间",
                corridor = corridor,
                cells = new List<Vector2Int>(),
            });
            Commit(target, regions, corridor ? "新建通道" : "新建房间");
            SetStatus(corridor ? "已新建通道，请在 Scene 中绘制。" : "已新建房间，请在 Scene 中绘制。");
            return id;
        }

        internal static bool Rename(RoomLifecycleManager target, string id, string label)
        {
            List<RoomRegion> regions = Clone(target != null ? target.Regions : null);
            RoomRegion region = regions.Find(item => item.id == id);
            string next = (label ?? string.Empty).Trim();
            if (region == null || string.IsNullOrEmpty(next) || region.label == next) return false;
            region.label = next;
            Commit(target, regions, "重命名区域");
            SetStatus("区域名称已更新；稳定 ID 保持不变。");
            return true;
        }

        internal static bool SetCorridor(RoomLifecycleManager target, string id, bool corridor)
        {
            List<RoomRegion> regions = Clone(target != null ? target.Regions : null);
            RoomRegion region = regions.Find(item => item.id == id);
            if (region == null || region.corridor == corridor) return false;
            region.corridor = corridor;
            Commit(target, regions, corridor ? "改为通道" : "改为房间");
            SetStatus(corridor ? "当前区域已标记为通道。" : "当前区域已标记为房间。");
            return true;
        }

        internal static bool TryApplySingleCellForAudit(RoomLifecycleManager target, string id, Vector2Int cell,
            SceneRegionBrush brush, out string reason)
        {
            manager = target;
            regionId = id;
            Brush = brush;
            RoomRegion selected = target?.Regions.FirstOrDefault(item => item != null && item.id == id);
            if (selected == null) { reason = "找不到测试区域。"; return false; }
            if (!CanChange(selected, cell, brush, out reason)) return false;
            BeginStroke();
            ApplyCell(selected, cell);
            FinishStroke();
            return true;
        }

        private static void OnSceneGUI(SceneView view)
        {
            if (!enabled || manager == null || view == null || view != boundView || manager.gameObject.scene != SceneManager.GetActiveScene() ||
                !SceneEditSession.IsLocked(view)) return;
            RoomRegion selected = manager.Regions.FirstOrDefault(item => item != null && item.id == regionId);
            Event evt = Event.current;
            if (evt == null) return;
            if (evt.type == EventType.Repaint) DrawRegions(selected);
            if (selected == null) return;
            if (evt.alt || evt.button != 0) return;
            controlId = GUIUtility.GetControlID("SceneRegionAuthoring".GetHashCode(), FocusType.Passive);
            HandleUtility.AddDefaultControl(controlId);
            SceneGridSpace grid = SceneGridSpace.For(manager);
            if (!grid.Raycast(HandleUtility.GUIPointToWorldRay(evt.mousePosition), out Vector3 world)) return;
            Vector2Int cell = grid.WorldToRegionCell(world);
            if (evt.type == EventType.Repaint) DrawHover(cell, selected.corridor);

            if (evt.type == EventType.MouseDown)
            {
                if (!CanChange(selected, cell, Brush, out string reason)) { SetStatus(reason); return; }
                BeginStroke();
                ApplyCell(selected, cell);
                lastCell = cell;
                GUIUtility.hotControl = controlId;
                evt.Use();
            }
            else if (evt.type == EventType.MouseDrag && stroking && GUIUtility.hotControl == controlId)
            {
                foreach (Vector2Int step in Manhattan(lastCell, cell))
                {
                    if (!CanChange(selected, step, Brush, out _)) break;
                    ApplyCell(selected, step);
                    lastCell = step;
                }
                evt.Use();
            }
            else if (evt.type == EventType.MouseUp && stroking)
            {
                GUIUtility.hotControl = 0;
                FinishStroke();
                evt.Use();
            }
            else if (evt.type == EventType.KeyDown && evt.keyCode == KeyCode.Escape && stroking)
            {
                Undo.RevertAllDownToGroup(undoGroup);
                stroking = false;
                undoGroup = -1;
                GUIUtility.hotControl = 0;
                manager.RebuildOwnership();
                SetStatus("已取消本次区域笔画。");
                evt.Use();
            }
        }

        private static void BeginStroke()
        {
            Undo.IncrementCurrentGroup();
            undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(Brush == SceneRegionBrush.Paint ? "绘制房间/通道" : "擦除房间/通道");
            Undo.RegisterCompleteObjectUndo(manager, Undo.GetCurrentGroupName());
            stroking = true;
        }

        private static void ApplyCell(RoomRegion selected, Vector2Int cell)
        {
            if (Brush == SceneRegionBrush.Paint) selected.cells.Add(cell);
            else selected.cells.Remove(cell);
            manager.RebuildOwnership();
            EditorUtility.SetDirty(manager);
            EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
            SetStatus(Brush == SceneRegionBrush.Paint ? $"已绘制 {cell}。" : $"已擦除 {cell}。");
        }

        private static void FinishStroke()
        {
            if (!stroking) return;
            Undo.CollapseUndoOperations(undoGroup);
            stroking = false;
            undoGroup = -1;
            Changed?.Invoke();
        }

        private static void CancelStroke()
        {
            if (stroking) FinishStroke();
        }

        private static bool CanChange(RoomRegion selected, Vector2Int cell, SceneRegionBrush brush, out string reason)
        {
            reason = string.Empty;
            if (brush == SceneRegionBrush.Paint)
            {
                foreach (RoomRegion region in manager.Regions)
                    if (region != null && region.cells != null && region.cells.Contains(cell))
                    { reason = region.id == selected.id ? "该格已属于当前区域。" : "该格已属于其他房间/通道。"; return false; }
                if (selected.cells.Count > 0 && !Neighbors(cell).Any(selected.cells.Contains))
                { reason = "新格必须与当前区域四邻接。"; return false; }
                return true;
            }
            if (!selected.cells.Contains(cell)) { reason = "该格不属于当前区域。"; return false; }
            if (selected.cells.Count <= 1) return true;
            var remaining = new HashSet<Vector2Int>(selected.cells); remaining.Remove(cell);
            if (!IsConnected(remaining)) { reason = "擦除会使区域断裂；请先拆分区域。"; return false; }
            return true;
        }

        private static bool IsConnected(HashSet<Vector2Int> cells)
        {
            if (cells.Count == 0) return true;
            var visited = new HashSet<Vector2Int>();
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(cells.First());
            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();
                if (!visited.Add(current)) continue;
                foreach (Vector2Int next in Neighbors(current)) if (cells.Contains(next) && !visited.Contains(next)) queue.Enqueue(next);
            }
            return visited.Count == cells.Count;
        }

        private static IEnumerable<Vector2Int> Manhattan(Vector2Int from, Vector2Int to)
        {
            Vector2Int current = from;
            while (current.x != to.x) { current.x += Math.Sign(to.x - current.x); yield return current; }
            while (current.y != to.y) { current.y += Math.Sign(to.y - current.y); yield return current; }
        }

        private static IEnumerable<Vector2Int> Neighbors(Vector2Int cell)
        {
            yield return cell + Vector2Int.left; yield return cell + Vector2Int.right;
            yield return cell + Vector2Int.up; yield return cell + Vector2Int.down;
        }

        private static void DrawRegions(RoomRegion selected)
        {
            EnsureRunCache(manager);
            CompareFunction previous = Handles.zTest;
            Handles.zTest = CompareFunction.Always;
            foreach (RoomRegion region in manager.Regions)
            {
                if (region == null || region.cells == null) continue;
                Color fill = region.corridor ? new Color(.1f, .75f, 1f, .12f) : new Color(.15f, 1f, .45f, .1f);
                Color outline = region.id == selected?.id ? new Color(1f, .8f, .15f, .95f) : new Color(fill.r, fill.g, fill.b, .55f);
                if (!CachedRuns.TryGetValue(region.id ?? string.Empty, out List<RectInt> runs)) continue;
                foreach (RectInt run in runs) DrawRect(run, fill, outline);
            }
            Handles.zTest = previous;
        }

        private static void DrawHover(Vector2Int cell, bool corridor) =>
            DrawCell(cell, corridor ? new Color(.1f, .75f, 1f, .22f) : new Color(.15f, 1f, .45f, .2f), Color.white);

        private static void DrawCell(Vector2Int cell, Color fill, Color outline)
            => DrawRect(new RectInt(cell, Vector2Int.one), fill, outline);

        private static void DrawRect(RectInt rect, Color fill, Color outline)
        {
            float size = manager.CellSize;
            float x0 = rect.xMin * size, x1 = rect.xMax * size;
            float y0 = -rect.yMin * size, y1 = -rect.yMax * size;
            Transform root = manager.transform;
            Corners[0] = root.TransformPoint(new Vector3(x0, y0, 0)); Corners[1] = root.TransformPoint(new Vector3(x1, y0, 0));
            Corners[2] = root.TransformPoint(new Vector3(x1, y1, 0)); Corners[3] = root.TransformPoint(new Vector3(x0, y1, 0));
            Handles.DrawSolidRectangleWithOutline(Corners, fill, outline);
        }

        internal static int CachedRectangleCount(RoomLifecycleManager target)
        {
            EnsureRunCache(target);
            return CachedRuns.Values.Sum(runs => runs.Count);
        }

        private static void EnsureRunCache(RoomLifecycleManager target)
        {
            if (target == null) { cachedManagerId = 0; cachedRevision = -1; CachedRuns.Clear(); return; }
            int id = target.GetInstanceID();
            if (cachedManagerId == id && cachedRevision == target.Revision) return;
            cachedManagerId = id; cachedRevision = target.Revision; CachedRuns.Clear();
            foreach (RoomRegion region in target.Regions)
            {
                if (region == null) continue;
                var runs = new List<RectInt>(); CachedRuns[region.id ?? string.Empty] = runs;
                if (region.cells == null || region.cells.Count == 0) continue;
                var active = new Dictionary<(int start, int width), RectInt>();
                foreach (IGrouping<int, Vector2Int> row in region.cells.Distinct().GroupBy(cell => cell.y).OrderBy(group => group.Key))
                {
                    int[] xs = row.Select(cell => cell.x).OrderBy(x => x).ToArray();
                    int start = xs[0], previous = xs[0];
                    var next = new Dictionary<(int start, int width), RectInt>();
                    for (int i = 1; i <= xs.Length; i++)
                    {
                        if (i < xs.Length && xs[i] == previous + 1) { previous = xs[i]; continue; }
                        int width = previous - start + 1;
                        var key = (start, width);
                        if (active.TryGetValue(key, out RectInt existing) && existing.yMax == row.Key)
                        {
                            existing.height++;
                            next.Add(key, existing);
                        }
                        else next.Add(key, new RectInt(start, row.Key, width, 1));
                        if (i < xs.Length) start = previous = xs[i];
                    }
                    foreach (KeyValuePair<(int start, int width), RectInt> pair in active)
                        if (!next.ContainsKey(pair.Key)) runs.Add(pair.Value);
                    active = next;
                }
                runs.AddRange(active.Values);
            }
        }

        private static List<RoomRegion> Clone(IReadOnlyList<RoomRegion> source)
        {
            var result = new List<RoomRegion>();
            if (source == null) return result;
            foreach (RoomRegion region in source)
                if (region != null) result.Add(new RoomRegion { id = region.id, label = region.label, corridor = region.corridor,
                    cells = region.cells != null ? new List<Vector2Int>(region.cells) : new List<Vector2Int>() });
            return result;
        }

        private static void Commit(RoomLifecycleManager target, List<RoomRegion> regions, string undoName)
        {
            Undo.RegisterCompleteObjectUndo(target, undoName);
            target.ConfigureAuthoredRegions(regions, target.CellSize);
            EditorUtility.SetDirty(target);
            EditorSceneManager.MarkSceneDirty(target.gameObject.scene);
            Changed?.Invoke();
        }

        private static void OnUndoRedo()
        {
            if (manager != null) manager.RebuildOwnership();
            Changed?.Invoke();
            SceneView.RepaintAll();
        }

        private static void SetStatus(string value)
        {
            Status = value;
            Changed?.Invoke();
        }
    }
}
