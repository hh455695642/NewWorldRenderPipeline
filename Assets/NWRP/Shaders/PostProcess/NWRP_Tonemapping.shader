Shader "Hidden/NWRP/PostProcess/Tonemapping"
{
    HLSLINCLUDE

        #pragma target 3.0
        #pragma editor_sync_compilation

        #include "../../ShaderLibrary/NWRPBlitCoreCompat.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        float4 _NWRPTonemapParams;

        #define NWRP_TONEMAP_PRE_EXPOSURE _NWRPTonemapParams.x
        #define NWRP_TONEMAP_POST_BRIGHTNESS _NWRPTonemapParams.y
        #define NWRP_TONEMAP_MAX_INPUT_BRIGHTNESS _NWRPTonemapParams.z
        #define NWRP_TONEMAP_AGX_GAMMA _NWRPTonemapParams.w

        float4 FetchTonemapInput(Varyings input)
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

            float maxInput = max(NWRP_TONEMAP_MAX_INPUT_BRIGHTNESS, 0.0);
            float4 color = SAMPLE_TEXTURE2D_X_LOD(
                _BlitTexture,
                sampler_LinearClamp,
                input.texcoord.xy,
                _BlitMipLevel);
            color.rgb = min(max(color.rgb, float3(0.0, 0.0, 0.0)), float3(maxInput, maxInput, maxInput))
                * NWRP_TONEMAP_PRE_EXPOSURE;
            return color;
        }

        float4 PackTonemapOutput(float3 color, float alpha)
        {
            return float4(
                saturate(color * NWRP_TONEMAP_POST_BRIGHTNESS),
                saturate(alpha));
        }

        float3 TonemapACES(float3 x)
        {
            const float A = 2.51;
            const float B = 0.03;
            const float C = 2.43;
            const float D = 0.59;
            const float E = 0.14;
            return (x * (A * x + B)) / (x * (C * x + D) + E);
        }

        // This ACES Fitted operator is adapted from BeautifyACESFitted.hlsl.
        //
        // ACES Fitted, an alternate ACES tonemap operator by MJP and David Neubelt
        // http://mynameismjp.wordpress.com/
        //
        // Licensed under the MIT license. The original fit was written by Stephen Hill.
        static const float3x3 NWRP_ACESInputMat = float3x3(
            0.59719, 0.35458, 0.04823,
            0.07600, 0.90834, 0.01566,
            0.02840, 0.13383, 0.83777);

        static const float3x3 NWRP_ACESOutputMat = float3x3(
             1.60475, -0.53108, -0.07367,
            -0.10208,  1.10813, -0.00605,
            -0.00327, -0.07276,  1.07602);

        float3 NWRP_RRTAndODTFit(float3 v)
        {
            float3 a = v * (v + 0.0245786) - 0.000090537;
            float3 b = v * (0.983729 * v + 0.4329510) + 0.238081;
            return a / b;
        }

        float3 TonemapACESFitted(float3 value)
        {
            value = mul(NWRP_ACESInputMat, value);
            value = NWRP_RRTAndODTFit(value);
            return mul(NWRP_ACESOutputMat, value);
        }

        // This AgX operator is adapted from BeautifyAGX.hlsl.
        //
        // MIT License
        // Copyright (c) 2024 Missing Deadlines (Benjamin Wrensch)
        //
        // Values are sourced from Troy Sobotka's initial AgX implementation/OCIO config.
        static const float3x3 NWRP_AGXMat = float3x3(
            0.842479062253094, 0.0784335999999992, 0.0792237451477643,
            0.0423282422610123, 0.878468636469772, 0.0791661274605434,
            0.0423756549057051, 0.0784336, 0.879142973793104);

        static const float3x3 NWRP_AGXMatInv = float3x3(
            1.19687900512017, -0.0980208811401368, -0.0990297440797205,
            -0.0528968517574562, 1.15190312990417, -0.0989611768448433,
            -0.0529716355144438, -0.0980434501171241, 1.15107367264116);

        float3 NWRP_AGXDefaultContrastApprox(float3 x)
        {
            float3 x2 = x * x;
            float3 x4 = x2 * x2;
            return 15.5 * x4 * x2
                - 40.14 * x4 * x
                + 31.96 * x4
                - 6.868 * x2 * x
                + 0.4298 * x2
                + 0.1191 * x
                - 0.00232;
        }

        float3 TonemapAGX(float3 value)
        {
            const float minEv = -12.47393;
            const float maxEv = 4.026069;

            value = mul(NWRP_AGXMat, max(value, float3(1.0e-6, 1.0e-6, 1.0e-6)));
            value = clamp(log2(value), minEv, maxEv);
            value = (value - minEv) / (maxEv - minEv);
            value = NWRP_AGXDefaultContrastApprox(value);
            value = mul(NWRP_AGXMatInv, value);
            return pow(max(value, float3(0.0, 0.0, 0.0)), max(NWRP_TONEMAP_AGX_GAMMA, 0.0001));
        }

        float4 FragLinear(Varyings input) : SV_Target
        {
            float4 color = FetchTonemapInput(input);
            return PackTonemapOutput(color.rgb, color.a);
        }

        float4 FragACES(Varyings input) : SV_Target
        {
            float4 color = FetchTonemapInput(input);
            return PackTonemapOutput(TonemapACES(color.rgb), color.a);
        }

        float4 FragACESFitted(Varyings input) : SV_Target
        {
            float4 color = FetchTonemapInput(input);
            return PackTonemapOutput(TonemapACESFitted(color.rgb), color.a);
        }

        float4 FragAGX(Varyings input) : SV_Target
        {
            float4 color = FetchTonemapInput(input);
            return PackTonemapOutput(TonemapAGX(color.rgb), color.a);
        }

    ENDHLSL

    SubShader
    {
        Tags { "RenderPipeline" = "NewWorldRenderPipeline" }

        Pass
        {
            Name "Linear"
            ZWrite Off ZTest Always Blend Off Cull Off

            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment FragLinear
            ENDHLSL
        }

        Pass
        {
            Name "ACES"
            ZWrite Off ZTest Always Blend Off Cull Off

            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment FragACES
            ENDHLSL
        }

        Pass
        {
            Name "ACES Fitted"
            ZWrite Off ZTest Always Blend Off Cull Off

            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment FragACESFitted
            ENDHLSL
        }

        Pass
        {
            Name "AGX"
            ZWrite Off ZTest Always Blend Off Cull Off

            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment FragAGX
            ENDHLSL
        }
    }

    Fallback Off
}
