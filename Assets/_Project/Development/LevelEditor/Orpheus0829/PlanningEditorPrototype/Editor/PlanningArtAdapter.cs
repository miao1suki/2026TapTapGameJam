using System.IO;
using Project.GridArt.Editor;
using Project.LevelEditor;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PlanningEditorPrototype
{
    [InitializeOnLoad]
    internal static class PlanningArtAdapter
    {
        static PlanningArtAdapter() { GridArtLevelSource.Read = Read; GridArtColorSource.Read = ReadColors; }
        private static System.Collections.Generic.IReadOnlyList<GridArtColorDefinition> ReadColors()
        {
            var result = new System.Collections.Generic.List<GridArtColorDefinition>();
            var catalog = Resources.Load<Project.ColorBlocks.ColorCatalog>("ColorBlocks/ColorCatalog");
            if (catalog != null) foreach (var color in catalog.Colors)
                if (color != null) result.Add(new GridArtColorDefinition(color.id,color.displayName,color.unityLayer));
            return result;
        }
        internal static GridArtLevelSnapshot Read(Scene scene)
        {
            var snapshot = new GridArtLevelSnapshot();
            var document = PlanningEditorWindow.DocumentForScene(scene.handle);
            if (document != null) snapshot.label = "关卡编辑器 · 当前草稿";
            else
            {
                string path = PlanningSharedSaveUtility.SceneSavePath(scene.path);
                if (path != null && File.Exists(path))
                { document = PlanningSaveStore.Read(path); snapshot.label = "本关规划存档"; }
            }
            if (document != null)
            {
                snapshot.cellSize = 1; // Unified planning world/detail grid.
                foreach (var room in document.rooms)
                {
                    var group = new GridArtLevelGroup { id = room.id, label = (room.isConnector ? "通道 · " : "房间 · ") + room.name };
                    foreach (var box in room.boxes)
                    {
                        if (!string.IsNullOrEmpty(box.propEntryId) || !string.IsNullOrEmpty(box.propEntryName) || box.type != PlanningDetailType.Solid) continue;
                        var rect = PlanningLayoutUtility.GetAssemblyBoxRect(room, box, document.worldBlockCellWidth, document.worldBlockCellHeight);
                        Vector2Int offset = rect.position - new Vector2Int(box.x,box.y);
                        foreach (RectInt part in PlanningShapeUtility.Rectangles(box))
                            group.bounds.Add(new Rect(part.x+offset.x, -part.yMax-offset.y, part.width, part.height));
                    }
                    snapshot.groups.Add(group);
                }
                var patches = new GridArtLevelGroup { id = "assembly", label = "装配补丁" };
                foreach (var box in document.assemblyPatches)
                    if (box.type == PlanningDetailType.Solid && string.IsNullOrEmpty(box.propEntryId) && string.IsNullOrEmpty(box.propEntryName))
                        foreach (RectInt part in PlanningShapeUtility.Rectangles(box))
                            patches.bounds.Add(new Rect(part.x, -part.yMax, part.width, part.height));
                if (patches.bounds.Count > 0) snapshot.groups.Add(patches);
                return snapshot;
            }
            var sceneGroup = new GridArtLevelGroup { id = "scene", label = "场景白块" };
            foreach (var root in scene.GetRootGameObjects())
                foreach (var block in root.GetComponentsInChildren<LevelEditorPlacedBlock>(true))
                {
                    if (block.IsProp) continue;
                    float size = block.CellWorldSize; snapshot.cellSize = size;
                    var shape = block.GetComponent<LevelEditorMergedShape>();
                    if (shape == null) sceneGroup.bounds.Add(new Rect((Vector2)block.Cell*size, (Vector2)block.SizeCells*size));
                    else foreach (RectInt part in shape.Rectangles)
                        sceneGroup.bounds.Add(new Rect(new Vector2(block.Cell.x+part.x,block.Cell.y+block.SizeCells.y-part.yMax)*size,(Vector2)part.size*size));
                }
            snapshot.groups.Add(sceneGroup); return snapshot;
        }
    }
}
