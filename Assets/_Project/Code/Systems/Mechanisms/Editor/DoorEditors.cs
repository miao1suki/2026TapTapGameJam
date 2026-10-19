using Project.Editor;
using UnityEditor;
using UnityEditor.UIElements;
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
            var timing = ProjectInspectorUtility.CreateFoldout("开门与关门", true);
            timing.Add(ProjectInspectorUtility.CreateProperty(serializedObject,"openDuration","开门时间","门绕侧边轴旋转 90°。"));
            timing.Add(ProjectInspectorUtility.CreateProperty(serializedObject,"unlockDelay","图案展示时间","所有按钮按下后，保留完整彩色图案的时间；有镜头演出时从镜头到达门附近后开始计算。"));
            timing.Add(ProjectInspectorUtility.CreateProperty(serializedObject,"closeDelay","松开后关门延迟","持续压住按钮释放后的通行缓冲；期间重新按齐按钮会取消关门。"));
            timing.Add(ProjectInspectorUtility.CreateProperty(serializedObject,"resetWithRoom","全局进度重置时关门","仅响应显式进度清空；不改变 R 或离开房间的按钮模式规则。"));
            root.Add(timing);
            var performance = ProjectInspectorUtility.CreateFoldout("可选Timeline演出", false);
            var trigger = serializedObject.FindProperty("timelineTrigger");
            performance.Add(ProjectInspectorUtility.CreateProperty(serializedObject,"timelineTrigger","Timeline触发","默认关闭（包括旧场景）。按钮模式每次真实按下触发一次；开门模式仅在全部按钮达成时触发。"));
            var shotSettings = new VisualElement();
            shotSettings.Add(ProjectInspectorUtility.CreateProperty(serializedObject,"lockPlayerDuringTimeline","演出锁定玩家","只在显式开启演出时生效。关闭后看门镜头不冻结玩家。"));
            shotSettings.Add(ProjectInspectorUtility.CreateProperty(serializedObject,"cameraMoveDuration","镜头移动时间","移向门中心。"));
            shotSettings.Add(ProjectInspectorUtility.CreateProperty(serializedObject,"cameraHoldDuration","镜头停留时间","门附近的停留时间。"));
            shotSettings.Add(ProjectInspectorUtility.CreateProperty(serializedObject,"cameraReturnDuration","镜头返回时间","平滑交还玩家跟随相机。"));
            void RefreshShotSettings() => shotSettings.style.display = trigger.hasMultipleDifferentValues || trigger.intValue != 0
                ? DisplayStyle.Flex : DisplayStyle.None;
            performance.Add(shotSettings);
            performance.TrackPropertyValue(trigger, _ => RefreshShotSettings());
            RefreshShotSettings();
            root.Add(performance);
            var state = new Label();
            root.Add(state);
            var reset = new Button(() => ((DoorController)target).ResetDoor()) { text = "重置门与按钮" };
            root.Add(reset);
            root.schedule.Execute(() =>
            {
                if (target is not DoorController door) return;
                state.text = "状态：" + (door.IsWaitingToOpen ? "等待开门" : door.IsWaitingToClose ? "等待关门" : door.State == DoorState.Unlocked ? "已解锁" : "未解锁") + " · 按钮 " + door.PressedCount + "/" + door.Buttons.Count;
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
            settings.Add(ProjectInspectorUtility.CreateProperty(serializedObject,"facing","按钮朝向","同一单格按钮的压力方向。只按90度旋转16像素素材，不拉伸。"));
            settings.Add(ProjectInspectorUtility.CreateProperty(serializedObject,"mode","触发模式","一次按下保持：保存已按进度；持续压住：松开、重生或离开房间时取消触发。"));
            settings.Add(ProjectInspectorUtility.CreateProperty(serializedObject,"footTolerance","脚部容差（格）","玩家脚部或三色接触方块底面压入触发区后按下。"));
            settings.Add(new UnityEditor.UIElements.PropertyField(serializedObject.FindProperty("visual")));
            root.Add(settings);
            var state = new Label();
            root.Add(state);
            root.schedule.Execute(() =>
            {
                if (target is DoorButton button)
                {
                    string color = button.Owner == null ? "未绑定" : button.BindingIndex == 1 ? "绿色" : button.BindingIndex == 2 ? "蓝色" : "红色";
                    state.text = "对应图案：" + color + " · 状态：" + (button.IsPressed ? "已按下" : "未按下");
                }
            }).Every(200);
            ProjectInspectorUtility.Bind(root,serializedObject);
            return root;
        }
    }
}
