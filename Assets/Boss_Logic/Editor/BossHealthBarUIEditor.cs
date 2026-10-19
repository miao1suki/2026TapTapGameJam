using Project.BossLogic;
using Project.Editor;
using UnityEditor;
using UnityEngine.UIElements;

[CustomEditor(typeof(BossHealthBarUI))]
public sealed class BossHealthBarUIEditor : Editor
{
    public override VisualElement CreateInspectorGUI()
    {
        VisualElement root = ProjectInspectorUtility.CreateRoot(
            serializedObject);
        root.Add(ProjectInspectorUtility.CreateTitle("Boss 血条"));
        root.Add(ProjectInspectorUtility.CreateScriptField(
            serializedObject));
        root.Add(ProjectInspectorUtility.CreateHelp(
            "血条在 BossController.ActivateHealthSystem 调用后才初始化，从 0 快速涨满；之后肉红快速下降，纯白血皮较慢跟随。外框以外保持透明。"));

        Foldout layout = ProjectInspectorUtility.CreateFoldout(
            "布局",
            true);
        Add(layout, "barWidth", "血条长度");
        Add(layout, "barHeight", "血条高度");
        Add(layout, "topOffset", "屏幕顶部偏移");
        Add(layout, "dividerWidth", "分段黑线宽度");
        root.Add(layout);

        Foldout name = ProjectInspectorUtility.CreateFoldout(
            "Boss 名字",
            true);
        Add(name, "uiSdfFont", "统一 SDF 字体");
        Add(name, "nameFontSize", "名字字号");
        Add(name, "nameMinFontSize", "自动缩小下限");
        Add(name, "nameMaxWidth", "名字最大宽度");
        Add(name, "nameGap", "与血条间距");
        Add(name, "nameColor", "名字颜色");
        Add(name, "nameOutlineWidth", "描边宽度");
        Add(name, "nameOutlineColor", "描边颜色");
        root.Add(name);

        Foldout frame = ProjectInspectorUtility.CreateFoldout(
            "外框",
            true);
        Add(frame, "frameThickness", "外框宽度");
        Add(frame, "frameAccentLength", "角部强调长度");
        Add(frame, "frameColor", "外框主色");
        Add(frame, "frameAccentColor", "角部强调色");
        root.Add(frame);

        Foldout colors = ProjectInspectorUtility.CreateFoldout(
            "颜色",
            true);
        Add(colors, "healthColor", "肉红血量颜色");
        Add(colors, "trailColor", "纯白拖尾血量颜色");
        Add(colors, "dividerColor", "硬边竖分隔线");
        root.Add(colors);

        Foldout animation = ProjectInspectorUtility.CreateFoldout(
            "动画",
            true);
        Add(animation, "initialFillDuration", "初始化涨满时长");
        Add(animation, "healthLerpSpeed", "红血变化速度");
        Add(animation, "trailLerpSpeed", "白色血皮变化速度");
        root.Add(animation);

        BossHealthBarUI ui = target as BossHealthBarUI;
        Foldout debug = ProjectInspectorUtility.CreateFoldout(
            "运行时调试",
            false);
        debug.Add(ProjectInspectorUtility.CreateReadOnlyRow(
            "已初始化",
            () => ui != null && ui.IsInitialized ? "是" : "否"));
        debug.Add(ProjectInspectorUtility.CreateReadOnlyRow(
            "分段数量",
            () => ui != null ? ui.SegmentCount.ToString() : "无"));
        debug.Add(ProjectInspectorUtility.CreateReadOnlyRow(
            "红血显示值",
            () => ui != null
                ? ui.DisplayedHealth.ToString("0.###")
                : "无"));
        debug.Add(ProjectInspectorUtility.CreateReadOnlyRow(
            "血皮显示值",
            () => ui != null
                ? ui.DisplayedTrail.ToString("0.###")
                : "无"));
        debug.Add(new Button(() =>
        {
            BossController owner =
                ui != null
                    ? ui.GetComponentInParent<BossController>()
                    : null;
            owner?.ActivateHealthSystem();
        })
        {
            text = "调试开启血条与受伤"
        });
        root.Add(debug);

        ProjectInspectorUtility.Bind(root, serializedObject);
        return root;
    }

    private void Add(
        VisualElement parent,
        string propertyName,
        string label)
    {
        parent.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            propertyName,
            label));
    }
}
