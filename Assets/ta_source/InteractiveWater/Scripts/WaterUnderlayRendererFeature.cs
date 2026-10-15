using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace InteractiveWater
{
    /// <summary>
    /// Captures visible objects with an unlit override shader before transparent water is drawn.
    /// The water front samples this texture and applies its own distortion and color.
    /// </summary>
    public sealed class WaterUnderlayRendererFeature : ScriptableRendererFeature
    {
        [SerializeField] private Shader albedoShader;
        [SerializeField] private LayerMask captureLayers = ~((1 << 4) | (1 << 5)); // Water and UI

        private sealed class CapturePass : ScriptableRenderPass
        {
            private static readonly int WaterUnderlayId = Shader.PropertyToID("_WaterUnderlayTexture");
            private static readonly List<ShaderTagId> ShaderTags = new()
            {
                new ShaderTagId("UniversalForward"),
                new ShaderTagId("UniversalForwardOnly"),
                new ShaderTagId("SRPDefaultUnlit"),
                new ShaderTagId("Universal2D")
            };

            private readonly Shader overrideShader;
            private readonly LayerMask layers;

            private sealed class PassData
            {
                public RendererListHandle objects;
            }

            public CapturePass(Shader shader, LayerMask layerMask)
            {
                overrideShader = shader;
                layers = layerMask;
                renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
                requiresIntermediateTexture = true;
            }

            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
            {
                if (!overrideShader) return;

                var resources = frameData.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer) return;

                var camera = frameData.Get<UniversalCameraData>();
                var source = resources.activeColorTexture;
                if (!source.IsValid()) return;

                var description = graph.GetTextureDesc(source);
                description.name = "Interactive Water Unlit Underlay";
                description.msaaSamples = MSAASamples.None;
                description.clearBuffer = false;
                var underlay = graph.CreateTexture(description);

                graph.AddBlitPass(source, underlay, Vector2.one, Vector2.zero,
                    passName: "Copy water background");

                var rendering = frameData.Get<UniversalRenderingData>();
                var lights = frameData.Get<UniversalLightData>();
                var drawing = RenderingUtils.CreateDrawingSettings(
                    ShaderTags, rendering, camera, lights, SortingCriteria.CommonTransparent);
                drawing.overrideShader = overrideShader;
                drawing.overrideShaderPassIndex = 0;

                var filtering = new FilteringSettings(RenderQueueRange.all, layers);
                var objects = graph.CreateRendererList(
                    new RendererListParams(rendering.cullResults, drawing, filtering));

                using (var builder = graph.AddRasterRenderPass<PassData>(
                    "Capture unlit water underlay", out var passData))
                {
                    passData.objects = objects;
                    builder.UseRendererList(objects);
                    builder.SetRenderAttachment(underlay, 0, AccessFlags.ReadWrite);
                    builder.SetRenderAttachmentDepth(resources.activeDepthTexture, AccessFlags.Read);
                    builder.SetGlobalTextureAfterPass(underlay, WaterUnderlayId);
                    builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
                        context.cmd.DrawRendererList(data.objects));
                }
            }
        }

        /// <summary>
        /// Copies the completed transparent scene and redraws only pixel-water filter panels.
        /// The panels sample this copy while writing into camera color, so the sampled image
        /// includes the character and the water's rear sprite without sampling the panel itself.
        /// </summary>
        private sealed class PixelWaterRefractionPass : ScriptableRenderPass
        {
            private static readonly int SceneColorId = Shader.PropertyToID("_PixelWaterSceneColor");
            private static readonly List<ShaderTagId> ShaderTags = new()
            {
                new ShaderTagId("PixelWaterRefraction")
            };

            private sealed class PassData
            {
                public TextureHandle sceneColor;
                public RendererListHandle waterPanels;
            }

            public PixelWaterRefractionPass()
            {
                renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
                requiresIntermediateTexture = true;
            }

            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer) return;

                var source = resources.activeColorTexture;
                if (!source.IsValid()) return;

                var sceneColorDescription = graph.GetTextureDesc(source);
                sceneColorDescription.name = "Pixel Water Scene Color";
                sceneColorDescription.depthBufferBits = DepthBits.None;
                sceneColorDescription.msaaSamples = MSAASamples.None;
                sceneColorDescription.clearBuffer = false;
                sceneColorDescription.useMipMap = false;
                sceneColorDescription.autoGenerateMips = false;
                var sceneColor = graph.CreateTexture(sceneColorDescription);
                graph.AddBlitPass(source, sceneColor, Vector2.one, Vector2.zero,
                    passName: "Copy scene for pixel water refraction");

                var rendering = frameData.Get<UniversalRenderingData>();
                var camera = frameData.Get<UniversalCameraData>();
                var lights = frameData.Get<UniversalLightData>();
                var drawing = RenderingUtils.CreateDrawingSettings(
                    ShaderTags, rendering, camera, lights, SortingCriteria.CommonTransparent);
                // The unique LightMode isolates the panels. Allow color-group Layers so HSV
                // can still identify the refraction area without replacing its Unity Layer.
                var filtering = new FilteringSettings(RenderQueueRange.transparent);
                var waterPanels = graph.CreateRendererList(
                    new RendererListParams(rendering.cullResults, drawing, filtering));

                using (var builder = graph.AddRasterRenderPass<PassData>(
                    "Draw pixel water refraction filters", out var passData))
                {
                    passData.sceneColor = sceneColor;
                    passData.waterPanels = waterPanels;
                    builder.UseTexture(sceneColor, AccessFlags.Read);
                    builder.UseRendererList(waterPanels);
                    builder.SetRenderAttachment(source, 0, AccessFlags.ReadWrite);
                    if (resources.activeDepthTexture.IsValid())
                        builder.SetRenderAttachmentDepth(resources.activeDepthTexture, AccessFlags.Read);
                    builder.AllowGlobalStateModification(true);
                    builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
                    {
                        context.cmd.SetGlobalTexture(SceneColorId, data.sceneColor);
                        context.cmd.DrawRendererList(data.waterPanels);
                    });
                }
            }
        }

        private CapturePass pass;
        private PixelWaterRefractionPass pixelWaterPass;

        public override void Create()
        {
            var shader = albedoShader ? albedoShader : Shader.Find("Hidden/InteractiveWater/UnderwaterAlbedo");
            pass = shader ? new CapturePass(shader, captureLayers) : null;
            pixelWaterPass = new PixelWaterRefractionPass();
        }

        protected override void Dispose(bool disposing)
        {
            pass = null;
            pixelWaterPass = null;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var camera = renderingData.cameraData.camera;
            var isSceneView = renderingData.cameraData.cameraType == CameraType.SceneView;
            var isMainGameCamera = renderingData.cameraData.cameraType == CameraType.Game &&
                                   camera && camera.CompareTag("MainCamera");

            if (pass != null && (isSceneView || (isMainGameCamera && InteractiveWater.ActiveInstanceCount > 0)))
                renderer.EnqueuePass(pass);

            if (pixelWaterPass != null && WaterUnderlayCaptureRequester.HasActiveRequests &&
                (isSceneView || isMainGameCamera))
                renderer.EnqueuePass(pixelWaterPass);
        }
    }
}
