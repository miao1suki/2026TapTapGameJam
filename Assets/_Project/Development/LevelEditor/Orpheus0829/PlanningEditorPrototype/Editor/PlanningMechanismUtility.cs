using System.Collections.Generic;
using Project.BlockFeatures;
using Project.LevelEditor;
using UnityEditor;
using UnityEngine;

namespace PlanningEditorPrototype
{
    internal static class PlanningMechanismUtility
    {
        internal const string LiftEntryId = "mechanism-crank-lift";
        internal const string LiftPath = "Assets/_Project/Content/ColorBlocks/Prefabs/Red_CrankLift.prefab";
        internal static void RemoveMissingLinks(IEnumerable<PlanningBox> boxes)
        {
            var all = new List<PlanningBox>(boxes);
            var ids = new HashSet<string>(); foreach (var box in all) ids.Add(box.id);
            foreach (var box in all)
            {
                box.NormalizeMechanismButtonLinks();
                if (!string.IsNullOrEmpty(box.crankOwnerId) && !ids.Contains(box.crankOwnerId)) box.crankOwnerId = null;
                if (!string.IsNullOrEmpty(box.platformMountId) && !ids.Contains(box.platformMountId)) box.platformMountId = null;
                if (!string.IsNullOrEmpty(box.mechanismBaseId) && !ids.Contains(box.mechanismBaseId))
                { box.mechanismBaseId = null; box.mechanismBaseCellOffset = default; }
                box.mechanismButtonIds.RemoveAll(id => !ids.Contains(id));
            }
        }
        private static bool SameButtonLinks(PlanningBox left, PlanningBox right)
        {
            left.NormalizeMechanismButtonLinks();
            right.NormalizeMechanismButtonLinks();
            if (left.mechanismButtonIds.Count != right.mechanismButtonIds.Count) return false;
            for (int index = 0; index < left.mechanismButtonIds.Count; index++)
                if (left.mechanismButtonIds[index] != right.mechanismButtonIds[index]) return false;
            return true;
        }
        private static void ReplaceButtonLink(PlanningBox box, string from, string to)
        {
            box.NormalizeMechanismButtonLinks();
            for (int index = 0; index < box.mechanismButtonIds.Count; index++)
                if (box.mechanismButtonIds[index] == from)
                    box.mechanismButtonIds[index] = to;
            box.NormalizeMechanismButtonLinks();
        }
        internal static bool IsLift(GameObject prefab) => prefab != null && prefab.GetComponent<MovingPlatformFeature>() is MovingPlatformFeature p && p.IsCrankControlled;
        internal static bool CanMerge(IReadOnlyList<PlanningBox> items)
        {
            if (items.Count == 0) return false;
            var first = items[0];
            foreach (var item in items)
                if ((item.crankOwnerId ?? "") != (first.crankOwnerId ?? "") ||
                    (item.platformMountId ?? "") != (first.platformMountId ?? "") ||
                    (item.mechanismBaseId ?? "") != (first.mechanismBaseId ?? "") ||
                    !SameButtonLinks(item, first) ||
                    !string.IsNullOrEmpty(item.mechanismBaseId) && item.mechanismBaseCellOffset != first.mechanismBaseCellOffset) return false;
            return true;
        }
        internal static void RemapMergedSources(IReadOnlyList<PlanningBox> originals, PlanningBox merged, IEnumerable<PlanningBox> consumers)
        {
            foreach (var consumer in consumers) foreach (var original in originals)
            {
                if (consumer.mechanismBaseId == original.id)
                {
                    consumer.mechanismBaseId = merged.id;
                    consumer.mechanismBaseCellOffset += new Vector2Int(original.x - merged.x, original.y - merged.y);
                }
                ReplaceButtonLink(consumer, original.id, merged.id);
                if (consumer.crankOwnerId == original.id) consumer.crankOwnerId = merged.id;
                if (consumer.platformMountId == original.id) consumer.platformMountId = merged.id;
            }
        }
        internal static bool IsConsumer(GameObject prefab) => prefab != null && (prefab.GetComponent<MovingPlatformFeature>() != null || prefab.GetComponent<CrankPlatformFeature>() != null);
        internal static void RemapCarvedSource(PlanningBox original, IReadOnlyList<PlanningBox> parts, IEnumerable<PlanningBox> consumers)
        {
            foreach (var consumer in consumers)
            {
                consumer.NormalizeMechanismButtonLinks();
                if (consumer.crankOwnerId == original.id) consumer.crankOwnerId = parts.Count == 1 ? parts[0].id : null;
                if (consumer.platformMountId == original.id) consumer.platformMountId = parts.Count == 1 ? parts[0].id : null;
                if (consumer.mechanismButtonIds.Contains(original.id))
                    ReplaceButtonLink(consumer, original.id, parts.Count == 1 ? parts[0].id : null);
                if (consumer.mechanismBaseId != original.id) continue;
                Vector2Int target = new Vector2Int(original.x, original.y) + consumer.mechanismBaseCellOffset;
                consumer.mechanismBaseId = null;
                foreach (var part in parts)
                {
                    if (!new RectInt(part.x, part.y, part.width, part.height).Contains(target)) continue;
                    consumer.mechanismBaseId = part.id;
                    consumer.mechanismBaseCellOffset = target - new Vector2Int(part.x, part.y);
                    break;
                }
            }
        }
        internal static void BindScene(Transform root, IReadOnlyList<PlanningRoom> rooms, IReadOnlyList<PlanningBox> patches)
        {
            if (root == null) return;
            var links = new Dictionary<string, PlanningBox>();
            foreach (var room in rooms) if (room != null) foreach (var box in room.boxes) links[box.id] = box;
            foreach (var box in patches) links[box.id] = box;
            var sources = new Dictionary<string, MechanismBaseFeature>();
            var cranks = new Dictionary<string, CrankPlatformFeature>();
            var buttons = new Dictionary<string, MonoBehaviour>();
            var platforms = new Dictionary<string, MovingPlatformFeature>();
            var placed = root.GetComponentsInChildren<LevelEditorPlacedBlock>(true);
            foreach (var item in placed)
            {
                if (!string.IsNullOrEmpty(item.PlanningBoxId) && item.TryGetComponent(out MovingPlatformFeature mountedPlatform))
                    platforms[item.PlanningBoxId] = mountedPlatform;
                var source = item.GetComponent<MechanismBaseFeature>();
                if (source != null && !string.IsNullOrEmpty(item.PlanningBoxId)) sources[item.PlanningBoxId + ":" + item.PlanningLocalCell] = source;
                var doorButton = item.GetComponent<Project.Mechanisms.DoorButton>();
                var signalButton = item.GetComponent<ButtonLockFeature>();
                if (!string.IsNullOrEmpty(item.PlanningBoxId) &&
                    (doorButton != null || signalButton != null))
                {
                    buttons[item.PlanningBoxId] = doorButton != null
                        ? doorButton
                        : signalButton;
                }
                var crank = item.GetComponent<CrankPlatformFeature>();
                if (crank != null && !string.IsNullOrEmpty(item.PlanningBoxId))
                { cranks[item.PlanningBoxId] = crank; crank.MarkInitialPlatformCreated(); EditorUtility.SetDirty(crank); }
            }
            // Clear old group references before resolving the new persistent IDs.
            // This also supports changing which handle owns a group on re-apply.
            foreach (var controller in cranks.Values) controller.BindSharedController(null);
            foreach (var item in placed)
            {
                if (string.IsNullOrEmpty(item.PlanningBoxId) || !links.TryGetValue(item.PlanningBoxId, out var box)) continue;
                MechanismBaseFeature source = null;
                if (!string.IsNullOrEmpty(box.mechanismBaseId) && links.TryGetValue(box.mechanismBaseId, out var baseBox))
                {
                    Vector2Int sourceCell = new Vector2Int(baseBox.x,baseBox.y) + (baseBox.isMerged ? Vector2Int.zero : box.mechanismBaseCellOffset);
                    sources.TryGetValue(baseBox.id + ":" + sourceCell,out source);
                }
                var linkedButtons = new List<MonoBehaviour>();
                box.NormalizeMechanismButtonLinks();
                foreach (string buttonId in box.mechanismButtonIds)
                {
                    if (links.ContainsKey(buttonId) &&
                        buttons.TryGetValue(buttonId, out MonoBehaviour button) &&
                        !linkedButtons.Contains(button))
                    {
                        linkedButtons.Add(button);
                    }
                }
                var platform = item.GetComponent<MovingPlatformFeature>();
                var crank = item.GetComponent<CrankPlatformFeature>();
                if (platform != null)
                {
                    if (platform.IsCrankControlled || !string.IsNullOrEmpty(box.crankOwnerId))
                    {
                        CrankPlatformFeature controller = null;
                        if (!string.IsNullOrEmpty(box.crankOwnerId)) cranks.TryGetValue(box.crankOwnerId, out controller);
                        platform.BindCrank(controller);
                    }
                    else
                    {
                        var signalSources = new List<MonoBehaviour>();
                        if (source != null) signalSources.Add(source);
                        signalSources.AddRange(linkedButtons);
                        platform.BindSignalSources(signalSources);
                    }
                    EditorUtility.SetDirty(platform);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(platform);
                }
                if (crank != null)
                {
                    crank.BindBase(source);
                    CrankPlatformFeature owner = null;
                    if (!string.IsNullOrEmpty(box.crankOwnerId)) cranks.TryGetValue(box.crankOwnerId, out owner);
                    if (!crank.BindSharedController(owner)) Debug.LogError("曲柄共享绑定不能串联或循环：" + box.id, crank);
                    EditorUtility.SetDirty(crank); PrefabUtility.RecordPrefabInstancePropertyModifications(crank);
                }
                var mount = item.GetComponent<PlatformMount>();
                if (!string.IsNullOrEmpty(box.platformMountId) && platforms.TryGetValue(box.platformMountId, out var mountOwner) &&
                    mountOwner.gameObject != item.gameObject)
                {
                    if (mount == null) mount = item.gameObject.AddComponent<PlatformMount>();
                    mount.Bind(mountOwner);
                    EditorUtility.SetDirty(mount);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(mount);
                }
                else if (mount != null) Object.DestroyImmediate(mount);
            }
        }
    }
}
