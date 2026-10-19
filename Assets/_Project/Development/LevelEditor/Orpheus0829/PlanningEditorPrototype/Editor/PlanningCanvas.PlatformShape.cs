using System.Collections.Generic;
using Project.BlockFeatures;
using UnityEngine;

namespace PlanningEditorPrototype
{
    public sealed partial class PlanningCanvas
    {
        private bool placementPlatformVertical;
        private int placementPlatformLength = 3;

        public void SetPlatformPlacementShape(bool vertical, int length)
        {
            placementPlatformVertical = vertical;
            placementPlatformLength = Mathf.Clamp(length, 1, 256);
            MarkDirtyRepaint();
        }

        private Vector2Int PlacementFootprint(string entryId) => PlanningPlatformShapeUtility.SupportsEntry(entryId)
            ? PlanningPlatformShapeUtility.Size(placementPlatformVertical, placementPlatformLength)
            : PlanningDoorUtility.Footprint(entryId);

        private void ApplyPlatformPlacementShape(PlanningBox box)
        {
            if (!PlanningPlatformShapeUtility.SupportsEntry(box.propEntryId)) return;
            Vector2Int size = PlacementFootprint(box.propEntryId);
            box.width = size.x; box.height = size.y; box.singleInstance = true;
            PlanningPlatformShapeUtility.SetOrientation(box, placementPlatformVertical);
        }

        public bool TryGetSelectedPlatformShape(out bool vertical, out int length)
        {
            vertical = false; length = 3;
            if (!FindSelectedBox(out _, out PlanningBox box) || !PlanningPlatformShapeUtility.SupportsEntry(box.propEntryId)) return false;
            var entry = FindPropEntry(box.propEntryId, box.propEntryName);
            var feature = entry?.Prefab != null ? entry.Prefab.GetComponent<MovingPlatformFeature>() : null;
            vertical = PlanningPlatformShapeUtility.IsVertical(feature,
                box.hasComponentOverrides ? box.componentOverrides : entry?.ComponentValueOverrides, new Vector2(box.width, box.height));
            length = vertical ? box.height : box.width;
            return true;
        }

        public bool SetSelectedPlatformShape(bool vertical, int length)
        {
            if (!FindSelectedBox(out PlanningRoom owner, out List<PlanningBox> boxes, out PlanningBox box) ||
                !PlanningPlatformShapeUtility.SupportsEntry(box.propEntryId)) return false;
            Vector2Int size = PlanningPlatformShapeUtility.Size(vertical, length);
            RectInt rect = new RectInt(box.x, box.y, size.x, size.y);
            if (owner != null && !CanPlaceLocalRect(owner, rect))
            { StatusChanged?.Invoke("平台新形状超出房间占格；原平台未修改。"); return false; }
            foreach (var other in boxes)
                if (other != box && !PlanningWaterLayerUtility.IsWater(other, palette) && PlanningShapeUtility.Overlaps(other, rect))
                { StatusChanged?.Invoke("平台新形状与物体重叠；原平台未修改。可先移到空处再调整。"); return false; }
            if (!box.hasComponentOverrides) CapturePlacementDefaults(box);
            box.width = size.x; box.height = size.y;
            // An explicit author resize makes one continuous body, not a hidden
            // batch of old one-cell records. Identity, route and links stay intact.
            box.singleInstance = true; box.isMerged = false; box.mergeParts.Clear();
            PlanningPlatformShapeUtility.SetOrientation(box, vertical);
            NotifyDocumentChanged(); SelectionChanged?.Invoke();
            StatusChanged?.Invoke($"平台已改为{(vertical ? "竖向" : "横向")} {size.x}×{size.y} 格；移动路线及绑定保持不变。");
            return true;
        }
    }
}
