using UnityEditor;
using UnityEngine;

namespace Project.GridArt.Editor
{
    [InitializeOnLoad]
    public static class GridArtScenePainter
    {
        private static GridArtWindow target;
        private static Vector2Int start, last;
        private static int group, control;
        private static bool dragging, changed, erase;
        public static bool IsPainting => target != null && target.ScenePainting;
        internal static bool IsStroking => dragging;
        static GridArtScenePainter() { SceneView.duringSceneGui += Draw; }
        public static void SetTarget(GridArtWindow value)
        {
            if (dragging) CancelStroke();
            if (value != null) Project.SurfaceTiles.Editor.SurfaceTileEditorBridge.SetPainting(false);
            target = value; SceneView.RepaintAll();
        }
        private static void CancelStroke()
        {
            if (!dragging) return;
            dragging = false;
            if (GUIUtility.hotControl == control) GUIUtility.hotControl = 0;
            Undo.RevertAllDownToGroup(group); changed = false;
            target?.Refresh();
        }
        private static void Draw(SceneView view)
        {
            if (target == null || !target.ScenePainting || Application.isPlaying || !target.CanEditMap || target.Brush == null || target.Layer == null) return;
            if (Project.SurfaceTiles.Editor.SurfaceTileEditorBridge.IsPainting) { target.StopScenePainting(); return; }
            var evt = Event.current;
            if (evt.type == EventType.KeyDown && evt.keyCode == KeyCode.Escape) { target.StopScenePainting(); evt.Use(); return; }
            if (target.Layer.locked || target.Layer.followLevel || !target.Layer.visible) { CancelStroke(); return; }
            if (evt.alt && !dragging) return;
            var ray = HandleUtility.GUIPointToWorldRay(evt.mousePosition);
            if (!new Plane(Vector3.forward, new Vector3(0, 0, target.Layer.depth)).Raycast(ray, out float distance)) return;
            var cell = target.Map.WorldToCell(ray.GetPoint(distance), Vector3.zero);
            float size = target.Map.cellSize;
            var low = dragging && target.RectangleMode ? Vector2Int.Min(start, cell) : cell;
            Vector3 origin = new Vector3(low.x * size, low.y * size, target.Layer.depth);
            var extent = (dragging && target.RectangleMode ? (Vector2)(Vector2Int.Max(start, cell) - low + Vector2Int.one) : target.Brush.autoTerrain ? Vector2.one : target.BrushDisplaySize) * size;
            Handles.DrawSolidRectangleWithOutline(new[] { origin, origin + Vector3.up * extent.y,
                origin + new Vector3(extent.x, extent.y), origin + Vector3.right * extent.x },
                new Color(.2f, .75f, 1, .1f), new Color(.2f, .75f, 1));
            int id = GUIUtility.GetControlID("GridArtPaint".GetHashCode(), FocusType.Passive);
            if (evt.type == EventType.Layout) HandleUtility.AddDefaultControl(id);
            if (evt.type == EventType.MouseDown && evt.button <= 1)
            {
                dragging = true; changed = false; start = last = cell; erase = target.EraseMode || evt.button == 1 || evt.shift;
                control = id; GUIUtility.hotControl = control;
                Undo.IncrementCurrentGroup(); group = Undo.GetCurrentGroup(); Undo.RegisterCompleteObjectUndo(target.Map, "场景美术笔画");
                if (!target.RectangleMode) Paint(cell); evt.Use();
            }
            else if (evt.type == EventType.MouseDrag && dragging)
            {
                if (!target.RectangleMode)
                {
                    int steps = Mathf.Max(Mathf.Abs(cell.x - last.x), Mathf.Abs(cell.y - last.y));
                    if (steps < 4096) for (int i = 1; i <= steps; i++) Paint(Vector2Int.RoundToInt(Vector2.Lerp(last, cell, (float)i / steps)));
                    last = cell;
                }
                evt.Use();
            }
            else if (evt.type == EventType.MouseUp && dragging)
            {
                if (target.RectangleMode)
                {
                    var rect = new RectInt(Vector2Int.Min(start, cell), new Vector2Int(Mathf.Abs(start.x-cell.x)+1, Mathf.Abs(start.y-cell.y)+1));
                    if ((long)rect.width * rect.height <= 4096)
                    {
                        var step = erase || target.Brush.autoTerrain ? Vector2Int.one : target.BrushFootprint;
                        for (int y = rect.yMin; y < rect.yMax; y += step.y) for (int x = rect.xMin; x < rect.xMax; x += step.x) Paint(new Vector2Int(x,y));
                    }
                }
                dragging = false; if (GUIUtility.hotControl == control) GUIUtility.hotControl = 0;
                if (changed) { target.Map.revision++; EditorUtility.SetDirty(target.Map); Undo.CollapseUndoOperations(group); target.CommitSceneStroke(); }
                evt.Use();
            }
            if (evt.type == EventType.MouseMove || dragging) view.Repaint();
        }
        private static void Paint(Vector2Int p) { changed |= target.PaintSceneCell(p, erase); }
    }
}
