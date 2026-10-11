using Project.BlockFeatures;
using Project.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Project.BlockFeatures.Editor
{
    [CustomEditor(typeof(CarryableFeature))]
    public sealed class CarryableFeatureEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            serializedObject.Update();
            VisualElement root = ProjectInspectorUtility.CreateRoot(
                serializedObject);
            root.Add(ProjectInspectorUtility.CreateTitle(
                "可搬运物"));
            root.Add(ProjectInspectorUtility.CreateScriptField(
                serializedObject));
            root.Add(ProjectInspectorUtility.CreateHelp(
                "持有该组件的物体可以按交互键搬到玩家头顶。" +
                "若同物体存在 IColorObject，则只有对应颜色解锁后" +
                "组件才允许搬运；万能方块没有 IColorObject，因此常态可搬。"));

            Foldout config = ProjectInspectorUtility.CreateFoldout(
                "搬运设置",
                true);
            config.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "canCarry",
                "允许搬运",
                "关闭后即使进入玩家相邻格也不会进入拾取队列。"));
            config.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "indicatorFont",
                "搬运提示字体",
                "第一个可搬运目标正中心的“搬运”字体。"));
            config.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "indicatorFontSize",
                "搬运提示字号",
                "世界空间“搬运”文字的字号。"));
            config.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "indicatorOffset",
                "搬运提示偏移",
                "相对于物体正中心的本地偏移。"));
            config.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "indicatorOutlineWidth",
                "搬运提示描边宽度",
                "白色描边宽度；过大会让黑色字面显得发灰。"));
            root.Add(config);

            CarryableFeature feature = target as CarryableFeature;
            Foldout state = ProjectInspectorUtility.CreateFoldout(
                "运行时状态（只读）",
                true);
            state.Add(ProjectInspectorUtility.CreateReadOnlyRow(
                "可搬运",
                () => feature != null &&
                      feature.CanCarryNow
                    ? "是"
                    : "否"));
            state.Add(ProjectInspectorUtility.CreateReadOnlyRow(
                "搬运状态",
                () => feature != null
                    ? feature.IsCarryResetAnimating
                        ? "复位中"
                        : feature.IsCarryDropAnimating
                            ? "放下中"
                            : feature.IsCarryActive
                                ? "携带中"
                                : "未搬运"
                    : "无"));
            root.Add(state);

            ProjectInspectorUtility.Bind(root, serializedObject);
            return root;
        }
    }
}
