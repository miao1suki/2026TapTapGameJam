using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Project.Player.Editor
{
    [CustomEditor(typeof(PlayerInputDriver))]
    public sealed class PlayerInputDriverEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = new VisualElement();
            root.Add(PlayerInspectorFields.CreateScriptField(
                serializedObject));

            Foldout bindings = PlayerInspectorFields.CreateFoldout(
                "输入动作绑定");
            bindings.Add(PlayerInspectorFields.Create(
                serializedObject,
                "actionBindings",
                "动作绑定",
                "输入动作到 Timeline 动作数据的映射。"));
            root.Add(bindings);

            HelpBox note = new HelpBox(
                "本组件是默认玩家马夫；调色由 Tab/F 开关轮盘，" +
                "交互由 E 处理，左键负责已选颜色的染色。" +
                "死亡后只响应重生键，默认键盘 R、手柄右摇杆按下。" +
                "其他驱动可以调用同一套 PlayerController 命令。",
                HelpBoxMessageType.Info);
            note.style.marginTop = 6;
            root.Add(note);

            root.Bind(serializedObject);
            return root;
        }
    }
}
