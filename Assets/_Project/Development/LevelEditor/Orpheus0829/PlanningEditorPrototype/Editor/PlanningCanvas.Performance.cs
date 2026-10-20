using System.Collections.Generic;
using Project.LevelEditor.Editor;
using UnityEngine;
using UnityEngine.UIElements;

namespace PlanningEditorPrototype
{
    public sealed partial class PlanningCanvas
    {
        private static readonly Unity.Profiling.ProfilerMarker DrawAssemblyMarker = new("PlanningCanvas.DrawAssembly");
        private static readonly Unity.Profiling.ProfilerMarker DrawDetailMarker = new("PlanningCanvas.DrawDetail");
        private static readonly Unity.Profiling.ProfilerMarker DrawLinksMarker = new("PlanningCanvas.DrawLinks");
        private static readonly Unity.Profiling.ProfilerMarker DrawMechanismLinksMarker = new("PlanningCanvas.DrawMechanismLinks");
        private static readonly Unity.Profiling.ProfilerMarker DrawTimelineRangesMarker = new("PlanningCanvas.DrawTimelineRanges");
        private static readonly Unity.Profiling.ProfilerMarker DrawOwnerMarker = new("PlanningCanvas.DrawOwner");
        private readonly Dictionary<(string, string), Project.LevelEditor.LevelEditorPropEntry> previewPropEntries = new();
        private int previewPaletteRevision = -1;
        private readonly Dictionary<string, Texture2D> boxPreviewCache = new Dictionary<string, Texture2D>();
        private readonly HashSet<string> pendingBoxPreviews = new HashSet<string>();
        private readonly List<string> stalePreviewKeys = new List<string>();
        private VisualElement waterPreviewLayer, objectPreviewLayer, triggerPreviewLayer;
        private Image triggerPlacementPreview;
        private readonly Dictionary<PlanningRoom, PlanningOwnerPreview> ownerPreviewCache = new Dictionary<PlanningRoom, PlanningOwnerPreview>();
        private readonly HashSet<string> activePreviewKeys = new HashSet<string>();
        private VisualElement blockPreviewLayer;
        private bool blockBatchesValid;
        private PlanningCanvasMode blockBatchMode;
        private string blockBatchRoom;
        private int blockBatchSelection;
        private readonly List<BlockPreviewBatch> blockBatches = new();
        private bool retainedImagesValid, buildingRetainedImages;
        private PlanningCanvasMode retainedMode;
        private string retainedRoom;
        private bool retainedWater;
        private bool retainedAssembly;
        private int retainedSelection;
        private Vector2 retainedPan;
        private float retainedZoom;
        public int PreviewGeometryRevision { get; private set; }
        private readonly Dictionary<(GameObject, System.Type), Component> previewComponents = new();
        private readonly Dictionary<PlanningBox, bool> previewWaterKinds = new();
        private readonly Dictionary<PlanningBox, Project.BlockFeatures.MovingPlatformPath> previewPlatformPaths = new();
        private bool previewLinksValid;
        private readonly List<(PlanningRoom room, List<PlanningBox> boxes, PlanningBox box)> previewMechanisms = new(), previewTimelines = new(), previewDoorButtons = new();
        private readonly Dictionary<string, (PlanningRoom room, PlanningBox box)> previewLinksById = new();

        private void EnsurePreviewLinks()
        {
            if (previewLinksValid) return;
            previewMechanisms.Clear(); previewTimelines.Clear(); previewDoorButtons.Clear(); previewLinksById.Clear();
            foreach (var item in AllBoxes())
            {
                if (!string.IsNullOrEmpty(item.box.id)) previewLinksById[item.box.id] = (item.room, item.box);
                if (!string.IsNullOrEmpty(item.box.doorOwnerId)) previewDoorButtons.Add(item);
                var prefab = FindPropEntry(item.box.propEntryId, item.box.propEntryName)?.Prefab;
                if (PreviewComponent<Project.BlockFeatures.MovingPlatformFeature>(prefab) != null ||
                    PreviewComponent<Project.BlockFeatures.CrankPlatformFeature>(prefab) != null) previewMechanisms.Add(item);
                if (PreviewComponent<Project.BlockFeatures.ContactTimelineTrigger>(prefab) != null) previewTimelines.Add(item);
            }
            previewLinksValid = true;
        }

        private T PreviewComponent<T>(GameObject prefab) where T : Component
        {
            if (prefab == null) return null;
            var key = (prefab, typeof(T));
            if (!previewComponents.TryGetValue(key, out var value))
            {
                prefab.TryGetComponent<T>(out var component);
                previewComponents[key] = value = component;
            }
            return value as T;
        }

