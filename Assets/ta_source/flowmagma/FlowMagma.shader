Shader "TapTap/FlowMagma/Flowing Body"
{
    Properties
    {
        [MainTexture] _MainTex ("岩浆图集", 2D) = "white" {}
        [MainColor] _Color ("颜色", Color) = (1, 1, 1, 1)
        _FlowSpeed ("流动速度", Float) = 0.28
        _WaveAmplitude ("波幅", Float) = 0.008
        _VerticalWarpStrength ("上下纹理扭曲", Float) = 0.006
        _WaveFrequency ("波频", Float) = 1.5
        _EffectTime ("时间", Float) = 0
        _BodySize ("尺寸", Vector) = (1, 1, 0, 0)
        _ColorRevealOpacity ("颜色渐显", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Name "FlowMagma"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "FlowMagmaSampling.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _ColorRevealOpacity;
                float4 _BodySize;
                float _VerticalWarpStrength;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float2 surface : TEXCOORD1; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float2 worldXY : TEXCOORD1; float2 surface : TEXCOORD2; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.worldXY = TransformObjectToWorld(input.positionOS.xyz).xy;
                output.surface = float2(TransformObjectToWorld(float3(0,input.surface.x,0)).y, input.surface.y);
                return output;
            }

            half4 SampleTile(float2 tileUv)
            {
                // Bright magma tile: atlas x=104..119, y=105..120 (top-origin), 215x134.
                float2 pixel = floor(frac(tileUv) * 16.0);
                float2 atlasUv = (float2(104.5, 13.5) + pixel) / float2(215.0, 134.0);
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, atlasUv);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float t = _EffectTime;
                ClipFlowMagmaSurface(input.worldXY, input.surface);
                float2 worldSample = FlowMagmaPoint(input.worldXY) / max(.001, _FlowCellWorldSize);
                float wave = sin(worldSample.x * _WaveFrequency * 6.2831853 + t * 1.7);

                // Two offset flow phases blend at their midpoint, hiding the UV reset.
                float phase = frac(t * _FlowSpeed);
                float2 direction = float2(0.68 + 0.32 * sin(worldSample.y * 8.0 + t * 0.8),
                                          _VerticalWarpStrength * sin(worldSample.x * 7.0 - t * 0.9));
                float2 baseUv = worldSample;
                baseUv.y += wave * _VerticalWarpStrength * 0.35;
                half4 a = SampleTile(baseUv - direction * phase);
                half4 b = SampleTile(baseUv - direction * frac(phase + 0.5));
                float blend = abs(phase * 2.0 - 1.0);
                half4 color = lerp(a, b, blend) * _Color;
                clip(_ColorRevealOpacity - .001);
                color.a *= _ColorRevealOpacity;
                return color;
            }
            ENDHLSL
        }
    }
}
