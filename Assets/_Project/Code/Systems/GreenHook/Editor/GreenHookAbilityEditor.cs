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
                "命中后四向拉动物体，或以绿色物体为挂点荡绳；连接中可点击其他目标切换。" +
                "钩子可穿过方块，但不能跨出当前房间。"));

            Foldout idle = ProjectInspectorUtility.CreateFoldout(
                "待机藤蔓头",
                true);
            idle.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "idleLengthCells",
                "待机长度（格）",
                "绿色选中时玩家身上探出的预览藤蔓长度。"));
            idle.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "idleOpacity",
                "待机透明度",
                "待机预览藤蔓的整体透明度。"));
            root.Add(idle);

            Foldout movement = ProjectInspectorUtility.CreateFoldout(
                "钩子伸缩与拉动",
                true);
            movement.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "growthSpeedCellsPerSecond",
                "伸长速度（格/秒）",
                "正式藤蔓从玩家伸向目标的生长速度。"));
            movement.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "hookCatchRadiusCells",
                "命中判定半径（格）",
                "藤蔓头与目标碰撞体的最近距离进入该范围后算抓到目标。"));
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
