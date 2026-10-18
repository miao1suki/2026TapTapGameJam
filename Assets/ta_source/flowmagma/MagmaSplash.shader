Shader "TapTap/FlowMagma/Square Particles"
{
    Properties
    {
        [MainTexture] _MainTex ("岩浆图集", 2D) = "white" {}
        [MainColor] _Color ("颜色", Color) = (1, 1, 1, 1)
        _ColorRevealOpacity ("颜色渐显", Range(0,1)) = 1
        _IgnitionColor ("喷出高亮", Color) = (1,0.82,0.38,1)
        _FlameColor ("灼热橙红", Color) = (1,0.33,0.12,1)
        _EmberColor ("降温深红", Color) = (0.7,0.24,0.24,1)
        _CinderColor ("余烬暗色", Color) = (0.17,0.035,0.05,1)
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
            #include "FlowMagmaSampling.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _ColorRevealOpacity;
                half4 _IgnitionColor, _FlameColor, _EmberColor, _CinderColor;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; float2 worldXY : TEXCOORD1; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                output.worldXY = TransformObjectToWorld(input.positionOS.xyz).xy;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // TA 95da62e: two solid atlas samples and a four-stage cooling palette.
                half4 hot = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex,
                    float2(49.5 / 215.0, (134.0 - 111.5) / 134.0));
                half4 cooled = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex,
                    float2(61.5 / 215.0, (134.0 - 111.5) / 134.0));
                float age = saturate(input.color.r);
                half3 palette;
                if (age < .18)
                    palette = lerp(_IgnitionColor.rgb,_FlameColor.rgb,smoothstep(0,.18,age));
                else if (age < .62)
                    palette = lerp(_FlameColor.rgb,_EmberColor.rgb,smoothstep(.18,.62,age));
                else
                    palette = lerp(_EmberColor.rgb,_CinderColor.rgb,smoothstep(.62,1,age));
                half4 result = half4(palette * _Color.rgb,
                    lerp(hot.a,cooled.a,age) * input.color.a * _Color.a * _ColorRevealOpacity);
                clip(result.a - 0.01);
                return result;
            }
            ENDHLSL
        }
    }
}
