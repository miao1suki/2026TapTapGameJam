using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Project.InputRebinding.Editor
{
    [CustomEditor(typeof(InputBindingBootstrap))]
    public sealed class InputBindingBootstrapEditor :
        UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            serializedObject.Update();
            var root = new VisualElement();
            root.style.paddingLeft = 10f;
            root.style.paddingRight = 10f;
            root.style.paddingTop = 8f;

            var title = new Label("按键重绑定启动器");
            title.style.fontSize = 17f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 6f;
            root.Add(title);
            root.Add(new PropertyField(
                serializedObject.FindProperty("m_Script")));

            var settings = new Foldout
            {
                text = "启动配置",
                value = true
            };
            settings.style.marginTop = 6f;
            settings.Add(new PropertyField(
                serializedObject.FindProperty("loadOnAwake"),
                "启动时读取存档"));
            root.Add(settings);

            InputBindingBootstrap bootstrap =
                (InputBindingBootstrap)target;
            var status = new Foldout
            {
                text = "运行时状态",
                value = true
            };
            status.style.marginTop = 6f;
            var service = new Label();
            System.Action refresh = () => service.text =
                bootstrap != null && bootstrap.Service != null
                    ? "重绑定服务已创建"
                    : "尚未创建";
            refresh();
            status.schedule.Execute(refresh).Every(150);
            status.Add(service);
            root.Add(status);

            var help = new HelpBox(
                "启动时从 InputService 创建按键重绑定服务，并读取本地覆盖。",
                HelpBoxMessageType.Info);
            help.style.marginTop = 6f;
            help.style.whiteSpace = WhiteSpace.Normal;
            root.Add(help);

            root.Bind(serializedObject);
            return root;
        }
    }
}
