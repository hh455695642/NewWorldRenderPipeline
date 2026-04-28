#ifndef NEWWORLD_SHADERLIB_PASS_DEPTH_ONLY_INCLUDED
#define NEWWORLD_SHADERLIB_PASS_DEPTH_ONLY_INCLUDED

#include "../Core.hlsl"

struct DepthOnlyAttributes
{
    float4 positionOS : POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct DepthOnlyVaryings
{
    float4 positionCS : SV_POSITION;
};

DepthOnlyVaryings DepthOnlyVert(DepthOnlyAttributes input)
{
    UNITY_SETUP_INSTANCE_ID(input);

    DepthOnlyVaryings output;
    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
    output.positionCS = TransformWorldToHClip(positionWS);
    return output;
}

half4 DepthOnlyFrag(DepthOnlyVaryings input) : SV_Target
{
    return 0;
}

#endif // NEWWORLD_SHADERLIB_PASS_DEPTH_ONLY_INCLUDED
