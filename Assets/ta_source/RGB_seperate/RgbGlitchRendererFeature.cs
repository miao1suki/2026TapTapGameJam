using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace TapTap.TA.RgbGlitch
{
    public sealed class RgbGlitchRendererFeature : ScriptableRendererFeature
    {
        [SerializeField] private Shader shader;
        [SerializeField, Range(0f, 1f)] private float vignetteDarkness = 0.65f;
        [SerializeField, Range(1f, 40f)] private float shakeRate = 15f;
        [SerializeField, Range(0f, 0.04f)] private float shakeStrength = 0.012f;
        [SerializeField, Range(0f, 24f)] private float colorSeparationPixels = 7f;

        private Material material;
        private GlitchPass pass;

        private sealed class PassData
        {
            public TextureHandle source;
            public Material material;
            public Vector4 settings;
            public float separationPixels;
            public float time;
        }

        private sealed class GlitchPass : ScriptableRenderPass
        {
            private static readonly int SettingsId = Shader.PropertyToID("_RgbGlitchSettings");
            private static readonly int SeparationId = Shader.PropertyToID("_RgbSeparationPixels");
            private static readonly int TimeId = Shader.PropertyToID("_RgbGlitchTime");
            private readonly RgbGlitchRendererFeature owner;

            public GlitchPass(RgbGlitchRendererFeature feature)
            {
                owner = feature;
                renderPassEvent = (RenderPassEvent)((int)RenderPassEvent.BeforeRenderingPostProcessing + 2);
                requiresIntermediateTexture = true;
            }

            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
            {
                float intensity = RgbGlitchEffect.Intensity;
                if (intensity <= 0.001f || owner.material == null) return;
                var resources = frameData.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer) return;

                var source = resources.activeColorTexture;
                var descriptor = graph.GetTextureDesc(source);
                descriptor.name = "RGB Glitch";
                descriptor.clearBuffer = false;
                var destination = graph.CreateTexture(descriptor);
                using (var builder = graph.AddRasterRenderPass<PassData>("RGB Glitch", out var data))
                {
                    data.source = source;
                    data.material = owner.material;
                    data.settings = new Vector4(
                        intensity,
                        owner.vignetteDarkness,
                        owner.shakeRate,
                        owner.shakeStrength);
                    data.separationPixels = owner.colorSeparationPixels;
                    data.time = Time.unscaledTime;
                    builder.UseTexture(source, AccessFlags.Read);
                    builder.SetRenderAttachment(destination, 0);
                    builder.AllowGlobalStateModification(true);
                    builder.SetRenderFunc((PassData renderData, RasterGraphContext context) =>
                    {
                        context.cmd.SetGlobalVector(SettingsId, renderData.settings);
                        context.cmd.SetGlobalFloat(SeparationId, renderData.separationPixels);
                        context.cmd.SetGlobalFloat(TimeId, renderData.time);
                        Blitter.BlitTexture(context.cmd, renderData.source, new Vector4(1, 1, 0, 0), renderData.material, 0);
                    });
                }
                resources.cameraColor = destination;
            }
        }

        public override void Create()
        {
            CoreUtils.Destroy(material);
            if (shader != null) material = CoreUtils.CreateEngineMaterial(shader);
            pass = new GlitchPass(this);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (Application.isPlaying && material != null && renderingData.cameraData.cameraType == CameraType.Game &&
                renderingData.cameraData.renderType == CameraRenderType.Base && RgbGlitchEffect.Intensity > 0.001f)
            {
                renderer.EnqueuePass(pass);
            }
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(material);
            material = null;
        }
    }
}
