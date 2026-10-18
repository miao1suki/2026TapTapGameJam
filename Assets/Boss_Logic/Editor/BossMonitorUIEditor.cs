using Project.BossLogic;
using Project.Editor;
using UnityEditor;
using UnityEngine.UIElements;

[CustomEditor(typeof(BossMonitorUI))]
public sealed class BossMonitorUIEditor : Editor
{
    public override VisualElement CreateInspectorGUI()
    {
        VisualElement root =
            ProjectInspectorUtility.CreateRoot(
                serializedObject);
        root.Add(ProjectInspectorUtility.CreateTitle(
            "Boss 监控窗"));
        root.Add(ProjectInspectorUtility.CreateScriptField(
            serializedObject));
        root.Add(ProjectInspectorUtility.CreateHelp(
            "使用独立正交相机和 RenderTexture 实时监控 Boss。窗口紧贴屏幕左上角，默认宽为屏幕约 23.3%、高为 35%；出现和退场都从中心向四周展开。相机显示除 UI 外的全部图层。"));

        Foldout layout = ProjectInspectorUtility.CreateFoldout(
            "窗口布局",
            true);
        Add(layout, "screenWidthFraction", "屏幕宽度比例");
        Add(layout, "screenHeightFraction", "屏幕高度比例");
        Add(layout, "frameThickness", "外框宽度");
        root.Add(layout);

        Foldout appearance = ProjectInspectorUtility.CreateFoldout(
            "外观",
            true);
        Add(appearance, "backgroundColor", "背景颜色");
        Add(appearance, "frameColor", "外框主色");
        Add(appearance, "frameAccentColor", "角部强调色");
        root.Add(appearance);

        Foldout animation = ProjectInspectorUtility.CreateFoldout(
            "出现与退场",
            true);
        Add(animation, "showDuration", "展开时长");
        Add(animation, "hideDuration", "收起时长");
        Add(animation, "showEase", "展开缓动");
        Add(animation, "hideEase", "收起缓动");
        Add(animation, "hideDelayAfterDeath", "死亡后延迟收起");
        root.Add(animation);

        Foldout camera = ProjectInspectorUtility.CreateFoldout(
            "监控相机",
            true);
        Add(camera, "monitorZoomMultiplier", "正交尺寸倍率");
        Add(camera, "fallbackCameraDistance", "备用相机距离");
        Add(camera, "renderResolutionScale", "渲染分辨率比例");
        Add(camera, "maximumRenderWidth", "最大渲染宽度");
        root.Add(camera);

        BossMonitorUI ui = target as BossMonitorUI;
        Foldout debug = ProjectInspectorUtility.CreateFoldout(
            "运行时调试",
            false);
        debug.Add(ProjectInspectorUtility.CreateReadOnlyRow(
            "窗口显示",
            () => ui != null && ui.IsVisible ? "是" : "否"));
        debug.Add(ProjectInspectorUtility.CreateReadOnlyRow(
            "RenderTexture",
            () => ui != null && ui.MonitorTexture != null
                ? $"{ui.MonitorTexture.width}×{ui.MonitorTexture.height}"
                : "未创建"));
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
