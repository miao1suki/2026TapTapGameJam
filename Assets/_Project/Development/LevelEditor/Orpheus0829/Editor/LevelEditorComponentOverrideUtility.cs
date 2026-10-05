using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Project.LevelEditor.Editor
{
    internal static class LevelEditorComponentOverrideUtility
    {
        public static bool IsSupported(
            SerializedProperty property)
        {
            return property != null &&
                   property.propertyType switch
                   {
                       SerializedPropertyType.Integer => true,
                       SerializedPropertyType.Boolean => true,
                       SerializedPropertyType.Float => true,
                       SerializedPropertyType.String => true,
                       SerializedPropertyType.Enum => true,
                       SerializedPropertyType.Color => true,
                       SerializedPropertyType.Vector2 => true,
                       SerializedPropertyType.Vector3 => true,
                       SerializedPropertyType.Vector4 => true,
                       SerializedPropertyType.Quaternion => true,
                       SerializedPropertyType.Rect => true,
                       SerializedPropertyType.Bounds => true,
                       SerializedPropertyType.ObjectReference => true,
                       SerializedPropertyType.AnimationCurve => true,
                       _ => false
                   };
        }

        public static bool TryCapture(
            Component component,
            SerializedProperty property,
            out LevelEditorComponentValueOverride value)
        {
            value = null;
            if (component == null ||
                component is Transform ||
                property == null ||
                !IsSupported(property))
            {
                return false;
            }

            string text = string.Empty;
            UnityEngine.Object objectValue = null;
            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.Enum:
                    text = property.longValue.ToString();
                    break;
                case SerializedPropertyType.Boolean:
                    text = property.boolValue ? "1" : "0";
                    break;
                case SerializedPropertyType.Float:
                    text = property.doubleValue.ToString(
                        "R",
                        System.Globalization.CultureInfo
                            .InvariantCulture);
                    break;
                case SerializedPropertyType.String:
                    text = property.stringValue ?? string.Empty;
                    break;
                case SerializedPropertyType.Color:
                    text = JsonUtility.ToJson(property.colorValue);
                    break;
                case SerializedPropertyType.Vector2:
                    text = JsonUtility.ToJson(property.vector2Value);
                    break;
                case SerializedPropertyType.Vector3:
                    text = JsonUtility.ToJson(property.vector3Value);
                    break;
                case SerializedPropertyType.Vector4:
                    text = JsonUtility.ToJson(property.vector4Value);
                    break;
                case SerializedPropertyType.Quaternion:
                    text = JsonUtility.ToJson(property.quaternionValue);
                    break;
                case SerializedPropertyType.Rect:
                    text = JsonUtility.ToJson(property.rectValue);
                    break;
                case SerializedPropertyType.Bounds:
                    text = JsonUtility.ToJson(property.boundsValue);
                    break;
                case SerializedPropertyType.AnimationCurve:
                    text = JsonUtility.ToJson(
                        BuildCurveData(property.animationCurveValue));
                    break;
                case SerializedPropertyType.ObjectReference:
                    objectValue = property.objectReferenceValue;
                    break;
            }

            value = new LevelEditorComponentValueOverride();
            value.Configure(
                GetComponentPath(component),
                component.GetType().FullName,
                GetComponentIndex(component),
                property.propertyPath,
                property.propertyType.ToString(),
                text,
                objectValue);
            return true;
        }

        public static bool IsSameField(
            LevelEditorComponentValueOverride value,
            Component component,
            SerializedProperty property)
        {
            return value != null &&
                   component != null &&
                   property != null &&
                   value.ComponentPath == GetComponentPath(component) &&
                   value.ComponentTypeName ==
                   component.GetType().FullName &&
                   value.ComponentIndex ==
                   GetComponentIndex(component) &&
                   value.PropertyPath == property.propertyPath;
        }

        public static bool IsSameComponent(
            LevelEditorComponentValueOverride value,
            Component component)
        {
            return value != null &&
                   component != null &&
                   value.ComponentPath == GetComponentPath(component) &&
                   value.ComponentTypeName ==
                   component.GetType().FullName &&
                   value.ComponentIndex ==
                   GetComponentIndex(component);
        }

        public static void ApplyOverrides(
            GameObject root,
            IReadOnlyList<LevelEditorComponentValueOverride> values)
        {
            if (root == null || values == null)
            {
                return;
            }

            for (int index = 0; index < values.Count; index++)
            {
                LevelEditorComponentValueOverride value = values[index];
                Component component = FindComponent(root, value);
                if (component == null || component is Transform)
                {
                    continue;
                }

                var serializedObject = new SerializedObject(component);
                serializedObject.Update();
                SerializedProperty property =
                    serializedObject.FindProperty(value.PropertyPath);
                if (property == null ||
                    !ApplyValue(property, value))
                {
                    continue;
                }

                serializedObject.ApplyModifiedProperties();
            }
        }

        public static void RestorePrefabDefaults(
            GameObject instance,
            GameObject prefab)
        {
            if (instance == null || prefab == null)
            {
                return;
            }

            Component[] components =
                instance.GetComponentsInChildren<Component>(true);
            for (int index = 0; index < components.Length; index++)
            {
                Component target = components[index];
                if (target == null || target is Transform)
                {
                    continue;
                }

                Type type = target.GetType();
                Component source = FindComponent(
                    prefab,
                    GetComponentPath(target),
                    type,
                    GetComponentIndex(target));
                if (source == null)
                {
                    continue;
                }

                Undo.RecordObject(target, "恢复道具组件默认值");
                EditorUtility.CopySerialized(source, target);
            }
        }

        public static Component FindComponent(
            GameObject root,
            LevelEditorComponentValueOverride value)
        {
            if (root == null || value == null)
            {
                return null;
            }

            Transform transform = string.IsNullOrEmpty(
                value.ComponentPath)
                ? root.transform
                : root.transform.Find(value.ComponentPath);
            if (transform == null)
            {
                return null;
            }

            Type type = ProjectDiscovery.FindType(
                value.ComponentTypeName);
            if (type == null)
            {
                return null;
            }

            return FindComponent(
                root,
                value.ComponentPath,
                type,
                value.ComponentIndex);
        }

        public static Component FindComponent(
            GameObject root,
            string componentPath,
            Type type,
            int componentIndex)
        {
            if (root == null || type == null)
            {
                return null;
            }

            Transform transform = string.IsNullOrEmpty(componentPath)
                ? root.transform
                : root.transform.Find(componentPath);
            if (transform == null)
            {
                return null;
            }

            Component[] components = transform.GetComponents<Component>();
            int matchIndex = 0;
            for (int index = 0; index < components.Length; index++)
            {
                Component component = components[index];
                if (component == null || !type.IsInstanceOfType(component))
                {
                    continue;
                }

                if (matchIndex == componentIndex)
                {
                    return component;
                }

                matchIndex++;
            }

            return null;
        }

        public static string GetComponentPath(Component component)
        {
            if (component == null)
            {
                return string.Empty;
            }

            Transform current = component.transform;
            if (current.parent == null)
            {
                return string.Empty;
            }

            var parts = new List<string>();
            while (current.parent != null)
            {
                parts.Add(current.name);
                current = current.parent;
            }

            parts.Reverse();
            return string.Join("/", parts);
        }

        public static int GetComponentIndex(Component component)
        {
            if (component == null)
            {
                return 0;
            }

            Component[] components =
                component.GetComponents<Component>();
            int matchIndex = 0;
            for (int index = 0; index < components.Length; index++)
            {
                Component candidate = components[index];
                if (candidate == null ||
                    candidate.GetType() != component.GetType())
                {
                    continue;
                }

                if (candidate == component)
                {
                    return matchIndex;
                }

                matchIndex++;
            }

            return 0;
        }

        private static bool ApplyValue(
            SerializedProperty property,
            LevelEditorComponentValueOverride value)
        {
            if (!Enum.TryParse(
                    value.ValueType,
                    out SerializedPropertyType type))
            {
                return false;
            }

            try
            {
                switch (type)
                {
                    case SerializedPropertyType.Integer:
                        property.longValue = long.Parse(
                            value.Value,
                            System.Globalization.CultureInfo
                                .InvariantCulture);
                        break;
                    case SerializedPropertyType.Enum:
                        property.intValue = int.Parse(
                            value.Value,
                            System.Globalization.CultureInfo
                                .InvariantCulture);
                        break;
                    case SerializedPropertyType.Boolean:
                        property.boolValue = value.Value == "1";
                        break;
                    case SerializedPropertyType.Float:
                        property.doubleValue = double.Parse(
                            value.Value,
                            System.Globalization.CultureInfo
                                .InvariantCulture);
                        break;
                    case SerializedPropertyType.String:
                        property.stringValue = value.Value ?? string.Empty;
                        break;
                    case SerializedPropertyType.Color:
                        property.colorValue =
                            JsonUtility.FromJson<Color>(value.Value);
                        break;
                    case SerializedPropertyType.Vector2:
                        property.vector2Value =
                            JsonUtility.FromJson<Vector2>(value.Value);
                        break;
                    case SerializedPropertyType.Vector3:
                        property.vector3Value =
                            JsonUtility.FromJson<Vector3>(value.Value);
                        break;
                    case SerializedPropertyType.Vector4:
                        property.vector4Value =
                            JsonUtility.FromJson<Vector4>(value.Value);
                        break;
                    case SerializedPropertyType.Quaternion:
                        property.quaternionValue =
                            JsonUtility.FromJson<Quaternion>(
                                value.Value);
                        break;
                    case SerializedPropertyType.Rect:
                        property.rectValue =
                            JsonUtility.FromJson<Rect>(value.Value);
                        break;
                    case SerializedPropertyType.Bounds:
                        property.boundsValue =
                            JsonUtility.FromJson<Bounds>(value.Value);
                        break;
                    case SerializedPropertyType.AnimationCurve:
                        CurveData curveData =
                            JsonUtility.FromJson<CurveData>(value.Value);
                        if (curveData == null)
                        {
                            return false;
                        }

                        if (curveData.keys == null ||
                            curveData.keys.Length == 0)
                        {
                            return false;
                        }

                        property.animationCurveValue =
                            new AnimationCurve(curveData.keys)
                            {
                                preWrapMode = curveData.preWrapMode,
                                postWrapMode = curveData.postWrapMode
                            };
                        break;
                    case SerializedPropertyType.ObjectReference:
                        property.objectReferenceValue =
                            value.ObjectReferenceValue;
                        break;
                    default:
                        return false;
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static CurveData BuildCurveData(
            AnimationCurve curve)
        {
            return new CurveData
            {
                keys = curve != null
                    ? curve.keys
                    : Array.Empty<Keyframe>(),
                preWrapMode = curve != null
                    ? curve.preWrapMode
                    : WrapMode.Default,
                postWrapMode = curve != null
                    ? curve.postWrapMode
                    : WrapMode.Default
            };
        }

        [Serializable]
        private sealed class CurveData
        {
            public Keyframe[] keys;
            public WrapMode preWrapMode;
            public WrapMode postWrapMode;
        }
    }
}
