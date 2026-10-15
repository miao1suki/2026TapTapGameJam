Shader "UI/GameTransitionReveal"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (0, 0, 0, 1)
        _Center ("Center", Vector) = (0.5, 0.5, 0, 0)
        _Radius ("Radius", Range(0, 2.5)) = 0
        _Softness ("Softness", Range(0, 0.5)) = 0.08
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Overlay"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            fixed4 _Color;
            float4 _Center;
            float _Radius;
            float _Softness;
            sampler2D _MainTex;

            v2f vert(appdata_t data)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(data.vertex);
                output.uv = data.texcoord;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float aspect = _ScreenParams.x / max(_ScreenParams.y, 1);
                float2 delta = input.uv - _Center.xy;
                delta.x *= aspect;
                float distanceFromCenter = length(delta);
                float alpha = smoothstep(
                    _Radius - _Softness,
                    _Radius + _Softness,
                    distanceFromCenter);
                return fixed4(_Color.rgb, _Color.a * alpha);
            }
            ENDCG
        }
    }
    Fallback Off
}
