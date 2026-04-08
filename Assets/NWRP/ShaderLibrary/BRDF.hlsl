#ifndef NEWWORLD_BRDF_INCLUDED
#define NEWWORLD_BRDF_INCLUDED

// ============================================================
// NewWorld Render Pipeline - BRDF.hlsl
//
// Layer 1: BRDF 积木函数库。
//
// 所有函数只接收标量/向量参数，不强制任何结构体。
// Shader 作者可自由组合这些积木，也可完全不用、手写光照。
//
// 分类：
//   1. 漫反射项（Diffuse）
//   2. 镜面反射项（Specular）
//   3. 菲涅尔项（Fresnel）
//   4. PBR 微表面项（D / V / F）
//   5. 金属工作流辅助
//   6. 粗糙度转换
//
// 依赖: Common.hlsl (PI, INV_PI, SafeNormalize, Pow5, FLT_EPSILON)
// ============================================================


// ************************************************************
// 1. 漫反射项 (Diffuse)
// ************************************************************

// Lambert 漫反射（能量守恒版本 / PI）
half3 DiffuseLambert(half3 albedo)
{
    return albedo * INV_PI;
}

// Half-Lambert（Valve 半兰伯特）
// 将 NdotL 从 [0,1] 映射到 [0.5, 1] 再平方，背光面更亮
half DiffuseHalfLambert(half NdotL)
{
    return pow(NdotL * 0.5 + 0.5, 2.0);
}

// Disney / Burley 漫反射（考虑粗糙度的能量补偿）
half3 DiffuseBurley(half NdotL, half NdotV, half LdotH, half perceptualRoughness, half3 albedo)
{
    half fd90 = 0.5 + 2.0 * LdotH * LdotH * perceptualRoughness;
    half lightScatter = 1.0 + (fd90 - 1.0) * Pow5(1.0 - NdotL);
    half viewScatter  = 1.0 + (fd90 - 1.0) * Pow5(1.0 - NdotV);
    return albedo * INV_PI * lightScatter * viewScatter;
}


// ************************************************************
// 2. 镜面反射项 (Specular)
// ************************************************************

// Phong 镜面反射
// reflect(-L, N) dot V，指数 = shininess
half3 SpecularPhong(half3 lightDir, half3 normal, half3 viewDir,
                    half3 specColor, half shininess)
{
    half3 reflectDir = reflect(-lightDir, normal);
    half RdotV = saturate(dot(reflectDir, viewDir));
    half spec = pow(RdotV, shininess);
    return specColor * spec;
}

// Blinn-Phong 镜面反射
// halfDir = normalize(L + V), dot(N, H)，指数 = shininess
half3 SpecularBlinnPhong(half3 lightDir, half3 normal, half3 viewDir,
                         half3 specColor, half shininess)
{
    half3 halfDir = SafeNormalize(float3(lightDir) + float3(viewDir));
    half NdotH = saturate(dot(normal, halfDir));
    half spec = pow(NdotH, shininess);
    return specColor * spec;
}


// ************************************************************
// 3. 菲涅尔项 (Fresnel)
// ************************************************************

// Schlick 菲涅尔近似
half3 F_Schlick(half3 f0, half cosTheta)
{
    return f0 + (1.0 - f0) * Pow5(1.0 - cosTheta);
}

// Schlick 菲涅尔（考虑粗糙度，用于 IBL 环境 BRDF）
half3 F_SchlickRoughness(half3 f0, half cosTheta, half perceptualRoughness)
{
    half smoothness = 1.0 - perceptualRoughness;
    return f0 + (max(smoothness.xxx, f0) - f0) * Pow5(1.0 - cosTheta);
}


// ************************************************************
// 4. PBR 微表面项 (D / V)
// ************************************************************

// GGX / Trowbridge-Reitz 法线分布函数 (NDF)
// roughness 为线性粗糙度（即 perceptualRoughness^2）
half D_GGX(half NdotH, half roughness)
{
    half a2 = roughness * roughness;
    half d = NdotH * NdotH * (a2 - 1.0) + 1.0;
    return a2 * INV_PI / (d * d + FLT_EPSILON);
}

// Smith-Joint 可见性函数近似（合并了 G 项和 4*NdotL*NdotV 分母）
// 来源: Filament / Google, 性能与质量平衡好
half V_SmithJointApprox(half NdotL, half NdotV, half roughness)
{
    half a = roughness;
    half lambdaV = NdotL * (NdotV * (1.0 - a) + a);
    half lambdaL = NdotV * (NdotL * (1.0 - a) + a);
    return 0.5 / (lambdaV + lambdaL + FLT_EPSILON);
}

// Unity URP 风格的 V*F 合并近似（更简化，移动端友好）
// V * F ≈ specColor / (LoH^2 * (roughness + 0.5))
half3 VF_Approximate(half LdotH, half roughness, half3 specColor)
{
    half denom = max(0.1h, LdotH * LdotH * (roughness + 0.5));
    return specColor / denom;
}


// ************************************************************
// 5. 金属工作流辅助 (Metallic Workflow)
// ************************************************************

// 电介质基础反射率 = 0.04（大多数非金属材质）
#define kDielectricF0 half3(0.04, 0.04, 0.04)

// 金属度 → 漫反射保留比例
// 金属表面完全吸收漫反射，仅保留镜面；电介质保留 96% 漫反射
half OneMinusReflectivityMetallic(half metallic)
{
    // 保留范围 [0.04, 1] 的电介质反射率
    half oneMinusDielectricSpec = 1.0 - 0.04; // = 0.96
    return oneMinusDielectricSpec * (1.0 - metallic);
}

// 计算 F0（基础反射率）
// 电介质 = 0.04，金属 = albedo
half3 ComputeF0(half3 albedo, half metallic)
{
    return lerp(kDielectricF0, albedo, metallic);
}

// 计算漫反射颜色
// 金属表面漫反射为 0，电介质保留大部分 albedo
half3 ComputeDiffuseColor(half3 albedo, half metallic)
{
    return albedo * OneMinusReflectivityMetallic(metallic);
}


// ************************************************************
// 6. 粗糙度转换 (Roughness Conversion)
// ************************************************************

// 感知粗糙度 → 线性粗糙度
// 感知粗糙度是美术友好的 [0,1] 范围，线性粗糙度 = 感知^2
half PerceptualRoughnessToRoughness(half perceptualRoughness)
{
    return perceptualRoughness * perceptualRoughness;
}

// 线性粗糙度 → 感知粗糙度
half RoughnessToPerceptualRoughness(half roughness)
{
    return sqrt(roughness);
}

// 感知粗糙度 → Cubemap mip 级别（近似）
// 通常反射探针有 6-7 级 mip，perceptualRoughness=1 对应最模糊级
half PerceptualRoughnessToMipmapLevel(half perceptualRoughness)
{
    return perceptualRoughness * 6.0;
}

// Smoothness → 感知粗糙度（常见美术接口转换）
half SmoothnessToPerceptualRoughness(half smoothness)
{
    return 1.0 - smoothness;
}

#endif // NEWWORLD_BRDF_INCLUDED
