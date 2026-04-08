#ifndef NEWWORLD_GLOBAL_ILLUMINATION_INCLUDED
#define NEWWORLD_GLOBAL_ILLUMINATION_INCLUDED

// ============================================================
// NewWorld Render Pipeline - GlobalIllumination.hlsl
//
// Layer 2: 全局光照（间接光）—— SH 球谐 + 反射探针。
//
// SH（Spherical Harmonics）用于间接漫反射：
//   Unity 将环境光/Light Probe 数据编码到 L2 球谐系数中，
//   无论环境光模式是 Color / Gradient / Skybox 都能正确工作。
//
// 反射探针用于间接镜面反射：
//   从 unity_SpecCube0 Cubemap 按粗糙度采样不同 mip 级别。
//
// 依赖:
//   - UnityInput.hlsl (unity_SHA*, unity_SHB*, unity_SHC,
//                      unity_SpecCube0, unity_SpecCube0_HDR,
//                      unity_AmbientSky/Equator/Ground)
//   - Common.hlsl     (DecodeHDREnvironment, SAMPLE_TEXTURECUBE_LOD)
//   - BRDF.hlsl       (PerceptualRoughnessToMipmapLevel) - 可选
// ============================================================


// ************************************************************
// 1. 球谐光照 (Spherical Harmonics)
// ************************************************************

/// 完整 SH L2 求值
/// 适用于所有环境光模式（Color / Gradient / Skybox / Light Probe）
/// Unity 自动将环境光数据编码到 SH 系数中
half3 SampleSH(half3 normalWS)
{
    // L0 + L1（4 个基函数）
    half4 n = half4(normalWS, 1.0);
    half3 res;
    res.r = dot(unity_SHAr, n);
    res.g = dot(unity_SHAg, n);
    res.b = dot(unity_SHAb, n);

    // L2（5 个基函数）
    half4 vB = normalWS.xyzz * normalWS.yzzx;
    res.r += dot(unity_SHBr, vB);
    res.g += dot(unity_SHBg, vB);
    res.b += dot(unity_SHBb, vB);

    half vC = normalWS.x * normalWS.x - normalWS.y * normalWS.y;
    res += unity_SHC.rgb * vC;

    return max(0.0, res);
}

/// 仅 SH L0（常数项），最快但最粗糙
/// 等同于 Color 模式下的环境光单色
half3 SampleSH_L0()
{
    return half3(unity_SHAr.w, unity_SHAg.w, unity_SHAb.w);
}

/// 三色渐变环境光（仅 Gradient 模式视觉正确，其他模式建议用 SampleSH）
half3 SampleGradientAmbient(half3 normalWS)
{
    half3 ambient = lerp(unity_AmbientEquator.rgb, unity_AmbientSky.rgb, saturate(normalWS.y));
    ambient = lerp(ambient, unity_AmbientGround.rgb, saturate(-normalWS.y));
    return ambient;
}


// ************************************************************
// 2. 反射探针 (Reflection Probe)
// ************************************************************

/// 按反射方向和粗糙度采样反射探针 Cubemap
/// reflectDir: 世界空间反射方向（已归一化）
/// perceptualRoughness: 感知粗糙度 [0, 1]
half3 SampleReflectionProbe(half3 reflectDir, half perceptualRoughness)
{
    // 粗糙度映射到 mip 级别（反射探针通常有 7 级 mip）
    half mip = perceptualRoughness * 6.0;
    half4 encoded = SAMPLE_TEXTURECUBE_LOD(
        unity_SpecCube0, samplerunity_SpecCube0, reflectDir, mip
    );
    return DecodeHDREnvironment(encoded, unity_SpecCube0_HDR);
}

/// 便捷封装：从法线和视线方向自动计算反射方向并采样
half3 SampleEnvironmentReflection(half3 normalWS, half3 viewDirWS, half perceptualRoughness)
{
    half3 reflectDir = reflect(-viewDirWS, normalWS);
    return SampleReflectionProbe(reflectDir, perceptualRoughness);
}


#endif // NEWWORLD_GLOBAL_ILLUMINATION_INCLUDED
