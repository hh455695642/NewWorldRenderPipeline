Shader "Hidden/NWRP/PostProcess/Bloom"
{
    HLSLINCLUDE

        #pragma target 3.0
        #pragma editor_sync_compilation

        #include "../../ShaderLibrary/NWRPBlitCoreCompat.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        TEXTURE2D_X(_NWRPBloomCombineTexture);
        TEXTURE2D_X(_NWRPBloomTexture);
        TEXTURE2D_X(_NWRPBloomTexture1);
        TEXTURE2D_X(_NWRPBloomTexture2);
        TEXTURE2D_X(_NWRPBloomTexture3);
        TEXTURE2D_X(_NWRPBloomTexture4);

        float4 _NWRPBloomThresholdParams;
        float4 _NWRPBloomWeights;
        float4 _NWRPBloomWeights2;
        float4 _NWRPBloomTint;
        float3 _NWRPBloomTint0;
        float3 _NWRPBloomTint1;
        float3 _NWRPBloomTint2;
        float3 _NWRPBloomTint3;
        float3 _NWRPBloomTint4;
        float3 _NWRPBloomTint5;
        float4 _NWRPBloomTexelSize;
        float _NWRPBloomBlurScale;
        float _NWRPBloomSpread;

        #define NWRP_BLOOM_THRESHOLD _NWRPBloomThresholdParams.x
        #define NWRP_BLOOM_CONSERVATIVE_THRESHOLD _NWRPBloomThresholdParams.y
        #define NWRP_BLOOM_MAX_BRIGHTNESS _NWRPBloomThresholdParams.z

        half BloomBrightness(half3 color)
        {
            return max(color.r, max(color.g, color.b));
        }

        half3 BloomAboveThreshold(half3 color, half brightness)
        {
            half threshold = (half)NWRP_BLOOM_THRESHOLD;
            half knee = clamp(brightness - 0.5h * threshold, 0.0h, threshold);
            knee = 0.5h * knee * knee / (threshold + 0.0001h);
            half3 conservative = color * (max(brightness - threshold, knee) / max(brightness, 0.0001h));
            half3 simple = max(color - threshold, half3(0.0h, 0.0h, 0.0h));
            return lerp(simple, conservative, saturate((half)NWRP_BLOOM_CONSERVATIVE_THRESHOLD));
        }

        half4 PackBloomLuminance(half3 color)
        {
            half brightness = BloomBrightness(color);
            color = lerp(
                color,
                half3(brightness, brightness, brightness) * (half3)_NWRPBloomTint.rgb,
                (half)_NWRPBloomTint.a);
            color = BloomAboveThreshold(color, brightness);
            return half4(color, brightness);
        }

        half3 FetchBloomInput(float2 uv)
        {
            half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
            half maxBrightnessValue = (half)NWRP_BLOOM_MAX_BRIGHTNESS;
            half4 maxBrightness = half4(
                maxBrightnessValue,
                maxBrightnessValue,
                maxBrightnessValue,
                maxBrightnessValue);
            color = clamp(color, half4(0.0h, 0.0h, 0.0h, 0.0h), maxBrightness);

        #if UNITY_COLORSPACE_GAMMA
            color.rgb = SRGBToLinear(color.rgb);
        #endif

            return color.rgb;
        }

        half4 FragLuminance(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            return PackBloomLuminance(FetchBloomInput(input.texcoord));
        }

        half4 FragLuminanceAntiFlicker(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float2 texel = _NWRPBloomTexelSize.xy;
            half4 c0 = half4(FetchBloomInput(input.texcoord + float2(0.0, texel.y)), 0.0h);
            half4 c1 = half4(FetchBloomInput(input.texcoord + float2(texel.x, 0.0)), 0.0h);
            half4 c2 = half4(FetchBloomInput(input.texcoord - float2(texel.x, 0.0)), 0.0h);
            half4 c3 = half4(FetchBloomInput(input.texcoord - float2(0.0, texel.y)), 0.0h);

            c0.a = BloomBrightness(c0.rgb);
            c1.a = BloomBrightness(c1.rgb);
            c2.a = BloomBrightness(c2.rgb);
            c3.a = BloomBrightness(c3.rgb);

            half w0 = 1.0h / (c0.a + 1.0h);
            half w1 = 1.0h / (c1.a + 1.0h);
            half w2 = 1.0h / (c2.a + 1.0h);
            half w3 = 1.0h / (c3.a + 1.0h);
            half invWeight = 1.0h / (w0 + w1 + w2 + w3);
            half4 color = (c0 * w0 + c1 * w1 + c2 * w2 + c3 * w3) * invWeight;

            color.rgb = lerp(
                color.rgb,
                half3(color.a, color.a, color.a) * (half3)_NWRPBloomTint.rgb,
                (half)_NWRPBloomTint.a);
            color.rgb = BloomAboveThreshold(color.rgb, color.a);
            return color;
        }

        half4 ResampleBloom(float2 uv)
        {
            float2 texel = _NWRPBloomTexelSize.xy;
            half4 c0 = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(0.0, texel.y));
            half4 c1 = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(texel.x, 0.0));
            half4 c2 = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv - float2(texel.x, 0.0));
            half4 c3 = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv - float2(0.0, texel.y));

            half w0 = 1.0h / (c0.a + 1.0h);
            half w1 = 1.0h / (c1.a + 1.0h);
            half w2 = 1.0h / (c2.a + 1.0h);
            half w3 = 1.0h / (c3.a + 1.0h);
            half invWeight = 1.0h / (w0 + w1 + w2 + w3);
            return (c0 * w0 + c1 * w1 + c2 * w2 + c3 * w3) * invWeight;
        }

        half4 FragResample(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            return ResampleBloom(input.texcoord);
        }

        half4 Lerp3(half4 a, half4 b, half4 c, half t)
        {
            return t <= 0.5h
                ? lerp(a, b, t * 2.0h)
                : lerp(b, c, t * 2.0h - 1.0h);
        }

        half4 FragResampleAndCombine(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            half4 bloom = ResampleBloom(input.texcoord);
            half4 previous = SAMPLE_TEXTURE2D_X(_NWRPBloomCombineTexture, sampler_LinearClamp, input.texcoord);
            return Lerp3(previous, previous + bloom, bloom, (half)_NWRPBloomSpread);
        }

        half4 FragBloomCompose(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

            half4 b0 = SAMPLE_TEXTURE2D_X(_NWRPBloomTexture, sampler_LinearClamp, input.texcoord);
            half4 b1 = SAMPLE_TEXTURE2D_X(_NWRPBloomTexture1, sampler_LinearClamp, input.texcoord);
            half4 b2 = SAMPLE_TEXTURE2D_X(_NWRPBloomTexture2, sampler_LinearClamp, input.texcoord);
            half4 b3 = SAMPLE_TEXTURE2D_X(_NWRPBloomTexture3, sampler_LinearClamp, input.texcoord);
            half4 b4 = SAMPLE_TEXTURE2D_X(_NWRPBloomTexture4, sampler_LinearClamp, input.texcoord);
            half4 b5 = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord);

            b0.rgb *= (half3)_NWRPBloomTint0;
            b1.rgb *= (half3)_NWRPBloomTint1;
            b2.rgb *= (half3)_NWRPBloomTint2;
            b3.rgb *= (half3)_NWRPBloomTint3;
            b4.rgb *= (half3)_NWRPBloomTint4;
            b5.rgb *= (half3)_NWRPBloomTint5;

            return b0 * (half)_NWRPBloomWeights.x
                + b1 * (half)_NWRPBloomWeights.y
                + b2 * (half)_NWRPBloomWeights.z
                + b3 * (half)_NWRPBloomWeights.w
                + b4 * (half)_NWRPBloomWeights2.x
                + b5 * (half)_NWRPBloomWeights2.y;
        }

        half4 FragBlurHorizontal(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float2 inc0 = float2(_NWRPBloomTexelSize.x * 1.3846153846 * _NWRPBloomBlurScale, 0.0);
            float2 inc1 = float2(_NWRPBloomTexelSize.x * 3.2307692308 * _NWRPBloomBlurScale, 0.0);
            return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord) * 0.2270270270h
                + (SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord - inc0)
                    + SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord + inc0)) * 0.3162162162h
                + (SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord - inc1)
                    + SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord + inc1)) * 0.0702702703h;
        }

        half4 FragBlurVertical(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float2 inc0 = float2(0.0, _NWRPBloomTexelSize.y * 1.3846153846 * _NWRPBloomBlurScale);
            float2 inc1 = float2(0.0, _NWRPBloomTexelSize.y * 3.2307692308 * _NWRPBloomBlurScale);
            return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord) * 0.2270270270h
                + (SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord - inc0)
                    + SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord + inc0)) * 0.3162162162h
                + (SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord - inc1)
                    + SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord + inc1)) * 0.0702702703h;
        }

    ENDHLSL

    SubShader
    {
        Tags { "RenderPipeline" = "NewWorldRenderPipeline" }

        Pass
        {
            Name "Luminance"
            ZWrite Off ZTest Always Blend Off Cull Off

            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment FragLuminance
            ENDHLSL
        }

        Pass
        {
            Name "Luminance AntiFlicker"
            ZWrite Off ZTest Always Blend Off Cull Off

            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment FragLuminanceAntiFlicker
            ENDHLSL
        }

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

        Pass
        {
            Name "Resample"
            ZWrite Off ZTest Always Blend Off Cull Off

            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment FragResample
            ENDHLSL
        }

        Pass
        {
            Name "Resample And Combine"
            ZWrite Off ZTest Always Blend Off Cull Off

            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment FragResampleAndCombine
            ENDHLSL
        }

        Pass
        {
            Name "Bloom Compose"
            ZWrite Off ZTest Always Blend Off Cull Off

            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment FragBloomCompose
            ENDHLSL
        }
    }

    Fallback Off
}
