Shader "Hidden/2026TapTap/StoryStartCircleCurtain"
{
    Properties
    {
        _Color ("Color", Color) = (0, 0, 0, 1)
        _Center ("Center", Vector) = (0.5, 0.5, 0, 0)
        _Radius ("Radius", Float) = 0
        _Aspect ("Aspect", Float) = 1.7777778
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            float2 _Center;
            float _Radius;
            float _Aspect;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS =
                    UnityObjectToClipPos(input.positionOS);
                output.color = input.color * _Color;
                output.uv = input.uv;
                return output;
            }

            fixed4 Frag(Varyings input) : SV_Target
            {
                float2 delta =
                    (input.uv - _Center) *
                    float2(_Aspect, 1.0);
                float mask =
                    step(_Radius, length(delta));
                return fixed4(
                    input.color.rgb,
                    input.color.a * mask);
            }
            ENDCG
        }
    }
}
