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

            Foldout camera = PlayerInspectorFields.CreateFoldout(
                "相机模式申请");
            camera.Add(PlayerInspectorFields.Create(
                serializedObject,
                "cameraModeController",
                "相机模式控制器",
                "留空时运行时查找场景中的 CameraModeController。"));
            camera.Add(PlayerInspectorFields.Create(
                serializedObject,
                "cameraModeTransitionDuration",
                "视角切换时间",
                "申请 2D/3D 切换时使用的过渡时间。"));
            root.Add(camera);

            HelpBox note = new HelpBox(
                "本组件是默认玩家马夫；其他驱动可以调用同一套 PlayerController 命令。",
                HelpBoxMessageType.Info);
            note.style.marginTop = 6;
            root.Add(note);

            root.Bind(serializedObject);
            return root;
        }
    }
}
