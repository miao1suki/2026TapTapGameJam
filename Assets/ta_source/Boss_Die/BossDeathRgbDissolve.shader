Shader "TA/Boss/RGB Death Dissolve"
{
    Properties
    {
        [NoScaleOffset] _MainTex ("Boss Sprite", 2D) = "white" {}
        _DissolveProgress ("Dissolve Progress", Range(0, 1)) = 0
        _HorizontalScatter ("Horizontal Scatter", Range(0, 3)) = 0.75
        _EdgeWidth ("Fragment Edge Width", Range(0.01, 0.3)) = 0.1
        _RGBStrength ("RGB Color Strength", Range(0, 1)) = 0.9
        _EdgeIntensity ("RGB Edge Intensity", Range(0, 2)) = 1.1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);

        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            float _DissolveProgress;
            float _HorizontalScatter;
            float _EdgeWidth;
            float _RGBStrength;
            float _EdgeIntensity;
        CBUFFER_END

        struct Attributes
        {
            float3 positionOS : POSITION;
            float2 uv : TEXCOORD0;
            float4 color : COLOR;
            float2 cellData : TEXCOORD1;
            float2 scatterData : TEXCOORD2;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            float4 color : COLOR;
            float2 cellData : TEXCOORD1;
        };

        Varyings Vert(Attributes input)
        {
            Varyings output;
            float progress = saturate(_DissolveProgress);
            float spreadIn = smoothstep(0.0, 0.42, progress);
            input.positionOS.x +=
                input.scatterData.x * _HorizontalScatter * spreadIn;
            output.positionCS = TransformObjectToHClip(input.positionOS);
            output.uv = TRANSFORM_TEX(input.uv, _MainTex);
            output.color = input.color;
            output.cellData = input.cellData;
            return output;
        }

        half4 Frag(Varyings input) : SV_Target
        {
            float progress = saturate(_DissolveProgress);
            float edgeWidth = max(0.005, _EdgeWidth);
            half4 source = SAMPLE_TEXTURE2D(
                _MainTex,
                sampler_MainTex,
                input.uv) * input.color;
            clip(source.a - 0.002);

            float dissolveOrder = saturate(input.cellData.x);
            float visibility = progress <= 0.001
                ? 1.0
                : 1.0 - smoothstep(
                    progress - edgeWidth,
                    progress + edgeWidth,
                    dissolveOrder);
            visibility *= progress >= 0.999 ? 0.0 : 1.0;
            clip(visibility - 0.002);

            float channel = saturate(input.cellData.y);
            float3 channelColor = channel < 0.333
                ? float3(1.0, 0.08, 0.12)
                : channel < 0.666
                    ? float3(0.08, 1.0, 0.2)
                    : float3(0.12, 0.4, 1.0);
            float luminance = max(
                dot(source.rgb, float3(0.2126, 0.7152, 0.0722)),
                0.32);
            float spectralBlend =
                smoothstep(0.04, 0.55, progress) *
                saturate(_RGBStrength);
            float3 color = lerp(
                source.rgb,
                channelColor * luminance,
                spectralBlend);
            float edge = 1.0 - smoothstep(
                0.0,
                edgeWidth * 1.5,
                abs(dissolveOrder - progress));
            edge *= smoothstep(0.05, 0.3, progress) *
                max(0.0, _EdgeIntensity);
            color += channelColor * edge;

            return half4(saturate(color), source.a * visibility);
        }
        ENDHLSL

        Pass
        {
            Name "Universal2D"
            Tags { "LightMode" = "Universal2D" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }
    }
}
