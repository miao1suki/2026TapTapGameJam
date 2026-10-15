using Project.Editor;
using Project.WordNote;
using UnityEditor;
using UnityEngine.UIElements;

[CustomEditor(typeof(DialogueTimelineClip))]
public sealed class DialogueTimelineClipInspector : Editor
{
    public override VisualElement CreateInspectorGUI()
    {
        VisualElement root = ProjectInspectorUtility.CreateRoot(
            serializedObject);
        root.Add(ProjectInspectorUtility.CreateTitle("对话轨道"));
        root.Add(ProjectInspectorUtility.CreateScriptField(
            serializedObject));
        root.Add(ProjectInspectorUtility.CreateHelp(
            "轨道直接向 WordNote_UI 申请使用权，消息与原 API 共用同一个画布和位置队列；轨道不需要绑定对象。"));

        Foldout content = ProjectInspectorUtility.CreateFoldout(
            "文本内容",
            true);
        content.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "dialogueText",
            "对话内容",
            "完整文本，支持 WordNote 的富文本格式。"));
        root.Add(content);

        Foldout style = ProjectInspectorUtility.CreateFoldout(
            "WordNote 样式",
            true);
        style.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "style.font",
            "字体",
            "留空时继承 WordNote_UI 的默认字体。"));
        style.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "style.fontSize",
            "字号"));
        style.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "style.richText",
            "允许富文本"));
        style.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "style.fontColor",
            "字体颜色"));
        style.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "style.useOutline",
            "启用描边"));
        style.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "style.outlineColor",
            "描边颜色"));
        style.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "style.textAlignment",
            "文字对齐"));
        style.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "style.position",
            "显示位置"));
        style.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "style.participateInStackLayout",
            "参与堆叠排序"));
        style.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "style.displayMode",
            "显示模式"));
        style.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "style.displayDuration",
            "显示时长",
            "仅“显示一段时间”模式使用。"));
        root.Add(style);

        Foldout enter = ProjectInspectorUtility.CreateFoldout(
            "入场动画",
            true);
        enter.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "style.useEnterAnimation",
            "启用入场动画"));
        enter.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "style.enterAnimation",
            "入场动画类型"));
        AddMotionFields(enter, "style.enterMotion", true);
        root.Add(enter);

        Foldout exit = ProjectInspectorUtility.CreateFoldout(
            "出场动画",
            true);
        exit.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "style.useExitAnimation",
            "启用出场动画"));
        exit.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "style.exitAnimation",
            "出场动画类型"));
        AddMotionFields(exit, "style.exitMotion", false);
        root.Add(exit);

        root.Add(ProjectInspectorUtility.CreateHelp(
            "Timeline 片段结束时只释放本次申请的消息句柄；其他 WordNote API 消息继续留在原队列中。",
            HelpBoxMessageType.Info));

        ProjectInspectorUtility.Bind(root, serializedObject);
        return root;
    }

    private void AddMotionFields(
        VisualElement parent,
        string path,
        bool entering)
    {
        parent.Add(ProjectInspectorUtility.CreateHelp(
            entering
                ? "入场参数与 WordNoteMotionSettings 一致。"
                : "出场参数与 WordNoteMotionSettings 一致。"));
        parent.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            path + ".duration",
            "动画时长"));
        parent.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            path + ".useCombinedChannels",
            "使用组合通道"));
        parent.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            path + ".useFade",
            "显隐"));
        parent.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            path + ".useHorizontalMove",
            "水平移动"));
        parent.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            path + ".horizontalDirection",
            "水平方向"));
        parent.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            path + ".horizontalDistance",
            "水平距离"));
        parent.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            path + ".useVerticalMove",
            "竖直移动"));
        parent.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            path + ".verticalDirection",
            "竖直方向"));
        parent.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            path + ".verticalDistance",
            "竖直距离"));
        parent.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            path + ".useScale",
            "缩放"));
        parent.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            path + ".startScale",
            "起始缩放"));
        parent.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            path + ".endScale",
            "结束缩放"));
        parent.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            path + ".useTypewriter",
            "打字机"));
        parent.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            path + ".typewriterCharactersPerSecond",
            "每秒字符数"));
        parent.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            path + ".startPosition",
            "起始位置"));
        parent.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            path + ".endPosition",
            "结束位置"));
        parent.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            path + ".startAlpha",
            "起始透明度"));
        parent.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            path + ".endAlpha",
            "结束透明度"));
        parent.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            path + ".startSpeed",
            "起始速度"));
        parent.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            path + ".endSpeed",
            "结束速度"));
        parent.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            path + ".easePower",
            "变速比例"));
    }
}
