#ifndef NEWWORLD_LIGHTING_MODEL_BLINNPHONG_INCLUDED
#define NEWWORLD_LIGHTING_MODEL_BLINNPHONG_INCLUDED

// ============================================================
// NewWorld LightingModels/BlinnPhong.hlsl
//
// Shared Blinn-Phong helper for lightweight direct lighting.
// This keeps main light and additional light shading aligned with
// the project shadow path without duplicating per-light logic.
// ============================================================

#include "../Lighting.hlsl"
#include "../BRDF.hlsl"

// Evaluate one light using diffuse plus Blinn-Phong specular.
half3 EvaluateBlinnPhong(
    Light light,
    half3 normalWS,
    half3 viewDirWS,
    half3 albedo,
    half3 specColor,
    half shininess)
{
    half3 debugColor;
    if (TryGetMainLightShadowDebugOverride(light, debugColor))
    {
        return debugColor;
    }

    half NdotL = saturate(dot(normalWS, light.direction));
    half3 atten = light.color * light.distanceAttenuation * light.shadowAttenuation;

    half3 diffuse = albedo * atten * NdotL;
    half3 specular = SpecularBlinnPhong(light.direction, normalWS, viewDirWS, specColor, shininess)
        * atten;
    return diffuse + specular;
}

// Evaluate the main light plus all additional lights.
half3 EvaluateBlinnPhongAllLights(
    half3 normalWS,
    half3 viewDirWS,
    float3 positionWS,
    half3 albedo,
    half3 specColor,
    half shininess)
{
    half3 color = half3(0, 0, 0);

    Light mainLight = GetMainLight(positionWS, normalWS);
    half3 debugColor;
    if (TryGetMainLightShadowDebugOverride(mainLight, debugColor))
    {
        return debugColor;
    }

    color += EvaluateBlinnPhong(mainLight, normalWS, viewDirWS, albedo, specColor, shininess);

    int count = GetAdditionalLightsCount();
    for (int i = 0; i < count; i++)
    {
        Light addLight = GetAdditionalLight(i, positionWS, normalWS);
        color += EvaluateBlinnPhong(addLight, normalWS, viewDirWS, albedo, specColor, shininess);
    }

    return color;
}

#endif // NEWWORLD_LIGHTING_MODEL_BLINNPHONG_INCLUDED
