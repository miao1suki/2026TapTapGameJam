using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Project.GameFlow.Editor
{
    public static class GameFlowProjectValidator
    {
        [MenuItem("Tools/2026TapTap/游戏流程/检查场景配置", priority = 20)]
        public static void ValidateFromMenu()
        {
            bool valid = Validate(out string report);
            if (valid)
            {
                Debug.Log(report);
            }
            else
            {
                Debug.LogError(report);
            }
        }

        public static bool Validate(out string report)
        {
            List<string> errors = new List<string>();
            GameSceneCatalog catalog =
                AssetDatabase.LoadAssetAtPath<GameSceneCatalog>(
                    GameFlowSceneScaffolder.CatalogPath);
            if (catalog == null)
            {
                errors.Add($"缺少场景目录：{GameFlowSceneScaffolder.CatalogPath}");
            }
            else
            {
                if (catalog.BootstrapScenePath != GameFlowSceneScaffolder.BootstrapPath)
                {
                    errors.Add("Scene Catalog 的 Bootstrap 路径不正确。");
                }

                if (catalog.PersistentScenePaths.Count !=
                    GameFlowSceneScaffolder.PersistentScenePaths.Length)
                {
                    errors.Add("Scene Catalog 的常驻系统场景数量不正确。");
                }

                if (catalog.FlowScenes.Count != 5)
                {
                    errors.Add("Scene Catalog 应包含主菜单、三个关卡和结束演出。");
                }
            }

            for (int index = 0;
                 index < GameFlowSceneScaffolder.CanonicalBuildScenePaths.Length;
                 index++)
            {
                string path = GameFlowSceneScaffolder.CanonicalBuildScenePaths[index];
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
                {
                    errors.Add($"缺少场景：{path}");
                }
            }

            EditorBuildSettingsScene[] buildScenes = EditorBuildSettings.scenes;
            if (buildScenes.Length !=
                GameFlowSceneScaffolder.CanonicalBuildScenePaths.Length)
            {
                errors.Add("Build Settings 的场景数量与标准结构不一致。");
            }

            int compareCount = System.Math.Min(
                buildScenes.Length,
                GameFlowSceneScaffolder.CanonicalBuildScenePaths.Length);
            for (int index = 0; index < compareCount; index++)
            {
                string expected =
                    GameFlowSceneScaffolder.CanonicalBuildScenePaths[index];
                if (buildScenes[index].path != expected || !buildScenes[index].enabled)
                {
                    errors.Add(
                        $"Build Settings 第 {index} 项应为启用状态的 {expected}");
                }
            }

            StringBuilder builder = new StringBuilder();
            if (errors.Count == 0)
            {
                builder.Append(
                    "游戏流程场景配置检查通过：Bootstrap 为构建入口，" +
                    "系统场景和流程场景均已纳入 Build Settings。");
                report = builder.ToString();
                return true;
            }

            builder.AppendLine("游戏流程场景配置检查失败：");
            for (int index = 0; index < errors.Count; index++)
            {
                builder.Append("- ").AppendLine(errors[index]);
            }

            report = builder.ToString();
            return false;
        }
    }
}
