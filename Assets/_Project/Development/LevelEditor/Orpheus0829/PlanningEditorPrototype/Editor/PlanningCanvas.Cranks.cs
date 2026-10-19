using System.Collections.Generic;
using System.Linq;
using Project.BlockFeatures;
using UnityEngine;

namespace PlanningEditorPrototype
{
    public sealed partial class PlanningCanvas
    {
        private bool IsCrankBox(PlanningBox box) => FindPropEntry(box.propEntryId, box.propEntryName)?.Prefab?.GetComponent<CrankPlatformFeature>() != null;

        private bool TryFindCrankPlatformCell(PlanningRoom room, PlanningBox crank, out Vector2Int cell)
        {
            // Prefer above/right of the crank; never overwrite authored map content.
            for (int radius = 1; radius <= 4; radius++)
            for (int y = crank.y - radius; y <= crank.y + radius; y++)
            for (int x = crank.x + radius; x >= crank.x - radius; x--)
            {
                cell = new Vector2Int(x,y);
                var rect = new RectInt(cell, Vector2Int.one);
                if (cell == new Vector2Int(crank.x,crank.y) || !CanPlaceLocalRect(room,rect)) continue;
                if (PlanningDoorUtility.CanPlace(room.boxes,rect, palette)) return true;
            }
            cell = default; return false;
        }

        private bool CreateCrankPlatform(PlanningRoom room, PlanningBox crank)
        {
            var entry = FindPropEntry(PlanningMechanismUtility.LiftEntryId,"曲柄升降平台");
            if (entry?.Prefab == null || !TryFindCrankPlatformCell(room,crank,out var cell)) return false;
            var platform = new PlanningBox(PlanningDetailType.Prop,new RectInt(cell,Vector2Int.one))
            {
                propEntryId = entry.EntryId, propEntryName = entry.DisplayName,
                paletteEntryId = string.Empty, paletteEntryName = string.Empty,
                crankOwnerId = crank.id
            };
            CapturePlacementDefaults(platform);
            room.boxes.Add(platform);
            crank.singleInstance = true;
            crank.initialCrankPlatformCreated = true;
            return true;
        }

        private void AddCrankPlatform(string id)
        {
            foreach (var item in AllBoxes()) if (item.box.id == id && item.room != null && IsCrankBox(item.box))
            {
                if (!CreateCrankPlatform(item.room,item.box)) { StatusChanged?.Invoke("曲柄附近没有可放置平台的空格。"); return; }
                NotifyDocumentChanged(); MarkDirtyRepaint(); StatusChanged?.Invoke("已添加独立平台，可拖动位置或与同绑定平台合并。"); return;
            }
        }

        private void EnsureInitialCrankPlatforms()
        {
            if (document == null || palette == null) return;
            var cranks = AllBoxes().Where(item => item.room != null && IsCrankBox(item.box)).ToList();
            foreach (var item in cranks)
            {
                item.box.singleInstance = true;
                if (!item.box.initialCrankPlatformCreated)
                {
                    if (AllBoxes().Any(other => other.box.crankOwnerId == item.box.id)) item.box.initialCrankPlatformCreated = true;
                    else CreateCrankPlatform(item.room,item.box);
                }
            }
        }
    }
}
