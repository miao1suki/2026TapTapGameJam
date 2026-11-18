#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Project.LevelEditor
{
    /// <summary>SceneView may orbit the flat map, while gameplay remains orthographic.</summary>
    [InitializeOnLoad]
    internal static class Native2DScenePreview
    {
        static Native2DScenePreview()
        {
            RenderPipelineManager.beginCameraRendering -= Prepare;
            RenderPipelineManager.beginCameraRendering += Prepare;
            EditorSceneManager.sceneOpened += (scene, _) => RestoreBlockTints(scene);
            EditorApplication.delayCall += () =>
            {
                for (int i=0;i<SceneManager.sceneCount;i++) RestoreBlockTints(SceneManager.GetSceneAt(i));
            };
        }

        // MaterialPropertyBlocks are transient. Restore only preview state after
        // reload; never change shared materials, author fields, dirty or Undo.
        internal static void RestoreBlockTints(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded || EditorApplication.isPlayingOrWillChangePlaymode) return;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var block in root.GetComponentsInChildren<LevelEditorPlacedBlock>(true))
                    if (block.HasColorData) block.ApplyEntryColor();
        }

        private static void Prepare(ScriptableRenderContext context, Camera camera)
        {
            if (!camera || camera.cameraType != CameraType.SceneView ||
                GraphicsSettings.currentRenderPipeline is not UniversalRenderPipelineAsset pipeline) return;
            var data = camera.GetComponent<UniversalAdditionalCameraData>();
            var renderer = data ? data.scriptableRenderer : pipeline.scriptableRenderer;
            if (renderer == null || renderer.GetType().Name != "Renderer2D") return;
            // Do this before URP builds camera data. Renderer2D has no skybox pass;
            // changing clearFlags later in CreateResources can leave cached load actions stale.
            camera.clearFlags = CameraClearFlags.SolidColor;
        }
    }
}
#endif
