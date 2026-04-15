#ifndef NEWWORLD_SHADOWS_INCLUDED
#define NEWWORLD_SHADOWS_INCLUDED

TEXTURE2D_SHADOW(_MainLightShadowmapTexture);
TEXTURE2D_SHADOW(_MainLightDynamicShadowmapTexture);
SAMPLER_CMP(sampler_LinearClampCompare);

float4x4 _MainLightWorldToShadow[2];
int _MainLightShadowCascadeCount;
half4 _MainLightShadowParams;
half4 _MainLightDynamicShadowParams;
float4 _MainLightShadowmapSize;
float4 _CascadeShadowSplitSpheres0;
float4 _CascadeShadowSplitSpheres1;
float4 _CascadeShadowSplitSphereRadii;

int GetMainLightShadowCascadeIndex(float3 positionWS)
{
    if (_MainLightShadowCascadeCount <= 0)
    {
        return -1;
    }

    float3 toCascade0 = positionWS - _CascadeShadowSplitSpheres0.xyz;
    if (dot(toCascade0, toCascade0) <= _CascadeShadowSplitSphereRadii.x)
    {
        return 0;
    }

    if (_MainLightShadowCascadeCount == 1)
    {
        return -1;
    }

    float3 toCascade1 = positionWS - _CascadeShadowSplitSpheres1.xyz;
    if (dot(toCascade1, toCascade1) <= _CascadeShadowSplitSphereRadii.y)
    {
        return 1;
    }

    return -1;
}

float4 GetMainLightShadowCoord(float3 positionWS, int cascadeIndex)
{
    return mul(_MainLightWorldToShadow[cascadeIndex], float4(positionWS, 1.0));
}

half GetMainLightReceiverShadowFade(float3 positionWS)
{
    float distanceToCamera = distance(positionWS, _WorldSpaceCameraPos.xyz);
    float maxDistance = _MainLightShadowParams.y;
    float invFadeRange = _MainLightShadowParams.z;
    return saturate((maxDistance - distanceToCamera) * invFadeRange);
}

half SampleMainLightShadow(float3 positionWS)
{
    if (_MainLightShadowParams.x <= 0.0h || _MainLightShadowCascadeCount <= 0)
    {
        return 1.0h;
    }

    int cascadeIndex = GetMainLightShadowCascadeIndex(positionWS);
    if (cascadeIndex < 0)
    {
        return 1.0h;
    }

    float4 shadowCoord = GetMainLightShadowCoord(positionWS, cascadeIndex);
    float3 shadowCoordUVW = shadowCoord.xyz;

    if (shadowCoord.x < 0.0 || shadowCoord.x > 1.0 || shadowCoord.y < 0.0 || shadowCoord.y > 1.0)
    {
        return 1.0h;
    }

    if (shadowCoord.z <= 0.0 || shadowCoord.z >= 1.0)
    {
        return 1.0h;
    }

    half visibility = SAMPLE_TEXTURE2D_SHADOW(
        _MainLightShadowmapTexture,
        sampler_LinearClampCompare,
        shadowCoordUVW
    );
    if (_MainLightDynamicShadowParams.x > 0.5h)
    {
        half dynamicVisibility = SAMPLE_TEXTURE2D_SHADOW(
            _MainLightDynamicShadowmapTexture,
            sampler_LinearClampCompare,
            shadowCoordUVW
        );
        visibility = min(visibility, dynamicVisibility);
    }

    half fade = GetMainLightReceiverShadowFade(positionWS);
    half fadedVisibility = lerp(1.0h, visibility, fade);
    return lerp(1.0h, fadedVisibility, _MainLightShadowParams.x);
}

half SampleMainLightShadow(float3 positionWS, float3 lightDirectionWS)
{
    return SampleMainLightShadow(positionWS);
}

half SampleMainLightShadow(float3 positionWS, float3 normalWS, float3 lightDirectionWS)
{
    return SampleMainLightShadow(positionWS);
}

#endif // NEWWORLD_SHADOWS_INCLUDED
