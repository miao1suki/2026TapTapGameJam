using UnityEngine;
using UnityEngine.UI;

namespace Project.GameFlow
{
    public sealed class EscWheelSectorGraphic :
        MaskableGraphic,
        ICanvasRaycastFilter
    {
        [SerializeField] private float startAngle = -36f;
        [SerializeField] private float endAngle = 36f;
        [SerializeField] private float innerRadius = 125f;
        [SerializeField] private float outerRadius = 360f;
        [SerializeField, Min(4)] private int segments = 24;

        public void Configure(
            float valueStartAngle,
            float valueEndAngle,
            float valueInnerRadius,
            float valueOuterRadius)
        {
            startAngle = valueStartAngle;
            endAngle = valueEndAngle;
            innerRadius = valueInnerRadius;
            outerRadius = valueOuterRadius;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            for (int index = 0; index < segments; index++)
            {
                float t0 = index / (float)segments;
                float t1 = (index + 1) / (float)segments;
                float a0 = Mathf.Lerp(startAngle, endAngle, t0) *
                           Mathf.Deg2Rad;
                float a1 = Mathf.Lerp(startAngle, endAngle, t1) *
                           Mathf.Deg2Rad;

                Vector2 inner0 = new Vector2(
                    Mathf.Cos(a0),
                    Mathf.Sin(a0)) * innerRadius;
                Vector2 outer0 = new Vector2(
                    Mathf.Cos(a0),
                    Mathf.Sin(a0)) * outerRadius;
                Vector2 inner1 = new Vector2(
                    Mathf.Cos(a1),
                    Mathf.Sin(a1)) * innerRadius;
                Vector2 outer1 = new Vector2(
                    Mathf.Cos(a1),
                    Mathf.Sin(a1)) * outerRadius;

                AddTriangle(
                    vertexHelper,
                    inner0,
                    outer0,
                    outer1);
                AddTriangle(
                    vertexHelper,
                    inner0,
                    outer1,
                    inner1);
            }
        }

        public bool IsRaycastLocationValid(
            Vector2 screenPoint,
            Camera eventCamera)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rectTransform,
                    screenPoint,
                    eventCamera,
                    out Vector2 localPoint))
            {
                return false;
            }

            float radius = localPoint.magnitude;
            if (radius < innerRadius || radius > outerRadius)
            {
                return false;
            }

            float angle = Mathf.Atan2(
                localPoint.y,
                localPoint.x) * Mathf.Rad2Deg;
            angle = Mathf.Repeat(angle, 360f);
            float start = Mathf.Repeat(startAngle, 360f);
            float end = Mathf.Repeat(endAngle, 360f);
            return start <= end
                ? angle >= start && angle <= end
                : angle >= start || angle <= end;
        }

        private void AddTriangle(
            VertexHelper vertexHelper,
            Vector2 a,
            Vector2 b,
            Vector2 c)
        {
            int start = vertexHelper.currentVertCount;
            vertexHelper.AddVert(
                new UIVertex
                {
                    position = a,
                    color = color,
                    uv0 = Vector2.zero,
                });
            vertexHelper.AddVert(
                new UIVertex
                {
                    position = b,
                    color = color,
                    uv0 = Vector2.zero,
                });
            vertexHelper.AddVert(
                new UIVertex
                {
                    position = c,
                    color = color,
                    uv0 = Vector2.zero,
                });
            vertexHelper.AddTriangle(
                start,
                start + 1,
                start + 2);
        }
    }
}
