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
            float4 cellShape : TEXCOORD3;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            float4 color : COLOR;
            float2 cellData : TEXCOORD1;
            float2 cellCorner : TEXCOORD2;
            float cellProgress : TEXCOORD3;
        };

        Varyings Vert(Attributes input)
        {
            Varyings output;
            float progress = saturate(_DissolveProgress);
            float breakup = saturate((progress - 0.2) / 0.42);
            float order = saturate(input.cellData.x);
            float cellProgress = smoothstep(
                order * 0.4,
                order * 0.4 + 0.6,
                breakup);
            float2 cellCenter = input.positionOS.xy -
                (input.cellShape.xy - 0.5) * input.cellShape.zw;
            float tileScale = lerp(1.0, 0.78, cellProgress);
            input.positionOS.xy = cellCenter +
                (input.cellShape.xy - 0.5) *
                input.cellShape.zw * tileScale;
            input.positionOS.x += input.scatterData.x *
                _HorizontalScatter * cellProgress;
            output.positionCS = TransformObjectToHClip(input.positionOS);
            output.uv = TRANSFORM_TEX(input.uv, _MainTex);
            output.color = input.color;
            output.cellData = input.cellData;
            output.cellCorner = input.cellShape.xy;
            output.cellProgress = cellProgress;
            return output;
        }

        half4 Frag(Varyings input) : SV_Target
        {
            float progress = saturate(_DissolveProgress);
            half4 source = SAMPLE_TEXTURE2D(
                _MainTex,
                sampler_MainTex,
                input.uv) * input.color;
            clip(source.a - 0.002);

            float dissolveOrder = saturate(input.cellData.x);
            float channel = saturate(input.cellData.y);
            float3 channelColor = channel < 0.333
                ? float3(1.0, 0.08, 0.12)
                : channel < 0.666
                    ? float3(0.08, 1.0, 0.2)
                    : float3(0.12, 0.4, 1.0);
            float luminance = max(
                dot(source.rgb, float3(0.2126, 0.7152, 0.0722)),
                0.32);
            float spectralBlend = input.cellProgress *
                saturate(_RGBStrength);
            float3 color = lerp(
                source.rgb,
                channelColor * luminance,
                spectralBlend);
            float edgeWidth = max(0.005, _EdgeWidth);
            float edgeDistance = min(
                min(input.cellCorner.x, 1.0 - input.cellCorner.x),
                min(input.cellCorner.y, 1.0 - input.cellCorner.y));
            float edge = 1.0 - smoothstep(
                0.0,
                edgeWidth,
                edgeDistance);
            color += channelColor * edge * input.cellProgress *
                max(0.0, _EdgeIntensity) * 0.25;

            float fadeProgress = saturate((progress - 0.62) / 0.38);
            float fadeEnd = lerp(0.62, 1.0, dissolveOrder);
            float visibility = 1.0 - smoothstep(
                fadeEnd - 0.18,
                fadeEnd,
                fadeProgress);

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
