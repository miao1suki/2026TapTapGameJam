Shader "Custom/Pixel Water/Screen Distortion"
{
    Properties
    {
        _DistortionStrength ("Screen Distortion", Range(0, 0.02)) = 0.004
        _NoiseScale ("Noise Scale", Range(0.5, 32)) = 6
        _AnimationSpeed ("Noise Flow Speed", Range(0, 4)) = 0.9
        _DistortionVariation ("Distortion Strength Variation", Range(0, 1)) = 0.75
        _DistortionChangeSpeed ("Distortion Change Speed", Range(0, 4)) = 0.9
        _Opacity ("Water Filter Opacity", Range(0, 1)) = 0.9
        _ColorRevealOpacity ("Color Reveal", Range(0, 1)) = 1
        _Tint ("Subtle Water Tint", Color) = (0.72, 0.92, 1, 1)
        _TintStrength ("Tint Strength", Range(0, 0.25)) = 0.1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "PixelWaterScreenDistortion"
            // Drawn only by WaterUnderlayRendererFeature after transparent objects have rendered.
            Tags { "LightMode" = "PixelWaterRefraction" }

            Blend SrcAlpha OneMinusSrcAlpha
            ColorMask RGB
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D_X(_PixelWaterSceneColor);
            SAMPLER(sampler_PixelWaterSceneColor);

            CBUFFER_START(UnityPerMaterial)
                float _DistortionStrength;
                float _NoiseScale;
                float _AnimationSpeed;
                float _DistortionVariation;
                float _DistortionChangeSpeed;
                float _Opacity;
                float _ColorRevealOpacity;
                float4 _Tint;
                float _TintStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float Hash21(float2 coord)
            {
                coord = frac(coord * float2(123.34, 456.21));
                coord += dot(coord, coord + 45.32);
                return frac(coord.x * coord.y);
            }

            float ValueNoise(float2 coord)
            {
                float2 cell = floor(coord);
                float2 blend = frac(coord);
                blend = blend * blend * (3.0 - 2.0 * blend);

                float lower = lerp(Hash21(cell), Hash21(cell + float2(1.0, 0.0)), blend.x);
                float upper = lerp(Hash21(cell + float2(0.0, 1.0)), Hash21(cell + 1.0), blend.x);
                return lerp(lower, upper, blend.y);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                // Keep the filter quad rigid. Refraction is fragment-only screen-space sampling.
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                clip(_Opacity * _ColorRevealOpacity - 0.001);

                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float time = _Time.y * _AnimationSpeed;
                float2 noiseUV = input.uv * _NoiseScale;
                float xWave = ValueNoise(noiseUV + float2(time, time * 0.37));
                float yWave = ValueNoise(noiseUV + float2(-time * 0.29, time + 17.0));
                float strengthNoise = ValueNoise(float2(_Time.y * _DistortionChangeSpeed, 23.41));
                float strengthMultiplier = lerp(1.0 - _DistortionVariation, 1.0 + _DistortionVariation, strengthNoise);
                float2 offset = (float2(xWave, yWave) * 2.0 - 1.0) * _DistortionStrength * strengthMultiplier;

                float2 refractedUV = saturate(screenUV + offset);
                half3 refractedColor = SAMPLE_TEXTURE2D_X(
                    _PixelWaterSceneColor, sampler_PixelWaterSceneColor, refractedUV).rgb;
                refractedColor = lerp(refractedColor, (half3)_Tint.rgb, _TintStrength);

                return half4(refractedColor, _Opacity * _ColorRevealOpacity);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
