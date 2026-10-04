using System;
using System.Collections.Generic;
using System.IO;
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
                    "..",
                    "Library",
                    FolderName));

        internal static List<PlanningSaveInfo> LoadAll()
        {
            var result = new List<PlanningSaveInfo>();
            try
            {
                string folder = EnsureSaveFolder();
                string[] files = Directory.GetFiles(
                    folder,
                    "*.json",
                    SearchOption.TopDirectoryOnly);
                for (int index = 0; index < files.Length; index++)
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

            PlanningDocument document =
                JsonUtility.FromJson<PlanningDocument>(
                    File.ReadAllText(path));
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
            string renamedPath = GetUniquePath(name, path);
            if (string.Equals(
                    path,
                    renamedPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                Write(path, document);
                return path;
            }

            Write(renamedPath, document);
            File.Delete(path);
            return renamedPath;
        }

        internal static void Delete(string path)
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                File.Delete(path);
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
            File.WriteAllText(
                path,
                JsonUtility.ToJson(document, true));
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
