#ifndef SCREEN_PIXEL_OUTLINE_INCLUDED
#define SCREEN_PIXEL_OUTLINE_INCLUDED

TEXTURE2D_X(_LearningPixelSelectionMask);
TEXTURE2D_X(_LearningPixelOriginalColor);

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

// 标准 3x3 Sobel 卷积核。中心点权重为 0，边缘由周围八个像素的梯度决定。
static const float2 kSobelOffsets[9] =
{
    float2(-1.0,  1.0), float2(0.0,  1.0), float2(1.0,  1.0),
    float2(-1.0,  0.0), float2(0.0,  0.0), float2(1.0,  0.0),
    float2(-1.0, -1.0), float2(0.0, -1.0), float2(1.0, -1.0)
};

static const float kSobelX[9] =
{
    -1.0, 0.0, 1.0,
    -2.0, 0.0, 2.0,
    -1.0, 0.0, 1.0
};

static const float kSobelY[9] =
{
     1.0,  2.0,  1.0,
     0.0,  0.0,  0.0,
    -1.0, -2.0, -1.0
};

// 透视和正交相机的深度线性化方式不同，这里统一转换为眼空间距离。
float GetLinearEyeDepthForOutline(float rawDepth)
{
    return unity_OrthoParams.w > 0.5
        ? LinearDepthToEyeDepth(rawDepth)
        : LinearEyeDepth(rawDepth, _ZBufferParams);
}

// 对线性深度取对数，使近处和远处相同比例的深度变化拥有更接近的响应。
float SampleDepthSignal(float2 uv)
{
    float rawDepth = SampleSceneDepth(uv);
    float eyeDepth = max(GetLinearEyeDepthForOutline(rawDepth), _ProjectionParams.y);
    return log2(eyeDepth);
}

// 使用 URP 的接口读取并解码世界空间法线，兼容八面体编码法线格式。
float3 SampleNormalSignal(float2 uv)
{
    float3 normalWS = SampleSceneNormals(uv);
    float lengthSquared = dot(normalWS, normalWS);
    return normalWS * rsqrt(max(lengthSquared, 1e-6));
}

// 颜色 Sobel 使用当前 Pass 的输入颜色。
// 第 0 个 Pass 的输入是原始相机颜色，因此颜色边缘不会受到像素块边界影响。
float3 SampleColorSignal(float2 uv)
{
    return SAMPLE_TEXTURE2D_X_LOD(
        _BlitTexture,
        sampler_PointClamp,
        saturate(uv),
        0.0).rgb;
}

float GetLuminance(float3 color)
{
    return dot(max(color, 0.0), float3(0.2126, 0.7152, 0.0722));
}

float GetPixelBlockSize()
{
    return max(floor(_PixelSize + 0.5), 1.0);
}

int GetPixelBlockSizeInt()
{
    return clamp((int)floor(_PixelSize + 0.5), 1, 64);
}

float2 GetSourceSizeForPixelGrid()
{
    // 由 RenderFeature 通过 MaterialPropertyBlock 传入，避免把相机尺寸误当成网格尺寸。
    return max(_ScreenPixelSourceSize.xy, float2(1.0, 1.0));
}

int2 GetSourceSizeForPixelGridInt()
{
    return max(
        (int2)round(_ScreenPixelSourceSize.xy),
        int2(1, 1));
}

int2 GetPixelGridSizeInt(int2 sourceSize, int blockSize)
{
    return max(
        (sourceSize + blockSize - 1) / blockSize,
        int2(1, 1));
}

// 根据屏幕 UV 找到它所属的像素块。
int2 GetPixelBlockIndex(
    float2 uv,
    int2 sourceSize,
    int2 gridSize,
    int blockSize)
{
    float2 blockPosition =
        saturate(uv) * float2(sourceSize) / (float)blockSize;
    return min((int2)floor(blockPosition), gridSize - 1);
}

// 返回某个像素块实际覆盖范围的中心 UV。最后一行或最后一列不足一个完整块时也能正确处理。
float2 GetPixelBlockCenterUV(
    int2 blockIndex,
    int2 sourceSize,
    int blockSize)
{
    int2 blockStart = blockIndex * blockSize;
    int2 blockExtent = min(
        int2(blockSize, blockSize),
        sourceSize - blockStart);
    return (
        float2(blockStart) + 0.5 * float2(blockExtent)) /
        float2(sourceSize);
}

