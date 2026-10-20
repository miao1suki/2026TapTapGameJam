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
        sealed class CompositeData
        {
            public TextureHandle source, sight, light;
            public Material material;
            public float ambient, gate;
        }
        sealed class VisibilityPass : ScriptableRenderPass
        {
            readonly Material material;
            static readonly int SightId = Shader.PropertyToID("_VisibilitySight");
            static readonly int LightId = Shader.PropertyToID("_VisibilityLight");
            static readonly int AmbientId = Shader.PropertyToID("_VisibilityAmbient");
            static readonly int CoreId = Shader.PropertyToID("_VisibilityCore");
            static readonly int GateId = Shader.PropertyToID("_VisibilityGate");
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
                var resources = frameData.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer) return;
                var source = resources.activeColorTexture;
                var desc = graph.GetTextureDesc(source);
                desc.width = Mathf.Max(64,Mathf.RoundToInt(desc.width * system.maskResolution));
                desc.height = Mathf.Max(64,Mathf.RoundToInt(desc.height * system.maskResolution));
                desc.format = GraphicsFormat.R8G8B8A8_UNorm;
                desc.msaaSamples = MSAASamples.None; desc.depthBufferBits = DepthBits.None;
                desc.clearBuffer = true; desc.clearColor = Color.black; desc.filterMode = FilterMode.Point;
                desc.name = "二维光照 玩家视线"; var sight = graph.CreateTexture(desc);
                desc.name = "二维光照 光源叠加"; var light = graph.CreateTexture(desc);
                DrawMask(sight,true); DrawMask(light,false);
                var outputDesc = graph.GetTextureDesc(source);
                outputDesc.name = "二维光照 明暗合成"; outputDesc.clearBuffer = false;
                var output = graph.CreateTexture(outputDesc);
                using (var builder = graph.AddRasterRenderPass<CompositeData>("二维光照 合成",out var data))
                {
                    data.source = source; data.sight = sight; data.light = light; data.material = material;
                    data.ambient = Mathf.Clamp01(system.ambient); data.gate = system.respectPlayerSight ? 1 : 0;
                    builder.UseTexture(source); builder.UseTexture(sight); builder.UseTexture(light);
                    builder.SetRenderAttachment(output,0); builder.AllowGlobalStateModification(true);
                    builder.SetRenderFunc((CompositeData value, RasterGraphContext context) =>
                    {
                        context.cmd.SetGlobalTexture(SightId,value.sight); context.cmd.SetGlobalTexture(LightId,value.light);
                        context.cmd.SetGlobalFloat(AmbientId,value.ambient); context.cmd.SetGlobalFloat(GateId,value.gate);
                        Blitter.BlitTexture(context.cmd,value.source,new Vector4(1,1,0,0),value.material,2);
                        LastExecutedFrame = Time.frameCount;
                    });
                }
                resources.cameraColor = output;
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
