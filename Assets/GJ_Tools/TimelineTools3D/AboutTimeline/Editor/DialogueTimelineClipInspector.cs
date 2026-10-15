using Project.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

[CustomEditor(typeof(DialogueTimelineClip))]
public sealed class DialogueTimelineClipInspector : Editor
{
    private VisualElement typewriterSettings;
    private VisualElement textStyleSettings;
    private VisualElement bubbleSettings;
    private VisualElement riseSettings;
    private VisualElement fadeSettings;

    public override VisualElement CreateInspectorGUI()
    {
        VisualElement root = ProjectInspectorUtility.CreateRoot(
            serializedObject);
        root.Add(ProjectInspectorUtility.CreateTitle("对话气泡轨道"));
        root.Add(ProjectInspectorUtility.CreateScriptField(
            serializedObject));
        root.Add(ProjectInspectorUtility.CreateHelp(
            "绑定角色 Transform 后，片段播放时会在角色头上创建气泡。" +
            "气泡、文字、动画参数全部可在下方调整。"));

        Foldout textGroup = ProjectInspectorUtility.CreateFoldout(
            "文本内容",
            true);
        textGroup.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "dialogueText",
            "对话内容",
            "气泡中显示的完整文本，支持 TMP 富文本。"));
        textGroup.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "font",
            "字体",
            "必须使用 Cubic_11 SDF；留空时读取默认配置。"));

        textStyleSettings = new VisualElement();
        textStyleSettings.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "fontSize",
            "基础字号"));
        textStyleSettings.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "autoSizeText",
            "自动缩放文字"));
        textStyleSettings.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "minFontSize",
            "自动缩放最小字号"));
        textStyleSettings.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "lineSpacing",
            "行距"));
        textStyleSettings.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "alignment",
            "对齐方式"));
        textStyleSettings.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "textColor",
            "文字颜色"));
        textStyleSettings.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "richText",
            "允许富文本"));
        textStyleSettings.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "enableTextOutline",
            "启用描边"));
        textStyleSettings.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "textOutlineColor",
            "描边颜色"));
        textStyleSettings.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "textOutlineWidth",
            "描边宽度"));
        textGroup.Add(textStyleSettings);

        textGroup.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "revealTextByCharacters",
            "逐个显示文字"));
        typewriterSettings = new VisualElement();
        typewriterSettings.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "typewriterStartDelay",
            "上浮后等待"));
        typewriterSettings.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "charactersPerSecond",
            "每秒字符数"));
        textGroup.Add(typewriterSettings);
        root.Add(textGroup);

        Foldout appearanceGroup =
            ProjectInspectorUtility.CreateFoldout(
                "气泡外观",
                true);
        appearanceGroup.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "bubbleSprite",
            "气泡 Sprite",
            "留空时读取 DialogueBubbleDefaults。"));
        appearanceGroup.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "bubbleMaterial",
            "气泡材质",
            "留空时读取 DialogueBubbleDefaults。"));
        bubbleSettings = new VisualElement();
        bubbleSettings.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "bubbleTint",
            "气泡染色"));
        bubbleSettings.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "bubbleSize",
            "气泡大小"));
        bubbleSettings.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "textPadding",
            "文字留白"));
        bubbleSettings.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "preserveAspect",
            "保持原始比例"));
        bubbleSettings.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "sliced",
            "九宫格缩放"));
        bubbleSettings.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "sortingLayerName",
            "排序层"));
        bubbleSettings.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "sortingOrder",
            "排序序号"));
        bubbleSettings.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "followBinding",
            "跟随绑定物体"));
        bubbleSettings.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "faceCamera",
            "朝向主相机"));
        appearanceGroup.Add(bubbleSettings);
        root.Add(appearanceGroup);

        Foldout motionGroup =
            ProjectInspectorUtility.CreateFoldout(
                "浮现与退场",
                false);
        riseSettings = new VisualElement();
        riseSettings.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "startLocalOffset",
            "起始本地偏移"));
        riseSettings.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "endLocalOffset",
            "结束本地偏移"));
        riseSettings.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "riseDuration",
            "上浮时长"));
        riseSettings.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "riseCurve",
            "上浮曲线"));
        riseSettings.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "startScale",
            "起始缩放"));
        riseSettings.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "endScale",
            "结束缩放"));
        motionGroup.Add(riseSettings);

        fadeSettings = new VisualElement();
        fadeSettings.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "fadeInDuration",
            "淡入时长"));
        fadeSettings.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "fadeInCurve",
            "淡入曲线"));
        fadeSettings.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "fadeOutDuration",
            "淡出时长"));
        fadeSettings.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "fadeOutCurve",
            "淡出曲线"));
        motionGroup.Add(fadeSettings);
        root.Add(motionGroup);

        root.Add(ProjectInspectorUtility.CreateHelp(
            "默认配置资源位于 Resources/UI/DialogueBubbleDefaults。" +
            "轨道未指定素材时会自动使用该资源，并强制读取项目字体。",
            HelpBoxMessageType.Info));

        serializedObject.Update();
        ProjectInspectorUtility.Bind(root, serializedObject);
        root.TrackSerializedObjectValue(
            serializedObject,
            _ => RefreshVisibility());
        RefreshVisibility();
        return root;
    }

    private void RefreshVisibility()
    {
        SetDisplay(
            typewriterSettings,
            serializedObject
                .FindProperty("revealTextByCharacters")
                .boolValue);
        SetDisplay(
            textStyleSettings,
            true);
        SetDisplay(
            bubbleSettings,
            true);
        SetDisplay(
            riseSettings,
            true);
        SetDisplay(
            fadeSettings,
            true);
    }

    private static void SetDisplay(
        VisualElement element,
        bool visible)
    {
        if (element != null)
        {
            element.style.display = visible
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }
    }
}
