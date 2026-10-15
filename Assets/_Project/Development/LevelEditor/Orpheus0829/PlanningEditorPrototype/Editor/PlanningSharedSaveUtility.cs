using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PlanningEditorPrototype
{
    internal static class PlanningSharedSaveUtility
    {
        private static readonly Dictionary<string, string> observedHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<int, PlanningDocument> pendingDocuments = new Dictionary<int, PlanningDocument>();
        internal static string SceneSavePath(string scenePath) =>
            string.IsNullOrEmpty(scenePath) || !scenePath.StartsWith("Assets/", StringComparison.Ordinal)
                ? null : Path.GetFullPath(Path.ChangeExtension(scenePath, ".planning.json"));

        internal static string AssetPath(string path)
        {
            string root = Path.GetFullPath(Application.dataPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string full = Path.GetFullPath(path);
            return full.StartsWith(root, StringComparison.OrdinalIgnoreCase)
                ? "Assets/" + full.Substring(root.Length).Replace('\\', '/') : null;
        }

        internal static void SaveSceneCopy(Scene scene, PlanningDocument document, string persistentJson = null)
        {
            string path = SceneSavePath(scene.path);
            if (path == null) { pendingDocuments[scene.handle] = document; return; }
            VerifySceneCopy(scene, document);
            PlanningSaveStore.Save(path, document, persistentJson);
            ObserveSceneCopy(scene);
            pendingDocuments.Remove(scene.handle);
        }

        internal static PlanningDocument PendingDocument(Scene scene) =>
            pendingDocuments.TryGetValue(scene.handle, out var document) ? document : null;

        internal static void ForgetScene(Scene scene) => pendingDocuments.Remove(scene.handle);

        internal static void ObserveSceneCopy(Scene scene)
        {
            string path = SceneSavePath(scene.path);
            if (path != null) observedHashes[path] = File.Exists(path) ? Hash128.Compute(File.ReadAllText(path)).ToString() : "";
        }

        internal static void VerifySceneCopy(Scene scene, PlanningDocument document)
        {
            string path = SceneSavePath(scene.path);
            if (path == null || !File.Exists(path) || !observedHashes.TryGetValue(path, out string expected)) return;
            string disk = File.ReadAllText(path);
            if (Hash128.Compute(disk).ToString() == expected || PlanningDocument.FromPersistentJson(disk).ToPersistentJson() == document.ToPersistentJson()) return;
            string backup = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Library", "PlanningConflicts",
                AssetDatabase.AssetPathToGUID(scene.path) + "_local.planning.json"));
            PlanningSaveStore.Save(backup, document);
            throw new IOException("共享存档已被外部更新，已停止覆盖。请重新打开关卡编辑器读取新版本，再导入本地备份：\n" + backup);
        }

        internal static string EmbeddedJson(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return null;
            foreach (var root in scene.GetRootGameObjects())
            {
                var manager = root.GetComponent<Project.Rooms.RoomLifecycleManager>();
                if (manager != null && !string.IsNullOrWhiteSpace(manager.PlanningJson)) return manager.PlanningJson;
            }
            return null;
        }
    }

    // Saving a generated Scene must also produce an editable, versionable planning document.
    [InitializeOnLoad]
    internal static class PlanningSceneSaveProcessor
    {
        static PlanningSceneSaveProcessor()
        {
            EditorSceneManager.sceneSaved += OnSceneSaved;
            EditorSceneManager.sceneClosed += PlanningSharedSaveUtility.ForgetScene;
        }

        private static void OnSceneSaved(Scene scene)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !scene.IsValid() || !scene.isLoaded) return;
            try
            {
                var document = PlanningEditorWindow.DocumentForScene(scene.handle) ?? PlanningSharedSaveUtility.PendingDocument(scene);
                string paired = PlanningSharedSaveUtility.SceneSavePath(scene.path);
                // Do not overwrite a newer editor save with stale generated geometry.
                if (document == null && !File.Exists(paired))
                {
                    string json = PlanningSharedSaveUtility.EmbeddedJson(scene);
                    if (!string.IsNullOrWhiteSpace(json)) document = PlanningDocument.FromPersistentJson(json);
                }
                if (document != null) PlanningSharedSaveUtility.SaveSceneCopy(scene, document);
            }
            catch (Exception exception)
            {
                Debug.LogError("场景已保存，但配套规划存档保存失败。请处理后重新保存场景，再提交关卡。\n" + exception);
            }
        }
    }
}
