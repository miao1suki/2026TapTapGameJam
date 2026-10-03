using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace Project.ColorBlocks
{
    /// <summary>URP Render Graph screen-space HSV pass, isolated to visible opaque objects on each configured layer.</summary>
    public sealed class SelectiveHsvRendererFeature : ScriptableRendererFeature
    {
        [SerializeField] private Shader maskShader;
        [SerializeField] private Shader fadeShader;
        private sealed class MaskData
        {
            public RendererListHandle renderers;
        }

        private sealed class SelectiveHsvPass : ScriptableRenderPass
        {
            private static readonly int MaskTextureId = Shader.PropertyToID("_ColorObjectMask");
            private static readonly int SaturationId = Shader.PropertyToID("_TargetSaturation");
            private static readonly List<ShaderTagId> Tags = new List<ShaderTagId>
            {
                new ShaderTagId("UniversalForward"),
                new ShaderTagId("UniversalForwardOnly"),
                new ShaderTagId("SRPDefaultUnlit")
            };

            private readonly Material maskMaterial;
            private readonly Material fadeMaterial;

            public SelectiveHsvPass(Material mask, Material fade)
            {
                maskMaterial = mask;
                fadeMaterial = fade;
                renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
                requiresIntermediateTexture = true;
            }

            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
            {
                if (!Application.isPlaying || maskMaterial == null || fadeMaterial == null) return;
                var catalog = ColorWorldManager.Instance.Catalog;
                if (catalog == null) return;
                var resources = frameData.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer) return;
                var rendering = frameData.Get<UniversalRenderingData>();
                var camera = frameData.Get<UniversalCameraData>();
                var lights = frameData.Get<UniversalLightData>();

                foreach (var color in catalog.Colors)
                {
                    if (color == null || color.unityLayer < 0 || color.unityLayer > 31) continue;
                    float saturation = HSVColorFadeManager.Instance.GetSaturation(color.id);
                    if (saturation >= 0.999f) continue;

                    var source = resources.activeColorTexture;
                    var maskDesc = graph.GetTextureDesc(source);
                    maskDesc.name = "Color Mask " + color.displayName;
                    maskDesc.format = GraphicsFormat.R8_UNorm;
                    maskDesc.msaaSamples = MSAASamples.None;
                    maskDesc.clearBuffer = true;
                    maskDesc.clearColor = Color.black;
                    var mask = graph.CreateTexture(maskDesc);

                    var filtering = new FilteringSettings(RenderQueueRange.opaque, 1 << color.unityLayer);
                    var drawing = RenderingUtils.CreateDrawingSettings(
                        Tags, rendering, camera, lights, camera.defaultOpaqueSortFlags);
                    drawing.overrideMaterial = maskMaterial;
                    var list = graph.CreateRendererList(new RendererListParams(rendering.cullResults, drawing, filtering));
                    using (var builder = graph.AddRasterRenderPass<MaskData>("Color mask " + color.displayName, out var data))
                    {
                        data.renderers = list;
                        builder.UseRendererList(list);
                        builder.SetRenderAttachment(mask, 0);
                        builder.SetRenderAttachmentDepth(resources.activeDepthTexture, AccessFlags.Read);
                        builder.SetGlobalTextureAfterPass(mask, MaskTextureId);
                        builder.SetRenderFunc((MaskData pass, RasterGraphContext context) =>
                            context.cmd.DrawRendererList(pass.renderers));
                    }

                    var destDesc = graph.GetTextureDesc(source);
                    destDesc.name = "Color HSV " + color.displayName;
                    destDesc.clearBuffer = false;
                    var destination = graph.CreateTexture(destDesc);
                    var properties = new MaterialPropertyBlock();
                    properties.SetFloat(SaturationId, saturation);
                    var blit = new RenderGraphUtils.BlitMaterialParameters(source, destination, fadeMaterial, 0, properties);
                    using (var builder = graph.AddBlitPass(blit, "Color HSV " + color.displayName, true))
                    {
                        builder.UseTexture(mask, AccessFlags.Read);
                        builder.UseAllGlobalTextures(true);
                    }
                    resources.cameraColor = destination;
                }
            }
        }

        private SelectiveHsvPass pass;
        private Material maskMaterial;
        private Material fadeMaterial;

        public override void Create()
        {
            var resolvedMask = maskShader != null ? maskShader : Shader.Find("Hidden/2026TapTap/ColorObjectMask");
            var resolvedFade = fadeShader != null ? fadeShader : Shader.Find("Hidden/2026TapTap/SelectiveHSV");
            if (resolvedMask != null) maskMaterial = CoreUtils.CreateEngineMaterial(resolvedMask);
            if (resolvedFade != null) fadeMaterial = CoreUtils.CreateEngineMaterial(resolvedFade);
            pass = new SelectiveHsvPass(maskMaterial, fadeMaterial);
        }

#if UNITY_EDITOR
        public void EditorConfigure(Shader mask, Shader fade)
        {
            maskShader = mask;
            fadeShader = fade;
            Create();
        }
#endif

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(maskMaterial);
            CoreUtils.Destroy(fadeMaterial);
            maskMaterial = null;
            fadeMaterial = null;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (Application.isPlaying && pass != null && renderingData.cameraData.cameraType == CameraType.Game)
                renderer.EnqueuePass(pass);
        }
    }
}
