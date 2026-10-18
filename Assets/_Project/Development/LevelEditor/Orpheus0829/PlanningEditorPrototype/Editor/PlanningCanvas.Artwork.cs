using System.Collections.Generic;
using Project.BlockFeatures.Editor;
using Project.LevelEditor.Editor;
using Project.ObjectArtwork;
using UnityEngine;
using UnityEngine.UIElements;

namespace PlanningEditorPrototype
{
    public sealed partial class PlanningCanvas
    {
        private ObjectArtworkLibrary previewArtwork;
        private readonly Dictionary<string, (bool matched, PlanningArtworkPreview artwork)> artworkPreviewCache = new Dictionary<string, (bool, PlanningArtworkPreview)>();
        private readonly Dictionary<string, HashSet<Vector2Int>> waterPreviewCells = new Dictionary<string, HashSet<Vector2Int>>();
        private readonly Dictionary<string, Dictionary<Vector2Int, string>> waterPreviewOwners = new();
        private PlanningArtworkPreview[] waterTileArtwork;

        private bool ResolveArtworkPreview(PlanningBox box, out PlanningArtworkPreview artwork)
        {
            if (artworkPreviewCache.TryGetValue(box.id, out var cached)) { artwork = cached.artwork; return cached.matched; }
            if (artworkPreviewCache.Count == 0) ClearVinePreviewGroups();
            var entry = FindPropEntry(box.propEntryId, box.propEntryName);
            artwork = default;
            bool matched = entry?.Prefab != null && (TryResolveVinePreview(box, out artwork) ||
                ObjectArtworkPlanningPreview.TryGet(entry.Prefab,
                    new Vector2(box.width, box.height), box.hasComponentOverrides ? box.componentOverrides : entry.ComponentValueOverrides, out artwork));
            if (matched && box.propEntryId == PlanningDoorUtility.ButtonEntryId && box.doorButtonOrder > 0)
            {
                if (previewArtwork == null) previewArtwork=ObjectArtworkLibrary.Load();
                var sprite=previewArtwork?.FirstFrame(ObjectArtworkLibrary.DoorButtonKind(box.doorButtonOrder-1));
                if (sprite != null) artwork=new PlanningArtworkPreview(sprite);
            }
            artworkPreviewCache[box.id] = (matched, artwork); return matched;
        }

        private void RefreshArtworkPreviews()
        {
            ClearVinePreviewGroups();
            artworkPreviewCache.Clear(); waterPreviewCells.Clear(); waterPreviewOwners.Clear(); boxPreviewCache.Clear(); previewArtwork=null; waterTileArtwork=null;
            UpdateDetailPreviewImages(); MarkDirtyRepaint();
        }

        private HashSet<Vector2Int> WaterCellsFor(PlanningBox box)
        {
            if (waterPreviewCells.TryGetValue(box.id,out var cached)) return cached;
            var cells = new HashSet<Vector2Int>(); var members = new List<PlanningBox>();
            var owners = new Dictionary<Vector2Int, string>();
            foreach (var source in WaterPreviewSources(box))
            {
                var item = source.box;
                if (!PlanningWaterLayerUtility.IsWater(item,palette)) continue;
                members.Add(item);
                foreach (var local in PlanningShapeUtility.Cells(item))
                {
                    Vector2Int cell = local + source.origin;
                    cells.Add(cell);
                    if (!owners.TryGetValue(cell, out string owner) || string.CompareOrdinal(item.id, owner) < 0)
                        owners[cell] = item.id;
                }
            }
            foreach (var item in members) { waterPreviewCells[item.id]=cells; waterPreviewOwners[item.id]=owners; }
            return cells;
        }

        private IEnumerable<(PlanningBox box, Vector2Int origin)> WaterPreviewSources(PlanningBox box)
        {
            if (mode == PlanningCanvasMode.Assembly)
            {
                // Build one world-cell union on the first visible water box. Every
                // member caches this same set until a document edit/mode change;
                // pan/repaint never rescans all rooms and global patches.
                foreach (var source in PlanningWaterLayerUtility.WorldBoxes(document)) yield return source;
                yield break;
            }
            // Detail hides other owners. A global patch must not win ownership
            // here and make this room's otherwise valid water disappear.
            IEnumerable<PlanningBox> siblings = document.assemblyPatches;
            foreach (var room in document.rooms) if (room.boxes.Contains(box)) { siblings = room.boxes; break; }
            foreach (var sibling in siblings) yield return (sibling, Vector2Int.zero);
        }

