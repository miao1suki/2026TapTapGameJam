using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Project.WorldVisibility
{
    public sealed class WorldVisibilityRendererFeature : ScriptableRendererFeature
    {
        public static int LastExecutedFrame { get; private set; } = -1;
        [SerializeField] Shader visibilityShader;
        Material material;
        VisibilityPass pass;
        public override void Create()
        {
            CoreUtils.Destroy(material);
            var shader = visibilityShader ? visibilityShader : Shader.Find("Hidden/2026TapTap/WorldVisibility2D");
            if (shader) material = CoreUtils.CreateEngineMaterial(shader);
            pass = new VisibilityPass(material);
        }
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var camera = renderingData.cameraData.camera;
            // Overlay Canvas draws after the camera; no darkening of HUD, menus or layout editors.
            if (material && renderingData.cameraData.cameraType == CameraType.Game && WorldVisibility2D.ForCamera(camera))
                renderer.EnqueuePass(pass);
        }
        protected override void Dispose(bool disposing) => CoreUtils.Destroy(material);
        sealed class MaskData
        {
            public Material material;
            public WorldVisibility2D system;
            public bool sight;
        }
        sealed class BlurData
        {
            public TextureHandle source, sight, light;
            public Material material;
            public Vector4 direction;
            public bool horizontal;
        }
        sealed class CompositeData
        {
            public TextureHandle source, sight, light, soft;
            public Material material;
            public float ambient, gate, strength;
            public Vector4 softness;
        }
        sealed class VisibilityPass : ScriptableRenderPass
        {
            readonly Material material;
            static readonly int SightId = Shader.PropertyToID("_VisibilitySight");
            static readonly int LightId = Shader.PropertyToID("_VisibilityLight");
            static readonly int AmbientId = Shader.PropertyToID("_VisibilityAmbient");
            static readonly int CoreId = Shader.PropertyToID("_VisibilityCore");
            static readonly int GateId = Shader.PropertyToID("_VisibilityGate");
            static readonly int SoftMaskId = Shader.PropertyToID("_VisibilitySoftMask");
            static readonly int BlurDirectionId = Shader.PropertyToID("_VisibilityBlurDirection");
            static readonly int SoftnessId = Shader.PropertyToID("_VisibilitySoftness");
            static readonly int StrengthId = Shader.PropertyToID("_VisibilityStrength");
            public VisibilityPass(Material value)
            {
                material = value;
                // Water/HSV/pixelization finish first; ordinary camera post and overlay UI follow.
                renderPassEvent = (RenderPassEvent)((int)RenderPassEvent.BeforeRenderingPostProcessing + 20);
                requiresIntermediateTexture = true;
            }
            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
            {
                var camera = frameData.Get<UniversalCameraData>().camera;
                var system = WorldVisibility2D.ForCamera(camera);
                if (!system || !system.Prepare(camera)) return;
                if (system.OcclusionStrength <= .0001f) return;
                var resources = frameData.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer) return;
                var source = resources.activeColorTexture;
                var desc = graph.GetTextureDesc(source);
                desc.width = Mathf.Max(64,Mathf.RoundToInt(desc.width * system.maskResolution));
                desc.height = Mathf.Max(64,Mathf.RoundToInt(desc.height * system.maskResolution));
                desc.format = GraphicsFormat.R8G8B8A8_UNorm;
                desc.msaaSamples = MSAASamples.None; desc.depthBufferBits = DepthBits.None;
                if (system.connectedSpaceVisibility)
                {
                    // One single-channel mask, one composite. No light RT, shadow maps or Gaussian passes.
                    desc.format = GraphicsFormat.R8_UNorm; desc.filterMode = FilterMode.Bilinear;
                    desc.clearBuffer = true; desc.clearColor = Color.black; desc.name = "局部连通空间";
                    var connected = graph.CreateTexture(desc);
                    using (var builder = graph.AddRasterRenderPass<MaskData>("连通空间遮罩", out var data))
                    {
                        data.material = material; data.system = system;
                        builder.SetRenderAttachment(connected, 0);
                        builder.SetRenderFunc((MaskData value, RasterGraphContext context) =>
                        {
                            if (value.system && value.system.SightMesh)
                                context.cmd.DrawMesh(value.system.SightMesh, Matrix4x4.identity, value.material, 0, 5);
                            if (value.system && value.system.WallMesh)
                                context.cmd.DrawMesh(value.system.WallMesh, Matrix4x4.identity, value.material, 0, 7);
                        });
                    }
                    var connectedOutput = graph.GetTextureDesc(source);
                    connectedOutput.name = "连通空间合成"; connectedOutput.clearBuffer = false;
                    var result = graph.CreateTexture(connectedOutput);
                    using (var builder = graph.AddRasterRenderPass<CompositeData>("连通空间原画合成", out var data))
                    {
                        data.source = source; data.sight = connected; data.material = material;
                        data.strength = system.OcclusionStrength;
                        builder.UseTexture(source); builder.UseTexture(connected);
                        builder.SetRenderAttachment(result, 0); builder.AllowGlobalStateModification(true);
                        builder.SetRenderFunc((CompositeData value, RasterGraphContext context) =>
                        {
                            context.cmd.SetGlobalTexture(SightId, value.sight);
                            context.cmd.SetGlobalFloat(StrengthId,value.strength);
                            Blitter.BlitTexture(context.cmd, value.source, new Vector4(1, 1, 0, 0), value.material, 6);
                            LastExecutedFrame = Time.frameCount;
                        });
                    }
                    resources.cameraColor = result;
                    return;
                }
                desc.clearBuffer = true; desc.clearColor = Color.black; desc.filterMode = FilterMode.Point;
                desc.name = "二维光照 玩家视线"; var sight = graph.CreateTexture(desc);
                desc.name = "二维光照 光源叠加"; var light = graph.CreateTexture(desc);
                DrawMask(sight,true); DrawMask(light,false);
                float softness = Mathf.Clamp(system.shadowSoftnessCells,0,4) * system.cellWorldSize;
                float height = Mathf.Max(.01f,camera.orthographicSize * 2);
                Vector4 softUV = new Vector4(softness / (height * Mathf.Max(.01f,camera.aspect)),softness / height,softness > 0 ? 1 : 0,0);
                var soft = light;
                if (softness > 0)
                {
                    desc.filterMode = FilterMode.Bilinear;
                    desc.name = "二维光照 横向高斯"; var horizontal = graph.CreateTexture(desc);
                    desc.name = "二维光照 纵向高斯"; soft = graph.CreateTexture(desc);
                    Blur(light,horizontal,true,new Vector4(softUV.x,0,0,0));
                    Blur(horizontal,soft,false,new Vector4(0,softUV.y,0,0));
                }
                var outputDesc = graph.GetTextureDesc(source);
                outputDesc.name = "二维光照 明暗合成"; outputDesc.clearBuffer = false;
                var output = graph.CreateTexture(outputDesc);
                using (var builder = graph.AddRasterRenderPass<CompositeData>("二维光照 合成",out var data))
                {
                    data.source = source; data.sight = sight; data.light = light; data.soft = soft; data.material = material;
                    data.ambient = Mathf.Clamp01(system.ambient); data.gate = system.respectPlayerSight ? 1 : 0;
                    data.strength = system.OcclusionStrength;
                    data.softness = softUV;
                    builder.UseTexture(source); builder.UseTexture(sight); builder.UseTexture(light); builder.UseTexture(soft);
                    builder.SetRenderAttachment(output,0); builder.AllowGlobalStateModification(true);
                    builder.SetRenderFunc((CompositeData value, RasterGraphContext context) =>
                    {
                        context.cmd.SetGlobalTexture(SightId,value.sight); context.cmd.SetGlobalTexture(LightId,value.light);
                        context.cmd.SetGlobalTexture(SoftMaskId,value.soft);
                        context.cmd.SetGlobalFloat(AmbientId,value.ambient); context.cmd.SetGlobalFloat(GateId,value.gate);
                        context.cmd.SetGlobalFloat(StrengthId,value.strength);
                        context.cmd.SetGlobalVector(SoftnessId,value.softness);
                        Blitter.BlitTexture(context.cmd,value.source,new Vector4(1,1,0,0),value.material,2);
                        LastExecutedFrame = Time.frameCount;
                    });
                }
                resources.cameraColor = output;
                void Blur(TextureHandle input, TextureHandle target, bool horizontal, Vector4 direction)
                {
                    using var builder = graph.AddRasterRenderPass<BlurData>(horizontal ? "二维光照 横向柔化" : "二维光照 纵向柔化",out var data);
                    data.source = input; data.sight = sight; data.light = light; data.material = material;
                    data.direction = direction; data.horizontal = horizontal;
                    builder.UseTexture(input);
                    if (horizontal) builder.UseTexture(sight);
                    builder.SetRenderAttachment(target,0); builder.AllowGlobalStateModification(true);
                    builder.SetRenderFunc((BlurData value, RasterGraphContext context) =>
                    {
                        context.cmd.SetGlobalVector(BlurDirectionId,value.direction);
                        if (value.horizontal) context.cmd.SetGlobalTexture(SightId,value.sight);
                        Blitter.BlitTexture(context.cmd,value.source,new Vector4(1,1,0,0),value.material,value.horizontal ? 3 : 4);
                    });
                }
                void DrawMask(TextureHandle target, bool isSight)
                {
                    using var builder = graph.AddRasterRenderPass<MaskData>(isSight ? "二维光照 视线遮挡" : "二维光照 灯光遮挡",out var data);
                    data.material = material; data.system = system; data.sight = isSight;
                    builder.SetRenderAttachment(target,0); builder.AllowGlobalStateModification(true);
                    builder.SetRenderFunc((MaskData value, RasterGraphContext context) =>
                    {
                        if (!value.system) return;
                        context.cmd.SetGlobalFloat(CoreId,Mathf.Clamp(value.system.lightCore,0,.99f));
                        context.cmd.DrawMesh(value.sight ? value.system.SightMesh : value.system.PlayerMesh,
                            Matrix4x4.identity,value.material,0,value.sight ? 1 : 0);
                        if (!value.sight) foreach (var lamp in value.system.Lights)
                            if (lamp && lamp.polygon.Mesh) context.cmd.DrawMesh(lamp.polygon.Mesh,Matrix4x4.identity,value.material,0,0);
                    });
                }
            }
        }
    }
}
