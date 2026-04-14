#ifndef NEWWORLD_SHADERLIB_PASS_SHADOW_CASTER_INCLUDED
#define NEWWORLD_SHADERLIB_PASS_SHADOW_CASTER_INCLUDED

#include "../Core.hlsl"

struct ShadowCasterAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
};

struct ShadowCasterVaryings
{
    float4 positionCS : SV_POSITION;
};

float4 _ShadowBias;
float4 _ShadowLightDirection;

float3 ApplyShadowBias(float3 positionWS, float3 normalWS, float3 lightDirectionWS)
{
    float invNdotL = 1.0 - saturate(dot(lightDirectionWS, normalWS));
    float normalBiasScale = invNdotL * _ShadowBias.y;

    positionWS += lightDirectionWS * _ShadowBias.x;
    positionWS += normalWS * normalBiasScale;
    return positionWS;
}

ShadowCasterVaryings ShadowCasterVert(ShadowCasterAttributes input)
{
    ShadowCasterVaryings output;
    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
    float3 lightDirectionWS = normalize(_ShadowLightDirection.xyz);
    float3 biasedPositionWS = ApplyShadowBias(positionWS, normalWS, lightDirectionWS);
    output.positionCS = TransformWorldToHClip(biasedPositionWS);

#if UNITY_REVERSED_Z
    output.positionCS.z = min(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
#else
    output.positionCS.z = max(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
#endif

    return output;
}

half4 ShadowCasterFrag(ShadowCasterVaryings input) : SV_Target
{
    return 0;
}

#endif // NEWWORLD_SHADERLIB_PASS_SHADOW_CASTER_INCLUDED
