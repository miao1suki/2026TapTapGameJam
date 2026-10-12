using Project.Editor;
using Project.WordNote;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Project.WordNote.Editor
{
    [CustomEditor(typeof(WordNoteUI))]
    public sealed class WordNoteUIEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            serializedObject.Update();
            VisualElement root = ProjectInspectorUtility.CreateRoot(
                serializedObject);
            root.Add(ProjectInspectorUtility.CreateTitle(
                "全局文字提示"));
            root.Add(ProjectInspectorUtility.CreateScriptField(
                serializedObject));
            root.Add(ProjectInspectorUtility.CreateHelp(
                "常驻 UI 场景中的文字提示画布。消息先进入总队列，" +
                "再进入对应位置队列；同一位置一次只显示一条。" +
                "DOTween 负责入场和退场。"));

            Foldout defaults = ProjectInspectorUtility.CreateFoldout(
                "默认显示配置",
                true);
            defaults.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "defaultFont",
                "字体文件",
                "默认使用 Cubic_11 SDF；单条消息可以覆盖。"));
            defaults.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "defaultFontSize",
                "默认字号",
                "没有单独指定字号时使用。"));
            defaults.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "defaultFontColor",
                "默认字体颜色",
                "七原色或白黑；没有单独指定时使用。"));
            defaults.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "defaultUseOutline",
                "默认启用描边",
                "是否默认给字体加描边。"));
            defaults.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "defaultOutlineColor",
                "默认描边颜色",
                "七原色或白黑；默认描边时使用。"));
            defaults.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "defaultOutlineWidth",
                "默认描边宽度",
                "TMP SDF 描边宽度。"));
            defaults.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "defaultOutlineSoftness",
                "默认描边柔化",
                "TMP SDF 描边柔化程度。"));
            defaults.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "defaultPosition",
                "默认位置",
                "没有单独指定显示位置时使用。"));
            defaults.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "defaultParticipateInStackLayout",
                "默认参与堆叠排序",
                "开启时按位置列表顺序向下排列；关闭时固定站在该位置第一格，不占用其他消息的堆叠位置。"));
            defaults.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "defaultDisplayMode",
                "默认显示模式",
                "驻留、定时显示或转瞬即逝。"));
            defaults.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "defaultDisplayDuration",
                "默认显示时长",
                "仅“显示一段时间”模式使用。"));
            defaults.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "defaultUseEnterAnimation",
                "启用默认入场动画",
                "关闭后消息直接进入显示状态。"));
            defaults.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "defaultEnterAnimation",
                "默认入场动画",
                "FROM 类型的选择方向在入场时表示从该方向进入。"));
            defaults.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "defaultEnterMotion",
                "默认入场参数",
                "初末位置、初末透明度、缩放、初末速度和变速比例。"));
            defaults.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "defaultUseExitAnimation",
                "启用默认退场动画",
                "关闭后消息立即消失。"));
            defaults.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "defaultExitAnimation",
                "默认退场动画",
                "FROM 类型的选择方向在退场时表示朝该方向离开。"));
            defaults.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "defaultExitMotion",
                "默认退场参数",
                "初末位置、初末透明度、缩放、初末速度和变速比例。"));
            defaults.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "sortingOrder",
                "排序层级",
                "建议高于 GameUI 的 1000。"));
            root.Add(defaults);

            Foldout stacking = ProjectInspectorUtility.CreateFoldout(
                "同位置堆叠",
                true);
            stacking.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "stackSpacing",
                "消息上下间距",
                "同一位置多条消息显示时的垂直间距，单位为 Canvas 像素。"));
            stacking.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "stackShiftDuration",
                "补位移动时长",
                "前一条消息消失后，后方消息向上补位的 DOTween 时长。"));
            stacking.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "stackShiftEase",
                "补位缓动",
                "后方消息补位时的 DOTween 缓动方式。"));
            root.Add(stacking);

            Foldout debug = ProjectInspectorUtility.CreateFoldout(
                "运行时调试",
                false);
            Label state = new Label();
            state.style.whiteSpace = WhiteSpace.Normal;
            state.style.opacity = .85f;
            debug.Add(state);
            debug.Add(new Button(() =>
            {
                WordNoteUI.Instance?.ShowTimed(
                    "居中提示",
                    2f,
                    WordNotePosition.Center);
            })
            {
                text = "测试居中提示"
            });
            debug.Add(new Button(() =>
            {
                WordNoteUI.Instance?.ShowTimed(
                    "右下提示",
                    2f,
                    WordNotePosition.BottomRight);
            })
            {
                text = "测试右下提示"
            });
            debug.Add(new Button(() =>
            {
                WordNoteUI.Instance?.ExitAll(true);
            })
            {
                text = "全部退场（保留驻留）"
            });
            debug.Add(new Button(() =>
            {
                WordNoteUI.Instance?.DisappearAll();
            })
            {
                text = "全部立即消失（保留驻留）"
            });
            root.Add(debug);

            root.schedule.Execute(() =>
            {
                WordNoteUI ui = target as WordNoteUI;
                if (ui == null)
                {
                    return;
                }

                state.text = Application.isPlaying
                    ? ui.GetAnimationDebugString()
                    : "进入 Play Mode 后显示消息队列状态";
            }).Every(150);

            ProjectInspectorUtility.Bind(root, serializedObject);
            return root;
        }
    }
}
