using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEngine;

namespace PlanningEditorPrototype
{
    internal sealed class PlanningSaveInfo
    {
        public string Path;
        public string Name;
        public DateTime LastWriteTime;
    }

    internal static class PlanningSaveStore
    {
        private const string FolderName = "PlanningEditorSaves";

        internal static string SaveFolder =>
            Path.GetFullPath(
                Path.Combine(
                    Application.dataPath,
                    "_Project", "Content", "LevelMaps"));

        internal static string LegacySaveFolder => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Library", FolderName));

        internal static List<PlanningSaveInfo> LoadAll()
        {
            var result = new List<PlanningSaveInfo>();
            try
            {
                string folder = EnsureSaveFolder();
                var files = new List<string>(Directory.GetFiles(folder, "*.json", SearchOption.AllDirectories));
                if (Directory.Exists(LegacySaveFolder)) files.AddRange(Directory.GetFiles(LegacySaveFolder, "*.json"));
                string sceneSave = PlanningSharedSaveUtility.SceneSavePath(SceneManager.GetActiveScene().path);
                if (!string.IsNullOrEmpty(sceneSave) && File.Exists(sceneSave) && !files.Contains(sceneSave)) files.Add(sceneSave);
                for (int index = 0; index < files.Count; index++)
                {
                    string path = files[index];
                    try
                    {
                        PlanningDocument document = Read(path);
                        result.Add(new PlanningSaveInfo
                        {
                            Path = path,
                            Name = document.name,
                            LastWriteTime = File.GetLastWriteTime(path)
                        });
                    }
                    catch
                    {
                        // Ignore invalid slot files instead of breaking the panel.
                    }
                }
            }
            catch
            {
                // The window will show an empty state if the folder is unavailable.
            }

            result.Sort((left, right) =>
                right.LastWriteTime.CompareTo(left.LastWriteTime));
            // A shared project save bound to this scene is also a selectable save slot.
            string scope = "2026TapTap.Planning.Scene." + Hash128.Compute(Application.dataPath) + "." +
                AssetDatabase.AssetPathToGUID(SceneManager.GetActiveScene().path);
            string shared = EditorPrefs.GetString(scope + ".SavePath", string.Empty);
            if (!string.IsNullOrEmpty(shared) && File.Exists(shared) &&
                !result.Exists(s => string.Equals(System.IO.Path.GetFullPath(s.Path),
                    System.IO.Path.GetFullPath(shared), StringComparison.OrdinalIgnoreCase)))
            {
                try
                {
                    var doc = Read(shared);
                    result.Insert(0, new PlanningSaveInfo { Path = shared, Name = doc.name,
                        LastWriteTime = File.GetLastWriteTime(shared) });
                }
                catch { /* Invalid shared saves must not prevent opening local saves. */ }
            }
            return result;
        }

        internal static PlanningDocument Read(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                throw new FileNotFoundException(
                    "找不到地图存档文件。",
                    path);
            }

            string json = File.ReadAllText(path);
            if (!json.Contains("\"rooms\"")) throw new InvalidDataException("不是关卡规划存档，请选择包含房间数据的 JSON。");
            PlanningDocument document = PlanningDocument.FromPersistentJson(json);
            if (document == null)
            {
                throw new InvalidDataException(
                    "JSON 为空或格式错误。");
            }

            document.Normalize();
            if (string.IsNullOrWhiteSpace(document.name))
            {
                document.name = Path.GetFileNameWithoutExtension(path);
            }

            return document;
        }

