Shader "Hidden/NWRP/PostProcess/ScreenBlur"
{
    HLSLINCLUDE

        #pragma target 3.0
        #pragma editor_sync_compilation

        #include "../../ShaderLibrary/NWRPBlitCoreCompat.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        float _NWRPScreenBlurRadius;
        float4 _NWRPScreenBlurTexelSize;

        half4 SampleNWRPScreenBlur(float2 uv, float2 axis)
        {
            float2 inc0 = axis * (1.3846153846 * _NWRPScreenBlurRadius);
            float2 inc1 = axis * (3.2307692308 * _NWRPScreenBlurRadius);

            return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv) * 0.2270270270h
                + (SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv - inc0)
                    + SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + inc0)) * 0.3162162162h
                + (SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv - inc1)
                    + SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + inc1)) * 0.0702702703h;
        }

        half4 FragBlurHorizontal(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            return SampleNWRPScreenBlur(
                input.texcoord,
                float2(_NWRPScreenBlurTexelSize.x, 0.0));
        }

        half4 FragBlurVertical(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            return SampleNWRPScreenBlur(
                input.texcoord,
                float2(0.0, _NWRPScreenBlurTexelSize.y));
        }

    ENDHLSL

    SubShader
    {
        Tags { "RenderPipeline" = "NewWorldRenderPipeline" }

        Pass
        {
            Name "Blur Horizontal"
            ZWrite Off ZTest Always Blend Off Cull Off

            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment FragBlurHorizontal
            ENDHLSL
        }

        Pass
        {
            Name "Blur Vertical"
            ZWrite Off ZTest Always Blend Off Cull Off

            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment FragBlurVertical
            ENDHLSL
        }
    }

    Fallback Off
}
