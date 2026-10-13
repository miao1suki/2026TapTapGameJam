Shader "2026TapTap/Giving Color Dye Overlay"
{
    Properties
    {
        [Header(Dye Animation Speed)]
        _RedSpeed("红色燃烧速度 (倍)", Range(0.25,4)) = 1
        _BlueSpeed("蓝色叠染速度 (倍)", Range(0.25,4)) = 1
        _GreenSpeed("绿色生长速度 (倍)", Range(0.25,4)) = 1
        [Header(Procedural Detail)]
        _Pixelation("像素化强度", Range(0,1)) = 0.7
        _RedRimWidth("红色橙边粗细", Range(0.005,0.12)) = 0.035
        _RedNoiseAmount("红色扩散噪波", Range(0,1)) = 0.65
        _BlueNoiseAmount("蓝色扩散噪波", Range(0,1)) = 0.55
        _GreenNoiseAmount("绿色枝条噪波", Range(0,1)) = 0.7
        [HideInInspector] _SourceMap("Source Alpha", 2D) = "white" {}
        [HideInInspector] _SourceTint("Source Tint", Color) = (1,1,1,1)
        [HideInInspector] _EffectUvRect("Effect UV Rect", Vector) = (0,0,1,1)
        [HideInInspector] _EffectAspect("Effect Aspect", Float) = 1
        [HideInInspector] _DyeColor("Dye Color", Color) = (1,0,0,1)
        [HideInInspector] _EffectMode("Effect Mode", Float) = 1
        [HideInInspector] _EffectProgress("Effect Progress", Range(0,1)) = 0
        [HideInInspector] _EffectVisibility("Effect Visibility", Range(0,1)) = 1
        [HideInInspector] _AlphaThreshold("Alpha Threshold", Range(0,0.25)) = 0.015
        [HideInInspector] _DyeStrength("Dye Strength", Range(0,1)) = 1
        [HideInInspector] _EdgeWidth("Edge Width", Range(0.005,0.15)) = 0.035
        [HideInInspector] _NoiseScale("Noise Scale", Range(1,64)) = 22
        [HideInInspector] _FireGlow("Fire Glow", Range(0,2)) = 1
        [HideInInspector] _WaterDiffusion("Water Diffusion", Range(0,2)) = 1
        [HideInInspector] _BranchCount("Branch Count", Range(3,12)) = 8
        [HideInInspector] _BranchWidth("Branch Width", Range(0.005,0.08)) = 0.025
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+100"
        }
        Pass
        {
            Name "GivingColorOverlay"
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

            TEXTURE2D(_SourceMap);
            SAMPLER(sampler_SourceMap);

            CBUFFER_START(UnityPerMaterial)
            float4 _SourceMap_ST;
            float4 _SourceTint;
            float4 _EffectUvRect;
            float4 _DyeColor;
            float _EffectAspect;
            float _EffectMode;
            float _EffectProgress;
            float _EffectVisibility;
            float _AlphaThreshold;
            float _DyeStrength;
            float _EdgeWidth;
            float _NoiseScale;
            float _FireGlow;
            float _WaterDiffusion;
            float _BranchCount;
            float _BranchWidth;
            float _RedSpeed;
            float _BlueSpeed;
            float _GreenSpeed;
            float _Pixelation;
            float _RedRimWidth;
            float _RedNoiseAmount;
            float _BlueNoiseAmount;
            float _GreenNoiseAmount;
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

            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float Noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x),
                    lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), f.x), f.y);
            }

            float Segment(float2 p, float2 a, float2 b, float width)
            {
                float2 ab = b - a;
                float t = saturate(dot(p - a, ab) / max(.000001, dot(ab, ab)));
                float d = length(p - (a + t * ab));
                return 1 - smoothstep(width, width * 1.8, d);
            }

            float BranchTree(float2 p, float angle, float growth, float width,
                float noiseAmount, float seed)
            {
                float sectorWidth = 6.2831853 / max(3, _BranchCount);
                float directionNoise = (Noise(float2(seed * 11.3 + 4.7, 23.1)) - .5) * 2;
                float densityNoise = Noise(float2(seed * 17.9 + 2.3, 41.7));
                float temporalNoise = (Noise(float2(seed * 13.7, growth * 7.3)) - .5) * 2;
                float stableNoise = (Noise(float2(seed * 5.1, 9.7)) - .5) * 2;
                // Noise redistributes each ray inside its angular sector and
                // offsets its local growth, so radial density is uneven.
                angle += directionNoise * noiseAmount * sectorWidth * .42;
                growth = saturate(growth + directionNoise * noiseAmount * .10);
                float2 stem = float2(cos(angle), sin(angle));
                float radiusVariation = lerp(.68, 1.38, densityNoise);
                float2 trunkEnd = stem * (.27 + stableNoise * noiseAmount * .055) *
                    lerp(1, radiusVariation, noiseAmount);
                float trunkGrow = saturate(growth / .28);
                float childGrow = saturate((growth - .25) / .31);
                float tipGrow = saturate((growth - .52) / .32);
                float childrenVisible = smoothstep(.25, .30, growth);
                float tipsVisible = smoothstep(.52, .58, growth);
                float splitDensity = lerp(.62, 1.48, densityNoise);
                float split = (.34 + temporalNoise * noiseAmount * .34) *
                    lerp(1, splitDensity, noiseAmount);
                float2 leftDirection = float2(cos(angle + split), sin(angle + split));
                float2 rightDirection = float2(cos(angle - split), sin(angle - split));
                float childLength = (.28 + stableNoise * noiseAmount * .075) *
                    lerp(1, radiusVariation, noiseAmount);
                float2 leftEnd = trunkEnd + leftDirection * childLength;
                float2 rightEnd = trunkEnd + rightDirection * childLength;
                float leftTipNoise = (Noise(float2(seed * 19.1, 3.4)) - .5) *
                    noiseAmount * .9;
                float rightTipNoise = (Noise(float2(seed * 7.9, 5.8)) - .5) *
                    noiseAmount * .9;
                float leftTipAngle = angle + .35 + leftTipNoise;
                float rightTipAngle = angle - .35 + rightTipNoise;
                float2 twistField = p * 7.5 + float2(seed * 3.1, growth * 2.8);
                float twist = (Noise(twistField) - .5) * noiseAmount * .42;
                float2 twistedP = p + float2(-p.y, p.x) * twist;
                float result = Segment(twistedP, float2(0, 0), trunkEnd * trunkGrow, width * 1.3) *
                    smoothstep(0, .04, growth);
                result = max(result, Segment(twistedP, trunkEnd,
                    trunkEnd + leftDirection * (.28 * childGrow), width) * childrenVisible);
                result = max(result, Segment(twistedP, trunkEnd,
                    trunkEnd + rightDirection * (.28 * childGrow), width) * childrenVisible);
                result = max(result, Segment(twistedP, leftEnd,
                    leftEnd + float2(cos(leftTipAngle + .20), sin(leftTipAngle + .20)) * (.35 * tipGrow),
                    width * .72) * tipsVisible);
                result = max(result, Segment(twistedP, leftEnd,
                    leftEnd + float2(cos(leftTipAngle - .20), sin(leftTipAngle - .20)) * (.35 * tipGrow),
                    width * .72) * tipsVisible);
                result = max(result, Segment(twistedP, rightEnd,
                    rightEnd + float2(cos(rightTipAngle + .20), sin(rightTipAngle + .20)) * (.35 * tipGrow),
                    width * .72) * tipsVisible);
                result = max(result, Segment(twistedP, rightEnd,
                    rightEnd + float2(cos(rightTipAngle - .20), sin(rightTipAngle - .20)) * (.35 * tipGrow),
                    width * .72) * tipsVisible);
                return result;
            }

            float WaterPulse(float2 p, float2 offset, float progress,
                float start, float growTime, float edge, float noiseAmount)
            {
                float age = saturate((progress - start) / growTime);
                float radius = age * 1.17;
                float2 movingNoiseUv = (p + offset) * 25 -
                    float2(progress * 2.7, progress * 1.9) + start * 8;
                float broadWarp = (Noise(movingNoiseUv * .24) - .5) * edge * 4.5;
                float fineWarp = (Noise(movingNoiseUv) - .5) * edge * 2.8;
                float roughness = (broadWarp + fineWarp) * noiseAmount;
                float distanceFromCenter = length(p - offset) + roughness;
                return (1 - smoothstep(radius - edge, radius + edge,
                    distanceFromCenter)) * smoothstep(0, .1, age);
            }

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
                float2 sourceUv = input.uv * _SourceMap_ST.xy + _SourceMap_ST.zw;
                half4 source = SAMPLE_TEXTURE2D(_SourceMap, sampler_SourceMap, sourceUv);
                float sourceAlpha = source.a * _SourceTint.a * input.color.a;
                clip(sourceAlpha - _AlphaThreshold);

                float2 uv = (input.uv - _EffectUvRect.xy) /
                    max(_EffectUvRect.zw, float2(.0001, .0001));
                // Interpolate pixel size rather than grid count: this gives
                // the middle of the slider a visible block size on small sprites.
                float pixelation = saturate(_Pixelation);
                float pixelCellSize = lerp(1.0 / 512.0, 1.0 / 10.0,
                    pow(pixelation, 1.5));
                float2 snappedUv = (floor(uv / pixelCellSize) + .5) * pixelCellSize;
                float2 effectUv = lerp(uv, snappedUv, pixelation);
                float2 pixelatedMeshUv = effectUv * _EffectUvRect.zw + _EffectUvRect.xy;
                float2 pixelatedSourceUv = pixelatedMeshUv * _SourceMap_ST.xy + _SourceMap_ST.zw;
                half4 pixelatedSource = SAMPLE_TEXTURE2D(
                    _SourceMap, sampler_SourceMap, pixelatedSourceUv);
                float2 centered = (effectUv - .5) * float2(max(.1, _EffectAspect), 1);
                float distance01 = length(centered) /
                    max(.001, length(float2(max(.1, _EffectAspect), 1) * .5));
                float progress = saturate(_EffectProgress);
                float2 radialPos = centered /
                    max(.001, length(float2(max(.1, _EffectAspect), 1) * .5));
                float variation = (Noise(effectUv * _NoiseScale + progress * 2) - .5)
                    * _EdgeWidth * 2;
                float front = progress - distance01 - variation;
                float cover = smoothstep(-_EdgeWidth, _EdgeWidth, front);
                float3 pigment = _DyeColor.rgb;

                if (_EffectMode < 1.5)
                {
                    // Reverse burn: the center catches first. Irregular flame
                    // tongues and a red/orange rim advance toward the outside.
                    float angle = atan2(radialPos.y, radialPos.x);
                    float redNoise = saturate(_RedNoiseAmount);
                    float rimWidth = max(.005, _RedRimWidth);
                    float tongues = sin(angle * 19 + progress * 25 +
                        sin(angle * 7 - progress * 9) * 1.3) *
                        _EdgeWidth * 2.2 * redNoise;
                    float2 fireNoiseUv = effectUv * _NoiseScale;
                    float broadFireNoise = (Noise(fireNoiseUv * .38 -
                        float2(progress * 2.8, progress * 1.7)) - .5) * _EdgeWidth * 5.5;
                    float detailFireNoise = (Noise(fireNoiseUv * 1.65 -
                        float2(progress * 7.5, progress * 5.2)) - .5) * _EdgeWidth * 2.5;
                    float hotNoise = (broadFireNoise + detailFireNoise) * redNoise;
                    front = progress - distance01 + tongues + hotNoise;
                    float burn = smoothstep(-_EdgeWidth, _EdgeWidth, front);
                    float orangeRim = 1 - smoothstep(0, rimWidth * 2.2,
                        abs(front + rimWidth * .35));
                    float yellowRim = 1 - smoothstep(0, rimWidth * 1.3,
                        abs(front - rimWidth * .55));
                    cover = max(burn, orangeRim * .82);
                    float fireFlicker = Noise(effectUv * _NoiseScale * 1.8 -
                        float2(progress * 8, progress * 13));
                    orangeRim *= lerp(1, .55 + fireFlicker, redNoise);
                    pigment = lerp(pigment, float3(1, .31, .025),
                        saturate(orangeRim * _FireGlow));
                    pigment = lerp(pigment, float3(1, .74, .12),
                        saturate(yellowRim * _FireGlow * .65));
                }
                else if (_EffectMode < 2.5)
                {
                    // Four successive translucent pigment drops. Each drop
                    // spreads out from a nearby center and deepens the wash.
                    float blueNoise = saturate(_BlueNoiseAmount);
                    float edge = _EdgeWidth * (1 + _WaterDiffusion * .7);
                    float first = WaterPulse(radialPos, float2(0, 0), progress,
                        0, .52, edge, blueNoise);
                    float second = WaterPulse(radialPos, float2(.045, -.025), progress,
                        .18, .51, edge, blueNoise);
                    float third = WaterPulse(radialPos, float2(-.04, .035), progress,
                        .39, .45, edge, blueNoise);
                    float fourth = WaterPulse(radialPos, float2(.018, .05), progress,
                        .59, .40, edge, blueNoise);
                    cover = saturate(first * .20 + second * .25 +
                        third * .27 + fourth * .28);
                    float secondRadius = saturate((progress - .18) / .51) * 1.17;
                    float fourthRadius = saturate((progress - .59) / .40) * 1.17;
                    float rimNoiseA = (Noise(effectUv * _NoiseScale * .8 +
                        float2(progress * 3, 0)) - .5) * edge * 2 * blueNoise;
                    float rimNoiseB = (Noise(effectUv * _NoiseScale * .65 -
                        float2(0, progress * 4)) - .5) * edge * 2 * blueNoise;
                    float watercolorRim =
                        (1 - smoothstep(0, edge * 2,
                            abs(length(radialPos - float2(.045, -.025)) + rimNoiseA - secondRadius))) *
                            smoothstep(.18, .24, progress) +
                        (1 - smoothstep(0, edge * 2,
                            abs(length(radialPos - float2(.018, .05)) + rimNoiseB - fourthRadius))) *
                            smoothstep(.59, .65, progress);
                    pigment = lerp(pigment, float3(.48, .84, 1),
                        saturate(watercolorRim * .45 * _WaterDiffusion));
                }
                else
                {
                    // Each radial stem first splits into two and then into
                    // four visible tips; the final wash fills gaps afterward.
                    float count = clamp(floor(_BranchCount + .5), 3, 12);
                    float stepAngle = 6.2831853 / count;
                    float width = _BranchWidth;
                    float greenNoise = saturate(_GreenNoiseAmount);
                    float branch = 0;
                    branch = max(branch, BranchTree(radialPos, stepAngle * 0, progress, width, greenNoise, 0));
                    branch = max(branch, BranchTree(radialPos, stepAngle * 1, progress, width, greenNoise, 1));
                    branch = max(branch, BranchTree(radialPos, stepAngle * 2, progress, width, greenNoise, 2));
                    branch = max(branch, BranchTree(radialPos, stepAngle * 3, progress, width, greenNoise, 3) * step(3.5, count));
                    branch = max(branch, BranchTree(radialPos, stepAngle * 4, progress, width, greenNoise, 4) * step(4.5, count));
                    branch = max(branch, BranchTree(radialPos, stepAngle * 5, progress, width, greenNoise, 5) * step(5.5, count));
                    branch = max(branch, BranchTree(radialPos, stepAngle * 6, progress, width, greenNoise, 6) * step(6.5, count));
                    branch = max(branch, BranchTree(radialPos, stepAngle * 7, progress, width, greenNoise, 7) * step(7.5, count));
                    branch = max(branch, BranchTree(radialPos, stepAngle * 8, progress, width, greenNoise, 8) * step(8.5, count));
                    branch = max(branch, BranchTree(radialPos, stepAngle * 9, progress, width, greenNoise, 9) * step(9.5, count));
                    branch = max(branch, BranchTree(radialPos, stepAngle * 10, progress, width, greenNoise, 10) * step(10.5, count));
                    branch = max(branch, BranchTree(radialPos, stepAngle * 11, progress, width, greenNoise, 11) * step(11.5, count));
                    float branchNoise = (Noise(effectUv * _NoiseScale * .7 +
                        progress * 2) - .5) * _EdgeWidth * greenNoise * 2;
                    float finalWash = smoothstep(.74, 1, progress) *
                        smoothstep(-_EdgeWidth, _EdgeWidth, front + branchNoise);
                    cover = max(branch, finalWash);
                    pigment = lerp(pigment, float3(.35, 1, .28),
                        saturate(branch * .65));
                }

                // A completed dye remains the requested hue indefinitely.
                if (progress >= .999)
                {
                    cover = 1;
                    pigment = _DyeColor.rgb;
                }
                // The gameplay material has already switched by the time the
                // success callback runs. Hold a neutral coat outside the
                // advancing front so the change is actually visible.
                float3 sourceColor = lerp(source.rgb, pixelatedSource.rgb, pixelation);
                float sourceLuma = dot(sourceColor, float3(.2126, .7152, .0722));
                float3 neutral = float3(1, 1, 1) * (.82 + .18 * sourceLuma);
                float3 visibleColor = lerp(neutral, pigment, saturate(cover));
                return half4(visibleColor,
                    saturate(_DyeStrength * sourceAlpha * _EffectVisibility));
            }
            ENDHLSL
        }
    }
    FallBack Off
}
