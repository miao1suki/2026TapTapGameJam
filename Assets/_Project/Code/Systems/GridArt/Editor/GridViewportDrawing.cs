using UnityEngine;
using UnityEngine.UIElements;

namespace Project.GridArt.Editor
{
    /// <summary>Display-only grid LOD. Editing coordinates always retain their original unit size.</summary>
    public static class GridViewportDrawing
    {
        public const float MinimumSpacing = 16f;
        public const int LineBudget = 320;

        public static int CellStride(float cellPixels, Rect viewport)
        {
            if (!(cellPixels > 0) || !Finite(cellPixels) || !Finite(viewport.width) || !Finite(viewport.height)) return 0;
            float minimum = Mathf.Max(MinimumSpacing, (viewport.width + viewport.height) / (LineBudget - 4));
            int stride = 1;
            while (cellPixels * stride < minimum && stride < 1 << 28) stride *= 2;
            return cellPixels * stride >= minimum ? stride : 0;
        }

        public static int LineCount(float spacing, float min, float max, float origin)
        {
            if (!(spacing > 0) || !(max > min)) return 0;
            float first = origin + Mathf.Ceil((min - origin) / spacing) * spacing;
            return Mathf.Max(0, Mathf.FloorToInt((max - first) / spacing) + 1);
        }

        public static void Draw(MeshGenerationContext context, Rect viewport, Vector2 pan, float cellPixels, Color color)
        {
            if (!(viewport.width > 0 && viewport.height > 0) || !Finite(pan.x) || !Finite(pan.y)) return;
            int stride = CellStride(cellPixels, viewport);
            if (stride == 0) return;
            float spacing = cellPixels * stride;
            int nx = LineCount(spacing, viewport.xMin, viewport.xMax, pan.x);
            int ny = LineCount(spacing, viewport.yMin, viewport.yMax, pan.y);
            if (nx + ny == 0) return;
            var mesh = context.Allocate((nx + ny) * 4, (nx + ny) * 6);
            int index = 0;
            for (int axis = 0; axis < 2; axis++)
            {
                float origin = axis == 0 ? pan.x : pan.y;
                float min = axis == 0 ? viewport.xMin : viewport.yMin;
                float firstCell = Mathf.Ceil((min - origin) / spacing);
                int count = axis == 0 ? nx : ny;
                for (int i = 0; i < count; i++)
                {
                    float position = origin + (firstCell + i) * spacing;
                    Color tint = color;
                    // Major lines stay aligned to the world origin, including negative coordinates.
                    if (Mathf.Abs((firstCell + i) % 8) > .1f) tint.a *= .45f;
                    Rect line = axis == 0
                        ? Rect.MinMaxRect(Mathf.Max(viewport.xMin, position - .5f), viewport.yMin,
                            Mathf.Min(viewport.xMax, position + .5f), viewport.yMax)
                        : Rect.MinMaxRect(viewport.xMin, Mathf.Max(viewport.yMin, position - .5f),
                            viewport.xMax, Mathf.Min(viewport.yMax, position + .5f));
                    mesh.SetNextVertex(V(line.xMin, line.yMin, tint)); mesh.SetNextVertex(V(line.xMax, line.yMin, tint));
                    mesh.SetNextVertex(V(line.xMax, line.yMax, tint)); mesh.SetNextVertex(V(line.xMin, line.yMax, tint));
                    ushort n = (ushort)(index++ * 4);
                    mesh.SetNextIndex(n); mesh.SetNextIndex((ushort)(n + 1)); mesh.SetNextIndex((ushort)(n + 2));
                    mesh.SetNextIndex((ushort)(n + 2)); mesh.SetNextIndex((ushort)(n + 3)); mesh.SetNextIndex(n);
                }
            }
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static Vertex V(float x, float y, Color color) => new Vertex
        { position = new Vector3(x, y, Vertex.nearZ), tint = color };
    }
}
