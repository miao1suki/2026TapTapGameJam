using System;
using System.Collections.Generic;
using System.Linq;
using Project.Rooms;
using Project.GridArt;
using Project.GridArt.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Project.LevelEditor.Editor
{
    internal readonly struct SceneRegionMovePreview
    {
        internal readonly int regions, cells, objects, logicalGroups, attachments, artAnchors;
        internal readonly string error;
        internal bool CanCommit => string.IsNullOrEmpty(error) && regions > 0;
        internal SceneRegionMovePreview(int regions, int cells, int objects, int logicalGroups, int attachments, int artAnchors, string error)
        { this.regions = regions; this.cells = cells; this.objects = objects; this.logicalGroups = logicalGroups; this.attachments = attachments; this.artAnchors = artAnchors; this.error = error; }
        public override string ToString() => CanCommit
            ? $"区域 {regions} · 区域格 {cells} · 对象 {objects} · 逻辑组 {logicalGroups} · 附属物 {attachments} · 已认领美术 {artAnchors}"
            : error;
    }

    internal static class SceneRegionAssembly
    {
        private static readonly string[] ProtectedNames = { "Player", "Gameplay Camera", "Main Camera", "EventSystem", "GameFlow", "UI", "GridArt" };

        internal static SceneRegionMovePreview Preview(RoomLifecycleManager manager, IReadOnlyCollection<string> regionIds, Vector2Int sceneDelta)
        {
            if (manager == null) return new SceneRegionMovePreview(0, 0, 0, 0, 0, 0, "当前 Scene 没有区域管理器。");
            var ids = new HashSet<string>((regionIds ?? Array.Empty<string>()).Where(id => !string.IsNullOrWhiteSpace(id)), StringComparer.Ordinal);
            RoomRegion[] regions = manager.Regions.Where(region => region != null && ids.Contains(region.id)).ToArray();
            if (regions.Length == 0) return new SceneRegionMovePreview(0, 0, 0, 0, 0, 0, "至少选择一个有效房间/通道。");
            if (sceneDelta == Vector2Int.zero) return new SceneRegionMovePreview(0, 0, 0, 0, 0, 0, "移动格数不能为 0。");
            Vector2Int regionDelta = new Vector2Int(sceneDelta.x, -sceneDelta.y);
            var selectedCells = new HashSet<Vector2Int>(regions.SelectMany(region => region.cells ?? new List<Vector2Int>()));
            var otherCells = new HashSet<Vector2Int>(manager.Regions.Where(region => region != null && !ids.Contains(region.id)).SelectMany(region => region.cells ?? new List<Vector2Int>()));
            foreach (Vector2Int cell in selectedCells)
                if (otherCells.Contains(cell + regionDelta)) return new SceneRegionMovePreview(0, 0, 0, 0, 0, 0, $"目标区域格 {cell + regionDelta} 与未选区域重叠。");

            SceneObjectIndex index = SceneObjectAuthoring.Read(manager.gameObject.scene);
            var records = new HashSet<SceneObjectRecord>(index.All.Where(record => ids.Contains(record.regionId) ||
                record.block != null && ids.Contains(manager.FindRegion(record.block.transform.position))));
            foreach (string authorId in records.Select(record => record.authorId).Where(id => !string.IsNullOrEmpty(id)).Distinct().ToArray())
                foreach (SceneObjectRecord member in index.All.Where(record => record.authorId == authorId)) records.Add(member);
            var movedBlocks = new HashSet<LevelEditorPlacedBlock>(records.Select(record => record.block));
            foreach (SceneObjectRecord record in records)
            foreach (Vector2Int cell in record.cells)
            foreach (SceneObjectRecord collision in index.At(cell + sceneDelta, record.channel))
                if (!movedBlocks.Contains(collision.block)) return new SceneRegionMovePreview(0, 0, 0, 0, 0, 0,
                    $"对象 {record.block.name} 移动后会与 {collision.block.name} 在 {cell + sceneDelta} 的 {record.channel} 通道冲突。");

            SceneRegionAttachment[] attachments = FindAll<SceneRegionAttachment>(manager.gameObject.scene).Where(item => ids.Contains(item.RegionId)).ToArray();
            foreach (SceneRegionAttachment attachment in attachments)
                if (ProtectedNames.Any(name => attachment.transform.root.name.Contains(name, StringComparison.OrdinalIgnoreCase)))
                    return new SceneRegionMovePreview(0, 0, 0, 0, 0, 0, $"附属物 {attachment.name} 位于受保护全局根，不能随区域移动。");
            GridArtScene art = FindAll<GridArtScene>(manager.gameObject.scene).FirstOrDefault(item => item.map != null && SceneArtAuthoring.OwnershipFor(item) != null);
            GridArtRegionOwnership ownership = SceneArtAuthoring.OwnershipFor(art);
            int artCount = 0;
            if (art != null)
            {
                if (!Mathf.Approximately(art.map.cellSize, manager.CellSize))
                    return new SceneRegionMovePreview(0, 0, 0, 0, 0, 0, "GridArt 与关卡格尺寸不同，已禁止跨系统整体移动。");
                var moving = ownership.Entries.Where(item => item != null && ids.Contains(item.regionId)).ToArray();
                artCount = moving.Length;
                foreach (GridArtRegionOwnershipEntry entry in moving)
                {
                    GridArtLayer artLayer = art.map.layers.Find(item => item != null && item.id == entry.layerId);
                    if (artLayer == null || artLayer.locked) return new SceneRegionMovePreview(0, 0, 0, 0, 0, 0, "已认领美术所在图层缺失或锁定。");
                    Vector2Int target = entry.anchorCell + sceneDelta;
                    int occupied = artLayer.Find(target);
                    if (occupied >= 0 && !moving.Any(item => item.layerId == entry.layerId && item.anchorCell == target))
                        return new SceneRegionMovePreview(0, 0, 0, 0, 0, 0, $"已认领美术移动后在 {target} 与同层未移动美术冲突。");
                }
            }
            return new SceneRegionMovePreview(regions.Length, selectedCells.Count, records.Count,
                records.Select(record => string.IsNullOrEmpty(record.authorId) ? record.block.GetInstanceID().ToString() : record.authorId).Distinct().Count(), attachments.Length, artCount, string.Empty);
        }

        internal static bool Move(RoomLifecycleManager manager, IReadOnlyCollection<string> regionIds, Vector2Int sceneDelta, out string reason)
        {
            SceneRegionMovePreview preview = Preview(manager, regionIds, sceneDelta);
            reason = preview.error;
            if (!preview.CanCommit) return false;
            var ids = new HashSet<string>(regionIds, StringComparer.Ordinal);
            Scene scene = manager.gameObject.scene;
            SceneObjectIndex index = SceneObjectAuthoring.Read(scene);
            var records = new HashSet<SceneObjectRecord>(index.All.Where(record => ids.Contains(record.regionId) || ids.Contains(manager.FindRegion(record.block.transform.position))));
            foreach (string authorId in records.Select(record => record.authorId).Where(id => !string.IsNullOrEmpty(id)).Distinct().ToArray())
                foreach (SceneObjectRecord member in index.All.Where(record => record.authorId == authorId)) records.Add(member);
            SceneRegionAttachment[] attachments = FindAll<SceneRegionAttachment>(scene).Where(item => ids.Contains(item.RegionId)).ToArray();
            GridArtScene art = FindAll<GridArtScene>(scene).FirstOrDefault(item => item.map != null && SceneArtAuthoring.OwnershipFor(item) != null);
            GridArtRegionOwnership ownership = SceneArtAuthoring.OwnershipFor(art);
            List<RoomRegion> regions = manager.Regions.Where(item => item != null).Select(item => new RoomRegion
            {
                id = item.id, label = item.label, corridor = item.corridor,
                cells = item.cells != null ? new List<Vector2Int>(item.cells) : new List<Vector2Int>(),
            }).ToList();
            Vector2Int regionDelta = new Vector2Int(sceneDelta.x, -sceneDelta.y);
            Vector3 worldDelta = manager.transform.TransformVector(new Vector3(sceneDelta.x * manager.CellSize, sceneDelta.y * manager.CellSize));

            Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("整体移动房间/通道");
            Undo.RegisterCompleteObjectUndo(manager, "整体移动房间/通道");
            if (art != null)
            {
                Undo.RegisterCompleteObjectUndo(art.map, "整体移动房间/通道");
                Undo.RegisterCompleteObjectUndo(ownership, "整体移动房间/通道");
            }
            foreach (RoomRegion region in regions.Where(region => ids.Contains(region.id)))
                for (int i = 0; i < region.cells.Count; i++) region.cells[i] += regionDelta;

            var movedTransforms = new HashSet<Transform>();
            foreach (SceneObjectRecord record in records)
            {
                if (record.block == null || !movedTransforms.Add(record.block.transform)) continue;
                RoomResetObject reset = record.block.GetComponent<RoomResetObject>();
                Undo.RecordObjects(reset != null ? new UnityEngine.Object[] { record.block.transform, record.block, reset } : new UnityEngine.Object[] { record.block.transform, record.block }, "整体移动房间/通道");
                record.block.transform.position += worldDelta;
                record.block.SetAuthoredCell(record.block.Cell + sceneDelta);
                EditorUtility.SetDirty(record.block); if (reset != null) EditorUtility.SetDirty(reset);
            }
            foreach (SceneRegionAttachment attachment in attachments)
            {
                if (attachment == null || !movedTransforms.Add(attachment.transform)) continue;
                Undo.RecordObject(attachment.transform, "整体移动房间/通道");
                attachment.transform.position += worldDelta;
            }
            if (art != null)
            {
                GridArtRegionOwnershipEntry[] owned = ownership.Entries.Where(item => item != null && ids.Contains(item.regionId)).ToArray();
                foreach (GridArtRegionOwnershipEntry entry in owned)
                {
                    GridArtLayer targetLayer = art.map.layers.Find(item => item != null && item.id == entry.layerId);
                    GridArtCell cell = targetLayer?.cells.Find(item => item.position == entry.anchorCell);
                    if (cell != null) cell.position += sceneDelta;
                    entry.anchorCell += sceneDelta;
                    targetLayer?.InvalidateLookup();
                }
                if (owned.Length > 0)
                {
                    art.map.revision++; EditorUtility.SetDirty(art.map); EditorUtility.SetDirty(ownership);
                }
            }
            manager.ConfigureAuthoredRegions(regions, manager.CellSize);
            EditorUtility.SetDirty(manager); EditorSceneManager.MarkSceneDirty(scene);
            Undo.CollapseUndoOperations(undo); SceneObjectAuthoring.NotifyChanged(); SceneView.RepaintAll();
            if (art != null && preview.artAnchors > 0 && !string.IsNullOrEmpty(scene.path)) GridArtBuilder.Apply(art.map, scene, false, false);
            reason = $"已整体移动：{preview}";
            return true;
        }

        private static IEnumerable<T> FindAll<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (T item in root.GetComponentsInChildren<T>(true)) yield return item;
        }
    }

    [CustomEditor(typeof(SceneRegionAttachment))]
    internal sealed class SceneRegionAttachmentEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            var title = new Label("区域附属物"); title.style.unityFontStyleAndWeight = FontStyle.Bold; root.Add(title);
            root.Add(new HelpBox("仅显式标记的非网格装饰会随区域整体移动；玩家、相机、UI、管理器和 GridArt 根不能认领。", HelpBoxMessageType.Info));
            SerializedProperty region = serializedObject.FindProperty("regionId");
            root.Add(new PropertyField(region, "所属区域稳定 ID"));
            return root;
        }
    }
}
