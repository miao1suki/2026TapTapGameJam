Shader "Hidden/2026TapTap/RgbGlitchPreviewPattern"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 color;
                if (input.uv.x < 0.25) color = half3(0.95, 0.12, 0.23);
                else if (input.uv.x < 0.5) color = half3(0.12, 0.8, 0.95);
                else if (input.uv.x < 0.75) color = half3(0.24, 0.95, 0.38);
                else color = half3(0.95, 0.77, 0.16);

                float grid = max(step(0.985, frac(input.uv.x * 16.0)),
                    step(0.985, frac(input.uv.y * 9.0)));
                return half4(lerp(color, half3(1.0, 1.0, 1.0), grid * 0.35), 1.0);
            }
            ENDHLSL
        }
    }
}
