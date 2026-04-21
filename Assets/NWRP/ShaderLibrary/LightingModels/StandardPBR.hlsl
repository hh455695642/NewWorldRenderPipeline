#ifndef NEWWORLD_LIGHTING_MODEL_STANDARD_PBR_INCLUDED
#define NEWWORLD_LIGHTING_MODEL_STANDARD_PBR_INCLUDED

// ============================================================
// NewWorld LightingModels/StandardPBR.hlsl
//
// Layer 3 便捷封装：标准 PBR（金属工作流）。
//
// 组合 Lighting.hlsl + BRDF.hlsl + GlobalIllumination.hlsl，
// 提供一站式 PBR 计算。这是可选的便捷层——
// 不想用的 Shader 可以直接从 BRDF.hlsl 中挑选积木自己组装。
//
// 光照模型:
//   - 直接光 = Cook-Torrance (D_GGX + V_SmithJoint + F_Schlick)
//   - 间接漫反射 = SH 球谐
//   - 间接镜面反射 = 反射探针 + 环境 BRDF
//
// 用法:
//   #include "../../ShaderLibrary/LightingModels/StandardPBR.hlsl"
//   half3 color = EvaluateStandardPBR(normalWS, viewDirWS, positionWS,
//                                      albedo, metallic, perceptualRoughness);
//
// 依赖: Lighting.hlsl, BRDF.hlsl, GlobalIllumination.hlsl
// ============================================================

#include "../Lighting.hlsl"
#include "../BRDF.hlsl"
#include "../GlobalIllumination.hlsl"


// ------------------------------------------------------------
// 单光源直接 PBR（Cook-Torrance BRDF）
// ------------------------------------------------------------
half3 DirectBRDF_StandardPBR(
    Light  light,
    half3  normalWS,
    half3  viewDirWS,
    half3  albedo,
    half   metallic,
    half   perceptualRoughness)
{
    half3 debugColor;
    if (TryGetMainLightShadowDebugOverride(light, debugColor))
    {
        return debugColor;
    }

    half roughness = PerceptualRoughnessToRoughness(perceptualRoughness);
    roughness = max(roughness, HALF_MIN_SQRT); // 防止零粗糙度导致除零

    half3 H = SafeNormalize(float3(light.direction) + float3(viewDirWS));

    half NdotL = saturate(dot(normalWS, light.direction));
    half NdotH = saturate(dot(normalWS, H));
    half NdotV = saturate(dot(normalWS, viewDirWS));
    half LdotH = saturate(dot(light.direction, H));

    // 金属工作流参数
    half3 f0 = ComputeF0(albedo, metallic);
    half3 diffuseColor = ComputeDiffuseColor(albedo, metallic);

    // Cook-Torrance 镜面反射: D * V * F
    half  D = D_GGX(NdotH, roughness);
    half  V = V_SmithJointApprox(NdotL, NdotV, roughness);
    half3 F = F_Schlick(f0, LdotH);

    half3 specular = D * V * F;

    // 能量守恒漫反射
    half3 diffuse = diffuseColor * INV_PI;

    // 辐照度（光源颜色 × 衰减 × cos θ）
    half3 radiance = light.color
                   * light.distanceAttenuation
                   * light.shadowAttenuation
                   * NdotL;

    return (diffuse + specular) * radiance;
}


// ------------------------------------------------------------
// 完整 PBR：直接光（所有光源） + 间接光（SH + 反射探针）
// 一站式调用，覆盖最常见的金属工作流 PBR 需求
// ------------------------------------------------------------
half3 EvaluateStandardPBR(
    half3  normalWS,
    half3  viewDirWS,
    float3 positionWS,
    half3  albedo,
    half   metallic,
    half   perceptualRoughness)
{
    half3 color = half3(0, 0, 0);

    // ── 直接光：主光源 ────────────────────────────────────
    Light mainLight = GetMainLight(positionWS, normalWS);
    half3 debugColor;
    if (TryGetMainLightShadowDebugOverride(mainLight, debugColor))
    {
        return debugColor;
    }

    color += DirectBRDF_StandardPBR(mainLight, normalWS, viewDirWS,
                                     albedo, metallic, perceptualRoughness);

    // ── 直接光：附加光源 ──────────────────────────────────
    int count = GetAdditionalLightsCount();
    for (int i = 0; i < count; i++)
    {
        Light addLight = GetAdditionalLight(i, positionWS);
        color += DirectBRDF_StandardPBR(addLight, normalWS, viewDirWS,
                                         albedo, metallic, perceptualRoughness);
    }

    // ── 间接漫反射（SH 球谐） ────────────────────────────
    half3 indirectDiffuse = SampleSH(normalWS) * ComputeDiffuseColor(albedo, metallic);

    // ── 间接镜面反射（反射探针） ──────────────────────────
    half3 f0 = ComputeF0(albedo, metallic);
    half NdotV = saturate(dot(normalWS, viewDirWS));
    half3 envBRDF = F_SchlickRoughness(f0, NdotV, perceptualRoughness);
    half3 indirectSpecular = SampleEnvironmentReflection(normalWS, viewDirWS, perceptualRoughness)
                           * envBRDF;

    color += indirectDiffuse + indirectSpecular;
    return color;
}


#endif // NEWWORLD_LIGHTING_MODEL_STANDARD_PBR_INCLUDED
