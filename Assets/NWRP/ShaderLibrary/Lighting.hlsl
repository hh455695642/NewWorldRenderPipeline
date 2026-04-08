#ifndef NEWWORLD_LIGHTING_INCLUDED
#define NEWWORLD_LIGHTING_INCLUDED

// ============================================================
// NewWorld Render Pipeline - Lighting.hlsl
//
// Layer 0: 纯光源数据访问层，零光照数学。
//
// 提供 Light 结构体和光源访问函数。
// 所有光照计算由上层（BRDF.hlsl / LightingModels / 用户自定义）完成。
//
// 依赖: UnityInput.hlsl (已在 Core.hlsl 中 include)
//       - _MainLightPosition / _MainLightColor
// ============================================================

// 附加光源最大数量（必须与 C# 端 kMaxAdditionalLights 一致）
#ifndef MAX_ADDITIONAL_LIGHTS
#define MAX_ADDITIONAL_LIGHTS 8
#endif

// ------------------------------------------------------------
// Light 结构体
// 统一表示任意类型光源的最小数据集
// ------------------------------------------------------------
struct Light
{
    half3 direction;           // 归一化方向：从表面指向光源
    half3 color;               // 颜色 * 强度（线性空间）
    half  distanceAttenuation; // 距离衰减 [0,1]（方向光 = 1）
    half  shadowAttenuation;   // 阴影衰减 [0,1]（1 = 无阴影）
};

// ------------------------------------------------------------
// 附加光源全局变量（由 CameraRenderer.SetupLights 上传）
// ------------------------------------------------------------
float4 _AdditionalLightsPosition[MAX_ADDITIONAL_LIGHTS];     // xyz=世界坐标, w=1(点/聚光)
half4  _AdditionalLightsColor[MAX_ADDITIONAL_LIGHTS];         // rgb=颜色*强度
half4  _AdditionalLightsAttenuation[MAX_ADDITIONAL_LIGHTS];   // x=1/range^2, zw=spot角度参数
half4  _AdditionalLightsSpotDir[MAX_ADDITIONAL_LIGHTS];       // xyz=聚光方向
int    _AdditionalLightsCount;                                // 当前帧可见附加光源数

// ------------------------------------------------------------
// 主光源
// 数据由 CameraRenderer.SetupLights() 通过 SetGlobalVector 上传
// ------------------------------------------------------------
Light GetMainLight()
{
    Light light;
    light.direction           = half3(_MainLightPosition.xyz); // C# 端已归一化
    light.color               = half3(_MainLightColor.rgb);
    light.distanceAttenuation = 1.0h; // 方向光无距离衰减
    light.shadowAttenuation   = 1.0h; // 阴影待后续 Phase 接入
    return light;
}

// ------------------------------------------------------------
// 附加光源
// index: [0, GetAdditionalLightsCount())
// positionWS: 当前片元的世界坐标（用于计算光源方向和距离）
// ------------------------------------------------------------
int GetAdditionalLightsCount()
{
    return _AdditionalLightsCount;
}

Light GetAdditionalLight(int index, float3 positionWS)
{
    Light light;

    // ── 方向与距离 ────────────────────────────────────────
    float4 lightPos = _AdditionalLightsPosition[index];
    // lightPos.w = 1 (点/聚光) → lightVec = lightPos.xyz - positionWS
    // lightPos.w = 0 (方向光)   → lightVec = lightPos.xyz
    float3 lightVec = lightPos.xyz - positionWS * lightPos.w;
    float distSq = max(dot(lightVec, lightVec), FLT_EPSILON);

    light.direction = half3(lightVec * rsqrt(distSq));
    light.color     = _AdditionalLightsColor[index].rgb;

    // ── 距离衰减（平滑逆平方） ────────────────────────────
    half4 attenData = _AdditionalLightsAttenuation[index];
    half distAtten  = rcp(distSq);                    // 1 / dist^2
    half factor     = half(distSq * attenData.x);     // (dist / range)^2
    half smoothFactor = saturate(1.0h - factor * factor);
    smoothFactor *= smoothFactor;                      // 更平滑的衰减曲线
    light.distanceAttenuation = distAtten * smoothFactor;

    // ── 聚光衰减（角度） ──────────────────────────────────
    half3 spotDir = _AdditionalLightsSpotDir[index].xyz;
    half SdotL    = dot(spotDir, light.direction);
    half spotAtten = saturate(SdotL * attenData.z + attenData.w);
    spotAtten *= spotAtten;                            // 平方使边缘更柔和
    light.distanceAttenuation *= spotAtten;

    // ── 阴影 ─────────────────────────────────────────────
    light.shadowAttenuation = 1.0h; // 阴影待后续 Phase 接入

    return light;
}

#endif // NEWWORLD_LIGHTING_INCLUDED
