Shader "2026TapTap/Interaction Link"
{
    Properties
    {
        _LinkColor("Link Color", Color) = (1,0.08,0.08,1)
        _Feasible("Feasible", Range(0,1)) = 1
        _LinkCount("Link Count", Float) = 8
        _Opacity("Opacity", Range(0,1)) = 1
        _FlowSpeed("Flow Speed", Float) = 1.5
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+50" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float4 _LinkColor;
            float _Feasible;
            float _LinkCount;
            float _Opacity;
            float _FlowSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float links = max(2.0, _LinkCount);
                float along = input.uv.x * links - _Time.y * _FlowSpeed;
                float localX = frac(along) - 0.5;
                float localY = (input.uv.y - 0.5) * 2.0;
                float oval = length(float2(localX / 0.46, localY / 0.74));
                float ring = 1.0 - smoothstep(0.08, 0.20, abs(oval - 0.74));
                float core = (1.0 - smoothstep(0.0, 0.45, abs(localY))) * 0.12;
                float pulse = 0.72 + 0.28 * sin(input.uv.x * 17.0 - _Time.y * 7.0);
                float broken = step(0.5, frac(floor(along) * 0.5));
                float visible = lerp(broken, 1.0, saturate(_Feasible));
                float strength = lerp(0.42, 1.0, saturate(_Feasible));
                float alpha = saturate((ring * pulse + core) * visible *
                    strength * _Opacity * input.color.a);
                clip(alpha - 0.01);
                return half4(_LinkColor.rgb * input.color.rgb *
                    (0.85 + 0.35 * pulse), alpha * _LinkColor.a);
            }
            ENDHLSL
        }
    }
}
