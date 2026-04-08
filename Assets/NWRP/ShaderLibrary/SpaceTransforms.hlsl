#ifndef NEWWORLD_SPACE_TRANSFORMS_INCLUDED
#define NEWWORLD_SPACE_TRANSFORMS_INCLUDED

// ============================================================
// NewWorld Render Pipeline - SpaceTransforms.hlsl
//
// 坐标空间变换函数全集。
// 依赖：UnityInput.hlsl 中定义的矩阵变量。
//
// 空间缩写约定：
//   OS  = Object Space  （模型空间）
//   WS  = World Space   （世界空间）
//   VS  = View Space    （观察空间，相机为原点）
//   CS  = Clip Space    （裁剪空间，-w~+w）
//   NDC = Normalized Device Coordinates（-1~+1）
//   SS  = Screen Space  （屏幕像素空间）
//   TS  = Tangent Space （切线空间，用于法线贴图）
// ============================================================

// ============================================================
// ── 顶点位置变换 ─────────────────────────────────────────────
// ============================================================

// Object → World
float3 TransformObjectToWorld(float3 positionOS)
{
    return mul(unity_ObjectToWorld, float4(positionOS, 1.0)).xyz;
}

// World → Object
float3 TransformWorldToObject(float3 positionWS)
{
    return mul(unity_WorldToObject, float4(positionWS, 1.0)).xyz;
}

// World → View
float3 TransformWorldToView(float3 positionWS)
{
    return mul(unity_MatrixV, float4(positionWS, 1.0)).xyz;
}

// Object → View
float3 TransformObjectToView(float3 positionOS)
{
    return TransformWorldToView(TransformObjectToWorld(positionOS));
}

// World → Clip（齐次裁剪空间，SV_POSITION 所需）
float4 TransformWorldToHClip(float3 positionWS)
{
    return mul(unity_MatrixVP, float4(positionWS, 1.0));
}

// Object → Clip（最常用的顶点变换，等价于 MVP * pos）
float4 TransformObjectToHClip(float3 positionOS)
{
    // 先 Object→World，再 World→Clip
    // 展开为: unity_MatrixVP * unity_ObjectToWorld * float4(positionOS, 1)
    return mul(unity_MatrixVP, mul(unity_ObjectToWorld, float4(positionOS, 1.0)));
}

// View → Clip
float4 TransformViewToHClip(float3 positionVS)
{
    return mul(glstate_matrix_projection, float4(positionVS, 1.0));
}

// View → Clip（别名，兼容 URP 命名风格: TransformWViewToHClip）
float4 TransformWViewToHClip(float3 positionVS)
{
    return TransformViewToHClip(positionVS);
}

// 常用矩阵别名（部分 Shader 使用 UNITY_MATRIX_XX 风格）
#define UNITY_MATRIX_VP   unity_MatrixVP
#define UNITY_MATRIX_V    unity_MatrixV
#define UNITY_MATRIX_I_V  unity_MatrixInvV
#define UNITY_MATRIX_P    glstate_matrix_projection

// Clip → NDC（透视除法，结果范围 [-1,1]^3）
float3 TransformHClipToNdc(float4 positionCS)
{
    return positionCS.xyz / positionCS.w;
}

// ============================================================
// ── 方向向量变换（不受平移影响） ─────────────────────────────
// ============================================================

// Object → World（方向）
float3 TransformObjectToWorldDir(float3 dirOS, bool doNormalize = true)
{
    float3 dirWS = mul((float3x3)unity_ObjectToWorld, dirOS);
    return doNormalize ? normalize(dirWS) : dirWS;
}

// World → Object（方向）
float3 TransformWorldToObjectDir(float3 dirWS, bool doNormalize = true)
{
    float3 dirOS = mul((float3x3)unity_WorldToObject, dirWS);
    return doNormalize ? normalize(dirOS) : dirOS;
}

// World → View（方向）
float3 TransformWorldToViewDir(float3 dirWS, bool doNormalize = false)
{
    float3 dirVS = mul((float3x3)unity_MatrixV, dirWS);
    return doNormalize ? normalize(dirVS) : dirVS;
}

// ============================================================
// ── 法线变换（需用逆转置矩阵，防止非均匀缩放变形） ───────────
// ============================================================

// Object → World（法线）
// 法线变换公式：N_ws = (M^-1)^T * N_os
// 由于 unity_WorldToObject = M^-1，其转置 = (M^-1)^T
// 即 N_ws = mul(N_os, unity_WorldToObject) （等价于矩阵转置后乘）
float3 TransformObjectToWorldNormal(float3 normalOS, bool doNormalize = true)
{
    // mul(vec, mat) 等价于 mul(mat^T, vec)，恰好得到 (M^-1)^T * N_os
    float3 normalWS = mul(normalOS, (float3x3)unity_WorldToObject);
    return doNormalize ? normalize(normalWS) : normalWS;
}

// World → Object（法线）
float3 TransformWorldToObjectNormal(float3 normalWS, bool doNormalize = true)
{
    float3 normalOS = mul(normalWS, (float3x3)unity_ObjectToWorld);
    return doNormalize ? normalize(normalOS) : normalOS;
}

// ============================================================
// ── 工具函数（前置声明，供后续函数使用） ─────────────────────
// ============================================================

