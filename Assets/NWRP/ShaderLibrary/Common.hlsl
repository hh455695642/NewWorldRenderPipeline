#ifndef NEWWORLD_COMMON_INCLUDED
#define NEWWORLD_COMMON_INCLUDED

// ============================================================
// NewWorld Render Pipeline - Common.hlsl
// 基础类型定义、宏、数学常量与工具函数
// ============================================================

// ------------------------------------------------------------
// 精度别名
// 桌面端 real = float；如需移动端半精度，将 float 改为 half
// ------------------------------------------------------------
#define real        float
#define real2       float2
#define real3       float3
#define real4       float4
#define real2x2     float2x2
#define real3x3     float3x3
#define real4x4     float4x4
#define real3x4     float3x4

// ------------------------------------------------------------
// 数学常量
// ------------------------------------------------------------
#define PI              3.14159265358979323846
#define TWO_PI          6.28318530717958647693
#define FOUR_PI         12.5663706143591729538
#define HALF_PI         1.57079632679489661923
#define INV_PI          0.31830988618379067154
#define INV_TWO_PI      0.15915494309189533577
#define INV_FOUR_PI     0.07957747154594766788
#define SQRT2           1.41421356237309504880
#define INV_SQRT2       0.70710678118654752440
#define FLT_EPSILON     1.192092896e-07
#define FLT_MAX         3.402823466e+38
#define FLT_MIN         1.175494351e-38
#define HALF_MAX        65504.0
#define HALF_MIN        6.103515625e-05
#define HALF_MIN_SQRT   0.0078125       // sqrt(HALF_MIN)

#ifndef UNITY_NEAR_CLIP_VALUE
    #if UNITY_REVERSED_Z
        #define UNITY_NEAR_CLIP_VALUE (1.0)
    #else
        #define UNITY_NEAR_CLIP_VALUE (-1.0)
    #endif
#endif

// ------------------------------------------------------------
// SRP Batcher 兼容的 CBUFFER 宏
// SRP Batcher 要求材质属性放在 CBUFFER_START(UnityPerMaterial) 中
// ------------------------------------------------------------
#define CBUFFER_START(name) cbuffer name {
#define CBUFFER_END         };

// ------------------------------------------------------------
// 纹理 & 采样器声明宏
// 将平台差异封装为统一接口
// ------------------------------------------------------------
#define TEXTURE2D(textureName)                          Texture2D textureName
#define TEXTURE2D_ARRAY(textureName)                    Texture2DArray textureName
#define TEXTURECUBE(textureName)                        TextureCube textureName
#define TEXTURECUBE_ARRAY(textureName)                  TextureCubeArray textureName
#define TEXTURE3D(textureName)                          Texture3D textureName
#define TEXTURE2D_SHADOW(textureName)                   Texture2D textureName

#define SAMPLER(samplerName)                            SamplerState samplerName
#define SAMPLER_CMP(samplerName)                        SamplerComparisonState samplerName

// 采样宏
#define SAMPLE_TEXTURE2D(tex, samp, uv)                 tex.Sample(samp, uv)
#define SAMPLE_TEXTURE2D_LOD(tex, samp, uv, lod)        tex.SampleLevel(samp, uv, lod)
#define SAMPLE_TEXTURE2D_BIAS(tex, samp, uv, bias)      tex.SampleBias(samp, uv, bias)
#define SAMPLE_TEXTURE2D_GRAD(tex,samp,uv,ddx,ddy)     tex.SampleGrad(samp, uv, ddx, ddy)
#define SAMPLE_TEXTURECUBE(tex, samp, dir)              tex.Sample(samp, dir)
#define SAMPLE_TEXTURECUBE_LOD(tex, samp, dir, lod)     tex.SampleLevel(samp, dir, lod)
#define SAMPLE_TEXTURE3D(tex, samp, uvw)                tex.Sample(samp, uvw)

