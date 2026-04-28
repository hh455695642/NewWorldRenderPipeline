#ifndef NEWWORLD_UNITY_INPUT_INCLUDED
#define NEWWORLD_UNITY_INPUT_INCLUDED

// ============================================================
// NewWorld Render Pipeline - UnityInput.hlsl
//
// 定义 Unity 引擎通过 SRP 框架自动填充的所有内置 Shader 变量。
//
// 数据来源分两类：
//   1. 全局变量  —— 由 SetupCameraProperties() 每帧写入，
//                  以及 Unity 引擎全局写入（时间、屏幕参数等）
//   2. UnityPerDraw cbuffer —— 由 SRP Batcher 逐 DrawCall 写入
//
// 注意：UnityPerDraw 的字段顺序与大小必须与 Unity 内部期望的
//       布局完全一致，否则 SRP Batcher 将回退到慢速路径。
// ============================================================

// ------------------------------------------------------------
// 每帧 / 每相机全局变量
// 由 ScriptableRenderContext.SetupCameraProperties(camera) 写入
// ------------------------------------------------------------

// View-Projection 矩阵（裁剪 = VP × 世界坐标）
float4x4 unity_MatrixVP;

// View 矩阵（世界 → 观察空间）
float4x4 unity_MatrixV;

// View 逆矩阵（观察 → 世界）
float4x4 unity_MatrixInvV;

// Projection 逆矩阵
float4x4 unity_MatrixInvP;

// View-Projection 逆矩阵（裁剪 → 世界）
float4x4 unity_MatrixInvVP;

// 原始投影矩阵（未经 Unity 平台翻转处理）
float4x4 glstate_matrix_projection;

// 相机世界坐标
float3 _WorldSpaceCameraPos;

// 投影参数
// x = 1 (透视) 或 -1 (需垂直翻转)
// y = near,  z = far,  w = 1/far
float4 _ProjectionParams;

// 屏幕参数
// x = width(px),  y = height(px)
// z = 1 + 1/width, w = 1 + 1/height
float4 _ScreenParams;

// Z-Buffer 重建参数（用于从深度图线性化深度）
// x = 1 - far/near,  y = far/near
// z = x/far,         w = y/far
float4 _ZBufferParams;

// 正交相机参数
// x = 半宽, y = 半高, z = 未使用, w = 0(透视)/1(正交)
float4 unity_OrthoParams;

// 雾效参数（引擎级，由 SetupCameraProperties 自动填充）
// Linear: x=-1/(end-start), y=end/(end-start); Exp/Exp2: x=density
float4 unity_FogParams;
half4  unity_FogColor;

// ------------------------------------------------------------
// 引擎全局时间变量（Unity 自动维护，不需要手动设置）
// ------------------------------------------------------------

// x = t/20,  y = t,  z = t*2,  w = t*3
float4 _Time;

// x = sin(t/8), y = sin(t/4), z = sin(t/2), w = sin(t)
float4 _SinTime;

// x = cos(t/8), y = cos(t/4), z = cos(t/2), w = cos(t)
float4 _CosTime;

// x = deltaTime,  y = 1/deltaTime
// z = smoothDeltaTime,  w = 1/smoothDeltaTime
float4 unity_DeltaTime;

