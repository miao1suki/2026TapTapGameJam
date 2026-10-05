using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Project.CameraModes.Editor
{
    [CustomEditor(typeof(CameraControlManager))]
    public sealed class CameraControlManagerEditor :
        UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            serializedObject.Update();
            var root = new VisualElement();
            root.style.paddingLeft = 10f;
            root.style.paddingRight = 10f;
            root.style.paddingTop = 8f;

            var title = new Label("相机控制管理器");
            title.style.fontSize = 17f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 6f;
            root.Add(title);
            root.Add(new PropertyField(
                serializedObject.FindProperty("m_Script")));

            var output = CreateFoldout("输出相机");
            output.Add(CreateProperty(
                serializedObject,
                "outputCamera",
                "目标相机",
                "唯一允许写入 Unity Camera 的引用。"));
            root.Add(output);

            var focus = CreateFoldout("焦点输入");
            focus.Add(CreateProperty(
                serializedObject,
                "focusTarget",
                "焦点目标",
                "简单场景直接绑定目标 Transform。"));
            focus.Add(CreateProperty(
                serializedObject,
                "fallbackFocusPoint",
                "备用焦点"));
            focus.Add(CreateProperty(
                serializedObject,
                "useUnscaledTime",
                "使用非缩放时间"));
            root.Add(focus);

            CameraControlManager manager =
                (CameraControlManager)target;
            var status = CreateFoldout("运行时状态");
            status.Add(CreateReadOnly(
                "当前控制者",
                () => manager != null &&
                       !string.IsNullOrWhiteSpace(
                           manager.ActiveControlName)
                    ? manager.ActiveControlName
                    : "无"));
            status.Add(CreateReadOnly(
                "过渡中",
                () => manager != null &&
                       manager.IsTransitioning
                    ? "是"
                    : "否"));
            status.Add(CreateReadOnly(
                "焦点",
                () => manager != null
                    ? manager.FocusPoint.ToString()
                    : "无"));
            root.Add(status);

            var help = new HelpBox(
                "相机最终姿态只由本组件写入；其他系统通过申请控制权提交需求。",
                HelpBoxMessageType.Info);
            help.style.marginTop = 6f;
            help.style.whiteSpace = WhiteSpace.Normal;
            root.Add(help);

            root.Bind(serializedObject);
            return root;
        }

        private static Foldout CreateFoldout(
            string title,
            bool expanded = true)
        {
            var foldout = new Foldout
            {
                text = title,
                value = expanded
            };
            foldout.style.marginTop = 6f;
            return foldout;
        }

        private static PropertyField CreateProperty(
            SerializedObject serializedObject,
            string path,
            string label,
            string tooltip = null)
        {
            var field = new PropertyField(
                serializedObject.FindProperty(path),
                label);
            if (!string.IsNullOrEmpty(tooltip))
            {
                field.tooltip = tooltip;
            }

            return field;
        }

        private static VisualElement CreateReadOnly(
            string label,
            Func<string> getter)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginBottom = 2f;
            var name = new Label(label);
            name.style.width = 96f;
            name.style.opacity = .72f;
            row.Add(name);
            var value = new Label();
            value.style.flexGrow = 1f;
            value.style.whiteSpace = WhiteSpace.Normal;
            row.Add(value);
            Action refresh = () =>
                value.text = getter != null
                    ? getter() ?? "无"
                    : "无";
            refresh();
            row.schedule.Execute(refresh).Every(120);
            return row;
        }
    }
}
