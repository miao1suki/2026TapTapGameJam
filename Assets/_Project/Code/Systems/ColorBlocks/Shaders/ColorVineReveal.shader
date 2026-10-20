Shader "2026TapTap/ColorVineReveal"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
        _TintColor("Tint", Color) = (1,1,1,1)
        _ColorRevealOpacity("Reveal", Range(0,1)) = 1
        [PerRendererData] _GridArtSliceClamp("Clamp Tile Slice", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST, _MainTex_TexelSize, _TintColor;
            float _ColorRevealOpacity, _GridArtSliceClamp;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; float4 slice:TEXCOORD1; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; nointerpolation float4 slice:TEXCOORD1; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color;
                output.slice = input.slice;
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                float2 uv = input.uv;
                if (_GridArtSliceClamp > .5 && all(input.slice.zw > input.slice.xy))
                {
                    float2 inset = min(_MainTex_TexelSize.xy * .5, (input.slice.zw-input.slice.xy)*.5);
                    uv = clamp(uv, input.slice.xy+inset, input.slice.zw-inset);
                }
                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv) * input.color * _TintColor;
                color.a *= _ColorRevealOpacity;
                clip(color.a - .001);
                return color;
            }
            ENDHLSL
        }
    }
}
