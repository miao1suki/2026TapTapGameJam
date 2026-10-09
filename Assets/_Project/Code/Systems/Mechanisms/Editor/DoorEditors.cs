using Project.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Project.Mechanisms.Editor
{
    [CustomEditor(typeof(DoorController)), CanEditMultipleObjects]
    public sealed class DoorControllerEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = ProjectInspectorUtility.CreateRoot(serializedObject);
            root.Add(ProjectInspectorUtility.CreateTitle("按钮解锁门"));
            root.Add(ProjectInspectorUtility.CreateScriptField(serializedObject));
            var links = ProjectInspectorUtility.CreateFoldout("模型与绑定", true);
            foreach (var field in new[] { "hinge", "blockingCollider", "buttons" })
                links.Add(new UnityEditor.UIElements.PropertyField(serializedObject.FindProperty(field)));
            root.Add(links);
            var timing = ProjectInspectorUtility.CreateFoldout("开门与镜头", true);
            timing.Add(ProjectInspectorUtility.CreateProperty(serializedObject,"openDuration","开门时间","门绕侧边轴旋转 90°。"));
            timing.Add(ProjectInspectorUtility.CreateProperty(serializedObject,"cameraMoveDuration","镜头移动时间","移向门中心。"));
            timing.Add(ProjectInspectorUtility.CreateProperty(serializedObject,"cameraHoldDuration","镜头停留时间","门附近的停留时间。"));
            timing.Add(ProjectInspectorUtility.CreateProperty(serializedObject,"cameraReturnDuration","镜头返回时间","平滑交还玩家跟随相机。"));
            timing.Add(ProjectInspectorUtility.CreateProperty(serializedObject,"resetWithRoom","房间重置时关门","响应房间重置事件，恢复门与按钮的初始状态。"));
            root.Add(timing);
            var state = new Label();
            root.Add(state);
            var reset = new Button(() => ((DoorController)target).ResetDoor()) { text = "重置门与按钮" };
            root.Add(reset);
            root.schedule.Execute(() =>
            {
                if (target is not DoorController door) return;
                state.text = "状态：" + (door.State == DoorState.Unlocked ? "已解锁" : "未解锁") + " · 按钮 " + door.Buttons.Count;
                reset.SetEnabled(Application.isPlaying);
            }).Every(200);
            reset.SetEnabled(Application.isPlaying);
            ProjectInspectorUtility.Bind(root,serializedObject);
            return root;
        }
        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
        private static void DrawLinks(DoorController door, GizmoType type)
        {
            var old = Handles.color;
            Handles.color = new Color(1f,.78f,.3f,.9f);
            foreach (var button in door.Buttons)
                if (button != null) Handles.DrawDottedLine(door.transform.TransformPoint(new Vector3(0,.5f,0)),button.transform.position,5f);
            Handles.color = old;
        }
    }
    [CustomEditor(typeof(DoorButton))]
    public sealed class DoorButtonEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = ProjectInspectorUtility.CreateRoot(serializedObject);
            root.Add(ProjectInspectorUtility.CreateTitle("门按钮"));
            root.Add(ProjectInspectorUtility.CreateScriptField(serializedObject));
            var settings = ProjectInspectorUtility.CreateFoldout("交互设置",true);
            var owner = new UnityEditor.UIElements.PropertyField(serializedObject.FindProperty("owner"));
            owner.SetEnabled(false);
            settings.Add(owner);
            settings.Add(ProjectInspectorUtility.CreateProperty(serializedObject,"interactionRadius","交互距离（格）","玩家靠近后按现有交互键；按下后保持状态。"));
            settings.Add(new UnityEditor.UIElements.PropertyField(serializedObject.FindProperty("visual")));
            root.Add(settings);
            var state = new Label();
            root.Add(state);
            root.schedule.Execute(() =>
            {
                if (target is DoorButton button) state.text = "状态：" + (button.IsPressed ? "已按下" : "未按下");
            }).Every(200);
            ProjectInspectorUtility.Bind(root,serializedObject);
            return root;
        }
    }
}
