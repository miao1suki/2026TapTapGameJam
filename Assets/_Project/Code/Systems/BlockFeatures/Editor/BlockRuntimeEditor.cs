using System.Text;
using Project.Editor;
using UnityEditor;
using UnityEngine.UIElements;

namespace Project.BlockFeatures.Editor
{
    [CustomEditor(typeof(BlockRuntime))]
    public sealed class BlockRuntimeEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            serializedObject.Update();
            VisualElement root = ProjectInspectorUtility.CreateRoot(
                serializedObject);
            root.Add(ProjectInspectorUtility.CreateTitle(
                "方块运行时"));
            root.Add(ProjectInspectorUtility.CreateScriptField(
                serializedObject));

            Foldout dispatch =
                ProjectInspectorUtility.CreateFoldout(
                    "派发设置",
                    true);
            dispatch.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "maxDispatchPasses",
                "最大派发次数",
                "单帧内允许信号和命令连续传播的最大轮数。"));
            dispatch.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "maxDebugEvents",
                "调试事件上限",
                "只影响 Inspector 中保留的最近事件数量。"));
            root.Add(dispatch);

            Foldout features =
                ProjectInspectorUtility.CreateFoldout(
                    "已接入功能",
                    true);
            features.Add(ProjectInspectorUtility.CreateReadOnlyRow(
                "生效组件",
                () => BuildFeatures((BlockRuntime)target)));
            root.Add(features);

            Foldout debug = ProjectInspectorUtility.CreateFoldout(
                "最近事件",
                false);
            debug.Add(ProjectInspectorUtility.CreateReadOnlyRow(
                "事件记录",
                () => BuildEvents((BlockRuntime)target)));
            root.Add(debug);

            ProjectInspectorUtility.Bind(root, serializedObject);
            return root;
        }

        private static string BuildFeatures(BlockRuntime runtime)
        {
            if (runtime == null || runtime.Features.Count == 0)
            {
                return "无；运行时只接入已启用的 BlockFeature。";
            }

            var builder = new StringBuilder();
            for (int index = 0;
                 index < runtime.Features.Count;
                 index++)
            {
                BlockFeature feature = runtime.Features[index];
                if (feature == null)
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append("、");
                }

                builder.Append(feature.Metadata.DisplayName);
            }

            return builder.Length > 0 ? builder.ToString() : "无";
        }

        private static string BuildEvents(BlockRuntime runtime)
        {
            if (runtime == null || runtime.DebugEvents.Count == 0)
            {
                return "暂无事件。";
            }

            var builder = new StringBuilder();
            int start = System.Math.Max(
                0,
                runtime.DebugEvents.Count - 8);
            for (int index = start;
                 index < runtime.DebugEvents.Count;
                 index++)
            {
                BlockDebugEvent entry = runtime.DebugEvents[index];
                if (entry == null)
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append('\n');
                }

                builder.Append(
                    $"{entry.Time:0.00}s · {entry.Kind} · " +
                    entry.Message);
            }

            return builder.ToString();
        }
    }
}
