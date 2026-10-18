Shader "Project/Objects/Pixel Sprite Reveal"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
        _Color("Tint", Color) = (1,1,1,1)
        _ColorRevealOpacity("Reveal", Range(0,1)) = 1
        [PerRendererData] _BurnEnabled("Plant Burn", Float) = 0
        [PerRendererData] _ArtworkPixelWorldSize("Artwork World Pixel", Float) = 0
        [PerRendererData] _ArtworkPixelUVRect("Artwork Frame UV", Vector) = (0,0,1,1)
        [PerRendererData] _ArtworkPixelTextureSize("Artwork Texture Size", Vector) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            #include "GridArtworkSampling.hlsl"
            #include "Assets/ta_source/Burn/PlantBurnSampling.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            half _ColorRevealOpacity;
            float _ArtworkPixelWorldSize;
            float4 _ArtworkPixelUVRect;
            float4 _ArtworkPixelTextureSize;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; float2 worldXY:TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                SetUpSpriteInstanceProperties();
                Varyings o;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS=TransformObjectToHClip(UnityFlipSprite(v.positionOS.xyz, unity_SpriteProps.xy));
                o.worldXY=TransformObjectToWorld(UnityFlipSprite(v.positionOS.xyz, unity_SpriteProps.xy)).xy;
                o.uv=v.uv; o.color=v.color*_Color*unity_SpriteColor;
                return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                float2 uv=GridArtworkSampleUV(i.uv,i.worldXY,_ArtworkPixelWorldSize,_ArtworkPixelUVRect,_ArtworkPixelTextureSize.xy);
                half4 color=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv)*i.color;
                float contour=PlantBurnContour(TEXTURE2D_ARGS(_MainTex,sampler_MainTex),uv,
                    _ArtworkPixelUVRect,_ArtworkPixelTextureSize.xy,color.a,i.color.a);
                color=ApplyPlantBurn(color,i.worldXY,contour);
                // Match the silhouette and handoff thresholds used by ColorObjectMask.
                clip(color.a-.01h);
                clip(_ColorRevealOpacity-.001h);
                color.a *= _ColorRevealOpacity;
                return color;
            }
            ENDHLSL
        }
    }
}
