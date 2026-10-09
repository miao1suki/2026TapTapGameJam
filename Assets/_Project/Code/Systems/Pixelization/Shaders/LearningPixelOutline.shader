Shader "Hidden/2026TapTap/LearningPixelOutline"
{
    Properties
    {
        // Blit API 会在运行时自动填入当前 Pass 的输入颜色纹理。
        [HideInInspector] _BlitTexture ("Blit Texture", 2D) = "white" {}

        // RenderFeature 通过 MaterialPropertyBlock 传入当前输入纹理的尺寸。
        [HideInInspector] _ScreenPixelSourceSize ("Source Size", Vector) = (1, 1, 1, 1)

        [Header(Pixelation)]
        _PixelSize ("像素块大小", Range(1, 64)) = 15
        _OutlineEnabled ("启用描边", Float) = 0

        [Header(Outline)]
        [HDR] _OutlineColor ("描边颜色", Color) = (1, 1, 1, 1)
        _OutlineThickness ("描边采样半径", Range(0.5, 4)) = 1
        _DepthWeight ("深度权重", Range(0, 10)) = 1.5
        _NormalWeight ("法线权重", Range(0, 10)) = 1

        [Header(Color Assisted Edges)]
        [Toggle] _UseLuminanceEdge ("启用亮度边缘", Float) = 0
        _LuminanceWeight ("亮度权重", Range(0, 5)) = 1
        _ColorWeight ("RGB 颜色权重", Range(0, 5)) = 0

        [Header(Edge Shaping)]
        _EdgeThreshold ("边缘阈值", Range(0, 2)) = 0.08
        _EdgeSoftness ("边缘软化", Range(0.001, 0.5)) = 0.04
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
        }

        // 三个全屏 Pass 都不参与场景深度测试，也不写入深度。
        ZTest Always
        ZWrite Off
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        // 所有 Pass 共用同一组材质参数。
        CBUFFER_START(UnityPerMaterial)
            float _PixelSize;
            float _OutlineEnabled;
            float4 _OutlineColor;
            float _OutlineThickness;
            float _DepthWeight;
            float _NormalWeight;
            float _UseLuminanceEdge;
            float _LuminanceWeight;
            float _ColorWeight;
            float _EdgeThreshold;
            float _EdgeSoftness;
            float4 _ScreenPixelSourceSize;
        CBUFFER_END

        #include "LearningPixelOutline.hlsl"
        ENDHLSL

        Pass
        {
            Name "Pixelation And Edge Detection"

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment PixelAndEdgeFrag

            // URP 使用八面体法线编码时，编译对应的法线解码变体。
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            half4 PixelAndEdgeFrag(Varyings input) : SV_Target
            {
                // XR 下需要先恢复当前眼睛索引，再采样 TEXTURE2D_X 纹理。
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return ScreenPixelAndDetectFragment(input.texcoord);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Pixelize Outline Mask"

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment ReduceOutlineFrag

            half4 ReduceOutlineFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return ScreenReduceOutlineFragment(input.texcoord);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Composite Pixel Aligned Outline"

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment CompositeFrag

            half4 CompositeFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return ScreenCompositeFragment(input.texcoord);
            }
            ENDHLSL
        }
    }
}
