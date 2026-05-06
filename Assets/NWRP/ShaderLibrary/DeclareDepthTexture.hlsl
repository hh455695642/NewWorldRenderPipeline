#ifndef NEWWORLD_DECLARE_DEPTH_TEXTURE_INCLUDED
#define NEWWORLD_DECLARE_DEPTH_TEXTURE_INCLUDED

#include "Core.hlsl"

TEXTURE2D(_CameraDepthTexture);
SAMPLER(sampler_CameraDepthTexture);

float SampleSceneDepth(float2 uv)
{
    return SAMPLE_TEXTURE2D(_CameraDepthTexture, sampler_CameraDepthTexture, uv).r;
}

float LoadSceneDepth(uint2 pixelCoord)
{
    return LOAD_TEXTURE2D(_CameraDepthTexture, pixelCoord).r;
}

float SampleSceneDepthLinear01(float2 uv)
{
    return Linear01Depth(SampleSceneDepth(uv));
}

float SampleSceneDepthLinearEye(float2 uv)
{
    return LinearEyeDepth(SampleSceneDepth(uv));
}

#endif // NEWWORLD_DECLARE_DEPTH_TEXTURE_INCLUDED
