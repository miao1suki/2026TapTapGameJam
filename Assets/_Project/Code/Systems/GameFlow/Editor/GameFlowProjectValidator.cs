using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Project.GameFlow.Editor
{
    public static class GameFlowProjectValidator
    {
        [MenuItem("Tools/2026TapTap/场景/检查场景配置", priority = 20)]
        public static void ValidateFromMenu()
        {
            bool valid = Validate(out string report);
            if (valid) Debug.Log(report); else Debug.LogError(report);
        }

        public static bool Validate(out string report)
        {
            var errors = new List<string>();
            var catalog = AssetDatabase.LoadAssetAtPath<GameSceneCatalog>(GameFlowSceneScaffolder.CatalogPath);
            if (catalog != null)
            {
                foreach (string path in catalog.PersistentScenePaths) CheckPath(path, errors);
                foreach (var entry in catalog.FlowScenes)
                    if (entry != null) CheckPath(entry.ScenePath, errors);
            }
            var seen = new HashSet<string>();
            foreach (var scene in EditorBuildSettings.scenes)
            {
                if (!scene.enabled) continue;
                CheckPath(scene.path, errors);
                if (!seen.Add(scene.path)) errors.Add("构建场景重复：" + scene.path);
            }
            if (!seen.Contains(GameFlowSceneScaffolder.GamingPath)) errors.Add("正式构建必须从 Gaming 启动。");
            if (seen.Contains(GameFlowSceneScaffolder.MainMenuPath)) errors.Add("MainMenu 仅为兼容入口，不得加入正式构建。");
            report = errors.Count == 0
                ? "场景配置检查通过。Gaming 单场景启动，同场景菜单与游玩。"
                : "场景配置检查失败：\n- " + string.Join("\n- ", errors);
            return errors.Count == 0;
        }

        private static void CheckPath(string path, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(path) || AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
                errors.Add("场景不存在：" + path);
        }
    }
}