        private bool IsPreviewWater(PlanningBox box)
        {
            if (!previewWaterKinds.TryGetValue(box, out bool result))
                previewWaterKinds[box] = result = PlanningWaterLayerUtility.IsWater(box, palette);
            return result;
        }

        // Water, mechanisms and native-size artwork are retained too. Rewriting
        // thousands of Image layout properties on every mouse move makes Yoga /
        // UI Toolkit rebuild the entire visual tree, even when wall meshes cache.
        private bool UpdateRetainedImageView()
        {
            int selection = 0;
            if (LevelEditorState.Tool == LevelEditorTool.Merge)
                foreach (string id in mergeSelection) selection ^= id.GetHashCode();
            bool valid = retainedImagesValid && retainedMode == mode && retainedRoom == selectedRoomId &&
                retainedWater == editWaterLayer && retainedSelection == selection && retainedAssembly == UsesAssemblyPreviewCoordinates && movingBox == null;
            if (valid)
            {
                float ratio = zoom / retainedZoom;
                Vector2 offset = pan - retainedPan * ratio;
                SetImageLayerView(waterPreviewLayer, offset, ratio);
                SetImageLayerView(objectPreviewLayer, offset, ratio);
                SetImageLayerView(triggerPreviewLayer, offset, ratio);
                return true;
            }
            SetImageLayerView(waterPreviewLayer, Vector2.zero, 1);
            SetImageLayerView(objectPreviewLayer, Vector2.zero, 1);
            SetImageLayerView(triggerPreviewLayer, Vector2.zero, 1);
            // Canonical geometry is always built at 26 UI points per world cell.
            // It must not bake a tiny zoom's rounded layout into later close-ups.
            retainedPan = Vector2.zero; retainedZoom = 1; retainedMode = mode;
            retainedRoom = selectedRoomId; retainedWater = editWaterLayer; retainedSelection = selection;
            retainedAssembly = UsesAssemblyPreviewCoordinates;
            return false;
        }

        private static void SetImageLayerView(VisualElement layer, Vector2 offset, float scale)
        {
            if (layer == null) return;
            layer.style.translate = new Translate(offset.x, offset.y);
            layer.style.scale = new Scale(new Vector3(scale, scale, 1));
        }

        // Ordinary wall/floor blocks share a preview texture. Keep their quads,
        // rather than creating one Image/layout node per cell on every viewport move.
        private static bool IsBatchedBlock(PlanningBox box) =>
            (box.type == PlanningDetailType.Solid || box.type == PlanningDetailType.Platform) &&
            string.IsNullOrEmpty(box.propEntryId) && string.IsNullOrEmpty(box.propEntryName);

        private void UpdateBlockBatches()
        {
            if (blockPreviewLayer == null) return;
            bool visible = document != null && (mode == PlanningCanvasMode.Detail || UsesAssemblyPreviewCoordinates);
            blockPreviewLayer.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!visible) return;
            int selection = 0;
            if (LevelEditorState.Tool == LevelEditorTool.Merge)
                foreach (string id in mergeSelection) selection ^= id.GetHashCode();
            if (!blockBatchesValid || blockBatchMode != mode || blockBatchRoom != selectedRoomId || blockBatchSelection != selection)
            {
                blockPreviewLayer.Clear(); blockBatches.Clear();
                var groups = new Dictionary<Texture2D, List<(Rect rect, Color color)>>();
                foreach (var item in AllBoxes())
                {
                    if (!IsBatchedBlock(item.box) || (mode == PlanningCanvasMode.Detail && item.room?.id != selectedRoomId)) continue;
                    Texture2D preview = GetBoxPreview(item.box);
                    Color tint = preview != null ? Color.white : ColorForBox(item.box);
                    if (mergeSelection.Contains(item.box.id) && LevelEditorState.Tool == LevelEditorTool.Merge) tint = new Color(.55f,.85f,1f);
                    preview ??= Texture2D.whiteTexture;
                    if (!groups.TryGetValue(preview, out var quads)) groups[preview] = quads = new();
                    RectInt display = DisplayRect(item.room, item.box);
                    Vector2Int offset = display.position - new Vector2Int(item.box.x, item.box.y);
                    foreach (RectInt part in PlanningShapeUtility.Rectangles(item.box))
                    {
                        // A merged part uses one stretched preview, just like the old Image path.
                        // Unmerged blocks repeat the same native front preview per world cell.
                        if (item.box.isMerged) quads.Add((new Rect(part.x + offset.x, part.y + offset.y, part.width, part.height), tint));
                        else for (int y = part.y; y < part.yMax; y++) for (int x = part.x; x < part.xMax; x++)
                            quads.Add((new Rect(x + offset.x, y + offset.y, 1, 1), tint));
                    }
                }
                foreach (var group in groups)
                    for (int start = 0; start < group.Value.Count; start += 512)
                    {
                        var batch = new BlockPreviewBatch(group.Key, group.Value, start, Mathf.Min(512, group.Value.Count - start));
                        blockBatches.Add(batch); blockPreviewLayer.Add(batch);
                    }
                blockBatchesValid = true; blockBatchMode = mode; blockBatchRoom = selectedRoomId; blockBatchSelection = selection;
            }
            blockPreviewLayer.style.opacity = editWaterLayer ? .2f : 1f;
            blockPreviewLayer.transform.position = pan;
            blockPreviewLayer.transform.scale = new Vector3(zoom, zoom, 1);
            Rect viewport = new Rect(ScreenToWorld(contentRect.min), contentRect.size / GridSize);
            foreach (var batch in blockBatches) batch.SetVisible(batch.Bounds.Overlaps(viewport));
        }

