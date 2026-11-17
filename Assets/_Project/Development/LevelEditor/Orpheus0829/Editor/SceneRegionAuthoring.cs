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
        private static RoomRegion strokeRegion;
        private static int strokeRevision;
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
            EditorApplication.playModeStateChanged += change =>
            {
                if (change == PlayModeStateChange.ExitingEditMode) CancelStroke();
            };
        }

        internal static bool Bind(SceneView view, RoomLifecycleManager value, string selectedId, bool valueEnabled)
        {
            selectedId ??= string.Empty;
            if (boundView == view && manager == value && regionId == selectedId && enabled == valueEnabled) return false;
            if (stroking && (boundView != view || manager != value || regionId != selectedId || !valueEnabled)) CancelStroke();
            boundView = view;
            manager = value;
            regionId = selectedId;
            enabled = valueEnabled;
            SceneView.RepaintAll();
            return true;
        }

        internal static bool StableBindIsNoOpForAudit() => !Bind(boundView, manager, regionId, enabled);

        internal static void PreviewAndCancelForAudit(RoomLifecycleManager target, string id, Vector2Int cell)
        {
            manager = target; regionId = id; Brush = SceneRegionBrush.Paint;
            BeginStroke(); ApplyCell(strokeRegion, cell); CancelStroke();
        }

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
            if (stroking) selected = strokeRegion;
            Event evt = Event.current;
            if (evt == null) return;
            if (evt.type == EventType.Repaint) DrawRegions(selected);
            if (SceneWorkbenchInput.IsOverPanel(view, evt)) { if (evt.type == EventType.MouseUp) CancelStroke(); return; }
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
                    if (Brush == SceneRegionBrush.Paint && selected.cells.Contains(step)) { lastCell = step; continue; }
                    if (!CanChange(selected, step, Brush, out string reason))
                    { CancelStroke(); SetStatus(reason); break; }
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
                CancelStroke();
                evt.Use();
            }
        }

        private static void BeginStroke()
        {
            strokeRegion = Clone(manager.Regions).First(item => item.id == regionId);
            strokeRevision = manager.Revision;
            stroking = true;
        }

        private static void ApplyCell(RoomRegion selected, Vector2Int cell)
        {
            if (Brush == SceneRegionBrush.Paint) strokeRegion.cells.Add(cell);
            else strokeRegion.cells.Remove(cell);
            SetStatus($"预览 {strokeRegion.cells.Count} 格 · 松开提交，Esc 取消。");
        }

        private static void FinishStroke()
        {
            if (!stroking) return;
            stroking = false;
            if (manager == null || manager.Revision != strokeRevision)
            { strokeRegion = null; SetStatus("区域已被其他操作改变，本笔已取消。请重新绘制。"); return; }
            List<RoomRegion> regions = Clone(manager.Regions);
            int index = regions.FindIndex(item => item.id == regionId);
            if (index < 0) { strokeRegion = null; return; }
            regions[index] = strokeRegion; strokeRegion = null;
            Commit(manager, regions, Brush == SceneRegionBrush.Paint ? "绘制房间/通道" : "擦除房间/通道");
            SetStatus("区域笔画已提交，可一次撤销。");
        }

        private static void CancelStroke()
        {
            if (!stroking) return;
            stroking = false; strokeRegion = null;
            if (GUIUtility.hotControl == controlId) GUIUtility.hotControl = 0;
            SetStatus("已取消未完成的区域笔画。");
        }

        private static bool CanChange(RoomRegion selected, Vector2Int cell, SceneRegionBrush brush, out string reason)
        {
            reason = string.Empty;
            if (brush == SceneRegionBrush.Paint)
            {
                if (selected.cells.Contains(cell)) { reason = "该格已属于当前区域。"; return false; }
                foreach (RoomRegion region in manager.Regions)
                    if (region != null && region.id != selected.id && region.cells != null && region.cells.Contains(cell))
                    { reason = region.id == selected.id ? "该格已属于当前区域。" : "该格已属于其他房间/通道。"; return false; }
                if (selected.cells.Count > 0 && !Neighbors(cell).Any(selected.cells.Contains))
                { reason = "新格必须与当前区域四邻接。"; return false; }
                return true;
            }
            if (!selected.cells.Contains(cell)) { reason = "该格不属于当前区域。"; return false; }
            Vector2Int sceneCell = SceneGridSpace.RegionToScene(cell);
            var occupants = SceneObjectAuthoring.Read(manager.gameObject.scene).At(sceneCell);
            if (occupants.Count > 0)
            { reason = $"该格有 {occupants.Count} 个作者对象；请先移动对象。擦区域不会删除对象。"; return false; }
            if (selected.cells.Count <= 1) return true;
            var remaining = new HashSet<Vector2Int>(selected.cells); remaining.Remove(cell);
            if (ComponentCount(remaining) > ComponentCount(new HashSet<Vector2Int>(selected.cells)))
            { reason = "擦除会增加区域断裂；请先调整连通部分。"; return false; }
            return true;
        }

        private static int ComponentCount(HashSet<Vector2Int> cells)
        {
            int count = 0;
            var queue = new Queue<Vector2Int>();
            while (cells.Count > 0)
            {
                count++; Vector2Int first = cells.First(); cells.Remove(first); queue.Enqueue(first);
                while (queue.Count > 0)
                {
                    Vector2Int current = queue.Dequeue();
                    foreach (Vector2Int next in Neighbors(current)) if (cells.Remove(next)) queue.Enqueue(next);
                }
            }
            return count;
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
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(undoName);
            Undo.RegisterCompleteObjectUndo(target, undoName);
            target.ConfigureAuthoredRegions(regions, target.CellSize);
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            EditorUtility.SetDirty(target);
            EditorSceneManager.MarkSceneDirty(target.gameObject.scene);
            Undo.CollapseUndoOperations(group);
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
