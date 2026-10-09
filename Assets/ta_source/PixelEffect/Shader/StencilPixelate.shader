Shader "Custom/StencilPixelate"
{
     Properties
    {
        _PixelSize (
            "Pixel Block Size",
            Range(1, 64)
        ) = 8

        _Strength (
            "Pixelation Strength",
            Range(0, 1)
        ) = 1

        _DebugView (
            "Debug Stencil",
            Float
        ) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
        }

        Pass
        {
            Name "StencilPixelation"

            ZWrite Off
            ZTest Always
            Cull Off

            // 只允许已经被标记的像素写入颜色
            Stencil
            {
                Ref 64
                ReadMask 64
                Comp Equal
                Pass Keep
            }

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _PixelSize;
                float _Strength;
                float _DebugView;
            CBUFFER_END

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord.xy;

                // 原始屏幕颜色
                half4 originalColor =
                    SAMPLE_TEXTURE2D_X(
                        _BlitTexture,
                        sampler_LinearClamp,
                        uv
                    );

                // 调试模式
                // 通过模板测试的位置会显示紫红色
                if (_DebugView > 0.5)
                {
                    return half4(1, 0, 1, 1);
                }

                // 当前实际渲染分辨率
                float2 screenSize =
                    _ScaledScreenParams.xy;

                // 每个像素块占用多少屏幕像素
                float blockSize =
                    max(_PixelSize, 1.0);

                // 计算横向、纵向像素格数量
                float2 gridCount =
                    max(
                        floor(screenSize / blockSize),
                        float2(1.0, 1.0)
                    );

                // 对屏幕 UV 进行量化
                float2 pixelUV =
                    (floor(uv * gridCount) + 0.5)
                    / gridCount;

                // 采样像素化后的屏幕颜色
                half4 pixelColor =
                    SAMPLE_TEXTURE2D_X(
                        _BlitTexture,
                        sampler_LinearClamp,
                        pixelUV
                    );

                // 控制像素化强度
                half strength =
                    saturate(_Strength);

                half3 finalRGB =
                    lerp(
                        originalColor.rgb,
                        pixelColor.rgb,
                        strength
                    );

                return half4(
                    finalRGB,
                    originalColor.a
                );
            }

            ENDHLSL
        }
    }

    FallBack Off
}