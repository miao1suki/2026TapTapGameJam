using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Project.InputAbstraction.Editor
{
    internal static class InputInspectorUi
    {
        public static VisualElement Root(
            SerializedObject serializedObject,
            string title)
        {
            var root = new VisualElement();
            root.style.paddingLeft = 10f;
            root.style.paddingRight = 10f;
            root.style.paddingTop = 8f;
            var label = new Label(title);
            label.style.fontSize = 17f;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginBottom = 6f;
            root.Add(label);
            root.Add(new PropertyField(
                serializedObject.FindProperty("m_Script")));
            return root;
        }

        public static Foldout Foldout(
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

        public static PropertyField Property(
            SerializedObject serializedObject,
            string path,
            string label,
            string tooltip = null)
        {
            SerializedProperty property =
                serializedObject.FindProperty(path);
            var field = new PropertyField(property, label);
            if (!string.IsNullOrEmpty(tooltip))
            {
                field.tooltip = tooltip;
            }

            return field;
        }

        public static VisualElement ReadOnly(
            string label,
            Func<string> getter)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginBottom = 2f;
            var name = new Label(label);
            name.style.width = 88f;
            name.style.opacity = .72f;
            row.Add(name);
            var value = new Label();
            value.style.flexGrow = 1f;
            row.Add(value);
            Action refresh = () =>
                value.text = getter != null
                    ? getter() ?? "无"
                    : "无";
            refresh();
            row.schedule.Execute(refresh).Every(120);
            return row;
        }

        public static HelpBox Help(string text)
        {
            var help = new HelpBox(
                text,
                HelpBoxMessageType.Info);
            help.style.marginTop = 6f;
            help.style.whiteSpace = WhiteSpace.Normal;
            return help;
        }
    }

    [CustomEditor(typeof(InputService))]
    public sealed class InputServiceEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            serializedObject.Update();
            VisualElement root = InputInspectorUi.Root(
                serializedObject,
                "输入服务");
            Foldout input = InputInspectorUi.Foldout("输入源");
            input.Add(InputInspectorUi.Property(
                serializedObject,
                "actionAsset",
                "输入资产"));
            input.Add(InputInspectorUi.Property(
                serializedObject,
                "platformMode",
                "平台模式"));
            input.Add(InputInspectorUi.Property(
                serializedObject,
                "persistAcrossSceneLoads",
                "跨场景保留"));
            root.Add(input);

            InputService service = (InputService)target;
            Foldout status = InputInspectorUi.Foldout("运行时状态");
            status.Add(InputInspectorUi.ReadOnly(
                "实例",
                () => service != null &&
                       InputService.Instance != null
                    ? "已创建"
                    : "未创建"));
            status.Add(InputInspectorUi.ReadOnly(
                "实际平台",
                () => service != null
                    ? service.ResolvedPlatformMode.ToString()
                    : "无"));
            root.Add(status);
            root.Add(InputInspectorUi.Help(
                "InputService 是硬件和虚拟输入的公共入口，其他脚本通过 GameInput 读取。"));
            root.Bind(serializedObject);
            return root;
        }
    }

    [CustomEditor(typeof(VirtualJoystick))]
    public sealed class VirtualJoystickEditor :
        UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            serializedObject.Update();
            VisualElement root = InputInspectorUi.Root(
                serializedObject,
                "虚拟摇杆");
            Foldout bindings = InputInspectorUi.Foldout("输入绑定");
            bindings.Add(InputInspectorUi.Property(
                serializedObject,
                "action",
                "动作"));
            bindings.Add(InputInspectorUi.Property(
                serializedObject,
                "background",
                "背景区域"));
            bindings.Add(InputInspectorUi.Property(
                serializedObject,
                "handle",
                "摇杆头"));
            root.Add(bindings);

            Foldout tuning = InputInspectorUi.Foldout("手感");
            tuning.Add(InputInspectorUi.Property(
                serializedObject,
                "deadZone",
                "死区"));
            tuning.Add(InputInspectorUi.Property(
                serializedObject,
                "movementRadius",
                "最大半径",
                "0 表示使用背景区域短边的一半。"));
            root.Add(tuning);

            VirtualJoystick joystick = (VirtualJoystick)target;
            Foldout status = InputInspectorUi.Foldout("运行时状态");
            status.Add(InputInspectorUi.ReadOnly(
                "当前值",
                () => joystick != null
                    ? joystick.Value.ToString()
                    : "无"));
            root.Add(status);
            root.Bind(serializedObject);
            return root;
        }
    }

    [CustomEditor(typeof(VirtualInputButton))]
    public sealed class VirtualInputButtonEditor :
        UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            serializedObject.Update();
            VisualElement root = InputInspectorUi.Root(
                serializedObject,
                "虚拟输入按钮");
            Foldout binding = InputInspectorUi.Foldout("输入绑定");
            binding.Add(InputInspectorUi.Property(
                serializedObject,
                "action",
                "动作"));
            root.Add(binding);

            VirtualInputButton button =
                (VirtualInputButton)target;
            Foldout status = InputInspectorUi.Foldout("运行时状态");
            status.Add(InputInspectorUi.ReadOnly(
                "按下",
                () => button != null && button.IsPressed
                    ? "是"
                    : "否"));
            root.Add(status);
            root.Bind(serializedObject);
            return root;
        }
    }
}
