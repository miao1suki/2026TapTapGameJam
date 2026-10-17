using System.Collections.Generic;
using System.Globalization;
using Project.BlockFeatures;
using Project.LevelEditor;
using UnityEditor;
using UnityEngine;

namespace PlanningEditorPrototype
{
    internal static class PlanningPlatformPathUtility
    {
        internal static bool ClipLine(Rect viewport, ref Vector2 start, ref Vector2 end)
        {
            Vector2 origin = start, delta = end - start;
            float enter = 0f, exit = 1f;
            bool Axis(float value, float direction, float low, float high)
            {
                if (Mathf.Abs(direction) < .00001f) return value >= low && value <= high;
                float a = (low - value) / direction, b = (high - value) / direction;
                enter = Mathf.Max(enter, Mathf.Min(a, b)); exit = Mathf.Min(exit, Mathf.Max(a, b));
                return enter <= exit;
            }
            if (!Axis(origin.x, delta.x, viewport.xMin, viewport.xMax) ||
                !Axis(origin.y, delta.y, viewport.yMin, viewport.yMax)) return false;
            start = origin + delta * enter; end = origin + delta * exit;
            return true;
        }

        internal static MovingPlatformPath Resolve(MovingPlatformFeature feature,
            IReadOnlyList<LevelEditorComponentValueOverride> overrides)
        {
            using var serialized = new SerializedObject(feature);
            float Read(string name)
            {
                var property = serialized.FindProperty(name);
                float result = property.propertyType == SerializedPropertyType.Boolean ? property.boolValue ? 1f : 0f
                    : property.propertyType == SerializedPropertyType.Float ? property.floatValue : property.intValue;
                if (overrides == null) return result;
                foreach (var value in overrides)
                {
                    if (value == null || value.PropertyPath != name || !string.IsNullOrEmpty(value.ComponentPath) || value.ComponentIndex != 0 ||
                        value.ComponentTypeName != typeof(MovingPlatformFeature).FullName &&
                        value.ComponentTypeName != typeof(MovingPlatformFeature).AssemblyQualifiedName) continue;
                    if (float.TryParse(value.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float number)) result = number;
                    else if (bool.TryParse(value.Value, out bool flag)) result = flag ? 1f : 0f;
                }
                return result;
            }
            return MovingPlatformPath.Resolve((MovingPlatformPathMode)(int)Read("pathMode"),
                (MovingPlatformRoute)(int)Read("route"), (int)Read("travelBlocks"), Read("reverseRoute") != 0f,
                Read("leftDistance"), Read("rightDistance"), Read("downDistance"), Read("upDistance"),
                Read("centerOffset"), Read("startNegative") != 0f);
        }
    }
}
