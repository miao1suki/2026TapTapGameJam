Shader "TapTap/FlowMagma/Square Particles"
{
    Properties
    {
        [MainTexture] _MainTex ("岩浆图集", 2D) = "white" {}
        [MainColor] _Color ("颜色", Color) = (1, 1, 1, 1)
        _IgnitionColor ("喷出高亮", Color) = (1, 0.82, 0.38, 1)
        _FlameColor ("灼热橙红", Color) = (1, 0.33, 0.12, 1)
        _EmberColor ("降温深红", Color) = (0.7, 0.24, 0.24, 1)
        _CinderColor ("余烬暗色", Color) = (0.17, 0.035, 0.05, 1)
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

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _IgnitionColor;
                half4 _FlameColor;
                half4 _EmberColor;
                half4 _CinderColor;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // The two solid atlas squares provide the particle silhouette.
                half4 hot = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex,
                    float2(49.5 / 215.0, (134.0 - 111.5) / 134.0));
                half4 cooled = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex,
                    float2(61.5 / 215.0, (134.0 - 111.5) / 134.0));
                float age = saturate(input.color.r);
                half3 palette;
                if (age < 0.18)
                    palette = lerp(_IgnitionColor.rgb, _FlameColor.rgb, smoothstep(0.0, 0.18, age));
                else if (age < 0.62)
                    palette = lerp(_FlameColor.rgb, _EmberColor.rgb, smoothstep(0.18, 0.62, age));
                else
                    palette = lerp(_EmberColor.rgb, _CinderColor.rgb, smoothstep(0.62, 1.0, age));
                half4 result = half4(palette * _Color.rgb,
                    lerp(hot.a, cooled.a, age) * input.color.a * _Color.a);
                clip(result.a - 0.01);
                return result;
            }
            ENDHLSL
        }
    }
}
