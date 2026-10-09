Shader "Hidden/2026TapTap/ColorObjectMask"
{
    Properties
    {
        _ColorMaskAlphaTexture("Alpha Texture", 2D) = "white" {}
        _ColorMaskUseVertexAlpha("Vertex Alpha", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_ColorMaskAlphaTexture); SAMPLER(sampler_ColorMaskAlphaTexture);
            float _ColorMaskUseVertexAlpha;
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float alpha : TEXCOORD1; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.alpha = lerp(1, input.color.a, saturate(_ColorMaskUseVertexAlpha));
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half alpha = SAMPLE_TEXTURE2D(_ColorMaskAlphaTexture, sampler_ColorMaskAlphaTexture, input.uv).a * input.alpha;
                clip(alpha - .01);
                // The source image is already alpha blended; applying alpha again would leave raw color visible.
                return half4(1, 1, 1, 1);
            }
            ENDHLSL
        }
    }
}
