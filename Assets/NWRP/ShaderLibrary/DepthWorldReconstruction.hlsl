#ifndef NEWWORLD_DEPTH_WORLD_RECONSTRUCTION_INCLUDED
#define NEWWORLD_DEPTH_WORLD_RECONSTRUCTION_INCLUDED

#include "DeclareDepthTexture.hlsl"

float NWRPRawDepthToDeviceDepth(float rawDepth)
{
#if UNITY_REVERSED_Z
    return rawDepth;
#else
    return lerp(UNITY_NEAR_CLIP_VALUE, 1.0, rawDepth);
#endif
}

float RawDepthToDeviceDepth(float rawDepth)
{
    return NWRPRawDepthToDeviceDepth(rawDepth);
}

bool IsSceneDepthValid(float rawDepth)
{
#if UNITY_REVERSED_Z
    return rawDepth > 1.0e-6;
#else
    return rawDepth < 0.999999;
#endif
}

float SampleSceneDepthFromPositionCS(float4 positionCS)
{
    return SampleSceneDepth(GetNormalizedScreenSpaceUV(positionCS));
}

float3 ComputeSceneWorldSpacePosition(float2 screenUV, float rawDepth)
{
    float deviceDepth = RawDepthToDeviceDepth(rawDepth);
    return ComputeWorldSpacePosition(screenUV, deviceDepth, UNITY_MATRIX_I_VP);
}

float3 ComputeSceneWorldSpacePositionFromPositionCS(float4 positionCS, float rawDepth)
{
    float2 screenUV = GetNormalizedScreenSpaceUV(positionCS);
    return ComputeSceneWorldSpacePosition(screenUV, rawDepth);
}

float3 ComputeNWRPWorldSpacePosition(float2 screenUV, float rawDepth)
{
    return ComputeSceneWorldSpacePosition(screenUV, rawDepth);
}

#endif // NEWWORLD_DEPTH_WORLD_RECONSTRUCTION_INCLUDED
