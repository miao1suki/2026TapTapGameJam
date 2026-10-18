using Project.BossLogic;
using Project.Editor;
using UnityEditor;
using UnityEngine.UIElements;

[CustomEditor(typeof(BossCameraSize))]
public sealed class BossCameraSizeEditor : Editor
{
    public override VisualElement CreateInspectorGUI()
    {
        VisualElement root =
            ProjectInspectorUtility.CreateRoot(
                serializedObject);
        root.Add(ProjectInspectorUtility.CreateTitle(
            "Boss 存活期视野"));
        root.Add(ProjectInspectorUtility.CreateScriptField(
            serializedObject));
        root.Add(ProjectInspectorUtility.CreateHelp(
            "组件默认不生效。开启后向 CameraControlManager 申请玩法正交视野倍率；Timeline 相机接管期间不覆盖演出镜头，Boss 死亡时平滑恢复。玩家死亡或复活不会撤销此申请。"));

        Foldout request =
            ProjectInspectorUtility.CreateFoldout(
                "视野申请",
                true);
        request.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "sizeMultiplier",
            "视野倍率"));
        request.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "blendDuration",
            "平滑时间（秒）"));
        request.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "requestOnEnable",
            "启用时自动申请"));
        root.Add(request);

        BossCameraSize component =
            target as BossCameraSize;
        Foldout debug =
            ProjectInspectorUtility.CreateFoldout(
                "运行时调试",
                true);
        debug.Add(
            ProjectInspectorUtility.CreateReadOnlyRow(
                "申请状态",
                () => component != null &&
                       component.IsRequested
                    ? "已申请"
                    : "未申请"));
        debug.Add(
            ProjectInspectorUtility.CreateReadOnlyRow(
                "请求句柄",
                () => component != null &&
                       component.HasValidLensRequest
                    ? "有效"
                    : "无"));
        debug.Add(new Button(() =>
        {
            component?.RequestEnable();
        })
        {
            text = "调试开启视野"
        });
        debug.Add(new Button(() =>
        {
            component?.RequestDisable();
        })
        {
            text = "调试恢复视野"
        });
        root.Add(debug);

        ProjectInspectorUtility.Bind(
            root,
            serializedObject);
        return root;
    }
}
