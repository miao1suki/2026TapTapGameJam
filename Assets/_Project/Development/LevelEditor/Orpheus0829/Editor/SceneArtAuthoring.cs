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
        private static GridArtMap strokeMap;
        private static GridArtLayer sourceLayer;
        private static GridArtRegionOwnership sourceOwnership;
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
            EditorApplication.playModeStateChanged += change =>
            {
                if (change == PlayModeStateChange.ExitingEditMode) CancelStroke();
            };
        }

        internal static bool Bind(SceneView view, GridArtScene scene, GridArtLayer selectedLayer, int selectedBrush,
            int turns, bool eraseMode, bool rectangleMode, bool claimNew, string selectedRegionId, bool valueEnabled)
        {
            selectedRegionId ??= string.Empty;
            turns = GridArtRotation.Normalize(turns);
            GridArtLayer boundLayer = sourceLayer ?? layer;
            if (boundView == view && artScene == scene && boundLayer == selectedLayer && brushIndex == selectedBrush && quarterTurns == turns &&
                erase == eraseMode && rectangle == rectangleMode && followRegion == claimNew && regionId == selectedRegionId && enabled == valueEnabled) return false;
            if (stroking) CancelStroke();
            boundView = view; artScene = scene; layer = selectedLayer; brushIndex = selectedBrush;
            quarterTurns = turns; erase = eraseMode; rectangle = rectangleMode;
            followRegion = claimNew; regionId = selectedRegionId; enabled = valueEnabled;
            SceneView.RepaintAll();
            return true;
        }

        internal static bool StableBindIsNoOpForAudit() =>
            !Bind(boundView, artScene, layer, brushIndex, quarterTurns, erase, rectangle, followRegion, regionId, enabled);

        internal static void StrokeForAudit(GridArtScene scene, GridArtLayer target, Vector2Int cell, bool cancel, bool claim = false)
        {
            artScene = scene; layer = target; erase = false; brushIndex = 0;
            followRegion = claim; regionId = claim ? "audit_region" : string.Empty;
            BeginStroke(); Paint(cell, null);
            if (cancel) CancelStroke(); else FinishStroke(false);
        }

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
            if (scene?.map == null || targetLayer == null || targetLayer.locked || targetLayer.followLevel || targetLayer.parallax != Vector2.one || manager == null || ownership == null || string.IsNullOrEmpty(targetRegionId)) return 0;
            RoomRegion region = manager.Regions.FirstOrDefault(item => item != null && item.id == targetRegionId);
            if (region == null || !Mathf.Approximately(scene.map.cellSize, manager.CellSize)) return 0;
            var regionCells = new HashSet<Vector2Int>(region.cells.Select(SceneGridSpace.RegionToScene));
            Vector2Int[] anchors = targetLayer.cells.Where(cell => regionCells.Contains(cell.position)).Select(cell => cell.position).ToArray();
            if (anchors.Any(cell => !string.IsNullOrEmpty(ownership.Get(targetLayer.id, cell)) && ownership.Get(targetLayer.id, cell) != targetRegionId))
            { Status = "区域内有其他房间已认领的美术，本次认领已拒绝。"; return 0; }
            anchors = anchors.Where(cell => ownership.Get(targetLayer.id, cell) != targetRegionId).ToArray();
            if (anchors.Length == 0) return 0;
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("认领区域内美术");
            Undo.RegisterCompleteObjectUndo(ownership, "认领区域内美术");
            int count = ownership.Claim(targetLayer.id, anchors, targetRegionId);
            if (count > 0) EditorUtility.SetDirty(ownership);
            Undo.CollapseUndoOperations(group);
            Status = count > 0 ? $"已认领 {count} 个美术锚点；格内容未改。" : "这些美术已经属于当前区域。";
            Changed?.Invoke(); return count;
        }

        internal static string PreviewClaim(GridArtScene scene, GridArtLayer targetLayer, RoomLifecycleManager manager, string id, out bool canClaim)
        {
            canClaim = false;
            var ownership = OwnershipFor(scene);
            var region = manager?.Regions.FirstOrDefault(r => r != null && r.id == id);
            if (scene?.map == null || ownership == null || region == null || targetLayer == null) return "请先选择区域、图层并创建稀疏归属资产。";
            if (targetLayer.locked || targetLayer.followLevel || targetLayer.parallax != Vector2.one) return "锁定、自动联动或视差背景图层不可认领。";
            if (!Mathf.Approximately(scene.map.cellSize, manager.CellSize)) return "关卡与美术格尺寸不同。";
            var cells = new HashSet<Vector2Int>(region.cells.Select(SceneGridSpace.RegionToScene));
            var anchors = targetLayer.cells.Where(c => cells.Contains(c.position)).ToArray();
            int conflicts = anchors.Count(c => !string.IsNullOrEmpty(ownership.Get(targetLayer.id, c.position)) && ownership.Get(targetLayer.id, c.position) != id);
            int newCount = anchors.Count(c => string.IsNullOrEmpty(ownership.Get(targetLayer.id, c.position)));
            int large = anchors.Count(c => scene.map.palette?.Get(c.brush)?.Size != Vector2Int.one);
            canClaim = conflicts == 0 && newCount > 0;
            return $"区域：{region.label}；图层：{targetLayer.label}\n新增认领 {newCount} · 其他区域归属冲突 {conflicts} · 大笔刷 {large}\n按锚点归属；大笔刷跨边界部分会整体跟随。格内容保持原样。";
        }

        private static void OnSceneGUI(SceneView view)
        {
            if (!enabled || view != boundView || artScene == null || artScene.map == null || layer == null ||
                artScene.gameObject.scene != SceneManager.GetActiveScene() || !SceneEditSession.IsLocked(view) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (layer.locked || layer.followLevel || !layer.visible) { CancelStroke(); Status = "当前图层不可手绘（隐藏、锁定或自动联动）。"; return; }
            GridArtBrush brush = artScene.map.palette != null ? artScene.map.palette.Get(brushIndex) : null;
            if (!erase && brush == null) return;
            Event evt = Event.current; if (evt == null || evt.alt) return;
            if (SceneWorkbenchInput.IsOverPanel(view, evt)) { if (evt.type == EventType.MouseUp) CancelStroke(); return; }
            var plane = new Plane(artScene.transform.forward, artScene.transform.TransformPoint(new Vector3(0, 0, layer.depth)));
            if (!plane.Raycast(HandleUtility.GUIPointToWorldRay(evt.mousePosition), out float distance)) return;
            Vector3 world = HandleUtility.GUIPointToWorldRay(evt.mousePosition).GetPoint(distance);
            Vector3 local = artScene.transform.InverseTransformPoint(world);
            Vector2Int cell = artScene.map.WorldToCell(local, Vector3.zero);
            if (evt.type == EventType.Repaint) DrawHover(cell, brush);
            controlId = GUIUtility.GetControlID("SceneArtAuthoring".GetHashCode(), FocusType.Passive);
            if (evt.type == EventType.Layout) HandleUtility.AddDefaultControl(controlId);
            if (evt.type == EventType.KeyDown && evt.keyCode == KeyCode.Escape && stroking) { CancelStroke(); evt.Use(); return; }
            if (evt.button != 0) return;
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
            sourceLayer = layer;
            strokeMap = UnityEngine.Object.Instantiate(artScene.map);
            strokeMap.hideFlags = HideFlags.HideAndDontSave;
            layer = strokeMap.layers.First(item => item.id == sourceLayer.id);
            sourceOwnership = OwnershipFor(artScene);
            strokeOwnership = sourceOwnership != null ? UnityEngine.Object.Instantiate(sourceOwnership) : null;
            if (strokeOwnership != null) strokeOwnership.hideFlags = HideFlags.HideAndDontSave;
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
            if (!changed) { ReleaseStroke(); Status = "没有数据变化。"; Changed?.Invoke(); return; }
            Undo.IncrementCurrentGroup(); undoGroup = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("场景美术笔画");
            Undo.RegisterCompleteObjectUndo(artScene.map, "场景美术笔画");
            sourceLayer.cells = layer.cells;
            if (sourceOwnership != null)
            {
                Undo.RegisterCompleteObjectUndo(sourceOwnership, "场景美术笔画");
                // Copy only sparse author entries, never temporary ScriptableObject identity/hide flags.
                foreach (var entry in sourceOwnership.Entries.ToArray()) sourceOwnership.Set(entry.layerId, entry.anchorCell, string.Empty);
                foreach (var entry in strokeOwnership.Entries) sourceOwnership.Set(entry.layerId, entry.anchorCell, entry.regionId);
                EditorUtility.SetDirty(sourceOwnership);
            }
            layer = sourceLayer; layer.InvalidateLookup();
            int previousRevision = artScene.map.revision; artScene.map.revision++; EditorUtility.SetDirty(artScene.map);
            Undo.CollapseUndoOperations(undoGroup);
            if (build) GridArtBuilder.ApplyAffected(artScene.map, artScene.gameObject.scene, layer, ChangedAnchors, previousRevision, false);
            ReleaseStroke();
            Status = "笔画已提交；Scene、Map 与稀疏归属保持同一 Undo。"; Changed?.Invoke();
        }

        private static void CancelStroke()
        {
            if (!stroking) return;
            stroking = false; changed = false; ReleaseStroke();
            if (GUIUtility.hotControl == controlId) GUIUtility.hotControl = 0;
            Status = "已取消本次美术笔画。"; Changed?.Invoke(); SceneView.RepaintAll();
        }

        private static void ReleaseStroke()
        {
            if (sourceLayer != null) layer = sourceLayer;
            if (strokeMap != null) UnityEngine.Object.DestroyImmediate(strokeMap);
            if (strokeOwnership != null) UnityEngine.Object.DestroyImmediate(strokeOwnership);
            strokeMap = null; strokeOwnership = null; sourceLayer = null; sourceOwnership = null;
            ChangedAnchors.Clear();
        }

        private static void OnUndoRedo()
        {
            if (artScene?.map != null)
            {
                foreach (GridArtLayer mapLayer in artScene.map.layers) mapLayer?.InvalidateLookup();
                if (artScene.map.palette != null && !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(artScene.map)) && !string.IsNullOrEmpty(artScene.gameObject.scene.path))
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
