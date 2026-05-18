Shader "Hidden/NWRP/PostProcess/Tonemapping"
{
    HLSLINCLUDE

        #pragma target 3.0
        #pragma editor_sync_compilation

        #include "../../ShaderLibrary/NWRPBlitCoreCompat.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        float4 _NWRPTonemapParams;
        float4 _NWRPColorAdjustParams;
        float4 _NWRPColorAdjustParams2;
        float4 _NWRPColorAdjustTint;
        float4 _NWRPVignetteColor;
        float4 _NWRPVignetteParams;
        float4 _NWRPVignetteParams2;
        float4 _NWRPBloomCompositeParams;
        float4 _NWRPFxaaParams;
        float4 _NWRPFxaaTexelSize;
        TEXTURE2D_X(_NWRPBloomTexture);
        TEXTURE2D_X(_NWRPBloomDirtSourceTexture);
        TEXTURE2D(_NWRPBloomDirtTexture);

        #define NWRP_TONEMAP_PRE_EXPOSURE _NWRPTonemapParams.x
        #define NWRP_TONEMAP_POST_BRIGHTNESS _NWRPTonemapParams.y
        #define NWRP_TONEMAP_MAX_INPUT_BRIGHTNESS _NWRPTonemapParams.z
        #define NWRP_TONEMAP_AGX_GAMMA _NWRPTonemapParams.w
        #define NWRP_COLOR_SEPIA _NWRPColorAdjustParams.x
        #define NWRP_COLOR_DALTONIZE _NWRPColorAdjustParams.y
        #define NWRP_COLOR_SATURATION _NWRPColorAdjustParams.z
        #define NWRP_COLOR_BRIGHTNESS _NWRPColorAdjustParams.w
        #define NWRP_COLOR_CONTRAST _NWRPColorAdjustParams2.x
        #define NWRP_COLOR_TEMPERATURE _NWRPColorAdjustParams2.y
        #define NWRP_COLOR_TEMPERATURE_BLEND _NWRPColorAdjustParams2.z
        #define NWRP_COLOR_ADJUST_ACTIVE _NWRPColorAdjustParams2.w
        #define NWRP_VIGNETTE_CENTER _NWRPVignetteParams.xy
        #define NWRP_VIGNETTE_ASPECT_Y _NWRPVignetteParams.z
        #define NWRP_VIGNETTE_OUTER_RING _NWRPVignetteParams.w
        #define NWRP_VIGNETTE_ASPECT_X _NWRPVignetteParams2.x
        #define NWRP_VIGNETTE_INNER_RING _NWRPVignetteParams2.y
        #define NWRP_VIGNETTE_FADE _NWRPVignetteParams2.z
        #define NWRP_VIGNETTE_ACTIVE _NWRPVignetteParams2.w
        #define NWRP_BLOOM_INTENSITY _NWRPBloomCompositeParams.x
        #define NWRP_BLOOM_DIRT_INTENSITY _NWRPBloomCompositeParams.y
        #define NWRP_BLOOM_DIRT_THRESHOLD _NWRPBloomCompositeParams.z
        #define NWRP_BLOOM_DIRT_CONTRIBUTION _NWRPBloomCompositeParams.w
        #define NWRP_FXAA_FIXED_THRESHOLD _NWRPFxaaParams.x
        #define NWRP_FXAA_RELATIVE_THRESHOLD _NWRPFxaaParams.y
        #define NWRP_FXAA_SUBPIXEL_BLENDING _NWRPFxaaParams.z
        #define NWRP_FXAA_TEXEL_SIZE _NWRPFxaaTexelSize.xy

        #define NWRP_TONEMAP_LINEAR 0
        #define NWRP_TONEMAP_ACES 1
        #define NWRP_TONEMAP_ACES_FITTED 2
        #define NWRP_TONEMAP_AGX 3
        #define NWRP_FXAA_SPAN_MAX 8.0
        #define NWRP_FXAA_REDUCE_MUL (1.0 / 8.0)
        #define NWRP_FXAA_REDUCE_MIN (1.0 / 128.0)

        half NWRPGetLuma(half3 color)
        {
            return dot(color, half3(0.299h, 0.587h, 0.114h));
        }

        float3 NWRPKelvinToRGB(float kelvin)
        {
            float safeKelvin = clamp(kelvin, 1000.0, 40000.0);
            float3 m0 = float3(0.0, -2902.1955373783176, -8257.7997278925690);
            float3 m1 = float3(0.0, 1669.5803561666639, 2575.2827530017594);
            float3 m2 = float3(1.0, 1.3302673723350029, 1.8993753891711275);
            return m0 / (safeKelvin.xxx + m1) + m2;
        }

        half3 ApplyNWRPColorAdjustments(half3 color)
        {
            UNITY_BRANCH
            if (NWRP_COLOR_ADJUST_ACTIVE <= 0.0)
            {
                return color;
            }

            half luma = NWRPGetLuma(color);
            half daltonize = (half)NWRP_COLOR_DALTONIZE;
            half3 inverseColor = half3(1.0h, 1.0h, 1.0h) - saturate(color);
            half3 daltonized = color;
            daltonized.r *= 1.0h + daltonized.r * inverseColor.g * inverseColor.b * daltonize;
            daltonized.g *= 1.0h + daltonized.g * inverseColor.r * inverseColor.b * daltonize;
            daltonized.b *= 1.0h + daltonized.b * inverseColor.r * inverseColor.g * daltonize;
            color = daltonized * (luma / (NWRPGetLuma(daltonized) + 0.0001h));

            half3 sepia = half3(
                dot(color, half3(0.393h, 0.769h, 0.189h)),
                dot(color, half3(0.349h, 0.686h, 0.168h)),
                dot(color, half3(0.272h, 0.534h, 0.131h)));
            color = lerp(color, sepia, saturate((half)NWRP_COLOR_SEPIA));

            half maxComponent = max(color.r, max(color.g, color.b));
            half minComponent = min(color.r, min(color.g, color.b));
            half saturation = saturate(maxComponent - minComponent);
            color *= 1.0h
                + (half)NWRP_COLOR_SATURATION
                * (1.0h - saturation)
                * (color - NWRPGetLuma(color));

            color = lerp(
                color,
                color * (half3)_NWRPColorAdjustTint.rgb,
                saturate((half)_NWRPColorAdjustTint.a));
            color = (color - half3(0.5h, 0.5h, 0.5h)) * (half)NWRP_COLOR_CONTRAST
                + half3(0.5h, 0.5h, 0.5h);
            color *= (half)NWRP_COLOR_BRIGHTNESS;

            UNITY_BRANCH
            if (NWRP_COLOR_TEMPERATURE_BLEND > 0.0)
            {
                half3 kelvin = (half3)NWRPKelvinToRGB(NWRP_COLOR_TEMPERATURE);
                color = lerp(color, color * kelvin, saturate((half)NWRP_COLOR_TEMPERATURE_BLEND));
            }

            return color;
        }

        half3 ApplyNWRPVignette(float2 uv, half3 color)
        {
            UNITY_BRANCH
            if (NWRP_VIGNETTE_ACTIVE <= 0.0)
            {
                return color;
            }

            float2 vignetteDelta = float2(
                (uv.x - NWRP_VIGNETTE_CENTER.x) * NWRP_VIGNETTE_ASPECT_X,
                (uv.y - NWRP_VIGNETTE_CENTER.y) * NWRP_VIGNETTE_ASPECT_Y);
            float outerRing = max(NWRP_VIGNETTE_OUTER_RING, 0.0001);
            float innerRing = min(NWRP_VIGNETTE_INNER_RING, outerRing - 0.0001);
            float ringRange = max(outerRing - innerRing, 0.0001);
            half edgeBlend = saturate(
                (dot(vignetteDelta, vignetteDelta) - innerRing) / ringRange
                + (half)NWRP_VIGNETTE_FADE);
            edgeBlend = smoothstep(0.0h, 1.0h, edgeBlend);
            edgeBlend *= edgeBlend;
            return lerp(
                color,
                (half3)_NWRPVignetteColor.rgb,
                edgeBlend * saturate((half)_NWRPVignetteColor.a));
        }

        float3 ApplyNWRPBloom(float2 uv, float3 color)
        {
            UNITY_BRANCH
            if (NWRP_BLOOM_INTENSITY > 0.0)
            {
                color += SAMPLE_TEXTURE2D_X(
                    _NWRPBloomTexture,
                    sampler_LinearClamp,
                    uv).rgb * NWRP_BLOOM_INTENSITY;
            }

            UNITY_BRANCH
            if (NWRP_BLOOM_DIRT_INTENSITY > 0.0)
            {
                float3 screenLum = SAMPLE_TEXTURE2D_X(
                    _NWRPBloomDirtSourceTexture,
                    sampler_LinearClamp,
                    uv).rgb * NWRP_BLOOM_DIRT_CONTRIBUTION;
                float3 dirt = SAMPLE_TEXTURE2D(
                    _NWRPBloomDirtTexture,
                    sampler_LinearRepeat,
                    uv).rgb;
                color += saturate(
                        float3(0.5, 0.5, 0.5)
                        - float3(
                            NWRP_BLOOM_DIRT_THRESHOLD,
                            NWRP_BLOOM_DIRT_THRESHOLD,
                            NWRP_BLOOM_DIRT_THRESHOLD)
                        + screenLum)
                    * dirt
                    * NWRP_BLOOM_DIRT_INTENSITY;
            }

            return color;
        }

        float4 FetchTonemapInputAtUv(float2 uv)
        {
            float maxInput = max(NWRP_TONEMAP_MAX_INPUT_BRIGHTNESS, 0.0);
            float4 color = SAMPLE_TEXTURE2D_X_LOD(
                _BlitTexture,
                sampler_LinearClamp,
                uv,
                _BlitMipLevel);
            color.rgb = ApplyNWRPBloom(uv, color.rgb);
            color.rgb = min(max(color.rgb, float3(0.0, 0.0, 0.0)), float3(maxInput, maxInput, maxInput))
                * NWRP_TONEMAP_PRE_EXPOSURE;
            return color;
        }

        float4 FetchTonemapInput(Varyings input)
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            return FetchTonemapInputAtUv(input.texcoord.xy);
        }

        float4 PackTonemapOutput(float2 uv, float3 color, float alpha)
        {
            half3 finalColor = (half3)saturate(color * NWRP_TONEMAP_POST_BRIGHTNESS);
            finalColor = ApplyNWRPColorAdjustments(finalColor);
            finalColor = ApplyNWRPVignette(uv, finalColor);

            return float4(
                saturate(finalColor),
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

        float4 ResolveNWRPFinalColor(float2 uv, int tonemapMode)
        {
            float4 color = FetchTonemapInputAtUv(uv);

            if (tonemapMode == NWRP_TONEMAP_ACES)
            {
                color.rgb = TonemapACES(color.rgb);
            }
            else if (tonemapMode == NWRP_TONEMAP_ACES_FITTED)
            {
                color.rgb = TonemapACESFitted(color.rgb);
            }
            else if (tonemapMode == NWRP_TONEMAP_AGX)
            {
                color.rgb = TonemapAGX(color.rgb);
            }

            return PackTonemapOutput(uv, color.rgb, color.a);
        }

        float4 ApplyNWRPFxaa(float2 uv, int tonemapMode)
        {
            float2 texelSize = NWRP_FXAA_TEXEL_SIZE;
            float4 colorM = ResolveNWRPFinalColor(uv, tonemapMode);

            float4 colorNW = ResolveNWRPFinalColor(uv + texelSize * float2(-1.0, -1.0), tonemapMode);
            float4 colorNE = ResolveNWRPFinalColor(uv + texelSize * float2(1.0, -1.0), tonemapMode);
            float4 colorSW = ResolveNWRPFinalColor(uv + texelSize * float2(-1.0, 1.0), tonemapMode);
            float4 colorSE = ResolveNWRPFinalColor(uv + texelSize * float2(1.0, 1.0), tonemapMode);

            half lumaM = NWRPGetLuma((half3)colorM.rgb);
            half lumaNW = NWRPGetLuma((half3)colorNW.rgb);
            half lumaNE = NWRPGetLuma((half3)colorNE.rgb);
            half lumaSW = NWRPGetLuma((half3)colorSW.rgb);
            half lumaSE = NWRPGetLuma((half3)colorSE.rgb);

            half lumaMin = min(lumaM, min(min(lumaNW, lumaNE), min(lumaSW, lumaSE)));
            half lumaMax = max(lumaM, max(max(lumaNW, lumaNE), max(lumaSW, lumaSE)));
            half contrast = lumaMax - lumaMin;
            half edgeThreshold = max(
                (half)NWRP_FXAA_FIXED_THRESHOLD,
                lumaMax * (half)NWRP_FXAA_RELATIVE_THRESHOLD);

            UNITY_BRANCH
            if (contrast < edgeThreshold)
            {
                return colorM;
            }

            float2 dir;
            dir.x = -((float)(lumaNW + lumaNE) - (float)(lumaSW + lumaSE));
            dir.y = ((float)(lumaNW + lumaSW) - (float)(lumaNE + lumaSE));

            half lumaSum = lumaNW + lumaNE + lumaSW + lumaSE;
            float dirReduce = max((float)lumaSum * (0.25 * NWRP_FXAA_REDUCE_MUL), NWRP_FXAA_REDUCE_MIN);
            float rcpDirMin = rcp(min(abs(dir.x), abs(dir.y)) + dirReduce);
            dir = clamp(dir * rcpDirMin, -NWRP_FXAA_SPAN_MAX, NWRP_FXAA_SPAN_MAX) * texelSize;

            float4 colorA = 0.5 * (
                ResolveNWRPFinalColor(uv + dir * (1.0 / 3.0 - 0.5), tonemapMode)
                + ResolveNWRPFinalColor(uv + dir * (2.0 / 3.0 - 0.5), tonemapMode));
            float4 colorB = colorA * 0.5 + 0.25 * (
                ResolveNWRPFinalColor(uv - dir * 0.5, tonemapMode)
                + ResolveNWRPFinalColor(uv + dir * 0.5, tonemapMode));

            half lumaB = NWRPGetLuma((half3)colorB.rgb);
            float4 edgeColor = (lumaB < lumaMin || lumaB > lumaMax) ? colorA : colorB;

            half averageLuma = (lumaNW + lumaNE + lumaSW + lumaSE) * 0.25h;
            half subpixelContrast = saturate(abs(averageLuma - lumaM) / max(contrast, 0.0001h));
            subpixelContrast = smoothstep(0.0h, 1.0h, subpixelContrast);
            half edgeBlend = saturate(0.5h + 0.5h * (half)NWRP_FXAA_SUBPIXEL_BLENDING * subpixelContrast);

            return float4(
                lerp(colorM.rgb, edgeColor.rgb, edgeBlend),
                colorM.a);
        }

        float4 FragLinear(Varyings input) : SV_Target
        {
            float4 color = FetchTonemapInput(input);
            return PackTonemapOutput(input.texcoord.xy, color.rgb, color.a);
        }

        float4 FragACES(Varyings input) : SV_Target
        {
            float4 color = FetchTonemapInput(input);
            return PackTonemapOutput(input.texcoord.xy, TonemapACES(color.rgb), color.a);
        }

        float4 FragACESFitted(Varyings input) : SV_Target
        {
            float4 color = FetchTonemapInput(input);
            return PackTonemapOutput(input.texcoord.xy, TonemapACESFitted(color.rgb), color.a);
        }

        float4 FragAGX(Varyings input) : SV_Target
        {
            float4 color = FetchTonemapInput(input);
            return PackTonemapOutput(input.texcoord.xy, TonemapAGX(color.rgb), color.a);
        }

        float4 FragLinearFXAA(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            return ApplyNWRPFxaa(input.texcoord.xy, NWRP_TONEMAP_LINEAR);
        }

        float4 FragACESFXAA(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            return ApplyNWRPFxaa(input.texcoord.xy, NWRP_TONEMAP_ACES);
        }

        float4 FragACESFittedFXAA(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            return ApplyNWRPFxaa(input.texcoord.xy, NWRP_TONEMAP_ACES_FITTED);
        }

        float4 FragAGXFXAA(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            return ApplyNWRPFxaa(input.texcoord.xy, NWRP_TONEMAP_AGX);
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

        Pass
        {
            Name "Linear FXAA"
            ZWrite Off ZTest Always Blend Off Cull Off

            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment FragLinearFXAA
            ENDHLSL
        }

        Pass
        {
            Name "ACES FXAA"
            ZWrite Off ZTest Always Blend Off Cull Off

            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment FragACESFXAA
            ENDHLSL
        }

        Pass
        {
            Name "ACES Fitted FXAA"
            ZWrite Off ZTest Always Blend Off Cull Off

            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment FragACESFittedFXAA
            ENDHLSL
        }

        Pass
        {
            Name "AGX FXAA"
            ZWrite Off ZTest Always Blend Off Cull Off

            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment FragAGXFXAA
            ENDHLSL
        }
    }

    Fallback Off
}
