Shader "Hidden/2026TapTap/RgbGlitch"
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

            float4 _RgbGlitchSettings;
            float _RgbSeparationPixels;
            float _RgbSeparationJitter;
            float _RgbGlitchTime;

            float Hash(float value)
            {
                return frac(sin(value * 127.1) * 43758.5453);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float intensity = _RgbGlitchSettings.x;
                float phase = floor(_RgbGlitchTime * max(1.0, _RgbGlitchSettings.z));
                float burst = step(0.82, Hash(phase));
                float globalShift = (Hash(phase + 23.0) * 2.0 - 1.0) * burst;
                float band = floor(uv.y * 32.0);
                float bandBurst = step(0.86, Hash(band + phase * 7.0));
                float bandShift = (Hash(band * 3.0 + phase * 11.0) * 2.0 - 1.0) * bandBurst;
                float2 shiftedUv = saturate(uv + float2(
                    (globalShift + bandShift * 0.6) * _RgbGlitchSettings.w * intensity,
                    globalShift * _RgbGlitchSettings.w * intensity * 0.25));
                float2 separation = float2(_RgbSeparationPixels / _ScreenParams.x,
                    _RgbSeparationPixels / _ScreenParams.y) * intensity;
                float channelJitter = saturate(_RgbSeparationJitter) * intensity;
                float redNoise = Hash(phase + 41.0) * 2.0 - 1.0;
                float greenNoise = Hash(phase + 59.0) * 2.0 - 1.0;
                float blueNoise = Hash(phase + 83.0) * 2.0 - 1.0;
                float2 redUv = saturate(shiftedUv + float2(
                    separation.x * (1.0 + redNoise * channelJitter),
                    separation.y * greenNoise * channelJitter * 0.3));
                float2 greenUv = saturate(shiftedUv + float2(
                    separation.x * greenNoise * channelJitter * 0.4,
                    separation.y * blueNoise * channelJitter * 0.2));
                float2 blueUv = saturate(shiftedUv + float2(
                    -separation.x * (1.0 + blueNoise * channelJitter),
                    separation.y * redNoise * channelJitter * 0.3));
                half4 center = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, shiftedUv);
                half red = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, redUv).r;
                half green = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, greenUv).g;
                half blue = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, blueUv).b;
                half3 color = half3(red, green, blue);
                float2 centered = (uv - 0.5) * float2(_ScreenParams.x / _ScreenParams.y, 1.0);
                float vignette = smoothstep(0.22, 0.85, length(centered));
                color *= 1.0 - vignette * _RgbGlitchSettings.y * intensity;
                return half4(color, center.a);
            }
            ENDHLSL
        }
    }
}
