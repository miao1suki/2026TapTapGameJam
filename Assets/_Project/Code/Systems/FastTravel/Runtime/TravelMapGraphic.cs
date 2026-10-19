using System;
using System.Collections.Generic;
using Project.Rooms;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Project.FastTravel
{
    /// <summary>Cached row runs, not one UI object / draw call per world cell.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TravelMapGraphic : MaskableGraphic, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler,
        IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        public readonly struct Span
        {
            public readonly Rect Rect;
            public readonly Color Color;
            public Span(Rect rect, Color color) { Rect = rect; Color = color; }
        }
        private readonly List<Span> spans = new List<Span>();
        private readonly Dictionary<int, Vector2> pointers = new Dictionary<int, Vector2>(2);
        private bool multiplePointers;
        public Vector2 Center { get; private set; }
        public float Scale { get; private set; } = 4f;
        public Rect MapBounds { get; private set; }
        public event Action ViewChanged;
        public event Action<Vector2> PointClicked;
        public int CachedSpanCount => spans.Count;

        public void Configure(IReadOnlyList<RoomRegion> regions)
        {
            spans.Clear();
            bool first = true;
            Vector2 min = default, max = default;
            for (int i = 0; i < regions.Count; i++)
            {
                RoomRegion room = regions[i];
                if (room?.cells == null) continue;
                var cells = new List<Vector2Int>(new HashSet<Vector2Int>(room.cells));
                cells.Sort((a, b) => a.y != b.y ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));
                Color fill = room.corridor ? new Color(.17f, .23f, .29f) : Color.HSVToRGB((i * .137f) % 1f, .22f, .46f);
                for (int c = 0; c < cells.Count;)
                {
                    Vector2Int start = cells[c++]; int end = start.x + 1;
                    while (c < cells.Count && cells[c].y == start.y && cells[c].x == end) { end++; c++; }
                    Rect rect = Rect.MinMaxRect(start.x, -start.y - 1, end, -start.y);
                    spans.Add(new Span(rect, fill));
                    min = first ? rect.min : Vector2.Min(min, rect.min);
                    max = first ? rect.max : Vector2.Max(max, rect.max);
                    first = false;
                }
            }
            MapBounds = first ? new Rect(-1f, -1f, 2f, 2f) : Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            Fit();
        }

        public void Fit()
        {
            Center = MapBounds.center;
            Vector2 size = rectTransform.rect.size - Vector2.one * 80f;
            Scale = Mathf.Clamp(Mathf.Min(size.x / Mathf.Max(1f, MapBounds.width), size.y / Mathf.Max(1f, MapBounds.height)), .2f, 60f);
            NotifyView();
        }
        public Vector2 Project(Vector2 point) => (point - Center) * Scale;
        public Vector2 Unproject(Vector2 point) => Center + point / Scale;
        public void Zoom(float factor, Vector2 localPivot)
        {
            if (float.IsNaN(factor) || float.IsInfinity(factor) || factor <= 0f) return;
            Vector2 before = Center + localPivot / Scale;
            Scale = Mathf.Clamp(Scale * factor, .2f, 60f);
            Center = before - localPivot / Scale;
            NotifyView();
        }
        public void OnPointerDown(PointerEventData data)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, data.position, data.pressEventCamera, out Vector2 local);
            if (pointers.Count == 0) multiplePointers = false;
            pointers[data.pointerId] = local;
            if (pointers.Count > 1) multiplePointers = true;
        }
        public void OnPointerUp(PointerEventData data) => pointers.Remove(data.pointerId);
        public void OnEndDrag(PointerEventData data) => pointers.Remove(data.pointerId);
        public void OnPointerClick(PointerEventData data)
        {
            if (multiplePointers || data.dragging || data.button != PointerEventData.InputButton.Left) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, data.position, data.pressEventCamera, out Vector2 local);
            PointClicked?.Invoke(Unproject(local));
        }
        public void OnBeginDrag(PointerEventData data) { }
        public void OnDrag(PointerEventData data)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, data.position, data.pressEventCamera, out Vector2 current);
            if (pointers.Count > 1 && pointers.TryGetValue(data.pointerId, out Vector2 old))
            {
                Vector2 other = default;
                foreach (var pair in pointers) if (pair.Key != data.pointerId) { other = pair.Value; break; }
                float before = Vector2.Distance(old, other), after = Vector2.Distance(current, other);
                Vector2 oldMidpoint = (old + other) * .5f, midpoint = (current + other) * .5f;
                // Use event-system pointer positions, not a second device input
                // path. Pinch and midpoint pan preserve the touched map point.
                Vector2 world = Unproject(oldMidpoint);
                if (before > 1f && after > 1f) Scale = Mathf.Clamp(Scale * after / before, .2f, 60f);
                Center = world - midpoint / Scale; pointers[data.pointerId] = current;
                ClampCenter(); NotifyView(); return;
            }
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, data.position - data.delta, data.pressEventCamera, out Vector2 previous);
            pointers[data.pointerId] = current;
            Center -= (current - previous) / Scale;
            ClampCenter(); NotifyView();
        }
        private void ClampCenter()
        {
            // Keep some of the world reachable; never fling the entire map irretrievably offscreen.
            Center = new Vector2(Mathf.Clamp(Center.x, MapBounds.xMin - MapBounds.width * .5f, MapBounds.xMax + MapBounds.width * .5f),
                Mathf.Clamp(Center.y, MapBounds.yMin - MapBounds.height * .5f, MapBounds.yMax + MapBounds.height * .5f));
        }
        protected override void OnDisable() { pointers.Clear(); multiplePointers = false; base.OnDisable(); }
        public void OnScroll(PointerEventData data)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, data.position, data.enterEventCamera, out Vector2 local);
            Zoom(Mathf.Pow(1.15f, data.scrollDelta.y), local);
        }
        private void NotifyView() { SetVerticesDirty(); ViewChanged?.Invoke(); }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect clip = rectTransform.rect;
            foreach (Span span in spans)
            {
                Vector2 min = Project(span.Rect.min), max = Project(span.Rect.max);
                if (max.x < clip.xMin || min.x > clip.xMax || max.y < clip.yMin || min.y > clip.yMax) continue;
                min = Vector2.Max(min, clip.min); max = Vector2.Min(max, clip.max);
                int v = vh.currentVertCount;
                if (v > 64000) break;
                vh.AddVert(new Vector3(min.x, min.y), span.Color, Vector2.zero);
                vh.AddVert(new Vector3(min.x, max.y), span.Color, Vector2.zero);
                vh.AddVert(new Vector3(max.x, max.y), span.Color, Vector2.zero);
                vh.AddVert(new Vector3(max.x, min.y), span.Color, Vector2.zero);
                vh.AddTriangle(v, v + 1, v + 2); vh.AddTriangle(v + 2, v + 3, v);
            }
        }
    }
}