        internal static string Create(PlanningDocument document)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            EnsureSaveFolder();
            document.name = GetUniqueName(
                NormalizeName(document.name));
            string path = GetUniquePath(document.name);
            Write(path, document);
            return path;
        }

        internal static void Save(
            string path,
            PlanningDocument document)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            EnsureSaveFolder();
            document.name = NormalizeName(document.name);
            Write(path, document);
        }

        internal static string Rename(
            string path,
            string requestedName)
        {
            PlanningDocument document = Read(path);
            string name = GetUniqueName(
                NormalizeName(requestedName),
                path);
            document.name = name;
            // Scene-paired filenames are stable even when the display name changes.
            if (path.EndsWith(".planning.json", StringComparison.OrdinalIgnoreCase))
            { Write(path, document); return path; }
            string renamedPath = GetUniquePath(name, path);
            if (string.Equals(
                    path,
                    renamedPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                Write(path, document);
                return path;
            }

            string assetPath = PlanningSharedSaveUtility.AssetPath(path);
            string targetAsset = PlanningSharedSaveUtility.AssetPath(renamedPath);
            if (assetPath != null && targetAsset != null)
            {
                string error = AssetDatabase.MoveAsset(assetPath, targetAsset);
                if (!string.IsNullOrEmpty(error)) throw new IOException(error);
                Write(renamedPath, document);
            }
            else { Write(renamedPath, document); File.Delete(path); }
            return renamedPath;
        }

        internal static void Delete(string path)
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                string assetPath = PlanningSharedSaveUtility.AssetPath(path);
                if (assetPath != null) AssetDatabase.MoveAssetToTrash(assetPath);
                else File.Delete(path);
            }
        }

        internal static string SanitizeFileName(string value)
        {
            string name = NormalizeName(value);
            char[] invalid = Path.GetInvalidFileNameChars();
            for (int index = 0; index < invalid.Length; index++)
            {
                name = name.Replace(
                    invalid[index].ToString(),
                    "_");
            }

            name = name.Trim().TrimEnd('.', ' ');
            return string.IsNullOrWhiteSpace(name)
                ? "未命名地图"
                : name;
        }

        private static string EnsureSaveFolder()
        {
            string folder = SaveFolder;
            Directory.CreateDirectory(folder);
            return folder;
        }

        private static void Write(
            string path,
            PlanningDocument document)
        {
            if (File.Exists(path))
            {
                string originalJson = File.ReadAllText(path);
                var original = JsonUtility.FromJson<PlanningDocument>(originalJson);
                if (original != null && (original.worldBlockCellWidth > 1 || original.worldBlockCellHeight > 1 ||
                    !originalJson.Contains("\"worldBlockCellWidth\"")))
                {
                    string backup = path + ".before-unified-grid.bak";
                    if (!File.Exists(backup)) File.Copy(path, backup);
                }
            }
            string json = document.ToPersistentJson(true);
            if (File.Exists(path) && File.ReadAllText(path) == json) return;
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            bool created = !File.Exists(path);
            string temporary = path + ".writing";
            try
            {
                File.WriteAllText(temporary, json, new System.Text.UTF8Encoding(false));
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
            string asset = PlanningSharedSaveUtility.AssetPath(path);
            if (created && asset != null)
                EditorApplication.delayCall += () => { if (File.Exists(path)) AssetDatabase.ImportAsset(asset); };
        }

        private static string GetUniqueName(
            string requestedName,
            string ignoredPath = null)
        {
            string baseName = NormalizeName(requestedName);
            string candidate = baseName;
            int suffix = 2;
            while (ContainsName(candidate, ignoredPath))
            {
                candidate = $"{baseName} ({suffix})";
                suffix++;
            }

            return candidate;
        }

        private static bool ContainsName(
            string name,
            string ignoredPath)
        {
            List<PlanningSaveInfo> saves = LoadAll();
            for (int index = 0; index < saves.Count; index++)
            {
                PlanningSaveInfo save = saves[index];
                if (!string.IsNullOrEmpty(ignoredPath) &&
                    string.Equals(
                        save.Path,
                        ignoredPath,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (string.Equals(
                        save.Name,
                        name,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string GetUniquePath(
            string requestedName,
            string ignoredPath = null)
        {
            string folder = EnsureSaveFolder();
            string baseName = SanitizeFileName(requestedName);
            string candidate = Path.Combine(folder, baseName + ".json");
            int suffix = 2;
            while (File.Exists(candidate) &&
                   !string.Equals(
                       candidate,
                       ignoredPath,
                       StringComparison.OrdinalIgnoreCase))
            {
                candidate = Path.Combine(
                    folder,
                    $"{baseName} ({suffix}).json");
                suffix++;
            }

            return candidate;
        }

        private static string NormalizeName(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? "未命名地图"
                : value.Trim();
        }
    }
}
