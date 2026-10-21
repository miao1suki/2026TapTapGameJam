using Project.BossLogic;
using Project.Editor;
using UnityEditor;
using UnityEngine.UIElements;

namespace Project.BossLogic.Editor
{
    [CustomEditor(typeof(BossCollectibleEntryGate)), CanEditMultipleObjects]
    public sealed class BossCollectibleEntryGateEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = ProjectInspectorUtility.CreateRoot(serializedObject);
            root.Add(ProjectInspectorUtility.CreateTitle("Boss 收集进度门槛"));
            root.Add(ProjectInspectorUtility.CreateScriptField(serializedObject));
            root.Add(ProjectInspectorUtility.CreateProperty(serializedObject, "gateEnabled", "是否启用该组件"));
            var settings = ProjectInspectorUtility.CreateFoldout("入场与返回位置");
            settings.Add(ProjectInspectorUtility.CreateProperty(serializedObject, "minimumRatio", "最低收集比例"));
            settings.Add(ProjectInspectorUtility.CreateProperty(serializedObject, "returnPoint", "门外返回位置"));
            settings.Add(ProjectInspectorUtility.CreateProperty(serializedObject, "returnDestinationId", "未达标返回目标ID"));
            settings.Add(ProjectInspectorUtility.CreateProperty(serializedObject, "showHint", "显示未达标提示"));
            root.Add(settings);
            var state = ProjectInspectorUtility.CreateFoldout("运行状态", false);
            state.Add(ProjectInspectorUtility.CreateReadOnlyRow("收集进度", () => target is BossCollectibleEntryGate gate
                ? $"{gate.Progress.Collected}/{gate.Progress.Total}" : "无"));
            state.Add(ProjectInspectorUtility.CreateReadOnlyRow("门槛状态", () => target is BossCollectibleEntryGate gate && gate.GateEnabled
                ? "启用" : "已关闭"));
            state.Add(ProjectInspectorUtility.CreateReadOnlyRow("可以入场", () => target is BossCollectibleEntryGate gate && gate.CanEnter ? "是" : "否"));
            root.Add(state);
            root.Add(ProjectInspectorUtility.CreateHelp("挂到Boss入场Timeline触发器。低于30%时拒绝播放且不消耗一次触发机会，默认返回同场景Start。正式Boss与关卡最终合入Gaming；独立Boss测试缺少Start时只拒绝并警告，不加载其他场景。Scene与planning必须同步提交。"));
            ProjectInspectorUtility.Bind(root, serializedObject);
            return root;
        }
    }
}
