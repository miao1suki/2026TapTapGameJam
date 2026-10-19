Shader "2026TapTap/Grid Art/Unlit"
{
    Properties { _BaseMap("图集", 2D) = "white" {} _BaseColor("颜色", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END
            float4 _BaseMap_TexelSize;
            struct A { float4 positionOS:POSITION; float2 uv:TEXCOORD0; float4 sampleBounds:TEXCOORD1; };
            struct V { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; nointerpolation float4 sampleBounds:TEXCOORD1; };
            V vert(A a) { V o; o.positionCS=TransformObjectToHClip(a.positionOS.xyz); o.uv=a.uv; o.sampleBounds=a.sampleBounds; return o; }
            half4 frag(V i):SV_Target
            {
                float2 uv = i.uv;
                // MSAA/interpolation can evaluate at the edge or just outside the atlas slice.
                // Clamp only sampling, not interpolated UVs or geometry: interior art pixels retain their exact size.
                if (all(i.sampleBounds.zw > i.sampleBounds.xy))
                {
                    float2 inset = min(_BaseMap_TexelSize.xy * .5, (i.sampleBounds.zw-i.sampleBounds.xy)*.5);
                    uv = clamp(uv, i.sampleBounds.xy+inset, i.sampleBounds.zw-inset);
                }
                half4 c=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,uv)*_BaseColor;
                clip(c.a-.001); return c;
            }
            ENDHLSL
        }
    }
}