// 获取奇数负缩放标志（用于副切线叉积方向修正）
// +1 = 正常, -1 = 奇数轴负缩放（需翻转叉积）
float GetOddNegativeScale()
{
    return unity_WorldTransformParams.w;
}

// ============================================================
// ── 切线空间构建与转换 ──────────────────────────────────────
// ============================================================

// 顶点法线输入结构（用于构建 TBN 矩阵）
struct VertexNormalInputs
{
    float3 tangentWS;
    float3 bitangentWS;
    float3 normalWS;
};

// 从 OS 法线和切线构建 WS 下的 TBN 数据
VertexNormalInputs GetVertexNormalInputs(float3 normalOS, float4 tangentOS)
{
    VertexNormalInputs output;
    output.normalWS  = TransformObjectToWorldNormal(normalOS);
    output.tangentWS = TransformObjectToWorldDir(tangentOS.xyz);

    float sign = tangentOS.w * GetOddNegativeScale();
    output.bitangentWS = cross(output.normalWS, output.tangentWS) * sign;
    return output;
}

// World → Tangent（方向向量）
// tbn: 由 tangentWS / bitangentWS / normalWS 构成的 3x3 矩阵
float3 TransformWorldToTangent(float3 dirWS, float3x3 tbn)
{
    // tbn 行 = [T, B, N]，TransformWorldToTangent = tbn * dir
    return mul(tbn, dirWS);
}

// Tangent → World（方向向量）
// tbn: 同上
float3 TransformTangentToWorldDir(float3 dirTS, float3x3 tbn)
{
    // 转置 tbn 即得 World 方向
    return mul(dirTS, tbn);
}

// 从 Object Space TBN 构建 World Space 法线
// tangentOS.w 存储副切线符号（+1 或 -1，由 unity_WorldTransformParams.w 调整）
float3 TransformTangentToWorld(float3 normalTS,
                                float3 normalWS,
                                float3 tangentWS,
                                float  tangentSign)
{
    // 重建副切线（Bitangent）
    // tangentSign 已合并 unity_WorldTransformParams.w 的奇数负缩放修正
    float3 bitangentWS = cross(normalWS, tangentWS) * tangentSign;

    // TBN 矩阵（列向量形式）将 Tangent Space → World Space
    float3x3 TBN = float3x3(tangentWS, bitangentWS, normalWS);
    return normalize(mul(normalTS, TBN));
}

// ============================================================
// ── 深度工具 ──────────────────────────────────────────────────
// ============================================================

// 原始深度值（0~1）→ 线性深度（0=near, 1=far）
// 使用 _ZBufferParams：x = 1-far/near, y = far/near
float Linear01Depth(float depth)
{
    return 1.0 / (_ZBufferParams.x * depth + _ZBufferParams.y);
}

// 原始深度值 → 视空间线性深度（near~far 范围的实际距离）
// 使用 _ZBufferParams：z = (1-far/near)/far, w = (far/near)/far
float LinearEyeDepth(float depth)
{
    return 1.0 / (_ZBufferParams.z * depth + _ZBufferParams.w);
}

// ============================================================
// ── 屏幕空间工具 ──────────────────────────────────────────────
// ============================================================

// 从 SV_POSITION 计算屏幕空间 UV（[0,1] 范围）
float2 GetScreenSpaceUV(float4 positionCS)
{
    // positionCS.xy 已经是像素坐标（由光栅化阶段填充）
    return positionCS.xy * _ScreenParams.zw - _ScreenParams.zw * 0.5 +
           float2(0.5, 0.5) * _ScreenParams.zw;
    // 简化版：直接用 positionCS.xy / _ScreenParams.xy
}

// 简化版：直接除以屏幕分辨率
float2 GetScreenUV(float4 positionCS)
{
    return positionCS.xy / _ScreenParams.xy;
}

// ============================================================
// ── 相机方向工具 ──────────────────────────────────────────────
// ============================================================

// 获取从世界坐标点指向相机的方向向量（归一化）
float3 GetWorldSpaceViewDir(float3 positionWS)
{
    return normalize(_WorldSpaceCameraPos - positionWS);
}

// 获取从世界坐标点指向相机的方向向量（非归一化，用于顶点着色器输出后在片元中归一化）
float3 GetWorldSpaceViewDirRaw(float3 positionWS)
{
    return _WorldSpaceCameraPos - positionWS;
}

// 获取相机在世界空间的正前方向（-Z）
float3 GetCameraForwardDir()
{
    return -normalize(unity_MatrixV[2].xyz);
}

// ============================================================
// ── 工具函数 ──────────────────────────────────────────────────
// ============================================================

// 计算屏幕空间位置（齐次坐标，未做透视除法）
// fragment 中使用 screenPos.xy / screenPos.w 获取 [0,1] UV
// 兼容 D3D (Y翻转) 和 OpenGL
float4 ComputeScreenPos(float4 positionCS)
{
    float4 o = positionCS * 0.5;
    o.xy = float2(o.x, o.y * _ProjectionParams.x) + o.w;
    o.zw = positionCS.zw;
    return o;
}

#endif // NEWWORLD_SPACE_TRANSFORMS_INCLUDED