// 保留原像素化算法的平滑像素边界。
float4 SamplePixelatedColor(float2 uv)
{
    float blockSize = GetPixelBlockSize();
    float2 sourceSize = GetSourceSizeForPixelGrid();
    float2 sourceTexel = max(
        1.0 / sourceSize,
        1.0 / max(_ScreenParams.xy, float2(1.0, 1.0)));
    float2 blockTexelSize = sourceTexel * blockSize;

    float2 boxSize = clamp(fwidth(uv) / blockTexelSize, 1e-5, 1.0);
    float2 blockPosition = uv / blockTexelSize - 0.5 * boxSize;
    float2 blockOffset = smoothstep(1.0 - boxSize, 1.0, frac(blockPosition));
    float2 pixelUV = (floor(blockPosition) + 0.5 + blockOffset) * blockTexelSize;

    return SAMPLE_TEXTURE2D_X_LOD(
        _BlitTexture,
        sampler_LinearClamp,
        saturate(pixelUV),
        0.0);
}

// 分别计算深度、法线、亮度和 RGB 颜色的 3x3 Sobel 梯度。
float4 CalculateSobelEdges(float2 uv)
{
    float2 screenTexel = max(
        _ScreenPixelSourceSize.zw,
        1.0 / max(_ScreenParams.xy, float2(1.0, 1.0)));
    float2 sampleStep = screenTexel * max(_OutlineThickness, 0.5);

    float depthX = 0.0;
    float depthY = 0.0;
    float3 normalX = 0.0;
    float3 normalY = 0.0;
    float luminanceX = 0.0;
    float luminanceY = 0.0;
    float3 colorX = 0.0;
    float3 colorY = 0.0;

    [unroll]
    for (int i = 0; i < 9; i++)
    {
        float2 sampleUV = uv + kSobelOffsets[i] * sampleStep;
        float depth = SampleDepthSignal(sampleUV);
        float3 normal = SampleNormalSignal(sampleUV);
        float3 color = SampleColorSignal(sampleUV);
        float luminance = GetLuminance(color);

        depthX += depth * kSobelX[i];
        depthY += depth * kSobelY[i];
        normalX += normal * kSobelX[i];
        normalY += normal * kSobelY[i];
        luminanceX += luminance * kSobelX[i];
        luminanceY += luminance * kSobelY[i];
        colorX += color * kSobelX[i];
        colorY += color * kSobelY[i];
    }

    // Sobel 正负权重各自的总和为 4，乘 0.25 将响应归一到更易调节的范围。
    const float normalization = 0.25;
    float depthEdge = length(float2(depthX, depthY)) * normalization;
    float normalEdge = sqrt(dot(normalX, normalX) + dot(normalY, normalY)) * normalization;
    float luminanceEdge = length(float2(luminanceX, luminanceY)) * normalization;
    float colorEdge = sqrt(dot(colorX, colorX) + dot(colorY, colorY)) * normalization;

    return float4(depthEdge, normalEdge, luminanceEdge, colorEdge);
}

float GetOutlineEdgeResponse(float4 sobelEdges)
{
    // 使用 max 合并信号，避免多个低强度噪声相加后被误判为真实边缘。
    float geometryEdge = max(
        sobelEdges.x * _DepthWeight,
        sobelEdges.y * _NormalWeight);
    float luminanceEdge =
        sobelEdges.z * _LuminanceWeight *
        step(0.5, _UseLuminanceEdge);
    float appearanceEdge = max(
        luminanceEdge,
        sobelEdges.w * _ColorWeight);
    return max(geometryEdge, appearanceEdge);
}

// 先得到软响应，再转成二值掩码。
// 这样后面的像素块归约只会产生“整块描边”或“整块不描边”，不会出现渐变黑边。
float GetBinaryOutlineMask(float2 uv)
{
    float edgeResponse =
        GetOutlineEdgeResponse(CalculateSobelEdges(uv));
    float edgeSoftness = max(_EdgeSoftness, 1e-5);
    float softMask = smoothstep(
        _EdgeThreshold,
        _EdgeThreshold + edgeSoftness,
        edgeResponse);
    return step(0.5, softMask);
}

// 第 0 个 Pass：得到像素化后的场景颜色，并把原始分辨率边缘保存到 Alpha。
float4 ScreenPixelAndDetectFragment(float2 uv)
{
    float4 pixelatedColor = SamplePixelatedColor(uv);
    float edgeMask = 0;
    [branch] if (_OutlineEnabled > .5) edgeMask = GetBinaryOutlineMask(uv);
    return float4(pixelatedColor.rgb, edgeMask);
}

float4 SamplePackedPixel(int2 pixel, int2 sourceSize)
{
    float2 uv = (float2(pixel) + 0.5) / float2(sourceSize);
    return SAMPLE_TEXTURE2D_X_LOD(
        _BlitTexture,
        sampler_PointClamp,
        uv,
        0.0);
}

