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
            #pragma multi_compile_local_fragment _ _OUTPUT_DEPTH

            #include "../../ShaderLibrary/Passes/CopyDepthPass.hlsl"

            ENDHLSL
        }
    }

    Fallback Off
}
