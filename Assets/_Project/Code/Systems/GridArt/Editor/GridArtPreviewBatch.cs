using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Project.GridArt.Editor
{
    // Retained geometry: moving the viewport changes the parent transform, not every tile vertex.
    internal sealed class GridArtPreviewBatch : VisualElement
    {
        internal const float Units = 32;
        internal readonly Rect Bounds;
        private readonly List<(GridArtQuad quad, Color color)> items;
        private readonly int begin, count;
        private bool visible = true;

        internal GridArtPreviewBatch(List<(GridArtQuad, Color)> source, int first, int length)
        {
            items = source; begin = first; count = length;
            var bounds = source[first].Item1.bounds;
            for (int i = first + 1; i < first + length; i++)
            {
                Rect r = source[i].Item1.bounds;
                bounds = Rect.MinMaxRect(Mathf.Min(bounds.xMin, r.xMin), Mathf.Min(bounds.yMin, r.yMin),
                    Mathf.Max(bounds.xMax, r.xMax), Mathf.Max(bounds.yMax, r.yMax));
            }
            Bounds = bounds;
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.left = bounds.xMin * Units; style.top = -bounds.yMax * Units;
            style.width = bounds.width * Units; style.height = bounds.height * Units;
            generateVisualContent += Draw;
        }

        internal void SetVisible(bool value)
        {
            if (visible == value) return;
            visible = value; style.display = value ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void Draw(MeshGenerationContext context)
        {
            var mesh = context.Allocate(count * 4, count * 6, items[begin].quad.texture);
            for (int j = 0; j < count; j++)
            {
                var item = items[begin + j]; Rect r = item.quad.bounds, uv = item.quad.uv;
                float x0 = (r.xMin - Bounds.xMin) * Units, x1 = (r.xMax - Bounds.xMin) * Units;
                float y0 = (Bounds.yMax - r.yMax) * Units, y1 = (Bounds.yMax - r.yMin) * Units;
                mesh.SetNextVertex(V(x0, y0, uv.xMin, uv.yMax, item.color));
                mesh.SetNextVertex(V(x1, y0, uv.xMax, uv.yMax, item.color));
                mesh.SetNextVertex(V(x1, y1, uv.xMax, uv.yMin, item.color));
                mesh.SetNextVertex(V(x0, y1, uv.xMin, uv.yMin, item.color));
                ushort i = (ushort)(j * 4);
                mesh.SetNextIndex(i); mesh.SetNextIndex((ushort)(i + 1)); mesh.SetNextIndex((ushort)(i + 2));
                mesh.SetNextIndex((ushort)(i + 2)); mesh.SetNextIndex((ushort)(i + 3)); mesh.SetNextIndex(i);
            }
        }
        private static Vertex V(float x, float y, float u, float v, Color color) => new Vertex
        { position = new Vector3(x, y, Vertex.nearZ), uv = new Vector2(u, v), tint = color };
    }
}