// 第 1 个 Pass：把每个像素块内的边缘 Alpha 做最大值归约。
// 一个块内只要有一个准确边缘像素，整个块就会成为描边块。
float4 ScreenReduceOutlineFragment(float2 uv)
{
    int2 sourceSize = GetSourceSizeForPixelGridInt();
    int blockSize = GetPixelBlockSizeInt();
    int2 gridSize = GetPixelGridSizeInt(sourceSize, blockSize);
    int2 blockIndex = GetPixelBlockIndex(
        uv,
        sourceSize,
        gridSize,
        blockSize);
    int2 blockStart = blockIndex * blockSize;

    float blockMask = 0.0;
    float selected = 0;
    float nearestDistance = 1e20;
    float3 nearestColor = 0;
    float2 centerPixel = float2(blockStart) + float2(blockSize, blockSize) * .5;

    // 动态循环最多扫描 64x64 个源像素；总扫描量约等于一帧源图像的像素数。
    // 这是为了保证细线不会因为只覆盖块内几个位置而丢失。
    [loop]
    for (int y = 0; y < 64; y++)
    {
        if (y >= blockSize || blockStart.y + y >= sourceSize.y)
            break;

        [loop]
        for (int x = 0; x < 64; x++)
        {
            if (x >= blockSize || blockStart.x + x >= sourceSize.x)
                break;

            int2 pixel = blockStart + int2(x,y);
            float2 sampleUV = (float2(pixel) + .5) / float2(sourceSize);
            float visible = SAMPLE_TEXTURE2D_X_LOD(_LearningPixelSelectionMask, sampler_PointClamp, sampleUV, 0).r;
            if (visible < .5) continue;
            selected = 1;
            if (_OutlineEnabled > .5) blockMask = max(blockMask, SamplePackedPixel(pixel,sourceSize).a);
            float distanceSquared = dot(float2(pixel) + .5 - centerPixel, float2(pixel) + .5 - centerPixel);
            if (distanceSquared < nearestDistance)
            {
                nearestDistance = distanceSquared;
                nearestColor = SAMPLE_TEXTURE2D_X_LOD(_LearningPixelOriginalColor, sampler_PointClamp, sampleUV, 0).rgb;
            }
        }


    }

    // RGB 取该块中心的第一次像素化结果，保证颜色也与块网格一致。
    float2 blockCenterUV = GetPixelBlockCenterUV(
        blockIndex,
        sourceSize,
        blockSize);
    float3 blockColor = SAMPLE_TEXTURE2D_X_LOD(
        _BlitTexture,
        sampler_PointClamp,
        blockCenterUV,
        0.0).rgb;

    float centerVisible = SAMPLE_TEXTURE2D_X_LOD(_LearningPixelSelectionMask,sampler_PointClamp,blockCenterUV,0).r;
    if (centerVisible < .5 && selected > .5) blockColor = nearestColor;
    return float4(blockColor, selected + 2 * blockMask);
}

// 第 2 个 Pass：把低分辨率像素块网格点采样放大回屏幕，并叠加统一描边颜色。
float4 ScreenCompositeFragment(float2 uv)
{
    int2 sourceSize = GetSourceSizeForPixelGridInt();
    int blockSize = GetPixelBlockSizeInt();
    int2 gridSize = GetPixelGridSizeInt(sourceSize, blockSize);
    int2 blockIndex = GetPixelBlockIndex(
        uv,
        sourceSize,
        gridSize,
        blockSize);
    float2 gridUV =
        (float2(blockIndex) + 0.5) / float2(gridSize);

    float4 gridSample = SAMPLE_TEXTURE2D_X_LOD(
        _BlitTexture,
        sampler_PointClamp,
        gridUV,
        0.0);

    // Alpha 已经是二值块掩码；同一个块内的每个屏幕像素得到完全相同的结果。
    float outlineAlpha =
        step(2.5, gridSample.a) * saturate(_OutlineColor.a) * step(.5,_OutlineEnabled);
    float3 finalColor = lerp(
        gridSample.rgb,
        _OutlineColor.rgb,
        outlineAlpha);

    // 当前效果针对不透明相机颜色；最终 Alpha 固定为 1，避免把边缘掩码泄漏到相机 Alpha。
    float4 original = SAMPLE_TEXTURE2D_X_LOD(_LearningPixelOriginalColor,sampler_PointClamp,uv,0);
    float visible = SAMPLE_TEXTURE2D_X_LOD(_LearningPixelSelectionMask,sampler_PointClamp,uv,0).r;
    return float4(lerp(original.rgb,finalColor,step(.5,visible) * step(.5,gridSample.a)),original.a);
}

#endif
