using Project.SurfaceTiles;
using Project.SurfaceTiles.Editor;
using UnityEditor;
using UnityEngine;

namespace Project.LevelEditor.Editor
{
    [InitializeOnLoad]
    internal static class LevelEditorTileLibraryBuilder
    {
        private const string SourcePath =
            "Assets/_Project/Development/LevelEditor/Orpheus0829/" +
            "TileLibrary/26TAPTAP.png";
        private const string OutputFolder =
            "Assets/_Project/Development/LevelEditor/Orpheus0829/" +
            "TileLibrary";
        private const string OutputName = "26TAPTAP";
        private const string PalettePath = OutputFolder +
                                           "/26TAPTAP_Palette.asset";
        private const string LibraryVersionPreferenceKey =
            "2026TapTap.LevelEditor.TileLibraryVersion";
        private const int LibraryVersion = 2;

        private static bool generating;

        static LevelEditorTileLibraryBuilder()
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        [MenuItem("Tools/2026TapTap/关卡编辑器/重建默认瓦片库")]
        private static void RebuildDefaultLibrary()
        {
            Generate(true);
        }

        private static void OnPlayModeStateChanged(
            PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.update -= Tick;
                EditorApplication.update += Tick;
            }
        }

        private static void Tick()
        {
            if (generating ||
                EditorApplication.isPlayingOrWillChangePlaymode ||
                EditorApplication.isCompiling ||
                EditorApplication.isUpdating)
            {
                return;
            }

            EditorApplication.update -= Tick;
            EnsureDefaultLibrary();
        }

        private static void EnsureDefaultLibrary()
        {
            bool paletteExists = AssetDatabase.LoadAssetAtPath<
                SurfaceTilePalette>(PalettePath) != null;
            bool versionMatches = EditorPrefs.GetInt(
                LibraryVersionPreferenceKey,
                0) >= LibraryVersion;
            if (paletteExists && versionMatches)
            {
                return;
            }

            Generate(false);
        }

        private static void Generate(bool showDialog)
        {
            if (generating)
            {
                return;
            }

            generating = true;
            try
            {
                bool success =
                    SurfaceTileEditorBridge.GeneratePaletteFromSheet(
                        SourcePath,
                        OutputFolder,
                        OutputName,
                        256,
                        24,
                        8,
                        2,
                        128,
                        128,
                        64,
                        true,
                        out string message);
                if (success)
                {
                    EditorPrefs.SetInt(
                        LibraryVersionPreferenceKey,
                        LibraryVersion);
                    AssetDatabase.Refresh();
                    Debug.Log("[LevelEditor] " + message);
                    if (showDialog)
                    {
                        EditorUtility.DisplayDialog(
                            "默认瓦片库",
                            message,
                            "确定");
                    }
                }
                else
                {
                    Debug.LogWarning(
                        "[LevelEditor] 默认瓦片库生成失败：" + message);
                    if (showDialog)
                    {
                        EditorUtility.DisplayDialog(
                            "默认瓦片库生成失败",
                            message,
                            "确定");
                    }
                }
            }
            finally
            {
                generating = false;
            }
        }
    }
}