        private bool TryAddArtworkPreview(PlanningBox box, RectInt rect, HashSet<string> active, Vector2Int offset)
        {
            if (!ResolveArtworkPreview(box,out var artwork)) return false;
            if (artwork.Hidden || artwork.Texture == null) return true;
            if (TryAddVinePreview(box, rect, active, offset)) return true;
            bool water = PlanningWaterLayerUtility.IsWater(box,palette);
            VisualElement layer = artwork.EditorOverlay ? triggerPreviewLayer : water ? waterPreviewLayer : objectPreviewLayer;
            Rect target = CellRect(rect.x,rect.y,rect.width,rect.height);
            Vector2 native = artwork.Cells;
            if (native.x <= 0 || native.y <= 0) return true;
            bool rotated = Mathf.Abs(Mathf.DeltaAngle(artwork.RotationDegrees,0)) % 180 > 45;
            Color tint = mergeSelection.Contains(box.id) && LevelEditorState.Tool == LevelEditorTool.Merge ? new Color(.55f,.85f,1f) : Color.white;
            if (water) tint.a = editWaterLayer ? 1f : .82f;
            if (artwork.EditorOverlay) tint.a = .75f;
            if (!artwork.Tile)
            {
                Vector2 size = native * GridSize;
                float displayHeight = rotated ? size.x : size.y;
                Vector2 center = new Vector2(target.center.x,target.yMax-displayHeight*.5f);
                AddArtworkImage(box.id+":art",artwork,new Rect(center-size*.5f,size),artwork.UV,tint,layer,active);
                return true;
            }

            Vector2 step = rotated ? new Vector2(native.y,native.x) : native;
            // Enumerate only visible native-sized tiles. Neither a merged footprint nor
            // a large zoomed-out map may stretch the original authored pixels.
            bool hasViewport = contentRect.width > 0 && contentRect.height > 0;
            float visibleLeft = hasViewport ? Mathf.Max(0,(contentRect.xMin-target.x)/GridSize) : 0;
            float visibleBottom = hasViewport ? Mathf.Max(0,(target.yMax-contentRect.yMax)/GridSize) : 0;
            int firstX = Mathf.Max(0,Mathf.FloorToInt(visibleLeft/step.x));
            int firstY = Mathf.Max(0,Mathf.FloorToInt(visibleBottom/step.y));
            int lastX = Mathf.CeilToInt(rect.width/step.x), lastY = Mathf.CeilToInt(rect.height/step.y);
            if (hasViewport)
            {
                lastX=Mathf.Min(lastX,Mathf.CeilToInt((contentRect.xMax-target.x)/GridSize/step.x));
                lastY=Mathf.Min(lastY,Mathf.CeilToInt((target.yMax-contentRect.yMin)/GridSize/step.y));
            }
            HashSet<Vector2Int> waterCells = water ? WaterCellsFor(box) : null;
            if (water && waterTileArtwork == null)
            {
                waterTileArtwork=new PlanningArtworkPreview[3];
                for(int kind=0;kind<3;kind++) ObjectArtworkPlanningPreview.TryGetWaterTile(kind,out waterTileArtwork[kind]);
            }
            for (int y=firstY;y<lastY;y++) for(int x=firstX;x<lastX;x++)
            {
                var tile = artwork;
                if (water)
                {
                    Vector2Int cell = new Vector2Int(rect.x + Mathf.RoundToInt(x*step.x),
                        rect.yMax - 1 - Mathf.RoundToInt(y*step.y));
                    // Assembly rectangles already include the room/connector
                    // world origin; Detail's owner-local cache does not.
                    if (mode != PlanningCanvasMode.Assembly) cell -= offset;
                    // Different water forces may overlap, but their common blue
                    // underlay is drawn once, not as several darker alpha layers.
                    if (waterPreviewOwners.TryGetValue(box.id, out var owners) &&
                        owners.TryGetValue(cell, out string owner) && owner != box.id) continue;
                    bool above=waterCells.Contains(cell+Vector2Int.down), below=waterCells.Contains(cell+Vector2Int.up);
                    var waterTile=waterTileArtwork[!above ? 0 : !below ? 2 : 1];
                    if (waterTile.Texture != null) tile=waterTile;
                }
                float width=Mathf.Min(step.x,rect.width-x*step.x), height=Mathf.Min(step.y,rect.height-y*step.y);
                Rect uv=tile.UV;
                uv.width*=rotated ? height/step.y : width/step.x;
                uv.height*=rotated ? width/step.x : height/step.y;
                Vector2 center=new Vector2(target.x+(x*step.x+width*.5f)*GridSize,target.yMax-(y*step.y+height*.5f)*GridSize);
                Vector2 size=(rotated ? new Vector2(height,width) : new Vector2(width,height))*GridSize;
                AddArtworkImage(box.id+":art:"+rect.x+":"+rect.y+":"+x+":"+y,tile,
                    new Rect(center-size*.5f,size),uv,tint,layer,active);
            }
            return true;
        }