        private sealed class BlockPreviewBatch : VisualElement
        {
            internal readonly Rect Bounds;
            private readonly Texture2D texture;
            private readonly List<(Rect rect, Color color)> quads;
            private readonly int start, count;
            private bool batchVisible = true;
            internal BlockPreviewBatch(Texture2D texture, List<(Rect rect, Color color)> quads, int start, int count)
            {
                this.texture = texture; this.quads = quads; this.start = start; this.count = count;
                Rect bounds = quads[start].rect;
                for (int i = start + 1; i < start + count; i++)
                {
                    Rect r = quads[i].rect;
                    bounds = Rect.MinMaxRect(Mathf.Min(bounds.xMin, r.xMin), Mathf.Min(bounds.yMin, r.yMin), Mathf.Max(bounds.xMax, r.xMax), Mathf.Max(bounds.yMax, r.yMax));
                }
                Bounds = bounds;
                pickingMode = PickingMode.Ignore; style.position = Position.Absolute;
                style.left = bounds.x * BaseGridSize; style.top = bounds.y * BaseGridSize;
                style.width = bounds.width * BaseGridSize; style.height = bounds.height * BaseGridSize;
                generateVisualContent += Draw;
            }
            internal void SetVisible(bool value)
            {
                if (batchVisible == value) return;
                batchVisible = value; style.display = value ? DisplayStyle.Flex : DisplayStyle.None;
            }
            private void Draw(MeshGenerationContext context)
            {
                var mesh = context.Allocate(count * 4, count * 6, texture);
                // Unity 6 remaps these coordinates into the atlas automatically.
                Rect uv = new Rect(0, 0, 1, 1);
                for (int j = 0; j < count; j++)
                {
                    var item = quads[start + j]; Rect r = item.rect;
                    float x0 = (r.xMin - Bounds.xMin) * BaseGridSize, x1 = (r.xMax - Bounds.xMin) * BaseGridSize;
                    float y0 = (r.yMin - Bounds.yMin) * BaseGridSize, y1 = (r.yMax - Bounds.yMin) * BaseGridSize;
                    mesh.SetNextVertex(V(x0, y0, new Vector2(uv.xMin, uv.yMax), item.color));
                    mesh.SetNextVertex(V(x1, y0, new Vector2(uv.xMax, uv.yMax), item.color));
                    mesh.SetNextVertex(V(x1, y1, new Vector2(uv.xMax, uv.yMin), item.color));
                    mesh.SetNextVertex(V(x0, y1, new Vector2(uv.xMin, uv.yMin), item.color));
                    ushort index = (ushort)(j * 4);
                    mesh.SetNextIndex(index); mesh.SetNextIndex((ushort)(index + 1)); mesh.SetNextIndex((ushort)(index + 2));
                    mesh.SetNextIndex((ushort)(index + 2)); mesh.SetNextIndex((ushort)(index + 3)); mesh.SetNextIndex(index);
                }
            }
            private static Vertex V(float x, float y, Vector2 uv, Color color) => new Vertex
                { position = new Vector3(x, y, Vertex.nearZ), uv = uv, tint = color };
        }

