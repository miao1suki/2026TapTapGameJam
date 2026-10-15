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
