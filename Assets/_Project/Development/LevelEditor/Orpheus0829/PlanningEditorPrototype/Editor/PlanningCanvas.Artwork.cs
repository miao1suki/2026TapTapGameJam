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
        private PlanningArtworkPreview[] waterTileArtwork;

        private bool ResolveArtworkPreview(PlanningBox box, out PlanningArtworkPreview artwork)
        {
            if (artworkPreviewCache.TryGetValue(box.id, out var cached)) { artwork = cached.artwork; return cached.matched; }
            var entry = FindPropEntry(box.propEntryId, box.propEntryName);
            artwork = default;
            bool matched = entry?.Prefab != null && ObjectArtworkPlanningPreview.TryGet(entry.Prefab,
                new Vector2(box.width, box.height), box.hasComponentOverrides ? box.componentOverrides : entry.ComponentValueOverrides, out artwork);
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
            artworkPreviewCache.Clear(); waterPreviewCells.Clear(); boxPreviewCache.Clear(); previewArtwork=null; waterTileArtwork=null;
            UpdateDetailPreviewImages(); MarkDirtyRepaint();
        }

        private HashSet<Vector2Int> WaterCellsFor(PlanningBox box)
        {
            if (waterPreviewCells.TryGetValue(box.id,out var cached)) return cached;
            IEnumerable<PlanningBox> siblings = document.assemblyPatches;
            foreach (var room in document.rooms) if (room.boxes.Contains(box)) { siblings = room.boxes; break; }
            var cells = new HashSet<Vector2Int>(); var members = new List<PlanningBox>();
            foreach (var item in siblings)
            {
                if (!PlanningWaterLayerUtility.IsWater(item,palette)) continue;
                members.Add(item);
                foreach (var cell in PlanningShapeUtility.Cells(item)) cells.Add(cell);
            }
            foreach (var item in members) waterPreviewCells[item.id]=cells;
            return cells;
        }

        private bool TryAddArtworkPreview(PlanningBox box, RectInt rect, HashSet<string> active, Vector2Int offset)
        {
            if (!ResolveArtworkPreview(box,out var artwork)) return false;
            if (artwork.Hidden || artwork.Texture == null) return true;
            bool water = PlanningWaterLayerUtility.IsWater(box,palette);
            VisualElement layer = water ? waterPreviewLayer : objectPreviewLayer;
            Rect target = CellRect(rect.x,rect.y,rect.width,rect.height);
            Vector2 native = artwork.Cells;
            if (native.x <= 0 || native.y <= 0) return true;
            bool rotated = Mathf.Abs(Mathf.DeltaAngle(artwork.RotationDegrees,0)) % 180 > 45;
            Color tint = mergeSelection.Contains(box.id) && LevelEditorState.Tool == LevelEditorTool.Merge ? new Color(.55f,.85f,1f) : Color.white;
            if (water) tint.a = editWaterLayer ? 1f : .82f;
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
                        rect.yMax - 1 - Mathf.RoundToInt(y*step.y)) - offset;
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
        }

        private bool TryAddPlatformPreview(PlanningBox box, RectInt rect, HashSet<string> active)
        {
            if (!PlanningDoorUtility.IsPlatformEntry(box.propEntryId)) return false;
            if (previewArtwork == null) previewArtwork = ObjectArtworkLibrary.Load();
            if (previewArtwork == null) return false;
            var kind = box.propEntryId == PlanningMechanismUtility.LiftEntryId
                ? MechanismArtworkKind.CrankPlatform : MechanismArtworkKind.MovingPlatform;
            var sprite = previewArtwork.FirstFrame(kind);
            if (sprite == null) return false;
            Rect source = sprite.textureRect;
            int nativeLength = Mathf.Max(1, Mathf.RoundToInt(source.width / GridArtworkMetrics.PixelsPerCell));
            bool vertical = ObjectArtworkLibrary.PlatformVisualIsVertical(new Vector2(box.width, box.height));
            int length = vertical ? rect.height : rect.width;
            int tracks = vertical ? rect.width : rect.height;
            for (int track = 0; track < tracks; track++)
            for (int start = 0; start < length; start += nativeLength)
            {
                int count = Mathf.Min(nativeLength, length - start);
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
                image.uv = new Rect(source.x / sprite.texture.width, source.y / sprite.texture.height,
                    Mathf.Min(source.width, count * GridArtworkMetrics.PixelsPerCell) / sprite.texture.width, source.height / sprite.texture.height);
                image.style.left = target.center.x - width * .5f;
                image.style.top = (vertical ? target.center.y : target.yMax-height*.5f) - height * .5f;
                image.style.width = width;
                image.style.height = height;
                image.style.rotate = new Rotate(new Angle(vertical ? 90 : 0, AngleUnit.Degree));
                image.tintColor = mergeSelection.Contains(box.id) && LevelEditorState.Tool == LevelEditorTool.Merge
                    ? new Color(.55f, .85f, 1f) : Color.white;
                image.style.display = DisplayStyle.Flex;
                active.Add(key);
            }
            return true;
        }
    }
}
