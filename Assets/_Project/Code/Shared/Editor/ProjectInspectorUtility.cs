using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Project.Editor
{
    /// <summary>
    /// 项目自定义 Inspector 的统一样式和排版入口。
    /// </summary>
    public static class ProjectInspectorUtility
    {
        public static VisualElement CreateRoot(
            SerializedObject serializedObject)
        {
            var root = new VisualElement();
            root.style.paddingLeft = 10f;
            root.style.paddingRight = 10f;
            root.style.paddingTop = 8f;
            root.style.paddingBottom = 8f;
            return root;
        }

        public static PropertyField CreateScriptField(
            SerializedObject serializedObject)
        {
            return CreateProperty(
                serializedObject,
                "m_Script");
        }

        public static Label CreateTitle(string text)
        {
            var title = new Label(text);
            title.style.fontSize = 17f;
            title.style.unityFontStyleAndWeight =
                FontStyle.Bold;
            title.style.marginBottom = 6f;
            return title;
        }

        public static Foldout CreateFoldout(
            string title,
            bool expanded = true,
            int topMargin = 6)
        {
            var foldout = new Foldout
            {
                text = title,
                value = expanded
            };
            foldout.style.marginTop = topMargin;
            return foldout;
        }

        public static PropertyField CreateProperty(
            SerializedObject serializedObject,
            string propertyPath,
            string label = null,
            string tooltip = null)
        {
            SerializedProperty property =
                serializedObject.FindProperty(propertyPath);
            var field = string.IsNullOrEmpty(label)
                ? new PropertyField(property)
                : new PropertyField(property, label);
            if (!string.IsNullOrEmpty(tooltip))
            {
                field.tooltip = tooltip;
            }

            return field;
        }

        public static VisualElement CreateReadOnlyRow(
            string label,
            Func<string> value,
            int refreshMilliseconds = 100)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginBottom = 2f;

            var name = new Label(label);
            name.style.width = 116f;
            name.style.opacity = .72f;
            row.Add(name);

            var content = new Label();
            content.style.flexGrow = 1f;
            content.style.whiteSpace = WhiteSpace.Normal;
            row.Add(content);

            Action refresh = () => content.text =
                value != null ? value() ?? "无" : "无";
            refresh();
            row.schedule.Execute(refresh)
                .Every(Mathf.Max(50, refreshMilliseconds));
            return row;
        }

        public static HelpBox CreateHelp(
            string message,
            HelpBoxMessageType type = HelpBoxMessageType.Info)
        {
            var help = new HelpBox(message, type);
            help.style.marginTop = 6f;
            help.style.whiteSpace = WhiteSpace.Normal;
            return help;
        }

        public static VisualElement CreateDivider()
        {
            var divider = new VisualElement();
            divider.style.height = 1f;
            divider.style.marginTop = 6f;
            divider.style.marginBottom = 6f;
            divider.style.backgroundColor =
                new Color(.25f, .25f, .25f, .8f);
            return divider;
        }

        public static void Bind(
            VisualElement root,
            SerializedObject serializedObject)
        {
            root.Bind(serializedObject);
        }
    }
}
