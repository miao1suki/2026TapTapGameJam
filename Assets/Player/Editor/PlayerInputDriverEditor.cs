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

            Foldout carryHint = PlayerInspectorFields.CreateFoldout(
                "携带提示");
            carryHint.Add(PlayerInspectorFields.Create(
                serializedObject,
                "carryHintFont",
                "携带提示字体",
                "显示搬运按键提示时使用的 TMP 字体；留空时使用字幕管理器默认字体。"));
            root.Add(carryHint);

            HelpBox note = new HelpBox(
                "调色由 Tab 开关轮盘，" +
                "交互/拾取由 F 处理，Q/E 放下左手侧/右手侧，" +
                "左键负责已选颜色的染色。" +
                "重生默认使用键盘 R、手柄右摇杆按下，" +
                "存活或死亡均可返回最近触碰的重生点，并恢复相关区域物体。" +
                "其他驱动可以调用同一套 PlayerController 命令。",
                HelpBoxMessageType.Info);
            note.style.marginTop = 6;
            root.Add(note);

            root.Bind(serializedObject);
            return root;
        }
    }
}
