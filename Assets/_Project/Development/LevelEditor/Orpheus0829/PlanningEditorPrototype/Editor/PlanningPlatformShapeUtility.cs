using System.Collections.Generic;
using System.Globalization;
using Project.BlockFeatures;
using Project.LevelEditor;
using Project.LevelEditor.Editor;
using UnityEditor;
using UnityEngine;

namespace PlanningEditorPrototype
{
    /// <summary>Body shape is independent of its motion route; old data stays Auto.</summary>
    internal static class PlanningPlatformShapeUtility
    {
        internal static bool SupportsEntry(string id) => id == PlanningDoorUtility.MovingPlatformEntryId ||
            id == PlanningMechanismUtility.LiftEntryId;

        internal static Vector2Int Size(bool vertical, int length) => vertical
            ? new Vector2Int(1, Mathf.Clamp(length, 1, 256)) : new Vector2Int(Mathf.Clamp(length, 1, 256), 1);

        internal static MovingPlatformBodyOrientation Orientation(MovingPlatformFeature feature,
            IReadOnlyList<LevelEditorComponentValueOverride> values)
        {
            var result = feature != null ? feature.BodyOrientation : MovingPlatformBodyOrientation.Auto;
            if (values != null) foreach (var value in values)
                if (Matches(value) && int.TryParse(value.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int selected))
                    result = (MovingPlatformBodyOrientation)selected;
            return result;
        }

        internal static bool IsVertical(MovingPlatformFeature feature, IReadOnlyList<LevelEditorComponentValueOverride> values, Vector2 cells)
        {
            var orientation = Orientation(feature, values);
            return orientation == MovingPlatformBodyOrientation.Vertical ||
                orientation == MovingPlatformBodyOrientation.Auto && cells.y > cells.x;
        }

        internal static void SetOrientation(PlanningBox box, bool vertical)
        {
            box.componentOverrides ??= new List<LevelEditorComponentValueOverride>();
            box.componentOverrides.RemoveAll(value => Matches(value));
            var next = new LevelEditorComponentValueOverride();
            next.Configure(string.Empty, typeof(MovingPlatformFeature).FullName, 0, "bodyOrientation", "Enum",
                ((int)(vertical ? MovingPlatformBodyOrientation.Vertical : MovingPlatformBodyOrientation.Horizontal)).ToString(CultureInfo.InvariantCulture), null);
            box.componentOverrides.Add(next);
            box.hasComponentOverrides = true;
        }

        internal static void PreserveOrientation(PlanningBox box, List<LevelEditorComponentValueOverride> values,
            IReadOnlyList<LevelEditorComponentValueOverride> inherited)
        {
            if (!SupportsEntry(box.propEntryId) || values == null || values.Exists(Matches)) return;
            var original = box.hasComponentOverrides ? box.componentOverrides : inherited;
            if (original != null) foreach (var value in original)
                if (Matches(value)) values.Add(value.Clone());
        }

        internal static bool CanMerge(IReadOnlyList<PlanningBox> items, LevelEditorPropEntry entry,
            RectInt bounds, int occupiedCells, out string reason)
        {
            reason = null;
            if (items.Count == 0 || entry?.Prefab == null) return false;
            // Unlike water/walls, one moving body must remain a continuous strip.
            // Never bridge a gap or turn a horizontal/vertical body into a sheet.
            if ((bounds.width != 1 && bounds.height != 1) || occupiedCells != bounds.width * bounds.height)
            { reason = "平台只能合并为无空格的连续横排或竖排。"; return false; }
            bool vertical = bounds.height > bounds.width;
            var feature = entry.Prefab.GetComponent<MovingPlatformFeature>();
            var first = Values(items[0], entry);
            foreach (var item in items)
            {
                var values = Values(item, entry);
                var orientation = Orientation(feature, values);
                bool incompatible = orientation == MovingPlatformBodyOrientation.Vertical ? !vertical || item.width != 1
                    : orientation == MovingPlatformBodyOrientation.Horizontal ? vertical || item.height != 1
                    : item.width > 1 && vertical || item.height > 1 && !vertical;
                if (incompatible)
                { reason = "平台本体朝向不相容；请先在“平台形状”统一横放或竖放。"; return false; }
                if (!SameEffectiveParameters(entry.Prefab, first, values))
                { reason = "平台路线、移动、信号或其他实例参数不同，不能合并。"; return false; }
            }
            return true;
        }

        private static IReadOnlyList<LevelEditorComponentValueOverride> Values(PlanningBox box, LevelEditorPropEntry entry) =>
            box.hasComponentOverrides ? box.componentOverrides : entry.ComponentValueOverrides;

        // Compare effective values, not list order or whether a default was
        // explicitly saved. Read-only prefab defaults resolve absent fields.
        private static bool SameEffectiveParameters(GameObject prefab, IReadOnlyList<LevelEditorComponentValueOverride> left,
            IReadOnlyList<LevelEditorComponentValueOverride> right)
        {
            var a = Fields(left); var b = Fields(right);
            var keys = new HashSet<string>(a.Keys); keys.UnionWith(b.Keys);
            foreach (string key in keys)
            {
                a.TryGetValue(key, out var av); b.TryGetValue(key, out var bv);
                var fallback = av ?? bv;
                if (av == null) av = Default(prefab, fallback);
                if (bv == null) bv = Default(prefab, fallback);
                if (av == null || bv == null || av.ValueType != bv.ValueType || ValueText(av) != ValueText(bv)) return false;
            }
            return true;
        }

        private static Dictionary<string, LevelEditorComponentValueOverride> Fields(IReadOnlyList<LevelEditorComponentValueOverride> values)
        {
            var result = new Dictionary<string, LevelEditorComponentValueOverride>();
            if (values != null) foreach (var value in values)
                if (value != null && !Matches(value))
                    result[(value.ComponentPath ?? "") + "|" + (value.ComponentTypeName ?? "").Split(',')[0] + "|" +
                        value.ComponentIndex + "|" + value.PropertyPath] = value;
            return result;
        }

        private static LevelEditorComponentValueOverride Default(GameObject prefab, LevelEditorComponentValueOverride value)
        {
            var component = LevelEditorComponentOverrideUtility.FindComponent(prefab, value);
            if (component == null) return null;
            using var serialized = new SerializedObject(component);
            return LevelEditorComponentOverrideUtility.TryCapture(component, serialized.FindProperty(value.PropertyPath), out var result) ? result : null;
        }

        private static string ValueText(LevelEditorComponentValueOverride value)
        {
            if (value.ValueType == "ObjectReference")
            {
                var reference = value.ObjectReferenceValue;
                if (reference != null) return EditorUtility.IsPersistent(reference)
                    ? GlobalObjectId.GetGlobalObjectIdSlow(reference).ToString() : "local:" + reference.GetInstanceID();
                return string.IsNullOrEmpty(value.Value) || value.Value == "reference:null" ? "reference:null" : value.Value;
            }
            if (value.ValueType == "Float" && double.TryParse(value.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double number))
                return number.ToString("R", CultureInfo.InvariantCulture);
            return value.Value ?? "";
        }

        private static bool Matches(LevelEditorComponentValueOverride value) => value != null &&
            string.IsNullOrEmpty(value.ComponentPath) && value.ComponentIndex == 0 && value.PropertyPath == "bodyOrientation" &&
            (value.ComponentTypeName == typeof(MovingPlatformFeature).FullName || value.ComponentTypeName == typeof(MovingPlatformFeature).AssemblyQualifiedName);
    }
}