// ------------------------------------------------------------
// Per-Draw（逐对象）cbuffer
// 由 SRP Batcher 在每个 DrawCall 前自动填充
//
// !! 字段顺序和类型必须与 Unity 引擎内部定义严格匹配 !!
// 基于 Unity 2022.3 LTS / SRP Core 14.x 标准布局
// ------------------------------------------------------------
CBUFFER_START(UnityPerDraw)

    // ── 变换矩阵 ──────────────────────────────────────────────
    float4x4 unity_ObjectToWorld;       // 模型矩阵 (Object → World)
    float4x4 unity_WorldToObject;       // 模型逆矩阵 (World → Object)

    // LOD 渐变
    // x = fade value [0,1],  y = 量化后 fade
    float4 unity_LODFade;

    // 世界变换附加参数
    // w = 1 (法线缩放正常) 或 -1 (奇数负缩放，需翻转法线叉积)
    real4 unity_WorldTransformParams;

    // 渲染层级掩码（Rendering Layer Mask）
    real4 unity_RenderingLayer;

    // ── 光照索引（用于 Forward+ 多光源） ────────────────────────
    real4 unity_LightData;
    real4 unity_LightIndices[2];

    // ── 光照探针代理体（LPPV） ───────────────────────────────────
    float4    unity_ProbeVolumeParams;
    float4x4  unity_ProbeVolumeWorldToObject;
    float4    unity_ProbeVolumeSizeInv;
    float4    unity_ProbeVolumeMin;

    // ── 反射探针 ─────────────────────────────────────────────────
    float4 unity_SpecCube0_HDR;
    float4 unity_SpecCube1_HDR;

    float4 unity_SpecCube0_BoxMax;
    float4 unity_SpecCube0_BoxMin;
    float4 unity_SpecCube0_ProbePosition;

    float4 unity_SpecCube1_BoxMax;
    float4 unity_SpecCube1_BoxMin;
    float4 unity_SpecCube1_ProbePosition;

    // ── 光照贴图 UV ──────────────────────────────────────────────
    float4 unity_LightmapST;
    float4 unity_DynamicLightmapST;

    // ── 球谐光照（Ambient / Light Probe SH L2） ──────────────────
    real4 unity_SHAr;   // 红通道 L1 系数
    real4 unity_SHAg;   // 绿通道 L1 系数
    real4 unity_SHAb;   // 蓝通道 L1 系数
    real4 unity_SHBr;   // 红通道 L2 系数
    real4 unity_SHBg;   // 绿通道 L2 系数
    real4 unity_SHBb;   // 蓝通道 L2 系数
    real4 unity_SHC;    // L2 最后一项（RGB）

    // ── 探针遮蔽（Probe Occlusion） ──────────────────────────────
    real4 unity_ProbesOcclusion;

    // ── Renderer AABB（用于裁剪、遮挡） ─────────────────────────
    float4 unity_RendererBounds_Min;
    float4 unity_RendererBounds_Max;

    // ── 运动向量（前一帧变换，用于 TAA / Motion Blur） ───────────
    float4x4 unity_MatrixPreviousM;
    float4x4 unity_MatrixPreviousMI;

CBUFFER_END

#define UNITY_MATRIX_M     unity_ObjectToWorld
#define UNITY_MATRIX_I_M   unity_WorldToObject
#define UNITY_MATRIX_V     unity_MatrixV
#define UNITY_MATRIX_I_V   unity_MatrixInvV
#define UNITY_MATRIX_P     glstate_matrix_projection
#define UNITY_MATRIX_I_P   unity_MatrixInvP
#define UNITY_MATRIX_VP    unity_MatrixVP
#define UNITY_MATRIX_I_VP  unity_MatrixInvVP
#define UNITY_MATRIX_MV    mul(UNITY_MATRIX_V, UNITY_MATRIX_M)
#define UNITY_MATRIX_MVP   mul(UNITY_MATRIX_VP, UNITY_MATRIX_M)
#define UNITY_PREV_MATRIX_M   unity_MatrixPreviousM
#define UNITY_PREV_MATRIX_I_M unity_MatrixPreviousMI

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"

// ------------------------------------------------------------
// 反射探针 Cubemap（引擎级，由 DrawRenderers 自动绑定）
// unity_SpecCube0_HDR 已在 UnityPerDraw cbuffer 中声明
// ------------------------------------------------------------
TEXTURECUBE(unity_SpecCube0);
SAMPLER(samplerunity_SpecCube0);

// ------------------------------------------------------------
// 环境光变量（Unity 根据 Lighting > Environment 设置自动填充）
// 这些是全局变量，不在 UnityPerDraw cbuffer 中
// ------------------------------------------------------------
half4 unity_AmbientSky;        // 天空色 / Color 模式下的环境色
half4 unity_AmbientEquator;    // 赤道色（Gradient 模式）
half4 unity_AmbientGround;     // 地面色（Gradient 模式）

// ------------------------------------------------------------
// 主方向光（由 CameraRenderer.SetupLights() 通过 SetGlobalVector 上传）
// ------------------------------------------------------------
float4 _MainLightPosition;     // xyz = 方向(世界空间，归一化), w = 0(方向光)
float4 _MainLightColor;        // rgb = 颜色*强度(线性空间), a = 未使用

#endif // NEWWORLD_UNITY_INPUT_INCLUDED
