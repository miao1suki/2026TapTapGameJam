using Project.Editor;
using Project.GreenHook;
using UnityEditor;
using UnityEngine.UIElements;

namespace Project.GreenHook.Editor
{
    [CustomEditor(typeof(GreenHookAbility))]
    public sealed class GreenHookAbilityEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            serializedObject.Update();
            VisualElement root = ProjectInspectorUtility.CreateRoot(
                serializedObject);
            root.Add(ProjectInspectorUtility.CreateTitle("绿钩子"));
            root.Add(ProjectInspectorUtility.CreateScriptField(
                serializedObject));
            root.Add(ProjectInspectorUtility.CreateHelp(
                "选中绿色后，藤蔓随指针自由瞄准，与红蓝线共用出线起点。" +
                "有效点击立即挂接，四向拉动物体或以绿色物体为挂点荡绳；连接中可点击其他目标切换。" +
                "钩子可穿过方块，但不能跨出当前房间。"));

            Foldout idle = ProjectInspectorUtility.CreateFoldout(
                "待机藤蔓头",
                true);
            idle.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "idleLengthCells",
                "无传感器时后备范围（格）",
                "正常待机绳尖跟随鼠标位置，并限制在染色范围内；仅缺少传感器时使用此值。"));
            idle.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "idleOpacity",
                "待机透明度",
                "待机预览藤蔓的整体透明度。"));
            root.Add(idle);

            Foldout movement = ProjectInspectorUtility.CreateFoldout(
                "挂接与拉动",
                true);
            movement.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "pullSpeed",
                "拉动速度",
                "目标或玩家向另一侧移动的速度。"));
            movement.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "contactPadding",
                "接触停止距离",
                "碰撞体距离小于该值后视为已经接触。"));
            movement.Add(ProjectInspectorUtility.CreateHelp("挂接后保持连接，跳跃或交互键主动脱钩。物体受阻时拉近玩家；绿色固定物体作为荡绳锚点。"));
            movement.Add(ProjectInspectorUtility.CreateProperty(serializedObject, "attachedRangeSlackCells", "挂接范围冗余（格）",
                "只增加已挂接连接的维持距离，不扩大可点击距离。默认额外3格。"));
            movement.Add(ProjectInspectorUtility.CreateProperty(serializedObject, "attachedOutOfRangeGraceSeconds", "持续超距宽限（秒）",
                "连续超过维持距离达到此时间才断绳，回到范围内即清空计时。默认0.4秒，暂停不计时。"));
            movement.Add(ProjectInspectorUtility.CreateProperty(serializedObject, "swingAcceleration", "荡绳加速度"));
            movement.Add(ProjectInspectorUtility.CreateProperty(serializedObject, "swingSpeedLimit", "荡绳速度上限"));
            movement.Add(ProjectInspectorUtility.CreateProperty(serializedObject, "swingDamping", "荡绳阻尼", "松开方向输入后动量衰减速度；0为无额外阻尼。"));
            movement.Add(ProjectInspectorUtility.CreateProperty(serializedObject, "retargetVisualSeconds", "换点视觉衔接（秒）", "切换挂点时绳尖的短过渡，不暂停玩家或清除惯性。"));
            root.Add(movement);

            Foldout visual = ProjectInspectorUtility.CreateFoldout(
                "藤蔓表现",
                false);
            visual.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "hookOpacity",
                "正式藤蔓透明度"));
            visual.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "finishDissolveSeconds",
                "结束溶解时长"));
            visual.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "sourceOffset",
                "后备出线偏移",
                "无红蓝连接组件时使用；存在该组件时共用其出线锚点和偏移。"));
            visual.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "cameraFacingOffset",
                "相机朝向偏移"));
            visual.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "curveSegments",
                "曲线段数"));
            visual.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "arcHeight",
                "弯曲幅度"));
            visual.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "waveHeight",
                "摆动幅度"));
            visual.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "noiseStrength",
                "噪波强度"));
            visual.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "effectSpeed",
                "效果速度"));
            root.Add(visual);

            GreenHookAbility ability = target as GreenHookAbility;
            Foldout state = ProjectInspectorUtility.CreateFoldout(
                "运行时状态（只读）",
                true);
            state.Add(ProjectInspectorUtility.CreateReadOnlyRow(
                "钩子状态",
                () => ability == null
                    ? "无"
                    : ability.IsSwinging ? "荡绳中" : ability.IsExtending
                        ? "伸出中"
                        : ability.IsRetracting
                            ? "收回中"
                        : ability.IsPulling
                            ? "拉动中"
                            : ability.IsHookActive
                                ? "收尾中"
                                : "待机"));
            root.Add(state);

            ProjectInspectorUtility.Bind(root, serializedObject);
            return root;
        }
    }
}
