using Project.BossLogic;
using Project.Editor;
using UnityEditor;
using UnityEngine.UIElements;

[CustomEditor(typeof(BossDefeatTimelinePresenter))]
public sealed class BossDefeatTimelinePresenterEditor :
    Editor
{
    public override VisualElement CreateInspectorGUI()
    {
        VisualElement root =
            ProjectInspectorUtility.CreateRoot(
                serializedObject);
        root.Add(ProjectInspectorUtility.CreateTitle(
            "Boss 死亡演出"));
        root.Add(ProjectInspectorUtility.CreateScriptField(
            serializedObject));
        root.Add(ProjectInspectorUtility.CreateHelp(
            "由 BossController 的“死亡演出请求”调用。只有进入 Death 状态才会播放，普通 Destroy 不会触发。Timeline 内部负责镜头、死亡停留在和玩家操作锁。"));

        Foldout timeline =
            ProjectInspectorUtility.CreateFoldout(
                "播放配置",
                true);
        timeline.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "director",
            "Playable Director"));
        timeline.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "defeatTimeline",
            "死亡 Timeline"));
        timeline.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "onlyPlayForThisBoss",
            "仅响应本 Boss"));
        root.Add(timeline);

        BossDefeatTimelinePresenter presenter =
            target as BossDefeatTimelinePresenter;
        Foldout debug =
            ProjectInspectorUtility.CreateFoldout(
                "运行时调试",
                false);
        debug.Add(
            ProjectInspectorUtility.CreateReadOnlyRow(
                "播放中",
                () => presenter != null &&
                       presenter.IsPlaying
                    ? "是"
                    : "否"));
        debug.Add(new Button(() =>
        {
            presenter?.StopDefeatPresentation();
        })
        {
            text = "停止死亡演出"
        });
        root.Add(debug);

        ProjectInspectorUtility.Bind(
            root,
            serializedObject);
        return root;
    }
}
