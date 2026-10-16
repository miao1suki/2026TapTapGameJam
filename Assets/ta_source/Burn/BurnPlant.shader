Shader "2026TapTap/Plant Burn"
{
    Properties
    {
        [PerRendererData] _MainTex("源纹理", 2D) = "white" {}
        [HideInInspector] _SourceTint("源颜色", Color) = (1,1,1,1)
        [HideInInspector] _BurnUvRect("UV 范围", Vector) = (0,0,1,1)
        [HideInInspector] _BurnYBounds("高度范围", Vector) = (-0.5,0.5,0,0)
        [HideInInspector] _BurnElapsed("燃烧时间", Float) = 0
        [HideInInspector] _BurnDuration("燃烧时长", Float) = 2.25
        [HideInInspector] _FlameEdgeWidth("火焰边宽度", Range(0.005,0.18)) = 0.065
        [HideInInspector] _FlameEdgeIntensity("橙色焰边强度", Range(0,2)) = 1.25
        [HideInInspector] _Pixelation("像素化程度", Range(0,1)) = 0.24
        [HideInInspector] _EdgeNoise("边缘扰动", Range(0,1)) = 0.5
        [HideInInspector] _AshFadeSeconds("灰烬消散时间", Float) = 0.7
        [HideInInspector] _FlameCoreColor("火焰内芯", Color) = (1,0.94,0.46,1)
        [HideInInspector] _FlameOuterColor("火焰外焰", Color) = (1,0.19,0.025,1)
        [HideInInspector] _AshColor("灰烬颜色", Color) = (0.19,0.17,0.16,1)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+20"
        }

        Pass
        {
            Name "PlantBurn"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _MainTex_TexelSize;
                float4 _SourceTint;
                float4 _BurnUvRect;
                float4 _BurnYBounds;
                float4 _FlameCoreColor;
                float4 _FlameOuterColor;
                float4 _AshColor;
                float _BurnElapsed;
                float _BurnDuration;
                float _FlameEdgeWidth;
                float _FlameEdgeIntensity;
                float _Pixelation;
                float _EdgeNoise;
                float _AshFadeSeconds;
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
                float3 positionOS : TEXCOORD1;
                half4 color : COLOR;
            };

            float Hash21(float2 value)
            {
                return frac(sin(dot(value, float2(127.1, 311.7))) * 43758.5453);
            }

            float Noise(float2 value)
            {
                float2 cell = floor(value);
                float2 local = frac(value);
                local = local * local * (3.0 - 2.0 * local);
                float low = lerp(Hash21(cell), Hash21(cell + float2(1, 0)), local.x);
                float high = lerp(Hash21(cell + float2(0, 1)), Hash21(cell + float2(1, 1)), local.x);
                return lerp(low, high, local.y);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.positionOS = input.positionOS.xyz;
                output.color = input.color;
                return output;
            }

            float4 SampleSource(float2 uv)
            {
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv) * _SourceTint;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float2 uvMin = _BurnUvRect.xy;
                float2 uvSize = max(_BurnUvRect.zw, float2(.0001, .0001));
                float2 pixelStep = max(_MainTex_TexelSize.xy, float2(.00001, .00001))
                    * lerp(1.0, 14.0, _Pixelation);
                float2 pixel = floor((uv - uvMin) / pixelStep) + .5;
                float2 sampledUv = uvMin + pixel * pixelStep;
                sampledUv = clamp(sampledUv, uvMin + pixelStep * .5, uvMin + uvSize - pixelStep * .5);

                float4 source = SampleSource(sampledUv) * input.color;
                clip(source.a - .008);

                float2 texel = max(_MainTex_TexelSize.xy, float2(.00001, .00001));
                float alphaLeft = SampleSource(clamp(sampledUv - float2(texel.x, 0), uvMin, uvMin + uvSize)).a;
                float alphaRight = SampleSource(clamp(sampledUv + float2(texel.x, 0), uvMin, uvMin + uvSize)).a;
                float alphaDown = SampleSource(clamp(sampledUv - float2(0, texel.y), uvMin, uvMin + uvSize)).a;
                float alphaUp = SampleSource(clamp(sampledUv + float2(0, texel.y), uvMin, uvMin + uvSize)).a;
                float contour = saturate(source.a - min(min(alphaLeft, alphaRight), min(alphaDown, alphaUp)));

                float height = saturate(
                    (input.positionOS.y - _BurnYBounds.x) /
                    max(.0001, _BurnYBounds.y - _BurnYBounds.x));
                float elapsed = max(0, _BurnElapsed);
                float duration = max(.1, _BurnDuration);
                float front = saturate(elapsed / duration);
                float noise = Noise(float2(uv.x * 41.0, height * 27.0 + elapsed * 2.4));
                float roughFront = front + (noise - .5) * _EdgeNoise * .095;
                float distanceBehindFront = roughFront - height;
                float edgeWidth = max(.005, _FlameEdgeWidth);

                float flameBand = 1.0 - smoothstep(edgeWidth * .18, edgeWidth, abs(distanceBehindFront));
                float fireTail = 1.0 - smoothstep(0, max(.05, _AshFadeSeconds), max(0, elapsed - duration));
                flameBand *= fireTail;

                float burned = smoothstep(-edgeWidth * .42, edgeWidth * .42, distanceBehindFront);
                float ageSinceIgnition = max(0, elapsed - height * duration);
                float ashAlpha = 1.0 - smoothstep(0, max(.05, _AshFadeSeconds), ageSinceIgnition);
                float bodyAlpha = lerp(1.0, ashAlpha, burned);

                float ember = saturate((noise - .67) * 3.2) * burned * ashAlpha;
                float3 ash = lerp(source.rgb, _AshColor.rgb, .88);
                ash += _FlameOuterColor.rgb * ember * .35;
                float3 body = lerp(source.rgb, ash, burned);

                float fireCore = 1.0 - smoothstep(.12, .7, abs(distanceBehindFront) / edgeWidth);
                float3 fire = lerp(_FlameOuterColor.rgb, _FlameCoreColor.rgb, fireCore);
                body = lerp(body, fire, saturate(flameBand * _FlameEdgeIntensity));
                body += _FlameOuterColor.rgb * flameBand * max(0, _FlameEdgeIntensity - 1.0) * .35;

                float silhouetteRim = contour * (1.0 - burned * .45) *
                    _FlameEdgeIntensity * .38;
                body = lerp(body, _FlameOuterColor.rgb, saturate(silhouetteRim));

                float alpha = source.a * max(bodyAlpha, flameBand * saturate(_FlameEdgeIntensity));
                alpha = max(alpha, source.a * silhouetteRim);
                return half4(body, alpha);
            }
            ENDHLSL
        }
    }
}
