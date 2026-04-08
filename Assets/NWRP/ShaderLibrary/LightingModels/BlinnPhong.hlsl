#ifndef NEWWORLD_LIGHTING_MODEL_BLINNPHONG_INCLUDED
#define NEWWORLD_LIGHTING_MODEL_BLINNPHONG_INCLUDED

// ============================================================
// NewWorld LightingModels/BlinnPhong.hlsl
//
// Layer 3 便捷封装：Blinn-Phong 光照模型（漫反射 + 镜面反射）。
//
// 用法:
//   #include "../../ShaderLibrary/LightingModels/BlinnPhong.hlsl"
//   half3 color = EvaluateBlinnPhong(light, normalWS, viewDirWS,
//                                     albedo, specColor, shininess);
//
// 依赖: Lighting.hlsl, BRDF.hlsl (SpecularBlinnPhong)
// ============================================================

#include "../Lighting.hlsl"
#include "../BRDF.hlsl"

/// 对单个光源计算 Blinn-Phong（漫反射 + 镜面反射）
half3 EvaluateBlinnPhong(Light light, half3 normalWS, half3 viewDirWS,
                         half3 albedo, half3 specColor, half shininess)
{
    half NdotL = saturate(dot(normalWS, light.direction));
    half3 atten = light.color * light.distanceAttenuation * light.shadowAttenuation;

    half3 diffuse  = albedo * atten * NdotL;
    half3 specular = SpecularBlinnPhong(light.direction, normalWS, viewDirWS,
                                        specColor, shininess) * atten;
    return diffuse + specular;
}

/// 对所有光源（主光 + 附加光）计算 Blinn-Phong 并求和
half3 EvaluateBlinnPhongAllLights(half3 normalWS, half3 viewDirWS, float3 positionWS,
                                   half3 albedo, half3 specColor, half shininess)
{
    half3 color = half3(0, 0, 0);

    Light mainLight = GetMainLight();
    color += EvaluateBlinnPhong(mainLight, normalWS, viewDirWS, albedo, specColor, shininess);

    int count = GetAdditionalLightsCount();
    for (int i = 0; i < count; i++)
    {
        Light addLight = GetAdditionalLight(i, positionWS);
        color += EvaluateBlinnPhong(addLight, normalWS, viewDirWS, albedo, specColor, shininess);
    }

    return color;
}

#endif // NEWWORLD_LIGHTING_MODEL_BLINNPHONG_INCLUDED