        private void AddArtworkImage(string key, PlanningArtworkPreview artwork, Rect rect, Rect uv, Color tint, VisualElement layer, HashSet<string> active)
        {
            if (!detailPreviewImages.TryGetValue(key,out var image))
            {
                image=new Image { pickingMode=PickingMode.Ignore,scaleMode=ScaleMode.StretchToFill };
                image.style.position=Position.Absolute; layer.Add(image); detailPreviewImages[key]=image;
            }
            else if (image.parent != layer) layer.Add(image);
            image.image=artwork.Texture; image.uv=uv; image.tintColor=tint;
            image.style.left=rect.x; image.style.top=rect.y; image.style.width=rect.width; image.style.height=rect.height;
            image.style.rotate=new Rotate(new Angle(-artwork.RotationDegrees,AngleUnit.Degree));
            image.style.scale=new Scale(new Vector3(artwork.FlipX ? -1 : 1,1,1));
            image.style.display=DisplayStyle.Flex; active.Add(key);
            if (artwork.EditorOverlay) triggerPlacementPreview.BringToFront();
        }

        private bool TryAddPlatformPreview(PlanningBox box, RectInt rect, HashSet<string> active)
        {
            var entry = FindPropEntry(box.propEntryId, box.propEntryName);
            var platform = entry?.Prefab != null ? entry.Prefab.GetComponent<Project.BlockFeatures.MovingPlatformFeature>() : null;
            bool oneWay = entry?.Prefab != null && entry.Prefab.GetComponent<Project.Mechanisms.OneWayPlatform>() != null;
            if (!PlanningDoorUtility.IsPlatformEntry(box.propEntryId) && platform == null && !oneWay) return false;
            if (previewArtwork == null) previewArtwork = ObjectArtworkLibrary.Load();
            if (previewArtwork == null) return false;
            var kind = oneWay || box.propEntryId == PlanningDoorUtility.OneWayPlatformEntryId ? MechanismArtworkKind.OneWayPlatform
                : box.propEntryId == PlanningMechanismUtility.LiftEntryId || platform != null && platform.IsCrankControlled
                ? MechanismArtworkKind.CrankPlatform : MechanismArtworkKind.MovingPlatform;
            var sprite = previewArtwork.FirstFrame(kind);
            if (sprite == null) return false;
            Rect source = sprite.textureRect;
            int nativeLength = Mathf.Max(1, Mathf.RoundToInt(source.width / GridArtworkMetrics.PixelsPerCell));
            // A legacy paint batch contains separate one-cell objects, not one long
            // merged platform. Keep each object's native-pixel crop and support edge.
            bool separateCells = !box.isMerged && !box.singleInstance;
            bool vertical = !separateCells && ObjectArtworkLibrary.PlatformVisualIsVertical(new Vector2(box.width, box.height));
            int length = vertical ? rect.height : rect.width;
            int tracks = vertical ? rect.width : rect.height;
            int stride = separateCells ? 1 : nativeLength;
            var bounds = entry?.Prefab != null ? entry.Prefab.GetComponent<BoxCollider>() : null;
            // Resolve author geometry once per box, not once per visible tile.
            float topInset = bounds != null
                ? (.5f - (bounds.center.y + bounds.size.y * .5f) * entry.Prefab.transform.localScale.y) * (separateCells ? 1 : rect.height)
                : 0f;
            for (int track = 0; track < tracks; track++)
            for (int start = 0; start < length; start += stride)
            {
                int count = Mathf.Min(stride, length - start);
                RectInt cells = vertical ? new RectInt(rect.x + track, rect.y + start, 1, count)
                    : new RectInt(rect.x + start, rect.y + track, count, 1);
                if (!PreviewVisible(cells)) continue;
                string key = box.id + ":platform:" + cells.x + ":" + cells.y;
                if (!detailPreviewImages.TryGetValue(key, out Image image))
                {
                    image = new Image { pickingMode = PickingMode.Ignore, scaleMode = ScaleMode.StretchToFill };
                    image.style.position = Position.Absolute;
                    objectPreviewLayer.Add(image);
                    detailPreviewImages[key] = image;
                }
                Rect target = CellRect(cells.x, cells.y, cells.width, cells.height);
                float width = count * GridSize, height = source.height / GridArtworkMetrics.PixelsPerCell * GridSize;
                image.image = sprite.texture;
                // Keep 16 authored pixels per cell. Partial end pieces crop instead of squeezing.
                image.uv = PlatformArtworkLayout.SliceUV(sprite, count);
                float thickness = PlatformArtworkLayout.VisibleThicknessCells(kind, sprite);
                // The crank and one-way collision strips are thinner than a grid
                // cell. Align art to their actual support, not the nominal cell top.
                image.style.left = target.center.x - width * .5f;
                image.style.top = (vertical ? target.center.y : target.yMin + (topInset + thickness) * GridSize - height * .5f) - height * .5f;
                if (vertical) image.style.left = target.xMin + thickness * GridSize - height * .5f - width * .5f;
                image.style.width = width;
                image.style.height = height;
                image.style.rotate = new Rotate(new Angle(vertical ? -90 : 0, AngleUnit.Degree));
                image.style.scale = new Scale(Vector3.one);
                image.tintColor = mergeSelection.Contains(box.id) && LevelEditorState.Tool == LevelEditorTool.Merge
                    ? new Color(.55f, .85f, 1f) : Color.white;
                image.style.display = DisplayStyle.Flex;
                active.Add(key);
            }
            return true;
        }
    }
}
