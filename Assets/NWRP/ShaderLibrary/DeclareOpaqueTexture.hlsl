#ifndef NEWWORLD_DECLARE_OPAQUE_TEXTURE_INCLUDED
#define NEWWORLD_DECLARE_OPAQUE_TEXTURE_INCLUDED

#include "Core.hlsl"

TEXTURE2D(_CameraOpaqueTexture);
SAMPLER(sampler_CameraOpaqueTexture);

half3 SampleSceneColor(float2 uv)
{
    return SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, uv).rgb;
}

half4 SampleSceneColorAlpha(float2 uv)
{
    return SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, uv);
}

half3 LoadSceneColor(uint2 pixelCoord)
{
    return LOAD_TEXTURE2D(_CameraOpaqueTexture, pixelCoord).rgb;
}

#endif
