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
            foreach (var field in new[] { "hinge", "blockingCollider", "buttons", "hint" })
                links.Add(new UnityEditor.UIElements.PropertyField(serializedObject.FindProperty(field)));
            root.Add(links);
            root.Add(new Button(() =>
            {
                var door = (DoorController)target;
                Undo.RecordObject(door, "刷新门绑定");
                door.SetButtons(door.Buttons);
                EditorUtility.SetDirty(door);
            }) { text = "刷新按钮绑定与提示" });
            root.Add(ProjectInspectorUtility.CreateProperty(serializedObject,"openEuler","开门旋转轴","局部门轴旋转；竖门绕 Y，横门绕 X。"));
            var timing = ProjectInspectorUtility.CreateFoldout("开门与镜头", true);
            timing.Add(ProjectInspectorUtility.CreateProperty(serializedObject,"openDuration","开门时间","门绕侧边轴旋转 90°。"));
            timing.Add(ProjectInspectorUtility.CreateProperty(serializedObject,"cameraMoveDuration","镜头移动时间","移向门中心。"));
            timing.Add(ProjectInspectorUtility.CreateProperty(serializedObject,"cameraHoldDuration","镜头停留时间","门附近的停留时间。"));
            timing.Add(ProjectInspectorUtility.CreateProperty(serializedObject,"cameraReturnDuration","镜头返回时间","平滑交还玩家跟随相机。"));
            timing.Add(ProjectInspectorUtility.CreateProperty(serializedObject,"resetWithRoom","全局进度重置时关门","仅响应显式进度清空；不改变 R 或离开房间的按钮模式规则。"));
            root.Add(timing);
            var state = new Label();
            root.Add(state);
            var reset = new Button(() => ((DoorController)target).ResetDoor()) { text = "重置门与按钮" };
            root.Add(reset);
            root.schedule.Execute(() =>
            {
                if (target is not DoorController door) return;
                state.text = "状态：" + (door.State == DoorState.Unlocked ? "已解锁" : "未解锁") + " · 按钮 " + door.PressedCount + "/" + door.Buttons.Count;
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
            var settings = ProjectInspectorUtility.CreateFoldout("踩踏设置",true);
            var owner = new UnityEditor.UIElements.PropertyField(serializedObject.FindProperty("owner"));
            owner.SetEnabled(false);
            settings.Add(owner);
            settings.Add(ProjectInspectorUtility.CreateProperty(serializedObject,"mode","触发模式","一次按下保持：保存已按进度；持续压住：松开、重生或离开房间时取消触发。"));
            settings.Add(ProjectInspectorUtility.CreateProperty(serializedObject,"footTolerance","脚部容差（格）","玩家脚部或三色接触方块底面压入触发区后按下。"));
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
