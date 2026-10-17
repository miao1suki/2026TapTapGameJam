Shader "Hidden/Project/Editor/Artwork Preview"
{
    Properties { _MainTex("Artwork", 2D) = "white" {} }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            struct Input { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varying { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
            Varying Vert(Input input) { Varying output; output.positionCS=TransformObjectToHClip(input.positionOS.xyz); output.uv=input.uv; return output; }
            half4 Frag(Varying input):SV_Target { half4 color=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,input.uv); clip(color.a-.01h); return color; }
            ENDHLSL
        }
    }
}
