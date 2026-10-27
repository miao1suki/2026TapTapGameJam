using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Project.GridArt;
using Project.GridArt.Editor;
using Project.Rooms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project.LevelEditor.Editor
{
    [InitializeOnLoad]
    internal static class SceneArtAuthoring
    {
        private static SceneView boundView;
        private static GridArtScene artScene;
        private static GridArtLayer layer;
        private static int brushIndex;
        private static int quarterTurns;
        private static bool enabled, erase, rectangle, followRegion;
        private static string regionId = string.Empty;
        private static Vector2Int start, last;
        private static bool stroking, changed;
        private static int undoGroup, controlId;
        private static GridArtRegionOwnership strokeOwnership;
        private static readonly HashSet<Vector2Int> ChangedAnchors = new();
        private static readonly List<Vector2Int> RemovedAnchors = new();
        private static readonly Vector3[] HoverCorners = new Vector3[4];
        internal static string Status { get; private set; } = "选择图层和笔刷后即可在 SceneView 绘制。";
        internal static event Action Changed;

        static SceneArtAuthoring()
        {
            SceneView.duringSceneGui -= OnSceneGUI; SceneView.duringSceneGui += OnSceneGUI;
            Undo.undoRedoPerformed -= OnUndoRedo; Undo.undoRedoPerformed += OnUndoRedo;
            AssemblyReloadEvents.beforeAssemblyReload += CancelStroke;
        }

        internal static bool Bind(SceneView view, GridArtScene scene, GridArtLayer selectedLayer, int selectedBrush,
            int turns, bool eraseMode, bool rectangleMode, bool claimNew, string selectedRegionId, bool valueEnabled)
        {
            selectedRegionId ??= string.Empty;
            turns = GridArtRotation.Normalize(turns);
            if (boundView == view && artScene == scene && layer == selectedLayer && brushIndex == selectedBrush && quarterTurns == turns &&
                erase == eraseMode && rectangle == rectangleMode && followRegion == claimNew && regionId == selectedRegionId && enabled == valueEnabled) return false;
            if (stroking && (boundView != view || artScene != scene || layer != selectedLayer || !valueEnabled)) FinishStroke(false);
            boundView = view; artScene = scene; layer = selectedLayer; brushIndex = selectedBrush;
            quarterTurns = turns; erase = eraseMode; rectangle = rectangleMode;
            followRegion = claimNew; regionId = selectedRegionId; enabled = valueEnabled;
            SceneView.RepaintAll();
            return true;
        }

        internal static bool StableBindIsNoOpForAudit() =>
            !Bind(boundView, artScene, layer, brushIndex, quarterTurns, erase, rectangle, followRegion, regionId, enabled);

        internal static GridArtRegionOwnership CreateOwnership(GridArtScene scene)
        {
            if (scene == null || scene.map == null) return null;
            GridArtRegionOwnershipLink link = scene.GetComponent<GridArtRegionOwnershipLink>();
            if (link != null && link.Ownership != null) return link.Ownership;
            string mapPath = AssetDatabase.GetAssetPath(scene.map);
            if (string.IsNullOrEmpty(mapPath)) return null;
            string path = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(Path.GetDirectoryName(mapPath) ?? "Assets", Path.GetFileNameWithoutExtension(mapPath) + "_Ownership.asset").Replace('\\', '/'));
            var ownership = ScriptableObject.CreateInstance<GridArtRegionOwnership>();
            ownership.name = Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(ownership, path);
            Undo.RegisterCreatedObjectUndo(ownership, "创建美术区域归属资产");
            if (link == null) link = Undo.AddComponent<GridArtRegionOwnershipLink>(scene.gameObject);
            Undo.RecordObject(link, "绑定美术区域归属资产"); link.SetOwnership(ownership);
            EditorUtility.SetDirty(link); EditorSceneManager.MarkSceneDirty(scene.gameObject.scene);
            Status = "已创建稀疏归属资产；旧美术仍保持未归属。"; Changed?.Invoke();
            return ownership;
        }

        internal static GridArtRegionOwnership OwnershipFor(GridArtScene scene) =>
            scene != null ? scene.GetComponent<GridArtRegionOwnershipLink>()?.Ownership : null;

        internal static int ClaimRegion(GridArtScene scene, GridArtLayer targetLayer, RoomLifecycleManager manager, string targetRegionId)
        {
            GridArtRegionOwnership ownership = OwnershipFor(scene);
            if (scene?.map == null || targetLayer == null || manager == null || ownership == null || string.IsNullOrEmpty(targetRegionId)) return 0;
            RoomRegion region = manager.Regions.FirstOrDefault(item => item != null && item.id == targetRegionId);
            if (region == null || !Mathf.Approximately(scene.map.cellSize, manager.CellSize)) return 0;
            var regionCells = new HashSet<Vector2Int>(region.cells.Select(SceneGridSpace.RegionToScene));
            Vector2Int[] anchors = targetLayer.cells.Where(cell => regionCells.Contains(cell.position)).Select(cell => cell.position).ToArray();
            if (anchors.Length == 0) return 0;
            Undo.RegisterCompleteObjectUndo(ownership, "认领区域内美术");
            int count = ownership.Claim(targetLayer.id, anchors, targetRegionId);
            if (count > 0) EditorUtility.SetDirty(ownership);
            Status = count > 0 ? $"已认领 {count} 个美术锚点；格内容未改。" : "这些美术已经属于当前区域。";
            Changed?.Invoke(); return count;
        }

        private static void OnSceneGUI(SceneView view)
        {
            if (!enabled || view != boundView || artScene == null || artScene.map == null || layer == null ||
                artScene.gameObject.scene != SceneManager.GetActiveScene() || !SceneEditSession.IsLocked(view) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (layer.locked || layer.followLevel || !layer.visible) { CancelStroke(); Status = "当前图层不可手绘（隐藏、锁定或自动联动）。"; return; }
            GridArtBrush brush = artScene.map.palette != null ? artScene.map.palette.Get(brushIndex) : null;
            if (!erase && brush == null) return;
            Event evt = Event.current; if (evt == null || evt.alt) return;
            var plane = new Plane(artScene.transform.forward, artScene.transform.TransformPoint(new Vector3(0, 0, layer.depth)));
            if (!plane.Raycast(HandleUtility.GUIPointToWorldRay(evt.mousePosition), out float distance)) return;
            Vector3 world = HandleUtility.GUIPointToWorldRay(evt.mousePosition).GetPoint(distance);
            Vector3 local = artScene.transform.InverseTransformPoint(world);
            Vector2Int cell = artScene.map.WorldToCell(local, Vector3.zero);
            if (evt.type == EventType.Repaint) DrawHover(cell, brush);
            controlId = GUIUtility.GetControlID("SceneArtAuthoring".GetHashCode(), FocusType.Passive);
            if (evt.type == EventType.Layout) HandleUtility.AddDefaultControl(controlId);
            if (evt.type == EventType.KeyDown && evt.keyCode == KeyCode.Escape && stroking) { CancelStroke(); evt.Use(); return; }
            if (evt.button > 1) return;
            if (evt.type == EventType.MouseDown)
            {
                BeginStroke(); start = last = cell; GUIUtility.hotControl = controlId;
                if (!rectangle) Paint(cell, brush); evt.Use();
            }
            else if (evt.type == EventType.MouseDrag && stroking && GUIUtility.hotControl == controlId)
            {
                if (!rectangle)
                {
                    int steps = Mathf.Max(Mathf.Abs(cell.x - last.x), Mathf.Abs(cell.y - last.y));
                    if (steps < 4096) for (int i = 1; i <= steps; i++) Paint(Vector2Int.RoundToInt(Vector2.Lerp(last, cell, (float)i / steps)), brush);
                    last = cell;
                }
                evt.Use();
            }
            else if (evt.type == EventType.MouseUp && stroking)
            {
                if (rectangle)
                {
                    RectInt rect = new RectInt(Vector2Int.Min(start, cell), new Vector2Int(Mathf.Abs(start.x - cell.x) + 1, Mathf.Abs(start.y - cell.y) + 1));
                    if ((long)rect.width * rect.height <= 4096)
                    {
                        Vector2Int step = erase || brush == null || brush.autoTerrain ? Vector2Int.one : GridArtRotation.Size(brush.Size, quarterTurns);
                        for (int y = rect.yMin; y < rect.yMax; y += step.y) for (int x = rect.xMin; x < rect.xMax; x += step.x) Paint(new Vector2Int(x, y), brush);
                    }
                }
                FinishStroke(true); evt.Use();
            }
            if (evt.type == EventType.MouseMove || stroking) view.Repaint();
        }

        private static void BeginStroke()
        {
            Undo.IncrementCurrentGroup(); undoGroup = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("场景美术笔画");
            Undo.RegisterCompleteObjectUndo(artScene.map, "场景美术笔画");
            strokeOwnership = OwnershipFor(artScene);
            if (strokeOwnership != null) Undo.RegisterCompleteObjectUndo(strokeOwnership, "场景美术笔画");
            ChangedAnchors.Clear(); stroking = true; changed = false;
        }

        private static void Paint(Vector2Int cell, GridArtBrush brush)
        {
            bool localChanged = false;
            if (erase)
            {
                layer.GetAnchorsCovering(cell, artScene.map.palette, RemovedAnchors);
                localChanged = layer.Erase(cell, artScene.map.palette);
                if (localChanged && strokeOwnership != null)
                    foreach (Vector2Int anchor in RemovedAnchors) strokeOwnership.Set(layer.id, anchor, string.Empty);
                if (localChanged)
                {
                    ChangedAnchors.Add(cell);
                    foreach (Vector2Int anchor in RemovedAnchors) ChangedAnchors.Add(anchor);
                }
            }
            else
            {
                localChanged = layer.Set(cell, brushIndex, quarterTurns);
                if (localChanged && followRegion && strokeOwnership != null && !string.IsNullOrEmpty(regionId))
                    strokeOwnership.Set(layer.id, cell, regionId);
                if (localChanged) ChangedAnchors.Add(cell);
            }
            changed |= localChanged;
        }

        private static void FinishStroke(bool build)
        {
            if (!stroking) return;
            stroking = false; if (GUIUtility.hotControl == controlId) GUIUtility.hotControl = 0;
            if (!changed) { Undo.RevertAllDownToGroup(undoGroup); Status = "没有数据变化。"; Changed?.Invoke(); return; }
            layer.InvalidateLookup(); int previousRevision = artScene.map.revision; artScene.map.revision++; EditorUtility.SetDirty(artScene.map);
            if (strokeOwnership != null) EditorUtility.SetDirty(strokeOwnership);
            Undo.CollapseUndoOperations(undoGroup);
            if (build) GridArtBuilder.ApplyAffected(artScene.map, artScene.gameObject.scene, layer, ChangedAnchors, previousRevision, false);
            ChangedAnchors.Clear(); strokeOwnership = null;
            Status = "笔画已提交；Scene、Map 与稀疏归属保持同一 Undo。"; Changed?.Invoke();
        }

        private static void CancelStroke()
        {
            if (!stroking) return;
            Undo.RevertAllDownToGroup(undoGroup); stroking = false; changed = false; ChangedAnchors.Clear(); strokeOwnership = null;
            if (GUIUtility.hotControl == controlId) GUIUtility.hotControl = 0;
            Status = "已取消本次美术笔画。"; Changed?.Invoke(); SceneView.RepaintAll();
        }

        private static void OnUndoRedo()
        {
            if (artScene?.map != null)
            {
                foreach (GridArtLayer mapLayer in artScene.map.layers) mapLayer?.InvalidateLookup();
                GridArtBuilder.Apply(artScene.map, artScene.gameObject.scene, false, false);
            }
            Changed?.Invoke(); SceneView.RepaintAll();
        }

        private static void DrawHover(Vector2Int cell, GridArtBrush brush)
        {
            Vector2Int footprint = erase || brush == null || brush.autoTerrain ? Vector2Int.one : GridArtRotation.Size(brush.Size, quarterTurns);
            float size = artScene.map.cellSize;
            Vector3 p0 = artScene.transform.TransformPoint(new Vector3(cell.x * size, cell.y * size, layer.depth));
            Vector3 px = artScene.transform.TransformVector(Vector3.right * footprint.x * size);
            Vector3 py = artScene.transform.TransformVector(Vector3.up * footprint.y * size);
            HoverCorners[0] = p0; HoverCorners[1] = p0 + py; HoverCorners[2] = p0 + px + py; HoverCorners[3] = p0 + px;
            Handles.DrawSolidRectangleWithOutline(HoverCorners,
                erase ? new Color(1, .2f, .2f, .12f) : new Color(.2f, .75f, 1, .12f), erase ? Color.red : Color.cyan);
        }
    }
}
