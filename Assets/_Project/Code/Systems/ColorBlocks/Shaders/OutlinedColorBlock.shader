Shader "2026TapTap/Outlined Color Block"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _ColorRevealOpacity ("Reveal Opacity", Range(0,1)) = 1
        _OutlineColor ("Outline Color", Color) = (0,0,0,1)
        _BorderWidth ("Border Width", Range(0,0.25)) = 0.035
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _OutlineColor;
                float _BorderWidth;
                float _ColorRevealOpacity;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionOS : TEXCOORD0; float3 normalOS : TEXCOORD1; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionOS = input.positionOS.xyz;
                output.normalOS = input.normalOS;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float3 axis = abs(normalize(input.normalOS));
                float2 face = axis.x > axis.y && axis.x > axis.z ? input.positionOS.yz :
                              axis.y > axis.z ? input.positionOS.xz : input.positionOS.xy;
                float edge = max(abs(face.x), abs(face.y));
                half4 result = edge >= (0.5 - _BorderWidth) ? _OutlineColor : _BaseColor;
                result.a *= saturate(_ColorRevealOpacity);
                return result;
            }
            ENDHLSL
        }
    }
}
