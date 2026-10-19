using Project.Editor;
using Project.Levels.ILorR;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

[CustomEditor(typeof(R1SpringCameraGuide)), CanEditMultipleObjects]
public sealed class R1SpringCameraGuideEditor : Editor
{
    public override VisualElement CreateInspectorGUI()
    {
        VisualElement root = ProjectInspectorUtility.CreateRoot(serializedObject);
        root.Add(ProjectInspectorUtility.CreateTitle("R1 弹性植物镜头提示"));
        root.Add(ProjectInspectorUtility.CreateHelp("靠近指定按钮时扩大视野，离开按钮后继续保持；落到指定植物后恢复普通视野。只改镜头，不控制玩家。"));
        Foldout lens = ProjectInspectorUtility.CreateFoldout("镜头大小与过渡");
        Add(lens, "autoFramePlant", "自动保证植物入镜", "默认关闭，直接使用手动尺寸。开启后才自动放大并保持本次最大值。");
        var sizeField = ProjectInspectorUtility.CreateProperty(serializedObject, "expandedSize", "拉远尺寸",
            "手动模式按此尺寸请求，可在触发后实时调大或调小，最低为普通视野；自动模式将此值作为最低尺寸。");
        lens.Add(sizeField);
        Add(lens, "enterSeconds", "拉远时间（秒）");
        Add(lens, "restoreSeconds", "恢复时间（秒）");
        var manualHelp = ProjectInspectorUtility.CreateHelp("手动模式：使用填写的尺寸，可实时调大或调小，最低为普通视野。尺寸太小可能看不到植物。");
        manualHelp.name = "spring-camera-manual-help";
        var autoHelp = ProjectInspectorUtility.CreateHelp("自动模式：填写的尺寸是下限，系统会继续放大以保证植物入镜；本次触发只增不减。");
        autoHelp.name = "spring-camera-auto-help";
        lens.Add(manualHelp);
        lens.Add(autoHelp);
        root.Add(lens);
        Foldout area = ProjectInspectorUtility.CreateFoldout("按钮附近的触发范围");
        Add(area, "approachWidthCells", "范围宽度（格）");
        Add(area, "approachHeightCells", "范围高度（格）");
        Add(area, "approachVerticalOffsetCells", "相对按钮上移（格）");
        area.Add(ProjectInspectorUtility.CreateHelp("选中相机并打开场景辅助线，可见按钮附近的金色范围框。此范围不是实体，不改变关卡碰撞。"));
        root.Add(area);
        Foldout anchors = ProjectInspectorUtility.CreateFoldout("高级：目标与构图", false);
        Add(anchors, "buttonAnchor", "起点按钮");
        Add(anchors, "landingPlantAnchor", "落点弹性植物");
        var margin = ProjectInspectorUtility.CreateProperty(serializedObject, "framingMarginCells", "画面安全边距（格）");
        margin.name = "spring-camera-framing-margin";
        anchors.Add(margin);
        root.Add(anchors);
        void RefreshMode()
        {
            SerializedProperty setting = serializedObject.FindProperty("autoFramePlant");
            bool mixed = setting.hasMultipleDifferentValues;
            bool automatic = setting.boolValue;
            sizeField.label = automatic && !mixed ? "最小拉远尺寸" : "拉远尺寸";
            manualHelp.style.display = !automatic || mixed ? DisplayStyle.Flex : DisplayStyle.None;
            autoHelp.style.display = automatic || mixed ? DisplayStyle.Flex : DisplayStyle.None;
            margin.style.display = automatic || mixed ? DisplayStyle.Flex : DisplayStyle.None;
        }
        RefreshMode();
        root.TrackPropertyValue(serializedObject.FindProperty("autoFramePlant"), _ => RefreshMode());
        var missing = ProjectInspectorUtility.CreateHelp("请指定起点按钮与落点植物。", HelpBoxMessageType.Warning);
        root.Add(missing);
        void RefreshWarning() => missing.style.display = !serializedObject.isEditingMultipleObjects &&
            (target is R1SpringCameraGuide guide) && (guide.ButtonAnchor == null || guide.LandingPlantAnchor == null)
                ? DisplayStyle.Flex : DisplayStyle.None;
        RefreshWarning();
        missing.schedule.Execute(RefreshWarning).Every(250);
        Foldout debug = ProjectInspectorUtility.CreateFoldout("运行状态与调试", false);
        debug.Add(ProjectInspectorUtility.CreateReadOnlyRow("尺寸控制", () => serializedObject.isEditingMultipleObjects
            ? "多选" : target is R1SpringCameraGuide guide && guide.AutomaticFraming ? "自动保证入镜" : "手动尺寸"));
        debug.Add(ProjectInspectorUtility.CreateReadOnlyRow("镜头状态", () => serializedObject.isEditingMultipleObjects
            ? "多选时不显示单体状态" : EditorApplication.isPlaying && target is R1SpringCameraGuide guide
                ? guide.GuideActive ? "拉远中，等待落到植物" : "普通视野，等待接近按钮" : "未运行"));
        debug.Add(ProjectInspectorUtility.CreateReadOnlyRow("本次请求尺寸", () => target is R1SpringCameraGuide guide &&
            !serializedObject.isEditingMultipleObjects && guide.GuideActive ? guide.RequestedSize.ToString("0.00") : "无"));
        var reset = new Button(() => ((R1SpringCameraGuide)target).ResetGuideForTesting()) { text = "释放本次镜头提示" };
        void RefreshButton() => reset.SetEnabled(EditorApplication.isPlaying && !serializedObject.isEditingMultipleObjects);
        RefreshButton();
        reset.schedule.Execute(RefreshButton).Every(200);
        debug.Add(reset);
        debug.Add(ProjectInspectorUtility.CreateHelp("调试释放后，仍在按钮范围内会重新触发；参数长期保存请在退出播放后修改并保存场景。"));
        root.Add(debug);
        ProjectInspectorUtility.Bind(root, serializedObject);
        return root;
    }

    private void Add(VisualElement parent, string name, string label, string tooltip = null) =>
        parent.Add(ProjectInspectorUtility.CreateProperty(serializedObject, name, label, tooltip));
}
