using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.WorldVisibility
{
    /// <summary>Bounded four-neighbour flood fill. Walls block links, not lines of sight.
    /// Buffers are retained; contiguous visible cells become one quad per row run.</summary>
    public sealed class ConnectedSpaceGeometry : IDisposable
    {
        public Mesh Mesh { get; private set; }
        public int ReachableCells { get; private set; }
        public int BuildCount { get; private set; }
        byte[] links = Array.Empty<byte>();
        bool[] visible = Array.Empty<bool>();
        int[] queue = Array.Empty<int>();
        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<Vector2> uv = new List<Vector2>();
        readonly List<int> indices = new List<int>();
        readonly List<Color32> colors = new List<Color32>();
        float[] brightness = Array.Empty<float>();
        bool snapshot;
        Vector2 rangeCentre;
        float rangeRadius;
        int width;
        float step;
        Vector2 minimum;

        public void Build(Vector2 origin, float radius, float spacing, IReadOnlyList<VisibilityGeometry.Segment> edges)
        {
            step = Mathf.Max(.01f, spacing);
            // Global grid: sub-cell motion does not invalidate the topology.
            var seed = new Vector2Int(Mathf.FloorToInt(origin.x / step), Mathf.FloorToInt(origin.y / step));
            int extent = Mathf.CeilToInt(radius / step) + 1;
            width = extent * 2 + 1;
            int count = width * width;
            if (links.Length != count) { links = new byte[count]; visible = new bool[count]; queue = new int[count]; }
            else { Array.Clear(links, 0, count); Array.Clear(visible, 0, count); }
            minimum = new Vector2(seed.x - extent + .5f, seed.y - extent + .5f) * step;
            foreach (var edge in edges) Rasterize(edge);
            int start = extent * width + extent, head = 0, tail = 1;
            queue[0] = start; visible[start] = true;
            // Centre the range on the cached seed cell, preventing edge shimmer during sub-cell motion.
            Vector2 centre = minimum + new Vector2(extent, extent) * step;
            while (head < tail)
            {
                int id = queue[head++], x = id % width, y = id / width;
                Visit(x + 1, y, (links[id] & 1) != 0);
                Visit(x - 1, y, x == 0 || (links[id - 1] & 1) != 0);
                Visit(x, y + 1, (links[id] & 2) != 0);
                Visit(x, y - 1, y == 0 || (links[id - width] & 2) != 0);
            }
            ReachableCells = tail; BuildCount++;
            snapshot = false; rangeCentre = centre; rangeRadius = radius;
            vertices.Clear(); uv.Clear(); indices.Clear(); colors.Clear();
            for (int y = 0; y < width; y++)
                for (int x = 0; x < width; x++)
                {
                    if (!visible[y * width + x]) continue;
                    int left = x;
                    while (x + 1 < width && visible[y * width + x + 1]) x++;
                    Quad(minimum + new Vector2(left - .5f, y - .5f) * step,
                        minimum + new Vector2(x + .5f, y + .5f) * step, centre, radius, 1);
                }
            Upload();

            void Visit(int x, int y, bool blocked)
            {
                if (blocked || x < 0 || y < 0 || x >= width || y >= width) return;
                int id = y * width + x;
                if (visible[id] || (minimum + new Vector2(x, y) * step - centre).sqrMagnitude > radius * radius) return;
                visible[id] = true; queue[tail++] = id;
            }
        }

        // Preserve the displayed blend, not the previous target, when a new
        // topology arrives before the current fade has finished.
        public void CaptureBlend(ConnectedSpaceGeometry previous, ConnectedSpaceGeometry current, float blend)
        {
            step = current.step;
            minimum = current.minimum;
            Vector2 maximum = minimum + Vector2.one * (current.width - 1) * step;
            if (previous.Mesh && Mathf.Approximately(previous.step, step))
            {
                Vector2 lo = Vector2.Min(minimum, previous.minimum);
                Vector2 hi = Vector2.Max(maximum, previous.minimum + Vector2.one * (previous.width - 1) * step);
                // Bound storage for distant cross-world teleports.
                if (Mathf.Max(hi.x - lo.x, hi.y - lo.y) / step < 384)
                { minimum = lo; maximum = hi; }
            }
            width = Mathf.CeilToInt(Mathf.Max(maximum.x - minimum.x, maximum.y - minimum.y) / step) + 1;
            int count = width * width;
            if (brightness.Length != count) brightness = new float[count];
            vertices.Clear(); uv.Clear(); indices.Clear(); colors.Clear();
            blend = Mathf.Clamp01(blend);
            for (int y = 0; y < width; y++)
                for (int x = 0; x < width; x++)
                {
                    Vector2 position = minimum + new Vector2(x, y) * step;
                    brightness[y * width + x] = Mathf.Lerp(previous.SampleBrightness(position), current.SampleBrightness(position), blend);
                }
            snapshot = true;
            for (int y = 0; y < width; y++)
                for (int x = 0; x < width; x++)
                {
                    byte value = (byte)Mathf.RoundToInt(Mathf.Clamp01(brightness[y * width + x]) * 255);
                    if (value == 0) continue;
                    int left = x;
                    while (x + 1 < width && Mathf.RoundToInt(brightness[y * width + x + 1] * 255) == value) x++;
                    Quad(minimum + new Vector2(left - .5f, y - .5f) * step,
                        minimum + new Vector2(x + .5f, y + .5f) * step, Vector2.zero, 1, value / 255f);
                }
            Upload();
        }

        public float SampleBrightness(Vector2 position)
        {
            if (!Mesh || width == 0) return 0;
            int x = Mathf.RoundToInt((position.x - minimum.x) / step), y = Mathf.RoundToInt((position.y - minimum.y) / step);
            if (x < 0 || y < 0 || x >= width || y >= width) return 0;
            if (snapshot) return brightness[y * width + x];
            if (!visible[y * width + x]) return 0;
            float t = Mathf.Clamp01((Vector2.Distance(position, rangeCentre) / rangeRadius - .85f) / .15f);
            return 1 - t * t * (3 - 2 * t);
        }

        void Upload()
        {
            Mesh ??= new Mesh { name = "局部连通空间遮罩", hideFlags = HideFlags.HideAndDontSave, indexFormat = IndexFormat.UInt32 };
            Mesh.MarkDynamic(); Mesh.Clear(); Mesh.SetVertices(vertices); Mesh.SetUVs(0, uv); Mesh.SetColors(colors);
            Mesh.SetTriangles(indices, 0); Mesh.RecalculateBounds();
        }

        void Rasterize(VisibilityGeometry.Segment edge)
        {
            Vector2 lo = (Vector2.Min(edge.a, edge.b) - minimum) / step;
            Vector2 hi = (Vector2.Max(edge.a, edge.b) - minimum) / step;
            // Check only grid links in the segment bounding box (not every edge against every cell).
            int x0 = Mathf.Clamp(Mathf.FloorToInt(lo.x) - 1, 0, width - 1);
            int x1 = Mathf.Clamp(Mathf.CeilToInt(hi.x), 0, width - 1);
            int y0 = Mathf.Clamp(Mathf.FloorToInt(lo.y) - 1, 0, width - 1);
            int y1 = Mathf.Clamp(Mathf.CeilToInt(hi.y), 0, width - 1);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    Vector2 p = minimum + new Vector2(x, y) * step;
                    if (Crosses(p, Vector2.right, edge)) links[y * width + x] |= 1;
                    if (Crosses(p, Vector2.up, edge)) links[y * width + x] |= 2;
                }
        }
        bool Crosses(Vector2 p, Vector2 direction, VisibilityGeometry.Segment edge)
        {
            // Use a limit larger than the tested link to distinguish a hit at its endpoint from no hit.
            return VisibilityGeometry.RayDistance(p, direction, edge, step * 2) <= step + .00001f;
        }
        void Quad(Vector2 lo, Vector2 hi, Vector2 centre, float radius, float value)
        {
            int start = vertices.Count;
            Add(lo); Add(new Vector2(hi.x, lo.y)); Add(hi); Add(new Vector2(lo.x, hi.y));
            indices.Add(start); indices.Add(start + 1); indices.Add(start + 2);
            indices.Add(start); indices.Add(start + 2); indices.Add(start + 3);
            void Add(Vector2 p)
            {
                vertices.Add(p); uv.Add(snapshot ? Vector2.zero : (p - centre) / radius);
                byte intensity = (byte)Mathf.RoundToInt(value * 255); colors.Add(new Color32(intensity, intensity, intensity, 255));
            }
        }
        public void Dispose()
        {
            if (Mesh) { if (Application.isPlaying) UnityEngine.Object.Destroy(Mesh); else UnityEngine.Object.DestroyImmediate(Mesh); }
            Mesh = null;
        }
    }
}
