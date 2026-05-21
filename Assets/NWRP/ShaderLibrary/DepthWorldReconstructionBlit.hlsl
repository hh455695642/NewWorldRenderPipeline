#ifndef NEWWORLD_DEPTH_WORLD_RECONSTRUCTION_BLIT_INCLUDED
#define NEWWORLD_DEPTH_WORLD_RECONSTRUCTION_BLIT_INCLUDED

// Blit shaders already include SRP Core blit helpers; keep this include free of
// NWRP Common.hlsl to avoid macro/function redefinitions on mobile backends.
#include "UnityInput.hlsl"

#ifndef UNITY_NEAR_CLIP_VALUE
    #if UNITY_REVERSED_Z
        #define UNITY_NEAR_CLIP_VALUE (1.0)
    #else
        #define UNITY_NEAR_CLIP_VALUE (-1.0)
    #endif
#endif

TEXTURE2D_X(_CameraDepthTexture);
SAMPLER(sampler_CameraDepthTexture);

float4 _CameraDepthTextureScaleBias;

float2 TransformSceneDepthUV(float2 screenUV)
{
    return screenUV * _CameraDepthTextureScaleBias.xy + _CameraDepthTextureScaleBias.zw;
}

float SampleSceneDepth(float2 screenUV)
{
    return SAMPLE_TEXTURE2D_X(
        _CameraDepthTexture,
        sampler_CameraDepthTexture,
        TransformSceneDepthUV(screenUV)).r;
}

float NWRPRawDepthToDeviceDepth(float rawDepth)
{
#if UNITY_REVERSED_Z
    return rawDepth;
#else
    return lerp(UNITY_NEAR_CLIP_VALUE, 1.0, rawDepth);
#endif
}

bool IsSceneDepthValid(float rawDepth)
{
#if UNITY_REVERSED_Z
    return rawDepth > 1.0e-6;
#else
    return rawDepth < 0.999999;
#endif
}

float3 ComputeSceneWorldSpacePosition(float2 screenUV, float rawDepth)
{
    return ComputeWorldSpacePosition(
        screenUV,
        NWRPRawDepthToDeviceDepth(rawDepth),
        UNITY_MATRIX_I_VP);
}

#endif // NEWWORLD_DEPTH_WORLD_RECONSTRUCTION_BLIT_INCLUDED
