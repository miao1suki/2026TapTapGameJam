Shader "2026TapTap/Interaction Link"
{
    Properties
    {
        _LinkColor("Link Color", Color) = (1,0.08,0.08,1)
        _Feasible("Feasible", Range(0,1)) = 1
        _Opacity("Opacity", Range(0,1)) = 0.68
        _PixelSize("Pixel Size", Range(1,16)) = 5
        _NoiseStrength("Noise Strength", Range(0,1)) = 0.55
        _FlickerSpeed("Flicker Speed", Range(0,20)) = 7
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+50" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float4 _LinkColor;
            float _Feasible;
            float _Opacity;
            float _PixelSize;
            float _NoiseStrength;
            float _FlickerSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            float Hash(float2 cell)
            {
                return frac(sin(dot(cell, float2(127.1, 311.7))) * 43758.5453);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // Screen-space cells keep the grain square and visibly pixelated.
                float2 cell = floor(input.positionCS.xy / max(1.0, _PixelSize));
                float tick = floor(_Time.y * _FlickerSpeed);
                float noise = Hash(cell + float2(tick * 13.0, tick * 7.0));
                float slowNoise = Hash(cell * 0.37 + float2(17.0, 41.0));
                float cross = abs(input.uv.y * 2.0 - 1.0);
                float edge = 0.83 + (slowNoise - 0.5) * _NoiseStrength * 0.55;
                float band = 1.0 - step(edge, cross);
                float grain = lerp(0.38, 1.0, noise);
                float gaps = lerp(step(0.34, slowNoise), 1.0, saturate(_Feasible));
                float strength = lerp(0.38, 1.0, saturate(_Feasible));
                float alpha = band * grain * gaps * strength * _Opacity * input.color.a;
                clip(alpha - 0.015);
                return half4(_LinkColor.rgb * input.color.rgb,
                    saturate(alpha * _LinkColor.a));
            }
            ENDHLSL
        }
    }
}
