using System.Collections.Generic;
using Project.LevelEditor;
using Project.Mechanisms;
using UnityEditor;
using UnityEngine;

namespace PlanningEditorPrototype
{
    internal static class PlanningDoorUtility
    {
        internal const string DoorEntryId = "mechanism-door-standard";
        internal const string HorizontalDoorEntryId = "mechanism-door-horizontal";
        internal const string FrontLadderEntryId = "mechanism-ladder-front";
        internal const string SideLadderEntryId = "mechanism-ladder-side";
        internal const string ButtonEntryId = "mechanism-door-owned-button";
        internal const string ButtonPath = "Assets/_Project/Content/Mechanisms/DoorButton.prefab";
        internal static bool IsDoor(PlanningBox box) => box != null && IsDoorEntry(box.propEntryId);
        internal static bool IsDoorEntry(string id) => id == DoorEntryId || id == HorizontalDoorEntryId;
        internal static bool HasFixedFootprint(string id) => IsDoorEntry(id) || id == FrontLadderEntryId || id == SideLadderEntryId;
        internal static Vector2Int Footprint(string id) => id == HorizontalDoorEntryId ? new Vector2Int(2, 1) : new Vector2Int(1, 2);
        internal static LevelEditorPropEntry GetButtonEntry()
        {
            var entry = new LevelEditorPropEntry();
            entry.Configure("门按钮", AssetDatabase.LoadAssetAtPath<GameObject>(ButtonPath));
            return entry;
        }
        internal static bool CanPlace(List<PlanningBox> boxes, RectInt rect)
        {
            foreach (var box in boxes)
                if (!PlanningWaterLayerUtility.IsWater(box) && rect.Overlaps(new RectInt(box.x, box.y, box.width, box.height))) return false;
            return true;
        }
        internal static bool TryFindButtonCell(List<PlanningBox> boxes, PlanningBox door, RectInt allowed, out Vector2Int cell,
            System.Func<Vector2Int, bool> cellAllowed = null)
        {
            var preferred = new[] { new Vector2Int(door.x-1,door.y+door.height-1),new Vector2Int(door.x+door.width,door.y+door.height-1) };
            foreach (var candidate in preferred)
                if (allowed.Contains(candidate) && (cellAllowed == null || cellAllowed(candidate)) && CanPlace(boxes,new RectInt(candidate,Vector2Int.one)))
                { cell = candidate; return true; }
            for (int radius = 1; radius <= 4; radius++)
            for (int y = door.y + door.height - 1 - radius; y <= door.y + door.height - 1 + radius; y++)
            for (int x = door.x - radius; x <= door.x + radius; x++)
            {
                cell = new Vector2Int(x,y);
                if (!allowed.Contains(cell) || (cellAllowed != null && !cellAllowed(cell)) || new RectInt(door.x,door.y,door.width,door.height).Contains(cell)) continue;
                if (CanPlace(boxes,new RectInt(cell,Vector2Int.one))) return true;
            }
            cell = default;
            return false;
        }

        internal static void RemoveOrphanButtons(PlanningDocument document)
        {
            var doors = new HashSet<string>();
            foreach (var room in document.rooms) foreach (var box in room.boxes) if (IsDoor(box)) doors.Add(box.id);
            foreach (var box in document.assemblyPatches) if (IsDoor(box)) doors.Add(box.id);
            foreach (var room in document.rooms)
                room.boxes.RemoveAll(box => !string.IsNullOrEmpty(box.doorOwnerId) && !doors.Contains(box.doorOwnerId));
            document.assemblyPatches.RemoveAll(box => !string.IsNullOrEmpty(box.doorOwnerId) && !doors.Contains(box.doorOwnerId));
        }
        internal static PlanningBox AddButton(List<PlanningBox> boxes, PlanningBox door, RectInt allowed,
            System.Func<Vector2Int, bool> cellAllowed = null)
        {
            int count = 0;
            int lastOrder = 0;
            foreach (var existing in boxes) if (existing.doorOwnerId == door.id)
            {
                count++;
                if (existing.doorButtonOrder <= 0) existing.doorButtonOrder = lastOrder + 1;
                lastOrder = Mathf.Max(lastOrder, existing.doorButtonOrder);
            }
            if (count >= DoorController.MaximumButtons) return null;
            if (!TryFindButtonCell(boxes,door,allowed,out var cell, cellAllowed)) return null;
            var button = new PlanningBox(PlanningDetailType.Prop,new RectInt(cell,Vector2Int.one))
            {
                propEntryId = ButtonEntryId, propEntryName = "门按钮", singleInstance = true, doorOwnerId = door.id,
                doorButtonOrder = lastOrder + 1
            };
            boxes.Add(button);
            return button;
        }
        internal static void BindSceneDoors(Transform root, IReadOnlyList<PlanningRoom> rooms, IReadOnlyList<PlanningBox> patches)
        {
            if (root == null) return;
            var owners = new Dictionary<string,string>();
            var orderedBoxes = new List<PlanningBox>();
            foreach (var room in rooms) if (room != null) foreach (var box in room.boxes)
                if (!string.IsNullOrEmpty(box.doorOwnerId)) { owners[box.id] = box.doorOwnerId; orderedBoxes.Add(box); }
            foreach (var box in patches) if (!string.IsNullOrEmpty(box.doorOwnerId)) { owners[box.id] = box.doorOwnerId; orderedBoxes.Add(box); }
            var doors = new Dictionary<string,DoorController>();
            var buttons = new Dictionary<string,DoorButton>();
            foreach (var placed in root.GetComponentsInChildren<LevelEditorPlacedBlock>(true))
            {
                var door = placed.GetComponent<DoorController>();
                if (door != null && !string.IsNullOrEmpty(placed.PlanningBoxId)) doors[placed.PlanningBoxId] = door;
                if (placed.GetComponent<DoorButton>() != null && !string.IsNullOrEmpty(placed.PlanningBoxId)) buttons[placed.PlanningBoxId] = placed.GetComponent<DoorButton>();
            }
            foreach (var pair in doors)
            {
                var linked = new List<DoorButton>();
                var ordered = orderedBoxes.FindAll(box => owners[box.id] == pair.Key);
                // Legacy saves recover document order, never hierarchy/temporary instance order.
                int lastOrder = 0;
                foreach (var box in ordered)
                {
                    if (box.doorButtonOrder <= 0) box.doorButtonOrder = lastOrder + 1;
                    lastOrder = Mathf.Max(lastOrder, box.doorButtonOrder);
                }
                ordered.Sort((a,b) => a.doorButtonOrder.CompareTo(b.doorButtonOrder));
                foreach (var box in ordered)
                    if (buttons.TryGetValue(box.id, out var button)) linked.Add(button);
                pair.Value.SetButtons(linked);
                EditorUtility.SetDirty(pair.Value);
                foreach (var button in linked) EditorUtility.SetDirty(button);
            }
        }
    }
}
