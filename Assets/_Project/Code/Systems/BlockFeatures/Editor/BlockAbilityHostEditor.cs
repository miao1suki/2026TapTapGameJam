using System.Collections.Generic;
using Project.ColorBlocks;
using Project.ColorBlocks.Editor;
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
                "方块能力控制"));
            root.Add(ProjectInspectorUtility.CreateScriptField(
                serializedObject));
            root.Add(ProjectInspectorUtility.CreateHelp(
                "根据方块当前颜色，精确启停对应的功能组件。"));

            BlockAbilityCatalog catalog =
                BlockAbilityCatalogService.Load();

            BlockAbilityHost host = (BlockAbilityHost)target;
            Foldout status = ProjectInspectorUtility.CreateFoldout(
                "运行时状态（只读）",
                true);
            status.Add(ProjectInspectorUtility.CreateReadOnlyRow(
                "当前颜色",
                () => host != null
                    ? FormatColor(host.CurrentColorId)
                    : "无"));
            status.Add(ProjectInspectorUtility.CreateReadOnlyRow(
                "状态来源",
                () => Application.isPlaying
                    ? "游戏运行中，由玩法颜色变化自动切换"
                    : "编辑模式，不执行玩法切换"));
            status.Add(ProjectInspectorUtility.CreateReadOnlyRow(
                "已启用组件",
                () => FormatFeatures(host, true)));
            status.Add(ProjectInspectorUtility.CreateReadOnlyRow(
                "已禁用组件",
                () => FormatFeatures(host, false)));
            status.Add(ProjectInspectorUtility.CreateHelp(
                "当前颜色和组件启停状态只读，只能由游戏内颜色变化触发；" +
                "不要在 Inspector 手动启停 BlockFeature。"));
            root.Add(status);

            if (catalog == null)
            {
                Label missing = new Label(
                    "能力目录尚未生成；重新编译项目后会自动扫描 BlockFeature。");
                missing.style.whiteSpace = WhiteSpace.Normal;
                missing.style.color = new Color(1f, .65f, .3f);
                missing.style.marginTop = 6f;
                root.Add(missing);
            }

            Foldout options = ProjectInspectorUtility.CreateFoldout(
                "运行选项",
                true);
            options.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "debugLog",
                "输出切换日志",
                "颜色切换并更新能力时输出日志。"));
            root.Add(options);

            ProjectInspectorUtility.Bind(root, serializedObject);
            return root;
        }

        private static string FormatColor(string colorId)
        {
            if (string.IsNullOrWhiteSpace(colorId))
            {
                return "未指定";
            }

            ColorCatalog catalog =
                AssetDatabase.LoadAssetAtPath<ColorCatalog>(
                    ColorProjectSetup.CatalogPath);
            ColorTypeDefinition definition = catalog?.Find(colorId);
            return definition != null &&
                   !string.IsNullOrWhiteSpace(definition.displayName)
                ? $"{definition.displayName} ({colorId})"
                : colorId;
        }

        private static string FormatFeatures(
            BlockAbilityHost host,
            bool enabled)
        {
            if (host == null)
            {
                return "无";
            }

            var enabledFeatures = new List<BlockFeature>();
            var disabledFeatures = new List<BlockFeature>();
            host.CollectFeatureStates(
                enabledFeatures,
                disabledFeatures);
            List<BlockFeature> features = enabled
                ? enabledFeatures
                : disabledFeatures;
            if (features.Count == 0)
            {
                return "无";
            }

            var names = new List<string>();
            for (int index = 0; index < features.Count; index++)
            {
                BlockFeature feature = features[index];
                string displayName = feature != null
                    ? feature.Metadata.DisplayName
                    : string.Empty;
                names.Add(string.IsNullOrWhiteSpace(displayName)
                    ? feature != null
                        ? feature.GetType().Name
                        : "未知组件"
                    : displayName);
            }

            return string.Join("、", names);
        }
    }
}
