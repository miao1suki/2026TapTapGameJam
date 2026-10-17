Shader "Hidden/2026TapTap/ColorObjectMask"
{
    Properties
    {
        _ColorMaskAlphaTexture("Alpha Texture", 2D) = "white" {}
        _ColorMaskUseVertexAlpha("Vertex Alpha", Float) = 0
        _ColorMaskOpacity("Source Opacity", Float) = 1
        _ColorMaskGroup("Color Group", Float) = -1
        _ColorMaskReal("Real Appearance", Float) = 0
        [PerRendererData] _ArtworkPixelWorldSize("Artwork World Pixel", Float) = 0
        [PerRendererData] _ArtworkPixelUVRect("Artwork Frame UV", Vector) = (0,0,1,1)
        [PerRendererData] _ArtworkPixelTextureSize("Artwork Texture Size", Vector) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/_Project/Code/Systems/ObjectArtwork/Runtime/GridArtworkSampling.hlsl"
            TEXTURE2D(_ColorMaskAlphaTexture); SAMPLER(sampler_ColorMaskAlphaTexture);
            float _ColorMaskUseVertexAlpha;
            float _ColorMaskOpacity;
            float _ColorMaskGroup, _ColorMaskReal;
            float _ArtworkPixelWorldSize;
            float4 _ArtworkPixelUVRect;
            float4 _ArtworkPixelTextureSize;
            float4 _ColorAppearanceBaseAlpha, _ColorAppearanceRealAlpha;
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float alpha : TEXCOORD1; float2 worldXY : TEXCOORD2; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.worldXY = TransformObjectToWorld(input.positionOS.xyz).xy;
                output.alpha = lerp(1, input.color.a, saturate(_ColorMaskUseVertexAlpha));
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = GridArtworkSampleUV(input.uv, input.worldXY, _ArtworkPixelWorldSize, _ArtworkPixelUVRect, _ArtworkPixelTextureSize.xy);
                half alpha = SAMPLE_TEXTURE2D(_ColorMaskAlphaTexture, sampler_ColorMaskAlphaTexture, uv).a * input.alpha * _ColorMaskOpacity;
                clip(alpha - .01);
                // The source image is already alpha blended; applying alpha again would leave raw color visible.
                int group = (int)_ColorMaskGroup;
                half reveal = group >= 0 && group < 3
                    ? (_ColorMaskReal > .5 ? _ColorAppearanceRealAlpha[group] : _ColorAppearanceBaseAlpha[group]) : 1;
                clip(reveal - .001);
                return half4(1, 1, 1, 1);
            }
            ENDHLSL
        }
    }
}
