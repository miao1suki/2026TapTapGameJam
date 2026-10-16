Shader "TapTap/Steam/岩浆冷却蒸汽"
{
    Properties
    {
        [HDR] _BaseColor ("冷却蒸汽颜色", Color) = (0.88, 0.95, 1.0, 0.78)
        _Density ("雾气浓度", Range(0.1, 2.0)) = 0.95
        _EffectSize ("雾团大小", Range(0.5, 1.8)) = 1.1
        _RiseSpeed ("上浮速度", Range(0.35, 1.25)) = 0.8
        _EdgeSoftness ("边缘柔化", Range(0.02, 0.5)) = 0.24
        _NoiseScale ("雾边细节", Range(1.0, 12.0)) = 6.5
        _Distortion ("雾边扰动", Range(0.0, 0.5)) = 0.2
        _FlowSpeed ("雾气流动速度", Range(0.0, 2.0)) = 0.45
        _CoreBrightness ("雾团中心亮度", Range(0.0, 1.0)) = 0.16
        _Pixelation ("像素化程度", Range(0.0, 1.0)) = 0.22
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

        Pass
        {
            Name "CoolingSteamUnlit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Density;
                half _EdgeSoftness;
                half _NoiseScale;
                half _Distortion;
                half _FlowSpeed;
                half _CoreBrightness;
                half _Pixelation;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float Noise21(float2 p)
            {
                float2 cell = floor(p);
                float2 local = frac(p);
                local = local * local * (3.0 - 2.0 * local);

                float a = Hash21(cell);
                float b = Hash21(cell + float2(1.0, 0.0));
                float c = Hash21(cell + float2(0.0, 1.0));
                float d = Hash21(cell + float2(1.0, 1.0));

                return lerp(lerp(a, b, local.x), lerp(c, d, local.x), local.y);
            }

            float FBM(float2 p)
            {
                float value = 0.0;
                float amplitude = 0.55;
                [unroll]
                for (int octave = 0; octave < 3; octave++)
                {
                    value += Noise21(p) * amplitude;
                    p = p * 2.03 + 17.17;
                    amplitude *= 0.5;
                }
                return value;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 pixelUv = input.uv;
                if (_Pixelation > 0.001)
                {
                    float2 uvPerPixelX = ddx(input.uv);
                    float2 uvPerPixelY = ddy(input.uv);
                    float blockPixels = 1.0 + floor(saturate(_Pixelation) * 47.0);
                    float2 blockCenter =
                        (floor(input.positionCS.xy / blockPixels) + 0.5) * blockPixels;
                    float2 pixelOffset = blockCenter - input.positionCS.xy;
                    pixelUv = saturate(
                        input.uv + uvPerPixelX * pixelOffset.x +
                        uvPerPixelY * pixelOffset.y);
                }

                float2 centered = (pixelUv - 0.5) * 2.0;
                float time = _Time.y * _FlowSpeed;
                float2 flowUv = float2(
                    centered.x * _NoiseScale,
                    centered.y * _NoiseScale * 0.72 - time);

                float warpA = FBM(flowUv + float2(time * 0.31, 2.7));
                float warpB = FBM(flowUv * 1.41 + float2(-1.9, -time * 0.73));
                float turbulence = warpA * 0.62 + warpB * 0.38;

                float drift = (turbulence - 0.48) * _Distortion;
                float distanceFromCenter = length(float2(centered.x + drift, centered.y * 0.92));
                float irregularRadius = 0.95 + (turbulence - 0.5) * _Distortion * 1.8;
                float edge = 1.0 - smoothstep(
                    irregularRadius - _EdgeSoftness,
                    irregularRadius,
                    distanceFromCenter);

                float wisps = smoothstep(0.18, 0.76, turbulence);
                float density = saturate(0.32 + turbulence * 0.78 + wisps * 0.12);
                float centerGlow = exp(-dot(centered, centered) * 8.5) * _CoreBrightness;
                float alpha = edge * density * _Density * input.color.a * _BaseColor.a;

                half3 vaporColor = _BaseColor.rgb * input.color.rgb;
                vaporColor = lerp(vaporColor * 0.78, vaporColor * 1.12, turbulence);
                vaporColor += (half)centerGlow;

                return half4(vaporColor, saturate(alpha));
            }
            ENDHLSL
        }
    }

    FallBack Off
}
