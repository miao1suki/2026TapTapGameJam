Shader "2026TapTap/Interaction Link"
{
    Properties
    {
        [HideInInspector] _LinkColor("Link Color", Color) = (1,0.08,0.08,1)
        _LineThickness("链条粗细", Range(0.02,1.0)) = 0.2
        [HideInInspector] _Feasible("Feasible", Range(0,1)) = 1
        [HideInInspector] _Opacity("Opacity", Range(0,1)) = 0.68
        _PixelSize("Pixel Size", Range(1,16)) = 5
        _NoiseStrength("枝条噪波幅度", Range(0,1)) = 0.75
        _FlickerSpeed("效果速度", Range(0,20)) = 7
        _WaterEffectStrength("水纹外围效果强度", Range(0,1)) = 0.55
        [HideInInspector] _Style("Color Style", Range(0,3)) = 0
        [HideInInspector] _RevealProgress("Reveal Progress", Range(0,1)) = 1
        [HideInInspector] _DissolveProgress("Dissolve Progress", Range(0,1)) = 0
        [HideInInspector] _LineLength("Line Length", Float) = 1
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
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float4 _LinkColor;
            float _LineThickness;
            float _Feasible;
            float _Opacity;
            float _PixelSize;
            float _NoiseStrength;
            float _FlickerSpeed;
            float _WaterEffectStrength;
            float _Style;
            float _RevealProgress;
            float _DissolveProgress;
            float _LineLength;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };

            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float Noise(float2 p)
            {
                float2 cell = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(cell), Hash(cell + float2(1,0)), f.x),
                    lerp(Hash(cell + float2(0,1)), Hash(cell + 1.0), f.x), f.y);
            }

            float2 Bud(float2 p, float2 root, float direction, float growth)
            {
                float2 axis = normalize(float2(0.82, direction * 0.57));
                float2 offset = p - root;
                float along = dot(offset, axis), across = dot(offset, float2(-axis.y, axis.x));
                float stemLength = 0.13 * growth;
                float stem = (1.0 - step(0.009, abs(across))) * step(0, along) * step(along, stemLength);
                float leafT = (along - stemLength) / max(0.001, 0.105 * growth);
                float leafWidth = 0.038 * growth * sin(3.14159 * saturate(leafT));
                float leaf = (1.0 - step(leafWidth, abs(across))) * step(0, leafT) * step(leafT, 1);
                return float2(stem, leaf) * step(0.001, growth);
            }

            float2 Flower(float2 p, float2 center, float growth)
            {
                float2 local = (p - center) / max(0.001, 0.045 * growth);
                float radius = length(local), angle = atan2(local.y, local.x);
                float petals = (1.0 - step(0.58 + 0.3 * cos(angle * 5.0), radius)) * step(0.001, growth);
                float centerDot = (1.0 - step(0.24, radius)) * step(0.001, growth);
                return float2(petals, centerDot);
            }

            float CurvedBranch(float2 p, float2 root, float direction,
                float branchLength, float seed, float growth, float irregularity)
            {
                float grownLength = branchLength * saturate(growth);
                if (grownLength < 0.001) return 0.0;
                float minimumDistance = 10.0;
                float minimumWidth = 0.0;
                float2 previous = root;
                float branchWobble = (Noise(float2(root.x * 2.0, seed * 9.0)) - 0.5) * irregularity * 0.09;
                [unroll]
                for (int index = 1; index <= 6; index++)
                {
                    float t = index / 6.0;
                    float curl = saturate((t - 0.76) / 0.24);
                    float x = grownLength * (t - 0.055 * sin(curl * 3.14159));
                    float wobble = branchWobble * sin(3.14159 * t * 2.0);
                    float arch = 0.28 * sin(3.14159 * t) + wobble;
                    float hook = curl * (0.11 + 0.025 * seed);
                    float2 current = root + float2(x, direction * grownLength * (arch + hook));
                    float2 segment = current - previous;
                    float along = saturate(dot(p - previous, segment) / max(0.00001, dot(segment, segment)));
                    float distanceToSegment = length(p - (previous + segment * along));
                    if (distanceToSegment < minimumDistance)
                    {
                        minimumDistance = distanceToSegment;
                        minimumWidth = lerp(0.018, 0.011, t);
                    }
                    previous = current;
                }
                return 1.0 - smoothstep(minimumWidth, minimumWidth + 0.006, minimumDistance);
            }

            Varyings Vert(Attributes input)
            {
                Varyings o; o.positionCS = TransformObjectToHClip(input.positionOS.xyz); o.uv = input.uv; o.color = input.color; return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float pixelSize = max(1.0, _PixelSize);
                float2 pixelCenter = (floor(input.positionCS.xy / pixelSize) + 0.5) * pixelSize;
                float2 uv = input.uv + ddx(input.uv) * (pixelCenter.x - input.positionCS.x) + ddy(input.uv) * (pixelCenter.y - input.positionCS.y);
                float lengthMeters = max(0.01, _LineLength);
                float x = uv.x * lengthMeters;
                float revealDistance = saturate(_RevealProgress) * lengthMeters;
                float halfWidth = max(0.01, _LineThickness * 0.5);
                // The renderer reserves a wider transparent ribbon for botanical details.
                float2 p = float2(x, (uv.y - 0.5) * (_LineThickness * 3.0));
                clip(x);
                clip(lengthMeters - x);
                // Red and blue appear as a whole ribbon. Only green grows along the line.
                if (_Style > 2.5) clip(revealDistance - x - 0.001);

                float dissolveNoise = Noise(floor(float2(x * 35.0, p.y * 70.0)));
                clip(dissolveNoise - (_DissolveProgress * 1.04 - 0.02));
                float speed = max(0.1, _FlickerSpeed);
                float irregularity = saturate(_NoiseStrength);
                float center = (Noise(float2(x * 2.7, 4.2)) - 0.5) * irregularity * 0.035;
                float silhouette = 0, alphaShape = 1;
                float3 color = _LinkColor.rgb;

                if (_Style > 0.5 && _Style < 1.5)
                {
                    // Fire repeats at fixed world-space intervals. Sparks drift outward from the hot stem.
                    float t = _Time.y * speed;
                    float n = Noise(float2(x * 3.2 - t * 0.16, p.y * 17.0 + t * 0.08));
                    float flameRadius = halfWidth * (0.28 + n * (0.4 + irregularity * 0.38));
                    float hotness = abs(p.y - center) / max(0.008, flameRadius);
                    float flame = 1.0 - smoothstep(0.82, 1.08, hotness);
                    float2 sparkGrid = float2(x * 12.0 - t * 0.34, abs(p.y - center) * 23.0 - t * 0.75);
                    float2 sc = floor(sparkGrid), sl = abs(frac(sparkGrid) - 0.5);
                    float seed = Hash(sc);
                    float sparks = step(0.975, seed) * (1.0 - smoothstep(0.13, 0.25, max(sl.x, sl.y))) * step(flameRadius * 0.8, abs(p.y - center));
                    silhouette = max(flame, sparks);
                    color = lerp(float3(1.0,0.92,0.48), float3(1.0,0.35,0.035), smoothstep(0.08,0.68,hotness));
                    color = lerp(color, float3(0.88,0.045,0.012), smoothstep(0.7,1.0,hotness));
                    color = lerp(color, float3(1.0,0.72,0.16), sparks);
                    color += float3(0.55,0.23,0.025) * exp(-hotness * 2.8) * flame;
                    alphaShape = max(flame, sparks * (0.4 + 0.6 * seed));
                }
                else if (_Style > 1.5 && _Style < 2.5)
                {
                    // The slider controls the thin orbiting ribbon and expanding outer ripples.
                    float n = Noise(float2(x * 2.4 + _Time.y * 0.16, 6.8));
                    float coreWidth = 0.02 + n * (0.004 + irregularity * 0.004);
                    float centerDistance = abs(p.y - center);
                    float core = 1.0 - smoothstep(coreWidth * 0.72, coreWidth, centerDistance);
                    float waterStrength = saturate(_WaterEffectStrength);
                    float phase = x * (8.5 + n * 3.0) - _Time.y * speed +
                        (Noise(float2(x * 4.2, 8.0)) - 0.5) * irregularity * 2.6;
                    float orbit = halfWidth * (0.44 + n * (0.16 + irregularity * 0.2)) * waterStrength;
                    float orbitWidth = lerp(0.002, 0.007, waterStrength);
                    float ribbon = 1.0 - smoothstep(orbitWidth * 0.6, orbitWidth,
                        abs(p.y - center - orbit * sin(phase)));
                    float orbitGaps = smoothstep(0.2, 0.62,
                        Noise(float2(x * 6.5 - _Time.y * speed * 0.18, 11.3)));
                    ribbon *= orbitGaps * waterStrength;
                    float radialPhase = centerDistance * (18.0 + irregularity * 13.0) -
                        _Time.y * speed * 1.7 + (n - 0.5) * irregularity * 5.0;
                    float ripple = 1.0 - smoothstep(0.025, 0.075,
                        abs(frac(radialPhase / 6.28318) - 0.5));
                    float rippleRange = smoothstep(0.025, 0.08, centerDistance) *
                        (1.0 - smoothstep(halfWidth * waterStrength,
                            halfWidth * waterStrength + 0.02, centerDistance));
                    ripple *= rippleRange * waterStrength;
                    float highlight = 0.5 + 0.5 * sin(x * 8.0 - _Time.y * speed + n * 2.0);
                    silhouette = max(core, max(ribbon, ripple * 0.75));
                    color = lerp(float3(0.035,0.26,0.72), float3(0.22,0.68,1.0), highlight * 0.42 + n * 0.18);
                    color = lerp(color, float3(0.48,0.9,1.0), saturate(ribbon * 0.9 + ripple * 0.55));
                    alphaShape = max(core * 0.92, max(ribbon * 0.68, ripple * 0.5));
                }
                else if (_Style > 2.5)
                {
                    // Green repeats a small stem/leaf/flower cluster at fixed intervals as the reveal front advances.
                    float stemY = center + sin(x * 5.0 + _Time.y * speed * 0.3) * 0.014;
                    float stem = 1.0 - smoothstep(0.018, 0.034, abs(p.y - stemY));
                    float branchMask = 0.0, leaves = 0.0, petals = 0.0, flowerCenters = 0.0;
                    float branchSpacing = lengthMeters / 5.0;
                    [unroll]
                    for (int branchIndex = 0; branchIndex < 4; branchIndex++)
                    {
                        float seed = Hash(float2((float)branchIndex, 8.2));
                        float rootX = branchSpacing * (branchIndex + 1);
                        float rootY = center + sin(rootX * 5.0 + _Time.y * speed * 0.3) * 0.014;
                        float direction = (seed > 0.5) ? 1.0 : -1.0;
                        float branchLength = min(0.38, max(0.14, lengthMeters * 0.16)) * lerp(0.84, 1.08, seed);
                        // A branch occupies only this small x interval. Do not
                        // run all 24 curve segments and four flowers for every
                        // pixel along a long rotating ribbon (pixelation is a
                        // visual snap, not a reduction in fragment invocations).
                        [branch]
                        if (p.x < rootX - 0.025 || p.x > rootX + max(branchLength + 0.04, branchLength * 0.42 + 0.24)) continue;
                        float branchGrowth = saturate((revealDistance - rootX) / max(0.08, branchLength * 0.75));
                        float2 root = float2(rootX, rootY);
                        branchMask = max(branchMask, CurvedBranch(p, root, direction,
                            branchLength, seed, branchGrowth, irregularity));
                        float2 leaf = Bud(p, root + float2(branchLength * 0.42,
                            direction * branchLength * 0.11), direction, branchGrowth);
                        leaves = max(leaves, leaf.y);
                        float flowerGrowth = saturate((revealDistance - rootX - branchLength * 0.14) /
                            max(0.08, branchLength * 0.65));
                        float2 flower = Flower(p, root + float2(branchLength * 0.78,
                            direction * branchLength * 0.14), flowerGrowth);
                        petals = max(petals, flower.x);
                        flowerCenters = max(flowerCenters, flower.y);
                    }
                    silhouette = max(stem, max(branchMask, max(leaves, max(petals, flowerCenters))));
                    float grain = Noise(float2(x * 5.0, p.y * 23.0));
                    color = lerp(float3(0.07,0.31,0.035), float3(0.17,0.58,0.055), grain);
                    color = lerp(color, float3(0.56,0.92,0.2), leaves * 0.9);
                    color = lerp(color, float3(1.0,0.66,0.78), petals * 0.95);
                    color = lerp(color, float3(1.0,0.82,0.18), flowerCenters);
                    alphaShape = max(stem, max(branchMask, max(leaves, max(petals, flowerCenters))));
                }
                else
                {
                    float radius = halfWidth * (0.8 + (Noise(float2(x * 3.0, 6.0)) - 0.5) * irregularity * 0.45);
                    float trunk = 1.0 - smoothstep(radius * 0.8, radius, abs(p.y - center));
                    silhouette = trunk;
                    color *= lerp(0.72, 1.04, Noise(float2(x * 6.0, p.y * 22.0)));
                }

                clip(silhouette - 0.22);
                float tip = (_Style > 2.5) ? 1.0 - smoothstep(0.0, 0.12, revealDistance - x) : 0.0;
                color = lerp(color, float3(1.0,0.96,0.72), tip * 0.18);
                float endFade = smoothstep(0.0, 0.07, x) * smoothstep(0.0, 0.09, lengthMeters - x);
                float feasibleStrength = lerp(0.4, 1.0, saturate(_Feasible));
                float appearance = (_Style > 0.5 && _Style < 2.5) ? saturate(_RevealProgress) : 1.0;
                float alpha = saturate(_Opacity * _LinkColor.a * input.color.a * feasibleStrength * alphaShape * endFade * appearance * (1.0 - _DissolveProgress * 0.38));
                return half4(color * input.color.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
