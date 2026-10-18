#ifndef PROJECT_PLANT_BURN_SAMPLING_INCLUDED
#define PROJECT_PLANT_BURN_SAMPLING_INCLUDED
// TA front/ash equations shared by artwork and its HSV silhouette; source reveal stays authoritative.
float _BurnEnabled, _BurnElapsed, _BurnDuration, _BurnTopDown, _BurnPixelWorldSize;
float4 _BurnWorldBounds, _FlameCoreColor, _FlameOuterColor, _AshColor;
float _FlameEdgeWidth, _FlameEdgeIntensity, _EdgeNoise, _AshFadeSeconds;
float PlantBurnHash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
float PlantBurnNoise(float2 p)
{
    float2 c = floor(p), f = frac(p); f = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(PlantBurnHash(c), PlantBurnHash(c + float2(1,0)), f.x),
        lerp(PlantBurnHash(c + float2(0,1)), PlantBurnHash(c + 1), f.x), f.y);
}
float PlantBurnContour(TEXTURE2D_PARAM(sourceTexture, sourceSampler), float2 uv,
    float4 frameUV, float2 textureSize, float sourceAlpha, float tintAlpha)
{
    // This path is entirely skipped for ordinary (non-burning) sprites.
    float contour = 0;
    [branch] if (_BurnEnabled >= .5)
    {
        float2 texelSize = 1.0 / max(textureSize, 1.0);
        float2 low = frameUV.xy + texelSize * .5;
        float2 high = max(low, frameUV.zw - texelSize * .5);
        float alphaLeft = SAMPLE_TEXTURE2D(sourceTexture, sourceSampler, clamp(uv - float2(texelSize.x,0),low,high)).a;
        float alphaRight = SAMPLE_TEXTURE2D(sourceTexture, sourceSampler, clamp(uv + float2(texelSize.x,0),low,high)).a;
        float alphaDown = SAMPLE_TEXTURE2D(sourceTexture, sourceSampler, clamp(uv - float2(0,texelSize.y),low,high)).a;
        float alphaUp = SAMPLE_TEXTURE2D(sourceTexture, sourceSampler, clamp(uv + float2(0,texelSize.y),low,high)).a;
        contour = saturate(sourceAlpha - min(min(alphaLeft,alphaRight),min(alphaDown,alphaUp)) * tintAlpha);
    }
    return contour;
}
half4 ApplyPlantBurn(half4 source, float2 worldXY, float contour)
{
    if (_BurnEnabled < .5) return source;
    float pixelSize = max(.00001, _BurnPixelWorldSize);
    float2 worldSample = (floor(worldXY / pixelSize) + .5) * pixelSize;
    float height = saturate((worldSample.y - _BurnWorldBounds.x) / max(.0001, _BurnWorldBounds.y - _BurnWorldBounds.x));
    height = lerp(height, 1.0 - height, saturate(_BurnTopDown));
    float elapsed = max(0, _BurnElapsed), duration = max(.05, _BurnDuration);
    float noise = PlantBurnNoise(float2(worldSample.x / pixelSize * .23, height * 27.0 + elapsed * 2.4));
    float distance = saturate(elapsed / duration) + (noise - .5) * _EdgeNoise * .095 - height;
    float width = max(.005, _FlameEdgeWidth);
    float band = (1.0 - smoothstep(width * .18, width, abs(distance))) *
        (1.0 - smoothstep(0, max(.05, _AshFadeSeconds), max(0, elapsed - duration)));
    float burned = smoothstep(-width * .42, width * .42, distance);
    float ashAlpha = 1.0 - smoothstep(0, max(.05, _AshFadeSeconds), max(0, elapsed - height * duration));
    float ember = saturate((noise - .67) * 3.2) * burned * ashAlpha;
    float3 ash = lerp(source.rgb, _AshColor.rgb, .88) + _FlameOuterColor.rgb * ember * .35;
    float3 body = lerp(source.rgb, ash, burned);
    float core = 1.0 - smoothstep(.12, .7, abs(distance) / width);
    body = lerp(body, lerp(_FlameOuterColor.rgb, _FlameCoreColor.rgb, core), saturate(band * _FlameEdgeIntensity));
    body += _FlameOuterColor.rgb * band * max(0, _FlameEdgeIntensity - 1.0) * .35;
    float rim = contour * (1.0 - burned * .45) * _FlameEdgeIntensity * .38;
    body = lerp(body, _FlameOuterColor.rgb, saturate(rim));
    float alpha = source.a * max(lerp(1.0, ashAlpha, burned), band * saturate(_FlameEdgeIntensity));
    alpha = max(alpha, source.a * rim);
    return half4(body, alpha);
}
#endif