// 深度纹理采样（SamplerComparisonState）
#define SAMPLE_TEXTURE2D_SHADOW(tex, samp, uv3)         tex.SampleCmpLevelZero(samp, uv3.xy, uv3.z)

// Load（直接按像素坐标读取，不走采样器）
#define LOAD_TEXTURE2D(tex, coord2)                     tex.Load(int3(coord2, 0))
#define LOAD_TEXTURE2D_LOD(tex, coord2, lod)            tex.Load(int3(coord2, lod))

// ------------------------------------------------------------
// UV 变换宏（纹理 Tiling + Offset）
// 使用前需在 CBUFFER 中声明 float4 name##_ST
// ------------------------------------------------------------
#define TRANSFORM_TEX(uv, name) ((uv) * name##_ST.xy + name##_ST.zw)

// ------------------------------------------------------------
// 通用数学工具函数
// ------------------------------------------------------------
float  Pow2(float x)  { return x * x; }
float2 Pow2(float2 x) { return x * x; }
float  Pow4(float x)  { float x2 = x * x; return x2 * x2; }
float  Pow5(float x)  { float x2 = x * x; return x2 * x2 * x; }

float  Max3(float  a, float  b, float  c) { return max(a, max(b, c)); }
float2 Max3(float2 a, float2 b, float2 c) { return max(a, max(b, c)); }
float3 Max3(float3 a, float3 b, float3 c) { return max(a, max(b, c)); }
float  Min3(float  a, float  b, float  c) { return min(a, min(b, c)); }

// 安全归一化（避免零向量导致 NaN）
float3 SafeNormalize(float3 v)
{
    float lenSq = dot(v, v);
    return v * rsqrt(max(lenSq, FLT_EPSILON));
}

// 安全 rsqrt（防止除以0）
float SafeRsqrt(float x)
{
    return rsqrt(max(x, FLT_EPSILON));
}

// 将值从 [a, b] 重映射到 [c, d]
float Remap(float value, float fromMin, float fromMax, float toMin, float toMax)
{
    return toMin + (value - fromMin) / (fromMax - fromMin) * (toMax - toMin);
}

// 平滑阶跃（smoothstep 的更平滑版本：6t^5 - 15t^4 + 10t^3）
float SmootherStep(float edge0, float edge1, float x)
{
    x = saturate((x - edge0) / (edge1 - edge0));
    return x * x * x * (x * (x * 6.0 - 15.0) + 10.0);
}

// 将 NDC 坐标 [-1,1] 转换为 UV [0,1]
float2 NdcToUv(float2 ndc)
{
    return ndc * 0.5 + 0.5;
}

// 将 UV [0,1] 转换为 NDC [-1,1]
float2 UvToNdc(float2 uv)
{
    return uv * 2.0 - 1.0;
}

// 色彩空间转换
float3 LinearToGamma(float3 linearColor)
{
    return pow(max(linearColor, 0.0), 1.0 / 2.2);
}

float3 GammaToLinear(float3 gammaColor)
{
    return pow(max(gammaColor, 0.0), 2.2);
}

// 亮度（感知加权）
float Luminance(float3 linearColor)
{
    return dot(linearColor, float3(0.2126729, 0.7151522, 0.0721750));
}

// 色调映射（Reinhard）
float3 ReinhardTonemap(float3 color)
{
    return color / (color + 1.0);
}

// ------------------------------------------------------------
// HDR 环境解码（反射探针 RGBM 编码）
// encodedIrradiance: SAMPLE_TEXTURECUBE 返回的 RGBM 值
// decodeInstructions: unity_SpecCube0_HDR (x=乘数, y=指数)
// ------------------------------------------------------------
half3 DecodeHDREnvironment(half4 encodedIrradiance, half4 decodeInstructions)
{
    half alpha = encodedIrradiance.a;
    return encodedIrradiance.rgb * (decodeInstructions.x * pow(abs(alpha), decodeInstructions.y));
}

#endif // NEWWORLD_COMMON_INCLUDED
