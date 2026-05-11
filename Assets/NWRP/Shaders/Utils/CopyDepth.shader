Shader "Hidden/NWRP/CopyDepth"
{
    SubShader
    {
        Tags { "RenderPipeline" = "NewWorldRenderPipeline" }

        Pass
        {
            Name "CopyDepth"

            ZTest Always
            ZWrite On
            ColorMask R
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragCopyDepth
            #pragma multi_compile_local_fragment _ _DEPTH_MSAA_2 _DEPTH_MSAA_4 _DEPTH_MSAA_8
            #pragma multi_compile_local_fragment _ _OUTPUT_DEPTH

            #include "../../ShaderLibrary/Passes/CopyDepthPass.hlsl"

            ENDHLSL
        }
    }

    Fallback Off
}
