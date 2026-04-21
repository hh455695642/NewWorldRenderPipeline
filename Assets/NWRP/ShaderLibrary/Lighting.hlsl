#ifndef NEWWORLD_LIGHTING_INCLUDED
#define NEWWORLD_LIGHTING_INCLUDED

#include "Shadows.hlsl"

#ifndef MAX_ADDITIONAL_LIGHTS
#define MAX_ADDITIONAL_LIGHTS 8
#endif

struct Light
{
    half3 direction;
    half3 color;
    half distanceAttenuation;
    half shadowAttenuation;
    half3 shadowDebugColor;
    half shadowDebugActive;
};

float4 _AdditionalLightsPosition[MAX_ADDITIONAL_LIGHTS];
half4 _AdditionalLightsColor[MAX_ADDITIONAL_LIGHTS];
half4 _AdditionalLightsAttenuation[MAX_ADDITIONAL_LIGHTS];
half4 _AdditionalLightsSpotDir[MAX_ADDITIONAL_LIGHTS];
int _AdditionalLightsCount;

void InitializeLightDebugData(inout Light light)
{
    light.shadowDebugColor = 0.0h.xxx;
    light.shadowDebugActive = 0.0h;
}

bool TryGetMainLightShadowDebugOverride(Light light, out half3 debugColor)
{
    debugColor = light.shadowDebugColor;
    return light.shadowDebugActive > 0.5h;
}

void ApplyMainLightShadowDebugData(inout Light light, MainLightShadowResult shadowResult)
{
    if (!IsMainLightShadowSourceTintDebugEnabled())
    {
        return;
    }
}

Light GetMainLight()
{
    Light light;
    light.direction = half3(_MainLightPosition.xyz);
    light.color = half3(_MainLightColor.rgb);
    light.distanceAttenuation = 1.0h;
    light.shadowAttenuation = 1.0h;
    InitializeLightDebugData(light);
    return light;
}

Light GetMainLight(float3 positionWS)
{
    Light light = GetMainLight();
    MainLightShadowResult shadowResult = GetMainLightShadowResult(positionWS, light.direction);
    light.shadowAttenuation = shadowResult.finalVisibility;
    ApplyMainLightShadowDebugData(light, shadowResult);
    return light;
}

Light GetMainLight(float3 positionWS, float3 normalWS)
{
    Light light = GetMainLight();
    MainLightShadowResult shadowResult = GetMainLightShadowResult(positionWS, normalWS, light.direction);
    light.shadowAttenuation = shadowResult.finalVisibility;
    ApplyMainLightShadowDebugData(light, shadowResult);
    return light;
}

int GetAdditionalLightsCount()
{
    return _AdditionalLightsCount;
}

Light GetAdditionalLight(int index, float3 positionWS)
{
    Light light;

    float4 lightPosition = _AdditionalLightsPosition[index];
    float3 lightVector = lightPosition.xyz - positionWS * lightPosition.w;
    float distanceSquared = max(dot(lightVector, lightVector), FLT_EPSILON);

    light.direction = half3(lightVector * rsqrt(distanceSquared));
    light.color = _AdditionalLightsColor[index].rgb;

    half4 attenuationData = _AdditionalLightsAttenuation[index];
    half distanceAttenuation = rcp(distanceSquared);
    half distanceFactor = half(distanceSquared * attenuationData.x);
    half smoothFactor = saturate(1.0h - distanceFactor * distanceFactor);
    smoothFactor *= smoothFactor;
    light.distanceAttenuation = distanceAttenuation * smoothFactor;

    half3 spotDirection = _AdditionalLightsSpotDir[index].xyz;
    half spotDot = dot(spotDirection, light.direction);
    half spotAttenuation = saturate(spotDot * attenuationData.z + attenuationData.w);
    spotAttenuation *= spotAttenuation;
    light.distanceAttenuation *= spotAttenuation;

    light.shadowAttenuation = 1.0h;
    InitializeLightDebugData(light);
    return light;
}

#endif // NEWWORLD_LIGHTING_INCLUDED
