Shader "TA/Star Panorama Skybox"
{
    Properties
    {
        _TimeScale ("时间 / 总倍率", Range(0, 10)) = 1

        _StarDensity ("星点 / 密度", Range(20, 300)) = 120
        _StarBrightness ("星点 / 亮度", Range(0, 8)) = 2.4
        _StarRotationSpeed ("星点 / 旋转速度", Range(-0.5, 0.5)) = 0.035
        _StarSize ("星点 / 尺寸", Range(0.005, 0.2)) = 0.075
        _StarSizeRandomness ("星点 / 尺寸随机度", Range(0, 1)) = 0.65
        _StarDistanceFeel ("星点 / 距离感", Range(0.35, 5)) = 1.45
        [HDR] _StarColor1 ("星点 / 颜色 1", Color) = (0.35, 0.6, 1, 1)
        [HDR] _StarColor2 ("星点 / 颜色 2", Color) = (0.75, 0.38, 1, 1)
        [HDR] _StarColor3 ("星点 / 颜色 3", Color) = (1, 0.28, 0.22, 1)
        [HDR] _StarColor4 ("星点 / 颜色 4", Color) = (1, 0.84, 0.28, 1)

        _StarGlowStrength ("光晕 / 强度", Range(0, 3)) = 0.7
        _StarGlowSize ("光晕 / 范围", Range(0.5, 5)) = 2.4

        _StarCrossStrength ("十字光芒 / 强度", Range(0, 5)) = 1.8
        _StarCrossLength ("十字光芒 / 长度", Range(0, 0.5)) = 0.24
        _StarCrossThickness ("十字光芒 / 粗细", Range(0.002, 0.05)) = 0.014
        _StarCrossRandomness ("十字光芒 / 出现比例", Range(0, 1)) = 0.65

        _StarTwinkleSpeed ("闪烁 / 速度", Range(0, 5)) = 0.75
        _StarTwinkleRandomness ("闪烁 / 随机度", Range(0, 1)) = 0.75

        _NebulaStrength ("星云 / 强度", Range(0, 2)) = 0.45
        _NebulaRotationSpeed ("星云 / 旋转速度", Range(-0.05, 0.05)) = 0.005
        _NebulaFlowSpeed ("星云 / 流动速度", Range(0, 0.2)) = 0.028
        _GalaxyWidth ("银河 / 宽度", Range(0.01, 0.8)) = 0.20
        _GalaxyTilt ("银河 / 倾斜", Range(-2, 2)) = 0.35
        _GalaxyIrregularity ("银河 / 不规则度", Range(0, 1)) = 0.45
        _GalaxyBreakup ("银河 / 断裂程度", Range(0, 1)) = 0.35
        _GalaxyPatchLength ("银河 / 长度", Range(0.1, 1.5)) = 0.75
        _GalaxyPatchSoftness ("银河 / 边缘柔和度", Range(0.001, 1.5)) = 0.35
        _GalaxyPatchCenter ("银河 / 中心偏移", Range(-1, 1)) = 0
        _GalaxyLeafCurve ("银河 / 弯曲", Range(-2, 2)) = 0.35

        [HDR] _SpaceTop ("空间与星云颜色 / 空间顶部", Color) = (0.025, 0.018, 0.085, 1)
        [HDR] _SpaceBottom ("空间与星云颜色 / 空间底部", Color) = (0.006, 0.006, 0.022, 1)
        [HDR] _NebulaColorA ("空间与星云颜色 / 星云 A", Color) = (0.42, 0.16, 0.82, 1)
        [HDR] _NebulaColorB ("空间与星云颜色 / 星云 B", Color) = (0.10, 0.36, 0.95, 1)
        [HDR] _NebulaColorC ("空间与星云颜色 / 星云 C", Color) = (0.95, 0.30, 0.52, 1)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Background"
            "RenderType" = "Background"
            "PreviewType" = "Skybox"
        }

        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float _StarPanoramaTime;

            CBUFFER_START(UnityPerMaterial)
                float _TimeScale;
                float _StarDensity;
                float _StarBrightness;
                float _StarRotationSpeed;
                float _StarSize;
                float _StarSizeRandomness;
                float _StarDistanceFeel;
                float4 _StarColor1;
                float4 _StarColor2;
                float4 _StarColor3;
                float4 _StarColor4;
                float _StarGlowStrength;
                float _StarGlowSize;
                float _StarCrossStrength;
                float _StarCrossLength;
                float _StarCrossThickness;
                float _StarCrossRandomness;
                float _StarTwinkleSpeed;
                float _StarTwinkleRandomness;
                float _NebulaStrength;
                float _NebulaRotationSpeed;
                float _NebulaFlowSpeed;
                float _GalaxyWidth;
                float _GalaxyTilt;
                float _GalaxyIrregularity;
                float _GalaxyBreakup;
                float _GalaxyPatchLength;
                float _GalaxyPatchSoftness;
                float _GalaxyPatchCenter;
                float _GalaxyLeafCurve;
                float4 _SpaceTop;
                float4 _SpaceBottom;
                float4 _NebulaColorA;
                float4 _NebulaColorB;
                float4 _NebulaColorC;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 direction : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.direction = input.positionOS.xyz;
                return output;
            }

            float SmoothStepSafe(float edge0, float edge1, float value)
            {
                float width = max(edge1 - edge0, 0.00001);
                float t = saturate((value - edge0) / width);
                return t * t * (3.0 - 2.0 * t);
            }

            float Hash31(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float3 Hash33(float3 p)
            {
                return float3(
                    Hash31(p + float3(11.1, 0.0, 0.0)),
                    Hash31(p + float3(0.0, 27.7, 0.0)),
                    Hash31(p + float3(0.0, 0.0, 43.3))
                );
            }

            float Noise3(float3 p)
            {
                float3 cell = floor(p);
                float3 local = frac(p);
                local = local * local * (3.0 - 2.0 * local);

                float n000 = Hash31(cell + float3(0.0, 0.0, 0.0));
                float n100 = Hash31(cell + float3(1.0, 0.0, 0.0));
                float n010 = Hash31(cell + float3(0.0, 1.0, 0.0));
                float n110 = Hash31(cell + float3(1.0, 1.0, 0.0));
                float n001 = Hash31(cell + float3(0.0, 0.0, 1.0));
                float n101 = Hash31(cell + float3(1.0, 0.0, 1.0));
                float n011 = Hash31(cell + float3(0.0, 1.0, 1.0));
                float n111 = Hash31(cell + float3(1.0, 1.0, 1.0));

                float nx00 = lerp(n000, n100, local.x);
                float nx10 = lerp(n010, n110, local.x);
                float nx01 = lerp(n001, n101, local.x);
                float nx11 = lerp(n011, n111, local.x);
                return lerp(lerp(nx00, nx10, local.y), lerp(nx01, nx11, local.y), local.z);
            }

            float Fbm3(float3 p)
            {
                float value = 0.0;
                float amplitude = 0.5;
                [unroll]
                for (int i = 0; i < 5; i++)
                {
                    value += Noise3(p) * amplitude;
                    p *= 2.07;
                    amplitude *= 0.5;
                }
                return value;
            }

            float3 RotateY(float3 p, float angle)
            {
                float s = sin(angle);
                float c = cos(angle);
                return float3(p.x * c - p.z * s, p.y, p.x * s + p.z * c);
            }

            float3 PickStarColor(float value)
            {
                if (value < 0.25) return _StarColor1.rgb;
                if (value < 0.50) return _StarColor2.rgb;
                if (value < 0.75) return _StarColor3.rgb;
                return _StarColor4.rgb;
            }

            float StarTwinkle(float3 cell, float shaderTime)
            {
                float phase = Hash31(cell + 14.8) * 6.28318;
                float speed = lerp(0.45, 2.4, Hash31(cell + 92.3));
                float pulse = sin(shaderTime * _StarTwinkleSpeed * speed + phase) * 0.5 + 0.5;

                float gateTime = floor(shaderTime * _StarTwinkleSpeed * 0.75);
                float randomGate = Hash31(cell + gateTime);
                float appear = SmoothStepSafe(1.0 - _StarTwinkleRandomness * 0.45, 1.0, randomGate);
                return pow(pulse, 3.0) * appear;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float shaderTime = _StarPanoramaTime * _TimeScale;
                float3 direction = normalize(input.direction);

                float vertical = saturate(direction.y * 0.5 + 0.5);
                float3 color = lerp(_SpaceBottom.rgb, _SpaceTop.rgb, vertical);

                float3 nebulaDirection = RotateY(direction, shaderTime * _NebulaRotationSpeed);
                float3 starDirection = RotateY(direction, shaderTime * _StarRotationSpeed);

                float flowTime = shaderTime * _NebulaFlowSpeed;
                float3 flowOffset = float3(flowTime, flowTime * 0.21, -flowTime * 0.14);
                float largeCloud = Fbm3(nebulaDirection * 1.35 + float3(3.0, 7.0, 2.0) + flowOffset);
                float mediumCloud = Fbm3(nebulaDirection * 4.8 + largeCloud * 1.7 + flowOffset * 1.8);
                float fineCloud = Fbm3(nebulaDirection * 12.0 + mediumCloud * 2.0 + flowOffset * 3.2);

                float alongAxis = nebulaDirection.x + _GalaxyPatchCenter;
                float curvedY = nebulaDirection.y + nebulaDirection.x * _GalaxyTilt;
                curvedY += sin(nebulaDirection.x * 3.14159) * _GalaxyLeafCurve * 0.18;
                curvedY += (largeCloud - 0.5) * _GalaxyIrregularity;

                float widthTaper = 1.0 - SmoothStepSafe(_GalaxyPatchLength * 0.25, _GalaxyPatchLength, abs(alongAxis));
                float leafWidth = _GalaxyWidth * lerp(0.25, 1.15, widthTaper);
                float widthRatio = curvedY / max(leafWidth, 0.01);
                float bandMask = exp(-(widthRatio * widthRatio));

                float lengthMask = 1.0 - SmoothStepSafe(
                    max(0.0, _GalaxyPatchLength - _GalaxyPatchSoftness),
                    _GalaxyPatchLength,
                    abs(alongAxis)
                );
                float galaxyMask = bandMask * lengthMask;

                float breakup = SmoothStepSafe(_GalaxyBreakup, 1.0, mediumCloud);
                galaxyMask *= lerp(1.0, breakup, _GalaxyBreakup);

                float nebula = galaxyMask * SmoothStepSafe(0.30, 0.88, mediumCloud);
                nebula += galaxyMask * fineCloud * 0.22;
                nebula *= _NebulaStrength;

                float3 nebulaColor = lerp(_NebulaColorA.rgb, _NebulaColorB.rgb, mediumCloud);
                nebulaColor = lerp(nebulaColor, _NebulaColorC.rgb, fineCloud * 0.45);
                color += nebulaColor * nebula;

                float3 p = starDirection * _StarDensity;
                float3 cell = floor(p);
                float3 local = frac(p) - 0.5;
                float randomValue = Hash31(cell);
                float visible = step(0.972, randomValue);

                float3 offset = Hash33(cell) - 0.5;
                local -= offset * 0.42;

                float distanceScale = max(0.35, _StarDistanceFeel);
                float randomSize = lerp(
                    1.0 - _StarSizeRandomness,
                    1.0 + _StarSizeRandomness,
                    Hash31(cell + 8.13)
                );
                float actualStarSize = _StarSize * randomSize / distanceScale;
                float distanceToStar = length(local);
                float core = (1.0 - SmoothStepSafe(0.0, actualStarSize, distanceToStar)) * visible;

                float softGlowSize = actualStarSize * _StarGlowSize;
                float softGlow = (1.0 - SmoothStepSafe(0.0, softGlowSize, distanceToStar)) * visible;

                float crossRandom = Hash31(cell + 61.7);
                float crossGate = step(1.0 - _StarCrossRandomness, crossRandom);
                float crossLength = _StarCrossLength * lerp(0.55, 1.45, Hash31(cell + 72.4));
                float crossThickness = _StarCrossThickness * lerp(0.75, 1.35, Hash31(cell + 83.2));
                float horizontal = exp(-abs(local.y) / max(crossThickness, 0.0001))
                    * (1.0 - SmoothStepSafe(0.0, crossLength, abs(local.x)));
                float verticalCross = exp(-abs(local.x) / max(crossThickness, 0.0001))
                    * (1.0 - SmoothStepSafe(0.0, crossLength, abs(local.y)));
                float cross = (horizontal + verticalCross) * visible * crossGate;

                float3 starColor = PickStarColor(Hash31(cell + 21.7));
                float baseRandom = lerp(0.55, 1.25, Hash31(cell + 3.4));
                float twinkle = StarTwinkle(cell, shaderTime);

                color += starColor * core * _StarBrightness * baseRandom;
                color += starColor * softGlow * _StarGlowStrength * twinkle * 0.35;
                color += starColor * cross * _StarCrossStrength * twinkle;

                float3 dustP = starDirection * _StarDensity * 2.6;
                float3 dustCell = floor(dustP);
                float3 dustLocal = frac(dustP) - 0.5;
                float dustVisible = step(0.991, Hash31(dustCell + 91.2));
                float dustSize = 0.018 / distanceScale;
                float dustCore = 1.0 - SmoothStepSafe(0.0, dustSize, length(dustLocal));
                color += float3(0.68, 0.76, 1.0) * dustCore * dustVisible * galaxyMask * 0.45;

                return float4(color, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
