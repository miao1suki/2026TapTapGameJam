using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Project.GridArt;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project.LevelEditor.Editor
{
    internal static class SceneContentWorkspaceAudit
    {
        internal static void RunFromCommandLine()
        {
            Scene prior = SceneManager.GetActiveScene();
            Scene test = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SceneContentWorkspace window = null;
            var palette = ScriptableObject.CreateInstance<GridArtPalette>();
            var map = ScriptableObject.CreateInstance<GridArtMap>();
            string report = "Library/EditorWorkflowValidation/workspace.txt";
            try
            {
                SceneManager.SetActiveScene(test);
                palette.brushes.Add(new GridArtBrush { label = "测试墙面", category = "地形" });
                map.palette = palette;
                map.layers.Add(new GridArtLayer { label = "测试层" });
                var root = new GameObject("测试美术");
                root.AddComponent<GridArtScene>().map = map;
                int undo = Undo.GetCurrentGroup();
                bool dirty = test.isDirty;
                string mapBefore = EditorJsonUtility.ToJson(map);
                var projectPalette = AssetDatabase.LoadAssetAtPath<LevelEditorPalette>(ScenePlacementPaletteCatalog.PalettePath);
                string templatesBefore = EditorJsonUtility.ToJson(projectPalette);
                var catalog = SceneContentCatalog.Build(test);
                Require(catalog.Any(x => x.IsArt && x.title == "测试墙面"), "美术笔刷未进入统一内容库");
                Require(catalog.Count(x => !x.IsArt) == ScenePlacementPaletteCatalog.Build(projectPalette).Count, "方块/道具模板缺失");
                Require(catalog.Any(x => !x.IsArt && x.template.isProp), "机关道具模板缺失");
                Require(catalog.Count(x => SceneContentCatalog.Matches(x, "美术", "测试墙")) == 1, "搜索或分类不正确");
                window = EditorWindow.GetWindow<SceneContentWorkspace>();
                window.CreateGUI();
                Invoke(window, "Select", catalog.First(x => x.IsArt));
                Invoke(window, "StartPainting");
                Require(SceneContentWorkspace.OwnsAuthoring, "无法进入绘制会话");
                Invoke(window, "Refresh");
                Require(SceneContentWorkspace.OwnsAuthoring, "目录更新错误终止了绘制会话");
                Invoke(window, "Filter", true);
                Require(!SceneContentWorkspace.OwnsAuthoring, "用户切换筛选时未停止旧笔刷");
                window.Close();
                window = null;
                Require(Undo.GetCurrentGroup() == undo, "浏览创建了 Undo 组");
                Require(test.isDirty == dirty, "打开/关闭改变了 Scene dirty 状态");
                Require(EditorJsonUtility.ToJson(map) == mapBefore, "浏览改变了美术作者数据");
                Require(EditorJsonUtility.ToJson(projectPalette) == templatesBefore, "浏览改变了模板资产");
                Directory.CreateDirectory(Path.GetDirectoryName(report));
                File.WriteAllText(report, "PASS 内容目录覆盖方块/道具/美术\nPASS 分类与搜索\nPASS 进入绘制、更新目录保留会话、切换筛选结束旧笔刷\nPASS UI 创建/关闭不增加 Undo、不改 Scene dirty、不改 Map/Palette\n");
                Debug.Log(File.ReadAllText(report));
            }
            catch (Exception exception)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(report));
                File.WriteAllText(report, "FAIL " + exception);
                Debug.LogException(exception);
                EditorApplication.Exit(1);
                return;
            }
            finally
            {
                if (window != null) window.Close();
                if (prior.IsValid()) SceneManager.SetActiveScene(prior);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                UnityEngine.Object.DestroyImmediate(map);
                UnityEngine.Object.DestroyImmediate(palette);
            }
            EditorApplication.Exit(0);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void Invoke(SceneContentWorkspace window, string method, params object[] args) =>
            typeof(SceneContentWorkspace).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, args);
    }
}
