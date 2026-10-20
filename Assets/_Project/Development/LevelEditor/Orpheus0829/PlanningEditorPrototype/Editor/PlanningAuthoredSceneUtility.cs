using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Project.LevelEditor;
using Project.LevelEditor.Editor;
using UnityEditor;
using UnityEngine;

namespace PlanningEditorPrototype
{
    /// <summary>Explicit final-scene receipts, never an automatic scene migration.</summary>
    internal static class PlanningAuthoredSceneUtility
    {
        internal static string StripEmptySnapshotFields(string value)
        {
            if (value == null) return null;
            value = Regex.Replace(value, ",?\"authoredSnapshotGuid\":(?:null|\"\")", "");
            value = Regex.Replace(value, ",?\"authoredObjectId\":(?:null|\"\")", "");
            value = Regex.Replace(value, ",?\"authoredInstanceId\":(?:null|\"\")", "");
            value = Regex.Replace(value, ",?\"authoredPositionOffset\":\\{\"x\":0(?:\\.0)?,\"y\":0(?:\\.0)?,\"z\":0(?:\\.0)?\\}", "");
            return Regex.Replace(value, ",?\"authoredSizeCells\":\\{\"x\":0,\"y\":0\\}", "");
        }

        internal static int BuildSnapshots(Transform generated, IReadOnlyList<PlanningRoom> rooms,
            int strideX, int strideY, float cellSize)
        {
            var entries = rooms.SelectMany(room => room.boxes.Where(box =>
                !string.IsNullOrEmpty(box.authoredSnapshotGuid)).Select(box => (room, box))).ToList();
            int count = 0;
            foreach (var group in entries.GroupBy(e => GroupKey(e.box)))
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(group.First().box.authoredSnapshotGuid);
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (asset == null) throw new InvalidDataException("Missing final scene receipt: " + group.Key);
                string revision = AssetDatabase.GetAssetDependencyHash(assetPath).ToString();
                // Instantiate the entire receipt together: Unity remaps all intra-level
                // references (vines, doors, cranks, mounts, Timeline bindings) atomically.
                var instance = UnityEngine.Object.Instantiate(asset);
                instance.name = ReceiptRootName(group.Key);
                instance.transform.SetParent(generated, false);
                var objects = instance.GetComponentsInChildren<LevelEditorPlacedBlock>(true)
                    .ToDictionary(b => b.PlanningBoxId, StringComparer.Ordinal);
                var included = new HashSet<string>(group.Select(e => e.box.authoredObjectId), StringComparer.Ordinal);
                var includedTransforms = new HashSet<Transform>(objects.Where(pair => included.Contains(pair.Key)).Select(pair => pair.Value.transform));
                var first = group.First();
                if (objects.TryGetValue(first.box.authoredObjectId, out var firstObject))
                {
                    var firstLayout = PlanningLayoutUtility.GetRoomLayout(first.room, strideX, strideY);
                    var firstPosition = new Vector3((firstLayout.OffsetX + first.box.x + .5f) * cellSize,
                        -(firstLayout.OffsetY + first.box.y + .5f) * cellSize, 0) + first.box.authoredPositionOffset * cellSize;
                    instance.transform.position += firstPosition - firstObject.transform.position;
                }
                // Parents must reach their final pose before their authored children.
                foreach (var entry in group.OrderBy(e => objects.TryGetValue(e.box.authoredObjectId, out var b) ? HierarchyDepth(b.transform) : int.MaxValue))
                {
                    if (!objects.TryGetValue(entry.box.authoredObjectId, out var placed))
                        throw new InvalidDataException("Missing authored object: " + entry.box.id);
                    var layout = PlanningLayoutUtility.GetRoomLayout(entry.room, strideX, strideY);
                    var cell = new Vector2Int(layout.OffsetX + entry.box.x, layout.OffsetY + entry.box.y);
                    Vector3 position = new Vector3((cell.x + .5f) * cellSize,
                        -(cell.y + .5f) * cellSize, 0) + entry.box.authoredPositionOffset * cellSize;
                    string containerName = entry.room.name + " [" + entry.room.id + "]";
                    var container = generated.Find(containerName);
                    if (container == null)
                    {
                        container = new GameObject(containerName).transform;
                        container.SetParent(generated, false);
                    }
                    bool hasIncludedParent = false;
                    for (var parent = placed.transform.parent; parent != null && parent != instance.transform; parent = parent.parent)
                    {
                        if (includedTransforms.Contains(parent)) { hasIncludedParent = true; break; }
                    }
                    // A vine segment, mounted object, or other nested marker retains
                    // its native hierarchy. Only receipt-level objects join rooms.
                    if (!hasIncludedParent) placed.transform.SetParent(container, true);
                    placed.transform.position = position;
                    if (entry.box.authoredSizeCells.x > 0 && entry.box.authoredSizeCells.y > 0)
                    {
                        var scale = placed.transform.localScale;
                        scale.x *= (float)entry.box.width / entry.box.authoredSizeCells.x;
                        scale.y *= (float)entry.box.height / entry.box.authoredSizeCells.y;
                        placed.transform.localScale = scale;
                    }
                    // Keep the snapshot's exact rotation and full component state; only
                    // explicit parameter edits made after the receipt override it.
                    LevelEditorComponentOverrideUtility.ApplyOverrides(placed.gameObject,
                        entry.box.componentOverrides, false);
                    var so = new SerializedObject(placed);
                    so.FindProperty("cell").vector2IntValue = new Vector2Int(cell.x, -cell.y - entry.box.height);
                    so.FindProperty("sizeCells").vector2IntValue = new Vector2Int(entry.box.width, entry.box.height);
                    so.FindProperty("cellWorldSize").floatValue = cellSize;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    string signature = JsonUtility.ToJson(entry.box.PersistentClone());
                    signature = signature.Substring(0, signature.Length - 1) + ",\"authoredAssetRevision\":\"" + revision + "\"}";
                    placed.SetPlanningSource(entry.box.id, signature,
                        new Vector2Int(entry.box.x, entry.box.y));
                    var reset = placed.GetComponent<Project.Rooms.RoomResetObject>();
                    if (reset != null) reset.SetRegion(entry.room.id);
                    count++;
                }
                // Deleted author objects stay deleted; do not resurrect the receipt's
                // leftovers when rebuilding an edited planning document.
                foreach (var unused in objects.Where(pair => !included.Contains(pair.Key)).Select(pair => pair.Value))
                    if (unused != null) UnityEngine.Object.DestroyImmediate(unused.gameObject);
                // Keep non-marker helpers referenced by the receipt. Empty author
                // room scaffolding is harmless and is reconciled with the group.
                if (instance.transform.childCount == 0 && instance.GetComponents<Component>().Length == 1)
                    UnityEngine.Object.DestroyImmediate(instance);
            }
            return count;
        }

        internal static string GroupKey(PlanningBox box) => box.authoredSnapshotGuid + "_" + (box.authoredInstanceId ?? string.Empty);
        internal static string ReceiptRootName(string key) => "__AuthoredReceipt_" + key;

        internal static bool TryGetGroup(LevelEditorPlacedBlock block, out string key)
        {
            key = null;
            if (block == null || string.IsNullOrEmpty(block.PlanningSignature) ||
                !block.PlanningSignature.Contains("\"authoredSnapshotGuid\"")) return false;
            try
            {
                var box = JsonUtility.FromJson<PlanningBox>(block.PlanningSignature);
                if (box == null || string.IsNullOrEmpty(box.authoredSnapshotGuid)) return false;
                key = GroupKey(box);
                return true;
            }
            catch (ArgumentException) { return false; }
        }

        private static int HierarchyDepth(Transform value)
        {
            int depth = 0;
            for (; value != null; value = value.parent) depth++;
            return depth;
        }
    }
}