        public static void VerifyRetainedBlockPreviews()
        {
            int checks = 0;
            void Check(bool value, string message)
            {
                if (!value) throw new System.InvalidOperationException(message);
                checks++; Debug.Log("BLOCK BATCH PASS: " + message);
            }
            var doc = new PlanningDocument();
            var room = new PlanningRoom("Unmerged 12000"); room.cells.Add(new PlanningCell(0, 0)); doc.rooms.Add(room);
            for (int y = 0; y < 120; y++) for (int x = 0; x < 100; x++)
                room.boxes.Add(new PlanningBox(PlanningDetailType.Solid, new RectInt(x, y, 1, 1)));
            var canvas = new PlanningCanvas(); canvas.SetDocument(doc); canvas.SelectRoom(room.id); canvas.SetMode(PlanningCanvasMode.Detail);
            string before = JsonUtility.ToJson(doc);
            Check(canvas.detailPreviewImages.Count == 0, "12000 unmerged walls allocate no per-cell Image nodes");
            Check(canvas.blockBatches.Count == 24, "12000 quads use 24 bounded mesh batches");
            var retained = canvas.blockBatches[0];
            var timer = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 120; i++) canvas.RestoreView(new Vector2(i, -i), .2f + i * .001f);
            timer.Stop();
            Check(ReferenceEquals(retained, canvas.blockBatches[0]), "120 pans and zooms retain geometry");
            Check(JsonUtility.ToJson(doc) == before, "render optimization preserves all unmerged author records");
            canvas.SetMode(PlanningCanvasMode.World);
            Check(canvas.blockPreviewLayer.style.display.value == DisplayStyle.None, "world overview hides blocks by default");
            canvas.SetWorldRoomContentsVisible(true);
            Check(canvas.blockPreviewLayer.style.display.value == DisplayStyle.Flex, "world contents switch includes walls");
            canvas.SetMode(PlanningCanvasMode.Detail);
            room.boxes.RemoveAt(0); canvas.InvalidatePreviewData(); canvas.UpdateDetailPreviewImages();
            Check(!ReferenceEquals(retained, canvas.blockBatches[0]), "author edit invalidates retained geometry");
            Check(!IsBatchedBlock(new PlanningBox { type = PlanningDetailType.Water }), "water union and surface artwork retain their original path");
            string report = "PASS " + checks + "; 12000 unmerged blocks; 120 viewport updates ms=" + timer.Elapsed.TotalMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
            System.IO.File.WriteAllText("Library/RetainedBlockPreviewsVerification.txt", report); Debug.Log(report);
        }

        public static void VerifyRetainedNativePreviews()
        {
            int checks = 0;
            void Check(bool value, string message)
            {
                if (!value) throw new System.InvalidOperationException(message);
                checks++; Debug.Log("NATIVE PREVIEW PASS: " + message);
            }
            var doc = new PlanningDocument();
            var room = new PlanningRoom("原生美术缓存"); room.cells.Add(new PlanningCell(0, 0)); doc.rooms.Add(room);
            room.boxes.Add(new PlanningBox(PlanningDetailType.Water, new RectInt(-12, -8, 3, 3)));
            var canvas = new PlanningCanvas(); canvas.SetDocument(doc); canvas.SetMode(PlanningCanvasMode.Assembly);
            string before = JsonUtility.ToJson(doc);
            int revision = canvas.PreviewGeometryRevision;
            Check(canvas.detailPreviewImages.Count > 0, "native water artwork exists before navigation");
            var first = new List<Image>(canvas.detailPreviewImages.Values)[0];
            float left = first.style.left.value.value;
            for (int i = 0; i < 120; i++) canvas.RestoreView(new Vector2(200 + i, -100 - i), .2f + i * .01f);
            Check(canvas.PreviewGeometryRevision == revision, "120 pans/zooms never reconstruct native artwork");
            Check(new List<Image>(canvas.detailPreviewImages.Values).Contains(first), "native image instances are retained");
            Check(first.style.left.value.value == left, "navigation does not rewrite child layout coordinates");
            Check(JsonUtility.ToJson(doc) == before, "navigation preserves authored document byte-for-byte");
            canvas.SetMode(PlanningCanvasMode.World); canvas.SetWorldRoomContentsVisible(true);
            Check(canvas.waterPreviewLayer.style.scale.value.value.x > 0, "world contents can show retained water");
            canvas.SetWorldRoomContentsVisible(false);
            foreach (var image in canvas.detailPreviewImages.Values) Check(image.style.display.value == DisplayStyle.None, "world contents off hides each native image");
            canvas.SetWorldRoomContentsVisible(true); canvas.SetMode(PlanningCanvasMode.Detail); canvas.SelectRoom(room.id);
            revision = canvas.PreviewGeometryRevision;
            room.boxes[0].x += 4; canvas.NotifyDocumentChanged();
            Check(canvas.PreviewGeometryRevision > revision, "author edits invalidate native geometry");
            revision = canvas.PreviewGeometryRevision;
            canvas.RefreshArtworkPreviews();
            Check(canvas.PreviewGeometryRevision > revision, "asset refresh invalidates native geometry and component metadata");
            string result = "PASS " + checks + " native preview retention checks";
            System.IO.File.WriteAllText("Library/RetainedNativePreviewsVerification.txt", result); Debug.Log(result);
        }

        private PlanningOwnerPreview OwnerPreview(PlanningRoom room)
        {
            if (!ownerPreviewCache.TryGetValue(room, out var preview) || preview.Source != room.cells || preview.Count != room.cells.Count)
                ownerPreviewCache[room] = preview = new PlanningOwnerPreview(room);
            return preview;
        }

        private void DrawOwnerPreview(UnityEngine.UIElements.Painter2D painter, PlanningRoom room,
            Vector2 offset, Vector2 scale, Color fill, Color outline)
        {
            using var profile = DrawOwnerMarker.Auto();
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
            waterPreviewLayer = new VisualElement { pickingMode = PickingMode.Ignore, usageHints = UsageHints.GroupTransform };
            objectPreviewLayer = new VisualElement { pickingMode = PickingMode.Ignore, usageHints = UsageHints.GroupTransform };
            waterPreviewLayer.style.transformOrigin = objectPreviewLayer.style.transformOrigin = new TransformOrigin(0, 0);
            waterPreviewLayer.StretchToParentSize(); objectPreviewLayer.StretchToParentSize();
            Add(waterPreviewLayer); Add(objectPreviewLayer);
            blockPreviewLayer = new VisualElement { name = "RetainedBlockPreviews", pickingMode = PickingMode.Ignore };
            blockPreviewLayer.style.position = Position.Absolute;
            blockPreviewLayer.style.transformOrigin = new TransformOrigin(0, 0, 0);
            Insert(IndexOf(objectPreviewLayer), blockPreviewLayer);
            triggerPreviewLayer = new VisualElement { name = "TimelineTriggerPreviewLayer", pickingMode = PickingMode.Ignore, usageHints = UsageHints.GroupTransform };
            triggerPreviewLayer.style.transformOrigin = new TransformOrigin(0, 0);
            triggerPreviewLayer.StretchToParentSize();
            Add(triggerPreviewLayer);
            triggerPlacementPreview = new Image { name = "TimelineTriggerPlacementPreview", pickingMode = PickingMode.Ignore, scaleMode = ScaleMode.ScaleToFit };
            triggerPlacementPreview.style.position = Position.Absolute;
            triggerPlacementPreview.style.opacity = .5f;
            triggerPlacementPreview.style.display = DisplayStyle.None;
            // The hover preview is screen-space, not retained author geometry.
            Add(triggerPlacementPreview);
        }

        private void UpdateTriggerPlacementPreview()
        {
            if (triggerPreviewLayer == null) return;
            triggerPreviewLayer.BringToFront();
            triggerPlacementPreview.BringToFront();
            var entry = FindPropEntry(detailPropEntryId, detailPropEntryName);
            bool show = document != null && hasHover && mode != PlanningCanvasMode.World && !editWaterLayer &&
                LevelEditorState.Tool == LevelEditorTool.Paint && entry?.Prefab != null &&
                entry.Prefab.GetComponent<Project.BlockFeatures.ContactTimelineTrigger>() != null;
            triggerPlacementPreview.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (!show) return;
            Rect rect = CellRect(hoveredCell.x, hoveredCell.y);
            triggerPlacementPreview.image = Project.BlockFeatures.Editor.ContactTimelineEditorPreview.Texture;
            triggerPlacementPreview.style.left = rect.x; triggerPlacementPreview.style.top = rect.y;
            triggerPlacementPreview.style.width = rect.width; triggerPlacementPreview.style.height = rect.height;
        }

        private void InvalidatePreviewData()
        {
            previewComponents.Clear(); previewWaterKinds.Clear();
            previewPropEntries.Clear();
            previewPlatformPaths.Clear();
            previewLinksValid = false;
            retainedImagesValid = false;
            blockBatchesValid = false;
            PlanningShapeUtility.Invalidate();
            boxPreviewCache.Clear(); pendingBoxPreviews.Clear();
            artworkPreviewCache.Clear(); waterPreviewCells.Clear(); waterPreviewOwners.Clear();
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
            if (buildingRetainedImages) return true;
            // Unattached verification canvases have no viewport yet.
            if (!(contentRect.width > 0f && contentRect.height > 0f)) return true;
            return CellRect(rect.x, rect.y, rect.width, rect.height).Overlaps(contentRect);
        }
    }
}
