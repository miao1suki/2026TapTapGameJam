using System;
using System.Text;
using Project.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Project.ColorBlocks.Editor
{
    [CustomEditor(typeof(ColorObject))]
    public sealed class ColorObjectEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            serializedObject.Update();
            VisualElement root = ProjectInspectorUtility.CreateRoot(
                serializedObject);
            root.Add(ProjectInspectorUtility.CreateTitle(
                "颜色物体"));
            root.Add(ProjectInspectorUtility.CreateScriptField(
                serializedObject));
            root.Add(ProjectInspectorUtility.CreateHelp(
                "固定颜色物体只负责解锁分组和视觉状态。" +
                "形态由预制体上的具体功能组件决定，不运行颜色切换。"));

            Foldout config = ProjectInspectorUtility.CreateFoldout(
                "颜色配置",
                true);
            config.Add(ColorInspectorChoices.Create(
                serializedObject,
                "baseColorTypeId",
                "解锁颜色"));
            config.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "targetRenderer",
                "状态渲染器",
                "留空时运行时自动查找第一个子渲染器。"));
            root.Add(config);

            ColorObject colorObject = (ColorObject)target;
            Foldout state = ProjectInspectorUtility.CreateFoldout(
                "运行时状态（只读）",
                true);
            state.Add(ProjectInspectorUtility.CreateReadOnlyRow(
                "当前状态",
                () => colorObject != null && colorObject.IsActive
                    ? "已激活"
                    : "失效/未解锁"));
            state.Add(ProjectInspectorUtility.CreateReadOnlyRow(
                "解锁颜色",
                () => colorObject != null
                    ? colorObject.BaseColorTypeId
                    : "无"));
            root.Add(state);

            ProjectInspectorUtility.Bind(root, serializedObject);
            return root;
        }
    }

    [CustomEditor(typeof(UniversalColorBlock))]
    public sealed class UniversalColorBlockEditor :
        UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            serializedObject.Update();
            VisualElement root = ProjectInspectorUtility.CreateRoot(
                serializedObject);
            root.Add(ProjectInspectorUtility.CreateTitle(
                "万能方块代理"));
            root.Add(ProjectInspectorUtility.CreateScriptField(
                serializedObject));
            root.Add(ProjectInspectorUtility.CreateHelp(
                "万能方块固定放置。点击后自身显示纯色，并把同一颜色" +
                "指令广播给左右上下四个紧邻的 IColorApplicationTarget。"));

            UniversalColorBlock proxy =
                (UniversalColorBlock)target;
            Foldout config = ProjectInspectorUtility.CreateFoldout(
                "染色设置",
                true);
            config.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "fadeMode",
                "染色持续方式",
                "持续染色会保留到房间重置或再次点击；延迟褪色会恢复中性色。"));
            PropertyField fadeDelay =
                ProjectInspectorUtility.CreateProperty(
                    serializedObject,
                    "fadeDelaySeconds",
                    "褪色延迟（秒）",
                    "选择“过一会褪色”后，等待多久恢复中性色。");
            config.Add(fadeDelay);
            config.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "statusRenderer",
                "自身状态渲染器",
                "显示万能方块当前的纯色；留空时自动查找自身 Renderer。"));

            SerializedProperty fadeModeProperty =
                serializedObject.FindProperty("fadeMode");
            Action refreshFadeDelay = () =>
            {
                fadeDelay.style.display =
                    fadeModeProperty != null &&
                    fadeModeProperty.intValue ==
                    (int)UniversalColorBlockFadeMode
                        .FadeAfterDelay
                        ? DisplayStyle.Flex
                        : DisplayStyle.None;
            };
            refreshFadeDelay();
            fadeDelay.schedule.Execute(refreshFadeDelay)
                .Every(100);
            root.Add(config);

            Foldout state = ProjectInspectorUtility.CreateFoldout(
                "运行时状态（只读）",
                true);
            state.Add(ProjectInspectorUtility.CreateReadOnlyRow(
                "当前模式",
                () => proxy != null
                    ? (proxy.FadeMode ==
                       UniversalColorBlockFadeMode.Persistent
                        ? "持续染色"
                        : "过一会褪色")
                    : "无"));
            state.Add(ProjectInspectorUtility.CreateReadOnlyRow(
                "当前颜色",
                () => proxy != null &&
                      !string.IsNullOrWhiteSpace(proxy.CurrentColorId)
                    ? proxy.CurrentColorId
                    : "无"));
            state.Add(ProjectInspectorUtility.CreateReadOnlyRow(
                "本次广播目标数",
                () => proxy != null
                    ? proxy.LastBroadcastTargetCount.ToString()
                    : "0"));
            state.Add(ProjectInspectorUtility.CreateReadOnlyRow(
                "褪色倒计时",
                () => proxy != null &&
                      proxy.FadeRemainingSeconds >= 0f
                    ? $"{proxy.FadeRemainingSeconds:0.00} 秒"
                    : "未计时"));
            root.Add(state);

            Foldout debug = ProjectInspectorUtility.CreateFoldout(
                "运行时调试",
                false);
            var clear = new Button(() =>
            {
                proxy?.ClearColor();
            })
            {
                text = "立即恢复中性色"
            };
            clear.SetEnabled(Application.isPlaying);
            debug.Add(clear);
            root.Add(debug);

            ProjectInspectorUtility.Bind(root, serializedObject);
            return root;
        }
    }
}
