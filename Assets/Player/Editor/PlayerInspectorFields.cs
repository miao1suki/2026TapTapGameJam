using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Project.Player.Editor
{
    internal static class PlayerInspectorFields
    {
        public static PropertyField Create(
            SerializedObject serializedObject,
            string propertyName,
            string label,
            string tooltip = null)
        {
            PropertyField field = new PropertyField(
                serializedObject.FindProperty(propertyName),
                label);
            if (!string.IsNullOrEmpty(tooltip))
            {
                field.tooltip = tooltip;
            }

            return field;
        }

        public static Foldout CreateFoldout(
            string title,
            bool expanded = true,
            int topMargin = 6)
        {
            Foldout foldout = new Foldout
            {
                text = title,
                value = expanded,
            };
            foldout.style.marginTop = topMargin;
            return foldout;
        }

        public static PropertyField CreateScriptField(
            SerializedObject serializedObject)
        {
            return new PropertyField(
                serializedObject.FindProperty("m_Script"));
        }
    }
}
