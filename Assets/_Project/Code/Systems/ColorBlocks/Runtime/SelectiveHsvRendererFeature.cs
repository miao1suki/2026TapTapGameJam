using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Project.ColorBlocks
{
    /// <summary>URP Render Graph layer mask for neutral whitening and real appearance restoration, after transparents.</summary>
    public sealed class SelectiveHsvRendererFeature : ScriptableRendererFeature
    {
        public static int LastExecutedFrame { get; private set; } = -1;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetDiagnostics() => LastExecutedFrame = -1;
        [SerializeField] private Shader maskShader;
        [SerializeField] private Shader fadeShader;
        [SerializeField, InspectorName("学习项目像素化 Shader")] private Shader pixelShader;
        [SerializeField, Range(1,64), InspectorName("像素块大小")] private float pixelSize = 15f;
        private sealed class MaskData
        {
            public RendererListHandle renderers;
        }

        private sealed class FadeData
        {
            public TextureHandle source, mask;
            public Material material;
            public float saturation, whiteAmount;
        }

        private sealed class SelectiveHsvPass : ScriptableRenderPass
        {
            private static readonly int MaskTextureId = Shader.PropertyToID("_ColorObjectMask");
            private static readonly int SaturationId = Shader.PropertyToID("_TargetSaturation");
            private static readonly int WhiteAmountId = Shader.PropertyToID("_WhiteAmount");
            private static readonly List<ShaderTagId> Tags = new List<ShaderTagId>
            {
                new ShaderTagId("UniversalForward"),
                new ShaderTagId("UniversalForwardOnly"),
                new ShaderTagId("SRPDefaultUnlit"),
                new ShaderTagId("UniversalGBuffer"),
                new ShaderTagId("Always")
            };

            private readonly Material maskMaterial;
            private readonly Material fadeMaterial;

            public SelectiveHsvPass(Material mask, Material fade)
            {
                maskMaterial = mask;
                fadeMaterial = fade;
                renderPassEvent = (RenderPassEvent)((int)RenderPassEvent.BeforeRenderingPostProcessing + 1);
                requiresIntermediateTexture = true;
            }

            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
            {
                if (!Application.isPlaying || maskMaterial == null || fadeMaterial == null) return;
                var catalog = ColorRuntimeService.Instance.Catalog;
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
                    float whiteAmount = HSVColorFadeManager.Instance.GetWhiteAmount(color.id);
                    bool pixelGroup = color.id == "blue" || color.id == "green";
                    if (!pixelGroup && saturation >= 0.999f && whiteAmount <= .001f) continue;

                    var source = resources.activeColorTexture;
                    var maskDesc = graph.GetTextureDesc(source);
                    maskDesc.name = "Color Mask " + color.displayName;
                    maskDesc.format = GraphicsFormat.R8_UNorm;
                    maskDesc.msaaSamples = MSAASamples.None;
                    maskDesc.clearBuffer = true;
                    maskDesc.clearColor = Color.black;
                    var mask = graph.CreateTexture(maskDesc);

                    var filtering = new FilteringSettings(RenderQueueRange.all, 1 << color.unityLayer);
                    var drawing = RenderingUtils.CreateDrawingSettings(
                        Tags, rendering, camera, lights, camera.defaultOpaqueSortFlags);
                    // A dedicated material supplies valid defaults regardless of the source shader's properties.
                    // Particle renderers supply their alpha texture through namespaced property-block values.
                    drawing.overrideMaterial = maskMaterial;
                    drawing.overrideMaterialPassIndex = 0;
                    var list = graph.CreateRendererList(new RendererListParams(rendering.cullResults, drawing, filtering));
                    using (var builder = graph.AddRasterRenderPass<MaskData>("Color mask " + color.displayName, out var data))
                    {
                        data.renderers = list;
                        builder.UseRendererList(list);
                        builder.SetRenderAttachment(mask, 0);
                        builder.SetRenderAttachmentDepth(resources.activeDepthTexture, AccessFlags.Read);
                        builder.SetRenderFunc((MaskData pass, RasterGraphContext context) =>
                            context.cmd.DrawRendererList(pass.renderers));
                    }

                    var destDesc = graph.GetTextureDesc(source);
                    destDesc.name = "Color HSV " + color.displayName;
                    destDesc.clearBuffer = false;
                    var destination = graph.CreateTexture(destDesc);
                    using (var builder = graph.AddRasterRenderPass<FadeData>("Color HSV " + color.displayName, out var data))
                    {
                        data.source = source;
                        data.mask = mask;
                        data.material = fadeMaterial;
                        data.saturation = saturation;
                        data.whiteAmount = whiteAmount;
                        builder.UseTexture(source, AccessFlags.Read);
                        builder.UseTexture(mask, AccessFlags.Read);
                        builder.SetRenderAttachment(destination, 0);
                        builder.AllowGlobalStateModification(true);
                        // Bind this color's textures at execution, not as shared globals during graph recording.
                        builder.SetRenderFunc((FadeData fade, RasterGraphContext context) =>
                        {
                            context.cmd.SetGlobalTexture(MaskTextureId, fade.mask);
                            context.cmd.SetGlobalFloat(SaturationId, fade.saturation);
                            context.cmd.SetGlobalFloat(WhiteAmountId, fade.whiteAmount);
                            Blitter.BlitTexture(context.cmd, fade.source, new Vector4(1, 1, 0, 0), fade.material, 0);
                            LastExecutedFrame = Time.frameCount;
                        });
                    }
                    resources.cameraColor = destination;
                }
            }
        }

        private SelectiveHsvPass pass;
        private Material maskMaterial;
        private Material fadeMaterial;
        private Material pixelMaterial;
        private Project.Pixelization.LearningPixelPass pixelPass;

        public override void Create()
        {
            CoreUtils.Destroy(maskMaterial);
            CoreUtils.Destroy(fadeMaterial);
            CoreUtils.Destroy(pixelMaterial);
            var resolvedMask = maskShader != null ? maskShader : Shader.Find("Hidden/2026TapTap/ColorObjectMask");
            var resolvedFade = fadeShader != null ? fadeShader : Shader.Find("Hidden/2026TapTap/SelectiveHSV");
            if (resolvedMask != null) maskMaterial = CoreUtils.CreateEngineMaterial(resolvedMask);
            if (resolvedFade != null) fadeMaterial = CoreUtils.CreateEngineMaterial(resolvedFade);
            var resolvedPixel = pixelShader != null ? pixelShader : Shader.Find("Hidden/2026TapTap/LearningPixelOutline");
            if (resolvedPixel != null) pixelMaterial = CoreUtils.CreateEngineMaterial(resolvedPixel);
            pixelPass = new Project.Pixelization.LearningPixelPass(maskMaterial,pixelMaterial,pixelSize);
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
            CoreUtils.Destroy(pixelMaterial);
            maskMaterial = null;
            fadeMaterial = null;
            pixelMaterial = null;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (Application.isPlaying && pass != null && renderingData.cameraData.cameraType == CameraType.Game)
            {
                pixelPass?.Prepare();
                if (pixelPass != null) renderer.EnqueuePass(pixelPass);
                renderer.EnqueuePass(pass);
            }
        }
    }
}
