#ifndef NEWWORLD_LIGHTING_MODEL_LAMBERT_INCLUDED
#define NEWWORLD_LIGHTING_MODEL_LAMBERT_INCLUDED

// ============================================================
// NewWorld LightingModels/Lambert.hlsl
//
// Layer 3 便捷封装：Lambert 漫反射光照模型。
// 一行调用即可完成单光源 Lambert 计算。
//
// 用法:
//   #include "../../ShaderLibrary/LightingModels/Lambert.hlsl"
//   half3 color = EvaluateLambert(light, normalWS, albedo);
//
// 依赖: Lighting.hlsl, Common.hlsl (INV_PI)
// ============================================================

#include "../Lighting.hlsl"

/// 对单个光源计算能量守恒 Lambert 漫反射
half3 EvaluateLambert(Light light, half3 normalWS, half3 albedo)
{
    half NdotL = saturate(dot(normalWS, light.direction));
    half3 radiance = light.color * light.distanceAttenuation * light.shadowAttenuation * NdotL;
    return albedo * INV_PI * radiance;
}

/// 对所有光源（主光 + 附加光）计算 Lambert 并求和
half3 EvaluateLambertAllLights(half3 normalWS, float3 positionWS, half3 albedo)
{
    half3 color = half3(0, 0, 0);

    // 主光
    Light mainLight = GetMainLight();
    color += EvaluateLambert(mainLight, normalWS, albedo);

    // 附加光
    int count = GetAdditionalLightsCount();
    for (int i = 0; i < count; i++)
    {
        Light addLight = GetAdditionalLight(i, positionWS);
        color += EvaluateLambert(addLight, normalWS, albedo);
    }

    return color;
}

#endif // NEWWORLD_LIGHTING_MODEL_LAMBERT_INCLUDED
