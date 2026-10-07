using System.Collections.Generic;
using System.Text;
using Project.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Project.BlockFeatures.Editor
{
    [CustomEditor(typeof(BlockAbilityHost))]
    public sealed class BlockAbilityHostEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            serializedObject.Update();
            VisualElement root = ProjectInspectorUtility.CreateRoot(
                serializedObject);
            root.Add(ProjectInspectorUtility.CreateTitle(
                "颜色物功能控制"));
            root.Add(ProjectInspectorUtility.CreateScriptField(
                serializedObject));
            root.Add(ProjectInspectorUtility.CreateHelp(
                "只根据固定颜色组的解锁状态，启停预制体上已经存在的功能组件。" +
                "不会在运行时查目录、添加组件或切换形态。"));

            BlockAbilityHost host = (BlockAbilityHost)target;
            Foldout status = ProjectInspectorUtility.CreateFoldout(
                "运行时状态（只读）",
                true);
            status.Add(ProjectInspectorUtility.CreateReadOnlyRow(
                "固定颜色",
                () => string.IsNullOrWhiteSpace(host.BaseColorId)
                    ? "未指定"
                    : host.BaseColorId));
            status.Add(ProjectInspectorUtility.CreateReadOnlyRow(
                "功能组件",
                () => BuildFeatures(host)));
            root.Add(status);

            Foldout options = ProjectInspectorUtility.CreateFoldout(
                "运行选项",
                true);
            options.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "debugLog",
                "输出启停日志",
                "颜色解锁或房间重置时输出组件启停日志。"));
            root.Add(options);

            ProjectInspectorUtility.Bind(root, serializedObject);
            return root;
        }

        private static string BuildFeatures(BlockAbilityHost host)
        {
            if (host == null || host.Features.Count == 0)
            {
                return "无固定功能组件";
            }

            var builder = new StringBuilder();
            for (int index = 0; index < host.Features.Count; index++)
            {
                BlockFeature feature = host.Features[index];
                if (feature == null)
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append("、");
                }

                string displayName = feature.Metadata.DisplayName;
                builder.Append(string.IsNullOrWhiteSpace(displayName)
                    ? feature.GetType().Name
                    : displayName);
                builder.Append(feature.enabled ? "（启用）" : "（停用）");
            }

            return builder.Length > 0
                ? builder.ToString()
                : "无固定功能组件";
        }
    }
}
