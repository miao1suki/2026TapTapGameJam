#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
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
