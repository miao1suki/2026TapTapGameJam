Shader "TapTap/FlowMagma/Square Particles"
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

            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // Two solid 2x2 squares below the lava tile: hot red and cooled dark red.
                half4 hot = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex,
                    float2(49.5 / 215.0, (134.0 - 111.5) / 134.0));
                half4 cooled = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex,
                    float2(61.5 / 215.0, (134.0 - 111.5) / 134.0));
                half4 result = lerp(hot, cooled, saturate(input.color.r)) * _Color;
                result.a *= input.color.a;
                clip(result.a - 0.01);
                return result;
            }
            ENDHLSL
        }
    }
}
