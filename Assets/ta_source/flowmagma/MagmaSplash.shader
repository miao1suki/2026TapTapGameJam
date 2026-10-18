Shader "TapTap/FlowMagma/Bubble Splash"
{
    Properties
    {
        [MainTexture] _MainTex ("岩浆图集", 2D) = "white" {}
        [MainColor] _Color ("颜色", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Name "MagmaSplash"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };

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
                // The atlas's adjacent red 2x2 spark supplies the particle color.
                half4 spark = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex,
                    float2(49.5 / 215.0, (134.0 - 111.5) / 134.0));
                float2 pixel = floor(saturate(input.uv) * 8.0) + 0.5;
                float radius = length((pixel - 4.0) / 4.0);
                float ring = step(0.42, radius) * (1.0 - step(0.92, radius));
                half4 result = spark * input.color * _Color;
                result.a *= ring;
                clip(result.a - 0.01);
                return result;
            }
            ENDHLSL
        }
    }
}
