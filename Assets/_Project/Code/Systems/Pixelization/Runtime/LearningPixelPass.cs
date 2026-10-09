using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Project.Pixelization
{
    internal sealed class LearningPixelPass : ScriptableRenderPass
    {
        private readonly Material maskMaterial, pixelMaterial;
        private readonly float pixelSize;
        private static readonly List<ShaderTagId> tags = new List<ShaderTagId>
        {
            new ShaderTagId("UniversalForward"),new ShaderTagId("UniversalForwardOnly"),
            new ShaderTagId("SRPDefaultUnlit"),new ShaderTagId("UniversalGBuffer"),new ShaderTagId("Always")
        };
        private sealed class MaskData { public RendererListHandle tagged, legacy; }
        private sealed class BlitData
        {
            public TextureHandle source, original, mask;
            public Material material;
            public MaterialPropertyBlock properties;
            public int shaderPass;
        }
        public LearningPixelPass(Material mask,Material pixel,float size)
        {
            maskMaterial = mask;
            pixelMaterial = pixel;
            pixelSize = Mathf.Clamp(Mathf.Round(size),1,64);
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            requiresIntermediateTexture = true;
        }
        public void Prepare()
        {
            ConfigureInput(PixelizationControl.OutlineEnabled
                ? ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal : ScriptableRenderPassInput.None);
        }
        public override void RecordRenderGraph(RenderGraph graph,ContextContainer frameData)
        {
            if (maskMaterial == null || pixelMaterial == null) return;
            var resources = frameData.Get<UniversalResourceData>();
            if (resources.isActiveTargetBackBuffer) return;
            var rendering = frameData.Get<UniversalRenderingData>();
            var camera = frameData.Get<UniversalCameraData>();
            var lights = frameData.Get<UniversalLightData>();
            var source = resources.activeColorTexture;
            var desc = graph.GetTextureDesc(source);
            int width = Mathf.Max(desc.width,1), height = Mathf.Max(desc.height,1);
            var maskDesc = desc;
            maskDesc.name = "Learning Pixel Selection";
            maskDesc.format = GraphicsFormat.R8_UNorm;
            maskDesc.depthBufferBits = DepthBits.None;
            maskDesc.msaaSamples = graph.GetTextureDesc(resources.activeDepthTexture).msaaSamples;
            maskDesc.clearBuffer = true;
            maskDesc.clearColor = Color.black;
            var mask = graph.CreateTexture(maskDesc);
            var drawing = RenderingUtils.CreateDrawingSettings(tags,rendering,camera,lights,camera.defaultOpaqueSortFlags);
            drawing.overrideMaterial = maskMaterial;
            drawing.overrideMaterialPassIndex = 0;
            var tagged = graph.CreateRendererList(new RendererListParams(rendering.cullResults,drawing,
                new FilteringSettings(RenderQueueRange.all,~0,128u)));
            var legacy = graph.CreateRendererList(new RendererListParams(rendering.cullResults,drawing,
                new FilteringSettings(RenderQueueRange.all,1 << 12)));
            using (var builder = graph.AddRasterRenderPass<MaskData>("Learning Pixel Object Mask",out var data))
            {
                data.tagged = tagged;
                data.legacy = legacy;
                builder.UseRendererList(tagged);
                builder.UseRendererList(legacy);
                builder.SetRenderAttachment(mask,0);
                builder.SetRenderAttachmentDepth(resources.activeDepthTexture,AccessFlags.Read);
                builder.SetRenderFunc((MaskData pass,RasterGraphContext context) =>
                {
                    context.cmd.DrawRendererList(pass.tagged);
                    context.cmd.DrawRendererList(pass.legacy);
                });
            }
            var packedDesc = desc;
            packedDesc.sizeMode = TextureSizeMode.Explicit;
            packedDesc.func = null;
            packedDesc.scale = Vector2.one;
            packedDesc.width = width;
            packedDesc.height = height;
            packedDesc.useDynamicScale = false;
            packedDesc.useDynamicScaleExplicit = false;
            packedDesc.useMipMap = false;
            packedDesc.autoGenerateMips = false;
            packedDesc.name = "Learning Pixel Packed";
            packedDesc.format = GraphicsFormat.R16G16B16A16_SFloat;
            packedDesc.depthBufferBits = DepthBits.None;
            packedDesc.msaaSamples = MSAASamples.None;
            packedDesc.clearBuffer = false;
            packedDesc.filterMode = FilterMode.Point;
            var packed = graph.CreateTexture(packedDesc);
            var gridDesc = packedDesc;
            gridDesc.name = "Learning Pixel Block Grid";
            gridDesc.sizeMode = TextureSizeMode.Explicit;
            gridDesc.width = Mathf.CeilToInt(width / pixelSize);
            gridDesc.height = Mathf.CeilToInt(height / pixelSize);
            var grid = graph.CreateTexture(gridDesc);
            var destinationDesc = desc;
            destinationDesc.name = "Learning Pixel Result";
            destinationDesc.clearBuffer = false;
            var destination = graph.CreateTexture(destinationDesc);
            AddStage(graph,source,source,mask,packed,0,width,height,resources);
            AddStage(graph,packed,source,mask,grid,1,width,height,resources);
            AddStage(graph,grid,source,mask,destination,2,width,height,resources);
            resources.cameraColor = destination;
        }
        private void AddStage(RenderGraph graph,TextureHandle source,TextureHandle original,TextureHandle mask,
            TextureHandle destination,int shaderPass,int width,int height,UniversalResourceData resources)
        {
            using (var builder = graph.AddRasterRenderPass<BlitData>("Learning Pixel Stage " + shaderPass,out var data))
            {
                data.source = source;
                data.original = original;
                data.mask = mask;
                data.material = pixelMaterial;
                data.shaderPass = shaderPass;
                data.properties = new MaterialPropertyBlock();
                data.properties.SetVector("_ScreenPixelSourceSize",new Vector4(width,height,1f/width,1f/height));
                data.properties.SetFloat("_PixelSize",pixelSize);
                data.properties.SetFloat("_OutlineEnabled",PixelizationControl.OutlineEnabled ? 1 : 0);
                builder.UseTexture(source,AccessFlags.Read);
                builder.UseTexture(original,AccessFlags.Read);
                builder.UseTexture(mask,AccessFlags.Read);
                if (shaderPass == 0 && PixelizationControl.OutlineEnabled)
                {
                    if (resources.cameraDepthTexture.IsValid()) builder.UseTexture(resources.cameraDepthTexture,AccessFlags.Read);
                    if (resources.cameraNormalsTexture.IsValid()) builder.UseTexture(resources.cameraNormalsTexture,AccessFlags.Read);
                }
                builder.SetRenderAttachment(destination,0);
                builder.AllowGlobalStateModification(true);
                builder.SetRenderFunc((BlitData pass,RasterGraphContext context) =>
                {
                    context.cmd.SetGlobalTexture("_LearningPixelSelectionMask",pass.mask);
                    context.cmd.SetGlobalTexture("_LearningPixelOriginalColor",pass.original);
                    pass.properties.SetTexture("_BlitTexture",pass.source);
                    pass.properties.SetVector("_BlitScaleBias",new Vector4(1,1,0,0));
                    context.cmd.DrawProcedural(Matrix4x4.identity,pass.material,pass.shaderPass,MeshTopology.Triangles,3,1,pass.properties);
                });
            }
        }
    }
}
