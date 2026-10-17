using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace PlanningEditorPrototype
{
    // mergeParts is already part of the saved format. Old rectangular merges stay rectangular;
    // new irregular merges use the same snapshots without introducing a map migration.
    internal static class PlanningShapeUtility
    {
        private sealed class Shape
        {
            internal int x, y, width, height;
            internal List<string> source;
            internal HashSet<Vector2Int> cells;
            internal readonly List<RectInt> rectangles = new List<RectInt>();
        }
        private static ConditionalWeakTable<PlanningBox, Shape> cache = new ConditionalWeakTable<PlanningBox, Shape>();
        internal static void Invalidate() => cache = new ConditionalWeakTable<PlanningBox, Shape>();

        internal static IReadOnlyList<RectInt> Rectangles(PlanningBox box)
        {
            Shape shape = cache.GetValue(box, _ => new Shape());
            if (shape.rectangles.Count > 0 && shape.x == box.x && shape.y == box.y &&
                shape.width == box.width && shape.height == box.height && shape.source == box.mergeParts)
                return shape.rectangles;
            shape.x = box.x; shape.y = box.y; shape.width = box.width; shape.height = box.height;
            shape.source = box.mergeParts; shape.rectangles.Clear(); shape.cells = null;
            var parts = new List<PlanningBox>();
            int originX = int.MaxValue, originY = int.MaxValue;
            if (box.isMerged && box.mergeParts != null)
                foreach (string json in box.mergeParts)
                {
                    PlanningBox part = PlanningBox.FromPersistentJson(json);
                    if (part == null) continue;
                    parts.Add(part); originX = Mathf.Min(originX, part.x); originY = Mathf.Min(originY, part.y);
                }
            if (parts.Count == 0)
            {
                shape.rectangles.Add(new RectInt(box.x, box.y, box.width, box.height));
                return shape.rectangles;
            }
            var cells = new HashSet<Vector2Int>();
            shape.cells = cells;
            foreach (var part in parts)
                foreach (RectInt rect in Rectangles(part))
                    foreach (Vector2Int p in rect.allPositionsWithin)
                    {
                        Vector2Int cell = p + new Vector2Int(box.x - originX, box.y - originY);
                        if (new RectInt(box.x, box.y, box.width, box.height).Contains(cell)) cells.Add(cell);
                    }
            // Compress horizontal spans vertically. Geometry is rebuilt on edits, never on pan.
            var spans = new List<RectInt>();
            foreach (Vector2Int cell in cells)
            {
                if (cells.Contains(cell + Vector2Int.left)) continue;
                int end = cell.x + 1;
                while (cells.Contains(new Vector2Int(end, cell.y))) end++;
                spans.Add(new RectInt(cell.x, cell.y, end - cell.x, 1));
            }
            spans.Sort((a, b) => a.y != b.y ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));
            var previous = new Dictionary<(int, int), int>();
            foreach (RectInt span in spans)
            {
                var key = (span.x, span.width);
                if (previous.TryGetValue(key, out int i) && shape.rectangles[i].yMax == span.y)
                { var rect = shape.rectangles[i]; rect.height++; shape.rectangles[i] = rect; }
                else { previous[key] = shape.rectangles.Count; shape.rectangles.Add(span); }
            }
            return shape.rectangles;
        }

        internal static bool Contains(PlanningBox box, Vector2Int cell)
        {
            var rectangles = Rectangles(box);
            var shape = cache.GetValue(box, _ => new Shape());
            return shape.cells != null ? shape.cells.Contains(cell) : rectangles[0].Contains(cell);
        }
        internal static bool Overlaps(PlanningBox box, RectInt area)
        {
            foreach (RectInt rect in Rectangles(box)) if (rect.Overlaps(area)) return true;
            return false;
        }
        internal static IEnumerable<Vector2Int> Cells(PlanningBox box)
        {
            foreach (RectInt rect in Rectangles(box)) foreach (Vector2Int cell in rect.allPositionsWithin) yield return cell;
        }
        internal static bool IsIrregular(PlanningBox box)
        {
            var rectangles = Rectangles(box);
            return rectangles.Count != 1 || rectangles[0] != new RectInt(box.x, box.y, box.width, box.height);
        }
    }
}
