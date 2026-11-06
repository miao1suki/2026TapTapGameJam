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
                ConfigureInput(ScriptableRenderPassInput.Color);
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
                description.clearBuffer = true;
                description.clearColor = camera.backgroundColor;
                description.depthBufferBits = DepthBits.None;
                var underlay = graph.CreateTexture(description);
                // Renderer2D injects BeforeRenderingTransparents before ALL scene draws.
                // Reading camera color/depth here reads an uninitialised/currently reused target.
                // This independent unlit capture owns its clear and uses 2D sorting instead.

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
            private static readonly int FootprintId = Shader.PropertyToID("_PixelWaterFootprint");
            private static readonly List<ShaderTagId> ShaderTags = new()
            {
                new ShaderTagId("PixelWaterRefraction")
            };

            private sealed class PassData
            {
                public TextureHandle sceneColor, footprint;
                public RendererListHandle waterPanels;
            }

            private sealed class FootprintData { public RendererListHandle panels; }

            public PixelWaterRefractionPass()
            {
                renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
                requiresIntermediateTexture = true;
                ConfigureInput(ScriptableRenderPassInput.Color);
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

                var footprintDescription = sceneColorDescription;
                footprintDescription.name = "Pixel Water Visible Footprint";
                footprintDescription.format = GraphicsFormat.R8_UNorm;
                footprintDescription.clearBuffer = true;
                footprintDescription.clearColor = Color.black;
                footprintDescription.filterMode = FilterMode.Point;
                if (resources.activeDepthTexture.IsValid())
                    footprintDescription.msaaSamples = graph.GetTextureDesc(resources.activeDepthTexture).msaaSamples;
                var footprint = graph.CreateTexture(footprintDescription);
                drawing.SetShaderPassName(0, new ShaderTagId("PixelWaterFootprint"));
                var footprintPanels = graph.CreateRendererList(
                    new RendererListParams(rendering.cullResults, drawing, filtering));
                using (var builder = graph.AddRasterRenderPass<FootprintData>("Mask visible pixel water", out var maskData))
                {
                    maskData.panels = footprintPanels;
                    builder.UseRendererList(footprintPanels);
                    builder.SetRenderAttachment(footprint, 0);
                    if (resources.activeDepthTexture.IsValid())
                        builder.SetRenderAttachmentDepth(resources.activeDepthTexture, AccessFlags.Read);
                    builder.SetRenderFunc(static (FootprintData data, RasterGraphContext context) =>
                        context.cmd.DrawRendererList(data.panels));
                }

                using (var builder = graph.AddRasterRenderPass<PassData>(
                    "Draw pixel water refraction filters", out var passData))
                {
                    passData.sceneColor = sceneColor;
                    passData.footprint = footprint;
                    passData.waterPanels = waterPanels;
                    builder.UseTexture(sceneColor, AccessFlags.Read);
                    builder.UseTexture(footprint, AccessFlags.Read);
                    builder.UseRendererList(waterPanels);
                    builder.SetRenderAttachment(source, 0, AccessFlags.ReadWrite);
                    if (resources.activeDepthTexture.IsValid())
                        builder.SetRenderAttachmentDepth(resources.activeDepthTexture, AccessFlags.Read);
                    builder.AllowGlobalStateModification(true);
                    builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
                    {
                        context.cmd.SetGlobalTexture(SceneColorId, data.sceneColor);
                        context.cmd.SetGlobalTexture(FootprintId, data.footprint);
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

            if (pass != null && InteractiveWater.ActiveInstanceCount > 0 && (isSceneView || isMainGameCamera))
                renderer.EnqueuePass(pass);

            if (pixelWaterPass != null && WaterUnderlayCaptureRequester.HasVisibleRequests(camera) &&
                (isSceneView || isMainGameCamera))
                renderer.EnqueuePass(pixelWaterPass);
        }
    }
}
