using System.Collections.Generic;
using UnityEngine;

namespace PlanningEditorPrototype
{
    /// <summary>Exact fill rectangles and outer/hole edges, rebuilt only after document edits.</summary>
    internal sealed class PlanningOwnerPreview
    {
        internal readonly List<RectInt> Rectangles = new List<RectInt>();
        internal readonly List<(Vector2 from, Vector2 to)> Edges = new List<(Vector2, Vector2)>();
        internal readonly RectInt Bounds;
        internal readonly List<PlanningCell> Source;
        internal readonly int Count;

        internal PlanningOwnerPreview(PlanningRoom room)
        {
            Source = room.cells; Count = Source.Count;
            Bounds = PlanningWorldUtility.Bounds(room);
            var cells = new HashSet<Vector2Int>();
            foreach (var c in Source) cells.Add(new Vector2Int(c.x, c.y));
            var spans = new List<RectInt>();
            var horizontal = new Dictionary<int, List<int>>();
            var vertical = new Dictionary<int, List<int>>();
            foreach (var c in cells)
            {
                if (!cells.Contains(c + Vector2Int.left))
                {
                    int end = c.x + 1;
                    while (cells.Contains(new Vector2Int(end, c.y))) end++;
                    spans.Add(new RectInt(c.x, c.y, end - c.x, 1));
                    AddEdge(vertical, c.x, c.y);
                }
                if (!cells.Contains(c + Vector2Int.right)) AddEdge(vertical, c.x + 1, c.y);
                if (!cells.Contains(c + Vector2Int.down)) AddEdge(horizontal, c.y, c.x);
                if (!cells.Contains(c + Vector2Int.up)) AddEdge(horizontal, c.y + 1, c.x);
            }
            spans.Sort((a, b) => a.y != b.y ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));
            var previous = new Dictionary<(int x, int width), int>();
            foreach (var span in spans)
            {
                var key = (span.x, span.width);
                if (previous.TryGetValue(key, out int i) && Rectangles[i].yMax == span.y)
                { var r = Rectangles[i]; r.height++; Rectangles[i] = r; }
                else { previous[key] = Rectangles.Count; Rectangles.Add(span); }
            }
            MergeEdges(horizontal, false); MergeEdges(vertical, true);
        }

        private static void AddEdge(Dictionary<int, List<int>> lines, int axis, int start)
        {
            if (!lines.TryGetValue(axis, out var values)) lines[axis] = values = new List<int>();
            values.Add(start);
        }
        private void MergeEdges(Dictionary<int, List<int>> lines, bool vertical)
        {
            foreach (var line in lines)
            {
                var positions = line.Value; positions.Sort();
                for (int i = 0; i < positions.Count;)
                {
                    int start = positions[i++], end = start + 1;
                    while (i < positions.Count && positions[i] == end) { end++; i++; }
                    Edges.Add(vertical
                        ? (new Vector2(line.Key, start), new Vector2(line.Key, end))
                        : (new Vector2(start, line.Key), new Vector2(end, line.Key)));
                }
            }
        }
    }
}
