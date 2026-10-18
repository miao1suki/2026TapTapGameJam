using System.Collections.Generic;
using Project.BlockFeatures;
using Project.LevelEditor;
using Project.LevelEditor.Editor;
using UnityEngine;

namespace PlanningEditorPrototype
{
    internal static class PlanningWaterLayerUtility
    {
        internal static bool IsWater(PlanningBox box, LevelEditorPalette palette = null)
        {
            if (box == null) return false;
            if (box.type == PlanningDetailType.Water) return true;
            if (box.type != PlanningDetailType.Prop) return false;
            palette ??= LevelEditorState.Palette;
            if (palette == null) return false;
            foreach (var entry in palette.PropEntries)
                if (entry != null && !string.IsNullOrEmpty(box.propEntryId) && entry.EntryId == box.propEntryId)
                    return entry.Prefab != null && entry.Prefab.GetComponent<WaterVisualFeature>() != null;
            foreach (var entry in palette.PropEntries)
                if (entry != null && !string.IsNullOrEmpty(box.propEntryName) && entry.DisplayName == box.propEntryName)
                    return entry.Prefab != null && entry.Prefab.GetComponent<WaterVisualFeature>() != null;
            return false;
        }

        internal static string WaterKind(PlanningBox box, LevelEditorPalette palette = null)
        {
            if (box == null) return null;
            if (box.type == PlanningDetailType.Water) return typeof(WaterSourceFeature).FullName;
            if (box.type != PlanningDetailType.Prop) return null;
            palette ??= LevelEditorState.Palette;
            if (palette == null) return null;
            foreach (var entry in palette.PropEntries)
                if (entry != null && !string.IsNullOrEmpty(box.propEntryId) && entry.EntryId == box.propEntryId)
                    return entry.Prefab != null ? entry.Prefab.GetComponent<WaterVisualFeature>()?.GetType().FullName : null;
            foreach (var entry in palette.PropEntries)
                if (entry != null && !string.IsNullOrEmpty(box.propEntryName) && entry.DisplayName == box.propEntryName)
                    return entry.Prefab != null ? entry.Prefab.GetComponent<WaterVisualFeature>()?.GetType().FullName : null;
            return null;
        }

        internal static HashSet<Vector2Int> Layer(Dictionary<string, HashSet<Vector2Int>> layers, string kind)
        {
            if (!layers.TryGetValue(kind, out var cells)) layers.Add(kind, cells = new HashSet<Vector2Int>());
            return cells;
        }

        internal static bool IsLava(PlanningBox box, LevelEditorPalette palette = null)
        {
            if (box == null || box.type != PlanningDetailType.Prop) return false;
            palette ??= LevelEditorState.Palette;
            if (palette == null) return false;
            foreach (var entry in palette.PropEntries)
                if (entry != null && !string.IsNullOrEmpty(box.propEntryId) && entry.EntryId == box.propEntryId)
                    return entry.Prefab != null && entry.Prefab.GetComponent<LavaHazardFeature>() != null;
            foreach (var entry in palette.PropEntries)
                if (entry != null && !string.IsNullOrEmpty(box.propEntryName) && entry.DisplayName == box.propEntryName)
                    return entry.Prefab != null && entry.Prefab.GetComponent<LavaHazardFeature>() != null;
            return false;
        }

        // Water has its own occupancy layer. Every foreground family may coexist with it;
        // lava is the only cross-layer exclusion, in either placement order.
        internal static bool Excludes(PlanningBox a, PlanningBox b, LevelEditorPalette palette) =>
            IsWater(a, palette) && IsLava(b, palette) || IsLava(a, palette) && IsWater(b, palette);

        internal static Vector2Int Origin(PlanningDocument document, PlanningRoom room)
        {
            if (room == null) return Vector2Int.zero;
            if (room.isConnector && !room.independentCells)
            {
                var path = PlanningLayoutUtility.GetConnectorAssemblyPath(document.rooms, room,
                    document.worldBlockCellWidth, document.worldBlockCellHeight);
                if (path.Count > 0) return path[0];
            }
            var position = PlanningWorldUtility.Bounds(room).position;
            return new Vector2Int(position.x * Mathf.Max(1, document.worldBlockCellWidth),
                position.y * Mathf.Max(1, document.worldBlockCellHeight));
        }

        internal static IEnumerable<(PlanningBox box, Vector2Int origin)> WorldBoxes(PlanningDocument document)
        {
            foreach (var room in document.rooms)
            {
                var origin = Origin(document, room);
                foreach (var box in room.boxes) yield return (box, origin);
            }
            foreach (var box in document.assemblyPatches) yield return (box, Vector2Int.zero);
        }

        internal static bool CanPlace(PlanningDocument document, PlanningRoom owner, PlanningBox candidate,
            Vector2Int localPosition, LevelEditorPalette palette, PlanningBox ignored = null)
        {
            if (document == null || candidate == null) return true;
            bool water = IsWater(candidate, palette), lava = IsLava(candidate, palette);
            if (!water && !lava) return true;
            Vector2Int candidateOffset = Origin(document, owner) + localPosition - new Vector2Int(candidate.x, candidate.y);
            foreach (var item in WorldBoxes(document))
            {
                if (ReferenceEquals(item.box, ignored) || ReferenceEquals(item.box, candidate)) continue;
                if (!(water ? IsLava(item.box, palette) : IsWater(item.box, palette))) continue;
                foreach (var a in PlanningShapeUtility.Rectangles(candidate))
                {
                    var worldA = a; worldA.position += candidateOffset;
                    foreach (var b in PlanningShapeUtility.Rectangles(item.box))
                    {
                        var worldB = b; worldB.position += item.origin;
                        if (worldA.Overlaps(worldB)) return false;
                    }
                }
            }
            return true;
        }

        internal static bool TryValidate(IEnumerable<(PlanningBox box, Vector2Int origin)> items,
            LevelEditorPalette palette, out string message)
        {
            var water = new HashSet<Vector2Int>();
            var lava = new HashSet<Vector2Int>();
            foreach (var item in items)
            {
                bool isWater = IsWater(item.box, palette);
                if (!isWater && !IsLava(item.box, palette)) continue;
                var own = isWater ? water : lava;
                var opposite = isWater ? lava : water;
                foreach (var local in PlanningShapeUtility.Cells(item.box))
                {
                    var cell = local + item.origin;
                    if (opposite.Contains(cell))
                    {
                        message = $"水与岩浆不能放在同一格（{cell.x}，{cell.y}）；请移动其中之一。其他物体可与水叠放。";
                        return false;
                    }
                    own.Add(cell);
                }
            }
            message = null;
            return true;
        }
    }
}
