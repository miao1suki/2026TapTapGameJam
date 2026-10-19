using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Project.GridArt.Editor
{
    /// <summary>
    /// Non-interactive level underlay. Source changes build retained, texture/spatial
    /// batches; navigation changes only one transform and batch visibility.
    /// </summary>
    public sealed class GridArtLevelReference : VisualElement
    {
        private const float ChunkSize = 16f;
        private const int BatchSize = 256;
        private readonly VisualElement geometry = new VisualElement { pickingMode = PickingMode.Ignore, usageHints = UsageHints.DynamicTransform };
        private readonly List<GridArtPreviewBatch> batches = new();
        private Rect viewport;
        private Vector2 pan;
        private float pixelsPerWorldUnit = 32;
        private bool shown = true;
        private float alpha = .35f;
        public int QuadCount { get; private set; }
        public int BatchCount => batches.Count;
        public int VisibleBatchCount { get; private set; }
        public int GeometryRevision { get; private set; }

        public GridArtLevelReference()
        {
            name = "level-readonly-reference";
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.left = style.top = style.right = style.bottom = 0;
            style.overflow = Overflow.Hidden;
            geometry.style.position = Position.Absolute;
            geometry.style.left = geometry.style.top = 0;
            geometry.style.width = geometry.style.height = 0;
            geometry.style.transformOrigin = new TransformOrigin(0, 0);
            Add(geometry);
            SetReferenceVisibility(true, .35f);
        }

        public void Configure(GridArtLevelSnapshot snapshot)
        {
            geometry.Clear(); batches.Clear(); QuadCount = 0; GeometryRevision++;
            var buckets = new Dictionary<(int layer, int texture, int x, int y), List<(GridArtQuad, Color)>>();
            var orderedRuns = new List<(int layer, int texture, int x, int y, List<(GridArtQuad, Color)> values)>();
            void AddQuad(GridArtQuad quad, Color tint, int layer)
            {
                if (quad.texture == null || quad.bounds.width <= 0 || quad.bounds.height <= 0) return;
                var key = (layer, quad.texture.GetInstanceID(), Mathf.FloorToInt(quad.bounds.center.x / ChunkSize),
                    Mathf.FloorToInt(quad.bounds.center.y / ChunkSize));
                // Natural-size artwork can extend outside its occupied cell.
                // Preserve transparent foreground painter order, only batching
                // consecutive compatible items. Deduplicated water/walls may sort.
                if (layer >= (int)GridArtReferenceLayer.Object)
                {
                    int last = orderedRuns.Count - 1;
                    if (last < 0 || orderedRuns[last].layer != layer || orderedRuns[last].texture != key.Item2 ||
                        orderedRuns[last].x != key.Item3 || orderedRuns[last].y != key.Item4)
                        orderedRuns.Add((layer, key.Item2, key.Item3, key.Item4, new()));
                    orderedRuns[orderedRuns.Count - 1].values.Add((quad, tint)); QuadCount++; return;
                }
                if (!buckets.TryGetValue(key, out var values)) buckets.Add(key, values = new());
                values.Add((quad, tint)); QuadCount++;
            }
            if (snapshot != null)
            {
                // Only four strips per room, never per-cell grid strokes.
                foreach (var room in snapshot.rooms)
                {
                    Rect r = room.boundsWorld; const float edge = 1f / 16f;
                    Color tint = new(.31f, .57f, .85f, .55f);
                    AddQuad(new GridArtQuad(new Rect(r.x, r.y, r.width, edge), new Rect(0, 0, 1, 1), Texture2D.whiteTexture), tint, -1);
                    AddQuad(new GridArtQuad(new Rect(r.x, r.yMax - edge, r.width, edge), new Rect(0, 0, 1, 1), Texture2D.whiteTexture), tint, -1);
                    AddQuad(new GridArtQuad(new Rect(r.x, r.y, edge, r.height), new Rect(0, 0, 1, 1), Texture2D.whiteTexture), tint, -1);
                    AddQuad(new GridArtQuad(new Rect(r.xMax - edge, r.y, edge, r.height), new Rect(0, 0, 1, 1), Texture2D.whiteTexture), tint, -1);
                }
                foreach (var item in snapshot.referenceItems) AddQuad(item.quad, item.tint, (int)item.layer);
                // Existing delegate providers can still supply walls without artwork.
                if (snapshot.referenceItems.Count == 0)
                    foreach (var group in snapshot.groups) foreach (var rect in group.bounds)
                        AddQuad(new GridArtQuad(rect, new Rect(0, 0, 1, 1), Texture2D.whiteTexture), new Color(.76f, .8f, .87f), 1);
            }
            var keys = new List<(int layer, int texture, int x, int y)>(buckets.Keys);
            keys.Sort((a, b) => a.layer != b.layer ? a.layer.CompareTo(b.layer) :
                a.texture != b.texture ? a.texture.CompareTo(b.texture) : a.y != b.y ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));
            foreach (var key in keys)
            {
                AddBatches(buckets[key]);
            }
            for (int layer = (int)GridArtReferenceLayer.Object; layer <= (int)GridArtReferenceLayer.EditorOverlay; layer++)
                foreach (var run in orderedRuns) if (run.layer == layer) AddBatches(run.values);
            Cull();

            void AddBatches(List<(GridArtQuad, Color)> values)
            {
                for (int i = 0; i < values.Count; i += BatchSize)
                {
                    var batch = new GridArtPreviewBatch(values, i, Mathf.Min(BatchSize, values.Count - i));
                    batches.Add(batch); geometry.Add(batch);
                }
            }
        }

        /// <param name="pan">Canvas pixel position of world (0,0).</param>
        /// <param name="pixelsPerWorldUnit">Canvas zoom divided by the art map's cell world size.</param>
        public void SetView(Vector2 pan, float pixelsPerWorldUnit, Rect viewport)
        {
            this.pan = pan; this.pixelsPerWorldUnit = Mathf.Max(.0001f, pixelsPerWorldUnit); this.viewport = viewport;
            geometry.style.translate = new Translate(pan.x, pan.y);
            geometry.style.scale = new Scale(Vector3.one * (this.pixelsPerWorldUnit / GridArtPreviewBatch.Units));
            Cull();
        }

        public void SetReferenceVisibility(bool visible, float opacity)
        {
            float next = Mathf.Clamp01(opacity);
            if (shown != visible) { shown = visible; style.display = shown ? DisplayStyle.Flex : DisplayStyle.None; }
            if (!Mathf.Approximately(alpha, next) || resolvedStyle.opacity != next) { alpha = next; style.opacity = alpha; }
            Cull();
        }

        private void Cull()
        {
            VisibleBatchCount = 0;
            bool hasViewport = viewport.width > 0 && viewport.height > 0;
            Rect world = new((viewport.xMin - pan.x) / pixelsPerWorldUnit, (pan.y - viewport.yMax) / pixelsPerWorldUnit,
                viewport.width / pixelsPerWorldUnit, viewport.height / pixelsPerWorldUnit);
            foreach (var batch in batches)
            {
                bool visible = shown && alpha > 0 && (!hasViewport || batch.Bounds.Overlaps(world));
                batch.SetVisible(visible); if (visible) VisibleBatchCount++;
            }
        }
    }
}
