Shader "Hidden/2026TapTap/SelectiveHSV"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            ZTest Always
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            TEXTURE2D_X(_ColorObjectMask);
            SAMPLER(sampler_ColorObjectMask);
            float _TargetSaturation;
            float _WhiteAmount;
            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                half4 source = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                half visible = SAMPLE_TEXTURE2D_X(_ColorObjectMask, sampler_ColorObjectMask, uv).r;
                // HSV: preserve hue and value, reduce only saturation.
                half value = max(source.r, max(source.g, source.b));
                half3 grayAtValue = half3(value, value, value);
                half3 restored = lerp(grayAtValue, source.rgb, _TargetSaturation);
                restored = lerp(restored, half3(1, 1, 1), saturate(_WhiteAmount));
                source.rgb = lerp(source.rgb, restored, visible);
                return source;
            }
            ENDHLSL
        }
    }
}
