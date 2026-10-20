Shader "Hidden/2026TapTap/WorldVisibility2D"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        struct MaskAttributes { float3 positionOS:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
        struct MaskVaryings { float4 positionCS:SV_POSITION; float2 radial:TEXCOORD0; half4 color:COLOR; };
        float _VisibilityCore;
        MaskVaryings MaskVert(MaskAttributes input)
        {
            MaskVaryings output;
            output.positionCS = TransformWorldToHClip(input.positionOS);
            output.radial = input.uv; output.color = input.color; return output;
        }
        half4 LightFrag(MaskVaryings input):SV_Target
        { return half4(input.color.rgb * (1-smoothstep(_VisibilityCore,1,length(input.radial))),1); }
        half4 SightFrag(MaskVaryings input):SV_Target { return half4(1,1,1,1); }
        ENDHLSL
        Pass
        {
            Name "遮挡灯光"
            ZWrite Off ZTest Always Cull Off Blend One One BlendOp Max
            HLSLPROGRAM
            #pragma vertex MaskVert
            #pragma fragment LightFrag
            ENDHLSL
        }
        Pass
        {
            Name "玩家视线"
            ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma vertex MaskVert
            #pragma fragment SightFrag
            ENDHLSL
        }
        Pass
        {
            Name "世界明暗合成"
            ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Composite
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            TEXTURE2D_X(_VisibilitySight);
            TEXTURE2D_X(_VisibilityLight);
            float _VisibilityAmbient, _VisibilityGate;
            half4 Composite(Varyings input):SV_Target
            {
                half4 source = SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_PointClamp,input.texcoord);
                half sight = SAMPLE_TEXTURE2D_X(_VisibilitySight,sampler_PointClamp,input.texcoord).r;
                half3 light = SAMPLE_TEXTURE2D_X(_VisibilityLight,sampler_PointClamp,input.texcoord).rgb;
                half3 illumination = max(_VisibilityAmbient,light);
                source.rgb *= saturate(illumination * lerp(1,sight,_VisibilityGate));
                return source;
            }
            ENDHLSL
        }
    }
}
