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
        half4 ConnectedFrag(MaskVaryings input):SV_Target
        { return 1-smoothstep(0.85,1.0,length(input.radial)); }
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
            TEXTURE2D_X(_VisibilityLight);
            float _VisibilityAmbient, _VisibilityGate;
            float4 _VisibilitySoftness;
            TEXTURE2D_X(_VisibilitySoftMask);
            half4 Composite(Varyings input):SV_Target
            {
                half4 source = SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_PointClamp,input.texcoord);
                half sight = SAMPLE_TEXTURE2D_X(_VisibilitySight,sampler_PointClamp,input.texcoord).r;
                half3 light = SAMPLE_TEXTURE2D_X(_VisibilityLight,sampler_PointClamp,input.texcoord).rgb;
                if (_VisibilitySoftness.z > 0)
                {
                    half4 filtered = SAMPLE_TEXTURE2D_X(_VisibilitySoftMask,sampler_LinearClamp,input.texcoord);
                    // Fade toward the boundary from inside the visible polygon.
                    // Never reveal a pixel rejected by the original sight/light masks.
                    sight *= smoothstep(0.5h,1.0h,filtered.a);
                    light = min(light,filtered.rgb);
                }
                half3 illumination = max(_VisibilityAmbient,light);
                half factor = saturate(illumination * lerp(1,sight,_VisibilityGate));
                source.rgb *= lerp(1,factor,saturate(_VisibilityStrength));
                return source;
            }
            ENDHLSL
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        TEXTURE2D_X(_VisibilitySight);
        TEXTURE2D_X(_VisibilityPreviousSight);
        float _VisibilityStrength;
        float _VisibilityConnectedBlend;
        float4 _VisibilityBlurDirection;
        half4 BlurSample(float2 uv, bool pack)
        {
            half4 value = SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,saturate(uv));
            if (pack) value.a = SAMPLE_TEXTURE2D_X(_VisibilitySight,sampler_LinearClamp,saturate(uv)).r;
            return value;
        }
        half4 Gaussian(float2 uv, bool pack)
        {
            half4 value = 0; half total = 0;
            [unroll] for (int i=-8; i<=8; i++)
            {
                half weight = exp(-0.5h * (i*i) / 9.0h);
                value += BlurSample(uv + _VisibilityBlurDirection.xy * (i / 8.0),pack) * weight;
                total += weight;
            }
            return value / total;
        }
        half4 Horizontal(Varyings input):SV_Target { return Gaussian(input.texcoord,true); }
        half4 Vertical(Varyings input):SV_Target { return Gaussian(input.texcoord,false); }
        half4 ConnectedComposite(Varyings input):SV_Target
        {
            half4 source = SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_PointClamp,input.texcoord);
            half current = SAMPLE_TEXTURE2D_X(_VisibilitySight,sampler_LinearClamp,input.texcoord).r;
            half previous = SAMPLE_TEXTURE2D_X(_VisibilityPreviousSight,sampler_LinearClamp,input.texcoord).r;
            half sight = saturate(lerp(previous,current,saturate(_VisibilityConnectedBlend)));
            source.rgb *= lerp(1,sight,saturate(_VisibilityStrength));
            return source;
        }
        ENDHLSL
        Pass
        {
            Name "横向高斯柔化"
            ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Horizontal
            ENDHLSL
        }
        Pass
        {
            Name "纵向高斯柔化"
            ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Vertical
            ENDHLSL
        }
        Pass
        {
            Name "连通空间范围渐隐"
            ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma vertex MaskVert
            #pragma fragment ConnectedFrag
            ENDHLSL
        }
        Pass
        {
            Name "连通空间原画亮度"
            ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment ConnectedComposite
            ENDHLSL
        }
        Pass
        {
            Name "墙体保留"
            ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma vertex MaskVert
            #pragma fragment SightFrag
            ENDHLSL
        }
    }
}
