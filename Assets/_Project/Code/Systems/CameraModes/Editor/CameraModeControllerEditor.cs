using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Project.CameraModes.Editor
{
    [CustomEditor(typeof(CameraModeController)), CanEditMultipleObjects]
    public sealed class CameraModeControllerEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            root.Add(new Label("平面相机") { style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 16 } });
            root.Add(new HelpBox("固定使用 XY 平面正交镜头，支持平移、缩放和绕画面旋转。演出结束后归还原有镜头控制权。", HelpBoxMessageType.Info));
            var binding = new Foldout { text = "控制与跟随", value = true };
            binding.Add(new PropertyField(serializedObject.FindProperty("cameraManager"), "相机管理器"));
            binding.Add(new PropertyField(serializedObject.FindProperty("controlPriority"), "控制优先级"));
            root.Add(binding);
            var framing = new Foldout { text = "构图", value = true };
            SerializedProperty settings = serializedObject.FindProperty("side2D");
            framing.Add(new PropertyField(settings.FindPropertyRelative("orthographicSize"), "正交半高"));
            framing.Add(new PropertyField(settings.FindPropertyRelative("verticalOffset"), "观察高度偏移"));
            framing.Add(new PropertyField(settings.FindPropertyRelative("yawDegrees"), "画面旋转角度"));
            var depth = new Foldout { text = "高级深度设置", value = false };
            depth.Add(new PropertyField(settings.FindPropertyRelative("distance"), "表现层观察距离"));
            depth.Add(new PropertyField(settings.FindPropertyRelative("depthOffset"), "表现层深度偏移"));
            framing.Add(depth);
            root.Add(framing);
            var transitions = new Foldout { text = "过渡与事件", value = false };
            transitions.Add(new PropertyField(serializedObject.FindProperty("transition"), "镜头过渡"));
            transitions.Add(new PropertyField(serializedObject.FindProperty("onTransitionStarted"), "开始过渡"));
            transitions.Add(new PropertyField(serializedObject.FindProperty("onModeChanged"), "模式变更"));
            root.Add(transitions);
            var state = new Label();
            root.Add(state);
            root.schedule.Execute(() =>
            {
                if (targets.Length != 1) { state.text = "已选择多个相机"; return; }
                var controller = (CameraModeController)target;
                state.text = controller && controller.Manager ? "当前控制者：" + controller.Manager.ActiveControlName : "尚未绑定管理器";
            }).Every(250);
            root.Add(new Button(CameraModeSetupWindow.OpenWindow) { text = "打开相机工具" });
            return root;
        }
    }
}