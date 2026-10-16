using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace PlanningEditorPrototype
{
    public sealed partial class PlanningCanvas
    {
        private int previewPaletteRevision = -1;
        private readonly Dictionary<string, Texture2D> boxPreviewCache = new Dictionary<string, Texture2D>();
        private readonly HashSet<string> pendingBoxPreviews = new HashSet<string>();
        private readonly List<string> stalePreviewKeys = new List<string>();
        private VisualElement waterPreviewLayer, objectPreviewLayer;
        private readonly Dictionary<PlanningRoom, PlanningOwnerPreview> ownerPreviewCache = new Dictionary<PlanningRoom, PlanningOwnerPreview>();
        private readonly HashSet<string> activePreviewKeys = new HashSet<string>();

        private PlanningOwnerPreview OwnerPreview(PlanningRoom room)
        {
            if (!ownerPreviewCache.TryGetValue(room, out var preview) || preview.Source != room.cells || preview.Count != room.cells.Count)
                ownerPreviewCache[room] = preview = new PlanningOwnerPreview(room);
            return preview;
        }

        private void DrawOwnerPreview(UnityEngine.UIElements.Painter2D painter, PlanningRoom room,
            Vector2 offset, Vector2 scale, Color fill, Color outline)
        {
            var preview = OwnerPreview(room);
            var bounds = preview.Bounds;
            var screen = new Rect(WorldToScreen(Vector2.Scale((Vector2)bounds.position + offset, scale)),
                Vector2.Scale(bounds.size, scale) * GridSize);
            if (!screen.Overlaps(contentRect)) return;
            foreach (var r in preview.Rectangles)
            {
                var rect = new Rect(WorldToScreen(Vector2.Scale((Vector2)r.position + offset, scale)), Vector2.Scale(r.size, scale) * GridSize);
                if (!rect.Overlaps(contentRect)) continue;
                // Clip before tessellation: an almost fully off-screen room must stay cheap.
                rect = Rect.MinMaxRect(Mathf.Max(rect.xMin, contentRect.xMin), Mathf.Max(rect.yMin, contentRect.yMin),
                    Mathf.Min(rect.xMax, contentRect.xMax), Mathf.Min(rect.yMax, contentRect.yMax));
                FillRect(painter, rect, fill, 0);
            }
            painter.strokeColor = outline; painter.lineWidth = 1; painter.BeginPath();
            foreach (var edge in preview.Edges)
            {
                Vector2 a = WorldToScreen(Vector2.Scale(edge.from + offset, scale));
                Vector2 b = WorldToScreen(Vector2.Scale(edge.to + offset, scale));
                if (Mathf.Max(a.x, b.x) < contentRect.xMin || Mathf.Min(a.x, b.x) > contentRect.xMax ||
                    Mathf.Max(a.y, b.y) < contentRect.yMin || Mathf.Min(a.y, b.y) > contentRect.yMax) continue;
                a = Vector2.Max(contentRect.min, Vector2.Min(contentRect.max, a));
                b = Vector2.Max(contentRect.min, Vector2.Min(contentRect.max, b));
                painter.MoveTo(a); painter.LineTo(b);
            }
            painter.Stroke();
        }

        private void BuildPreviewLayers()
        {
            waterPreviewLayer = new VisualElement { pickingMode = PickingMode.Ignore };
            objectPreviewLayer = new VisualElement { pickingMode = PickingMode.Ignore };
            waterPreviewLayer.StretchToParentSize(); objectPreviewLayer.StretchToParentSize();
            Add(waterPreviewLayer); Add(objectPreviewLayer);
        }

        private void InvalidatePreviewData()
        {
            boxPreviewCache.Clear(); pendingBoxPreviews.Clear();
            ownerPreviewCache.Clear();
        }

        private void RemoveUnusedPreviewImages(HashSet<string> active)
        {
            stalePreviewKeys.Clear();
            foreach (var pair in detailPreviewImages)
                if (!active.Contains(pair.Key)) stalePreviewKeys.Add(pair.Key);
            foreach (string key in stalePreviewKeys)
            {
                detailPreviewImages[key].RemoveFromHierarchy();
                detailPreviewImages.Remove(key);
            }
        }

        private bool PreviewVisible(RectInt rect)
        {
            // Unattached verification canvases have no viewport yet.
            if (!(contentRect.width > 0f && contentRect.height > 0f)) return true;
            return CellRect(rect.x, rect.y, rect.width, rect.height).Overlaps(contentRect);
        }
    }
}
