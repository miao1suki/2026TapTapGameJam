using System.Collections.Generic;
using Project.BlockFeatures;
using Project.BlockFeatures.Editor;
using Project.LevelEditor;
using Project.LevelEditor.Editor;
using UnityEngine;

namespace PlanningEditorPrototype
{
    public sealed partial class PlanningCanvas
    {
        private readonly Dictionary<string, PlanningArtworkPreview> vinePreviewSlices = new();
        private readonly Dictionary<string, Dictionary<Vector2Int, PlanningArtworkPreview>> vinePreviewCellSlices = new();
        private readonly HashSet<object> vinePreviewScopes = new();

        private void ClearVinePreviewGroups()
        {
            vinePreviewSlices.Clear();
            vinePreviewCellSlices.Clear();
            vinePreviewScopes.Clear();
        }

        private bool TryResolveVinePreview(PlanningBox box, out PlanningArtworkPreview artwork)
        {
            if (vinePreviewSlices.TryGetValue(box.id, out artwork)) return true;
            var entry = FindPropEntry(box.propEntryId, box.propEntryName);
            if (entry?.Prefab == null || entry.Prefab.GetComponent<ClimbableVineFeature>() == null) return false;
            IList<PlanningBox> siblings = document.assemblyPatches;
            foreach (var room in document.rooms)
                if (room.boxes.Contains(box)) { siblings = room.boxes; break; }
            if (vinePreviewScopes.Add(siblings)) BuildVinePreviewGroups(siblings);
            return vinePreviewSlices.TryGetValue(box.id, out artwork);
        }

        private bool TryAddVinePreview(PlanningBox box, RectInt rect, HashSet<string> active, Vector2Int offset)
        {
            if (!vinePreviewCellSlices.TryGetValue(box.id, out var slices)) return false;
            Color tint = mergeSelection.Contains(box.id) && LevelEditorState.Tool == LevelEditorTool.Merge
                ? new Color(.55f, .85f, 1f) : Color.white;
            var visible = rect;
            if (!buildingRetainedImages && contentRect.width > 0f && contentRect.height > 0f)
            {
                Rect target = CellRect(rect.x, rect.y, rect.width, rect.height);
                int left = Mathf.Max(0, Mathf.FloorToInt((contentRect.xMin - target.xMin) / GridSize));
                int top = Mathf.Max(0, Mathf.FloorToInt((contentRect.yMin - target.yMin) / GridSize));
                int right = Mathf.Min(rect.width, Mathf.CeilToInt((contentRect.xMax - target.xMin) / GridSize));
                int bottom = Mathf.Min(rect.height, Mathf.CeilToInt((contentRect.yMax - target.yMin) / GridSize));
                visible = new RectInt(rect.x + left, rect.y + top, Mathf.Max(0, right - left), Mathf.Max(0, bottom - top));
            }
            foreach (var cell in visible.allPositionsWithin)
            {
                if (!slices.TryGetValue(cell - offset, out var artwork)) continue;
                Rect target = CellRect(cell.x, cell.y, 1, 1);
                Vector2 size = artwork.Cells * GridSize;
                Vector2 center = new(target.center.x, target.yMax - size.y * .5f);
                AddArtworkImage(box.id + ":vine:" + cell.x + ":" + cell.y, artwork,
                    new Rect(center - size * .5f, size), artwork.UV, tint, objectPreviewLayer, active);
            }
            return true;
        }

        private void BuildVinePreviewGroups(IList<PlanningBox> siblings)
        {
            var cells = new Dictionary<Vector2Int, (PlanningBox box, GameObject prefab,
                ClimbableVineFeature feature, IReadOnlyList<LevelEditorComponentValueOverride> values)>();
            foreach (var box in siblings)
            {
                if (box.type != PlanningDetailType.Prop ||
                    (box.isMerged || box.singleInstance) && (box.width != 1 || box.height != 1)) continue;
                var entry = FindPropEntry(box.propEntryId, box.propEntryName);
                var feature = entry?.Prefab != null ? entry.Prefab.GetComponent<ClimbableVineFeature>() : null;
                // Exactly the same grouping eligibility as PlanningSceneBuilder:
                // Brush rectangles are expanded to one-cell mothers by the builder,
                // too. Their boundaries must not restart tips or stretch one image.
                if (feature == null || feature is LadderSonFeature || feature.GrowthSegmentPrefab == null ||
                    feature.GrowthSegmentPrefab.GetComponent<LadderSonFeature>() == null) continue;
                foreach (var cell in PlanningShapeUtility.Cells(box))
                    cells.TryAdd(cell, (box, entry.Prefab, feature,
                        box.hasComponentOverrides ? box.componentOverrides : entry.ComponentValueOverrides));
            }
            foreach (var pair in cells)
            {
                if (cells.TryGetValue(pair.Key + Vector2Int.up, out var lower) && lower.prefab == pair.Value.prefab) continue;
                int rows = 1;
                while (cells.TryGetValue(pair.Key + Vector2Int.down * rows, out var upper) && upper.prefab == pair.Value.prefab) rows++;
                if (!ObjectArtworkPlanningPreview.TryGetVine(pair.Value.feature, new Vector2(1, rows),
                    pair.Value.values, rows > 1 ? 1f : pair.Value.feature.AuthoredArtworkGrowthProgress, out var root)) continue;
                for (int row = 0; row < rows; row++)
                {
                    var member = cells[pair.Key + Vector2Int.down * row].box;
                    var slice = ObjectArtworkPlanningPreview.VineSlice(root, row, rows);
                    if (!vinePreviewCellSlices.TryGetValue(member.id, out var slices))
                        vinePreviewCellSlices[member.id] = slices = new Dictionary<Vector2Int, PlanningArtworkPreview>();
                    slices[pair.Key + Vector2Int.down * row] = slice;
                    vinePreviewSlices.TryAdd(member.id, slice);
                }
            }
        }
    }
}
