using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.WorldVisibility
{
    /// <summary>XY segment visibility. Geometry is independent of room IDs and art Z-depth.</summary>
    public static class VisibilityGeometry
    {
        public struct Segment
        {
            public Vector2 a, b;
            public Segment(Vector2 start, Vector2 end) { a = start; b = end; }
        }
        public static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
        public static float RayDistance(Vector2 origin, Vector2 direction, Segment edge, float limit)
        {
            Vector2 side = edge.b - edge.a;
            float denominator = Cross(direction, side);
            if (Mathf.Abs(denominator) < .000001f) return limit;
            Vector2 relative = edge.a - origin;
            float distance = Cross(relative, side) / denominator;
            float along = Cross(relative, direction) / denominator;
            return distance >= -.00001f && along >= -.00001f && along <= 1.00001f
                ? Mathf.Min(limit, Mathf.Max(0, distance)) : limit;
        }
        public static void AddBox(BoxCollider box, List<Segment> result)
        {
            if (!box || !box.enabled || box.isTrigger || !box.gameObject.activeInHierarchy) return;
            var half = box.size * .5f;
            Vector3 c = box.center;
            // Gameplay walls rotate only in XY. Project their real four corners, never merged AABBs.
            Vector2 a = box.transform.TransformPoint(c + new Vector3(-half.x, -half.y, 0));
            Vector2 b = box.transform.TransformPoint(c + new Vector3(half.x, -half.y, 0));
            Vector2 d = box.transform.TransformPoint(c + new Vector3(-half.x, half.y, 0));
            Vector2 e = box.transform.TransformPoint(c + new Vector3(half.x, half.y, 0));
            result.Add(new Segment(a,b)); result.Add(new Segment(b,e));
            result.Add(new Segment(e,d)); result.Add(new Segment(d,a));
        }

        /// <summary>Cancel opposing shared edges, then join collinear runs. No occupancy AABB fill.</summary>
        public static void Compact(List<Segment> edges)
        {
            var unique = new Dictionary<(Vector2Int,Vector2Int),(Segment edge,int orientation)>();
            foreach (var edge in edges)
            {
                var a = new Vector2Int(Mathf.RoundToInt(edge.a.x*10000),Mathf.RoundToInt(edge.a.y*10000));
                var b = new Vector2Int(Mathf.RoundToInt(edge.b.x*10000),Mathf.RoundToInt(edge.b.y*10000));
                bool forward = a.x < b.x || a.x == b.x && a.y < b.y;
                var key = forward ? (a,b) : (b,a); int sign = forward ? 1 : -1;
                if (unique.TryGetValue(key,out var previous))
                {
                    if (previous.orientation != sign) unique[key] = (edge,0);
                }
                else unique.Add(key,(edge,sign));
            }
            edges.Clear();
            var lines = new Dictionary<(bool,int),List<Vector2>>();
            foreach (var value in unique.Values)
            {
                if (value.orientation == 0) continue;
                var edge = value.edge;
                bool horizontal = Mathf.Abs(edge.a.y-edge.b.y) < .00001f;
                bool vertical = Mathf.Abs(edge.a.x-edge.b.x) < .00001f;
                if (!horizontal && !vertical) { edges.Add(edge); continue; }
                var key = (horizontal,Mathf.RoundToInt((horizontal ? edge.a.y : edge.a.x)*10000));
                if (!lines.TryGetValue(key,out var intervals)) lines.Add(key,intervals = new List<Vector2>());
                float a = horizontal ? edge.a.x : edge.a.y, b = horizontal ? edge.b.x : edge.b.y;
                intervals.Add(new Vector2(Mathf.Min(a,b),Mathf.Max(a,b)));
            }
            foreach (var line in lines)
            {
                var intervals = line.Value; intervals.Sort((a,b) => a.x.CompareTo(b.x));
                Vector2 run = intervals[0];
                for (int i = 1; i < intervals.Count; i++)
                {
                    if (intervals[i].x <= run.y + .00001f) run.y = Mathf.Max(run.y,intervals[i].y);
                    else { Add(run); run = intervals[i]; }
                }
                Add(run);
                void Add(Vector2 value)
                {
                    float fixedAxis = line.Key.Item2 / 10000f;
                    edges.Add(line.Key.Item1 ? new Segment(new Vector2(value.x,fixedAxis),new Vector2(value.y,fixedAxis))
                        : new Segment(new Vector2(fixedAxis,value.x),new Vector2(fixedAxis,value.y)));
                }
            }
        }

        public sealed class Polygon : IDisposable
        {
            public Mesh Mesh { get; private set; }
            readonly List<float> angles = new List<float>(1024);
            readonly List<Vector3> vertices = new List<Vector3>(1024);
            readonly List<Vector2> uv = new List<Vector2>(1024);
            readonly List<Color> colors = new List<Color>(1024);
            readonly List<int> triangles = new List<int>(3072);
            public int RayCount => angles.Count;
            public void Build(Vector2 origin, float radius, IReadOnlyList<Segment> segments, Color color, int roundRays = 128)
            {
                radius = Mathf.Max(.01f,radius);
                angles.Clear(); vertices.Clear(); uv.Clear(); colors.Clear(); triangles.Clear();
                for (int i = 0; i < roundRays; i++) angles.Add(i * (Mathf.PI * 2f / roundRays));
                for (int i = 0; i < segments.Count; i++)
                {
                    AddAngle(segments[i].a); AddAngle(segments[i].b);
                }
                angles.Sort();
                vertices.Add(origin); uv.Add(Vector2.zero); colors.Add(color);
                float previous = float.NegativeInfinity;
                foreach (float angle in angles)
                {
                    if (angle - previous < .000005f) continue;
                    previous = angle;
                    Vector2 direction = new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
                    float distance = radius;
                    for (int i = 0; i < segments.Count; i++) distance = RayDistance(origin,direction,segments[i],distance);
                    Vector2 delta = direction * distance;
                    vertices.Add(origin + delta); uv.Add(delta / radius); colors.Add(color);
                }
                for (int i = 1; i < vertices.Count; i++)
                { triangles.Add(0); triangles.Add(i); triangles.Add(i == vertices.Count - 1 ? 1 : i + 1); }
                Mesh ??= new Mesh { name = "二维光照可见多边形", hideFlags = HideFlags.HideAndDontSave };
                Mesh.MarkDynamic(); Mesh.Clear(); Mesh.SetVertices(vertices); Mesh.SetUVs(0,uv);
                Mesh.SetColors(colors); Mesh.SetTriangles(triangles,0); Mesh.RecalculateBounds();
                void AddAngle(Vector2 point)
                {
                    Vector2 delta = point - origin;
                    if (delta.sqrMagnitude > radius * radius || delta.sqrMagnitude < .0000001f) return;
                    float angle = Mathf.Atan2(delta.y,delta.x);
                    for (int offset = -1; offset <= 1; offset++)
                        angles.Add(Mathf.Repeat(angle + offset * .0001f,Mathf.PI * 2f));
                }
            }
            public void Dispose()
            {
                if (Mesh) { if (Application.isPlaying) UnityEngine.Object.Destroy(Mesh); else UnityEngine.Object.DestroyImmediate(Mesh); }
                Mesh = null;
            }
        }
    }
}
