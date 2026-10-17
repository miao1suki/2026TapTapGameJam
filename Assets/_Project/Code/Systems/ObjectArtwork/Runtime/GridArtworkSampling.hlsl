#ifndef PROJECT_GRID_ARTWORK_SAMPLING_INCLUDED
#define PROJECT_GRID_ARTWORK_SAMPLING_INCLUDED

// The same UV calculation is used by sprite color and HSV coverage. It converts
// the interpolated world XY position back into UV at the center of a fixed-size
// artwork pixel. Root stretching and camera zoom cannot change that pixel size.
float2 GridArtworkSampleUV(float2 uv, float2 worldXY, float pixelWorldSize, float4 uvRect, float2 textureSize)
{
    float2 dx = ddx(worldXY);
    float2 dy = ddy(worldXY);
    float2 uvDx = ddx(uv);
    float2 uvDy = ddy(uv);
    float determinant = dx.x * dy.y - dx.y * dy.x;
    if (pixelWorldSize <= 0.000001 || abs(determinant) <= 0.00000001) return uv;
    float2 pixelCenter = (floor(worldXY / pixelWorldSize) + 0.5) * pixelWorldSize;
    float2 offset = pixelCenter - worldXY;
    float x = (offset.x * dy.y - offset.y * dy.x) / determinant;
    float y = (dx.x * offset.y - dx.y * offset.x) / determinant;
    float2 sampleUV = uv + uvDx * x + uvDy * y;
    // Derivative reconstruction can land exactly on a source texel boundary.
    // Stable texel-center sampling prevents float cancellation selecting both
    // neighbours inside one world pixel (visible as thin moving hairlines).
    textureSize = max(textureSize, 1.0);
    sampleUV = (floor(sampleUV * textureSize + 0.0001) + 0.5) / textureSize;
    // Sequence frames share an atlas; never sample into the adjacent frame.
    return clamp(sampleUV, uvRect.xy + 0.5 / textureSize, uvRect.zw - 0.5 / textureSize);
}
#endif
