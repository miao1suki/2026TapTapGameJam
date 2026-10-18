#ifndef PROJECT_FLOW_MAGMA_SAMPLING_INCLUDED
#define PROJECT_FLOW_MAGMA_SAMPLING_INCLUDED
float _FlowMagmaBody, _FlowMagmaSplash, _FlowCellWorldSize;
float _EffectTime, _FlowSpeed, _WaveAmplitude, _WaveFrequency;
float2 FlowMagmaPoint(float2 worldPosition)
{
    float pixel = max(.0001, _FlowCellWorldSize / 16.0);
    return (floor(worldPosition / pixel) + .5) * pixel;
}
void ClipFlowMagmaSurface(float2 worldXY, float2 surface)
{
    if (surface.y < .5) return;
    float2 p = FlowMagmaPoint(worldXY);
    float wave = sin(p.x / max(.001, _FlowCellWorldSize) * _WaveFrequency * 6.2831853 + _EffectTime * 1.7);
    clip(surface.x + wave * _WaveAmplitude * _FlowCellWorldSize - p.y);
}
float FlowMagmaSplashAlpha(float2 uv, float2 worldXY)
{
    // TA's latest particles are solid quads, with integer-world-pixel mesh edges.
    // The HSV mask must not resurrect the obsolete hollow bubble silhouette.
    return 1.0;
}
#endif
