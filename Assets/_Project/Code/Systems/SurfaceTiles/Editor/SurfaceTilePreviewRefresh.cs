using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project.SurfaceTiles.Editor
{
    [InitializeOnLoad]
    internal static class SurfaceTilePreviewRefresh
    {
        private static bool queued;

        static SurfaceTilePreviewRefresh()
        {
            EditorSceneManager.sceneOpened -= OnSceneOpened;
            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorApplication.hierarchyChanged -= Queue;
            EditorApplication.hierarchyChanged += Queue;
            Undo.undoRedoPerformed -= Queue;
            Undo.undoRedoPerformed += Queue;
            Queue();
        }

        private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            Queue();
        }

        private static void Queue()
        {
            if (queued)
            {
                return;
            }

            queued = true;
            EditorApplication.delayCall += RefreshMissingPreviews;
        }

        private static void RefreshMissingPreviews()
        {
            queued = false;
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                EditorApplication.isCompiling)
            {
                return;
            }

            SurfaceTileBlock[] blocks = Object.FindObjectsByType<SurfaceTileBlock>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int index = 0; index < blocks.Length; index++)
            {
                SurfaceTileBlock block = blocks[index];
                if (block == null || block.Palette == null || block.BakeUpToDate)
                {
                    continue;
                }

                bool missing = block.OutputFilter == null ||
                               block.OutputRenderer == null ||
                               block.OutputFilter.sharedMesh == null;
                bool showingStaleBake = block.OutputFilter != null &&
                                        block.BakedMesh != null &&
                                        block.OutputFilter.sharedMesh == block.BakedMesh;
                if (missing || showingStaleBake)
                {
                    SurfaceTileMeshBuilder.RefreshPreview(block);
                }
            }

            SceneView.RepaintAll();
        }
    }
}
